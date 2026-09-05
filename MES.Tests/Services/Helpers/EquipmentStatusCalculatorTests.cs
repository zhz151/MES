using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities.Equipment;
using MES.Services.Helpers;
using MES.Tests.Tests;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// 设备物化状态计算器 EquipmentStatusCalculator 测试（基于 InMemory + 行版本覆盖 TestBase）：
/// RunningStatus（无维修/维修中/已结束/无起止/取最新单）与 Inspection/Maint 任务状态机
/// （NotApplicable / Pending / Normal(周期内记录·含闭界) / Overdue(超期无记录)）。
/// 4 个公开入口（全量/仅点检/仅保养/仅维修）至少覆盖一次。
/// </summary>
public class EquipmentStatusCalculatorTests : TestBase
{
    private static async Task<Equipment> SeedEquipmentAsync(AppDbContext ctx,
        bool needInspection = false, DateTime? inspectStart = null, int cycle = 7,
        bool needMaint = false, DateTime? maintStart = null, int maintCycle = 30)
    {
        var e = new Equipment
        {
            EquipmentCode = "EQ001",
            EquipmentName = "测试设备",
            Location = "车间A",
            LifecycleStatus = "Active",
            UsageType = "Primary",
            NeedInspection = needInspection,
            CurrentInspectionStartDate = inspectStart,
            InspectionCycleDays = cycle,
            NeedMaintenance = needMaint,
            CurrentMaintStartDate = maintStart,
            MaintCycleDays = maintCycle
        };
        ctx.Equipment.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static async Task SeedRepairAsync(AppDbContext ctx, int equipmentId, string no,
        DateTime? start = null, DateTime? end = null)
    {
        ctx.RepairOrders.Add(new RepairOrder
        {
            RepairOrderNo = no,
            EquipmentId = equipmentId,
            RepairStartTime = start,
            RepairEndTime = end,
            FaultDescription = "异响",
            ReportPerson = "王五",
            RepairStatus = "Pending"
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedInspectionAsync(AppDbContext ctx, int equipmentId, DateTime date)
    {
        ctx.InspectionRecords.Add(new InspectionRecord
        {
            RecordNo = "DJ-" + Guid.NewGuid().ToString("N")[..8],
            EquipmentId = equipmentId,
            ActualDate = date,
            Inspector = "测试员",
            ExecutionSummary = "点检"
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedMaintAsync(AppDbContext ctx, int equipmentId, DateTime date)
    {
        ctx.MaintenanceOrders.Add(new MaintenanceOrder
        {
            MaintOrderNo = "BY-" + Guid.NewGuid().ToString("N")[..8],
            EquipmentId = equipmentId,
            ActualDate = date,
            Executor = "张三",
            ExecutionSummary = "保养"
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task<Equipment> ReloadAsync(AppDbContext ctx, int id)
        => await ctx.Equipment.AsNoTracking().SingleAsync(e => e.Id == id);

    // ========== RunningStatus ==========

    [Fact]
    public async Task Running_无维修记录_返回Normal()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);

        await EquipmentStatusCalculator.RecalculateRunningStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).RunningStatus.Should().Be(nameof(RunningStatus.Normal));
    }

    [Fact]
    public async Task Running_维修中有开始无结束_返回InProgress()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedRepairAsync(ctx, eq.Id, "WX-001", start: DateTime.Today);

        await EquipmentStatusCalculator.RecalculateRunningStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).RunningStatus.Should().Be(nameof(RunningStatus.InProgress));
    }

    [Fact]
    public async Task Running_维修已结束_返回Normal()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedRepairAsync(ctx, eq.Id, "WX-001",
            start: DateTime.Today.AddDays(-1), end: DateTime.Today);

        await EquipmentStatusCalculator.RecalculateRunningStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).RunningStatus.Should().Be(nameof(RunningStatus.Normal));
    }

    [Fact]
    public async Task Running_维修单无起止时间_返回Pending()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedRepairAsync(ctx, eq.Id, "WX-001");

        await EquipmentStatusCalculator.RecalculateRunningStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).RunningStatus.Should().Be(nameof(RunningStatus.Pending));
    }

    [Fact]
    public async Task Running_多维修单_取最新单据决定状态()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedRepairAsync(ctx, eq.Id, "WX-已结束",
            start: DateTime.Today.AddDays(-2), end: DateTime.Today.AddDays(-2));
        await SeedRepairAsync(ctx, eq.Id, "WX-维修中", start: DateTime.Today); // 最新，无结束

        await EquipmentStatusCalculator.RecalculateRunningStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).RunningStatus.Should().Be(nameof(RunningStatus.InProgress));
    }

    // ========== InspectionStatus 状态机 ==========

    [Fact]
    public async Task Inspection_无需点检_返回NotApplicable()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx, needInspection: false);

        await EquipmentStatusCalculator.RecalculateInspectionStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).InspectionStatus
            .Should().Be(nameof(EquipmentTaskStatus.NotApplicable));
    }

    [Fact]
    public async Task Inspection_需点检但无起始日_返回Pending()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx, needInspection: true, inspectStart: null);

        await EquipmentStatusCalculator.RecalculateInspectionStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).InspectionStatus.Should().Be(nameof(EquipmentTaskStatus.Pending));
    }

    [Fact]
    public async Task Inspection_起始日未到_返回Normal()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx, needInspection: true, inspectStart: DateTime.Today.AddDays(5));

        await EquipmentStatusCalculator.RecalculateInspectionStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).InspectionStatus.Should().Be(nameof(EquipmentTaskStatus.Normal));
    }

    [Fact]
    public async Task Inspection_周期末日记点_闭界命中返回Normal()
    {
        var ctx = CreateDbContext();
        // 起始日 = 今天-6，周期 7 → 周期末 = 起始日+6 = 今天；今日点检恰在闭界内
        var eq = await SeedEquipmentAsync(ctx, needInspection: true,
            inspectStart: DateTime.Today.AddDays(-6), cycle: 7);
        await SeedInspectionAsync(ctx, eq.Id, DateTime.Today);

        await EquipmentStatusCalculator.RecalculateInspectionStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).InspectionStatus.Should().Be(nameof(EquipmentTaskStatus.Normal));
    }

    [Fact]
    public async Task Inspection_超期无周期内记录_返回Overdue()
    {
        var ctx = CreateDbContext();
        // 起始日 = 今天-10，周期 7 → 周期末 = 今天-4；记录在 -3（周期外）不应命中
        var eq = await SeedEquipmentAsync(ctx, needInspection: true,
            inspectStart: DateTime.Today.AddDays(-10), cycle: 7);
        await SeedInspectionAsync(ctx, eq.Id, DateTime.Today.AddDays(-3));

        await EquipmentStatusCalculator.RecalculateInspectionStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).InspectionStatus.Should().Be(nameof(EquipmentTaskStatus.Overdue));
    }

    [Fact]
    public async Task Inspection_周期内无记录且未超期_返回Pending()
    {
        var ctx = CreateDbContext();
        // 起始日 = 今天-1，周期 7 → 周期末 = 今天+5；今日在周期内但无记录 → Pending
        var eq = await SeedEquipmentAsync(ctx, needInspection: true,
            inspectStart: DateTime.Today.AddDays(-1), cycle: 7);

        await EquipmentStatusCalculator.RecalculateInspectionStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).InspectionStatus.Should().Be(nameof(EquipmentTaskStatus.Pending));
    }

    // ========== MaintStatus ==========

    [Fact]
    public async Task Maint_保养周期内记录_返回Normal()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx, needMaint: true,
            maintStart: DateTime.Today.AddDays(-5), maintCycle: 30);
        await SeedMaintAsync(ctx, eq.Id, DateTime.Today);

        await EquipmentStatusCalculator.RecalculateMaintStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).MaintStatus.Should().Be(nameof(EquipmentTaskStatus.Normal));
    }

    [Fact]
    public async Task Maint_超期无周期内记录_返回Overdue()
    {
        var ctx = CreateDbContext();
        // 起始日 = 今天-40，周期 30 → 周期末 = 今天-11；记录在 -10（周期外）不应命中
        var eq = await SeedEquipmentAsync(ctx, needMaint: true,
            maintStart: DateTime.Today.AddDays(-40), maintCycle: 30);
        await SeedMaintAsync(ctx, eq.Id, DateTime.Today.AddDays(-10));

        await EquipmentStatusCalculator.RecalculateMaintStatusAsync(ctx, eq.Id);

        (await ReloadAsync(ctx, eq.Id)).MaintStatus.Should().Be(nameof(EquipmentTaskStatus.Overdue));
    }

    // ========== RecalculateAndSaveAsync 全量入口 ==========

    [Fact]
    public async Task RecalculateAndSave_无任何单据_三状态全落库()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx); // 无需点检/保养、无维修

        await EquipmentStatusCalculator.RecalculateAndSaveAsync(ctx, eq.Id);

        var after = await ReloadAsync(ctx, eq.Id);
        after.RunningStatus.Should().Be(nameof(RunningStatus.Normal));
        after.InspectionStatus.Should().Be(nameof(EquipmentTaskStatus.NotApplicable));
        after.MaintStatus.Should().Be(nameof(EquipmentTaskStatus.NotApplicable));
    }

    [Fact]
    public async Task RecalculateAndSave_需点检超期且需保养在期内_全量三状态正确()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx,
            needInspection: true, inspectStart: DateTime.Today.AddDays(-10), cycle: 7,   // 周期末 = 今天-4 → Overdue
            needMaint: true, maintStart: DateTime.Today.AddDays(-5), maintCycle: 30);     // 周期内
        await SeedMaintAsync(ctx, eq.Id, DateTime.Today);

        await EquipmentStatusCalculator.RecalculateAndSaveAsync(ctx, eq.Id);

        var after = await ReloadAsync(ctx, eq.Id);
        after.InspectionStatus.Should().Be(nameof(EquipmentTaskStatus.Overdue));
        after.MaintStatus.Should().Be(nameof(EquipmentTaskStatus.Normal));
        after.RunningStatus.Should().Be(nameof(RunningStatus.Normal));
    }

    [Fact]
    public async Task RecalculateAndSave_设备不存在_静默返回不抛异常()
    {
        var ctx = CreateDbContext();

        await EquipmentStatusCalculator.RecalculateAndSaveAsync(ctx, 99999);

        // 无异常即通过
    }
}
