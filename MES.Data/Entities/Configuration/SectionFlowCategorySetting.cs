namespace MES.Data.Entities.Configuration;

/// <summary>
/// 流转类别日产配置 — 每个流转类别一行，存储用户可编辑的参数。
/// 类别包含的(工序组,工段,产类)组合由组合归类表 CombinationGroups 承载。
/// </summary>
public class SectionFlowCategorySetting : BaseEntity
{
    public string CategoryName { get; set; } = null!;

    /// <summary>展示序号（汇总表显示顺序）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>日产设定（用户编辑）</summary>
    public decimal? DailyProductionTarget { get; set; }

    /// <summary>偏少天数值（用户编辑）</summary>
    public decimal? LowerLimitDays { get; set; }

    /// <summary>过多天数值（用户编辑）</summary>
    public decimal? UpperLimitDays { get; set; }

    public string? Remark { get; set; }
}
