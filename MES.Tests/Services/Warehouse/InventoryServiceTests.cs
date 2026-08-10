using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Models;
using MES.Services.Warehouse;
using MES.Tests.Tests;
using Moq;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Warehouse;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 库存服务测试：入库、出库、批次扣减、异常分支
/// </summary>
public class InventoryServiceTests : TestBase
{
    private InventoryService CreateService(AppDbContext ctx, Mock<IFixedLengthWorkOrderService>? fixedLenMock = null, Mock<IProductionRecordService>? prMock = null, Mock<IWorkOrderExecutionService>? woExecMock = null)
    {
        woExecMock ??= new Mock<IWorkOrderExecutionService>();
        var qualityMock = new Mock<IQualityProcessTrackingService>();
        var configMock = new Mock<IConfigParameterService>();
        var loggerMain = new Mock<ILogger<InventoryService>>();
        var loggerBatch = new Mock<ILogger<InventoryBatchWriteService>>();
        var loggerOutbound = new Mock<ILogger<OutboundWriteService>>();
        var loggerSync = new Mock<ILogger<InventorySyncService>>();
        var syncMock = new Mock<IInventorySyncService>();
        prMock ??= new Mock<IProductionRecordService>();
        var notifMock = new Mock<INotificationService>();
        fixedLenMock ??= new Mock<IFixedLengthWorkOrderService>();

        var batchWrite = new InventoryBatchWriteService(ctx, woExecMock.Object, qualityMock.Object, prMock.Object, syncMock.Object, notifMock.Object, fixedLenMock.Object, loggerBatch.Object);
        var outboundWrite = new OutboundWriteService(ctx, woExecMock.Object, loggerOutbound.Object);
        var syncService = new InventorySyncService(ctx, configMock.Object, woExecMock.Object, loggerSync.Object, new MemoryCache(new MemoryCacheOptions()));

        return new InventoryService(ctx, batchWrite, outboundWrite, syncService, loggerMain.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    // ========== 入库 ==========

    [Fact]
    public async Task InboundAsync_仓库不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = 999,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("仓库不存在");
    }

    [Fact]
    public async Task InboundAsync_成功入库_剩余量等于初始量()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            InboundDate = DateTime.Today
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().StartWith("CK");
        result.RemainingQuantity.Should().Be(10);
        result.RemainingWeight.Should().Be(1000m);
    }

    // ========== 定尺切割长度匹配标识 ==========

    private static FixedLengthLengthMaps BuildLengthMaps(params (string WorkOrderNo, decimal Length)[] byWorkOrder)
    {
        var maps = new FixedLengthLengthMaps();
        foreach (var (woNo, length) in byWorkOrder)
        {
            if (!maps.ByWorkOrderNo.TryGetValue(woNo, out var set))
                maps.ByWorkOrderNo[woNo] = set = new HashSet<decimal>();
            set.Add(length);
        }
        return maps;
    }

    private async Task<ProductionBatch> SeedProductionBatchAsync(AppDbContext ctx, string batchNo,
        string workOrderNo, string salesOrderNo, string mainNo)
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
            TotalItemCount = 1
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    private async Task<MES.Data.Entities.WorkOrder.WorkOrder> SeedWorkOrderAsync(AppDbContext ctx, string workOrderNo,
        string salesOrderNo, string mainNo)
    {
        var wo = new MES.Data.Entities.WorkOrder.WorkOrder
        {
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = mainNo,
            ProductionSubNo = "01",
            OrderItemIds = "1",
            Status = WorkOrderStatus.Confirmed,
            SignDate = DateTime.Today,
            Salesman = "张三",
            DeliveryDate = DateTime.Today.AddMonths(1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "GB/T 14976",
            DeliveryState = DeliveryState.Hard,
            PlantGrade = "304",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m
        };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();
        return wo;
    }

    private static Mock<IFixedLengthWorkOrderService> FixedLenMockWith(FixedLengthLengthMaps maps)
    {
        var fixedLenMock = new Mock<IFixedLengthWorkOrderService>();
        fixedLenMock.Setup(m => m.GetLengthMapsAsync()).ReturnsAsync(maps);
        return fixedLenMock;
    }

    /// <summary>种子成品库（FG），定尺切割长度匹配仅在此库核查</summary>
    private async Task<Warehouse> SeedFgWarehouseAsync(AppDbContext ctx)
    {
        var wh = new Warehouse { Name = "成品库", Code = "FG" };
        ctx.Warehouses.Add(wh);
        await ctx.SaveChangesAsync();
        return wh;
    }

    /// <summary>种子次品库（DEFECT），不参与定尺切割长度匹配核查</summary>
    private async Task<Warehouse> SeedDefectWarehouseAsync(AppDbContext ctx)
    {
        var wh = new Warehouse { Name = "次品库", Code = "DEFECT" };
        ctx.Warehouses.Add(wh);
        await ctx.SaveChangesAsync();
        return wh;
    }

    [Fact]
    public async Task InboundAsync_生产批号关联_定尺长度命中本工单号_完全匹配()
    {
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedProductionBatchAsync(ctx, "PB-001", "SO-X01-01", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var result = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            ProductionBatchNo = "PB-001",
            InboundDate = DateTime.Today
        });

        result.CutLengthMatchType.Should().Be(CutLengthMatchType.FullMatch);
    }

