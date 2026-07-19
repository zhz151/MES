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
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.WorkOrder;
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
        return new OrderService(ctx, loggerMock.Object, notificationMock, configMock.Object, new MemoryCache(new MemoryCacheOptions()));
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
            Status = SalesOrderStatus.Confirmed.ToString(),
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
            Status = SalesOrderStatus.Confirmed.ToString(),
            RowVersion = new byte[8]
        });

        var act = () => svc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Pending.ToString(),
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
            Status = SalesOrderStatus.Confirmed.ToString(),
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
}
