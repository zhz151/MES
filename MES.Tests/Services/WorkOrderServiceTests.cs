using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data.Entities;
using MES.Services;
using MES.Services.Order;
using MES.Tests.Tests;
using MES.Data;
using Moq;
using QuestPDF.Infrastructure;

namespace MES.Tests.Services;

/// <summary>
/// 工单服务测试：工单生成、查询、状态变更、物料聚合
/// </summary>
public class WorkOrderServiceTests : TestBase
{
    static WorkOrderServiceTests()
    {
        // QuestPDF 社区版许可（测试环境需要手动设置）
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private WorkOrderService CreateService(AppDbContext ctx)
    {
        var loggerMock = new Mock<ILogger<WorkOrderService>>();
        return new WorkOrderService(ctx, loggerMock.Object);
    }

    private async Task<(int OrderId, string OrderNo)> SeedConfirmedOrderAsync(AppDbContext ctx)
    {
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        var notifMock = new Mock<INotificationService>();
        var orderSvc = new OrderService(ctx, new Mock<ILogger<OrderService>>().Object, notifMock.Object);

        var order = await orderSvc.CreateAsync(new CreateSalesOrderRequest
        {
            OrderNumber = $"WO-TEST-{Guid.NewGuid():N}"[..15],
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

        // 变更为已确认
        await orderSvc.UpdateAsync(order.Id, new UpdateSalesOrderRequest
        {
            Status = SalesOrderStatus.Confirmed.ToString(),
            RowVersion = new byte[8]
        });

        return (order.Id, order.OrderNumber);
    }

    /// <summary>
    /// 种子一个已确认订单并生成工单，返回 (orderId, orderNo, workOrderIds)
    /// </summary>
    private async Task<(int OrderId, string OrderNo, List<int> WorkOrderIds)> SeedConfirmedOrderWithWorkOrdersAsync(AppDbContext ctx, int itemCount = 1)
    {
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);

        var items = await ctx.OrderItems
            .Where(oi => oi.SalesOrderId == orderId)
            .ToListAsync();
        var itemIds = items.Select(i => i.Sequence).ToList();

        var svc = CreateService(ctx);
        var generated = await svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D01", ProductionSubNo = "C01", OrderItemIds = itemIds }
            }
        });

