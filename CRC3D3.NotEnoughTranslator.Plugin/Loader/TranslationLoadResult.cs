using System;
using System.Collections.Generic;
using CRC3D3.NotEnoughTranslator.Plugin.Translation;

namespace CRC3D3.NotEnoughTranslator.Plugin.Loader;

public sealed class TranslationLoadResult
{
    public readonly Dictionary<string, string> I2Terms = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string> TextTranslations = new(StringComparer.Ordinal);
    public readonly List<RegexTranslationRule> TextRegexTranslations = new();

    public long ElapsedMilliseconds;
    public int SkippedLines;
    public int TotalEntries;
    public int TotalFiles;

    public bool HasAnyEntry => I2Terms.Count + TextTranslations.Count + TextRegexTranslations.Count > 0;
}
