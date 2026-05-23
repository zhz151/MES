using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Services.DataFix;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services;

/// <summary>
/// 数据修复服务测试：验证 FixAllAsync 各修复逻辑正确执行
/// </summary>
public class DataFixServiceTests : TestBase
{
    private DataFixService CreateService(AppDbContext ctx,
        IProductionRecordService? prodRecordMock = null,
        IPurchaseOrderService? purchaseMock = null,
        ISubcontractOrderService? subcontractMock = null)
    {
        prodRecordMock ??= new Mock<IProductionRecordService>().Object;
        purchaseMock ??= new Mock<IPurchaseOrderService>().Object;
        subcontractMock ??= new Mock<ISubcontractOrderService>().Object;
        var loggerMock = new Mock<ILogger<DataFixService>>().Object;
        return new DataFixService(ctx, prodRecordMock, purchaseMock, subcontractMock, loggerMock);
    }

    /// <summary>
    /// 种子一个最小 ProductionBatch（仅填必要字段）
    /// </summary>
    private async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx, string batchNo = "TEST-001")
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            WorkOrderNo = "WO-TEST",
            SalesOrderNo = "SO-TEST",
            ProductionMainNo = "M-TEST",
            ManufacturingItem = "订单成品",
            MaterialName = "无缝管",
            SettlementMethod = "过磅",
            StandardCode = "GB/T 8163",
            DeliveryState = "固溶酸洗",
            PlantGrade = "Q345B",
            Specification = "219*8",
            LengthStatus = "定尺",
            TechnicalRequirements = "按标准",
            Salesman = "测试",
            CreatedBy = "tester",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            OrderItemIds = "1",
            ProductionRatio = 1,
            TotalQuantity = 1,
            TotalMeters = 10m,
            TotalWeight = 100m,
            TotalItemCount = 1,
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            IsForceCompleted = false,
        };
        ctx.Set<ProductionBatch>().Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    /// <summary>
    /// 种子一个最小 ProcessGroup
    /// </summary>
    private async Task<ProcessGroup> SeedProcessGroupAsync(AppDbContext ctx,
        ProductionBatch batch, string processName = "60冷轧",
        int? coldRollDraw = 2)
    {
        var pg = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = processName,
            ManufacturingSpec = "219*8",
            ManufacturingMultiple = 1,
            ColdRollDraw = coldRollDraw,
            OilPipeCut = null,
            Degrease = null,
            Solution = null,
            Straighten = null,
            Cut = null,
            ThicknessMeasure = null,
            Pickle = null,
            OuterPolish = null,
            InnerGrinding = null,
            OuterSpotGrinding = null,
            Inspection = null,
            WeldingHead = null,
            Lubrication = null,
            Warehouse = null,
        };
        ctx.Set<ProcessGroup>().Add(pg);
        await ctx.SaveChangesAsync();
        return pg;
    }

    // ========== 测试 ==========

    [Fact]
    public async Task FixAllAsync_空数据库_各计数为0()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var report = await svc.FixAllAsync();

        report.SequenceNumbersFixed.Should().Be(0);
        report.OutsourceStatusFixed.Should().Be(0);
        report.BatchTrackingFixed.Should().Be(0);
        report.PurchaseOrdersFixed.Should().Be(0);
        report.SubcontractOrdersFixed.Should().Be(0);
        report.EquipmentFixed.Should().Be(0);
        report.Total.Should().Be(0);
    }

    [Fact]
    public async Task FixSequenceNumbersAsync_修复ProductionRecord序号和ProcessGroupId()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);

        // 创建两个工序组：一个冷轧拔=2，一个冷轧拔=5
        var pg1 = await SeedProcessGroupAsync(ctx, batch, "60冷轧", coldRollDraw: 2);
        var pg2 = await SeedProcessGroupAsync(ctx, batch, "60冷轧2", coldRollDraw: 5);

        // ProductionRecord：指向 pg2，SectionName="冷轧拔"，SequenceNumber=0（错误值）
        var record = new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg2.Id,
            ProcessName = pg1.ProcessName,
            ManufacturingSpec = pg1.ManufacturingSpec,
            SectionName = "冷轧拔",
            SequenceNumber = 0,
            CreatedBy = "tester",
        };
        ctx.Set<ProductionRecord>().Add(record);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var report = await svc.FixAllAsync();

        report.SequenceNumbersFixed.Should().Be(1);

        // 验证：记录被修复 → 指向 pg1，序号变为 2
        var fixedRecord = await ctx.Set<ProductionRecord>().FirstAsync();
        fixedRecord.ProcessGroupId.Should().Be(pg1.Id);
        fixedRecord.SequenceNumber.Should().Be(2);
    }

    [Fact]
    public async Task FixSequenceNumbersAsync_修复ProcessInspection序号()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch, "60冷轧", coldRollDraw: 3);

        var inspection = new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = pg.ProcessName,
            ManufacturingSpec = pg.ManufacturingSpec ?? "",
            SectionName = "冷轧拔",
            SequenceNumber = 0,
            CreatedBy = "tester",
        };
        ctx.Set<ProcessInspection>().Add(inspection);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.FixAllAsync();

        var fixedInsp = await ctx.Set<ProcessInspection>().FirstAsync();
        fixedInsp.SequenceNumber.Should().Be(3);
    }

    [Fact]
    public async Task FixSequenceNumbersAsync_修复SectionOutsource序号()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch, "60冷轧", coldRollDraw: 4);

        var os = new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = pg.ProcessName,
            ManufacturingSpec = pg.ManufacturingSpec,
            SectionName = "冷轧拔",
            SequenceNumber = 0,
            OutsourceVendor = "测试委外厂",
            SendOutDate = DateTime.Today,
            CreatedBy = "tester",
        };
        ctx.Set<SectionOutsource>().Add(os);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.FixAllAsync();

        var fixedOs = await ctx.Set<SectionOutsource>().FirstAsync();
        fixedOs.SequenceNumber.Should().Be(4);
    }

    [Fact]
    public async Task FixSectionOutsourceStatusAsync_修复为Recovered()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch);

        var os = new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = "冷轧拔",
            SequenceNumber = 1,
            OutsourceVendor = "测试委外厂",
            SendOutDate = DateTime.Today,
            SendWeight = 100m,  // 发出 100kg
            Status = Core.Enums.SectionOutsourceStatus.PendingRecovery,
            CreatedBy = "tester",
        };
        ctx.Set<SectionOutsource>().Add(os);
        await ctx.SaveChangesAsync();

        // 回收 100kg → 达到 99% 阈值 → 应变为 Recovered
        var recovery = new OutsourceRecovery
        {
            SectionOutsourceId = os.Id,
            RecoveryDate = DateTime.Today,
            RecoveryWeight = 100m,
            UnprocessedWeight = 0m,
            CreatedBy = "tester",
        };
        ctx.Set<OutsourceRecovery>().Add(recovery);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.FixAllAsync();

        var fixedOs = await ctx.Set<SectionOutsource>().FirstAsync();
        fixedOs.Status.Should().Be(Core.Enums.SectionOutsourceStatus.Recovered);
    }

    [Fact]
    public async Task FixSectionOutsourceStatusAsync_未达阈值_保持PendingRecovery()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch);

        var os = new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = "冷轧拔",
            SequenceNumber = 1,
            OutsourceVendor = "测试委外厂",
            SendOutDate = DateTime.Today,
            SendWeight = 100m,
            Status = Core.Enums.SectionOutsourceStatus.PendingRecovery,
            CreatedBy = "tester",
        };
        ctx.Set<SectionOutsource>().Add(os);
        await ctx.SaveChangesAsync();

        // 仅回收 50kg → 未达 99%
        var recovery = new OutsourceRecovery
        {
            SectionOutsourceId = os.Id,
            RecoveryDate = DateTime.Today,
            RecoveryWeight = 50m,
            UnprocessedWeight = 0m,
            CreatedBy = "tester",
        };
        ctx.Set<OutsourceRecovery>().Add(recovery);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.FixAllAsync();

        var fixedOs = await ctx.Set<SectionOutsource>().FirstAsync();
        fixedOs.Status.Should().Be(Core.Enums.SectionOutsourceStatus.PendingRecovery);
    }

    [Fact]
    public async Task FixBatchTrackingAsync_调用BatchUpdateBatchTrackingAsync()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "TEST-001");

        var prodRecordMock = new Mock<IProductionRecordService>();
        prodRecordMock.Setup(x => x.BatchUpdateBatchTrackingAsync(It.IsAny<ICollection<int>>()))
            .Returns(Task.CompletedTask);

        var svc = CreateService(ctx, prodRecordMock: prodRecordMock.Object);
        var report = await svc.FixAllAsync();

        report.BatchTrackingFixed.Should().Be(1);
        prodRecordMock.Verify(x => x.BatchUpdateBatchTrackingAsync(
            It.Is<ICollection<int>>(ids => ids.Contains(batch.Id))), Times.Once);
    }

    [Fact]
    public async Task FixEquipmentTrackingAsync_修复设备日期字段()
    {
        var ctx = CreateDbContext();

        var equipment = new Equipment
        {
            EquipmentCode = "EQ001",
            EquipmentName = "测试设备",
            Location = "车间A",
            LifecycleStatus = nameof(Core.Enums.LifecycleStatus.Active),
            UsageType = nameof(Core.Enums.UsageType.Primary),
            InspectionCycleDays = 7,
            MaintCycleDays = 30,
            CreatedBy = "tester",
        };
        ctx.Set<Equipment>().Add(equipment);
        await ctx.SaveChangesAsync();

        var inspectionDate = new DateTime(2026, 5, 1);
        var inspection = new InspectionRecord
        {
            EquipmentId = equipment.Id,
            ActualDate = inspectionDate,
            RecordNo = "IR001",
            Inspector = "张三",
            CreatedBy = "tester",
        };
        ctx.Set<InspectionRecord>().Add(inspection);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.FixAllAsync();

        var fixedEq = await ctx.Set<Equipment>().FirstAsync();
        fixedEq.LastInspectionDate.Should().Be(inspectionDate);
    }

    [Fact]
    public async Task FixPurchaseOrdersAsync_调用SyncAllAsync()
    {
        var ctx = CreateDbContext();

        var purchaseMock = new Mock<IPurchaseOrderService>();
        purchaseMock.Setup(x => x.SyncAllAsync()).Returns(Task.CompletedTask);

        var svc = CreateService(ctx, purchaseMock: purchaseMock.Object);
        await svc.FixAllAsync();

        purchaseMock.Verify(x => x.SyncAllAsync(), Times.Once);
    }

    [Fact]
    public async Task FixSubcontractOrdersAsync_调用SyncAllAsync()
    {
        var ctx = CreateDbContext();

        var subcontractMock = new Mock<ISubcontractOrderService>();
        subcontractMock.Setup(x => x.SyncAllAsync()).Returns(Task.CompletedTask);

        var svc = CreateService(ctx, subcontractMock: subcontractMock.Object);
        await svc.FixAllAsync();

        subcontractMock.Verify(x => x.SyncAllAsync(), Times.Once);
    }

    [Fact]
    public async Task FixAllAsync_完整链路_各模块均执行()
    {
        var ctx = CreateDbContext();

        // ===== Seed 数据供 FixSequenceNumbers 和 FixEquipmentTracking =====
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch, "60冷轧", coldRollDraw: 7);

        var record = new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = pg.ProcessName,
            ManufacturingSpec = pg.ManufacturingSpec,
            SectionName = "冷轧拔",
            SequenceNumber = 0,
            CreatedBy = "tester",
        };
        ctx.Set<ProductionRecord>().Add(record);

        var equipment = new Equipment
        {
            EquipmentCode = "EQ002",
            EquipmentName = "测试设备2",
            Location = "车间B",
            LifecycleStatus = nameof(Core.Enums.LifecycleStatus.Active),
            UsageType = nameof(Core.Enums.UsageType.Primary),
            InspectionCycleDays = 7,
            MaintCycleDays = 30,
            CreatedBy = "tester",
        };
        ctx.Set<Equipment>().Add(equipment);

        var inspection = new InspectionRecord
        {
            EquipmentId = equipment.Id,
            ActualDate = new DateTime(2026, 5, 15),
            RecordNo = "IR002",
            Inspector = "李四",
            CreatedBy = "tester",
        };
        ctx.Set<InspectionRecord>().Add(inspection);
        await ctx.SaveChangesAsync();

        // ===== Mocks =====
        var prodRecordMock = new Mock<IProductionRecordService>();
        prodRecordMock.Setup(x => x.BatchUpdateBatchTrackingAsync(It.IsAny<ICollection<int>>()))
            .Returns(Task.CompletedTask);
        var purchaseMock = new Mock<IPurchaseOrderService>();
        purchaseMock.Setup(x => x.SyncAllAsync()).Returns(Task.CompletedTask);
        var subcontractMock = new Mock<ISubcontractOrderService>();
        subcontractMock.Setup(x => x.SyncAllAsync()).Returns(Task.CompletedTask);

        var svc = CreateService(ctx,
            prodRecordMock: prodRecordMock.Object,
            purchaseMock: purchaseMock.Object,
            subcontractMock: subcontractMock.Object);

        // ===== 执行 =====
        var report = await svc.FixAllAsync();

        // ===== 验证 =====
        report.SequenceNumbersFixed.Should().Be(1);
        report.BatchTrackingFixed.Should().Be(1);
        report.EquipmentFixed.Should().Be(1);

        var fixedRecord = await ctx.Set<ProductionRecord>().FirstAsync();
        fixedRecord.SequenceNumber.Should().Be(7);

        var fixedEq = await ctx.Set<Equipment>().FirstAsync();
        fixedEq.LastInspectionDate.Should().Be(new DateTime(2026, 5, 15));

        prodRecordMock.Verify(x => x.BatchUpdateBatchTrackingAsync(
            It.Is<List<int>>(ids => ids.Contains(batch.Id))), Times.Once);
        purchaseMock.Verify(x => x.SyncAllAsync(), Times.Once);
        subcontractMock.Verify(x => x.SyncAllAsync(), Times.Once);
    }
}
