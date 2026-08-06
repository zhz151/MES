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

    // ========== Group 2: 用料计划（来自用料计划总览 WorkOrderListSummary） ==========
    /// <summary>工单计划状态(0=未计划 1=部分 2=理论满足 3=满足 4=超量)</summary>
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

    // ========== G11: 原始投料 ==========
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

    // ========== Group 13: 合格流转 ==========
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

    // ========== Group 21: 次品总量 ==========
    public int? ProcessInspectionDefectWeight { get; set; }
    public int? ProcessInspectionReworkWeight { get; set; }
    public int? ProcessInspectionWarehouseWeight { get; set; }
    public int? ProcessInspectionScrapWeight { get; set; }
    public int? FinalInspectionDefectQty { get; set; }
    public int? FinalInspectionDefectWeight { get; set; }
    public int? FinalInspectionReworkWeight { get; set; }
    public int? FinalInspectionWarehouseWeight { get; set; }
    public int? FinalInspectionScrapWeight { get; set; }

    // ========== Group 12: 有效流转 ==========
    public decimal FlowOutputRatio { get; set; }
    public int FlowStatus { get; set; }
    public decimal MainNoFlowOutputRatio { get; set; }
    public int MainNoFlowStatus { get; set; }
    public int FlowTotalBatchCount { get; set; }
    public int FlowIncompleteBatchCount { get; set; }
    public int FlowMaxRemainingWorkDays { get; set; }

    // ========== Group 15: 成品入库 ==========
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
    /// <summary>计划投料总重量(kg)：G4~G10 七个计划量之和</summary>
    public decimal TotalPlanWeight =>
        PiercingPlanWeight + SemiPlanWeight + FinishPlanWeight
        + InventoryPlanWeight + ReworkPlanWeight
        + InProcessReworkPlanWeight + InMainPlanWeight;

    /// <summary>现可投料总重量(kg)：G4回收量 + G5到货量 + G6到货量 + G7出库量 + G8投料量 + G9投料量 + G10投料量</summary>
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

    /// <summary>计划实投一致性：0=一致 1=疑问 2=错误</summary>
    public int PlanInputConsistency
    {
        get
        {
            // (1) 实际投料量 > 可投料总重量 × 1.1 → 疑问
            if (TotalPlanWeight > 0 && InputWeight > TotalAvailableWeight * 1.1m)
                return 1;

            // (2) 实投主号状态="满足"(2) 但 G4~G10 中存在"部分"(2) → 错误
            if (MainNoInputStatus == 2)
            {
                var hasPartial = PiercingSubStatus == 2 || PiercingReturnStatus == 2
                    || SemiOrderStatus == 2 || SemiInStatus == 2
                    || FinishOrderStatus == 2 || FinishInStatus == 2
                    || InventoryOutStatus == 2 || ReworkPlanInputStatus == 2
                    || InProcessReworkInputStatus == 2 || InMainInputStatus == 2;
                if (hasPartial) return 2;
            }

            return 0;
        }
    }

    public string PlanInputConsistencyText => IntStatusDisplayHelper.GetPlanInputConsistencyText(PlanInputConsistency);

    // ========== G16: 实时关注 ==========
    public int ScheduleStage { get; set; }
    public int? TotalRemainingWorkDays { get; set; }
    public int? CapacityWorkDays { get; set; }
    public string? UrgencyLevel { get; set; }
    public DateTime? EstimatedProcessCompletionDate { get; set; }
    public int? DaysDiffFromDelivery { get; set; }
    public string? RawMaterialLockRemark { get; set; }

    // ========== G15 状态文本（入库状态） ==========
    public string WoWarehousingStatusText => IntStatusDisplayHelper.GetWarehousingStatusText(WoWarehousingStatus);

    public string MainNoWarehousingStatusText => IntStatusDisplayHelper.GetMainNoWarehousingStatusText(MainNoWarehousingStatus);

    public string OrderWarehousingStatusText => IntStatusDisplayHelper.GetWarehousingStatusText(OrderWarehousingStatus);

    // ========== G16 关注状态文本 ==========
    public string ScheduleStageText => IntStatusDisplayHelper.GetScheduleStageText(ScheduleStage);

    // ========== Group 17: 在产节点待量 ==========
    public decimal? PendingSectionRoughTube { get; set; }
    public decimal? PendingSectionWarehouseFix { get; set; }
    public decimal? PendingSection60Roll { get; set; }
    public decimal? PendingSection50Roll { get; set; }
    public decimal? PendingSection30Roll { get; set; }
    public decimal? PendingSection20Roll { get; set; }
    public decimal? PendingSectionThreeRoll { get; set; }
    public decimal? PendingSectionDrawBench { get; set; }
    public bool DeformedProcessCompleted { get; set; }
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
