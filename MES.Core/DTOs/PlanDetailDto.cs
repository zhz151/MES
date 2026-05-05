namespace MES.Core.DTOs;

/// <summary>
/// 用料计划详情（用于点击工单号自动填充采购订单行）
/// </summary>
public class PlanDetailDto
{
    public string WorkOrderNo { get; set; } = null!;
    public string MaterialCategory { get; set; } = null!;
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public decimal? UnitWeight { get; set; }
    public int? Quantity { get; set; }
    public decimal Weight { get; set; }
    public string? Remark { get; set; }
    public DateTime? RequiredDate { get; set; }
}
