using MES.Core.Enums;

namespace MES.Core.Models;

/// <summary>
/// 库存查询参数
/// </summary>
public class InventoryQueryParams : QueryParams
{
    /// <summary>
    /// 仓库ID筛选
    /// </summary>
    public int? WarehouseId { get; set; }

    /// <summary>
    /// 物料名称筛选
    /// </summary>
    public string? MaterialType { get; set; }

    /// <summary>
    /// 钢种筛选
    /// </summary>
    public string? PlantGrade { get; set; }

    /// <summary>
    /// 是否仅显示有库存的（剩余重量>0）
    /// </summary>
    public bool OnlyWithStock { get; set; } = true;

    /// <summary>
    /// 工单号筛选
    /// </summary>
    public string? WorkOrderNo { get; set; }

    // ========== 表头列筛选字段 ==========

    /// <summary>
    /// 批次号精确筛选
    /// </summary>
    public string? BatchNo { get; set; }

    /// <summary>
    /// 入库来源筛选
    /// </summary>
    public InboundSource? InboundSource { get; set; }

    /// <summary>
    /// 来料单位筛选
    /// </summary>
    public string? SourceName { get; set; }

    /// <summary>
    /// 炉号筛选
    /// </summary>
    public string? HeatNo { get; set; }

    /// <summary>
    /// 名义规格筛选
    /// </summary>
    public string? Specification { get; set; }

    /// <summary>
    /// 长度状态筛选
    /// </summary>
    public string? LengthStatus { get; set; }

    /// <summary>
    /// 制造状态筛选
    /// </summary>
    public string? ManufacturingStatus { get; set; }

    /// <summary>
    /// 次品原因筛选
    /// </summary>
    public string? DefectReason { get; set; }

    /// <summary>
    /// 责任类型筛选
    /// </summary>
    public string? LiabilityType { get; set; }

    /// <summary>
    /// 生产批号筛选
    /// </summary>
    public string? ProductionBatchNo { get; set; }

    /// <summary>
    /// 实际规格筛选
    /// </summary>
    public string? ActualSpecification { get; set; }

    /// <summary>
    /// 原始来料单位筛选
    /// </summary>
    public string? OriginalSupplier { get; set; }
}

/// <summary>
/// 出库记录查询参数
/// </summary>
public class OutboundQueryParams : QueryParams
{
    public int? InventoryBatchId { get; set; }
    public int? WarehouseId { get; set; }
    public string? OutboundType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
