using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MES.Core.DTOs.Configuration;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 业务参数配置服务测试：CRUD、缺失抛业务异常、关键字/分页、GetConfigMapAsync 分类映射（忽略大小写键）、
/// 筛选上下文去重、保存非敏感类目不触发读模型刷新。
/// </summary>
public class ConfigParameterServiceTests : TestBase
{
    private static ConfigParameterService CreateService(AppDbContext ctx)
        => new(ctx, new Mock<IServiceScopeFactory>().Object);

    private static async Task<ConfigParameter> SeedAsync(AppDbContext ctx, string category = "TestCategory",
        string paramKey = "Ratio", decimal paramValue = 0.5m, string? categoryDisplay = null,
        string? remark = null, string? context = null)
    {
        var e = new ConfigParameter
        {
            Category = category,
            ParamKey = paramKey,
            ParamValue = paramValue,
            CategoryDisplay = categoryDisplay,
            Remark = remark,
            Context = context
        };
        ctx.ConfigParameters.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static ConfigParameterDto NewDto(string category = "TestCategory", string paramKey = "Ratio")
        => new()
        {
            Category = category,
            CategoryDisplay = "测试-比例",
            Context = "工单",
            ParamKey = paramKey,
            ParamValue = 0.6m,
            Remark = "说明"
        };

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx);
        var svc = CreateService(ctx);

        var dto = await svc.GetByIdAsync(e.Id);

        dto!.Category.Should().Be("TestCategory");
        dto.ParamKey.Should().Be("Ratio");
        dto.ParamValue.Should().Be(0.5m);
    }

    [Fact]
    public async Task GetByIdAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.GetByIdAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*参数配置不存在*");
    }

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_关键字命中分类或备注()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, category: "WarehouseThreshold", paramKey: "CompleteRatio", remark: "仓库完工阈值");
        await SeedAsync(ctx, category: "OrderDays", paramKey: "Cycle", remark: "工单天数");
        var svc = CreateService(ctx);

        var byCategory = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "Warehouse" });
        byCategory.Items.Should().ContainSingle().Which.ParamKey.Should().Be("CompleteRatio");

        var byDisplay = await SeedAndGetDisplayHitAsync(ctx, svc);

        byDisplay.Items.Should().ContainSingle();
        byDisplay.Items[0].ParamKey.Should().Be("K1");
    }

    private static async Task<PagedResult<ConfigParameterDto>> SeedAndGetDisplayHitAsync(AppDbContext ctx,
        ConfigParameterService svc)
    {
        await SeedAsync(ctx, category: "X1", paramKey: "K1",
            categoryDisplay: "库存-下限阈值", remark: null);
        return await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "下限阈值" });
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

    // ========== SaveAsync ==========

    [Fact]
    public async Task SaveAsync_新增_落库可读()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.SaveAsync(NewDto())).Should().BeTrue();

        var row = await ctx.ConfigParameters.SingleAsync();
        row.Category.Should().Be("TestCategory");
        row.ParamValue.Should().Be(0.6m);
    }

    [Fact]
    public async Task SaveAsync_更新_修改字段()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx);
        var svc = CreateService(ctx);
        var dto = new ConfigParameterDto
        {
            Id = e.Id,
            Category = e.Category,
            ParamKey = e.ParamKey,
            ParamValue = 0.9m,
            Remark = "更新备注"
        };

        (await svc.SaveAsync(dto)).Should().BeTrue();

        var row = await ctx.ConfigParameters.SingleAsync();
        row.ParamValue.Should().Be(0.9m);
        row.Remark.Should().Be("更新备注");
    }

    [Fact]
    public async Task SaveAsync_更新缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.SaveAsync(new ConfigParameterDto { Id = 99999, Category = "X", ParamKey = "Y" });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*参数配置不存在*");
    }

    [Fact]
    public async Task SaveAsync_敏感类目容差_保存不抛()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.SaveAsync(new ConfigParameterDto
        {
            Category = "MaterialPlanTolerance",
            ParamKey = "InputConsistencyTolerance",
            ParamValue = 0.5m
        });

        await act.Should().NotThrowAsync();
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx);
        var svc = CreateService(ctx);

        (await svc.DeleteAsync(e.Id)).Should().BeTrue();

        ctx.ConfigParameters.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.DeleteAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*参数配置不存在*");
    }

    // ========== GetConfigMapAsync / GetFilterContextsAsync ==========

    [Fact]
    public async Task GetConfigMapAsync_按分类过滤_忽略大小写键()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, category: "WH", paramKey: "A", paramValue: 10m);
        await SeedAsync(ctx, category: "WH", paramKey: "B", paramValue: 20m);
        await SeedAsync(ctx, category: "OTHER", paramKey: "A", paramValue: 999m);
        var svc = CreateService(ctx);

        var map = await svc.GetConfigMapAsync("WH");

        map.Should().BeEquivalentTo(new Dictionary<string, decimal> { ["A"] = 10m, ["B"] = 20m });
        map.Should().ContainKey("a"); // 字典键 OrdinalIgnoreCase
        map.Should().NotContainKey("A0");
    }

    [Fact]
    public async Task GetFilterContextsAsync_去重排除空()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, paramKey: "A", categoryDisplay: "库存", context: "仓库");
        await SeedAsync(ctx, paramKey: "B", categoryDisplay: "库存", context: null);
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["ParamKey"].Should().Equal("A", "B");
        contexts["CategoryDisplay"].Should().Equal("库存");
        contexts["Context"].Should().Equal("仓库");
    }
}
