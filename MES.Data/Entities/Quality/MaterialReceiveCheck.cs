using MES.Data.Entities.Batch;

namespace MES.Data.Entities.Quality;

/// <summary>
/// 成检到料 — 所有工序完成后料到成品检验处，标记批次完成
/// </summary>
public class MaterialReceiveCheck : BaseEntity
{
    // ========== 核心数据 ==========

    /// <summary>
    /// 关联生产批次ID（UK唯一）
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 到料日期
    /// </summary>
    public DateTime ReceiveDate { get; set; }

    /// <summary>
    /// 班次（白班/中班/夜班）
    /// </summary>
    public string? Shift { get; set; }

    /// <summary>
    /// 确认人
    /// </summary>
    public string? Checker { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入），默认 MANUAL
    /// </summary>
    public string? DataSource { get; set; }

    // ========== 批次冗余字段（从 ProductionBatch 自动复制） ==========

    /// <summary>生产编号</summary>
    public string? BatchNo { get; set; }

    /// <summary>制造物品（订单成品/备料成品/余库料/中间品，待批次上下文枚举迁移后改为 ManufacturingItem?）</summary>
    public string? ManufacturingItem { get; set; }

    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }

    /// <summary>工单号</summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>订单号</summary>
    public string? SalesOrderNo { get; set; }

    /// <summary>来料单位</summary>
    public string? SourceUnit { get; set; }

    /// <summary>炉号</summary>
    public string? FurnaceNo { get; set; }

    /// <summary>工厂牌号</summary>
    public string? PlantGrade { get; set; }

    /// <summary>规格</summary>
    public string? Specification { get; set; }

    /// <summary>生产类型（待批次上下文枚举迁移后改为 ProductionType?）</summary>
    public string? ProductionType { get; set; }

    /// <summary>
    /// 长度状态（从 ProductionBatch 复制）
    /// </summary>
    public string? LengthStatus { get; set; }

    /// <summary>
    /// 生产重量(kg) — 按生产类型区分计算逻辑，创建时快照
    /// </summary>
    public decimal? ProductionWeight { get; set; }

    /// <summary>
    /// 生产支数 — 按生产类型区分计算逻辑，创建时快照
    /// </summary>
    public int ProductionCutQuantity { get; set; }

    // ========== 状态控制 ==========

    /// <summary>
    /// 强制完成（人控开关）
    /// </summary>
    public bool IsForceCompleted { get; set; }

    // ========== 冗余字段（从 WorkOrder/ProductionBatch 自动复制） ==========

    /// <summary>业务员</summary>
    public string? Salesman { get; set; }

    /// <summary>交货状态</summary>
    public string? DeliveryState { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属生产批次
    /// </summary>
    public ProductionBatch ProductionBatch { get; set; } = null!;
}
