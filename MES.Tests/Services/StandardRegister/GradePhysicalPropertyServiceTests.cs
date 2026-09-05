using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.StandardRegister;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.StandardRegister;
using MES.Services.StandardRegister;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 牌号物理性能服务测试：CRUD、(牌号+牌号类别) 组合唯一、缺失抛业务异常、null 补丁更新、分页关键字、筛选上下文。
/// </summary>
public class GradePhysicalPropertyServiceTests : TestBase
{
    private static GradePhysicalPropertyService CreateService(AppDbContext ctx) => new(ctx);

    private static async Task<GradePhysicalProperty> SeedAsync(AppDbContext ctx, string grade,
        string? category = null, decimal density = 0m)
    {
        var e = new GradePhysicalProperty
        {
            StandardGrade = grade,
            StandardGradeCategory = category,
            Density = density
        };
        ctx.GradePhysicalProperties.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static CreateGradePhysicalPropertyRequest NewCreate(string grade = "304", string? category = null) => new()
    {
        StandardGrade = grade,
        StandardGradeCategory = category,
        Density = 7.93m,
        TensileStrength = "≥515"
    };

    [Fact]
    public async Task CreateAsync_新增_落库返回Dto()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = await svc.CreateAsync(NewCreate("304", "奥氏体"));

        dto.Id.Should().BeGreaterThan(0);
        dto.StandardGrade.Should().Be("304");
        dto.StandardGradeCategory.Should().Be("奥氏体");
        dto.Density.Should().Be(7.93m);
    }

    [Fact]
    public async Task CreateAsync_同牌号同类别重复_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await svc.CreateAsync(NewCreate("304", "奥氏体"));

        var act = async () => await svc.CreateAsync(NewCreate("304", "奥氏体"));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*304*已存在*");
    }

    [Fact]
    public async Task CreateAsync_同牌号不同类别_允许()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await svc.CreateAsync(NewCreate("304", "奥氏体"));

        var dto = await svc.CreateAsync(NewCreate("304", "铁素体"));

        dto.Id.Should().BeGreaterThan(0);
        ctx.GradePhysicalProperties.Count().Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, "316L", "奥氏体", 7.98m);
        var dto = await CreateService(ctx).GetByIdAsync(e.Id);

        dto.StandardGrade.Should().Be("316L");
        dto.Density.Should().Be(7.98m);
    }

    [Fact]
    public async Task GetByIdAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.GetByIdAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*牌号物理性能不存在*");
    }

    [Fact]
    public async Task UpdateAsync_部分字段更新_键保持原值()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, "304", "奥氏体", 7.93m);
        var svc = CreateService(ctx);

        var dto = await svc.UpdateAsync(e.Id, new UpdateGradePhysicalPropertyRequest
        {
            StandardGrade = "304",
            StandardGradeCategory = "奥氏体",
            Density = 7.99m
        });

        dto.Density.Should().Be(7.99m);
        dto.StandardGrade.Should().Be("304");
        dto.StandardGradeCategory.Should().Be("奥氏体");
    }

    [Fact]
    public async Task UpdateAsync_改成他人组合_抛业务异常()
    {
        var ctx = CreateDbContext();
        var a = await SeedAsync(ctx, "304", "奥氏体");
        await SeedAsync(ctx, "304", "铁素体");
        var svc = CreateService(ctx);

        var act = async () => await svc.UpdateAsync(a.Id, new UpdateGradePhysicalPropertyRequest
        {
            StandardGrade = "304",
            StandardGradeCategory = "铁素体"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*304*已存在*");
    }

    [Fact]
    public async Task UpdateAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.UpdateAsync(99999, new UpdateGradePhysicalPropertyRequest
        {
            StandardGrade = "304"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*牌号物理性能不存在*");
    }

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, "304");
        var svc = CreateService(ctx);

        await svc.DeleteAsync(e.Id);

        ctx.GradePhysicalProperties.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.DeleteAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*牌号物理性能不存在*");
    }

    // ========== GetPagedAsync / GetAllAsync / GetFilterContextsAsync ==========

    [Fact]
    public async Task GetPagedAsync_关键字命中牌号或类别()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, "304", "奥氏体");
        await SeedAsync(ctx, "316L", "奥氏体");
        var svc = CreateService(ctx);

        var byGrade = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "316" });
        byGrade.Items.Should().ContainSingle().Which.StandardGrade.Should().Be("316L");

        var byCategory = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "奥氏" });
        byCategory.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_按牌号排序返回()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, "316L");
        await SeedAsync(ctx, "304");
        var svc = CreateService(ctx);

        var all = await svc.GetAllAsync();

        all.Select(g => g.StandardGrade).Should().Equal("304", "316L");
    }

    [Fact]
    public async Task GetFilterContextsAsync_返回去重上下文()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, "304", "奥氏体", 7.93m);
        await SeedAsync(ctx, "316L", "奥氏体", 7.98m);
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["StandardGrade"].Should().Equal("304", "316L");
        contexts["StandardGradeCategory"].Should().Equal("奥氏体");
        contexts.Should().ContainKey("Density");
    }
}
