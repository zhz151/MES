using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.DTOs.Scheduling;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Scheduling;
using MES.Services.Scheduling;
using MES.Tests.Tests;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 冷轧机台组配置服务测试：CRUD/排序/GroupKey 与工序校验/供需链合法性（2026-08-29 方案 A：
/// 组角色字段已移除，链完全由 SupplyTargetGroupKey 显式表达，校验=目标存在+无环，允许多链/多级链）/三缓存键失效。
/// </summary>
public class ColdRollMachineGroupConfigServiceTests : TestBase
{
    /// <summary>附加冷轧/冷拔工序 Key（模拟配置表新增工序，避免新增组与种子 4 组工序重叠）</summary>
    private ColdRollMachineGroupConfigService CreateService(AppDbContext ctx, params string[] extraProcessKeys)
        => new(ctx, new MemoryCache(new MemoryCacheOptions()), CreateProcessDefinitionServiceMock(extraProcessKeys));

    /// <summary>预置标准 4 组（5060 供给目标 2030；2030/三辊/冷拔无供给目标），工序全部被占用</summary>
    private void SeedGroups(AppDbContext ctx)
    {
        ctx.ColdRollMachineGroupConfigs.AddRange(
            new ColdRollMachineGroupConfig { GroupKey = ColdRollMachineGroupKeys.Roll5060, DisplayName = ColdRollMachineGroupKeys.Roll5060Display, ProcessKeys = $"{ProcessKeys.ColdRoll60},{ProcessKeys.ColdRoll50}", DisplayOrder = 1, SupplyTargetGroupKey = ColdRollMachineGroupKeys.Roll2030 },
            new ColdRollMachineGroupConfig { GroupKey = ColdRollMachineGroupKeys.Roll2030, DisplayName = ColdRollMachineGroupKeys.Roll2030Display, ProcessKeys = $"{ProcessKeys.ColdRoll20},{ProcessKeys.ColdRoll30}", DisplayOrder = 2 },
            new ColdRollMachineGroupConfig { GroupKey = ColdRollMachineGroupKeys.ThreeRoll, DisplayName = ColdRollMachineGroupKeys.ThreeRollDisplay, ProcessKeys = ProcessKeys.ThreeRollColdRoll, DisplayOrder = 3 },
            new ColdRollMachineGroupConfig { GroupKey = ColdRollMachineGroupKeys.Draw, DisplayName = ColdRollMachineGroupKeys.DrawDisplay, ProcessKeys = ProcessKeys.ColdDraw, DisplayOrder = 4 });
        ctx.SaveChanges();
    }

    private static ColdRollMachineGroupConfigDto Dto(int id, string groupKey, string display, string[] keys, int order, string? supplyTarget = null)
        => new()
        {
            Id = id,
            GroupKey = groupKey,
            DisplayName = display,
            ProcessKeys = keys.ToList(),
            DisplayOrder = order,
            SupplyTargetGroupKey = supplyTarget,
        };

    // ==================== GetAllAsync ====================

    [Fact]
    public async Task GetAllAsync_按显示顺序返回()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync();

