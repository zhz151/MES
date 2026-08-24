using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Data.Entities;
using MES.Services.Order;
using MES.Tests.Tests;
using Moq;
using MES.Data;
using MES.Data.Entities.StandardRegister;
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Interfaces.Infrastructure;
using Microsoft.Extensions.Logging;
using MES.Data.Entities.Order;

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
        var orderSvc = new OrderService(ctx, new Mock<ILogger<OrderService>>().Object, notifMock.Object, orderConfigMock.Object, new Mock<IOperationLogService>().Object, new MemoryCache(new MemoryCacheOptions()));

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
            ChemicalComposition = true
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
            ChemicalComposition = true
        });

        result.Should().NotBeNull();
        result.RequirementType.Should().Be(RequirementType.Normal);
        result.ChemicalComposition.Should().BeTrue();
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
            ChemicalComposition = true
        });

        // 更新
        var updated = await svc.CreateOrUpdateAsync(itemId, new CreateProductRequirementRequest
        {
            RequirementType = RequirementType.Special,
            ChemicalComposition = true
        });

        updated.RequirementType.Should().Be(RequirementType.Special);
        updated.ChemicalComposition.Should().BeTrue();
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
            ChemicalComposition = true
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
            ChemicalComposition = true
        });

        var result = await svc.GetByOrderItemIdAsync(itemId);
        result.Should().NotBeNull();
        result!.OrderItemId.Should().Be(itemId);
        result.ChemicalComposition.Should().BeTrue();
    }

    [Fact]
    public async Task GetByOrderIdAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var results = await svc.GetByOrderIdAsync(999);
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDefaultRequirementsByStandardNoAsync_含必检字段_默认true()
    {
        var ctx = CreateDbContext();
        var sr = await SeedRegisterAsync(ctx);

        ctx.FactoryInspectionRequirements.Add(new FactoryInspectionRequirement
        {
            StandardNo = sr.StandardNo,
            ChemicalComposition = "必检",
            PmiInspection = "按需",
            SurfaceInspection = "必检",
            Dimension = "必检",
            Endoscopy = "按需",
            GrainSize = null
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var defaults = await svc.GetDefaultRequirementsByStandardNoAsync(sr.StandardNo);

        defaults.Should().NotBeNull();
        defaults.ChemicalComposition.Should().BeTrue();
        defaults.PmiInspection.Should().Be(InspectionRequirementStage.None);
        defaults.SurfaceInspection.Should().Be(InspectionRequirementStage.FinalOnly);
        defaults.Dimension.Should().Be(InspectionRequirementStage.FinalOnly);
        defaults.Endoscopy.Should().Be(InspectionRequirementStage.None);
        defaults.GrainSize.Should().BeFalse();
    }

    [Fact]
    public async Task GetDefaultRequirementsByStandardNoAsync_标准号空格差异_规范化匹配()
    {
        var ctx = CreateDbContext();

        ctx.FactoryInspectionRequirements.Add(new FactoryInspectionRequirement
        {
            StandardNo = "GB/T 99999-2025",
            ChemicalComposition = "必检",
            SurfaceInspection = "必检"
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var defaults = await svc.GetDefaultRequirementsByStandardNoAsync("GB/T99999-2025");

        defaults.ChemicalComposition.Should().BeTrue();
        defaults.SurfaceInspection.Should().Be(InspectionRequirementStage.FinalOnly);
    }

    [Fact]
    public async Task GetDefaultRequirementsByStandardNoAsync_标准号为空或不存在_返回默认终()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 标准号为空/匹配不到：不覆盖，保持 DTO 默认「终」（FinalOnly），前端预填为「终」
        var nullResult = await svc.GetDefaultRequirementsByStandardNoAsync(null);
        nullResult.Should().NotBeNull();
        nullResult.ChemicalComposition.Should().BeFalse();
        nullResult.PmiInspection.Should().Be(InspectionRequirementStage.FinalOnly);

        var missingResult = await svc.GetDefaultRequirementsByStandardNoAsync("NO-SUCH-STANDARD");
        missingResult.ChemicalComposition.Should().BeFalse();
    }


    [Fact]
    public async Task GetQualityRemarkByOrderItemIdsAsync_多项次_按项次号拼接()
    {
        var ctx = CreateDbContext();
        var (orderId, itemId) = await SeedOrderItemAsync(ctx);

        // 项次1 固定 Sequence=1 且同订单号 "X"，并加第二个项次 Sequence=2
        var oi1 = await ctx.OrderItems.FirstAsync(oi => oi.Id == itemId);
        oi1.Sequence = 1;
        oi1.OrderNumber = "X";
        var item2 = new OrderItem
        {
            SalesOrderId = orderId,
            OrderNumber = "X",
            Sequence = 2,
            StandardNo = "GB",
            StandardGrade = "304",
            PlantGrade = "304",
            Density = 7.93m,
            Specification = "219*8",
            OuterDiameter = 219m,
            WallThickness = 8m,
            LengthStatus = LengthStatus.NonFixed,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SettlementMethod = SettlementMethod.Theoretical,
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            Quantity = 5,
            ContractWeight = 100m
        };
        ctx.OrderItems.Add(item2);
        await ctx.SaveChangesAsync();

        ctx.ProductRequirements.Add(new ProductRequirement { OrderItemId = oi1.Id, OrderNo = "X", ItemSequence = 1, OtherRequirement = "项次A要求" });
        ctx.ProductRequirements.Add(new ProductRequirement { OrderItemId = item2.Id, OrderNo = "X", ItemSequence = 2, OtherRequirement = "项次B要求" });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        // OrderItemIds 存的是「项次序号 Sequence」，须结合订单号匹配（非 OrderItem.Id）
        var result = await svc.GetQualityRemarkByOrderItemIdsAsync("X", "1,2");

        result.Should().Be(string.Join(Environment.NewLine, "项次1：项次A要求", "项次2：项次B要求"));
    }

    [Fact]
    public async Task GetQualityRemarkByOrderItemIdsAsync_无其他要求或参数为空_返回空()
    {
        var ctx = CreateDbContext();
        var (_, itemId) = await SeedOrderItemAsync(ctx);
        var orderNo = await ctx.OrderItems.Where(oi => oi.Id == itemId).Select(oi => oi.OrderNumber).FirstAsync();
        await ctx.SaveChangesAsync();

        ctx.ProductRequirements.Add(new ProductRequirement { OrderItemId = itemId, OrderNo = orderNo, ItemSequence = 1, OtherRequirement = null });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        (await svc.GetQualityRemarkByOrderItemIdsAsync(null, null)).Should().BeEmpty();
        (await svc.GetQualityRemarkByOrderItemIdsAsync("", "")).Should().BeEmpty();
        (await svc.GetQualityRemarkByOrderItemIdsAsync("X", "abc")).Should().BeEmpty();
        (await svc.GetQualityRemarkByOrderItemIdsAsync("X", "999999")).Should().BeEmpty();
        // ⚠️ 传 OrderItem.Id（非 Sequence）作为项次列表 → 匹配不到 → 空（验证 Sequence 语义）
        (await svc.GetQualityRemarkByOrderItemIdsAsync(orderNo, itemId.ToString())).Should().BeEmpty();
    }
}
