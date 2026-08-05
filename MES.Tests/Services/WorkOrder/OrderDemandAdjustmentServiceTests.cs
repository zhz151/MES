using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Core.Enums;
using MES.Services.WorkOrder;
using MES.Tests.Tests;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services.WorkOrder;

/// <summary>
/// 工单需求调整服务测试：分页查询、保存、筛选上下文
/// </summary>
public class OrderDemandAdjustmentServiceTests : TestBase
{
    private OrderDemandAdjustmentService CreateService(AppDbContext ctx, Mock<IWorkOrderExecutionService>? woMock = null)
    {
        woMock ??= new Mock<IWorkOrderExecutionService>();
        woMock.Setup(x => x.RefreshAllAsync()).ReturnsAsync(new WorkOrderExecutionRefreshResultDto());
        return new OrderDemandAdjustmentService(ctx, woMock.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    private void SeedWorkOrder(AppDbContext ctx, int id, string workOrderNo, string salesOrderNo = "SO001", string productionMainNo = "D01")
    {
        ctx.Set<MES.Data.Entities.WorkOrder.WorkOrder>().Add(new MES.Data.Entities.WorkOrder.WorkOrder
        {
            Id = id,
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = productionMainNo,
            OrderItemIds = "1",
            Status = WorkOrderStatus.Confirmed,
            RowVersion = Array.Empty<byte>(),
            SignDate = DateTime.Today,
            Salesman = "张三",
            DeliveryDate = DateTime.Today.AddMonths(1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "GB/T 8163",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "304",
            Specification = "219*8",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.3m,
            LengthStatus = LengthStatus.Fixed,
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            TotalItemCount = 1,
        });
    }

    private void SeedSummary(AppDbContext ctx, string workOrderNo, int workOrderId, string salesman = "", string customerName = "", string plantGrade = "", string specification = "", int woWarehousingStatus = 1)
    {
        ctx.Set<WorkOrderExecutionSummary>().Add(new WorkOrderExecutionSummary
        {
            Id = workOrderId,
            WorkOrderId = workOrderId,
            WorkOrderNo = workOrderNo,
            Salesman = salesman,
            CustomerName = customerName,
            SettlementMethod = "Theoretical",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            MaterialName = "无缝管",
            DeliveryState = "SolutionAnnealedAndPickled",
            PlantGrade = plantGrade,
            Specification = specification,
            LengthStatus = "Fixed",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            WoWarehousingStatus = woWarehousingStatus,
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
        SeedWorkOrder(ctx, 1, "WO001");
        var woMock = new Mock<IWorkOrderExecutionService>();
        woMock.Setup(x => x.RefreshByWorkOrderNosAsync(It.IsAny<List<string>>())).Returns(Task.CompletedTask);
        var svc = CreateService(ctx, woMock);

        var result = await svc.SaveUrgingAsync(1, true, false, false, "催单备注");

        result.Should().BeTrue();
        var saved = await ctx.Set<OrderDemandAdjustment>().FirstOrDefaultAsync(u => u.WorkOrderId == 1);
        saved.Should().NotBeNull();
        saved!.IsUrging.Should().BeTrue();
        saved.IsBatchDelivery.Should().BeFalse();
        saved.IsPaused.Should().BeFalse();
        saved.AdjustmentRemark.Should().Be("催单备注");
        woMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.IsAny<List<string>>()), Times.Once);
    }

    [Fact]
    public async Task SaveUrgingAsync_更新已有记录()
    {
        using var ctx = CreateDbContext();
        SeedWorkOrder(ctx, 1, "WO001");
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
        woMock.Setup(x => x.RefreshByWorkOrderNosAsync(It.IsAny<List<string>>())).Returns(Task.CompletedTask);
        var svc = CreateService(ctx, woMock);

        var result = await svc.SaveUrgingAsync(1, true, true, true, "新备注");

        result.Should().BeTrue();
        var updated = await ctx.Set<OrderDemandAdjustment>().FirstAsync(u => u.WorkOrderId == 1);
        updated.IsUrging.Should().BeTrue();
        updated.IsBatchDelivery.Should().BeTrue();
        updated.IsPaused.Should().BeTrue();
        updated.AdjustmentRemark.Should().Be("新备注");
        woMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.IsAny<List<string>>()), Times.Once);
    }

    [Fact]
    public async Task SaveUrgingAsync_主号联动连带_同主号未完结工单同步暂停()
    {
        using var ctx = CreateDbContext();
        SeedWorkOrder(ctx, 1, "WO001");
        SeedWorkOrder(ctx, 2, "WO002");
        // 同主号（SO001/D01）下两个工单均未入库完结
        SeedSummary(ctx, "WO001", 1, woWarehousingStatus: 1);
        SeedSummary(ctx, "WO002", 2, woWarehousingStatus: 1);
        await ctx.SaveChangesAsync();

        var woMock = new Mock<IWorkOrderExecutionService>();
        woMock.Setup(x => x.RefreshByWorkOrderNosAsync(It.IsAny<List<string>>())).Returns(Task.CompletedTask);
        var svc = CreateService(ctx, woMock);

        // 暂停 WO001 → 主号暂停联动，WO002 一并暂停
        var result = await svc.SaveUrgingAsync(1, true, false, true, "主号暂停备注");

        result.Should().BeTrue();
        var adjustments = await ctx.Set<OrderDemandAdjustment>().ToListAsync();
        adjustments.Should().HaveCount(2);

        var w1 = adjustments.Single(u => u.WorkOrderId == 1);
        w1.IsUrging.Should().BeTrue();
        w1.IsPaused.Should().BeTrue();
        w1.AdjustmentRemark.Should().Be("主号暂停备注");

        var w2 = adjustments.Single(u => u.WorkOrderId == 2);
        w2.IsUrging.Should().BeFalse();
        w2.IsPaused.Should().BeTrue();
        w2.AdjustmentRemark.Should().BeNull();

        woMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(n =>
            n.Contains("WO001", StringComparer.OrdinalIgnoreCase) && n.Contains("WO002", StringComparer.OrdinalIgnoreCase))), Times.Once);
    }

    [Fact]
    public async Task SaveUrgingAsync_联动连带_已入库完结工单不同步()
    {
        using var ctx = CreateDbContext();
        SeedWorkOrder(ctx, 1, "WO001");
        SeedWorkOrder(ctx, 2, "WO002");
        // WO001 未入库完结，WO002 已入库完结（主号闭环）
        SeedSummary(ctx, "WO001", 1, woWarehousingStatus: 1);
        SeedSummary(ctx, "WO002", 2, woWarehousingStatus: 2);
        await ctx.SaveChangesAsync();

        var woMock = new Mock<IWorkOrderExecutionService>();
        woMock.Setup(x => x.RefreshByWorkOrderNosAsync(It.IsAny<List<string>>())).Returns(Task.CompletedTask);
        var svc = CreateService(ctx, woMock);

        var result = await svc.SaveUrgingAsync(1, true, false, true, "主号暂停备注");

        result.Should().BeTrue();
        // 只有当前工单被写入，已完结工单不被牵连
        var adjustments = await ctx.Set<OrderDemandAdjustment>().ToListAsync();
        adjustments.Should().HaveCount(1);
        adjustments.Single().WorkOrderId.Should().Be(1);
        adjustments.Single().IsPaused.Should().BeTrue();

        woMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(n =>
            n.Contains("WO001", StringComparer.OrdinalIgnoreCase) && !n.Contains("WO002", StringComparer.OrdinalIgnoreCase))), Times.Once);
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
