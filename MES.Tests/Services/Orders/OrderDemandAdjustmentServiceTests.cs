using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Scheduling;
using MES.Services.Orders;
using MES.Tests.Tests;

namespace MES.Tests.Services.Orders;

/// <summary>
/// 订单需求调整服务测试：分页查询、保存、筛选上下文
/// </summary>
public class OrderDemandAdjustmentServiceTests : TestBase
{
    private OrderDemandAdjustmentService CreateService(AppDbContext ctx, Mock<IWorkOrderExecutionService>? woMock = null)
    {
        woMock ??= new Mock<IWorkOrderExecutionService>();
        woMock.Setup(x => x.RefreshAllAsync()).ReturnsAsync(new WorkOrderExecutionRefreshResultDto());
        return new OrderDemandAdjustmentService(ctx, woMock.Object);
    }

    private void SeedSummary(AppDbContext ctx, string workOrderNo, int workOrderId, string salesman = "", string customerName = "", string plantGrade = "", string specification = "")
    {
        ctx.Set<WorkOrderExecutionSummary>().Add(new WorkOrderExecutionSummary
        {
            Id = workOrderId,
            WorkOrderId = workOrderId,
            WorkOrderNo = workOrderNo,
            Salesman = salesman,
            CustomerName = customerName,
            SettlementMethod = "理算",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            MaterialName = "无缝管",
            DeliveryState = "固溶酸洗",
            PlantGrade = plantGrade,
            Specification = specification,
            LengthStatus = "Fixed",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            ScheduleStage = 1,
        });
    }

    // ==================== GetPagedAsync 测试 ====================

    [Fact]
    public async Task GetPagedAsync_无关键字_返回全部()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1, salesman: "张三");
        SeedSummary(ctx, "WO002", 2, salesman: "李四");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_关键字搜索工单号()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1);
        SeedSummary(ctx, "WO002", 2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "WO001" });

        result.TotalCount.Should().Be(1);
        result.Items.Single().WorkOrderNo.Should().Be("WO001");
    }

    [Fact]
    public async Task GetPagedAsync_关键字搜索客户名称()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1, customerName: "测试客户A");
        SeedSummary(ctx, "WO002", 2, customerName: "测试客户B");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "客户A" });

        result.TotalCount.Should().Be(1);
        result.Items.Single().CustomerName.Should().Be("测试客户A");
    }

    [Fact]
    public async Task GetPagedAsync_关键字无匹配_返回空()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_分页正确()
    {
        using var ctx = CreateDbContext();
        for (int i = 1; i <= 5; i++)
            SeedSummary(ctx, $"WO{i:D3}", i);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var page1 = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 2, SortBy = "WorkOrderNo", IsDescending = false });
        var page2 = await svc.GetPagedAsync(new QueryParams { PageIndex = 2, PageSize = 2, SortBy = "WorkOrderNo", IsDescending = false });

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_排序默认ScheduleStage降序()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1);
        SeedSummary(ctx, "WO002", 2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        // 无 SortBy 时默认按 ScheduleStage 降序
        result.Items.Select(i => i.ScheduleStage).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetPagedAsync_关联AdjustmentRemark()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1);
        ctx.Set<OrderDemandAdjustment>().Add(new OrderDemandAdjustment
        {
            WorkOrderId = 1,
            IsUrging = true,
            IsBatchDelivery = false,
            IsPaused = false,
            AdjustmentRemark = "紧急插单"
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var item = result.Items.Single();
        item.IsUrging.Should().BeTrue();
        item.AdjustmentRemark.Should().Be("紧急插单");
    }

    [Fact]
    public async Task GetPagedAsync_空表返回空()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    // ==================== SaveUrgingAsync 测试 ====================

    [Fact]
    public async Task SaveUrgingAsync_创建新记录()
    {
        using var ctx = CreateDbContext();
        var woMock = new Mock<IWorkOrderExecutionService>();
        woMock.Setup(x => x.RefreshAllAsync()).ReturnsAsync(new WorkOrderExecutionRefreshResultDto());
        var svc = CreateService(ctx, woMock);

        var result = await svc.SaveUrgingAsync(1, true, false, false, "催单备注");

        result.Should().BeTrue();
        var saved = await ctx.Set<OrderDemandAdjustment>().FirstOrDefaultAsync(u => u.WorkOrderId == 1);
        saved.Should().NotBeNull();
        saved!.IsUrging.Should().BeTrue();
        saved.IsBatchDelivery.Should().BeFalse();
        saved.IsPaused.Should().BeFalse();
        saved.AdjustmentRemark.Should().Be("催单备注");
        woMock.Verify(x => x.RefreshAllAsync(), Times.Once);
    }

    [Fact]
    public async Task SaveUrgingAsync_更新已有记录()
    {
        using var ctx = CreateDbContext();
        ctx.Set<OrderDemandAdjustment>().Add(new OrderDemandAdjustment
        {
            WorkOrderId = 1,
            IsUrging = false,
            IsBatchDelivery = false,
            IsPaused = false,
            AdjustmentRemark = "旧备注"
        });
        await ctx.SaveChangesAsync();

        var woMock = new Mock<IWorkOrderExecutionService>();
        woMock.Setup(x => x.RefreshAllAsync()).ReturnsAsync(new WorkOrderExecutionRefreshResultDto());
        var svc = CreateService(ctx, woMock);

        var result = await svc.SaveUrgingAsync(1, true, true, true, "新备注");

        result.Should().BeTrue();
        var updated = await ctx.Set<OrderDemandAdjustment>().FirstAsync(u => u.WorkOrderId == 1);
        updated.IsUrging.Should().BeTrue();
        updated.IsBatchDelivery.Should().BeTrue();
        updated.IsPaused.Should().BeTrue();
        updated.AdjustmentRemark.Should().Be("新备注");
        woMock.Verify(x => x.RefreshAllAsync(), Times.Once);
    }

    // ==================== GetFilterContextsAsync 测试 ====================

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1, salesman: "张三", customerName: "客户A", plantGrade: "304", specification: "219*8");
        SeedSummary(ctx, "WO002", 2, salesman: "李四", customerName: "客户B", plantGrade: "Q345B", specification: "273*10");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("WorkOrderNo", "Salesman", "CustomerName", "SalesOrderNo", "PlantGrade", "Specification");
        result["WorkOrderNo"].Should().BeEquivalentTo(new[] { "WO001", "WO002" }, options => options.WithStrictOrdering());
        result["Salesman"].Should().BeEquivalentTo(new[] { "张三", "李四" });
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_各字段返回空列表()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("WorkOrderNo", "Salesman", "CustomerName");
        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty($"字段 {kvp.Key} 应返回空列表");
    }
}
