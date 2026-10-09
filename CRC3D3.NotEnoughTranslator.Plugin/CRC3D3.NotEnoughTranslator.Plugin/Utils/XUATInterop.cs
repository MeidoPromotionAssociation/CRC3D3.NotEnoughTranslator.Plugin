using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using CRC3D3.NotEnoughTranslator.Plugin.Manger;
using HarmonyLib;

namespace CRC3D3.NotEnoughTranslator.Plugin.Utils;

public static class XUATInterop
{
    /// <summary>
    /// XUAT 互操作判定补丁的独立 Harmony ID。
    /// The dedicated Harmony ID for XUAT interoperability decision patches.
    /// </summary>
    public const string HarmonyId =
        "github.meidopromotionassociation.crc3d3.notenoughtranslator.plugin.utils.xuatinterop";

    private const string AssemblyName = "XUnity.AutoTranslator.Plugin.Core";
    private static readonly string[] DecisionTypes =
    {
        "XUnity.AutoTranslator.Plugin.Core.Utilities.LanguageHelper",
        "XUnity.AutoTranslator.Plugin.Core.TextTranslationCache",
        "XUnity.AutoTranslator.Plugin.Core.CompositeTextTranslationCache"
    };

    private static volatile Harmony _harmony;
    private static volatile bool _listening;
    private static SynchronizationContext _mainThreadContext;
    private static int _decisionWarningLogged;

    public static bool IsInstalled => _harmony != null;
    public static int PatchedMethodCount { get; private set; }

    /// <summary>
    /// 在 Unity 主线程初始化可选互操作，并订阅晚加载程序集以按事件重试。
    /// Initialize optional interop on Unity's main thread and subscribe to late assembly loads for event-driven retries.
    /// </summary>
    public static void Init()
    {
        if (NotEnoughTranslator.EnableXUATInterop?.Value != true) return;
        if (!_listening)
        {
            _mainThreadContext = SynchronizationContext.Current;
            if (_mainThreadContext == null)
            {
                LogManager.Warning("[XUAT] Main-thread synchronization context unavailable/主线程同步上下文不可用");
                return;
            }
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            _listening = true;
        }

        if (!IsInstalled) TryInstall();
    }

    /// <summary>
    /// 根据当前配置初始化或卸载 XUAT 互操作。
    /// Initialize or unload XUAT interoperability according to the current configuration.
    /// </summary>
    public static void Reload()
    {
        if (NotEnoughTranslator.EnableXUATInterop?.Value == true) Init();
        else Unload();
    }

    /// <summary>
    /// 停止程序集监听并移除 NET 自身的互操作补丁，阻止排队回调重新安装。
    /// Stop assembly monitoring and remove NET-owned interop patches, preventing queued callbacks from reinstalling them.
    /// </summary>
    public static void Unload()
    {
        if (_listening)
        {
            _listening = false;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        }

        Interlocked.Exchange(ref _decisionWarningLogged, 0);
        var harmony = _harmony;
        _harmony = null;
        PatchedMethodCount = 0;
        try
        {
            harmony?.UnpatchSelf();
        }
        catch (Exception exception)
        {
            LogManager.Warning($"[XUAT] Cannot remove interop hooks/无法移除互操作补丁: {exception.Message}");
        }
    }

