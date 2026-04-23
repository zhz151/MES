using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

/// <summary>
/// 工单分组
/// </summary>
public class WorkOrderItemGroup
{
    /// <summary>
    /// 主号
    /// </summary>
    [Required(ErrorMessage = "主号不能为空")]
    public string ProductionMainNo { get; set; } = null!;

    /// <summary>
    /// 次号（定尺时必填）
    /// </summary>
    public string? ProductionSubNo { get; set; }

    /// <summary>
    /// 合并的订单项次ID列表
    /// </summary>
    [Required(ErrorMessage = "项次ID列表不能为空")]
    [MinLength(1, ErrorMessage = "至少选择一个项次")]
    public List<int> OrderItemIds { get; set; } = new();
}

/// <summary>
/// 生成工单请求 DTO
/// </summary>
public class CreateWorkOrderRequest
{
    /// <summary>
    /// 源订单号
    /// </summary>
    [Required(ErrorMessage = "订单号不能为空")]
    public string SalesOrderNo { get; set; } = null!;

    /// <summary>
    /// 工单分组列表
    /// </summary>
    [Required(ErrorMessage = "工单分组不能为空")]
    [MinLength(1, ErrorMessage = "至少创建一个工单")]
    public List<WorkOrderItemGroup> WorkOrders { get; set; } = new();

    /// <summary>
    /// 是否重新生成（覆盖原工单，保留主号/次号）
    /// true: 覆盖生成，保留原主号/次号
    /// false: 首次生成或待修正生成，创建新工单
    /// </summary>
    public bool IsRegenerate { get; set; }
}

/// <summary>
/// 生成工单响应 DTO
/// </summary>
public class GeneratedWorkOrderDto
{
    /// <summary>
    /// 工单ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 工单号
    /// </summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>
    /// 源订单号
    /// </summary>
    public string SalesOrderNo { get; set; } = null!;

    /// <summary>
    /// 主号
    /// </summary>
    public string ProductionMainNo { get; set; } = null!;

    /// <summary>
    /// 次号
    /// </summary>
    public string? ProductionSubNo { get; set; }

    /// <summary>
    /// 工单状态值
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 工单状态文本
    /// </summary>
    public string StatusText
    {
        get
        {
            return Status switch
            {
                0 => "未编制",
                1 => "已确定",
                2 => "待修正",
                3 => "已取消",
                _ => "未知"
            };
        }
    }

    /// <summary>
    /// 总数量
    /// </summary>
    public int TotalQuantity { get; set; }

    /// <summary>
    /// 总重量
    /// </summary>
    public decimal TotalWeight { get; set; }
}