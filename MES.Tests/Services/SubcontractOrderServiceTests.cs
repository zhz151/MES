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
/// 委外加工单服务测试：CRUD、子表操作、状态流转、同步、关键字筛选
/// </summary>
public class SubcontractOrderServiceTests : TestBase
{
    private SubcontractOrderService CreateService(AppDbContext ctx) => new(ctx);

    private async Task<int> SeedSupplierAsync(AppDbContext ctx, string name = "委外供应商")
    {
        var entity = new SupplierProfile { SupplierName = name, IsActive = true };
        ctx.SupplierProfiles.Add(entity);
        await ctx.SaveChangesAsync();
        return entity.Id;
    }

    private async Task<SubcontractOrder> SeedOrderAsync(AppDbContext ctx, int supplierId, string status = "Sent",
        DateTime? orderDate = null, int outQty = 100, decimal outWt = 1000m)
    {
        var order = new SubcontractOrder
        {
            OrderNo = $"WW{DateTime.Now:yyMMdd}001",
            SupplierId = supplierId,
            OrderDate = orderDate ?? DateTime.Today,
            Status = status,
            OutMaterialCategory = "钢管",
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = outQty,
            OutWeight = outWt,
            ReturnDeadline = DateTime.Today.AddDays(60)
        };
        order.ReturnItems.Add(new SubcontractReturnItem
        {
            Sequence = 1,
            ProcessType = "车丝",
            MaterialCategory = "钢管",
            ProcessSpecification = "219*8",
            ProcessUnitPrice = 10m,
            ProcessTotalAmount = 1000m
        });
        ctx.SubcontractOrders.Add(order);
        await ctx.SaveChangesAsync();
        return order;
    }

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new SubcontractQueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_按关键字搜索_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx, name: "大明委外");
        await SeedOrderAsync(ctx, sid);
        var sid2 = await SeedSupplierAsync(ctx, name: "宝钢委外");
        ctx.SubcontractOrders.Add(new SubcontractOrder
        {
            OrderNo = "WW20260101002",
            SupplierId = sid2,
            OrderDate = DateTime.Today,
            Status = "Sent",
            OutMaterialCategory = "不锈钢管",
            OutPlantGrade = "304",
            OutSpecification = "273*10",
            OutQuantity = 50,
            OutWeight = 500m
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new SubcontractQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "大明" });

        result.Items.Should().HaveCount(1);
        result.Items[0].SupplierName.Should().Be("大明委外");
    }

    [Fact]
    public async Task GetPagedAsync_按状态筛选_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid, status: "Sent");
        await SeedOrderAsync(ctx, sid, status: "Completed");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new SubcontractQueryParams
        { PageIndex = 1, PageSize = 20, Status = "Completed" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be("Completed");
    }

    [Fact]
    public async Task GetPagedAsync_按日期筛选_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid, orderDate: DateTime.Today.AddDays(-5));
        await SeedOrderAsync(ctx, sid, orderDate: DateTime.Today);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new SubcontractQueryParams
        { PageIndex = 1, PageSize = 20, DateFrom = DateTime.Today.AddDays(-1) });

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_填充供应商名称()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx, name: "委外供应商");
        await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new SubcontractQueryParams { PageIndex = 1, PageSize = 20 });

        result.Items[0].SupplierName.Should().Be("委外供应商");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_包含子表返回Dto()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx, name: "委外供应商");
        var order = await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(order.Id);

        result.Should().NotBeNull();
        result.OrderNo.Should().Be(order.OrderNo);
        result.SupplierName.Should().Be("委外供应商");
        result.ReturnItems.Should().HaveCount(1);
        result.ReturnItems[0].ProcessType.Should().Be("车丝");
        result.ReturnItems[0].ProcessTotalAmount.Should().Be(1000m);
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("委外单不存在");
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建委外单_自动生成单号()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateSubcontractOrderRequest
        {
            SupplierId = sid,
            OrderDate = DateTime.Today,
            OutMaterialCategory = "钢管",
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 100,
            OutWeight = 1000m,
            ReturnDeadline = DateTime.Today.AddDays(60),
            SourceWorkOrderNo = "GD20260101001",
            ReturnItems = new List<MES.Core.DTOs.CreateReturnItemRequest>
            {
                new()
                {
                    ProcessType = "车丝",
                    MaterialCategory = "钢管",
                    ProcessSpecification = "219*8",
                    ProcessUnitPrice = 10m,
                    ProcessTotalAmount = 1000m,
                    SourceWorkOrderNo = "GD20260101001"
                }
            }
        });

        result.Should().NotBeNull();
        result.OrderNo.Should().StartWith("WW" + DateTime.Now.ToString("yyMMdd"));
        result.ReturnItems.Should().HaveCount(1);
        result.ReturnItems[0].ProcessType.Should().Be("车丝");
        result.ReturnItems[0].ProcessTotalAmount.Should().Be(1000m);
        result.SourceWorkOrderNo.Should().Be("GD20260101001");

        var saved = await ctx.SubcontractOrders.Include(s => s.ReturnItems).FirstAsync(s => !s.IsDeleted);
        saved.ReturnItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_无明细_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateSubcontractOrderRequest
        {
            SupplierId = sid,
            OrderDate = DateTime.Today,
            OutMaterialCategory = "钢管",
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 100,
            OutWeight = 1000m,
            ReturnItems = new List<MES.Core.DTOs.CreateReturnItemRequest>()
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*至少需要一条*");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新_全量子表替换()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(order.Id, new UpdateSubcontractOrderRequest
        {
            SupplierId = sid,
            OutMaterialCategory = "不锈钢管",
            OutPlantGrade = "304",
            OutSpecification = "273*10",
            OutQuantity = 200,
            OutWeight = 2000m,
            ReturnDeadline = DateTime.Today.AddDays(90),
            ReturnItems = new List<MES.Core.DTOs.CreateReturnItemRequest>
            {
                new()
                {
                    ProcessType = "抛光",
                    MaterialCategory = "不锈钢管",
                    ProcessSpecification = "273*10",
                    ProcessUnitPrice = 20m,
                    ProcessTotalAmount = 4000m
                },
                new()
                {
                    ProcessType = "车丝",
                    MaterialCategory = "不锈钢管",
                    ProcessSpecification = "273*10",
                    ProcessUnitPrice = 15m,
                    ProcessTotalAmount = 3000m
                }
            }
        });

        result.OutQuantity.Should().Be(200);
        result.ReturnItems.Should().HaveCount(2);

        var saved = await ctx.SubcontractOrders.Include(s => s.ReturnItems).FirstAsync(s => s.Id == order.Id);
        saved.ReturnItems.Where(r => !r.IsDeleted).Should().HaveCount(2);
        saved.OutMaterialCategory.Should().Be("不锈钢管");
    }

    [Fact]
    public async Task UpdateAsync_已取消_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: "Cancelled");
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(order.Id, new UpdateSubcontractOrderRequest
        {
            SupplierId = sid,
            OutMaterialCategory = "钢管",
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 100,
            OutWeight = 1000m,
            ReturnItems = new List<MES.Core.DTOs.CreateReturnItemRequest>()
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已取消*无法编辑*");
    }

    [Fact]
    public async Task UpdateAsync_已完成_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: "Completed");
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(order.Id, new UpdateSubcontractOrderRequest
        {
            SupplierId = sid,
            OutMaterialCategory = "钢管",
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 100,
            OutWeight = 1000m,
            ReturnItems = new List<MES.Core.DTOs.CreateReturnItemRequest>()
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已完成*无法编辑*");
    }

    // ========== SyncAllAsync / SyncSingleAsync ==========

    [Fact]
    public async Task SyncSingleAsync_有批次_更新收回数量和状态()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, outQty: 100, outWt: 1000m);

        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "BATCH001",
            InboundSource = "委外",
            SourceName = "委外供应商",
            SourceOrderNo = order.OrderNo,
            MaterialType = "钢管",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 50,
            InitialWeight = 500m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSingleAsync(order.Id);

        var updated = await ctx.SubcontractOrders.FindAsync(order.Id);
        updated!.InQuantity.Should().Be(50);
        updated.InWeight.Should().Be(500m);
        updated.Status.Should().Be("PartialReturned"); // 50/1000 < 95%
    }

    [Fact]
    public async Task SyncSingleAsync_收回重量达到95_状态变为Completed()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, outQty: 100, outWt: 1000m);

        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "BATCH001",
            InboundSource = "委外",
            SourceName = "委外供应商",
            SourceOrderNo = order.OrderNo,
            MaterialType = "钢管",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 100,
            InitialWeight = 950m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSingleAsync(order.Id);

        var updated = await ctx.SubcontractOrders.FindAsync(order.Id);
        updated!.InWeight.Should().Be(950m);
        updated.Status.Should().Be("Completed"); // 950/1000 >= 95%
    }

    [Fact]
    public async Task SyncSingleAsync_无批次_状态保持Sent()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        await svc.SyncSingleAsync(order.Id);

        var updated = await ctx.SubcontractOrders.FindAsync(order.Id);
        updated!.InQuantity.Should().Be(0);
        updated.Status.Should().Be("Sent");
    }

    [Fact]
    public async Task SyncSingleAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.SyncSingleAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("委外单不存在");
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

        var updated = await ctx.SubcontractOrders.FindAsync(order.Id);
        updated!.ManualStatus.Should().Be("Completed");
    }

    [Fact]
    public async Task UpdateStatusAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateStatusAsync(999, new UpdateOrderStatusRequest { ManualStatus = "Completed" });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("委外单不存在");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功软删除()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        await svc.DeleteAsync(order.Id);

        var deleted = await ctx.SubcontractOrders.FindAsync(order.Id);
        deleted!.IsDeleted.Should().BeTrue();
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
        await act.Should().ThrowAsync<BusinessException>().WithMessage("委外单不存在");
    }
}
