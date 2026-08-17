using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 工单执行状况 DTO（列表页展示）
/// </summary>
public class WorkOrderExecutionSummaryDto
{
    public int Id { get; set; }

    // ========== 工单标识 ==========
    public int WorkOrderId { get; set; }
    public string WorkOrderNo { get; set; } = null!;
    public DateTime? LastRefreshTime { get; set; }

    // ========== Group 1: 工单基础数据 ==========
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

    // ========== Group 3: 用料计划及执行实况（G4~G11 的汇整，来自用料计划总览 WorkOrderListSummary） ==========
    /// <summary>工单计划状态(0=未计划 1=部分 2=满足 3=超量)</summary>
    public MaterialPlanStatus MaterialPlanStatus { get; set; }
    public string MaterialPlanStatusDisplay => EnumHelper.GetDisplayName(MaterialPlanStatus);

    /// <summary>主号满足率(%)</summary>
    public decimal MainNoMaterialPlanRate { get; set; }

    /// <summary>主号计划状态</summary>
    public MaterialPlanStatus MainNoMaterialPlanStatus { get; set; }
    public string MainNoMaterialPlanStatusDisplay => EnumHelper.GetDisplayName(MainNoMaterialPlanStatus);

    /// <summary>料态种数：4种料态中有做计划的种数(0-4)</summary>
    public int MaterialPlanCoveredCount { get; set; }

    /// <summary>用料占比文本：如"穿105% 荒160% 成20% 库40%"</summary>
    public string? MaterialPlanProportion { get; set; }

    /// <summary>理论截止投料日：交货日-(主号最大工艺周期+产能工量)</summary>
    public DateTime? TheoreticalCutoffDate { get; set; }

    /// <summary>截止到料日：仓库实际到料（G4~G6 委外/采购进库）与出库（G7/G8 生产领用）动作日期的最大值</summary>
    public DateTime? CutoffArrivalDate { get; set; }

    /// <summary>主号截止到料日：同主号各工单 CutoffArrivalDate 的最大值</summary>
    public DateTime? MainNoCutoffArrivalDate { get; set; }

    // ========== Group 11: 原始投料 ==========
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

    // ========== G4~G10: 7 种用料计划执行状况 ==========

    // G4: 圆棒穿孔
    public decimal PiercingPlanWeight { get; set; }
    public decimal PiercingSubOutWeight { get; set; }
    public int PiercingSubStatus { get; set; }
    public decimal PiercingSubInWeight { get; set; }
    public decimal PiercingSubPendingWeight { get; set; }
    public int PiercingReturnStatus { get; set; }

    // G5: 荒管采购
    public decimal SemiPlanWeight { get; set; }
    public decimal SemiOrderWeight { get; set; }
    public int SemiOrderStatus { get; set; }
    public decimal SemiInWeight { get; set; }
    public decimal SemiPendingWeight { get; set; }
    public int SemiInStatus { get; set; }

    // G6: 成品采购
    public decimal FinishPlanWeight { get; set; }
    public decimal FinishOrderWeight { get; set; }
    public int FinishOrderStatus { get; set; }
    public decimal FinishInWeight { get; set; }
    public decimal FinishPendingWeight { get; set; }
    public int FinishInStatus { get; set; }

    // G7: 库存使用
    public decimal InventoryPlanWeight { get; set; }
    public decimal InventoryOutWeight { get; set; }
    public int InventoryOutStatus { get; set; }

    // G8: 库料改制
    public decimal ReworkPlanWeight { get; set; }
    public decimal ReworkPlanInputWeight { get; set; }
    public int ReworkPlanInputStatus { get; set; }

    // G9: 在产改制
    public decimal InProcessReworkPlanWeight { get; set; }
    public decimal InProcessReworkInputWeight { get; set; }
    public int InProcessReworkInputStatus { get; set; }

    // G10: 在产主工单
    public decimal InMainPlanWeight { get; set; }
    public decimal InMainInputWeight { get; set; }
    public int InMainInputStatus { get; set; }

    // ========== Group 13: 原始投料有效流转 ==========
    public int ValidBatchCount { get; set; }
    public int ValidInputQuantity { get; set; }
    public decimal ValidInputWeight { get; set; }
    public decimal ValidOutputQty { get; set; }
    public decimal ValidOutputWeight { get; set; }
    // ========== Group 14: 返整执行 ==========
    public int? ReworkTheoreticalProduceQty { get; set; }
    public decimal? ReworkTheoreticalProduceWeight { get; set; }
    public decimal? PendingReworkOutputQty { get; set; }
    public decimal? PendingReworkOutputWeight { get; set; }
    public int ReworkMainNoStatus { get; set; }
    public string ReworkMainNoStatusText => IntStatusDisplayHelper.GetInputStatusText(ReworkMainNoStatus);
    public string? ReworkInputConsistency { get; set; }
    public string? ReworkInputConsistencyText => ReworkInputConsistency;
    public DateTime? ReworkInputEndDate { get; set; }
    public int ReworkBatchCount { get; set; }
    public int ReworkInputQuantity { get; set; }
    public decimal ReworkInputWeight { get; set; }
    public decimal ReworkTheoreticalOutputQty { get; set; }
    public decimal ReworkTheoreticalOutputWeight { get; set; }

