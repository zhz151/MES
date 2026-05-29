using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Services;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services;

/// <summary>
/// 用料计划总览读模型刷新服务测试
/// </summary>
public class WorkOrderListSummaryServiceTests : TestBase
{
    private WorkOrderListSummaryService CreateService(AppDbContext ctx)
    {
        var loggerMock = new Mock<ILogger<WorkOrderListSummaryService>>();
        return new WorkOrderListSummaryService(ctx, loggerMock.Object);
    }

    /// <summary>种子一个工单并返回其 ID 和订单号</summary>
    private async Task<(int WorkOrderId, string SalesOrderNo)> SeedWorkOrderAsync(AppDbContext ctx,
        string mainNo = "D01", string? subNo = "C01", LengthStatus lengthStatus = LengthStatus.Fixed)
    {
        var cust = await SeedCustomerAsync(ctx);
        var orderNo = $"WOLS-{Guid.NewGuid():N}"[..15];

        var order = new SalesOrder
        {
            OrderNumber = orderNo,
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Status = SalesOrderStatus.Confirmed
        };
        ctx.SalesOrders.Add(order);
        await ctx.SaveChangesAsync();

        var wo = new WorkOrder
        {
            WorkOrderNo = $"{orderNo}-{mainNo}",
            SalesOrderNo = orderNo,
            ProductionMainNo = mainNo,
            ProductionSubNo = subNo,
            OrderItemIds = "1,2,3",
            SignDate = DateTime.Today,
            Status = WorkOrderStatus.Confirmed,
            LengthStatus = lengthStatus,
            MaterialName = MaterialName.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219×8",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            MinLength = 6000m,
            MaxLength = 6000m,
            TotalQuantity = 10,
            TotalMeters = 60m,
            TotalWeight = 500m,
            TotalItemCount = 3,
            TechnicalRequirements = RequirementType.Normal,
            DeliveryDate = DateTime.Today.AddMonths(1),
            DelayPenalty = false,
            Salesman = "测试业务员",
            StandardCode = "GB/T 8163"
        };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        return (wo.Id, orderNo);
    }

    // ========== RefreshAllAsync ==========

