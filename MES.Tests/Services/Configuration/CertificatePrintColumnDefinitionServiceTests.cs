using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.DTOs.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services.Configuration;

/// <summary>
/// 质量证明书打印列布局配置服务测试：全量排序、配置映射键与缓存、批量保存新增/更新/校验/清缓存。
/// （明细表无行概念，故无 RowIndex，仿工艺卡但去掉行维度）
/// </summary>
public class CertificatePrintColumnDefinitionServiceTests : TestBase
{
    private CertificatePrintColumnDefinitionService CreateService(AppDbContext ctx)
        => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private static CertificatePrintColumnDefinition Row(string blockKey, string fieldKey, string label, int columnIndex, int weight = 3, bool visible = true)
        => new()
        {
            BlockKey = blockKey,
            FieldKey = fieldKey,
            Label = label,
            Visible = visible,
            ColumnIndex = columnIndex,
            ColumnWeight = weight
        };

    private static CertificatePrintColumnDefinitionDto Dto(string blockKey, string fieldKey, string label, int columnIndex, int weight = 3, bool visible = true)
        => new()
        {
            Id = 0,
            BlockKey = blockKey,
            FieldKey = fieldKey,
            Label = label,
            Visible = visible,
            ColumnIndex = columnIndex,
            ColumnWeight = weight
        };

    // ========== GetAllAsync ==========

    [Fact]
    public async Task GetAllAsync_按区块升序再列顺序升序()
    {
        var ctx = CreateDbContext();
        ctx.CertificatePrintColumnDefinitions.AddRange(
            Row("Inspection", "Pmi", "PMI", 2),
            Row("Material", "ProductionBatchNo", "生产批号", 2),
            Row("Material", "HeatNo", "炉号", 1),
            Row("Chemistry", "Element", "元素", 1));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var rows = await svc.GetAllAsync();

        // 按 BlockKey 字母升序：Chemistry → Inspection → Material；区块内再按 ColumnIndex 升序
        rows.Select(r => r.FieldKey).Should().Equal("Element", "Pmi", "HeatNo", "ProductionBatchNo");
        rows[2].FieldKey.Should().Be("HeatNo");
        rows[2].ColumnIndex.Should().Be(1);
        rows[3].FieldKey.Should().Be("ProductionBatchNo");
        rows[3].ColumnIndex.Should().Be(2);
    }

    // ========== GetConfigMapAsync ==========

    [Fact]
    public async Task GetConfigMapAsync_键为区块加字段_忽略大小写()
    {
        var ctx = CreateDbContext();
        ctx.CertificatePrintColumnDefinitions.Add(Row("Material", "HeatNo", "炉号", 1, weight: 9));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetConfigMapAsync();

        map.Should().ContainKey("Material|HeatNo");
        map["material|heatno"].Label.Should().Be("炉号");   // 大小写不敏感
        map["Material|HeatNo"].ColumnWeight.Should().Be(9);
        map.Should().NotContainKey("Material|ProductionBatchNo");
    }

    [Fact]
    public async Task GetConfigMapAsync_首次查询后写库_缓存内不变()
    {
        var ctx = CreateDbContext();
        ctx.CertificatePrintColumnDefinitions.Add(Row("Material", "HeatNo", "炉号", 1));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map1 = await svc.GetConfigMapAsync();
        map1.Should().ContainKey("Material|HeatNo");

        // 缓存期内直插数据库：再次查询仍返回缓存旧值
        ctx.CertificatePrintColumnDefinitions.Add(Row("Material", "ProductionBatchNo", "生产批号", 2));
        await ctx.SaveChangesAsync();

        var map2 = await svc.GetConfigMapAsync();
        map2.Should().ContainKey("Material|HeatNo");
        map2.Should().NotContainKey("Material|ProductionBatchNo");
    }

    // ========== SaveAllAsync ==========

    [Fact]
    public async Task SaveAllAsync_新增行_返回写入行数()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var written = await svc.SaveAllAsync(new List<CertificatePrintColumnDefinitionDto>
        {
            Dto("Material", "HeatNo", "炉号", 1, weight: 9),
            Dto("Material", "SteelGrade", "牌号", 2, weight: 6)
        });

