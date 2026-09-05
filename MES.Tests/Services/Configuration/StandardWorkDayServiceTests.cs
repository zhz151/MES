using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Configuration;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 标准工量天数服务测试：CRUD、缺失抛业务异常、GetStandardDaysMapAsync 牌号前缀精确匹配/通用回退/按 SectionKey 归并、
/// GetEnabledSectionsAsync 启用过滤/同名去重取通用行。
/// </summary>
public class StandardWorkDayServiceTests : TestBase
{
    private static StandardWorkDayService CreateService(AppDbContext ctx) => new(ctx);

    private static async Task<StandardWorkDay> SeedAsync(AppDbContext ctx, string sectionName, string? sectionKey,
        double days, string? prefix = null, bool enabled = true, int displayOrder = 1)
    {
        var e = new StandardWorkDay
        {
            SectionName = sectionName,
            SectionKey = sectionKey,
            StandardDays = days,
            PlantGradePrefix = prefix,
            IsEnabled = enabled,
            DisplayOrder = displayOrder
        };
        ctx.StandardWorkDays.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static StandardWorkDayDto NewDto(string sectionKey = "Cut") => new()
    {
        SectionName = "断切",
        SectionKey = sectionKey,
        DisplayOrder = 1,
        IsEnabled = true,
        StandardDays = 3.5,
        Remark = "备注"
    };

    // ========== Save / GetById / Delete ==========

    [Fact]
    public async Task SaveAsync_新增_落库可读()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.SaveAsync(NewDto())).Should().BeTrue();

        var row = await ctx.StandardWorkDays.SingleAsync();
        row.SectionKey.Should().Be("Cut");
        row.StandardDays.Should().Be(3.5);
    }

    [Fact]
    public async Task SaveAsync_更新缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.SaveAsync(new StandardWorkDayDto { Id = 99999 });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*标准工量天数配置不存在*");
    }

    [Fact]
    public async Task GetByIdAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.GetByIdAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*标准工量天数配置不存在*");
    }

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, "断切", "Cut", 3);
        var svc = CreateService(ctx);

        (await svc.DeleteAsync(e.Id)).Should().BeTrue();

        ctx.StandardWorkDays.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.DeleteAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*标准工量天数配置不存在*");
    }

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_关键字命中工段名或备注()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, "冷拔", "ColdRollDraw", 15, displayOrder: 1);
        await SeedAsync(ctx, "断切", "Cut", 3, displayOrder: 2);
        var svc = CreateService(ctx);

        var hit = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "冷拔" });
        hit.Items.Should().ContainSingle().Which.SectionKey.Should().Be("ColdRollDraw");
    }

    [Fact]
    public async Task GetPagedAsync_无数据_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var page = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        page.Items.Should().BeEmpty();
    }

    // ========== GetStandardDaysMapAsync ==========

    [Fact]
    public async Task GetStandardDaysMapAsync_前缀精确匹配_优于通用()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, "断切", "Cut", 8, prefix: "3", displayOrder: 1);
        await SeedAsync(ctx, "断切", "Cut", 22, prefix: null, displayOrder: 2);
        var svc = CreateService(ctx);

        var map3 = await svc.GetStandardDaysMapAsync("316L");
        map3["Cut"].Should().Be(8);     // 3 前缀命中

        var map1 = await svc.GetStandardDaysMapAsync("1Cr18Ni9");
        map1["Cut"].Should().Be(22);    // 无前缀命中 → 通用
    }

    [Fact]
    public async Task GetStandardDaysMapAsync_按SectionKey归并_忽略空键行()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, "断切", "Cut", 3, prefix: null);
        await SeedAsync(ctx, "冷拔", "ColdRollDraw", 15, prefix: null);
        await SeedAsync(ctx, "历史无键", null, 99); // SectionKey 为空 → 忽略
        var svc = CreateService(ctx);

        var map = await svc.GetStandardDaysMapAsync(null);

        map.Should().BeEquivalentTo(new Dictionary<string, double>
        {
            ["Cut"] = 3,
            ["ColdRollDraw"] = 15
        });
        map.Should().NotContainKey("");
    }

    // ========== GetEnabledSectionsAsync ==========

    [Fact]
    public async Task GetEnabledSectionsAsync_禁用过滤_同名取通用行()
    {
        var ctx = CreateDbContext();
        // 同 SectionKey 两行：通用(prefix=null, order 2) + 前缀覆盖行(order 1)，结果取通用行
        await SeedAsync(ctx, "断切", "Cut", 22, prefix: null, enabled: true, displayOrder: 2);
        await SeedAsync(ctx, "断切", "Cut", 8, prefix: "3", enabled: true, displayOrder: 1);
        // 禁用行不进结果
        await SeedAsync(ctx, "退火", "Annealing", 10, prefix: null, enabled: false, displayOrder: 3);
        var svc = CreateService(ctx);

        var rows = await svc.GetEnabledSectionsAsync();

        rows.Should().ContainSingle();
        var row = rows[0];
        row.SectionKey.Should().Be("Cut");
        row.DisplayOrder.Should().Be(2); // 取通用行（PlantGradePrefix=null）的显示顺序
    }

    [Fact]
    public async Task GetEnabledSectionsAsync_无启用行_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var rows = await svc.GetEnabledSectionsAsync();

        rows.Should().BeEmpty();
    }
}
