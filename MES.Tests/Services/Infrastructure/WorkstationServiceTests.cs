using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Infrastructure;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 工位管理服务测试：分页查询、按编码查询、新增/更新、删除
/// </summary>
public class WorkstationServiceTests : TestBase
{
    private WorkstationService CreateService(AppDbContext ctx) => new(ctx);

    private async Task<Workstation> SeedWorkstationAsync(AppDbContext ctx,
        string code = "WS001", string sectionName = "酸洗", bool isActive = true)
    {
        var ws = new Workstation
        {
            Code = code,
            Name = "测试工位",
            SectionName = sectionName,
            ReportType = "PicklingInRecord",
            IsActive = isActive
        };
        ctx.Workstations.Add(ws);
        await ctx.SaveChangesAsync();
        return ws;
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
    public async Task GetPagedAsync_返回分页数据()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, "WS001");
        await SeedWorkstationAsync(ctx, "WS002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_按关键字搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, "WS001", "酸洗");
        await SeedWorkstationAsync(ctx, "WS002", "去油");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "酸洗" });

        result.Items.Should().HaveCount(1);
        result.Items[0].SectionName.Should().Be("酸洗");
    }

    [Fact]
    public async Task GetPagedAsync_默认排序为Code()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, "WS002");
        await SeedWorkstationAsync(ctx, "WS001");
        var svc = CreateService(ctx);

        // IsDescending 默认为 true，Code 降序
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items[0].Code.Should().Be("WS002");
        result.Items[1].Code.Should().Be("WS001");
    }

    // ========== GetByCodeAsync ==========

    [Fact]
    public async Task GetByCodeAsync_存在_返回工位()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("WS001");

        result.Should().NotBeNull();
        result!.Code.Should().Be("WS001");
    }

    [Fact]
    public async Task GetByCodeAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("NONEXISTENT");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByCodeAsync_未启用_返回Null()
    {
        var ctx = CreateDbContext();
        await SeedWorkstationAsync(ctx, isActive: false);
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("WS001");

        result.Should().BeNull();
    }

    // ========== SaveAsync ==========

    [Fact]
    public async Task SaveAsync_新增_成功创建()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Code = "WS001",
            Name = "酸洗工位",
            SectionName = "酸洗",
            ReportType = "PicklingInRecord"
        });

        result.Should().BeTrue();

        var saved = await ctx.Workstations.FirstAsync();
        saved.Code.Should().Be("WS001");
    }

    [Fact]
    public async Task SaveAsync_更新_成功修改()
    {
        var ctx = CreateDbContext();
        var ws = await SeedWorkstationAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new WorkstationDto
        {
            Id = ws.Id,
            Code = "WS001",
            Name = "更新名称",
            SectionName = "去油",
            ReportType = "PicklingInRecord",
            IsActive = true
        });

        result.Should().BeTrue();

        var updated = await ctx.Workstations.FindAsync(ws.Id);
        updated!.Name.Should().Be("更新名称");
        updated.SectionName.Should().Be("去油");
    }

    [Fact]
    public async Task SaveAsync_更新不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(new WorkstationDto
        {
            Id = 999,
            Code = "WS001",
            SectionName = "酸洗",
            ReportType = "PicklingInRecord"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var ws = await SeedWorkstationAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.DeleteAsync(ws.Id);

        result.Should().BeTrue();
        var deleted = await ctx.Workstations.FindAsync(ws.Id);
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
}
