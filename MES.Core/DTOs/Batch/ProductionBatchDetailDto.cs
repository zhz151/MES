using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Batch;

/// <summary>
/// 生产批次详情DTO
/// </summary>
public class ProductionBatchDetailDto
{
    // ========== 批次自身 ==========
    public int Id { get; set; }
    public string BatchNo { get; set; } = null!;
    public BatchStatus Status { get; set; }
    public string StatusDisplay => EnumHelper.GetDisplayName(Status);
    public string? TagNo { get; set; }
    public ProductionType? ProductionType { get; set; }
    public MaterialType ManufacturingItem { get; set; }
    public string ManufacturingItemDisplay => EnumHelper.GetDisplayName(ManufacturingItem);
    public int ProductionRatio { get; set; }
    public bool IsForceCompleted { get; set; }
    public string? QualityRemark { get; set; }
    public string? SolutionParams { get; set; }
    public DateTime? CurrentExecDate { get; set; }
    public string? CurrentGroupName { get; set; }
    public string? CurrentSectionName { get; set; }
    public string? CurrentEquipmentName { get; set; }
    public string? CurrentOutsource { get; set; }
    public string? CurrentSpec { get; set; }
    public string? NextSectionName { get; set; }
    public string? CorrespondingSpec { get; set; }
    public string? NextProcess { get; set; }
    public bool? CurrentSectionCompleted { get; set; }
    public bool? HasInputChange { get; set; }
    public int RemainingWorkDays { get; set; }
    public int TotalWorkDays { get; set; }
    public string? Remark { get; set; }

    // ========== 工单冗余 ==========
    public string WorkOrderNo { get; set; } = null!;
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string? ProductionSubNo { get; set; }
    public string OrderItemIds { get; set; } = null!;
    public DateTime SignDate { get; set; }
    public string Salesman { get; set; } = null!;
    public string? EndCustomer { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool DelayPenalty { get; set; }
    public string MaterialName { get; set; } = null!;
    public SettlementMethod SettlementMethod { get; set; }
    public string SettlementMethodDisplay => EnumHelper.GetDisplayName(SettlementMethod);
    public string StandardCode { get; set; } = null!;
    public DeliveryState DeliveryState { get; set; }
    public string DeliveryStateDisplay => EnumHelper.GetDisplayName(DeliveryState);
    public DeliveryState? ManufacturingStatus { get; set; }
    public string? ManufacturingStatusDisplay => ManufacturingStatus.HasValue ? EnumHelper.GetDisplayName(ManufacturingStatus.Value) : null;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal OuterDiameterNegative { get; set; }
    public decimal OuterDiameterPositive { get; set; }
    public decimal WallThicknessNegative { get; set; }
    public decimal WallThicknessPositive { get; set; }
    public LengthStatus LengthStatus { get; set; }
    public string LengthStatusDisplay => EnumHelper.GetDisplayName(LengthStatus);
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalMeters { get; set; }
    public decimal TotalWeight { get; set; }

    /// <summary>
    /// 产品单支量(kg/支) = 总重量/总支数，仅"定尺"批次有效，非定尺为空；保留1位小数
    /// </summary>
    public decimal? ProductUnitWeight { get; set; }

    public int TotalItemCount { get; set; }
    public string? ItemDetails { get; set; }
    public string TechnicalRequirements { get; set; } = null!;

    // ========== 投料信息 ==========
    public string? SourceBatchNo { get; set; }
    public MaterialType? SourceMaterialType { get; set; }
    public string? SourceMaterialTypeDisplay => SourceMaterialType.HasValue ? EnumHelper.GetDisplayName(SourceMaterialType.Value) : null;
    public string? SourceName { get; set; }
    public string? SourceHeatNo { get; set; }
    public string? SourcePlantGrade { get; set; }
    public string? SourceSpecification { get; set; }
    public LengthStatus? SourceLengthStatus { get; set; }
    public decimal? SourceUnitWeight { get; set; }
    public int? InputQuantity { get; set; }
    public decimal? InputWeight { get; set; }
    public BatchInputType? InputType { get; set; }
    public string? SourceRemark { get; set; }
    public string? SourceProductionNo { get; set; }
    public int? CurrentValidQty { get; set; }
    public int? CurrentValidWeight { get; set; }
    public int? TheoreticalOutputQty { get; set; }
    public int? TheoreticalOutputWeight { get; set; }
    public decimal? TheoreticalUnitWeight { get; set; }

    // ========== 成检附加 ==========

    /// <summary>成检附加：仅"成检"状态有效——PreInspection=预检，FormalInspection=终检；其余状态/无到料为 null（空）</summary>
    public string? InspectionStage { get; set; }
    public string? InspectionStageDisplay => string.Equals(InspectionStage, nameof(InspectionType.PreInspection), StringComparison.OrdinalIgnoreCase)
        ? "预检"
        : string.Equals(InspectionStage, nameof(InspectionType.FormalInspection), StringComparison.OrdinalIgnoreCase)
            ? "终检"
            : "";

    // ========== 成切跟踪 ==========

    /// <summary>成切需求：成品工序组（制造规格=成品规格）内是否有「断切」工段</summary>
    public bool CutRequirement { get; set; }
    public string CutRequirementDisplay => CutRequirement ? "是" : "否";

    /// <summary>成切执行：需求=否→略；成品工序组内已有断切生产记录→是；否则→否</summary>
    public bool? CutExecution { get; set; }
    public string? CutExecutionDisplay => CutExecution switch { true => "是", false => "否", null => "略" };

    /// <summary>成切支数：断切生产记录 PostCutQuantity（切后支数）汇总；无→略</summary>
    public int? CutQuantity { get; set; }
    public string? CutQuantityDisplay => CutQuantity.HasValue ? CutQuantity.Value.ToString() : "略";

    /// <summary>成切存疑：略/正常/疑问-数量/疑问-缺少</summary>
    public CutDoubtType? CutDoubt { get; set; }
    public string? CutDoubtDisplay => CutDoubt switch
    {
        CutDoubtType.QuantityMismatch => "疑问-数量",
        CutDoubtType.MissingRecords => "疑问-缺少",
        CutDoubtType.Normal => "正常",
        _ => "略"
    };

    // ========== 审计字段 ==========
    public DateTimeOffset CreatedTime { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset UpdatedTime { get; set; }
    public string UpdatedBy { get; set; } = null!;

    // ========== 乐观锁 ==========
    public byte[] RowVersion { get; set; } = null!;

    // ========== 工序组列表 ==========
    public List<ProcessGroupDto> ProcessGroups { get; set; } = new();

    // ========== 合并投料来源 ==========

    /// <summary>
    /// 来源库存批次列表
    /// </summary>
    public List<SourceBatchItemDto> SourceItems { get; set; } = new();
}
