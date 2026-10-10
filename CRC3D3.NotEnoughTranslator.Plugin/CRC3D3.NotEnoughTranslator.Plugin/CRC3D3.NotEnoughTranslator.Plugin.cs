using System;
using System.IO;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CRC3D3.NotEnoughTranslator.Plugin.Manger;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;

namespace CRC3D3.NotEnoughTranslator.Plugin;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class NotEnoughTranslator : BaseUnityPlugin
{
    public const string PluginGuid = "Github.MeidoPromotionAssociation.CRC3D3.NotEnoughTranslator.Plugin";
    public const string PluginName = "CRC3D3.NotEnoughTranslator.Plugin";
    public const string PluginVersion = "0.0.1";

    /// <summary>
    /// 翻译资源与导出数据的公共根目录，不包含语言层级。
    /// The common root for translation resources and dumps, without a language directory.
    /// </summary>
    public static readonly string TranslationRootPath = Path.Combine(Paths.BepInExRootPath, "NotEnoughTranslator");

    /// <summary>
    /// 通用文本 JSONL 及其 ZIP 翻译包目录。
    /// The directory for general-text JSONL files and their ZIP packs.
    /// </summary>
    public static readonly string TranslationTextPath = Path.Combine(TranslationRootPath, "Text");

    /// <summary>
    /// 纹理替换图片目录，沿用 JAT 的单数目录名。
    /// The replacement-image directory, using JAT's singular folder name.
    /// </summary>
    public static readonly string TextureReplacePath = Path.Combine(TranslationRootPath, "Texture");

    /// <summary>
    /// UI 模块的资源根目录。
    /// The resource root for the UI module.
    /// </summary>
    public static readonly string UIPath = Path.Combine(TranslationRootPath, "UI");

    /// <summary>
    /// I2 词条 CSV 及其 ZIP 翻译包目录。
    /// The directory for I2 term CSV files and their ZIP packs.
    /// </summary>
    public static readonly string UITextPath = Path.Combine(UIPath, "Text");

    /// <summary>
    /// 仅用于导出的根目录，不参与翻译资源加载。
    /// The dump-only root, excluded from translation-resource loading.
    /// </summary>
    public static readonly string DumpPath = Path.Combine(TranslationRootPath, "Dump");

    /// <summary>
    /// UI 模块的导出根目录。
    /// The dump root for the UI module.
    /// </summary>
    public static readonly string DumpUIPath = Path.Combine(DumpPath, "UI");

    /// <summary>
    /// 未翻译 I2 词条的 CSV 导出目录。
    /// The CSV dump directory for untranslated I2 terms.
    /// </summary>
    public static readonly string TermDumpPath = Path.Combine(DumpUIPath, "Text");

    /// <summary>
    /// 未翻译通用文本的 JSONL 导出目录。
    /// The JSONL dump directory for untranslated general text.
    /// </summary>
    public static readonly string TextDumpPath = Path.Combine(DumpPath, "Text");

    /// <summary>
    /// 原始纹理 PNG 的导出目录。
    /// The dump directory for original textures encoded as PNG.
    /// </summary>
    public static readonly string TextureDumpPath = Path.Combine(DumpPath, "Texture");

    public static ConfigEntry<LogLevel> LogLevelConfig;
    public static ConfigEntry<bool> AllowFilesInZipLoadInOrder;
    public static ConfigEntry<KeyboardShortcut> ReloadTranslateResourceShortcut;
    public static ConfigEntry<bool> EnableUITranslation;
    public static ConfigEntry<bool> EnableTextTranslation;
    public static ConfigEntry<bool> EnableTextureReplacement;
    public static ConfigEntry<int> RegexTimeoutMilliseconds;
    public static ConfigEntry<bool> EnableXUATInterop;
    public static ConfigEntry<bool> EnableTermDump;
    public static ConfigEntry<bool> EnableTextDump;
    public static ConfigEntry<bool> EnableTexturesDump;
    public static ConfigEntry<int> TermDumpThreshold;
    public static ConfigEntry<int> TextDumpThreshold;
    public static ConfigEntry<KeyboardShortcut> FlushDumpShortcut;

    private SynchronizationContext _mainThreadContext;
    private int _reloadRequested;
    private bool _destroyed;

