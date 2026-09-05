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
/// 子标准速览服务测试：CRUD、标准号唯一约束、缺失抛业务异常、null 补丁更新、分页关键字、筛选上下文去重。
/// </summary>
public class SubStandardQuickViewServiceTests : TestBase
{
    private static SubStandardQuickViewService CreateService(AppDbContext ctx) => new(ctx);

    private static async Task<SubStandardQuickView> SeedAsync(AppDbContext ctx, string standardNo, string? flaring = null)
    {
        var e = new SubStandardQuickView { StandardNo = standardNo, FlaringTest = flaring };
        ctx.SubStandardQuickViews.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static CreateSubStandardQuickViewRequest NewCreate(string standardNo = "GB/T 14976") => new()
    {
        StandardNo = standardNo,
        ChemicalComposition = "按标准",
        FlaringTest = "扩口不裂",
        HydrostaticTest = "试验压力 10MPa"
    };

    [Fact]
    public async Task CreateAsync_新增_落库返回Dto()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = await svc.CreateAsync(NewCreate("GB/T 14976-2012"));

        dto.Id.Should().BeGreaterThan(0);
        dto.StandardNo.Should().Be("GB/T 14976-2012");
        dto.FlaringTest.Should().Be("扩口不裂");
        ctx.SubStandardQuickViews.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_标准号重复_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await svc.CreateAsync(NewCreate("GB/T 14976"));

        var act = async () => await svc.CreateAsync(NewCreate("GB/T 14976"));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*GB/T 14976*已存在*");
    }

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, "GB/T 14976", "扩口不裂");

        var dto = await CreateService(ctx).GetByIdAsync(e.Id);

        dto.StandardNo.Should().Be("GB/T 14976");
        dto.FlaringTest.Should().Be("扩口不裂");
    }

    [Fact]
    public async Task GetByIdAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.GetByIdAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*子标准速览记录不存在*");
    }

    [Fact]
    public async Task UpdateAsync_部分字段更新_未提供字段保持原值()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, "GB/T 14976", "扩口不裂");
        var svc = CreateService(ctx);

        var dto = await svc.UpdateAsync(e.Id, new UpdateSubStandardQuickViewRequest
        {
            StandardNo = "GB/T 14976", // 键列非空语义：须随请求带回
            ChemicalComposition = "更新成分"
        });

        dto.ChemicalComposition.Should().Be("更新成分");
        dto.StandardNo.Should().Be("GB/T 14976");
        dto.FlaringTest.Should().Be("扩口不裂");
    }

    [Fact]
    public async Task UpdateAsync_重命名为他人标准号_抛业务异常()
    {
        var ctx = CreateDbContext();
        var a = await SeedAsync(ctx, "STD-A");
        await SeedAsync(ctx, "STD-B");
        var svc = CreateService(ctx);

        var act = async () => await svc.UpdateAsync(a.Id, new UpdateSubStandardQuickViewRequest
        {
            StandardNo = "STD-B"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*STD-B*已存在*");
    }

    [Fact]
    public async Task UpdateAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.UpdateAsync(99999, new UpdateSubStandardQuickViewRequest());

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*子标准速览记录不存在*");
    }

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, "GB/T 14976");
        var svc = CreateService(ctx);

        await svc.DeleteAsync(e.Id);

        ctx.SubStandardQuickViews.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.DeleteAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*子标准速览记录不存在*");
    }

    // ========== GetPagedAsync / GetFilterContextsAsync ==========

    [Fact]
    public async Task GetPagedAsync_关键字命中标准号()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, "GB/T 14976");
        await SeedAsync(ctx, "GB/T 21833");
        var svc = CreateService(ctx);

        var page = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "14976" });

        page.Items.Should().ContainSingle().Which.StandardNo.Should().Be("GB/T 14976");
    }

    [Fact]
    public async Task GetPagedAsync_无数据_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var page = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetFilterContextsAsync_返回去重非空上下文()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, "STD-A", "扩口不裂");
        await SeedAsync(ctx, "STD-B", "扩口不裂");
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["StandardNo"].Should().Equal("STD-A", "STD-B");
        contexts["FlaringTest"].Should().Equal("扩口不裂"); // 重复去重
        contexts.Should().ContainKey("ChemicalComposition");
    }
}
