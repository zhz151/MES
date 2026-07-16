using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Enums;
using MES.Data.Entities;
using MES.Data;
using MES.Services.WorkOrder;
using MES.Tests.Tests;
using MES.Data.Entities.WorkOrder;

namespace MES.Tests.Services;

/// <summary>
/// 通知服务测试：CRUD、已读标记、类型筛选、去重检查
/// </summary>
public class NotificationServiceTests : TestBase
{
    private NotificationService CreateService(AppDbContext ctx) => new(ctx);

    private async Task<Notification> SeedNotificationAsync(AppDbContext ctx,
        string type = "OrderChanged", string title = "订单变更通知",
        string content = "订单D26Z2117001项次已变更", bool isRead = false,
        DateTimeOffset? createdTime = null, int? targetId = null)
    {
        var n = new Notification
        {
            NotificationType = type,
            Title = title,
            Content = content,
            IsRead = isRead,
            Receiver = "测试用户",
            TargetId = targetId,
            CreatedTime = createdTime ?? DateTimeOffset.Now
        };
        ctx.Notifications.Add(n);
        await ctx.SaveChangesAsync();
        return n;
    }

    // ========== GetUnreadCountAsync ==========

    [Fact]
    public async Task GetUnreadCountAsync_无通知_返回0()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var count = await svc.GetUnreadCountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task GetUnreadCountAsync_有未读通知_返回数量()
    {
        var ctx = CreateDbContext();
        await SeedNotificationAsync(ctx);
        await SeedNotificationAsync(ctx);
        await SeedNotificationAsync(ctx, isRead: true); // 已读不计入
        var svc = CreateService(ctx);

        var count = await svc.GetUnreadCountAsync();

        count.Should().Be(2);
    }

    // ========== GetPagedNotificationsAsync ==========

    [Fact]
    public async Task GetPagedNotificationsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedNotificationsAsync(1, 20);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedNotificationsAsync_按时间倒序返回()
    {
        var ctx = CreateDbContext();
        var old = await SeedNotificationAsync(ctx, createdTime: DateTimeOffset.Now.AddDays(-2));
        var recent = await SeedNotificationAsync(ctx, createdTime: DateTimeOffset.Now);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedNotificationsAsync(1, 20);

        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(recent.Id); // 最新的在前
        result.Items[1].Id.Should().Be(old.Id);
    }

