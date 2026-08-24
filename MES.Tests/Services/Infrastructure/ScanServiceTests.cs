using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Infrastructure;
using MES.Tests.Tests;
using MES.Data.Entities.Batch;

namespace MES.Tests.Services;

/// <summary>
/// 扫码执行服务测试：批次/工序组解析、工段列表
/// </summary>
public class ScanServiceTests : TestBase
{
    private ScanService CreateService(AppDbContext ctx) => new(ctx);

    /// <summary>
    /// 种子一个测试批次和工序组
    /// </summary>
    private async Task<(ProductionBatch batch, ProcessGroup group)> SeedBatchWithGroupAsync(
        AppDbContext ctx,
        BatchStatus status = BatchStatus.None,
        Action<ProcessGroup>? configureGroup = null)
    {
        var batch = new ProductionBatch
        {
            BatchNo = $"SCAN-TEST-{Guid.NewGuid():N}"[..15],
            Status = status,
            PlantGrade = "304",
            Specification = "219*8",
            TagNo = "TAG001",
            ProductionType = "在制生产",
            ManufacturingItem = "OrderFinished",
            WorkOrderNo = "GD250101001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            DelayPenalty = false,
            MaterialName = "无缝管",
            SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163",
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = "Fixed",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            RowVersion = new byte[8]
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();

        var group = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            ColdRollDraw = 1,
            Solution = 2,
            Straighten = 3,
            Inspection = 10
        };
        configureGroup?.Invoke(group);
        ctx.ProcessGroups.Add(group);
        await ctx.SaveChangesAsync();

        return (batch, group);
    }

    [Fact]
    public async Task ResolveAsync_成功返回可用工段()
    {
        using var ctx = CreateDbContext();
        var (batch, group) = await SeedBatchWithGroupAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.ResolveAsync(batch.BatchNo, group.Id);

        result.Should().NotBeNull();
        result.BatchNo.Should().Be(batch.BatchNo);
        result.PlantGrade.Should().Be("304");
        result.ProcessGroupId.Should().Be(group.Id);
        result.ProcessName.Should().Be("60冷轧");
        result.ManufacturingSpec.Should().Be("219*8");
    }

