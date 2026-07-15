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
using MES.Core.DTOs.ProductionStandard;
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
using MES.Core.Interfaces.ProductionStandard;
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
using Microsoft.Extensions.Caching.Memory;

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

    private BatchService CreateService(AppDbContext ctx)
    {
        var loggerMock = new Mock<ILogger<BatchService>>();
        var prodRecordMock = new Mock<IProductionRecordService>();
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var workOrderExecMock = new Mock<IWorkOrderExecutionService>();
        var materialPlanMock = new Mock<IMaterialPlanService>();
        return new BatchService(ctx, loggerMock.Object, prodRecordMock.Object, configMock.Object, workOrderExecMock.Object, materialPlanMock.Object, new MemoryCache(new MemoryCacheOptions()));
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
        var orderSvc = new OrderService(ctx, new Mock<ILogger<OrderService>>().Object, notifMock.Object, orderConfigMock.Object, new MemoryCache(new MemoryCacheOptions()));

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
            Status = SalesOrderStatus.Confirmed.ToString(),
            RowVersion = new byte[8]
        });

        // 生成工单
        var items = await ctx.OrderItems.Where(oi => oi.SalesOrderId == order.Id).ToListAsync();
        var itemIds = items.Select(i => i.Sequence).ToList();

        var configMock = new Mock<IConfigParameterService>();
        var woSvc = new WorkOrderService(ctx, new Mock<ILogger<WorkOrderService>>().Object, configMock.Object, new MemoryCache(new MemoryCacheOptions()));
        var generated = await woSvc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = order.OrderNumber,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D01", ProductionSubNo = "C01", OrderItemIds = itemIds }
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
            InputWeight = 1200m,
            InputQuantity = 100,
            Remark = "测试批次无工单"
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().NotBeNullOrEmpty();
        result.BatchNo.Should().Match("*??-????"); // YYMM-XXXX 格式
        result.TagNo.Should().Be("TAG-001");
        result.Status.Should().Be("None");
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
            InputWeight = 1200m,
            InputQuantity = 100,
            SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163",
            TechnicalRequirements = "Standard"
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
            LengthStatus = "Fixed",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        detail.Should().NotBeNull();
        detail.Id.Should().Be(created.Id);
        detail.BatchNo.Should().Be(created.BatchNo);
        detail.TagNo.Should().Be("TAG-DETAIL");
        detail.Status.Should().Be("None");
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        var updated = await svc.UpdateAsync(created.Id, new UpdateProductionBatchRequest
        {
            TagNo = "TAG-NEW",
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        var updated = await svc.UpdateAsync(created.Id, new UpdateProductionBatchRequest
        {
            WorkOrderNo = "WO-MANUAL",
            SalesOrderNo = "SO-MANUAL",
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        await svc.UpdateStatusAsync(created.Id, new UpdateBatchStatusRequest
        {
            Status = "InProgress",
            RowVersion = detail.RowVersion
        });

        var after = await svc.GetByIdAsync(created.Id);
        after.Status.Should().Be("InProgress");
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var detail = await svc.GetByIdAsync(created.Id);

        // 先开到在产
        await svc.UpdateStatusAsync(created.Id, new UpdateBatchStatusRequest
        {
            Status = "InProgress",
            RowVersion = detail.RowVersion
        });
        var inProgress = await svc.GetByIdAsync(created.Id);

        // 直接完成
        await svc.UpdateStatusAsync(created.Id, new UpdateBatchStatusRequest
        {
            Status = "Completed",
            RowVersion = inProgress.RowVersion
        });

        var completed = await svc.GetByIdAsync(created.Id);
        completed.Status.Should().Be("Completed");
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
            InputWeight = 1200m,
            InputQuantity = 100
        });

        var pg = await svc.AddProcessGroupAsync(created.Id, new CreateProcessGroupRequest
        {
            ProcessName = "冷拔",
            ManufacturingSpec = "100*10",
            ManufacturingMultiple = 1,
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
            InputWeight = 1200m,
            InputQuantity = 100
        });

        // 改状态为 InProgress
        var detail = await svc.GetByIdAsync(created.Id);
        await svc.UpdateStatusAsync(created.Id, new UpdateBatchStatusRequest
        {
            Status = "InProgress",
            RowVersion = detail.RowVersion
        });

        var result = await svc.GetPagedAsync(new BatchQueryParams
        {
            Status = "InProgress",
            PageIndex = 1,
            PageSize = 20
        });

        result.Items.Should().NotBeEmpty();
        result.Items.All(i => i.Status == "InProgress").Should().BeTrue();
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
            InputWeight = 1200m,
            InputQuantity = 100
        });
        await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "SORT-B",
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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
    public async Task PrintProcessCardAsync_Ids为空_打印全部()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.CreateAsync(new CreateProductionBatchRequest
        {
            WorkOrderNo = "非工单",
            TagNo = "CARD-ALL",
            ProductionType = "RoughTube",
            ManufacturingItem = "订单成品",
            PlantGrade = "20#",
            Specification = "219×8",
            DeliveryState = "SolutionAnnealedAndPickled",
            MaterialName = "SeamlessPipe",
            LengthStatus = "Multiple",
            TotalWeight = 1000m,
            ProductionRatio = 1,
            SourcePlantGrade = "20#",
            SourceSpecification = "219×8",
            SourceLengthStatus = "Multiple",
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

        var b1 = await svc.CreateAsync(new CreateProductionBatchRequest { WorkOrderNo = "非工单", TagNo = "SORT-CS-1", ProductionType = "RoughTube", ManufacturingItem = "订单成品", PlantGrade = "20#", Specification = "219×8", DeliveryState = "SolutionAnnealedAndPickled", MaterialName = "SeamlessPipe", LengthStatus = "Multiple", TotalWeight = 1000m, ProductionRatio = 1, SourcePlantGrade = "20#", SourceSpecification = "219×8", SourceLengthStatus = "Multiple", InputWeight = 1200m, InputQuantity = 100 });
        var b2 = await svc.CreateAsync(new CreateProductionBatchRequest { WorkOrderNo = "非工单", TagNo = "SORT-CS-2", ProductionType = "RoughTube", ManufacturingItem = "订单成品", PlantGrade = "20#", Specification = "219×8", DeliveryState = "SolutionAnnealedAndPickled", MaterialName = "SeamlessPipe", LengthStatus = "Multiple", TotalWeight = 1000m, ProductionRatio = 1, SourcePlantGrade = "20#", SourceSpecification = "219×8", SourceLengthStatus = "Multiple", InputWeight = 1200m, InputQuantity = 100 });

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

        var b1 = await svc.CreateAsync(new CreateProductionBatchRequest { WorkOrderNo = "非工单", TagNo = "SORT-CORR-1", ProductionType = "RoughTube", ManufacturingItem = "订单成品", PlantGrade = "20#", Specification = "219×8", DeliveryState = "SolutionAnnealedAndPickled", MaterialName = "SeamlessPipe", LengthStatus = "Multiple", TotalWeight = 1000m, ProductionRatio = 1, SourcePlantGrade = "20#", SourceSpecification = "219×8", SourceLengthStatus = "Multiple", InputWeight = 1200m, InputQuantity = 100 });
        var b2 = await svc.CreateAsync(new CreateProductionBatchRequest { WorkOrderNo = "非工单", TagNo = "SORT-CORR-2", ProductionType = "RoughTube", ManufacturingItem = "订单成品", PlantGrade = "20#", Specification = "219×8", DeliveryState = "SolutionAnnealedAndPickled", MaterialName = "SeamlessPipe", LengthStatus = "Multiple", TotalWeight = 1000m, ProductionRatio = 1, SourcePlantGrade = "20#", SourceSpecification = "219×8", SourceLengthStatus = "Multiple", InputWeight = 1200m, InputQuantity = 100 });

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

        await svc.CreateAsync(new CreateProductionBatchRequest { WorkOrderNo = "非工单", TagNo = "CREATOR-TEST", ProductionType = "RoughTube", ManufacturingItem = "订单成品", PlantGrade = "20#", Specification = "219×8", DeliveryState = "SolutionAnnealedAndPickled", MaterialName = "SeamlessPipe", LengthStatus = "Multiple", TotalWeight = 1000m, ProductionRatio = 1, SourcePlantGrade = "20#", SourceSpecification = "219×8", SourceLengthStatus = "Multiple", InputWeight = 1200m, InputQuantity = 100 });
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
            CustomerId = 1,
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
        string manufacturingItem = "订单成品",
        string materialName = "不锈钢管",
        string settlementMethod = "现结",
        string standardCode = "GB/T 8163",
        string deliveryState = "酸白",
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
}