    [Fact]
    public async Task GetPagedNotificationsAsync_分页返回正确()
    {
        var ctx = CreateDbContext();
        for (int i = 0; i < 5; i++)
            await SeedNotificationAsync(ctx, title: $"通知{i + 1}");
        var svc = CreateService(ctx);

        var page1 = await svc.GetPagedNotificationsAsync(1, 2);

        page1.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(5);
        page1.PageIndex.Should().Be(1);
        page1.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedNotificationsAsync_Dto字段映射正确()
    {
        var ctx = CreateDbContext();
        var n = await SeedNotificationAsync(ctx, type: "WorkOrderDeleted", title: "工单删除",
            content: "工单GD20260101001已删除", targetId: 123);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedNotificationsAsync(1, 20);

        var dto = result.Items[0];
        dto.Id.Should().Be(n.Id);
        dto.NotificationType.Should().Be(NotificationType.WorkOrderDeleted);
        dto.TargetId.Should().Be(123);
        dto.Title.Should().Be("工单删除");
        dto.Content.Should().Be("工单GD20260101001已删除");
        dto.IsRead.Should().BeFalse();
    }

    // ========== MarkAsReadAsync ==========

    [Fact]
    public async Task MarkAsReadAsync_标记单条已读()
    {
        var ctx = CreateDbContext();
        var n = await SeedNotificationAsync(ctx);
        var svc = CreateService(ctx);

        await svc.MarkAsReadAsync(n.Id);

        var updated = await ctx.Notifications.FindAsync(n.Id);
        updated!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsReadAsync_已读通知_不报错()
    {
        var ctx = CreateDbContext();
        var n = await SeedNotificationAsync(ctx, isRead: true);
        var svc = CreateService(ctx);

        // 不应抛出异常
        await svc.MarkAsReadAsync(n.Id);

        var updated = await ctx.Notifications.FindAsync(n.Id);
        updated!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsReadAsync_不存在_不报错()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 不应抛出异常
        await svc.MarkAsReadAsync(999);
    }

    // ========== MarkAllAsReadAsync ==========

    [Fact]
    public async Task MarkAllAsReadAsync_全部标记已读()
    {
        var ctx = CreateDbContext();
        await SeedNotificationAsync(ctx);
        await SeedNotificationAsync(ctx);
        await SeedNotificationAsync(ctx);
        var svc = CreateService(ctx);

        await svc.MarkAllAsReadAsync();

        var unreadCount = await ctx.Notifications.CountAsync(n => !n.IsRead);
        unreadCount.Should().Be(0);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_无未读_不报错()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.MarkAllAsReadAsync();
    }

    // ========== HasRecentItemChangedNotificationAsync ==========

    [Fact]
    public async Task HasRecentItemChangedNotificationAsync_有未读变更_返回true()
    {
        var ctx = CreateDbContext();
        await SeedNotificationAsync(ctx, type: "OrderChanged",
            content: "订单D26Z2117001项次已变更",
            createdTime: DateTimeOffset.Now.AddMinutes(-5));
        var svc = CreateService(ctx);

        var result = await svc.HasRecentItemChangedNotificationAsync("D26Z2117001", 60);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasRecentItemChangedNotificationAsync_超时_返回false()
    {
        var ctx = CreateDbContext();
        await SeedNotificationAsync(ctx, type: "OrderChanged",
            content: "订单D26Z2117001项次已变更",
            createdTime: DateTimeOffset.Now.AddHours(-2));
        var svc = CreateService(ctx);

        var result = await svc.HasRecentItemChangedNotificationAsync("D26Z2117001", 60);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasRecentItemChangedNotificationAsync_已读_返回false()
    {
        var ctx = CreateDbContext();
        await SeedNotificationAsync(ctx, type: "OrderChanged",
            content: "订单D26Z2117001项次已变更",
            isRead: true,
            createdTime: DateTimeOffset.Now.AddMinutes(-5));
        var svc = CreateService(ctx);

        var result = await svc.HasRecentItemChangedNotificationAsync("D26Z2117001", 60);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasRecentItemChangedNotificationAsync_订单号不匹配_返回false()
    {
        var ctx = CreateDbContext();
        await SeedNotificationAsync(ctx, type: "OrderChanged",
            content: "订单OTHER001项次已变更",
            createdTime: DateTimeOffset.Now.AddMinutes(-5));
        var svc = CreateService(ctx);

        var result = await svc.HasRecentItemChangedNotificationAsync("D26Z2117001", 60);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasRecentItemChangedNotificationAsync_非OrderChanged类型_返回false()
    {
        var ctx = CreateDbContext();
        await SeedNotificationAsync(ctx, type: "WorkOrderDeleted",
            content: "订单D26Z2117001工单已删除",
            createdTime: DateTimeOffset.Now.AddMinutes(-5));
        var svc = CreateService(ctx);

        var result = await svc.HasRecentItemChangedNotificationAsync("D26Z2117001", 60);

        result.Should().BeFalse();
    }

    // ========== GetUnreadByTypeAsync ==========

    [Fact]
    public async Task GetUnreadByTypeAsync_按类型返回未读()
    {
        var ctx = CreateDbContext();
        await SeedNotificationAsync(ctx, type: "OrderChanged", title: "变更1");
        await SeedNotificationAsync(ctx, type: "OrderChanged", title: "变更2");
        await SeedNotificationAsync(ctx, type: "WorkOrderDeleted", title: "删除1");
        await SeedNotificationAsync(ctx, type: "OrderChanged", title: "已读变更", isRead: true);
        var svc = CreateService(ctx);

        var result = await svc.GetUnreadByTypeAsync("OrderChanged");

        result.Should().HaveCount(2);
        result.Should().OnlyContain(n => n.NotificationType == NotificationType.OrderChanged && !n.IsRead);
    }

    [Fact]
    public async Task GetUnreadByTypeAsync_无匹配类型_返回空()
    {
        var ctx = CreateDbContext();
        await SeedNotificationAsync(ctx, type: "OrderChanged");
        var svc = CreateService(ctx);

        var result = await svc.GetUnreadByTypeAsync("WorkOrderDeleted");

        result.Should().BeEmpty();
    }

    // ========== MarkAllByTypeAsReadAsync ==========

    [Fact]
    public async Task MarkAllByTypeAsReadAsync_按类型全部标记已读()
    {
        var ctx = CreateDbContext();
        await SeedNotificationAsync(ctx, type: "OrderChanged");
        await SeedNotificationAsync(ctx, type: "OrderChanged");
        await SeedNotificationAsync(ctx, type: "WorkOrderDeleted"); // 不应影响
        var svc = CreateService(ctx);

        await svc.MarkAllByTypeAsReadAsync("OrderChanged");

        var orderChangedUnread = await ctx.Notifications.CountAsync(
            n => n.NotificationType == "OrderChanged" && !n.IsRead);
        orderChangedUnread.Should().Be(0);

        var workDeletedUnread = await ctx.Notifications.CountAsync(
            n => n.NotificationType == "WorkOrderDeleted" && !n.IsRead);
        workDeletedUnread.Should().Be(1); // 其他类型不受影响
    }

    [Fact]
    public async Task MarkAllByTypeAsReadAsync_无匹配类型_不报错()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.MarkAllByTypeAsReadAsync("NonExistentType");
    }
}
