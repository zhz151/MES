using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services.Configuration;

/// <summary>
/// 工段显示名解析服务测试：通用行优先、兜底 26 键、缓存、双向转换。
/// </summary>
public class SectionNameDisplayServiceTests : TestBase
{
    private SectionNameDisplayService CreateService(AppDbContext ctx)
        => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    // ========== GetSectionNameMapAsync ==========

    [Fact]
    public async Task GetSectionNameMapAsync_无配置行_兜底SectionDefs26键()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var map = await svc.GetSectionNameMapAsync();

        map.Should().HaveCount(26);
        foreach (var key in SectionKeys.All)
        {
            map.Should().ContainKey(key);
            map[key].Should().Be(SectionKeys.ToChinese(key));
        }
    }

    [Fact]
    public async Task GetSectionNameMapAsync_配置覆盖兜底()
    {
        var ctx = CreateDbContext();
        ctx.StandardWorkDays.Add(new StandardWorkDay
        {
            SectionName = "成品切割",
            SectionKey = SectionKeys.Cut,
            DisplayOrder = 1,
            IsEnabled = true,
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetSectionNameMapAsync();

        map[SectionKeys.Cut].Should().Be("成品切割"); // 配置优先
        map[SectionKeys.Pickle].Should().Be(SectionDefs.Pickle); // 未配置兜底
    }

    [Fact]
    public async Task GetSectionNameMapAsync_同Key多行_通用行优先()
    {
        var ctx = CreateDbContext();
        ctx.StandardWorkDays.Add(new StandardWorkDay
        {
            SectionName = "通用断切",
            SectionKey = SectionKeys.Cut,
            DisplayOrder = 1,
            IsEnabled = true,
            PlantGradePrefix = null, // 通用行
        });
        ctx.StandardWorkDays.Add(new StandardWorkDay
        {
            SectionName = "3号断切",
            SectionKey = SectionKeys.Cut,
            DisplayOrder = 2,
            IsEnabled = true,
            PlantGradePrefix = "3", // 专用行
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetSectionNameMapAsync();

        map[SectionKeys.Cut].Should().Be("通用断切");
    }

    [Fact]
    public async Task GetSectionNameMapAsync_同Key多行_仅专用行时取专用行()
    {
        var ctx = CreateDbContext();
        ctx.StandardWorkDays.Add(new StandardWorkDay
        {
            SectionName = "3号断切",
            SectionKey = SectionKeys.Cut,
            DisplayOrder = 2,
            IsEnabled = true,
            PlantGradePrefix = "3",
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetSectionNameMapAsync();

        map[SectionKeys.Cut].Should().Be("3号断切");
    }

    [Fact]
    public async Task GetSectionNameMapAsync_SectionKey大小写不敏感_同组归一()
    {
        var ctx = CreateDbContext();
        ctx.StandardWorkDays.Add(new StandardWorkDay
        {
            SectionName = "小写key",
            SectionKey = "cut", // 小写
            DisplayOrder = 1,
            IsEnabled = true,
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetSectionNameMapAsync();

        map[SectionKeys.Cut].Should().Be("小写key");
    }

    [Fact]
    public async Task GetSectionNameMapAsync_SectionName为空_跳过该行()
    {
        var ctx = CreateDbContext();
        ctx.StandardWorkDays.Add(new StandardWorkDay
        {
            SectionName = "", // 空显示名
            SectionKey = SectionKeys.Cut,
            DisplayOrder = 1,
            IsEnabled = true,
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetSectionNameMapAsync();

        // 空显示名不覆盖兜底
        map[SectionKeys.Cut].Should().Be(SectionDefs.Cut);
    }

    [Fact]
    public async Task GetSectionNameMapAsync_英文种子值_回退规范中文()
    {
        var ctx = CreateDbContext();
        ctx.StandardWorkDays.Add(new StandardWorkDay
        {
            SectionName = nameof(SectionDefs.ColdRollDraw), // 种子 SectionName 存英文 Key（如 "ColdRollDraw"）
            SectionKey = SectionKeys.ColdRollDraw,
            DisplayOrder = 1,
            IsEnabled = true,
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetSectionNameMapAsync();

        map[SectionKeys.ColdRollDraw].Should().Be(SectionDefs.ColdRollDraw); // 英文种子值回退规范中文
    }

    // ========== 缓存 ==========

    [Fact]
    public async Task GetSectionNameMapAsync_5分钟内缓存命中()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var map1 = await svc.GetSectionNameMapAsync();
        map1[SectionKeys.Cut].Should().Be(SectionDefs.Cut);

        // 修改数据库后，同一 service（同一缓存实例）仍返回缓存旧值
        ctx.StandardWorkDays.Add(new StandardWorkDay
        {
            SectionName = "改了",
            SectionKey = SectionKeys.Cut,
            DisplayOrder = 1,
            IsEnabled = true,
        });
        await ctx.SaveChangesAsync();

        var map2 = await svc.GetSectionNameMapAsync();
        map2[SectionKeys.Cut].Should().Be(SectionDefs.Cut, "5 分钟 TTL 内应命中缓存");
    }

    // ========== ToDisplayAsync ==========

    [Fact]
    public async Task ToDisplayAsync_Key转配置中文()
    {
        var ctx = CreateDbContext();
        ctx.StandardWorkDays.Add(new StandardWorkDay
        {
            SectionName = "成品切割",
            SectionKey = SectionKeys.Cut,
            DisplayOrder = 1,
            IsEnabled = true,
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        (await svc.ToDisplayAsync(SectionKeys.Cut)).Should().Be("成品切割");
    }

    [Fact]
    public async Task ToDisplayAsync_Key无配置_兜底规范中文()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.ToDisplayAsync(SectionKeys.Cut)).Should().Be(SectionDefs.Cut);
    }

    [Fact]
    public async Task ToDisplayAsync_未知Key_按原样兜底()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 未知 Key 不是 IsKey → 原样返回
        (await svc.ToDisplayAsync("UnknownKey")).Should().Be("UnknownKey");
    }

    [Fact]
    public async Task ToDisplayAsync_中文原样返回()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.ToDisplayAsync("断切")).Should().Be("断切");
        (await svc.ToDisplayAsync("切管")).Should().Be("切管"); // 别名原样
    }

    [Fact]
    public async Task ToDisplayAsync_null或空返回null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.ToDisplayAsync(null)).Should().BeNull();
        (await svc.ToDisplayAsync("")).Should().BeNull();
    }

    // ========== ToKeyAsync ==========

    [Fact]
    public async Task ToKeyAsync_中文转Key()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.ToKeyAsync("断切")).Should().Be(SectionKeys.Cut);
        (await svc.ToKeyAsync("切管")).Should().Be(SectionKeys.OilPipeCut); // 别名
        (await svc.ToKeyAsync(SectionKeys.Cut)).Should().Be(SectionKeys.Cut); // Key 幂等
        (await svc.ToKeyAsync("不存在的工段")).Should().BeNull();
    }
}
