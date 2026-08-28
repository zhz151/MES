using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 原锁计划 DTO（G1+G2+G4-G10+G3+G13+G15，对齐工单执行状况读模型）
/// </summary>
public class RawMaterialLockPlanAndExecutionDto
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }

    // ========== G1: 工单基础数据 ==========
    public string WorkOrderNo { get; set; } = null!;
    public string Salesman { get; set; } = null!;
    public string CustomerName { get; set; } = null!;

    /// <summary>最终客户（终端用户）</summary>
    public string? EndCustomer { get; set; }
    public DateTime SignDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool DelayPenalty { get; set; }
    public SettlementMethod SettlementMethod { get; set; }
    public string SettlementMethodDisplay => EnumHelper.GetDisplayName(SettlementMethod);
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string? ProductionSubNo { get; set; }
    public string MaterialName { get; set; } = null!;
    public DeliveryState DeliveryState { get; set; }
    public string DeliveryStateDisplay => EnumHelper.GetDisplayName(DeliveryState);
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public LengthStatus LengthStatus { get; set; }
    public string LengthStatusDisplay => EnumHelper.GetDisplayName(LengthStatus);
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int TotalItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalMeters { get; set; }
    public decimal TotalWeight { get; set; }

    // ========== G4: 用料计划及执行实况 ==========
    public MaterialPlanStatus MaterialPlanStatus { get; set; }
    public string MaterialPlanStatusDisplay => EnumHelper.GetDisplayName(MaterialPlanStatus);
    public decimal MainNoMaterialPlanRate { get; set; }
    public MaterialPlanStatus MainNoMaterialPlanStatus { get; set; }
    public string MainNoMaterialPlanStatusDisplay => EnumHelper.GetDisplayName(MainNoMaterialPlanStatus);

    /// <summary>主号计划执行状态(0=无计划 1=未执行 2=执行中 3=计划落实)</summary>
    public int MainNoPlanExecutionStatus { get; set; }
    public string MainNoPlanExecutionStatusText => IntStatusDisplayHelper.GetMainNoPlanExecutionStatusText(MainNoPlanExecutionStatus);

    /// <summary>料态种数：4种料态中有做计划的种数(0-4)</summary>
    public int MaterialPlanCoveredCount { get; set; }

    /// <summary>用料占比文本：如"穿105% 荒160% 成20% 库40%"</summary>
    public string? MaterialPlanProportion { get; set; }

    /// <summary>理论截止投料日：交货日-(主号最大工艺周期+产能工量)</summary>
    public DateTime? TheoreticalCutoffDate { get; set; }

    /// <summary>截止到料日：仓库实际到料与出库动作日期的最大值</summary>
    public DateTime? CutoffArrivalDate { get; set; }

    /// <summary>主号截止到料日：同主号各工单 CutoffArrivalDate 的最大值</summary>
    public DateTime? MainNoCutoffArrivalDate { get; set; }

    // ========== G5: 圆棒穿孔 ==========
    public decimal PiercingPlanWeight { get; set; }
    public decimal PiercingSubOutWeight { get; set; }
    public int PiercingSubStatus { get; set; }
    public decimal PiercingSubInWeight { get; set; }
    public decimal PiercingSubPendingWeight { get; set; }
    public int PiercingReturnStatus { get; set; }

    // ========== G6: 荒管采购 ==========
    public decimal SemiPlanWeight { get; set; }
    public decimal SemiOrderWeight { get; set; }
    public int SemiOrderStatus { get; set; }
    public decimal SemiInWeight { get; set; }
    public decimal SemiPendingWeight { get; set; }
    public int SemiInStatus { get; set; }

    // ========== G7: 成品采购 ==========
    public decimal FinishPlanWeight { get; set; }
    public decimal FinishOrderWeight { get; set; }
    public int FinishOrderStatus { get; set; }
    public decimal FinishInWeight { get; set; }
    public decimal FinishPendingWeight { get; set; }
    public int FinishInStatus { get; set; }

    // ========== G8: 库存使用 ==========
    public decimal InventoryPlanWeight { get; set; }
    public decimal InventoryOutWeight { get; set; }
    public int InventoryOutStatus { get; set; }

    // ========== G9: 库料改制 ==========
    public decimal ReworkPlanWeight { get; set; }
    public decimal ReworkPlanInputWeight { get; set; }
    public int ReworkPlanInputStatus { get; set; }

    // ========== G10: 在产改制 ==========
    public decimal InProcessReworkPlanWeight { get; set; }
    public decimal InProcessReworkInputWeight { get; set; }
    public int InProcessReworkInputStatus { get; set; }

    // ========== G11: 在产主工单 ==========
    public decimal InMainPlanWeight { get; set; }
    public decimal InMainInputWeight { get; set; }
    public int InMainInputStatus { get; set; }

    // ========== G3: 投料数据 ==========
    public DateTime? InputStartDate { get; set; }
    public DateTime? InputEndDate { get; set; }
    public int TotalBatchCount { get; set; }
    public int InputQuantity { get; set; }
    public decimal InputWeight { get; set; }
    public decimal TheoreticalOutputQty { get; set; }
    public decimal TheoreticalOutputWeight { get; set; }
    public decimal InputOutputRatio { get; set; }
    public int InputStatus { get; set; }
    public decimal MainNoInputOutputRatio { get; set; }
    public int MainNoInputStatus { get; set; }

    // ========== G13: 实际生产总流转 ==========
    public decimal FlowOutputRatio { get; set; }
    public int FlowStatus { get; set; }
    public decimal MainNoFlowOutputRatio { get; set; }
    public int MainNoFlowStatus { get; set; }
    public int FlowTotalBatchCount { get; set; }
    public int FlowIncompleteBatchCount { get; set; }
    public int FlowMaxRemainingWorkDays { get; set; }

    // ========== G12: 实时关注 ==========
    public int ScheduleStage { get; set; }
    public int? TotalRemainingWorkDays { get; set; }
    public int? CapacityWorkDays { get; set; }
    public string? UrgencyLevel { get; set; }
    public DateTime? EstimatedProcessCompletionDate { get; set; }
    public int? DaysDiffFromDelivery { get; set; }
    public string? RawMaterialLockRemark { get; set; }

    // ========== G2: 工单需求调整（从 WorkOrderExecutionSummary 实体读取） ==========
    public bool IsUrging { get; set; }
    public bool IsBatchDelivery { get; set; }
    public bool IsPaused { get; set; }
    public string? AdjustmentRemark { get; set; }

    // ========== G15: 预执行（页面操作标记）==========
    /// <summary>执行：近几日会投料</summary>
    public bool IsPreInput { get; set; }

    /// <summary>预算投料日</summary>
    public DateTime? BudgetInputDate { get; set; }

    // ========== G4 计算列 ==========

    /// <summary>计划投料总重量(kg)：G5~G11 七个计划量之和</summary>
    public decimal TotalPlanWeight =>
        PiercingPlanWeight + SemiPlanWeight + FinishPlanWeight
        + InventoryPlanWeight + ReworkPlanWeight
        + InProcessReworkPlanWeight + InMainPlanWeight;

    /// <summary>现可投料总重量(kg)：G5委外到货 + G6采购到货 + G7采购到货 + G8出库量 + G9投料量 + G10投料量 + G11投料量（到货量口径：下单≠到货，未收货的量不视为"现可投料"）</summary>
    public decimal TotalAvailableWeight =>
        PiercingSubInWeight + SemiInWeight + FinishInWeight
        + InventoryOutWeight + ReworkPlanInputWeight
        + InProcessReworkInputWeight + InMainInputWeight;

    /// <summary>理论缺失总料重量(kg)：计划投料总重量 − 现可投料总重量；
    /// 仅当缺口 &gt; 计划投料总重×3%（InputConsistencyTolerance）才取值，否则为 0（小缺口降噪，与档5缺口率阈值同源）</summary>
    public decimal TotalMissingWeight
    {
        get
        {
            var plan = TotalPlanWeight;
            var missing = plan - TotalAvailableWeight;
            return missing > plan * MaterialPlanToleranceProvider.InputConsistencyTolerance ? missing : 0m;
        }
    }

    /// <summary>实际已投料量(kg) = 投料总重量(InputWeight)</summary>
    public decimal ActualInputWeight => InputWeight;

    /// <summary>实投主号状态 = 关联主号投料状态(MainNoInputStatus)</summary>
    public int ActualMainNoInputStatus => MainNoInputStatus;
    public string ActualMainNoInputStatusText => IntStatusDisplayHelper.GetInputStatusText(MainNoInputStatus);

    /// <summary>
    /// 到料实投一致性：0=一致 1=待投 2=疑问-到料少投 3=疑问-到料超投 4=错误-无料已投 5=错误-无需投料 6=略。
    /// 判定顺序：阶段门控优先（主号关注=生产执行(3)/成品检验(4)/主号完成(1) 已过投料期，理论缺失总料重&gt;计划投料总重×3% → 5 错误-无需投料；其余（含缺口≤3% 容差内）→ 6 略）；
    /// 否则走原有五态——基准：实际已投料量(InputWeight) vs 现可投料总重(TotalAvailableWeight，到货量口径，下单未到货的量不视为现可)。
    /// </summary>
    public int PlanInputConsistency
    {
        get
        {
            // 阶段门控：主号关注=生产执行(3)/成品检验(4)/主号完成(1) → 已过投料期，仅按缺失量判定（缺口率>计划×3% 才标错误，容差内归略）
            if (ScheduleStage is 1 or 3 or 4)
                return TotalMissingWeight > TotalPlanWeight * MaterialPlanToleranceProvider.InputConsistencyTolerance ? 5 : 6;
            if (ActualInputWeight > 0 && TotalAvailableWeight <= 0) return 4;
            if (TotalAvailableWeight <= 0) return 0;
            if (ActualInputWeight > TotalAvailableWeight * MaterialPlanToleranceProvider.InputConsistencyUpper) return 3;
            if (ActualInputWeight < TotalAvailableWeight * MaterialPlanToleranceProvider.InputConsistencyLower)
            {
                if (!CutoffArrivalDate.HasValue) return 0;
                var d = CutoffArrivalDate.Value.Date;
                if (d < DateTime.Today) return 2;
                if (d == DateTime.Today) return 1;
                return 0;
            }
            return 0;
        }
    }

    public string PlanInputConsistencyText => IntStatusDisplayHelper.GetPlanInputConsistencyText(PlanInputConsistency);

    // ========== G5~G11 状态文本 ==========
    public string PiercingSubStatusText => PlanExecutionStatusText(PiercingSubStatus);
    public string PiercingReturnStatusText => PlanExecutionStatusText(PiercingReturnStatus);
    public string SemiOrderStatusText => PlanExecutionStatusText(SemiOrderStatus);
    public string SemiInStatusText => PlanExecutionStatusText(SemiInStatus);
    public string FinishOrderStatusText => PlanExecutionStatusText(FinishOrderStatus);
    public string FinishInStatusText => PlanExecutionStatusText(FinishInStatus);
    public string InventoryOutStatusText => PlanExecutionStatusText(InventoryOutStatus);
    public string ReworkPlanInputStatusText => PlanExecutionStatusText(ReworkPlanInputStatus);
    public string InProcessReworkInputStatusText => PlanExecutionStatusText(InProcessReworkInputStatus);
    public string InMainInputStatusText => PlanExecutionStatusText(InMainInputStatus);

    private static string PlanExecutionStatusText(int status) => IntStatusDisplayHelper.GetPlanExecutionStatusText(status);

    // ========== 显示文本 ==========
    public string DelayPenaltyText => DelayPenalty ? "是" : "否";
    public string ScheduleStageText => IntStatusDisplayHelper.GetScheduleStageText(ScheduleStage);
    public string UrgingText => IsUrging ? "是" : "否";
    public string IsPreInputText => IsPreInput ? "是" : "否";
    public string BudgetInputDateText => BudgetInputDate?.ToString("yyyy-MM-dd") ?? "-";
}
