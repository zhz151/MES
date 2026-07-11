// 文件路径: MES.Core/DTOs/UpdateWorkOrderStatusRequest.cs

using System.ComponentModel.DataAnnotations;
using MES.Core.Enums;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 更新工单状态请求 DTO
/// </summary>
public class UpdateWorkOrderStatusRequest
{
    /// <summary>
    /// 工单状态
    /// </summary>
    [Required(ErrorMessage = "状态不能为空")]
    public WorkOrderStatus Status { get; set; }

    /// <summary>
    /// 乐观并发控制版本号
    /// </summary>
    [Required(ErrorMessage = "版本号不能为空")]
    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>
/// 更新工单状态响应 DTO
/// </summary>
public class UpdateWorkOrderStatusResponseDto
{
    /// <summary>
    /// 工单ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 工单状态
    /// </summary>
    public WorkOrderStatus Status { get; set; }
}