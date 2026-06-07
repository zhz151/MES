namespace MES.Data.Entities;

/// <summary>
/// 工单执行状况汇总表（物化读模型）
/// 通过 [即时更新] 按钮手动刷新，不提供手工增删改
/// </summary>
public class WorkOrderExecutionSummary : BaseEntity
{
    // ========== 工单标识 ==========
    /// <summary>工单ID（唯一，一个工单一条记录）</summary>
    public int WorkOrderId { get; set; }

    /// <summary>工单号</summary>
    public string WorkOrderNo { get; set; } = null!;

    // ========== Group 1: 工单基础数据 ==========
    public string Salesman { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public DateTime SignDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool DelayPenalty { get; set; }
    public string SettlementMethod { get; set; } = null!;
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string? ProductionSubNo { get; set; }
    public string MaterialName { get; set; } = null!;
    public string DeliveryState { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public string LengthStatus { get; set; } = null!;
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int TotalItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalMeters { get; set; }
    public decimal TotalWeight { get; set; }

    // ========== Group 2: 用料计划 ==========
    /// <summary>用料计划截止日期</summary>
    public DateTime? LatestPlanDate { get; set; }

    /// <summary>工单满足率(%)</summary>
    public decimal MaterialPlanRate { get; set; }

    /// <summary>工单用料计划状态(0=未计划 1=部分 2=理论满足 3=满足 4=超量)</summary>
    public int MaterialPlanStatus { get; set; }

    /// <summary>主号满足率(%)</summary>
    public decimal MainNoMaterialPlanRate { get; set; }

    /// <summary>关联主号用料状态</summary>
    public int MainNoMaterialPlanStatus { get; set; }

    /// <summary>工艺周期（天）：4种用料计划中 StandardCycle 的最大值，未计划时默认25</summary>
    public int ProcessCycle { get; set; }

    /// <summary>用料占比：4种料态中有做计划的种数(0-4)</summary>
    public int MaterialPlanCoveredCount { get; set; }

    /// <summary>用料占比文本：如"穿105% 荒160% 成20% 库40%"</summary>
    public string? MaterialPlanProportion { get; set; }

    /// <summary>要求到货日（最晚）：采购类取RequiredDate，库存/库料改制取PlanDate</summary>
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

    // ========== Group 3: 投料数据（所有关联批次） ==========
    /// <summary>投料起始日</summary>
    public DateTime? InputStartDate { get; set; }

    /// <summary>投料截止日</summary>
    public DateTime? InputEndDate { get; set; }

    /// <summary>投料总批次数</summary>
    public int TotalBatchCount { get; set; }

    /// <summary>投料总支数（SUM InputQuantity）</summary>
    public int InputQuantity { get; set; }

    /// <summary>投料总重量（SUM InputWeight）</summary>
    public decimal InputWeight { get; set; }

    /// <summary>理论生产成品支数</summary>
    public decimal TheoreticalOutputQty { get; set; }

    /// <summary>理论生产成品重量</summary>
    public decimal TheoreticalOutputWeight { get; set; }

    /// <summary>工单投料成品比(%)</summary>
    public decimal InputOutputRatio { get; set; }

    /// <summary>工单投料状态(0=未投料 1=部分 2=满足)</summary>
    public int InputStatus { get; set; }

    /// <summary>关联主号投料成品比(%)</summary>
    public decimal MainNoInputOutputRatio { get; set; }

    /// <summary>关联主号投料状态</summary>
    public int MainNoInputStatus { get; set; }

    // ========== Group 4: 有效数据（排除作废批次） ==========
    /// <summary>有效在产批次数（排除 Cancelled）</summary>
    public int ValidBatchCount { get; set; }

    /// <summary>有效投料总支数（SUM CurrentValidQty）</summary>
    public int ValidInputQuantity { get; set; }

    /// <summary>有效投料总重量（SUM CurrentValidWeight）</summary>
    public decimal ValidInputWeight { get; set; }

    /// <summary>有效生产成品支数</summary>
    public decimal ValidOutputQty { get; set; }

    /// <summary>有效生产成品重量</summary>
    public decimal ValidOutputWeight { get; set; }

    // ========== Group 6: 返整执行数据（ProductionType=返整 且 ManufacturingItem=订单成品） ==========
    /// <summary>返整投料截止日</summary>
    public DateTime? ReworkInputEndDate { get; set; }

    /// <summary>返整批次数</summary>
    public int ReworkBatchCount { get; set; }

    /// <summary>返整投料支数</summary>
    public int ReworkInputQuantity { get; set; }

    /// <summary>返整投料重量</summary>
    public decimal ReworkInputWeight { get; set; }

    /// <summary>返整理论成品支数</summary>
    public decimal ReworkTheoreticalOutputQty { get; set; }

    /// <summary>返整理论成品重量</summary>
    public decimal ReworkTheoreticalOutputWeight { get; set; }

    // ========== Group 7: 有效流转（Group 4 + Group 6 合并比值） ==========
    /// <summary>流转成品比(%)</summary>
    public decimal FlowOutputRatio { get; set; }

    /// <summary>有效流转状态(0=未投料 1=部分 2=满足)</summary>
    public int FlowStatus { get; set; }

    /// <summary>有效主号流转比(%)</summary>
    public decimal MainNoFlowOutputRatio { get; set; }

    /// <summary>有效主号状态(0=未计划 1=部分 2=满足)</summary>
    public int MainNoFlowStatus { get; set; }

    /// <summary>总批次数（制造物品=订单成品的所有批次计数）</summary>
    public int FlowTotalBatchCount { get; set; }

    /// <summary>未完成批数（上述批次中执行状态≠完成的计数）</summary>
    public int FlowIncompleteBatchCount { get; set; }

    /// <summary>剩余工量（天）：关联批次中最大 RemainingWorkDays</summary>
    public int FlowMaxRemainingWorkDays { get; set; }

    // ========== Group 8: 过程不合格（G3 − G4，负值归零） ==========
    /// <summary>原料不合格支数</summary>
    public int DefectiveRawQty { get; set; }

    /// <summary>原料不合格重量</summary>
    public decimal DefectiveRawWeight { get; set; }

    /// <summary>影响成品支数</summary>
    public decimal DefectiveOutputQty { get; set; }

    /// <summary>影响成品重量</summary>
    public decimal DefectiveOutputWeight { get; set; }

    /// <summary>不合格占比(%)</summary>
    public decimal DefectiveRatio { get; set; }

    // ========== Group 9: 成检不合格（从 FinalInspection 聚合） ==========
    /// <summary>成检起始日</summary>
    public DateTime? InspectionStartDate { get; set; }

    /// <summary>成检截止日</summary>
    public DateTime? InspectionEndDate { get; set; }

    /// <summary>成检不合格支数（总检验支数−总合格支数）</summary>
    public int InspectionDefectQty { get; set; }

    /// <summary>成检不合格重量</summary>
    public decimal InspectionDefectWeight { get; set; }

    /// <summary>成检不合格占比(%)</summary>
    public decimal InspectionDefectRatio { get; set; }

    // ========== Group 10: 汇总不合格 ==========
    /// <summary>一般问题重（=G6 返整理论成品重）</summary>
    public decimal GeneralDefectWeight { get; set; }

    /// <summary>一般问题占比(%)</summary>
    public decimal GeneralDefectRatio { get; set; }

    /// <summary>严重问题重（G8影响成品重+G9成检不合格重−G6返整理论成品重，负值归零）</summary>
    public decimal SeriousDefectWeight { get; set; }

    /// <summary>严重问题占比(%)</summary>
    public decimal SeriousDefectRatio { get; set; }

    /// <summary>成检报废重量</summary>
    public decimal ScrapWeight { get; set; }

    /// <summary>成检报废占比(%)</summary>
    public decimal ScrapRatio { get; set; }

    // ========== Group 11: 成品入库（从 InventoryBatch 聚合） ==========
    /// <summary>入库起始日</summary>
    public DateTime? WarehousingStartDate { get; set; }

    /// <summary>入库截止日</summary>
    public DateTime? WarehousingEndDate { get; set; }

    /// <summary>入库总支数（SUM InitialQuantity）</summary>
    public int WarehousingTotalQty { get; set; }

    /// <summary>入库总重量（SUM InitialWeight）</summary>
    public decimal WarehousingTotalWeight { get; set; }

    /// <summary>工单入库状态(0=无入库 1=入库部分 2=入库完结)</summary>
    public int WoWarehousingStatus { get; set; }

    /// <summary>主号入库状态(0=无入库 1=入库部分 2=入库完结)</summary>
    public int MainNoWarehousingStatus { get; set; }

    /// <summary>订单入库状态(0=无入库 1=入库部分 2=入库完结)</summary>
    public int OrderWarehousingStatus { get; set; }

    // ========== Group 12: 实时关注 ==========
    /// <summary>关注状态(0=工单完成 1=原料锁定 2=生产执行 3=成品检验)</summary>
    public int ScheduleStage { get; set; }

    /// <summary>剩余总工量（天）：根据关注状态取关联主号的工艺周期/剩余工量</summary>
    public int? TotalRemainingWorkDays { get; set; }

    /// <summary>产能工量（天）：主号汇总总量(吨) / 日产估算(吨/天)</summary>
    public int? CapacityWorkDays { get; set; }

    /// <summary>工单计划性（A+急/A急/B顺/C缓/D缓）</summary>
    public string? UrgencyLevel { get; set; }

    /// <summary>工艺预计完成日：今天 + 剩余总工量</summary>
    public DateTime? EstimatedProcessCompletionDate { get; set; }

    /// <summary>交期相差天数：工艺预计完成日 - 交货日期</summary>
    public int? DaysDiffFromDelivery { get; set; }

    /// <summary>原锁备注：原料锁定原因（A质量影响/B已购未回/C计划未执行/D未完善计划），仅ScheduleStage=1时有值</summary>
    public string? RawMaterialLockRemark { get; set; }

    // ========== Group 14: 在产节点待量（固定节点） ==========
    // 固定节点定义：(ProcessName, SectionName) 对
    // Pending 值 = 未到达 + 正在做指定工段且未完成的批次 CurrentValidWeight 累加
    // "未到达" = 批次当前工序 SequenceNumber < 目标节点 SequenceNumber
    // 冷拔为瞬时工序，不含"生产中"状态

    /// <summary>荒管处理·外抛光 待量(kg)</summary>
    public decimal? PendingSectionRoughTube { get; set; }

    /// <summary>在制修检·检验 待量(kg)</summary>
    public decimal? PendingSectionWarehouseFix { get; set; }

    /// <summary>60冷轧·冷轧拔 待量(kg)</summary>
    public decimal? PendingSection60Roll { get; set; }

    /// <summary>50冷轧·冷轧拔 待量(kg)</summary>
    public decimal? PendingSection50Roll { get; set; }

    /// <summary>30冷轧·冷轧拔 待量(kg)</summary>
    public decimal? PendingSection30Roll { get; set; }

    /// <summary>20冷轧·冷轧拔 待量(kg)</summary>
    public decimal? PendingSection20Roll { get; set; }

    /// <summary>三辊冷轧·冷轧拔 待量(kg)</summary>
    public decimal? PendingSectionThreeRoll { get; set; }

    /// <summary>冷拔·冷轧拔 待量(kg)</summary>
    public decimal? PendingSectionDrawBench { get; set; }

    /// <summary>变形工序是否完成（后6项之和=0→true）</summary>
    public bool DeformedProcessCompleted { get; set; }

    /// <summary>生产关注工序：前8项中值>0且SequenceNumber最小的工序名称</summary>
    public string? ProductionAttentionProcess { get; set; }

    // ========== 刷新追踪 ==========
    /// <summary>最后刷新时间</summary>
    public DateTime? LastRefreshTime { get; set; }
}