        result.Select(g => g.GroupKey).Should().Equal("5060", "2030", "ThreeRoll", "Draw");
    }

    // ==================== GetPagedAsync ====================

    [Fact]
    public async Task GetPagedAsync_关键词过滤组显示名()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 10, SortBy = "displayorder", IsDescending = false, Keyword = "三辊" });

        result.TotalCount.Should().Be(1);
        result.Items.Single().GroupKey.Should().Be("ThreeRoll");
    }

    [Fact]
    public async Task GetPagedAsync_按组Key排序()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 10, SortBy = "groupkey", IsDescending = false });

        result.Items.Select(g => g.GroupKey).Should().Equal("2030", "5060", "Draw", "ThreeRoll");
    }

    // ==================== SaveAsync ====================

    [Fact]
    public async Task SaveAsync_新增成功_工序逗号串入库()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx, "ColdRoll75");
        var ok = await svc.SaveAsync(Dto(0, "NewGroup", "新组", new[] { "ColdRoll75" }, 5));
        ok.Should().BeTrue();

        var entity = ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == "NewGroup");
        entity.ProcessKeys.Should().Be("ColdRoll75");
        entity.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public async Task SaveAsync_更新成功()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);
        var id = ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == "Draw").Id;

        var ok = await svc.SaveAsync(Dto(id, "Draw", "冷拔(改名)", new[] { ProcessKeys.ColdDraw }, 9));
        ok.Should().BeTrue();

        var entity = ctx.ColdRollMachineGroupConfigs.Single(g => g.Id == id);
        entity.DisplayName.Should().Be("冷拔(改名)");
        entity.DisplayOrder.Should().Be(9);
    }

    [Fact]
    public async Task SaveAsync_组Key格式非法_抛错()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(Dto(0, "50组!", "非法Key", new[] { ProcessKeys.ColdRoll60 }, 5));
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*格式不正确*");
    }

    [Fact]
    public async Task SaveAsync_组Key重复_抛错()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx, "ColdRoll75");

        var act = () => svc.SaveAsync(Dto(0, "5060", "重复组", new[] { "ColdRoll75" }, 5));
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已存在*");
    }

    [Fact]
    public async Task SaveAsync_组内工序为空_抛错()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(Dto(0, "EmptyGroup", "空组", Array.Empty<string>(), 5));
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不能为空*");
    }

    [Fact]
    public async Task SaveAsync_工序非冷轧_抛错()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(Dto(0, "BadGroup", "坏组", new[] { "ColdRoll75" }, 5));
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不是已启用的冷轧/冷拔工序*");
    }

    [Fact]
    public async Task SaveAsync_禁用冷轧工序归组_抛错()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = new ColdRollMachineGroupConfigService(ctx, new MemoryCache(new MemoryCacheOptions()),
            CreateProcessDefinitionServiceMock(Array.Empty<string>(), new[] { ProcessKeys.ColdRoll60 }));

        var act = () => svc.SaveAsync(Dto(0, "DisabledGroup", "禁用工序组", new[] { ProcessKeys.ColdRoll60 }, 5));
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不是已启用的冷轧/冷拔工序*");
    }

    [Fact]
    public async Task SaveAsync_工序跨组重叠_抛错()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);

        // 60 已归 5060 组，再归入新组 → 重叠
        var act = () => svc.SaveAsync(Dto(0, "NewGroup", "新组", new[] { ProcessKeys.ColdRoll60, ProcessKeys.ColdDraw }, 5));
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已归属其他机台组*");
    }

    [Fact]
    public async Task SaveAsync_配置供给目标_目标不存在_抛错()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx, "ColdRoll75");

        // 供给目标填了不存在的组 Key → 链不合法
        var act = () => svc.SaveAsync(Dto(0, "Sup2", "第二供给方", new[] { "ColdRoll75" }, 5, "Nope"));
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task SaveAsync_并行链合法_成功()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx, "ColdRoll75");

        // 多供给方并行链合法：5060→2030、Sup2→2030（组角色字段已移除，配了供给目标即供给方）
        var ok = await svc.SaveAsync(Dto(0, "Sup2", "第二供给方", new[] { "ColdRoll75" }, 5, ColdRollMachineGroupKeys.Roll2030));
        ok.Should().BeTrue();
        ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == "Sup2").SupplyTargetGroupKey.Should().Be("2030");
    }

    [Fact]
    public async Task SaveAsync_多级链末端指向None组_成功()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx, "ColdRoll75");

        // 多级链中间节点供给目标指向默认组(None)合法：如 2030 → 冷拔(Draw)，Draw 被指向即成为需求承接端
        var ok = await svc.SaveAsync(Dto(0, "Mid", "中间节点", new[] { "ColdRoll75" }, 5, ColdRollMachineGroupKeys.Draw));
        ok.Should().BeTrue();
        ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == "Mid").SupplyTargetGroupKey.Should().Be("Draw");
    }

    [Fact]
    public async Task SaveAsync_多级链三组链_成功()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);
        var id = ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == "2030").Id;

        // 5060 → 2030 → 冷拔 三级链：2030 既被 5060 指向又供给冷拔（中间节点），链合法
        var ok = await svc.SaveAsync(Dto(id, "2030", "冷轧2030", new[] { ProcessKeys.ColdRoll20, ProcessKeys.ColdRoll30 }, 2, ColdRollMachineGroupKeys.Draw));
        ok.Should().BeTrue();
        ctx.ColdRollMachineGroupConfigs.Single(g => g.Id == id).SupplyTargetGroupKey.Should().Be("Draw");
    }

    [Fact]
    public async Task SaveAsync_供给链成环_抛错()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);
        var id = ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == "2030").Id;

        // 2030 → 5060 → 2030 成环
        var act = () => svc.SaveAsync(Dto(id, "2030", "冷轧2030", new[] { ProcessKeys.ColdRoll20, ProcessKeys.ColdRoll30 }, 2, ColdRollMachineGroupKeys.Roll5060));
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*存在环*");
    }

    [Fact]
    public async Task SaveAsync_新增无供给目标组_成功()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx, "ColdRoll75");

        var ok = await svc.SaveAsync(Dto(0, "NewNone", "新默认组", new[] { "ColdRoll75" }, 5));
        ok.Should().BeTrue();
    }

    // ==================== DeleteAsync ====================

    [Fact]
    public async Task DeleteAsync_删除被指向的需求方组_抛错()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);
        var id = ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == "2030").Id;

        // 2030 被 5060 的供给目标指向，删除后 5060 供给目标悬空 → 链不合法
        var act = () => svc.DeleteAsync(id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task DeleteAsync_删除供给方组_成功()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);
        var id = ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == "5060").Id;

        // 删除唯一供给方后无供给方组，链合法性不受影响（方案 A 不强制至少一个供给方）
        var ok = await svc.DeleteAsync(id);
        ok.Should().BeTrue();
        ctx.ColdRollMachineGroupConfigs.Any(g => g.Id == id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_删除None组_成功()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var svc = CreateService(ctx);
        var id = ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == "Draw").Id;

        var ok = await svc.DeleteAsync(id);
        ok.Should().BeTrue();
        ctx.ColdRollMachineGroupConfigs.Any(g => g.Id == id).Should().BeFalse();
    }

    // ==================== 缓存失效 ====================

    [Fact]
    public async Task SaveAsync_失效三处引擎缓存键()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new ColdRollMachineGroupConfigService(ctx, cache, CreateProcessDefinitionServiceMock("ColdRoll75"));
        cache.Set(ColdRollPlanService.MachineGroupCacheKey, "x");
        cache.Set(ColdRollPlanService.MachineEstimateCacheKey, "x");
        cache.Set(ColdRollPlanService.ScheduleSuggestionCacheKey, "x");

        await svc.SaveAsync(Dto(0, "NewNone", "新默认组", new[] { "ColdRoll75" }, 5));

        cache.Get(ColdRollPlanService.MachineGroupCacheKey).Should().BeNull();
        cache.Get(ColdRollPlanService.MachineEstimateCacheKey).Should().BeNull();
        cache.Get(ColdRollPlanService.ScheduleSuggestionCacheKey).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_失效三处引擎缓存键()
    {
        using var ctx = CreateDbContext();
        SeedGroups(ctx);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new ColdRollMachineGroupConfigService(ctx, cache, CreateProcessDefinitionServiceMock());
        cache.Set(ColdRollPlanService.MachineGroupCacheKey, "x");
        cache.Set(ColdRollPlanService.MachineEstimateCacheKey, "x");
        cache.Set(ColdRollPlanService.ScheduleSuggestionCacheKey, "x");
        var id = ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == "Draw").Id;

        await svc.DeleteAsync(id);

        cache.Get(ColdRollPlanService.MachineGroupCacheKey).Should().BeNull();
        cache.Get(ColdRollPlanService.MachineEstimateCacheKey).Should().BeNull();
        cache.Get(ColdRollPlanService.ScheduleSuggestionCacheKey).Should().BeNull();
    }
}
