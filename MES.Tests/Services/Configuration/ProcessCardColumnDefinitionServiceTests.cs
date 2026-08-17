using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.DTOs.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services.Configuration;

/// <summary>
/// 工艺卡打印列布局配置服务测试：全量排序、配置映射键与缓存、批量保存新增/更新/校验/清缓存。
/// </summary>
public class ProcessCardColumnDefinitionServiceTests : TestBase
{
    private ProcessCardColumnDefinitionService CreateService(AppDbContext ctx)
        => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private static ProcessCardColumnDefinition Row(string blockKey, string fieldKey, string label, int columnIndex, int weight = 3, int row = 1, bool visible = true)
        => new()
        {
            BlockKey = blockKey,
            FieldKey = fieldKey,
            Label = label,
            Visible = visible,
            RowIndex = row,
            ColumnIndex = columnIndex,
            ColumnWeight = weight
        };

    private static ProcessCardColumnDefinitionDto Dto(string blockKey, string fieldKey, string label, int columnIndex, int weight = 3, int row = 1, bool visible = true)
        => new()
        {
            Id = 0,
            BlockKey = blockKey,
            FieldKey = fieldKey,
            Label = label,
            Visible = visible,
            RowIndex = row,
            ColumnIndex = columnIndex,
            ColumnWeight = weight
        };

    // ========== GetAllAsync ==========

    [Fact]
    public async Task GetAllAsync_按区块升序再列顺序升序()
    {
        var ctx = CreateDbContext();
        ctx.ProcessCardColumnDefinitions.AddRange(
            Row("WorkOrder", "WorkOrderNo", "工单号", 1),
            Row("BatchInfo", "BatchNo", "生产编号", 2),
            Row("BatchInfo", "TagNo", "挂牌号", 1),
            Row("Warehouse", "SourceBatchNo", "来源批次", 1));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var rows = await svc.GetAllAsync();

        // 按 BlockKey 字母升序：BatchInfo → Warehouse → WorkOrder；区块内再按 ColumnIndex 升序
        rows.Select(r => r.FieldKey).Should().Equal("TagNo", "BatchNo", "SourceBatchNo", "WorkOrderNo");
        rows[0].FieldKey.Should().Be("TagNo");
        rows[0].ColumnIndex.Should().Be(1);
        rows[1].FieldKey.Should().Be("BatchNo");
        rows[1].ColumnIndex.Should().Be(2);
        rows[3].BlockKey.Should().Be("WorkOrder");
    }

    // ========== GetConfigMapAsync ==========

    [Fact]
    public async Task GetConfigMapAsync_键为区块加字段_忽略大小写()
    {
        var ctx = CreateDbContext();
        ctx.ProcessCardColumnDefinitions.Add(Row("BatchInfo", "BatchNo", "生产编号", 1, weight: 9));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map = await svc.GetConfigMapAsync();

        map.Should().ContainKey("BatchInfo|BatchNo");
        map["batchinfo|batchno"].Label.Should().Be("生产编号");   // 大小写不敏感
        map["BatchInfo|BatchNo"].ColumnWeight.Should().Be(9);
        map.Should().NotContainKey("BatchInfo|TagNo");
    }

    [Fact]
    public async Task GetConfigMapAsync_首次查询后写库_缓存内不变()
    {
        var ctx = CreateDbContext();
        ctx.ProcessCardColumnDefinitions.Add(Row("BatchInfo", "BatchNo", "生产编号", 1));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var map1 = await svc.GetConfigMapAsync();
        map1.Should().ContainKey("BatchInfo|BatchNo");

        // 缓存期内直插数据库：再次查询仍返回缓存旧值
        ctx.ProcessCardColumnDefinitions.Add(Row("BatchInfo", "TagNo", "挂牌号", 2));
        await ctx.SaveChangesAsync();

        var map2 = await svc.GetConfigMapAsync();
        map2.Should().ContainKey("BatchInfo|BatchNo");
        map2.Should().NotContainKey("BatchInfo|TagNo");
    }