        written.Should().Be(2);
        var rows = ctx.CertificatePrintColumnDefinitions.ToList();
        rows.Should().HaveCount(2);
        rows.Single(x => x.FieldKey == "HeatNo").ColumnWeight.Should().Be(9);
        rows.Single(x => x.FieldKey == "HeatNo").BlockKey.Should().Be("Material");
    }

    [Fact]
    public async Task SaveAllAsync_更新已存在锚点_不重复插入()
    {
        var ctx = CreateDbContext();
        ctx.CertificatePrintColumnDefinitions.Add(Row("Material", "HeatNo", "炉号", 1, weight: 9));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var written = await svc.SaveAllAsync(new List<CertificatePrintColumnDefinitionDto>
        {
            Dto("Material", "HeatNo", "熔炼炉号", 3, weight: 12, visible: false)
        });

        written.Should().Be(1); // 仅 1 条更新
        var rows = ctx.CertificatePrintColumnDefinitions.ToList();
        rows.Should().HaveCount(1);
        rows[0].Label.Should().Be("熔炼炉号");
        rows[0].ColumnIndex.Should().Be(3);
        rows[0].ColumnWeight.Should().Be(12);
        rows[0].Visible.Should().BeFalse();
        rows[0].BlockKey.Should().Be("Material"); // 锚点不变
        rows[0].FieldKey.Should().Be("HeatNo");
    }

    [Fact]
    public async Task SaveAllAsync_空列表_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintColumnDefinitionDto>()))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>();
    }

    [Fact]
    public async Task SaveAllAsync_非法标识_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintColumnDefinitionDto>
            {
                Dto("Material", "1Bad-Key", "炉号", 1)
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*标识格式不正确*");
    }

    [Fact]
    public async Task SaveAllAsync_正整数校验_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 权重/列顺序任一项非正整数即拦截
        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintColumnDefinitionDto>
            {
                Dto("Material", "HeatNo", "炉号", 0)
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*正整数*");
    }

    [Fact]
    public async Task SaveAllAsync_列表内重复锚点_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintColumnDefinitionDto>
            {
                Dto("Material", "HeatNo", "炉号", 1),
                Dto("Material", "HeatNo", "炉号（重复）", 2)
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*重复锚点*");
    }

    [Fact]
    public async Task SaveAllAsync_新增行带LabelEn_持久化并返回()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var written = await svc.SaveAllAsync(new List<CertificatePrintColumnDefinitionDto>
        {
            new()
            {
                BlockKey = "BasicInfo", FieldKey = "CustomerName", Label = "客户名称",
                LabelEn = "Customer Name", Visible = true, ColumnIndex = 1, ColumnWeight = 3
            }
        });

        written.Should().Be(1);
        var row = ctx.CertificatePrintColumnDefinitions.Single();
        row.LabelEn.Should().Be("Customer Name");
    }

    [Fact]
    public async Task SaveAllAsync_更新已存在锚点_LabelEn同步更新()
    {
        var ctx = CreateDbContext();
        ctx.CertificatePrintColumnDefinitions.Add(Row("Material", "HeatNo", "炉号", 1));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var written = await svc.SaveAllAsync(new List<CertificatePrintColumnDefinitionDto>
        {
            new()
            {
                BlockKey = "Material", FieldKey = "HeatNo", Label = "炉号",
                LabelEn = "Heat Number", Visible = true, ColumnIndex = 1, ColumnWeight = 4
            }
        });

        written.Should().Be(1);
        var row = ctx.CertificatePrintColumnDefinitions.Single();
        row.LabelEn.Should().Be("Heat Number");
    }

    [Fact]
    public async Task SaveAllAsync_LabelEn超长_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<CertificatePrintColumnDefinitionDto>
            {
                new()
                {
                    BlockKey = "Material", FieldKey = "HeatNo", Label = "炉号",
                    LabelEn = new string('X', 51), Visible = true, ColumnIndex = 1, ColumnWeight = 4
                }
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*英文显示名*");
    }

    [Fact]
    public async Task SaveAllAsync_写入后清缓存_再次查询反映最新()
    {
        var ctx = CreateDbContext();
        ctx.CertificatePrintColumnDefinitions.Add(Row("Material", "HeatNo", "炉号", 1));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var before = await svc.GetConfigMapAsync();
        before.Should().ContainKey("Material|HeatNo");

        await svc.SaveAllAsync(new List<CertificatePrintColumnDefinitionDto>
        {
            Dto("Material", "HeatNo", "熔炼炉号", 1, weight: 9),
            Dto("Material", "SteelGrade", "牌号", 2)
        });

        var after = await svc.GetConfigMapAsync();
        after.Should().ContainKey("Material|HeatNo");
        after.Should().ContainKey("Material|SteelGrade");
        after["Material|HeatNo"].Label.Should().Be("熔炼炉号");
    }
}
