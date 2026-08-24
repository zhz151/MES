using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Constants;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Infrastructure;
using MES.Services.WorkOrder;
using MES.Services.Order;
using MES.Tests.Tests;
using Moq;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.StandardRegister;

namespace MES.Tests.Services;

/// <summary>
/// 用料计划服务测试：4种类型CRUD、测算、满足率计算、可用库存查询
/// </summary>
public class MaterialPlanServiceTests : TestBase
{
    private MaterialPlanService CreateService(AppDbContext ctx, Dictionary<string, double>? deliveryExtraDays = null,
        Mock<IWorkOrderExecutionService>? workOrderExecMock = null)
    {
        var loggerMock = new Mock<ILogger<MaterialPlanService>>();
        var mockDaySvc = new Mock<IStandardWorkDayService>();
        mockDaySvc.Setup(s => s.GetStandardDaysMapAsync(It.IsAny<string?>()))
            .ReturnsAsync(() => SectionKeys.All.ToDictionary(s => s, s => 3.0));
        var mockDsSvc = new Mock<IStandardWorkDayDeliveryStateService>();
        mockDsSvc.Setup(s => s.GetDeliveryStateExtraDaysMapAsync())
            .ReturnsAsync(() => deliveryExtraDays ?? new Dictionary<string, double>());
        var mockConfigSvc = new Mock<IConfigParameterService>();
        mockConfigSvc.Setup(s => s.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(() => new Dictionary<string, decimal>());
        var mockRefreshSvc = new Mock<IWorkOrderListSummaryRefreshService>();
        workOrderExecMock ??= new Mock<IWorkOrderExecutionService>();
        return new MaterialPlanService(ctx, loggerMock.Object,
            mockDaySvc.Object, mockDsSvc.Object, mockConfigSvc.Object, mockRefreshSvc.Object, workOrderExecMock.Object);
    }

    /// <summary>
    /// 种子一个已确认的订单并生成工单，返回工单ID
    /// </summary>
    private async Task<(int WorkOrderId, string WorkOrderNo)> SeedWorkOrderAsync(AppDbContext ctx,
        LengthStatus lengthStatus = LengthStatus.Fixed,
        decimal od = 219m, decimal wt = 8m)
    {
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        var notifMock = new Mock<INotificationService>();
        var orderConfigMock = new Mock<IConfigParameterService>();
        orderConfigMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var orderSvc = new OrderService(ctx, new Mock<ILogger<OrderService>>().Object, notifMock.Object, orderConfigMock.Object, new Mock<IOperationLogService>().Object, new MemoryCache(new MemoryCacheOptions()));

        var order = await orderSvc.CreateAsync(new CreateSalesOrderRequest
        {
            OrderNumber = $"MP-TEST-{Guid.NewGuid():N}"[..15],
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    StandardNo = sr.StandardNo,
                    StandardGrade = gm.StandardGrade,
                    PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
                    OuterDiameter = od,
                    WallThickness = wt,
                    OuterDiameterNegative = 0.5m,
                    OuterDiameterPositive = 0.5m,
                    WallThicknessNegative = 0.5m,
                    WallThicknessPositive = 0.5m,
                    LengthStatus = lengthStatus,
                    MinLength = 6000m,
                    MaxLength = 6000m,
                    Quantity = 10,
                    ContractWeight = 2500m,
                    DeliveryDate = DateTime.Today.AddMonths(1),
                    SettlementMethod = SettlementMethod.Theoretical,
                    DeliveryState = DeliveryState.SolutionAnnealedAndPickled
                }
            }
        });

