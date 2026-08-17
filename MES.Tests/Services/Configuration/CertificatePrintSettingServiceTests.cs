using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.DTOs.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services.Configuration;

/// <summary>
/// 质量证明书打印配置服务测试：全量排序、配置映射与缓存、批量保存新增/更新/校验/清缓存。
/// </summary>
public class CertificatePrintSettingServiceTests : TestBase
{
    private CertificatePrintSettingService CreateService(AppDbContext ctx)
        => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private static CertificatePrintSetting Row(string key, string value, string displayName, string? remark = null)
        => new()
        {
            Key = key,
            Value = value,
            DisplayName = displayName,
            Remark = remark
        };

    private static CertificatePrintSettingDto Dto(string key, string value, string displayName, string? remark = null)
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
        ctx.CertificatePrintSettings.AddRange(
            Row("HeaderFontSize", "18", "标题字号"),
            Row("CompanyName", "", "公司名称"),
            Row("FooterStatement", "说明文字", "页脚说明文字"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var rows = await svc.GetAllAsync();

        // 按 Key 字母升序：CompanyName → FooterStatement → HeaderFontSize
        rows.Select(r => r.Key).Should().Equal("CompanyName", "FooterStatement", "HeaderFontSize");
        rows[0].Value.Should().Be("");
        rows[2].DisplayName.Should().Be("标题字号");
    }

    // ========== GetSettingMapAsync ==========

    [Fact]
    public async Task GetSettingMapAsync_键值映射_大小写不敏感()
    {
        var ctx = CreateDbContext();
        ctx.CertificatePrintSettings.Add(Row("HeaderTitle", "质量证明书", "页眉标题"));
        ctx.CertificatePrintSettings.Add(Row("CompanyName", "某某钢管有限公司", "公司名称"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetSettingMapAsync();

        map.Should().ContainKey("HeaderTitle");
        map["headertitle"].Should().Be("质量证明书");   // 大小写不敏感
        map["CompanyName"].Should().Be("某某钢管有限公司");
        map.Should().NotContainKey("PageFontSize");
    }

    [Fact]
    public async Task GetSettingMapAsync_首次查询后写库_缓存内不变()
    {
        var ctx = CreateDbContext();
        ctx.CertificatePrintSettings.Add(Row("HeaderTitle", "质量证明书", "页眉标题"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map1 = await svc.GetSettingMapAsync();
        map1.Should().ContainKey("HeaderTitle");

        // 缓存期内直插数据库：再次查询仍返回缓存旧值
        ctx.CertificatePrintSettings.Add(Row("PageFontSize", "9", "正文字号"));
        await ctx.SaveChangesAsync();

        var map2 = await svc.GetSettingMapAsync();
        map2.Should().ContainKey("HeaderTitle");
        map2.Should().NotContainKey("PageFontSize");
    }

    // ========== SaveAllAsync ==========

    [Fact]
    public async Task SaveAllAsync_新增行_返回写入行数()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var written = await svc.SaveAllAsync(new List<CertificatePrintSettingDto>
        {
            Dto("CompanyName", "某某钢管有限公司", "公司名称"),
            Dto("HeaderFontSize", "18", "标题字号")
        });

        written.Should().Be(2);
        var rows = ctx.CertificatePrintSettings.ToList();
        rows.Should().HaveCount(2);
        rows.Single(x => x.Key == "CompanyName").Value.Should().Be("某某钢管有限公司");
        rows.Single(x => x.Key == "HeaderFontSize").Value.Should().Be("18");
    }

    [Fact]
    public async Task SaveAllAsync_更新已存在锚点_不重复插入()
    {
        var ctx = CreateDbContext();
        ctx.CertificatePrintSettings.Add(Row("CompanyName", "旧名", "公司名称"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var written = await svc.SaveAllAsync(new List<CertificatePrintSettingDto>
        {
            Dto("CompanyName", "新名", "公司名称（改）", "说明")
        });

        written.Should().Be(1); // 仅 1 条更新
        var rows = ctx.CertificatePrintSettings.ToList();
        rows.Should().HaveCount(1);
        rows[0].Key.Should().Be("CompanyName");   // 锚点不变
        rows[0].Value.Should().Be("新名");
        rows[0].DisplayName.Should().Be("公司名称（改）");
        rows[0].Remark.Should().Be("说明");
    }

    [Fact]
    public async Task SaveAllAsync_空列表_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintSettingDto>()))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*配置列表不能为空*");
    }

    [Fact]
    public async Task SaveAllAsync_非法标识_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintSettingDto>
            {
                Dto("1Bad-Key", "20", "标题字号")
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
        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintSettingDto>
            {
                Dto("CompanyName", "", "公司名称")
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*值不能为空*");

        // 显示名空
        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintSettingDto>
            {
                Dto("CompanyName", "某某钢管有限公司", "")
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*显示名不能为空*");
    }

    [Fact]
    public async Task SaveAllAsync_键超50字符_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintSettingDto>
            {
                Dto(new string('A', 51), "20", "标题字号")
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*不能超过 50 字符*");
    }

    [Fact]
    public async Task SaveAllAsync_值超500字符_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintSettingDto>
            {
                Dto("FooterStatement", new string('长', 501), "页脚说明文字")
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*不能超过 500 字符*");
    }

    [Fact]
    public async Task SaveAllAsync_长值500字符内_可保存()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var longValue = string.Join("，", Enumerable.Repeat("公司地址：XX省XX市XX区XX工业园X号（电话 12345678）", 10));
        longValue.Length.Should().BeGreaterThan(50); // 确保覆盖原 50 字符限制
        longValue.Length.Should().BeLessThanOrEqualTo(500);

        var written = await svc.SaveAllAsync(new List<CertificatePrintSettingDto>
        {
            Dto("CompanyAddress", longValue, "公司地址")
        });

        written.Should().Be(1);
        ctx.CertificatePrintSettings.Single(x => x.Key == "CompanyAddress").Value.Should().Be(longValue);
    }

    [Fact]
    public async Task SaveAllAsync_列表内重复锚点_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintSettingDto>
            {
                Dto("CompanyName", "名1", "公司名称"),
                Dto("CompanyName", "名2", "公司名称（重复）")
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*重复锚点*");
    }

    [Fact]
    public async Task SaveAllAsync_写入后清缓存_再次查询反映最新()
    {
        var ctx = CreateDbContext();
        ctx.CertificatePrintSettings.Add(Row("CompanyName", "旧名", "公司名称"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var before = await svc.GetSettingMapAsync();
        before.Should().ContainKey("CompanyName");

        await svc.SaveAllAsync(new List<CertificatePrintSettingDto>
        {
            Dto("CompanyName", "新名", "公司名称"),
            Dto("PageFontSize", "10", "正文字号")
        });

        var after = await svc.GetSettingMapAsync();
        after.Should().ContainKey("CompanyName");
        after.Should().ContainKey("PageFontSize");
        after["CompanyName"].Should().Be("新名");
    }
}
