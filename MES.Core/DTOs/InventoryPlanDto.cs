namespace MES.Core.DTOs;

/// <summary>
/// 库存使用计划 DTO
/// </summary>
public class InventoryPlanDto
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public DateTime PlanDate { get; set; }
    public string InventoryBatchNo { get; set; } = null!;
    public string BatchNo { get; set; } = null!;
    public string MaterialType { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public string? LocationArea { get; set; }
    public string? LocationRack { get; set; }
    public int InputMultiple { get; set; }
    public string UsageMode { get; set; } = null!;
    public int? UsedQuantity { get; set; }
    public decimal UsedWeight { get; set; }
    public DateTime? RequiredDate { get; set; }
    public int PlanStatus { get; set; }
    public string PlanStatusText { get; set; } = null!;
    public string? Remark { get; set; }
    public string? ReworkType { get; set; }
    public string? ReworkTypeText { get; set; }
    public string? ProcessPlan { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public string CreatedBy { get; set; } = null!;
}

/// <summary>
/// 创建库存使用计划请求
/// </summary>
public class CreateInventoryPlanRequest
{
    public int WorkOrderId { get; set; }
    public DateTime PlanDate { get; set; }
    public string InventoryBatchNo { get; set; } = null!;
    public string MaterialType { get; set; } = null!;
    public string? LocationArea { get; set; }
    public string? LocationRack { get; set; }
    public int InputMultiple { get; set; } = 1;
    public string UsageMode { get; set; } = "All";
    public int? UsedQuantity { get; set; }
    public decimal UsedWeight { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Remark { get; set; }
    public string? ReworkType { get; set; }
    public string? ProcessPlan { get; set; }
}

/// <summary>
/// 可用库存批次 DTO（展示给用户选择）
/// </summary>
public class AvailableInventoryBatchDto
{
    public int Id { get; set; }
    public string BatchNo { get; set; } = null!;
    public string MaterialType { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public string? LengthStatus { get; set; }
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int RemainingQuantity { get; set; }
    public decimal RemainingWeight { get; set; }
    public decimal? UnitWeight { get; set; }
    public string? SurfaceCondition { get; set; }
    public string? LocationArea { get; set; }
    public string? LocationRack { get; set; }
    public decimal? ActualOuterDiameter { get; set; }
    public decimal? ActualWallThickness { get; set; }
}
