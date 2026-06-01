namespace MES.Core.DTOs;

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
    public decimal? InProcurementTons { get; set; }
    public decimal? TotalRemainingTons { get; set; }
    public int? EstDays { get; set; }
    public DateTime? EstDeadline { get; set; }
    public List<decimal> DateBucketTons { get; set; } = new();
}
