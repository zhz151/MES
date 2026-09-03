using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;
using MES.Services.Payroll;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 靠工计件月结服务测试（2026-09-03）：
/// 靠工工资 = 靠工岗位当月平均小时工资 × 本人实出勤 × 靠工系数；
/// 平均时薪 = 选中岗位（个人计件 + 集体计件并集）当月计件总工资 ÷ 同批岗位计件人员总出勤（分子分母各自合并）；
/// 靠工/计时写名不进入分子与分母；非计件者出勤不进分母；缺靠工岗位/分母 0 → null + warning；
/// 本人无出勤 → 引擎 0；月结保存 upsert 快照冻结；停用/换岗历史月回溯。
/// </summary>
public class PayrollAttendanceServiceTests : TestBase
{
    // ==================== 种子 Helper ====================

    private static async Task<Employee> SeedEmpAsync(AppDbContext ctx, string code, string name,
        SalaryMode mode, string? position = null, bool active = true,
        string? attendancePositions = null, decimal? attendanceCoefficient = null)
    {
        var e = new Employee
        {
            Code = code,
            Name = name,
            SalaryMode = mode,
            Position = position,
            IsActive = active,
            AttendancePositions = attendancePositions,
            AttendanceCoefficient = attendanceCoefficient
        };
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static async Task SeedAttendanceAsync(AppDbContext ctx, int empId, DateTime date, decimal hours)
    {
        ctx.AttendanceRecords.Add(new AttendanceRecord { EmployeeId = empId, AttendDate = date, WorkHours = hours });
        await ctx.SaveChangesAsync();
    }

    private static async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx)
    {
        var batch = new ProductionBatch
        {
            BatchNo = "BATCH-ATT-" + Guid.NewGuid().ToString("N")[..8],
            MaterialName = "不锈钢管",
            PlantGrade = "304",
            Specification = "60*5",
            Status = BatchStatus.InProgress,
            ProductionType = "Internal",
            ManufacturingItem = "OrderFinished",
            WorkOrderNo = "WO-1",
            SalesOrderNo = "SO-1",
            ProductionMainNo = "M-1",
            OrderItemIds = "1",
            Salesman = "张三",
            SettlementMethod = "Weighing",
            StandardCode = "GB/T 14976",
            DeliveryState = "Hard",
            LengthStatus = "NonFixed",
            TechnicalRequirements = "无",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            TotalQuantity = 100,
            TotalMeters = 1000m,
            TotalWeight = 5000m,
            TotalItemCount = 1
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    /// <summary>计件单价：冷轧拉段 每吨 10 元（2000kg → 20 元）</summary>
    private static async Task SeedRateAsync(AppDbContext ctx)
    {
        ctx.PieceRateProductionCategories.Add(new PieceRateProductionCategory
        {
            SectionKey = SectionKeys.ColdRollDraw,
            BasePrice = 10m,
            Unit = PieceRateUnitKeys.PerTon,
            IsActive = true,
            ConstraintKeys = new List<PieceRateProductionCategoryKey>(),
            Tiers = new List<PieceRateProductionCategoryTier>()
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedProdRecordAsync(AppDbContext ctx, ProductionBatch batch,
        string operatorText, decimal weightKg, int day = 10)
    {
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.ColdRoll60,
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = new DateTime(2024, 3, day),
            Operator = operatorText,
            ProductStatus = ProductStatuses.InProgress,
            Weight = weightKg,
            Quantity = null
        });
        await ctx.SaveChangesAsync();
    }

    private static AttendanceWageRowDto RowOf(AttendanceWageMonthDto month, string code)
        => month.Rows.Single(r => r.EmployeeCode == code);

    // ==================== 平均时薪与引擎月得 ====================

    [Fact]
    public async Task GetMonthAsync_选中岗位单岗_平均时薪等于岗计件总额除计件人员总出勤()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollAttendanceService(ctx);

        // 计件人员 P：个人计件 P1 + 集体计件 P2 同岗 AcidWashing，各出勤 100h
        var p1 = await SeedEmpAsync(ctx, "P001", "计件一", SalaryMode.PieceIndividual, "AcidWashing");
        var p2 = await SeedEmpAsync(ctx, "P002", "计件二", SalaryMode.PieceCollective, "AcidWashing");
        await SeedAttendanceAsync(ctx, p1.Id, new DateTime(2024, 3, 1), 100m);
        await SeedAttendanceAsync(ctx, p2.Id, new DateTime(2024, 3, 1), 100m);
        var batch = await SeedBatchAsync(ctx);
        await SeedRateAsync(ctx);
        // 一单行 P1、P2 同写名：TotalHeadcount=2，行额 20 → 份额各 10 → 岗位计件总额 20
        await SeedProdRecordAsync(ctx, batch, $"{OperatorNameHelper.Format("计件一", "P001")}、{OperatorNameHelper.Format("计件二", "P002")}", 2000m);

        // 靠工员工 X：岗位 AcidWashing、系数 1.5、本人出勤 80h
        await SeedEmpAsync(ctx, "X001", "靠工甲", SalaryMode.PieceAttendance, "AcidWashing", attendancePositions: "AcidWashing", attendanceCoefficient: 1.5m);
        await SeedAttendanceAsync(ctx, (await ctx.Employees.SingleAsync(e => e.Code == "X001")).Id, new DateTime(2024, 3, 2), 80m);

        var month = await svc.GetMonthAsync(2024, 3);
        month.Rows.Should().ContainSingle(r => r.EmployeeCode == "X001");
        var row = RowOf(month, "X001");
        row.EngineCovered.Should().BeTrue();
        row.AttendanceHours.Should().Be(80m);
        row.AttendanceCoefficient.Should().Be(1.5m);
        // avg = 20 ÷ 200 = 0.1 元/时；engine = 0.1 × 80 × 1.5 = 12
        row.AvgHourlyWage.Should().Be(0.1m);
        row.EngineAmount.Should().Be(12m);
        month.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMonthAsync_一人多岗_分子分母各自合并成一个总平均不逐岗重复计酬()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollAttendanceService(ctx);

        // AcidWashing：P1/P2 各出勤 100h，一单行 20 → positionPay 20（同前例）
        var p1 = await SeedEmpAsync(ctx, "P001", "计件一", SalaryMode.PieceIndividual, "AcidWashing");
        var p2 = await SeedEmpAsync(ctx, "P002", "计件二", SalaryMode.PieceCollective, "AcidWashing");
        await SeedAttendanceAsync(ctx, p1.Id, new DateTime(2024, 3, 1), 100m);
        await SeedAttendanceAsync(ctx, p2.Id, new DateTime(2024, 3, 1), 100m);
        // Grinding：P3 个人出勤 100h，单写一单行 3000kg → 30
        var p3 = await SeedEmpAsync(ctx, "P003", "计件三", SalaryMode.PieceIndividual, "Grinding");
        await SeedAttendanceAsync(ctx, p3.Id, new DateTime(2024, 3, 1), 100m);
        var batch = await SeedBatchAsync(ctx);
        await SeedRateAsync(ctx);
        await SeedProdRecordAsync(ctx, batch, $"{OperatorNameHelper.Format("计件一", "P001")}、{OperatorNameHelper.Format("计件二", "P002")}", 2000m);
        await SeedProdRecordAsync(ctx, batch, OperatorNameHelper.Format("计件三", "P003"), 3000m);

        var xId = (await SeedEmpAsync(ctx, "X001", "靠工甲", SalaryMode.PieceAttendance, null,
            attendancePositions: "AcidWashing,Grinding", attendanceCoefficient: 1.0m)).Id;
        await SeedAttendanceAsync(ctx, xId, new DateTime(2024, 3, 2), 60m);

        var month = await svc.GetMonthAsync(2024, 3);
        var row = RowOf(month, "X001");
        // avg = (20+30)÷(200+100) = 50/300；engine = 50/300×60 ≈ 10（合并单时薪，非逐岗 0.1×60 + 0.3×60=24）
        row.AvgHourlyWage.Should().BeApproximately(50m / 300m, 0.000001m);
        row.EngineAmount.Should().BeApproximately(10m, 0.000001m);
    }

    [Fact]
    public async Task GetMonthAsync_个人与集体并集都计入岗位计件总额_同岗非计件与靠工出勤不进分母()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollAttendanceService(ctx);

        // 靠工 X 与 个人计件 P1 同写一行：TotalHeadcount=2，行额 20 → P1 份额 10（X 写名占人头但不进分子）
        var p1 = await SeedEmpAsync(ctx, "P001", "计件一", SalaryMode.PieceIndividual, "AcidWashing");
        var y = await SeedEmpAsync(ctx, "T001", "计时员", SalaryMode.Hourly, "AcidWashing"); // 计时员工有同岗但不在计件集 P
        var xId = (await SeedEmpAsync(ctx, "X001", "靠工甲", SalaryMode.PieceAttendance, "AcidWashing",
            attendancePositions: "AcidWashing", attendanceCoefficient: 1.0m)).Id;
        var batch = await SeedBatchAsync(ctx);
        await SeedRateAsync(ctx);
        await SeedProdRecordAsync(ctx, batch, $"{OperatorNameHelper.Format("计件一", "P001")}、{OperatorNameHelper.Format("靠工甲", "X001")}", 2000m);

        // 出勤：P1 100h；计时 Y 888h、靠工 X 本人 999h——均不得进岗位计件人员出勤分母
        await SeedAttendanceAsync(ctx, p1.Id, new DateTime(2024, 3, 1), 100m);
        await SeedAttendanceAsync(ctx, y.Id, new DateTime(2024, 3, 1), 888m);
        await SeedAttendanceAsync(ctx, xId, new DateTime(2024, 3, 2), 999m);

        var month = await svc.GetMonthAsync(2024, 3);
        var row = RowOf(month, "X001");
        // positionPay[AcidWashing]=10（X 的份额不入池）、positionHours[AcidWashing]=P1 100h → avg 0.1
        row.AvgHourlyWage.Should().Be(0.1m);
        // engine = 0.1 × X 本人出勤 999 × 1.0 = 99.9
        row.AttendanceHours.Should().Be(999m);
        row.EngineAmount.Should().BeApproximately(99.9m, 0.000001m);
    }

    [Fact]
    public async Task GetMonthAsync_缺靠工岗位_平均时薪空加提示_分母零仅行级不整页()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollAttendanceService(ctx);

        // 靠工 Z 未设靠工岗位
        await SeedEmpAsync(ctx, "Z001", "未配岗", SalaryMode.PieceAttendance, "AcidWashing", attendancePositions: null);
        // 靠工 Q 设了岗位但该岗当月无计件人员出勤（无 P 或 P 无出勤）
        await SeedEmpAsync(ctx, "Q001", "无参照", SalaryMode.PieceAttendance, null, attendancePositions: "Cutting", attendanceCoefficient: 1.0m);

        var month = await svc.GetMonthAsync(2024, 3);
        var z = RowOf(month, "Z001");
        z.AvgHourlyWage.Should().BeNull();
        z.EngineAmount.Should().BeNull();
        var q = RowOf(month, "Q001");
        q.AvgHourlyWage.Should().BeNull();
        q.EngineAmount.Should().BeNull();
        month.Warnings.Should().Contain(w => w.Contains("Z001") && w.Contains("未设置靠工岗位"));
        // 分母 0 不再产生整页提醒（用户要求去掉该条描述），仅行级「当月无计件参照」备注
        month.Warnings.Should().NotContain(w => w.Contains("无计件人员出勤"));
    }

