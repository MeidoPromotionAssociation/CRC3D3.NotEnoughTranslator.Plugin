using System;
using System.Globalization;
using System.Threading;
using CRC.UI;
using CRC3D3.NotEnoughTranslator.Plugin.Manger;
using CRC3D3.NotEnoughTranslator.Plugin.Translation;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;
using HarmonyLib;
using Script;
using AdvUtility = AdvSystem.Utility;

namespace CRC3D3.NotEnoughTranslator.Plugin.Hooks.Text;

/// <summary>
/// 通用文本显示补丁集合：在游戏的具体显示入口把原文交给 Text 词典。
/// 由 <see cref="TextTranslateManager"/> 通过 CreateAndPatchAll 一次性安装。
/// General-text display patches that hand source strings to the Text dictionary at each display entry.
/// Installed in one go by <see cref="TextTranslateManager"/> through CreateAndPatchAll.
/// </summary>
internal static class TextTranslatePatch
{
    /// <summary>
    /// 通用文本显示补丁的独立 Harmony ID。
    /// The dedicated Harmony ID for general-text display patches.
    /// </summary>
    public const string HarmonyId =
        "github.meidopromotionassociation.crc3d3.notenoughtranslator.plugin.hooks.text.texttranslatepatch";

    private static int _failureWarningLogged;
    private static int _contextWarningLogged;
    private static readonly Type[] TagStringParameterTypes = { typeof(string), typeof(string).MakeByRefType() };

    [ThreadStatic] private static ScriptScope _scriptScope;

    private struct ScriptScope
    {
        public ScriptTag Tag;
        public KagTextContext Kag;
    }

    private enum ScriptTag
    {
        None,
        Dialogue,
        Choice,
        ChoiceMiss,
        ChoiceHit
    }

    /// <summary>
    /// TMP 显示补丁缺失时使用系统消息队列入口查询通用词库。
    /// Look up system messages at the queue entry when the TMP display hook is unavailable.
    /// </summary>
    /// <param name="__0">按位置绑定的消息原文，游戏改名不影响补丁 / The message text bound by position, unaffected by game-side parameter renames.</param>
    [HarmonyPatch(typeof(SystemMessagesManager), nameof(SystemMessagesManager.ShowSystemMessage), typeof(string))]
    [HarmonyPrefix]
    private static void SystemMessagePrefix(ref string __0)
    {
        if (!UiTextDisplayPatch.IsTmpInstalled) Translate(ref __0);
    }

    /// <summary>
    /// 进入对白标签时读取剧本位置、原始说话人和语音文件名，并保存外层标签状态。
    /// Capture the script location, raw speaker, and voice filename on entry to the dialogue tag and save the enclosing state.
    /// </summary>
    [HarmonyPatch(typeof(ADVKagManager), nameof(ADVKagManager.TagHitRet))]
    [HarmonyPrefix]
    private static void DialoguePrefix(ADVKagManager __instance, ADVKagManager.MessageData ___textData,
        out ScriptScope? __state)
    {
        __state = _scriptScope;
        _scriptScope = new ScriptScope
        {
            Tag = ScriptTag.Dialogue,
            Kag = ReadKagContext(__instance, ___textData?.talkName, ___textData?.voiceData?.voiceFileName)
        };
    }

    /// <summary>
    /// 对白标签结束或抛出异常后恢复外层标签状态。
    /// Restore the enclosing tag state when the dialogue tag returns or throws.
    /// </summary>
    [HarmonyPatch(typeof(ADVKagManager), nameof(ADVKagManager.TagHitRet))]
    [HarmonyFinalizer]
    private static void DialogueFinalizer(ScriptScope? __state)
    {
        if (__state is ScriptScope previous) _scriptScope = previous;
    }

