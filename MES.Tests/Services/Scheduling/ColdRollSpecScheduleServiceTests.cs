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

    private ColdRollSpecScheduleDto BuildDto(decimal? dailyOutput = null, string? machineNo = "60-1#")
    {
        return new ColdRollSpecScheduleDto
        {
            ProcessType = ProcessKeys.ColdRoll60,
            BilletSpec = "219*8",
            RollingSpec = "160*6",
            IsFinished = false,
            MachineNo = machineNo,
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

    // ===== 产能档案反哺（SaveAllAsync → ColdRollCapacity）=====

    [Fact]
    public async Task SaveAllAsync_有产能行_反哺新增产能档案()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = BuildDto(dailyOutput: 50500m);
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { dto });

        var cap = ctx.ColdRollCapacities.Should().ContainSingle().Subject;
        cap.ProcessType.Should().Be(ProcessKeys.ColdRoll60);
        cap.BilletSpec.Should().Be("219*8");
        cap.RollingSpec.Should().Be("160*6");
        cap.IsFinished.Should().BeFalse();
        cap.MachineNo.Should().Be("60-1#");
        cap.DailyOutput.Should().Be(50500m);
        cap.SampleCount.Should().Be(1);
        cap.LastConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAllAsync_同维度二次保存_反哺覆盖并累计样本()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = BuildDto(dailyOutput: 50500m);
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { dto });

        dto.DailyOutput = 60000m;
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { dto });

        var cap = ctx.ColdRollCapacities.Should().ContainSingle().Subject;
        cap.DailyOutput.Should().Be(60000m);
        cap.SampleCount.Should().Be(2);
    }

    [Fact]
    public async Task SaveAllAsync_无产能信息_不反哺()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = BuildDto(dailyOutput: null, machineNo: null);
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { dto });
        ctx.ColdRollCapacities.Should().BeEmpty();

        // 纯空白机台同样视为无产能
        var blank = BuildDto(dailyOutput: null, machineNo: "  ");
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { blank });
        ctx.ColdRollCapacities.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAllAsync_小表僵尸维度删除_产能档案保留()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var a = BuildDto(dailyOutput: 50500m);
        var b = BuildDto(dailyOutput: 60000m);
        b.RollingSpec = "150*6";

        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { a, b });
        ctx.ColdRollCapacities.Should().HaveCount(2);

        // 仅再存 a → b 成小表僵尸被删，但产能档案保留（累积不删除）
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { a });
        ctx.ColdRollSpecSchedules.Should().ContainSingle();
        ctx.ColdRollCapacities.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveAllAsync_有产能行清空再存_不覆盖不清除()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = BuildDto(dailyOutput: 50500m);
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { dto });
        var first = ctx.ColdRollCapacities.Single();

        dto.DailyOutput = null;
        dto.MachineNo = null;
        await svc.SaveAllAsync(new List<ColdRollSpecScheduleDto> { dto });

        var cap = ctx.ColdRollCapacities.Should().ContainSingle().Subject;
        cap.DailyOutput.Should().Be(50500m);
        cap.SampleCount.Should().Be(1);
        cap.LastConfirmedAt.Should().Be(first.LastConfirmedAt);
    }
}
