using MES.Core.Enums;

namespace MES.Data.Entities.WorkOrder;

/// <summary>
/// 用料计划总览读模型（物化表，从 WorkOrders + 4 种计划表 + CustomerProfiles 聚合计算）
/// 在工单/计划/客户变更时自动刷新
/// </summary>
public class WorkOrderListSummary : BaseEntity
{
    // ========== Group A: WorkOrder 基础字段 ==========

    /// <summary>工单ID（唯一，一个工单一条记录）</summary>
    public int WorkOrderId { get; set; }

    /// <summary>工单号</summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>源订单号</summary>
    public string SalesOrderNo { get; set; } = null!;

    /// <summary>主号</summary>
    public string ProductionMainNo { get; set; } = null!;

    /// <summary>次号</summary>
    public string? ProductionSubNo { get; set; }

    /// <summary>项次序列号集合（逗号分隔）</summary>
    public string? OrderItemIds { get; set; }

    /// <summary>签订日期</summary>
    public DateTime SignDate { get; set; }

    /// <summary>业务员</summary>
    public string Salesman { get; set; } = null!;

    /// <summary>最终用户</summary>
    public string? EndCustomer { get; set; }

    /// <summary>交货日期</summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>延期罚款</summary>
    public bool DelayPenalty { get; set; }

    /// <summary>结算方式</summary>
    public string SettlementMethod { get; set; } = null!;

    /// <summary>物料名称</summary>
    public string MaterialName { get; set; } = null!;

    /// <summary>标准/代号</summary>
    public string? StandardCode { get; set; }

    /// <summary>交货状态</summary>
    public string DeliveryState { get; set; } = null!;

    /// <summary>工厂牌号</summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>规格</summary>
    public string Specification { get; set; } = null!;

    /// <summary>外径下偏差</summary>
    public decimal? OuterDiameterNegative { get; set; }

    /// <summary>外径上偏差</summary>
    public decimal? OuterDiameterPositive { get; set; }

    /// <summary>壁厚下偏差</summary>
    public decimal? WallThicknessNegative { get; set; }

    /// <summary>壁厚上偏差</summary>
    public decimal? WallThicknessPositive { get; set; }

    /// <summary>长度状态</summary>
    public string LengthStatus { get; set; } = null!;

    /// <summary>最小长度</summary>
    public decimal? MinLength { get; set; }

    /// <summary>最大长度</summary>
    public decimal? MaxLength { get; set; }

    /// <summary>总支数</summary>
    public int TotalQuantity { get; set; }

    /// <summary>总米数</summary>
    public decimal TotalMeters { get; set; }

    /// <summary>总重量</summary>
    public decimal TotalWeight { get; set; }

    /// <summary>总项次数</summary>
    public int TotalItemCount { get; set; }

    /// <summary>项次明细</summary>
    public string? ItemDetails { get; set; }

    /// <summary>技术要求</summary>
    public string TechnicalRequirements { get; set; } = null!;

    /// <summary>工单状态(0=未编制,1=已确定,2=待修正,3=已取消)</summary>
    public int Status { get; set; }

    /// <summary>创建时间（工单创建时快照，显式隐藏 BaseEntity.CreatedTime）</summary>
    public new DateTimeOffset CreatedTime { get; set; }

    // ========== Group B: 预计算计划聚合 ==========

    /// <summary>最新计划日期</summary>
    public DateTime? LatestPlanDate { get; set; }

    /// <summary>工单满足率(%)</summary>
    public decimal MaterialPlanRate { get; set; }

    /// <summary>工单用料计划状态(0=未计划,1=部分,2=满足,3=超量)</summary>
    public int MaterialPlanStatus { get; set; }

    /// <summary>原料采购计划总重量(kg)</summary>
    public decimal? SemiPlanTotalWeight { get; set; }

    /// <summary>原料采购计划总支数</summary>
    public int? SemiPlanTotalPieces { get; set; }

    /// <summary>成品采购计划总重量(kg)</summary>
    public decimal? FinishedPlanTotalWeight { get; set; }

    /// <summary>成品采购计划总支数</summary>
    public int? FinishedPlanTotalPieces { get; set; }

    /// <summary>库存使用计划总重量(kg)</summary>
    public decimal? InventoryPlanTotalWeight { get; set; }

    /// <summary>库存使用计划总支数</summary>
    public int? InventoryPlanTotalPieces { get; set; }

    /// <summary>库料改制计划总重量(kg)</summary>
    public decimal? ReworkPlanTotalWeight { get; set; }

    /// <summary>库料改制计划总支数</summary>
    public int? ReworkPlanTotalPieces { get; set; }

    /// <summary>在产改制计划总重量(kg)</summary>
    public decimal? InProcessReworkPlanTotalWeight { get; set; }

    /// <summary>在产改制计划总支数</summary>
    public int? InProcessReworkPlanTotalPieces { get; set; }

    /// <summary>在产主工单计划总重量(kg)</summary>
    public decimal? InMainWorkOrderPlanTotalWeight { get; set; }

    /// <summary>在产主工单计划总支数</summary>
    public int? InMainWorkOrderPlanTotalPieces { get; set; }

    /// <summary>圆棒穿孔计划总重量(kg)</summary>
    public decimal? PiercingPlanTotalWeight { get; set; }

    /// <summary>圆棒穿孔计划总支数</summary>
    public int? PiercingPlanTotalPieces { get; set; }

    /// <summary>最大工艺周期（天）：4种用料计划中 StandardCycle 的最大值</summary>
    public int MaxStandardCycle { get; set; }

    /// <summary>主号最大工艺周期（天）：同主号下所有工单 MaxStandardCycle 的最大值</summary>
    public int MainNoMaxStandardCycle { get; set; }

    /// <summary>产能工量（天）：(主号总重量 - 成品采购 - 库存使用 - 已完成批次有效产出) / 1000 / 日产估算(吨/天)，向上取整。
    /// 主号完成（执行读模型 ScheduleStage=1）时置 null（显示「-」），其余无剩余产能（剩余重量≤0）时为 0（显示「0天」），与执行表口径一致</summary>
    public int? CapacityWorkDays { get; set; }

    /// <summary>理论截止投料日：交货日期 - 主号最大工艺周期 - 产能工量。仅含需要产能的用料计划时计算</summary>
    public DateTime? TheoreticalCutoffDate { get; set; }

    /// <summary>用料占比：有做计划的料态种数(0-4)</summary>
    public int MaterialPlanCoveredCount { get; set; }

    /// <summary>用料占比文本：如"穿105% 荒160% 成20% 库40% 改30%"</summary>
    public string? MaterialPlanProportion { get; set; }

    /// <summary>最新要求到货日：各计划中 RequiredDate/PlanDate 的最晚值</summary>
    public DateTime? LatestRequiredDate { get; set; }

    // ========== Group C: 预计算主号/订单聚合 ==========

    /// <summary>关联主号满足率(%)</summary>
    public decimal MainNoMaterialPlanRate { get; set; }

    /// <summary>关联主号用料状态</summary>
    public int MainNoMaterialPlanStatus { get; set; }

    /// <summary>关联订单用料状态</summary>
    public int OrderMaterialPlanStatus { get; set; }

    // ========== Group D: 行元数据 ==========

    /// <summary>乐观并发令牌</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>最后刷新时间</summary>
    public DateTime? LastRefreshTime { get; set; }
}
