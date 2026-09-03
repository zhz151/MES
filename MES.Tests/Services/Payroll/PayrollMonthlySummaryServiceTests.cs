using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;
using MES.Services.Payroll;
using MES.Tests.Tests;
using QuestPDF.Infrastructure;

namespace MES.Tests.Services;

/// <summary>
/// 「月工资汇总」服务测试（2026-09-04）：
/// 基础工资按薪酬归口取当月已保存金额（Fixed=Employee.MonthlyWage；其余 集体计件快照→靠工计件快照→每日工资Σ 择优）；
/// 出勤天数 = 当月考勤去重日期数；杂辅 Σ；津贴 7 项入列且处罚/代缴源表正数 → 汇总存负；
/// 应发 = 基础+杂辅+7 正津贴（不含扣减）；实发 = 应发 + 处罚 + 代缴；
/// 保存 = 整月重算替换快照（每人每月一行）；打印读快照，未保存抛业务异常。
/// </summary>
public class PayrollMonthlySummaryServiceTests : TestBase
{
    static PayrollMonthlySummaryServiceTests()
    {
        // QuestPDF 社区版许可（测试环境需要手动设置）
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static async Task<Employee> SeedEmpAsync(AppDbContext ctx, string code, string name,
        SalaryMode? mode = null, decimal? monthlyWage = null, bool active = true)
    {
        var e = new Employee { Code = code, Name = name, SalaryMode = mode, MonthlyWage = monthlyWage, IsActive = active };
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    /// <summary>考勤：每人每天唯一（同月同日重复会撞唯一索引，测试勿重复加）</summary>
    private static async Task AddAttendanceAsync(AppDbContext ctx, int empId, int year, int month, int day)
    {
        ctx.AttendanceRecords.Add(new AttendanceRecord { EmployeeId = empId, AttendDate = new DateTime(year, month, day), WorkHours = 8m });
        await ctx.SaveChangesAsync();
    }

    private static async Task AddDailyAsync(AppDbContext ctx, int empId, int year, int month, int day, decimal amount)
    {
        ctx.PayrollDailyWageRecords.Add(new PayrollDailyWageRecord
        {
            EmployeeId = empId,
            WageDate = new DateTime(year, month, day),
            Amount = amount,
            SalaryMode = "NonPiece",
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task AddCollectiveAsync(AppDbContext ctx, int empId, int year, int month, decimal amount)
    {
        ctx.PayrollCollectiveWageRecords.Add(new PayrollCollectiveWageRecord
        {
            EmployeeId = empId,
            WageYear = year,
            WageMonth = month,
            Position = "Collective",
            Amount = amount,
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task AddAttendanceWageAsync(AppDbContext ctx, int empId, int year, int month, decimal amount)
    {
        ctx.PayrollAttendanceWageRecords.Add(new PayrollAttendanceWageRecord
        {
            EmployeeId = empId,
            WageYear = year,
            WageMonth = month,
            Amount = amount,
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task AddMiscAsync(AppDbContext ctx, int empId, int year, int month, int day, decimal amount)
    {
        ctx.PayrollMiscWorkRecords.Add(new PayrollMiscWorkRecord
        {
            EmployeeId = empId,
            WorkDate = new DateTime(year, month, day),
            Content = "杂活",
            Hours = 1m,
            Amount = amount,
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task AddAllowanceAsync(AppDbContext ctx, int empId, int year, int month,
        decimal? pos = null, decimal? sen = null, decimal? full = null, decimal? pen = null, decimal? ss = null)
    {
        ctx.PayrollAllowanceRecords.Add(new PayrollAllowanceRecord
        {
            EmployeeId = empId,
            Year = year,
            Month = month,
            PositionAllowance = pos,
            SeniorityBonus = sen,
            FullAttendanceBonus = full,
            Penalty = pen,
            SocialSecurity = ss,
        });
        await ctx.SaveChangesAsync();
    }

    // ==================== 归口基础工资映射 ====================

    [Fact]
    public async Task GetMonthAsync_Fixed在册取MonthlyWage_杂辅求和_津贴映射处罚代缴取负_应发实发()
    {
        using var ctx = CreateDbContext();
        var emp = await SeedEmpAsync(ctx, "YG001", "钱秋明", SalaryMode.Fixed, 4000m);
        // 杂辅 11 月两条求和；考勤 3 个出勤日；津贴行（处罚/代缴源表正数）
        await AddMiscAsync(ctx, emp.Id, 2026, 11, 5, 1500m);
        await AddMiscAsync(ctx, emp.Id, 2026, 11, 8, 1500m);
        await AddAttendanceAsync(ctx, emp.Id, 2026, 11, 3);
        await AddAttendanceAsync(ctx, emp.Id, 2026, 11, 4);
        await AddAttendanceAsync(ctx, emp.Id, 2026, 11, 5);
        await AddAllowanceAsync(ctx, emp.Id, 2026, 11, pos: 50m, sen: 300m, full: 150m, pen: 50m, ss: 600m);

        var svc = new PayrollMonthlySummaryService(ctx);
        var dto = await svc.GetMonthAsync(2026, 11);

        var row = dto.Rows.Should().ContainSingle(r => r.EmployeeCode == "YG001").Subject;
        row.EmployeeName.Should().Be("钱秋明");
        row.IsActive.Should().BeTrue();
        row.AttendanceDays.Should().Be(3);
        row.BaseWage.Should().Be(4000m);
        row.MiscWorkAmount.Should().Be(3000m);
        row.PositionAllowance.Should().Be(50m);
        row.SeniorityBonus.Should().Be(300m);
        row.FullAttendanceBonus.Should().Be(150m);
        // 未填津贴为 0
        row.NightShiftAllowance.Should().Be(0m);
        row.LeadBonus.Should().Be(0m);
        // 处罚/代缴源表正数 → 汇总存负
        row.Penalty.Should().Be(-50m);
        row.SocialSecurity.Should().Be(-600m);
        // 应发 = 基础+杂辅+7 正津贴；实发 = 应发 + 处罚 + 代缴
        row.TotalPayable.Should().Be(7500m);
        row.TotalPaid.Should().Be(6850m);
    }

    [Fact]
    public async Task GetMonthAsync_非固定_每日工资逐日求和_仅当月不过滤他月()
    {
        using var ctx = CreateDbContext();
        var emp = await SeedEmpAsync(ctx, "A001", "张三", SalaryMode.PieceIndividual);
        await AddDailyAsync(ctx, emp.Id, 2026, 11, 1, 100m);
        await AddDailyAsync(ctx, emp.Id, 2026, 11, 15, 200m);
        await AddDailyAsync(ctx, emp.Id, 2026, 10, 31, 500m); // 上月行不得混入 11 月

        var svc = new PayrollMonthlySummaryService(ctx);
        var dto = await svc.GetMonthAsync(2026, 11);

        dto.Rows.Single(r => r.EmployeeCode == "A001").BaseWage.Should().Be(300m);
    }

    [Fact]
    public async Task GetMonthAsync_集体计件快照优先于靠工与每日_靠工优先于每日()
    {
        using var ctx = CreateDbContext();
        // 集体：同时有集体快照/靠工快照/每日工资 → 取集体快照
        var col = await SeedEmpAsync(ctx, "C001", "集体甲", SalaryMode.PieceCollective);
        await AddCollectiveAsync(ctx, col.Id, 2026, 11, 500m);
        await AddAttendanceWageAsync(ctx, col.Id, 2026, 11, 700m);
        await AddDailyAsync(ctx, col.Id, 2026, 11, 5, 800m);
        // 靠工：有靠工快照 + 每日工资（无集体）→ 取靠工快照
        var att = await SeedEmpAsync(ctx, "D001", "靠工乙", SalaryMode.PieceAttendance);
        await AddAttendanceWageAsync(ctx, att.Id, 2026, 11, 900m);
        await AddDailyAsync(ctx, att.Id, 2026, 11, 5, 600m);

        var svc = new PayrollMonthlySummaryService(ctx);
        var dto = await svc.GetMonthAsync(2026, 11);

        dto.Rows.Single(r => r.EmployeeCode == "C001").BaseWage.Should().Be(500m);
        dto.Rows.Single(r => r.EmployeeCode == "D001").BaseWage.Should().Be(900m);
    }

    // ==================== 行集 / 员工状态 / 排序 / 筛选 ====================

    [Fact]
    public async Task GetMonthAsync_并入当月有源停用员工_无源停用不入_停用行灰显_按工号升序()
    {
        using var ctx = CreateDbContext();
        await SeedEmpAsync(ctx, "A001", "在册甲");
        var inactive = await SeedEmpAsync(ctx, "X099", "停用乙", active: false);
        await AddAllowanceAsync(ctx, inactive.Id, 2026, 11, pos: 100m);
        await SeedEmpAsync(ctx, "Y001", "停用无源", active: false); // 无当月源 → 不入

        var svc = new PayrollMonthlySummaryService(ctx);
        var dto = await svc.GetMonthAsync(2026, 11);

        dto.Rows.Select(r => r.EmployeeCode).Should().BeEquivalentTo(new[] { "A001", "X099" }, options => options.WithStrictOrdering());
        dto.Rows.Single(r => r.EmployeeCode == "A001").IsActive.Should().BeTrue();
        var x = dto.Rows.Single(r => r.EmployeeCode == "X099");
        x.IsActive.Should().BeFalse();
        x.PositionAllowance.Should().Be(100m);
    }

    [Fact]
    public async Task GetMonthAsync_关键词过滤工号姓名_大小写不敏感()
    {
        using var ctx = CreateDbContext();
        var a = await SeedEmpAsync(ctx, "A001", "张三");
        var b = await SeedEmpAsync(ctx, "B002", "李四");
        await AddMiscAsync(ctx, a.Id, 2026, 11, 1, 100m);
        await AddMiscAsync(ctx, b.Id, 2026, 11, 1, 200m);

        var svc = new PayrollMonthlySummaryService(ctx);
        var byName = await svc.GetMonthAsync(2026, 11, "李四");
        byName.Rows.Should().ContainSingle().Which.EmployeeCode.Should().Be("B002");
        var byCodeLower = await svc.GetMonthAsync(2026, 11, "a001");
        byCodeLower.Rows.Should().ContainSingle().Which.EmployeeName.Should().Be("张三");
    }

    [Fact]
    public async Task GetMonthAsync_月份参数无效抛()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMonthlySummaryService(ctx);
        var act = () => svc.GetMonthAsync(1999, 3);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("月份参数无效");
        var act2 = () => svc.GetMonthAsync(2026, 13);
        await act2.Should().ThrowAsync<BusinessException>().WithMessage("月份参数无效");
    }

    // ==================== 保存快照 / 幂等 ====================

    [Fact]
    public async Task SaveMonthAsync_整月替换快照_二次保存不重复_GetMonth_HasSaved()
    {
        using var ctx = CreateDbContext();
        var a = await SeedEmpAsync(ctx, "A001", "张三", SalaryMode.Fixed, 5000m);
        var b = await SeedEmpAsync(ctx, "B002", "李四");
        await AddMiscAsync(ctx, a.Id, 2026, 11, 1, 100m);
        await AddMiscAsync(ctx, b.Id, 2026, 11, 2, 300m);

        var svc = new PayrollMonthlySummaryService(ctx);
        var saved = await svc.SaveMonthAsync(2026, 11);
        saved.Should().Be(2);

        var snap = await ctx.PayrollMonthlySummaryRecords.Where(r => r.Year == 2026 && r.Month == 11).ToListAsync();
        snap.Should().HaveCount(2);
        var aSnap = snap.Single(r => r.EmployeeId == a.Id);
        aSnap.BaseWage.Should().Be(5000m);
        aSnap.MiscWorkAmount.Should().Be(100m);
        aSnap.TotalPaid.Should().Be(5100m);
        bSnapCheck(ctx, b.Id);

        // 二次保存 → 整月替换仍 2 行（幂等不重复）
        var saved2 = await svc.SaveMonthAsync(2026, 11);
        saved2.Should().Be(2);
        ctx.PayrollMonthlySummaryRecords.Count(r => r.Year == 2026 && r.Month == 11).Should().Be(2);

        var dto = await svc.GetMonthAsync(2026, 11);
        dto.HasSaved.Should().BeTrue();
    }

    private static void bSnapCheck(AppDbContext ctx, int bId)
    {
        var bSnap = ctx.PayrollMonthlySummaryRecords.Single(r => r.EmployeeId == bId);
        bSnap.BaseWage.Should().Be(0m);
        bSnap.MiscWorkAmount.Should().Be(300m);
    }

    // ==================== 打印（读快照，未保存抛） ====================

    [Fact]
    public async Task 打印_无快照抛业务异常提示先保存()
    {
        using var ctx = CreateDbContext();
        var emp = await SeedEmpAsync(ctx, "A001", "张三", SalaryMode.Fixed, 5000m);
        await AddMiscAsync(ctx, emp.Id, 2026, 11, 1, 100m);

        var svc = new PayrollMonthlySummaryService(ctx);
        var actAll = () => svc.PrintAllAsync(2026, 11);
        await actAll.Should().ThrowAsync<BusinessException>().WithMessage("*保存本月*");
        var actPersonal = () => svc.PrintPersonalAsync(2026, 11);
        await actPersonal.Should().ThrowAsync<BusinessException>().WithMessage("*保存本月*");
    }

    [Fact]
    public async Task 打印_保存后可生成PDF_全部与个人_非空()
    {
        using var ctx = CreateDbContext();
        var emp = await SeedEmpAsync(ctx, "A001", "张三", SalaryMode.Fixed, 4000m);
        await AddMiscAsync(ctx, emp.Id, 2026, 11, 1, 3000m);
        await AddAllowanceAsync(ctx, emp.Id, 2026, 11, pos: 50m, pen: 50m, ss: 600m);

        var svc = new PayrollMonthlySummaryService(ctx);
        await svc.SaveMonthAsync(2026, 11);

        var allPdf = await svc.PrintAllAsync(2026, 11);
        allPdf.Should().NotBeNullOrEmpty();
        allPdf.Length.Should().BeGreaterThan(1000);
        var personalPdf = await svc.PrintPersonalAsync(2026, 11);
        personalPdf.Should().NotBeNullOrEmpty();
        personalPdf.Length.Should().BeGreaterThan(1000);
    }
}