        return (orderId, orderNo, generated.Select(g => g.Id).ToList());
    }

    // ========== 获取工单项次 ==========

    [Fact]
    public async Task GetOrderItemsForWorkOrderAsync_订单不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetOrderItemsForWorkOrderAsync("NONEXISTENT");
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task GetOrderItemsForWorkOrderAsync_订单未确认_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var svc = CreateService(ctx);

        // 创建一个未确认的订单
        var salesOrder = new SalesOrder
        {
            OrderNumber = "ORD-PENDING",
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Status = SalesOrderStatus.Pending
        };
        ctx.SalesOrders.Add(salesOrder);
        await ctx.SaveChangesAsync();

        var act = () => svc.GetOrderItemsForWorkOrderAsync("ORD-PENDING");
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*状态不是已确认*");
    }

    [Fact]
    public async Task GetOrderItemsForWorkOrderAsync_成功返回分组项次()
    {
        var ctx = CreateDbContext();
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);
        var svc = CreateService(ctx);

        var items = await svc.GetOrderItemsForWorkOrderAsync(orderNo);

        items.Should().NotBeEmpty();
        items[0].OrderNumber.Should().Be(orderNo);
        items[0].SuggestedMainNo.Should().NotBeNullOrEmpty();
    }

    // ========== 生成工单 ==========

    [Fact]
    public async Task GenerateWorkOrdersAsync_订单不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = "NONEXISTENT",
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D01", ProductionSubNo = "C01", OrderItemIds = new List<int> { 1 } }
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task GenerateWorkOrdersAsync_成功生成工单()
    {
        var ctx = CreateDbContext();
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);

        // 获取订单项次
        var items = await ctx.OrderItems
            .Where(oi => oi.SalesOrderId == orderId)
            .ToListAsync();
        var itemIds = items.Select(i => i.Sequence).ToList();

        var svc = CreateService(ctx);

        var result = await svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new()
                {
                    ProductionMainNo = "D01",
                    ProductionSubNo = "C01",
                    OrderItemIds = itemIds
                }
            }
        });

        result.Should().HaveCount(1);
        result[0].WorkOrderNo.Should().StartWith("GD");
        result[0].SalesOrderNo.Should().Be(orderNo);
        result[0].ProductionMainNo.Should().Be("D01");
        result[0].ProductionSubNo.Should().Be("C01");
        result[0].Status.Should().Be((int)WorkOrderStatus.Confirmed);
    }

    [Fact]
    public async Task GenerateWorkOrdersAsync_重新生成_物理删除旧工单()
    {
        var ctx = CreateDbContext();
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);

        var items = await ctx.OrderItems
            .Where(oi => oi.SalesOrderId == orderId)
            .ToListAsync();
        var itemIds = items.Select(i => i.Sequence).ToList();

        var svc = CreateService(ctx);

        // 第一次生成
        await svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D01", ProductionSubNo = "C01", OrderItemIds = itemIds }
            }
        });

        // 第二次生成（覆盖）
        await svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D02", ProductionSubNo = "C02", OrderItemIds = itemIds }
            }
        });

        // 应该只有第二次的工单
        var workOrders = await ctx.WorkOrders
            .Where(wo => wo.SalesOrderNo == orderNo)
            .ToListAsync();

        workOrders.Should().HaveCount(1);
        workOrders[0].ProductionMainNo.Should().Be("D02");
    }

    // ========== 工单状态变更 ==========

    [Fact]
    public async Task UpdateStatusAsync_合法流转_成功()
    {
        var ctx = CreateDbContext();
        var (_, orderNo) = await SeedConfirmedOrderAsync(ctx);
        var items = await ctx.OrderItems.ToListAsync();

        var svc = CreateService(ctx);
        var generated = await svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D01", ProductionSubNo = "C01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
            }
        });

        var wo = generated[0];
        var dto = await svc.GetByIdAsync(wo.Id);
        dto.Status.Should().Be((int)WorkOrderStatus.Confirmed);

        // Confirmed -> Pending
        var updated = await svc.UpdateStatusAsync(wo.Id, new UpdateWorkOrderStatusRequest
        {
            Status = (int)WorkOrderStatus.Pending,
            RowVersion = dto.RowVersion
        });
        updated.Status.Should().Be((int)WorkOrderStatus.Pending);
    }

    [Fact]
    public async Task UpdateStatusAsync_非法流转_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (_, orderNo) = await SeedConfirmedOrderAsync(ctx);
        var items = await ctx.OrderItems.ToListAsync();

        var svc = CreateService(ctx);
        var generated = await svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D01", ProductionSubNo = "C01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
            }
        });

        // NotGenerated 直接跳 Cancelled —> 不应该允许
        // 实际上工单创建后是 Confirmed，Confirmed -> Cancelled 是允许的
        // 但 Cancelled -> Pending 不允许
        var wo = generated[0];
        var dto = await svc.GetByIdAsync(wo.Id);

        // Confirmed -> Pending -> Cancelled （合法）
        await svc.UpdateStatusAsync(wo.Id, new UpdateWorkOrderStatusRequest
        {
            Status = (int)WorkOrderStatus.Pending,
            RowVersion = dto.RowVersion
        });
        var dto2 = await svc.GetByIdAsync(wo.Id);
        await svc.UpdateStatusAsync(wo.Id, new UpdateWorkOrderStatusRequest
        {
            Status = (int)WorkOrderStatus.Cancelled,
            RowVersion = dto2.RowVersion
        });

        // Cancelled -> Confirmed （非法）
        var dto3 = await svc.GetByIdAsync(wo.Id);
        var act = () => svc.UpdateStatusAsync(wo.Id, new UpdateWorkOrderStatusRequest
        {
            Status = (int)WorkOrderStatus.Confirmed,
            RowVersion = dto3.RowVersion
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不允许*");
    }

    // ========== 工单查询 ==========

    [Fact]
    public async Task GetPagedAsync_按订单号筛选()
    {
        var ctx = CreateDbContext();
        var (_, orderNo) = await SeedConfirmedOrderAsync(ctx);

        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new WorkOrderQueryParams
        {
            SalesOrderNo = orderNo,
            PageIndex = 0,
            PageSize = 10
        });

        result.TotalCount.Should().Be(0); // 还没生成工单
    }

    // ========== 工单删除 ==========

    [Fact]
    public async Task DeleteAsync_物理删除工单()
    {
        var ctx = CreateDbContext();
        var (_, orderNo) = await SeedConfirmedOrderAsync(ctx);
        var items = await ctx.OrderItems.ToListAsync();

        var svc = CreateService(ctx);
        var generated = await svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D01", ProductionSubNo = "C01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
            }
        });

        await svc.DeleteAsync(generated[0].Id);

        var act = () => svc.GetByIdAsync(generated[0].Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("工单不存在");
    }

    [Fact]
    public async Task DeleteAsync_级联删除圆棒穿孔计划()
    {
        var ctx = CreateDbContext();
        var (_, orderNo) = await SeedConfirmedOrderAsync(ctx);
        var items = await ctx.OrderItems.ToListAsync();

        var svc = CreateService(ctx);
        var generated = await svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D01", ProductionSubNo = "C01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
            }
        });

        // 创建圆棒穿孔计划
        var piercing = new RoundBarPiercingPlan
        {
            WorkOrderId = generated[0].Id,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = RawMaterialType.RoundBar,
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredPieces = 10,
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        };
        ctx.RoundBarPiercingPlans.Add(piercing);
        await ctx.SaveChangesAsync();

        // 删除工单
        await svc.DeleteAsync(generated[0].Id);

        // 验证圆棒穿孔计划也被级联删除
        var remaining = await ctx.RoundBarPiercingPlans
            .Where(p => p.WorkOrderId == generated[0].Id)
            .ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_物料计划聚合包含圆棒穿孔()
    {
        var ctx = CreateDbContext();
        var (_, orderNo) = await SeedConfirmedOrderAsync(ctx);
        var items = await ctx.OrderItems.ToListAsync();

        var svc = CreateService(ctx);
        var generated = await svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D01", ProductionSubNo = "C01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
            }
        });

        // 创建圆棒穿孔计划
        var piercing = new RoundBarPiercingPlan
        {
            WorkOrderId = generated[0].Id,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = RawMaterialType.RoundBar,
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredPieces = 10,
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        };
        ctx.RoundBarPiercingPlans.Add(piercing);
        await ctx.SaveChangesAsync();

        // 查询列表
        var result = await svc.GetPagedAsync(new WorkOrderQueryParams
        {
            PageIndex = 1,
            PageSize = 20
        });

        result.Items.Should().Contain(i => i.Id == generated[0].Id);
        var woDto = result.Items.First(i => i.Id == generated[0].Id);
        woDto.PiercingPlanTotalWeight.Should().Be(3000m);
        woDto.PiercingPlanTotalPieces.Should().Be(10);
    }

    [Fact]
    public async Task GetOrderWorkOrderRelationAsync_成功返回关系()
    {
        var ctx = CreateDbContext();
        var (_, orderNo) = await SeedConfirmedOrderAsync(ctx);
        var items = await ctx.OrderItems.ToListAsync();

        var svc = CreateService(ctx);
        await svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "D01", ProductionSubNo = "C01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
            }
        });

        var relation = await svc.GetOrderWorkOrderRelationAsync(orderNo);

        relation.Should().NotBeNull();
        relation.OrderNumber.Should().Be(orderNo);
        relation.WorkOrders.Should().HaveCount(1);
        relation.WorkOrders[0].ProductionMainNo.Should().Be("D01");
        relation.WorkOrders[0].OrderItems.Should().HaveCount(1);
    }

    // ========== 工单打印 ==========

    [Fact]
    public async Task PrintWorkOrderAsync_成功生成PDF()
    {
        var ctx = CreateDbContext();
        var (_, _, workOrderIds) = await SeedConfirmedOrderWithWorkOrdersAsync(ctx);
        var svc = CreateService(ctx);

        var pdfBytes = await svc.PrintWorkOrderAsync(workOrderIds[0]);

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        // PDF 文件头固定为 %PDF
        pdfBytes[0].Should().Be((byte)'%');
        pdfBytes[1].Should().Be((byte)'P');
        pdfBytes[2].Should().Be((byte)'D');
        pdfBytes[3].Should().Be((byte)'F');
    }

    [Fact]
    public async Task PrintWorkOrderAsync_工单不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.PrintWorkOrderAsync(99999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("工单不存在");
    }

    [Fact]
    public async Task PrintWorkOrdersByOrderAsync_成功生成批量PDF()
    {
        var ctx = CreateDbContext();
        var (_, orderNo, _) = await SeedConfirmedOrderWithWorkOrdersAsync(ctx);
        var svc = CreateService(ctx);

        var pdfBytes = await svc.PrintWorkOrdersByOrderAsync(orderNo);

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        pdfBytes[0].Should().Be((byte)'%');
    }

    [Fact]
    public async Task PrintWorkOrdersByOrderAsync_无工单_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.PrintWorkOrdersByOrderAsync("NONEXISTENT-ORDER");
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*没有可打印的工单*");
    }

    [Fact]
    public async Task PrintWorkOrdersByOrderBatchAsync_选中订单批量打印()
    {
        var ctx = CreateDbContext();
        var (_, orderNoA, _) = await SeedConfirmedOrderWithWorkOrdersAsync(ctx);
        var (_, orderNoB, _) = await SeedConfirmedOrderWithWorkOrdersAsync(ctx);
        var svc = CreateService(ctx);

        var pdfBytes = await svc.PrintWorkOrdersByOrderBatchAsync(new[] { orderNoA, orderNoB });

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        pdfBytes[0].Should().Be((byte)'%');
    }

    [Fact]
    public async Task PrintWorkOrdersByOrderAllAsync_按筛选条件全部打印()
    {
        var ctx = CreateDbContext();
        var (_, orderNo, _) = await SeedConfirmedOrderWithWorkOrdersAsync(ctx);
        var svc = CreateService(ctx);

        var pdfBytes = await svc.PrintWorkOrdersByOrderAllAsync(new WorkOrderQueryParams
        {
            Keyword = orderNo,
            PageIndex = 1,
            PageSize = 20
        });

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        pdfBytes[0].Should().Be((byte)'%');
    }

    // ========== 工单首页订单状态分页 ==========

    [Fact]
    public async Task GetOrderWorkOrderStatusPageAsync_搜索关键字筛选()
    {
        var ctx = CreateDbContext();
        var (_, orderNo, _) = await SeedConfirmedOrderWithWorkOrdersAsync(ctx);
        var svc = CreateService(ctx);

        // 按订单号搜索
        var result = await svc.GetOrderWorkOrderStatusPageAsync(new WorkOrderQueryParams
        {
            Keyword = orderNo,
            PageIndex = 1,
            PageSize = 20
        });

        result.Should().NotBeNull();
        result.Items.Should().Contain(i => i.OrderNumber == orderNo);
        result.Items[0].WorkOrderStatus.Should().Be(WorkOrderStatus.Confirmed);
    }

    [Fact]
    public async Task GetOrderWorkOrderStatusPageAsync_按工单状态筛选()
    {
        var ctx = CreateDbContext();
        var (_, orderNo, _) = await SeedConfirmedOrderWithWorkOrdersAsync(ctx);
        var svc = CreateService(ctx);

        // 筛选已确认的工单
        var result = await svc.GetOrderWorkOrderStatusPageAsync(new WorkOrderQueryParams
        {
            Keyword = orderNo,
            WorkOrderStatus = "Confirmed",
            PageIndex = 1,
            PageSize = 20
        });

        result.Should().NotBeNull();
        result.Items.Should().Contain(i => i.OrderNumber == orderNo);
        result.Items.All(i => i.WorkOrderStatus == WorkOrderStatus.Confirmed).Should().BeTrue();
    }

    [Fact]
    public async Task GetOrderWorkOrderStatusPageAsync_按客户名称搜索()
    {
        var ctx = CreateDbContext();
        // 创建测试订单（客户名在 SeedConfirmedOrderAsync 中为"测试客户"）
        var (_, orderNo, _) = await SeedConfirmedOrderWithWorkOrdersAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetOrderWorkOrderStatusPageAsync(new WorkOrderQueryParams
        {
            Keyword = "测试客户",
            PageIndex = 1,
            PageSize = 20
        });

        result.Should().NotBeNull();
        result.Items.Should().Contain(i => i.OrderNumber == orderNo);
    }

    [Fact]
    public async Task GetOrderWorkOrderStatusPageAsync_按业务员搜索()
    {
        var ctx = CreateDbContext();
        var (_, orderNo, _) = await SeedConfirmedOrderWithWorkOrdersAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetOrderWorkOrderStatusPageAsync(new WorkOrderQueryParams
        {
            Keyword = "测试业务员",
            PageIndex = 1,
            PageSize = 20
        });

        result.Should().NotBeNull();
        result.Items.Should().Contain(i => i.OrderNumber == orderNo);
    }

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_关键词搜索牌号_返回匹配()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        ctx.WorkOrders.Add(new WorkOrder
        {
            WorkOrderNo = $"WO-KW-{Guid.NewGuid():N}"[..15],
            SalesOrderNo = "SO-KWTEST",
            ProductionMainNo = "D01",
            ProductionSubNo = "C01",
            Status = WorkOrderStatus.Pending,
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = MaterialName.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = ps.StandardCode,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219*8",
            OrderItemIds = "[1]",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            TotalQuantity = 10,
            TotalMeters = 60,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            RowVersion = new byte[8]
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new WorkOrderQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "20#" });

        result.Items.Should().NotBeEmpty();
        result.Items.Any(i => i.PlantGrade == "20#").Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索规格_返回匹配()
    {
        var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        ctx.WorkOrders.Add(new WorkOrder
        {
            WorkOrderNo = $"WO-KW-{Guid.NewGuid():N}"[..15],
            SalesOrderNo = "SO-KWTEST",
            ProductionMainNo = "D01",
            ProductionSubNo = "C01",
            Status = WorkOrderStatus.Pending,
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = MaterialName.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = ps.StandardCode,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219*8",
            OrderItemIds = "[1]",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            TotalQuantity = 10,
            TotalMeters = 60,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            RowVersion = new byte[8]
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new WorkOrderQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "219" });

        result.Items.Should().NotBeEmpty();
        result.Items.Any(i => i.Specification == "219*8").Should().BeTrue();
    }
}
