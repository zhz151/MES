using System.ComponentModel.DataAnnotations;

namespace MES.Data.Entities.Configuration;

/// <summary>
/// 日产估算：以成品外径区分日产能（吨/天），
/// 用于计算产能工量 = 主号汇总总量 / 日产估算
/// </summary>
public class DailyOutputEstimate : BaseEntity
{
    /// <summary>最小外径（mm），查找时取 MinOuterDiameter &lt;= 实际外径 的最大值</summary>
    public decimal MinOuterDiameter { get; set; }

    /// <summary>日产能力（吨/天）</summary>
    public decimal DailyOutputTons { get; set; }

    /// <summary>说明</summary>
    [MaxLength(200)]
    public string? Remark { get; set; }
}
