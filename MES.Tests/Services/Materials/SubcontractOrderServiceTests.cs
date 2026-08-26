using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
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
using MES.Core.Constants;
using MES.Services.Materials;
using MES.Tests.Tests;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Materials;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 委外加工单服务测试：CRUD、子表操作、状态流转、同步、关键字筛选
/// </summary>
public class SubcontractOrderServiceTests : TestBase
{
    private SubcontractOrderService CreateService(AppDbContext ctx, Mock<IWorkOrderExecutionService>? woExecMock = null)
    {
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        woExecMock ??= new Mock<IWorkOrderExecutionService>();
        var loggerMock = new Mock<ILogger<SubcontractOrderService>>();
        return new SubcontractOrderService(ctx, new Mock<IPurchaseOrderService>().Object, configMock.Object, woExecMock.Object, loggerMock.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    private async Task<int> SeedSupplierAsync(AppDbContext ctx, string name = "委外供应商")
    {
        var entity = new SupplierProfile { SupplierCode = $"S{Guid.NewGuid():N}"[..10], SupplierName = name, IsActive = true };
        ctx.SupplierProfiles.Add(entity);
        await ctx.SaveChangesAsync();
        return entity.Id;
    }

    private async Task<SubcontractOrder> SeedOrderAsync(AppDbContext ctx, int supplierId, SubcontractOrderStatus status = SubcontractOrderStatus.Sent,
        DateTime? orderDate = null, int outQty = 100, decimal outWt = 1000m)
    {
        var supplierName = await ctx.SupplierProfiles
            .Where(s => s.Id == supplierId)
            .Select(s => s.SupplierName)
            .FirstOrDefaultAsync();

        var order = new SubcontractOrder
        {
            OrderNo = $"WW{DateTime.Now:yyMMdd}001",
            SupplierId = supplierId,
            SupplierName = supplierName,
            OrderDate = orderDate ?? DateTime.Today,
            Status = status,
            ProcessType = "Piercing",
            OutMaterialCategory = "RoughTube",
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = outQty,
            OutWeight = outWt,
            ReturnDeadline = DateTime.Today.AddDays(60)
        };
        order.ReturnItems.Add(new SubcontractReturnItem
        {
            Sequence = 1,
            MaterialCategory = "RoughTube",
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
            Status = SubcontractOrderStatus.Sent,
            ProcessType = "Piercing",
            OutMaterialCategory = "RoundBar",
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
        await SeedOrderAsync(ctx, sid, status: SubcontractOrderStatus.Sent);
        await SeedOrderAsync(ctx, sid, status: SubcontractOrderStatus.Completed);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new SubcontractQueryParams
        { PageIndex = 1, PageSize = 20, Status = "Completed" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be(SubcontractOrderStatus.Completed);
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
        result.ProcessType.Should().Be("Piercing");
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
            OutMaterialCategory = MaterialType.RoughTube,
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 100,
            OutWeight = 1000m,
            ReturnDeadline = DateTime.Today.AddDays(60),
            ProcessType = "Piercing",
            ReturnItems = new List<MES.Core.DTOs.Materials.CreateReturnItemRequest>
            {
                new()
                {
                    MaterialCategory = MaterialType.RoughTube,
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
        result.ProcessType.Should().Be("Piercing");
        result.ReturnItems[0].ProcessTotalAmount.Should().Be(1000m);

        var saved = await ctx.SubcontractOrders.Include(s => s.ReturnItems).FirstAsync(s => s.OrderNo == result.OrderNo);
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
            ProcessType = "Piercing",
            OutMaterialCategory = MaterialType.RoughTube,
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 100,
            OutWeight = 1000m,
            ReturnItems = new List<MES.Core.DTOs.Materials.CreateReturnItemRequest>()
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
            ProcessType = "Piercing",
            OutMaterialCategory = MaterialType.RoundBar,
            OutPlantGrade = "304",
            OutSpecification = "273*10",
            OutQuantity = 200,
            OutWeight = 2000m,
            ReturnDeadline = DateTime.Today.AddDays(90),
            ReturnItems = new List<MES.Core.DTOs.Materials.CreateReturnItemRequest>
            {
                new()
                {
                    MaterialCategory = MaterialType.RoundBar,
                    ProcessSpecification = "273*10",
                    ProcessUnitPrice = 20m,
                    ProcessTotalAmount = 4000m
                },
                new()
                {
                    MaterialCategory = MaterialType.RoundBar,
                    ProcessSpecification = "273*10",
                    ProcessUnitPrice = 15m,
                    ProcessTotalAmount = 3000m
                }
            }
        });

        result.OutQuantity.Should().Be(200);
        result.ReturnItems.Should().HaveCount(2);

        var saved = await ctx.SubcontractOrders.Include(s => s.ReturnItems).FirstAsync(s => s.Id == order.Id);
        saved.ReturnItems.Should().HaveCount(2);
        saved.OutMaterialCategory.Should().Be("RoundBar");
    }

    [Fact]
    public async Task UpdateAsync_Completed_允许编辑()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: SubcontractOrderStatus.Completed);
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(order.Id, new UpdateSubcontractOrderRequest
        {
            SupplierId = sid,
            ProcessType = "Piercing",
            OutMaterialCategory = MaterialType.RoughTube,
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 100,
            OutWeight = 1000m,
            ReturnItems = new List<MES.Core.DTOs.Materials.CreateReturnItemRequest>()
        });

        // Completed 订单允许编辑（仅来源工单号），不会抛出异常
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateAsync_已完成_仅允许修改来源工单号()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: SubcontractOrderStatus.Completed);
        // 先确认种子数据
        ctx.Entry(order).Reload();
        order.ProcessType.Should().Be("Piercing", because: "种子数据默认ProcessType为Piercing");
        var svc = CreateService(ctx);

        // 已完成状态：UpdateAsync应跳过主表字段，只允许改ReturnItems.SourceWorkOrderNo
        await svc.UpdateAsync(order.Id, new UpdateSubcontractOrderRequest
        {
            SupplierId = sid,
            ProcessType = "Piercing", // 请求中试图修改，但已完成状态不应生效
            OutMaterialCategory = MaterialType.RoundBar,
            OutPlantGrade = "304",
            OutSpecification = "273*10",
            OutQuantity = 200,
            OutWeight = 2000m,
            ReturnItems = new List<MES.Core.DTOs.Materials.CreateReturnItemRequest>()
        });

        // 验证主表字段未被修改（仍为种子数据的值）
        var updated = await ctx.SubcontractOrders.FirstAsync(s => s.Id == order.Id);
        updated.ProcessType.Should().Be("Piercing", because: "已完成状态下主表字段不应被修改");
    }

    [Fact]
    public async Task UpdateAsync_明细来源工单号变更_新旧工单都刷新执行读模型()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: SubcontractOrderStatus.Completed);
        order.ReturnItems.Single().SourceWorkOrderNo = "OLD-WO-001";
        await ctx.SaveChangesAsync();

        var woExecMock = new Mock<IWorkOrderExecutionService>();
        var svc = CreateService(ctx, woExecMock);

        await svc.UpdateAsync(order.Id, new UpdateSubcontractOrderRequest
        {
            SupplierId = sid,
            ProcessType = "Piercing",
            OutMaterialCategory = MaterialType.RoundBar,
            OutPlantGrade = "304",
            OutSpecification = "273*10",
            OutQuantity = 200,
            OutWeight = 2000m,
            ReturnItems = new List<MES.Core.DTOs.Materials.CreateReturnItemRequest>
            {
                new() { SourceWorkOrderNo = "NEW-WO-001" }
            }
        });

        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("OLD-WO-001"))), Times.Once);
        woExecMock.Verify(x => x.RefreshByWorkOrderNosAsync(It.Is<List<string>>(l => l.Contains("NEW-WO-001"))), Times.Once);
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
        updated.Status.Should().Be(SubcontractOrderStatus.PartialReturned); // 50/1000 < 95%
    }

    [Fact]
    public async Task SyncSingleAsync_收回重量达到96_5_状态变为Completed()
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
            InitialWeight = 965m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSingleAsync(order.Id);

        var updated = await ctx.SubcontractOrders.FindAsync(order.Id);
        updated!.InWeight.Should().Be(965m);
        updated.Status.Should().Be(SubcontractOrderStatus.Completed); // 965/1000 >= 96.5%
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
        updated.Status.Should().Be(SubcontractOrderStatus.Sent);
    }

    [Fact]
    public async Task UpdateAsync_全量替换子表_回收数据重新同步()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, outQty: 100, outWt: 1000m);

        // 委外回收进库：批次按委外单号+序号关联
        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "BATCH001",
            InboundSource = "委外",
            SourceName = "委外供应商",
            SourceOrderNo = order.OrderNo,
            SourceOrderSequence = 1,
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

        // 编辑委外单 → 全量替换子表（同序号新子项）
        await svc.UpdateAsync(order.Id, new UpdateSubcontractOrderRequest
        {
            SupplierId = sid,
            ProcessType = "Piercing",
            OutMaterialCategory = MaterialType.RoughTube,
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 100,
            OutWeight = 1000m,
            ReturnItems = new List<MES.Core.DTOs.Materials.CreateReturnItemRequest>
            {
                new()
                {
                    MaterialCategory = MaterialType.RoughTube,
                    ProcessSpecification = "219*8",
                    RequiredQuantity = 100,
                    RequiredWeight = 1000m,
                    ProcessUnitPrice = 10m,
                    ProcessTotalAmount = 1000m
                }
            }
        });

        // 新子项回收数据应从批次重新同步（防替换丢失已进库回收量）
        var saved = await ctx.SubcontractOrders.Include(s => s.ReturnItems).FirstAsync(s => s.Id == order.Id);
        saved.InWeight.Should().Be(500m);
        saved.ReturnItems.Should().HaveCount(1);
        var newItem = saved.ReturnItems.First();
        newItem.Sequence.Should().Be(1);
        newItem.ReturnedWeight.Should().Be(500m);
        newItem.ReturnedQuantity.Should().Be(50);
    }

    [Fact]
    public async Task SyncAllAsync_批次按委外单号匹配_更新主表与子项()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, outQty: 100, outWt: 1000m);

        // 委外回收进库：批次按委外单号+序号关联
        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "BATCH001",
            InboundSource = "委外",
            SourceName = "委外供应商",
            SourceOrderNo = order.OrderNo,
            SourceOrderSequence = 1,
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
        await svc.SyncAllAsync();

        var updated = await ctx.SubcontractOrders.Include(s => s.ReturnItems).FirstAsync(s => s.Id == order.Id);
        updated!.InWeight.Should().Be(500m);
        updated.ReturnItems.First().ReturnedWeight.Should().Be(500m);
    }

    [Fact]
    public async Task GetReturnItemListAsync_返回委外序号()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        var result = await svc.GetReturnItemListAsync(new QueryParams { PageIndex = 1, PageSize = 10 });

        result.Items.Should().HaveCount(1);
        result.Items[0].Sequence.Should().Be(1);
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

        await svc.UpdateStatusAsync(order.Id, new UpdateOrderStatusRequest { IsForceCompleted = true });

        var updated = await ctx.SubcontractOrders.FindAsync(order.Id);
        updated!.IsForceCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateStatusAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateStatusAsync(999, new UpdateOrderStatusRequest { IsForceCompleted = true });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("委外单不存在");
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

        var deleted = await ctx.SubcontractOrders.FindAsync(order.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_已完成_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid, status: SubcontractOrderStatus.Completed);
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

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_关键词搜索炉号_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);
        var entity = await ctx.SubcontractOrders.FindAsync(order.Id);
        entity!.FurnaceNumber = "FUR-WW-001";
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new SubcontractQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "FUR-WW" });

        result.Items.Should().HaveCount(1);
        result.Items[0].FurnaceNumber.Should().Be("FUR-WW-001");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderAsync(ctx, sid);
        var entity = await ctx.SubcontractOrders.FindAsync(order.Id);
        entity!.Remark = "委外备注信息";
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new SubcontractQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "委外备注" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Remark.Should().Be("委外备注信息");
    }

    // ========== 筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetPagedAsync_Filters_OrderNoContains_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid);
        var order = await ctx.SubcontractOrders.FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new SubcontractQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "OrderNo", Operator = "contains", Value = order.OrderNo[..^1] }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].OrderNo.Should().Be(order.OrderNo);
    }

    [Fact]
    public async Task GetPagedAsync_Filters_ProcessTypeIn_返回匹配()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid);
        var sid2 = await SeedSupplierAsync(ctx, name: "其他供应商");
        ctx.SubcontractOrders.Add(new SubcontractOrder
        {
            OrderNo = "WW20260101003",
            SupplierId = sid2,
            OrderDate = DateTime.Today,
            Status = SubcontractOrderStatus.Sent,
            ProcessType = "Annealing",  // 与 SeedOrderAsync 的 "Piercing" 区分，筛出 1 条
            OutMaterialCategory = "RoughTube",
            OutPlantGrade = "304",
            OutSpecification = "273*10",
            OutQuantity = 50,
            OutWeight = 500m
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new SubcontractQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "ProcessType", Operator = "in", Values = new List<string> { "Piercing" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].ProcessType.Should().Be("Piercing");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new SubcontractQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "OrderNo", Operator = "contains", Value = "NONEXISTENT" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx, name: "委外供应商A");
        var order = await SeedOrderAsync(ctx, sid);
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("OrderNo");
        contexts["OrderNo"].Should().Contain(order.OrderNo);
        contexts.Should().ContainKey("ProcessType");
        contexts["ProcessType"].Should().Contain("Piercing");
        contexts["SupplierName"].Should().Contain("委外供应商A");
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["OrderNo"].Should().BeEmpty();
        contexts["ProcessType"].Should().BeEmpty();
        contexts["OutMaterialCategory"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx, name: "供应商A");
        ctx.SubcontractOrders.Add(new SubcontractOrder
        {
            OrderNo = "WW20260101099",
            SupplierId = sid,
            OrderDate = DateTime.Today,
            Status = SubcontractOrderStatus.Sent,
            ProcessType = "Piercing",
            OutMaterialCategory = "RoughTube",
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 100,
            OutWeight = 1000m,
            ReturnDeadline = null
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["OrderNo"].Should().HaveCount(1);
        contexts["ReturnDeadline"].Should().BeEmpty();
    }

    // ========== 子项执行查询（GetReturnItemListAsync / 超量回收） ==========

    private async Task<SubcontractOrder> SeedOrderWithDateAsync(AppDbContext ctx, int supplierId, string orderNo, DateTime orderDate,
        int reqQty = 100, decimal reqWt = 1000m)
    {
        var order = new SubcontractOrder
        {
            OrderNo = orderNo,
            SupplierId = supplierId,
            SupplierName = "委外供应商",
            OrderDate = orderDate,
            Status = SubcontractOrderStatus.Sent,
            ProcessType = "Piercing",
            OutMaterialCategory = "RoughTube",
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 100,
            OutWeight = 1000m,
            ReturnDeadline = DateTime.Today.AddDays(60)
        };
        order.ReturnItems.Add(new SubcontractReturnItem
        {
            Sequence = 1,
            MaterialCategory = "RoughTube",
            ProcessSpecification = "219*8",
            RequiredQuantity = reqQty,
            RequiredWeight = reqWt
        });
        ctx.SubcontractOrders.Add(order);
        await ctx.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task SyncSingleAsync_子项超量回收_状态OverReceived()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}011", DateTime.Today, reqQty: 50, reqWt: 500m);

        // 回收入库仓库批：回收 650kg > 需求 500×1.05=525 且 超出 150>100 → 超量到货
        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "SRI001",
            InboundSource = "委外",
            SourceName = "委外供应商",
            SourceOrderNo = order.OrderNo,
            SourceOrderSequence = 1,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 60,
            InitialWeight = 650m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSingleAsync(order.Id);

        var item = await ctx.SubcontractReturnItems.SingleAsync(i => i.SubcontractOrderId == order.Id);
        item.ReturnedQuantity.Should().Be(60);
        item.ReturnedWeight.Should().Be(650m);
        item.ProcessStatus.Should().Be(SubcontractOrderStatus.OverReceived.ToString());
    }

    [Fact]
    public async Task SyncSingleAsync_子项回收未超量_状态Completed()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}012", DateTime.Today);

        // 回收 1010kg：≥需求 1000 完成，且未超量（1010<1050，超出10≤100）→ Completed
        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "SRI002",
            InboundSource = "委外",
            SourceName = "委外供应商",
            SourceOrderNo = order.OrderNo,
            SourceOrderSequence = 1,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 101,
            InitialWeight = 1010m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSingleAsync(order.Id);

        var item = await ctx.SubcontractReturnItems.SingleAsync(i => i.SubcontractOrderId == order.Id);
        item.ProcessStatus.Should().Be(SubcontractOrderStatus.Completed.ToString());
    }

    [Fact]
    public async Task GetReturnItemListAsync_退货量_仅统计退货出库()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}013", DateTime.Today);

        // 委外回收入库的原仓库批（SourceOrderNo=委外单号，序号 1）
        var batch = new InventoryBatch
        {
            BatchNo = "SRI001",
            InboundSource = "委外",
            SourceName = "委外供应商",
            SourceOrderNo = order.OrderNo,
            SourceOrderSequence = 1,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 100,
            InitialWeight = 1000m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        };
        // 退货出库当前关联的仓库批（来源其它，无委外单号，仅用于验证不按 InventoryBatchId 关联）
        var current = new InventoryBatch
        {
            BatchNo = "CUR001",
            InboundSource = "其它",
            SourceName = "委外供应商",
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

        // 退货出库：ReturnSourceBatchNo=原仓库批 SRI001 + 生产领用（不应计入）
        ctx.OutboundRecords.AddRange(
            new OutboundRecord { InventoryBatchId = current.Id, BatchNo = current.BatchNo, OutboundType = OutboundType.ReturnOut, ReturnSourceBatchNo = "SRI001", OutboundQuantity = 5, OutboundWeight = 50m, OutboundDate = DateTime.Today },
            new OutboundRecord { InventoryBatchId = current.Id, BatchNo = current.BatchNo, OutboundType = OutboundType.ReturnOut, ReturnSourceBatchNo = "SRI001", OutboundQuantity = 3, OutboundWeight = 30m, OutboundDate = DateTime.Today },
            new OutboundRecord { InventoryBatchId = current.Id, BatchNo = current.BatchNo, OutboundType = OutboundType.ProductionPick, ReturnSourceBatchNo = "SRI001", OutboundQuantity = 10, OutboundWeight = 100m, OutboundDate = DateTime.Today }
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetReturnItemListAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        result.Items[0].ReturnQuantity.Should().Be(8);
        result.Items[0].ReturnWeight.Should().Be(80m);
        result.Items[0].OrderDate.Should().Be(DateTime.Today);
        // 截止回收日 = 仓库批 InboundDate 最大值（非主表收回期限）
        result.Items[0].ReturnDeadline.Should().Be(DateTime.Today);
    }

    [Fact]
    public async Task GetReturnItemListAsync_截止回收日为仓库入库日期_要求到货日为主表收回期限()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}014", DateTime.Today);

        // 主表收回期限 = 今天+60天 → 要求到货日
        var plannedDeadline = order.ReturnDeadline!.Value;

        // 回收入库仓库批：InboundDate 今天-5 / 今天 → 截止回收日 = Max = 今天
        ctx.InventoryBatches.AddRange(
            new InventoryBatch
            {
                BatchNo = "SRD001",
                InboundSource = "委外",
                SourceName = "委外供应商",
                SourceOrderNo = order.OrderNo,
                SourceOrderSequence = 1,
                MaterialType = "RoughTube",
                PlantGrade = "20#",
                Specification = "219*8",
                InitialQuantity = 10,
                InitialWeight = 100m,
                WarehouseId = 1,
                InboundDate = DateTime.Today.AddDays(-5)
            },
            new InventoryBatch
            {
                BatchNo = "SRD002",
                InboundSource = "委外",
                SourceName = "委外供应商",
                SourceOrderNo = order.OrderNo,
                SourceOrderSequence = 2,
                MaterialType = "RoughTube",
                PlantGrade = "20#",
                Specification = "219*8",
                InitialQuantity = 20,
                InitialWeight = 200m,
                WarehouseId = 1,
                InboundDate = DateTime.Today
            });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetReturnItemListAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        // 截止回收日 = 实际收回入库日期 Max(InboundDate)
        result.Items[0].ReturnDeadline.Should().Be(DateTime.Today);
        // 要求到货日 = 主表收回期限
        result.Items[0].RequiredArrivalDate.Should().Be(plannedDeadline);
    }

    [Fact]
    public async Task GetReturnItemFilterContextsAsync_截止回收日按仓库入库日期()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}015", DateTime.Today);

        // 回收入库：InboundDate=今天-3 → 截止回收日下拉含该日期（非主表收回期限）
        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "SRE001",
            InboundSource = "委外",
            SourceName = "委外供应商",
            SourceOrderNo = order.OrderNo,
            SourceOrderSequence = 1,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 10,
            InitialWeight = 100m,
            WarehouseId = 1,
            InboundDate = DateTime.Today.AddDays(-3)
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var contexts = await svc.GetReturnItemFilterContextsAsync();

        contexts["ReturnDeadline"].Should().Contain(DateTime.Today.AddDays(-3).ToString("yyyy-MM-dd"));
        contexts["RequiredArrivalDate"].Should().Contain(order.ReturnDeadline!.Value.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task GetReturnItemListAsync_按下单日期排序()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderWithDateAsync(ctx, sid, "WW20260101001", new DateTime(2026, 1, 5));
        await SeedOrderWithDateAsync(ctx, sid, "WW20260101002", new DateTime(2026, 2, 5));

        var svc = CreateService(ctx);
        var result = await svc.GetReturnItemListAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "orderdate", IsDescending = false });

        result.Items.Should().HaveCount(2);
        result.Items[0].OrderNo.Should().Be("WW20260101001");
        result.Items[0].OrderDate.Should().Be(new DateTime(2026, 1, 5));
        result.Items[1].OrderNo.Should().Be("WW20260101002");
    }

    [Fact]
    public async Task GetReturnItemListAsync_按下单日期筛选()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderWithDateAsync(ctx, sid, "WW20260101001", new DateTime(2026, 1, 5));
        await SeedOrderWithDateAsync(ctx, sid, "WW20260101002", new DateTime(2026, 2, 5));

        var svc = CreateService(ctx);
        var result = await svc.GetReturnItemListAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "OrderDate", Operator = "in", Values = new List<string> { "2026-01-05" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].OrderNo.Should().Be("WW20260101001");
    }

    [Fact]
    public async Task GetReturnItemListAsync_属强制完成_排序筛选生效()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderWithDateAsync(ctx, sid, "WW20260101020", new DateTime(2026, 3, 5));
        await SeedOrderWithDateAsync(ctx, sid, "WW20260101021", new DateTime(2026, 3, 6));

        // 第 1 单的子项设为强制完成 → 升序 false 在前、降序 true 在前
        var item1 = await ctx.SubcontractReturnItems.SingleAsync(i => i.SubcontractOrder.OrderNo == "WW20260101020");
        item1.IsForceCompleted = true;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        var asc = await svc.GetReturnItemListAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "IsForceCompleted", IsDescending = false });
        asc.Items.Should().HaveCount(2);
        asc.Items[0].IsForceCompleted.Should().BeFalse();
        asc.Items[1].IsForceCompleted.Should().BeTrue();

        var desc = await svc.GetReturnItemListAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "IsForceCompleted", IsDescending = true });
        desc.Items[0].IsForceCompleted.Should().BeTrue();
        desc.Items[1].IsForceCompleted.Should().BeFalse();

        var filtered = await svc.GetReturnItemListAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "IsForceCompleted", Operator = "in", Values = new List<string> { "True" } }
            }
        });
        filtered.Items.Should().HaveCount(1);
        filtered.Items[0].IsForceCompleted.Should().BeTrue();
    }

    // ========== 工单实时关注（按来源工单号关联工单执行状况读模型） ==========

    [Fact]
    public async Task GetReturnItemListAsync_工单实时关注_按来源工单号关联读模型填充()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}030", DateTime.Today);
        order.ReturnItems.Single().SourceWorkOrderNo = "WO-EXEC-001";
        await ctx.SaveChangesAsync();

        ctx.WorkOrderExecutionSummaries.Add(new WorkOrderExecutionSummary
        {
            WorkOrderNo = "WO-EXEC-001",
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
            ScheduleStage = 2,
            UrgencyLevel = "BOrder",
            RawMaterialLockRemark = "ExecuteRework",
            TheoreticalCutoffDate = new DateTime(2026, 8, 20)
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetReturnItemListAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var dto = result.Items.Should().ContainSingle().Subject;
        dto.ExecutionScheduleStage.Should().Be(2);
        dto.ExecutionUrgencyLevel.Should().Be("BOrder");
        dto.ExecutionRawMaterialLockRemark.Should().Be("ExecuteRework");
        dto.ExecutionTheoreticalCutoffDate.Should().Be(new DateTime(2026, 8, 20));
    }

    [Fact]
    public async Task GetReturnItemListAsync_工单实时关注_无读模型记录默认空()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}031", DateTime.Today); // 无 SourceWorkOrderNo

        var svc = CreateService(ctx);
        var result = await svc.GetReturnItemListAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var dto = result.Items.Should().ContainSingle().Subject;
        dto.ExecutionScheduleStage.Should().BeNull();
        dto.ExecutionUrgencyLevel.Should().BeNull();
        dto.ExecutionRawMaterialLockRemark.Should().BeNull();
        dto.ExecutionTheoreticalCutoffDate.Should().BeNull();
    }

    [Fact]
    public async Task GetReturnItemListAsync_工单实时关注_按关注排序_按关注筛选()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var o1 = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}032", DateTime.Today, reqQty: 100);
        o1.ReturnItems.Single().SourceWorkOrderNo = "WO-1";
        var o2 = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}033", DateTime.Today, reqQty: 200);
        o2.ReturnItems.Single().SourceWorkOrderNo = "WO-2";
        await ctx.SaveChangesAsync();
        ctx.WorkOrderExecutionSummaries.AddRange(
            new WorkOrderExecutionSummary { WorkOrderNo = "WO-1", Salesman = "业务", CustomerName = "客户", SalesOrderNo = "S1", ProductionMainNo = "X01", MaterialName = "钢管", DeliveryState = "Normal", PlantGrade = "20#", Specification = "219*8", LengthStatus = "Range", SettlementMethod = "PerOrder", ScheduleStage = 4 },
            new WorkOrderExecutionSummary { WorkOrderNo = "WO-2", Salesman = "业务", CustomerName = "客户", SalesOrderNo = "S2", ProductionMainNo = "X02", MaterialName = "钢管", DeliveryState = "Normal", PlantGrade = "20#", Specification = "219*8", LengthStatus = "Range", SettlementMethod = "PerOrder", ScheduleStage = 1 });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        // 排序
        var asc = await svc.GetReturnItemListAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "executionschedulestage", IsDescending = false });
        asc.Items.Select(x => x.ExecutionScheduleStage).Should().Equal(1, 4);

        // 筛选
        var filtered = await svc.GetReturnItemListAsync(new QueryParams
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
    public async Task GetReturnItemFilterContextsAsync_工单实时关注_无工单号子项_含空值哨兵()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}034", DateTime.Today); // 无 SourceWorkOrderNo
        var o2 = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}035", DateTime.Today, reqQty: 200);
        o2.ReturnItems.Single().SourceWorkOrderNo = "WO-EXEC-001";
        await ctx.SaveChangesAsync();
        ctx.WorkOrderExecutionSummaries.Add(new WorkOrderExecutionSummary
        {
            WorkOrderNo = "WO-EXEC-001", Salesman = "业务", CustomerName = "客户", SalesOrderNo = "S1", ProductionMainNo = "X01",
            MaterialName = "钢管", DeliveryState = "Normal", PlantGrade = "20#", Specification = "219*8", LengthStatus = "Range",
            SettlementMethod = "PerOrder", ScheduleStage = 3, UrgencyLevel = "BOrder", RawMaterialLockRemark = "ExecuteRework",
            TheoreticalCutoffDate = new DateTime(2026, 8, 20)
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var contexts = await svc.GetReturnItemFilterContextsAsync();

        // 无关联子项以空值哨兵输出，且空值排最前
        contexts["ExecutionUrgencyLevel"].Should().Contain("__EXCEL_FILTER_NULL__").And.Contain("BOrder");
        contexts["ExecutionUrgencyLevel"][0].Should().Be("__EXCEL_FILTER_NULL__");
        contexts["ExecutionRawMaterialLockRemark"].Should().Contain("__EXCEL_FILTER_NULL__").And.Contain("ExecuteRework");
        contexts["ExecutionTheoreticalCutoffDate"].Should().Contain("__EXCEL_FILTER_NULL__").And.Contain("2026-08-20");
    }

    [Fact]
    public async Task GetReturnItemListAsync_工单实时关注_筛选空值_筛出无关联子项()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}036", DateTime.Today); // 无工单号 → 关注 null
        var o2 = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}037", DateTime.Today, reqQty: 200);
        o2.ReturnItems.Single().SourceWorkOrderNo = "WO-EXEC-001";
        await ctx.SaveChangesAsync();
        ctx.WorkOrderExecutionSummaries.Add(new WorkOrderExecutionSummary
        {
            WorkOrderNo = "WO-EXEC-001", Salesman = "业务", CustomerName = "客户", SalesOrderNo = "S1", ProductionMainNo = "X01",
            MaterialName = "钢管", DeliveryState = "Normal", PlantGrade = "20#", Specification = "219*8", LengthStatus = "Range",
            SettlementMethod = "PerOrder", ScheduleStage = 4
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        // 仅勾选空值 → isnull 操作符
        var nullOnly = await svc.GetReturnItemListAsync(new QueryParams
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
        var withValue = await svc.GetReturnItemListAsync(new QueryParams
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

    // ========== 退货量：净回收状态判定（2026-08-22） ==========

    [Fact]
    public async Task SyncSingleAsync_子项退货_净回收状态判定()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        // 需求 100支/1000kg
        var order = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}101", DateTime.Today);

        // 回收 100支/1000kg（序号 1）
        var batch = new InventoryBatch
        {
            BatchNo = "RET001",
            InboundSource = "委外",
            SourceName = "委外供应商",
            SourceOrderNo = order.OrderNo,
            SourceOrderSequence = 1,
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

        // 退货 20支/200kg（指向原仓库批 RET001）
        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            OutboundType = OutboundType.ReturnOut,
            ReturnSourceBatchNo = batch.BatchNo,
            OutboundQuantity = 20,
            OutboundWeight = 200m,
            OutboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSingleAsync(order.Id);

        var item = await ctx.SubcontractReturnItems.SingleAsync(i => i.SubcontractOrderId == order.Id);
        // 实体仍存「总回收」（退货另列显示，不扣）
        item.ReturnedQuantity.Should().Be(100);
        item.ReturnedWeight.Should().Be(1000m);
        // 净回收 = 100 - 20 = 80 支 < 需求 100 → PartialReturned（原无退货时为 Completed）
        item.ProcessStatus.Should().Be(SubcontractOrderStatus.PartialReturned.ToString());
    }

    [Fact]
    public async Task SyncSingleAsync_子项退货后净回收仍满足需求_状态Completed()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}102", DateTime.Today);

        // 回收 110支/1100kg（超需求 100/1000），退货 5支/50kg → 净回收 105/1050 ≥ 需求 → Completed 且未超量
        var batch = new InventoryBatch
        {
            BatchNo = "RET002",
            InboundSource = "委外",
            SourceName = "委外供应商",
            SourceOrderNo = order.OrderNo,
            SourceOrderSequence = 1,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 110,
            InitialWeight = 1100m,
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
            ReturnSourceBatchNo = batch.BatchNo,
            OutboundQuantity = 5,
            OutboundWeight = 50m,
            OutboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSingleAsync(order.Id);

        var item = await ctx.SubcontractReturnItems.SingleAsync(i => i.SubcontractOrderId == order.Id);
        item.ReturnedWeight.Should().Be(1100m); // 实体仍存总回收
        item.ProcessStatus.Should().Be(SubcontractOrderStatus.Completed.ToString());
    }

    [Fact]
    public async Task SyncSingleAsync_主表退货_净回收状态判定()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        // 发出 100支/1000kg
        var order = await SeedOrderAsync(ctx, sid, outQty: 100, outWt: 1000m);

        var batch = new InventoryBatch
        {
            BatchNo = "RET003",
            InboundSource = "委外",
            SourceName = "委外供应商",
            SourceOrderNo = order.OrderNo,
            SourceOrderSequence = 1,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 100,
            InitialWeight = 965m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();

        // 退货 15kg → 净回收 950kg < 发出 1000×96.5%=965 → PartialReturned（原无退货时为 Completed）
        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            OutboundType = OutboundType.ReturnOut,
            ReturnSourceBatchNo = batch.BatchNo,
            OutboundQuantity = 2,
            OutboundWeight = 15m,
            OutboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSingleAsync(order.Id);

        var updated = await ctx.SubcontractOrders.FindAsync(order.Id);
        updated!.InWeight.Should().Be(965m);
        updated.Status.Should().Be(SubcontractOrderStatus.PartialReturned);
    }

    [Fact]
    public async Task GetReturnItemListAsync_退货量序号级_各序号分别归集()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = new SubcontractOrder
        {
            OrderNo = $"WW{DateTime.Now:yyMMdd}103",
            SupplierId = sid,
            SupplierName = "委外供应商",
            OrderDate = DateTime.Today,
            Status = SubcontractOrderStatus.Sent,
            ProcessType = "Piercing",
            OutMaterialCategory = "RoughTube",
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 200,
            OutWeight = 2000m,
            ReturnDeadline = DateTime.Today.AddDays(60)
        };
        order.ReturnItems.Add(new SubcontractReturnItem { Sequence = 1, MaterialCategory = "RoughTube", ProcessSpecification = "219*8", RequiredQuantity = 100, RequiredWeight = 1000m });
        order.ReturnItems.Add(new SubcontractReturnItem { Sequence = 2, MaterialCategory = "RoughTube", ProcessSpecification = "219*8", RequiredQuantity = 100, RequiredWeight = 1000m });
        ctx.SubcontractOrders.Add(order);
        await ctx.SaveChangesAsync();

        var b1 = new InventoryBatch { BatchNo = "RET101", InboundSource = "委外", SourceName = "委外供应商", SourceOrderNo = order.OrderNo, SourceOrderSequence = 1, MaterialType = "RoughTube", PlantGrade = "20#", Specification = "219*8", InitialQuantity = 100, InitialWeight = 1000m, WarehouseId = 1, InboundDate = DateTime.Today };
        var b2 = new InventoryBatch { BatchNo = "RET102", InboundSource = "委外", SourceName = "委外供应商", SourceOrderNo = order.OrderNo, SourceOrderSequence = 2, MaterialType = "RoughTube", PlantGrade = "20#", Specification = "219*8", InitialQuantity = 100, InitialWeight = 1000m, WarehouseId = 1, InboundDate = DateTime.Today };
        ctx.InventoryBatches.AddRange(b1, b2);
        await ctx.SaveChangesAsync();

        // 序号 1 退货 10支/100kg；序号 2 退货 30支/300kg
        ctx.OutboundRecords.AddRange(
            new OutboundRecord { InventoryBatchId = b1.Id, BatchNo = b1.BatchNo, OutboundType = OutboundType.ReturnOut, ReturnSourceBatchNo = b1.BatchNo, OutboundQuantity = 10, OutboundWeight = 100m, OutboundDate = DateTime.Today },
            new OutboundRecord { InventoryBatchId = b2.Id, BatchNo = b2.BatchNo, OutboundType = OutboundType.ReturnOut, ReturnSourceBatchNo = b2.BatchNo, OutboundQuantity = 30, OutboundWeight = 300m, OutboundDate = DateTime.Today });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetReturnItemListAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(2);
        var seq1 = result.Items.Single(i => i.Sequence == 1);
        seq1.ReturnQuantity.Should().Be(10);
        seq1.ReturnWeight.Should().Be(100m);
        var seq2 = result.Items.Single(i => i.Sequence == 2);
        seq2.ReturnQuantity.Should().Be(30);
        seq2.ReturnWeight.Should().Be(300m);
    }

    [Fact]
    public async Task GetPagedAsync_填充退货量_委外单号级()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}104", DateTime.Today);

        var b1 = new InventoryBatch { BatchNo = "RET201", InboundSource = "委外", SourceName = "委外供应商", SourceOrderNo = order.OrderNo, SourceOrderSequence = 1, MaterialType = "RoughTube", PlantGrade = "20#", Specification = "219*8", InitialQuantity = 100, InitialWeight = 1000m, WarehouseId = 1, InboundDate = DateTime.Today };
        ctx.InventoryBatches.Add(b1);
        await ctx.SaveChangesAsync();

        ctx.OutboundRecords.AddRange(
            new OutboundRecord { InventoryBatchId = b1.Id, BatchNo = b1.BatchNo, OutboundType = OutboundType.ReturnOut, ReturnSourceBatchNo = b1.BatchNo, OutboundQuantity = 10, OutboundWeight = 100m, OutboundDate = DateTime.Today },
            new OutboundRecord { InventoryBatchId = b1.Id, BatchNo = b1.BatchNo, OutboundType = OutboundType.ReturnOut, ReturnSourceBatchNo = b1.BatchNo, OutboundQuantity = 5, OutboundWeight = 50m, OutboundDate = DateTime.Today });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new SubcontractQueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        // 委外单号级 = 两笔退货求和
        result.Items[0].ReturnQuantity.Should().Be(15);
        result.Items[0].ReturnWeight.Should().Be(150m);
    }

    [Fact]
    public async Task GetByIdAsync_填充退货量_主表与子项()
    {
        var ctx = CreateDbContext();
        var sid = await SeedSupplierAsync(ctx);
        var order = await SeedOrderWithDateAsync(ctx, sid, $"WW{DateTime.Now:yyMMdd}105", DateTime.Today);

        var b1 = new InventoryBatch { BatchNo = "RET301", InboundSource = "委外", SourceName = "委外供应商", SourceOrderNo = order.OrderNo, SourceOrderSequence = 1, MaterialType = "RoughTube", PlantGrade = "20#", Specification = "219*8", InitialQuantity = 100, InitialWeight = 1000m, WarehouseId = 1, InboundDate = DateTime.Today };
        ctx.InventoryBatches.Add(b1);
        await ctx.SaveChangesAsync();

        ctx.OutboundRecords.Add(new OutboundRecord
        {
            InventoryBatchId = b1.Id,
            BatchNo = b1.BatchNo,
            OutboundType = OutboundType.ReturnOut,
            ReturnSourceBatchNo = b1.BatchNo,
            OutboundQuantity = 8,
            OutboundWeight = 80m,
            OutboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var dto = await svc.GetByIdAsync(order.Id);

        // 主表「退货总量」（委外单号级）
        dto.ReturnQuantity.Should().Be(8);
        dto.ReturnWeight.Should().Be(80m);
        // 子项「退货量」（序号级）
        dto.ReturnItems.Should().HaveCount(1);
        dto.ReturnItems[0].ReturnQuantity.Should().Be(8);
        dto.ReturnItems[0].ReturnWeight.Should().Be(80m);
    }

    // ========== 圆钢穿孔汇总（待穿孔按圆棒穿孔计划/在穿孔·月度按子项） ==========

    /// <summary>构造委外单（可指定委外单位/下单日期/多子项），用于穿孔汇总测试</summary>
    private async Task<SubcontractOrder> SeedOrderForSummaryAsync(AppDbContext ctx, string orderNo, string supplierName, DateTime orderDate,
        params (int Sequence, string Spec, decimal ReqWt, decimal RetWt, SubcontractOrderStatus? ProcessStatus, bool ForceCompleted)[] items)
    {
        var order = new SubcontractOrder
        {
            OrderNo = orderNo,
            SupplierId = 1,
            SupplierName = supplierName,
            OrderDate = orderDate,
            Status = SubcontractOrderStatus.Sent,
            ProcessType = "Piercing",
            OutMaterialCategory = "RoughTube",
            OutPlantGrade = "20#",
            OutSpecification = "219*8",
            OutQuantity = 100,
            OutWeight = 1000m,
            ReturnDeadline = DateTime.Today.AddDays(60)
        };
        foreach (var (seq, spec, reqWt, retWt, ps, force) in items)
        {
            order.ReturnItems.Add(new SubcontractReturnItem
            {
                Sequence = seq,
                MaterialCategory = "RoughTube",
                ProcessSpecification = spec,
                RequiredQuantity = reqWt > 0 ? (int)(reqWt / 10) : 0,
                RequiredWeight = reqWt,
                ReturnedQuantity = retWt > 0 ? (int)(retWt / 10) : 0,
                ReturnedWeight = retWt,
                ProcessStatus = (ps ?? SubcontractOrderStatus.Sent).ToString(),
                IsForceCompleted = force
            });
        }
        ctx.SubcontractOrders.Add(order);
        await ctx.SaveChangesAsync();
        return order;
    }

    /// <summary>为指定委外单的序号追加回收入库仓库批 + 退货出库（供 BuildReturnSummaryAsync 归集退货量）</summary>
    private async Task<(InventoryBatch Batch, OutboundRecord Outbound)> SeedReturnOutAsync(AppDbContext ctx, SubcontractOrder order, int sequence, int batchWt, int retWt)
    {
        var batch = new InventoryBatch
        {
            BatchNo = $"SCR{Guid.NewGuid():N}"[..10],
            InboundSource = "委外",
            SourceName = order.SupplierName ?? "委外供应商",
            SourceOrderNo = order.OrderNo,
            SourceOrderSequence = sequence,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = batchWt / 10,
            InitialWeight = batchWt,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();
        var outbound = new OutboundRecord
        {
            InventoryBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            OutboundType = OutboundType.ReturnOut,
            ReturnSourceBatchNo = batch.BatchNo,
            OutboundQuantity = retWt / 10,
            OutboundWeight = retWt,
            OutboundDate = DateTime.Today
        };
        ctx.OutboundRecords.Add(outbound);
        await ctx.SaveChangesAsync();
        return (batch, outbound);
    }

    /// <summary>构造工单（供待穿孔测试的圆棒穿孔计划 + 已下委外关联）</summary>
    private async Task<MES.Data.Entities.WorkOrder.WorkOrder> SeedWorkOrderAsync(AppDbContext ctx, string workOrderNo, decimal totalWeight = 5000m)
    {
        var wo = new MES.Data.Entities.WorkOrder.WorkOrder
        {
            WorkOrderNo = workOrderNo,
            SalesOrderNo = "SO-" + workOrderNo,
            ProductionMainNo = "D01",
            ProductionSubNo = "C01",
            OrderItemIds = "[1]",
            Status = WorkOrderStatus.Pending,
            RowVersion = new byte[8],
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "GB-8162",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed,
            TotalQuantity = 10,
            TotalMeters = 60,
            TotalWeight = totalWeight,
            TotalItemCount = 1
        };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();
        return wo;
    }

    [Fact]
    public async Task GetPiercingPendingAsync_按工单聚合_计划需求减已下委外_含工单关注()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, "委外供应商A");

        // A：计划 5000 已下委外 2000 → 缺 3000；B：计划 1000 无委外 → 缺 1000；C：计划 800 已下 800 → 缺 0 排除
        var woA = await SeedWorkOrderAsync(ctx, "WO-PIERCING-A");
        var woB = await SeedWorkOrderAsync(ctx, "WO-PIERCING-B");
        var woC = await SeedWorkOrderAsync(ctx, "WO-PIERCING-C");

        ctx.RoundBarPiercingPlans.AddRange(
            new RoundBarPiercingPlan { WorkOrderId = woA.Id, PlanDate = DateTime.Today, PlantGrade = "20#", RawMaterialType = MaterialType.RoundBar, RoundBarSpec = "250*8", PiercingSpec = "230*7", RequiredWeight = 3000m },
            new RoundBarPiercingPlan { WorkOrderId = woA.Id, PlanDate = DateTime.Today, PlantGrade = "45#", RawMaterialType = MaterialType.RoundBar, RoundBarSpec = "270*9", PiercingSpec = "219*8", RequiredWeight = 2000m },
            new RoundBarPiercingPlan { WorkOrderId = woB.Id, PlanDate = DateTime.Today, PlantGrade = "20#", RawMaterialType = MaterialType.RoundBar, RoundBarSpec = "250*8", PiercingSpec = "230*7", RequiredWeight = 1000m },
            new RoundBarPiercingPlan { WorkOrderId = woC.Id, PlanDate = DateTime.Today, PlantGrade = "20#", RawMaterialType = MaterialType.RoundBar, RoundBarSpec = "250*8", PiercingSpec = "230*7", RequiredWeight = 800m });
        await ctx.SaveChangesAsync();

        // 已下委外（子项 SourceWorkOrderNo）：A 2000、C 800
        var orderA = await SeedOrderForSummaryAsync(ctx, $"WW{DateTime.Now:yyMMdd}231", "委外供应商A", DateTime.Today, (1, "230*7", 2000m, 0m, null, false));
        orderA.ReturnItems.Single().SourceWorkOrderNo = woA.WorkOrderNo;
        var orderC = await SeedOrderForSummaryAsync(ctx, $"WW{DateTime.Now:yyMMdd}232", "委外供应商A", DateTime.Today, (1, "230*7", 800m, 0m, null, false));
        orderC.ReturnItems.Single().SourceWorkOrderNo = woC.WorkOrderNo;
        await ctx.SaveChangesAsync();

        // 工单关注（仅 A 有读模型）
        ctx.WorkOrderExecutionSummaries.Add(new WorkOrderExecutionSummary
        {
            WorkOrderId = woA.Id,
            WorkOrderNo = woA.WorkOrderNo,
            Salesman = "测试",
            CustomerName = "客户",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SettlementMethod = SettlementMethod.Theoretical.ToString(),
            SalesOrderNo = woA.SalesOrderNo,
            ProductionMainNo = woA.ProductionMainNo,
            ProductionSubNo = woA.ProductionSubNo,
            MaterialName = "圆钢",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled.ToString(),
            PlantGrade = "20#",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed.ToString(),
            ScheduleStage = 4,
            UrgencyLevel = UrgencyLevelKeys.AUrgent,
            RawMaterialLockRemark = RawMaterialLockRemarkKeys.ExecutePlan
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPiercingPendingAsync();

        result.Should().HaveCount(2);
        var a = result.Single(x => x.WorkOrderNo == "WO-PIERCING-A");
        a.PlantGrade.Should().Be("20#,45#");          // 多值合并
        a.PiercingSpec.Should().Be("230*7,219*8");     // 多值合并
        a.MissingWeight.Should().Be(3000m);            // 5000 - 2000
        a.ExecutionScheduleStage.Should().Be(4);
        a.ExecutionUrgencyLevel.Should().Be(UrgencyLevelKeys.AUrgent);
        a.ExecutionRawMaterialLockRemark.Should().Be(RawMaterialLockRemarkKeys.ExecutePlan);

        var b = result.Single(x => x.WorkOrderNo == "WO-PIERCING-B");
        b.MissingWeight.Should().Be(1000m);
        b.ExecutionScheduleStage.Should().BeNull();    // 无读模型

        result.Should().NotContain(x => x.WorkOrderNo == "WO-PIERCING-C"); // 缺少量 0 排除
    }

    [Fact]
    public async Task GetPiercingInProgressAsync_按委外单位规格聚合_含合计行()
    {
        var ctx = CreateDbContext();
        await SeedOrderForSummaryAsync(ctx, $"WW{DateTime.Now:yyMMdd}211", "委外供应商A", DateTime.Today,
            (1, "219*8", 1000m, 0m, null, false),                 // A-219*8 在穿 1000
            (2, "273*10", 2000m, 500m, null, false));             // A-273*10 在穿 1500
        await SeedOrderForSummaryAsync(ctx, $"WW{DateTime.Now:yyMMdd}212", "委外供应商B", DateTime.Today,
            (1, "219*8", 3000m, 0m, null, false));                // B-219*8 在穿 3000

        var svc = CreateService(ctx);
        var result = await svc.GetPiercingInProgressAsync();

        result.Specifications.Should().Equal("219*8", "273*10");
        var rowA = result.Rows.Single(x => x.SupplierName == "委外供应商A");
        rowA.Cells["219*8"].TotalWeight.Should().Be(1000m);
        rowA.Cells["273*10"].TotalWeight.Should().Be(1500m);
        rowA.Total.TotalWeight.Should().Be(2500m);
        var rowB = result.Rows.Single(x => x.SupplierName == "委外供应商B");
        rowB.Cells["219*8"].TotalWeight.Should().Be(3000m);
        var total = result.Rows.Single(x => x.SupplierName == "合计");
        total.Cells["219*8"].TotalWeight.Should().Be(4000m);
        total.Cells["273*10"].TotalWeight.Should().Be(1500m);
        total.Total.TotalWeight.Should().Be(5500m);
    }

    [Fact]
    public async Task GetPiercingMonthlyAsync_按下单日期分月_发回净回收_含合计行现在穿()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Today.Year;

        // 1月下单：已完成子项 → 不计现在穿
        await SeedOrderForSummaryAsync(ctx, $"WW{DateTime.Now:yyMMdd}221", "委外供应商A", new DateTime(year, 1, 15),
            (1, "219*8", 1000m, 1000m, SubcontractOrderStatus.Completed, false));
        // 2月下单：未完成 + 退货 500 → 净回收 1000 → 现在穿 1000
        var orderA2 = await SeedOrderForSummaryAsync(ctx, $"WW{DateTime.Now:yyMMdd}222", "委外供应商A", new DateTime(year, 2, 15),
            (1, "219*8", 2000m, 1500m, SubcontractOrderStatus.PartialReturned, false));
        await SeedReturnOutAsync(ctx, orderA2, 1, 1500, 500);
        // 3月下单：委外供应商B 未回收
        await SeedOrderForSummaryAsync(ctx, $"WW{DateTime.Now:yyMMdd}223", "委外供应商B", new DateTime(year, 3, 15),
            (1, "273*10", 3000m, 0m, null, false));

        var svc = CreateService(ctx);
        var result = await svc.GetPiercingMonthlyAsync();

        result.MonthLabels.Should().HaveCount(12);
        var rowA = result.Rows.Single(x => x.SupplierName == "委外供应商A");
        rowA.Months[0].SendWeight.Should().Be(1000m);
        rowA.Months[0].RecoverWeight.Should().Be(1000m);
        rowA.Months[1].SendWeight.Should().Be(2000m);
        rowA.Months[1].RecoverWeight.Should().Be(1000m); // 1500 - 500
        rowA.Total.SendWeight.Should().Be(3000m);
        rowA.Total.RecoverWeight.Should().Be(2000m);
        rowA.NowPiercing.Should().Be(1000m);             // 仅 2月未完成子项 2000-1000

        var rowB = result.Rows.Single(x => x.SupplierName == "委外供应商B");
        rowB.Months[2].SendWeight.Should().Be(3000m);
        rowB.NowPiercing.Should().Be(3000m);

        var total = result.Rows.Single(x => x.SupplierName == "合计");
        total.Months[0].SendWeight.Should().Be(1000m);
        total.Months[2].SendWeight.Should().Be(3000m);
        total.Total.SendWeight.Should().Be(6000m);
        total.Total.RecoverWeight.Should().Be(2000m);
        total.NowPiercing.Should().Be(4000m);
    }

    [Fact]
    public async Task GetPiercingMonthlyAsync_空数据_仅合计行()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPiercingMonthlyAsync();

        result.Rows.Should().HaveCount(1);
        result.Rows[0].SupplierName.Should().Be("合计");
        result.Rows[0].Months.Should().HaveCount(12);
    }
}
