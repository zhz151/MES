using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Data.Entities;
using MES.Services.Order;
using MES.Tests.Tests;
using Moq;
using MES.Data;
using MES.Data.Entities.ProductionStandard;
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.WorkOrder;
using Microsoft.Extensions.Logging;

namespace MES.Tests.Services;

/// <summary>
/// 产品要求服务测试：CRUD
/// </summary>
public class ProductRequirementServiceTests : TestBase
{
    private ProductRequirementService CreateService(AppDbContext ctx)
    {
        var orderSvcMock = new Mock<IOrderService>();
        return new ProductRequirementService(ctx, orderSvcMock.Object);
    }

    private async Task<(int OrderId, int ItemId)> SeedOrderItemAsync(AppDbContext ctx)
    {
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        var notifMock = new Mock<INotificationService>();
        var orderConfigMock = new Mock<IConfigParameterService>();
        orderConfigMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var orderSvc = new OrderService(ctx, new Mock<ILogger<OrderService>>().Object, notifMock.Object, orderConfigMock.Object, null);

        var order = await orderSvc.CreateAsync(new CreateSalesOrderRequest
        {
            OrderNumber = $"REQ-TEST-{Guid.NewGuid():N}"[..15],
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    StandardNo = sr.StandardNo,
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

        var item = await ctx.OrderItems.FirstAsync(oi => oi.SalesOrderId == order.Id);
        return (order.Id, item.Id);
    }

    [Fact]
    public async Task GetByOrderItemIdAsync_不存在_返回null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetByOrderItemIdAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateOrUpdateAsync_不存在的项次_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateOrUpdateAsync(999, new CreateProductRequirementRequest
        {
            RequirementType = RequirementType.Normal,
            ChemicalComposition = "C:0.20, Si:0.30, Mn:1.20"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("订单项次不存在");
    }

    [Fact]
    public async Task CreateOrUpdateAsync_首次创建_成功()
    {
        var ctx = CreateDbContext();
        var (_, itemId) = await SeedOrderItemAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateOrUpdateAsync(itemId, new CreateProductRequirementRequest
        {
            RequirementType = RequirementType.Normal,
            ChemicalComposition = "C:0.20, Si:0.30, Mn:1.20",
            MechanicalProperty = "抗拉≥410MPa, 屈服≥245MPa",
            NdtRequirement = "UT 100%"
        });

        result.Should().NotBeNull();
        result.RequirementType.Should().Be(RequirementType.Normal);
        result.ChemicalComposition.Should().Be("C:0.20, Si:0.30, Mn:1.20");
    }

    [Fact]
    public async Task CreateOrUpdateAsync_更新已存在的要求_成功()
    {
        var ctx = CreateDbContext();
        var (_, itemId) = await SeedOrderItemAsync(ctx);
        var svc = CreateService(ctx);

        await svc.CreateOrUpdateAsync(itemId, new CreateProductRequirementRequest
        {
            RequirementType = RequirementType.Normal,
            ChemicalComposition = "C:0.20"
        });

        // 更新
        var updated = await svc.CreateOrUpdateAsync(itemId, new CreateProductRequirementRequest
        {
            RequirementType = RequirementType.Special,
            ChemicalComposition = "C:0.25"
        });

        updated.RequirementType.Should().Be(RequirementType.Special);
        updated.ChemicalComposition.Should().Be("C:0.25");
    }

    [Fact]
    public async Task GetByOrderIdAsync_返回订单下所有要求()
    {
        var ctx = CreateDbContext();
        var (orderId, itemId) = await SeedOrderItemAsync(ctx);
        var svc = CreateService(ctx);

        await svc.CreateOrUpdateAsync(itemId, new CreateProductRequirementRequest
        {
            RequirementType = RequirementType.Normal,
            ChemicalComposition = "C:0.20"
        });

        var results = await svc.GetByOrderIdAsync(orderId);
        results.Should().HaveCount(1);
        results[0].OrderItemId.Should().Be(itemId);
    }

    [Fact]
    public async Task DeleteAsync_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("技术要求不允许单独删除，请删除对应的订单项次");
    }

    [Fact]
    public async Task GetByOrderItemIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var (_, itemId) = await SeedOrderItemAsync(ctx);
        var svc = CreateService(ctx);

        await svc.CreateOrUpdateAsync(itemId, new CreateProductRequirementRequest
        {
            RequirementType = RequirementType.Normal,
            ChemicalComposition = "C:0.20"
        });

        var result = await svc.GetByOrderItemIdAsync(itemId);
        result.Should().NotBeNull();
        result!.OrderItemId.Should().Be(itemId);
        result.ChemicalComposition.Should().Be("C:0.20");
    }

    [Fact]
    public async Task GetByOrderIdAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var results = await svc.GetByOrderIdAsync(999);
        results.Should().BeEmpty();
    }
}
