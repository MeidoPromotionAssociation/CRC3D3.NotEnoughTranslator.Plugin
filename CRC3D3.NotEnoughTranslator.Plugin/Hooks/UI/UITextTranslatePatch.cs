using System;
using System.Collections.Generic;
using System.Reflection;
using CRC3D3.NotEnoughTranslator.Plugin.Manger;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;
using HarmonyLib;
using I2.Loc;
using UnityEngine;

namespace CRC3D3.NotEnoughTranslator.Plugin.Hooks.UI;

/// <summary>
/// I2 译文查询补丁集合：为未显式指定语言的查询返回 NET 译文。
/// 由 <see cref="UITranslateManager"/> 通过 PatchAll 一次性安装。
/// I2 translation-query patches that supply NET output for queries without an explicit language.
/// Installed in one go by <see cref="UITranslateManager"/> through PatchAll.
/// </summary>
[HarmonyPatch]
internal static class UITextTranslatePatch
{
    /// <summary>
    /// I2 译文查询补丁的独立 Harmony ID。
    /// The dedicated Harmony ID for I2 translation-query patches.
    /// </summary>
    public const string HarmonyId =
        "github.meidopromotionassociation.crc3d3.notenoughtranslator.plugin.hooks.ui.uitexttranslatepatch";

    private static readonly HashSet<string> FailedTerms = new(StringComparer.Ordinal);
    [ThreadStatic] private static int _explicitLanguageDepth;

    /// <summary>
    /// 按完整签名定位 I2 查询方法；`out string` 参数无法写进特性构造参数，因此改用目标方法解析。
    /// Resolve the I2 query method by its full signature; its `out string` parameter cannot appear in an attribute argument.
    /// </summary>
    /// <returns>命中的查询方法 / The resolved query method.</returns>
    /// <exception cref="MissingMethodException">签名不匹配时抛出，交给管理器回滚并停用 UI 模块 / Thrown on a signature mismatch so the manager can roll back and disable the UI module.</exception>
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        var target = AccessTools.Method(typeof(LocalizationManager), nameof(LocalizationManager.TryGetTranslation),
            new[]
            {
                typeof(string), typeof(Localize.AdvancedFormat), typeof(string).MakeByRefType(),
                typeof(bool), typeof(int), typeof(bool), typeof(bool), typeof(GameObject), typeof(string)
            });

        return target ?? throw new MissingMethodException(
            "[UI] Unsupported I2 translation signature; UI module disabled/不支持的 I2 签名，UI 模块已停用");
    }

    /// <summary>
    /// 清空翻译异常告警记录，允许资源重载后重新报告。
    /// Clear translation-failure warning records so failures can be reported again after reload.
    /// </summary>
    public static void ClearWarnings()
    {
        lock (FailedTerms) FailedTerms.Clear();
    }

    /// <summary>
    /// 记录显式指定语言的调用上下文，保护其嵌套查询不被覆盖。
    /// Track explicit-language call context to protect nested queries from translation overrides.
    /// </summary>
    /// <param name="overrideLanguage">显式指定的查询语言，null 表示未指定 / The explicit query language, or null if unspecified.</param>
    /// <param name="__state">本次调用是否增加了显式语言深度 / Whether this call increased the explicit-language depth.</param>
    [HarmonyPrefix]
    private static void Prefix(string overrideLanguage, out bool __state)
    {
        __state = overrideLanguage != null;
        if (__state) _explicitLanguageDepth++;
    }

    /// <summary>
    /// 恢复显式语言调用深度，并原样传递游戏查询产生的异常。
    /// Restore explicit-language call depth and preserve exceptions from the original query.
    /// </summary>
    /// <param name="__state">前置补丁记录的深度变更状态 / The depth-change state recorded by the prefix.</param>
    /// <param name="__exception">原查询的异常，成功时为 null / The original query exception, or null on success.</param>
    /// <returns>未经修改的原异常 / The original exception without modification.</returns>
    [HarmonyFinalizer]
    private static Exception Finalizer(bool __state, Exception __exception)
    {
        if (__state) _explicitLanguageDepth--;
        return __exception;
    }

    /// <summary>
    /// 为未显式指定语言的查询返回 NET 译文或按配置导出未翻译词条，失败时保留 I2 原结果。
    /// Return NET translations or optionally dump missing terms for queries without an explicit language, preserving I2 results on failure.
    /// </summary>
    /// <param name="Term">查询的 I2 词条键 / The queried I2 term key.</param>
    /// <param name="advancedFormat">可选的高级格式处理器 / The optional advanced-format processor.</param>
    /// <param name="Translation">原译文，处理成功后替换为 NET 译文 / The original translation, replaced with NET output on success.</param>
    /// <param name="FixForRTL">是否执行从右向左文字修正 / Whether to apply right-to-left text correction.</param>
    /// <param name="maxLineLengthForRTL">RTL 修正使用的最大行长度 / The maximum line length used for RTL correction.</param>
    /// <param name="ignoreRTLnumbers">RTL 修正是否忽略数字 / Whether RTL correction ignores numbers.</param>
    /// <param name="applyParameters">是否展开本地化参数 / Whether to expand localization parameters.</param>
    /// <param name="localParametersRoot">查找局部参数的对象根节点 / The root object used to resolve local parameters.</param>
    /// <param name="overrideLanguage">显式语言覆盖，非 null 时跳过 NET 翻译 / An explicit language override; non-null values bypass NET.</param>
    /// <param name="__result">I2 查询是否成功，返回 NET 译文时置为 true / I2 query success, set to true when NET supplies a translation.</param>
    [HarmonyPostfix]
    private static void Postfix(string Term, Localize.AdvancedFormat advancedFormat,
        ref string Translation, bool FixForRTL, int maxLineLengthForRTL, bool ignoreRTLnumbers,
        bool applyParameters, GameObject localParametersRoot, string overrideLanguage, ref bool __result)
    {
        if (overrideLanguage != null || _explicitLanguageDepth != 0) return;

        try
        {
            if (!UITranslateManager.TryGetTranslation(Term, out var translated))
            {
                if (NotEnoughTranslator.EnableTermDump.Value &&
                    !string.IsNullOrWhiteSpace(Term) && Term != "-")
                {
                    var originalTermData = LocalizationManager.GetTermData(Term);
                    if (originalTermData == null || originalTermData.TermType == eTermType.Text)
                        UITranslateManager.DumpTerm(Term, Translation);
                }
                return;
            }
            var termData = LocalizationManager.GetTermData(Term);
            if (termData != null && termData.TermType != eTermType.Text) return;

            if (applyParameters)
                LocalizationManager.ApplyLocalizationParams(ref translated, localParametersRoot);
            if (LocalizationManager.IsRight2Left && FixForRTL)
                translated = LocalizationManager.ApplyRTLfix(translated, maxLineLengthForRTL, ignoreRTLnumbers);
            if (advancedFormat != null)
                translated = advancedFormat.Format(translated);
            if (string.IsNullOrWhiteSpace(translated)) return;

            UITranslateManager.MarkTranslated(translated);
            Translation = translated;
            __result = true;
        }
        catch (Exception exception)
        {
            lock (FailedTerms)
                if (!FailedTerms.Add(Term ?? string.Empty)) return;
            LogManager.Warning($"[UI] Translation failed; original preserved/翻译处理失败，保留原结果: {Term}: {exception.Message}");
        }
    }
}
