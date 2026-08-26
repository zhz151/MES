using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;
using MES.Services.Order;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 待发货查询服务测试：工单关注（主号-关注档位，WorkOrderExecutionSummary.ScheduleStage 按工单号关联）
/// </summary>
public class PendingDeliveryQueryServiceTests : TestBase
{
    private PendingDeliveryQueryService CreateService(AppDbContext ctx)
        => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private async Task<InventoryBatch> SeedFinishedBatchAsync(
        AppDbContext ctx, string batchNo, string? workOrderNo = null, int remainingQty = 10)
    {
        var wh = await SeedWarehouseAsync(ctx);
        var batch = new InventoryBatch
        {
            BatchNo = batchNo,
            WarehouseId = wh.Id,
            MaterialType = "OrderFinished",
            InboundSource = "Purchase",
            SourceName = "供应商A",
            PlantGrade = "Q345B",
            Specification = "219*8",
            RemainingQuantity = remainingQty,
            RemainingWeight = 1000m,
            InboundDate = DateTime.Today,
            SalesOrderNo = "SO001",
            WorkOrderNo = workOrderNo,
            CreatedBy = "u1"
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    private void SeedExecutionSummary(AppDbContext ctx, string workOrderNo, int scheduleStage)
    {
        ctx.Set<WorkOrderExecutionSummary>().Add(new WorkOrderExecutionSummary
        {
            WorkOrderNo = workOrderNo,
            ScheduleStage = scheduleStage,
            Salesman = "测试业务员",
            CustomerName = "测试客户",
            SalesOrderNo = "SO001",
            ProductionMainNo = "G001",
            MaterialName = "无缝管",
            DeliveryState = "Fixed",
            PlantGrade = "Q345B",
            Specification = "219*8",
            LengthStatus = "Fixed",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today,
            SettlementMethod = "电汇",
            CreatedBy = "u1"
        });
    }

    [Fact]
    public async Task GetPagedAsync_批次有工单号且有读模型_工单关注填充主号关注档位()
    {
        var ctx = CreateDbContext();
        await SeedFinishedBatchAsync(ctx, "PD-001", workOrderNo: "G001-01");
        SeedExecutionSummary(ctx, "G001-01", scheduleStage: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 10 });

        result.Items.Should().ContainSingle();
        result.Items[0].WorkOrderAttention.Should().Be(3);
    }

    [Fact]
    public async Task GetPagedAsync_批次有工单号但无读模型_工单关注为空()
    {
        var ctx = CreateDbContext();
        await SeedFinishedBatchAsync(ctx, "PD-002", workOrderNo: "G001-02");

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 10 });

        result.Items.Should().ContainSingle();
        result.Items[0].WorkOrderAttention.Should().BeNull();
    }

    [Fact]
    public async Task GetPagedAsync_批次无工单号_工单关注为空()
    {
        var ctx = CreateDbContext();
        await SeedFinishedBatchAsync(ctx, "PD-003", workOrderNo: null);

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 10 });

        result.Items.Should().ContainSingle();
        result.Items[0].WorkOrderAttention.Should().BeNull();
    }

    [Fact]
    public async Task GetFilterContextsAsync_工单关注_包含读模型档位选项()
    {
        var ctx = CreateDbContext();
        await SeedFinishedBatchAsync(ctx, "PD-004", workOrderNo: "G001-04");
        SeedExecutionSummary(ctx, "G001-04", scheduleStage: 2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("WorkOrderAttention");
        contexts["WorkOrderAttention"].Should().Contain("2");
    }

    [Fact]
    public async Task InvalidateCachesAsync_清空缓存并可重建()
    {
        var ctx = CreateDbContext();
        await SeedFinishedBatchAsync(ctx, "PD-005", workOrderNo: "G001-05");
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new PendingDeliveryQueryService(ctx, cache);

        // 首次查询填充 C0 缓存
        var before = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 10 });
        before.Items.Should().ContainSingle();
        cache.Get(PendingDeliveryQueryService.CacheKey).Should().NotBeNull();

        // 失效后 C0 清空
        await svc.InvalidateCachesAsync();
        cache.Get(PendingDeliveryQueryService.CacheKey).Should().BeNull();

        // 再查可重建
        var after = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 10 });
        after.Items.Should().ContainSingle();
    }
}
