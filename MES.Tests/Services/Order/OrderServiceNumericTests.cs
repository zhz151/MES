using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MES.Core.Constants;
using MES.Core.DTOs.Order;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.WorkOrder;
using MES.Data;
using MES.Data.Entities.Order;
using MES.Services.Order;
using MES.Tests.Tests;
using Moq;
using OrderListSummaryEntity = MES.Data.Entities.Order.OrderListSummary;

namespace MES.Tests.Services;

/// <summary>
/// TC08 数值边界 + TC09 数量/重量计算逻辑（Mock 层）
/// 聚焦 OrderService.CreateAsync 的：
///   - 米数/理论重量算术（定尺=长度×数量、范围尺=请求米数）
///   - 多 Item 订单头汇总（TotalContractWeight / ItemCount）
///   - 数值边界（数量 0/负、合同重量负、长度非法、超长文本）不崩塌
/// </summary>
public class OrderServiceNumericTests : TestBase
{
    private OrderService CreateService(AppDbContext ctx)
    {
        var loggerMock = new Mock<ILogger<OrderService>>();
        var notificationMock = Mock.Of<INotificationService>();
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var listSummaryMock = new Mock<IWorkOrderListSummaryRefreshService>();
        var pendingDeliveryMock = new Mock<IPendingDeliveryQueryService>();

        var services = new ServiceCollection();
        services.AddSingleton(new Mock<IWorkOrderExecutionService>().Object);
        services.AddSingleton(listSummaryMock.Object);
        services.AddSingleton(pendingDeliveryMock.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new OrderService(ctx, loggerMock.Object, notificationMock, configMock.Object,
            new Mock<IOperationLogService>().Object, new MemoryCache(new MemoryCacheOptions()),
            workOrderService: null, listSummaryService: listSummaryMock.Object, scopeFactory: scopeFactory);
    }

    // ========== TC09 计算逻辑 ==========

    [Fact]
    public async Task CreateAsync_定尺_米数等于长度乘数量()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        // 定尺：MinLength=MaxLength=6000，10 支 → 米数 = 6000*10/1000 = 60
        var request = BuildRequest(cust.Id, gm.StandardGrade, q =>
        {
            q.LengthStatus = LengthStatus.Fixed;
            q.MinLength = 6000m;
            q.MaxLength = 6000m;
            q.Quantity = 10;
        });

        var result = await svc.CreateAsync(request);
        var detail = await svc.GetByIdAsync(result.Id);

        detail.Items[0].Meters.Should().Be(60m);
    }

    [Fact]
    public async Task CreateAsync_范围尺_米数使用请求米数()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = BuildRequest(cust.Id, gm.StandardGrade, q =>
        {
            q.LengthStatus = LengthStatus.Range;
            q.MinLength = 6000m;
            q.MaxLength = 12000m;
            q.Meters = 500m;
        });

        var result = await svc.CreateAsync(request);
        var detail = await svc.GetByIdAsync(result.Id);

