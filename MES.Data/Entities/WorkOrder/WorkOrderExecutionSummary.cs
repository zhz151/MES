namespace MES.Data.Entities.WorkOrder;

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

    /// <summary>最终客户（终端用户，从 SalesOrder.EndCustomer 快照）</summary>
    public string? EndCustomer { get; set; }
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

    // ========== Group 3: 用料计划及执行实况（G4~G11 的汇整） ==========
    /// <summary>工单用料计划状态(0=未计划 1=部分 2=理论满足 3=满足 4=超量)</summary>
    public int MaterialPlanStatus { get; set; }

    /// <summary>主号满足率(%)</summary>
    public decimal MainNoMaterialPlanRate { get; set; }

    /// <summary>关联主号用料状态</summary>
    public int MainNoMaterialPlanStatus { get; set; }

    /// <summary>主号计划执行状态(0=无计划 1=未执行 2=部分 3=已完成 4=异常)：同主号所有工单 G4~G10 执行状态取最差</summary>
    public int MainNoPlanExecutionStatus { get; set; }

    /// <summary>工艺周期（天）：用于 G16 剩余工量计算，取值由 RefreshAllAsync 兜底处理</summary>
    public int ProcessCycle { get; set; }

    /// <summary>用料占比：4种料态中有做计划的种数(0-4)</summary>
    public int MaterialPlanCoveredCount { get; set; }

    /// <summary>用料占比文本：如"穿105% 荒160% 成20% 库40%"</summary>
    public string? MaterialPlanProportion { get; set; }

    /// <summary>理论截止投料日：交货日-(主号最大工艺周期+产能工量)，来自用料计划总览</summary>
    public DateTime? TheoreticalCutoffDate { get; set; }

    /// <summary>截止到料日：仓库实际到料（G4~G6 委外/采购进库）与出库（G7/G8 生产领用）动作日期的最大值，仅与仓库到料+出库相关</summary>
    public DateTime? CutoffArrivalDate { get; set; }

    /// <summary>主号截止到料日：同主号各工单 CutoffArrivalDate 的最大值</summary>
    public DateTime? MainNoCutoffArrivalDate { get; set; }

    // ========== Group 3: 圆棒穿孔计划执行 ==========
    /// <summary>计划量(kg)</summary>
    public decimal PiercingPlanWeight { get; set; }
    /// <summary>委外量(kg)</summary>
    public decimal PiercingSubOutWeight { get; set; }
    /// <summary>计划状态：0无计划 1未执行 2部分 3已完成 4异常</summary>
    public int PiercingSubStatus { get; set; }
    /// <summary>已回收量(kg)</summary>
    public decimal PiercingSubInWeight { get; set; }
    /// <summary>待回收量(kg)</summary>
    public decimal PiercingSubPendingWeight { get; set; }
    /// <summary>执行状态：0无计划 1未执行 2部分 3已完成 4异常</summary>
    public int PiercingReturnStatus { get; set; }

    // ========== Group 4: 荒管采购计划执行 ==========
    /// <summary>计划量(kg)</summary>
    public decimal SemiPlanWeight { get; set; }
    /// <summary>采购量(kg)</summary>
    public decimal SemiOrderWeight { get; set; }
    /// <summary>计划状态：0无计划 1未执行 2部分 3已完成 4异常</summary>
    public int SemiOrderStatus { get; set; }
    /// <summary>已到货量(kg)</summary>
    public decimal SemiInWeight { get; set; }
    /// <summary>未到货量(kg)</summary>
    public decimal SemiPendingWeight { get; set; }
    /// <summary>执行状态：0无计划 1未执行 2部分 3已完成 4异常</summary>
    public int SemiInStatus { get; set; }

    // ========== Group 5: 成品采购计划执行 ==========
    /// <summary>计划量(kg)</summary>
    public decimal FinishPlanWeight { get; set; }
    /// <summary>采购量(kg)</summary>
    public decimal FinishOrderWeight { get; set; }
    /// <summary>计划状态：0无计划 1未执行 2部分 3已完成 4异常</summary>
    public int FinishOrderStatus { get; set; }
    /// <summary>已到货量(kg)</summary>
    public decimal FinishInWeight { get; set; }
    /// <summary>未到货量(kg)</summary>
    public decimal FinishPendingWeight { get; set; }
    /// <summary>执行状态：0无计划 1未执行 2部分 3已完成 4异常</summary>
    public int FinishInStatus { get; set; }

    // ========== Group 6: 库存使用计划执行 ==========
    /// <summary>计划量(kg)</summary>
    public decimal InventoryPlanWeight { get; set; }
    /// <summary>出库量(kg)</summary>
    public decimal InventoryOutWeight { get; set; }
    /// <summary>执行状态：0无计划 1未执行 2部分 3已完成 4异常</summary>
    public int InventoryOutStatus { get; set; }

    // ========== Group 7: 库料改制计划执行 ==========
    /// <summary>计划量(kg)</summary>
    public decimal ReworkPlanWeight { get; set; }
    /// <summary>投料量(kg)</summary>
    public decimal ReworkPlanInputWeight { get; set; }
    /// <summary>执行状态：0无计划 1未执行 2部分 3已完成 4异常</summary>
    public int ReworkPlanInputStatus { get; set; }

    // ========== Group 8: 在产改制计划执行 ==========
    /// <summary>计划量(kg)</summary>
    public decimal InProcessReworkPlanWeight { get; set; }
    /// <summary>投料量(kg)</summary>
    public decimal InProcessReworkInputWeight { get; set; }
    /// <summary>执行状态：0无计划 1未执行 2部分 3已完成 4异常</summary>
    public int InProcessReworkInputStatus { get; set; }

    // ========== Group 9: 在产主工单计划执行 ==========
    /// <summary>计划量(kg)</summary>
    public decimal InMainPlanWeight { get; set; }
    /// <summary>投料量(kg)</summary>
    public decimal InMainInputWeight { get; set; }
    /// <summary>执行状态：0无计划 1未执行 2部分 3已完成 4异常</summary>
    public int InMainInputStatus { get; set; }

    // ========== Group 5（旧）: 物料执行实时信息（从采购订单聚合） ==========
    // 前端显示为：物料执行（已废弃）
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

    // ========== Group 11: 原始投料（所有关联批次） ==========
    // 前端显示为 G11：原始投料
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

    // ========== Group 13: 原始投料有效流转（排除作废批次） ==========
    // 前端显示为 G13：合格流转
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

    // ========== Group 14: 返整执行数据（ProductionType=返整 且 ManufacturingItem=订单成品） ==========
    // 前端显示为 G14：返整执行
    /// <summary>理论返整可产成支（支，整数值） = Σ(每条返整记录重量 ÷ 该记录原批次单支重)</summary>
    public int? ReworkTheoreticalProduceQty { get; set; }

    /// <summary>理论返整可产成重(kg) = 过程检返整量×0.92 + 成品检返整量×0.96（无返整量为空）</summary>
    public decimal? ReworkTheoreticalProduceWeight { get; set; }

    /// <summary>待返整成支 = 理论返整可产成支 − 返整理论成品支（无返整为空，负值归0）</summary>
    public decimal? PendingReworkOutputQty { get; set; }

    /// <summary>待返整成重 = 理论返整可产成重 − 返整理论成品重（无返整为空，负值归0）</summary>
    public decimal? PendingReworkOutputWeight { get; set; }

    /// <summary>附返整主号状态（0=未投料/1=部分/2=满足，主号级：有效流转基础上加待返整后按主号总需求判定）</summary>
    public int ReworkMainNoStatus { get; set; }

    /// <summary>是否必返整（是/否，主号级：附返整主号状态=满足 且 有效主号状态≠满足 时为"是"）</summary>
    public string? ReworkInputConsistency { get; set; }

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

    // ========== Group 15: 次品总量（过程检/成检次品聚合，仅订单成品批次） ==========
    // 前端显示为 G15：次品总量（位于合格流转之后）
    /// <summary>过程检次品总重(kg) = Σ(理论返整重 + 理论入库重 + 理论报废重)</summary>
    public int? ProcessInspectionDefectWeight { get; set; }

    /// <summary>过程检返整重(kg) = Σ 过程检验理论返整重</summary>
    public int? ProcessInspectionReworkWeight { get; set; }

    /// <summary>过程检入库重(kg) = Σ 过程检验理论入库重</summary>
    public int? ProcessInspectionWarehouseWeight { get; set; }

    /// <summary>过程检报废重(kg) = Σ 过程检验理论报废重</summary>
    public int? ProcessInspectionScrapWeight { get; set; }

    /// <summary>成检次品总支(支) = Σ(返整支数 + 入库支数 + 报废支数)</summary>
    public int? FinalInspectionDefectQty { get; set; }

    /// <summary>成检次品总重(kg) = Σ(返整重 + 入库重 + 报废重)</summary>
    public int? FinalInspectionDefectWeight { get; set; }

    /// <summary>成品检返整重(kg) = Σ 成品检验返整重</summary>
    public int? FinalInspectionReworkWeight { get; set; }

    /// <summary>成检入库重(kg) = Σ 成品检验入库重</summary>
    public int? FinalInspectionWarehouseWeight { get; set; }

    /// <summary>成检报废重(kg) = Σ 成品检验报废重</summary>
    public int? FinalInspectionScrapWeight { get; set; }

    // ========== Group 12: 实际生产总流转（G13~G15 的汇整） ==========
    // 前端显示为 G12：有效流转
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

    // ========== Group 15: 成品入库（从 InventoryBatch 聚合） ==========
    // 前端显示为 G15：成品入库
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

    // ========== Group 16: 实时关注 ==========
    // 前端显示为 G16：实时关注
    /// <summary>主号关注(0=主号暂停 1=主号完成 2=原料锁定 3=生产执行 4=成品检验)</summary>
    public int ScheduleStage { get; set; }

    /// <summary>剩余总工量（天）：根据主号关注取关联主号的工艺周期/剩余工量</summary>
    public int? TotalRemainingWorkDays { get; set; }

    /// <summary>产能工量（天）：主号汇总总量(吨) / 日产估算(吨/天)</summary>
    public int? CapacityWorkDays { get; set; }

    /// <summary>主号计划性（A+急/A急/B顺/C缓/D缓）</summary>
    public string? UrgencyLevel { get; set; }

    /// <summary>工艺预计完成日：今天 + 剩余总工量</summary>
    public DateTime? EstimatedProcessCompletionDate { get; set; }

    /// <summary>交期相差天数：工艺预计完成日 - 交货日期</summary>
    public int? DaysDiffFromDelivery { get; set; }

    /// <summary>原锁备注：原料锁定原因（A质量补料/B执行返整/C执行计划/D完善计划），仅ScheduleStage=2时有值</summary>
    public string? RawMaterialLockRemark { get; set; }

    // ========== Group 17: 在产节点待量（固定节点） ==========
    // 前端显示为 G17：在产节点待量
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

    /// <summary>变形工序完成三档：null=略（无在产批次）/ true=是（后6项之和=0，收尾）/ false=否（后6项之和&gt;0）</summary>
    public bool? DeformedProcessCompleted { get; set; }

    /// <summary>生产关注工序：前8项中值>0且SequenceNumber最小的工序名称</summary>
    public string? ProductionAttentionProcess { get; set; }

    /// <summary>最大剩余工量（天）：此工单号下所有批次中 RemainingWorkDays 的最大值</summary>
    public int? MaxBatchRemainingWorkDays { get; set; }

    /// <summary>主号关注工序：同订单号+同主号下，取剩余工量最大值所在工单的生产关注工序</summary>
    public string? MainNoAttentionProcess { get; set; }

    // ========== Group 2: 工单需求调整 ==========
    // 前端显示为 G2：工单需求调整
    /// <summary>催单</summary>
    public bool IsUrging { get; set; }

    /// <summary>分批交货</summary>
    public bool IsBatchDelivery { get; set; }

    /// <summary>暂停</summary>
    public bool IsPaused { get; set; }

    /// <summary>调整备注</summary>
    public string? AdjustmentRemark { get; set; }

    // ========== 生产流转性（持久化字段，RefreshAllAsync 时计算填入） ==========
    /// <summary>生产流转性：暂停/正常/待料/疑问/略</summary>
    public string? ProductionFlowProperty { get; set; }

    // ========== 刷新追踪 ==========
    /// <summary>最后刷新时间</summary>
    public DateTime? LastRefreshTime { get; set; }
}
