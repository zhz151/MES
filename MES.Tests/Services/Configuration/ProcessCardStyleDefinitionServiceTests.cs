using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.DTOs.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services.Configuration;

/// <summary>
/// 工艺卡打印版式配置服务测试：全量排序、配置映射与缓存、批量保存新增/更新/校验/清缓存。
/// </summary>
public class ProcessCardStyleDefinitionServiceTests : TestBase
{
    private ProcessCardStyleDefinitionService CreateService(AppDbContext ctx)
        => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private static ProcessCardStyleDefinition Row(string key, string value, string displayName, string? remark = null)
        => new()
        {
            Key = key,
            Value = value,
            DisplayName = displayName,
            Remark = remark
        };

    private static ProcessCardStyleDefinitionDto Dto(string key, string value, string displayName, string? remark = null)
        => new()
        {
            Id = 0,
            Key = key,
            Value = value,
            DisplayName = displayName,
            Remark = remark
        };

    // ========== GetAllAsync ==========

    [Fact]
    public async Task GetAllAsync_按Key升序()
    {
        var ctx = CreateDbContext();
        ctx.ProcessCardStyleDefinitions.AddRange(
            Row("HeaderFontSize", "20", "主标题字号"),
            Row("BatchNoFontSize", "12", "生产编号字号"),
            Row("CellFontSize", "9", "数据字号"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var rows = await svc.GetAllAsync();

        // 按 Key 字母升序：BatchNoFontSize → CellFontSize → HeaderFontSize
        rows.Select(r => r.Key).Should().Equal("BatchNoFontSize", "CellFontSize", "HeaderFontSize");
        rows[0].Value.Should().Be("12");
        rows[2].DisplayName.Should().Be("主标题字号");
    }

    // ========== GetStyleMapAsync ==========

    [Fact]
    public async Task GetStyleMapAsync_键值映射()
    {
        var ctx = CreateDbContext();
        ctx.ProcessCardStyleDefinitions.Add(Row("HeaderFontSize", "22", "主标题字号"));
        ctx.ProcessCardStyleDefinitions.Add(Row("PageFontFamily", "华文仿宋", "正文字体"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetStyleMapAsync();

        map.Should().ContainKey("HeaderFontSize");
        map["headerfontsize"].Should().Be("22");   // 大小写不敏感
        map["PageFontFamily"].Should().Be("华文仿宋");
        map.Should().NotContainKey("CellFontSize");
    }

    [Fact]
    public async Task GetStyleMapAsync_首次查询后写库_缓存内不变()
    {
        var ctx = CreateDbContext();
        ctx.ProcessCardStyleDefinitions.Add(Row("HeaderFontSize", "20", "主标题字号"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map1 = await svc.GetStyleMapAsync();
        map1.Should().ContainKey("HeaderFontSize");

        // 缓存期内直插数据库：再次查询仍返回缓存旧值
        ctx.ProcessCardStyleDefinitions.Add(Row("CellFontSize", "9", "数据字号"));
        await ctx.SaveChangesAsync();

        var map2 = await svc.GetStyleMapAsync();
        map2.Should().ContainKey("HeaderFontSize");
        map2.Should().NotContainKey("CellFontSize");
    }

    // ========== SaveAllAsync ==========

    [Fact]
    public async Task SaveAllAsync_新增行_返回写入行数()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var written = await svc.SaveAllAsync(new List<ProcessCardStyleDefinitionDto>
        {
            Dto("HeaderFontSize", "22", "主标题字号"),
            Dto("PageFontFamily", "华文仿宋", "正文字体")
        });

        written.Should().Be(2);
        var rows = ctx.ProcessCardStyleDefinitions.ToList();
        rows.Should().HaveCount(2);
        rows.Single(x => x.Key == "HeaderFontSize").Value.Should().Be("22");
        rows.Single(x => x.Key == "PageFontFamily").Value.Should().Be("华文仿宋");
    }

    [Fact]
    public async Task SaveAllAsync_更新已存在锚点_不重复插入()
    {
        var ctx = CreateDbContext();
        ctx.ProcessCardStyleDefinitions.Add(Row("HeaderFontSize", "20", "主标题字号"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var written = await svc.SaveAllAsync(new List<ProcessCardStyleDefinitionDto>
        {
            Dto("HeaderFontSize", "24", "主标题字号（改）", "说明")
        });

        written.Should().Be(1); // 仅 1 条更新
        var rows = ctx.ProcessCardStyleDefinitions.ToList();
        rows.Should().HaveCount(1);
        rows[0].Key.Should().Be("HeaderFontSize");   // 锚点不变
        rows[0].Value.Should().Be("24");
        rows[0].DisplayName.Should().Be("主标题字号（改）");
        rows[0].Remark.Should().Be("说明");
    }

    [Fact]
    public async Task SaveAllAsync_空列表_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<ProcessCardStyleDefinitionDto>()))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>();
    }

    [Fact]
    public async Task SaveAllAsync_非法标识_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<ProcessCardStyleDefinitionDto>
            {
                Dto("1Bad-Key", "20", "主标题字号")
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*格式不正确*");
    }

    [Fact]
    public async Task SaveAllAsync_值或显示名空_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 值空
        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<ProcessCardStyleDefinitionDto>
            {
                Dto("HeaderFontSize", "", "主标题字号")
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*值不能为空*");

        // 显示名空
        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<ProcessCardStyleDefinitionDto>
            {
                Dto("HeaderFontSize", "20", "")
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*显示名不能为空*");
    }

    [Fact]
    public async Task SaveAllAsync_列表内重复锚点_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<ProcessCardStyleDefinitionDto>
            {
                Dto("HeaderFontSize", "20", "主标题字号"),
                Dto("HeaderFontSize", "24", "主标题字号（重复）")
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*重复锚点*");
    }

    [Fact]
    public async Task SaveAllAsync_写入后清缓存_再次查询反映最新()
    {
        var ctx = CreateDbContext();
        ctx.ProcessCardStyleDefinitions.Add(Row("HeaderFontSize", "20", "主标题字号"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var before = await svc.GetStyleMapAsync();
        before.Should().ContainKey("HeaderFontSize");

        await svc.SaveAllAsync(new List<ProcessCardStyleDefinitionDto>
        {
            Dto("HeaderFontSize", "24", "主标题字号（新）"),
            Dto("CellFontSize", "10", "数据字号")
        });

        var after = await svc.GetStyleMapAsync();
        after.Should().ContainKey("HeaderFontSize");
        after.Should().ContainKey("CellFontSize");
        after["HeaderFontSize"].Should().Be("24");
    }
}
