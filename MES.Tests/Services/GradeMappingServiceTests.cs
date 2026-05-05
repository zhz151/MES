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
/// 牌号对照服务测试：CRUD、关键字搜索、排序
/// </summary>
public class GradeMappingServiceTests : TestBase
{
    private GradeMappingService CreateService(AppDbContext ctx) => new(ctx);

    private async Task SeedMappingAsync(AppDbContext ctx, string standardGrade = "Q345B",
        string plantGrade = "Q345B", decimal density = 7.85m, bool specialMaterial = false)
    {
        ctx.StandardGradeMappings.Add(new StandardGradeMapping
        {
            StandardGrade = standardGrade,
            PlantGrade = plantGrade,
            Density = density,
            SpecialMaterial = specialMaterial
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
    public async Task GetPagedAsync_按标准牌号搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedMappingAsync(ctx, standardGrade: "A-Q345B", plantGrade: "X-Q345B");
        await SeedMappingAsync(ctx, standardGrade: "20#", plantGrade: "20#G");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "Q345" });

        result.Items.Should().HaveCount(1);
        result.Items[0].StandardGrade.Should().Be("A-Q345B");
    }

    [Fact]
    public async Task GetPagedAsync_按工厂牌号搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedMappingAsync(ctx, standardGrade: "S-GRADE-1", plantGrade: "304");
        await SeedMappingAsync(ctx, standardGrade: "S-GRADE-2", plantGrade: "316L");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "304" });

        result.Items.Should().HaveCount(1);
        result.Items[0].PlantGrade.Should().Be("304");
    }

    [Fact]
    public async Task GetPagedAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedMappingAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_按标准牌号排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedMappingAsync(ctx, standardGrade: "B-Grade");
        await SeedMappingAsync(ctx, standardGrade: "A-Grade");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "StandardGrade", IsDescending = false });

        result.Items[0].StandardGrade.Should().Be("A-Grade");
        result.Items[1].StandardGrade.Should().Be("B-Grade");
    }

    [Fact]
    public async Task GetPagedAsync_删除后不显示()
    {
        var ctx = CreateDbContext();
        await SeedMappingAsync(ctx);
        var id = await ctx.StandardGradeMappings.Select(g => g.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });
        result.Items.Should().BeEmpty();
    }

    // ========== GetAllAsync ==========

    [Fact]
    public async Task GetAllAsync_返回所有非删除记录()
    {
        var ctx = CreateDbContext();
        await SeedMappingAsync(ctx, standardGrade: "A");
        await SeedMappingAsync(ctx, standardGrade: "B");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync();

        result.Should().HaveCount(2);
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedMappingAsync(ctx);
        var id = await ctx.StandardGradeMappings.Select(g => g.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result.StandardGrade.Should().Be("Q345B");
        result.Density.Should().Be(7.85m);
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
    public async Task CreateAsync_成功创建牌号对照()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateGradeMappingRequest
        {
            StandardGrade = "20#",
            PlantGrade = "20#G",
            Density = 7.85m,
            HeatTreatment = "正火",
            SpecialMaterial = false
        });

        result.Should().NotBeNull();
        result.StandardGrade.Should().Be("20#");
        result.PlantGrade.Should().Be("20#G");
        result.Density.Should().Be(7.85m);

        var saved = await ctx.StandardGradeMappings.FirstAsync(g => g.StandardGrade == "20#");
        saved.StandardGrade.Should().Be("20#");
    }

    [Fact]
    public async Task CreateAsync_重复标准牌号_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedMappingAsync(ctx, standardGrade: "Q345B");
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateGradeMappingRequest
        {
            StandardGrade = "Q345B",
            PlantGrade = "Q345C",
            Density = 7.85m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*already exists*");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新牌号对照()
    {
        var ctx = CreateDbContext();
        await SeedMappingAsync(ctx);
        var id = await ctx.StandardGradeMappings.Select(g => g.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateGradeMappingRequest
        {
            StandardGrade = "Q345B",
            PlantGrade = "Q345C",
            Density = 7.93m,
            HeatTreatment = "调质"
        });

        result.PlantGrade.Should().Be("Q345C");
        result.Density.Should().Be(7.93m);

        var saved = await ctx.StandardGradeMappings.FirstAsync(g => g.Id == id);
        saved.PlantGrade.Should().Be("Q345C");
        saved.Density.Should().Be(7.93m);
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateGradeMappingRequest
        {
            StandardGrade = "NEW",
            PlantGrade = "NEW",
            Density = 7.85m
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*does not exist*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedMappingAsync(ctx);
        var id = await ctx.StandardGradeMappings.Select(g => g.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.StandardGradeMappings.FindAsync(id);
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