    /// <summary>
    /// 进入选项标签时读取剧本位置和跳转目标，并保存外层标签状态。
    /// Capture the script location and jump target on entry to the choice tag and save the enclosing state.
    /// </summary>
    [HarmonyPatch(typeof(ADVKagManager), nameof(ADVKagManager.TagChoicesSet))]
    [HarmonyPrefix]
    private static void ChoicePrefix(ADVKagManager __instance, object __0, out ScriptScope? __state)
    {
        __state = _scriptScope;
        _scriptScope = new ScriptScope
        {
            Tag = ScriptTag.Choice,
            Kag = ReadKagContext(__instance, target: ReadChoiceTarget(__0))
        };
    }

    /// <summary>
    /// 选项标签结束或抛出异常后恢复外层标签状态。
    /// Restore the enclosing tag state when the choice tag returns or throws.
    /// </summary>
    [HarmonyPatch(typeof(ADVKagManager), nameof(ADVKagManager.TagChoicesSet))]
    [HarmonyFinalizer]
    private static void ChoiceFinalizer(ScriptScope? __state)
    {
        if (__state is ScriptScope previous) _scriptScope = previous;
    }

    /// <summary>
    /// 读取标签执行时的 KAG 位置，保留已成功读取的字段。
    /// Read the KAG location at tag execution, retaining any fields already read successfully.
    /// </summary>
    private static KagTextContext ReadKagContext(ADVKagManager instance, string speaker = null, string voice = null,
        string target = null)
    {
        string filename = null;
        string label = null;
        int? line = null;
        try
        {
            filename = instance.currentFileName;
            label = instance.currentLabel;
            var currentLine = instance.currentLine;
            if (currentLine >= 0) line = currentLine;
        }
        catch (Exception exception)
        {
            LogKagContextFailure(exception);
        }

        return new KagTextContext(string.IsNullOrWhiteSpace(filename) ? null : filename, label,
            string.IsNullOrWhiteSpace(speaker) ? null : speaker, string.IsNullOrWhiteSpace(voice) ? null : voice,
            target, line);
    }

    /// <summary>
    /// 通过游戏标签对象读取原始跳转目标，避免引入 Com3d2Native 编译引用。
    /// Read the raw jump target from the game's tag object without a compile-time Com3d2Native reference.
    /// </summary>
    private static string ReadChoiceTarget(object tagData)
    {
        if (tagData == null) return null;
        try
        {
            var method = AccessTools.Method(tagData.GetType(), "TryPropAsString", TagStringParameterTypes);
            var arguments = new object[] { "label", null };
            if (method?.Invoke(tagData, arguments) is true && arguments[1] is string target &&
                !string.IsNullOrWhiteSpace(target)) return target;
        }
        catch (Exception exception)
        {
            LogKagContextFailure(exception);
        }

        return null;
    }

    /// <summary>
    /// 语境读取失败时在本次运行中只告警一次。
    /// Warn only once per run when reading KAG context fails.
    /// </summary>
    private static void LogKagContextFailure(Exception exception)
    {
        if (Interlocked.Exchange(ref _contextWarningLogged, 1) == 0)
            LogManager.Warning($"[Text] Cannot read KAG context/无法读取 KAG 语境: {exception.Message}");
    }

    /// <summary>
    /// 在选项第一次展开角色名前查询词库，后续展开直接交给游戏。
    /// Query the catalog before the choice's first name expansion and let the game handle later expansions.
    /// </summary>
    [HarmonyPatch(typeof(AdvUtility), nameof(AdvUtility.ReplaceCharaName), typeof(string))]
    [HarmonyPrefix]
    private static void ChoiceNameExpansionPrefix(ref string __0)
    {
        if (_scriptScope.Tag != ScriptTag.Choice) return;
        _scriptScope.Tag = TranslateScriptText(ref __0) ? ScriptTag.ChoiceHit : ScriptTag.ChoiceMiss;
    }

