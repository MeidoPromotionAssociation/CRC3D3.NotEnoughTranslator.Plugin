using System;

namespace CRC3D3.NotEnoughTranslator.Plugin.Translation;

/// <summary>
/// 保存一次通用文本查询或词条的可选语境。
/// Holds the optional context of a general-text lookup or entry.
/// </summary>
internal readonly struct TextContext : IEquatable<TextContext>
{
    public KagTextContext Kag { get; }
    public UiTextContext Ui { get; }
    public bool IsEmpty => Kag.IsEmpty && Ui.IsEmpty;

    /// <summary>
    /// 使用剧情语境创建查询或词条条件。
    /// Create a lookup or entry condition from scenario context.
    /// </summary>
    public TextContext(KagTextContext kag)
    {
        Kag = kag;
        Ui = default;
    }

    /// <summary>
    /// 使用界面语境创建查询或词条条件。
    /// Create a lookup or entry condition from UI context.
    /// </summary>
    public TextContext(UiTextContext ui)
    {
        Kag = default;
        Ui = ui;
    }

    /// <summary>
    /// 在同类语境中匹配词条指定的字段。
    /// Match the entry's conditions within the same kind of context.
    /// </summary>
    public bool Matches(TextContext actual) =>
        !Kag.IsEmpty ? !actual.Kag.IsEmpty && Kag.Matches(actual.Kag) :
        !Ui.IsEmpty && !actual.Ui.IsEmpty && Ui.Matches(actual.Ui);

    /// <summary>
    /// 比较完整语境，供覆盖和导出去重使用。
    /// Compare complete contexts for replacement and dump deduplication.
    /// </summary>
    public bool Equals(TextContext other) => Kag.Equals(other.Kag) && Ui.Equals(other.Ui);

    /// <summary>
    /// 检查另一个对象是否表示相同的语境。
    /// Check whether another object represents the same context.
    /// </summary>
    public override bool Equals(object obj) => obj is TextContext other && Equals(other);

    /// <summary>
    /// 计算与完整语境相等规则一致的散列值。
    /// Compute a hash code consistent with context equality.
    /// </summary>
    public override int GetHashCode() => (Kag, Ui).GetHashCode();
}
