using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.Batch;

/// <summary>
/// 合并投料来源批次请求项
/// </summary>
public class SourceBatchItemRequest
{
    /// <summary>
    /// 关联库存批次ID
    /// </summary>
    [Required(ErrorMessage = "InventoryBatchId不能为空")]
    public int InventoryBatchId { get; set; }

    /// <summary>
    /// 关联出库记录ID（按出库记录粒度跟踪消耗；可空=向后兼容）
    /// </summary>
    public long? OutboundRecordId { get; set; }

    /// <summary>
    /// 领料支数
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "领料支数必须大于0")]
    public int InputQuantity { get; set; }

    /// <summary>
    /// 领料重量(kg)
    /// </summary>
    [Range(0.001, double.MaxValue, ErrorMessage = "领料重量必须大于0")]
    public decimal InputWeight { get; set; }
}
