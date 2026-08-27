using MES.Data.Entities.Warehouse;

namespace MES.Data.Entities.Batch;

/// <summary>
/// 生产批次与库存批次多对多关联（合并投料）
/// </summary>
public class ProductionBatchInventory : BaseEntity
{
    /// <summary>
    /// 关联生产批次ID
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 关联库存批次ID
    /// </summary>
    public int InventoryBatchId { get; set; }

    /// <summary>
    /// 关联出库记录ID（bigint），按出库记录粒度跟踪消耗
    /// 可空：向后兼容已有的关联数据
    /// </summary>
    public long? OutboundRecordId { get; set; }

    /// <summary>
    /// 领料支数
    /// </summary>
    public int InputQuantity { get; set; }

    /// <summary>
    /// 领料重量(kg)
    /// </summary>
    public decimal InputWeight { get; set; }

    // ========== 来源批次快照（创建/编辑时从关联库存批次复制，用于合并投料明细追溯展示，不实时 JOIN 仓库） ==========

    /// <summary>
    /// 来源批次号快照
    /// </summary>
    public string? SnapshotBatchNo { get; set; }

    /// <summary>
    /// 炉号快照
    /// </summary>
    public string? SnapshotHeatNo { get; set; }

    /// <summary>
    /// 工厂牌号快照
    /// </summary>
    public string? SnapshotPlantGrade { get; set; }

    /// <summary>
    /// 规格快照
    /// </summary>
    public string? SnapshotSpecification { get; set; }

    /// <summary>
    /// 原料类型快照（枚举字符串）
    /// </summary>
    public string? SnapshotMaterialType { get; set; }

    /// <summary>
    /// 来料单位快照
    /// </summary>
    public string? SnapshotSourceName { get; set; }

    /// <summary>
    /// 仓库名称快照
    /// </summary>
    public string? SnapshotWarehouseName { get; set; }

    // ========== 导航属性 ==========

    public ProductionBatch ProductionBatch { get; set; } = null!;
    public InventoryBatch InventoryBatch { get; set; } = null!;

    /// <summary>
    /// 出库记录导航属性
    /// </summary>
    public OutboundRecord? OutboundRecord { get; set; }
}
