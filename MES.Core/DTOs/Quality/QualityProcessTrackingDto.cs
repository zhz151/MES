using MES.Core.Enums;
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
    public MaterialType? ManufacturingItem { get; set; }
    public string? TagNo { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? SourceUnit { get; set; }
    public string? FurnaceNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public ProductionType? ProductionType { get; set; }
    public LengthStatus? LengthStatus { get; set; }
    public decimal? ProductionWeight { get; set; }
    public DateTime ReceiveDate { get; set; }
    public ShiftType? Shift { get; set; }
    public string? Checker { get; set; }
    public string? Salesman { get; set; }
    public DeliveryState? DeliveryState { get; set; }
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
    public int TotalQuantity { get; set; }                   // 检验支数（单项最大值）
    public int QualifiedQuantity { get; set; }               // 合格支数（单项最小值）
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
