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
using MES.Core.Constants;
using WorkOrderEntity = MES.Data.Entities.WorkOrder.WorkOrder;
using WorkOrderExecutionSummaryEntity = MES.Data.Entities.WorkOrder.WorkOrderExecutionSummary;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Interfaces.Infrastructure;
using OrderListSummaryEntity = MES.Data.Entities.Order.OrderListSummary;
using Moq;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 订单服务测试：CRUD、状态流转、项次管理、理论重量计算
/// </summary>
public class OrderServiceTests : TestBase
{
    private OrderService CreateService(AppDbContext ctx, INotificationService? notificationMock = null)
    {
        var loggerMock = new Mock<ILogger<OrderService>>();
        notificationMock ??= Mock.Of<INotificationService>();
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        return new OrderService(ctx, loggerMock.Object, notificationMock, configMock.Object, new Mock<IOperationLogService>().Object, new MemoryCache(new MemoryCacheOptions()));
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
}
