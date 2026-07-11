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
            ManufacturingItem = "订单成品",
            WorkOrderNo = "GD250101001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            DelayPenalty = false,
            MaterialName = "无缝管",
            SettlementMethod = "理算",
            StandardCode = "GB/T 8163",
            DeliveryState = "固溶酸洗",
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
        result.Status.Should().Be("未产");
        result.PlantGrade.Should().Be("304");
        result.Specification.Should().Be("219*8");
        result.TagNo.Should().Be("TAG001");
        result.ProductionType.Should().Be("在制生产");
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
            ManufacturingItem = "订单成品",
            WorkOrderNo = "GD250101002",
            SalesOrderNo = "SO002",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管",
            SettlementMethod = "理算",
            StandardCode = "GB/T 8163",
            DeliveryState = "固溶酸洗",
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
    public async Task ResolveAsync_状态中文映射验证()
    {
        using var ctx = CreateDbContext();

        // Test all status mappings
        var statusMappings = new Dictionary<BatchStatus, string>
        {
            [BatchStatus.None] = "未产",
            [BatchStatus.InProgress] = "在产",
            [BatchStatus.Completed] = "完成",
            [BatchStatus.Suspended] = "挂起",
        };

        foreach (var (status, expectedText) in statusMappings)
        {
            var batchNo = $"SCAN-STATUS-{Guid.NewGuid():N}"[..15];
            var batch = new ProductionBatch
            {
                BatchNo = batchNo,
                Status = status,
                PlantGrade = "304",
                Specification = "219*8",
                ManufacturingItem = "订单成品",
                WorkOrderNo = "GD250101003",
                SalesOrderNo = "SO003",
                ProductionMainNo = "D01",
                OrderItemIds = "1",
                SignDate = DateTime.Today,
                Salesman = "测试",
                DeliveryDate = DateTime.Today.AddMonths(1),
                MaterialName = "无缝管",
                SettlementMethod = "理算",
                StandardCode = "GB/T 8163",
                DeliveryState = "固溶酸洗",
                LengthStatus = "Fixed",
                TotalQuantity = 100,
                TotalMeters = 600,
                TotalWeight = 2500m,
                TotalItemCount = 1,
                TechnicalRequirements = "NORMAL",
                RowVersion = new byte[8]
            };
            ctx.ProductionBatches.Add(batch);
            var group = new ProcessGroup
            {
                ProductionBatchId = batch.Id,
                SequenceNumber = 1,
                ProcessName = "60冷轧",
                ColdRollDraw = 1
            };
            ctx.ProcessGroups.Add(group);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var result = await svc.ResolveAsync(batchNo, group.Id);
            result.Status.Should().Be(expectedText, $"批次状态 {status} 应映射为 \"{expectedText}\"");

            // Clean up for next iteration
            ctx.ProductionBatches.Remove(batch);
            ctx.ProcessGroups.Remove(group);
            await ctx.SaveChangesAsync();
        }
    }
}
