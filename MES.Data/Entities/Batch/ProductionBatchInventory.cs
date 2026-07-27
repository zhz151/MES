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

    // ========== 导航属性 ==========

    public ProductionBatch ProductionBatch { get; set; } = null!;
    public InventoryBatch InventoryBatch { get; set; } = null!;

    /// <summary>
    /// 出库记录导航属性
    /// </summary>
    public OutboundRecord? OutboundRecord { get; set; }
}
