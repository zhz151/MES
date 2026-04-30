using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Services.Order;
using MES.Tests.Tests;
using MES.Data;
using Moq;

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
        return new OrderService(ctx, loggerMock.Object, notificationMock);
    }

    // ========== 创建订单 ==========

    [Fact]
    public async Task CreateAsync_重复订单号_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = CreateSampleOrderRequest(cust.Id, ps.Id, gm.StandardGrade);
        await svc.CreateAsync(request);
        var act = () => svc.CreateAsync(request);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("订单号已存在");
    }

    [Fact]
    public async Task CreateAsync_客户不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var ps = await SeedStandardAsync(ctx);
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
                    ProductionStandardId = ps.Id,
                    StandardGrade = gm.StandardGrade,
                    MaterialName = MaterialName.SeamlessPipe,
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
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = CreateSampleOrderRequest(cust.Id, ps.Id, gm.StandardGrade);

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
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, ps.Id, gm.StandardGrade));

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
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, ps.Id, gm.StandardGrade));
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

    [Fact]
    public async Task UpdateAsync_取消的订单不能修改()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, ps.Id, gm.StandardGrade));
        await svc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Cancelled.ToString(),
            RowVersion = new byte[8]
        });

        var act = () => svc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            OrderNumber = "SHOULD-FAIL",
            RowVersion = new byte[8]
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("已取消的订单不能修改");
    }

    // ========== 删除 ==========

    [Fact]
    public async Task DeleteAsync_取消的订单不能删除()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, ps.Id, gm.StandardGrade));
        await svc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Cancelled.ToString(),
            RowVersion = new byte[8]
        });

        var act = () => svc.DeleteAsync(order.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("已取消的订单不能删除");
    }

    [Fact]
    public async Task DeleteAsync_成功删除_软删除订单和项次()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, ps.Id, gm.StandardGrade));
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
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, ps.Id, gm.StandardGrade));

        var act = () => svc.AddItemAsync(order.Id, new AddOrderItemRequest
        {
            Sequence = 1, // 已存在
            ProductionStandardId = ps.Id,
            StandardGrade = gm.StandardGrade,
            MaterialName = MaterialName.SeamlessPipe,
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
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, ps.Id, gm.StandardGrade));

        var newItem = await svc.AddItemAsync(order.Id, new AddOrderItemRequest
        {
            ProductionStandardId = ps.Id,
            StandardGrade = gm.StandardGrade,
            MaterialName = MaterialName.SeamlessPipe,
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
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = CreateSampleOrderRequest(cust.Id, ps.Id, gm.StandardGrade);

        var result = await svc.CreateAsync(request);
        var detail = await svc.GetByIdAsync(result.Id);

        // 219*8 无缝管，密度7.85，长度6m*10支=60m
        // 有效壁厚 = 8 - 0.5*0.5 + 0.5*0.5 = 8
        // 有效外径 = 219 - 0.5*0.5 + 0.5*0.5 = 219
        // 重量 = 7.85 * 3.1416 * 8 * (219-8) * 60 / 1000
        // = 7.85 * 3.1416 * 8 * 211 * 60 / 1000
        // = 7.85 * 3.1416 * 8 * 211 * 0.06
        var expectedWeight = Math.Round(7.85m * 3.1416m * 8m * (219m - 8m) * 60m / 1000m, 2);

        detail.Items[0].TheoreticalWeight.Should().Be(expectedWeight);
        detail.Items[0].Specification.Should().Be("219*8");
    }

    // ========== 查询 ==========

    [Fact]
    public async Task GetPagedAsync_状态筛选_正确过滤()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var order = await svc.CreateAsync(CreateSampleOrderRequest(cust.Id, ps.Id, gm.StandardGrade));
        await svc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Confirmed.ToString(),
            RowVersion = new byte[8]
        });

        // 只查待处理
        var pendingResult = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 0,
            PageSize = 10
        }, statuses: new List<SalesOrderStatus> { SalesOrderStatus.Pending });

        pendingResult.Items.Should().BeEmpty();

        // 只查已确认
        var confirmedResult = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 0,
            PageSize = 10
        }, statuses: new List<SalesOrderStatus> { SalesOrderStatus.Confirmed });

        confirmedResult.Items.Should().HaveCount(1);
        confirmedResult.Items[0].Status.Should().Be(SalesOrderStatus.Confirmed);
    }

    // ========== 辅助方法 ==========

    private CreateSalesOrderRequest CreateSampleOrderRequest(int customerId, int psId, string standardGrade)
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
                    ProductionStandardId = psId,
                    StandardGrade = standardGrade,
                    MaterialName = MaterialName.SeamlessPipe,
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
}
