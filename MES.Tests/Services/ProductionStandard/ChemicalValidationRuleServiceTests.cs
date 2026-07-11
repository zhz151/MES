using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.ProductionStandard;
using MES.Tests.Tests;
using MES.Data.Entities.ProductionStandard;
using MES.Core.DTOs.ProductionStandard;

namespace MES.Tests.Services;

/// <summary>
/// 牌号验证服务测试：CRUD、关键字搜索、排序、GetByPlantGrade
/// </summary>
public class ChemicalValidationRuleServiceTests : TestBase
{
    private ChemicalValidationRuleService CreateService(AppDbContext ctx)
        => new(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<ChemicalValidationRuleService>.Instance);

    private async Task SeedRuleAsync(AppDbContext ctx, string plantGrade = "Q345B",
        string? cMin = "0.10", string? cMax = "0.15")
    {
        ctx.ChemicalValidationRules.Add(new ChemicalValidationRule
        {
            PlantGrade = plantGrade,
            CMin = cMin,
            CMax = cMax
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
        await SeedRuleAsync(ctx, plantGrade: "Q345B");
        await SeedRuleAsync(ctx, plantGrade: "20#");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "Q345" });

        result.Items.Should().HaveCount(1);
        result.Items[0].PlantGrade.Should().Be("Q345B");
    }

    [Fact]
    public async Task GetAllAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedRuleAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedRuleAsync(ctx, plantGrade: "B-Grade");
        await SeedRuleAsync(ctx, plantGrade: "A-Grade");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "plantgrade", IsDescending = false });

        result.Items[0].PlantGrade.Should().Be("A-Grade");
    }

    // ========== BatchCreateAsync ==========

    [Fact]
    public async Task BatchCreateAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateChemicalValidationRuleRequest>
        {
            new() { PlantGrade = "Q345B", CMin = "0.10", CMax = "0.15" },
            new() { PlantGrade = "20#", CMin = "0.18", CMax = "0.22" }
        });

        result.Should().HaveCount(2);
        result[0].PlantGrade.Should().Be("Q345B");
        result[0].CMin.Should().Be("0.10");
    }

    [Fact]
    public async Task BatchCreateAsync_空列表_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateChemicalValidationRuleRequest>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchCreateAsync_重复牌号_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedRuleAsync(ctx, plantGrade: "Q345B");
        var svc = CreateService(ctx);

        var act = () => svc.BatchCreateAsync(new List<CreateChemicalValidationRuleRequest>
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
        await SeedRuleAsync(ctx);
        var id = await ctx.ChemicalValidationRules.Select(r => r.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateChemicalValidationRuleRequest
        {
            PlantGrade = "Q355B",
            CMin = "0.12",
            CMax = "0.18"
        });

        result.PlantGrade.Should().Be("Q355B");
        result.CMin.Should().Be("0.12");
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateChemicalValidationRuleRequest { PlantGrade = "New" });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedRuleAsync(ctx);
        var id = await ctx.ChemicalValidationRules.Select(r => r.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.ChemicalValidationRules.FindAsync(id);
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

    // ========== GetByPlantGradeAsync ==========

    [Fact]
    public async Task GetByPlantGradeAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedRuleAsync(ctx, plantGrade: "Q345B");
        var svc = CreateService(ctx);

        var result = await svc.GetByPlantGradeAsync("Q345B");

        result.Should().NotBeNull();
        result!.PlantGrade.Should().Be("Q345B");
    }

    [Fact]
    public async Task GetByPlantGradeAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetByPlantGradeAsync("NONEXISTENT");

        result.Should().BeNull();
    }

    // ========== 筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetAllAsync_Filters_PlantGradeContains_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedRuleAsync(ctx, plantGrade: "Q345B");
        await SeedRuleAsync(ctx, plantGrade: "20#");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlantGrade", Operator = "contains", Value = "Q345" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].PlantGrade.Should().Be("Q345B");
    }

    [Fact]
    public async Task GetAllAsync_Filters_CMinIn_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedRuleAsync(ctx, plantGrade: "Q345B", cMin: "0.10");
        await SeedRuleAsync(ctx, plantGrade: "20#", cMin: "0.18");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "CMin", Operator = "in", Values = new List<string> { "0.18" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].PlantGrade.Should().Be("20#");
    }

    [Fact]
    public async Task GetAllAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedRuleAsync(ctx, plantGrade: "Q345B");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
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
        await SeedRuleAsync(ctx, plantGrade: "Q345B", cMin: "0.10", cMax: "0.15");
        await SeedRuleAsync(ctx, plantGrade: "20#", cMin: "0.18", cMax: "0.22");
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("PlantGrade");
        contexts["PlantGrade"].Should().BeEquivalentTo(new[] { "20#", "Q345B" }, opts => opts.WithStrictOrdering());
        contexts.Should().ContainKey("CMin");
        contexts["CMin"].Should().BeEquivalentTo(new[] { "0.10", "0.18" }, opts => opts.WithStrictOrdering());
        contexts["CMax"].Should().BeEquivalentTo(new[] { "0.15", "0.22" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["PlantGrade"].Should().BeEmpty();
        contexts["CMin"].Should().BeEmpty();
        contexts["CMax"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        ctx.ChemicalValidationRules.Add(new ChemicalValidationRule
        {
            PlantGrade = "Q345B",
            CMin = null,
            CMax = null
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["PlantGrade"].Should().HaveCount(1);
        contexts["CMin"].Should().BeEmpty();
        contexts["CMax"].Should().BeEmpty();
    }
}
