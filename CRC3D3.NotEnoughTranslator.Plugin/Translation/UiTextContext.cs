using System;

namespace CRC3D3.NotEnoughTranslator.Plugin.Translation;

/// <summary>
/// 表示界面文字的组件位置，或界面词条指定的可选匹配条件。
/// Represents a UI text component's location or the optional conditions of a UI entry.
/// </summary>
internal readonly struct UiTextContext : IEquatable<UiTextContext>
{
    private readonly (string Scene, string Path, string Component) _fields;

    public string Scene => _fields.Scene;
    public string Path => _fields.Path;
    public string Component => _fields.Component;
    public bool IsEmpty => Equals(default(UiTextContext));
    public (int Count, bool Path, bool Scene, bool Component) Priority =>
        ((Path == null ? 0 : 1) + (Scene == null ? 0 : 1) + (Component == null ? 0 : 1),
            Path != null, Scene != null, Component != null);

    /// <summary>
    /// 保存组件位置，null 表示字段缺失或词条省略了该条件。
    /// Store the component location, using null for unavailable or unspecified conditions.
    /// </summary>
    public UiTextContext(string scene, string path, string component) => _fields = (scene, path, component);

    /// <summary>
    /// 匹配词条指定的所有界面字段，区分大小写。
    /// Match all UI fields specified by the entry with case-sensitive comparisons.
    /// </summary>
    public bool Matches(UiTextContext actual) =>
        (Scene == null || Scene == actual.Scene) &&
        (Path == null || Path == actual.Path) &&
        (Component == null || Component == actual.Component);

    /// <summary>
    /// 逐字段比较界面语境，供覆盖和导出去重使用。
    /// Compare UI contexts field by field for replacement and dump deduplication.
    /// </summary>
    public bool Equals(UiTextContext other) => _fields.Equals(other._fields);

    /// <summary>
    /// 检查另一个对象是否表示相同的界面语境。
    /// Check whether another object represents the same UI context.
    /// </summary>
    public override bool Equals(object obj) => obj is UiTextContext other && Equals(other);

    /// <summary>
    /// 计算与逐字段相等规则一致的散列值。
    /// Compute a hash code consistent with field-by-field equality.
    /// </summary>
    public override int GetHashCode() => _fields.GetHashCode();
}
