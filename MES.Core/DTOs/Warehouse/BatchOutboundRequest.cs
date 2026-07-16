using System.ComponentModel.DataAnnotations;
using MES.Core.Enums;

namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 批量出库请求
/// </summary>
public class BatchOutboundRequest
{
    /// <summary>
    /// 出库类型
    /// </summary>
    [Required(ErrorMessage = "出库类型不能为空")]
    public OutboundType OutboundType { get; set; }

    /// <summary>
    /// 物料单号（委外关联）
    /// </summary>
    public string? SourceOrderNo { get; set; }

    /// <summary>
    /// 目标单位
    /// </summary>
    public string? TargetCompany { get; set; }

    /// <summary>
    /// 出库日期
    /// </summary>
    [Required]
    public DateTime OutboundDate { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 出库条目列表
    /// </summary>
    [Required(ErrorMessage = "出库条目不能为空")]
    [MinLength(1, ErrorMessage = "至少选择一条出库条目")]
    public List<OutboundItemRequest> Items { get; set; } = new();
}

/// <summary>
/// 单条出库项
/// </summary>
public class OutboundItemRequest
{
    /// <summary>
    /// 库存批次ID
    /// </summary>
    [Required]
    public int InventoryBatchId { get; set; }

    /// <summary>
    /// 出库支数
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "出库支数必须大于0")]
    public int OutboundQuantity { get; set; }

    /// <summary>
    /// 出库重量(kg)
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "出库重量必须大于等于0")]
    public decimal OutboundWeight { get; set; }

    /// <summary>
    /// 出库米数（仅成品库使用）
    /// </summary>
    public decimal? OutboundMeters { get; set; }

    // 行级可覆盖字段（row ?? request 回退）
    public OutboundType? OutboundType { get; set; }
    public string? SourceOrderNo { get; set; }
    public string? TargetCompany { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 批量出库结果
/// </summary>
public class BatchOutboundResult
{
    /// <summary>
    /// 成功出库的记录数
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 出库记录列表
    /// </summary>
    public List<OutboundRecordDto> Records { get; set; } = new();
}
