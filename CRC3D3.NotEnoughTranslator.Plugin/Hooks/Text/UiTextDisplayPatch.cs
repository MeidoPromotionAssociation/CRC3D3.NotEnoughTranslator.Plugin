using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using AdvSystem;
using CRC.UI;
using CRC3D3.NotEnoughTranslator.Plugin.Manger;
using CRC3D3.NotEnoughTranslator.Plugin.Translation;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;
using HarmonyLib;
using I2.Loc;
using MiniGameMode;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using UguiText = UnityEngine.UI.Text;

namespace CRC3D3.NotEnoughTranslator.Plugin.Hooks.Text;

/// <summary>
/// 在 Unity 文本组件的显示入口应用通用词库，并保留更新或恢复显示所需的原文。
/// Apply the general catalog at Unity text display entries and retain the source needed to refresh or restore a display.
/// </summary>
internal static class UiTextDisplayPatch
{
    /// <summary>
    /// 界面文本显示补丁使用的 Harmony ID。
    /// The Harmony ID for UI text display patches.
    /// </summary>
    public const string HarmonyId =
        "github.meidopromotionassociation.crc3d3.notenoughtranslator.plugin.hooks.text.uitextdisplaypatch";

    private static ConditionalWeakTable<Component, DisplayText> _displayed = new();
    private static ConditionalWeakTable<TextMesh, object> _observedFishingLabels = new();
    private static readonly object Observed = new();
    private static readonly FieldInfo TopicFilterLabel = AccessTools.Field(typeof(TopicFilterItem), "m_nameText");
    private static Harmony _harmony;
    private static bool _uguiInstalled;
    private static int _failureWarningLogged;
    [ThreadStatic] private static int _sourceScopeDepth;
    [ThreadStatic] private static Component _refreshing;

    public static bool IsTmpInstalled { get; private set; }
    public static bool IsInstalled => _harmony != null;

    private sealed class DisplayText
    {
        public string Original;
        public string Value;
    }