    // ========== SaveAllAsync ==========

    [Fact]
    public async Task SaveAllAsync_新增行_返回写入行数()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var written = await svc.SaveAllAsync(new List<ProcessCardColumnDefinitionDto>
        {
            Dto("BatchInfo", "BatchNo", "生产编号", 1, weight: 9),
            Dto("BatchInfo", "TagNo", "挂牌号", 2, weight: 6)
        });

        written.Should().Be(2);
        var rows = ctx.ProcessCardColumnDefinitions.ToList();
        rows.Should().HaveCount(2);
        rows.Single(x => x.FieldKey == "BatchNo").ColumnWeight.Should().Be(9);
        rows.Single(x => x.FieldKey == "BatchNo").BlockKey.Should().Be("BatchInfo");
    }

    [Fact]
    public async Task SaveAllAsync_更新已存在锚点_不重复插入()
    {
        var ctx = CreateDbContext();
        ctx.ProcessCardColumnDefinitions.Add(Row("BatchInfo", "BatchNo", "生产编号", 1, weight: 9));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var written = await svc.SaveAllAsync(new List<ProcessCardColumnDefinitionDto>
        {
            Dto("BatchInfo", "BatchNo", "生产编号（改）", 3, weight: 12, row: 2, visible: false)
        });

        written.Should().Be(1); // 仅 1 条更新
        var rows = ctx.ProcessCardColumnDefinitions.ToList();
        rows.Should().HaveCount(1);
        rows[0].Label.Should().Be("生产编号（改）");
        rows[0].ColumnIndex.Should().Be(3);
        rows[0].ColumnWeight.Should().Be(12);
        rows[0].RowIndex.Should().Be(2);
        rows[0].Visible.Should().BeFalse();
        rows[0].BlockKey.Should().Be("BatchInfo"); // 锚点不变
        rows[0].FieldKey.Should().Be("BatchNo");
    }

    [Fact]
    public async Task SaveAllAsync_空列表_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<ProcessCardColumnDefinitionDto>()))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>();
    }

    [Fact]
    public async Task SaveAllAsync_非法标识_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<ProcessCardColumnDefinitionDto>
            {
                Dto("BatchInfo", "1Bad-Key", "生产编号", 1)
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*标识格式不正确*");
    }

    [Fact]
    public async Task SaveAllAsync_正整数校验_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 权重/行/列任一项非正整数即拦截
        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<ProcessCardColumnDefinitionDto>
            {
                Dto("BatchInfo", "BatchNo", "生产编号", 0)
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*正整数*");
    }

    [Fact]
    public async Task SaveAllAsync_列表内重复锚点_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.SaveAllAsync(new List<ProcessCardColumnDefinitionDto>
            {
                Dto("BatchInfo", "BatchNo", "生产编号", 1),
                Dto("BatchInfo", "BatchNo", "生产编号（重复）", 2)
            }))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*重复锚点*");
    }

    [Fact]
    public async Task SaveAllAsync_写入后清缓存_再次查询反映最新()
    {
        var ctx = CreateDbContext();
        ctx.ProcessCardColumnDefinitions.Add(Row("BatchInfo", "BatchNo", "生产编号", 1));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var before = await svc.GetConfigMapAsync();
        before.Should().ContainKey("BatchInfo|BatchNo");

        await svc.SaveAllAsync(new List<ProcessCardColumnDefinitionDto>
        {
            Dto("BatchInfo", "BatchNo", "生产编号（新）", 1, weight: 9),
            Dto("BatchInfo", "TagNo", "挂牌号", 2)
        });

        var after = await svc.GetConfigMapAsync();
        after.Should().ContainKey("BatchInfo|BatchNo");
        after.Should().ContainKey("BatchInfo|TagNo");
        after["BatchInfo|BatchNo"].Label.Should().Be("生产编号（新）");
    }
}
