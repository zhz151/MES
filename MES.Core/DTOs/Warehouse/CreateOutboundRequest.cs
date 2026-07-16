using System.ComponentModel.DataAnnotations;
using MES.Core.Enums;

namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 出库请求
/// </summary>
public class CreateOutboundRequest
{
    [Required(ErrorMessage = "批次不能为空")]
    public int InventoryBatchId { get; set; }

    [Required(ErrorMessage = "出库类型不能为空")]
    public OutboundType OutboundType { get; set; }

    public string? SourceOrderNo { get; set; }
    public string? TargetCompany { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "出库支数必须大于0")]
    public int OutboundQuantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "出库重量必须大于等于0")]
    public decimal OutboundWeight { get; set; }

    /// <summary>
    /// 出库米数（仅成品库使用）
    /// </summary>
    public decimal? OutboundMeters { get; set; }

    [Required(ErrorMessage = "出库日期不能为空")]
    public DateTime OutboundDate { get; set; }

    public string? Remark { get; set; }
}
