namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 工艺卡打印版式配置 DTO（格式设置面板「打印版式」Tab 批量保存/加载用）。
/// Key 为唯一锚点（ProcessCardStyleKeys 之一），Value 为配置值字符串（字体族名或字号数字）。
/// </summary>
public class ProcessCardStyleDefinitionDto
{
    public int Id { get; set; }

    /// <summary>配置键：ProcessCardStyleKeys 之一</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>配置值（字体族名或字号数字字符串）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>显示名（可改中文）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>说明</summary>
    public string? Remark { get; set; }
}
