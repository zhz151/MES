using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services.Configuration;

/// <summary>
/// 枚举显示配置服务测试：显示映射（配置优先/静态兜底）、显示选项排序、恢复默认、唯一性校验。
/// </summary>
public class EnumDisplayDefinitionServiceTests : TestBase, IDisposable
{
    public void Dispose()
    {
        // 清理本类测试注入的进程内静态覆盖，避免污染其他测试类
        EnumHelper.ClearEnumOverrides();
    }

    private EnumDisplayDefinitionService CreateService(AppDbContext ctx)
        => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private static EnumDisplayDefinition Row(string enumKey, string value, string display, int order)
        => new() { EnumKey = enumKey, Value = value, DisplayName = display, DisplayOrder = order };

    // ========== GetDisplayMapAsync ==========

    [Fact]
    public async Task GetDisplayMapAsync_空表_全量静态兜底()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var map = await svc.GetDisplayMapAsync();

        map.Should().ContainKey("BatchStatus");
        map["BatchStatus"]["None"].Should().Be("未产");
        map["BatchStatus"]["Suspended"].Should().Be("暂停");
    }

    [Fact]
    public async Task GetDisplayMapAsync_配置优先_静态兜底()
    {
        var ctx = CreateDbContext();
        ctx.EnumDisplayDefinitions.Add(Row("BatchStatus", "None", "未投产", 1));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetDisplayMapAsync();

        map["BatchStatus"]["None"].Should().Be("未投产");           // 配置优先
        map["BatchStatus"]["InProgress"].Should().Be("在产");        // 未配置兜底
    }

    // ========== GetOptionsMapAsync ==========

    [Fact]
    public async Task GetOptionsMapAsync_空表_静态注册顺序()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var map = await svc.GetOptionsMapAsync();

        map.Should().ContainKey("BatchStatus");
        map["BatchStatus"].Select(o => o.Value).Should().Equal(
            "None", "InProgress", "InFinalInspection", "Completed", "Suspended");
    }

    [Fact]
    public async Task GetOptionsMapAsync_配置按DisplayOrder排序_缺失值追加末尾()
    {
        var ctx = CreateDbContext();
        ctx.EnumDisplayDefinitions.Add(Row("BatchStatus", "InFinalInspection", "成检（新）", 1));
        ctx.EnumDisplayDefinitions.Add(Row("BatchStatus", "Suspended", "暂停（新）", 2));
        ctx.EnumDisplayDefinitions.Add(Row("BatchStatus", "None", "未产（新）", 3));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetOptionsMapAsync();

        // 配置行按 DisplayOrder 升序在前，缺失静态值（InProgress/Completed）追加末尾
        map["BatchStatus"].Select(o => o.Value).Should().Equal(
            "InFinalInspection", "Suspended", "None", "InProgress", "Completed");
        map["BatchStatus"].First(o => o.Value == "InProgress").DisplayOrder.Should().Be(4);
        map["BatchStatus"].First(o => o.Value == "Completed").DisplayOrder.Should().Be(5);
        map["BatchStatus"].First(o => o.Value == "InFinalInspection").DisplayName.Should().Be("成检（新）");
    }

    // ========== RestoreDefaultsAsync ==========

    [Fact]
    public async Task RestoreDefaultsAsync_只补缺失行_不改已存在()
    {
        var ctx = CreateDbContext();
        ctx.EnumDisplayDefinitions.Add(Row("BatchStatus", "InProgress", "在制（自定义）", 1));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var added = await svc.RestoreDefaultsAsync("BatchStatus");

        added.Should().Be(4); // None/InFinalInspection/Completed/Suspended 缺失
        var rows = ctx.EnumDisplayDefinitions.Where(x => x.EnumKey == "BatchStatus").ToList();
        rows.Should().HaveCount(5);
        rows.Single(x => x.Value == "InProgress").DisplayName.Should().Be("在制（自定义）"); // 不覆盖
    }

    [Fact]
    public async Task RestoreDefaultsAsync_未注册枚举_返回0()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.RestoreDefaultsAsync("NoSuchEnum")).Should().Be(0);
    }

    // ========== SaveAsync ==========

    [Fact]
    public async Task SaveAsync_同Key同值重复_抛业务异常()
    {
        var ctx = CreateDbContext();
        ctx.EnumDisplayDefinitions.Add(Row("BatchStatus", "None", "未产", 1));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var dto = new MES.Core.DTOs.Configuration.EnumDisplayDefinitionDto
        {
            Id = 0,
            EnumKey = "BatchStatus",
            Value = "None",
            DisplayName = "未产",
            DisplayOrder = 2
        };

        await FluentActions.Invoking(() => svc.SaveAsync(dto))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>();
    }

    [Fact]
    public async Task SaveAsync_中文显示不含汉字_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = new MES.Core.DTOs.Configuration.EnumDisplayDefinitionDto
        {
            Id = 0,
            EnumKey = "BatchStatus",
            Value = "None",
            DisplayName = "NotAvailable",  // 纯英文显示名 → 必须拦截
            DisplayOrder = 1
        };

        await FluentActions.Invoking(() => svc.SaveAsync(dto))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*汉字*");
    }

    [Fact]
    public async Task SaveAsync_编辑行改锚点Value_抛业务异常()
    {
        var ctx = CreateDbContext();
        var row = Row("BatchStatus", "None", "未产", 1);
        ctx.EnumDisplayDefinitions.Add(row);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var dto = new MES.Core.DTOs.Configuration.EnumDisplayDefinitionDto
        {
            Id = row.Id,
            EnumKey = "BatchStatus",
            Value = "InProgress",          // 锚点被改 → 应拦截
            DisplayName = "未产（新）",
            DisplayOrder = 1
        };

        await FluentActions.Invoking(() => svc.SaveAsync(dto))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*锚点*");
    }

    [Fact]
    public async Task SaveAsync_编辑行改中文名_成功()
    {
        var ctx = CreateDbContext();
        var row = Row("BatchStatus", "None", "未产", 1);
        ctx.EnumDisplayDefinitions.Add(row);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var dto = new MES.Core.DTOs.Configuration.EnumDisplayDefinitionDto
        {
            Id = row.Id,
            EnumKey = "BatchStatus",
            Value = "None",                // 锚点不变
            DisplayName = "未产（新）",
            DisplayOrder = 2
        };

        (await svc.SaveAsync(dto)).Should().BeTrue();
        var updated = ctx.EnumDisplayDefinitions.Single(x => x.Id == row.Id);
        updated.DisplayName.Should().Be("未产（新）");
        updated.DisplayOrder.Should().Be(2);
        updated.Value.Should().Be("None");
        updated.EnumKey.Should().Be("BatchStatus");
    }

    [Fact]
    public async Task SaveAsync_改中文名_刷新进程内静态覆盖()
    {
        var ctx = CreateDbContext();
        var row = Row("BatchStatus", "None", "未产", 1);
        ctx.EnumDisplayDefinitions.Add(row);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var dto = new MES.Core.DTOs.Configuration.EnumDisplayDefinitionDto
        {
            Id = row.Id,
            EnumKey = "BatchStatus",
            Value = "None",
            DisplayName = "未产（保存即生效）",
            DisplayOrder = 1
        };

        await svc.SaveAsync(dto);

        // 保存后进程内静态覆盖已刷新，后端打印/DataExchange 免重启即用新中文
        EnumHelper.GetDisplayName<BatchStatus>(BatchStatus.None).Should().Be("未产（保存即生效）");
    }
}
