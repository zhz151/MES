using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data.Entities;
using MES.Data;
using MES.Services;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 采购订单服务测试：CRUD、状态流转、同步、筛选、金额自动计算
/// </summary>
public class PurchaseOrderServiceTests : TestBase
{
    private PurchaseOrderService CreateService(AppDbContext ctx) => new(ctx);

    private async Task<int> SeedSupplierAsync(AppDbContext ctx, string name = "测试供应商")
    {
        var entity = new SupplierProfile { SupplierCode = $"S{Guid.NewGuid():N}"[..10], SupplierName = name, IsActive = true };
        ctx.SupplierProfiles.Add(entity);
        await ctx.SaveChangesAsync();
        return entity.Id;
    }

    private async Task<PurchaseOrder> SeedOrderAsync(AppDbContext ctx, int supplierId, string status = "Open",
        DateTime? orderDate = null, DateTime? requiredDate = null, int? quantity = 100)
    {
        var order = new PurchaseOrder
        {
            OrderNo = $"CG{DateTime.Now:yyMMdd}001",
            SupplierId = supplierId,
            OrderDate = orderDate ?? DateTime.Today,
            Status = status,
            MaterialCategory = "钢管",
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
            Status = "Open",
            MaterialCategory = "钢管",
            PlantGrade = "304",
            Specification = "273*10",
            Quantity = 50,
            Weight = 500m,
            RequiredDate = DateTime.Today.AddDays(30)
        });
        await ctx.SaveChangesAsync();

        var seedOrderNo = await ctx.PurchaseOrders
            .Where(p => p.MaterialCategory == "钢管" && p.PlantGrade == "20#")
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
            Status = "Open",
            MaterialCategory = "钢管",
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
        await SeedOrderAsync(ctx, sid, status: "Open");
        await SeedOrderAsync(ctx, sid, status: "Completed", quantity: 200);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new PurchaseOrderQueryParams
        { PageIndex = 1, PageSize = 20, Status = "Completed" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be("Completed");
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
            MaterialCategory = "钢管",
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
            MaterialCategory = "钢管",
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
            MaterialCategory = "钢管",
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
    public async Task UpdateAsync_已取消_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: "Cancelled");
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(order.Id, new UpdatePurchaseOrderRequest
        {
            SupplierId = sid,
            MaterialCategory = "钢管",
            PlantGrade = "20#",
            Specification = "219*8",
            Quantity = 100,
            Weight = 1000m,
            RequiredDate = DateTime.Today.AddDays(30)
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已取消*无法编辑*");
    }

    // ========== SyncAllAsync / SyncSingleAsync ==========

    [Fact]
    public async Task SyncSingleAsync_更新到货数量_状态变为部分到货()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, quantity: 100);

        // 创建关联的库存批次
        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "BATCH001",
            InboundSource = "采购",
            SourceName = "测试供应商",
            SourceOrderNo = order.OrderNo,
            MaterialType = "钢管",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 30,
            InitialWeight = 300m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSingleAsync(order.Id);

        var updated = await ctx.PurchaseOrders.FindAsync(order.Id);
        updated!.ReceivedQuantity.Should().Be(30);
        updated.ReceivedWeight.Should().Be(300m);
        updated.Status.Should().Be("Partial");
    }

    [Fact]
    public async Task SyncSingleAsync_全部到货_状态变为Completed()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, quantity: 100);

        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "BATCH001",
            InboundSource = "采购",
            SourceName = "测试供应商",
            SourceOrderNo = order.OrderNo,
            MaterialType = "钢管",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 100,
            InitialWeight = 1000m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSingleAsync(order.Id);

        var updated = await ctx.PurchaseOrders.FindAsync(order.Id);
        updated!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task SyncSingleAsync_无批次_状态保持Open()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, quantity: 100);
        var svc = CreateService(ctx);

        await svc.SyncSingleAsync(order.Id);

        var updated = await ctx.PurchaseOrders.FindAsync(order.Id);
        updated!.ReceivedQuantity.Should().Be(0);
        updated.Status.Should().Be("Open");
    }

    [Fact]
    public async Task SyncSingleAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.SyncSingleAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("采购单不存在");
    }

    // ========== UpdateStatusAsync ==========

    [Fact]
    public async Task UpdateStatusAsync_成功更新手动状态()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        await svc.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest { ManualStatus = "Completed" });

        var updated = await ctx.PurchaseOrders.FindAsync(order.Id);
        updated!.ManualStatus.Should().Be("Completed");
    }

    [Fact]
    public async Task UpdateStatusAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateStatusAsync(999, new UpdateOrderStatusRequest { ManualStatus = "Completed" });
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
    public async Task DeleteAsync_已取消_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: "Cancelled");
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(order.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已取消*");
    }

    [Fact]
    public async Task DeleteAsync_已完成_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: "Completed");
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
}
