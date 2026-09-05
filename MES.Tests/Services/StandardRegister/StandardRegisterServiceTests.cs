using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.StandardRegister;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.StandardRegister;
using MES.Services.StandardRegister;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 标准号注册服务测试：主表 CRUD/Save 缺失返回 0/删除布尔、关键字分页、GetAll 排序、
/// ResolveNameAsync 容错匹配（精确/去年份/去空白）、子项目 CRUD、孤儿子项清理。
/// </summary>
public class StandardRegisterServiceTests : TestBase
{
    private static StandardRegisterService CreateService(AppDbContext ctx) => new(ctx);

    private static async Task<Data.Entities.StandardRegister.StandardRegister> SeedStandardAsync(AppDbContext ctx,
        string standardNo, string standardName)
    {
        var e = new Data.Entities.StandardRegister.StandardRegister
        {
            StandardNo = standardNo,
            StandardName = standardName,
            ManufactureMethod = "冷拔",
            SteelType = "奥氏体"
        };
        ctx.StandardRegisters.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static async Task<StandardRegisterItem> SeedItemAsync(AppDbContext ctx, int registerId, int seqNo,
        string inspectionItem = "拉伸")
    {
        var e = new StandardRegisterItem
        {
            StandardRegisterId = registerId,
            SeqNo = seqNo,
            InspectionItem = inspectionItem
        };
        ctx.StandardRegisterItems.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static StandardRegisterDto NewDto(string standardNo = "GB/T 14976", string name = "不锈钢无缝钢管") => new()
    {
        StandardNo = standardNo,
        StandardName = name,
        ManufactureMethod = "冷拔",
        SteelType = "奥氏体"
    };

    // ========== 主表 CRUD ==========

    [Fact]
    public async Task GetPagedAsync_关键字命中多字段()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, "GB/T 14976", "流体输送钢管");
        await SeedStandardAsync(ctx, "GB/T 21833", "奥氏体钢管");
        var svc = CreateService(ctx);

        var byNo = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "14976" });
        byNo.Items.Should().ContainSingle().Which.StandardNo.Should().Be("GB/T 14976");

        var byName = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "流体" });
        byName.Items.Should().ContainSingle().Which.StandardName.Should().Be("流体输送钢管");

        var byMethod = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "冷拔" });
        byMethod.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_存在返回_缺失返回Null()
    {
        var ctx = CreateDbContext();
        var e = await SeedStandardAsync(ctx, "GB/T 14976", "不锈钢无缝钢管");
        var svc = CreateService(ctx);

        var dto = await svc.GetByIdAsync(e.Id);
        dto!.StandardNo.Should().Be("GB/T 14976");

        var missing = await svc.GetByIdAsync(99999);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_新增_返回Id且落库()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var id = await svc.SaveAsync(NewDto("GB/T 14976-2012", "不锈钢无缝钢管-新版"));

        id.Should().BeGreaterThan(0);
        var row = await ctx.StandardRegisters.SingleAsync();
        row.StandardNo.Should().Be("GB/T 14976-2012");
    }

    [Fact]
    public async Task SaveAsync_更新_修改字段()
    {
        var ctx = CreateDbContext();
        var e = await SeedStandardAsync(ctx, "GB/T 14976", "原名称");
        var svc = CreateService(ctx);
        var dto = NewDto("GB/T 14976", "更新名称");
        dto.Id = e.Id;

        var id = await svc.SaveAsync(dto);

        id.Should().Be(e.Id);
        var row = await ctx.StandardRegisters.SingleAsync();
        row.StandardName.Should().Be("更新名称");
    }

    [Fact]
    public async Task SaveAsync_更新缺失_返回0()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var dto = NewDto("X");
        dto.Id = 99999;

        var id = await svc.SaveAsync(dto);

        id.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_成功与缺失()
    {
        var ctx = CreateDbContext();
        var e = await SeedStandardAsync(ctx, "GB/T 14976", "名称");
        var svc = CreateService(ctx);

        (await svc.DeleteAsync(e.Id)).Should().BeTrue();
        ctx.StandardRegisters.Should().BeEmpty();

        (await svc.DeleteAsync(99999)).Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_按标准号排序()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, "GB/T 21833", "B");
        await SeedStandardAsync(ctx, "GB/T 14976", "A");
        var svc = CreateService(ctx);

        var all = await svc.GetAllAsync();

        all.Select(s => s.StandardNo).Should().Equal("GB/T 14976", "GB/T 21833");
    }

    // ========== ResolveNameAsync 容错 ==========

    [Fact]
    public async Task ResolveNameAsync_精确命中返回名称()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, "GB/T 14976", "不锈钢无缝钢管");
        var svc = CreateService(ctx);

        var name = await svc.ResolveNameAsync("GB/T 14976");

        name.Should().Be("不锈钢无缝钢管");
    }

    [Fact]
    public async Task ResolveNameAsync_去年份后缀命中()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, "GB/T 14976", "不锈钢无缝钢管");
        var svc = CreateService(ctx);

        var name = await svc.ResolveNameAsync("GB/T 14976-2012");

        name.Should().Be("不锈钢无缝钢管");
    }

    [Fact]
    public async Task ResolveNameAsync_键含空格_去空白命中()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, "GB/T 14976", "不锈钢无缝钢管");
        var svc = CreateService(ctx);

        var name = await svc.ResolveNameAsync("GB /T  14976");

        name.Should().Be("不锈钢无缝钢管");
    }

    [Fact]
    public async Task ResolveNameAsync_空白入参与未命中_返回Null()
    {
        var ctx = CreateDbContext();
        await SeedStandardAsync(ctx, "GB/T 14976", "不锈钢无缝钢管");
        var svc = CreateService(ctx);

        (await svc.ResolveNameAsync("  ")).Should().BeNull();
        (await svc.ResolveNameAsync("GB/T 00000")).Should().BeNull();
    }

    // ========== 子项目 ==========

    [Fact]
    public async Task SaveItem_新增_GetItems返回()
    {
        var ctx = CreateDbContext();
        var reg = await SeedStandardAsync(ctx, "GB/T 14976", "不锈钢无缝钢管");
        var svc = CreateService(ctx);

        var itemId = await svc.SaveItemAsync(new StandardRegisterItemDto
        {
            StandardRegisterId = reg.Id,
            SeqNo = 1,
            InspectionItem = "拉伸试验",
            IsMandatory = "关键"
        });

        itemId.Should().BeGreaterThan(0);
        var items = await svc.GetItemsAsync(reg.Id);
        items.Should().ContainSingle().Which.InspectionItem.Should().Be("拉伸试验");
    }

    [Fact]
    public async Task DeleteItemAsync_成功与缺失()
    {
        var ctx = CreateDbContext();
        var reg = await SeedStandardAsync(ctx, "GB/T 14976", "不锈钢无缝钢管");
        var item = await SeedItemAsync(ctx, reg.Id, 1);
        var svc = CreateService(ctx);

        (await svc.DeleteItemAsync(item.Id)).Should().BeTrue();
        ctx.StandardRegisterItems.Should().BeEmpty();

        (await svc.DeleteItemAsync(99999)).Should().BeFalse();
    }

    [Fact]
    public async Task CleanupOrphanedItemsAsync_清理孤儿与重复序号()
    {
        var ctx = CreateDbContext();
        var reg = await SeedStandardAsync(ctx, "GB/T 14976", "不锈钢无缝钢管");
        // 保留：合法唯一项
        await SeedItemAsync(ctx, reg.Id, 20, "合法项");
        // 待清理：StandardRegisterId=0
        await SeedItemAsync(ctx, 0, 1);
        // 待清理：引用的标准号不存在
        await SeedItemAsync(ctx, 99999, 1);
        // 待清理：同 (register, seqNo) 重复（保留 Id 最小者）
        var dupA = await SeedItemAsync(ctx, reg.Id, 10, "重复-A");
        await SeedItemAsync(ctx, reg.Id, 10, "重复-B");
        var svc = CreateService(ctx);

        var removed = await svc.CleanupOrphanedItemsAsync();

        removed.Should().Be(3);
        var remaining = await ctx.StandardRegisterItems.ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Select(i => i.InspectionItem).Should().BeEquivalentTo("合法项", "重复-A");
    }
}