    /// <summary>
    /// 初始化配置、主线程上下文、导出设施和翻译模块。
    /// Initialize configuration, the main-thread context, dump infrastructure, and translation modules.
    /// </summary>
    private void Awake()
    {
        LogManager.Init(Logger);
        _mainThreadContext = SynchronizationContext.Current ??
            throw new InvalidOperationException("Unity main-thread synchronization context is unavailable.");
        LogLevelConfig = Config.Bind("2General", "LogLevel/日志等级", LogLevel.Info,
            "Log level/日志等级");
        AllowFilesInZipLoadInOrder = Config.Bind("2General",
            "AllowFilesInZipLoadInOrder/允许 ZIP 文件内文件按顺序加载", true,
            "Sort ZIP entries by ordinal filename; otherwise use archive order/按文件名排序 ZIP 条目，关闭则使用归档顺序");
        ReloadTranslateResourceShortcut = Config.Bind("2General", "ReloadTranslateResource/重载翻译资源",
            KeyboardShortcut.Empty, "Reload all enabled modules/重载所有启用模块的资源");
        EnableXUATInterop = Config.Bind("2General", "EnableXUATInterop/启用 XUAT 互操作", true,
            "Use JAT-style translation decisions without special markers/通过 JAT 式翻译判定与 XUAT 共存，不使用特殊标记");
        EnableUITranslation = Config.Bind("3UI", "Enabled/启用", true,
            "Override I2 text queries without changing the game language/覆盖 I2 文本查询，不改变游戏语言");
        EnableTextTranslation = Config.Bind("4Text", "Enabled/启用", true,
            "Load JSONL exact and regex translations/加载 JSONL 精确与正则翻译");
        RegexTimeoutMilliseconds = Config.Bind("4Text", "RegexTimeoutMilliseconds/正则超时毫秒", 100,
            new ConfigDescription("Disable a timed-out rule until reload/超时规则停用至下次重载",
                new AcceptableValueRange<int>(1, 5000)));
        EnableTextureReplacement = Config.Bind("5Textures", "Enabled/启用", true,
            "Index replacement images under Texture/索引 Texture 目录内的替换图片");
        EnableTermDump = Config.Bind("6Dump", "EnableTermDump/启用 UI 词条导出", false,
            "Dump untranslated I2 terms while the UI module is loaded/在 UI 模块加载后导出未翻译的 I2 词条");
        EnableTextDump = Config.Bind("6Dump", "EnableTextDump/启用通用文本导出", false,
            "Dump untranslated general-text queries as JSONL/将通用文本查询未命中的原文导出为 JSONL");
        EnableTexturesDump = Config.Bind("6Dump", "EnableTexturesDump/启用纹理导出", false,
            "Allow captured original PNG data to be dumped; capture hooks are not yet connected/允许导出已捕获的原始 PNG 数据，捕获 Hook 尚未接入");
        TermDumpThreshold = Config.Bind("6Dump", "TermDumpThreshold/UI 词条导出阈值", 100,
            new ConfigDescription("Flush after buffering this many new I2 terms/累计此数量的新 I2 词条后写入文件",
                new AcceptableValueRange<int>(1, 10000)));
        TextDumpThreshold = Config.Bind("6Dump", "TextDumpThreshold/通用文本导出阈值", 100,
            new ConfigDescription("Flush after buffering this many new source strings/累计此数量的新原文后写入文件",
                new AcceptableValueRange<int>(1, 10000)));
        FlushDumpShortcut = Config.Bind("6Dump", "FlushDump/写入导出缓冲", KeyboardShortcut.Empty,
            "Write pending UI and general-text dumps to disk/将待导出的 UI 词条和通用文本写入文件");

        AllowFilesInZipLoadInOrder.SettingChanged += OnResourceSettingsChanged;
        EnableUITranslation.SettingChanged += OnResourceSettingsChanged;
        EnableTextTranslation.SettingChanged += OnResourceSettingsChanged;
        EnableTextureReplacement.SettingChanged += OnResourceSettingsChanged;
        RegexTimeoutMilliseconds.SettingChanged += OnResourceSettingsChanged;
        EnableXUATInterop.SettingChanged += OnResourceSettingsChanged;
        EnableTermDump.SettingChanged += OnDumpSettingsChanged;
        EnableTextDump.SettingChanged += OnDumpSettingsChanged;
        EnableTexturesDump.SettingChanged += OnDumpSettingsChanged;
        TermDumpThreshold.SettingChanged += OnDumpSettingsChanged;
        TextDumpThreshold.SettingChanged += OnDumpSettingsChanged;

        Logger.LogInfo($"{PluginName} {PluginVersion} is loading/正在载入");
        Logger.LogInfo("Translation data is not included/本插件不附带翻译数据");
        DumpManager.Init();
        UITranslateManager.Init();
        TextTranslateManager.Init();
        TextureReplaceManager.Init();
    }

