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
    /// <summary>计划状态（0=主号暂停 1=主号完成 2=原料锁定 3=生产执行 4=成品检验）</summary>
    public int ScheduleStage { get; set; }

    /// <summary>关注状态文本</summary>
    public string ScheduleStageText => IntStatusDisplayHelper.GetScheduleStageText(ScheduleStage);

    /// <summary>工单计划性（A+急/A急/B顺/C缓/D缓）</summary>
    public string? UrgencyLevel { get; set; }

    // ===== G3 成品切割执行 =====
    /// <summary>切割截止日（该长度断切记录最大执行日）</summary>
    public DateTime? CutDeadline { get; set; }

    /// <summary>切后支数（主号级该长度断切成品 切后支数之和）</summary>
    public int CutQuantity { get; set; }

    // ===== G4 成检数据 =====
    /// <summary>成检截止日（该长度成检记录最大检验日）</summary>
    public DateTime? InspectionDeadline { get; set; }

    /// <summary>到料支数（主号级该长度 尺寸+正式成检 检验支数之和）</summary>
    public int ArrivedQuantity { get; set; }

    /// <summary>成切到料支数（反查批次成切需求=是 的批次 检验支数之和）</summary>
    public int CutArrivedQuantity { get; set; }

    /// <summary>非成切到料支数（反查批次成切需求=否 的批次 检验支数之和）</summary>
    public int NonCutArrivedQuantity { get; set; }

    /// <summary>次品支数（三种次品支之和）</summary>
    public int DefectQuantity { get; set; }

    /// <summary>合格支数（到料支数−次品支数）</summary>
    public int QualifiedQuantity => ArrivedQuantity - DefectQuantity;

    /// <summary>盈缺支数（合格支数−需求支数）</summary>
    public int QualifiedSurplus => QualifiedQuantity - PlannedQuantity;

    // ===== G5 成品入库 =====
    /// <summary>入库截止日（该工单该长度 匹配物料类型 入库记录最大入库日）</summary>
    public DateTime? InboundDeadline { get; set; }

    /// <summary>入库支数（该工单该长度 匹配物料类型 入库记录入库支数之和）</summary>
    public int InboundQuantity { get; set; }

    /// <summary>入库盈缺支数（入库支数−需求支数）</summary>
    public int InboundSurplus => InboundQuantity - PlannedQuantity;

    /// <summary>
    /// 入库存疑（工单完成 且 入库支数 &lt; 需求支数；或 入库支数−需求支数 &gt; 10支 且 入库/需求 &gt; 105% → 疑问；否则正常）
    /// </summary>
    public string InboundDoubt
    {
        get
        {
            if (ScheduleStage == 0 && InboundQuantity < PlannedQuantity) return "疑问";
            if (PlannedQuantity > 0 && InboundQuantity - PlannedQuantity > 10
                && (decimal)InboundQuantity / PlannedQuantity > 1.05m) return "疑问";
            return "正常";
        }
    }

    // ===== G6 主号数据及现况分析（主号级） =====
    /// <summary>主号总需求支</summary>
    public int MainNoTotalRequirement { get; set; }

    /// <summary>主号总投料支</summary>
    public int MainNoTotalInput { get; set; }

    /// <summary>主号无需切割支（CutRequirement=否 批次的 理论成品支）</summary>
    public int MainNoNoCutQty { get; set; }

    /// <summary>主号需切未切支（CutRequirement=是 且 无断切记录 批次的 理论成品支）</summary>
    public int MainNoNeedCutUncutQty { get; set; }

    /// <summary>主号切割批理论总支（CutRequirement=是 且 有断切记录 批次的 理论成品支）</summary>
    public int MainNoCutTheoretical { get; set; }

    /// <summary>主号切割批实际总支（断切记录 切后支数）</summary>
    public int MainNoCutActual { get; set; }

    /// <summary>实切合理性（|实际切割−理论切割|/理论切割 ≤5% 正常；否则异常；无切割理论基数=略）</summary>
    public string MainNoCutRationality
    {
        get
        {
            if (MainNoCutTheoretical <= 0) return "略";
            var diff = Math.Abs((decimal)MainNoCutActual - MainNoCutTheoretical) / MainNoCutTheoretical;
            return diff <= 0.05m ? "正常" : "异常";
        }
    }

    /// <summary>主号成检总次品支（三种次品支之和）</summary>
    public int MainNoDefect { get; set; }

    /// <summary>预计损耗支（待切理论支×1%，四舍五入取整）</summary>
    public int EstimatedLossQty => (int)Math.Round(MainNoNeedCutUncutQty * 0.01m, MidpointRounding.AwayFromZero);

    /// <summary>现理论实投支（无需切割支 + 需切未切支 + 实际切割支 − 预计损耗支；= 总投料支 − 切割缺口 − 预计损耗）</summary>
    public int MainNoCurrentInput => MainNoNoCutQty + MainNoNeedCutUncutQty + MainNoCutActual - EstimatedLossQty;

    /// <summary>
    /// 总盈亏支数 = 现理论实投支 − 主号成检次品总 − 主号总需求支
    /// （现理论实投支已扣减预计损耗支；三档恒等式：无需切割支 + 需切未切支 + 切割批理论总支 = 主号总投料支）
    /// </summary>
    public int TotalSurplus => MainNoCurrentInput - MainNoDefect - MainNoTotalRequirement;

    /// <summary>总盈亏状态（工单完成/原料锁定=略；总盈亏&lt;0=缺少；否则=合理）</summary>
    public string TotalSurplusStatus => ScheduleStage == 0 || ScheduleStage == 1
        ? "略"
        : TotalSurplus < 0 ? "缺少" : "合理";
}
