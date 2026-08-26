using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Services.Batch;
using MES.Services.WorkOrder;
using MES.Services.Order;
using MES.Tests.Tests;
using Moq;
using QuestPDF.Infrastructure;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Order;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Quality;

namespace MES.Tests.Services;

/// <summary>
/// 批次服务测试：创建、查询、更新、删除、工序组、打印
/// </summary>
public class BatchServiceTests : TestBase
{
    static BatchServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private BatchService CreateService(AppDbContext ctx, Mock<IProductionRecordService>? prodRecordMock = null, Mock<IFinalInspectionService>? finalInspectionMock = null, Mock<IWorkOrderExecutionService>? workOrderExecMock = null, Mock<IProcessCardColumnDefinitionService>? pccMock = null, Mock<IWorkOrderListSummaryRefreshService>? listSummaryMock = null)
    {
        var loggerMock = new Mock<ILogger<BatchService>>();
        prodRecordMock ??= new Mock<IProductionRecordService>();
        finalInspectionMock ??= new Mock<IFinalInspectionService>();
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        workOrderExecMock ??= new Mock<IWorkOrderExecutionService>();
        var materialPlanMock = new Mock<IMaterialPlanService>();
        var qptMock = new Mock<IQualityProcessTrackingService>();
        pccMock ??= CreateProcessCardColumnDefinitionServiceMock();
        var styleMock = CreateProcessCardStyleDefinitionServiceMock();
        listSummaryMock ??= new Mock<IWorkOrderListSummaryRefreshService>();
        return new BatchService(ctx, loggerMock.Object, prodRecordMock.Object, finalInspectionMock.Object, configMock.Object, workOrderExecMock.Object, materialPlanMock.Object, new Mock<IOperationLogService>().Object, qptMock.Object, new Mock<INotificationService>().Object, new Mock<ISectionNameDisplayService>().Object, CreateProcessDefinitionServiceMock(), pccMock.Object, styleMock.Object, new MemoryCache(new MemoryCacheOptions()), listSummaryMock.Object);
    }

    /// <summary>工艺卡列布局配置服务 mock：默认返回空配置映射（打印合并时请求列全部走兜底）</summary>
    private static Mock<IProcessCardColumnDefinitionService> CreateProcessCardColumnDefinitionServiceMock()
    {
        var mock = new Mock<IProcessCardColumnDefinitionService>();
        mock.Setup(x => x.GetConfigMapAsync())
            .ReturnsAsync(new Dictionary<string, ProcessCardColumnDefinitionDto>(StringComparer.OrdinalIgnoreCase));
        return mock;
    }

    /// <summary>工艺卡版式配置服务 mock：默认返回空样式映射（打印时全部回退硬编码默认值）</summary>
    private static Mock<IProcessCardStyleDefinitionService> CreateProcessCardStyleDefinitionServiceMock()
    {
        var mock = new Mock<IProcessCardStyleDefinitionService>();
        mock.Setup(x => x.GetStyleMapAsync())
            .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        return mock;
    }

    // ========== 种子数据辅助方法 ==========

    /// <summary>
    /// 种子一个工单（含订单），返回工单号
    /// </summary>
    private async Task<string> SeedWorkOrderAsync(AppDbContext ctx)
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
            OrderNumber = $"WO-BATCH-{Guid.NewGuid():N}"[..15],
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    StandardNo = sr.StandardNo,
                    StandardGrade = gm.StandardGrade,
                    PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
                    OuterDiameter = 219m,
                    WallThickness = 8m,
                    OuterDiameterNegative = 0.5m,
                    OuterDiameterPositive = 0.5m,
                    WallThicknessNegative = 0.5m,
                    WallThicknessPositive = 0.5m,
                    LengthStatus = LengthStatus.Fixed,
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

