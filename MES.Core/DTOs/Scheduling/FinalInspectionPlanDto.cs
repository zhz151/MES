using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 成检计划 DTO
/// </summary>
public class FinalInspectionPlanDto
{
    public int ProductionBatchId { get; set; }

    // ========== G1 批次信息 ==========
    public string? BatchNo { get; set; }              // 生产编号
    public string? TagNo { get; set; }                 // 挂牌号
    public string? PlantGrade { get; set; }            // 原料钢号
    public decimal? CurrentValidWeight { get; set; }   // 重量(kg)

    // ========== G2 关联工单 ==========
    public string? WorkOrderNo { get; set; }           // 工单号
    public string? Salesman { get; set; }              // 业务员
    public DateTime? DeliveryDate { get; set; }        // 交货日期
    public string? Specification { get; set; }         // 成品规格
    public LengthStatus? LengthStatus { get; set; }
    public string? LengthStatusDisplay => LengthStatus.HasValue ? EnumHelper.GetDisplayName(LengthStatus.Value) : null;
    public decimal? MinLength { get; set; }            // 最小长度
    public decimal? MaxLength { get; set; }            // 最大长度

    // ========== G12 排程信息（WorkOrderExecutionSummary） ==========
    public int? ScheduleStage { get; set; }            // 关注状态
    public string? UrgencyLevel { get; set; }          // 工单计划性

    // ========== G4: 各项检验的日期（来自 FinalInspection） ==========
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

    // ========== G5: 检验的数量信息（来自 FinalInspection） ==========
    public int TotalQuantity { get; set; }                   // 检验支数（单项最大值）
    public int QualifiedQuantity { get; set; }               // 合格支数（单项最小值）
    public int DefectReworkQuantity { get; set; }            // 返整支数合计
    public int DefectWarehouseQuantity { get; set; }         // 不合格入库支数合计
    public int DefectScrapQuantity { get; set; }             // 报废支数合计

    // ========== G6: 成检状态 ==========
    public DateTime? ReceiveDate { get; set; }               // 到料日期
    public DateTime? MaxInspectionDate { get; set; }         // 最大检验日期
    public string KanbanStage { get; set; } = "";             // 待到料/待检验/检验中
}