    /// <summary>
    /// 按界面组件和上游本地化方法安装显示补丁。
    /// Install display patches for UI components and upstream localization methods.
    /// </summary>
    public static void Install()
    {
        if (_harmony != null) return;
        _harmony = new Harmony(HarmonyId);

        foreach (var target in new[]
                 {
                     AccessTools.Method(typeof(Localize), nameof(Localize.OnLocalize), new[] { typeof(bool) }),
                     AccessTools.Method(typeof(LocalizeDropdown), nameof(LocalizeDropdown.OnLocalize)),
                     AccessTools.PropertySetter(typeof(MessageDisplayClassicStyle), nameof(MessageDisplayClassicStyle.messageText)),
                     AccessTools.PropertySetter(typeof(MessageDisplayClassicStyle), nameof(MessageDisplayClassicStyle.charaNameText)),
                     AccessTools.Method(typeof(NewStyleMessageBlock), "SetText", new[] { typeof(TMP_Text), typeof(string) }),
                     AccessTools.Method(typeof(BackLogMessageUnit), nameof(BackLogMessageUnit.SetMessage)),
                     AccessTools.Method(typeof(ChoicesButton), nameof(ChoicesButton.SetData))
                 })
            TryPatch(target, prefix: nameof(SourceScopePrefix), finalizer: nameof(SourceScopeFinalizer));

        _uguiInstalled = TryPatch(AccessTools.PropertySetter(typeof(UguiText), nameof(UguiText.text)),
            prefix: nameof(TextWritePrefix));
        if (_uguiInstalled)
            TryPatch(AccessTools.DeclaredMethod(typeof(UguiText), "OnEnable"), postfix: nameof(LabelEnabledPostfix));

        IsTmpInstalled = TryPatch(AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text)),
            prefix: nameof(TextWritePrefix));
        if (IsTmpInstalled) InstallTmpHooks();

        TryPatch(AccessTools.DeclaredMethod(typeof(FishingGameBlinker), "Update"), prefix: nameof(FishingLabelPrefix));
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// 挂接 TMP 的其他写入方式、启用事件和相关的游戏读回入口。
    /// Patch additional TMP writes, enable callbacks, and the relevant game readback entries.
    /// </summary>
    private static void InstallTmpHooks()
    {
        TryPatch(AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(string), typeof(bool) }),
            prefix: nameof(TextWritePrefix));
        TryPatch(AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[]
        {
            typeof(string), typeof(float), typeof(float), typeof(float), typeof(float),
            typeof(float), typeof(float), typeof(float), typeof(float)
        }), postfix: nameof(FormattedWritePostfix));
        TryPatch(AccessTools.DeclaredMethod(typeof(TMP_Text), nameof(TMP_Text.SetText),
            new[] { typeof(StringBuilder), typeof(int), typeof(int) }), postfix: nameof(FormattedWritePostfix));
        TryPatch(AccessTools.DeclaredMethod(typeof(TMP_Text), nameof(TMP_Text.SetCharArray),
            new[] { typeof(char[]), typeof(int), typeof(int) }), postfix: nameof(FormattedWritePostfix));
        foreach (var type in new[] { typeof(TextMeshProUGUI), typeof(TextMeshPro) })
            TryPatch(AccessTools.DeclaredMethod(type, "OnEnable"), postfix: nameof(LabelEnabledPostfix));

        if (TopicFilterLabel == null) return;
        TryPatch(AccessTools.PropertyGetter(typeof(TopicFilterItem), nameof(TopicFilterItem.FilterName)),
            postfix: nameof(TopicFilterNamePostfix));
        TryPatch(AccessTools.Method(typeof(TopicListManager), nameof(TopicListManager.NameFilter)),
            prefix: nameof(TopicFilterPrefix));
        TryPatch(AccessTools.Method(typeof(TopicListManager), nameof(TopicListManager.ExpirationTypeFilter)),
            prefix: nameof(TopicFilterPrefix));
    }

    /// <summary>
    /// 移除界面补丁，并在仍显示 NET 译文的组件上恢复原文。
    /// Remove UI hooks and restore the sources of components still displaying NET output.
    /// </summary>
    public static void Uninstall()
    {
        if (_harmony == null) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _harmony.UnpatchSelf();
        if (_uguiInstalled) RestoreSources(typeof(UguiText));
        if (IsTmpInstalled) RestoreSources(typeof(TMP_Text));
        RestoreSources(typeof(TextMesh));
        _displayed = new ConditionalWeakTable<Component, DisplayText>();
        _observedFishingLabels = new ConditionalWeakTable<TextMesh, object>();
        _harmony = null;
        _uguiInstalled = false;
        IsTmpInstalled = false;
    }

    /// <summary>
    /// 恢复仍显示 NET 译文的组件，也检查当前隐藏的组件。
    /// Restore original text for components still showing NET output, including hidden components.
    /// </summary>
    private static void RestoreSources(Type type)
    {
        foreach (Component component in Resources.FindObjectsOfTypeAll(type))
        {
            if (component == null || !_displayed.TryGetValue(component, out var shown) ||
                shown.Original == null || shown.Original == shown.Value) continue;
            try
            {
                if (ReadText(component) == shown.Value) WriteText(component, shown.Original);
            }
            catch (Exception exception)
            {
                WarnDisplayFailure(exception);
            }
        }
    }

    /// <summary>
    /// 快照发布后更新当前可见文字，包括在资源加载前已启用的预制体文字。
    /// Refresh visible labels after publishing a snapshot, including prefabs enabled before it loaded.
    /// </summary>
    public static void RefreshVisible()
    {
        if (_harmony == null) return;
        if (_uguiInstalled) RefreshVisible(typeof(UguiText));
        if (IsTmpInstalled) RefreshVisible(typeof(TMP_Text));
        RefreshVisible(typeof(TextMesh));
    }

    /// <summary>
    /// 更新某种组件的活动场景实例。
    /// Refresh active scene instances of a component type.
    /// </summary>
    private static void RefreshVisible(Type type)
    {
        foreach (Component component in Object.FindObjectsByType(type, FindObjectsSortMode.None))
            if (component is not Behaviour behaviour || behaviour.isActiveAndEnabled)
                RefreshComponent(component);
    }

    /// <summary>
    /// 在场景就绪后处理使用原生 setter 的旧式 TextMesh。
    /// Handle legacy TextMesh labels with native setters once their scene has loaded.
    /// </summary>
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var mesh in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            if (mesh.gameObject.scene == scene) RefreshComponent(mesh);
    }

    /// <summary>
    /// 在钓鱼界面首次使用 TextMesh 时处理运行时生成的文字。
    /// Handle dynamically created TextMesh labels when the fishing UI first uses them.
    /// </summary>
    private static void FishingLabelPrefix(FishingGameBlinker __instance)
    {
        var label = __instance.textMesh;
        if (label == null || _observedFishingLabels.TryGetValue(label, out _)) return;
        _observedFishingLabels.Add(label, Observed);
        RefreshComponent(label);
    }

    /// <summary>
    /// 安装一个显示补丁，个别版本不兼容时保留其他入口。
    /// Install one display patch while preserving other entries if a game version differs.
    /// </summary>
    private static bool TryPatch(MethodBase target, string prefix = null, string postfix = null,
        string finalizer = null)
    {
        if (target == null) return false;
        try
        {
            _harmony.Patch(target,
                prefix == null ? null : new HarmonyMethod(typeof(UiTextDisplayPatch), prefix) { priority = Priority.First },
                postfix == null ? null : new HarmonyMethod(typeof(UiTextDisplayPatch), postfix) { priority = Priority.First },
                finalizer: finalizer == null ? null : new HarmonyMethod(typeof(UiTextDisplayPatch), finalizer));
            return true;
        }
        catch (Exception exception)
        {
            LogManager.Warning($"[Text] UI hook unavailable/界面补丁不可用 {target.DeclaringType?.FullName}.{target.Name}: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// 进入 I2 或剧情的文字写入范围，保存外层深度。
    /// Enter the I2 or scenario write scope, saving the enclosing depth.
    /// </summary>
    private static void SourceScopePrefix(out int? __state) => __state = _sourceScopeDepth++;

    /// <summary>
    /// 在退出或异常后恢复外层文字写入范围。
    /// Restore the enclosing text write scope after return or failure.
    /// </summary>
    private static void SourceScopeFinalizer(int? __state)
    {
        if (__state is int previous) _sourceScopeDepth = previous;
    }

    /// <summary>
    /// 在组件接收字符串之前替换其内容。
    /// Substitute strings before the text component receives them.
    /// </summary>
    private static void TextWritePrefix(Component __instance, ref string __0)
    {
        if (ReferenceEquals(_refreshing, __instance)) return;
        if (_sourceScopeDepth == 0 && _displayed.TryGetValue(__instance, out var shown) &&
            shown.Original != null && ReferenceEquals(shown.Value, __0))
        {
            TextTranslateManager.MarkTranslated(__0);
            return;
        }
        __0 = TranslateWrite(__instance, __0);
    }

    /// <summary>
    /// 对 TMP 已完成格式化或缓冲区转换的实际字串做查询。
    /// Look up the actual string produced by TMP numeric formatting or buffer conversion.
    /// </summary>
    private static void FormattedWritePostfix(TMP_Text __instance)
    {
        try
        {
            var current = __instance.text;
            var translated = TranslateWrite(__instance, current);
            if (translated != current) WriteText(__instance, translated);
        }
        catch (Exception exception)
        {
            WarnDisplayFailure(exception);
        }
    }

    /// <summary>
    /// 组件启用时检查序列化的预制体文字。
    /// Check serialized prefab text when a label is enabled.
    /// </summary>
    private static void LabelEnabledPostfix(Component __instance) => RefreshComponent(__instance);

    /// <summary>
    /// 从组件读取仍可刷新的原文，保留 I2 用作隐式词条的预制体文字。
    /// Recover the source for a display refresh and preserve prefab text used as an implicit I2 term.
    /// </summary>
    private static void RefreshComponent(Component component)
    {
        try
        {
            if (component == null || _sourceScopeDepth != 0) return;
            var current = ReadText(component);
            var source = current;
            if (_displayed.TryGetValue(component, out var shown) && shown.Value == current)
            {
                if (shown.Original == null) return;
                source = shown.Original;
            }
            else if (component.GetComponent<Localize>() is { enabled: true }) return;

            var translated = TranslateWrite(component, source);
            if (translated != current) WriteText(component, translated);
        }
        catch (Exception exception)
        {
            WarnDisplayFailure(exception);
        }
    }

    /// <summary>
    /// 为一次文字写入查词并登记实际应用的译文，同时保留输入栏与上游本地化结果。
    /// Look up a text write and register applied output while preserving input fields and upstream localization.
    /// </summary>
    private static string TranslateWrite(Component component, string source)
    {
        try
        {
            _displayed.Remove(component);
            if (string.IsNullOrWhiteSpace(source) ||
                double.TryParse(source, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return source;
            if (_sourceScopeDepth != 0 || UITranslateManager.IsI2Output(source))
            {
                _displayed.Add(component, new DisplayText { Value = source });
                return source;
            }
            if (IsInputLabel(component)) return source;

            var context = TextTranslateManager.NeedsUiContext(source) ? ReadContext(component) : default;
            if (TextTranslateManager.TryGetOriginalTranslation(source, out var translation, context) &&
                !string.IsNullOrWhiteSpace(translation))
            {
                _displayed.Add(component, new DisplayText { Original = source, Value = translation });
                TextTranslateManager.MarkTranslated(translation);
                return translation;
            }
        }
        catch (Exception exception)
        {
            WarnDisplayFailure(exception);
        }

        return source;
    }

    /// <summary>
    /// 识别用于显示输入值的标签，输入框的提示标签仍可查词。
    /// Identify labels that display editable values, while keeping placeholders eligible for lookup.
    /// </summary>
    private static bool IsInputLabel(Component component) => component switch
    {
        TMP_Text label => label.GetComponentInParent<TMP_InputField>(true)?.textComponent == label,
        UguiText label => label.GetComponentInParent<InputField>(true)?.textComponent == label,
        _ => false
    };

    /// <summary>
    /// 读取组件所在场景、从根对象开始的层级路径，以及具体组件类名。
    /// Read the component's scene, hierarchy path from the root, and concrete type name.
    /// </summary>
    private static TextContext ReadContext(Component component)
    {
        var names = new Stack<string>();
        for (var node = component.transform; node != null; node = node.parent)
            names.Push(node.name.Replace("\\", "\\\\").Replace("/", "\\/"));
        var scene = component.gameObject.scene.name;
        var path = string.Join("/", names);
        return new TextContext(new UiTextContext(string.IsNullOrWhiteSpace(scene) ? null : scene,
            string.IsNullOrWhiteSpace(path) ? null : path, component.GetType().FullName));
    }

    /// <summary>
    /// 从支持的文本组件读取当前字串。
    /// Read the current string from a supported text component.
    /// </summary>
    private static string ReadText(Component component) => component switch
    {
        TMP_Text label => label.text,
        UguiText label => label.text,
        TextMesh mesh => mesh.text,
        _ => null
    };

    /// <summary>
    /// 写入已经处理的字串，防止回到同一个通用文本查询入口。
    /// Write a resolved string without re-entering general lookup for the same component.
    /// </summary>
    private static void WriteText(Component component, string value)
    {
        var previous = _refreshing;
        _refreshing = component;
        try
        {
            switch (component)
            {
                case TMP_Text label:
                    label.text = value;
                    break;
                case UguiText label:
                    label.text = value;
                    break;
                case TextMesh mesh:
                    mesh.text = value;
                    break;
            }
        }
        finally
        {
            _refreshing = previous;
        }
    }

    /// <summary>
    /// 话题筛选的逻辑键沿用游戏写入的原名。
    /// Preserve the game's original topic filter name as the logical key.
    /// </summary>
    private static void TopicFilterNamePostfix(TopicFilterItem __instance, ref string __result) =>
        __result = OriginalFilterName(__instance, __result);

    /// <summary>
    /// 点击筛选时将直接从标签读取的显示名恢复为逻辑键。
    /// Restore a logical key when filter clicks read a name directly from the label.
    /// </summary>
    private static void TopicFilterPrefix(TopicFilterItem __0, ref string __2) =>
        __2 = OriginalFilterName(__0, __2);

    /// <summary>
    /// 当筛选标签仍显示 NET 译文时返回对应原名。
    /// Return the original filter name if its label still shows NET output.
    /// </summary>
    private static string OriginalFilterName(TopicFilterItem item, string current)
    {
        if (item != null && TopicFilterLabel.GetValue(item) is Component label &&
            _displayed.TryGetValue(label, out var shown) && shown.Value == current)
            return shown.Original ?? current;
        return current;
    }

    /// <summary>
    /// 界面处理异常时记录一次告警，保留游戏原来的显示行为。
    /// Warn once on a UI display failure and preserve the game's display behavior.
    /// </summary>
    private static void WarnDisplayFailure(Exception exception)
    {
        if (Interlocked.Exchange(ref _failureWarningLogged, 1) == 0)
            LogManager.Warning($"[Text] UI display translation failed/界面文字翻译失败: {exception.Message}");
    }
}
