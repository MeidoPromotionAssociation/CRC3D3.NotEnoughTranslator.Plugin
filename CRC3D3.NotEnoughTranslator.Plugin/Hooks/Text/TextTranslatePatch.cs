using System;
using System.Globalization;
using System.Threading;
using CRC.UI;
using CRC3D3.NotEnoughTranslator.Plugin.Manger;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;
using HarmonyLib;

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

    /// <summary>
    /// 系统消息队列入口，覆盖不经过 I2 的硬编码提示。
    /// The system-message queue entry, covering hardcoded notices that never reach I2.
    /// </summary>
    /// <param name="__0">按位置绑定的消息原文，游戏改名不影响补丁 / The message text bound by position, unaffected by game-side parameter renames.</param>
    [HarmonyPatch(typeof(SystemMessagesManager), nameof(SystemMessagesManager.ShowSystemMessage), typeof(string))]
    [HarmonyPrefix]
    private static void SystemMessagePrefix(ref string __0)
    {
        Translate(ref __0);
    }

    /// <summary>
    /// 替换可翻译的原文，失败时保留原值并只警告一次。
    /// Replace translatable source text, preserving the original value and warning only once on failure.
    /// </summary>
    /// <param name="text">显示入口收到的原文，命中后改写成译文 / The source text received by the display entry, rewritten to the translation on a hit.</param>
    private static void Translate(ref string text)
    {
        if (!IsTranslatableSource(text)) return;
        try
        {
            if (TextTranslateManager.TryGetTranslation(text, out var translated) &&
                !string.IsNullOrWhiteSpace(translated))
                text = translated;
        }
        catch (Exception exception)
        {
            if (Interlocked.Exchange(ref _failureWarningLogged, 1) != 0) return;
            LogManager.Warning($"[Text] Display translation failed; original preserved/显示翻译失败，保留原文: {text}: {exception.Message}");
        }
    }

    /// <summary>
    /// 排除空白、纯数字和已登记的 NET 译文，避免无意义查询与重复导出。
    /// Exclude blank text, bare numbers, and recorded NET output to avoid pointless queries and repeated dumps.
    /// </summary>
    /// <param name="text">显示入口收到的原文 / The source text received by the display entry.</param>
    /// <returns>需要查询词典时为 true / True if the dictionary should be queried.</returns>
    private static bool IsTranslatableSource(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (TextTranslateManager.IsNetTranslatedText(text)) return false;

        return !double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
    }
}
