using System;
using CRC3D3.NotEnoughTranslator.Plugin.Manger;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;
using HarmonyLib;

namespace CRC3D3.NotEnoughTranslator.Plugin.Hooks.UI;

internal static class LocalizationReadyPatch
{
    /// <summary>
    /// 本地化就绪回调补丁的独立 Harmony ID。
    /// The dedicated Harmony ID for localization-readiness callbacks.
    /// </summary>
    public const string HarmonyId =
        "github.meidopromotionassociation.crc3d3.notenoughtranslator.plugin.hooks.ui.localizationreadypatch";

    private static Harmony _harmony;

    /// <summary>
    /// 挂接游戏本地化就绪回调，使待处理的 UI 刷新无需逐帧轮询。
    /// Hook localization setup completion so pending UI refreshes require no per-frame polling.
    /// </summary>
    public static void Install()
    {
        if (_harmony != null || LocalizeManager.isSetupCompleted) return;
        var target = AccessTools.PropertySetter(typeof(LocalizeManager), nameof(LocalizeManager.isSetupCompleted));
        if (target == null)
        {
            LogManager.Warning("[UI] Localization setup callback unavailable/无法挂接本地化就绪回调");
            return;
        }

        var harmony = new Harmony(HarmonyId);
        try
        {
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(UITranslateManager),
                nameof(UITranslateManager.RefreshIfReady)));
            _harmony = harmony;
        }
        catch (Exception exception)
        {
            harmony.UnpatchSelf();
            LogManager.Warning($"[UI] Cannot install localization setup callback/无法安装本地化就绪回调: {exception}");
        }
    }

    /// <summary>
    /// 移除插件自身的本地化就绪补丁。
    /// Remove the plugin's localization-readiness patch.
    /// </summary>
    public static void Uninstall()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}
