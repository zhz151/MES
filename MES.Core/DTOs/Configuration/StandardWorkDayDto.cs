namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 标准工量天数 DTO
/// </summary>
public class StandardWorkDayDto
{
    public int Id { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string? SectionKey { get; set; }
    public string? EnglishName { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? PlantGradePrefix { get; set; }
    public double StandardDays { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 启用工段信息：展示层动态化用（列显隐/名称/顺序运行时从参数表加载）
/// </summary>
public class SectionInfoDto
{
    /// <summary>稳定 Key（对应 SectionDefs 常量名 / ProcessGroup 属性名），程序识别用</summary>
    public string SectionKey { get; set; } = string.Empty;

    /// <summary>显示名称（管理员可改名，界面显示此值）</summary>
    public string SectionName { get; set; } = string.Empty;

    /// <summary>显示顺序（升序，1 排最前）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }
}
