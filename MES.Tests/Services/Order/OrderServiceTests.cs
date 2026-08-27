using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Services.Order;
using MES.Tests.Tests;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Order;
using MES.Data.Entities.Warehouse;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Shared;
using MES.Core.Constants;
using WorkOrderEntity = MES.Data.Entities.WorkOrder.WorkOrder;
using WorkOrderExecutionSummaryEntity = MES.Data.Entities.WorkOrder.WorkOrderExecutionSummary;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Interfaces.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using OrderListSummaryEntity = MES.Data.Entities.Order.OrderListSummary;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using QuestPDF.Infrastructure;

namespace MES.Tests.Services;

/// <summary>
/// 订单服务测试：CRUD、状态流转、项次管理、理论重量计算
/// </summary>
public class OrderServiceTests : TestBase
{
    static OrderServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private OrderService CreateService(AppDbContext ctx, INotificationService? notificationMock = null,
        Mock<IWorkOrderExecutionService>? woExecMock = null,
        Mock<IWorkOrderListSummaryRefreshService>? listSummaryMock = null,
        Mock<IPendingDeliveryQueryService>? pendingDeliveryMock = null)
    {
        var loggerMock = new Mock<ILogger<OrderService>>();
        notificationMock ??= Mock.Of<INotificationService>();
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        woExecMock ??= new Mock<IWorkOrderExecutionService>();
        listSummaryMock ??= new Mock<IWorkOrderListSummaryRefreshService>();
        pendingDeliveryMock ??= new Mock<IPendingDeliveryQueryService>();

        // 真实 ServiceProvider 供 OrderService 经 scope 解析执行读模型/待发货缓存服务
        var services = new ServiceCollection();
        services.AddSingleton(woExecMock.Object);
        services.AddSingleton(listSummaryMock.Object);
        services.AddSingleton(pendingDeliveryMock.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new OrderService(ctx, loggerMock.Object, notificationMock, configMock.Object,
            new Mock<IOperationLogService>().Object, new MemoryCache(new MemoryCacheOptions()),
            workOrderService: null, listSummaryService: listSummaryMock.Object, scopeFactory: scopeFactory);
    }

    [Fact]
    public async Task CreateAsync_重复订单号_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = CreateSampleOrderRequest(cust.Id, gm.StandardGrade);
        await svc.CreateAsync(request);
        var act = () => svc.CreateAsync(request);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("订单号已存在");
    }

    [Fact]
    public async Task CreateAsync_客户不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateSalesOrderRequest
        {
            OrderNumber = "ORD-001",
            SignDate = DateTime.Today,
            CustomerId = 999,
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    StandardNo = sr.StandardNo,
                    StandardGrade = gm.StandardGrade,
                    PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
                    OuterDiameter = 219m,
                    WallThickness = 8m,
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

        await act.Should().ThrowAsync<BusinessException>().WithMessage("客户不存在");
    }

    [Fact]
    public async Task CreateAsync_成功创建订单_包含项次()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = CreateSampleOrderRequest(cust.Id, gm.StandardGrade);

        var result = await svc.CreateAsync(request);

        result.Should().NotBeNull();
        result.OrderNumber.Should().Be("ORD-TEST-001");
        result.Status.Should().Be(SalesOrderStatus.Pending);

