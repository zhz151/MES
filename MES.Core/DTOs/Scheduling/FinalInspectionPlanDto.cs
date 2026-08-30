using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 成检计划 DTO — 候选为 Status==InFinalInspection（成检）状态的批次，
/// 展示字段参照「成检追踪」：合并批次信息 + 关联工单，行粒度 = 生产编号 + 成检类型。
/// </summary>
public class FinalInspectionPlanDto
{
    public int ProductionBatchId { get; set; }

    // ========== G1 批次信息 + 关联工单（合并，参照成检追踪） ==========
    public string? BatchNo { get; set; }                      // 生产编号
    public InspectionType? InspectionType { get; set; }       // 成检类型（来自批次「成检附加」InspectionStage；空默认正式成检）
    public string? InspectionTypeDisplay => InspectionType.HasValue ? EnumHelper.GetDisplayName(InspectionType.Value) : null;

    /// <summary>
    /// 是否正式成检（成检类型==FormalInspection；预成检/其他均视为非正式成检）
    /// 仅正式成检时「制造状态/是否交付态」才有效，否则统一显示 "-"
    /// </summary>
    public bool IsFormalInspection =>
        InspectionType == MES.Core.Enums.InspectionType.FormalInspection;

    /// <summary>是否交付态（存储值："是"/"否"；非正式成检统一显示 "-"）</summary>
    public string? IsDeliveryStatus { get; set; }
    public string? IsDeliveryStatusDisplay => IsFormalInspection ? IsDeliveryStatus : "-";
    public ProductionType? ProductionType { get; set; }        // 生产类型
    public string? ProductionTypeDisplay => ProductionType.HasValue ? EnumHelper.GetDisplayName(ProductionType.Value) : null;
    public MaterialType? ManufacturingItem { get; set; }      // 制造物品
    public string? ManufacturingItemDisplay => ManufacturingItem.HasValue ? EnumHelper.GetDisplayName(ManufacturingItem.Value) : null;
    public DeliveryState? ManufacturingStatus { get; set; }   // 制造状态
    public string? ManufacturingStatusDisplay => !IsFormalInspection
        ? "-"
        : ManufacturingStatus.HasValue ? EnumHelper.GetDisplayName(ManufacturingStatus.Value) : "-";
    public DeliveryState? DeliveryState { get; set; }         // 交货状态
    public string? DeliveryStateDisplay => DeliveryState.HasValue ? EnumHelper.GetDisplayName(DeliveryState.Value) : null;
    public string? PlantGrade { get; set; }                   // 工厂牌号
    public string? Specification { get; set; }                // 规格
    public LengthStatus? LengthStatus { get; set; }           // 长度状态
    public int ProductionCutQuantity { get; set; }            // 生产支数
    public decimal? ProductionWeight { get; set; }            // 生产重量(kg)
    public string? SourceHeatNo { get; set; }                 // 炉号（来源炉号）
    public string? SourceName { get; set; }                   // 来料单位
    public string? WorkOrderNo { get; set; }                  // 工单号
    public string? SalesOrderNo { get; set; }                 // 订单号
    public string? ProductionMainNo { get; set; }             // 主号
    public string? Salesman { get; set; }                     // 业务员
    public string? EndCustomer { get; set; }                  // 最终用户

    // ========== G3 排程信息（WorkOrderExecutionSummary） ==========
    public int? ScheduleStage { get; set; }                   // 计划状态
    public string? UrgencyLevel { get; set; }                 // 紧急程度

    // ========== G4 成检状态 ==========
    public DateTime? ReceiveDate { get; set; }                // 到料日期
    public DateTime? MaxInspectionDate { get; set; }          // 最晚检验
    public string KanbanStage { get; set; } = "";              // 待到料/待检验/检验中/完成检验待入库

    // ========== G5: 技术要求检验项（ProductRequirement，表检+尺寸恒必检；与四档判定同源） ==========
    public int ReqCount { get; set; }                         // 必检项数（= 要求项集合大小，表检+尺寸恒含）
    public bool ReqPmi { get; set; }                          // PMI检验
    public bool ReqVisual { get; set; }                       // 表检（恒必检）
    public bool ReqDimension { get; set; }                    // 尺寸（恒必检）
    public bool ReqEndoscopy { get; set; }                    // 内窥
    public bool ReqHydro { get; set; }                        // 水压
    public bool ReqUnderwater { get; set; }                   // 水下气压
    public bool ReqEddy { get; set; }                         // 涡流
    public bool ReqUltrasonic { get; set; }                   // 超声波
    public bool ReqPortColoring { get; set; }                 // 端口着色

    // ========== G6: 各项检验的日期（来自 FinalInspection） ==========
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

    // ========== G7: 检验的数量信息（来自 FinalInspection） ==========
    public int TotalQuantity { get; set; }                   // 检验支数（按检验项目分组求和取最大，与成检追踪一致）
    public int QualifiedQuantity { get; set; }               // 理论合格支（检验支数 - 返整/入库/报废三次品汇总，负值归零）
    public int DefectReworkQuantity { get; set; }            // 返整支数合计
    public int DefectWarehouseQuantity { get; set; }         // 不合格入库支数合计
    public int DefectScrapQuantity { get; set; }             // 报废支数合计
}
