using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MES.Core.Constants;
using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.WorkOrder;
using MES.Services.WorkOrder;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 定尺工单服务测试：长度查询（空白入参兜底）、长度双映射构建、
/// 联通视图列表（断切记录主号级长度聚合 + 总现况三档划分）、打印 PDF 字节。
/// </summary>
public class FixedLengthWorkOrderServiceTests : TestBase
{
    private static FixedLengthWorkOrderService CreateService(AppDbContext ctx)
        => new(ctx, NullLogger<FixedLengthWorkOrderService>.Instance, new MemoryCache(new MemoryCacheOptions()));

    private static async Task SeedFixedAsync(AppDbContext ctx, string workOrderNo, string salesOrderNo,
        string mainNo, decimal length, int plannedQuantity = 10, int workOrderId = 1)
    {
        ctx.FixedLengthWorkOrders.Add(new FixedLengthWorkOrder
        {
            WorkOrderId = workOrderId,
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = mainNo,
            Length = length,
            PlannedQuantity = plannedQuantity
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx, string workOrderNo,
        string salesOrderNo, string mainNo, bool cutRequirement, int theoreticalOutputQty,
        string batchNo = "BATCH")
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            MaterialName = "不锈钢管",
            PlantGrade = "304",
            Specification = "219*8",
            Status = BatchStatus.InProgress,
            ProductionType = "Internal",
            ManufacturingItem = "OrderFinished",
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = mainNo,
            OrderItemIds = "1",
            Salesman = "张三",
            SettlementMethod = "Weighing",
            StandardCode = "GB/T 14976",
            DeliveryState = "Hard",
            LengthStatus = "Fixed",
            TechnicalRequirements = "无",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.3m,
            TotalQuantity = 100,
            TotalMeters = 1000m,
            TotalWeight = 5000m,
            TotalItemCount = 1,
            CutRequirement = cutRequirement,
            TheoreticalOutputQty = theoreticalOutputQty
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    private static async Task SeedCutRecordAsync(AppDbContext ctx, int batchId, decimal finishedCutLength,
        int postCutQuantity, DateTime? execDate = null)
    {
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batchId,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            SequenceNumber = 5,
            ExecDate = execDate ?? DateTime.Today,
            ProductStatus = ProductStatuses.Finished,
            LengthStatus = nameof(LengthStatus.Fixed),
            FinishedCutLength = finishedCutLength,
            PostCutQuantity = postCutQuantity
        });
        await ctx.SaveChangesAsync();
    }

    // ========== 长度查询 ==========

