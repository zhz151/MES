using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Scheduling;
using MES.Services.Scheduling;
using MES.Tests.Tests;
using MES.Data.Entities.Order;
using MES.Data.Entities.WorkOrder;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 工单排程服务测试：分页查询、筛选条件、关键词搜索
/// </summary>
public class WorkOrderScheduleServiceTests : TestBase
{
    private WorkOrderScheduleService CreateService(AppDbContext ctx) => new(ctx);

    private void SeedSummary(AppDbContext ctx, string workOrderNo, int workOrderId,
        int scheduleStage = 2,
        string? urgencyLevel = null,
        string? productionFlowProperty = ProductionFlowKeys.Normal)
    {
        ctx.Set<WorkOrderExecutionSummary>().Add(new WorkOrderExecutionSummary
        {
            Id = workOrderId,
            WorkOrderId = workOrderId,
            WorkOrderNo = workOrderNo,
            Salesman = "业务员",
            CustomerName = "客户",
            SettlementMethod = "Theoretical",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            MaterialName = "无缝管",
            DeliveryState = "SolutionAnnealedAndPickled",
            PlantGrade = "304",
            Specification = "219*8",
            LengthStatus = "Fixed",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            ScheduleStage = scheduleStage,
            UrgencyLevel = urgencyLevel,
            ProductionFlowProperty = productionFlowProperty,
            FlowOutputRatio = 85m,
            FlowStatus = 1,
        });
    }

    // ==================== GetPagedAsync 测试 ====================

    [Fact]
    public async Task GetPagedAsync_仅返回ScheduleStage为2的工单()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1, scheduleStage: 2, productionFlowProperty: ProductionFlowKeys.Normal);
        SeedSummary(ctx, "WO002", 2, scheduleStage: 1, productionFlowProperty: null); // 应排除
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        result.Items.Single().WorkOrderNo.Should().Be("WO001");
    }

    [Fact]
    public async Task GetPagedAsync_原料锁定催单且分批交货也返回()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1, scheduleStage: 2);
        SeedSummary(ctx, "WO002", 2, scheduleStage: 1); // 需通过催单+分批交货条件
        ctx.Set<OrderDemandAdjustment>().Add(new OrderDemandAdjustment
        {
            WorkOrderId = 2,
            IsUrging = true,
            IsBatchDelivery = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_关键字搜索工单号()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1, scheduleStage: 2);
        SeedSummary(ctx, "WO002", 2, scheduleStage: 2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "WO001" });

        result.TotalCount.Should().Be(1);
        result.Items.Single().WorkOrderNo.Should().Be("WO001");
    }

    [Fact]
    public async Task GetPagedAsync_关键字搜索规格()
    {
        using var ctx = CreateDbContext();
        // 添加不同规格的工单，直接调用 SaveChanges 后进行修改
        SeedSummary(ctx, "WO001", 1, scheduleStage: 2);
        SeedSummary(ctx, "WO002", 2, scheduleStage: 2);
        await ctx.SaveChangesAsync();

        // 手动更新规格后再次保存
        var toUpdate = ctx.Set<WorkOrderExecutionSummary>().First(w => w.WorkOrderNo == "WO001");
        toUpdate.Specification = "特殊规格-219";
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "特殊规格" });

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_关键字搜索需求调整备注()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1, scheduleStage: 2);
        var summary = ctx.Set<WorkOrderExecutionSummary>().Find(1);
        if (summary != null) summary.AdjustmentRemark = "紧急插单-测试";
        SeedSummary(ctx, "WO002", 2, scheduleStage: 2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "紧急插单" });

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_分页正确()
    {
        using var ctx = CreateDbContext();
        for (int i = 1; i <= 5; i++)
            SeedSummary(ctx, $"WO{i:D3}", i, scheduleStage: 2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var page1 = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 2, SortBy = "WorkOrderNo", IsDescending = false });
        var page2 = await svc.GetPagedAsync(new QueryParams { PageIndex = 2, PageSize = 2, SortBy = "WorkOrderNo", IsDescending = false });

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page1.Items[0].WorkOrderNo.Should().Be("WO001");
    }

    [Fact]
    public async Task GetPagedAsync_G14字段映射()
    {
        using var ctx = CreateDbContext();
        var now = DateTime.UtcNow;
        ctx.Set<WorkOrderExecutionSummary>().Add(new WorkOrderExecutionSummary
        {
            Id = 1,
            WorkOrderId = 1,
            WorkOrderNo = "WO001",
            Salesman = "业务员",
            CustomerName = "客户",
            SettlementMethod = "Theoretical",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            MaterialName = "无缝管",
            DeliveryState = "SolutionAnnealedAndPickled",
            PlantGrade = "304",
            Specification = "219*8",
            LengthStatus = "Fixed",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            ScheduleStage = 2,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            PendingSectionRoughTube = 10.5m,
            PendingSection60Roll = 20m,
            DeformedProcessCompleted = true,
            ProductionAttentionProcess = "荒管处理",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var item = result.Items.Single();
        item.PendingSectionRoughTube.Should().Be(10.5m);
        item.PendingSection60Roll.Should().Be(20m);
        item.DeformedProcessCompleted.Should().BeTrue();
        item.ProductionAttentionProcess.Should().Be("荒管处理");
    }

    [Fact]
    public async Task GetPagedAsync_ProductionAttentionProcess直接返回存值()
    {
        using var ctx = CreateDbContext();
        ctx.Set<WorkOrderExecutionSummary>().Add(new WorkOrderExecutionSummary
        {
            Id = 1,
            WorkOrderId = 1,
            WorkOrderNo = "WO001",
            Salesman = "业务员",
            CustomerName = "客户",
            SettlementMethod = "Theoretical",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            MaterialName = "无缝管",
            DeliveryState = "SolutionAnnealedAndPickled",
            PlantGrade = "304",
            Specification = "219*8",
            LengthStatus = "Fixed",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            ScheduleStage = 2,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            ProductionAttentionProcess = "AdditionalFinalInspection",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Single().ProductionAttentionProcess.Should().Be("AdditionalFinalInspection");
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

    // ==================== GetFilterContextsAsync 测试 ====================

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", 1, scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent);
        SeedSummary(ctx, "WO002", 2, scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.BOrder);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("WorkOrderNo", "Salesman", "UrgencyLevel");
        result["UrgencyLevel"].Should().Contain(new[] { UrgencyLevelKeys.AUrgent, UrgencyLevelKeys.BOrder });
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty();
    }
}
