using System;

namespace MES.Core.DTOs;

/// <summary>
/// 订单变更通知 DTO
/// </summary>
public class OrderChangeNotificationDto
{
    /// <summary>
    /// 通知ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 订单号
    /// </summary>
    public string OrderNumber { get; set; } = null!;

    /// <summary>
    /// 变更类型（0=删除，1=项次变更）
    /// </summary>
    public int ChangeType { get; set; }

    /// <summary>
    /// 变更类型文本
    /// </summary>
    public string ChangeTypeText
    {
        get
        {
            return ChangeType switch
            {
                0 => "订单删除",
                1 => "项次变更",
                _ => "未知"
            };
        }
    }

    /// <summary>
    /// 清理工单数量（仅删除类型有效）
    /// </summary>
    public int WorkOrderCount { get; set; }

    /// <summary>
    /// 是否已读
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }
}