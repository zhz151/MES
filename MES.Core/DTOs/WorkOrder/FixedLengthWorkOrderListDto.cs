using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 定尺工单定尺数据（联通视图，列表模式）
/// 每行 = 一个定尺工单的一个长度（工单号 + 长度 + 需求支数）
/// 切割/成检支数为「主号级」按长度聚合值；总现况分析为主号级汇总
/// </summary>
public class FixedLengthWorkOrderListDto
{
    // ===== G1 基础数据 =====
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>定尺长度(mm)</summary>
    public decimal Length { get; set; }

    /// <summary>需求支数（该工单该长度计划支数）</summary>
    public int PlannedQuantity { get; set; }

    public string Salesman { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public DateTime SignDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string? ProductionSubNo { get; set; }

    /// <summary>交货状态（读模型存字符串，投影时转为枚举）</summary>
    public DeliveryState DeliveryState { get; set; }
    public string DeliveryStateDisplay => EnumHelper.GetDisplayName(DeliveryState);

    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;

    // ===== G2 计划状态 =====
    /// <summary>计划状态（0=工单完成 1=原料锁定 2=生产执行 3=成品检验）</summary>
    public int ScheduleStage { get; set; }

    /// <summary>关注状态文本</summary>
    public string ScheduleStageText => ScheduleStage switch
    {
        0 => "工单完成",
        1 => "原料锁定",
        2 => "生产执行",
        3 => "成品检验",
        _ => ScheduleStage.ToString()
    };

    /// <summary>工单计划性（A+急/A急/B顺/C缓/D缓）</summary>
    public string? UrgencyLevel { get; set; }

    // ===== G3 成品切割执行 =====
    /// <summary>切割截止日（该长度断切记录最大执行日）</summary>
    public DateTime? CutDeadline { get; set; }

    /// <summary>切割支数（主号级该长度断切成品 切后支数之和）</summary>
    public int CutQuantity { get; set; }

    /// <summary>盈缺支数（切割支数−需求支数）</summary>
    public int CutSurplus => CutQuantity - PlannedQuantity;

    // ===== G4 成检数据 =====
    /// <summary>成检截止日（该长度成检记录最大检验日）</summary>
    public DateTime? InspectionDeadline { get; set; }

    /// <summary>到料支数（主号级该长度 尺寸+正式成检 检验支数之和）</summary>
    public int ArrivedQuantity { get; set; }

    /// <summary>次品支数（三种次品支之和）</summary>
    public int DefectQuantity { get; set; }

    /// <summary>合格支数（到料支数−次品支数）</summary>
    public int QualifiedQuantity => ArrivedQuantity - DefectQuantity;

    /// <summary>盈缺支数（合格支数−需求支数）</summary>
    public int QualifiedSurplus => QualifiedQuantity - PlannedQuantity;

    // ===== G5 总现况分析（主号级） =====
    /// <summary>主号总需求支</summary>
    public int MainNoTotalRequirement { get; set; }

    /// <summary>主号总投料支</summary>
    public int MainNoTotalInput { get; set; }

    /// <summary>主号未切总支（未执行成品切割批次的 理论成品支）</summary>
    public int MainNoUncut { get; set; }

    /// <summary>主号切割批理论总支（已执行成品切割批次的 理论成品支）</summary>
    public int MainNoCutTheoretical { get; set; }

    /// <summary>主号切割批实际总支（断切记录 切后支数）</summary>
    public int MainNoCutActual { get; set; }

    /// <summary>主号成检总次品支（三种次品支之和）</summary>
    public int MainNoDefect { get; set; }

    /// <summary>总盈亏支数 = 未切总支 + 切割批实际总支 − 成检总次品支 − 总需求支</summary>
    public int TotalSurplus => MainNoUncut + MainNoCutActual - MainNoDefect - MainNoTotalRequirement;

    /// <summary>总盈亏状态（工单完成/原料锁定=略；总盈亏&lt;0=缺少；否则=合理）</summary>
    public string TotalSurplusStatus => ScheduleStage == 0 || ScheduleStage == 1
        ? "略"
        : TotalSurplus < 0 ? "缺少" : "合理";
}
