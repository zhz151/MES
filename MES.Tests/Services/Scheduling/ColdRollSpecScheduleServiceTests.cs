using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.DTOs.Scheduling;
using MES.Data;
using MES.Services.Scheduling;
using MES.Tests.Tests;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 冷轧排程服务测试：按规格维度的排程决策全量同步保存
/// </summary>
public class ColdRollSpecScheduleServiceTests : TestBase
{
    private ColdRollSpecScheduleService CreateService(AppDbContext ctx) => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private ColdRollSpecScheduleDto BuildDto(decimal? dailyOutput = null)
    {
        return new ColdRollSpecScheduleDto
        {
            ProcessType = ProcessKeys.ColdRoll60,
            BilletSpec = "219*8",
            RollingSpec = "160*6",
            IsFinished = false,
            MachineNo = "60-1#",
            DailyOutput = dailyOutput,
            CompletionType = "All",
            RollType = "All",
        };
    }

    [Fact]
    public async Task SaveAllAsync_保存并读取单机单日量()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = BuildDto(dailyOutput: 50500m);
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { dto });

        var all = await svc.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].DailyOutput.Should().Be(50500m);
        all[0].MachineNo.Should().Be("60-1#");
    }

    [Fact]
    public async Task SaveAllAsync_同维度二次保存_覆盖单机单日量()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = BuildDto(dailyOutput: 50500m);
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { dto });

        dto.DailyOutput = 60000m;
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { dto });

        var all = await svc.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].DailyOutput.Should().Be(60000m);
    }

    [Fact]
    public async Task SaveAllAsync_单机单日量为空_保存null()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = BuildDto(dailyOutput: null);
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { dto });

        var all = await svc.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].DailyOutput.Should().BeNull();
    }
}
