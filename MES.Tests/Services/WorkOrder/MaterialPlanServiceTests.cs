using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Services;
using MES.Services.Order;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services;

/// <summary>
/// 用料计划服务测试：4种类型CRUD、测算、满足率计算、可用库存查询
/// </summary>
public class MaterialPlanServiceTests : TestBase
{
    private MaterialPlanService CreateService(AppDbContext ctx)
    {
        var loggerMock = new Mock<ILogger<MaterialPlanService>>();
        return new MaterialPlanService(ctx, loggerMock.Object);
    }

    /// <summary>
    /// 种子一个已确认的订单并生成工单，返回工单ID
    /// </summary>
    private async Task<(int WorkOrderId, string WorkOrderNo)> SeedWorkOrderAsync(AppDbContext ctx,
        LengthStatus lengthStatus = LengthStatus.Fixed,
        decimal od = 219m, decimal wt = 8m)
    {
        var cust = await SeedCustomerAsync(ctx);
        var ps = await SeedStandardAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        var notifMock = new Mock<INotificationService>();
        var orderSvc = new OrderService(ctx, new Mock<ILogger<OrderService>>().Object, notifMock.Object, null!);

        var order = await orderSvc.CreateAsync(new CreateSalesOrderRequest
        {
            OrderNumber = $"MP-TEST-{Guid.NewGuid():N}"[..15],
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductionStandardId = ps.Id,
                    StandardGrade = gm.StandardGrade,
                    MaterialName = MaterialName.SeamlessPipe,
                    OuterDiameter = od,
                    WallThickness = wt,
                    OuterDiameterNegative = 0.5m,
                    OuterDiameterPositive = 0.5m,
                    WallThicknessNegative = 0.5m,
                    WallThicknessPositive = 0.5m,
                    LengthStatus = lengthStatus,
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

        // 生成工单
        var items = await ctx.OrderItems
            .Where(oi => oi.SalesOrderId == order.Id)
            .ToListAsync();
        var itemIds = items.Select(i => i.Sequence).ToList();

        var woLoggerMock = new Mock<ILogger<WorkOrderService>>();
        var woSvc = new WorkOrderService(ctx, woLoggerMock.Object);
        var result = await woSvc.GenerateWorkOrdersAsync(new CreateWorkOrderRequest
        {
            SalesOrderNo = order.OrderNumber,
            WorkOrders = new List<WorkOrderItemGroup>
            {
                new()
                {
                    ProductionMainNo = "D01",
                    ProductionSubNo = lengthStatus == LengthStatus.NonFixed ? null : "C01",
                    OrderItemIds = itemIds
                }
            }
        });

        // 种子标准工艺生产周期（所有工单通用）
        await SeedStandardCycleAsync(ctx);

        return (result[0].Id, result[0].WorkOrderNo);
    }

    /// <summary>
    /// 种子标准工艺生产周期：两种常见测试规格
    /// </summary>
    private async Task SeedStandardCycleAsync(AppDbContext ctx)
    {
        if (!await ctx.StandardProcessCycles.AnyAsync())
        {
            ctx.StandardProcessCycles.AddRange(
                new StandardProcessCycle
                {
                    PlantGrade = "Q345B",
                    ProductSpec = "219*8",
                    DeliveryState = "固溶酸洗",
                    RawMaterialType = "荒管",
                    RawSpec = "245*10",
                    StandardCycleDays = 15
                },
                new StandardProcessCycle
                {
                    PlantGrade = "Q345B",
                    ProductSpec = "219*8",
                    DeliveryState = "固溶酸洗",
                    RawMaterialType = "荒管",
                    RawSpec = "230*7",
                    StandardCycleDays = 15
                });
            await ctx.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 种子一个库存批次
    /// </summary>
    private async Task<InventoryBatch> SeedInventoryBatchAsync(AppDbContext ctx,
        string plantGrade = "Q345B",
        string specification = "219*8",
        decimal od = 219m, decimal wt = 8m,
        int quantity = 100, decimal weight = 10000m,
        decimal unitWeight = 250m)
    {
        var batch = new InventoryBatch
        {
            BatchNo = $"BATCH-{Guid.NewGuid():N}"[..20],
            WarehouseId = 1,
            MaterialType = "备料成品",
            PlantGrade = plantGrade,
            Specification = specification,
            InboundSource = "采购入库",
            SourceName = "测试供应商",
            InboundDate = DateTime.Today,
            LengthStatus = "Fixed",
            MinLength = 6000m,
            MaxLength = 6000m,
            InitialQuantity = quantity,
            InitialWeight = weight,
            UnitWeight = unitWeight,
            RemainingQuantity = quantity,
            RemainingWeight = weight,
            ActualOuterDiameter = od,
            ActualWallThickness = wt
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    // ========== 原料采购计划 CRUD ==========

    [Fact]
    public async Task GetSemiPlansAsync_无计划_返回空列表()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var plans = await svc.GetSemiPlansAsync(woId);

        plans.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSemiPlanByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetSemiPlanByIdAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task CreateSemiPlanAsync_定尺_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var result = await svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = "SemiFinished",
            RawMaterialSpec = "245*10",
            RequiredPieces = 10,
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        result.Should().NotBeNull();
        result.WorkOrderId.Should().Be(woId);
        result.RawMaterialSpec.Should().Be("245*10");
        result.Density.Should().Be(7.85m);
        result.UnitWeight.Should().BeGreaterThan(0);
        result.RequiredPieces.Should().Be(10);

        // 验证数据库中有记录
        var plans = await ctx.PurchaseSemiPlans.Where(p => p.WorkOrderId == woId).ToListAsync();
        plans.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateSemiPlanAsync_工单不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = 999,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = "SemiFinished",
            RawMaterialSpec = "245*10",
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task CreateSemiPlanAsync_非定尺无RequiredPieces_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.NonFixed);
        var svc = CreateService(ctx);

        var act = () => svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = "SemiFinished",
            RawMaterialSpec = "245*10",
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*非定尺*需求支数*");
    }

    [Fact]
    public async Task DeleteSemiPlanAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var created = await svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = "SemiFinished",
            RawMaterialSpec = "245*10",
            RequiredPieces = 10,
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        await svc.DeleteSemiPlanAsync(created.Id);

        var act = () => svc.GetSemiPlanByIdAsync(created.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task DeleteSemiPlanAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteSemiPlanAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== 成品采购计划 CRUD ==========

    [Fact]
    public async Task GetFinishedPlansAsync_无计划_返回空列表()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var plans = await svc.GetFinishedPlansAsync(woId);

        plans.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFinishedPlanAsync_定尺_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var result = await svc.CreateFinishedPlanAsync(new CreatePurchaseFinishedPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            ProductType = "Critical",
            RequiredPiece = 10,
            RequiredWeight = 2500m,
            RequiredDate = DateTime.Today.AddMonths(1),
            PlantGrade = "Q345B",
            Specification = "89*10",
            OuterDiameterNegative = 0.3m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.5m,
            LengthStatus = "Fixed",
            DeliveryState = "SolutionAnnealedAndPickled"
        });

        result.Should().NotBeNull();
        result.WorkOrderId.Should().Be(woId);
        result.RequiredPiece.Should().Be(10);

        var plans = await ctx.PurchaseFinishedPlans.Where(p => p.WorkOrderId == woId).ToListAsync();
        plans.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateFinishedPlanAsync_定尺无支数_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var act = () => svc.CreateFinishedPlanAsync(new CreatePurchaseFinishedPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            ProductType = "Critical",
            RequiredWeight = 2500m,
            PlantGrade = "Q345B",
            Specification = "89*10",
            OuterDiameterNegative = 0.3m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.5m,
            LengthStatus = "Fixed",
            DeliveryState = "SolutionAnnealedAndPickled"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*采购支数*");
    }

    [Fact]
    public async Task DeleteFinishedPlanAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.NonFixed);
        var svc = CreateService(ctx);

        var created = await svc.CreateFinishedPlanAsync(new CreatePurchaseFinishedPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            ProductType = "Order",
            RequiredWeight = 2500m,
            PlantGrade = "Q345B",
            Specification = "89*10",
            OuterDiameterNegative = 0.3m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.5m,
            LengthStatus = "Fixed",
            DeliveryState = "SolutionAnnealedAndPickled"
        });

        await svc.DeleteFinishedPlanAsync(created.Id);

        var act = () => svc.GetFinishedPlanByIdAsync(created.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== 库存使用计划 CRUD ==========

    [Fact]
    public async Task CreateInventoryPlanAsync_全部使用模式_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().Be(batch.BatchNo);
        result.UsedQuantity.Should().Be(batch.RemainingQuantity);
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_部分使用模式_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 10,
            UsedWeight = 1000m
        });

        result.Should().NotBeNull();
        result.UsedQuantity.Should().Be(10);
        result.UsedWeight.Should().Be(1000m);
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_批次已被引用_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 第一次创建成功
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        // 第二次引用同一批次应失败
        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId2,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已被其他工单*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = "NON_EXISTENT",
            UsageMode = "All"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task CreateInventoryPlanAsync_部分模式用量超库存_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx, quantity: 10, weight: 1000m);
        var svc = CreateService(ctx);

