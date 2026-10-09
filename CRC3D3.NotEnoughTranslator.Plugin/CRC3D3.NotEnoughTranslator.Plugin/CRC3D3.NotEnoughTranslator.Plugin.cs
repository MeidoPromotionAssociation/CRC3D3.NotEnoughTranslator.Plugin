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

    public static readonly string TranslationRootPath = Path.Combine(Paths.BepInExRootPath, "NotEnoughTranslator");
    public static readonly string TranslationPath = TranslationRootPath;
    public static readonly string TextureReplacePath = Path.Combine(TranslationRootPath, "Textures");

    public static ConfigEntry<LogLevel> LogLevelConfig;
    public static ConfigEntry<bool> AllowFilesInZipLoadInOrder;
    public static ConfigEntry<KeyboardShortcut> ReloadTranslateResourceShortcut;
    public static ConfigEntry<bool> EnableUITranslation;
    public static ConfigEntry<bool> EnableTextTranslation;
    public static ConfigEntry<bool> EnableTextureReplacement;
    public static ConfigEntry<int> RegexTimeoutMilliseconds;
    public static ConfigEntry<bool> EnableXUATInterop;

    private SynchronizationContext _mainThreadContext;
    private int _reloadRequested;
    private bool _destroyed;

    /// <summary>
    /// 初始化配置、主线程上下文和翻译模块。
    /// Initialize configuration, the main-thread context, and translation modules.
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
            "Index replacement images under Textures/索引 Textures 目录内的替换图片");

        AllowFilesInZipLoadInOrder.SettingChanged += OnResourceSettingsChanged;
        EnableUITranslation.SettingChanged += OnResourceSettingsChanged;
        EnableTextTranslation.SettingChanged += OnResourceSettingsChanged;
        EnableTextureReplacement.SettingChanged += OnResourceSettingsChanged;
        RegexTimeoutMilliseconds.SettingChanged += OnResourceSettingsChanged;
        EnableXUATInterop.SettingChanged += OnResourceSettingsChanged;

        Logger.LogInfo($"{PluginName} {PluginVersion} is loading/正在载入");
        Logger.LogInfo("Translation data is not included/本插件不附带翻译数据");
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
    /// 逐帧仅检查重载快捷键，资源处理由回调驱动。
    /// Check only the reload shortcut each frame; resource work is callback-driven.
    /// </summary>
    private void Update()
    {
        if (ReloadTranslateResourceShortcut.Value.IsDown()) RequestReload();
    }

    /// <summary>
    /// 取消事件订阅并卸载模块和插件自身的补丁。
    /// Unsubscribe from events and unload modules and plugin-owned patches.
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
        XUATInterop.Unload();
        UITranslateManager.Shutdown();
        TextTranslateManager.Unload();
        TextureReplaceManager.Unload();
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
            XUATInterop.Reload();
            UITranslateManager.Reload();
            TextTranslateManager.Reload();
            TextureReplaceManager.Reload();
        }, null);
    }
}
