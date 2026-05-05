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
/// 产品标准服务测试：CRUD、关键字搜索、激活筛选、排序
/// </summary>
public class ProductionStandardServiceTests : TestBase
{
    private ProductionStandardService CreateService(AppDbContext ctx) => new(ctx);

    private async Task SeedStandardAsync(AppDbContext ctx, string code = "GB/T 8163", string name = "流体管",
        int sortOrder = 1, bool isActive = true)
    {
        ctx.ProductionStandards.Add(new ProductionStandard
        {
            StandardCode = code,
            StandardName = name,
            SortOrder = sortOrder,
            IsActive = isActive
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
    public async Task GetPagedAsync_按关键字搜索编码_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, code: "GB/T 8163");
        await SeedStandardAsync(ctx, code: "GB/T 14976");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "8163" });

        result.Items.Should().HaveCount(1);
        result.Items[0].StandardCode.Should().Be("GB/T 8163");
    }

    [Fact]
    public async Task GetPagedAsync_按关键字搜索名称_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, name: "流体管");
        await SeedStandardAsync(ctx, name: "结构管");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "流体" });

        result.Items.Should().HaveCount(1);
        result.Items[0].StandardName.Should().Be("流体管");
    }

    [Fact]
    public async Task GetPagedAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_按IsActive筛选_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, code: "STD-ACTIVE", isActive: true);
        await SeedStandardAsync(ctx, code: "STD-INACTIVE", isActive: false);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 }, isActive: true);

        result.Items.Should().HaveCount(1);
        result.Items[0].StandardCode.Should().Be("STD-ACTIVE");
    }

    [Fact]
    public async Task GetPagedAsync_按编码排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, code: "B-STD");
        await SeedStandardAsync(ctx, code: "A-STD");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "StandardCode", IsDescending = false });

        result.Items[0].StandardCode.Should().Be("A-STD");
        result.Items[1].StandardCode.Should().Be("B-STD");
    }

    [Fact]
    public async Task GetPagedAsync_删除后不显示()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx);
        var id = await ctx.ProductionStandards.Select(s => s.Id).FirstAsync();
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
        await SeedStandardAsync(ctx, code: "ACTIVE", isActive: true);
        await SeedStandardAsync(ctx, code: "INACTIVE", isActive: false);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync();

        result.Should().HaveCount(1);
        result[0].StandardCode.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task GetAllAsync_onlyActiveFalse_返回全部()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, code: "ACTIVE", isActive: true);
        await SeedStandardAsync(ctx, code: "INACTIVE", isActive: false);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(onlyActive: false);

        result.Should().HaveCount(2);
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx);
        var id = await ctx.ProductionStandards.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result.StandardCode.Should().Be("GB/T 8163");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*does not exist*");
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建标准()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateProductionStandardRequest
        {
            StandardCode = "GB/T 14976",
            StandardName = "不锈钢管",
            SortOrder = 10,
            IsActive = true
        });

        result.Should().NotBeNull();
        result.StandardCode.Should().Be("GB/T 14976");
        result.StandardName.Should().Be("不锈钢管");
        result.IsActive.Should().BeTrue();

        var saved = await ctx.ProductionStandards.FirstAsync(s => s.StandardCode == "GB/T 14976");
        saved.StandardCode.Should().Be("GB/T 14976");
    }

    [Fact]
    public async Task CreateAsync_重复编码_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, code: "GB/T 8163");
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateProductionStandardRequest
        {
            StandardCode = "GB/T 8163",
            StandardName = "重复标准",
            SortOrder = 1
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*already exists*");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新标准()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx);
        var id = await ctx.ProductionStandards.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateProductionStandardRequest
        {
            StandardName = "新名称",
            IsActive = false
        });

        result.StandardName.Should().Be("新名称");
        result.IsActive.Should().BeFalse();

        var saved = await ctx.ProductionStandards.FirstAsync(s => s.Id == id);
        saved.StandardName.Should().Be("新名称");
        saved.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateProductionStandardRequest
        {
            StandardCode = "NEW",
            StandardName = "新标准"
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*does not exist*");
    }

    [Fact]
    public async Task UpdateAsync_重复编码_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, code: "STD-001");
        await SeedStandardAsync(ctx, code: "STD-002");
        var id = await ctx.ProductionStandards
            .Where(s => s.StandardCode == "STD-001")
            .Select(s => s.Id)
            .FirstAsync();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(id, new UpdateProductionStandardRequest
        {
            StandardCode = "STD-002"
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*already exists*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx);
        var id = await ctx.ProductionStandards.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.ProductionStandards.FindAsync(id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*does not exist*");
    }
}
