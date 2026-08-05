using System.ComponentModel.DataAnnotations;

namespace MES.Data.Entities.Configuration;

/// <summary>
/// 标准工量天数：定义各工段在各条件下的标准生产天数，
/// 替代 SectionDefs.GetStandardDays() 的硬编码，支持管理员页面动态配置。
/// </summary>
public class StandardWorkDay : BaseEntity
{
    /// <summary>工段名称，对应 SectionDefs 常量</summary>
    [Required]
    [MaxLength(50)]
    public string SectionName { get; set; } = string.Empty;

    /// <summary>
    /// 稳定 Key（对应 SectionDefs 常量名，如 ColdRollDraw），程序识别工段用，不受改名影响。
    /// null 表示历史数据尚未回填。
    /// </summary>
    [MaxLength(50)]
    public string? SectionKey { get; set; }

    /// <summary>英文名称</summary>
    [MaxLength(100)]
    public string? EnglishName { get; set; }

    /// <summary>显示顺序（升序排列，1 排最前）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>是否启用（false 表示隐藏，界面不显示该工段）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>牌号前缀（如 "3"），null 表示所有牌号通用</summary>
    [MaxLength(50)]
    public string? PlantGradePrefix { get; set; }

    /// <summary>标准天数</summary>
    public double StandardDays { get; set; }

    /// <summary>说明</summary>
    [MaxLength(200)]
    public string? Remark { get; set; }
}
