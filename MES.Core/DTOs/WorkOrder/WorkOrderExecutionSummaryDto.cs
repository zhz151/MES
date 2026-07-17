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

    // ========== Group 2: 用料计划 ==========
    public DateTime? LatestPlanDate { get; set; }
    public decimal MaterialPlanRate { get; set; }
    public MaterialPlanStatus MaterialPlanStatus { get; set; }
    public string MaterialPlanStatusDisplay => EnumHelper.GetDisplayName(MaterialPlanStatus);
    public decimal MainNoMaterialPlanRate { get; set; }
    public MaterialPlanStatus MainNoMaterialPlanStatus { get; set; }
    public string MainNoMaterialPlanStatusDisplay => EnumHelper.GetDisplayName(MainNoMaterialPlanStatus);

    /// <summary>工艺周期（天）</summary>
    public int ProcessCycle { get; set; }

    /// <summary>用料占比：4种料态中有做计划的种数(0-4)</summary>
    public int MaterialPlanCoveredCount { get; set; }

    /// <summary>用料占比文本：如"穿105% 荒160% 成20% 库40%"</summary>
    public string? MaterialPlanProportion { get; set; }

    /// <summary>要求到货日（最晚）</summary>
    public DateTime? LatestRequiredDate { get; set; }

    // ========== Group 5: 物料执行实时信息（从采购订单聚合） ==========
    /// <summary>待回荒管支数</summary>
    public int PendingRoughTubeQty { get; set; }

    /// <summary>待回荒管重量</summary>
    public decimal PendingRoughTubeWeight { get; set; }

    /// <summary>待回外购成支</summary>
    public int PendingOutsourceFinishQty { get; set; }

    /// <summary>待回外购成重</summary>
    public decimal PendingOutsourceFinishWeight { get; set; }

    /// <summary>理论成品支（Σ 每笔待回收支 × 投料倍率）</summary>
    public decimal TheoreticalFinishQty { get; set; }

    /// <summary>理论成品重（待回荒管重量 × 0.92 + 待回外购成重）</summary>
    public decimal TheoreticalFinishWeight { get; set; }

    // ========== Group 3: 投料数据 ==========
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

    // ========== Group 4: 有效数据 ==========
    public int ValidBatchCount { get; set; }
    public int ValidInputQuantity { get; set; }
    public decimal ValidInputWeight { get; set; }
    public decimal ValidOutputQty { get; set; }
    public decimal ValidOutputWeight { get; set; }
    // ========== Group 6: 返整执行数据 ==========
    public DateTime? ReworkInputEndDate { get; set; }
    public int ReworkBatchCount { get; set; }
    public int ReworkInputQuantity { get; set; }
    public decimal ReworkInputWeight { get; set; }
    public decimal ReworkTheoreticalOutputQty { get; set; }
    public decimal ReworkTheoreticalOutputWeight { get; set; }

    // ========== Group 7: 有效流转 ==========
    public decimal FlowOutputRatio { get; set; }
    public int FlowStatus { get; set; }
    public decimal MainNoFlowOutputRatio { get; set; }
    public int MainNoFlowStatus { get; set; }
    public int FlowTotalBatchCount { get; set; }
    public int FlowIncompleteBatchCount { get; set; }
    public int FlowMaxRemainingWorkDays { get; set; }

    // ========== Group 8: 过程不合格 ==========
    public int DefectiveRawQty { get; set; }
    public decimal DefectiveRawWeight { get; set; }
    public decimal DefectiveOutputQty { get; set; }
    public decimal DefectiveOutputWeight { get; set; }
    public decimal DefectiveRatio { get; set; }

    // ========== Group 9: 成检不合格 ==========
    public DateTime? InspectionStartDate { get; set; }
    public DateTime? InspectionEndDate { get; set; }
    public int InspectionDefectQty { get; set; }
    public decimal InspectionDefectWeight { get; set; }
    public decimal InspectionDefectRatio { get; set; }

    // ========== Group 10: 汇总不合格 ==========
    public decimal GeneralDefectWeight { get; set; }
    public decimal GeneralDefectRatio { get; set; }
    public decimal SeriousDefectWeight { get; set; }
    public decimal SeriousDefectRatio { get; set; }
    public decimal ScrapWeight { get; set; }
    public decimal ScrapRatio { get; set; }

    // ========== Group 11: 成品入库 ==========
    public DateTime? WarehousingStartDate { get; set; }
    public DateTime? WarehousingEndDate { get; set; }
    public int WarehousingTotalQty { get; set; }
    public decimal WarehousingTotalWeight { get; set; }
    public int WoWarehousingStatus { get; set; }
    public int MainNoWarehousingStatus { get; set; }
    public int OrderWarehousingStatus { get; set; }

    // ========== 显示用 ==========
    public string MaterialPlanStatusText => MaterialPlanStatus switch
    {
        MaterialPlanStatus.NotPlanned => "未计划",
        MaterialPlanStatus.Partial => "部分",
        MaterialPlanStatus.TheoreticalSatisfied => "理论满足",
        MaterialPlanStatus.Satisfied => "满足",
        MaterialPlanStatus.Excess => "超量",
        _ => "未知"
    };

    public string MainNoMaterialPlanStatusText => MainNoMaterialPlanStatus switch
    {
        MaterialPlanStatus.NotPlanned => "未计划",
        MaterialPlanStatus.Partial => "部分",
        MaterialPlanStatus.TheoreticalSatisfied => "理论满足",
        MaterialPlanStatus.Satisfied => "满足",
        MaterialPlanStatus.Excess => "超量",
        _ => "未知"
    };

    public string InputStatusText => InputStatus switch
    {
        0 => "未投料",
        1 => "部分",
        2 => "满足",
        _ => "未知"
    };

    public string MainNoInputStatusText => MainNoInputStatus switch
    {
        0 => "未投料",
        1 => "部分",
        2 => "满足",
        _ => "未知"
    };

    public string FlowStatusText => FlowStatus switch
    {
        0 => "未投料",
        1 => "部分",
        2 => "满足",
        _ => "未知"
    };

    public string MainNoFlowStatusText => MainNoFlowStatus switch
    {
        0 => "未计划",
        1 => "部分",
        2 => "满足",
        _ => "未知"
    };

    public string DelayPenaltyText => DelayPenalty ? "是" : "否";

    // ========== G12: 关注状态 ==========
    public int ScheduleStage { get; set; }
    public int? TotalRemainingWorkDays { get; set; }
    public int? CapacityWorkDays { get; set; }
    public string? UrgencyLevel { get; set; }
    public DateTime? EstimatedProcessCompletionDate { get; set; }
    public int? DaysDiffFromDelivery { get; set; }
    public string? RawMaterialLockRemark { get; set; }

    // ========== G11 状态文本 ==========
    public string WoWarehousingStatusText => WoWarehousingStatus switch
    {
        0 => "无入库",
        1 => "入库部分",
        2 => "入库完结",
        _ => "未知"
    };

    public string MainNoWarehousingStatusText => MainNoWarehousingStatus switch
    {
        0 => "无入库",
        1 => "入库部分",
        2 => "入库完结",
        _ => "未知"
    };

    public string OrderWarehousingStatusText => OrderWarehousingStatus switch
    {
        0 => "无入库",
        1 => "入库部分",
        2 => "入库完结",
        _ => "未知"
    };

    // ========== G12 关注状态文本 ==========
    public string ScheduleStageText => ScheduleStage switch
    {
        0 => "工单完成",
        1 => "原料锁定",
        2 => "生产执行",
        3 => "成品检验",
        _ => "未知"
    };

    // ========== Group 14: 在产节点待量 ==========
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

    // ========== Group 13: 工单需求调整 ==========
    public bool IsUrging { get; set; }
    public bool IsBatchDelivery { get; set; }
    public bool IsPaused { get; set; }
    public string? AdjustmentRemark { get; set; }

    // ========== 生产流转性（实体字段，RefreshAllAsync 时计算填入） ==========
    public string? ProductionFlowProperty { get; set; }
}
