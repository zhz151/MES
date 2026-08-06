using MES.Core.Enums;
using MES.Core.Helpers;
namespace MES.Core.DTOs.Quality;

/// <summary>
/// 质量过程跟踪 DTO（整合成检到料 → 成品检验 → 成品入库）
/// </summary>
public class QualityProcessTrackingDto
{
    // ========== G1: 批次信息（来自 MaterialReceiveCheck 冗余字段） ==========
    public int Id { get; set; }
    public int ProductionBatchId { get; set; }
    public string? BatchNo { get; set; }
    public InspectionType? InspectionType { get; set; }
    public string? InspectionTypeDisplay => InspectionType.HasValue ? EnumHelper.GetDisplayName(InspectionType.Value) : null;

    /// <summary>
    /// 是否正式成检（成检类型==FormalInspection；null/其他/预成检均视为非正式成检）
    /// 仅正式成检时「制造状态/是否交付态」才有效，否则统一显示 "-"
    /// </summary>
    public bool IsFormalInspection =>
        InspectionType == MES.Core.Enums.InspectionType.FormalInspection;

    /// <summary>是否交付态（存储值："是"/"否"；非正式成检统一显示 "-"）</summary>
    public string? IsDeliveryStatus { get; set; }
    public string? IsDeliveryStatusDisplay => IsFormalInspection ? IsDeliveryStatus : "-";
    public MaterialType? ManufacturingItem { get; set; }
    public string? ManufacturingItemDisplay => ManufacturingItem.HasValue ? EnumHelper.GetDisplayName(ManufacturingItem.Value) : null;
    public string? TagNo { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? ProductionMainNo { get; set; }
    public string? SourceUnit { get; set; }
    public string? FurnaceNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public ProductionType? ProductionType { get; set; }
    public string? ProductionTypeDisplay => ProductionType.HasValue ? EnumHelper.GetDisplayName(ProductionType.Value) : null;
    public LengthStatus? LengthStatus { get; set; }
    public string? LengthStatusDisplay => LengthStatus.HasValue ? EnumHelper.GetDisplayName(LengthStatus.Value) : null;
    public decimal? ProductionWeight { get; set; }
    public DateTime ReceiveDate { get; set; }
    public ShiftType? Shift { get; set; }
    public string? ShiftDisplay => Shift.HasValue ? EnumHelper.GetDisplayName(Shift.Value) : null;
    public string? Checker { get; set; }
    public string? Salesman { get; set; }
    public DeliveryState? ManufacturingStatus { get; set; }
    public string? ManufacturingStatusDisplay => !IsFormalInspection
        ? "-"
        : ManufacturingStatus.HasValue ? EnumHelper.GetDisplayName(ManufacturingStatus.Value) : "-";
    public DeliveryState? DeliveryState { get; set; }
    public string? DeliveryStateDisplay => DeliveryState.HasValue ? EnumHelper.GetDisplayName(DeliveryState.Value) : null;
    public string? EndCustomer { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }

    // ========== G2: 检验日期（按 InspectionItem 拆分） ==========
    public DateTime? PmiDate { get; set; }                  // PMI检验
    public DateTime? VisualDate { get; set; }                // 表检
    public DateTime? DimensionDate { get; set; }             // 尺寸
    public DateTime? EndoscopyDate { get; set; }             // 内窥
    public DateTime? HydroDate { get; set; }                 // 水压
    public DateTime? UnderwaterPneumaticDate { get; set; }   // 水下气压
    public DateTime? EddyCurrentDate { get; set; }           // 涡流
    public DateTime? UltrasonicDate { get; set; }            // 超声波
    public DateTime? PortColoringDate { get; set; }          // 端口着色
    public int InspectionCount { get; set; }                 // 已检测项数

    // ========== G3: 检验汇总 ==========
    public int ProductionCutQuantity { get; set; }           // 生产支数（断切成品切后支数和）
    public int TotalQuantity { get; set; }                   // 检验支数（按唯一性+检验项目汇总 Quantity，跨项目取最大）
    public int QualifiedQuantity { get; set; }               // 理论合格支（检验支数 - 三次品汇总）
    public int DefectReworkQuantity { get; set; }            // 返整支数合计
    public int DefectWarehouseQuantity { get; set; }         // 不合格入库支数合计
    public int DefectScrapQuantity { get; set; }             // 报废支数合计
    public DateTime? MaxInspectionDate { get; set; }         // 最晚检验日期

    // ========== G4: 成品入库 ==========
    public int InboundQuantity { get; set; }                 // 入库支数汇总
    public decimal? InboundWeight { get; set; }              // 入库重量汇总
    public DateTime? InboundDate { get; set; }

    // ========== G5: 执行状态 ==========
    public string QualityStatus { get; set; } = "待检验";    // 待检验/检验中/完成检验
    public bool IsForceCompleted { get; set; }               // 强制完成
}
