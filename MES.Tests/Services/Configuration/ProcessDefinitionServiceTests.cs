using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Scheduling;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services.Configuration;

/// <summary>
/// 工序组定义服务测试：CRUD + 禁用工序自动删除机台数配置/机台组工序清理（2026-08-29 Q2）。
/// </summary>
public class ProcessDefinitionServiceTests : TestBase
{
    private static ProcessDefinitionService CreateService(AppDbContext ctx)
        => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private static ProcessDefinitionDto BuildDto(int id, string key, string name, bool isEnabled, bool isColdRoll = true, bool isColdDraw = false)
        => new()
        {
            Id = id,
            ProcessKey = key,
            ProcessName = name,
            DisplayOrder = 1,
            IsEnabled = isEnabled,
            IsColdRoll = isColdRoll,
            IsColdDraw = isColdDraw,
        };

    [Fact]
    public async Task SaveAsync_禁用冷轧工序_自动删除机台数配置()
    {
        using var ctx = CreateDbContext();
        var pd = new ProcessDefinition { ProcessKey = ProcessKeys.ColdRoll60, ProcessName = "60冷轧", DisplayOrder = 1, IsEnabled = true, IsColdRoll = true };
        ctx.ProcessDefinitions.Add(pd);
        ctx.ColdRollMachineConfigs.Add(new ColdRollMachineConfig { ProcessType = ProcessKeys.ColdRoll60, OwnedCount = 3, MinMachines = 2, MaxMachines = 4 });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.SaveAsync(BuildDto(pd.Id, ProcessKeys.ColdRoll60, "60冷轧", isEnabled: false));

        result.Should().BeTrue();
        ctx.ColdRollMachineConfigs.Should().BeEmpty();
        (await ctx.ProcessDefinitions.FindAsync(pd.Id))!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_禁用冷轧工序_从机台组配置移除该工序()
    {
        using var ctx = CreateDbContext();
        var pd = new ProcessDefinition { ProcessKey = ProcessKeys.ColdRoll60, ProcessName = "60冷轧", DisplayOrder = 1, IsEnabled = true, IsColdRoll = true };
        ctx.ProcessDefinitions.Add(pd);
        ctx.ColdRollMachineGroupConfigs.Add(new ColdRollMachineGroupConfig
        {
            GroupKey = "5060",
            DisplayName = "冷轧5060",
            ProcessKeys = $"{ProcessKeys.ColdRoll50},{ProcessKeys.ColdRoll60}",
            DisplayOrder = 1,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SaveAsync(BuildDto(pd.Id, ProcessKeys.ColdRoll60, "60冷轧", isEnabled: false));

        var group = await ctx.ColdRollMachineGroupConfigs.SingleAsync();
        group.ProcessKeys.Should().Be(ProcessKeys.ColdRoll50);
    }

    [Fact]
    public async Task SaveAsync_禁用工序_组内仅该工序时置空()
    {
        using var ctx = CreateDbContext();
        var pd = new ProcessDefinition { ProcessKey = ProcessKeys.ColdDraw, ProcessName = "冷拔", DisplayOrder = 1, IsEnabled = true, IsColdDraw = true };
        ctx.ProcessDefinitions.Add(pd);
        ctx.ColdRollMachineGroupConfigs.Add(new ColdRollMachineGroupConfig
        {
            GroupKey = "Draw",
            DisplayName = "冷拔",
            ProcessKeys = ProcessKeys.ColdDraw,
            DisplayOrder = 4,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SaveAsync(BuildDto(pd.Id, ProcessKeys.ColdDraw, "冷拔", isEnabled: false, isColdRoll: false, isColdDraw: true));

        var group = await ctx.ColdRollMachineGroupConfigs.SingleAsync();
        group.ProcessKeys.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_保持启用_机台数配置保留()
    {
        using var ctx = CreateDbContext();
        var pd = new ProcessDefinition { ProcessKey = ProcessKeys.ColdRoll60, ProcessName = "60冷轧", DisplayOrder = 1, IsEnabled = true, IsColdRoll = true };
        ctx.ProcessDefinitions.Add(pd);
        ctx.ColdRollMachineConfigs.Add(new ColdRollMachineConfig { ProcessType = ProcessKeys.ColdRoll60, OwnedCount = 3, MinMachines = 2, MaxMachines = 4 });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SaveAsync(BuildDto(pd.Id, ProcessKeys.ColdRoll60, "60冷轧", isEnabled: true));

        ctx.ColdRollMachineConfigs.Should().ContainSingle();
    }
}
