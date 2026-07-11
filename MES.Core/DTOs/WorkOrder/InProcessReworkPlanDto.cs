namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 在产改制计划 DTO
/// </summary>
public class InProcessReworkPlanDto
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public DateTime PlanDate { get; set; }
    public int ProductionBatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public string? BatchTagNo { get; set; }
    public string MaterialName { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public string LengthStatus { get; set; } = null!;
    public int InputMultiple { get; set; }
    public int? UsedQuantity { get; set; }
    public decimal UsedWeight { get; set; }
    public DateTime? RequiredDate { get; set; }
    public int PlanStatus { get; set; }
    public string PlanStatusText { get; set; } = null!;
    public string? Remark { get; set; }
    public string ReworkType { get; set; } = null!;
    public string ReworkTypeText { get; set; } = null!;
    public int StandardCycle { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public string CreatedBy { get; set; } = null!;
}

/// <summary>
/// 创建在产改制计划请求
/// </summary>
public class CreateInProcessReworkPlanRequest
{
    public int WorkOrderId { get; set; }
    public DateTime PlanDate { get; set; }
    public int ProductionBatchId { get; set; }
    public int InputMultiple { get; set; } = 1;
    public int? UsedQuantity { get; set; }
    public decimal UsedWeight { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Remark { get; set; }
    public string ReworkType { get; set; } = null!;
}

/// <summary>
/// 可用在产批次 DTO（展示给用户选择）
/// </summary>
public class AvailableInProcessBatchDto
{
    /// <summary>批次ID</summary>
    public int Id { get; set; }
    /// <summary>批次号</summary>
    public string BatchNo { get; set; } = null!;
    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }
    /// <summary>物料名称</summary>
    public string MaterialName { get; set; } = null!;
    /// <summary>工厂牌号</summary>
    public string PlantGrade { get; set; } = null!;
    /// <summary>规格</summary>
    public string Specification { get; set; } = null!;
    /// <summary>长度状态</summary>
    public string LengthStatus { get; set; } = null!;
    /// <summary>批次总支数</summary>
    public int TotalQuantity { get; set; }
    /// <summary>批次总重量</summary>
    public decimal TotalWeight { get; set; }
    /// <summary>现有效原料支数</summary>
    public int? CurrentValidQty { get; set; }
    /// <summary>现有效原料重量(kg)</summary>
    public decimal? CurrentValidWeight { get; set; }
    /// <summary>来源库存批次号</summary>
    public string? SourceBatchNo { get; set; }
    /// <summary>原料类型</summary>
    public string? SourceMaterialType { get; set; }
    /// <summary>炉号</summary>
    public string? SourceHeatNo { get; set; }
    /// <summary>来源规格</summary>
    public string? SourceSpecification { get; set; }
    /// <summary>生产类型</summary>
    public string? ProductionType { get; set; }
    /// <summary>制造物品</summary>
    public string ManufacturingItem { get; set; } = null!;
}