    [Fact]
    public async Task GetLengthsByMainNoAsync_空白入参_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.GetLengthsByMainNoAsync("", "")).Should().BeEmpty();
        (await svc.GetLengthsByMainNoAsync("   ", null!)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetLengthsByMainNoAsync_命中返回长度集合()
    {
        var ctx = CreateDbContext();
        await SeedFixedAsync(ctx, "WO-1", "SO-1", "M-1", 6000m);
        await SeedFixedAsync(ctx, "WO-1", "SO-1", "M-1", 4000m);
        await SeedFixedAsync(ctx, "WO-2", "SO-1", "M-2", 3000m); // 不同主号 → 排除
        var svc = CreateService(ctx);

        var lengths = await svc.GetLengthsByMainNoAsync("SO-1", "M-1");

        lengths.Should().BeEquivalentTo(new[] { 6000m, 4000m });
        lengths.Should().NotContain(3000m);
    }

    [Fact]
    public async Task GetLengthsByWorkOrderNoAsync_空白入参_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.GetLengthsByWorkOrderNoAsync("  ")).Should().BeEmpty();
        (await svc.GetLengthsByWorkOrderNoAsync(null!)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetLengthsByWorkOrderNoAsync_命中返回长度集合()
    {
        var ctx = CreateDbContext();
        await SeedFixedAsync(ctx, "WO-1", "SO-1", "M-1", 6000m);
        await SeedFixedAsync(ctx, "WO-2", "SO-1", "M-1", 8000m);
        var svc = CreateService(ctx);

        var lengths = await svc.GetLengthsByWorkOrderNoAsync("WO-1");

        lengths.Should().Equal(6000m);
    }

    // ========== GetLengthMapsAsync ==========

    [Fact]
    public async Task GetLengthMapsAsync_构建工单号与主号双映射()
    {
        var ctx = CreateDbContext();
        await SeedFixedAsync(ctx, "WO-1", "SO-1", "M-1", 6000m);
        await SeedFixedAsync(ctx, "WO-1", "SO-1", "M-1", 4000m);
        await SeedFixedAsync(ctx, "WO-2", "SO-2", "M-2", 8000m);
        var svc = CreateService(ctx);

        var maps = await svc.GetLengthMapsAsync();

        maps.ByWorkOrderNo["WO-1"].Should().BeEquivalentTo(new[] { 6000m, 4000m });
        maps.ByWorkOrderNo["WO-2"].Should().Equal(8000m);
        maps.ByMainKey["SO-1|M-1"].Should().BeEquivalentTo(new[] { 6000m, 4000m });
        maps.ByMainKey["SO-2|M-2"].Should().Equal(8000m);
        // 主号键大小写归一（NormalizeMainKey 转大写）
        maps.ByMainKey.Should().ContainKey("so-1|m-1");
    }

    // ========== GetListAsync ==========

    [Fact]
    public async Task GetListAsync_无定尺工单_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var list = await svc.GetListAsync();

        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GetListAsync_断切记录_主号级长度聚合与总现况三档()
    {
        var ctx = CreateDbContext();
        await SeedFixedAsync(ctx, "WO-1", "SO-1", "M-1", 6000m, plannedQuantity: 10);
        // 需切批（有断切记录）→ 计入切割批理论；无需切批 → 计入无需切割
        var cutBatch = await SeedBatchAsync(ctx, "WO-1", "SO-1", "M-1", cutRequirement: true, 100, "BATCH-CUT");
        await SeedBatchAsync(ctx, "WO-1", "SO-1", "M-1", cutRequirement: false, 50, "BATCH-NOCUT");
        await SeedCutRecordAsync(ctx, cutBatch.Id, 6000m, 5, new DateTime(2026, 9, 1));
        var svc = CreateService(ctx);

        var list = await svc.GetListAsync();

        var row = list.Should().ContainSingle().Subject;
        row.WorkOrderNo.Should().Be("WO-1");
        row.Length.Should().Be(6000m);
        row.PlannedQuantity.Should().Be(10);
        // G3 成品切割执行（主号级该长度聚合）
        row.CutQuantity.Should().Be(5);
        row.CutDeadline.Should().Be(new DateTime(2026, 9, 1));
        // G6 主号级总现况三档恒等式
        row.MainNoTotalRequirement.Should().Be(10);
        row.MainNoTotalInput.Should().Be(150);
        row.MainNoNoCutQty.Should().Be(50);
        row.MainNoNeedCutUncutQty.Should().Be(0);
        row.MainNoCutTheoretical.Should().Be(100);
        row.MainNoCutActual.Should().Be(5);
    }

    [Fact]
    public async Task GetListAsync_多长度_切割支数按长度过滤()
    {
        var ctx = CreateDbContext();
        await SeedFixedAsync(ctx, "WO-1", "SO-1", "M-1", 6000m);
        await SeedFixedAsync(ctx, "WO-1", "SO-1", "M-1", 4000m);
        var batch = await SeedBatchAsync(ctx, "WO-1", "SO-1", "M-1", cutRequirement: true, 100);
        await SeedCutRecordAsync(ctx, batch.Id, 6000m, 5);
        var svc = CreateService(ctx);

        var list = await svc.GetListAsync();

        list.Should().HaveCount(2);
        list.Single(r => r.Length == 6000m).CutQuantity.Should().Be(5);
        list.Single(r => r.Length == 4000m).CutQuantity.Should().Be(0);
        // 主号级实际切割为共享聚合（两行一致）
        list.All(r => r.MainNoCutActual == 5).Should().BeTrue();
    }

    // ========== 打印 ==========

    [Fact]
    public async Task PrintFileAsync_返回PDF字节()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var bytes = await svc.PrintFileAsync("定尺工单", new List<Dictionary<string, object>>
        {
            new() { ["WorkOrderNo"] = "WO-1", ["Length"] = 6000m }
        }, new List<PrintColumnDef>
        {
            new() { Key = "WorkOrderNo", Label = "工单号", Width = 30 },
            new() { Key = "Length", Label = "定尺长", Width = 20 }
        });

        bytes.Should().NotBeNullOrEmpty();
        bytes.Length.Should().BeGreaterThan(10);
    }
}
