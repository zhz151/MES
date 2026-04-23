// 文件路径: MES.Core/DTOs/WorkOrderListDto.cs

namespace MES.Core.DTOs;

/// <summary>
/// 工单列表 DTO
/// </summary>
public class WorkOrderListDto
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
    /// 物料名称
    /// </summary>
    public string MaterialName { get; set; } = null!;

    /// <summary>
    /// 规格
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 交货日期
    /// </summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// 总数量
    /// </summary>
    public int TotalQuantity { get; set; }

    /// <summary>
    /// 总重量
    /// </summary>
    public decimal TotalWeight { get; set; }

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
                0 => "待处理",
                1 => "已确定",
                2 => "已取消",
                _ => "未知"
            };
        }
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }
}