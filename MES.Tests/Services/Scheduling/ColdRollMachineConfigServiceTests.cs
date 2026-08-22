using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.DTOs.Scheduling;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Scheduling;
using MES.Services.Scheduling;
using MES.Tests.Tests;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 冷轧机台数配置服务测试：机台数参数表查询与维护
/// </summary>
public class ColdRollMachineConfigServiceTests : TestBase
{
    private ColdRollMachineConfigService CreateService(AppDbContext ctx) => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private static ColdRollMachineConfig BuildConfig(string processType, int ownedCount, int minMachines, int maxMachines, decimal? estimatedDailyOutput = null, string? remark = null)
        => new()
        {
            ProcessType = processType,
            OwnedCount = ownedCount,
            MinMachines = minMachines,
            MaxMachines = maxMachines,
            EstimatedDailyOutput = estimatedDailyOutput,
            Remark = remark,
        };

    // ===== GetAllAsync =====

    [Fact]
    public async Task GetAllAsync_空表_返回空列表()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var all = await svc.GetAllAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_乱序种子_按机型升序返回()
    {
        using var ctx = CreateDbContext();
        ctx.ColdRollMachineConfigs.AddRange(
            BuildConfig("ColdRoll60", 3, 2, 4, 50000m),
            BuildConfig("ColdRoll20", 2, 1, 3, 25000m),
            BuildConfig("ColdRoll50", 2, 1, 3, 40000m));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var all = await svc.GetAllAsync();

        all.Should().HaveCount(3);
        all[0].ProcessType.Should().Be("ColdRoll20");
        all[1].ProcessType.Should().Be("ColdRoll50");
        all[2].ProcessType.Should().Be("ColdRoll60");
    }

    [Fact]
    public async Task GetAllAsync_字段映射完整()
    {
        using var ctx = CreateDbContext();
        ctx.ColdRollMachineConfigs.Add(BuildConfig("ColdRoll60", 3, 2, 4, 50500m, "60 线"));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var dto = (await svc.GetAllAsync()).Should().ContainSingle().Subject;
        dto.ProcessType.Should().Be("ColdRoll60");
        dto.OwnedCount.Should().Be(3);
        dto.MinMachines.Should().Be(2);
        dto.MaxMachines.Should().Be(4);
        dto.EstimatedDailyOutput.Should().Be(50500m);
        dto.Remark.Should().Be("60 线");
        dto.UpdatedTime.Should().NotBe(default);
    }

    // ===== GetPagedAsync =====

    [Fact]
    public async Task GetPagedAsync_关键字命中备注_仅返回匹配行()
    {
        using var ctx = CreateDbContext();
        ctx.ColdRollMachineConfigs.AddRange(
            BuildConfig("ColdRoll60", 3, 2, 4, 50000m, "主用"),
            BuildConfig("ColdRoll30", 2, 1, 3, 30000m, "备用"));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 10, Keyword = "主用", SortBy = "processtype", IsDescending = false });

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].ProcessType.Should().Be("ColdRoll60");
    }

    [Fact]
    public async Task GetPagedAsync_默认机型升序返回()
    {
        using var ctx = CreateDbContext();
        ctx.ColdRollMachineConfigs.AddRange(
            BuildConfig("ColdRoll60", 3, 2, 4, 50000m),
            BuildConfig("ColdRoll20", 2, 1, 3, 25000m));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 10, SortBy = "processtype", IsDescending = false });

        result.TotalCount.Should().Be(2);
        result.Items[0].ProcessType.Should().Be("ColdRoll20");
        result.Items[1].ProcessType.Should().Be("ColdRoll60");
    }

    [Fact]
    public async Task GetPagedAsync_分页生效()
    {
        using var ctx = CreateDbContext();
        ctx.ColdRollMachineConfigs.AddRange(
            BuildConfig("ColdRoll60", 3, 2, 4, 50000m),
            BuildConfig("ColdRoll20", 2, 1, 3, 25000m));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 1, SortBy = "processtype", IsDescending = false });

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(1);
    }

    // ===== SaveAsync =====

    [Fact]
    public async Task SaveAsync_新增_保存成功()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.SaveAsync(new ColdRollMachineConfigDto
        {
            Id = 0,
            ProcessType = "ColdRoll60",
            OwnedCount = 3,
            MinMachines = 2,
            MaxMachines = 4,
            EstimatedDailyOutput = 50500m,
            Remark = "主用",
        });

        var entity = ctx.ColdRollMachineConfigs.Single();
        entity.ProcessType.Should().Be("ColdRoll60");
        entity.OwnedCount.Should().Be(3);
        entity.MinMachines.Should().Be(2);
        entity.MaxMachines.Should().Be(4);
        entity.EstimatedDailyOutput.Should().Be(50500m);
        entity.Remark.Should().Be("主用");
    }

    [Fact]
    public async Task SaveAsync_更新_覆盖字段()
    {
        using var ctx = CreateDbContext();
        var cfg = BuildConfig("ColdRoll60", 3, 2, 4, 50500m);
        ctx.ColdRollMachineConfigs.Add(cfg);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SaveAsync(new ColdRollMachineConfigDto
        {
            Id = cfg.Id,
            ProcessType = "ColdRoll60",
            OwnedCount = 4,
            MinMachines = 3,
            MaxMachines = 5,
            EstimatedDailyOutput = 60000m,
            Remark = "更新",
        });

        var entity = ctx.ColdRollMachineConfigs.Single();
        entity.OwnedCount.Should().Be(4);
        entity.MinMachines.Should().Be(3);
        entity.MaxMachines.Should().Be(5);
        entity.EstimatedDailyOutput.Should().Be(60000m);
        entity.Remark.Should().Be("更新");
    }

    [Fact]
    public async Task SaveAsync_重复机型_抛出业务异常()
    {
        using var ctx = CreateDbContext();
        ctx.ColdRollMachineConfigs.Add(BuildConfig("ColdRoll60", 3, 2, 4, 50500m));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var act = async () => await svc.SaveAsync(new ColdRollMachineConfigDto
        { Id = 0, ProcessType = "ColdRoll60", OwnedCount = 1, MinMachines = 1, MaxMachines = 2 });
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task SaveAsync_最小大于最大_抛出业务异常()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.SaveAsync(new ColdRollMachineConfigDto
        { Id = 0, ProcessType = "ColdRoll60", OwnedCount = 3, MinMachines = 5, MaxMachines = 4 });
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task SaveAsync_机型为空_抛出业务异常()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.SaveAsync(new ColdRollMachineConfigDto
        { Id = 0, ProcessType = "  ", OwnedCount = 3, MinMachines = 2, MaxMachines = 4 });
        await act.Should().ThrowAsync<BusinessException>();
    }

    // ===== DeleteAsync =====

    [Fact]
    public async Task DeleteAsync_删除成功()
    {
        using var ctx = CreateDbContext();
        var cfg = BuildConfig("ColdRoll60", 3, 2, 4, 50500m);
        ctx.ColdRollMachineConfigs.Add(cfg);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.DeleteAsync(cfg.Id);

        ctx.ColdRollMachineConfigs.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_Id不存在_抛出业务异常()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>();
    }
}