    [Fact]
    public async Task GetMonthAsync_有参照时薪但本人无出勤_引擎额为零()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollAttendanceService(ctx);

        var p1 = await SeedEmpAsync(ctx, "P001", "计件一", SalaryMode.PieceIndividual, "AcidWashing");
        await SeedAttendanceAsync(ctx, p1.Id, new DateTime(2024, 3, 1), 100m);
        var batch = await SeedBatchAsync(ctx);
        await SeedRateAsync(ctx);
        await SeedProdRecordAsync(ctx, batch, OperatorNameHelper.Format("计件一", "P001"), 2000m);

        // 靠工 X 有岗位、有 avg 参照，但本人当月无任何出勤
        await SeedEmpAsync(ctx, "X001", "靠工甲", SalaryMode.PieceAttendance, "AcidWashing",
            attendancePositions: "AcidWashing", attendanceCoefficient: 1.0m);

        var month = await svc.GetMonthAsync(2024, 3);
        var row = RowOf(month, "X001");
        row.AvgHourlyWage.Should().Be(0.2m); // 20 ÷ 100
        row.AttendanceHours.Should().BeNull();
        row.EngineAmount.Should().Be(0m);
    }

    // ==================== 月结保存（快照冻结） ====================

    [Fact]
    public async Task SaveMonth_金额大于零存小于等于零删_快照冻结靠工岗位系数出勤()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollAttendanceService(ctx);

        var x = await SeedEmpAsync(ctx, "X001", "靠工甲", SalaryMode.PieceAttendance, "AcidWashing",
            attendancePositions: "AcidWashing", attendanceCoefficient: 1.5m);
        await SeedAttendanceAsync(ctx, x.Id, new DateTime(2024, 3, 2), 60m);
        var y = await SeedEmpAsync(ctx, "Y001", "靠工乙", SalaryMode.PieceAttendance, "AcidWashing", attendancePositions: "AcidWashing");

        await svc.SaveMonthAsync(new SaveAttendanceWageDto
        {
            Year = 2024,
            Month = 3,
            Entries =
            [
                new AttendanceWageEntryDto { EmployeeId = x.Id, Amount = 100m },
                new AttendanceWageEntryDto { EmployeeId = y.Id, Amount = null }
            ]
        });

        var saved = await ctx.PayrollAttendanceWageRecords.ToListAsync();
        saved.Should().ContainSingle();
        var rec = saved.Single();
        rec.EmployeeId.Should().Be(x.Id);
        rec.Amount.Should().Be(100m);
        rec.AttendancePositions.Should().Be("AcidWashing"); // 靠工岗位快照
        rec.AttendanceHours.Should().Be(60m);               // 出勤快照
        rec.AttendanceCoefficient.Should().Be(1.5m);        // 系数快照

        // 第二遍：X 改 150、Y 新增 50
        await svc.SaveMonthAsync(new SaveAttendanceWageDto
        {
            Year = 2024,
            Month = 3,
            Entries =
            [
                new AttendanceWageEntryDto { EmployeeId = x.Id, Amount = 150m },
                new AttendanceWageEntryDto { EmployeeId = y.Id, Amount = 50m }
            ]
        });
        var after = await ctx.PayrollAttendanceWageRecords.OrderBy(r => r.EmployeeId).ToListAsync();
        after.Should().HaveCount(2);
        after.First(r => r.EmployeeId == x.Id).Amount.Should().Be(150m);
        after.First(r => r.EmployeeId == y.Id).Amount.Should().Be(50m);
    }

    [Fact]
    public async Task SaveMonth_已存记录更新仅改金额_快照不随档案与出勤变动漂移()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollAttendanceService(ctx);

        var x = await SeedEmpAsync(ctx, "X001", "靠工甲", SalaryMode.PieceAttendance, "AcidWashing",
            attendancePositions: "AcidWashing", attendanceCoefficient: 1.5m);
        await SeedAttendanceAsync(ctx, x.Id, new DateTime(2024, 3, 2), 60m);
        await svc.SaveMonthAsync(new SaveAttendanceWageDto
        {
            Year = 2024,
            Month = 3,
            Entries = [new AttendanceWageEntryDto { EmployeeId = x.Id, Amount = 100m }]
        });

        // 换岗/改系数/补出勤：更新路径只改金额，快照冻结不变
        x.AttendancePositions = "Grinding";
        x.AttendanceCoefficient = 2.0m;
        await ctx.SaveChangesAsync();
        await SeedAttendanceAsync(ctx, x.Id, new DateTime(2024, 3, 20), 40m);

        await svc.SaveMonthAsync(new SaveAttendanceWageDto
        {
            Year = 2024,
            Month = 3,
            Entries = [new AttendanceWageEntryDto { EmployeeId = x.Id, Amount = 200m }]
        });

        var rec = await ctx.PayrollAttendanceWageRecords.SingleAsync();
        rec.Amount.Should().Be(200m);
        rec.AttendancePositions.Should().Be("AcidWashing");
        rec.AttendanceCoefficient.Should().Be(1.5m);
        rec.AttendanceHours.Should().Be(60m);
    }

    [Fact]
    public async Task GetMonth_停用后历史月仍显示_引擎不覆盖快照保留()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollAttendanceService(ctx);

        var x = await SeedEmpAsync(ctx, "X001", "靠工甲", SalaryMode.PieceAttendance, "AcidWashing",
            attendancePositions: "AcidWashing", attendanceCoefficient: 1.5m);
        await SeedAttendanceAsync(ctx, x.Id, new DateTime(2024, 3, 2), 60m);
        await svc.SaveMonthAsync(new SaveAttendanceWageDto
        {
            Year = 2024,
            Month = 3,
            Entries = [new AttendanceWageEntryDto { EmployeeId = x.Id, Amount = 100m }]
        });

        // 员工停用（不再是当前在册靠工员工）→ 历史月仍须按快照显示可回溯
        x.IsActive = false;
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 3);
        month.HasSaved.Should().BeTrue();
        var row = RowOf(month, "X001");
        row.EngineCovered.Should().BeFalse();
        row.SavedAmount.Should().Be(100m);
        row.EngineAmount.Should().BeNull();
        row.AvgHourlyWage.Should().BeNull();
        row.AttendancePositions.Should().Be("AcidWashing");
        row.AttendanceCoefficient.Should().Be(1.5m);
        row.AttendanceHours.Should().Be(60m);
    }

    [Fact]
    public async Task GetMonth_未配岗的在册靠工员工也列出_仅历史快照补集可另见()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollAttendanceService(ctx);

        // 未配岗的在册靠工员工（提示补岗）也应带出
        await SeedEmpAsync(ctx, "Z001", "未配岗", SalaryMode.PieceAttendance, "AcidWashing", attendancePositions: null);

        var month = await svc.GetMonthAsync(2024, 3);
        var row = RowOf(month, "Z001");
        row.EngineCovered.Should().BeTrue();
        row.AttendancePositions.Should().BeNull();
        row.EngineAmount.Should().BeNull();
    }
}
