using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Services;
using MES.Services.Order;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services.Order;

/// <summary>
/// 工单首页读模型刷新服务测试
/// </summary>
public class WorkOrderStatusSummaryServiceTests : TestBase
{
    private WorkOrderStatusSummaryService CreateService(AppDbContext ctx)
    {
        var loggerMock = new Mock<ILogger<WorkOrderStatusSummaryService>>();
        return new WorkOrderStatusSummaryService(ctx, loggerMock.Object);
    }

    private async Task<(int OrderId, string OrderNo)> SeedConfirmedOrderAsync(AppDbContext ctx,
        string orderNoPrefix = "WOSS-TEST")
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

    /// <summary>种子一个已确认订单并生成工单</summary>
    private async Task<(int OrderId, string OrderNo, List<int> WorkOrderIds)> SeedConfirmedWithWorkOrdersAsync(
        AppDbContext ctx, WorkOrderStatus woStatus = WorkOrderStatus.Confirmed)
    {
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);

        var items = await ctx.OrderItems
            .Where(oi => oi.SalesOrderId == orderId)
            .ToListAsync();
        var itemIds = items.Select(i => i.Sequence).ToList();

        var woLoggerMock = new Mock<ILogger<WorkOrderService>>();
        var woSvc = new WorkOrderService(ctx, woLoggerMock.Object);
        var generated = await woSvc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D01", ProductionSubNo = "C01", OrderItemIds = itemIds }
            }
        });

        // 如果需要特定工单状态，直接修改
        if (woStatus != WorkOrderStatus.Confirmed)
        {
            var wo = await ctx.WorkOrders.FirstAsync(w => w.SalesOrderNo == orderNo);
            wo.Status = woStatus;
            await ctx.SaveChangesAsync();
        }

        return (orderId, orderNo, generated.Select(g => g.Id).ToList());
    }

    // ========== RefreshAllAsync ==========

    [Fact]
    public async Task RefreshAllAsync_无已确认订单_不创建汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 创建未确认订单
        var cust = await SeedCustomerAsync(ctx);
        var order = new SalesOrder
        {
            OrderNumber = "PENDING-ORDER",
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Status = SalesOrderStatus.Pending
        };
        ctx.SalesOrders.Add(order);
        await ctx.SaveChangesAsync();

        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderStatusSummary>().ToListAsync();
        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshAllAsync_已确认订单_创建汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);

        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderStatusSummary>().ToListAsync();
        summaries.Should().HaveCount(1);
        var s = summaries[0];
        s.SalesOrderId.Should().Be(orderId);
        s.OrderNumber.Should().Be(orderNo);
        s.CustomerName.Should().Be("测试客户");
        s.HasWorkOrder.Should().BeFalse();
        s.WorkOrderCount.Should().Be(0);
        s.WorkOrderStatus.Should().Be(WorkOrderStatus.NotGenerated);
    }

    [Fact]
    public async Task RefreshAllAsync_已确认订单含工单_正确聚合工单状态()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (orderId, orderNo, woIds) = await SeedConfirmedWithWorkOrdersAsync(ctx);

        await svc.RefreshAllAsync();

        var summary = await ctx.Set<WorkOrderStatusSummary>().FirstAsync();
        summary.HasWorkOrder.Should().BeTrue();
        summary.WorkOrderCount.Should().Be(1);
        summary.WorkOrderStatus.Should().Be(WorkOrderStatus.Confirmed);
        summary.WorkOrderId.Should().Be(woIds[0]);
    }

    // ========== RefreshByOrderAsync ==========

    [Fact]
    public async Task RefreshByOrderAsync_未确认订单_删除已有汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (orderId, _) = await SeedConfirmedOrderAsync(ctx);

        // 先刷新创建汇总
        await svc.RefreshAllAsync();
        (await ctx.Set<WorkOrderStatusSummary>().CountAsync()).Should().Be(1);

        // 将订单状态改回未确认
        var order = await ctx.SalesOrders.FindAsync(orderId);
        order!.Status = SalesOrderStatus.Pending;
        await ctx.SaveChangesAsync();

        // 刷新 → 应删除
        await svc.RefreshByOrderAsync(orderId);

        var summaries = await ctx.Set<WorkOrderStatusSummary>().ToListAsync();
        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshByOrderAsync_已确认订单_创建汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (orderId, _) = await SeedConfirmedOrderAsync(ctx);

        await svc.RefreshByOrderAsync(orderId);

        var summaries = await ctx.Set<WorkOrderStatusSummary>().ToListAsync();
        summaries.Should().HaveCount(1);
        summaries[0].SalesOrderId.Should().Be(orderId);
    }

    // ========== RefreshByCustomerAsync ==========

    [Fact]
    public async Task RefreshByCustomerAsync_客户有已确认订单_刷新()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var cust = await SeedCustomerAsync(ctx);

        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        // 创建 2 个同客户已确认订单
        for (int i = 0; i < 2; i++)
        {
            var order = new SalesOrder
            {
                OrderNumber = $"CUST-WOSS-{Guid.NewGuid():N}"[..15],
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

        var summaries = await ctx.Set<WorkOrderStatusSummary>().ToListAsync();
        summaries.Should().HaveCount(2);
        summaries.All(s => s.CustomerName == "测试客户").Should().BeTrue();
    }

    [Fact]
    public async Task RefreshByCustomerAsync_客户无已确认订单_不报错()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var cust = await SeedCustomerAsync(ctx);

        // 客户只有未确认订单
        var order = new SalesOrder
        {
            OrderNumber = "PENDING-ONLY",
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Status = SalesOrderStatus.Pending
        };
        ctx.SalesOrders.Add(order);
        await ctx.SaveChangesAsync();

        await svc.RefreshByCustomerAsync(cust.Id);
        (await ctx.Set<WorkOrderStatusSummary>().CountAsync()).Should().Be(0);
    }

    // ========== RefreshByWorkOrderAsync ==========

    [Fact]
    public async Task RefreshByWorkOrderAsync_按订单号刷新()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (_, orderNo) = await SeedConfirmedOrderAsync(ctx);

        await svc.RefreshByWorkOrderAsync(orderNo);

        var summaries = await ctx.Set<WorkOrderStatusSummary>().ToListAsync();
        summaries.Should().HaveCount(1);
        summaries[0].OrderNumber.Should().Be(orderNo);
    }

    // ========== 工单状态推导 ==========

    [Fact]
    public async Task 工单状态推导_NotGenerated_无工单()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await SeedConfirmedOrderAsync(ctx);

        await svc.RefreshAllAsync();

        var summary = await ctx.Set<WorkOrderStatusSummary>().FirstAsync();
        summary.WorkOrderStatus.Should().Be(WorkOrderStatus.NotGenerated);
        summary.HasWorkOrder.Should().BeFalse();
        summary.WorkOrderCount.Should().Be(0);
    }

    [Fact]
    public async Task 工单状态推导_Pending_有待修正工单()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await SeedConfirmedWithWorkOrdersAsync(ctx, WorkOrderStatus.Pending);

        await svc.RefreshAllAsync();

        var summary = await ctx.Set<WorkOrderStatusSummary>().FirstAsync();
        summary.WorkOrderStatus.Should().Be(WorkOrderStatus.Pending);
        summary.HasWorkOrder.Should().BeTrue();
        summary.WorkOrderCount.Should().Be(1);
    }

    [Fact]
    public async Task 工单状态推导_Confirmed_全部已确定()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await SeedConfirmedWithWorkOrdersAsync(ctx, WorkOrderStatus.Confirmed);

        await svc.RefreshAllAsync();

        var summary = await ctx.Set<WorkOrderStatusSummary>().FirstAsync();
        summary.WorkOrderStatus.Should().Be(WorkOrderStatus.Confirmed);
        summary.HasWorkOrder.Should().BeTrue();
        summary.WorkOrderCount.Should().Be(1);
    }

    [Fact]
    public async Task 取消的工单不计入聚合()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 使用 SeedConfirmedOrderAsync 获取已确认订单
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);

        // 直接添加 2 个工单：1 已取消，1 已确定
        var items = await ctx.OrderItems
            .Where(oi => oi.SalesOrderId == orderId)
            .ToListAsync();
        var itemIds = string.Join(",", items.Select(i => i.Sequence));

        var wo1 = new WorkOrder
        {
            WorkOrderNo = "WO-CANCELLED",
            SalesOrderNo = orderNo,
            ProductionMainNo = "D01",
            ProductionSubNo = "C01",
            OrderItemIds = itemIds,
            SignDate = DateTime.Today,
            Status = WorkOrderStatus.Cancelled,
            LengthStatus = LengthStatus.Fixed,
            MaterialName = MaterialName.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219×8",
            TotalQuantity = 10,
            TotalMeters = 60m,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            TechnicalRequirements = RequirementType.Normal,
            DeliveryDate = DateTime.Today.AddMonths(1),
            DelayPenalty = false,
            Salesman = "测试业务员",
            StandardCode = "GB/T 8163"
        };
        ctx.WorkOrders.Add(wo1);

        var wo2 = new WorkOrder
        {
            WorkOrderNo = "WO-ACTIVE",
            SalesOrderNo = orderNo,
            ProductionMainNo = "D01",
            ProductionSubNo = "C02",
            OrderItemIds = itemIds,
            SignDate = DateTime.Today,
            Status = WorkOrderStatus.Confirmed,
            LengthStatus = LengthStatus.Fixed,
            MaterialName = MaterialName.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219×8",
            TotalQuantity = 10,
            TotalMeters = 60m,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            TechnicalRequirements = RequirementType.Normal,
            DeliveryDate = DateTime.Today.AddMonths(1),
            DelayPenalty = false,
            Salesman = "测试业务员",
            StandardCode = "GB/T 8163"
        };
        ctx.WorkOrders.Add(wo2);
        await ctx.SaveChangesAsync();

        await svc.RefreshAllAsync();

        var summary = await ctx.Set<WorkOrderStatusSummary>().FirstAsync();
        summary.WorkOrderCount.Should().Be(1); // 取消的不计入
        summary.WorkOrderStatus.Should().Be(WorkOrderStatus.Confirmed);
    }
}
