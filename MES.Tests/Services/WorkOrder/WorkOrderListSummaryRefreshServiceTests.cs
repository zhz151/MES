using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MES.Core.DTOs.Configuration;
using MES.Core.Enums;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.Materials;
using MES.Data.Entities.WorkOrder;
using MES.Services.WorkOrder;
using MES.Tests.Tests;
using WoEntity = MES.Data.Entities.WorkOrder.WorkOrder;

namespace MES.Tests.Services;

/// <summary>
/// 用料计划总览读模型刷新服务测试：按订单重建行快照（工单/主号/订单三级字段）、
/// 各计划聚合重量支数与料态种数、空工单清行、全量刷新清理孤儿行。
/// </summary>
public class WorkOrderListSummaryRefreshServiceTests : TestBase
{
    private WorkOrderListSummaryRefreshService CreateService(AppDbContext ctx)
    {
        // 配置表类别返回空字典（GetConfig 走默认阈值/默认周期 22 天），日产估算返回空
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var dailyMock = new Mock<IDailyOutputEstimateService>();
        dailyMock.Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<DailyOutputEstimateDto>());
        return new WorkOrderListSummaryRefreshService(
            ctx, NullLogger<WorkOrderListSummaryRefreshService>.Instance, configMock.Object, dailyMock.Object);
    }

    private async Task<WoEntity> SeedWorkOrderAsync(AppDbContext ctx, string salesOrderNo = "SO-1",
        string workOrderNo = "WO-1", string mainNo = "X01", int quantity = 10, decimal weight = 2500m,
        DateTime? deliveryDate = null)
    {
        var wo = new WoEntity
        {
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = mainNo,
            ProductionSubNo = "01",
            OrderItemIds = "1",
            SignDate = new DateTime(2026, 1, 1),
            Salesman = "测试业务员",
            EndCustomer = null,
            DeliveryDate = deliveryDate ?? new DateTime(2026, 3, 1),
            DelayPenalty = false,
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "GB/T 8163",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "Q345B",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            TotalQuantity = quantity,
            TotalMeters = 0m,
            TotalWeight = weight,
            TotalItemCount = 1,
            ItemDetails = "1项,6000mm,10支;",
            Status = WorkOrderStatus.Pending,
        };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();
        return wo;
    }

    private async Task AddSummaryRowAsync(AppDbContext ctx, string salesOrderNo)
    {
        ctx.Set<WorkOrderListSummary>().Add(new WorkOrderListSummary
        {
            WorkOrderNo = "X",
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = "X01",
            DeliveryState = "None",
            LengthStatus = "None",
            MaterialName = "None",
            PlantGrade = "X",
            Salesman = "X",
            SettlementMethod = "None",
            Specification = "X",
            TechnicalRequirements = "None"
        });
        await ctx.SaveChangesAsync();
    }

    // ========== 无工单订单 → 清空该订单读模型行 ==========

    [Fact]
    public async Task RefreshBySalesOrder_订单无工单_清空已有读模型行()
    {
        var ctx = CreateDbContext();
        await AddSummaryRowAsync(ctx, "SO-GHOST");
        var svc = CreateService(ctx);

        await svc.RefreshBySalesOrderAsync("SO-GHOST");

        ctx.Set<WorkOrderListSummary>().Should().BeEmpty();
    }

    // ========== 有工单无计划 → 重建单行快照 + 主号级字段默认口径 ==========

    [Fact]
    public async Task RefreshBySalesOrder_有工单无计划_重建行快照()
    {
        var ctx = CreateDbContext();
        var wo = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        await svc.RefreshBySalesOrderAsync("SO-1");

        var row = await ctx.Set<WorkOrderListSummary>().SingleAsync();
        row.WorkOrderId.Should().Be(wo.Id);
        row.WorkOrderNo.Should().Be("WO-1");
        row.SalesOrderNo.Should().Be("SO-1");
        row.ProductionMainNo.Should().Be("X01");
        row.PlantGrade.Should().Be("Q345B");
        row.Specification.Should().Be("219*8");
        row.TotalQuantity.Should().Be(10);
        row.TotalWeight.Should().Be(2500m);
        // 无任何计划：满足率 0、NotPlanned、无聚合、料态 0 种
        row.MaterialPlanRate.Should().Be(0);
        row.MaterialPlanStatus.Should().Be((int)MaterialPlanStatus.NotPlanned);
        row.SemiPlanTotalWeight.Should().BeNull();
        row.MaterialPlanCoveredCount.Should().Be(0);
        row.MaxStandardCycle.Should().Be(0);
        row.LatestPlanDate.Should().BeNull();
    }

    [Fact]
    public async Task RefreshBySalesOrder_未满足主号_默认周期22天且截止日可算()
    {
        var ctx = CreateDbContext();
        await SeedWorkOrderAsync(ctx, deliveryDate: new DateTime(2026, 3, 1));
        var svc = CreateService(ctx);

        await svc.RefreshBySalesOrderAsync("SO-1");

        var row = await ctx.Set<WorkOrderListSummary>().SingleAsync();
        row.MainNoMaxStandardCycle.Should().Be(22); // NotPlanned → 默认工艺周期
        row.CapacityWorkDays.Should().BeNull();      // 无日产估算 → 无法折算
        row.TheoreticalCutoffDate.Should().Be(new DateTime(2026, 3, 1).AddDays(-22));
    }

    // ========== 有半成品计划 → 聚合重量/支数 + 料态种数 + 满足率 ==========

    [Fact]
    public async Task RefreshBySalesOrder_半成品计划_聚合重量支数与料态种数()
    {
        var ctx = CreateDbContext();
        var wo = await SeedWorkOrderAsync(ctx);
        ctx.PurchaseSemiPlans.Add(new PurchaseSemiPlan
        {
            WorkOrderId = wo.Id,
            PlanDate = new DateTime(2026, 2, 1),
            InputMultiple = 1,
            RequiredPieces = 20,
            RequiredWeight = 1000m,
            RequiredDate = new DateTime(2026, 2, 10),
            StandardCycle = 5,
            PlantGrade = "Q345B",
            RawMaterialSpec = "230*9"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        await svc.RefreshBySalesOrderAsync("SO-1");

        var row = await ctx.Set<WorkOrderListSummary>().SingleAsync();
        row.SemiPlanTotalWeight.Should().Be(1000m);
        row.SemiPlanTotalPieces.Should().Be(20);
        row.MaterialPlanCoveredCount.Should().Be(1);
        row.MaterialPlanRate.Should().Be(200); // 20 支 / 需求 10 支
        row.LatestPlanDate.Should().Be(new DateTime(2026, 2, 1));
        row.LatestRequiredDate.Should().Be(new DateTime(2026, 2, 10));
        row.MaxStandardCycle.Should().Be(5);
        row.MaterialPlanProportion.Should().Be("荒200%");
    }

    // ========== 全量刷新 → 清理孤儿残留行，工单行保留重建 ==========

    [Fact]
    public async Task RefreshAll_清理孤儿读模型行_重建有效工单行()
    {
        var ctx = CreateDbContext();
        await SeedWorkOrderAsync(ctx);
        await AddSummaryRowAsync(ctx, "SO-ORPHAN"); // 无对应工单的残留行
        var svc = CreateService(ctx);

        await svc.RefreshAllAsync();

        var salesOrders = await ctx.Set<WorkOrderListSummary>()
            .Select(s => s.SalesOrderNo)
            .ToListAsync();
        salesOrders.Should().Contain("SO-1");
        salesOrders.Should().NotContain("SO-ORPHAN");
    }
}