    [Fact]
    public async Task ResolveAsync_批次不存在_抛出BusinessException()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.ResolveAsync("NOT-EXIST-BATCH", 1);

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*未找到批次*");
    }

    [Fact]
    public async Task ResolveAsync_工序组不存在_抛出BusinessException()
    {
        using var ctx = CreateDbContext();
        var batch = new ProductionBatch
        {
            BatchNo = $"SCAN-NOGROUP-{Guid.NewGuid():N}"[..15],
            Status = BatchStatus.None,
            PlantGrade = "304",
            Specification = "219*8",
            ManufacturingItem = "OrderFinished",
            WorkOrderNo = "GD250101002",
            SalesOrderNo = "SO002",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管",
            SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163",
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = "Fixed",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            RowVersion = new byte[8]
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var act = () => svc.ResolveAsync(batch.BatchNo, 99999);

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*未找到工序组*");
    }

    [Fact]
    public async Task ResolveAsync_多工序段_按序号排序()
    {
        using var ctx = CreateDbContext();
        var (batch, group) = await SeedBatchWithGroupAsync(ctx);
        // Modify group sections to have multiple entries with different orders
        group.ColdRollDraw = 3;
        group.Solution = 1;
        group.Straighten = 2;
        group.Pickle = null; // no pickle
        group.Inspection = null; // not relevant for this test
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.ResolveAsync(batch.BatchNo, group.Id);

        result.AvailableSections.Should().HaveCount(3);
        result.AvailableSections[0].SequenceNumber.Should().Be(1);
        result.AvailableSections[0].SectionName.Should().Be("固溶");
        result.AvailableSections[1].SequenceNumber.Should().Be(2);
        result.AvailableSections[1].SectionName.Should().Be("矫直");
        result.AvailableSections[2].SequenceNumber.Should().Be(3);
        result.AvailableSections[2].SectionName.Should().Be("冷轧拔");
    }

    [Fact]
    public async Task ResolveAsync_工段包含检验()
    {
        using var ctx = CreateDbContext();
        var (batch, group) = await SeedBatchWithGroupAsync(ctx);
        // Default group: ColdRollDraw=1, Solution=2, Straighten=3, Inspection=10
        var svc = CreateService(ctx);
        var result = await svc.ResolveAsync(batch.BatchNo, group.Id);

        // Inspection should be included along with ColdRollDraw/Solution/Straighten
        result.AvailableSections.Should().HaveCount(4);
        result.AvailableSections.Should().Contain(s => s.SectionName == "检验");
        result.AvailableSections.Should().Contain(s => s.SectionName == "冷轧拔");
        result.AvailableSections.Should().Contain(s => s.SectionName == "固溶");
        result.AvailableSections.Should().Contain(s => s.SectionName == "矫直");
    }

    [Fact]
    public async Task ResolveAsync_空工段列表_返回空列表()
    {
        using var ctx = CreateDbContext();
        var (batch, group) = await SeedBatchWithGroupAsync(ctx);
        // Clear all sections in the group
        group.ColdRollDraw = null;
        group.Solution = null;
        group.Straighten = null;
        group.Inspection = null;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.ResolveAsync(batch.BatchNo, group.Id);

        result.AvailableSections.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBatchProcessGroupsAsync_成功返回工序组列表()
    {
        using var ctx = CreateDbContext();
        var (batch, group) = await SeedBatchWithGroupAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetBatchProcessGroupsAsync(batch.BatchNo);

        result.Should().NotBeNull();
        result.BatchNo.Should().Be(batch.BatchNo);
        result.ProcessGroups.Should().HaveCount(1);
        result.ProcessGroups[0].Id.Should().Be(group.Id);
        result.ProcessGroups[0].ProcessName.Should().Be("60冷轧");
    }

    [Fact]
    public async Task GetBatchProcessGroupsAsync_批次不存在_抛出BusinessException()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetBatchProcessGroupsAsync("NOT-EXIST");

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task GetBatchProcessGroupsAsync_多工序组_按序号排序()
    {
        using var ctx = CreateDbContext();
        var (batch, _) = await SeedBatchWithGroupAsync(ctx);
        var group2 = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 2,
            ProcessName = "LG60冷轧",
            ManufacturingSpec = "219*8",
            ColdRollDraw = 1
        };
        ctx.ProcessGroups.Add(group2);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetBatchProcessGroupsAsync(batch.BatchNo);

        result.ProcessGroups.Should().HaveCount(2);
        result.ProcessGroups[0].SequenceNumber.Should().Be(1);
        result.ProcessGroups[1].SequenceNumber.Should().Be(2);
    }

    [Fact]
    public async Task GetBatchProcessGroupsAsync_返回单支重量()
    {
        using var ctx = CreateDbContext();
        var (batch, _) = await SeedBatchWithGroupAsync(ctx); // TotalWeight=2500 / TotalQuantity=100 → 25
        var svc = CreateService(ctx);

        var result = await svc.GetBatchProcessGroupsAsync(batch.BatchNo);

        result.UnitWeight.Should().Be(25m);
    }

    [Fact]
    public async Task GetBatchProcessGroupsAsync_有效重量优先_计算单支重量()
    {
        using var ctx = CreateDbContext();
        var (batch, _) = await SeedBatchWithGroupAsync(ctx);
        batch.CurrentValidWeight = 2000; // 有效重量优先于总重量（int?）
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetBatchProcessGroupsAsync(batch.BatchNo);

        result.UnitWeight.Should().Be(20m); // 2000/100
    }

    [Fact]
    public async Task ResolveAsync_新工段包装_命中工段列表()
    {
        using var ctx = CreateDbContext();
        var (batch, group) = await SeedBatchWithGroupAsync(ctx, configureGroup: g =>
        {
            g.ColdRollDraw = null;
            g.Solution = null;
            g.Straighten = null;
            g.Inspection = null;
            g.Packing = 5; // 26 工段补齐后的新工段
        });
        var svc = CreateService(ctx);

        var result = await svc.ResolveAsync(batch.BatchNo, group.Id);

        result.AvailableSections.Should().HaveCount(1);
        result.AvailableSections[0].SectionName.Should().Be("包装");
        result.AvailableSections[0].SequenceNumber.Should().Be(5);
    }

    [Fact]
    public async Task GetBatchProcessGroupsAsync_新工段包装_返回工段名()
    {
        using var ctx = CreateDbContext();
        var (batch, group) = await SeedBatchWithGroupAsync(ctx, configureGroup: g =>
        {
            g.ColdRollDraw = null;
            g.Solution = null;
            g.Straighten = null;
            g.Inspection = null;
            g.Packing = 1;
        });
        var svc = CreateService(ctx);

        var result = await svc.GetBatchProcessGroupsAsync(batch.BatchNo);

        result.ProcessGroups[0].SectionNames.Should().Contain("包装");
    }
}
