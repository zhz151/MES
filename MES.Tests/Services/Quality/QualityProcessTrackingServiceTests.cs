using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;
using MES.Services.Quality;
using MES.Tests.Tests;

namespace MES.Tests.Services.Quality;

/// <summary>
/// 成检追踪（QualityProcessTracking）物化读模型刷新测试
/// 覆盖：唯一键「批次+成检类型」两字段、检验支数分组求和取最大、理论合格支
/// </summary>
public class QualityProcessTrackingServiceTests : TestBase
{
    private QualityProcessTrackingService CreateService(AppDbContext ctx)
        => new(ctx, NullLogger<QualityProcessTrackingService>.Instance, new MemoryCache(new MemoryCacheOptions()));

    private async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx, string batchNo = "BATCH001")
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            WorkOrderNo = "WO001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "[]",
            MaterialName = "不锈钢管",
            PlantGrade = "304",
            Specification = "219*8",
            Status = BatchStatus.InFinalInspection,
            ProductionType = "InProcess",
            ManufacturingItem = "OrderFinished",
            CurrentValidQty = 100,
            CurrentValidWeight = 5000,
            Salesman = "张三",
            SettlementMethod = "Weighing",
            StandardCode = "GB/T 14976",
            DeliveryState = "Hard",
            ManufacturingStatus = "Hard",
            LengthStatus = "NonFixed",
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
            CutRequirement = false,
            TheoreticalOutputQty = 100,
            TheoreticalOutputWeight = 5000
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    private static MaterialReceiveCheck SeedMrCheck(AppDbContext ctx, ProductionBatch batch, string inspectionType, int processGroupId = 1)
    {
        var rc = new MaterialReceiveCheck
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            ReceiveDate = DateTime.Today,
            ProcessGroupId = processGroupId,
            InspectionType = inspectionType
        };
        ctx.Set<MaterialReceiveCheck>().Add(rc);
        return rc;
    }

    private static FinalInspection SeedInspection(AppDbContext ctx, ProductionBatch batch,
        InspectionItem item, int quantity, string inspectionType,
        int? rework = null, int? warehouse = null, int? scrap = null)
    {
        var fi = new FinalInspection
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            InspectionItem = item,
            InspectionDate = DateTime.Today,
            InspectionType = inspectionType,
            Quantity = quantity,
            QualifiedQuantity = quantity
        };
        if (rework.HasValue) fi.DefectReworkQuantity = rework;
        if (warehouse.HasValue) fi.DefectWarehouseQuantity = warehouse;
        if (scrap.HasValue) fi.DefectScrapQuantity = scrap;
        ctx.FinalInspections.Add(fi);
        return fi;
    }

    private static ProductionRecord SeedCutRecord(AppDbContext ctx, ProductionBatch batch,
        string sectionName, int qty, int? postCutQty)
    {
        var pr = new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ExecDate = DateTime.Today,
            ProcessGroupId = 1,
            ProcessName = sectionName,
            SectionName = sectionName,
            ProductStatus = ProductStatuses.Finished,
            Quantity = qty,
            PostCutQuantity = postCutQty,
            IsPreCut = false
        };
        ctx.ProductionRecords.Add(pr);
        return pr;
    }

    // ========== 聚合逻辑 ==========

    [Fact]
    public async Task Refresh_检验支数按项目分组求和取最大_理论合格支为检验减次品()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        SeedMrCheck(ctx, batch, nameof(InspectionType.FormalInspection));
        // Dimension 3 条 → 10+8+12=30；Hydrostatic 2 条 → 20+5=25；检验支数取 max(30,25)=30
        SeedInspection(ctx, batch, InspectionItem.Dimension, 10, "FormalInspection", rework: 2);
        SeedInspection(ctx, batch, InspectionItem.Dimension, 8, "FormalInspection", scrap: 3);
        SeedInspection(ctx, batch, InspectionItem.Dimension, 12, "FormalInspection");
        SeedInspection(ctx, batch, InspectionItem.HydrostaticPressure, 20, "FormalInspection", warehouse: 1);
        SeedInspection(ctx, batch, InspectionItem.HydrostaticPressure, 5, "FormalInspection");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshByProductionBatchIdAsync(batch.Id);

        var row = await ctx.QualityProcessTrackings
            .SingleAsync(q => q.ProductionBatchId == batch.Id && q.InspectionType == "FormalInspection");
        row.TotalQuantity.Should().Be(30);
        row.DefectReworkQuantity.Should().Be(2);
        row.DefectWarehouseQuantity.Should().Be(1);
        row.DefectScrapQuantity.Should().Be(3);
        row.QualifiedQuantity.Should().Be(30 - 2 - 1 - 3);
        // 非定尺 + 无需切割：生产支数=理论成品支数，生产重量=批次理论成品重量
        row.ProductionCutQuantity.Should().Be(100);
        row.ProductionWeight.Should().Be(5000);
    }

    [Fact]
    public async Task Refresh_次品总和大于检验支数_理论合格支归零()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        SeedMrCheck(ctx, batch, nameof(InspectionType.FormalInspection));
        SeedInspection(ctx, batch, InspectionItem.Dimension, 10, "FormalInspection", scrap: 8);
        SeedInspection(ctx, batch, InspectionItem.HydrostaticPressure, 5, "FormalInspection", scrap: 9);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshByProductionBatchIdAsync(batch.Id);

        var row = await ctx.QualityProcessTrackings
            .SingleAsync(q => q.ProductionBatchId == batch.Id);
        // 检验支数 max(10,5)=10；次品=8+9=17 → 理论合格支归零
        row.TotalQuantity.Should().Be(10);
        row.QualifiedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Refresh_无检验记录_检验支数与理论合格支为零()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        SeedMrCheck(ctx, batch, nameof(InspectionType.FormalInspection));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshByProductionBatchIdAsync(batch.Id);

        var row = await ctx.QualityProcessTrackings
            .SingleAsync(q => q.ProductionBatchId == batch.Id);
        row.TotalQuantity.Should().Be(0);
        row.QualifiedQuantity.Should().Be(0);
    }

    // ========== 唯一键「批次+成检类型」 ==========

    [Fact]
    public async Task Refresh_同批次同类型多次刷新_仅一行()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        SeedMrCheck(ctx, batch, nameof(InspectionType.FormalInspection));
        SeedInspection(ctx, batch, InspectionItem.Dimension, 10, "FormalInspection");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshByProductionBatchIdAsync(batch.Id);
        await svc.RefreshByProductionBatchIdAsync(batch.Id);

        var rows = await ctx.QualityProcessTrackings
            .Where(q => q.ProductionBatchId == batch.Id).ToListAsync();
        rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task Refresh_交付态变化_不产生第二行仅更新原行()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        SeedMrCheck(ctx, batch, nameof(InspectionType.FormalInspection));
        SeedInspection(ctx, batch, InspectionItem.Dimension, 10, "FormalInspection");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshByProductionBatchIdAsync(batch.Id);

        // 交付态变化：批次制造状态改为与交货状态不同 → 交付态由"是"变"否"
        batch.ManufacturingStatus = "Bright";
        await ctx.SaveChangesAsync();
        await svc.RefreshByProductionBatchIdAsync(batch.Id);

        var rows = await ctx.QualityProcessTrackings
            .Where(q => q.ProductionBatchId == batch.Id).ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].IsDeliveryStatus.Should().Be("否");
    }

    [Fact]
    public async Task Refresh_同批次预检与正式成检_各一行()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        SeedMrCheck(ctx, batch, nameof(InspectionType.PreInspection), processGroupId: 1);
        SeedMrCheck(ctx, batch, nameof(InspectionType.FormalInspection), processGroupId: 2);
        SeedInspection(ctx, batch, InspectionItem.Dimension, 10, nameof(InspectionType.PreInspection));
        SeedInspection(ctx, batch, InspectionItem.Dimension, 20, nameof(InspectionType.FormalInspection));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshByProductionBatchIdAsync(batch.Id);

        var rows = await ctx.QualityProcessTrackings
            .Where(q => q.ProductionBatchId == batch.Id).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(r => r.InspectionType == nameof(InspectionType.PreInspection));
        rows.Should().ContainSingle(r => r.InspectionType == nameof(InspectionType.FormalInspection));
        rows.Single(r => r.InspectionType == nameof(InspectionType.PreInspection)).TotalQuantity.Should().Be(10);
        rows.Single(r => r.InspectionType == nameof(InspectionType.FormalInspection)).TotalQuantity.Should().Be(20);
    }

    // ========== 生产支数 / 生产重量 ==========

    [Fact]
    public async Task Refresh_定尺需切割_生产支数为切后支数汇总_生产重量为产品单支重乘生产支数()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        batch.LengthStatus = "Fixed";
        batch.CutRequirement = true;
        batch.ProductUnitWeight = 25.0m;
        batch.TheoreticalUnitWeight = 30.0m;
        SeedMrCheck(ctx, batch, nameof(InspectionType.FormalInspection));
        // 断切成品记录：切后支数 40+60=100
        SeedCutRecord(ctx, batch, SectionKeys.Cut, 100, 40);
        SeedCutRecord(ctx, batch, SectionKeys.Cut, 100, 60);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshByProductionBatchIdAsync(batch.Id);

        var row = await ctx.QualityProcessTrackings
            .SingleAsync(q => q.ProductionBatchId == batch.Id);
        row.ProductionCutQuantity.Should().Be(100);
        row.ProductionWeight.Should().Be(25.0m * 100); // 产品单支重 × 生产支数
    }

    [Fact]
    public async Task Refresh_定尺需切割无断切记录_生产支数为零_生产重量为产品单支重乘零()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        batch.LengthStatus = "Fixed";
        batch.CutRequirement = true;
        batch.ProductUnitWeight = 25.0m;
        SeedMrCheck(ctx, batch, nameof(InspectionType.FormalInspection));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshByProductionBatchIdAsync(batch.Id);

        var row = await ctx.QualityProcessTrackings
            .SingleAsync(q => q.ProductionBatchId == batch.Id);
        row.ProductionCutQuantity.Should().Be(0);
        row.ProductionWeight.Should().Be(0);
    }
}
