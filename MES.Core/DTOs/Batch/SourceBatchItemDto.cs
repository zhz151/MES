using MES.Core.Enums;

namespace MES.Core.DTOs.Batch;

/// <summary>
/// 合并投料来源批次显示DTO
/// </summary>
public class SourceBatchItemDto
{
    /// <summary>
    /// 关联库存批次ID
    /// </summary>
    public int InventoryBatchId { get; set; }

    /// <summary>
    /// 关联出库记录ID
    /// </summary>
    public long? OutboundRecordId { get; set; }

    /// <summary>
    /// 库存批次号
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 炉号
    /// </summary>
    public string? HeatNo { get; set; }

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string? PlantGrade { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    public string? Specification { get; set; }

    /// <summary>
    /// 原料类型
    /// </summary>
    public MaterialType? MaterialType { get; set; }

    /// <summary>
    /// 来料单位
    /// </summary>
    public string? SourceName { get; set; }

    /// <summary>
    /// 仓库名称
    /// </summary>
    public string? WarehouseName { get; set; }

    /// <summary>
    /// 领料支数
    /// </summary>
    public int InputQuantity { get; set; }

    /// <summary>
    /// 领料重量(kg)
    /// </summary>
    public decimal InputWeight { get; set; }
}
