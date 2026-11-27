using System;
using System.Collections.Generic;
using System.Linq;

namespace CRC3D3.NotEnoughTranslator.Plugin.Translation;

/// <summary>
/// 保存精确译文与正则规则的查询快照，优先精确匹配，再按后加载规则优先查询，不缓存命中或未命中结果。
/// Holds a lookup snapshot of exact translations and regex rules, preferring exact matches and then later-loaded rules without caching hits or misses.
/// </summary>
public sealed class TranslationCatalog
{
    public static readonly TranslationCatalog Empty = new(new Dictionary<string, string>(),
        Array.Empty<RegexTranslationRule>());

    private readonly Dictionary<string, string> _translations;
    private readonly Dictionary<string, (TextContext Context, string Text)[]> _contextualTranslations;
    private readonly HashSet<string> _uiSources;
    private readonly int _contextualExactCount;
    private readonly RegexTranslationRule[] _rules;

    public int ExactCount => _translations.Count + _contextualExactCount;
    public int RegexCount => _rules.Length;

    /// <summary>
    /// 复制精确译文和规则列表，创建独立的查询快照。
    /// Copy exact translations and the rule list into a separate lookup snapshot.
    /// </summary>
    /// <param name="translations">按 Ordinal 规则复制的精确译文词典 / The exact translations copied with Ordinal key comparison.</param>
    /// <param name="rules">按加载顺序提供的正则规则 / Regex rules supplied in loading order.</param>
    public TranslationCatalog(IDictionary<string, string> translations,
        IEnumerable<RegexTranslationRule> rules)
        : this(translations, rules, null)
    {
    }

    /// <summary>
    /// 复制通用精确词条、语境词条和正则规则，创建独立的查询快照。
    /// Copy general exact entries, contextual entries, and regex rules into a separate lookup snapshot.
    /// </summary>
    internal TranslationCatalog(IDictionary<string, string> translations,
        IEnumerable<RegexTranslationRule> rules,
        IDictionary<(TextContext Context, string Original), string> contextualTranslations)
    {
        _translations = new Dictionary<string, string>(translations, StringComparer.Ordinal);
        _rules = new List<RegexTranslationRule>(rules).ToArray();
        if (contextualTranslations == null || contextualTranslations.Count == 0) return;

        _contextualExactCount = contextualTranslations.Count;
        _uiSources = new HashSet<string>(contextualTranslations.Keys.Where(key => !key.Context.Ui.IsEmpty)
            .Select(key => key.Original), StringComparer.Ordinal);
        _contextualTranslations = contextualTranslations.GroupBy(entry => entry.Key.Original, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(entry => entry.Key.Context.Kag.Priority)
                    .ThenByDescending(entry => entry.Key.Context.Ui.Priority)
                    .Select(entry => (entry.Key.Context, entry.Value)).ToArray(),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// 检查当前原文是否存在界面语境词条，避免为普通查询遍历 Unity 层级。
    /// Check for UI entries for this source to avoid walking the Unity hierarchy for general lookups.
    /// </summary>
    internal bool HasUiContext(string sourceText) => _uiSources?.Contains(sourceText) == true;

    /// <summary>
    /// 对同一原文按优先级检查语境条件，未命中时回退到通用词库。
    /// Check the source text's contextual entries in priority order and fall back to the general catalog on a miss.
    /// </summary>
    internal bool TryGetTranslation(string sourceText, TextContext context, out string translation)
    {
        if (_contextualTranslations == null || context.IsEmpty || string.IsNullOrEmpty(sourceText) ||
            !_contextualTranslations.TryGetValue(sourceText, out var candidates))
            return TryGetTranslation(sourceText, out translation);

        foreach (var candidate in candidates)
        {
            if (!candidate.Context.Matches(context)) continue;
            translation = candidate.Text;
            return true;
        }

        return TryGetTranslation(sourceText, out translation);
    }

    /// <summary>
    /// 先查询精确词条，再按后加载优先的顺序尝试正则规则。
    /// Look up exact entries first, then try regex rules with later-loaded rules taking precedence.
    /// </summary>
    /// <param name="sourceText">待查询的原文或 I2 词条键 / The source text or I2 term key to look up.</param>
    /// <param name="translation">命中时返回的精确译文或正则替换结果 / The exact translation or regex replacement returned on a match.</param>
    /// <returns>精确词条或正则规则命中时为 true，否则为 false / True if an exact entry or regex rule matches; otherwise false.</returns>
    public bool TryGetTranslation(string sourceText, out string translation)
    {
        translation = null;
        if (string.IsNullOrEmpty(sourceText)) return false;
        if (_translations.TryGetValue(sourceText, out translation)) return true;
        if (_rules.Length == 0) return false;

        for (var ruleIndex = _rules.Length - 1; ruleIndex >= 0; ruleIndex--)
        {
            if (!_rules[ruleIndex].TryReplace(sourceText, _translations, out translation)) continue;
            return true;
        }

        return false;
    }
}
