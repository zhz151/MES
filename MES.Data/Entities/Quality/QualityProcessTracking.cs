namespace MES.Data.Entities.Quality;

/// <summary>
/// 质量过程跟踪物化读模型（成检到料 → 成品检验 → 成品入库）
/// 通过业务 Service 写入后自动刷新，不提供手工增删改
/// </summary>
public class QualityProcessTracking : BaseEntity
{
    // ========== 关联标识 ==========
    /// <summary>成检到料ID（唯一，一条 MRCheck 对应一条记录）</summary>
    public int MaterialReceiveCheckId { get; set; }

    /// <summary>批次ID</summary>
    public int ProductionBatchId { get; set; }

    /// <summary>成检类型（PreInspection=预成检，FormalInspection=正式成检；唯一键之一，与批次ID组合）</summary>
    public string? InspectionType { get; set; }

    /// <summary>
    /// 是否交付态（批次制造状态==交货状态为"是"，否则"否"；信息列非唯一键，
    /// 随批次当前制造状态实时计算）
    /// </summary>
    public string? IsDeliveryStatus { get; set; }

    // ========== G1: 批次信息（来自 MaterialReceiveCheck 冗余字段） ==========
    public string? BatchNo { get; set; }
    /// <summary>制造物品（MaterialType 枚举名：OrderFinished/Finished/Surplus/SpecialDeliveryStatus）</summary>
    public string? ManufacturingItem { get; set; }
    public string? TagNo { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? ProductionMainNo { get; set; }
    public string? SourceUnit { get; set; }
    public string? FurnaceNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public string? ProductionType { get; set; }
    public string? LengthStatus { get; set; }
    public decimal? ProductionWeight { get; set; }
    public DateTime ReceiveDate { get; set; }
    public string? Shift { get; set; }
    public string? Checker { get; set; }
    public string? Salesman { get; set; }
    public string? ManufacturingStatus { get; set; }
    public string? DeliveryState { get; set; }
    public string? EndCustomer { get; set; }
    public bool IsForceCompleted { get; set; }

    // ========== G2: 检验日期（按 InspectionItem 拆分） ==========
    public DateTime? PmiDate { get; set; }
    public DateTime? VisualDate { get; set; }
    public DateTime? DimensionDate { get; set; }
    public DateTime? EndoscopyDate { get; set; }
    public DateTime? HydroDate { get; set; }
    public DateTime? UnderwaterPneumaticDate { get; set; }
    public DateTime? EddyCurrentDate { get; set; }
    public DateTime? UltrasonicDate { get; set; }
    public DateTime? PortColoringDate { get; set; }
    public int InspectionCount { get; set; }

    // ========== G3: 检验汇总 ==========
    public int ProductionCutQuantity { get; set; }
    /// <summary>检验支数（按「批次+成检类型+检验项目」汇总 Quantity，跨项目取最大）</summary>
    public int TotalQuantity { get; set; }
    /// <summary>理论合格支（检验支数 - 返整/入库/报废三次品汇总；负值归零）</summary>
    public int QualifiedQuantity { get; set; }
    public int DefectReworkQuantity { get; set; }
    public int DefectWarehouseQuantity { get; set; }
    public int DefectScrapQuantity { get; set; }
    public DateTime? MaxInspectionDate { get; set; }

    // ========== G4: 成品入库 ==========
    public int InboundQuantity { get; set; }
    public decimal? InboundWeight { get; set; }
    public DateTime? InboundDate { get; set; }

    // ========== G5: 执行状态 ==========
    public string QualityStatus { get; set; } = "待检验";

    // ========== 刷新追踪 ==========
    /// <summary>最后刷新时间</summary>
    public DateTime? LastRefreshTime { get; set; }
}
