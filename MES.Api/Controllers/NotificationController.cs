using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

/// <summary>
/// 通知控制器
/// </summary>
[ApiController]
[Route("api/notification")]
[Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// 获取未读通知数量
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<ApiResponse<int>> GetUnreadCount()
    {
        var count = await _notificationService.GetUnreadCountAsync();
        return ApiResponse<int>.Ok(count);
    }

    /// <summary>
    /// 分页获取通知列表
    /// </summary>
    [HttpGet("paged")]
    public async Task<ApiResponse<PagedResult<OrderChangeNotificationDto>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _notificationService.GetPagedNotificationsAsync(pageIndex, pageSize);
        return ApiResponse<PagedResult<OrderChangeNotificationDto>>.Ok(result);
    }

    /// <summary>
    /// 标记单条通知为已读
    /// </summary>
    [HttpPost("{id}/read")]
    public async Task<ApiResponse> MarkAsRead(int id)
    {
        await _notificationService.MarkAsReadAsync(id);
        return ApiResponse.Ok("已标记为已读");
    }

    /// <summary>
    /// 标记所有通知为已读
    /// </summary>
    [HttpPost("read-all")]
    public async Task<ApiResponse> MarkAllAsRead()
    {
        await _notificationService.MarkAllAsReadAsync();
        return ApiResponse.Ok("已全部标记为已读");
    }
}