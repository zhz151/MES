using MES.Data.Entities.Batch;

namespace MES.Data.Entities.Warehouse;

/// <summary>
/// 库存批次（核心实体）
/// </summary>
public class InventoryBatch : BaseEntity
{
    // ========== 基础标识 ==========

    /// <summary>
    /// 批次号，唯一
    /// 格式：CK + yyMMdd + 3位流水号
    /// </summary>
    public string BatchNo { get; set; } = null!;

    // ========== 仓库与物料标识 ==========

    /// <summary>
    /// 关联仓库
    /// </summary>
    public int WarehouseId { get; set; }

    /// <summary>
    /// 仓库导航属性
    /// </summary>
    public Warehouse Warehouse { get; set; } = null!;

    /// <summary>
    /// 物料名称（荒管/圆钢/临界成品/半成品等）
    /// </summary>
    public string MaterialType { get; set; } = null!;

    /// <summary>
    /// 厂内钢种
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 名义规格
    /// </summary>
    public string Specification { get; set; } = null!;

    // ========== 来源信息 ==========

    /// <summary>
    /// 入库来源（枚举字符串）
    /// </summary>
    public string InboundSource { get; set; } = null!;

    /// <summary>
    /// 来料单位或部门
    /// </summary>
    public string SourceName { get; set; } = null!;

    /// <summary>
    /// 入库日期
    /// </summary>
    public DateTime InboundDate { get; set; }

    // ========== 钢种与规格 ==========

    /// <summary>
    /// 炉号
    /// </summary>
    public string? HeatNo { get; set; }

    /// <summary>
    /// 生产批号
    /// </summary>
    public string? ProductionBatchNo { get; set; }

    /// <summary>
    /// 长度状态
    /// </summary>
    public string? LengthStatus { get; set; }

    /// <summary>
    /// 最小长度(mm)
    /// </summary>
    public decimal? MinLength { get; set; }

    /// <summary>
    /// 最大长度(mm)
    /// </summary>
    public decimal? MaxLength { get; set; }

    // ========== 数量与重量 ==========

    /// <summary>
    /// 入库支数
    /// </summary>
    public int InitialQuantity { get; set; }

    /// <summary>
    /// 入库重量(kg)
    /// </summary>
    public decimal InitialWeight { get; set; }

    /// <summary>
    /// 理论单支重(kg)
    /// </summary>
    public decimal? UnitWeight { get; set; }

    /// <summary>
    /// 米数
    /// </summary>
    public decimal? Meters { get; set; }

    /// <summary>
    /// 当前剩余米数（仅成品库使用）
    /// </summary>
    public decimal? RemainingMeters { get; set; }

    /// <summary>
    /// 当前剩余支数
    /// </summary>
    public int RemainingQuantity { get; set; }

    /// <summary>
    /// 当前剩余重量(kg)
    /// </summary>
    public decimal RemainingWeight { get; set; }

    // ========== 实际规格 ==========

    /// <summary>
    /// 实际规格
    /// </summary>
    public string? ActualSpecification { get; set; }

    // ========== 位置与状态 ==========

    /// <summary>
    /// 表面状态（固溶酸洗/精密矫直等）
    /// </summary>
    public string? SurfaceCondition { get; set; }

    /// <summary>
    /// 放置区域
    /// </summary>
    public string? LocationArea { get; set; }

    /// <summary>
    /// 放置架号
    /// </summary>
    public string? LocationRack { get; set; }

    /// <summary>
    /// 物料备注
    /// </summary>
    public string? Remark { get; set; }

    // ========== 次品相关 ==========

    /// <summary>
    /// 次品原因
    /// </summary>
    public string? DefectReason { get; set; }

    /// <summary>
    /// 责任类型
    /// </summary>
    public string? LiabilityType { get; set; }

    /// <summary>
    /// 原始来料单位
    /// </summary>
    public string? OriginalSupplier { get; set; }

    /// <summary>
    /// 挂牌号
    /// </summary>
    public string? TagNo { get; set; }

    /// <summary>
    /// 次品备注
    /// </summary>
    public string? DefectRemark { get; set; }

    // ========== 跨上下文关联（松耦合，仅存单号文本） ==========

    /// <summary>
    /// 是否关联工单
    /// </summary>
    public bool IsLinkedToWorkOrder { get; set; }

    /// <summary>
    /// 工单号（无FK，文本关联）
    /// </summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>
    /// 订单号
    /// </summary>
    public string? SalesOrderNo { get; set; }

    /// <summary>
    /// 项次ID列表（逗号分隔）
    /// </summary>
    public string? OrderItemIds { get; set; }

    /// <summary>
    /// 来源单号（采购单号/委外单号，无FK，追溯采购或委外执行）
    /// 前缀 CG=采购，WW=委外；具体类型由 InboundSource 区分
    /// </summary>
    public string? SourceOrderNo { get; set; }

    /// <summary>
    /// 来源序号（委外时对应 SubcontractReturnItem.Sequence，采购时为空）
    /// </summary>
    public int? SourceOrderSequence { get; set; }

    // ========== 乐观并发控制 ==========

    /// <summary>
    /// 乐观并发令牌（行版本，由数据库自动生成）
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>
    /// 关联的生产批次（合并投料）
    /// </summary>
    public List<ProductionBatchInventory> ProductionBatchInventories { get; set; } = new();
}
