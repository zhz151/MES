namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 段落分类明细 — 每个(工序组, 工段)组合对应一个变异量系数
/// </summary>
public class SectionFlowCategoryItem : BaseEntity
{
    public int SettingId { get; set; }

    public string ProcessGroupName { get; set; } = null!;

    public string SectionName { get; set; } = null!;

    /// <summary>变异量系数</summary>
    public decimal Coefficient { get; set; }

    /// <summary>排序号</summary>
    public int DisplayOrder { get; set; }

    public SectionFlowCategorySetting Setting { get; set; } = null!;
}
