using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;

namespace CRC3D3.NotEnoughTranslator.Plugin.Translation;

/// <summary>
/// 封装编译后的正则模式与译文模板，支持捕获内容二次精确翻译，并在执行超时或参数错误时停用规则。
/// Encapsulates a compiled regex and translation template, supports secondary exact translation of captured content, and disables the rule on timeouts or argument errors.
/// </summary>
public sealed class RegexTranslationRule
{
    private static readonly Regex ReplacementTokens = new(
        @"\$(?:\$|&|`|'|\+|_|[0-9]+|\{[^}]+\})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private readonly Regex _regex;
    private readonly string _source;
    private int _disabled;

    public string Pattern => _regex.ToString();
    public string Replacement { get; }

    /// <summary>
    /// 编译带超时限制的正则，并保存替换模板和诊断来源。
    /// Compile a timeout-bounded regex and store its replacement template and diagnostic source.
    /// </summary>
    /// <param name="pattern">用于匹配原文的 .NET 正则模式 / The .NET regex pattern used to match source text.</param>
    /// <param name="replacement">使用 .NET 替换标记的译文模板 / The translation template using .NET replacement tokens.</param>
    /// <param name="timeout">单次正则匹配的超时时间 / The timeout for a regex match operation.</param>
    /// <param name="source">用于错误定位的规则来源 / The rule source used in diagnostics.</param>
    public RegexTranslationRule(string pattern, string replacement, TimeSpan timeout, string source)
    {
        _regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, timeout);
        Replacement = replacement ?? throw new ArgumentNullException(nameof(replacement));
        _source = source;
    }

    /// <summary>
    /// 应用正则替换，并对捕获引用的内容再做一次精确词条查询。
    /// Apply regex replacement and perform an additional exact lookup for captured references.
    /// </summary>
    /// <param name="input">待匹配和替换的原文 / The source text to match and replace.</param>
    /// <param name="translations">用于捕获内容二次翻译的精确词典 / The exact dictionary used for secondary translation of captured content.</param>
    /// <param name="translation">匹配成功时生成的替换结果 / The replacement result produced on a successful match.</param>
    /// <returns>匹配成功时为 true；未命中、已停用或执行失败时为 false / True on a match; false on a miss, a disabled rule, or an execution failure.</returns>
    public bool TryReplace(string input, IReadOnlyDictionary<string, string> translations,
        out string translation)
    {
        translation = null;
        if (Volatile.Read(ref _disabled) != 0) return false;

        try
        {
            var matched = false;
            var replaced = _regex.Replace(input, match =>
            {
                matched = true;
                return ReplacementTokens.Replace(Replacement, tokenMatch =>
                {
                    var token = tokenMatch.Value;
                    var value = match.Result(token);
                    if (IsCaptureReference(match, token) &&
                        translations.TryGetValue(value, out var capturedTranslation))
                        return capturedTranslation;

                    return value;
                });
            });

            if (!matched) return false;
            translation = replaced;
            return true;
        }
        catch (RegexMatchTimeoutException exception)
        {
            Disable(exception.Message);
        }
        catch (ArgumentException exception)
        {
            Disable(exception.Message);
        }

        return false;
    }

    /// <summary>
    /// 判断替换标记是否引用捕获内容，以决定是否执行二次翻译。
    /// Determine whether a replacement token refers to captured content eligible for secondary translation.
    /// </summary>
    /// <param name="match">当前正则匹配及其捕获组 / The current regex match and its capture groups.</param>
    /// <param name="token">已识别的 .NET 替换标记 / The recognized .NET replacement token.</param>
    /// <returns>标记引用可用于二次翻译的捕获内容时为 true / True if the token references captured content eligible for secondary translation.</returns>
    private static bool IsCaptureReference(Match match, string token)
    {
        switch (token[1])
        {
            case '&':
            case '+':
                return true;
            case '$':
            case '`':
            case '\'':
            case '_':
                return false;
            case '{':
                return match.Groups[token.Substring(2, token.Length - 3)].Success;
            default:
                return match.Groups[token.Substring(1)].Success;
        }
    }

    /// <summary>
    /// 停用当前规则并仅记录一次告警，资源重载后由新规则恢复处理。
    /// Disable this rule and log once; resource reloading creates a new active rule.
    /// </summary>
    /// <param name="reason">正则停用的诊断原因 / The diagnostic reason for disabling the regex.</param>
    private void Disable(string reason)
    {
        if (Interlocked.Exchange(ref _disabled, 1) != 0) return;
        LogManager.Warning($"[Text] Regex disabled until reload/正则规则已停用，重载后恢复: {_source}: {reason}");
    }
}