        // 验证项次
        var detail = await svc.GetByIdAsync(result.Id);
        detail.Items.Should().HaveCount(1);
        detail.Items[0].Sequence.Should().Be(1);
        detail.Items[0].Specification.Should().Be("219*8");
    }

    // ========== 状态流转 ==========

    [Fact]
    public async Task UpdateAsync_Pending转Confirmed_成功()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));

        var updated = await svc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Confirmed,
            RowVersion = new byte[8]
        });

        updated.Status.Should().Be(SalesOrderStatus.Confirmed);
    }

    [Fact]
    public async Task UpdateAsync_订单头客户变更_刷新执行读模型与用料总览与待发货缓存()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        var woExecMock = new Mock<IWorkOrderExecutionService>();
        var listSummaryMock = new Mock<IWorkOrderListSummaryRefreshService>();
        var pendingDeliveryMock = new Mock<IPendingDeliveryQueryService>();
        var svc = CreateService(ctx, woExecMock: woExecMock, listSummaryMock: listSummaryMock, pendingDeliveryMock: pendingDeliveryMock);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));
        await SeedWorkOrderForFinishedAsync(ctx, order.OrderNumber, "WO-ORD-001");

        await svc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            CustomerName = "新客户",
            RowVersion = new byte[8]
        });

        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("WO-ORD-001"))), Times.Once);
        listSummaryMock.Verify(x => x.RefreshBySalesOrderAsync(order.OrderNumber), Times.Once);
        pendingDeliveryMock.Verify(x => x.InvalidateCachesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Confirmed转Pending_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));
        await svc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Confirmed,
            RowVersion = new byte[8]
        });

        var act = () => svc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Pending,
            RowVersion = new byte[8]
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*不允许从*已确认*变更为*待处理*");
    }

    // ========== 删除 ==========

    [Fact]
    public async Task DeleteAsync_成功删除_物理删除订单和项次()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));
        await svc.DeleteAsync(order.Id);

        // 查询应抛出不存在
        var act = () => svc.GetByIdAsync(order.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("订单不存在");
    }

    // ========== 项次管理 ==========

    [Fact]
    public async Task AddItemAsync_项次号重复_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));

        var act = () => svc.AddItemAsync(order.Id, new AddOrderItemRequest
        {
            Sequence = 1, // 已存在
            StandardNo = sr.StandardNo,
            StandardGrade = gm.StandardGrade,
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            OuterDiameter = 273m,
            WallThickness = 10m,
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            Quantity = 5,
            ContractWeight = 2000m,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*项次号*已存在*");
    }

    [Fact]
    public async Task AddItemAsync_成功添加项次()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));

        var newItem = await svc.AddItemAsync(order.Id, new AddOrderItemRequest
        {
            StandardNo = sr.StandardNo,
            StandardGrade = gm.StandardGrade,
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            OuterDiameter = 273m,
            WallThickness = 10m,
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            Quantity = 5,
            ContractWeight = 2000m,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled
        });

        newItem.Sequence.Should().Be(2); // 自动递增
    }

    // ========== 理论重量计算 ==========

    [Fact]
    public async Task CreateAsync_定尺模式_理论重量正确计算()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = CreateSampleOrderRequest(cust.Id, gm.StandardGrade);

        var result = await svc.CreateAsync(request);
        var detail = await svc.GetByIdAsync(result.Id);

        // 219*8 无缝管，密度7.85，长度6m*10支=60m
        // 有效壁厚 = 8 - 0.5*0.5 + 0.5*0.5 = 8
        // 有效外径 = 219 - 0.5*0.5 + 0.5*0.5 = 219
        // 重量 = 7.85 * 3.1416 * 8 * (219-8) * 60 / 1000
        // = 7.85 * 3.1416 * 8 * 211 * 60 / 1000
        // = 7.85 * 3.1416 * 8 * 211 * 0.06
        var expectedWeight = 7.85m * 3.1416m * 8m * (219m - 8m) * 60m / 1000m;

        // Service 层使用 Math.Round(weight, 1)，需匹配精度
        detail.Items[0].TheoreticalWeight.Should().Be(Math.Round(expectedWeight, 1, MidpointRounding.AwayFromZero));
        detail.Items[0].Specification.Should().Be("219*8");
    }

    // ========== 辅助方法 ==========

    [Fact]
    public async Task GetPagedAsync_状态筛选_正确过滤()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));
        await svc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Confirmed,
            RowVersion = new byte[8]
        });
        // UpdateAsync 内部通过 RefreshByOrderIdAsync 已自动创建 OrderListSummary，无需手动添加

        // 只查待处理
        var pendingResult = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 10
        }, orderStatus: "Pending");

        pendingResult.Items.Should().BeEmpty();

        // 只查已确认
        var confirmedResult = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 10
        }, orderStatus: "Confirmed");

        confirmedResult.Items.Should().HaveCount(1);
        confirmedResult.Items[0].Status.Should().Be(SalesOrderStatus.Confirmed);
    }

    // ========== GetIdByOrderNumberAsync ==========

    [Fact]
    public async Task GetIdByOrderNumberAsync_存在_返回Id()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));

        var id = await svc.GetIdByOrderNumberAsync("ORD-TEST-001");

        id.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetIdByOrderNumberAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var id = await svc.GetIdByOrderNumberAsync("NONEXISTENT");

        id.Should().BeNull();
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("订单不存在");
    }

    // ========== UpdateItemAsync ==========

    [Fact]
    public async Task UpdateItemAsync_成功更新项次()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));
        var itemId = (await svc.GetByIdAsync(order.Id)).Items[0].Id;

        var result = await svc.UpdateItemAsync(order.Id, itemId, new UpdateOrderItemRequest
        {
            Sequence = 1,
            StandardNo = sr.StandardNo,
            StandardGrade = gm.StandardGrade,
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            OuterDiameter = 273m,
            WallThickness = 10m,
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            Quantity = 5,
            ContractWeight = 2000m,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled
        });

        result.Specification.Should().Be("273*10");
    }

    [Fact]
    public async Task UpdateItemAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));

        var act = () => svc.UpdateItemAsync(order.Id, 999, new UpdateOrderItemRequest
        {
            Sequence = 1,
            StandardNo = sr.StandardNo,
            StandardGrade = gm.StandardGrade,
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            OuterDiameter = 273m,
            WallThickness = 10m,
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            Quantity = 5,
            ContractWeight = 2000m,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== DeleteItemAsync ==========

    [Fact]
    public async Task DeleteItemAsync_成功删除项次()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));
        var itemId = (await svc.GetByIdAsync(order.Id)).Items[0].Id;

        await svc.DeleteItemAsync(order.Id, itemId);

        var detail = await svc.GetByIdAsync(order.Id);
        detail.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteItemAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));

        var act = () => svc.DeleteItemAsync(order.Id, 999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== SaveAllAsync ==========

    [Fact]
    public async Task SaveAllAsync_添加新项次_成功()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));
        var rowVersion = (await svc.GetByIdAsync(order.Id)).RowVersion;

        var result = await svc.SaveAllAsync(order.Id, new SaveAllOrderRequest
        {
            RowVersion = rowVersion,
            NewItems = new List<OrderItemSaveRequest>
            {
                new()
                {
                    StandardNo = sr.StandardNo,
                    StandardGrade = gm.StandardGrade,
                    PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
                    OuterDiameter = 273m,
                    WallThickness = 10m,
                    OuterDiameterNegative = 0.5m,
                    OuterDiameterPositive = 0.5m,
                    WallThicknessNegative = 0.5m,
                    WallThicknessPositive = 0.5m,
                    LengthStatus = LengthStatus.Fixed,
                    MinLength = 6000m,
                    MaxLength = 6000m,
                    Quantity = 5,
                    ContractWeight = 2000m,
                    DeliveryDate = DateTime.Today.AddMonths(1),
                    SettlementMethod = SettlementMethod.Theoretical,
                    DeliveryState = DeliveryState.SolutionAnnealedAndPickled
                }
            }
        });

        result.Should().NotBeNull();
        result.RowVersion.Should().NotBeNull();

        var detail = await svc.GetByIdAsync(order.Id);
        detail.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveAllAsync_删除所有项次_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));
        var detail = await svc.GetByIdAsync(order.Id);
        var itemId = detail.Items[0].Id;
        var rowVersion = detail.RowVersion;

        var act = () => svc.SaveAllAsync(order.Id, new SaveAllOrderRequest
        {
            RowVersion = rowVersion,
            DeletedItemIds = new List<int> { itemId }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*至少需要包含一个项次*");
    }

    // ========== 辅助方法 ==========

    private CreateSalesOrderRequest CreateSampleOrderRequest(int customerId, string standardGrade)
    {
        return new CreateSalesOrderRequest
        {
            OrderNumber = "ORD-TEST-001",
            SignDate = DateTime.Today,
            CustomerId = customerId,
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    StandardNo = "TEST-STD-NO",
                    StandardGrade = standardGrade,
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
        };
    }

    /// <summary>
    /// 种子一个工单（直接构造实体，仅用于成品聚合测试）
    /// </summary>
    private async Task<WorkOrderEntity> SeedWorkOrderForFinishedAsync(AppDbContext ctx,
        string salesOrderNo, string workOrderNo, string mainNo = "X01", string subNo = "01")
    {
        var wo = new WorkOrderEntity
        {
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = mainNo,
            ProductionSubNo = subNo,
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "测试业务员",
            DeliveryDate = DateTime.Today.AddMonths(1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "TEST-STD-NO",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "Q345B",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            TotalQuantity = 10,
            TotalMeters = 0m,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            Status = WorkOrderStatus.Pending,
            MaterialPlanStatus = MaterialPlanStatus.NotPlanned
        };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();
        return wo;
    }

    /// <summary>
    /// 种子一个成品库存批次
    /// </summary>
    private async Task<InventoryBatch> SeedFinishedInventoryBatchAsync(AppDbContext ctx,
        string workOrderNo, string materialType, decimal initialWeight, decimal remainingWeight,
        string manufacturingStatus = "SolutionAnnealedAndPickled")
    {
        var batch = new InventoryBatch
        {
            BatchNo = $"FG-{Guid.NewGuid():N}"[..20],
            WarehouseId = 1,
            MaterialType = materialType,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = "Production",
            SourceName = "生产入库",
            InboundDate = DateTime.Today,
            LengthStatus = "Fixed",
            InitialQuantity = (int)(initialWeight / 100m),
            InitialWeight = initialWeight,
            UnitWeight = 100m,
            RemainingQuantity = (int)(remainingWeight / 100m),
            RemainingWeight = remainingWeight,
            WorkOrderNo = workOrderNo,
            ManufacturingStatus = manufacturingStatus
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    /// <summary>
    /// 种子一个出库记录
    /// </summary>
    private async Task SeedOutboundRecordAsync(AppDbContext ctx, int inventoryBatchId,
        OutboundType outboundType, decimal outboundWeight)
    {
        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = inventoryBatchId,
            BatchNo = $"FG-OUT-{Guid.NewGuid():N}"[..16],
            OutboundType = outboundType,
            WorkOrderNo = "WO-FG",
            OutboundQuantity = (int)(outboundWeight / 100m),
            OutboundWeight = outboundWeight,
            OutboundDate = DateTime.Today,
            CreatedTime = DateTimeOffset.Now,
            UpdatedTime = DateTimeOffset.Now
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    /// 种子一个工单执行状况摘要（成品聚合测试指定 ScheduleStage）
    /// </summary>
    private async Task SeedExecutionSummaryAsync(AppDbContext ctx, int workOrderId,
        string workOrderNo, string salesOrderNo, int scheduleStage)
    {
        ctx.Set<WorkOrderExecutionSummaryEntity>().Add(new WorkOrderExecutionSummaryEntity
        {
            WorkOrderId = workOrderId,
            WorkOrderNo = workOrderNo,
            Salesman = "测试业务员",
            CustomerName = "测试客户",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SettlementMethod = SettlementMethod.Theoretical.ToString(),
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = "X01",
            MaterialName = "无缝管",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled.ToString(),
            PlantGrade = "Q345B",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed.ToString(),
            ScheduleStage = scheduleStage
        });
        await ctx.SaveChangesAsync();
    }

    // ========== 成品数据聚合（业务完结/入库/出库/库存） ==========

    [Fact]
    public async Task RefreshByOrderIdAsync_聚合成品入库出库库存()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));

        var wo = await SeedWorkOrderForFinishedAsync(ctx, order.OrderNumber, "WO-FG-001", "X01", "01");
        var batch1 = await SeedFinishedInventoryBatchAsync(ctx, wo.WorkOrderNo, InventoryMaterialTypes.OrderFinished, 1000m, 400m);
        // 订成-非交付态（SpecialDeliveryStatus，制造状态≠交货状态）不计入成品
        var batch2 = await SeedFinishedInventoryBatchAsync(ctx, wo.WorkOrderNo, InventoryMaterialTypes.SpecialDeliveryStatus, 2000m, 0m);
        // 非成品批次（在制 Finished）不计入
        await SeedFinishedInventoryBatchAsync(ctx, wo.WorkOrderNo, InventoryMaterialTypes.Finished, 5000m, 5000m);
        // 其他订单的工单成品批次不计入
        await SeedWorkOrderForFinishedAsync(ctx, "OTHER-ORDER", "WO-FG-002", "X01", "02");
        await SeedFinishedInventoryBatchAsync(ctx, "WO-FG-002", InventoryMaterialTypes.OrderFinished, 8000m, 8000m);

        // OrderFinished 批次销售出库 600kg 计入；SpecialDeliveryStatus 批次销售出库 500kg 不计入（批次被排除）
        await SeedOutboundRecordAsync(ctx, batch1.Id, OutboundType.SalesOut, 600m);
        await SeedOutboundRecordAsync(ctx, batch2.Id, OutboundType.SalesOut, 500m);
        // 执行关注=生产执行(3)，业务完结为否
        await SeedExecutionSummaryAsync(ctx, wo.Id, wo.WorkOrderNo, order.OrderNumber, 3);

        await svc.RefreshByOrderIdAsync(order.Id);

        var summary = await ctx.Set<OrderListSummaryEntity>().FirstAsync(s => s.OrderId == order.Id);
        summary.FinishedInboundWeight.Should().Be(1000m);
        summary.FinishedOutboundWeight.Should().Be(600m);
        summary.FinishedStockWeight.Should().Be(400m);
        summary.BusinessCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshByOrderIdAsync_主号完成且库存清零_业务完结()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));

        var wo = await SeedWorkOrderForFinishedAsync(ctx, order.OrderNumber, "WO-FG-003", "X02", "01");
        await SeedFinishedInventoryBatchAsync(ctx, wo.WorkOrderNo, InventoryMaterialTypes.OrderFinished, 1000m, 0m);
        await SeedExecutionSummaryAsync(ctx, wo.Id, wo.WorkOrderNo, order.OrderNumber, 1);

        await svc.RefreshByOrderIdAsync(order.Id);

        var summary = await ctx.Set<OrderListSummaryEntity>().FirstAsync(s => s.OrderId == order.Id);
        summary.FinishedInboundWeight.Should().Be(1000m);
        summary.FinishedStockWeight.Should().Be(0m);
        summary.BusinessCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshByOrderIdAsync_无成品入库_业务完结为否()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, gm.StandardGrade));

        var wo = await SeedWorkOrderForFinishedAsync(ctx, order.OrderNumber, "WO-FG-004", "X03", "01");
        await SeedExecutionSummaryAsync(ctx, wo.Id, wo.WorkOrderNo, order.OrderNumber, 1);

        await svc.RefreshByOrderIdAsync(order.Id);

        var summary = await ctx.Set<OrderListSummaryEntity>().FirstAsync(s => s.OrderId == order.Id);
        summary.FinishedInboundWeight.Should().Be(0m);
        summary.FinishedStockWeight.Should().Be(0m);
        summary.BusinessCompleted.Should().BeFalse();
    }

    // ========== 筛选上下文 ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        ctx.Set<OrderListSummaryEntity>().AddRange(
            new OrderListSummaryEntity
            {
                OrderNumber = "SO001",
                SignDate = DateTime.Today.AddDays(-1),
                CustomerName = "客户A",
                Salesman = "张三",
                Status = SalesOrderStatus.Confirmed,
                CreatedTime = DateTimeOffset.Now,
                UpdatedTime = DateTimeOffset.Now
            },
            new OrderListSummaryEntity
            {
                OrderNumber = "SO002",
                SignDate = DateTime.Today,
                CustomerName = "客户B",
                Salesman = "李四",
                Status = SalesOrderStatus.Confirmed,
                CreatedTime = DateTimeOffset.Now,
                UpdatedTime = DateTimeOffset.Now
            }
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("OrderNumber", "SignDate", "Salesman", "CustomerName");
        result["OrderNumber"].Should().BeEquivalentTo(new[] { "SO001", "SO002" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_各字段返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("OrderNumber", "SignDate", "Salesman", "CustomerName", "EndCustomer", "DeliveryStart", "DeliveryEnd", "LastChangeDate");
        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty($"字段 {kvp.Key} 应返回空列表");
    }

    // ========== 订单接单·出库及现负荷汇总 ==========

    private static OrderListSummaryEntity SeedSummary(int orderId, string orderNo, DateTime signDate, SalesOrderStatus status, int weight = 0, int? scheduleStage = null, decimal stock = 0m)
        => new()
        {
            OrderId = orderId,
            OrderNumber = orderNo,
            SignDate = signDate,
            CustomerName = "客户A",
            Salesman = "张三",
            Status = status,
            TotalContractWeight = weight,
            ScheduleStage = scheduleStage,
            FinishedStockWeight = stock,
            CreatedTime = DateTimeOffset.Now,
            UpdatedTime = DateTimeOffset.Now
        };

    [Fact]
    public async Task GetOrderInOutSummaryAsync_接单量按签订月份汇总_排除取消订单()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Today.Year;
        ctx.Set<OrderListSummaryEntity>().AddRange(
            SeedSummary(1, "SO-01", new DateTime(year, 1, 15), SalesOrderStatus.Confirmed, weight: 1000),
            SeedSummary(2, "SO-02", new DateTime(year, 5, 10), SalesOrderStatus.Confirmed, weight: 2000),
            SeedSummary(3, "SO-03", new DateTime(year, 3, 5), SalesOrderStatus.Cancelled, weight: 500),
            SeedSummary(4, "SO-04", new DateTime(year - 1, 12, 20), SalesOrderStatus.Confirmed, weight: 3000));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOrderInOutSummaryAsync(year);

        result.OrderWeightByMonth[0].Should().Be(1000m);    // 1月
        result.OrderWeightByMonth[4].Should().Be(2000m);    // 5月
        result.OrderWeightByMonth[2].Should().Be(0m);       // 3月 取消订单不计
        result.OrderWeightByMonth.Sum().Should().Be(3000m); // 上年12月签订不在本年范围
    }

    [Fact]
    public async Task GetOrderInOutSummaryAsync_出库量按出库月份汇总_仅成品销售出库()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Today.Year;

        var finished = new InventoryBatch
        {
            BatchNo = "CK-FG-001",
            WarehouseId = 1,
            MaterialType = InventoryMaterialTypes.OrderFinished,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = "成检入库",
            SourceName = "测试",
            InboundDate = new DateTime(year, 1, 1),
            InitialQuantity = 100,
            InitialWeight = 10000m,
            RemainingQuantity = 100,
            RemainingWeight = 10000m
        };
        var nonFinished = new InventoryBatch
        {
            BatchNo = "CK-HT-001",
            WarehouseId = 1,
            MaterialType = InventoryMaterialTypes.RoughTube,
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = "荒管入库",
            SourceName = "测试",
            InboundDate = new DateTime(year, 1, 1),
            InitialQuantity = 100,
            InitialWeight = 10000m,
            RemainingQuantity = 100,
            RemainingWeight = 10000m
        };
        ctx.InventoryBatches.AddRange(finished, nonFinished);
        await ctx.SaveChangesAsync();

        ctx.OutboundRecords.AddRange(
            new OutboundRecord
            {
                InventoryBatchId = finished.Id,
                BatchNo = finished.BatchNo,
                OutboundType = OutboundType.SalesOut,
                OutboundDate = new DateTime(year, 2, 10),
                OutboundQuantity = 1,
                OutboundWeight = 100m,
                CreatedTime = DateTimeOffset.Now,
                UpdatedTime = DateTimeOffset.Now
            },
            new OutboundRecord
            {
                InventoryBatchId = finished.Id,
                BatchNo = finished.BatchNo,
                OutboundType = OutboundType.SalesOut,
                OutboundDate = new DateTime(year, 8, 20),
                OutboundQuantity = 2,
                OutboundWeight = 250m,
                CreatedTime = DateTimeOffset.Now,
                UpdatedTime = DateTimeOffset.Now
            },
            new OutboundRecord
            {
                InventoryBatchId = nonFinished.Id,
                BatchNo = nonFinished.BatchNo,
                OutboundType = OutboundType.SalesOut,
                OutboundDate = new DateTime(year, 3, 15),
                OutboundQuantity = 1,
                OutboundWeight = 999m,
                CreatedTime = DateTimeOffset.Now,
                UpdatedTime = DateTimeOffset.Now
            });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOrderInOutSummaryAsync(year);

        result.OutboundWeightByMonth[1].Should().Be(100m);   // 2月
        result.OutboundWeightByMonth[7].Should().Be(250m);   // 8月
        result.OutboundWeightByMonth[2].Should().Be(0m);     // 3月 非成品批次不计
        result.OutboundWeightByMonth.Sum().Should().Be(350m);
    }

    [Fact]
    public async Task GetOrderInOutSummaryAsync_库存按执行关注分档_所有年份_并算周转总量()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Today.Year;
        ctx.Set<OrderListSummaryEntity>().AddRange(
            SeedSummary(1, "SO-01", new DateTime(year, 2, 1), SalesOrderStatus.Confirmed, weight: 8000, scheduleStage: 1, stock: 300m),      // 完工：库存300
            SeedSummary(2, "SO-02", new DateTime(year, 3, 1), SalesOrderStatus.Confirmed, weight: 6000, scheduleStage: 3, stock: 500m),      // 未完工
            SeedSummary(3, "SO-03", new DateTime(year, 4, 1), SalesOrderStatus.Confirmed, weight: 2000, scheduleStage: null, stock: 200m),   // 未完工（未排产）
            SeedSummary(4, "SO-04", new DateTime(year - 1, 11, 1), SalesOrderStatus.Confirmed, weight: 3000, scheduleStage: 3, stock: 100m), // 上年未完工：全年份计入
            SeedSummary(5, "SO-05", new DateTime(year, 5, 1), SalesOrderStatus.Cancelled, weight: 999, scheduleStage: 3, stock: 999m));      // 已取消：不计入
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOrderInOutSummaryAsync(year);

        result.FinishedStockCompleted.Should().Be(300m);      // 完工库存仅 SO-01
        result.FinishedStockUncompleted.Should().Be(800m);    // 500 + 200 + 100（含上年，全年份口径）
        // 周转总量 = 未完工订单合同重量(6000+2000+3000) − 未完工库存(800)
        result.TurnoverTotal.Should().Be(10200m);
    }

    // ========== 订单交期预估（GetDeliveryEstimateAsync，2026-08-23） ==========

    private static OrderListSummaryEntity SeedDeliverySummary(int orderId, string orderNo, int weight, int? scheduleStage, DateTime? deliveryEnd, DateTime? estimated, bool hasDelayPenalty = false)
        => new()
        {
            OrderId = orderId,
            OrderNumber = orderNo,
            SignDate = DateTime.Today.AddDays(-30),
            CustomerName = "客户A",
            Salesman = "张三",
            Status = SalesOrderStatus.Confirmed,
            TotalContractWeight = weight,
            ScheduleStage = scheduleStage,
            DeliveryEnd = deliveryEnd,
            EstimatedCompletionDate = estimated,
            HasDelayPenalty = hasDelayPenalty,
            CreatedTime = DateTimeOffset.Now,
            UpdatedTime = DateTimeOffset.Now
        };

    [Fact]
    public async Task GetDeliveryEstimateAsync_延期非延期按桶归集_单数按订单号()
    {
        var ctx = CreateDbContext();
        var today = DateTime.Today;
        ctx.Set<OrderListSummaryEntity>().AddRange(
            // 订单A：延期（预计完成 today+10 > 交期 today+3），延期罚款=是（急中急）
            SeedDeliverySummary(1, "SO-01", 1000, 3, today.AddDays(3), today.AddDays(10), hasDelayPenalty: true),
            // 订单B：非延期（预计完成 today+3 <= 交期 today+10）
            SeedDeliverySummary(2, "SO-02", 2000, 3, today.AddDays(10), today.AddDays(3)),
            // 订单C：延期（预计完成 today+5 > 交期 today-1，交期已过 → 桶0）
            SeedDeliverySummary(3, "SO-03", 3000, 2, today.AddDays(-1), today.AddDays(5)),
            // 排除：未排产 / 无预计完成 / 已取消
            SeedDeliverySummary(4, "SO-04", 999, null, today.AddDays(3), today.AddDays(10)),
            SeedDeliverySummary(5, "SO-05", 999, 3, today.AddDays(3), null),
            new OrderListSummaryEntity
            {
                OrderId = 6, OrderNumber = "SO-06", SignDate = today.AddDays(-30), CustomerName = "客户A", Salesman = "张三",
                Status = SalesOrderStatus.Cancelled, TotalContractWeight = 999, ScheduleStage = 3,
                DeliveryEnd = today.AddDays(3), EstimatedCompletionDate = today.AddDays(10),
                CreatedTime = DateTimeOffset.Now, UpdatedTime = DateTimeOffset.Now
            });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetDeliveryEstimateAsync();

        result.Tables.Should().HaveCount(2);
        result.Tables[0].Name.Should().Be("订单(整单)完成预估");
        result.Tables[0].BucketLabels[0].Should().Be($"≤{today:yy/M/d}");
        result.Tables[0].BucketLabels[1].Should().Be($"{today.AddDays(1):yy/M/d}-{today.AddDays(7):yy/M/d}");
        result.Tables[0].BucketLabels[3].Should().Be($"{today.AddDays(16):yy/M/d}-{today.AddDays(30):yy/M/d}");
        result.Tables[0].BucketLabels[6].Should().Be($"≥{today.AddDays(61):yy/M/d}");
        result.Tables[1].Name.Should().Be("风险-已延期订单(整单)");
        result.Tables[1].BucketLabels.Should().BeEquivalentTo(result.Tables[0].BucketLabels);

        // 表1 订单完成预估：延期按预计完成、非延期按交期
        // 桶1（今日+7）：订单C（预计完成 today+5）→ 1单/3.0吨
        result.Tables[0].Buckets[1].Count.Should().Be(1);
        result.Tables[0].Buckets[1].Weight.Should().Be(3.0m);
        // 桶2（今日+15）：订单A（预计完成 today+10）+ 订单B（交期 today+10）→ 2单/3.0吨
        result.Tables[0].Buckets[2].Count.Should().Be(2);
        result.Tables[0].Buckets[2].Weight.Should().Be(3.0m);
        // 其余桶为空
        result.Tables[0].Buckets.Sum(b => b.Count).Should().Be(3);
        result.Tables[0].Buckets[0].Count.Should().Be(0);
        result.Tables[0].Buckets[3].Count.Should().Be(0);
        result.Tables[0].Buckets[4].Count.Should().Be(0);
        result.Tables[0].Buckets[5].Count.Should().Be(0);
        result.Tables[0].Buckets[6].Count.Should().Be(0);

        // 表2 延期交货订单预估：延期订单按交期
        // 桶0（交期截止-今日）：订单C（交期 today-1）→ 1单/3.0吨
        result.Tables[1].Buckets[0].Count.Should().Be(1);
        result.Tables[1].Buckets[0].Weight.Should().Be(3.0m);
        // 桶1（今日+7）：订单A（交期 today+3）→ 1单/1.0吨
        result.Tables[1].Buckets[1].Count.Should().Be(1);
        result.Tables[1].Buckets[1].Weight.Should().Be(1.0m);
        result.Tables[1].Buckets.Sum(b => b.Count).Should().Be(2);

        // 表2 急中急子集（延期罚款=是）：桶0 订单C 无延期罚款 → 0；桶1 订单A 有延期罚款 → 1单/1.0吨
        result.Tables[1].Buckets[0].UrgentCount.Should().Be(0);
        result.Tables[1].Buckets[0].UrgentWeight.Should().Be(0m);
        result.Tables[1].Buckets[1].UrgentCount.Should().Be(1);
        result.Tables[1].Buckets[1].UrgentWeight.Should().Be(1.0m);
        result.Tables[1].Buckets.Sum(b => b.UrgentCount).Should().Be(1);
        // 表1 完成预估不统计急中急（恒 0）
        result.Tables[0].Buckets.Sum(b => b.UrgentCount).Should().Be(0);
    }

    [Fact]
    public async Task GetDeliveryEstimateAsync_远日量桶_及无数据返回空表()
    {
        var ctx = CreateDbContext();
        var today = DateTime.Today;
        ctx.Set<OrderListSummaryEntity>().AddRange(
            // 订单G：非延期（预计完成 = 交期 = today+100）→ 表1 按交期归桶6（>45日）
            SeedDeliverySummary(1, "SO-01", 4000, 3, today.AddDays(100), today.AddDays(100)),
            // 订单H：延期（预计完成 today+100 > 交期 today+80）→ 表2 按交期归桶6、表1 按预计完成归桶6
            SeedDeliverySummary(2, "SO-02", 5000, 4, today.AddDays(80), today.AddDays(100)));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetDeliveryEstimateAsync();

        result.Tables[0].Buckets[6].Count.Should().Be(2);      // 完成预估：G+H
        result.Tables[0].Buckets[6].Weight.Should().Be(9.0m);
        result.Tables[1].Buckets[6].Count.Should().Be(1);      // 延期预估：仅 H（G 非延期不计）
        result.Tables[1].Buckets[6].Weight.Should().Be(5.0m);
    }

    [Fact]
    public async Task GetDeliveryEstimateAsync_全部未排产_两表全空()
    {
        var ctx = CreateDbContext();
        ctx.Set<OrderListSummaryEntity>().AddRange(
            SeedDeliverySummary(1, "SO-01", 1000, null, DateTime.Today.AddDays(3), DateTime.Today.AddDays(10)),
            // scheduleStage=1（主号完成）也不纳入延期统计（与现负荷 ScheduleStage>=2 口径一致）
            SeedDeliverySummary(2, "SO-02", 2000, 1, DateTime.Today.AddDays(3), DateTime.Today.AddDays(10)));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetDeliveryEstimateAsync();

        result.Tables.Should().HaveCount(2);
        result.Tables[0].Buckets.Sum(b => b.Count).Should().Be(0);
        result.Tables[1].Buckets.Sum(b => b.Count).Should().Be(0);
    }

    // ========== 小表点击联动筛选（GetPagedAsync + estimateFilter，2026-08-26） ==========

    [Fact]
    public async Task GetPagedAsync_表2联动_筛出延期订单按交期截止()
    {
        var ctx = CreateDbContext();
        var today = DateTime.Today;
        ctx.Set<OrderListSummaryEntity>().AddRange(
            // 延期，交期 today+3（桶1 +1~+7）
            SeedDeliverySummary(1, "SO-01", 1000, 3, today.AddDays(3), today.AddDays(10)),
            // 非延期：表2 不计
            SeedDeliverySummary(2, "SO-02", 2000, 3, today.AddDays(10), today.AddDays(3)),
            // 延期，交期 today-1（桶0 ≤今日）
            SeedDeliverySummary(3, "SO-03", 3000, 2, today.AddDays(-1), today.AddDays(5)),
            // 排除：未排产 / 无交期 / 已取消
            SeedDeliverySummary(4, "SO-04", 999, null, today.AddDays(3), today.AddDays(10)),
            SeedDeliverySummary(5, "SO-05", 999, 3, null, today.AddDays(10)));
        ctx.Set<OrderListSummaryEntity>().Add(new OrderListSummaryEntity
        {
            OrderId = 6, OrderNumber = "SO-06", SignDate = today.AddDays(-30), CustomerName = "客户A", Salesman = "张三",
            Status = SalesOrderStatus.Cancelled, TotalContractWeight = 999, ScheduleStage = 3,
            DeliveryEnd = today.AddDays(-1), EstimatedCompletionDate = today.AddDays(5),
            CreatedTime = DateTimeOffset.Now, UpdatedTime = DateTimeOffset.Now
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        // 桶0（≤今日）：交期截止 ≤ today 的延期订单 → 仅 SO-03
        var bucket0 = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 10 },
            estimateFilter: new OrderDeliveryEstimateFilterDto { Table = "delay", DateTo = today });
        bucket0.TotalCount.Should().Be(1);
        bucket0.Items.Should().ContainSingle().Which.OrderNumber.Should().Be("SO-03");

        // 桶1（+1~+7）：交期截止 today+1..today+7 的延期订单 → 仅 SO-01
        var bucket1 = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 10 },
            estimateFilter: new OrderDeliveryEstimateFilterDto { Table = "delay", DateFrom = today.AddDays(1), DateTo = today.AddDays(7) });
        bucket1.TotalCount.Should().Be(1);
        bucket1.Items.Should().ContainSingle().Which.OrderNumber.Should().Be("SO-01");
    }

    [Fact]
    public async Task GetPagedAsync_表1联动_延期按预计完成或非延期按交期双口径()
    {
        var ctx = CreateDbContext();
        var today = DateTime.Today;
        ctx.Set<OrderListSummaryEntity>().AddRange(
            // 延期（预计 today+3 > 交期 today-5）：按预计完成归桶 → 桶1
            SeedDeliverySummary(1, "SO-01", 1000, 3, today.AddDays(-5), today.AddDays(3)),
            // 非延期（预计 today+1 <= 交期 today+2）：按交期归桶 → 桶1
            SeedDeliverySummary(2, "SO-02", 2000, 3, today.AddDays(2), today.AddDays(1)),
            // 非延期（预计=交期 today+10）：按交期归桶 → 桶2
            SeedDeliverySummary(3, "SO-03", 3000, 3, today.AddDays(10), today.AddDays(10)),
            // 延期（预计 today-1 > 交期 today-10）：按预计完成归桶 → 桶0
            SeedDeliverySummary(4, "SO-04", 4000, 2, today.AddDays(-10), today.AddDays(-1)));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        // 桶0（≤今日）：延期按预计完成 ≤ today → 仅 SO-04（非延期无交期 ≤ today 的）
        var bucket0 = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 10 },
            estimateFilter: new OrderDeliveryEstimateFilterDto { Table = "complete", DateTo = today });
        bucket0.TotalCount.Should().Be(1);
        bucket0.Items.Should().ContainSingle().Which.OrderNumber.Should().Be("SO-04");

        // 桶1（+1~+7）：延期按预计完成（SO-01）+ 非延期按交期（SO-02）→ 2 单
        var bucket1 = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 10 },
            estimateFilter: new OrderDeliveryEstimateFilterDto { Table = "complete", DateFrom = today.AddDays(1), DateTo = today.AddDays(7) });
        bucket1.TotalCount.Should().Be(2);
        bucket1.Items.Select(i => i.OrderNumber).Should().BeEquivalentTo(new[] { "SO-01", "SO-02" });
    }

    [Fact]
    public async Task GetDeliveryEstimateAsync_桶边界结构化与标签一致()
    {
        var ctx = CreateDbContext();
        var today = DateTime.Today;
        ctx.Set<OrderListSummaryEntity>().AddRange(
            SeedDeliverySummary(1, "SO-01", 1000, 3, today.AddDays(3), today.AddDays(10)));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetDeliveryEstimateAsync();

        // 桶0：无下界、上界=今日；桶1：+1~+7（默认配置 bucket1=7）；桶6：+61 起、无上界
        result.Tables[0].Id.Should().Be("complete");
        result.Tables[1].Id.Should().Be("delay");
        result.Tables[0].Buckets[0].DateFrom.Should().BeNull();
        result.Tables[0].Buckets[0].DateTo.Should().Be(today);
        result.Tables[0].Buckets[1].DateFrom.Should().Be(today.AddDays(1));
        result.Tables[0].Buckets[1].DateTo.Should().Be(today.AddDays(7));
        result.Tables[0].Buckets[2].DateFrom.Should().Be(today.AddDays(8));
        result.Tables[0].Buckets[2].DateTo.Should().Be(today.AddDays(15));
        result.Tables[0].Buckets[6].DateFrom.Should().Be(today.AddDays(61));
        result.Tables[0].Buckets[6].DateTo.Should().BeNull();
        // 两表桶边界一致
        for (var i = 0; i < 7; i++)
        {
            result.Tables[1].Buckets[i].DateFrom.Should().Be(result.Tables[0].Buckets[i].DateFrom);
            result.Tables[1].Buckets[i].DateTo.Should().Be(result.Tables[0].Buckets[i].DateTo);
        }
    }

    [Fact]
    public async Task PrintOrderListAsync_生成列表PDF()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var items = new List<Dictionary<string, object>>
        {
            new() { ["ordernumber"] = "SO001", ["customername"] = "客户A", ["TotalContractWeight"] = 123.45m },
            new() { ["ordernumber"] = "SO002", ["customername"] = "客户B", ["TotalContractWeight"] = 200m }
        };
        var columns = new List<PrintColumnDef>
        {
            new() { Key = "ordernumber", Label = "订单号" },
            new() { Key = "customername", Label = "客户名称" },
            new() { Key = "TotalContractWeight", Label = "订单总重量" }
        };

        var bytes = await svc.PrintOrderListAsync("订单列表", items, columns);

        bytes.Should().NotBeNullOrEmpty();
        // PDF 魔数 %PDF
        System.Text.Encoding.ASCII.GetString(bytes.Take(4).ToArray()).Should().Be("%PDF");
    }
}
