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
/// 冷轧产能配置服务测试：产能档案查询
/// </summary>
public class ColdRollCapacityServiceTests : TestBase
{
    private ColdRollCapacityService CreateService(AppDbContext ctx) => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private static ColdRollCapacity BuildCapacity(string processType, string billetSpec, string rollingSpec, bool isFinished, string? machineNo, decimal? dailyOutput, int sampleCount = 1)
        => new()
        {
            ProcessType = processType,
            BilletSpec = billetSpec,
            RollingSpec = rollingSpec,
            IsFinished = isFinished,
            MachineNo = machineNo,
            DailyOutput = dailyOutput,
            SampleCount = sampleCount,
            LastConfirmedAt = DateTimeOffset.Now,
        };

    [Fact]
    public async Task GetAllAsync_空表_返回空列表()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var all = await svc.GetAllAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_乱序种子_按四维升序返回()
    {
        using var ctx = CreateDbContext();
        ctx.ColdRollCapacities.AddRange(
            new ColdRollCapacity { ProcessType = "ColdRoll60", BilletSpec = "219*8", RollingSpec = "160*6", IsFinished = true, MachineNo = "60-1#", DailyOutput = 50500m, SampleCount = 2, LastConfirmedAt = DateTimeOffset.Now },
            new ColdRollCapacity { ProcessType = "ColdRoll30", BilletSpec = "110*8", RollingSpec = "89*6", IsFinished = false, MachineNo = "30-1#", DailyOutput = 30000m, SampleCount = 1, LastConfirmedAt = DateTimeOffset.Now },
            new ColdRollCapacity { ProcessType = "ColdRoll60", BilletSpec = "219*8", RollingSpec = "160*6", IsFinished = false, MachineNo = "60-1#", DailyOutput = 50500m, SampleCount = 3, LastConfirmedAt = DateTimeOffset.Now });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var all = await svc.GetAllAsync();

        all.Should().HaveCount(3);
        all[0].ProcessType.Should().Be("ColdRoll30");
        all[1].ProcessType.Should().Be("ColdRoll60");
        all[1].IsFinished.Should().BeFalse();
        all[2].ProcessType.Should().Be("ColdRoll60");
        all[2].IsFinished.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_字段映射完整()
    {
        using var ctx = CreateDbContext();
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = "ColdRoll60",
            BilletSpec = "219*8",
            RollingSpec = "160*6",
            IsFinished = false,
            MachineNo = "60-1#",
            DailyOutput = 50500m,
            SampleCount = 4,
            LastConfirmedAt = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero),
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var dto = (await svc.GetAllAsync()).Should().ContainSingle().Subject;
        dto.ProcessType.Should().Be("ColdRoll60");
        dto.BilletSpec.Should().Be("219*8");
        dto.RollingSpec.Should().Be("160*6");
        dto.IsFinished.Should().BeFalse();
        dto.MachineNo.Should().Be("60-1#");
        dto.DailyOutput.Should().Be(50500m);
        dto.SampleCount.Should().Be(4);
        dto.LastConfirmedAt.Should().Be(new DateTime(2026, 8, 22, 10, 0, 0));
        dto.UpdatedTime.Should().NotBe(default);
    }

    // ===== GetPagedAsync 分页/搜索/排序 =====

    [Fact]
    public async Task GetPagedAsync_关键字命中机台号_仅返回匹配行()
    {
        using var ctx = CreateDbContext();
        ctx.ColdRollCapacities.AddRange(
            BuildCapacity("ColdRoll60", "219*8", "160*6", true, "60-1#", 50500m),
            BuildCapacity("ColdRoll60", "219*8", "160*6", false, "60-2#", 48000m));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 10, Keyword = "60-2", SortBy = "processtype", IsDescending = false });

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].MachineNo.Should().Be("60-2#");
    }

    [Fact]
    public async Task GetPagedAsync_默认四维升序返回()
    {
        using var ctx = CreateDbContext();
        ctx.ColdRollCapacities.AddRange(
            BuildCapacity("ColdRoll60", "219*8", "160*6", true, "60-1#", 50500m),
            BuildCapacity("ColdRoll30", "110*8", "89*6", false, "30-1#", 30000m));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 10, SortBy = "processtype", IsDescending = false });

        result.TotalCount.Should().Be(2);
        result.Items[0].ProcessType.Should().Be("ColdRoll30");
        result.Items[1].ProcessType.Should().Be("ColdRoll60");
    }

    [Fact]
    public async Task GetPagedAsync_分页生效()
    {
        using var ctx = CreateDbContext();
        ctx.ColdRollCapacities.AddRange(
            BuildCapacity("ColdRoll60", "219*8", "160*6", true, "60-1#", 50500m),
            BuildCapacity("ColdRoll30", "110*8", "89*6", false, "30-1#", 30000m));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 1, SortBy = "processtype", IsDescending = false });

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(1);
    }

    // ===== SaveAsync 手工调整 + 反向同步 =====

    [Fact]
    public async Task SaveAsync_手工调整_反向同步排程小表已存在维度()
    {
        using var ctx = CreateDbContext();
        var cap = BuildCapacity("ColdRoll60", "219*8", "160*6", false, "60-1#", 50500m, sampleCount: 1);
        ctx.ColdRollCapacities.Add(cap);
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = "ColdRoll60",
            BilletSpec = "219*8",
            RollingSpec = "160*6",
            IsFinished = false,
            MachineNo = "60-1#",
            DailyOutput = 50500m,
            CompletionType = "All",
            RollType = "All",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SaveAsync(new ColdRollCapacityDto { Id = cap.Id, MachineNo = "60-2#", DailyOutput = 60000m });

        var savedCap = ctx.ColdRollCapacities.Single();
        savedCap.MachineNo.Should().Be("60-2#");
        savedCap.DailyOutput.Should().Be(60000m);
        savedCap.SampleCount.Should().Be(2);
        savedCap.LastConfirmedAt.Should().NotBeNull();

        var schedule = ctx.ColdRollSpecSchedules.Single();
        schedule.MachineNo.Should().Be("60-2#");
        schedule.DailyOutput.Should().Be(60000m);
    }

    [Fact]
    public async Task SaveAsync_排程小表无对应维度_不新增小表行()
    {
        using var ctx = CreateDbContext();
        var cap = BuildCapacity("ColdRoll60", "219*8", "160*6", false, "60-1#", 50500m, sampleCount: 1);
        ctx.ColdRollCapacities.Add(cap);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SaveAsync(new ColdRollCapacityDto { Id = cap.Id, MachineNo = "60-2#", DailyOutput = 60000m });

        ctx.ColdRollSpecSchedules.Should().BeEmpty();
        var savedCap = ctx.ColdRollCapacities.Single();
        savedCap.SampleCount.Should().Be(2);
    }

    [Fact]
    public async Task SaveAsync_Id不存在_抛出业务异常()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.SaveAsync(new ColdRollCapacityDto { Id = 999, MachineNo = "60-2#", DailyOutput = 60000m });
        await act.Should().ThrowAsync<BusinessException>();
    }
}