        await orderSvc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Confirmed,
            RowVersion = new byte[8]
        });

        // 生成工单
        var items = await ctx.OrderItems
            .Where(oi => oi.SalesOrderId == order.Id)
            .ToListAsync();
        var itemIds = items.Select(i => i.Sequence).ToList();

        var woLoggerMock = new Mock<ILogger<WorkOrderService>>();
        var configMock = new Mock<IConfigParameterService>();
        var woSvc = new WorkOrderService(ctx, woLoggerMock.Object, configMock.Object, new Mock<IOperationLogService>().Object, new MemoryCache(new MemoryCacheOptions()));
        var result = await woSvc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = order.OrderNumber,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new()
                {
                    ProductionMainNo = "X01",
                    ProductionSubNo = lengthStatus == LengthStatus.NonFixed ? "F0" : "01",
                    OrderItemIds = itemIds
                }
            }
        });

        return (result[0].Id, result[0].WorkOrderNo);
    }

    /// <summary>
    /// 种子一个库存批次
    /// </summary>
    private async Task<InventoryBatch> SeedInventoryBatchAsync(AppDbContext ctx,
        string plantGrade = "Q345B",
        string specification = "219*8",
        decimal od = 219m, decimal wt = 8m,
        int quantity = 100, decimal weight = 10000m,
        decimal unitWeight = 250m)
    {
        var batch = new InventoryBatch
        {
            BatchNo = $"BATCH-{Guid.NewGuid():N}"[..20],
            WarehouseId = 1,
            MaterialType = "Finished",
            PlantGrade = plantGrade,
            Specification = specification,
            InboundSource = "Purchase",
            SourceName = "测试供应商",
            InboundDate = DateTime.Today,
            LengthStatus = "Fixed",
            MinLength = 6000m,
            MaxLength = 6000m,
            InitialQuantity = quantity,
            InitialWeight = weight,
            UnitWeight = unitWeight,
            RemainingQuantity = quantity,
            RemainingWeight = weight,
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    /// <summary>
    /// 种子一个生产批次（第6/7类在产改制/在产主工单计划测试用）
    /// </summary>
    private async Task<ProductionBatch> SeedProductionBatchAsync(AppDbContext ctx,
        string batchNo,
        string workOrderNo = "非工单",
        BatchStatus status = BatchStatus.InProgress,
        string plantGrade = "Q345B",
        string specification = "219*8",
        string manufacturingItem = "OrderFinished",
        string lengthStatus = "Fixed",
        string deliveryState = "SolutionAnnealedAndPickled",
        string technicalRequirements = "Normal",
        int? currentValidQty = 10,
        int? currentValidWeight = 3000,
        decimal? inputWeight = null,
        string? sourceProductionNo = null,
        int productionRatio = 1)
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            MaterialName = "不锈钢管",
            PlantGrade = plantGrade,
            Specification = specification,
            Status = status,
            ProductionType = "Internal",
            ManufacturingItem = manufacturingItem,
            WorkOrderNo = workOrderNo,
            SalesOrderNo = "SO-001",
            ProductionMainNo = "X01",
            OrderItemIds = "1",
            Salesman = "张三",
            SettlementMethod = "Weighing",
            StandardCode = "GB/T 14976",
            DeliveryState = deliveryState,
            LengthStatus = lengthStatus,
            TechnicalRequirements = technicalRequirements,
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.3m,
            TotalQuantity = 10,
            TotalMeters = 1000m,
            TotalWeight = currentValidWeight ?? 0m,
            TotalItemCount = 1,
            CurrentValidQty = currentValidQty,
            CurrentValidWeight = currentValidWeight,
            ProductionRatio = productionRatio,
            InputWeight = inputWeight,
            SourceProductionNo = sourceProductionNo
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    /// <summary>
    /// 为批次添加一条生产记录（模拟批次已实际投产 → "已投料"判据触发）
    /// </summary>
    private async Task<ProductionRecord> SeedProductionRecordAsync(AppDbContext ctx, int productionBatchId)
    {
        var record = new ProductionRecord
        {
            ProductionBatchId = productionBatchId,
            ProcessGroupId = 0,
            ProcessName = "荒管处理",
            SectionName = "去油",
            ExecDate = DateTime.Today,
            Quantity = 1,
            Weight = 100m
        };
        ctx.ProductionRecords.Add(record);
        await ctx.SaveChangesAsync();
        return record;
    }

    /// <summary>
    /// 种子一个主工单（第7类在产主工单计划测试用）
    /// </summary>
    private async Task<MES.Data.Entities.WorkOrder.WorkOrder> SeedMainWorkOrderAsync(AppDbContext ctx, string workOrderNo)
    {
        var wo = new MES.Data.Entities.WorkOrder.WorkOrder
        {
            WorkOrderNo = workOrderNo,
            SalesOrderNo = "SO-001",
            ProductionMainNo = "X01",
            ProductionSubNo = "01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "张三",
            DeliveryDate = DateTime.Today.AddMonths(1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Weighing,
            StandardCode = "GB/T 14976",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "Q345B",
            Specification = "219*8",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.3m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            TotalQuantity = 10,
            TotalMeters = 1000m,
            TotalWeight = 1000m,
            TotalItemCount = 1
        };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();
        return wo;
    }

    /// <summary>
    /// 种子一个工单执行状况摘要（第7类可分配上限测试用）
    /// </summary>
    private async Task SeedExecutionSummaryAsync(AppDbContext ctx, string workOrderNo, string salesOrderNo, string mainNo,
        decimal totalWeight, decimal flowRatio)
    {
        var e = new WorkOrderExecutionSummary
        {
            WorkOrderId = Math.Abs(workOrderNo.GetHashCode()),
            WorkOrderNo = workOrderNo,
            Salesman = "测试",
            CustomerName = "测试客户",
            SettlementMethod = "Weighing",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = mainNo,
            MaterialName = "无缝管",
            DeliveryState = "SolutionAnnealedAndPickled",
            PlantGrade = "Q345B",
            Specification = "219*8",
            LengthStatus = "Fixed",
            TotalQuantity = 10,
            TotalWeight = totalWeight,
            MainNoFlowOutputRatio = flowRatio
        };
        ctx.WorkOrderExecutionSummaries.Add(e);
        await ctx.SaveChangesAsync();
    }

    // ========== 原料采购计划 CRUD ==========

    [Fact]
    public async Task GetSemiPlansAsync_无计划_返回空列表()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var plans = await svc.GetSemiPlansAsync(woId);

        plans.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSemiPlanByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetSemiPlanByIdAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    private static List<SavePlanProcessGroupItem> GetTestProcessGroups()
    {
        return new List<SavePlanProcessGroupItem>
        {
            new()
            {
                ProcessName = "荒管处理",
                ColdRollDraw = 1,
                Inspection = 2,
                Warehouse = 3
            }
        };
    }

    [Fact]
    public async Task CreateSemiPlanAsync_定尺_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var result = await svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = MaterialType.RoughTube,
            RawMaterialSpec = "245*10",
            RequiredPieces = 10,
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1),
            ProcessGroups = GetTestProcessGroups()
        });

        result.Should().NotBeNull();
        result.WorkOrderId.Should().Be(woId);
        result.RawMaterialSpec.Should().Be("245*10");
        result.Density.Should().Be(7.85m);
        result.UnitWeight.Should().BeGreaterThan(0);
        result.RequiredPieces.Should().Be(10);

        // 验证数据库中有记录
        var plans = await ctx.PurchaseSemiPlans.Where(p => p.WorkOrderId == woId).ToListAsync();
        plans.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateSemiPlanAsync_工单不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = 999,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = MaterialType.RoughTube,
            RawMaterialSpec = "245*10",
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task CreateSemiPlanAsync_非定尺无RequiredPieces_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.NonFixed);
        var svc = CreateService(ctx);

        var act = () => svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = MaterialType.RoughTube,
            RawMaterialSpec = "245*10",
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*非定尺*需求支数*");
    }

    [Fact]
    public async Task DeleteSemiPlanAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var created = await svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = MaterialType.RoughTube,
            RawMaterialSpec = "245*10",
            RequiredPieces = 10,
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1),
            ProcessGroups = GetTestProcessGroups()
        });

        await svc.DeleteSemiPlanAsync(created.Id);

        var act = () => svc.GetSemiPlanByIdAsync(created.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task DeleteSemiPlanAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteSemiPlanAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== 成品采购计划 CRUD ==========

    [Fact]
    public async Task GetFinishedPlansAsync_无计划_返回空列表()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var plans = await svc.GetFinishedPlansAsync(woId);

        plans.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFinishedPlanAsync_定尺_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var result = await svc.CreateFinishedPlanAsync(new CreatePurchaseFinishedPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            ProductType = FinishedProductType.Critical,
            RequiredPiece = 10,
            RequiredWeight = 2500m,
            RequiredDate = DateTime.Today.AddMonths(1),
            PlantGrade = "Q345B",
            Specification = "89*10",
            OuterDiameterNegative = 0.3m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled
        });

        result.Should().NotBeNull();
        result.WorkOrderId.Should().Be(woId);
        result.RequiredPiece.Should().Be(10);

        var plans = await ctx.PurchaseFinishedPlans.Where(p => p.WorkOrderId == woId).ToListAsync();
        plans.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateFinishedPlanAsync_定尺无支数_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var act = () => svc.CreateFinishedPlanAsync(new CreatePurchaseFinishedPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            ProductType = FinishedProductType.Critical,
            RequiredWeight = 2500m,
            PlantGrade = "Q345B",
            Specification = "89*10",
            OuterDiameterNegative = 0.3m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*采购支数*");
    }

    [Fact]
    public async Task DeleteFinishedPlanAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.NonFixed);
        var svc = CreateService(ctx);

        var created = await svc.CreateFinishedPlanAsync(new CreatePurchaseFinishedPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            ProductType = FinishedProductType.Order,
            RequiredWeight = 2500m,
            PlantGrade = "Q345B",
            Specification = "89*10",
            OuterDiameterNegative = 0.3m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled
        });

        await svc.DeleteFinishedPlanAsync(created.Id);

        var act = () => svc.GetFinishedPlanByIdAsync(created.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== 库存使用计划 CRUD ==========

    [Fact]
    public async Task CreateInventoryPlanAsync_全部使用模式_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().Be(batch.BatchNo);
        result.UsedQuantity.Should().Be(batch.RemainingQuantity);
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_部分使用模式_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 10,
            UsedWeight = 1000m
        });

        result.Should().NotBeNull();
        result.UsedQuantity.Should().Be(10);
        result.UsedWeight.Should().Be(1000m);
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_库料改制无工序组_抛出()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 库料改制（ReworkType 非空）无工序组不可提交
        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight,
            ReworkType = ReworkType.EmptyDrawing
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*库料改制必须填写工序组*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_库料改制内算StandardCycle()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight,
            ReworkType = ReworkType.EmptyDrawing,
            // 工序组随创建请求内算工量（3 工段 × mock 每工段 3 天 = 9）
            ProcessGroups = GetTestProcessGroups()
        });

        result.StandardCycle.Should().Be(9);
    }

    [Fact]
    public async Task UpdateInventoryPlanAsync_库料改制无工序组_抛出()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        var created = await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight,
            ReworkType = ReworkType.EmptyDrawing,
            ProcessGroups = GetTestProcessGroups()
        });

        // 库料改制编辑时无工序组不可提交
        var act = () => svc.UpdateInventoryPlanAsync(created.Id, new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight,
            ReworkType = ReworkType.EmptyDrawing
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*库料改制必须填写工序组*");
    }

    [Fact]
    public async Task DeleteInventoryPlanAsync_已出库_仍可删除()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        var created = await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        // 模拟批次已生产领用出库（放宽：已出库也允许删除）
        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            OutboundType = OutboundType.ProductionPick,
            OutboundQuantity = 10,
            OutboundWeight = 1000m,
            OutboundDate = DateTime.Today,
            CreatedBy = "user1"
        });
        await ctx.SaveChangesAsync();

        await svc.DeleteInventoryPlanAsync(created.Id);

        // 计划已删除，出库记录独立保留（仅释放预留，不删出库历史）
        ctx.InventoryPlans.Should().NotContain(p => p.Id == created.Id);
        ctx.OutboundRecords.Should().Contain(o => o.BatchNo == batch.BatchNo);
    }

    [Fact]
    public async Task GetPendingPlanBatchesByWarehouseAsync_待出库_返回支数重量()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 10,
            UsedWeight = 1000m
        });

        var pending = await svc.GetPendingPlanBatchesByWarehouseAsync(batch.WarehouseId);

        var item = pending.Should().ContainSingle().Subject;
        item.PlanType.Should().Be("库存使用");
        item.RequiredQuantity.Should().Be(10);
        item.RequiredWeight.Should().Be(1000m);
    }

    [Fact]
    public async Task GetPendingPlanBatchesByWarehouseAsync_同批次两工单_仅出库工单号匹配的计划完成()
    {
        var ctx = CreateDbContext();
        var (woId1, woNo1) = await SeedWorkOrderAsync(ctx);
        var (woId2, woNo2) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 同一仓库批被工单1、工单2 各引用一个部分领用计划（余料共享）
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId1,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 10,
            UsedWeight = 1000m
        });
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId2,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 10,
            UsedWeight = 1000m
        });

        // 工单1 生产领用出库（出库工单号=工单1号）
        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            OutboundType = OutboundType.ProductionPick,
            WorkOrderNo = woNo1,
            OutboundQuantity = 10,
            OutboundWeight = 1000m,
            OutboundDate = DateTime.Today,
            CreatedBy = "user1"
        });
        await ctx.SaveChangesAsync();

        // 通知应只剩工单2 的计划（工单1 已按出库工单号匹配完成）
        var pending = await svc.GetPendingPlanBatchesByWarehouseAsync(batch.WarehouseId);

        var item = pending.Should().ContainSingle().Subject;
        item.WorkOrderNo.Should().Be(woNo2);
        item.RequiredQuantity.Should().Be(10);
        item.RequiredWeight.Should().Be(1000m);
    }

    [Fact]
    public async Task GetPendingPlanBatchesByWarehouseAsync_出库工单号为空_计划不视为完成()
    {
        var ctx = CreateDbContext();
        var (woId, woNo) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 10,
            UsedWeight = 1000m
        });

        // 生产领用出库但出库工单号为空 → 无法匹配该计划，通知保留
        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            OutboundType = OutboundType.ProductionPick,
            WorkOrderNo = null,
            OutboundQuantity = 10,
            OutboundWeight = 1000m,
            OutboundDate = DateTime.Today,
            CreatedBy = "user1"
        });
        await ctx.SaveChangesAsync();

        var pending = await svc.GetPendingPlanBatchesByWarehouseAsync(batch.WarehouseId);

        var item = pending.Should().ContainSingle().Subject;
        item.WorkOrderNo.Should().Be(woNo);
        item.RequiredQuantity.Should().Be(10);
    }

    [Fact]
    public async Task GetInventoryPlansAsync_IsOutbound_按出库工单号匹配计划工单()
    {
        var ctx = CreateDbContext();
        var (woId1, woNo1) = await SeedWorkOrderAsync(ctx);
        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 同一仓库批被两个工单各引用一个库存使用计划
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId1,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 10,
            UsedWeight = 1000m
        });
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId2,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 10,
            UsedWeight = 1000m
        });

        // 工单1 生产领用出库（出库工单号=工单1号）
        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            OutboundType = OutboundType.ProductionPick,
            WorkOrderNo = woNo1,
            OutboundQuantity = 10,
            OutboundWeight = 1000m,
            OutboundDate = DateTime.Today,
            CreatedBy = "user1"
        });
        await ctx.SaveChangesAsync();

        var plans1 = await svc.GetInventoryPlansAsync(woId1);
        plans1.Should().ContainSingle().Which.IsOutbound.Should().BeTrue();

        var plans2 = await svc.GetInventoryPlansAsync(woId2);
        plans2.Should().ContainSingle().Which.IsOutbound.Should().BeFalse();
    }

    [Fact]
    public async Task GetPendingInProcessReworkPlansAsync_待处理_返回支数重量()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        var batch = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15]);
        var svc = CreateService(ctx);

        await svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 3,
            UsedWeight = 1000m,
            ProcessGroups = GetTestProcessGroups()
        });

        var pending = await svc.GetPendingInProcessReworkPlansAsync();

        var item = pending.Should().ContainSingle().Subject;
        item.RequiredQuantity.Should().Be(3);
        item.RequiredWeight.Should().Be(1000m);
    }

    [Fact]
    public async Task GetPendingInMainWorkOrderPlansAsync_待处理_返回支数重量()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-1");
        await SeedMainWorkOrderAsync(ctx, "IMW-MAIN-1");
        await SeedExecutionSummaryAsync(ctx, "IMW-MAIN-1", "SO-001", "X01", 0m, 0m);
        var batch = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15],
            workOrderNo: "IMW-MAIN-1", currentValidWeight: 3000);
        var svc = CreateService(ctx);

        await svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 5,
            AllocatedWeight = 2000m,
            ProductionRatio = 1
        });

        var pending = await svc.GetPendingInMainWorkOrderPlansAsync();

        var item = pending.Should().ContainSingle().Subject;
        item.PlanType.Should().Be("在产工单分配");
        // 通知显示分工单号（被覆盖的工单），而非源批次主工单号
        item.WorkOrderNo.Should().Be("IMW-SUB-1");
        item.BatchNo.Should().Be(batch.BatchNo);
        item.RequiredQuantity.Should().Be(5);
        item.RequiredWeight.Should().Be(2000m);
    }

    [Fact]
    public async Task CreateInProcessReworkPlanAsync_无工序组_抛出()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        var batch = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15]);
        var svc = CreateService(ctx);

        // 在产改制无工序组不可提交
        var act = () => svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 3,
            UsedWeight = 1000m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*在产改制必须填写工序组*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_批次已被引用_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 第一次创建成功
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        // 第二次引用同一批次应失败
        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId2,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不可全部使用*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = "NON_EXISTENT",
            UsageMode = "All"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_部分模式用量超库存_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx, quantity: 10, weight: 1000m);
        var svc = CreateService(ctx);

        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 20, // 超过库存10
            UsedWeight = 2000m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*超过剩余可用支数*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_其他工单部分使用预留_禁止全部使用()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx, quantity: 100, weight: 10000m);
        var svc = CreateService(ctx);

        // 工单1 部分使用预留
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 30,
            UsedWeight = 3000m
        });

        // 工单2 全部使用 → 禁止（部分预留存在）
        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId2,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不可全部使用*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_同工单先部分使用预留_再全部使用_禁止()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx, quantity: 100, weight: 10000m);
        var svc = CreateService(ctx);

        // 同一工单先部分使用预留 30 支
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 30,
            UsedWeight = 3000m
        });

        // 同一工单再全部使用 → 应禁止（预留已存在，全部=剩余100，合计将超量）
        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不可全部使用*");
    }

    [Fact]
    public async Task UpdateInventoryPlanAsync_同工单部分预留_自身改为全部使用_禁止()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx, quantity: 100, weight: 10000m);
        var svc = CreateService(ctx);

        // 工单1 部分使用预留 30 支
        var plan1 = await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 30,
            UsedWeight = 3000m
        });

        // 工单1 再建一条部分使用 20 支
        var plan2 = await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 20,
            UsedWeight = 2000m
        });

        // 工单1 将 plan2 改为全部使用 → 应禁止（plan1 预留仍存在）
        var act = () => svc.UpdateInventoryPlanAsync(plan2.Id, new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不可全部使用*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_同工单全部使用_禁止部分使用()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx, quantity: 100, weight: 10000m);
        var svc = CreateService(ctx);

        // 同一工单先全部使用（整批锁定）
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        // 同一工单再部分使用 → 应禁止（整批已锁定）
        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 10,
            UsedWeight = 1000m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不可部分使用*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_其他工单全部使用_禁止部分使用()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx, quantity: 100, weight: 10000m);
        var svc = CreateService(ctx);

        // 工单1 全部使用（整批锁定）
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        // 工单2 部分使用 → 禁止（整批锁定）
        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId2,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 10,
            UsedWeight = 1000m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不可部分使用*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_部分使用累计预留超量_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx, quantity: 10, weight: 1000m);
        var svc = CreateService(ctx);

        // 工单1 预留 6 支
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 6,
            UsedWeight = 600m
        });

        // 工单2 再预留 5 支 → 合计 11 > 10，应失败
        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId2,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 5,
            UsedWeight = 500m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*超过剩余可用支数*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_部分使用累计预留未超量_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx, quantity: 10, weight: 1000m);
        var svc = CreateService(ctx);

        // 工单1 预留 6 支
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 6,
            UsedWeight = 600m
        });

        // 工单2 再预留 4 支 → 合计 10 ≤ 10，应成功
        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var result = await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId2,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 4,
            UsedWeight = 400m
        });

        result.Should().NotBeNull();
        result.UsedQuantity.Should().Be(4);
    }

    [Fact]
    public async Task CreateInventoryPlanBatchAsync_部分使用累计预留超量_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx, quantity: 100, weight: 10000m);
        var svc = CreateService(ctx);

        // 工单1 预留 70 支
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 70,
            UsedWeight = 7000m
        });

        // 工单2 批量两条 20 + 20 → 合计 110 > 100，应失败
        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var act = () => svc.CreateInventoryPlanBatchAsync(new List<CreateInventoryPlanRequest>
        {
            new()
            {
                WorkOrderId = woId2,
                PlanDate = DateTime.Today,
                InventoryBatchNo = batch.BatchNo,
                UsageMode = "Partial",
                UsedQuantity = 20,
                UsedWeight = 2000m
            },
            new()
            {
                WorkOrderId = woId2,
                PlanDate = DateTime.Today,
                InventoryBatchNo = batch.BatchNo,
                UsageMode = "Partial",
                UsedQuantity = 20,
                UsedWeight = 2000m
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*超过剩余可用支数*");
    }

    [Fact]
    public async Task UpdateInventoryPlanAsync_其他工单部分预留_改为全部使用_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx, quantity: 100, weight: 10000m);
        var svc = CreateService(ctx);

        // 工单1 部分使用预留
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 30,
            UsedWeight = 3000m
        });

        // 工单2 部分使用
        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var plan2 = await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId2,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 20,
            UsedWeight = 2000m
        });

        // 工单2 将自身计划改为全部使用 → 禁止（工单1 部分预留存在）
        var act = () => svc.UpdateInventoryPlanAsync(plan2.Id, new CreateInventoryPlanRequest
        {
            WorkOrderId = woId2,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不可全部使用*");
    }

    // ========== 用料测算 ==========

    [Fact]
    public async Task CalculateAsync_定尺_完整计算()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        var svc = CreateService(ctx);

        var result = await svc.CalculateAsync(new MaterialCalculateRequest
        {
            WorkOrderId = woId,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m
        });

        result.Density.Should().Be(7.85m);
        result.UnitWeightPerMeter.Should().BeGreaterThan(0);
        result.UnitWeight.Should().NotBeNull().And.BeGreaterThan(0);
        result.RawUnitWeight.Should().NotBeNull().And.BeGreaterThan(0);
        result.RequiredPieces.Should().NotBeNull().And.BeGreaterThan(0);
        result.RequiredWeight.Should().NotBeNull().And.BeGreaterThan(0);
    }

    [Fact]
    public async Task CalculateAsync_非定尺_不计算单重()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.NonFixed);
        var svc = CreateService(ctx);

        var result = await svc.CalculateAsync(new MaterialCalculateRequest
        {
            WorkOrderId = woId,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m
        });

        result.Density.Should().Be(7.85m);
        result.UnitWeightPerMeter.Should().BeGreaterThan(0);
        result.UnitWeight.Should().BeNull();
        result.RawUnitWeight.Should().BeNull();
        result.RequiredPieces.Should().BeNull();
        result.RequiredWeight.Should().BeNull();
    }

    [Fact]
    public async Task CalculateAsync_工单不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CalculateAsync(new MaterialCalculateRequest
        {
            WorkOrderId = 999,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task CalculateAsync_默认牌号密度()
    {
        var ctx = CreateDbContext();
        // 先用有效牌号创建工单，再删除牌号映射，使默認密度生效
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);

        // 删除所有 StandardGradeMapping，让 CalculateAsync 找不到牌号
        ctx.StandardGradeMappings.RemoveRange(ctx.StandardGradeMappings);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var calc = await svc.CalculateAsync(new MaterialCalculateRequest
        {
            WorkOrderId = woId,
            AdjustedWallThickness = 8m,
            YieldRate = 90m,
            InputMultiple = 1,
            QualifiedRate = 98m
        });

        calc.Density.Should().Be(7.93m); // 默认密度
    }

    // ========== 计划状态汇总 ==========

    [Fact]
    public async Task GetWorkOrderMaterialPlanAsync_无任何计划_返回NotPlanned()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var dto = await svc.GetWorkOrderMaterialPlanAsync(woId);

        dto.Should().NotBeNull();
        dto.MaterialPlanStatus.Should().Be(MaterialPlanStatus.NotPlanned);
        dto.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWorkOrderMaterialPlanAsync_有原料采购计划_返回汇总()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        await svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = MaterialType.RoughTube,
            RawMaterialSpec = "245*10",
            RequiredPieces = 10,
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1),
            ProcessGroups = GetTestProcessGroups()
        });

        var dto = await svc.GetWorkOrderMaterialPlanAsync(woId);

        dto.Items.Should().HaveCount(1);
        dto.Items[0].PlanType.Should().Be("Semi");
        dto.MaterialPlanStatus.Should().NotBe(MaterialPlanStatus.NotPlanned);
    }

    [Fact]
    public async Task UpdateMaterialPlanStatusAsync_多类型计划_聚合状态正确()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        // 先标记工单的TotalQuantity/TotalWeight以便计算满足率
        var wo = await ctx.WorkOrders.FindAsync(woId);
        wo!.TotalQuantity = 10;
        wo.TotalWeight = 2500m;
        await ctx.SaveChangesAsync();

        // 创建原料采购计划
        await svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = MaterialType.RoughTube,
            RawMaterialSpec = "245*10",
            RequiredPieces = 10,
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1),
            ProcessGroups = GetTestProcessGroups()
        });

        // 创建成品采购计划
        await svc.CreateFinishedPlanAsync(new CreatePurchaseFinishedPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            ProductType = FinishedProductType.Critical,
            RequiredPiece = 10,
            RequiredWeight = 2500m,
            PlantGrade = "Q345B",
            Specification = "89*10",
            OuterDiameterNegative = 0.3m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled
        });

        // 验证状态已更新
        await ctx.Entry(wo).ReloadAsync();
        wo.MaterialPlanStatus.Should().NotBe(MaterialPlanStatus.NotPlanned);
        wo.MaterialPlanRate.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteSemiPlan_更新状态为NotPlanned()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var created = await svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = MaterialType.RoughTube,
            RawMaterialSpec = "245*10",
            RequiredPieces = 10,
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1),
            ProcessGroups = GetTestProcessGroups()
        });

        // 删除后，状态恢复为未计划
        await svc.DeleteSemiPlanAsync(created.Id);

        var wo = await ctx.WorkOrders.FindAsync(woId);
        wo!.MaterialPlanStatus.Should().Be(MaterialPlanStatus.NotPlanned);
        wo.MaterialPlanRate.Should().Be(0);
    }

    // ========== 可用库存查询 ==========

    [Fact]
    public async Task GetAvailableInventoryAsync_返回可用批次()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        var batch = await SeedInventoryBatchAsync(ctx, specification: "219*8", od: 219m, wt: 8m);
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableInventoryAsync(woId);

        available.Should().NotBeEmpty();
        available[0].BatchNo.Should().Be(batch.BatchNo);
    }

    [Fact]
    public async Task GetAvailableInventoryAsync_已被使用的批次_不显示()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 先创建计划引用该批次
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var available = await svc.GetAvailableInventoryAsync(woId2);

        available.Should().NotContain(a => a.Id == batch.Id);
    }

    [Fact]
    public async Task GetAvailableInventoryAsync_计划已出库_批次释放可再次利用()
    {
        var ctx = CreateDbContext();
        var (woId, woNo) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 工单1 部分领用计划
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 40,
            UsedWeight = 4000m
        });

        // 部分出库（生产领用），并模拟出库副作用扣减批次剩余量
        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            OutboundType = OutboundType.ProductionPick,
            WorkOrderNo = woNo,
            OutboundQuantity = 40,
            OutboundWeight = 4000m,
            OutboundDate = DateTime.Today,
            CreatedBy = "user1"
        });
        batch.RemainingQuantity -= 40;
        batch.RemainingWeight -= 4000m;
        await ctx.SaveChangesAsync();

        // 工单2 可用列表应重新出现该批次（剩余 60 支可再次计划，已出库预留不再计入）
        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var available = await svc.GetAvailableInventoryAsync(woId2);

        available.Should().Contain(a => a.Id == batch.Id);
        available.First(a => a.Id == batch.Id).RemainingQuantity.Should().Be(60);
        available.First(a => a.Id == batch.Id).ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task GetAvailableInventoryAsync_非生产领用出库_批次仍占用()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        // 销售出库（非生产领用）不释放批次占用
        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            OutboundType = OutboundType.SalesOut,
            OutboundQuantity = 2,
            OutboundWeight = 200m,
            OutboundDate = DateTime.Today,
            CreatedBy = "user1"
        });
        await ctx.SaveChangesAsync();

        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var available = await svc.GetAvailableInventoryAsync(woId2);

        available.Should().NotContain(a => a.Id == batch.Id);
    }

    [Fact]
    public async Task GetAvailableInventoryAsync_外径不匹配_排除()
    {
        var ctx = CreateDbContext();
        // 工单外径219，批次外径159——不匹配
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        await SeedInventoryBatchAsync(ctx, specification: "159*6", od: 159m, wt: 6m);
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableInventoryAsync(woId);

        available.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableInventoryAsync_部分使用计划_批次仍可被下个工单使用且返回预留量()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        var batch = await SeedInventoryBatchAsync(ctx, specification: "219*8", od: 219m, wt: 8m, quantity: 100, weight: 10000m);
        var svc = CreateService(ctx);

        // 工单1 部分使用预留
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 30,
            UsedWeight = 3000m
        });

        // 工单2 仍可搜索到该批次，且预留量正确
        var (woId2, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        var available = await svc.GetAvailableInventoryAsync(woId2);

        available.Should().Contain(a => a.Id == batch.Id);
        var item = available.First(a => a.Id == batch.Id);
        item.RemainingQuantity.Should().Be(100);
        item.ReservedQuantity.Should().Be(30);
        item.ReservedWeight.Should().Be(3000m);
    }

    [Fact]
    public async Task GetAvailableReworkInventoryAsync_部分使用计划_批次仍可被下个工单使用且返回预留量()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        var batch = await SeedInventoryBatchAsync(ctx, specification: "250*8", od: 250m, wt: 8.2m,
            plantGrade: "Q345B", unitWeight: 270m);
        var svc = CreateService(ctx);

        // 工单1 部分使用预留
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            ReworkType = ReworkType.EmptyDrawing,
            UsageMode = "Partial",
            UsedQuantity = 30,
            UsedWeight = 3000m,
            ProcessGroups = GetTestProcessGroups()
        });

        // 工单2 空拉改制仍可搜索到该批次，且预留量正确
        var (woId2, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        var available = await svc.GetAvailableReworkInventoryAsync(woId2, ReworkType.EmptyDrawing);

        available.Should().Contain(a => a.Id == batch.Id);
        var item = available.First(a => a.Id == batch.Id);
        item.ReservedQuantity.Should().Be(30);
        item.ReservedWeight.Should().Be(3000m);
    }

    // ========== 可用改制库存查询 ==========

    [Fact]
    public async Task GetAvailableReworkInventoryAsync_空拉改制_返回匹配批次()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        // 空拉改制需要外径 >= 测算OD*1.05，壁厚在0.95~1.05倍之间
        var batch = await SeedInventoryBatchAsync(ctx, specification: "250*8", od: 250m, wt: 8.2m,
            plantGrade: "Q345B", unitWeight: 270m);
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableReworkInventoryAsync(woId, ReworkType.EmptyDrawing);

        available.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAvailableReworkInventoryAsync_计划已出库_批次释放可再次利用()
    {
        var ctx = CreateDbContext();
        var (woId, woNo) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        var batch = await SeedInventoryBatchAsync(ctx, specification: "250*8", od: 250m, wt: 8.2m,
            plantGrade: "Q345B", unitWeight: 270m);
        var svc = CreateService(ctx);

        // 工单1 空拉改制计划（部分领用）
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            ReworkType = ReworkType.EmptyDrawing,
            UsageMode = "Partial",
            UsedQuantity = 30,
            UsedWeight = 3000m,
            ProcessGroups = GetTestProcessGroups()
        });

        // 生产领用出库，并模拟出库副作用扣减批次剩余量
        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            OutboundType = OutboundType.ProductionPick,
            WorkOrderNo = woNo,
            OutboundQuantity = 30,
            OutboundWeight = 3000m,
            OutboundDate = DateTime.Today,
            CreatedBy = "user1"
        });
        batch.RemainingQuantity -= 30;
        batch.RemainingWeight -= 3000m;
        await ctx.SaveChangesAsync();

        // 工单2 空拉改制可用列表应重新出现该批次（剩余 70 支可再次计划，已出库预留不再计入）
        var (woId2, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        var available = await svc.GetAvailableReworkInventoryAsync(woId2, ReworkType.EmptyDrawing);

        available.Should().Contain(a => a.Id == batch.Id);
        available.First(a => a.Id == batch.Id).RemainingQuantity.Should().Be(70);
        available.First(a => a.Id == batch.Id).ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task GetAvailableReworkInventoryAsync_空拉改制外径过小_排除()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        // 外径219 * 1.05 ≈ 230，批次外径200太小，壁厚保持在合适范围内以排除外径因素
        await SeedInventoryBatchAsync(ctx, specification: "200*8", od: 200m, wt: 8m,
            plantGrade: "Q345B", unitWeight: 270m);
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableReworkInventoryAsync(woId, ReworkType.EmptyDrawing);

        available.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableReworkInventoryAsync_空拉改制不匹配规格_返回空()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableReworkInventoryAsync(woId, ReworkType.EmptyDrawing);

        available.Should().BeEmpty();
    }

    // ========== 圆棒穿孔计划 CRUD ==========

    [Fact]
    public async Task GetPiercingPlansAsync_无计划_返回空列表()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var plans = await svc.GetPiercingPlansAsync(woId);

        plans.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePiercingPlanAsync_定尺_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var result = await svc.CreatePiercingPlanAsync(new CreateRoundBarPiercingPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = MaterialType.RoundBar,
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredUnitWeight = 300m,
            RequiredPieces = 10,
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1),
            Remark = "穿孔测试",
            ProcessGroups = GetTestProcessGroups()
        });

        result.Should().NotBeNull();
        result.WorkOrderId.Should().Be(woId);
        result.RoundBarSpec.Should().Be("250*8");
        result.PiercingSpec.Should().Be("230*7");
        result.Density.Should().Be(7.85m);
        result.RequiredPieces.Should().Be(10);
        result.Remark.Should().Be("穿孔测试");

        // 验证数据库中有记录
        var plans = await ctx.RoundBarPiercingPlans.Where(p => p.WorkOrderId == woId).ToListAsync();
        plans.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreatePiercingPlanAsync_非定尺_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.NonFixed);
        var svc = CreateService(ctx);

        var result = await svc.CreatePiercingPlanAsync(new CreateRoundBarPiercingPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = MaterialType.RoundBar,
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredPieces = 10,
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1),
            ProcessGroups = GetTestProcessGroups()
        });

        result.Should().NotBeNull();
        result.Should().NotBeNull();
        // 非定尺：无 RequiredUnitWeight 但仍有支数/重量
        result.RequiredPieces.Should().Be(10);
        result.RequiredWeight.Should().Be(3000m);
    }

    [Fact]
    public async Task CreatePiercingPlanAsync_工单不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreatePiercingPlanAsync(new CreateRoundBarPiercingPlanRequest
        {
            WorkOrderId = 999,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = MaterialType.RoundBar,
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task DeletePiercingPlanAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var created = await svc.CreatePiercingPlanAsync(new CreateRoundBarPiercingPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = MaterialType.RoundBar,
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredPieces = 10,
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1),
            ProcessGroups = GetTestProcessGroups()
        });

        await svc.DeletePiercingPlanAsync(created.Id);

        var act = () => svc.GetPiercingPlanByIdAsync(created.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task DeletePiercingPlanAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeletePiercingPlanAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task GetWorkOrderMaterialPlanAsync_包含圆棒穿孔计划()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        // 创建圆棒穿孔计划
        await svc.CreatePiercingPlanAsync(new CreateRoundBarPiercingPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = MaterialType.RoundBar,
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredPieces = 10,
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1),
            ProcessGroups = GetTestProcessGroups()
        });

        var tabs = await svc.GetWorkOrderMaterialPlanAsync(woId);

        tabs.Should().NotBeNull();
        tabs.Items.Should().Contain(i => i.PlanType == "Piercing");
        var piercingTab = tabs.Items.First(i => i.PlanType == "Piercing");
        piercingTab.RecordCount.Should().Be(1);
        piercingTab.Summary.Should().Contain("250*8");
    }

    [Fact]
    public async Task CalculateAsync_定尺_返回计算结果()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var result = await svc.CalculateAsync(new MaterialCalculateRequest
        {
            WorkOrderId = woId,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m
        });

        result.Should().NotBeNull();
        result.Density.Should().Be(7.85m);
    }

    // ========== 第6类 在产改制计划：部分预留共享 + 已投料禁改 ==========

    [Fact]
    public async Task GetAvailableInProcessBatchesAsync_部分使用计划_批次仍可被其他工单使用且返回预留量()
    {
        var ctx = CreateDbContext();
        var wo1 = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        var batch = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15]);
        var svc = CreateService(ctx);

        // 工单1 部分使用预留
        await svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 3,
            UsedWeight = 1000m,
            ProcessGroups = GetTestProcessGroups()
        });

        // 工单2 仍可搜索到该批次，且预留量正确
        var wo2 = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-2");
        var available = await svc.GetAvailableInProcessBatchesAsync(wo2.Id);

        available.Should().Contain(a => a.Id == batch.Id);
        var item = available.First(a => a.Id == batch.Id);
        item.ReservedQuantity.Should().Be(3);
        item.ReservedWeight.Should().Be(1000m);
    }

    [Fact]
    public async Task GetAvailableInProcessBatchesAsync_同工单已引用批次_仍呈现且可追加()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        var batch = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15]);
        var svc = CreateService(ctx);

        await svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 3,
            UsedWeight = 1000m,
            ProcessGroups = GetTestProcessGroups()
        });

        // 同一工单再次查询可用批次：已引用批次不再消失，仍呈现且预留量正确（可继续追加余量）
        var available = await svc.GetAvailableInProcessBatchesAsync(wo.Id);

        available.Should().Contain(a => a.Id == batch.Id);
        var item = available.First(a => a.Id == batch.Id);
        item.ReservedQuantity.Should().Be(3);
        item.ReservedWeight.Should().Be(1000m);
    }

    [Fact]
    public async Task GetAvailableInProcessBatchesAsync_批次已投产_仍占用预留()
    {
        var ctx = CreateDbContext();
        var wo1 = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        var batch = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15]);
        var svc = CreateService(ctx);

        await svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 3,
            UsedWeight = 1000m,
            ProcessGroups = GetTestProcessGroups()
        });

        // 批次已实际投产（有生产记录）仍占用预留——余料共享依赖预留账本始终累计计划用量
        await SeedProductionRecordAsync(ctx, batch.Id);

        var wo2 = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-2");
        var available = await svc.GetAvailableInProcessBatchesAsync(wo2.Id);

        available.Should().Contain(a => a.Id == batch.Id);
        var item = available.First(a => a.Id == batch.Id);
        item.ReservedQuantity.Should().Be(3);
        item.ReservedWeight.Should().Be(1000m);
    }

    [Fact]
    public async Task GetAvailableInProcessBatchesAsync_未产批次_按下个规格判定()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1"); // 规格 219*8，改制壁厚目标 8
        var svc = CreateService(ctx);

        // 未产批次：自身规格壁厚 6 < 8 不匹配，但"下个规格"219*8 匹配 → 应显示（用下个规格判定）
        var batchA = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15], status: BatchStatus.None);
        batchA.Specification = "219*6";
        batchA.CurrentSpec = null;
        batchA.CorrespondingSpec = "219*8";
        batchA.CurrentGroupName = null;
        batchA.CurrentSectionName = null;
        batchA.NextProcess = "RoughTubeProcessing";
        batchA.NextSectionName = "RoughTubeProcessing";
        await ctx.SaveChangesAsync();

        // 未产批次：下个规格壁厚 5 < 8 仍不匹配 → 不显示
        var batchB = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15], status: BatchStatus.None);
        batchB.Specification = "219*6";
        batchB.CurrentSpec = null;
        batchB.CorrespondingSpec = "219*5";
        batchB.CurrentGroupName = null;
        batchB.CurrentSectionName = null;
        await ctx.SaveChangesAsync();

        // 在产批次：当前规格 219*8 匹配 → 显示（在产逻辑不变，用当前规格）
        var batchC = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15], status: BatchStatus.InProgress);
        batchC.Specification = "219*6";
        batchC.CurrentSpec = "219*8";
        await ctx.SaveChangesAsync();

        // 在产批次：当前规格空 → 兜底自身规格 219*6 壁厚 6 < 8 → 不显示
        var batchD = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15], status: BatchStatus.InProgress);
        batchD.Specification = "219*6";
        batchD.CurrentSpec = null;
        await ctx.SaveChangesAsync();

        var available = await svc.GetAvailableInProcessBatchesAsync(wo.Id);

        available.Should().Contain(a => a.Id == batchA.Id);
        var itemA = available.First(a => a.Id == batchA.Id);
        itemA.Specification.Should().Be("219*8"); // 规格列随状态切换为下个规格
        itemA.CorrespondingSpec.Should().Be("219*8");
        itemA.CurrentSpec.Should().BeNull();
        itemA.NextProcess.Should().Be("RoughTubeProcessing");
        itemA.NextSectionName.Should().Be("RoughTubeProcessing");

        available.Should().NotContain(a => a.Id == batchB.Id);
        available.Should().Contain(a => a.Id == batchC.Id);
        available.First(a => a.Id == batchC.Id).Specification.Should().Be("219*8"); // 在产规格=当前规格
        available.Should().NotContain(a => a.Id == batchD.Id);
    }

    [Fact]
    public async Task GetAvailableInProcessBatchesAsync_未产批次_跳过单支重量校验()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1"); // 规格 219*8 Fixed，最小单支重量≈264.9kg
        var svc = CreateService(ctx);

        // 未产批次：重量/支数/倍率换算（3000/10/4=75kg）远低于目标最小单支，但未产材料为原料态
        // （未经历任何工序），跳过单支重量判定 → 应显示（下个规格 219*8 匹配）
        var batchNone = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15],
            status: BatchStatus.None, currentValidWeight: 3000, productionRatio: 4);
        batchNone.CurrentSpec = null;
        batchNone.CorrespondingSpec = "219*8";
        await ctx.SaveChangesAsync();

        // 在产批次：同样重量/支数/倍率数据，但材料已开工 → 单支重量判定仍生效（75kg < 264.9kg）→ 不显示
        var batchInProgress = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15],
            status: BatchStatus.InProgress, currentValidWeight: 3000, productionRatio: 4);
        batchInProgress.CurrentSpec = "219*8";
        await ctx.SaveChangesAsync();

        var available = await svc.GetAvailableInProcessBatchesAsync(wo.Id);

        available.Should().Contain(a => a.Id == batchNone.Id);
        available.Should().NotContain(a => a.Id == batchInProgress.Id);
    }

    [Fact]
    public async Task GetAvailableInProcessBatchesAsync_成检批次_不显示()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15], status: BatchStatus.InFinalInspection);
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableInProcessBatchesAsync(wo.Id);

        available.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateInProcessReworkPlanAsync_成检批次_抛出()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        var batch = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15], status: BatchStatus.InFinalInspection);
        var svc = CreateService(ctx);

        var act = () => svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 3,
            UsedWeight = 1000m,
            ProcessGroups = GetTestProcessGroups()
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*只能选择未产或在产状态的批次*");
    }

    [Fact]
    public async Task CreateInProcessReworkPlanAsync_累计预留超量_抛出()
    {
        var ctx = CreateDbContext();
        var wo1 = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        var batch = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15]);
        var svc = CreateService(ctx);

        await svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 5,
            UsedWeight = 2000m,
            ProcessGroups = GetTestProcessGroups()
        });

        // 工单2 再使用 1500kg > 3000-2000=1000 → 抛错
        var wo2 = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-2");
        var act = () => svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo2.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 4,
            UsedWeight = 1500m,
            ProcessGroups = GetTestProcessGroups()
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*超过批次可用有效重量*");
    }

    [Fact]
    public async Task CreateInProcessReworkPlanAsync_累计预留未超量_成功()
    {
        var ctx = CreateDbContext();
        var wo1 = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        var batch = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15]);
        var svc = CreateService(ctx);

        await svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 5,
            UsedWeight = 2000m,
            ProcessGroups = GetTestProcessGroups()
        });

        // 工单2 再使用 800kg ≤ 3000-2000=1000 → 成功
        var wo2 = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-2");
        var created = await svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo2.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 2,
            UsedWeight = 800m,
            ProcessGroups = GetTestProcessGroups()
        });

        created.Id.Should().BeGreaterThan(0);
        created.UsedWeight.Should().Be(800m);
        // 在产改制：工艺周期随创建请求内算（工序组 3 工段 × mock 每工段 3 天 = 9）
        var wo1Plan = await ctx.InProcessReworkPlans.SingleAsync(p => p.WorkOrderId == wo1.Id);
        wo1Plan.StandardCycle.Should().Be(9);
    }

    [Fact]
    public async Task UpdateInProcessReworkPlanAsync_完全锁死_一律禁止修改()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        var batch = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15]);
        var svc = CreateService(ctx);

        var created = await svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 3,
            UsedWeight = 1000m,
            ProcessGroups = GetTestProcessGroups()
        });

        // 编辑完全锁死：无论是否已投料，一律不可修改（可删除后重建）
        var act = () => svc.UpdateInProcessReworkPlanAsync(created.Id, new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 5,
            UsedWeight = 1500m,
            ProcessGroups = GetTestProcessGroups()
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不可修改*");
    }

    [Fact]
    public async Task UpdateInProcessReworkPlanAsync_完全锁死_未投料也不可修改()
    {
        var ctx = CreateDbContext();
        var wo1 = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        var wo2 = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-2");
        var batch = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15]);
        var svc = CreateService(ctx);

        var planA = await svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 3,
            UsedWeight = 1000m,
            ProcessGroups = GetTestProcessGroups()
        });

        await svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo2.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 2,
            UsedWeight = 800m,
            ProcessGroups = GetTestProcessGroups()
        });

        // 未投料、余量充足也不可修改（编辑完全锁死）
        var act = () => svc.UpdateInProcessReworkPlanAsync(planA.Id, new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 5,
            UsedWeight = 1500m,
            ProcessGroups = GetTestProcessGroups()
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不可修改*");
    }

    [Fact]
    public async Task DeleteInProcessReworkPlanAsync_已投料_仍可删除()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IPR-SUB-1");
        var batch = await SeedProductionBatchAsync(ctx, $"IPR-{Guid.NewGuid():N}"[..15]);
        var svc = CreateService(ctx);

        var created = await svc.CreateInProcessReworkPlanAsync(new CreateInProcessReworkPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            UsedQuantity = 3,
            UsedWeight = 1000m,
            ProcessGroups = GetTestProcessGroups()
        });

        await SeedProductionRecordAsync(ctx, batch.Id);

        await svc.DeleteInProcessReworkPlanAsync(created.Id);

        var deleted = await ctx.InProcessReworkPlans.FindAsync(created.Id);
        deleted.Should().BeNull();
    }

    // ========== 第7类 在产主工单计划：部分预留共享 + 已投料禁改 ==========

    [Fact]
    public async Task CreateInMainWorkOrderPlanAsync_累计预留超量_抛出()
    {
        var ctx = CreateDbContext();
        var wo1 = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-1");
        await SeedMainWorkOrderAsync(ctx, "IMW-MAIN-1");
        await SeedExecutionSummaryAsync(ctx, "IMW-MAIN-1", "SO-001", "X01", 0m, 0m);
        var batch = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15], workOrderNo: "IMW-MAIN-1", currentValidWeight: 3000);
        var svc = CreateService(ctx);

        await svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 5,
            AllocatedWeight = 2000m,
            ProductionRatio = 1
        });

        // 工单2 再分配 1500kg > 3000-2000=1000 → 抛错
        var wo2 = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-2");
        var act = () => svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo2.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 4,
            AllocatedWeight = 1500m,
            ProductionRatio = 1
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*分配重量(1500)超过可用重量(1000)*");
    }

    [Fact]
    public async Task CreateInMainWorkOrderPlanAsync_累计预留未超量_成功()
    {
        var ctx = CreateDbContext();
        var wo1 = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-1");
        await SeedMainWorkOrderAsync(ctx, "IMW-MAIN-1");
        await SeedExecutionSummaryAsync(ctx, "IMW-MAIN-1", "SO-001", "X01", 0m, 0m);
        var batch = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15], workOrderNo: "IMW-MAIN-1", currentValidWeight: 3000);
        var svc = CreateService(ctx);

        await svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 5,
            AllocatedWeight = 2000m,
            ProductionRatio = 1
        });

        // 工单2 再分配 800kg ≤ 3000-2000=1000 → 成功
        var wo2 = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-2");
        var created = await svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo2.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 2,
            AllocatedWeight = 800m,
            ProductionRatio = 1
        });

        created.Id.Should().BeGreaterThan(0);
        created.AllocatedWeight.Should().Be(800m);
    }

    [Fact]
    public async Task CreateInMainWorkOrderPlanAsync_成检批次_允许()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-1");
        await SeedMainWorkOrderAsync(ctx, "IMW-MAIN-1");
        await SeedExecutionSummaryAsync(ctx, "IMW-MAIN-1", "SO-001", "X01", 0m, 0m);
        var batch = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15],
            workOrderNo: "IMW-MAIN-1", status: BatchStatus.InFinalInspection, currentValidWeight: 3000);
        var svc = CreateService(ctx);

        var created = await svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 3,
            AllocatedWeight = 1000m,
            ProductionRatio = 1
        });

        created.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateInMainWorkOrderPlanAsync_完成批次_抛出()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-1");
        var batch = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15],
            workOrderNo: "IMW-MAIN-1", status: BatchStatus.Completed, currentValidWeight: 3000);
        var svc = CreateService(ctx);

        var act = () => svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 3,
            AllocatedWeight = 1000m,
            ProductionRatio = 1
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*只能选择未产、在产或成检状态的批次*");
    }

    [Fact]
    public async Task CreateInMainWorkOrderPlanAsync_超主号可分配上限_抛出()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-1");
        await SeedMainWorkOrderAsync(ctx, "IMW-MAIN-1");
        await SeedExecutionSummaryAsync(ctx, "IMW-MAIN-1", "SO-001", "X01", 1000m, 0m);
        var batch = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15], workOrderNo: "IMW-MAIN-1", currentValidWeight: 1000);
        var svc = CreateService(ctx);

        // 可分配剩余总重量 = max(0, 1000 − 1000 − 0) = 0 → 500 超限
        var act = () => svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 5,
            AllocatedWeight = 500m,
            ProductionRatio = 1
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*分配重量(500)超过可用重量(0)*");
    }

    [Fact]
    public async Task GetAvailableMainWorkOrderBatchesAsync_部分使用计划_批次仍可被其他工单使用且返回预留量()
    {
        var ctx = CreateDbContext();
        var sub1 = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-1");
        await SeedMainWorkOrderAsync(ctx, "IMW-MAIN-1");
        await SeedExecutionSummaryAsync(ctx, "IMW-MAIN-1", "SO-001", "X01", 500m, 0m);
        var batch = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15],
            workOrderNo: "IMW-MAIN-1", currentValidQty: 10, currentValidWeight: 1000);
        var svc = CreateService(ctx);

        await svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = sub1.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 2,
            AllocatedWeight = 200m,
            ProductionRatio = 1
        });

        // 工单2 仍可搜索到该批次，且预留量与主工单级聚合正确
        var sub2 = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-2");
        var available = await svc.GetAvailableMainWorkOrderBatchesAsync(sub2.Id);

        available.Should().Contain(a => a.Id == batch.Id);
        var item = available.First(a => a.Id == batch.Id);
        item.ReservedQuantity.Should().Be(2);
        item.ReservedWeight.Should().Be(200m);
        // 主工单级聚合：总有效投料 1000 / 总预留 200 / 可分配剩余 = max(0, 1000−500−200) = 300
        item.MainNoTotalValidWeight.Should().Be(1000m);
        item.MainNoTotalReservedWeight.Should().Be(200m);
        item.MainNoAllocatableRemaining.Should().Be(300m);
    }

    [Fact]
    public async Task GetAvailableMainWorkOrderBatchesAsync_同工单已引用批次_仍呈现且可追加()
    {
        var ctx = CreateDbContext();
        var sub = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-1");
        await SeedMainWorkOrderAsync(ctx, "IMW-MAIN-1");
        await SeedExecutionSummaryAsync(ctx, "IMW-MAIN-1", "SO-001", "X01", 500m, 0m);
        var batch = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15],
            workOrderNo: "IMW-MAIN-1", currentValidQty: 10, currentValidWeight: 1000);
        var svc = CreateService(ctx);

        await svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = sub.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 2,
            AllocatedWeight = 200m,
            ProductionRatio = 1
        });

        // 同一工单再次查询可用批次：已引用批次不再消失，仍呈现且预留量正确（可继续追加余量）
        var available = await svc.GetAvailableMainWorkOrderBatchesAsync(sub.Id);

        available.Should().Contain(a => a.Id == batch.Id);
        var item = available.First(a => a.Id == batch.Id);
        item.ReservedQuantity.Should().Be(2);
        item.ReservedWeight.Should().Be(200m);
        // 主工单级聚合：总有效投料 1000 / 总预留 200 / 可分配剩余 = max(0, 1000−500−200) = 300
        item.MainNoTotalValidWeight.Should().Be(1000m);
        item.MainNoTotalReservedWeight.Should().Be(200m);
        item.MainNoAllocatableRemaining.Should().Be(300m);
    }

    [Fact]
    public async Task UpdateInMainWorkOrderPlanAsync_完全锁死_一律禁止修改()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-1");
        await SeedMainWorkOrderAsync(ctx, "IMW-MAIN-1");
        await SeedExecutionSummaryAsync(ctx, "IMW-MAIN-1", "SO-001", "X01", 0m, 0m);
        var batch = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15],
            workOrderNo: "IMW-MAIN-1", currentValidWeight: 3000);
        var svc = CreateService(ctx);

        var created = await svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 3,
            AllocatedWeight = 1000m,
            ProductionRatio = 1
        });

        // 编辑完全锁死：无论是否已投料，一律不可修改（可删除后重建）
        var act = () => svc.UpdateInMainWorkOrderPlanAsync(created.Id, new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 5,
            AllocatedWeight = 1500m,
            ProductionRatio = 1
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不可修改*");
    }

    [Fact]
    public async Task UpdateInMainWorkOrderPlanAsync_完全锁死_未投料也不可修改()
    {
        var ctx = CreateDbContext();
        var wo1 = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-1");
        var wo2 = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-2");
        await SeedMainWorkOrderAsync(ctx, "IMW-MAIN-1");
        await SeedExecutionSummaryAsync(ctx, "IMW-MAIN-1", "SO-001", "X01", 0m, 0m);
        var batch = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15],
            workOrderNo: "IMW-MAIN-1", currentValidWeight: 3000);
        var svc = CreateService(ctx);

        var planA = await svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 3,
            AllocatedWeight = 1000m,
            ProductionRatio = 1
        });

        await svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo2.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 2,
            AllocatedWeight = 800m,
            ProductionRatio = 1
        });

        // 未投料、余量充足也不可修改（编辑完全锁死）
        var act = () => svc.UpdateInMainWorkOrderPlanAsync(planA.Id, new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 5,
            AllocatedWeight = 1500m,
            ProductionRatio = 1
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不可修改*");
    }

    [Fact]
    public async Task DeleteInMainWorkOrderPlanAsync_已投料_仍可删除()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMainWorkOrderAsync(ctx, "IMW-SUB-1");
        await SeedMainWorkOrderAsync(ctx, "IMW-MAIN-1");
        await SeedExecutionSummaryAsync(ctx, "IMW-MAIN-1", "SO-001", "X01", 0m, 0m);
        var batch = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15],
            workOrderNo: "IMW-MAIN-1", currentValidWeight: 3000);
        var svc = CreateService(ctx);

        var created = await svc.CreateInMainWorkOrderPlanAsync(new CreateInMainWorkOrderPlanRequest
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductionBatchId = batch.Id,
            AllocatedQuantity = 3,
            AllocatedWeight = 1000m,
            ProductionRatio = 1
        });

        var descendant = await SeedProductionBatchAsync(ctx, $"IMW-{Guid.NewGuid():N}"[..15],
            workOrderNo: "非工单", sourceProductionNo: batch.BatchNo);
        await SeedProductionRecordAsync(ctx, descendant.Id);

        await svc.DeleteInMainWorkOrderPlanAsync(created.Id);

        var deleted = await ctx.InMainWorkOrderPlans.FindAsync(created.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task RecalculateStandardCycleForBatchAsync_批次编辑重算_按工单交货状态加天()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedProductionBatchAsync(ctx, $"RSC-{Guid.NewGuid():N}"[..15]);

        // 批次工序组：2 个工段（冷轧拔+去油），mock 每个工段 3 天
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ColdRollDraw = 1,
            Degrease = 1
        });

        // 库料改制计划（ReworkType 非空）关联该批次
        var plan = new InventoryPlan
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = $"INV-{Guid.NewGuid():N}"[..15],
            BatchNo = batch.BatchNo,
            MaterialType = "InProcess",
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            UsageMode = "All",
            UsedWeight = 1000m,
            ReworkType = ReworkType.ManualSelect,
            StandardCycle = 999
        };
        ctx.InventoryPlans.Add(plan);
        await ctx.SaveChangesAsync();

        // 工单交货状态=固溶酸洗 → 附加 +5 天；旧实现传 null 只加默认加天（此测试默认加天为 0），会漏算
        var svc = CreateService(ctx, new Dictionary<string, double> { ["SolutionAnnealedAndPickled"] = 5 });

        await svc.RecalculateStandardCycleForBatchAsync(batch.BatchNo);

        var reloaded = await ctx.InventoryPlans.FindAsync(plan.Id);
        // 2 工段 × 3 天 + 交货状态附加 5 天 = 11
        reloaded!.StandardCycle.Should().Be(11);
    }

    [Fact]
    public async Task DismissInProcessReworkPlansByBatchAsync_消除计划_刷新工单执行状况()
    {
        var ctx = CreateDbContext();
        var (woId, woNo) = await SeedWorkOrderAsync(ctx);
        var workOrderExecMock = new Mock<IWorkOrderExecutionService>();
        var svc = CreateService(ctx, workOrderExecMock: workOrderExecMock);

        ctx.InProcessReworkPlans.Add(new InProcessReworkPlan
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            ProductionBatchId = 999,
            BatchNo = "BATCH-DISMISS",
            BatchTagNo = "TAG-DISMISS",
            PlantGrade = "20#",
            Specification = "219×8",
            LengthStatus = "NonFixed",
            UsedWeight = 1000m,
            PlanStatus = InventoryPlanStatus.Planned
        });
        await ctx.SaveChangesAsync();

        workOrderExecMock.Invocations.Clear();

        await svc.DismissInProcessReworkPlansByBatchAsync(999);

        // 计划消除（Planned→Completed）后，所属工单的执行状况须重算（G9 计划执行不再计入已消除计划）
        workOrderExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains(woNo))), Times.AtLeastOnce);

        var plan = await ctx.InProcessReworkPlans.FirstAsync(p => p.ProductionBatchId == 999);
        plan.PlanStatus.Should().Be(InventoryPlanStatus.Completed);
    }
}
