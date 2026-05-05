using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 仓库服务测试：CRUD、关键字搜索、激活筛选、删除关联检查
/// </summary>
public class WarehouseServiceTests : TestBase
{
    private WarehouseService CreateService(AppDbContext ctx) => new(ctx);

    private async Task<Warehouse> SeedWarehouseAsync(AppDbContext ctx, string code = "WH001", string name = "测试仓库",
        int sortOrder = 1, bool isActive = true)
    {
        var entity = new Warehouse
        {
            Code = code,
            Name = name,
            SortOrder = sortOrder,
            IsActive = isActive
        };
        ctx.Warehouses.Add(entity);
        await ctx.SaveChangesAsync();
        return entity;
    }

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_按仓库编码搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx, code: "WH001");
        await SeedWarehouseAsync(ctx, code: "WH002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "WH001" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Code.Should().Be("WH001");
    }

    [Fact]
    public async Task GetPagedAsync_按仓库名称搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx, name: "一号仓库");
        await SeedWarehouseAsync(ctx, name: "二号仓库");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "一号" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("一号仓库");
    }

    [Fact]
    public async Task GetPagedAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_按IsActive筛选_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx, code: "WH-ACTIVE", isActive: true);
        await SeedWarehouseAsync(ctx, code: "WH-INACTIVE", isActive: false);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 }, isActive: true);

        result.Items.Should().HaveCount(1);
        result.Items[0].Code.Should().Be("WH-ACTIVE");
    }

    [Fact]
    public async Task GetPagedAsync_按编码排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx, code: "B-WH");
        await SeedWarehouseAsync(ctx, code: "A-WH");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "Code", IsDescending = false });

        result.Items[0].Code.Should().Be("A-WH");
        result.Items[1].Code.Should().Be("B-WH");
    }

    [Fact]
    public async Task GetPagedAsync_删除后不显示()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx);
        var id = await ctx.Warehouses.Select(w => w.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });
        result.Items.Should().BeEmpty();
    }

    // ========== GetAllAsync ==========

    [Fact]
    public async Task GetAllAsync_默认只返回激活()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx, code: "ACTIVE", isActive: true);
        await SeedWarehouseAsync(ctx, code: "INACTIVE", isActive: false);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync();

        result.Should().HaveCount(1);
        result[0].Code.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task GetAllAsync_onlyActiveFalse_返回全部()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx, code: "ACTIVE", isActive: true);
        await SeedWarehouseAsync(ctx, code: "INACTIVE", isActive: false);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(onlyActive: false);

        result.Should().HaveCount(2);
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx);
        var id = await ctx.Warehouses.Select(w => w.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result.Code.Should().Be("WH001");
        result.Name.Should().Be("测试仓库");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("仓库不存在");
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建仓库()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateWarehouseRequest
        {
            Code = "WH-NEW",
            Name = "新仓库",
            SortOrder = 10,
            IsActive = true
        });

        result.Should().NotBeNull();
        result.Code.Should().Be("WH-NEW");
        result.Name.Should().Be("新仓库");
        result.IsActive.Should().BeTrue();

        var saved = await ctx.Warehouses.FirstAsync(w => w.Code == "WH-NEW");
        saved.Code.Should().Be("WH-NEW");
    }

    [Fact]
    public async Task CreateAsync_重复编码_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx, code: "WH001");
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateWarehouseRequest
        {
            Code = "WH001",
            Name = "重复仓库",
            SortOrder = 1
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已存在*");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新仓库()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx);
        var id = await ctx.Warehouses.Select(w => w.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateWarehouseRequest
        {
            Name = "新名称",
            IsActive = false
        });

        result.Name.Should().Be("新名称");
        result.IsActive.Should().BeFalse();

        var saved = await ctx.Warehouses.FirstAsync(w => w.Id == id);
        saved.Name.Should().Be("新名称");
        saved.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateWarehouseRequest
        {
            Code = "NEW",
            Name = "新仓库"
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("仓库不存在");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedWarehouseAsync(ctx);
        var id = await ctx.Warehouses.Select(w => w.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.Warehouses.FindAsync(id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("仓库不存在");
    }

    [Fact]
    public async Task DeleteAsync_有关联库存批次_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var whId = wh.Id;

        // 创建关联的库存批次
        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "BATCH001",
            WarehouseId = whId,
            MaterialType = "钢管",
            PlantGrade = "20#",
            Specification = "219*8",
            InboundSource = "采购",
            SourceName = "供应商",
            InboundDate = DateTime.Today,
            InitialQuantity = 10,
            InitialWeight = 1000m,
            RemainingQuantity = 10,
            RemainingWeight = 1000m
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var act = () => svc.DeleteAsync(whId);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*库存批次*");
    }
}
