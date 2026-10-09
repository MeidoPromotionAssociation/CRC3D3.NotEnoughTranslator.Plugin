using System;
using System.Collections.Generic;

namespace CRC3D3.NotEnoughTranslator.Plugin.Translation;

/// <summary>
/// 线程安全地登记 NET 已返回的译文，供 XUAT 互操作识别并防止重复翻译；仅保存译文集合，不缓存原文到译文的映射。
/// Records NET translation outputs in a thread-safe set for XUAT retranslation prevention, without caching source-to-translation mappings.
/// </summary>
internal sealed class TranslatedTextRegistry
{
    private readonly HashSet<string> _texts = new(StringComparer.Ordinal);

    /// <summary>
    /// 以 Ordinal 字符串比较规则，线程安全地检查已登记译文。
    /// Check recorded translations with thread-safe Ordinal string matching.
    /// </summary>
    /// <param name="text">待检查的译文 / The translated text to check.</param>
    /// <returns>非空文字已登记时为 true，否则为 false / True if non-empty text is recorded; otherwise false.</returns>
    public bool Contains(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        lock (_texts) return _texts.Contains(text);
    }

    /// <summary>
    /// 线程安全地登记非空译文，不保存原文到译文的映射。
    /// Record non-empty translations safely across threads without storing source-to-translation mappings.
    /// </summary>
    /// <param name="text">需要防止重复翻译的输出文字 / The output text to protect from retranslation.</param>
    public void Add(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        lock (_texts) _texts.Add(text);
    }

    /// <summary>
    /// 线程安全地清空当前模块的全部译文登记。
    /// Clear all recorded translations for the current module in a thread-safe manner.
    /// </summary>
    public void Clear()
    {
        lock (_texts) _texts.Clear();
    }
}
