using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.Helpers;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services.Configuration;

/// <summary>
/// 字典值配置服务测试：启用值列表（配置排序/隐藏/新加值/静态兜底）。
/// </summary>
public class DictValueDefinitionServiceTests : TestBase, IDisposable
{
    public void Dispose()
    {
        // 清理本类测试注入的进程内静态覆盖，避免污染其他测试类
        DictValueDisplayHelper.OverrideMap = null;
    }

    private DictValueDefinitionService CreateService(AppDbContext ctx)
        => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private static DictValueDefinition Row(string dictKey, string value, string display, int order, bool enabled = true)
        => new() { DictKey = dictKey, Value = value, DisplayName = display, DisplayOrder = order, IsEnabled = enabled };

    [Fact]
    public async Task GetEnabledValuesAsync_空表_静态兜底全量()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetEnabledValuesAsync(DictValueDefaults.LiabilityTypeKey);

        result.Select(r => r.Value).Should().Equal(
            LiabilityTypeKeys.FactoryDepartment, LiabilityTypeKeys.OutsourcedPurchase);
        result[0].DisplayName.Should().Be("厂部");
    }

    [Fact]
    public async Task GetEnabledValuesAsync_配置排序优先_缺失值追加末尾()
    {
        var ctx = CreateDbContext();
        ctx.DictValueDefinitions.Add(Row(DictValueDefaults.LiabilityTypeKey, LiabilityTypeKeys.OutsourcedPurchase, "外购责任", 1));
        ctx.DictValueDefinitions.Add(Row(DictValueDefaults.LiabilityTypeKey, LiabilityTypeKeys.FactoryDepartment, "厂部责任", 2));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetEnabledValuesAsync(DictValueDefaults.LiabilityTypeKey);

        // 配置行按 DisplayOrder 升序在前，静态兜底不重复
        result.Select(r => r.Value).Should().Equal(
            LiabilityTypeKeys.OutsourcedPurchase, LiabilityTypeKeys.FactoryDepartment);
    }

    [Fact]
    public async Task GetEnabledValuesAsync_隐藏值不出现且不被兜底补回()
    {
        var ctx = CreateDbContext();
        ctx.DictValueDefinitions.Add(Row(DictValueDefaults.LiabilityTypeKey, LiabilityTypeKeys.FactoryDepartment, "厂部", 1, enabled: false));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetEnabledValuesAsync(DictValueDefaults.LiabilityTypeKey);

        // 隐藏值既不在配置行，也不被静态兜底补回
        result.Select(r => r.Value).Should().Equal(LiabilityTypeKeys.OutsourcedPurchase);
    }

    [Fact]
    public async Task GetEnabledValuesAsync_新加值出现()
    {
        var ctx = CreateDbContext();
        ctx.DictValueDefinitions.Add(Row(DictValueDefaults.LiabilityTypeKey, "ThirdParty", "第三方", 3));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetEnabledValuesAsync(DictValueDefaults.LiabilityTypeKey);

        result.Select(r => r.Value).Should().Contain("ThirdParty");
    }

    // ========== SaveAsync ==========

    [Fact]
    public async Task SaveAsync_编辑行改锚点Value_抛业务异常()
    {
        var ctx = CreateDbContext();
        var row = Row(DictValueDefaults.LiabilityTypeKey, LiabilityTypeKeys.FactoryDepartment, "厂部", 1);
        ctx.DictValueDefinitions.Add(row);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var dto = new MES.Core.DTOs.Configuration.DictValueDefinitionDto
        {
            Id = row.Id,
            DictKey = DictValueDefaults.LiabilityTypeKey,
            Value = LiabilityTypeKeys.OutsourcedPurchase,   // 锚点被改 → 应拦截
            DisplayName = "厂部（新）",
            DisplayOrder = 1,
            IsEnabled = true
        };

        await FluentActions.Invoking(() => svc.SaveAsync(dto))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*锚点*");
    }

    [Fact]
    public async Task SaveAsync_中文显示不含汉字_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = new MES.Core.DTOs.Configuration.DictValueDefinitionDto
        {
            Id = 0,
            DictKey = DictValueDefaults.LiabilityTypeKey,
            Value = LiabilityTypeKeys.FactoryDepartment,
            DisplayName = "Factory",     // 纯英文显示名 → 必须拦截
            DisplayOrder = 1,
            IsEnabled = true
        };

        await FluentActions.Invoking(() => svc.SaveAsync(dto))
            .Should().ThrowAsync<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*汉字*");
    }

    [Fact]
    public async Task SaveAsync_编辑行改中文名_成功()
    {
        var ctx = CreateDbContext();
        var row = Row(DictValueDefaults.LiabilityTypeKey, LiabilityTypeKeys.FactoryDepartment, "厂部", 1);
        ctx.DictValueDefinitions.Add(row);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var dto = new MES.Core.DTOs.Configuration.DictValueDefinitionDto
        {
            Id = row.Id,
            DictKey = DictValueDefaults.LiabilityTypeKey,
            Value = LiabilityTypeKeys.FactoryDepartment,     // 锚点不变
            DisplayName = "厂部（新）",
            DisplayOrder = 2,
            IsEnabled = false
        };

        (await svc.SaveAsync(dto)).Should().BeTrue();
        var updated = ctx.DictValueDefinitions.Single(x => x.Id == row.Id);
        updated.DisplayName.Should().Be("厂部（新）");
        updated.DisplayOrder.Should().Be(2);
        updated.IsEnabled.Should().BeFalse();
        updated.Value.Should().Be(LiabilityTypeKeys.FactoryDepartment);
        updated.DictKey.Should().Be(DictValueDefaults.LiabilityTypeKey);
    }

    [Fact]
    public async Task SaveAsync_改中文名_刷新进程内静态覆盖()
    {
        var ctx = CreateDbContext();
        var row = Row(DictValueDefaults.LiabilityTypeKey, LiabilityTypeKeys.FactoryDepartment, "厂部", 1);
        ctx.DictValueDefinitions.Add(row);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var dto = new MES.Core.DTOs.Configuration.DictValueDefinitionDto
        {
            Id = row.Id,
            DictKey = DictValueDefaults.LiabilityTypeKey,
            Value = LiabilityTypeKeys.FactoryDepartment,
            DisplayName = "厂部（保存即生效）",
            DisplayOrder = 1,
            IsEnabled = true
        };

        await svc.SaveAsync(dto);

        // 保存后进程内静态 OverrideMap 已刷新，后端打印/DataExchange 免重启即用新中文
        DictValueDisplayHelper.GetText(DictValueDefaults.LiabilityTypeKey, LiabilityTypeKeys.FactoryDepartment)
            .Should().Be("厂部（保存即生效）");
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回可筛列DISTINCT值()
    {
        var ctx = CreateDbContext();
        ctx.DictValueDefinitions.Add(Row(DictValueDefaults.LiabilityTypeKey, LiabilityTypeKeys.FactoryDepartment, "厂部", 1));
        ctx.DictValueDefinitions.Add(Row(DictValueDefaults.LiabilityTypeKey, LiabilityTypeKeys.OutsourcedPurchase, "外购", 2, enabled: false));
        ctx.DictValueDefinitions.Add(Row(DictValueDefaults.NcrResponsibilityKey, NcrResponsibilityKeys.ProductionInternal, "生产-厂内", 1));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["DictKey"].Should().Equal(DictValueDefaults.LiabilityTypeKey, DictValueDefaults.NcrResponsibilityKey);
        contexts["Value"].Should().Equal(
            LiabilityTypeKeys.FactoryDepartment, LiabilityTypeKeys.OutsourcedPurchase, NcrResponsibilityKeys.ProductionInternal);
        // 中文按 CurrentCulture 拼音序（厂c/生sh/外w）
        contexts["DisplayName"].Should().Equal("厂部", "生产-厂内", "外购");
        contexts["IsEnabled"].Should().Equal("False", "True");
        contexts["Remark"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_空Remark_不包含()
    {
        var ctx = CreateDbContext();
        var withRemark = Row(DictValueDefaults.LiabilityTypeKey, LiabilityTypeKeys.FactoryDepartment, "厂部", 1);
        withRemark.Remark = "责任类型";
        ctx.DictValueDefinitions.Add(withRemark);
        ctx.DictValueDefinitions.Add(Row(DictValueDefaults.LiabilityTypeKey, LiabilityTypeKeys.OutsourcedPurchase, "外购", 2));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["Remark"].Should().Equal("责任类型");
    }
}