    [Fact]
    public async Task InboundAsync_生产批号关联_定尺长度仅命中主号_主号匹配()
    {
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedProductionBatchAsync(ctx, "PB-002", "SO-X01-01", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-01", 6500m)); // 本工单号定尺 6500，无 6000
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m, 6500m }; // 主号含 6000
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var result = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            ProductionBatchNo = "PB-002",
            InboundDate = DateTime.Today
        });

        result.CutLengthMatchType.Should().Be(CutLengthMatchType.MainNoMatch);
    }

    [Fact]
    public async Task InboundAsync_工单号兜底关联_定尺长度命中_完全匹配()
    {
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedWorkOrderAsync(ctx, "SO-X01-02", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-02", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var result = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            WorkOrderNo = "SO-X01-02",
            InboundDate = DateTime.Today
        });

        result.CutLengthMatchType.Should().Be(CutLengthMatchType.FullMatch);
    }

    [Fact]
    public async Task InboundAsync_非成品物料_不适用()
    {
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var result = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.Surplus, // 非 FG 成品
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            InboundDate = DateTime.Today
        });

        result.CutLengthMatchType.Should().BeNull();
    }

    [Fact]
    public async Task InboundAsync_非定尺_不适用()
    {
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var result = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            LengthStatus = LengthStatus.Range, // 范围尺
            MinLength = 5000m,
            MaxLength = 6000m,
            InboundDate = DateTime.Today
        });

        result.CutLengthMatchType.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAllCutLengthMatchAsync_回填_生产批号为主工单号兜底()
    {
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedProductionBatchAsync(ctx, "PB-003", "SO-X01-01", "SO-X01", "X01");
        await SeedWorkOrderAsync(ctx, "SO-Y01-01", "SO-Y01", "Y01");

        // 已有入库批次（CutLengthMatchType 留空待回填）
        ctx.InventoryBatches.AddRange(
            new InventoryBatch { BatchNo = "CK100", WarehouseId = wh.Id, MaterialType = MaterialType.OrderFinished.ToString(), PlantGrade = "Q345B", Specification = "219*8", InboundSource = InboundSource.Purchase.ToString(), SourceName = "供应商A", InboundDate = DateTime.Today, InitialQuantity = 10, InitialWeight = 1000m, RemainingQuantity = 10, RemainingWeight = 1000m, LengthStatus = "Fixed", MinLength = 6000m, MaxLength = 6000m, ProductionBatchNo = "PB-003" },
            new InventoryBatch { BatchNo = "CK101", WarehouseId = wh.Id, MaterialType = MaterialType.OrderFinished.ToString(), PlantGrade = "Q345B", Specification = "219*8", InboundSource = InboundSource.Purchase.ToString(), SourceName = "供应商A", InboundDate = DateTime.Today, InitialQuantity = 10, InitialWeight = 1000m, RemainingQuantity = 10, RemainingWeight = 1000m, LengthStatus = "Fixed", MinLength = 6000m, MaxLength = 6000m, WorkOrderNo = "SO-Y01-01" },
            new InventoryBatch { BatchNo = "CK102", WarehouseId = wh.Id, MaterialType = MaterialType.Surplus.ToString(), PlantGrade = "Q345B", Specification = "219*8", InboundSource = InboundSource.Purchase.ToString(), SourceName = "供应商A", InboundDate = DateTime.Today, InitialQuantity = 10, InitialWeight = 1000m, RemainingQuantity = 10, RemainingWeight = 1000m, LengthStatus = "Fixed", MinLength = 6000m, MaxLength = 6000m, ProductionBatchNo = "PB-003" }
        );
        await ctx.SaveChangesAsync();

        var maps = BuildLengthMaps(("SO-X01-01", 6000m), ("SO-Y01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        maps.ByMainKey["SO-Y01|Y01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var count = await svc.RefreshAllCutLengthMatchAsync();

        count.Should().Be(2); // CK100 生产批号主路径 + CK101 工单号兜底路径，CK102 非成品不改
        var after = await ctx.InventoryBatches.AsNoTracking().ToListAsync();
        after.Single(b => b.BatchNo == "CK100").CutLengthMatchType.Should().Be("FullMatch");
        after.Single(b => b.BatchNo == "CK101").CutLengthMatchType.Should().Be("FullMatch");
        after.Single(b => b.BatchNo == "CK102").CutLengthMatchType.Should().BeNull();
    }

    [Fact]
    public async Task InboundAsync_非成品库_即使关联可解析_不核查()
    {
        // 用户强调：核查仅针对成品库(FG)，次品库等其他库房即使有生产批号关联的定尺入库也不核查
        var ctx = CreateDbContext();
        var wh = await SeedDefectWarehouseAsync(ctx); // DEFECT 次品库
        await SeedProductionBatchAsync(ctx, "PB-004", "SO-X01-01", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var result = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished, // 订单类成品（本来可核查）
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            ProductionBatchNo = "PB-004", // 且生产批号可解析
            InboundDate = DateTime.Today
        });

        result.CutLengthMatchType.Should().BeNull(); // 非成品库 → 不核查
    }

    [Fact]
    public async Task InboundAsync_备料成品_即使关联可解析_不核查()
    {
        // 用户强调：备料成品(Finished)无工单号关联，即使定尺 + 生产批号可解析也不参与核查
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx); // FG 成品库
        await SeedProductionBatchAsync(ctx, "PB-005", "SO-X01-01", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var result = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.Finished, // 备料成品
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            ProductionBatchNo = "PB-005", // 且生产批号可解析（关联可解析）
            InboundDate = DateTime.Today
        });

        result.CutLengthMatchType.Should().BeNull(); // 备料成品 → 不核查
    }

    // ========== 定尺切割长度匹配硬校验（新建自动填充第2种 + 内联编辑） ==========

    [Fact]
    public async Task BatchInboundAsync_自动填充第2种_定尺长度不在主号集合_禁止保存()
    {
        // 用户决策#3：新建自动填充第2种（检验入库按生产批号）+ 定尺 → 硬校验禁止保存
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedProductionBatchAsync(ctx, "PB-006", "SO-X01-01", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var act = () => svc.BatchInboundAsync(new BatchInboundRequest
        {
            WarehouseId = wh.Id,
            InboundSource = InboundSource.InspectionInbound,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            EnforceCutLengthMatch = true,
            Rows = new List<InboundRow>
            {
                new() { InitialQuantity = 10, InitialWeight = 1000m, LengthStatus = LengthStatus.Fixed, MinLength = 7000m, MaxLength = 7000m, ProductionBatchNo = "PB-006", WorkOrderNo = "SO-X01-01" }
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不在主号*");
        // 硬校验在 SaveChanges 前抛出 → 未落库
        (await ctx.InventoryBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BatchInboundAsync_自动填充第2种_定尺长度命中主号_入库成功()
    {
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedProductionBatchAsync(ctx, "PB-007", "SO-X01-01", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var result = await svc.BatchInboundAsync(new BatchInboundRequest
        {
            WarehouseId = wh.Id,
            InboundSource = InboundSource.InspectionInbound,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            EnforceCutLengthMatch = true,
            Rows = new List<InboundRow>
            {
                new() { InitialQuantity = 10, InitialWeight = 1000m, LengthStatus = LengthStatus.Fixed, MinLength = 6000m, MaxLength = 6000m, ProductionBatchNo = "PB-007", WorkOrderNo = "SO-X01-01" }
            }
        });

        result.SuccessCount.Should().Be(1);
        var saved = await ctx.InventoryBatches.AsNoTracking().SingleAsync();
        saved.CutLengthMatchType.Should().Be(nameof(CutLengthMatchType.FullMatch));
    }

    [Fact]
    public async Task BatchInboundAsync_非自动填充_定尺长度不匹配_不核查()
    {
        // 用户决策：非自动填充模式不做任何核查（EnforceCutLengthMatch=false → 仅软计算，不阻止）
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedProductionBatchAsync(ctx, "PB-008", "SO-X01-01", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var result = await svc.BatchInboundAsync(new BatchInboundRequest
        {
            WarehouseId = wh.Id,
            InboundSource = InboundSource.InspectionInbound,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            EnforceCutLengthMatch = false,
            Rows = new List<InboundRow>
            {
                new() { InitialQuantity = 10, InitialWeight = 1000m, LengthStatus = LengthStatus.Fixed, MinLength = 7000m, MaxLength = 7000m, ProductionBatchNo = "PB-008", WorkOrderNo = "SO-X01-01" }
            }
        });

        result.SuccessCount.Should().Be(1); // 不阻止保存
        var saved = await ctx.InventoryBatches.AsNoTracking().SingleAsync();
        saved.CutLengthMatchType.Should().BeNull(); // 7000 不在主号定尺集，软计算为空
    }

    [Fact]
    public async Task BatchInboundAsync_第1种采购_即使长度不匹配_不核查()
    {
        // 用户决策#2：第1种自动填充（采购/委外按来源单号）不核查定尺长度匹配
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedProductionBatchAsync(ctx, "PB-009", "SO-X01-01", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var result = await svc.BatchInboundAsync(new BatchInboundRequest
        {
            WarehouseId = wh.Id,
            InboundSource = InboundSource.Purchase, // 第1种
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            EnforceCutLengthMatch = true, // 即使前端误传 true，第1种也不核查
            Rows = new List<InboundRow>
            {
                new() { InitialQuantity = 10, InitialWeight = 1000m, LengthStatus = LengthStatus.Fixed, MinLength = 7000m, MaxLength = 7000m, ProductionBatchNo = "PB-009", WorkOrderNo = "SO-X01-01" }
            }
        });

        result.SuccessCount.Should().Be(1); // 不阻止保存
    }

    [Fact]
    public async Task UpdateInventoryBatchAsync_生产批次工单号都非空_定尺长度不在主号_禁止保存()
    {
        // 用户决策#1：内联编辑核查条件=生产批次+工单号都非空；长度不在主号定尺集 → 报错无法保存
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedProductionBatchAsync(ctx, "PB-010", "SO-X01-01", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.InspectionInbound,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            ProductionBatchNo = "PB-010",
            WorkOrderNo = "SO-X01-01",
            InboundDate = DateTime.Today
        });

        var act = () => svc.UpdateInventoryBatchAsync(batch.Id, new UpdateInventoryBatchRequest
        {
            MinLength = 7000m,
            MaxLength = 7000m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不在主号*");
        // 硬校验在 SaveChanges 前抛出 → 长度未落库
        var after = await ctx.InventoryBatches.AsNoTracking().SingleAsync();
        after.MinLength.Should().Be(6000m);
    }

    [Fact]
    public async Task UpdateInventoryBatchAsync_生产批次为空_定尺不核查并清空标识()
    {
        // 用户决策#1：生产批次空 → 符合工单长度=""
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedWorkOrderAsync(ctx, "SO-X01-01", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            WorkOrderNo = "SO-X01-01", // 仅工单号，无生产批次
            InboundDate = DateTime.Today
        });
        batch.CutLengthMatchType.Should().Be(CutLengthMatchType.FullMatch); // 新建软计算仍按工单号兜底

        var updated = await svc.UpdateInventoryBatchAsync(batch.Id, new UpdateInventoryBatchRequest
        {
            MinLength = 7000m,
            MaxLength = 7000m // 不匹配主号定尺集，但因生产批次空不核查、仅清空
        });

        updated.CutLengthMatchType.Should().BeNull(); // 内联编辑：生产批次空 → 清空
    }

    [Fact]
    public async Task UpdateInventoryBatchAsync_工单号为空_定尺不核查并清空标识()
    {
        // 用户决策#1：工单号空 → 符合工单长度=""
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedProductionBatchAsync(ctx, "PB-011", "SO-X01-01", "SO-X01", "X01");

        var maps = BuildLengthMaps(("SO-X01-01", 6000m));
        maps.ByMainKey["SO-X01|X01"] = new HashSet<decimal> { 6000m };
        var svc = CreateService(ctx, FixedLenMockWith(maps));

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.InspectionInbound,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            ProductionBatchNo = "PB-011", // 仅生产批次，无工单号
            InboundDate = DateTime.Today
        });
        batch.CutLengthMatchType.Should().Be(CutLengthMatchType.FullMatch); // 新建软计算按生产批号

        var updated = await svc.UpdateInventoryBatchAsync(batch.Id, new UpdateInventoryBatchRequest
        {
            MinLength = 7000m,
            MaxLength = 7000m // 不匹配主号定尺集，但因工单号空不核查、仅清空
        });

        updated.CutLengthMatchType.Should().BeNull(); // 内联编辑：工单号空 → 清空
    }

    // ========== 主号（ProductionMainNo）落库/回填/清空 ==========

    [Fact]
    public async Task InboundAsync_生产批号关联_主号落库等于批次主号()
    {
        // 第1种自动填充：前端 LookupProductionBatch 已把批次主号填入请求 → 入库落库
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedProductionBatchAsync(ctx, "PB-020", "SO-X01-01", "SO-X01", "X01");

        var result = await CreateService(ctx).InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.InspectionInbound,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            ProductionBatchNo = "PB-020", // 仅生产批次，无工单号
            SalesOrderNo = "SO-X01",
            ProductionMainNo = "X01", // 前端自动填充携带
            InboundDate = DateTime.Today
        });

        result.ProductionMainNo.Should().Be("X01");
        var persisted = await ctx.InventoryBatches.AsNoTracking().SingleAsync();
        persisted.ProductionMainNo.Should().Be("X01");
    }

    [Fact]
    public async Task InboundAsync_工单号关联_主号落库等于工单主号()
    {
        // 工单号非空时后端按 WorkOrder 权威覆盖（不依赖请求携带）
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedWorkOrderAsync(ctx, "SO-X01-01", "SO-X01", "X01");

        var result = await CreateService(ctx).InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            WorkOrderNo = "SO-X01-01", // 请求不携带主号
            InboundDate = DateTime.Today
        });

        result.ProductionMainNo.Should().Be("X01");
        var persisted = await ctx.InventoryBatches.AsNoTracking().SingleAsync();
        persisted.ProductionMainNo.Should().Be("X01");
    }

    [Fact]
    public async Task BatchInboundAsync_批量入库_主号按行或公共回退落库()
    {
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);

        var result = await CreateService(ctx).BatchInboundAsync(new BatchInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            ProductionMainNo = "X01", // 公共回退
            Rows = new List<InboundRow>
            {
                new() { InitialQuantity = 10, InitialWeight = 1000m, SalesOrderNo = "SO-X01", ProductionMainNo = "X02" }, // 行级优先
                new() { InitialQuantity = 10, InitialWeight = 1000m, SalesOrderNo = "SO-X01" } // 行级为空 → 公共回退
            }
        });

        result.SuccessCount.Should().Be(2);
        var persisted = await ctx.InventoryBatches.AsNoTracking()
            .OrderBy(b => b.ProductionMainNo).ToListAsync();
        persisted.Should().HaveCount(2);
        persisted.Select(b => b.ProductionMainNo).Should().Equal("X01", "X02");
    }

    [Fact]
    public async Task UpdateInventoryBatchAsync_解绑工单_清空主号()
    {
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedWorkOrderAsync(ctx, "SO-X01-01", "SO-X01", "X01");
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            WorkOrderNo = "SO-X01-01",
            InboundDate = DateTime.Today
        });
        batch.ProductionMainNo.Should().Be("X01");

        var updated = await svc.UpdateInventoryBatchAsync(batch.Id, new UpdateInventoryBatchRequest
        {
            IsLinkedToWorkOrder = false, // 前端解绑工单：级联清空订单关联
            WorkOrderNo = "",
            SalesOrderNo = "",
            ProductionMainNo = ""
        });

        updated.ProductionMainNo.Should().BeNull();
        var persisted = await ctx.InventoryBatches.AsNoTracking().SingleAsync();
        persisted.ProductionMainNo.Should().BeNull();
    }

    [Fact]
    public async Task UpdateInventoryBatchAsync_解绑工单_刷新旧工单执行状况()
    {
        var ctx = CreateDbContext();
        var wh = await SeedFgWarehouseAsync(ctx);
        await SeedWorkOrderAsync(ctx, "SO-X01-01", "SO-X01", "X01");
        var woExecMock = new Mock<IWorkOrderExecutionService>();
        var svc = CreateService(ctx, woExecMock: woExecMock);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            WorkOrderNo = "SO-X01-01",
            InboundDate = DateTime.Today
        });

        woExecMock.Invocations.Clear();

        await svc.UpdateInventoryBatchAsync(batch.Id, new UpdateInventoryBatchRequest
        {
            IsLinkedToWorkOrder = false, // 前端解绑工单：级联清空订单关联
            WorkOrderNo = "",
            SalesOrderNo = "",
            ProductionMainNo = ""
        });

        // 解绑后 WorkOrderNo 已清空，但旧工单的成品入库数据须一并重算（G17 移除已解绑入库）
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("SO-X01-01"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ValidateProductionBatchAsync_返回批次主号()
    {
        var ctx = CreateDbContext();
        await SeedProductionBatchAsync(ctx, "PB-021", "SO-X01-01", "SO-X01", "X01");
        var svc = CreateService(ctx);

        var result = await svc.ValidateProductionBatchAsync("PB-021");

        result.IsValid.Should().BeTrue();
        result.ProductionMainNo.Should().Be("X01");
        result.SalesOrderNo.Should().Be("SO-X01");
    }

    // ========== 出库 ==========

    [Fact]
    public async Task OutboundAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = 999,
            OutboundQuantity = 1,
            OutboundWeight = 100m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("批次不存在");
    }

    [Fact]
    public async Task OutboundAsync_数量不足_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 5,
            InitialWeight = 500m
        });

        var act = () => svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 10,
            OutboundWeight = 100m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*剩余支数不足*");
    }

    [Fact]
    public async Task OutboundAsync_重量不足_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 5,
            InitialWeight = 500m
        });

        var act = () => svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 1,
            OutboundWeight = 600m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*剩余重量不足*");
    }

    [Fact]
    public async Task OutboundAsync_成功出库_剩余量正确扣减()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            WorkOrderNo = "WO-001"
        });

        var record = await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 3,
            OutboundWeight = 300m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });

        record.Should().NotBeNull();
        record.OutboundQuantity.Should().Be(3);
        record.OutboundWeight.Should().Be(300m);
        // 未显式传出库工单号时，默认回退仓库批的工单号
        record.WorkOrderNo.Should().Be("WO-001");

        // 验证批次剩余量已更新
        var updated = await svc.GetByIdAsync(batch.Id);
        updated.RemainingQuantity.Should().Be(7);
        updated.RemainingWeight.Should().Be(700m);
    }

    // ========== 批量出库 ==========

    [Fact]
    public async Task BatchOutboundAsync_部分批次库存不足_事务全部回滚()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var b1 = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 2,
            InitialWeight = 200m
        });

        var b2 = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 5,
            InitialWeight = 500m
        });

        var act = () => svc.BatchOutboundAsync(new BatchOutboundRequest
        {
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户Y",
            OutboundDate = DateTime.Today,
            Items = new List<OutboundItemRequest>
            {
                new() { InventoryBatchId = b1.Id, OutboundQuantity = 1, OutboundWeight = 100m },
                new() { InventoryBatchId = b2.Id, OutboundQuantity = 10, OutboundWeight = 100m } // 不足
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*剩余支数不足*");

        // 验证事务回滚：第一笔出库也被回滚
        var updatedB1 = await svc.GetByIdAsync(b1.Id);
        updatedB1.RemainingQuantity.Should().Be(2);
    }

    [Fact]
    public async Task BatchOutboundAsync_成功出库_出库工单号默认回退批次工单号()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var b1 = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            WorkOrderNo = "WO-001"
        });

        var result = await svc.BatchOutboundAsync(new BatchOutboundRequest
        {
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户Y",
            OutboundDate = DateTime.Today,
            Items = new List<OutboundItemRequest>
            {
                new() { InventoryBatchId = b1.Id, OutboundQuantity = 3, OutboundWeight = 300m }
            }
        });

        result.SuccessCount.Should().Be(1);
        // 行/请求级未传出库工单号，默认回退仓库批的工单号
        result.Records.Should().ContainSingle().Which.WorkOrderNo.Should().Be("WO-001");

        var persisted = await ctx.OutboundRecords.FirstOrDefaultAsync(r => r.Id == result.Records[0].Id);
        persisted!.WorkOrderNo.Should().Be("WO-001");
    }

    [Fact]
    public async Task UpdateOutboundRecordAsync_更新出库工单号()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            WorkOrderNo = "WO-001"
        });

        var record = await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 3,
            OutboundWeight = 300m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });
        record.WorkOrderNo.Should().Be("WO-001");

        var updated = await svc.UpdateOutboundRecordAsync(record.Id, new UpdateOutboundRecordRequest
        {
            WorkOrderNo = "WO-999"
        });

        updated.WorkOrderNo.Should().Be("WO-999");

        var persisted = await ctx.OutboundRecords.FirstOrDefaultAsync(r => r.Id == record.Id);
        persisted!.WorkOrderNo.Should().Be("WO-999");
    }

    // ========== 批量入库 ==========

    [Fact]
    public async Task BatchInboundAsync_仓库不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.BatchInboundAsync(new BatchInboundRequest
        {
            WarehouseId = 999,
            MaterialType = MaterialType.OrderFinished,
            Rows = new List<InboundRow>
            {
                new() { InitialQuantity = 1, InitialWeight = 100m }
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("仓库不存在");
    }

    [Fact]
    public async Task BatchInboundAsync_成功批量入库()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.BatchInboundAsync(new BatchInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            Rows = new List<InboundRow>
            {
                new() { InitialQuantity = 5, InitialWeight = 500m },
                new() { InitialQuantity = 10, InitialWeight = 1000m }
            }
        });

        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(2);
        result.BatchNos.Should().HaveCount(2);
    }

    // ========== 查询 ==========

    [Fact]
    public async Task GetPagedAsync_关键字搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.Finished,
            PlantGrade = "Q235B",
            Specification = "159*6",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商B",
            InitialQuantity = 20,
            InitialWeight = 2000m
        });

        var result = await svc.GetPagedAsync(new InventoryQueryParams
        {
            Keyword = "订单成品",
            PageIndex = 0,
            PageSize = 10
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].MaterialType.Should().Be(MaterialType.OrderFinished);
    }

    [Fact]
    public async Task GetPagedAsync_OnlyWithStock_只返回有库存批次()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        var b2 = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.Finished,
            PlantGrade = "Q235B",
            Specification = "159*6",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商B",
            InitialQuantity = 5,
            InitialWeight = 500m
        });

        // 把 b2 出库到零
        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = b2.Id,
            OutboundQuantity = 5,
            OutboundWeight = 500m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });

        var result = await svc.GetPagedAsync(new InventoryQueryParams
        {
            OnlyWithStock = true,
            PageIndex = 0,
            PageSize = 10
        });

        result.Items.Should().HaveCount(1);
    }

    // ========== 更新 ==========

    [Fact]
    public async Task UpdateInventoryBatchAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateInventoryBatchAsync(999, new UpdateInventoryBatchRequest
        {
            BatchNo = "NEW-BATCH"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("入库批次不存在");
    }

    [Fact]
    public async Task UpdateInventoryBatchAsync_修改数量_剩余量同步更新()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        var updated = await svc.UpdateInventoryBatchAsync(batch.Id, new UpdateInventoryBatchRequest
        {
            InitialQuantity = 20,
            InitialWeight = 2000m
        });

        updated.InitialQuantity.Should().Be(20);
        updated.RemainingQuantity.Should().Be(20);
        updated.InitialWeight.Should().Be(2000m);
        updated.RemainingWeight.Should().Be(2000m);
    }

    // ========== 物理删除 ==========

    [Fact]
    public async Task HardDeleteInventoryBatchAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.HardDeleteInventoryBatchAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("入库批次不存在");
    }

    [Fact]
    public async Task HardDeleteInventoryBatchAsync_有出库记录_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        var outRecord = await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 3,
            OutboundWeight = 300m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });

        // 有出库记录时无法删除批次
        var act = () => svc.HardDeleteInventoryBatchAsync(batch.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*存在出库记录*");

        // 先删除出库记录，再删除批次
        await svc.HardDeleteOutboundRecordAsync(outRecord.Id);
        await svc.HardDeleteInventoryBatchAsync(batch.Id);

        // 验证已删除
        var getAct = () => svc.GetByIdAsync(batch.Id);
        await getAct.Should().ThrowAsync<BusinessException>().WithMessage("批次不存在");
    }

    // ========== 出库记录 ==========

    [Fact]
    public async Task GetOutboundRecordsAsync_按条件筛选()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 2,
            OutboundWeight = 200m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户A",
            OutboundDate = DateTime.Today
        });
        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 3,
            OutboundWeight = 300m,
            OutboundType = OutboundType.TransferOut,
            TargetCompany = "客户B",
            OutboundDate = DateTime.Today
        });

        var result = await svc.GetOutboundRecordsAsync(new OutboundQueryParams
        {
            OutboundType = "SalesOut",
            PageIndex = 0,
            PageSize = 10
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].OutboundType.Should().Be(OutboundType.SalesOut);
        result.Items[0].BatchNo.Should().Be(batch.BatchNo);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOutboundRecordsAsync_列表DTO_包含出库工单号()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 2,
            OutboundWeight = 200m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户A",
            OutboundDate = DateTime.Today,
            WorkOrderNo = "WO-TEST-001"
        });

        var result = await svc.GetOutboundRecordsAsync(new OutboundQueryParams
        {
            PageIndex = 0,
            PageSize = 10
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].WorkOrderNo.Should().Be("WO-TEST-001");
    }

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_关键词搜索区域_返回匹配()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        var batch = await ctx.InventoryBatches.OrderByDescending(b => b.Id).FirstAsync();
        batch.LocationArea = "A区-3排";
        await ctx.SaveChangesAsync();

        var result = await svc.GetPagedAsync(new InventoryQueryParams
        { PageIndex = 0, PageSize = 10, Keyword = "A区" });

        result.Items.Should().HaveCount(1);
        result.Items[0].LocationArea.Should().Be("A区-3排");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        var batch = await ctx.InventoryBatches.OrderByDescending(b => b.Id).FirstAsync();
        batch.Remark = "库存批次备注";
        await ctx.SaveChangesAsync();

        var result = await svc.GetPagedAsync(new InventoryQueryParams
        { PageIndex = 0, PageSize = 10, Keyword = "库存批次" });

        result.Items[0].Remark.Should().Be("库存批次备注");
    }

    [Fact]
    public async Task GetPagedAsync_按是否关联工单排序_成功()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });
        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.Finished,
            PlantGrade = "Q235B",
            Specification = "159*6",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商B",
            InitialQuantity = 20,
            InitialWeight = 2000m,
            SalesOrderNo = "SO-001"
        });

        var batches = await ctx.InventoryBatches.OrderBy(b => b.Id).ToListAsync();
        batches[0].IsLinkedToWorkOrder = false;
        batches[1].IsLinkedToWorkOrder = true;
        await ctx.SaveChangesAsync();

        var resultAsc = await svc.GetPagedAsync(new InventoryQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "islinkedtoworkorder", IsDescending = false });

        resultAsc.Items[0].IsLinkedToWorkOrder.Should().BeFalse();
        resultAsc.Items[1].IsLinkedToWorkOrder.Should().BeTrue();
    }

    // ========== 出库记录 B10 专项测试 ==========

    [Fact]
    public async Task GetOutboundRecordsAsync_按源单号排序_成功()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 1,
            OutboundWeight = 100m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });
        // Add a second with different order so we can test ordering
        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 2,
            OutboundWeight = 200m,
            OutboundType = OutboundType.TransferOut,
            TargetCompany = "客户Y",
            OutboundDate = DateTime.Today
        });

        // Update source order numbers for sort testing
        var records = await ctx.OutboundRecords.OrderBy(r => r.Id).ToListAsync();
        records[0].SourceOrderNo = "B-SO";
        records[1].SourceOrderNo = "A-SO";
        await ctx.SaveChangesAsync();

        var result = await svc.GetOutboundRecordsAsync(new OutboundQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "sourceorderno", IsDescending = false });

        result.Items[0].SourceOrderNo.Should().Be("A-SO");
        result.Items[1].SourceOrderNo.Should().Be("B-SO");
    }

    [Fact]
    public async Task GetOutboundRecordsAsync_按备注排序_成功()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 1,
            OutboundWeight = 100m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });
        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 2,
            OutboundWeight = 200m,
            OutboundType = OutboundType.TransferOut,
            TargetCompany = "客户Y",
            OutboundDate = DateTime.Today
        });

        var records = await ctx.OutboundRecords.OrderBy(r => r.Id).ToListAsync();
        records[0].Remark = "B备注";
        records[1].Remark = "A备注";
        await ctx.SaveChangesAsync();

        var result = await svc.GetOutboundRecordsAsync(new OutboundQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "remark", IsDescending = false });

        result.Items[0].Remark.Should().Be("A备注");
        result.Items[1].Remark.Should().Be("B备注");
    }

    [Fact]
    public async Task GetOutboundRecordsAsync_关键词搜索出库类型_返回匹配()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = InboundSource.Purchase,
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 1,
            OutboundWeight = 100m,
            OutboundType = OutboundType.SalesOut,
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });
        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 2,
            OutboundWeight = 200m,
            OutboundType = OutboundType.TransferOut,
            TargetCompany = "客户Y",
            OutboundDate = DateTime.Today
        });

        var result = await svc.GetOutboundRecordsAsync(new OutboundQueryParams
        { PageIndex = 0, PageSize = 20, Keyword = "TransferOut" });

        result.Items.Should().HaveCount(1);
        result.Items[0].OutboundType.Should().Be(OutboundType.TransferOut);
    }

    // ========== 库存筛选上下文 ==========

    [Fact]
    public async Task GetInventoryFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        // 直接插入 InventoryBatch
        ctx.InventoryBatches.AddRange(
            new InventoryBatch { BatchNo = "CK001", WarehouseId = wh.Id, MaterialType = MaterialType.OrderFinished.ToString(), PlantGrade = "Q345B", Specification = "219*8", InboundSource = InboundSource.Purchase.ToString(), SourceName = "供应商A", InboundDate = DateTime.Today, InitialQuantity = 10, InitialWeight = 1000m, RemainingQuantity = 10, RemainingWeight = 1000m, IsLinkedToWorkOrder = false },
            new InventoryBatch { BatchNo = "CK002", WarehouseId = wh.Id, MaterialType = MaterialType.Finished.ToString(), PlantGrade = "Q235B", Specification = "159*6", InboundSource = InboundSource.Purchase.ToString(), SourceName = "供应商B", InboundDate = DateTime.Today, InitialQuantity = 20, InitialWeight = 2000m, RemainingQuantity = 20, RemainingWeight = 2000m, IsLinkedToWorkOrder = true, ManufacturingStatus = "酸洗", HeatNo = "H001", LocationArea = "A区", LocationRack = "R01" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetInventoryFilterContextsAsync();

        result.Should().ContainKeys("BatchNo", "InboundDate", "MaterialType", "SourceName", "PlantGrade", "Specification", "IsLinkedToWorkOrder");
        result["BatchNo"].Should().BeEquivalentTo(new[] { "CK001", "CK002" }, options => options.WithStrictOrdering());
        result["MaterialType"].Should().BeEquivalentTo(new[] { "OrderFinished", "Finished" });
        result["IsLinkedToWorkOrder"].Should().BeEquivalentTo(new[] { "False", "True" });
        result["ManufacturingStatus"].Should().Contain("酸洗");
        result["HeatNo"].Should().Contain("H001");
        result["LocationArea"].Should().Contain("A区");
    }

    [Fact]
    public async Task GetInventoryFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetInventoryFilterContextsAsync();

        result.Should().NotBeNull();
        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty($"字段 {kvp.Key} 应返回空列表");
    }

    // ========== 出库筛选上下文 ==========

    [Fact]
    public async Task GetOutboundFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        // 需要 InventoryBatch 才能创建 OutboundRecord
        var batch = new InventoryBatch { BatchNo = "CK001", WarehouseId = wh.Id, MaterialType = "无缝管", PlantGrade = "Q345B", Specification = "219*8", InboundSource = InboundSource.Purchase.ToString(), SourceName = "供应商A", InboundDate = DateTime.Today, InitialQuantity = 10, InitialWeight = 1000m, RemainingQuantity = 10, RemainingWeight = 1000m };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();
        ctx.OutboundRecords.AddRange(
            new OutboundRecord { InventoryBatchId = batch.Id, BatchNo = batch.BatchNo, OutboundType = Core.Enums.OutboundType.SalesOut, SourceOrderNo = "SO001", TargetCompany = "客户A", OutboundQuantity = 2, OutboundWeight = 200m, OutboundDate = DateTime.Today, CreatedBy = "user1" },
            new OutboundRecord { InventoryBatchId = batch.Id, BatchNo = batch.BatchNo, OutboundType = Core.Enums.OutboundType.TransferOut, SourceOrderNo = null, TargetCompany = null, OutboundQuantity = 3, OutboundWeight = 300m, OutboundDate = DateTime.Today, CreatedBy = "user2", Remark = "调拨" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetOutboundFilterContextsAsync();

        result.Should().ContainKeys("BatchNo", "OutboundType", "SourceOrderNo", "TargetCompany", "Remark", "CreatedBy");
        result["BatchNo"].Should().Contain("CK001");
        result["OutboundType"].Should().Contain("SalesOut").And.Contain("TransferOut");
        result["SourceOrderNo"].Should().HaveCount(1).And.Contain("SO001");
        result["TargetCompany"].Should().HaveCount(1).And.Contain("客户A");
        result["Remark"].Should().HaveCount(1).And.Contain("调拨");
        // AppDbContext.SaveChangesAsync 将 CreatedBy 覆盖为 "system"（无 HttpContext 时）
        result["CreatedBy"].Should().AllBe("system");
    }

    [Fact]
    public async Task GetOutboundFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetOutboundFilterContextsAsync();

        result.Should().NotBeNull();
        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty($"字段 {kvp.Key} 应返回空列表");
    }

    [Fact]
    public async Task HardDeleteInventoryBatch_非完成批次删除最后入库_重算批次跟踪()
    {
        var ctx = CreateDbContext();
        var batch = await SeedProductionBatchAsync(ctx, "PB-DEL", "WO-DEL", "SO-DEL", "M-DEL");
        batch.Status = BatchStatus.None; // 非完成：修复前仅 Completed 才触发重算，导致"入库"当前工段残留
        await ctx.SaveChangesAsync();
        var wh = await SeedWarehouseAsync(ctx);
        var prMock = new Mock<IProductionRecordService>();
        var svc = CreateService(ctx, prMock: prMock);

        var inbound = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            ProductionBatchNo = batch.BatchNo,
            MaterialType = MaterialType.OrderFinished,
            PlantGrade = "304",
            Specification = "219*8",
            InboundSource = InboundSource.ProductionInbound,
            SourceName = "内部",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        await svc.HardDeleteInventoryBatchAsync(inbound.Id);

        prMock.Verify(x => x.RefreshBatchTrackingFieldsAsync(batch.Id), Times.Once);
    }

    // ========== 出库后工单执行状况增量刷新目标 ==========

    private OutboundWriteService CreateOutboundWriteService(AppDbContext ctx, out Mock<IWorkOrderExecutionService> woExecMock)
    {
        woExecMock = new Mock<IWorkOrderExecutionService>();
        var logger = new Mock<ILogger<OutboundWriteService>>();
        return new OutboundWriteService(ctx, woExecMock.Object, logger.Object);
    }

    private async Task<InventoryBatch> SeedInventoryBatchAsync(AppDbContext ctx, string workOrderNo)
    {
        var wh = await SeedWarehouseAsync(ctx);
        var batch = new InventoryBatch
        {
            BatchNo = "CK001", WarehouseId = wh.Id, MaterialType = "OrderFinished", PlantGrade = "Q345B",
            Specification = "219*8", InboundSource = InboundSource.Purchase.ToString(), SourceName = "供应商A",
            InboundDate = DateTime.Today, InitialQuantity = 10, InitialWeight = 1000m,
            RemainingQuantity = 10, RemainingWeight = 1000m, WorkOrderNo = workOrderNo, RowVersion = new byte[8]
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    [Fact]
    public async Task OutboundAsync_出库工单号不同于批次工单号_增量刷新目标为出库工单号()
    {
        var ctx = CreateDbContext();
        var batch = await SeedInventoryBatchAsync(ctx, "WO-001"); // 批次原工单号 WO-001
        var svc = CreateOutboundWriteService(ctx, out var woExecMock);

        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 3,
            OutboundWeight = 300m,
            OutboundType = OutboundType.ProductionPick,
            WorkOrderNo = "WO-999", // 出库工单号显式填为计划工单号（≠批次原工单号）
            OutboundDate = DateTime.Today
        });

        // 增量刷新必须刷出库记录实际工单号 WO-999，而非批次原工单号 WO-001
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("WO-999"))), Times.AtLeastOnce);
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("WO-001"))), Times.Never);
    }

    [Fact]
    public async Task BatchOutboundAsync_行级出库工单号_增量刷新目标为出库工单号()
    {
        var ctx = CreateDbContext();
        var batch = await SeedInventoryBatchAsync(ctx, "WO-001");
        var svc = CreateOutboundWriteService(ctx, out var woExecMock);

        var result = await svc.BatchOutboundAsync(new BatchOutboundRequest
        {
            OutboundType = OutboundType.ProductionPick,
            OutboundDate = DateTime.Today,
            Items = new List<OutboundItemRequest>
            {
                new() { InventoryBatchId = batch.Id, OutboundQuantity = 3, OutboundWeight = 300m, WorkOrderNo = "WO-999" }
            }
        });

        result.SuccessCount.Should().Be(1);
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("WO-999"))), Times.AtLeastOnce);
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("WO-001"))), Times.Never);
    }

    [Fact]
    public async Task UpdateOutboundRecordAsync_修改出库工单号_增量刷新新旧工单()
    {
        var ctx = CreateDbContext();
        var batch = await SeedInventoryBatchAsync(ctx, "WO-001");
        var svc = CreateOutboundWriteService(ctx, out var woExecMock);

        var record = await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 3,
            OutboundWeight = 300m,
            OutboundType = OutboundType.ProductionPick,
            WorkOrderNo = "WO-001",
            OutboundDate = DateTime.Today
        });

        woExecMock.Invocations.Clear();

        await svc.UpdateOutboundRecordAsync(record.Id, new UpdateOutboundRecordRequest
        {
            WorkOrderNo = "WO-999"
        });

        // 新工单号 WO-999 计入出库量 → 必须刷新；旧工单号 WO-001 出库量消失 → 也必须刷新
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("WO-999"))), Times.AtLeastOnce);
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("WO-001"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task HardDeleteOutboundRecordAsync_删除出库记录_增量刷新目标为出库记录工单号()
    {
        var ctx = CreateDbContext();
        var batch = await SeedInventoryBatchAsync(ctx, "WO-001");
        var svc = CreateOutboundWriteService(ctx, out var woExecMock);

        var record = await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 3,
            OutboundWeight = 300m,
            OutboundType = OutboundType.ProductionPick,
            WorkOrderNo = "WO-999", // 出库记录实际工单号≠批次原工单号
            OutboundDate = DateTime.Today
        });

        woExecMock.Invocations.Clear();

        await svc.HardDeleteOutboundRecordAsync(record.Id);

        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("WO-999"))), Times.AtLeastOnce);
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("WO-001"))), Times.Never);
    }
}
