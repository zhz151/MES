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
/// 物料服务测试：物料CRUD、分类查询、匹配、删除关联检查
/// </summary>
public class MaterialServiceTests : TestBase
{
    private MaterialService CreateService(AppDbContext ctx) => new(ctx);

    private async Task SeedMaterialAsync(AppDbContext ctx, string category = "钢管", string grade = "20#", string spec = "219*8", bool isActive = true)
    {
        ctx.Materials.Add(new Material
        {
            MaterialCategory = category,
            PlantGrade = grade,
            Specification = spec,
            IsActive = isActive,
            Remark = null
        });
        await ctx.SaveChangesAsync();
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
    public async Task GetPagedAsync_按关键字搜索分类_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx, category: "不锈钢管");
        await SeedMaterialAsync(ctx, category: "碳钢管");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "不锈钢" });

        result.Items.Should().HaveCount(1);
        result.Items[0].MaterialCategory.Should().Be("不锈钢管");
    }

    [Fact]
    public async Task GetPagedAsync_按关键字搜索钢种_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx, grade: "304");
        await SeedMaterialAsync(ctx, grade: "316L");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "304" });

        result.Items.Should().HaveCount(1);
        result.Items[0].PlantGrade.Should().Be("304");
    }

    [Fact]
    public async Task GetPagedAsync_按关键字搜索规格_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx, spec: "219*8");
        await SeedMaterialAsync(ctx, spec: "273*10");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "219" });

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_软删除后不显示()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx, category: "待删除物料");
        var id = await ctx.Materials.Select(m => m.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_按分类排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx, category: "B管");
        await SeedMaterialAsync(ctx, category: "A管");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "MaterialCategory", IsDescending = false });

        result.Items[0].MaterialCategory.Should().Be("A管");
        result.Items[1].MaterialCategory.Should().Be("B管");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx);
        var id = await ctx.Materials.Select(m => m.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result.MaterialCategory.Should().Be("钢管");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("物料不存在");
    }

    // ========== GetActiveAsync ==========

    [Fact]
    public async Task GetActiveAsync_仅返回激活物料()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx, category: "激活物料", isActive: true);
        await SeedMaterialAsync(ctx, category: "停用物料", isActive: false);
        var svc = CreateService(ctx);

        var result = await svc.GetActiveAsync();

        result.Should().HaveCount(1);
        result[0].MaterialCategory.Should().Be("激活物料");
    }

    // ========== GetCategoriesAsync ==========

    [Fact]
    public async Task GetCategoriesAsync_返回去重分类列表()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx, category: "钢管", isActive: true);
        await SeedMaterialAsync(ctx, category: "钢管", grade: "304", spec: "219*8", isActive: true);
        await SeedMaterialAsync(ctx, category: "不锈钢管", isActive: true);
        var svc = CreateService(ctx);

        var result = await svc.GetCategoriesAsync();

        result.Should().HaveCount(2);
        result.Should().Contain("钢管");
        result.Should().Contain("不锈钢管");
    }

    [Fact]
    public async Task GetCategoriesAsync_排除停用物料()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx, category: "停用分类", isActive: false);
        var svc = CreateService(ctx);

        var result = await svc.GetCategoriesAsync();

        result.Should().BeEmpty();
    }

    // ========== MatchAsync ==========

    [Fact]
    public async Task MatchAsync_找到匹配_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx, category: "钢管", grade: "20#", spec: "219*8");
        var svc = CreateService(ctx);

        var result = await svc.MatchAsync("钢管", "20#", "219*8");

        result.Should().NotBeNull();
        result!.MaterialCategory.Should().Be("钢管");
    }

    [Fact]
    public async Task MatchAsync_无匹配_返回Null()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.MatchAsync("钢管", "304", "219*8");

        result.Should().BeNull();
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建物料()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateMaterialRequest
        {
            MaterialCategory = "钢管",
            PlantGrade = "20#",
            Specification = "219*8",
            Remark = "测试"
        });

        result.Should().NotBeNull();
        result.MaterialCategory.Should().Be("钢管");
        result.PlantGrade.Should().Be("20#");
        result.Specification.Should().Be("219*8");

        var saved = await ctx.Materials.FirstAsync(m => !m.IsDeleted);
        saved.MaterialCategory.Should().Be("钢管");
    }

    [Fact]
    public async Task CreateAsync_重复组合_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx, category: "钢管", grade: "20#", spec: "219*8");
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateMaterialRequest
        {
            MaterialCategory = "钢管",
            PlantGrade = "20#",
            Specification = "219*8"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已存在*");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新物料()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx);
        var id = await ctx.Materials.Select(m => m.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateMaterialRequest
        {
            PlantGrade = "25#",
            Remark = "更新备注"
        });

        result.PlantGrade.Should().Be("25#");

        var saved = await ctx.Materials.FirstAsync(m => m.Id == id);
        saved.PlantGrade.Should().Be("25#");
        saved.Remark.Should().Be("更新备注");
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateMaterialRequest { MaterialCategory = "钢管" });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("物料不存在");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功软删除()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx);
        var id = await ctx.Materials.Select(m => m.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.Materials.FindAsync(id);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("物料不存在");
    }

    [Fact]
    public async Task DeleteAsync_有关联库存批次_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedMaterialAsync(ctx, category: "钢管", grade: "20#", spec: "219*8");
        var materialId = await ctx.Materials.Select(m => m.Id).FirstAsync();

        // 创建关联的库存批次
        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "BATCH001",
            InboundSource = "采购",
            SourceName = "测试供应商",
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
        var act = () => svc.DeleteAsync(materialId);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*关联的库存批次*");
    }
}
