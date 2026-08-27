using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.WorkOrder;
using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Models;
using MES.Data.Entities;
using MES.Data.Entities.Order;
using MES.Data.Entities.WorkOrder;
using MES.Services.WorkOrder;
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
        return new WorkOrderService(ctx, loggerMock.Object, new Mock<IOperationLogService>().Object, new MemoryCache(new MemoryCacheOptions()));
    }

    private async Task<(int OrderId, string OrderNo)> SeedConfirmedOrderAsync(AppDbContext ctx)
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
            OrderNumber = $"WO-TEST-{Guid.NewGuid():N}"[..15],
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
            Status = SalesOrderStatus.Confirmed,
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
                new() { ProductionMainNo = "X01", ProductionSubNo = "01", OrderItemIds = itemIds }
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
                new() { ProductionMainNo = "X01", ProductionSubNo = "01", OrderItemIds = new List<int> { 1 } }
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
                    ProductionMainNo = "X01",
                    ProductionSubNo = "01",
                    OrderItemIds = itemIds
                }
            }
        });

        result.Should().HaveCount(1);
        result[0].WorkOrderNo.Should().Be($"{orderNo}-X01-01");
        result[0].SalesOrderNo.Should().Be(orderNo);
        result[0].ProductionMainNo.Should().Be("X01");
        result[0].ProductionSubNo.Should().Be("01");
        result[0].Status.Should().Be(WorkOrderStatus.Confirmed);
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
                new() { ProductionMainNo = "X01", ProductionSubNo = "01", OrderItemIds = itemIds }
            }
        });

        // 第二次生成（覆盖）
        await svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "X02", ProductionSubNo = "02", OrderItemIds = itemIds }
            }
        });

        // 应该只有第二次的工单
        var workOrders = await ctx.WorkOrders
            .Where(wo => wo.SalesOrderNo == orderNo)
            .ToListAsync();

        workOrders.Should().HaveCount(1);
        workOrders[0].ProductionMainNo.Should().Be("X02");
    }

    [Fact]
    public async Task GenerateWorkOrdersAsync_次号为空_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);
        var itemIds = await ctx.OrderItems
            .Where(oi => oi.SalesOrderId == orderId)
            .Select(oi => oi.Sequence).ToListAsync();
        var svc = CreateService(ctx);

        var act = () => svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "X01", ProductionSubNo = null, OrderItemIds = itemIds }
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*次号不能为空*");
    }

    [Fact]
    public async Task GenerateWorkOrdersAsync_定尺次号格式非法_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);
        var itemIds = await ctx.OrderItems
            .Where(oi => oi.SalesOrderId == orderId)
            .Select(oi => oi.Sequence).ToListAsync();
        var svc = CreateService(ctx);

        var act = () => svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "X01", ProductionSubNo = "123", OrderItemIds = itemIds }
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*两位数字*");
    }

    [Fact]
    public async Task GenerateWorkOrdersAsync_非定尺次号非F0_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (orderId, orderNo) = await SeedConfirmedOrderAsync(ctx);

        // 将订单项次改为非定尺，次号必须为 F0
        var items = await ctx.OrderItems.Where(oi => oi.SalesOrderId == orderId).ToListAsync();
        foreach (var i in items) i.LengthStatus = LengthStatus.NonFixed;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var act = () => svc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = orderNo,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new() { ProductionMainNo = "X01", ProductionSubNo = "01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*F0*");
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
                new() { ProductionMainNo = "X01", ProductionSubNo = "01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
            }
        });

        var wo = generated[0];
        var dto = await svc.GetByIdAsync(wo.Id);
        dto.Status.Should().Be(WorkOrderStatus.Confirmed);

        // Confirmed -> Pending
        var updated = await svc.UpdateStatusAsync(wo.Id, new UpdateWorkOrderStatusRequest
        {
            Status = WorkOrderStatus.Pending,
            RowVersion = dto.RowVersion
        });
        updated.Status.Should().Be(WorkOrderStatus.Pending);
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
                new() { ProductionMainNo = "X01", ProductionSubNo = "01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
            }
        });

        // Confirmed -> Pending （合法）
        var wo = generated[0];
        var dto = await svc.GetByIdAsync(wo.Id);
        await svc.UpdateStatusAsync(wo.Id, new UpdateWorkOrderStatusRequest
        {
            Status = WorkOrderStatus.Pending,
            RowVersion = dto.RowVersion
        });

        // Pending -> Confirmed （合法）
        var dto2 = await svc.GetByIdAsync(wo.Id);
        await svc.UpdateStatusAsync(wo.Id, new UpdateWorkOrderStatusRequest
        {
            Status = WorkOrderStatus.Confirmed,
            RowVersion = dto2.RowVersion
        });

        // Confirmed -> NotGenerated （非法）
        var dto3 = await svc.GetByIdAsync(wo.Id);
        var act = () => svc.UpdateStatusAsync(wo.Id, new UpdateWorkOrderStatusRequest
        {
            Status = WorkOrderStatus.NotGenerated,
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
                new() { ProductionMainNo = "X01", ProductionSubNo = "01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
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
                new() { ProductionMainNo = "X01", ProductionSubNo = "01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
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
            RawMaterialType = MaterialType.RoundBar,
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
                new() { ProductionMainNo = "X01", ProductionSubNo = "01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
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
            RawMaterialType = MaterialType.RoundBar,
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredPieces = 10,
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        };
        ctx.RoundBarPiercingPlans.Add(piercing);
        await ctx.SaveChangesAsync();

        // 插入读模型数据（因为 GetPagedWithPlansAsync 现在查询 WorkOrderListSummary 表）
        var wo = await ctx.WorkOrders.FindAsync(generated[0].Id);
        var salesOrder = await ctx.SalesOrders.FirstAsync(so => so.OrderNumber == wo!.SalesOrderNo);
        var orderItem = await ctx.OrderItems.FirstAsync(oi => oi.SalesOrderId == salesOrder.Id);
        var customer = await ctx.CustomerProfiles.FirstOrDefaultAsync(c => c.CustomerUnit == salesOrder.CustomerName);
        ctx.Set<WorkOrderListSummary>().Add(new WorkOrderListSummary
        {
            WorkOrderId = wo!.Id,
            WorkOrderNo = wo.WorkOrderNo,
            SalesOrderNo = wo.SalesOrderNo,
            ProductionMainNo = wo.ProductionMainNo,
            ProductionSubNo = wo.ProductionSubNo,
            SignDate = salesOrder?.SignDate ?? DateTime.Today,
            Salesman = customer?.Salesman ?? "",
            EndCustomer = customer?.EndCustomer,
            DeliveryDate = orderItem.DeliveryDate,
            DelayPenalty = orderItem.DelayPenalty,
            SettlementMethod = orderItem.SettlementMethod.ToString(),
            MaterialName = orderItem.PipeManufacturingType.ToString(),
            PlantGrade = orderItem.PlantGrade ?? "",
            Specification = $"{orderItem.OuterDiameter}*{orderItem.WallThickness}",
            LengthStatus = orderItem.LengthStatus.ToString(),
            MinLength = orderItem.MinLength,
            MaxLength = orderItem.MaxLength,
            TotalQuantity = orderItem.Quantity ?? 0,
            TotalWeight = orderItem.ContractWeight,
            TotalItemCount = 1,
            TechnicalRequirements = "Normal",
            Status = (int)wo.Status,
            CreatedTime = DateTimeOffset.UtcNow,
            DeliveryState = orderItem.DeliveryState.ToString(),
            PiercingPlanTotalWeight = 3000m,
            PiercingPlanTotalPieces = 10
        });
        await ctx.SaveChangesAsync();

        // 查询列表（含用料计划数据）
        var result = await svc.GetPagedWithPlansAsync(new WorkOrderQueryParams
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
    public async Task GetPagedWithPlansAsync_关键字匹配用料占比()
    {
        var ctx = CreateDbContext();
        var (_, _, woIdsA) = await SeedConfirmedOrderWithWorkOrdersAsync(ctx);
        var (_, _, woIdsB) = await SeedConfirmedOrderWithWorkOrdersAsync(ctx);

        var woA = await ctx.WorkOrders.FindAsync(woIdsA[0]);
        var woB = await ctx.WorkOrders.FindAsync(woIdsB[0]);

        ctx.Set<WorkOrderListSummary>().Add(new WorkOrderListSummary
        {
            WorkOrderId = woA!.Id,
            WorkOrderNo = woA.WorkOrderNo,
            SalesOrderNo = woA.SalesOrderNo,
            ProductionMainNo = woA.ProductionMainNo,
            ProductionSubNo = woA.ProductionSubNo,
            SignDate = woA.SignDate,
            Salesman = woA.Salesman,
            DeliveryDate = woA.DeliveryDate,
            SettlementMethod = woA.SettlementMethod.ToString(),
            MaterialName = woA.PipeManufacturingType.ToString(),
            PlantGrade = woA.PlantGrade,
            Specification = woA.Specification,
            LengthStatus = woA.LengthStatus.ToString(),
            MinLength = woA.MinLength,
            MaxLength = woA.MaxLength,
            TotalQuantity = woA.TotalQuantity,
            TotalWeight = woA.TotalWeight,
            TotalItemCount = 1,
            TechnicalRequirements = "Normal",
            Status = (int)woA.Status,
            CreatedTime = DateTimeOffset.UtcNow,
            DeliveryState = woA.DeliveryState.ToString(),
            MaterialPlanProportion = "穿105% 荒60% 成20% 库40%"
        });
        ctx.Set<WorkOrderListSummary>().Add(new WorkOrderListSummary
        {
            WorkOrderId = woB!.Id,
            WorkOrderNo = woB.WorkOrderNo,
            SalesOrderNo = woB.SalesOrderNo,
            ProductionMainNo = woB.ProductionMainNo,
            ProductionSubNo = woB.ProductionSubNo,
            SignDate = woB.SignDate,
            Salesman = woB.Salesman,
            DeliveryDate = woB.DeliveryDate,
            SettlementMethod = woB.SettlementMethod.ToString(),
            MaterialName = woB.PipeManufacturingType.ToString(),
            PlantGrade = woB.PlantGrade,
            Specification = woB.Specification,
            LengthStatus = woB.LengthStatus.ToString(),
            MinLength = woB.MinLength,
            MaxLength = woB.MaxLength,
            TotalQuantity = woB.TotalQuantity,
            TotalWeight = woB.TotalWeight,
            TotalItemCount = 1,
            TechnicalRequirements = "Normal",
            Status = (int)woB.Status,
            CreatedTime = DateTimeOffset.UtcNow,
            DeliveryState = woB.DeliveryState.ToString(),
            MaterialPlanProportion = "穿0% 荒0% 库100%"
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedWithPlansAsync(new WorkOrderQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Keyword = "成20"
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().Id.Should().Be(woA.Id);
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
                new() { ProductionMainNo = "X01", ProductionSubNo = "01", OrderItemIds = items.Select(i => i.Sequence).ToList() }
            }
        });

        var relation = await svc.GetOrderWorkOrderRelationAsync(orderNo);

        relation.Should().NotBeNull();
        relation.OrderNumber.Should().Be(orderNo);
        relation.WorkOrders.Should().HaveCount(1);
        relation.WorkOrders[0].ProductionMainNo.Should().Be("X01");
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
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        ctx.WorkOrders.Add(new MES.Data.Entities.WorkOrder.WorkOrder
        {
            WorkOrderNo = $"WO-KW-{Guid.NewGuid():N}"[..15],
            SalesOrderNo = "SO-KWTEST",
            ProductionMainNo = "X01",
            ProductionSubNo = "01",
            Status = WorkOrderStatus.Pending,
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = sr.StandardNo,
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
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        ctx.WorkOrders.Add(new MES.Data.Entities.WorkOrder.WorkOrder
        {
            WorkOrderNo = $"WO-KW-{Guid.NewGuid():N}"[..15],
            SalesOrderNo = "SO-KWTEST",
            ProductionMainNo = "X01",
            ProductionSubNo = "01",
            Status = WorkOrderStatus.Pending,
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = sr.StandardNo,
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

    // ========== 筛选上下文 ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        ctx.Set<WorkOrderListSummary>().AddRange(
            new WorkOrderListSummary
            {
                WorkOrderNo = "WO001",
                SalesOrderNo = "SO001",
                ProductionMainNo = "X01",
                ProductionSubNo = "01",
                SignDate = DateTime.Today,
                Salesman = "张三",
                DeliveryDate = DateTime.Today.AddMonths(1),
                PlantGrade = "304",
                Specification = "219*8",
                Status = (int)WorkOrderStatus.Confirmed,
                SettlementMethod = "Theoretical",
                MaterialName = "无缝管",
                DeliveryState = "SolutionAnnealedAndPickled",
                LengthStatus = "Fixed",
                TechnicalRequirements = "无",
                TotalQuantity = 10,
                TotalMeters = 60,
                TotalWeight = 2500m,
                TotalItemCount = 1,
                CreatedTime = DateTimeOffset.Now,
                UpdatedTime = DateTimeOffset.Now
            },
            new WorkOrderListSummary
            {
                WorkOrderNo = "WO002",
                SalesOrderNo = "SO002",
                ProductionMainNo = "X02",
                ProductionSubNo = null,
                SignDate = DateTime.Today,
                Salesman = "李四",
                DeliveryDate = DateTime.Today.AddMonths(1),
                PlantGrade = "20#",
                Specification = "273*10",
                Status = (int)WorkOrderStatus.Pending,
                SettlementMethod = "Theoretical",
                MaterialName = "无缝管",
                DeliveryState = "SolutionAnnealedAndPickled",
                LengthStatus = "Fixed",
                TechnicalRequirements = "无",
                TotalQuantity = 5,
                TotalMeters = 30,
                TotalWeight = 2000m,
                TotalItemCount = 1,
                CreatedTime = DateTimeOffset.Now,
                UpdatedTime = DateTimeOffset.Now
            }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("WorkOrderNo", "SalesOrderNo", "ProductionMainNo", "ProductionSubNo", "SignDate", "Salesman", "PlantGrade", "Specification", "DeliveryDate");
        result["WorkOrderNo"].Should().BeEquivalentTo(new[] { "WO001", "WO002" }, options => options.WithStrictOrdering());
        result["Salesman"].Should().BeEquivalentTo(new[] { "张三", "李四" });
        result["ProductionSubNo"].Should().HaveCount(1).And.Contain("01");
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_各字段返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("WorkOrderNo", "SalesOrderNo", "ProductionMainNo", "ProductionSubNo", "SignDate", "Salesman", "EndCustomer", "DeliveryDate", "PlantGrade", "Specification", "LatestPlanDate");
        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty($"字段 {kvp.Key} 应返回空列表");
    }

    [Fact]
    public async Task PrintWorkOrderListAsync_生成列表PDF()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var items = new List<Dictionary<string, object>>
        {
            new() { ["WorkOrderNo"] = "WO001", ["SalesOrderNo"] = "SO001", ["TotalWeight"] = "2000" },
            new() { ["WorkOrderNo"] = "WO002", ["SalesOrderNo"] = "SO001", ["TotalWeight"] = "1500" }
        };
        var columns = new List<PrintColumnDef>
        {
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "SalesOrderNo", Label = "订单号" },
            new() { Key = "TotalWeight", Label = "总重量" }
        };

        var bytes = await svc.PrintWorkOrderListAsync("工单列表", items, columns);

        bytes.Should().NotBeNullOrEmpty();
        // PDF 魔数 %PDF
        System.Text.Encoding.ASCII.GetString(bytes.Take(4).ToArray()).Should().Be("%PDF");
    }
}