        detail.Items[0].Meters.Should().Be(500m);
    }

    [Fact]
    public async Task CreateAsync_多Item_订单头总重量与项次数汇总正确()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var item1 = BuildItem(gm.StandardGrade, cw: 2500m);
        var item2 = BuildItem(gm.StandardGrade, cw: 2600m);
        var request = BuildRequest(cust.Id, gm.StandardGrade, extraItems: new[] { item1, item2 });

        var result = await svc.CreateAsync(request);

        var summary = await ctx.Set<OrderListSummaryEntity>().FirstAsync(s => s.OrderId == result.Id);
        summary.TotalContractWeight.Should().Be(5100);   // 2500 + 2600
        summary.ItemCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateAsync_理论重量随数量线性变化()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var req10 = BuildRequest(cust.Id, gm.StandardGrade, q => q.Quantity = 10);
        var req20 = BuildRequest(cust.Id, gm.StandardGrade, q =>
        {
            q.Quantity = 20;
            q.ContractWeight = 5000m;   // 理算约 4995.4，合同重量须在 ±6% 内
        });
        var r1 = await svc.CreateAsync(req10);
        var r2 = await svc.CreateAsync(req20);

        var w1 = (await svc.GetByIdAsync(r1.Id)).Items[0].TheoreticalWeight;
        var w2 = (await svc.GetByIdAsync(r2.Id)).Items[0].TheoreticalWeight;

        // 20 支理论重量应为 10 支的 2 倍（米数 120 vs 60）
        w2.Should().Be(2m * w1);
    }

    // ========== TC08 数值边界 ==========

    [Fact]
    public async Task CreateAsync_定尺_数量为零_不崩溃且理论重量为零()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = BuildRequest(cust.Id, gm.StandardGrade, q =>
        {
            q.LengthStatus = LengthStatus.Fixed;
            q.MinLength = 6000m;
            q.MaxLength = 6000m;
            q.Quantity = 0;
        });

        var result = await svc.CreateAsync(request);   // 数量 0 不抛异常即验证通过
        var detail = await svc.GetByIdAsync(result.Id);
        detail.Items[0].Quantity.Should().Be(0);
        detail.Items[0].TheoreticalWeight.Should().Be(0m);   // 数量 0 → 米数 null → 理算重量 0
    }

    [Fact]
    public async Task CreateAsync_定尺_数量为负数_不崩溃()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = BuildRequest(cust.Id, gm.StandardGrade, q =>
        {
            q.LengthStatus = LengthStatus.Fixed;
            q.MinLength = 6000m;
            q.MaxLength = 6000m;
            q.Quantity = -5;
        });

        var act = () => svc.CreateAsync(request);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateAsync_定尺_合同重量为负_抛业务异常()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        // 理论重量 > 0（数量 10 × 6m），负合同重量必低于下界 → 触发"可能亏损"校验
        var request = BuildRequest(cust.Id, gm.StandardGrade, q => q.ContractWeight = -100m);

        var act = () => svc.CreateAsync(request);
        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*合同重量*低于理算重量*");
    }

    [Fact]
    public async Task CreateAsync_定尺_最小长度为零_抛业务异常()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = BuildRequest(cust.Id, gm.StandardGrade, q =>
        {
            q.LengthStatus = LengthStatus.Fixed;
            q.MinLength = 0m;
            q.MaxLength = 6000m;
        });

        var act = () => svc.CreateAsync(request);
        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("定尺时必须填写长度");
    }

    [Fact]
    public async Task CreateAsync_定尺_最大长度不等于最小长度_抛业务异常()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = BuildRequest(cust.Id, gm.StandardGrade, q =>
        {
            q.LengthStatus = LengthStatus.Fixed;
            q.MinLength = 6000m;
            q.MaxLength = 7000m;
        });

        var act = () => svc.CreateAsync(request);
        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("定尺模式下最小长度必须等于最大长度");
    }

    [Fact]
    public async Task CreateAsync_超长备注_不崩溃()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);
        var svc = CreateService(ctx);

        var request = BuildRequest(cust.Id, gm.StandardGrade, q => q.Remark = new string('x', 10000));

        var act = () => svc.CreateAsync(request);
        await act.Should().NotThrowAsync();
    }

    // ========== 辅助方法 ==========

    private static CreateSalesOrderRequest BuildRequest(int customerId, string standardGrade,
        Action<CreateOrderItemRequest>? mutate = null,
        CreateOrderItemRequest[]? extraItems = null)
    {
        var items = extraItems != null && extraItems.Length > 0
            ? extraItems.ToList()
            : new List<CreateOrderItemRequest> { BuildItem(standardGrade, mutate) };

        return new CreateSalesOrderRequest
        {
            OrderNumber = $"ORD-TEST-{Guid.NewGuid():N}"[..12],
            SignDate = DateTime.Today,
            CustomerId = customerId,
            Items = items
        };
    }

    private static CreateOrderItemRequest BuildItem(string standardGrade,
        Action<CreateOrderItemRequest>? mutate = null, decimal cw = 2500m)
    {
        var item = new CreateOrderItemRequest
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
            ContractWeight = cw,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled
        };
        mutate?.Invoke(item);
        return item;
    }
}
