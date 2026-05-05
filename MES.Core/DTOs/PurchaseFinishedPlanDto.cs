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

    // ========== 工单冗余字段 ==========
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal OuterDiameterNegative { get; set; }
    public decimal OuterDiameterPositive { get; set; }
    public decimal WallThicknessNegative { get; set; }
    public decimal WallThicknessPositive { get; set; }
    public string LengthStatus { get; set; } = null!;
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public string DeliveryState { get; set; } = null!;
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

    // ========== 工单冗余字段 ==========
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal OuterDiameterNegative { get; set; }
    public decimal OuterDiameterPositive { get; set; }
    public decimal WallThicknessNegative { get; set; }
    public decimal WallThicknessPositive { get; set; }
    public string LengthStatus { get; set; } = null!;
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public string DeliveryState { get; set; } = null!;
}
