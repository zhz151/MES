namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 订单总况 DTO
/// </summary>
public class ProductionOverviewDto
{
    public List<OverviewRowDto> Rows { get; set; } = new();
    public List<DateBucketDto> DateBuckets { get; set; } = new();
    public DateTime GeneratedTime { get; set; }
}

public class DateBucketDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Label { get; set; } = null!;
}

public class OverviewRowDto
{
    public int Seq { get; set; }
    public string Category { get; set; } = null!;
    public string Section { get; set; } = null!;

    /// <summary>大类序号（1原料/2生产/3成检；0=总估算不编号）</summary>
    public int CategoryNo { get; set; }

    /// <summary>大类内明细行序号（汇总行/总估算行不使用）</summary>
    public int RowNo { get; set; }
    public decimal? PendingPlanTons { get; set; }
    public decimal? InProcurementTons { get; set; }
    public decimal? TotalRemainingTons { get; set; }

    /// <summary>待产量附加量-在制（投料-在产 冷轧5060/2030/三辊/冷拔行按待生产产类拆分的在制部分；仅 4 行有值，其余为 null）</summary>
    public decimal? PendingInProgressTons { get; set; }

    /// <summary>待产量附加量-成品（同上，成品部分）</summary>
    public decimal? PendingFinishedTons { get; set; }
    public int? EstDays { get; set; }
    public DateTime? EstDeadline { get; set; }
    public List<decimal> DateBucketTons { get; set; } = new();

    /// <summary>日期桶副值（订单延期-原料/生产/成检行的投料缺少量/理论成品重量、订单延期量行的超1周量；无副值行为空列表）</summary>
    public List<decimal?> DateBucketSubTons { get; set; } = new();

    /// <summary>日期桶副值中文前缀（待料/在产/在检/超1周）</summary>
    public string? SubValuePrefix { get; set; }

    /// <summary>副值是否使用括号式（主值(前缀副值)），false 用斜杠式（主值/前缀副值）。订单延期量行「超1周」用括号式，延期分类行用斜杠式。</summary>
    public bool SubValueParenFormat { get; set; }

    /// <summary>是否为类别汇总行（原料/投料-在产/投料-成检小计）</summary>
    public bool IsSummary { get; set; }
}