        var act = () => svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "Partial",
            UsedQuantity = 20, // 超过库存10
            UsedWeight = 2000m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*超过库存*");
    }

    // ========== 用料测算 ==========

    [Fact]
    public async Task CalculateAsync_定尺_完整计算()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        var svc = CreateService(ctx);

        var result = await svc.CalculateAsync(new MaterialCalculateRequest
        {
            WorkOrderId = woId,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m
        });

        result.Density.Should().Be(7.85m);
        result.UnitWeightPerMeter.Should().BeGreaterThan(0);
        result.UnitWeight.Should().NotBeNull().And.BeGreaterThan(0);
        result.RawUnitWeight.Should().NotBeNull().And.BeGreaterThan(0);
        result.RequiredPieces.Should().NotBeNull().And.BeGreaterThan(0);
        result.RequiredWeight.Should().NotBeNull().And.BeGreaterThan(0);
    }

    [Fact]
    public async Task CalculateAsync_非定尺_不计算单重()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.NonFixed);
        var svc = CreateService(ctx);

        var result = await svc.CalculateAsync(new MaterialCalculateRequest
        {
            WorkOrderId = woId,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m
        });

        result.Density.Should().Be(7.85m);
        result.UnitWeightPerMeter.Should().BeGreaterThan(0);
        result.UnitWeight.Should().BeNull();
        result.RawUnitWeight.Should().BeNull();
        result.RequiredPieces.Should().BeNull();
        result.RequiredWeight.Should().BeNull();
    }

    [Fact]
    public async Task CalculateAsync_工单不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CalculateAsync(new MaterialCalculateRequest
        {
            WorkOrderId = 999,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task CalculateAsync_默认牌号密度()
    {
        var ctx = CreateDbContext();
        // 先用有效牌号创建工单，再删除牌号映射，使默認密度生效
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);

        // 删除所有 StandardGradeMapping，让 CalculateAsync 找不到牌号
        ctx.StandardGradeMappings.RemoveRange(ctx.StandardGradeMappings);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var calc = await svc.CalculateAsync(new MaterialCalculateRequest
        {
            WorkOrderId = woId,
            AdjustedWallThickness = 8m,
            YieldRate = 90m,
            InputMultiple = 1,
            QualifiedRate = 98m
        });

        calc.Density.Should().Be(7.93m); // 默认密度
    }

    // ========== 计划状态汇总 ==========

    [Fact]
    public async Task GetWorkOrderMaterialPlanAsync_无任何计划_返回NotPlanned()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var dto = await svc.GetWorkOrderMaterialPlanAsync(woId);

        dto.Should().NotBeNull();
        dto.MaterialPlanStatus.Should().Be(MaterialPlanStatus.NotPlanned);
        dto.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWorkOrderMaterialPlanAsync_有原料采购计划_返回汇总()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        await svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = "SemiFinished",
            RawMaterialSpec = "245*10",
            RequiredPieces = 10,
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        var dto = await svc.GetWorkOrderMaterialPlanAsync(woId);

        dto.Items.Should().HaveCount(1);
        dto.Items[0].PlanType.Should().Be("Semi");
        dto.MaterialPlanStatus.Should().NotBe(MaterialPlanStatus.NotPlanned);
    }

    [Fact]
    public async Task UpdateMaterialPlanStatusAsync_多类型计划_聚合状态正确()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        // 先标记工单的TotalQuantity/TotalWeight以便计算满足率
        var wo = await ctx.WorkOrders.FindAsync(woId);
        wo!.TotalQuantity = 10;
        wo.TotalWeight = 2500m;
        await ctx.SaveChangesAsync();

        // 创建原料采购计划
        await svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = "SemiFinished",
            RawMaterialSpec = "245*10",
            RequiredPieces = 10,
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        // 创建成品采购计划
        await svc.CreateFinishedPlanAsync(new CreatePurchaseFinishedPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            ProductType = "Critical",
            RequiredPiece = 10,
            RequiredWeight = 2500m,
            PlantGrade = "Q345B",
            Specification = "89*10",
            OuterDiameterNegative = 0.3m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.5m,
            LengthStatus = "Fixed",
            DeliveryState = "SolutionAnnealedAndPickled"
        });

        // 验证状态已更新
        await ctx.Entry(wo).ReloadAsync();
        wo.MaterialPlanStatus.Should().NotBe(MaterialPlanStatus.NotPlanned);
        wo.MaterialPlanRate.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteSemiPlan_更新状态为NotPlanned()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var created = await svc.CreateSemiPlanAsync(new CreatePurchaseSemiPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 7.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "Q345B",
            RawMaterialType = "SemiFinished",
            RawMaterialSpec = "245*10",
            RequiredPieces = 10,
            RequiredWeight = 1000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        // 删除后，状态恢复为未计划
        await svc.DeleteSemiPlanAsync(created.Id);

        var wo = await ctx.WorkOrders.FindAsync(woId);
        wo!.MaterialPlanStatus.Should().Be(MaterialPlanStatus.NotPlanned);
        wo.MaterialPlanRate.Should().Be(0);
    }

    // ========== 可用库存查询 ==========

    [Fact]
    public async Task GetAvailableInventoryAsync_返回可用批次()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        var batch = await SeedInventoryBatchAsync(ctx, specification: "219*8", od: 219m, wt: 8m);
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableInventoryAsync(woId);

        available.Should().NotBeEmpty();
        available[0].BatchNo.Should().Be(batch.BatchNo);
    }

    [Fact]
    public async Task GetAvailableInventoryAsync_已被使用的批次_不显示()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var batch = await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 先创建计划引用该批次
        await svc.CreateInventoryPlanAsync(new CreateInventoryPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            InventoryBatchNo = batch.BatchNo,
            UsageMode = "All",
            UsedWeight = batch.RemainingWeight
        });

        var (woId2, _) = await SeedWorkOrderAsync(ctx);
        var available = await svc.GetAvailableInventoryAsync(woId2);

        available.Should().NotContain(a => a.Id == batch.Id);
    }

    [Fact]
    public async Task GetAvailableInventoryAsync_外径不匹配_排除()
    {
        var ctx = CreateDbContext();
        // 工单外径219，批次外径159——不匹配
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        await SeedInventoryBatchAsync(ctx, specification: "159*6", od: 159m, wt: 6m);
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableInventoryAsync(woId);

        available.Should().BeEmpty();
    }

    // ========== 可用改制库存查询 ==========

    [Fact]
    public async Task GetAvailableReworkInventoryAsync_空拉改制_返回匹配批次()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        // 空拉改制需要外径 >= 测算OD*1.05，壁厚在0.95~1.05倍之间
        var batch = await SeedInventoryBatchAsync(ctx, specification: "250*8", od: 250m, wt: 8.2m,
            plantGrade: "Q345B", unitWeight: 270m);
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableReworkInventoryAsync(woId, ReworkType.EmptyDrawing);

        available.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAvailableReworkInventoryAsync_空拉改制外径过小_排除()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed, od: 219m, wt: 8m);
        // 外径219 * 1.05 ≈ 230，批次外径200太小，壁厚保持在合适范围内以排除外径因素
        await SeedInventoryBatchAsync(ctx, specification: "200*8", od: 200m, wt: 8m,
            plantGrade: "Q345B", unitWeight: 270m);
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableReworkInventoryAsync(woId, ReworkType.EmptyDrawing);

        available.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableReworkInventoryAsync_空拉改制不匹配规格_返回空()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        await SeedInventoryBatchAsync(ctx);
        var svc = CreateService(ctx);

        var available = await svc.GetAvailableReworkInventoryAsync(woId, ReworkType.EmptyDrawing);

        available.Should().BeEmpty();
    }

    // ========== 圆棒穿孔计划 CRUD ==========

    [Fact]
    public async Task GetPiercingPlansAsync_无计划_返回空列表()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx);
        var svc = CreateService(ctx);

        var plans = await svc.GetPiercingPlansAsync(woId);

        plans.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePiercingPlanAsync_定尺_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var result = await svc.CreatePiercingPlanAsync(new CreateRoundBarPiercingPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = "RoundBar",
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredUnitWeight = 300m,
            RequiredPieces = 10,
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1),
            ProcessPlan = "[{\"step\":1,\"spec\":\"250*8\"},{\"step\":2,\"spec\":\"230*7\"}]",
            Remark = "穿孔测试"
        });

        result.Should().NotBeNull();
        result.WorkOrderId.Should().Be(woId);
        result.RoundBarSpec.Should().Be("250*8");
        result.PiercingSpec.Should().Be("230*7");
        result.Density.Should().Be(7.85m);
        result.RequiredPieces.Should().Be(10);
        result.Remark.Should().Be("穿孔测试");

        // 验证数据库中有记录
        var plans = await ctx.RoundBarPiercingPlans.Where(p => p.WorkOrderId == woId).ToListAsync();
        plans.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreatePiercingPlanAsync_非定尺_成功创建()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.NonFixed);
        var svc = CreateService(ctx);

        var result = await svc.CreatePiercingPlanAsync(new CreateRoundBarPiercingPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = "RoundBar",
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredPieces = 10,
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        result.Should().NotBeNull();
        result.Should().NotBeNull();
        // 非定尺：无 RequiredUnitWeight 但仍有支数/重量
        result.RequiredPieces.Should().Be(10);
        result.RequiredWeight.Should().Be(3000m);
    }

    [Fact]
    public async Task CreatePiercingPlanAsync_工单不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreatePiercingPlanAsync(new CreateRoundBarPiercingPlanRequest
        {
            WorkOrderId = 999,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = "RoundBar",
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task DeletePiercingPlanAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var created = await svc.CreatePiercingPlanAsync(new CreateRoundBarPiercingPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = "RoundBar",
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredPieces = 10,
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        await svc.DeletePiercingPlanAsync(created.Id);

        var act = () => svc.GetPiercingPlanByIdAsync(created.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task DeletePiercingPlanAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeletePiercingPlanAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task GetWorkOrderMaterialPlanAsync_包含圆棒穿孔计划()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        // 创建圆棒穿孔计划
        await svc.CreatePiercingPlanAsync(new CreateRoundBarPiercingPlanRequest
        {
            WorkOrderId = woId,
            PlanDate = DateTime.Today,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m,
            PlantGrade = "20#",
            RawMaterialType = "RoundBar",
            RoundBarSpec = "250*8",
            PiercingSpec = "230*7",
            RequiredPieces = 10,
            RequiredWeight = 3000m,
            RequiredDate = DateTime.Today.AddMonths(1)
        });

        var tabs = await svc.GetWorkOrderMaterialPlanAsync(woId);

        tabs.Should().NotBeNull();
        tabs.Items.Should().Contain(i => i.PlanType == "Piercing");
        var piercingTab = tabs.Items.First(i => i.PlanType == "Piercing");
        piercingTab.RecordCount.Should().Be(1);
        piercingTab.Summary.Should().Contain("250*8");
    }

    [Fact]
    public async Task CalculateAsync_定尺_返回计算结果()
    {
        var ctx = CreateDbContext();
        var (woId, _) = await SeedWorkOrderAsync(ctx, LengthStatus.Fixed);
        var svc = CreateService(ctx);

        var result = await svc.CalculateAsync(new MaterialCalculateRequest
        {
            WorkOrderId = woId,
            AdjustedWallThickness = 8.5m,
            YieldRate = 85m,
            InputMultiple = 1,
            QualifiedRate = 95m
        });

        result.Should().NotBeNull();
        result.Density.Should().Be(7.85m);
    }
}
