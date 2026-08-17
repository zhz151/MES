namespace MES.Data.Entities.Configuration;

/// <summary>
/// 工艺卡打印版式配置表：打印字体/字号键值对（Key 唯一），
/// 数据库全局共享（仿 ProcessCardColumnDefinition 模式）。
/// </summary>
public class ProcessCardStyleDefinition : BaseEntity
{
    /// <summary>配置键：ProcessCardStyleKeys 之一</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>配置值（字体族名或字号数字字符串）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>显示名（可改中文）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>说明</summary>
    public string? Remark { get; set; }
}
