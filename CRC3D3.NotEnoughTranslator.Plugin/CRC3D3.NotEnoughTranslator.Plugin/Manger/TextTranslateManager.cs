using System;
using CRC3D3.NotEnoughTranslator.Plugin.Loader;
using CRC3D3.NotEnoughTranslator.Plugin.Loader.Processor;
using CRC3D3.NotEnoughTranslator.Plugin.Translation;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;

namespace CRC3D3.NotEnoughTranslator.Plugin.Manger;

public static class TextTranslateManager
{
    private static readonly TranslatedTextRegistry TranslatedTexts = new();
    private static ResourceModule<TranslationLoadResult> _module;
    private static volatile TranslationCatalog _catalog = TranslationCatalog.Empty;
    private static volatile bool _displayHooksInstalled;

    public static bool IsLoaded => _module?.IsLoaded == true;
    public static bool IsLoading => _module?.IsLoading == true;
    public static int ExactCount => _catalog.ExactCount;
    public static int RegexCount => _catalog.RegexCount;
    public static int EntryCount => ExactCount + RegexCount;
    public static bool AreDisplayHooksInstalled => _displayHooksInstalled;

    /// <summary>
    /// 在 Unity 主线程初始化通用文本模块，并从 Text 异步加载 JSONL 精确与正则译文。
    /// Initialize the general-text module on Unity's main thread and load JSONL exact and regex translations asynchronously from Text.
    /// </summary>
    public static void Init()
    {
        if (_module != null || !NotEnoughTranslator.EnableTextTranslation.Value) return;
        _module = new ResourceModule<TranslationLoadResult>("Text", token =>
            AsyncTranslationLoader.LoadOnceAsync("Text", NotEnoughTranslator.TranslationTextPath,
                NotEnoughTranslator.AllowFilesInZipLoadInOrder.Value, token,
                new JsonlTranslationFileProcessor(TimeSpan.FromMilliseconds(
                    NotEnoughTranslator.RegexTimeoutMilliseconds.Value))), ApplyResult);
        _module.Reload();
    }

    /// <summary>
    /// 查询通用文本译文，按配置导出未命中的原文，并在互操作启用时跳过和登记已译文字。
    /// Look up general-text translations, optionally dump misses, and skip or record translated output while interop is active.
    /// </summary>
    /// <param name="sourceText">待翻译的显示文字 / The display text to translate.</param>
    /// <param name="translation">查询成功时返回的译文 / The translated text returned on a successful lookup.</param>
    /// <returns>查询命中时为 true，未命中或跳过已译文字时为 false / True on a match; false on a miss or when skipping recorded output.</returns>
    public static bool TryGetTranslation(string sourceText, out string translation)
    {
        translation = null;
        if (XUATInterop.IsInstalled &&
            (IsNetTranslatedText(sourceText) || UITranslateManager.IsNetTranslatedText(sourceText))) return false;
        if (!_catalog.TryGetTranslation(sourceText, out translation))
        {
            if (IsLoaded && NotEnoughTranslator.EnableTextDump.Value) DumpManager.DumpText(sourceText);
            return false;
        }
        MarkTranslated(translation);
        return true;
    }

    /// <summary>
    /// 仅探测精确或正则译文，不将候选结果登记为已翻译输出。
    /// Probe exact or regex translations without recording candidates as translated output.
    /// </summary>
    /// <param name="sourceText">待探测的原文 / The source text to probe.</param>
    /// <returns>精确词条或正则规则命中时为 true / True if an exact entry or regex rule matches.</returns>
    public static bool IsInTranslationDictionary(string sourceText)
    {
        return _catalog.TryGetTranslation(sourceText, out _);
    }

    /// <summary>
    /// 检查文字是否已登记为 NET 的通用文本译文。
    /// Check whether text is recorded as NET general-text output.
    /// </summary>
    /// <param name="text">待检查的文字 / The text to check.</param>
    /// <returns>已登记时为 true，否则为 false / True if the text is recorded; otherwise false.</returns>
    public static bool IsNetTranslatedText(string text)
    {
        return TranslatedTexts.Contains(text);
    }

    /// <summary>
    /// XUAT 互操作启用时登记通用文本译文，避免再次翻译。
    /// Record general-text translations while XUAT interop is active to prevent retranslation.
    /// </summary>
    /// <param name="text">已返回或由显示 Hook 处理后的最终译文 / The returned translation or final output processed by a display hook.</param>
    public static void MarkTranslated(string text)
    {
        if (XUATInterop.IsInstalled) TranslatedTexts.Add(text);
    }

    /// <summary>
    /// 登记实际显示 Hook 的安装状态，控制 NET 是否接管 XUAT 的原文判定。
    /// Track display-hook installation so NET can control XUAT decisions for source text.
    /// </summary>
    /// <param name="installed">显示 Hook 确已安装时为 true，移除后为 false / True only after display hooks are installed; false after removal.</param>
    public static void SetDisplayHooksInstalled(bool installed)
    {
        _displayHooksInstalled = installed;
    }

    /// <summary>
    /// 在 Unity 主线程按启用配置初始化、重载或卸载通用文本模块。
    /// Initialize, reload, or unload the general-text module on Unity's main thread according to its enable setting.
    /// </summary>
    public static void Reload()
    {
        if (!NotEnoughTranslator.EnableTextTranslation.Value) Unload();
        else if (_module == null) Init();
        else _module.Reload();
    }

    /// <summary>
    /// 取消加载，清空通用文本资源及译文登记，并重置显示 Hook 状态。
    /// Cancel loading, clear general-text resources and recorded outputs, and reset display-hook state.
    /// </summary>
    public static void Unload()
    {
        _module?.Dispose();
        _module = null;
        _catalog = TranslationCatalog.Empty;
        _displayHooksInstalled = false;
        TranslatedTexts.Clear();
    }

    /// <summary>
    /// 发布新的通用文本快照，并清空本模块上一轮的译文登记。
    /// Publish a new general-text snapshot and clear this module's previously recorded outputs.
    /// </summary>
    /// <param name="result">成功完成的通用文本加载结果 / The successfully loaded general-text resources.</param>
    private static void ApplyResult(TranslationLoadResult result)
    {
        _catalog = new TranslationCatalog(result.TextTranslations, result.TextRegexTranslations);
        TranslatedTexts.Clear();
        LogManager.Info($"[Text] Loaded/已加载 {ExactCount} exact, {RegexCount} regex, " +
                        $"{result.SkippedLines} skipped, {result.ElapsedMilliseconds} ms");
    }
}
