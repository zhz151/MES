using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Services.Materials;
using MES.Tests.Tests;
using Moq;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Order;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 采购订单服务测试：CRUD、状态流转、同步、筛选、金额自动计算
/// </summary>
public class PurchaseOrderServiceTests : TestBase
{
    private PurchaseOrderService CreateService(AppDbContext ctx, Mock<IWorkOrderExecutionService>? woExecMock = null)
    {
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        woExecMock ??= new Mock<IWorkOrderExecutionService>();
        var loggerMock = new Mock<ILogger<PurchaseOrderService>>();
        return new PurchaseOrderService(ctx, configMock.Object, woExecMock.Object, loggerMock.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    private async Task<int> SeedSupplierAsync(AppDbContext ctx, string name = "测试供应商")
    {
        var entity = new SupplierProfile { SupplierCode = $"S{Guid.NewGuid():N}"[..10], SupplierName = name, IsActive = true };
        ctx.SupplierProfiles.Add(entity);
        await ctx.SaveChangesAsync();
        return entity.Id;
    }

    private async Task<PurchaseOrder> SeedOrderAsync(AppDbContext ctx, int supplierId, PurchaseOrderStatus status = PurchaseOrderStatus.Open,
        DateTime? orderDate = null, DateTime? requiredDate = null, int? quantity = 100)
    {
        var supplierName = await ctx.SupplierProfiles
            .Where(s => s.Id == supplierId)
            .Select(s => s.SupplierName)
            .FirstOrDefaultAsync();

        var order = new PurchaseOrder
        {
            OrderNo = $"CG{DateTime.Now:yyMMdd}001",
            SupplierId = supplierId,
            SupplierName = supplierName,
            OrderDate = orderDate ?? DateTime.Today,
            Status = status,
            MaterialCategory = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            Quantity = quantity,
            Weight = 1000m,
            RequiredDate = requiredDate ?? DateTime.Today.AddDays(30),
            UnitPrice = 100m,
            TotalAmount = quantity.HasValue ? quantity.Value * 100m : null
        };
        ctx.PurchaseOrders.Add(order);
        await ctx.SaveChangesAsync();
        return order;
    }

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_按订单号搜索_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid);
        // 创建第二个
        ctx.PurchaseOrders.Add(new PurchaseOrder
        {
            OrderNo = "CG20260101001",
            SupplierId = sid,
            OrderDate = DateTime.Today,
            Status = PurchaseOrderStatus.Open,
            MaterialCategory = "RoughTube",
            PlantGrade = "304",
            Specification = "273*10",
            Quantity = 50,
            Weight = 500m,
            RequiredDate = DateTime.Today.AddDays(30)
        });
        await ctx.SaveChangesAsync();

        var seedOrderNo = await ctx.PurchaseOrders
            .Where(p => p.MaterialCategory == "RoughTube" && p.PlantGrade == "20#")
            .Select(p => p.OrderNo)
            .FirstAsync();
        var svc = CreateService(ctx);

        // 按完整订单号搜索（唯一匹配）
        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = seedOrderNo });

        result.Items.Should().HaveCount(1);
        result.Items[0].OrderNo.Should().Be(seedOrderNo);
    }

    [Fact]
    public async Task GetPagedAsync_按供应商名称搜索_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx, name: "大明钢铁");
        await SeedOrderAsync(ctx, sid);
        var sid2 = await SeedSupplierAsync(ctx, name: "宝钢");
        ctx.PurchaseOrders.Add(new PurchaseOrder
        {
            OrderNo = "CG20260101002",
            SupplierId = sid2,
            OrderDate = DateTime.Today,
            Status = PurchaseOrderStatus.Open,
            MaterialCategory = "RoughTube",
            PlantGrade = "304",
            Specification = "273*10",
            Quantity = 50,
            Weight = 500m,
            RequiredDate = DateTime.Today.AddDays(30)
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "大明" });

        result.Items.Should().HaveCount(1);
        result.Items[0].SupplierName.Should().Be("大明钢铁");
    }

    [Fact]
    public async Task GetPagedAsync_按状态筛选_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid, status: PurchaseOrderStatus.Open);
        await SeedOrderAsync(ctx, sid, status: PurchaseOrderStatus.Completed, quantity: 200);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams
        { PageIndex = 1, PageSize = 20, Status = "Completed" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be(PurchaseOrderStatus.Completed);
    }

    [Fact]
    public async Task GetPagedAsync_按下单日期筛选_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid, orderDate: DateTime.Today.AddDays(-5));
        await SeedOrderAsync(ctx, sid, orderDate: DateTime.Today);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams
        { PageIndex = 1, PageSize = 20, DateFrom = DateTime.Today.AddDays(-1) });

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_按要求到货日筛选_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid, requiredDate: DateTime.Today.AddDays(30));
        await SeedOrderAsync(ctx, sid, requiredDate: DateTime.Today.AddDays(60));
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams
        { PageIndex = 1, PageSize = 20, RequiredDateFrom = DateTime.Today.AddDays(40) });

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_填充供应商名称()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx, name: "测试供应商");
        await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams { PageIndex = 1, PageSize = 20 });

        result.Items[0].SupplierName.Should().Be("测试供应商");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx, name: "测试供应商");
        var order = await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(order.Id);

        result.Should().NotBeNull();
        result.OrderNo.Should().Be(order.OrderNo);
        result.SupplierName.Should().Be("测试供应商");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("采购单不存在");
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建采购单_自动生成单号和总金额()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = sid,
            OrderDate = DateTime.Today,
            MaterialCategory = MaterialType.RoughTube,
            PlantGrade = "20#",
            Specification = "219*8",
            Quantity = 100,
            Weight = 1000m,
            RequiredDate = DateTime.Today.AddDays(30),
            UnitPrice = 50m,
            SourceWorkOrderNo = "GD20260101001"
        });

        result.Should().NotBeNull();
        result.OrderNo.Should().StartWith("CG" + DateTime.Now.ToString("yyMMdd"));
        result.TotalAmount.Should().Be(5000m); // 100 * 50
        result.SourceWorkOrderNo.Should().Be("GD20260101001");

        var saved = await ctx.PurchaseOrders.FirstAsync(p => p.OrderNo == result.OrderNo);
        saved.OrderNo.Should().Be(result.OrderNo);
        saved.TotalAmount.Should().Be(5000m);
    }

    [Fact]
    public async Task CreateAsync_无数量和单价_TotalAmount为Null()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = sid,
            OrderDate = DateTime.Today,
            MaterialCategory = MaterialType.RoughTube,
            PlantGrade = "20#",
            Specification = "219*8",
            Quantity = null,
            Weight = 1000m,
            RequiredDate = DateTime.Today.AddDays(30)
        });

        result.TotalAmount.Should().BeNull();
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新采购单_重新计算总金额()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, quantity: 100);
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(order.Id, new UpdatePurchaseOrderRequest
        {
            SupplierId = sid,
            MaterialCategory = MaterialType.RoughTube,
            PlantGrade = "25#",
            Specification = "273*10",
            Quantity = 200,
            Weight = 2000m,
            RequiredDate = DateTime.Today.AddDays(60),
            UnitPrice = 80m
        });

        result.PlantGrade.Should().Be("25#");
        result.TotalAmount.Should().Be(16000m); // 200 * 80
    }

    [Fact]
    public async Task UpdateAsync_Completed_允许编辑()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: PurchaseOrderStatus.Completed);
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(order.Id, new UpdatePurchaseOrderRequest
        {
            SupplierId = sid,
            MaterialCategory = MaterialType.RoughTube,
            PlantGrade = "20#",
            Specification = "219*8",
            Quantity = 100,
            Weight = 1000m,
            RequiredDate = DateTime.Today.AddDays(30)
        });

        // Completed 订单允许编辑，不会抛出异常
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateAsync_来源工单号变更_新旧工单都刷新执行读模型()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: PurchaseOrderStatus.Completed);
        order.SourceWorkOrderNo = "OLD-WO-001";
        await ctx.SaveChangesAsync();

        var woExecMock = new Mock<IWorkOrderExecutionService>();
        var svc = CreateService(ctx, woExecMock);

        var result = await svc.UpdateAsync(order.Id, new UpdatePurchaseOrderRequest
        {
            SourceWorkOrderNo = "NEW-WO-001"
        });

        result.SourceWorkOrderNo.Should().Be("NEW-WO-001");
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("OLD-WO-001"))), Times.Once);
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("NEW-WO-001"))), Times.Once);
    }

    // ========== SyncAllAsync ==========

    [Fact]
    public async Task SyncAllAsync_批次删光_到货字段回退为零状态Open()
    {
        // 场景：关联批次已被删除（表内无 SourceOrderNo 匹配），残留快照应回退为 0
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: PurchaseOrderStatus.Completed, quantity: 100);
        order.ReceivedQuantity = 1500;
        order.ReceivedWeight = 32000m;
        order.LastArrivalDate = DateTime.Today;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncAllAsync();

        var updated = await ctx.PurchaseOrders.FindAsync(order.Id);
        updated!.ReceivedQuantity.Should().Be(0);
        updated.ReceivedWeight.Should().Be(0m);
        updated.LastArrivalDate.Should().BeNull();
        updated.Status.Should().Be(PurchaseOrderStatus.Open);
    }

    // ========== UpdateStatusAsync ==========

    [Fact]
    public async Task UpdateStatusAsync_成功更新手动状态()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        await svc.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest { IsForceCompleted = true });

        var updated = await ctx.PurchaseOrders.FindAsync(order.Id);
        updated!.IsForceCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateStatusAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateStatusAsync(999, new UpdateOrderStatusRequest { IsForceCompleted = true });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("采购单不存在");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        await svc.DeleteAsync(order.Id);

        var deleted = await ctx.PurchaseOrders.FindAsync(order.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_已完成_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: PurchaseOrderStatus.Completed);
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(order.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已完成*无法删除*");
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("采购单不存在");
    }

    // ========== 圆棒穿孔采购状态 ==========

    [Fact]
    public async Task GetPiercingProcurementStatusAsync_有圆棒穿孔计划_返回状态()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var cust = await SeedCustomerAsync(ctx);
        var sr = await SeedRegisterAsync(ctx);
        var gm = await SeedGradeMappingAsync(ctx);

        // 使用 SeedConfirmedOrderAsync 模式创建工单
        var order = new SalesOrder
        {
            OrderNumber = $"PO-PIERCE-{Guid.NewGuid():N}"[..15],
            SignDate = DateTime.Today,
            Status = SalesOrderStatus.Confirmed,
            RowVersion = new byte[8]
        };
        ctx.SalesOrders.Add(order);

        var item = new OrderItem
        {
            SalesOrderId = order.Id,
            Sequence = 1,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SettlementMethod = SettlementMethod.Theoretical,
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            StandardNo = sr.StandardNo,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            StandardGrade = gm.StandardGrade,
            PlantGrade = "20#",
            Density = 7.85m,
            OuterDiameter = 219m,
            WallThickness = 8m,
            Specification = "219*8",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            Quantity = 10,
            ContractWeight = 2500m,
            TheoreticalWeight = 2500m
        };
        ctx.OrderItems.Add(item);
        await ctx.SaveChangesAsync();

        var wo = new MES.Data.Entities.WorkOrder.WorkOrder
        {
            WorkOrderNo = $"WO-PIERCE-{Guid.NewGuid():N}"[..15],
            SalesOrderNo = order.OrderNumber,
            ProductionMainNo = "D01",
            ProductionSubNo = "C01",
            OrderItemIds = $"[{item.Id}]",
            Status = WorkOrderStatus.Pending,
            RowVersion = new byte[8],
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = sr.StandardNo,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219*8",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            TotalQuantity = 10,
            TotalMeters = 60,
            TotalWeight = 2500m,
            TotalItemCount = 1
        };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 创建圆棒穿孔计划
        var piercing = new RoundBarPiercingPlan
        {
            WorkOrderId = wo.Id,
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

        // 添加工单执行状况读模型记录（验证工单关注/原锁执行/工单计划性填充）
        ctx.WorkOrderExecutionSummaries.Add(new WorkOrderExecutionSummary
        {
            WorkOrderId = wo.Id,
            WorkOrderNo = wo.WorkOrderNo,
            Salesman = "测试",
            CustomerName = "测试客户",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SettlementMethod = SettlementMethod.Theoretical.ToString(),
            SalesOrderNo = order.OrderNumber,
            ProductionMainNo = wo.ProductionMainNo,
            ProductionSubNo = wo.ProductionSubNo,
            MaterialName = "圆钢",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled.ToString(),
            PlantGrade = "20#",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed.ToString(),
            TotalItemCount = 1,
            TotalQuantity = 10,
            TotalMeters = 60,
            TotalWeight = 2500m,
            ScheduleStage = 3,
            UrgencyLevel = "B",
            RawMaterialLockRemark = "A质量补料"
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var statuses = await svc.GetPiercingProcurementStatusAsync();

        var row = statuses.Should().ContainSingle(s => s.WorkOrderNo == wo.WorkOrderNo).Subject;
        row.MaterialCategory.Should().Be(MaterialType.RoundBar);
        row.PlanWeight.Should().Be(3000m);
        row.SubcontractWeight.Should().Be(0m);
        row.MissingWeight.Should().Be(3000m);
        row.ExecutionScheduleStage.Should().Be(3);
        row.ExecutionUrgencyLevel.Should().Be("B");
        row.ExecutionRawMaterialLockRemark.Should().Be("A质量补料");
        row.PlantGrade.Should().Be("20#");
        row.StatusText.Should().Be("未穿孔");
    }

    [Fact]
    public async Task GetProcurementStatusAsync_成品计划订成非交付态_面板独立映射且采购量匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);

        // 最小工单（InMemory 无外键约束，必填字段按实体契约补全）
        var wo = new MES.Data.Entities.WorkOrder.WorkOrder
        {
            WorkOrderNo = $"WO-SDS-{Guid.NewGuid():N}"[..15],
            SalesOrderNo = $"SO-SDS-{Guid.NewGuid():N}"[..15],
            ProductionMainNo = "D01",
            ProductionSubNo = "C01",
            OrderItemIds = "[]",
            Status = WorkOrderStatus.Pending,
            RowVersion = new byte[8],
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "GB/T-8163",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219*8",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            TotalQuantity = 10,
            TotalMeters = 60,
            TotalWeight = 2500m,
            TotalItemCount = 1
        };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 订成-非交付态成品采购计划
        var plan = new PurchaseFinishedPlan
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            ProductType = FinishedProductType.SpecialDeliveryStatus,
            RequiredWeight = 3000m,
            PlantGrade = "20#",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            StandardCycle = 3
        };
        ctx.PurchaseFinishedPlans.Add(plan);

        // 订成-非交付态采购单（应与面板键匹配，不被归并到订单成品）
        var po = new PurchaseOrder
        {
            OrderNo = $"CG{DateTime.Now:yyMMdd}001",
            SupplierId = sid,
            SupplierName = "测试供应商",
            OrderDate = DateTime.Today,
            Status = PurchaseOrderStatus.Open,
            MaterialCategory = "SpecialDeliveryStatus",
            PlantGrade = "20#",
            Specification = "219*8",
            Quantity = 10,
            Weight = 1500m,
            RequiredDate = DateTime.Today.AddDays(30),
            SourceWorkOrderNo = wo.WorkOrderNo
        };
        ctx.PurchaseOrders.Add(po);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var statuses = await svc.GetProcurementStatusAsync();

        var row = statuses.Should().ContainSingle(s => s.WorkOrderNo == wo.WorkOrderNo).Subject;
        row.MaterialCategory.Should().Be(MaterialType.SpecialDeliveryStatus);
        row.PlanWeight.Should().Be(3000m);
        row.PurchaseWeight.Should().Be(1500m);
        row.MissingWeight.Should().Be(1500m);
        row.PlantGrade.Should().Be("20#");
        row.StatusText.Should().Be("部分采购");
    }

    [Fact]
    public async Task GetProcurementStatusAsync_同工单多牌号_去重拼接()
    {
        var ctx = CreateDbContext();
        var wo = await SeedMinWorkOrderAsync(ctx, "PG");

        // 同工单同分类（RoughTube）3 条计划行：牌号 20#（两条）/45#（一条），计划行级去重拼接
        await SeedPurchaseSemiPlanAsync(ctx, wo.Id, "20#", "219*8", 1000m);
        await SeedPurchaseSemiPlanAsync(ctx, wo.Id, "20#", "273*10", 2000m);
        await SeedPurchaseSemiPlanAsync(ctx, wo.Id, "45#", "250*8", 500m);

        var svc = CreateService(ctx);
        var statuses = await svc.GetProcurementStatusAsync();

        var row = statuses.Should().ContainSingle(s => s.WorkOrderNo == wo.WorkOrderNo).Subject;
        row.PlantGrade.Should().Be("20#、45#");
        row.PlanWeight.Should().Be(3500m);
        row.StatusText.Should().Be("未采购");
    }

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid);
        var order = await ctx.PurchaseOrders.FirstAsync();
        order.Remark = "采购备注测试";
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "采购备注" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Remark.Should().Be("采购备注测试");
    }


    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回OrderNo选项()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid);
        ctx.PurchaseOrders.Add(new PurchaseOrder
        {
            OrderNo = "CG20260101099",
            SupplierId = sid,
            OrderDate = DateTime.Today,
            Status = PurchaseOrderStatus.Open,
            MaterialCategory = "RoundBar",
            PlantGrade = "45#",
            Specification = "50*1000",
            Quantity = 100,
            Weight = 5000m,
            RequiredDate = DateTime.Today.AddDays(30)
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("OrderNo");
        contexts.Should().ContainKey("MaterialCategory");
        contexts.Should().ContainKey("PlantGrade");
        contexts.Should().ContainKey("Specification");
        contexts["OrderNo"].Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["OrderNo"].Should().BeEmpty();
        contexts["MaterialCategory"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_SupplierName从关联表取()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx, name: "大明钢铁");
        await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("SupplierName");
        contexts["SupplierName"].Should().Contain("大明钢铁");
    }

    // ========== 退货量汇总（ReturnOut） ==========

    [Fact]
    public async Task GetPagedAsync_退货量_仅统计退货出库()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);

        // 采购入库的原仓库批（SourceOrderNo=采购单号）
        var batch = new InventoryBatch
        {
            BatchNo = "BATCH001",
            InboundSource = "采购",
            SourceName = "测试供应商",
            SourceOrderNo = order.OrderNo,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 100,
            InitialWeight = 1000m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        };
        // 退货出库当前关联的仓库批（来源其它，无采购单号，仅用于验证不按 InventoryBatchId 关联）
        var current = new InventoryBatch
        {
            BatchNo = "CUR001",
            InboundSource = "其它",
            SourceName = "测试供应商",
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 100,
            InitialWeight = 1000m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        };
        ctx.InventoryBatches.AddRange(batch, current);
        await ctx.SaveChangesAsync();

        // 退货出库：ReturnSourceBatchNo=原仓库批批次号 BATCH001，按「退货-原仓库批」归集 + 生产领用（不应计入）
        ctx.OutboundRecords.AddRange(
            new OutboundRecord { InventoryBatchId = current.Id, BatchNo = current.BatchNo, OutboundType = OutboundType.ReturnOut, ReturnSourceBatchNo = "BATCH001", OutboundQuantity = 5, OutboundWeight = 50m, OutboundDate = DateTime.Today },
            new OutboundRecord { InventoryBatchId = current.Id, BatchNo = current.BatchNo, OutboundType = OutboundType.ReturnOut, ReturnSourceBatchNo = "BATCH001", OutboundQuantity = 3, OutboundWeight = 30m, OutboundDate = DateTime.Today },
            new OutboundRecord { InventoryBatchId = current.Id, BatchNo = current.BatchNo, OutboundType = OutboundType.ProductionPick, ReturnSourceBatchNo = "BATCH001", OutboundQuantity = 10, OutboundWeight = 100m, OutboundDate = DateTime.Today }
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams { PageIndex = 1, PageSize = 20 });

        result.Items[0].ReturnQuantity.Should().Be(8);
        result.Items[0].ReturnWeight.Should().Be(80m);
    }

    [Fact]
    public async Task GetPagedAsync_退货量_无退货出库为0()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);

        var batch = new InventoryBatch
        {
            BatchNo = "BATCH001",
            InboundSource = "采购",
            SourceName = "测试供应商",
            SourceOrderNo = order.OrderNo,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 100,
            InitialWeight = 1000m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();

        // 仅生产领用，无退货出库
        ctx.OutboundRecords.Add(new OutboundRecord { InventoryBatchId = batch.Id, BatchNo = batch.BatchNo, OutboundType = OutboundType.ProductionPick, OutboundQuantity = 10, OutboundWeight = 100m, OutboundDate = DateTime.Today });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams { PageIndex = 1, PageSize = 20 });

        result.Items[0].ReturnQuantity.Should().Be(0);
        result.Items[0].ReturnWeight.Should().Be(0m);
    }

    [Fact]
    public async Task GetByIdAsync_退货量_单条带出()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);

        var batch = new InventoryBatch
        {
            BatchNo = "BATCH001",
            InboundSource = "采购",
            SourceName = "测试供应商",
            SourceOrderNo = order.OrderNo,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 100,
            InitialWeight = 1000m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.OutboundRecords.Add(new OutboundRecord { InventoryBatchId = batch.Id, BatchNo = batch.BatchNo, OutboundType = OutboundType.ReturnOut, ReturnSourceBatchNo = "BATCH001", OutboundQuantity = 2, OutboundWeight = 20m, OutboundDate = DateTime.Today });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetByIdAsync(order.Id);

        result.ReturnQuantity.Should().Be(2);
        result.ReturnWeight.Should().Be(20m);
        result.IsForceCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetPagedAsync_属强制完成_排序生效()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        ctx.PurchaseOrders.AddRange(
            new PurchaseOrder { OrderNo = "CG20260101998", SupplierId = sid, OrderDate = DateTime.Today.AddDays(-2), Status = PurchaseOrderStatus.Open, MaterialCategory = "RoughTube", PlantGrade = "20#", Specification = "219*8", Quantity = 100, Weight = 1000m, RequiredDate = DateTime.Today.AddDays(30), IsForceCompleted = true },
            new PurchaseOrder { OrderNo = "CG20260101999", SupplierId = sid, OrderDate = DateTime.Today.AddDays(-1), Status = PurchaseOrderStatus.Open, MaterialCategory = "RoughTube", PlantGrade = "20#", Specification = "219*8", Quantity = 100, Weight = 1000m, RequiredDate = DateTime.Today.AddDays(30), IsForceCompleted = false });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        // 升序：false 在前，true 在后
        var asc = await svc.GetPagedAsync(new PurchaseOrderQueryParams { PageIndex = 1, PageSize = 20, SortBy = "isforcecompleted", IsDescending = false });
        asc.Items[0].IsForceCompleted.Should().BeFalse();
        asc.Items[1].IsForceCompleted.Should().BeTrue();

        // 降序：true 在前
        var desc = await svc.GetPagedAsync(new PurchaseOrderQueryParams { PageIndex = 1, PageSize = 20, SortBy = "isforcecompleted", IsDescending = true });
        desc.Items[0].IsForceCompleted.Should().BeTrue();
        desc.Items[1].IsForceCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetPagedAsync_属强制完成_筛选生效()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        ctx.PurchaseOrders.AddRange(
            new PurchaseOrder { OrderNo = "CG20260101998", SupplierId = sid, OrderDate = DateTime.Today, Status = PurchaseOrderStatus.Open, MaterialCategory = "RoughTube", PlantGrade = "20#", Specification = "219*8", Quantity = 100, Weight = 1000m, RequiredDate = DateTime.Today.AddDays(30), IsForceCompleted = true },
            new PurchaseOrder { OrderNo = "CG20260101999", SupplierId = sid, OrderDate = DateTime.Today, Status = PurchaseOrderStatus.Open, MaterialCategory = "RoughTube", PlantGrade = "20#", Specification = "219*8", Quantity = 100, Weight = 1000m, RequiredDate = DateTime.Today.AddDays(30), IsForceCompleted = false });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "IsForceCompleted", Operator = "in", Values = new List<string> { "True" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].IsForceCompleted.Should().BeTrue();
    }

    // ========== 工单实时关注（按来源工单号关联工单执行状况读模型） ==========

    private async Task SeedWorkOrderExecutionAsync(AppDbContext ctx, string woNo, int scheduleStage,
        string? urgencyLevel = null, string? rawMaterialLockRemark = null, DateTime? theoreticalCutoffDate = null)
    {
        ctx.WorkOrderExecutionSummaries.Add(new WorkOrderExecutionSummary
        {
            WorkOrderNo = woNo,
            Salesman = "测试业务",
            CustomerName = "测试客户",
            SalesOrderNo = "SO-001",
            ProductionMainNo = "X01",
            MaterialName = "无缝钢管",
            DeliveryState = "Normal",
            PlantGrade = "20#",
            Specification = "219*8",
            LengthStatus = "Range",
            SettlementMethod = "PerOrder",
            ScheduleStage = scheduleStage,
            UrgencyLevel = urgencyLevel,
            RawMaterialLockRemark = rawMaterialLockRemark,
            TheoreticalCutoffDate = theoreticalCutoffDate
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task GetPagedAsync_工单实时关注_按来源工单号关联读模型填充()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);
        order.SourceWorkOrderNo = "WO-EXEC-001";
        await ctx.SaveChangesAsync();
        await SeedWorkOrderExecutionAsync(ctx, "WO-EXEC-001", 3, "AUrgent", "QualityReplenish", new DateTime(2026, 8, 15));

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams { PageIndex = 1, PageSize = 20 });

        var dto = result.Items.Should().ContainSingle().Subject;
        dto.ExecutionScheduleStage.Should().Be(3);
        dto.ExecutionUrgencyLevel.Should().Be("AUrgent");
        dto.ExecutionRawMaterialLockRemark.Should().Be("QualityReplenish");
        dto.ExecutionTheoreticalCutoffDate.Should().Be(new DateTime(2026, 8, 15));
    }

    [Fact]
    public async Task GetPagedAsync_工单实时关注_无读模型记录默认空()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid); // 无 SourceWorkOrderNo，读模型必然无匹配

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams { PageIndex = 1, PageSize = 20 });

        var dto = result.Items.Should().ContainSingle().Subject;
        dto.ExecutionScheduleStage.Should().BeNull();
        dto.ExecutionUrgencyLevel.Should().BeNull();
        dto.ExecutionRawMaterialLockRemark.Should().BeNull();
        dto.ExecutionTheoreticalCutoffDate.Should().BeNull();
    }

    [Fact]
    public async Task GetPagedAsync_工单实时关注_按关注排序_按关注筛选()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var o1 = await SeedOrderAsync(ctx, sid);
        o1.SourceWorkOrderNo = "WO-1";
        var o2 = await SeedOrderAsync(ctx, sid, quantity: 200);
        o2.SourceWorkOrderNo = "WO-2";
        await ctx.SaveChangesAsync();
        await SeedWorkOrderExecutionAsync(ctx, "WO-1", 4);
        await SeedWorkOrderExecutionAsync(ctx, "WO-2", 1);

        var svc = CreateService(ctx);

        // 排序
        var asc = await svc.GetPagedAsync(new PurchaseOrderQueryParams { PageIndex = 1, PageSize = 20, SortBy = "executionschedulestage", IsDescending = false });
        asc.Items.Select(x => x.ExecutionScheduleStage).Should().Equal(1, 4);

        // 筛选
        var filtered = await svc.GetPagedAsync(new PurchaseOrderQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "ExecutionScheduleStage", Operator = "in", Values = new List<string> { "4" } }
            }
        });
        filtered.Items.Should().HaveCount(1);
        filtered.Items[0].ExecutionScheduleStage.Should().Be(4);
    }

    [Fact]
    public async Task GetFilterContextsAsync_工单实时关注_无工单号记录_筛选上下文含空值哨兵()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid);               // 无 SourceWorkOrderNo → 4 字段 null
        var o2 = await SeedOrderAsync(ctx, sid, quantity: 200); // 有读模型记录
        o2.SourceWorkOrderNo = "WO-EXEC-001";
        await ctx.SaveChangesAsync();
        await SeedWorkOrderExecutionAsync(ctx, "WO-EXEC-001", 3, "AUrgent", "QualityReplenish", new DateTime(2026, 8, 15));

        var svc = CreateService(ctx);
        var contexts = await svc.GetFilterContextsAsync();

        // 无关联记录以空值哨兵输出，且空值排最前
        contexts["ExecutionUrgencyLevel"].Should().Contain("__EXCEL_FILTER_NULL__").And.Contain("AUrgent");
        contexts["ExecutionUrgencyLevel"][0].Should().Be("__EXCEL_FILTER_NULL__");
        contexts["ExecutionRawMaterialLockRemark"].Should().Contain("__EXCEL_FILTER_NULL__").And.Contain("QualityReplenish");
        contexts["ExecutionTheoreticalCutoffDate"].Should().Contain("__EXCEL_FILTER_NULL__").And.Contain("2026-08-15");
    }

    [Fact]
    public async Task GetPagedAsync_工单实时关注_筛选空值_筛出无关联记录()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid);               // 无工单号 → 关注 null
        var o2 = await SeedOrderAsync(ctx, sid, quantity: 200); // 有读模型 ScheduleStage=4
        o2.SourceWorkOrderNo = "WO-EXEC-001";
        await ctx.SaveChangesAsync();
        await SeedWorkOrderExecutionAsync(ctx, "WO-EXEC-001", 4);

        var svc = CreateService(ctx);

        // 仅勾选空值 → isnull 操作符
        var nullOnly = await svc.GetPagedAsync(new PurchaseOrderQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "ExecutionScheduleStage", Operator = "isnull", IncludeNull = true }
            }
        });
        nullOnly.Items.Should().HaveCount(1);
        nullOnly.Items[0].ExecutionScheduleStage.Should().BeNull();

        // 空值 + 具体值 → in + IncludeNull
        var withValue = await svc.GetPagedAsync(new PurchaseOrderQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "ExecutionScheduleStage", Operator = "in", Values = new List<string> { "4" }, IncludeNull = true }
            }
        });
        withValue.Items.Should().HaveCount(2);
    }

    // ========== 采购首页汇总（GetPurchasePending / GetPurchaseInProgress / GetPurchaseMonthly） ==========

    private async Task<MES.Data.Entities.WorkOrder.WorkOrder> SeedMinWorkOrderAsync(AppDbContext ctx, string suffix = "")
    {
        var wo = new MES.Data.Entities.WorkOrder.WorkOrder
        {
            WorkOrderNo = $"WO-SUM-{suffix}{Guid.NewGuid():N}"[..20],
            SalesOrderNo = $"SO-SUM-{Guid.NewGuid():N}"[..15],
            ProductionMainNo = "D01",
            ProductionSubNo = "C01",
            OrderItemIds = "[]",
            Status = WorkOrderStatus.Pending,
            RowVersion = new byte[8],
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "GB/T-8163",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219*8",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            TotalQuantity = 10,
            TotalMeters = 60,
            TotalWeight = 2500m,
            TotalItemCount = 1
        };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();
        return wo;
    }

    private async Task SeedPurchaseSemiPlanAsync(AppDbContext ctx, int workOrderId, string grade, string spec, decimal requiredWeight)
    {
        ctx.PurchaseSemiPlans.Add(new PurchaseSemiPlan
        {
            WorkOrderId = workOrderId,
            PlanDate = DateTime.Today,
            PlantGrade = grade,
            RawMaterialType = MaterialType.RoughTube,
            RawMaterialSpec = spec,
            RequiredWeight = requiredWeight,
            StandardCycle = 3
        });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedPurchaseFinishedPlanAsync(AppDbContext ctx, int workOrderId, FinishedProductType productType, string grade, string spec, decimal requiredWeight)
    {
        ctx.PurchaseFinishedPlans.Add(new PurchaseFinishedPlan
        {
            WorkOrderId = workOrderId,
            PlanDate = DateTime.Today,
            ProductType = productType,
            RequiredWeight = requiredWeight,
            PlantGrade = grade,
            Specification = spec,
            LengthStatus = LengthStatus.Fixed,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            StandardCycle = 3
        });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedSummaryOrderAsync(AppDbContext ctx, int supplierId, string orderNo, decimal weight,
        decimal receivedWeight, PurchaseOrderStatus status, string materialCategory, string supplierName,
        string plantGrade, string? sourceWoNo = null, DateTime? orderDate = null)
    {
        ctx.PurchaseOrders.Add(new PurchaseOrder
        {
            OrderNo = orderNo,
            SupplierId = supplierId,
            SupplierName = supplierName,
            OrderDate = orderDate ?? DateTime.Today,
            Status = status,
            MaterialCategory = materialCategory,
            PlantGrade = plantGrade,
            Specification = "219*8",
            Quantity = 100,
            Weight = weight,
            ReceivedWeight = receivedWeight,
            RequiredDate = DateTime.Today.AddDays(30),
            SourceWorkOrderNo = sourceWoNo
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task GetPurchasePendingAsync_荒管_按工单物料分类聚合_合并钢种规格_关联执行字段()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var wo = await SeedMinWorkOrderAsync(ctx, "RT");

        // 同类别 RoughTube 两个计划（不同钢种/规格）
        await SeedPurchaseSemiPlanAsync(ctx, wo.Id, "20#", "219*8", 3000m);
        await SeedPurchaseSemiPlanAsync(ctx, wo.Id, "20#", "273*10", 2000m);
        // 已采购 1500
        await SeedSummaryOrderAsync(ctx, sid, $"CG{DateTime.Now:yyMMdd}RT01", 1500m, 0m, PurchaseOrderStatus.Open, "RoughTube", "测试供应商", "20#", wo.WorkOrderNo);
        await SeedWorkOrderExecutionAsync(ctx, wo.WorkOrderNo, 3, "APlusUrgent", "QualityReplenish");

        var svc = CreateService(ctx);
        var result = await svc.GetPurchasePendingAsync(false);

        var row = result.Should().ContainSingle().Subject;
        row.WorkOrderNo.Should().Be(wo.WorkOrderNo);
        row.MaterialCategory.Should().Be(MaterialType.RoughTube);
        row.PlantGrade.Should().Be("20#");
        row.Specification.Should().Be("219*8,273*10");
        row.PendingWeight.Should().Be(3500m); // 5000 - 1500
        row.ExecutionScheduleStage.Should().Be(3);
        row.ExecutionUrgencyLevel.Should().Be("APlusUrgent");
        row.ExecutionRawMaterialLockRemark.Should().Be("QualityReplenish");
    }

    [Fact]
    public async Task GetPurchasePendingAsync_待购量已足_不返回行()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var wo = await SeedMinWorkOrderAsync(ctx, "RT2");
        await SeedPurchaseSemiPlanAsync(ctx, wo.Id, "20#", "219*8", 1000m);
        // 已采购 1000 ≥ 计划 → 待购 0
        await SeedSummaryOrderAsync(ctx, sid, $"CG{DateTime.Now:yyMMdd}RT2", 1000m, 1000m, PurchaseOrderStatus.Completed, "RoughTube", "测试供应商", "20#", wo.WorkOrderNo);

        var svc = CreateService(ctx);
        var result = await svc.GetPurchasePendingAsync(false);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPurchasePendingAsync_成品_ProductType映射分类_与荒管隔离()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var woF = await SeedMinWorkOrderAsync(ctx, "FN");
        await SeedPurchaseFinishedPlanAsync(ctx, woF.Id, FinishedProductType.Critical, "304", "219*8", 3000m);
        await SeedSummaryOrderAsync(ctx, sid, $"CG{DateTime.Now:yyMMdd}FN", 1200m, 0m, PurchaseOrderStatus.Open, "CriticalFinished", "测试供应商", "304", woF.WorkOrderNo);

        var woS = await SeedMinWorkOrderAsync(ctx, "SM");
        await SeedPurchaseSemiPlanAsync(ctx, woS.Id, "20#", "219*8", 2000m);

        var svc = CreateService(ctx);

        // 成品路径：只含成品行，Critical→CriticalFinished
        var finished = await svc.GetPurchasePendingAsync(true);
        var frow = finished.Should().ContainSingle(s => s.WorkOrderNo == woF.WorkOrderNo).Subject;
        frow.MaterialCategory.Should().Be(MaterialType.CriticalFinished);
        frow.PendingWeight.Should().Be(1800m); // 3000 - 1200
        finished.Should().NotContain(s => s.WorkOrderNo == woS.WorkOrderNo);

        // 荒管路径：只含荒管行
        var semi = await svc.GetPurchasePendingAsync(false);
        var srow = semi.Should().ContainSingle(s => s.WorkOrderNo == woS.WorkOrderNo).Subject;
        srow.MaterialCategory.Should().Be(MaterialType.RoughTube);
        srow.PendingWeight.Should().Be(2000m);
        semi.Should().NotContain(s => s.WorkOrderNo == woF.WorkOrderNo);
    }

    [Fact]
    public async Task GetPurchaseInProgressAsync_供应商钢种二维聚合_急量_合计行()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);

        // 供应商A 钢种20#：Open 3000 已到1000 → 在购2000，急单（APlusUrgent）→ 急2000
        await SeedSummaryOrderAsync(ctx, sid, "CGIP01", 3000m, 1000m, PurchaseOrderStatus.Open, "RoughTube", "A供应商", "20#", "WO-IP-A");
        // 供应商A 钢种20#：Open 500 已到1000 → 在购<=0 跳过（超收）
        await SeedSummaryOrderAsync(ctx, sid, "CGIP02", 500m, 1000m, PurchaseOrderStatus.Open, "RoughTube", "A供应商", "20#");
        // 供应商B 钢种304：Partial 2000 已到500 → 在购1500，无工单 → 不急
        await SeedSummaryOrderAsync(ctx, sid, "CGIP03", 2000m, 500m, PurchaseOrderStatus.Partial, "RoughTube", "B供应商", "304");
        await SeedWorkOrderExecutionAsync(ctx, "WO-IP-A", 3, "APlusUrgent", null);

        var svc = CreateService(ctx);
        var result = await svc.GetPurchaseInProgressAsync(false);

        result.SteelGrades.Should().ContainInOrder("20#", "304");
        var rowA = result.Rows.Should().ContainSingle(r => r.SupplierName == "A供应商").Subject;
        rowA.Cells["20#"].TotalWeight.Should().Be(2000m);
        rowA.Cells["20#"].UrgentWeight.Should().Be(2000m);
        rowA.Cells["304"].TotalWeight.Should().Be(0m);
        rowA.Total.TotalWeight.Should().Be(2000m);
        rowA.Total.UrgentWeight.Should().Be(2000m);

        var rowB = result.Rows.Should().ContainSingle(r => r.SupplierName == "B供应商").Subject;
        rowB.Cells["304"].TotalWeight.Should().Be(1500m);
        rowB.Cells["304"].UrgentWeight.Should().Be(0m);
        rowB.Total.TotalWeight.Should().Be(1500m);

        var totalRow = result.Rows.Should().ContainSingle(r => r.SupplierName == "合计").Subject;
        totalRow.Total.TotalWeight.Should().Be(3500m);
        totalRow.Total.UrgentWeight.Should().Be(2000m);
        totalRow.Cells["20#"].TotalWeight.Should().Be(2000m);
        totalRow.Cells["304"].TotalWeight.Should().Be(1500m);
    }

    [Fact]
    public async Task GetPurchaseInProgressAsync_退货量加回_已完成状态排除()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);

        // Open 1000 已到600，退货50 → 在购 1000+50-600=450
        await SeedSummaryOrderAsync(ctx, sid, "CGIP11", 1000m, 600m, PurchaseOrderStatus.Open, "RoughTube", "A供应商", "20#");
        var order = await ctx.PurchaseOrders.FirstAsync(o => o.OrderNo == "CGIP11");
        var batch = new InventoryBatch
        {
            BatchNo = "BATCH-IP11",
            InboundSource = "采购",
            SourceName = "测试供应商",
            SourceOrderNo = order.OrderNo,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 100,
            InitialWeight = 1000m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();
        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            OutboundType = OutboundType.ReturnOut,
            ReturnSourceBatchNo = "BATCH-IP11",
            OutboundQuantity = 5,
            OutboundWeight = 50m,
            OutboundDate = DateTime.Today
        });

        // 已完成采购单不计入在购
        await SeedSummaryOrderAsync(ctx, sid, "CGIP12", 2000m, 2000m, PurchaseOrderStatus.Completed, "RoughTube", "A供应商", "20#");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPurchaseInProgressAsync(false);

        var rowA = result.Rows.Should().ContainSingle(r => r.SupplierName == "A供应商").Subject;
        rowA.Total.TotalWeight.Should().Be(450m);
        result.Rows.Should().ContainSingle(r => r.SupplierName == "合计").Subject.Total.TotalWeight.Should().Be(450m);
    }

    [Fact]
    public async Task GetPurchaseMonthlyAsync_按月分桶_购回合计_现在购_合计行()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var year = DateTime.Today.Year;

        // 供应商A 1月 Open 1000 已到400 → 购1000 回400 现在购600
        await SeedSummaryOrderAsync(ctx, sid, "CGMP01", 1000m, 400m, PurchaseOrderStatus.Open, "RoughTube", "A供应商", "20#", null, new DateTime(year, 1, 15));
        // 供应商A 6月 Completed 2000 已到2500 → 购2000 回2500（已完成不计现在购）
        await SeedSummaryOrderAsync(ctx, sid, "CGMP02", 2000m, 2500m, PurchaseOrderStatus.Completed, "RoughTube", "A供应商", "20#", null, new DateTime(year, 6, 15));
        // 供应商B 12月 Open 800 已到300 → 购800 回300 现在购500
        await SeedSummaryOrderAsync(ctx, sid, "CGMP03", 800m, 300m, PurchaseOrderStatus.Open, "RoughTube", "B供应商", "304", null, new DateTime(year, 12, 15));

        var svc = CreateService(ctx);
        var result = await svc.GetPurchaseMonthlyAsync(false);

        result.MonthLabels[0].Should().Be($"{year}-01");
        result.MonthLabels[11].Should().Be($"{year}-12");

        var rowA = result.Rows.Should().ContainSingle(r => r.SupplierName == "A供应商").Subject;
        rowA.Months[0].BuyWeight.Should().Be(1000m);
        rowA.Months[0].ReturnWeight.Should().Be(400m);
        rowA.Months[5].BuyWeight.Should().Be(2000m);
        rowA.Months[5].ReturnWeight.Should().Be(2500m);
        rowA.Total.BuyWeight.Should().Be(3000m);
        rowA.Total.ReturnWeight.Should().Be(2900m);
        rowA.NowInProgress.Should().Be(600m);

        var rowB = result.Rows.Should().ContainSingle(r => r.SupplierName == "B供应商").Subject;
        rowB.Months[11].BuyWeight.Should().Be(800m);
        rowB.Months[11].ReturnWeight.Should().Be(300m);
        rowB.NowInProgress.Should().Be(500m);

        var totalRow = result.Rows.Should().ContainSingle(r => r.SupplierName == "合计").Subject;
        totalRow.Total.BuyWeight.Should().Be(3800m);
        totalRow.Total.ReturnWeight.Should().Be(3200m);
        totalRow.NowInProgress.Should().Be(1100m);
        totalRow.Months[0].BuyWeight.Should().Be(1000m);
        totalRow.Months[11].BuyWeight.Should().Be(800m);
    }

    [Fact]
    public async Task GetPurchaseMonthlyAsync_无本年订单_返回空行仅合计()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedSummaryOrderAsync(ctx, sid, "CGMP99", 1000m, 0m, PurchaseOrderStatus.Open, "RoughTube", "A供应商", "20#", null, new DateTime(2020, 1, 15));

        var svc = CreateService(ctx);
        var result = await svc.GetPurchaseMonthlyAsync(false);

        result.Rows.Should().ContainSingle(r => r.SupplierName == "合计");
    }
}
