namespace MES.Core.DTOs;

/// <summary>
/// 成品采购计划 DTO
/// </summary>
public class PurchaseFinishedPlanDto
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public DateTime PlanDate { get; set; }
    public string ProductType { get; set; } = null!;
    public int? RequiredPiece { get; set; }
    public decimal RequiredWeight { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Remark { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public string CreatedBy { get; set; } = null!;
}

/// <summary>
/// 创建成品采购计划请求
/// </summary>
public class CreatePurchaseFinishedPlanRequest
{
    public int WorkOrderId { get; set; }
    public DateTime PlanDate { get; set; }
    public string ProductType { get; set; } = null!;
    public int? RequiredPiece { get; set; }
    public decimal RequiredWeight { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Remark { get; set; }
}
