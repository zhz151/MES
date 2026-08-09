using System.ComponentModel.DataAnnotations;

namespace MES.Data.Entities.Configuration;

/// <summary>
/// 工序组定义：定义各工序组的稳定 Key 与可改名中文名、启停、排序及分类标记，
/// 替代 ProcessNames 常量类的硬编码，支持管理员页面动态配置（新增/启停/排序/改名）。
/// 冷轧类判定（IsColdRoll/IsColdDraw）配置化，新增冷轧/冷拔工序只需勾选标记。
/// </summary>
public class ProcessDefinition : BaseEntity
{
    /// <summary>
    /// 稳定 Key（对应 ProcessKeys 常量名，如 ColdRoll60），程序识别工序用，不受改名影响。
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ProcessKey { get; set; } = string.Empty;

    /// <summary>工序组中文名（可改名，显示用）</summary>
    [Required]
    [MaxLength(50)]
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>显示顺序（升序排列，1 排最前）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>是否启用（false 表示隐藏，界面不显示该工序）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>是否冷轧系列（含三辊冷轧）</summary>
    public bool IsColdRoll { get; set; }

    /// <summary>是否冷拔工序</summary>
    public bool IsColdDraw { get; set; }

    /// <summary>
    /// 默认工段（JSON 数组字符串，存 SectionKey 列表，如 ["Straighten","Cut","Pickle","OuterPolish","OuterSpotGrinding","Inspection"]）。
    /// 计划页新增该工序行时自动填充的默认工段；null/空数组表示无默认工段。
    /// </summary>
    [MaxLength(500)]
    public string? DefaultSections { get; set; }

    /// <summary>说明</summary>
    [MaxLength(200)]
    public string? Remark { get; set; }
}
