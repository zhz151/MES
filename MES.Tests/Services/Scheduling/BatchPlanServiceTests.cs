using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Scheduling;
using MES.Tests.Tests;
using MES.Data.Entities.Batch;
using MES.Data.Entities.WorkOrder;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 在产明细计划服务测试：分页查询、工段筛选、关键词搜索、汇总
/// </summary>
public class BatchPlanServiceTests : TestBase
{
    private BatchPlanService CreateService(AppDbContext ctx) => new(ctx);

    private ProductionBatch CreateBatch(AppDbContext ctx, string batchNo, string workOrderNo,
        BatchStatus status = BatchStatus.InProgress,
        string? currentGroupName = null, string? currentSectionName = null,
        bool? currentSectionCompleted = null,
        string? nextProcess = null, string? nextSectionName = null,
        int currentValidWeight = 1000)
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            Status = status,
            WorkOrderNo = workOrderNo,
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管",
            SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163",
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished",
            PlantGrade = "304",
            Specification = "219*8",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            CurrentValidWeight = currentValidWeight,
            CurrentGroupName = currentGroupName,
            CurrentSectionName = currentSectionName,
            CurrentSectionCompleted = currentSectionCompleted,
            NextProcess = nextProcess,
            NextSectionName = nextSectionName,
            RowVersion = new byte[8],
        };
        ctx.ProductionBatches.Add(batch);
        return batch;
    }

    private void SeedSummary(AppDbContext ctx, string workOrderNo,
        int scheduleStage = 1, string? urgencyLevel = null)
    {
        // Use a deterministic hash as WorkOrderId
        int wid = Math.Abs(workOrderNo.GetHashCode());
        ctx.Set<WorkOrderExecutionSummary>().Add(new WorkOrderExecutionSummary
        {
            Id = wid,
            WorkOrderId = wid,
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
        });
    }

    // ==================== GetPagedAsync 测试 ====================

    [Fact]
    public async Task GetPagedAsync_返回在产和待产批次()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        CreateBatch(ctx, "B002", "WO001", BatchStatus.None);
        CreateBatch(ctx, "B003", "WO001", BatchStatus.Completed); // 应排除
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedAsync_关键字搜索批次号()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "BATCH001", "WO001");
        CreateBatch(ctx, "BATCH002", "WO002");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "BATCH001" });

        result.TotalCount.Should().Be(1);
        result.Items.Single().BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetPagedAsync_关键字搜索工单号()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001");
        CreateBatch(ctx, "B002", "WO002");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "WO002" });

        result.TotalCount.Should().Be(1);
        result.Items.Single().BatchNo.Should().Be("B002");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索紧急级别()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001");
        CreateBatch(ctx, "B002", "WO002");
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: "A急");
        SeedSummary(ctx, "WO002", scheduleStage: 1, urgencyLevel: "B常");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "A急" });

        // Should only find B001 (linked to WO001 with A急)
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_分页正确()
    {
        using var ctx = CreateDbContext();
        for (int i = 1; i <= 5; i++)
            CreateBatch(ctx, $"B{i:D3}", $"WO{i:D3}");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var page1 = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 2, SortBy = "BatchNo", IsDescending = false });
        var page2 = await svc.GetPagedAsync(new QueryParams { PageIndex = 2, PageSize = 2, SortBy = "BatchNo", IsDescending = false });

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page1.Items[0].BatchNo.Should().Be("B001");
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

    [Fact]
    public async Task GetPagedAsync_关联Summary数据()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001");
        SeedSummary(ctx, "WO001", scheduleStage: 2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var item = result.Items.Single();
        // summary 5 档(2=原料锁定) 映射为排程 4 档(1)
        item.ScheduleStage.Should().Be(1); // From WorkOrderExecutionSummary
    }

    [Fact]
    public async Task GetPagedAsync_工段筛选冷轧类()
    {
        using var ctx = CreateDbContext();

        // 批次在60冷轧工序，冷轧拔工段，未完成
        var batch = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: "60冷轧",
            currentSectionName: "冷轧拔",
            currentSectionCompleted: false);
        // 需要 ProcessGroup 数据支持检查
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = "60冷轧",
            SequenceNumber = 1,
            ColdRollDraw = 1,
        });

        await ctx.SaveChangesAsync();

        // 使用 __SectionTab = "60冷轧" 进行工段筛选
        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "__SectionTab", Value = "60冷轧" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items.Single().BatchNo.Should().Be("B001");
    }

    [Fact]
    public async Task GetPagedAsync_Extras包含汇总数据()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", currentValidWeight: 500);
        CreateBatch(ctx, "B002", "WO002", currentValidWeight: 1500);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Extras.Should().ContainKey("batchCount");
        result.Extras["batchCount"].Should().Be(2);
        result.Extras.Should().ContainKey("totalWeight");
        result.Extras["totalWeight"].Should().Be(2000m);
    }

    // ==================== GetFilterContextsAsync 测试 ====================

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001");
        CreateBatch(ctx, "B002", "WO002");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("BatchNo", "PlantGrade", "WorkOrderNo", "Specification");
        result["BatchNo"].Should().BeEquivalentTo(new[] { "B001", "B002" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var result = await svc.GetFilterContextsAsync();

        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty();
    }
}