    // ========== Group 15: 次品总量 ==========
    public int? ProcessInspectionDefectWeight { get; set; }
    public int? ProcessInspectionReworkWeight { get; set; }
    public int? ProcessInspectionWarehouseWeight { get; set; }
    public int? ProcessInspectionScrapWeight { get; set; }
    public int? FinalInspectionDefectQty { get; set; }
    public int? FinalInspectionDefectWeight { get; set; }
    public int? FinalInspectionReworkWeight { get; set; }
    public int? FinalInspectionWarehouseWeight { get; set; }
    public int? FinalInspectionScrapWeight { get; set; }

    // ========== Group 12: 实际生产总流转（G13~G15 的汇整） ==========
    public decimal FlowOutputRatio { get; set; }
    public int FlowStatus { get; set; }
    public decimal MainNoFlowOutputRatio { get; set; }
    public int MainNoFlowStatus { get; set; }
    public int FlowTotalBatchCount { get; set; }
    public int FlowIncompleteBatchCount { get; set; }
    public int FlowMaxRemainingWorkDays { get; set; }

    // ========== Group 16: 成品入库 ==========
    public DateTime? WarehousingStartDate { get; set; }
    public DateTime? WarehousingEndDate { get; set; }
    public int WarehousingTotalQty { get; set; }
    public decimal WarehousingTotalWeight { get; set; }
    public int WoWarehousingStatus { get; set; }
    public int MainNoWarehousingStatus { get; set; }
    public int OrderWarehousingStatus { get; set; }

    // ========== 显示用 ==========
    public string MaterialPlanStatusText => EnumHelper.GetDisplayName(MaterialPlanStatus);

    public string MainNoMaterialPlanStatusText => EnumHelper.GetDisplayName(MainNoMaterialPlanStatus);

    public string InputStatusText => IntStatusDisplayHelper.GetInputStatusText(InputStatus);

    public string MainNoInputStatusText => IntStatusDisplayHelper.GetInputStatusText(MainNoInputStatus);

    public string FlowStatusText => IntStatusDisplayHelper.GetInputStatusText(FlowStatus);

    public string MainNoFlowStatusText => IntStatusDisplayHelper.GetMainNoFlowStatusText(MainNoFlowStatus);

    public string DelayPenaltyText => DelayPenalty ? "是" : "否";

    // ========== G4~G10 状态文本（5档） ==========
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

    // ========== G3 汇总字段（工单号级，DTO 计算属性） ==========
    /// <summary>主号计划执行状态(0=无计划 1=未执行 2=执行中 3=计划落实)：同主号所有工单的计划/现可/缺失求和后按比例判定</summary>
    public int MainNoPlanExecutionStatus { get; set; }
    public string MainNoPlanExecutionStatusText => IntStatusDisplayHelper.GetMainNoPlanExecutionStatusText(MainNoPlanExecutionStatus);

    /// <summary>计划投料总重量(kg)：G4~G10 七个计划量之和</summary>
    public decimal TotalPlanWeight =>
        PiercingPlanWeight + SemiPlanWeight + FinishPlanWeight
        + InventoryPlanWeight + ReworkPlanWeight
        + InProcessReworkPlanWeight + InMainPlanWeight;

    /// <summary>现可投料总重量(kg)：G4委外到货 + G5采购到货 + G6采购到货 + G7出库量 + G8投料量 + G9投料量 + G10投料量（到货量口径：下单≠到货，未收货的量不视为"现可投料"）</summary>
    public decimal TotalAvailableWeight =>
        PiercingSubInWeight + SemiInWeight + FinishInWeight
        + InventoryOutWeight + ReworkPlanInputWeight
        + InProcessReworkInputWeight + InMainInputWeight;

    /// <summary>理论缺失总料重量(kg)：计划投料总重量 − 现可投料总重量</summary>
    public decimal TotalMissingWeight => Math.Max(0m, TotalPlanWeight - TotalAvailableWeight);

    /// <summary>实际已投料量(kg) = 投料总重量(InputWeight)</summary>
    public decimal ActualInputWeight => InputWeight;

    /// <summary>实投主号状态 = 关联主号投料状态(MainNoInputStatus)</summary>
    public int ActualMainNoInputStatus => MainNoInputStatus;
    public string ActualMainNoInputStatusText => MainNoInputStatusText;

