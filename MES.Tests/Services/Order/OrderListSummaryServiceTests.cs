using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Order;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services.Order;

/// <summary>
/// 订单列表读模型刷新服务测试
/// </summary>
public class OrderListSummaryServiceTests : TestBase
{
    private OrderListSummaryService CreateService(AppDbContext ctx)
    {
        var loggerMock = new Mock<ILogger<OrderListSummaryService>>();
        return new OrderListSummaryService(ctx, loggerMock.Object);
    }

    private async Task<(int OrderId, string OrderNo)> SeedConfirmedOrderAsync(AppDbContext ctx,
        string orderNoPrefix = "OLS-TEST", string? standardGrade = null)
    {
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        var notifMock = new Mock<INotificationService>();
        var orderSvc = new OrderService(ctx, new Mock<ILogger<OrderService>>().Object, notifMock.Object, null!);

        var order = await orderSvc.CreateAsync(new CreateSalesOrderRequest
        {
            OrderNumber = $"{orderNoPrefix}-{Guid.NewGuid():N}"[..15],
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductionStandardId = ps.Id,
                    StandardGrade = gm.StandardGrade,
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
        });

        await orderSvc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Confirmed.ToString(),
            RowVersion = new byte[8]
        });

        return (order.Id, order.OrderNumber);
    }

    // ========== RefreshAllAsync ==========

    [Fact]
    public async Task RefreshAllAsync_无订单_不创建任何汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<OrderListSummary>().ToListAsync();
        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshAllAsync_单个订单_创建正确汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);

        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<OrderListSummary>().ToListAsync();
        summaries.Should().HaveCount(1);
        var s = summaries[0];
        s.OrderId.Should().Be(orderId);
        s.OrderNumber.Should().Be(orderNo);
        s.CustomerName.Should().Be("测试客户");
        s.Salesman.Should().Be("测试业务员");
        s.ItemCount.Should().Be(1);
        s.TotalContractWeight.Should().Be(2500); // (int)Math.Round(2500m)
        s.HasDelayPenalty.Should().BeFalse();
        s.DeliveryStart.Should().Be(DateTime.Today.AddMonths(1));
        s.DeliveryEnd.Should().Be(DateTime.Today.AddMonths(1));
    }

    [Fact]
    public async Task RefreshAllAsync_多个订单_创建多个汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        // 直接创建 2 个订单避免 SeedConfirmedOrderAsync 重复种子冲突
        var orderIds = new List<int>();
        for (int i = 0; i < 2; i++)
        {
            var order = new SalesOrder
            {
                OrderNumber = $"MULTI-{Guid.NewGuid():N}"[..15],
                SignDate = DateTime.Today,
                CustomerId = cust.Id,
                Status = SalesOrderStatus.Confirmed
            };
            ctx.SalesOrders.Add(order);
            await ctx.SaveChangesAsync();

            ctx.OrderItems.Add(new OrderItem
            {
                SalesOrderId = order.Id,
                ProductionStandardId = ps.Id,
                StandardGrade = gm.StandardGrade,
                MaterialName = MaterialName.SeamlessPipe,
                PlantGrade = "20#",
                Specification = "219×8",
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
            });
            await ctx.SaveChangesAsync();
            orderIds.Add(order.Id);
        }

        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<OrderListSummary>().ToListAsync();
        summaries.Should().HaveCount(2);
        summaries.Select(s => s.OrderId).Should().BeEquivalentTo(orderIds);
    }

    // ========== RefreshByOrderAsync ==========

    [Fact]
    public async Task RefreshByOrderAsync_订单不存在_删除已有汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (orderId, _) = await SeedConfirmedOrderAsync(ctx);

        // 先刷新生成汇总
        await svc.RefreshAllAsync();
        (await ctx.Set<OrderListSummary>().CountAsync()).Should().Be(1);

        // 删除订单
        var order = await ctx.SalesOrders.FindAsync(orderId);
        ctx.SalesOrders.Remove(order!);
        await ctx.SaveChangesAsync();

        // 再次刷新单个订单（不存在 → 删除汇总）
        await svc.RefreshByOrderAsync(orderId);

        var summaries = await ctx.Set<OrderListSummary>().ToListAsync();
        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshByOrderAsync_订单存在_刷新汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);

        await svc.RefreshByOrderAsync(orderId);

        var summaries = await ctx.Set<OrderListSummary>().ToListAsync();
        summaries.Should().HaveCount(1);
        summaries[0].OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task RefreshByOrderAsync_修改项次后刷新_汇总更新()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (orderId, _) = await SeedConfirmedOrderAsync(ctx);

        // 先刷新创建汇总
        await svc.RefreshAllAsync();
        (await ctx.Set<OrderListSummary>().FirstAsync()).DeliveryStart.Should().Be(DateTime.Today.AddMonths(1));

        // 修改项次交货日期
        var item = await ctx.OrderItems.FirstAsync(oi => oi.SalesOrderId == orderId);
        item.DeliveryDate = DateTime.Today.AddMonths(2);
        item.ContractWeight = 3000m;
        await ctx.SaveChangesAsync();

        // 刷新单个订单
        await svc.RefreshByOrderAsync(orderId);

        var summary = await ctx.Set<OrderListSummary>().FirstAsync();
        summary.DeliveryStart.Should().Be(DateTime.Today.AddMonths(2));
        summary.TotalContractWeight.Should().Be(3000);
    }

    // ========== RefreshByCustomerAsync ==========

    [Fact]
    public async Task RefreshByCustomerAsync_客户变更_刷新该客户所有订单()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var cust = await SeedCustomerAsync(ctx, "同客户");

        // 直接创建 2 个同客户订单
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        for (int i = 0; i < 2; i++)
        {
            var order = new SalesOrder
            {
                OrderNumber = $"CUST-TEST-{Guid.NewGuid():N}"[..15],
                SignDate = DateTime.Today,
                CustomerId = cust.Id,
                Status = SalesOrderStatus.Confirmed
            };
            ctx.SalesOrders.Add(order);
            await ctx.SaveChangesAsync();

            ctx.OrderItems.Add(new OrderItem
            {
                SalesOrderId = order.Id,
                ProductionStandardId = ps.Id,
                StandardGrade = gm.StandardGrade,
                MaterialName = MaterialName.SeamlessPipe,
                PlantGrade = "20#",
                Specification = "219×8",
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
            });
            await ctx.SaveChangesAsync();
        }

        await svc.RefreshByCustomerAsync(cust.Id);

        var summaries = await ctx.Set<OrderListSummary>().ToListAsync();
        summaries.Should().HaveCount(2);
        summaries.All(s => s.CustomerName == "同客户").Should().BeTrue();
    }

    [Fact]
    public async Task RefreshByCustomerAsync_无订单_不报错()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 不创建任何订单，针对一个不存在的客户 ID 刷新
        await svc.RefreshByCustomerAsync(999);
        (await ctx.Set<OrderListSummary>().CountAsync()).Should().Be(0);
    }

    // ========== 聚合计算正确性 ==========

    [Fact]
    public async Task HasTechReqCount_聚合正确()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (orderId, _) = await SeedConfirmedOrderAsync(ctx);

        // 为项次添加技术要求
        var item = await ctx.OrderItems.FirstAsync(oi => oi.SalesOrderId == orderId);
        ctx.ProductRequirements.Add(new ProductRequirement
        {
            OrderItemId = item.Id,
            RequirementType = RequirementType.Special,
            ChemicalComposition = "Test"
        });
        await ctx.SaveChangesAsync();

        await svc.RefreshAllAsync();

        var summary = await ctx.Set<OrderListSummary>().FirstAsync();
        summary.HasTechReqCount.Should().Be(1);
    }

    [Fact]
    public async Task TotalContractWeight_取整正确()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        // 创建合同重量为 2499.6m 的订单 → 应取整为 2500
        var order = new SalesOrder
        {
            OrderNumber = $"ROUND-TEST-{Guid.NewGuid():N}"[..15],
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Status = SalesOrderStatus.Confirmed
        };
        ctx.SalesOrders.Add(order);
        await ctx.SaveChangesAsync();

        ctx.OrderItems.Add(new OrderItem
        {
            SalesOrderId = order.Id,
            ProductionStandardId = ps.Id,
            StandardGrade = gm.StandardGrade,
            MaterialName = MaterialName.SeamlessPipe,
            PlantGrade = "20#",
            Specification = "219×8",
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
            ContractWeight = 2499.6m,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled
        });
        await ctx.SaveChangesAsync();

        await svc.RefreshAllAsync();

        var summary = await ctx.Set<OrderListSummary>().FirstAsync();
        summary.TotalContractWeight.Should().Be(2500);
    }
}
