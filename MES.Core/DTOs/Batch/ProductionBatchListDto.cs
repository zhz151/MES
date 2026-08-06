using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Batch;

/// <summary>
/// 生产批次列表DTO
/// </summary>
public class ProductionBatchListDto
{
    public int Id { get; set; }
    public string BatchNo { get; set; } = null!;
    public string? TagNo { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
    public string WorkOrderNo { get; set; } = null!;
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string? ProductionSubNo { get; set; }
    public ProductionType? ProductionType { get; set; }
    public MaterialType ManufacturingItem { get; set; }
    public string ManufacturingItemDisplay => EnumHelper.GetDisplayName(ManufacturingItem);
    public BatchStatus Status { get; set; }
    public string StatusDisplay => EnumHelper.GetDisplayName(Status);
    public bool IsForceCompleted { get; set; }
    public int ProductionRatio { get; set; }
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
    public int RemainingWorkDays { get; set; }
    public int TotalWorkDays { get; set; }
    public int? CurrentValidQty { get; set; }
    public int? CurrentValidWeight { get; set; }
    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }

    // ========== 工单冗余字段 ==========
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
    public LengthStatus LengthStatus { get; set; }
    public string LengthStatusDisplay => EnumHelper.GetDisplayName(LengthStatus);
    public decimal OuterDiameterNegative { get; set; }
    public decimal OuterDiameterPositive { get; set; }
    public decimal WallThicknessNegative { get; set; }
    public decimal WallThicknessPositive { get; set; }
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalMeters { get; set; }
    public decimal TotalWeight { get; set; }

    /// <summary>
    /// 产品单支量(kg/支) = 总重量/总支数，仅"定尺"批次有效，非定尺为空；保留1位小数
    /// </summary>
    public decimal? ProductUnitWeight { get; set; }

    public string TechnicalRequirements { get; set; } = null!;

    /// <summary>
    /// 有效投料变更：有效投料支数与领料支数是否一致，有/无
    /// </summary>
    public bool? HasInputChange { get; set; }

    /// <summary>
    /// 过程检合格支：批次当前执行工序组全部过程检验的合格支数聚合；无检验/不在产 → null
    /// </summary>
    public int? ProcessInspectionQualifiedQty { get; set; }

    /// <summary>
    /// 过程检合格量(kg)：批次当前执行工序组全部过程检验的合格重量聚合；无检验/不在产 → null
    /// </summary>
    public decimal? ProcessInspectionQualifiedWeight { get; set; }

    /// <summary>
    /// 过程检理论成品支：Round(合格量 ÷ 合格支 ÷ 成品的理论单支重, AwayFromZero) × 合格支（重量口径折算）
    /// </summary>
    public int? ProcessInspectionTheoreticalQty { get; set; }

    /// <summary>
    /// 需调整：批次状态为 成检/完成 时固定 null（-）不判定；其余状态 过程检理论成品支 与 当前理论成品支 偏差 &gt; 3% → true（是）；否则/无数据 → null（-）
    /// </summary>
    public bool? ProcessInspectionNeedAdjust { get; set; }

    /// <summary>
    /// 缺陷-返整量（重量 kg）：过程检验 理论返整重 全量聚合（返整会另开批次，不延续本批）
    /// </summary>
    public int ProcessInspectionReworkWeight { get; set; }

    /// <summary>
    /// 缺陷-纯次品量（重量 kg）：过程检验 理论报废重+理论入库重 全量聚合（彻底退出正常流）
    /// </summary>
    public int ProcessInspectionScrapWeight { get; set; }

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

    // ========== 扩展字段（从 Entity 补充） ==========
    public string? Remark { get; set; }
    public string? SourceHeatNo { get; set; }
    public int TotalItemCount { get; set; }
    public string? SourceSpecification { get; set; }
    public int? InputQuantity { get; set; }
    public decimal? InputWeight { get; set; }
    public string? SolutionParams { get; set; }
    public string? QualityRemark { get; set; }
    public MaterialType? SourceMaterialType { get; set; }
    public string? SourceMaterialTypeDisplay => SourceMaterialType.HasValue ? EnumHelper.GetDisplayName(SourceMaterialType.Value) : null;
    public string? SourceName { get; set; }
    public string? SourceBatchNo { get; set; }
    public string? SourcePlantGrade { get; set; }
    public decimal? SourceUnitWeight { get; set; }
    public BatchInputType InputType { get; set; }
    public string InputTypeDisplay => EnumHelper.GetDisplayName(InputType);
    public LengthStatus? SourceLengthStatus { get; set; }
    public string? SourceProductionNo { get; set; }

    // ========== 理论计算字段 ==========
    public int? TheoreticalOutputQty { get; set; }
    public int? TheoreticalOutputWeight { get; set; }
    public decimal? TheoreticalUnitWeight { get; set; }
}
