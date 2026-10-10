using System;
using CRC3D3.NotEnoughTranslator.Plugin.Hooks.UI;
using CRC3D3.NotEnoughTranslator.Plugin.Loader;
using CRC3D3.NotEnoughTranslator.Plugin.Loader.Processor;
using CRC3D3.NotEnoughTranslator.Plugin.Translation;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;
using I2.Loc;

namespace CRC3D3.NotEnoughTranslator.Plugin.Manger;

public static class UITranslateManager
{
    private static readonly TranslatedTextRegistry TranslatedTexts = new();
    private static ResourceModule<TranslationLoadResult> _module;
    private static volatile TranslationCatalog _catalog = TranslationCatalog.Empty;
    private static bool _refreshPending;

    public static bool IsLoaded => _module?.IsLoaded == true;
    public static bool IsLoading => _module?.IsLoading == true;
    public static bool IsHookInstalled => UITextTranslatePatch.IsInstalled;
    public static int EntryCount => _catalog.ExactCount;

    /// <summary>
    /// 在 Unity 主线程安装 I2 翻译与本地化就绪补丁，并从 UI/Text 异步加载词条。
    /// Install I2 translation and localization-readiness hooks on Unity's main thread and load terms asynchronously from UI/Text.
    /// </summary>
    public static void Init()
    {
        if (_module != null || !NotEnoughTranslator.EnableUITranslation.Value) return;
        if (!UITextTranslatePatch.Install()) return;
        LocalizationReadyPatch.Install();

        _module = new ResourceModule<TranslationLoadResult>("UI", token =>
            AsyncTranslationLoader.LoadOnceAsync("UI", NotEnoughTranslator.UITextPath,
                NotEnoughTranslator.AllowFilesInZipLoadInOrder.Value, token,
                new CsvTranslationFileProcessor()), ApplyResult);
        _module.Reload();
    }

    /// <summary>
    /// 从当前 UI 快照中按 I2 词条键查询译文，不执行参数或格式处理。
    /// Look up an I2 term in the current UI snapshot without processing parameters or formatting.
    /// </summary>
    /// <param name="term">I2 词条键 / The I2 term key.</param>
    /// <param name="translation">查询成功时返回的译文模板 / The translation template returned on a successful lookup.</param>
    /// <returns>命中词条时为 true，否则为 false / True if the term is found; otherwise false.</returns>
    public static bool TryGetTranslation(string term, out string translation)
    {
        return _catalog.TryGetTranslation(term, out translation);
    }

    /// <summary>
    /// 首份快照发布后导出未翻译的 I2 词条，包含发布过程触发的首次 UI 刷新。
    /// Dump untranslated I2 terms after the first snapshot is published, including the initial UI refresh during publication.
    /// </summary>
    /// <param name="term">查询未命中的 I2 词条键 / The I2 term key from an unsuccessful lookup.</param>
    /// <param name="original">游戏返回的原始显示文字 / The original display text returned by the game.</param>
    public static void DumpTerm(string term, string original)
    {
        var catalog = _catalog;
        if (catalog == TranslationCatalog.Empty || catalog.TryGetTranslation(term, out _)) return;
        DumpManager.DumpTerm(term, original);
    }

    /// <summary>
    /// 检查文字是否已登记为 NET 的 UI 译文。
    /// Check whether text is recorded as NET UI output.
    /// </summary>
    /// <param name="text">待检查的文字 / The text to check.</param>
    /// <returns>已登记时为 true，否则为 false / True if the text is recorded; otherwise false.</returns>
    public static bool IsNetTranslatedText(string text)
    {
        return TranslatedTexts.Contains(text);
    }

    /// <summary>
    /// XUAT 互操作启用时登记处理完成的最终 UI 译文，避免再次翻译。
    /// Record final processed UI translations while XUAT interop is active to prevent retranslation.
    /// </summary>
    /// <param name="text">参数展开和格式处理完成后的译文 / The translation after parameter expansion and formatting.</param>
    public static void MarkTranslated(string text)
    {
        if (XUATInterop.IsInstalled) TranslatedTexts.Add(text);
    }

    /// <summary>
    /// 在 Unity 主线程按启用配置初始化、重载或卸载 UI 模块。
    /// Initialize, reload, or unload the UI module on Unity's main thread according to its enable setting.
    /// </summary>
    public static void Reload()
    {
        if (!NotEnoughTranslator.EnableUITranslation.Value) Unload();
        else if (_module == null) Init();
        else _module.Reload();
    }

    /// <summary>
    /// 卸载 UI 资源和翻译补丁并请求原生刷新，保留就绪回调直到插件退出。
    /// Unload UI resources and translation hooks and request a native refresh, retaining readiness callbacks until plugin shutdown.
    /// </summary>
    public static void Unload()
    {
        var hadTranslations = EntryCount != 0;
        _module?.Dispose();
        _module = null;
        _catalog = TranslationCatalog.Empty;
        TranslatedTexts.Clear();
        UITextTranslatePatch.Uninstall();
        _refreshPending |= hadTranslations;
        RefreshIfReady();
    }

    /// <summary>
    /// 插件销毁时卸载 UI 模块，清空待刷新状态并移除本地化就绪补丁。
    /// Unload the UI module on plugin destruction, clear pending refresh state, and remove the localization-readiness hook.
    /// </summary>
    internal static void Shutdown()
    {
        Unload();
        _refreshPending = false;
        LocalizationReadyPatch.Uninstall();
    }

    /// <summary>
    /// 在主线程发布 UI 词条快照，清理旧登记记录并请求刷新显示。
    /// Publish the UI term snapshot on the main thread, clear old recorded outputs, and request a display refresh.
    /// </summary>
    /// <param name="result">成功完成的 UI 资源加载结果 / The successfully loaded UI resources.</param>
    private static void ApplyResult(TranslationLoadResult result)
    {
        _catalog = new TranslationCatalog(result.I2Terms, Array.Empty<RegexTranslationRule>());
        TranslatedTexts.Clear();
        UITextTranslatePatch.ClearWarnings();
        _refreshPending = true;
        LogManager.Info($"[UI] Loaded/已加载 {EntryCount} terms, {result.SkippedLines} skipped, {result.ElapsedMilliseconds} ms");
        RefreshIfReady();
    }

    /// <summary>
    /// 本地化初始化完成且存在待处理刷新时，强制刷新 I2 组件。
    /// Force an I2 component refresh only when localization setup is complete and a refresh is pending.
    /// </summary>
    internal static void RefreshIfReady()
    {
        if (!_refreshPending || !LocalizeManager.isSetupCompleted) return;
        _refreshPending = false;
        try
        {
            LocalizationManager.LocalizeAll(true);
        }
        catch (Exception exception)
        {
            LogManager.Warning($"[UI] Cannot refresh localized objects/无法刷新本地化组件: {exception.Message}");
        }
    }
}