    /// <summary>
    /// 在插件启动阶段初始化可选的 XUAT 互操作。
    /// Initialize optional XUAT interoperability when the plugin starts.
    /// </summary>
    private void Start()
    {
        XUATInterop.Init();
    }

    /// <summary>
    /// 逐帧仅检查重载和导出落盘快捷键，资源处理由回调驱动。
    /// Check only reload and dump-flush shortcuts each frame; resource work is callback-driven.
    /// </summary>
    private void Update()
    {
        if (ReloadTranslateResourceShortcut.Value.IsDown()) RequestReload();
        if (FlushDumpShortcut.Value.IsDown()) DumpManager.Flush();
    }

    /// <summary>
    /// 取消事件订阅，卸载模块和插件自身的补丁，并写入待导出的数据。
    /// Unsubscribe from events, unload modules and plugin-owned patches, and flush pending dumps.
    /// </summary>
    private void OnDestroy()
    {
        _destroyed = true;
        if (AllowFilesInZipLoadInOrder != null)
            AllowFilesInZipLoadInOrder.SettingChanged -= OnResourceSettingsChanged;
        if (EnableUITranslation != null)
            EnableUITranslation.SettingChanged -= OnResourceSettingsChanged;
        if (EnableTextTranslation != null)
            EnableTextTranslation.SettingChanged -= OnResourceSettingsChanged;
        if (EnableTextureReplacement != null)
            EnableTextureReplacement.SettingChanged -= OnResourceSettingsChanged;
        if (RegexTimeoutMilliseconds != null)
            RegexTimeoutMilliseconds.SettingChanged -= OnResourceSettingsChanged;
        if (EnableXUATInterop != null)
            EnableXUATInterop.SettingChanged -= OnResourceSettingsChanged;
        if (EnableTermDump != null)
            EnableTermDump.SettingChanged -= OnDumpSettingsChanged;
        if (EnableTextDump != null)
            EnableTextDump.SettingChanged -= OnDumpSettingsChanged;
        if (EnableTexturesDump != null)
            EnableTexturesDump.SettingChanged -= OnDumpSettingsChanged;
        if (TermDumpThreshold != null)
            TermDumpThreshold.SettingChanged -= OnDumpSettingsChanged;
        if (TextDumpThreshold != null)
            TextDumpThreshold.SettingChanged -= OnDumpSettingsChanged;
        XUATInterop.Unload();
        UITranslateManager.Shutdown();
        TextTranslateManager.Unload();
        TextureReplaceManager.Unload();
        DumpManager.Shutdown();
    }

    /// <summary>
    /// 导出开关或阈值变化时写入已有缓冲，不触发翻译资源重载。
    /// Flush buffered dumps when dump switches or thresholds change, without reloading translations.
    /// </summary>
    /// <param name="sender">发生变化的导出配置项 / The dump configuration entry that changed.</param>
    /// <param name="eventArgs">配置变更事件参数 / Configuration-change event arguments.</param>
    private void OnDumpSettingsChanged(object sender, EventArgs eventArgs)
    {
        DumpManager.Flush();
    }

    /// <summary>
    /// 配置变化时请求重载，避免在事件线程直接操作 Unity。
    /// Request a reload after configuration changes without accessing Unity on the event thread.
    /// </summary>
    /// <param name="sender">发生变化的配置项 / The configuration entry that changed.</param>
    /// <param name="eventArgs">配置变更事件参数 / Configuration-change event arguments.</param>
    private void OnResourceSettingsChanged(object sender, EventArgs eventArgs)
    {
        RequestReload();
    }

    /// <summary>
    /// 合并待处理的重载请求，并投递到 Unity 主线程。
    /// Coalesce pending reload requests and dispatch them to Unity's main thread.
    /// </summary>
    private void RequestReload()
    {
        if (Interlocked.Exchange(ref _reloadRequested, 1) != 0) return;
        _mainThreadContext.Post(_ =>
        {
            Interlocked.Exchange(ref _reloadRequested, 0);
            if (_destroyed) return;
            DumpManager.Flush();
            XUATInterop.Reload();
            UITranslateManager.Reload();
            TextTranslateManager.Reload();
            TextureReplaceManager.Reload();
        }, null);
    }
}