    /// <summary>
    /// 检测 XUAT 程序集加载，并将补丁安装请求投递到 Unity 主线程。
    /// Detect the XUAT assembly loading and dispatch a patch-installation request to Unity's main thread.
    /// </summary>
    /// <param name="sender">触发加载事件的应用程序域 / The application domain raising the load event.</param>
    /// <param name="eventArgs">包含已加载程序集的事件参数 / Event arguments containing the loaded assembly.</param>
    private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs eventArgs)
    {
        if (!_listening || eventArgs.LoadedAssembly.GetName().Name != AssemblyName) return;
        _mainThreadContext.Post(_ =>
        {
            if (_listening && !IsInstalled && NotEnoughTranslator.EnableXUATInterop?.Value == true)
                TryInstall();
        }, null);
    }

    /// <summary>
    /// 查找兼容的 XUAT 判定方法并安装前置补丁，失败时回滚本次安装。
    /// Find compatible XUAT decision methods and install prefixes, rolling back this installation on failure.
    /// </summary>
    private static void TryInstall()
    {
        Harmony harmony = null;
        try
        {
            Assembly assembly = null;
            foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (loadedAssembly.GetName().Name != AssemblyName) continue;
                assembly = loadedAssembly;
                break;
            }

            if (assembly == null)
            {
                LogManager.Info("[XUAT] Plugin not detected; interop inactive/未检测到插件，互操作未启用");
                return;
            }

            var targets = new List<MethodInfo>();
            foreach (var typeName in DecisionTypes)
            {
                var type = assembly.GetType(typeName, false);
                if (type == null) continue;
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                      BindingFlags.Static | BindingFlags.Instance |
                                                      BindingFlags.DeclaredOnly))
                {
                    if (method.Name != "IsTranslatable" || method.ReturnType != typeof(bool) ||
                        method.ContainsGenericParameters || method.IsAbstract) continue;
                    var parameters = method.GetParameters();
                    if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string)) continue;
                    targets.Add(method);
                }
            }

            if (targets.Count == 0)
            {
                LogManager.Warning("[XUAT] No compatible IsTranslatable methods; interop inactive/未找到兼容的判定方法，互操作未启用");
                return;
            }

            harmony = new Harmony(HarmonyId);
            var prefix = new HarmonyMethod(typeof(XUATInterop), nameof(IsTranslatablePrefix));
            foreach (var target in targets)
                harmony.Patch(target, prefix: prefix);

            PatchedMethodCount = targets.Count;
            Interlocked.Exchange(ref _decisionWarningLogged, 0);
            _harmony = harmony;
            LogManager.Info($"[XUAT] Interop enabled/互操作已启用: {PatchedMethodCount} decision hooks/判定补丁");
        }
        catch (Exception exception)
        {
            try
            {
                harmony?.UnpatchSelf();
            }
            catch (Exception cleanupException)
            {
                LogManager.Warning($"[XUAT] Cannot roll back hooks/无法回滚补丁: {cleanupException.Message}");
            }

            _harmony = null;
            PatchedMethodCount = 0;
            LogManager.Warning($"[XUAT] Interop unavailable; XUAT left in control/互操作不可用，交由 XUAT 处理: {exception.Message}");
        }
    }

    /// <summary>
    /// 阻止 XUAT 重译已登记输出，或翻译已由 NET 显示 Hook 接管且存在译文的原文。
    /// Prevent XUAT from retranslating recorded outputs or translating sources covered by NET display hooks and translations.
    /// </summary>
    /// <param name="__0">XUAT 待判定的文字 / The text whose translatability XUAT is checking.</param>
    /// <param name="__result">拦截时置为 false，表示 XUAT 不应翻译 / Set to false when intercepted to tell XUAT not to translate.</param>
    /// <returns>保留 XUAT 原方法时为 true，已完成拦截判定时为 false / True to run XUAT's original method; false when NET handles the decision.</returns>
    private static bool IsTranslatablePrefix(string __0, ref bool __result)
    {
        if (!IsInstalled || string.IsNullOrEmpty(__0)) return true;
        try
        {
            if (!TextTranslateManager.IsNetTranslatedText(__0) &&
                !UITranslateManager.IsNetTranslatedText(__0) &&
                !(TextTranslateManager.AreDisplayHooksInstalled &&
                  TextTranslateManager.IsInTranslationDictionary(__0))) return true;

            __result = false;
            return false;
        }
        catch (Exception exception)
        {
            if (Interlocked.Exchange(ref _decisionWarningLogged, 1) == 0)
                LogManager.Warning($"[XUAT] Decision failed; original behavior preserved/判定失败，保留原行为: {exception.Message}");
            return true;
        }
    }
}