        // 确认订单
        await orderSvc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Confirmed,
            RowVersion = new byte[8]
        });

        // 生成工单
        var items = await ctx.OrderItems.Where(oi => oi.SalesOrderId == order.Id).ToListAsync();
        var itemIds = items.Select(i => i.Sequence).ToList();

        var configMock = new Mock<IConfigParameterService>();
        var woSvc = new WorkOrderService(ctx, new Mock<ILogger<WorkOrderService>>().Object, configMock.Object, new Mock<IOperationLogService>().Object, new MemoryCache(new MemoryCacheOptions()));
        var generated = await woSvc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = order.OrderNumber,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "X01", ProductionSubNo = "01", OrderItemIds = itemIds }
            }
        });

        return generated[0].WorkOrderNo;
    }

    /// <summary>
    /// 种子一个测试仓库
    /// </summary>
    private async Task<Warehouse> SeedWarehouseAsync(AppDbContext ctx)
    {
        var wh = new Warehouse { Name = "原料仓库", Code = "WH-RAW" };
        ctx.Warehouses.Add(wh);
        await ctx.SaveChangesAsync();
        return wh;
    }

    // ========== 创建批次 ==========

    [Fact]
    public async Task CreateAsync_无工单_创建成功()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-001",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100,
            Remark = "测试批次无工单"
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().NotBeNullOrEmpty();
        result.BatchNo.Should().Match("*??-????"); // YYMM-XXXX 格式
        result.TagNo.Should().Be("TAG-001");
        result.Status.Should().Be(BatchStatus.None);
    }

    [Fact]
    public async Task CreateAsync_带工单_自动填充工单字段()
    {
        var ctx = CreateDbContext();
        var workOrderNo = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = workOrderNo,
            TagNo = "TAG-WO-001",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "GB/T 8163",
            TechnicalRequirements = RequirementType.Normal
        });

        result.Should().NotBeNull();
        result.WorkOrderNo.Should().Be(workOrderNo);

        // 验证工单字段已复制
        var detail = await svc.GetByIdAsync(result.Id);
        detail.SalesOrderNo.Should().NotBeNullOrEmpty();
        detail.PlantGrade.Should().NotBeNullOrEmpty();
        detail.Specification.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAsync_带工单_刷新用料计划总览()
    {
        var ctx = CreateDbContext();
        var workOrderNo = await SeedWorkOrderAsync(ctx);
        var listSummaryMock = new Mock<IWorkOrderListSummaryRefreshService>();
        var svc = CreateService(ctx, listSummaryMock: listSummaryMock);

        await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = workOrderNo,
            TagNo = "TAG-WO-LS-001",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "GB/T 8163",
            TechnicalRequirements = RequirementType.Normal
        });

        // 带工单批次创建后，按订单号刷新用料计划总览（产能工量依赖批次）
        listSummaryMock.Verify(x => x.RefreshBySalesOrderAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_工单不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "NONEXISTENT-WO",
            TagNo = "TAG-ERR"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*工单不存在*");
    }

    [Fact]
    public async Task CreateAsync_带工序组_保存成功()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-PG",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100,
            Remark = "带工序组测试",
            ProcessGroups = new List<CreateProcessGroupRequest>
            {
                new()
                {
                    ProcessName = "矫切酸检",
                    ManufacturingSpec = "Φ50×5",
                    ColdRollDraw = 1,
                    Pickle = 2,
                    Inspection = 3
                },
                new()
                {
                    ProcessName = "60冷轧",
                    ManufacturingSpec = "Φ50×5",
                    ColdRollDraw = 4,
                    Solution = 5
                }
            }
        });

        result.Should().NotBeNull();

        // 验证工序组已保存
        var groups = await svc.GetProcessGroupsAsync(result.Id);
        groups.Should().HaveCount(2);
        groups[0].SequenceNumber.Should().Be(1);
        groups[0].ProcessName.Should().Be("矫切酸检");
        groups[1].SequenceNumber.Should().Be(2);
        groups[1].ProcessName.Should().Be("60冷轧");
    }

    [Fact]
    public async Task CreateAsync_定尺状态_自动计算制成倍数()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-RATIO",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            LengthStatus = LengthStatus.Fixed,
            TotalWeight = 1000m,
            TotalQuantity = 100,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        result.Should().NotBeNull();

        // 投料单重 = 1200/100 = 12, 工单单重 = 1000/100 = 10
        // 制成倍数 = floor(12/10) = 1
        var detail = await svc.GetByIdAsync(result.Id);
        detail.ProductionRatio.Should().Be(1);
    }

    // ========== 查询批次详情 ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回详情()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-DETAIL",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        detail.Should().NotBeNull();
        detail.Id.Should().Be(created.Id);
        detail.BatchNo.Should().Be(created.BatchNo);
        detail.TagNo.Should().Be("TAG-DETAIL");
        detail.Status.Should().Be(BatchStatus.None);
        detail.ProcessGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(99999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*生产批次不存在*");
    }

    // ========== 更新批次 ==========

    [Fact]
    public async Task UpdateAsync_部分字段_更新成功()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-OLD",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        var updated = await svc.UpdateAsync(created.Id, new UpdateProductionBatchRequest
        {
            TagNo = "TAG-NEW",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            Remark = "备注已更新",
            RowVersion = detail.RowVersion
        });

        updated.TagNo.Should().Be("TAG-NEW");
        updated.Remark.Should().Be("备注已更新");
    }

    [Fact]
    public async Task UpdateAsync_RowVersion冲突_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-CONFLICT",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var act = () => svc.UpdateAsync(created.Id, new UpdateProductionBatchRequest
        {
            TagNo = "TAG-CONFLICT-NEW",
            RowVersion = new byte[8] { 0, 0, 0, 0, 0, 0, 0, 1 } // 与默认的8个0不同
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已被其他用户修改*");
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(99999, new UpdateProductionBatchRequest
        {
            TagNo = "TAG",
            RowVersion = new byte[8] { 0, 0, 0, 0, 0, 0, 0, 1 }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*生产批次不存在*");
    }

    [Fact]
    public async Task UpdateAsync_更新工单字段_成功()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-WO-UPDATE",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        var updated = await svc.UpdateAsync(created.Id, new UpdateProductionBatchRequest
        {
            WorkOrderNo = "WO-MANUAL",
            SalesOrderNo = "SO-MANUAL",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "304",
            SourcePlantGrade = "304",
            Specification = "219*8",
            RowVersion = detail.RowVersion
        });

        updated.WorkOrderNo.Should().Be("WO-MANUAL");
        updated.SalesOrderNo.Should().Be("SO-MANUAL");
        updated.PlantGrade.Should().Be("304");
        updated.Specification.Should().Be("219*8");
    }

    [Fact]
    public async Task UpdateAsync_LengthStatus变更_级联重算生产记录与成检匹配标识()
    {
        var ctx = CreateDbContext();
        var prodMock = new Mock<IProductionRecordService>();
        var fiMock = new Mock<IFinalInspectionService>();
        var svc = CreateService(ctx, prodMock, fiMock);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-CL-MATCH",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        await svc.UpdateAsync(created.Id, new UpdateProductionBatchRequest
        {
            TagNo = "TAG-CL-MATCH",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            LengthStatus = LengthStatus.Fixed, // 上游字段变更 → 触发级联重算
            TotalWeight = 1000m,
            RowVersion = detail.RowVersion
        });

        prodMock.Verify(x => x.RecomputeCutLengthMatchByBatchAsync(created.Id), Times.Once);
        fiMock.Verify(x => x.RecomputeCutLengthMatchByBatchAsync(created.Id), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_无上游字段变更_不触发匹配标识级联()
    {
        var ctx = CreateDbContext();
        var prodMock = new Mock<IProductionRecordService>();
        var fiMock = new Mock<IFinalInspectionService>();
        var svc = CreateService(ctx, prodMock, fiMock);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-CL-NOCHANGE",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        await svc.UpdateAsync(created.Id, new UpdateProductionBatchRequest
        {
            TagNo = "TAG-CL-NOCHANGE-2",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            RowVersion = detail.RowVersion
        });

        prodMock.Verify(x => x.RecomputeCutLengthMatchByBatchAsync(It.IsAny<int>()), Times.Never);
        fiMock.Verify(x => x.RecomputeCutLengthMatchByBatchAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_变更工单号_新旧工单号都刷新执行状况()
    {
        var ctx = CreateDbContext();
        var woExecMock = new Mock<IWorkOrderExecutionService>();
        var svc = CreateService(ctx, null, null, woExecMock);
        var oldWo = await SeedWorkOrderAsync(ctx);
        var newWo = await SeedWorkOrderAsync(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = oldWo,
            TagNo = "TAG-WO-CHANGE",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "GB/T 8163",
            TechnicalRequirements = RequirementType.Normal
        });

        var detail = await svc.GetByIdAsync(created.Id);

        woExecMock.Invocations.Clear();

        await svc.UpdateAsync(created.Id, new UpdateProductionBatchRequest
        {
            WorkOrderNo = newWo,
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            RowVersion = detail.RowVersion
        });

        // 新工单号（当前归属）与旧工单号（投料量已迁出）都必须重算
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains(newWo))), Times.AtLeastOnce);
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains(oldWo))), Times.AtLeastOnce);
    }

    // ========== 更新状态 ==========

    [Fact]
    public async Task UpdateStatusAsync_None到InProgress_成功()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-STATUS",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        await svc.UpdateStatusAsync(created.Id, new UpdateBatchStatusRequest
        {
            Status = BatchStatus.InProgress,
            RowVersion = detail.RowVersion
        });

        var after = await svc.GetByIdAsync(created.Id);
        after.Status.Should().Be(BatchStatus.InProgress);
    }

    [Fact]
    public async Task UpdateStatusAsync_Completed回退_清除强制完成标记()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-ROLLBACK",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        // 先开到在产
        await svc.UpdateStatusAsync(created.Id, new UpdateBatchStatusRequest
        {
            Status = BatchStatus.InProgress,
            RowVersion = detail.RowVersion
        });
        var inProgress = await svc.GetByIdAsync(created.Id);

        // 直接完成
        await svc.UpdateStatusAsync(created.Id, new UpdateBatchStatusRequest
        {
            Status = BatchStatus.Completed,
            RowVersion = inProgress.RowVersion
        });

        var completed = await svc.GetByIdAsync(created.Id);
        completed.Status.Should().Be(BatchStatus.Completed);
        completed.IsForceCompleted.Should().BeTrue();
    }

    // ========== 删除批次 ==========

    [Fact]
    public async Task DeleteAsync_级联删除工序组()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-DEL",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100,
            ProcessGroups = new List<CreateProcessGroupRequest>
            {
                new() { ProcessName = "矫切酸检", ManufacturingSpec = "Φ50×5", ColdRollDraw = 1 }
            }
        });

        await svc.DeleteAsync(created.Id);

        var act = () => svc.GetByIdAsync(created.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*生产批次不存在*");
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(99999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*生产批次不存在*");
    }

    // ========== 工序组 ==========

    [Fact]
    public async Task AddProcessGroupAsync_成功添加()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-ADD-PG",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var pg = await svc.AddProcessGroupAsync(created.Id, new CreateProcessGroupRequest
        {
            ProcessName = "冷拔",
            ManufacturingSpec = "100*10",
            ColdRollDraw = 1,
            Degrease = 2
        });

        pg.Should().NotBeNull();
        pg.SequenceNumber.Should().Be(1);
        pg.ProcessName.Should().Be("冷拔");
        pg.ManufacturingSpec.Should().Be("100*10");
    }

    [Fact]
    public async Task AddProcessGroupAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.AddProcessGroupAsync(99999, new CreateProcessGroupRequest
        {
            ProcessName = "冷拔"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*生产批次不存在*");
    }

    [Fact]
    public async Task DeleteProcessGroupAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-DEL-PG",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100,
            ProcessGroups = new List<CreateProcessGroupRequest>
            {
                new() { ProcessName = "矫切酸检", ManufacturingSpec = "Φ50×5", ColdRollDraw = 1 }
            }
        });

        var groups = await svc.GetProcessGroupsAsync(created.Id);
        groups.Should().HaveCount(1);

        await svc.DeleteProcessGroupAsync(groups[0].Id);

        var after = await svc.GetProcessGroupsAsync(created.Id);
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteProcessGroupAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteProcessGroupAsync(99999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*工序组不存在*");
    }

    // ========== 分页查询 ==========

    [Fact]
    public async Task GetPagedAsync_关键词搜索()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "SEARCH-TAG",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var result = await svc.GetPagedAsync(new BatchQueryParams
        {
            Keyword = "SEARCH-TAG",
            PageIndex = 1,
            PageSize = 20
        });

        result.Items.Should().NotBeEmpty();
        result.Items.Any(i => i.TagNo == "SEARCH-TAG").Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_状态筛选()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "STATUS-FILTER",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        // 改状态为 InProgress
        var detail = await svc.GetByIdAsync(created.Id);
        await svc.UpdateStatusAsync(created.Id, new UpdateBatchStatusRequest
        {
            Status = BatchStatus.InProgress,
            RowVersion = detail.RowVersion
        });

        var result = await svc.GetPagedAsync(new BatchQueryParams
        {
            Status = "InProgress",
            PageIndex = 1,
            PageSize = 20
        });

        result.Items.Should().NotBeEmpty();
        result.Items.All(i => i.Status == BatchStatus.InProgress).Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_排序_默认降序()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "SORT-A",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });
        await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "SORT-B",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var result = await svc.GetPagedAsync(new BatchQueryParams
        {
            PageIndex = 1,
            PageSize = 20
        });

        result.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(2);
    }

    // ========== 编号生成 ==========

    [Fact]
    public async Task GetNextBatchNoAsync_返回YYMM_XXXX格式()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var batchNo = await svc.GetNextBatchNoAsync();

        batchNo.Should().NotBeNullOrEmpty();
        batchNo.Should().Match("*??-????");
    }

    [Fact]
    public async Task GetNextBatchNoAsync_已有批次_序号递增()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var first = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "SEQ-NO-1",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var nextNo = await svc.GetNextBatchNoAsync();

        nextNo.Should().NotBe(first.BatchNo);

        // 解析序号验证递增
        var firstSeq = int.Parse(first.BatchNo[5..9]);
        var nextSeq = int.Parse(nextNo[5..9]);
        nextSeq.Should().BeGreaterThanOrEqualTo(firstSeq);
    }

    // ========== 可用批次 ==========

    [Fact]
    public async Task GetAvailableBatchesAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableBatchesAsync();

        available.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableBatchesAsync_关联工单号_出库工单号优先_空则回退库存批工单号()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var wh = await SeedWarehouseAsync(ctx);
        var ib1 = new InventoryBatch { BatchNo = "CK-IB-1", WarehouseId = wh.Id, MaterialType = MaterialType.OrderFinished.ToString(), PlantGrade = "Q345B", Specification = "219*8", InboundSource = InboundSource.Purchase.ToString(), SourceName = "供应商A", InboundDate = DateTime.Today, InitialQuantity = 10, InitialWeight = 1000m, RemainingQuantity = 10, RemainingWeight = 1000m, WorkOrderNo = "IB-WO-1" };
        var ib2 = new InventoryBatch { BatchNo = "CK-IB-2", WarehouseId = wh.Id, MaterialType = MaterialType.OrderFinished.ToString(), PlantGrade = "Q345B", Specification = "219*8", InboundSource = InboundSource.Purchase.ToString(), SourceName = "供应商A", InboundDate = DateTime.Today, InitialQuantity = 10, InitialWeight = 1000m, RemainingQuantity = 10, RemainingWeight = 1000m, WorkOrderNo = "IB-WO-2" };
        ctx.InventoryBatches.AddRange(ib1, ib2);
        await ctx.SaveChangesAsync();

        // 出库记录1：带出库工单号 → 优先取它
        // 出库记录2：出库工单号为空 → 回退库存批工单号
        ctx.OutboundRecords.AddRange(
            new OutboundRecord { InventoryBatchId = ib1.Id, BatchNo = ib1.BatchNo, OutboundType = OutboundType.ProductionPick, WorkOrderNo = "OUT-WO-1", OutboundQuantity = 5, OutboundWeight = 500m, OutboundDate = DateTime.Today, CreatedBy = "user1" },
            new OutboundRecord { InventoryBatchId = ib2.Id, BatchNo = ib2.BatchNo, OutboundType = OutboundType.ProductionPick, WorkOrderNo = null, OutboundQuantity = 5, OutboundWeight = 500m, OutboundDate = DateTime.Today, CreatedBy = "user1" });
        await ctx.SaveChangesAsync();

        var available = await svc.GetAvailableBatchesAsync();

        var b1 = available.Should().ContainSingle(x => x.BatchNo == "CK-IB-1").Subject;
        b1.WorkOrderNo.Should().Be("OUT-WO-1");
        var b2 = available.Should().ContainSingle(x => x.BatchNo == "CK-IB-2").Subject;
        b2.WorkOrderNo.Should().Be("IB-WO-2");
    }

    // ========== 复制上批次工序组 ==========

    [Fact]
    public async Task GetLastBatchProcessGroupsAsync_无批次_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var groups = await svc.GetLastBatchProcessGroupsAsync();

        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLastBatchProcessGroupsAsync_有批次_返回工序组()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "COPY-SRC",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100,
            ProcessGroups = new List<CreateProcessGroupRequest>
            {
                new() { ProcessName = "矫切酸检", ManufacturingSpec = "Φ50×5", ColdRollDraw = 1, Pickle = 2 },
                new() { ProcessName = "60冷轧", ManufacturingSpec = "Φ50×5", ColdRollDraw = 3 }
            }
        });

        var groups = await svc.GetLastBatchProcessGroupsAsync();

        groups.Should().NotBeEmpty();
        groups.Should().HaveCount(2);
        groups[0].ProcessName.Should().Be("矫切酸检");
        groups[1].ProcessName.Should().Be("60冷轧");
        groups[0].ColdRollDraw.Should().Be(1);
    }

    // ========== 打印 ==========

    [Fact]
    public async Task PrintBatchAsync_成功生成PDF()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "PRINT-TEST",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var pdfBytes = await svc.PrintBatchAsync(created.Id);

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        pdfBytes[0].Should().Be((byte)'%');
        pdfBytes[1].Should().Be((byte)'P');
        pdfBytes[2].Should().Be((byte)'D');
        pdfBytes[3].Should().Be((byte)'F');
    }

    [Fact]
    public async Task PrintBatchAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.PrintBatchAsync(99999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*生产批次不存在*");
    }

    [Fact]
    public async Task PrintBatchAllAsync_成功生成PDF()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "PRINT-ALL",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var pdfBytes = await svc.PrintBatchAllAsync(new BatchPrintAllRequest());

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        pdfBytes[0].Should().Be((byte)'%');
    }

    [Fact]
    public async Task PrintProcessCardAsync_选中批次_成功生成PDF()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "CARD-PRINT",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var pdfBytes = await svc.PrintProcessCardAsync(new ProcessCardPrintRequest
        {
            Ids = new[] { created.Id },
            Columns = new List<ProcessCardColumnDef>
            {
                new() { BlockKey = "BatchInfo", Key = "BatchNo", Label = "生产编号", Visible = true },
                new() { BlockKey = "BatchInfo", Key = "Status", Label = "状态", Visible = true },
                new() { BlockKey = "BatchInfo", Key = "TagNo", Label = "挂牌号", Visible = true }
            }
        });

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        pdfBytes[0].Should().Be((byte)'%');
    }

    [Fact]
    public async Task PrintProcessCardAsync_格式设置配置覆盖请求列_成功生成PDF()
    {
        var ctx = CreateDbContext();
        var pccMock = CreateProcessCardColumnDefinitionServiceMock();
        pccMock.Setup(x => x.GetConfigMapAsync())
            .ReturnsAsync(new Dictionary<string, ProcessCardColumnDefinitionDto>(StringComparer.OrdinalIgnoreCase)
            {
                ["BatchInfo|BatchNo"] = new()
                {
                    BlockKey = "BatchInfo",
                    FieldKey = "BatchNo",
                    Label = "生产编号",
                    Visible = true,
                    RowIndex = 1,
                    ColumnIndex = 1,
                    ColumnWeight = 9
                }
            });
        var svc = CreateService(ctx, pccMock: pccMock);

        var created = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "CARD-CONFIG",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var pdfBytes = await svc.PrintProcessCardAsync(new ProcessCardPrintRequest
        {
            Ids = new[] { created.Id },
            Columns = new List<ProcessCardColumnDef>
            {
                new() { BlockKey = "BatchInfo", Key = "BatchNo", Label = "生产编号", Visible = true },
                new() { BlockKey = "BatchInfo", Key = "TagNo", Label = "挂牌号", Visible = true, ColumnWeight = 9 }
            }
        });

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        // 打印链路必须读取格式设置配置（DB 权威覆盖请求列）
        pccMock.Verify(x => x.GetConfigMapAsync(), Times.Once);
    }

    [Fact]
    public async Task PrintProcessCardAsync_Ids为空_打印全部()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "CARD-ALL",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            LengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = LengthStatus.NonFixed,
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var pdfBytes = await svc.PrintProcessCardAsync(new ProcessCardPrintRequest
        {
            Ids = Array.Empty<int>(),
            Columns = new List<ProcessCardColumnDef>
            {
                new() { BlockKey = "BatchInfo", Key = "BatchNo", Label = "生产编号", Visible = true }
            }
        });

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        pdfBytes[0].Should().Be((byte)'%');
    }

    [Fact]
    public async Task PrintProcessCardAsync_无数据_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.PrintProcessCardAsync(new ProcessCardPrintRequest
        {
            Ids = Array.Empty<int>(),
            Columns = new List<ProcessCardColumnDef>()
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*未找到批次数据*");
    }

    // ========== B10/B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_按当前规格排序_成功()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var b1 = await svc.CreateAsync(new CreateProductionBatchRequest { WorkOrderNo = "非工单", TagNo = "SORT-CS-1", ProductionType = ProductionType.RoughTube, ManufacturingItem = MaterialType.OrderFinished, PlantGrade = "20#", Specification = "219×8", DeliveryState = DeliveryState.SolutionAnnealedAndPickled, ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled, MaterialName = PipeManufacturingType.SeamlessPipe, LengthStatus = LengthStatus.NonFixed, TotalWeight = 1000m, ProductionRatio = 1, SourcePlantGrade = "20#", SourceSpecification = "219×8", SourceLengthStatus = LengthStatus.NonFixed, InputWeight = 1200m, InputQuantity = 100 });
        var b2 = await svc.CreateAsync(new CreateProductionBatchRequest { WorkOrderNo = "非工单", TagNo = "SORT-CS-2", ProductionType = ProductionType.RoughTube, ManufacturingItem = MaterialType.OrderFinished, PlantGrade = "20#", Specification = "219×8", DeliveryState = DeliveryState.SolutionAnnealedAndPickled, ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled, MaterialName = PipeManufacturingType.SeamlessPipe, LengthStatus = LengthStatus.NonFixed, TotalWeight = 1000m, ProductionRatio = 1, SourcePlantGrade = "20#", SourceSpecification = "219×8", SourceLengthStatus = LengthStatus.NonFixed, InputWeight = 1200m, InputQuantity = 100 });

        var entity1 = await ctx.ProductionBatches.FindAsync(b1.Id);
        entity1!.CurrentSpec = "B-Spec";
        var entity2 = await ctx.ProductionBatches.FindAsync(b2.Id);
        entity2!.CurrentSpec = "A-Spec";
        await ctx.SaveChangesAsync();

        var result = await svc.GetPagedAsync(new BatchQueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "currentspec", IsDescending = false });

        result.Items[0].CurrentSpec.Should().Be("A-Spec");
        result.Items[1].CurrentSpec.Should().Be("B-Spec");
    }

    [Fact]
    public async Task GetPagedAsync_按对应规格排序_成功()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var b1 = await svc.CreateAsync(new CreateProductionBatchRequest { WorkOrderNo = "非工单", TagNo = "SORT-CORR-1", ProductionType = ProductionType.RoughTube, ManufacturingItem = MaterialType.OrderFinished, PlantGrade = "20#", Specification = "219×8", DeliveryState = DeliveryState.SolutionAnnealedAndPickled, ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled, MaterialName = PipeManufacturingType.SeamlessPipe, LengthStatus = LengthStatus.NonFixed, TotalWeight = 1000m, ProductionRatio = 1, SourcePlantGrade = "20#", SourceSpecification = "219×8", SourceLengthStatus = LengthStatus.NonFixed, InputWeight = 1200m, InputQuantity = 100 });
        var b2 = await svc.CreateAsync(new CreateProductionBatchRequest { WorkOrderNo = "非工单", TagNo = "SORT-CORR-2", ProductionType = ProductionType.RoughTube, ManufacturingItem = MaterialType.OrderFinished, PlantGrade = "20#", Specification = "219×8", DeliveryState = DeliveryState.SolutionAnnealedAndPickled, ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled, MaterialName = PipeManufacturingType.SeamlessPipe, LengthStatus = LengthStatus.NonFixed, TotalWeight = 1000m, ProductionRatio = 1, SourcePlantGrade = "20#", SourceSpecification = "219×8", SourceLengthStatus = LengthStatus.NonFixed, InputWeight = 1200m, InputQuantity = 100 });

        var entity1 = await ctx.ProductionBatches.FindAsync(b1.Id);
        entity1!.CorrespondingSpec = "B-Corr";
        var entity2 = await ctx.ProductionBatches.FindAsync(b2.Id);
        entity2!.CorrespondingSpec = "A-Corr";
        await ctx.SaveChangesAsync();

        var result = await svc.GetPagedAsync(new BatchQueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "correspondingspec", IsDescending = false });

        result.Items[0].CorrespondingSpec.Should().Be("A-Corr");
        result.Items[1].CorrespondingSpec.Should().Be("B-Corr");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索创建人_返回匹配()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.CreateAsync(new CreateProductionBatchRequest { WorkOrderNo = "非工单", TagNo = "CREATOR-TEST", ProductionType = ProductionType.RoughTube, ManufacturingItem = MaterialType.OrderFinished, PlantGrade = "20#", Specification = "219×8", DeliveryState = DeliveryState.SolutionAnnealedAndPickled, ManufacturingStatus = DeliveryState.SolutionAnnealedAndPickled, MaterialName = PipeManufacturingType.SeamlessPipe, LengthStatus = LengthStatus.NonFixed, TotalWeight = 1000m, ProductionRatio = 1, SourcePlantGrade = "20#", SourceSpecification = "219×8", SourceLengthStatus = LengthStatus.NonFixed, InputWeight = 1200m, InputQuantity = 100 });
        var entity = await ctx.ProductionBatches.FirstAsync(b => b.TagNo == "CREATOR-TEST");
        entity.CreatedBy = "测试创建人";
        await ctx.SaveChangesAsync();

        var result = await svc.GetPagedAsync(new BatchQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "测试创建人" });

        result.Items.Should().HaveCount(1);
        result.Items[0].CreatedBy.Should().Be("测试创建人");
    }

    // ========== 通用筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetPagedAsync_Filters_BatchNo_Contains_返回匹配()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await SeedBatchViaDirectAsync(ctx, batchNo: "2501-0001");
        await SeedBatchViaDirectAsync(ctx, batchNo: "2501-0002");

        var result = await svc.GetPagedAsync(new BatchQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "BatchNo", Operator = "contains", Value = "0001" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].BatchNo.Should().Be("2501-0001");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_WorkOrderNo_In_返回匹配()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await SeedBatchViaDirectAsync(ctx, batchNo: "B001", workOrderNo: "WO-A");
        await SeedBatchViaDirectAsync(ctx, batchNo: "B002", workOrderNo: "WO-B");

        var result = await svc.GetPagedAsync(new BatchQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "WorkOrderNo", Operator = "in", Values = new List<string> { "WO-A" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].WorkOrderNo.Should().Be("WO-A");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_Status_In_返回匹配()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await SeedBatchViaDirectAsync(ctx, batchNo: "B001", status: BatchStatus.None);
        await SeedBatchViaDirectAsync(ctx, batchNo: "B002", status: BatchStatus.InProgress);

        var result = await svc.GetPagedAsync(new BatchQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "Status", Operator = "in", Values = new List<string> { "None" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].BatchNo.Should().Be("B001");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await SeedBatchViaDirectAsync(ctx, batchNo: "B001");

        var result = await svc.GetPagedAsync(new BatchQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "BatchNo", Operator = "contains", Value = "NONEXISTENT" }
            }
        });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await SeedBatchViaDirectAsync(ctx, batchNo: "B001", plantGrade: "304", standardCode: "GB/T 8163");
        await SeedBatchViaDirectAsync(ctx, batchNo: "B002", plantGrade: "316L", standardCode: "GB/T 14976");

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("BatchNo");
        contexts["BatchNo"].Should().BeEquivalentTo(new[] { "B001", "B002" }, opts => opts.WithStrictOrdering());
        contexts.Should().ContainKey("PlantGrade");
        contexts["PlantGrade"].Should().BeEquivalentTo(new[] { "304", "316L" }, opts => opts.WithStrictOrdering());
        contexts.Should().ContainKey("StandardCode");
        contexts["StandardCode"].Should().BeEquivalentTo(new[] { "GB/T 14976", "GB/T 8163" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_各项返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKeys(
            "BatchNo", "TagNo", "WorkOrderNo", "SalesOrderNo",
            "ProductionMainNo", "ProductionSubNo", "CurrentExecDate",
            "CurrentGroupName", "CurrentSectionName", "CurrentEquipmentName",
            "CurrentOutsource", "CurrentSpec", "NextSectionName",
            "CorrespondingSpec", "SignDate", "Salesman", "EndCustomer",
            "DeliveryDate", "StandardCode", "PlantGrade", "Specification", "CreatedBy");
        foreach (var kvp in contexts)
            kvp.Value.Should().BeEmpty($"{kvp.Key} should be empty when no data");
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // TagNo 为 null 的批次
        await SeedBatchViaDirectAsync(ctx, batchNo: "B001", tagNo: null);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["TagNo"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_Salesman从BatchSnapshot读取()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 1. 种子 SalesOrder 含业务员快照字段
        var salesOrder = new SalesOrder
        {
            OrderNumber = "SO-CUSTOM",
            SignDate = new DateTime(2025, 1, 1),
            Status = SalesOrderStatus.Confirmed,
            CustomerName = "测试客户",
            Salesman = "订单业务员",
            EndCustomer = null
        };
        ctx.SalesOrders.Add(salesOrder);
        await ctx.SaveChangesAsync();

        // 2. 种子 ProductionBatch 使用该 SalesOrderNo，Salesman 设不同值（快照）
        await SeedBatchViaDirectAsync(ctx, batchNo: "B001", salesOrderNo: "SO-CUSTOM", salesman: "批次快照业务员");

        var contexts = await svc.GetFilterContextsAsync();

        // 不再从 CustomerProfile 覆盖，返回 ProductionBatch 自身的快照值
        contexts["Salesman"].Should().Contain("批次快照业务员");
        contexts["Salesman"].Should().NotContain("订单业务员");
    }

    // ========== 辅助方法：直接种子（避开 CreateAsync 的复杂校验） ==========

    /// <summary>
    /// 直接插入 ProductionBatch 实体，仅用于筛选/筛选上下文测试。
    /// 所有非可空字段均提供默认值。
    /// </summary>
    private async Task<ProductionBatch> SeedBatchViaDirectAsync(AppDbContext ctx,
        string batchNo = "2501-0001",
        string workOrderNo = "WO-001",
        string salesOrderNo = "SO-001",
        string productionMainNo = "PM-001",
        string manufacturingItem = "OrderFinished",
        string materialName = "不锈钢管",
        string settlementMethod = "Weighing",
        string standardCode = "GB/T 8163",
        string deliveryState = "SolutionAnnealedAndPickled",
        string plantGrade = "304",
        string specification = "48*4",
        string lengthStatus = "Fixed",
        string technicalRequirements = "标准",
        BatchStatus status = BatchStatus.None,
        string salesman = "测试业务员",
        string createdBy = "admin",
        string? tagNo = null)
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = productionMainNo,
            ManufacturingItem = manufacturingItem,
            MaterialName = materialName,
            SettlementMethod = settlementMethod,
            StandardCode = standardCode,
            DeliveryState = deliveryState,
            PlantGrade = plantGrade,
            Specification = specification,
            LengthStatus = lengthStatus,
            TechnicalRequirements = technicalRequirements,
            Status = status,
            Salesman = salesman,
            CreatedBy = createdBy,
            TagNo = tagNo,
            OrderItemIds = "",
            SignDate = new DateTime(2025, 1, 1),
            DeliveryDate = new DateTime(2025, 6, 1),
            ProductionRatio = 1
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    #region 枚举字符串验证 — CreateAsync 入参

    [Fact]
    public async Task CreateAsync_所有枚举字段使用有效英文值_成功()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-ENUM-VALID",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = MaterialType.OrderFinished,
            MaterialName = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.Hard,
            ManufacturingStatus = DeliveryState.Hard,
            LengthStatus = LengthStatus.NonFixed,
            TechnicalRequirements = RequirementType.Normal,
            PlantGrade = "304",
            Specification = "50×5",
            SourcePlantGrade = "304",
            SourceSpecification = "50×5",
            SourceLengthStatus = LengthStatus.NonFixed,
            TotalWeight = 1000m,
            ProductionRatio = 1,
            InputWeight = 1200m,
            InputQuantity = 100,
        });

        result.Should().NotBeNull();

        // 回读验证所有枚举字段值正确存储
        var detail = await svc.GetByIdAsync(result.Id);
        detail.Should().NotBeNull();
        detail!.ProductionType.Should().Be(ProductionType.RoughTube);
        detail.ManufacturingItem.Should().Be(MaterialType.OrderFinished);
        detail.MaterialName.Should().Be("SeamlessPipe");
        detail.SettlementMethod.Should().Be(SettlementMethod.Theoretical);
        detail.DeliveryState.Should().Be(DeliveryState.Hard);
        detail.LengthStatus.Should().Be(LengthStatus.NonFixed);
        detail.TechnicalRequirements.Should().Be("Normal");
    }

    /// <summary>
    /// DTO 枚举字段已使用类型安全的枚举类型，编译器确保传入值合法。
    /// 此测试验证 null 枚举字段（如 ManufacturingItem）仍然会被 Service 层拒绝。
    /// </summary>
    [Fact]
    public async Task CreateAsync_枚举字段为null_被验证拒绝()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "TAG-NULL-REJECT",
            ProductionType = ProductionType.RoughTube,
            ManufacturingItem = null,
            PlantGrade = "304",
            Specification = "50×5",
            SourcePlantGrade = "304",
            SourceSpecification = "50×5",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            InputWeight = 1200m,
            InputQuantity = 100,
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*制造物品不能为空*");
    }

    #endregion

    // ========== 成检到料强制完成通知 ==========

    [Fact]
    public async Task GetForcedCompletedInspectionBatchesAsync_强制完成到料批次_返回且含工单号()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchViaDirectAsync(ctx, batchNo: "B-FC-1", status: BatchStatus.InFinalInspection);
        ctx.MaterialReceiveChecks.Add(new MaterialReceiveCheck
        {
            ProductionBatchId = batch.Id,
            ReceiveDate = DateTime.Today,
            ProcessGroupId = 1,
            ProcessName = "检验",
            SequenceNumber = 2,
            IsForceCompleted = true,
            InspectionType = "FormalInspection"
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetForcedCompletedInspectionBatchesAsync();

        var item = result.Should().ContainSingle().Subject;
        item.BatchId.Should().Be(batch.Id);
        item.BatchNo.Should().Be("B-FC-1");
        item.WorkOrderNo.Should().Be("WO-001");
        item.InspectionType.Should().Be("FormalInspection");
        item.InspectionTypeDisplay.Should().Be("终检");
    }

    [Fact]
    public async Task GetForcedCompletedInspectionBatchesAsync_批次已转完成_通知消失()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchViaDirectAsync(ctx, batchNo: "B-FC-2", status: BatchStatus.Completed);
        ctx.MaterialReceiveChecks.Add(new MaterialReceiveCheck
        {
            ProductionBatchId = batch.Id,
            ReceiveDate = DateTime.Today,
            ProcessGroupId = 1,
            ProcessName = "检验",
            SequenceNumber = 2,
            IsForceCompleted = true,
            InspectionType = "FormalInspection"
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetForcedCompletedInspectionBatchesAsync();

        // 批次已「完成」，不再属于成检阶段 → 通知消失（批次详情页强制完成后的目标状态）
        result.Should().BeEmpty();
    }
}
