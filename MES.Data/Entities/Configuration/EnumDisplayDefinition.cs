namespace MES.Data.Entities.Configuration;

/// <summary>
/// 枚举显示配置表：管理所有 C# 强类型枚举的中文显示名与排序（不改值域）。
/// EnumKey = 枚举类型名（typeof(T).Name），Value = 枚举值名（Enum.ToString()，稳定英文名）。
/// 显示层"配置表优先 → EnumHelper 静态字典兜底"；改名后 DataExchange 导出新中文，导入可反向解析。
/// </summary>
public class EnumDisplayDefinition : BaseEntity
{
    /// <summary>枚举标识（枚举类型名，如 "BatchStatus"）</summary>
    public string EnumKey { get; set; } = string.Empty;

    /// <summary>枚举值名（Enum.ToString()，如 "InProgress"），稳定英文名</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>可改名中文显示</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>显示顺序（升序，1 排最前）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>说明</summary>
    public string? Remark { get; set; }
}
