using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.ProductionStandard;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 牌号化学成分服务测试：CRUD、关键字搜索、排序、重复检查、导入
/// </summary>
public class ChemicalCompositionServiceTests : TestBase
{
    private ChemicalCompositionService CreateService(AppDbContext ctx)
        => new(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<ChemicalCompositionService>.Instance);

    private async Task SeedChemicalAsync(AppDbContext ctx, string plantGrade = "Q345B",
        string? carbon = "0.12", string? silicon = "0.30")
    {
        ctx.ChemicalCompositions.Add(new ChemicalComposition
        {
            PlantGrade = plantGrade,
            Carbon = carbon,
            Silicon = silicon
        });
        await ctx.SaveChangesAsync();
    }

    // ========== GetAllAsync ==========

    [Fact]
    public async Task GetAllAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_按牌号搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx, plantGrade: "Q345B");
        await SeedChemicalAsync(ctx, plantGrade: "20#");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "Q345" });

        result.Items.Should().HaveCount(1);
        result.Items[0].PlantGrade.Should().Be("Q345B");
    }

    [Fact]
    public async Task GetAllAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx, plantGrade: "B-Grade");
        await SeedChemicalAsync(ctx, plantGrade: "A-Grade");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "plantgrade", IsDescending = false });

        result.Items[0].PlantGrade.Should().Be("A-Grade");
        result.Items[1].PlantGrade.Should().Be("B-Grade");
    }

    [Fact]
    public async Task GetAllAsync_删除后不显示()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx);
        var id = await ctx.ChemicalCompositions.Select(c => c.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20 });
        result.Items.Should().BeEmpty();
    }

    // ========== BatchCreateAsync ==========

    [Fact]
    public async Task BatchCreateAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateChemicalCompositionRequest>
        {
            new() { PlantGrade = "Q345B", Carbon = "0.12" },
            new() { PlantGrade = "20#", Carbon = "0.20" }
        });

        result.Should().HaveCount(2);
        result[0].PlantGrade.Should().Be("Q345B");
        result[1].PlantGrade.Should().Be("20#");

        var saved = await ctx.ChemicalCompositions.CountAsync();
        saved.Should().Be(2);
    }

    [Fact]
    public async Task BatchCreateAsync_空列表_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateChemicalCompositionRequest>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchCreateAsync_重复牌号_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx, plantGrade: "Q345B");
        var svc = CreateService(ctx);

        var act = () => svc.BatchCreateAsync(new List<CreateChemicalCompositionRequest>
        {
            new() { PlantGrade = "Q345B" }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已存在*");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx);
        var id = await ctx.ChemicalCompositions.Select(c => c.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateChemicalCompositionRequest
        {
            PlantGrade = "Q355B",
            Carbon = "0.15",
            Silicon = "0.35"
        });

        result.PlantGrade.Should().Be("Q355B");
        result.Carbon.Should().Be("0.15");

        var saved = await ctx.ChemicalCompositions.FirstAsync(c => c.Id == id);
        saved.PlantGrade.Should().Be("Q355B");
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateChemicalCompositionRequest { PlantGrade = "New" });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx);
        var id = await ctx.ChemicalCompositions.Select(c => c.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.ChemicalCompositions.FindAsync(id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetAllAsync_关键词搜索碳含量_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx, plantGrade: "Q345B", carbon: "0.12");
        await SeedChemicalAsync(ctx, plantGrade: "Q235B", carbon: "0.20");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "0.12" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Carbon.Should().Be("0.12");
    }

    // ========== 筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetAllAsync_Filters_PlantGradeContains_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx, plantGrade: "Q345B");
        await SeedChemicalAsync(ctx, plantGrade: "20#");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlantGrade", Operator = "contains", Value = "Q345" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].PlantGrade.Should().Be("Q345B");
    }

    [Fact]
    public async Task GetAllAsync_Filters_PlantGradeIn_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx, plantGrade: "Q345B");
        await SeedChemicalAsync(ctx, plantGrade: "20#");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlantGrade", Operator = "in", Values = new List<string> { "20#" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].PlantGrade.Should().Be("20#");
    }

    [Fact]
    public async Task GetAllAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx, plantGrade: "Q345B");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlantGrade", Operator = "contains", Value = "NONEXISTENT" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        await SeedChemicalAsync(ctx, plantGrade: "Q345B", carbon: "0.12");
        await SeedChemicalAsync(ctx, plantGrade: "20#", carbon: "0.20");
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("PlantGrade");
        contexts["PlantGrade"].Should().BeEquivalentTo(new[] { "20#", "Q345B" }, opts => opts.WithStrictOrdering());
        contexts.Should().ContainKey("Carbon");
        contexts["Carbon"].Should().BeEquivalentTo(new[] { "0.12", "0.20" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["PlantGrade"].Should().BeEmpty();
        contexts["Carbon"].Should().BeEmpty();
        contexts["Silicon"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        ctx.ChemicalCompositions.Add(new ChemicalComposition
        {
            PlantGrade = "Q345B",
            Carbon = null,
            Silicon = null
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["PlantGrade"].Should().HaveCount(1);
        contexts["Carbon"].Should().BeEmpty();
        contexts["Silicon"].Should().BeEmpty();
    }
}