    [Fact]
    public async Task RefreshAllAsync_无工单_不创建任何汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderListSummary>().ToListAsync();
        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshAllAsync_单个工单无计划_创建汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await SeedWorkOrderAsync(ctx);

        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderListSummary>().ToListAsync();
        summaries.Should().HaveCount(1);
        var s = summaries[0];
        s.MaterialPlanRate.Should().Be(0);
        s.MaterialPlanStatus.Should().Be((int)MaterialPlanStatus.NotPlanned);
        s.SemiPlanTotalWeight.Should().BeNull();
        s.FinishedPlanTotalWeight.Should().BeNull();
        s.InventoryPlanTotalWeight.Should().BeNull();
        s.ReworkPlanTotalWeight.Should().BeNull();
        s.PiercingPlanTotalWeight.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAllAsync_多个工单_创建多个汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (id1, _) = await SeedWorkOrderAsync(ctx, "D01");
        var (id2, _) = await SeedWorkOrderAsync(ctx, "D02");

        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderListSummary>().ToListAsync();
        summaries.Should().HaveCount(2);
        summaries.Select(s => s.WorkOrderId).Should().BeEquivalentTo(new[] { id1, id2 });
    }

    // ========== RefreshByWorkOrderAsync ==========

    [Fact]
    public async Task RefreshByWorkOrderAsync_工单存在_刷新()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (woId, _) = await SeedWorkOrderAsync(ctx);

        await svc.RefreshByWorkOrderAsync(woId);

        var summaries = await ctx.Set<WorkOrderListSummary>().ToListAsync();
        summaries.Should().HaveCount(1);
        summaries[0].WorkOrderId.Should().Be(woId);
    }

    [Fact]
    public async Task RefreshByWorkOrderAsync_工单已取消_删除汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (woId, _) = await SeedWorkOrderAsync(ctx);

        // 先刷新
        await svc.RefreshAllAsync();
        (await ctx.Set<WorkOrderListSummary>().CountAsync()).Should().Be(1);

        // 取消工单
        var wo = await ctx.WorkOrders.FindAsync(woId);
        wo!.Status = WorkOrderStatus.Cancelled;
        await ctx.SaveChangesAsync();

        // 刷新 → 应删除
        await svc.RefreshByWorkOrderAsync(woId);

        var summaries = await ctx.Set<WorkOrderListSummary>().ToListAsync();
        summaries.Should().BeEmpty();
    }

    // ========== RefreshBySalesOrderAsync ==========

    [Fact]
    public async Task RefreshBySalesOrderAsync_订单无工单_删除已有汇总()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (woId, salesOrderNo) = await SeedWorkOrderAsync(ctx);

        // 先刷新
        await svc.RefreshAllAsync();
        (await ctx.Set<WorkOrderListSummary>().CountAsync()).Should().Be(1);

        // 删除所有工单（InMemory 不支持 ExecuteDeleteAsync，使用 Load + RemoveRange）
        var allWos = await ctx.WorkOrders.ToListAsync();
        ctx.WorkOrders.RemoveRange(allWos);
        await ctx.SaveChangesAsync();

        // 按订单号刷新
        await svc.RefreshBySalesOrderAsync(salesOrderNo);

        var summaries = await ctx.Set<WorkOrderListSummary>().ToListAsync();
        summaries.Should().BeEmpty();
    }

    // ========== RefreshByCustomerAsync ==========

    [Fact]
    public async Task RefreshByCustomerAsync_客户有工单_刷新()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var cust = await SeedCustomerAsync(ctx);

        // 创建 2 个同客户订单 + 工单
        for (int i = 0; i < 2; i++)
        {
            var orderNo = $"CUST-WOLS-{Guid.NewGuid():N}"[..15];
            var order = new SalesOrder
            {
                OrderNumber = orderNo,
                SignDate = DateTime.Today,
                CustomerId = cust.Id,
                Status = SalesOrderStatus.Confirmed
            };
            ctx.SalesOrders.Add(order);
            await ctx.SaveChangesAsync();

            var wo = new WorkOrder
            {
                WorkOrderNo = $"{orderNo}-D01",
                SalesOrderNo = orderNo,
                ProductionMainNo = "D01",
                ProductionSubNo = "C01",
                OrderItemIds = "1",
                SignDate = DateTime.Today,
                Status = WorkOrderStatus.Confirmed,
                LengthStatus = LengthStatus.Fixed,
                MaterialName = MaterialName.SeamlessPipe,
                SettlementMethod = SettlementMethod.Theoretical,
                DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
                PlantGrade = "20#",
                Specification = "219×8",
                OuterDiameterNegative = 0.5m,
                OuterDiameterPositive = 0.5m,
                WallThicknessNegative = 0.5m,
                WallThicknessPositive = 0.5m,
                MinLength = 6000m,
                MaxLength = 6000m,
                TotalQuantity = 10,
                TotalMeters = 60m,
                TotalWeight = 500m,
                TotalItemCount = 1,
                TechnicalRequirements = RequirementType.Normal,
                DeliveryDate = DateTime.Today.AddMonths(1),
                DelayPenalty = false,
                Salesman = "测试业务员",
                StandardCode = "GB/T 8163"
            };
            ctx.WorkOrders.Add(wo);
            await ctx.SaveChangesAsync();
        }

        await svc.RefreshByCustomerAsync(cust.Id);

        var summaries = await ctx.Set<WorkOrderListSummary>().ToListAsync();
        summaries.Should().HaveCount(2);
    }

    // ========== 计划聚合 ==========

    [Fact]
    public async Task 原料采购计划聚合_验证重量计算()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (woId, _) = await SeedWorkOrderAsync(ctx);

        ctx.PurchaseSemiPlans.Add(new PurchaseSemiPlan
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            PlantGrade = "20#",
            RawMaterialType = RawMaterialType.SemiFinished,
            RawMaterialSpec = "245×10",
            RequiredPieces = 10,
            InputMultiple = 1,
            RequiredWeight = 3000m,
            AdjustedWallThickness = 8m,
            YieldRate = 95m,
            QualifiedRate = 98m
        });
        await ctx.SaveChangesAsync();

        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderListSummary>().FirstAsync();
        s.SemiPlanTotalWeight.Should().Be(3000m);
        s.SemiPlanTotalPieces.Should().Be(10); // 10 * 1
    }

    [Fact]
    public async Task 成品采购计划聚合_验证重量计算()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (woId, _) = await SeedWorkOrderAsync(ctx);

        ctx.PurchaseFinishedPlans.Add(new PurchaseFinishedPlan
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            ProductType = FinishedProductType.Order,
            RequiredPiece = 10,
            RequiredWeight = 2000m,
            PlantGrade = "20#",
            Specification = "219×8",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled
        });
        await ctx.SaveChangesAsync();

        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderListSummary>().FirstAsync();
        s.FinishedPlanTotalWeight.Should().Be(2000m);
        s.FinishedPlanTotalPieces.Should().Be(10);
    }

    [Fact]
    public async Task 库存使用计划聚合_验证重量计算()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (woId, _) = await SeedWorkOrderAsync(ctx);

        ctx.InventoryPlans.Add(new InventoryPlan
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = "BATCH001",
            BatchNo = "BATCH001",
            MaterialType = "SeamlessPipe",
            PlantGrade = "20#",
            Specification = "219×8",
            UsedQuantity = 5,
            InputMultiple = 1,
            UsedWeight = 1500m,
            PlanStatus = InventoryPlanStatus.Planned,
            ReworkType = null
        });
        await ctx.SaveChangesAsync();

        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderListSummary>().FirstAsync();
        s.InventoryPlanTotalWeight.Should().Be(1500m);
        s.InventoryPlanTotalPieces.Should().Be(5); // 5 * 1
        s.ReworkPlanTotalWeight.Should().BeNull();
    }

    [Fact]
    public async Task 库料改制计划聚合_验证重量计算()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (woId, _) = await SeedWorkOrderAsync(ctx);

        ctx.InventoryPlans.Add(new InventoryPlan
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = "BATCH002",
            BatchNo = "BATCH002",
            MaterialType = "SeamlessPipe",
            PlantGrade = "20#",
            Specification = "219×8",
            UsedQuantity = 3,
            InputMultiple = 1,
            UsedWeight = 900m,
            PlanStatus = InventoryPlanStatus.Planned,
            ReworkType = ReworkType.EmptyDrawing
        });
        await ctx.SaveChangesAsync();

        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderListSummary>().FirstAsync();
        s.ReworkPlanTotalWeight.Should().Be(900m);
        s.ReworkPlanTotalPieces.Should().Be(3);
        s.InventoryPlanTotalWeight.Should().BeNull();
    }

    [Fact]
    public async Task 圆棒穿孔计划聚合_验证重量计算()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (woId, _) = await SeedWorkOrderAsync(ctx);

        ctx.RoundBarPiercingPlans.Add(new RoundBarPiercingPlan
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            PlantGrade = "20#",
            RawMaterialType = RawMaterialType.SemiFinished,
            RoundBarSpec = "250",
            PiercingSpec = "245×10",
            RequiredPieces = 10,
            InputMultiple = 1,
            RequiredWeight = 4000m,
            AdjustedWallThickness = 8m,
            YieldRate = 95m,
            QualifiedRate = 98m
        });
        await ctx.SaveChangesAsync();

        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderListSummary>().FirstAsync();
        s.PiercingPlanTotalWeight.Should().Be(4000m);
        s.PiercingPlanTotalPieces.Should().Be(10);
    }

    // ========== 主号级和订单级聚合 ==========

    [Fact]
    public async Task 主号级聚合_验证满足率计算_Fixed长度()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        // Fixed 长度，TotalQuantity=10
        var (woId, salesOrderNo) = await SeedWorkOrderAsync(ctx, "D01", "C01", LengthStatus.Fixed);

        // 添加 10 支原料采购计划 → 满足率 = 10/10*100 = 100%
        ctx.PurchaseSemiPlans.Add(new PurchaseSemiPlan
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            PlantGrade = "20#",
            RawMaterialType = RawMaterialType.SemiFinished,
            RawMaterialSpec = "245×10",
            RequiredPieces = 10,
            InputMultiple = 1,
            RequiredWeight = 3000m,
            AdjustedWallThickness = 8m,
            YieldRate = 95m,
            QualifiedRate = 98m
        });
        await ctx.SaveChangesAsync();

        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderListSummary>().FirstAsync();
        // 单计划：有效件数 10，总计需求 10，供给/需求 = 10/10*100 = 100%
        s.MaterialPlanRate.Should().Be(100);
        s.MainNoMaterialPlanRate.Should().Be(100);
        // Fixed + TotalQuantity=10（≤20=小批量）：100% >= 100% → Satisfied(3)
        s.MainNoMaterialPlanStatus.Should().Be((int)MaterialPlanStatus.Satisfied);
    }

    [Fact]
    public async Task 订单级聚合_多个主号_其中一个Partial()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var cust = await SeedCustomerAsync(ctx);
        var orderNo = $"ORD-AGG-{Guid.NewGuid():N}"[..15];

        var order = new SalesOrder
        {
            OrderNumber = orderNo,
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Status = SalesOrderStatus.Confirmed
        };
        ctx.SalesOrders.Add(order);
        await ctx.SaveChangesAsync();

        // 主号 D01：TotalQuantity=10，有 10 支计划 → 满足
        var wo1 = new WorkOrder
        {
            WorkOrderNo = $"{orderNo}-D01",
            SalesOrderNo = orderNo,
            ProductionMainNo = "D01",
            ProductionSubNo = "C01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Status = WorkOrderStatus.Confirmed,
            LengthStatus = LengthStatus.Fixed,
            MaterialName = MaterialName.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219×8",
            OuterDiameterNegative = 0.5m, OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m, WallThicknessPositive = 0.5m,
            MinLength = 6000m, MaxLength = 6000m,
            TotalQuantity = 10, TotalMeters = 60m, TotalWeight = 500m,
            TotalItemCount = 1,
            TechnicalRequirements = RequirementType.Normal,
            DeliveryDate = DateTime.Today.AddMonths(1),
            DelayPenalty = false,
            Salesman = "测试业务员",
            StandardCode = "GB/T 8163"
        };
        ctx.WorkOrders.Add(wo1);
        await ctx.SaveChangesAsync();

        ctx.PurchaseSemiPlans.Add(new PurchaseSemiPlan
        {
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            PlantGrade = "20#",
            RawMaterialType = RawMaterialType.SemiFinished,
            RawMaterialSpec = "245×10",
            RequiredPieces = 10,
            InputMultiple = 1,
            RequiredWeight = 3000m,
            AdjustedWallThickness = 8m,
            YieldRate = 95m,
            QualifiedRate = 98m
        });
        await ctx.SaveChangesAsync();

        // 主号 D02：TotalQuantity=10，无计划 → NotPlanned → Partial
        var wo2 = new WorkOrder
        {
            WorkOrderNo = $"{orderNo}-D02",
            SalesOrderNo = orderNo,
            ProductionMainNo = "D02",
            ProductionSubNo = "C01",
            OrderItemIds = "2",
            SignDate = DateTime.Today,
            Status = WorkOrderStatus.Confirmed,
            LengthStatus = LengthStatus.Fixed,
            MaterialName = MaterialName.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219×8",
            OuterDiameterNegative = 0.5m, OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m, WallThicknessPositive = 0.5m,
            MinLength = 6000m, MaxLength = 6000m,
            TotalQuantity = 10, TotalMeters = 60m, TotalWeight = 500m,
            TotalItemCount = 1,
            TechnicalRequirements = RequirementType.Normal,
            DeliveryDate = DateTime.Today.AddMonths(1),
            DelayPenalty = false,
            Salesman = "测试业务员",
            StandardCode = "GB/T 8163"
        };
        ctx.WorkOrders.Add(wo2);
        await ctx.SaveChangesAsync();

        await svc.RefreshBySalesOrderAsync(orderNo);

        var summaries = await ctx.Set<WorkOrderListSummary>().ToListAsync();
        summaries.Should().HaveCount(2);

        // 订单级状态应为 Partial（D01 satisfied, D02 not planned）
        foreach (var s in summaries)
        {
            s.OrderMaterialPlanStatus.Should().Be((int)MaterialPlanStatus.Partial);
        }
    }

    // ========== Non-Fixed 长度满足率计算 ==========

    [Fact]
    public async Task NonFixLength_满足率计算正确()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        // Non-fixed 长度，TotalWeight=500
        (int woId, string _) = await SeedWorkOrderAsync(ctx, "D01", null, LengthStatus.Range);

        // 添加 250kg 原料采购计划 → 满足率 = 250/500*100 = 50%
        ctx.PurchaseSemiPlans.Add(new PurchaseSemiPlan
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            PlantGrade = "20#",
            RawMaterialType = RawMaterialType.SemiFinished,
            RawMaterialSpec = "245×10",
            RequiredPieces = 5,
            InputMultiple = 1,
            RequiredWeight = 250m,
            AdjustedWallThickness = 8m,
            YieldRate = 95m,
            QualifiedRate = 98m
        });
        await ctx.SaveChangesAsync();

        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderListSummary>().FirstAsync();
        s.MaterialPlanRate.Should().Be(50);
        // Non-fixed: 50% < 100% → Partial
        s.MaterialPlanStatus.Should().Be((int)MaterialPlanStatus.Partial);
    }

    [Fact]
    public async Task 取消的工单不参与聚合()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var cust = await SeedCustomerAsync(ctx);
        var orderNo = $"CANCEL-TEST-{Guid.NewGuid():N}"[..15];

        var order = new SalesOrder
        {
            OrderNumber = orderNo,
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Status = SalesOrderStatus.Confirmed
        };
        ctx.SalesOrders.Add(order);
        await ctx.SaveChangesAsync();

        // 1 个取消工单 + 1 个正常工单
        var wo1 = new WorkOrder
        {
            WorkOrderNo = $"{orderNo}-CANCEL",
            SalesOrderNo = orderNo,
            ProductionMainNo = "D01",
            ProductionSubNo = "C01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Status = WorkOrderStatus.Cancelled,
            LengthStatus = LengthStatus.Fixed,
            MaterialName = MaterialName.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219×8",
            OuterDiameterNegative = 0.5m, OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m, WallThicknessPositive = 0.5m,
            MinLength = 6000m, MaxLength = 6000m,
            TotalQuantity = 10, TotalMeters = 60m, TotalWeight = 500m,
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
            WorkOrderNo = $"{orderNo}-ACTIVE",
            SalesOrderNo = orderNo,
            ProductionMainNo = "D01",
            ProductionSubNo = "C02",
            OrderItemIds = "2",
            SignDate = DateTime.Today,
            Status = WorkOrderStatus.Confirmed,
            LengthStatus = LengthStatus.Fixed,
            MaterialName = MaterialName.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219×8",
            OuterDiameterNegative = 0.5m, OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m, WallThicknessPositive = 0.5m,
            MinLength = 6000m, MaxLength = 6000m,
            TotalQuantity = 10, TotalMeters = 60m, TotalWeight = 500m,
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

        var summaries = await ctx.Set<WorkOrderListSummary>().ToListAsync();
        summaries.Should().HaveCount(1); // 只有 1 个非取消工单
        summaries[0].WorkOrderNo.Should().Be($"{orderNo}-ACTIVE");
    }
}