    /// <summary>
    /// 到料实投一致性：0=一致 1=待投 2=疑问-到料少投 3=疑问-到料超投 4=错误-无料已投。
    /// 判定基准：实际已投料量(InputWeight) vs 现可投料总重(TotalAvailableWeight，到货量口径，下单未到货的量不视为现可)。
    /// 错误(4)=无到料已投（已投&gt;0 且 现可=0，计划外投料，最异常）；
    /// 疑问-到料超投(3)=已投&gt;现可×1.03（超投）；
    /// 投料滞后（已投&lt;现可×0.97）按下料到位时点细分：截止到料日=今天→待投(1)（操作时间差，正常）；
    /// 早于今天→疑问-到料少投(2)（料已到位需投未投，存在问题）；晚于今天或空→一致(0)（料未到位，投料滞后正常）；
    /// 一致(0)=已投≈现可（±3% 内）或双零。
    /// 阶段门控（判定顺序最前）：主号关注=生产执行(3)/成品检验(4)/主号完成(1) 已过投料期，不再细看比例——
    /// 理论缺失总料重&gt;计划投料总重×3% → 5 错误-无需投料（本应无需投料却仍缺料，缺口率&gt;3% 需修正计划残留）；其余（含缺口≤3% 容差内）→ 6 略（降噪不细看）。
    /// </summary>
    public int PlanInputConsistency
    {
        get
        {
            // 阶段门控：主号关注=生产执行(3)/成品检验(4)/主号完成(1) → 已过投料期，仅按缺失量判定（缺口率>计划×3% 才标错误，容差内归略）
            if (ScheduleStage is 1 or 3 or 4)
                return TotalMissingWeight > TotalPlanWeight * 0.03m ? 5 : 6;
            // 错误-无料已投(4)：实际已投料量>0 但 现可投料总重=0 —— 无到料/无执行动作却投了料（计划外投料，最异常）
            if (ActualInputWeight > 0 && TotalAvailableWeight <= 0) return 4;
            // 现可=0 且 已投=0 → 一致（无执行、无投料，无矛盾）
            if (TotalAvailableWeight <= 0) return 0;
            // 疑问-到料超投(3)：已投 > 现可×1.03（超投）
            if (ActualInputWeight > TotalAvailableWeight * 1.03m) return 3;
            // 投料滞后（已投 < 现可×0.97）：按下料到位时点细分
            if (ActualInputWeight < TotalAvailableWeight * 0.97m)
            {
                if (!CutoffArrivalDate.HasValue) return 0;          // 空 → 一致（料未到位，投料滞后正常）
                var d = CutoffArrivalDate.Value.Date;
                if (d < DateTime.Today) return 2;                   // 早于今天 → 疑问-到料少投（料已到位需投未投）
                if (d == DateTime.Today) return 1;                  // 今天 → 待投（操作时间差，正常）
                return 0;                                           // 晚于今天 → 一致（料未到位）
            }
            // 一致(0)：已投≈现可（±3% 内）
            return 0;
        }
    }

    public string PlanInputConsistencyText => IntStatusDisplayHelper.GetPlanInputConsistencyText(PlanInputConsistency);

    // ========== Group 17: 实时关注 ==========
    public int ScheduleStage { get; set; }
    public int? TotalRemainingWorkDays { get; set; }
    public int? CapacityWorkDays { get; set; }
    public string? UrgencyLevel { get; set; }
    public DateTime? EstimatedProcessCompletionDate { get; set; }
    public int? DaysDiffFromDelivery { get; set; }
    public string? RawMaterialLockRemark { get; set; }

    // ========== Group 16 状态文本（入库状态） ==========
    public string WoWarehousingStatusText => IntStatusDisplayHelper.GetWarehousingStatusText(WoWarehousingStatus);

    public string MainNoWarehousingStatusText => IntStatusDisplayHelper.GetMainNoWarehousingStatusText(MainNoWarehousingStatus);

    public string OrderWarehousingStatusText => IntStatusDisplayHelper.GetWarehousingStatusText(OrderWarehousingStatus);

    // ========== Group 17 主号关注文本 ==========
    public string ScheduleStageText => IntStatusDisplayHelper.GetScheduleStageText(ScheduleStage);

    // ========== Group 18: 在产节点待量 ==========
    public decimal? PendingSectionRoughTube { get; set; }
    public decimal? PendingSectionWarehouseFix { get; set; }
    public decimal? PendingSection60Roll { get; set; }
    public decimal? PendingSection50Roll { get; set; }
    public decimal? PendingSection30Roll { get; set; }
    public decimal? PendingSection20Roll { get; set; }
    public decimal? PendingSectionThreeRoll { get; set; }
    public decimal? PendingSectionDrawBench { get; set; }
    public bool? DeformedProcessCompleted { get; set; }
    public string? ProductionAttentionProcess { get; set; }
    public int? MaxBatchRemainingWorkDays { get; set; }
    public string? MainNoAttentionProcess { get; set; }

    // ========== Group 2: 工单需求调整 ==========
    public bool IsUrging { get; set; }
    public bool IsBatchDelivery { get; set; }
    public bool IsPaused { get; set; }
    public string? AdjustmentRemark { get; set; }

    // ========== 生产流转性（实体字段，RefreshAllAsync 时计算填入） ==========
    public string? ProductionFlowProperty { get; set; }
}