    /// <summary>
    /// 在游戏展开角色名之前翻译对白和说话人名，并传递选项的查询结果。
    /// Translate dialogue and speaker names before the game expands character names, and carry forward the choice lookup result.
    /// </summary>
    [HarmonyPatch(typeof(AdvUtility), nameof(AdvUtility.GetLocalizationStringFromScriptText), typeof(string))]
    [HarmonyPrefix]
    private static void ScriptLocalizationPrefix(ref string __0, out bool __state)
    {
        __state = _scriptScope.Tag == ScriptTag.ChoiceHit ||
                  (_scriptScope.Tag == ScriptTag.Dialogue && TranslateScriptText(ref __0));
    }

    /// <summary>
    /// NET 命中时登记展开角色名并拆分语言段后的译文，以及对白界面去除首尾空白后的版本。
    /// On a NET hit, record the translations after name expansion and language parsing, including the trimmed versions shown in dialogue.
    /// </summary>
    [HarmonyPatch(typeof(AdvUtility), nameof(AdvUtility.GetLocalizationStringFromScriptText), typeof(string))]
    [HarmonyPostfix]
    private static void ScriptLocalizationPostfix(LocalizeString __result, bool __state)
    {
        if (!__state || __result == null || !XUATInterop.IsInstalled) return;
        foreach (var localized in __result)
        {
            var text = localized.Value;
            TextTranslateManager.MarkTranslated(text);
            var trimmed = text.Trim();
            if (trimmed.Length != text.Length) TextTranslateManager.MarkTranslated(trimmed);
        }
    }

    /// <summary>
    /// 按游戏的换行规则统一对白与选项的词库键，并登记角色名展开前的译文。
    /// Normalize dialogue and choice lookup keys using the game's line-break rules and record the translation before name expansion.
    /// </summary>
    private static bool TranslateScriptText(ref string text)
    {
        if (text == null) return false;
        var source = text.Trim().Replace('|', '\n');
        if (!Translate(ref source, scriptSource: true)) return false;
        text = source;
        TextTranslateManager.MarkTranslated(text);
        return true;
    }

    /// <summary>
    /// 替换可翻译的原文，失败时保留原值并只警告一次。
    /// Replace translatable source text, preserving the original value and warning only once on failure.
    /// </summary>
    /// <param name="text">显示入口收到的原文，命中后改写成译文 / The source text received by the display entry, rewritten to the translation on a hit.</param>
    /// <param name="scriptSource">原文直接来自剧本时为 true / True if the original text comes directly from the script.</param>
    /// <returns>应用了非空译文时为 true / True if a nonblank translation was applied.</returns>
    private static bool Translate(ref string text, bool scriptSource = false)
    {
        if (!IsTranslatableSource(text, scriptSource)) return false;
        try
        {
            var found = scriptSource
                ? TextTranslateManager.TryGetOriginalTranslation(text, out var translated, new TextContext(_scriptScope.Kag))
                : TextTranslateManager.TryGetTranslation(text, out translated);
            if (found && !string.IsNullOrWhiteSpace(translated))
            {
                text = translated;
                return true;
            }
        }
        catch (Exception exception)
        {
            if (Interlocked.Exchange(ref _failureWarningLogged, 1) == 0)
                LogManager.Warning($"[Text] Display translation failed; original preserved/显示翻译失败，保留原文: {text}: {exception.Message}");
        }

        return false;
    }

    /// <summary>
    /// 排除空白和纯数字；直接来自剧本以外的文字还需检查是否为已登记译文。
    /// Exclude blanks and bare numbers, and check recorded outputs for text that did not come directly from the script.
    /// </summary>
    /// <param name="text">显示入口收到的原文 / The source text received by the display entry.</param>
    /// <param name="scriptSource">原文直接来自剧本时为 true / True if the original text comes directly from the script.</param>
    /// <returns>需要查询词典时为 true / True if the dictionary should be queried.</returns>
    private static bool IsTranslatableSource(string text, bool scriptSource)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!scriptSource && TextTranslateManager.IsNetTranslatedText(text)) return false;

        return !double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
    }
}
