using System;

namespace CRC3D3.NotEnoughTranslator.Plugin.Translation;

/// <summary>
/// 表示 KAG 文本的实际语境，或词条要求匹配的可选条件。
/// Represents the context of KAG text or the optional conditions required by an entry.
/// </summary>
internal readonly struct KagTextContext : IEquatable<KagTextContext>
{
    private readonly (string Filename, string Label, string Speaker, string Voice, string Target, int? Line) _fields;

    public string Filename => _fields.Filename;
    public string Label => _fields.Label;
    public string Speaker => _fields.Speaker;
    public string Voice => _fields.Voice;
    public string Target => _fields.Target;
    public int? Line => _fields.Line;
    public bool IsEmpty => Equals(default(KagTextContext));

    public (int Count, bool Filename, bool Label, bool Voice, bool Target, bool Speaker, bool Line) Priority
    {
        get
        {
            var hasFilename = Filename != null;
            var hasLabel = Label != null;
            var hasVoice = Voice != null;
            var hasTarget = Target != null;
            var hasSpeaker = Speaker != null;
            var hasLine = Line.HasValue;
            var count = (hasFilename ? 1 : 0) + (hasLabel ? 1 : 0) + (hasVoice ? 1 : 0) +
                        (hasTarget ? 1 : 0) + (hasSpeaker ? 1 : 0) + (hasLine ? 1 : 0);
            return (count, hasFilename, hasLabel, hasVoice, hasTarget, hasSpeaker, hasLine);
        }
    }

    /// <summary>
    /// 保存原始语境值，null 表示缺少该字段或词条未指定此条件。
    /// Store raw context values, using null for unavailable fields or unspecified conditions.
    /// </summary>
    public KagTextContext(string filename, string label, string speaker, string voice, string target, int? line)
    {
        _fields = (filename, label, speaker, voice, target, line);
    }

    /// <summary>
    /// 对词条指定的每个字段做精确匹配。
    /// Match every field specified by the entry exactly.
    /// </summary>
    public bool Matches(KagTextContext actual)
    {
        return (Filename == null || Filename == actual.Filename) &&
               (Label == null || Label == actual.Label) &&
               (Speaker == null || Speaker == actual.Speaker) &&
               (Voice == null || Voice == actual.Voice) &&
               (Target == null || Target == actual.Target) &&
               (!Line.HasValue || Line == actual.Line);
    }

    /// <summary>
    /// 逐字段比较两个语境，供词条覆盖和导出去重使用。
    /// Compare contexts field by field for entry replacement and dump deduplication.
    /// </summary>
    public bool Equals(KagTextContext other) => _fields.Equals(other._fields);

    /// <summary>
    /// 比较另一个对象是否表示相同的 KAG 语境。
    /// Check whether another object represents the same KAG context.
    /// </summary>
    public override bool Equals(object obj) => obj is KagTextContext other && Equals(other);

    /// <summary>
    /// 计算与逐字段相等规则一致的散列值。
    /// Compute a hash code consistent with field-by-field equality.
    /// </summary>
    public override int GetHashCode() => _fields.GetHashCode();
}
