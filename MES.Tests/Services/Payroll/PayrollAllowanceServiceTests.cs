using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Payroll;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;
using MES.Services.Payroll;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 「津贴与处罚」服务测试（2026-09-04）：
/// 宽表固定 9 列，每人每月一行（EmployeeId+Year+Month 唯一）；金额强制整元（AwayFromZero）、空/0=null、禁止负数；
/// 月历 = IsActive 在册员工 ∪ 当月已有记录员工；保存整月 upsert，全空行删除、空列表清空整月；
/// 仅新增强制 IsActive（停用员工已有行可改、无行新增抛）。
/// </summary>
public class PayrollAllowanceServiceTests : TestBase
{
    private static async Task<Employee> SeedEmpAsync(AppDbContext ctx, string code, string name, bool active = true)
    {
        var e = new Employee { Code = code, Name = name, IsActive = active };
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    /// <summary>直接落一行当月记录（供构造停用历史等夹具，9 金额列默认空）</summary>
    private static async Task AddRowAsync(AppDbContext ctx, int empId, int year, int month,
        decimal? full = null, decimal? sen = null, decimal? night = null, decimal? pos = null,
        decimal? high = null, decimal? inj = null, decimal? lead = null, decimal? pen = null, decimal? ss = null)
    {
        ctx.PayrollAllowanceRecords.Add(new PayrollAllowanceRecord
        {
            EmployeeId = empId,
            Year = year,
            Month = month,
            FullAttendanceBonus = full,
            SeniorityBonus = sen,
            NightShiftAllowance = night,
            PositionAllowance = pos,
            HighTempAllowance = high,
            InjurySubsidy = inj,
            LeadBonus = lead,
            Penalty = pen,
            SocialSecurity = ss,
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>造一行保存输入（只设感兴趣的金额列，其余默认 null）</summary>
    private static AllowanceRowInputDto RowIn(int empId,
        decimal? full = null, decimal? sen = null, decimal? night = null, decimal? pos = null,
        decimal? high = null, decimal? inj = null, decimal? lead = null, decimal? pen = null, decimal? ss = null)
        => new()
        {
            EmployeeId = empId,
            FullAttendanceBonus = full,
            SeniorityBonus = sen,
            NightShiftAllowance = night,
            PositionAllowance = pos,
            HighTempAllowance = high,
            InjurySubsidy = inj,
            LeadBonus = lead,
            Penalty = pen,
            SocialSecurity = ss,
        };

    // ==================== 读取：月历 / 映射 / 排序 / 仅当月 ====================

    [Fact]
    public async Task GetMonthAsync_无数据返回在册全员空行_停用不入_按工号升序()
    {
        using var ctx = CreateDbContext();
        await SeedEmpAsync(ctx, "A001", "张三");
        await SeedEmpAsync(ctx, "B002", "李四");
        await SeedEmpAsync(ctx, "c003", "王五");
        await SeedEmpAsync(ctx, "D099", "停用", active: false);

        var svc = new PayrollAllowanceService(ctx);
        var dto = await svc.GetMonthAsync(2026, 3);

        dto.Rows.Should().HaveCount(3); // D099 停用不入
        dto.Rows.Select(r => r.EmployeeCode).Should().BeEquivalentTo(new[] { "A001", "B002", "c003" }, options => options.WithStrictOrdering());
        dto.Rows.Should().AllSatisfy(r => r.IsActive.Should().BeTrue());
        dto.Rows.Should().AllSatisfy(r => AssertNineNull(r));
    }

    [Fact]
    public async Task GetMonthAsync_并入当月历史停用行_IsActiveFalse_仅当月不过滤他月()
    {
        using var ctx = CreateDbContext();
        var actEmp = await SeedEmpAsync(ctx, "Y001", "在册甲");
        var inactiveEmp = await SeedEmpAsync(ctx, "X099", "停用乙", active: false);
        // 停用乙：3 月有行（该并入），2 月有另一行（不得污染 3 月读取）
        await AddRowAsync(ctx, actEmp.Id, 2026, 3, full: 100);
        await AddRowAsync(ctx, inactiveEmp.Id, 2026, 3, sen: 50);
        await AddRowAsync(ctx, inactiveEmp.Id, 2026, 2, sen: 999);

        var svc = new PayrollAllowanceService(ctx);
        var dto = await svc.GetMonthAsync(2026, 3);

        dto.Rows.Select(r => r.EmployeeCode).Should().BeEquivalentTo(new[] { "X099", "Y001" }, options => options.WithStrictOrdering());
        var x = dto.Rows.Single(r => r.EmployeeCode == "X099");
        x.IsActive.Should().BeFalse();
        x.SeniorityBonus.Should().Be(50);
        x.FullAttendanceBonus.Should().BeNull();
        var y = dto.Rows.Single(r => r.EmployeeCode == "Y001");
        y.IsActive.Should().BeTrue();
        y.FullAttendanceBonus.Should().Be(100);
    }

    [Fact]
    public async Task GetMonthAsync_月份参数无效抛()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollAllowanceService(ctx);
        var act = () => svc.GetMonthAsync(1999, 3);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("月份参数无效");
        var act2 = () => svc.GetMonthAsync(2026, 13);
        await act2.Should().ThrowAsync<BusinessException>().WithMessage("月份参数无效");
    }

    // ==================== 保存：整元规约 / upsert / 空处理 / 员工状态 ====================

    [Fact]
    public async Task SaveMonthAsync_整元规约_AwayFromZero_新增落整行含空列_返回保存数()
    {
        using var ctx = CreateDbContext();
        var a = await SeedEmpAsync(ctx, "A001", "张三");
        var b = await SeedEmpAsync(ctx, "B002", "李四");

        var svc = new PayrollAllowanceService(ctx);
        var saved = await svc.SaveMonthAsync(2026, 3, new[]
        {
            RowIn(a.Id, full: 12.6m, pen: 12.4m),   // 12.6→13（AwayFromZero 进位）、12.4→12（不进位）
            RowIn(b.Id),                              // 全空 → 不落库
        });

        saved.Should().Be(1);
        var row = await ctx.PayrollAllowanceRecords.SingleAsync(r => r.EmployeeId == a.Id && r.Year == 2026 && r.Month == 3);
        row.FullAttendanceBonus.Should().Be(13);
        row.Penalty.Should().Be(12);
        row.SeniorityBonus.Should().BeNull();
        row.SocialSecurity.Should().BeNull();
        ctx.PayrollAllowanceRecords.Count(r => r.Month == 3).Should().Be(1); // B 全空未落库
    }

    [Fact]
    public async Task SaveMonthAsync_金额0与空一律null_不进位小数为0_不落库()
    {
        using var ctx = CreateDbContext();
        var a = await SeedEmpAsync(ctx, "A001", "张三");

        var svc = new PayrollAllowanceService(ctx);
        // 0 → null（等价未填）；0.4 → AwayFromZero 得 0 → null
        await svc.SaveMonthAsync(2026, 3, new[] { RowIn(a.Id, full: 0m, sen: 0.4m) });

        ctx.PayrollAllowanceRecords.Should().HaveCount(0);
    }

    [Fact]
    public async Task SaveMonthAsync_编辑覆盖_整月整行更新_清空原值()
    {
        using var ctx = CreateDbContext();
        var a = await SeedEmpAsync(ctx, "A001", "张三");

        var svc = new PayrollAllowanceService(ctx);
        await svc.SaveMonthAsync(2026, 3, new[] { RowIn(a.Id, full: 100, ss: 500) });
        await svc.SaveMonthAsync(2026, 3, new[] { RowIn(a.Id, sen: 7) });

        var row = await ctx.PayrollAllowanceRecords.SingleAsync();
        row.FullAttendanceBonus.Should().BeNull(); // 第二次整行覆盖 → 原值被清空
        row.SocialSecurity.Should().BeNull();
        row.SeniorityBonus.Should().Be(7);
    }

    [Fact]
    public async Task SaveMonthAsync_全空行删除当月记录_其余行保留()
    {
        using var ctx = CreateDbContext();
        var a = await SeedEmpAsync(ctx, "A001", "张三");
        var b = await SeedEmpAsync(ctx, "B002", "李四");

        var svc = new PayrollAllowanceService(ctx);
        await svc.SaveMonthAsync(2026, 3, new[] { RowIn(a.Id, full: 100), RowIn(b.Id, lead: 30) });
        // A 整行清空 → 删除其当月行；B 保留
        var saved = await svc.SaveMonthAsync(2026, 3, new[] { RowIn(a.Id), RowIn(b.Id, lead: 30) });

        saved.Should().Be(1);
        var rows = await ctx.PayrollAllowanceRecords.Where(r => r.Year == 2026 && r.Month == 3).ToListAsync();
        rows.Should().ContainSingle(r => r.EmployeeId == b.Id);
        rows.Should().NotContain(r => r.EmployeeId == a.Id);
    }

    [Fact]
    public async Task SaveMonthAsync_空列表清空整月()
    {
        using var ctx = CreateDbContext();
        var a = await SeedEmpAsync(ctx, "A001", "张三");
        var b = await SeedEmpAsync(ctx, "B002", "李四");

        var svc = new PayrollAllowanceService(ctx);
        await svc.SaveMonthAsync(2026, 3, new[] { RowIn(a.Id, full: 100), RowIn(b.Id, lead: 30) });
        var saved = await svc.SaveMonthAsync(2026, 3, Array.Empty<AllowanceRowInputDto>());

        saved.Should().Be(0);
        ctx.PayrollAllowanceRecords.Should().HaveCount(0);
    }

    [Fact]
    public async Task SaveMonthAsync_停用员工已有行可改_无行新增抛()
    {
        using var ctx = CreateDbContext();
        var emp = await SeedEmpAsync(ctx, "A001", "张三", active: true);
        await AddRowAsync(ctx, emp.Id, 2026, 3, full: 100);
        // 停用后：已有 3 月行 → 仍可更新
        emp.IsActive = false;
        await ctx.SaveChangesAsync();
        var inactive = await SeedEmpAsync(ctx, "X099", "停用乙", active: false);

        var svc = new PayrollAllowanceService(ctx);
        var saved = await svc.SaveMonthAsync(2026, 3, new[] { RowIn(emp.Id, full: 88) });
        saved.Should().Be(1);
        (await ctx.PayrollAllowanceRecords.SingleAsync(r => r.EmployeeId == emp.Id)).FullAttendanceBonus.Should().Be(88);

        // 停用且当月无行 → 拒绝新增
        var act = () => svc.SaveMonthAsync(2026, 3, new[] { RowIn(inactive.Id, full: 50) });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不能新增当月津贴*");
    }

    [Fact]
    public async Task SaveMonthAsync_员工不存在抛()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollAllowanceService(ctx);
        var act = () => svc.SaveMonthAsync(2026, 3, new[] { RowIn(999999, full: 100) });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("员工不存在");
    }

    [Fact]
    public async Task SaveMonthAsync_负值抛_无残留()
    {
        using var ctx = CreateDbContext();
        var a = await SeedEmpAsync(ctx, "A001", "张三");
        var b = await SeedEmpAsync(ctx, "B002", "李四");

        var svc = new PayrollAllowanceService(ctx);
        // 前一行合法、后一行负值 → 整批回滚不落库
        var act = () => svc.SaveMonthAsync(2026, 3, new[] { RowIn(a.Id, full: 100), RowIn(b.Id, pen: -5) });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*金额不能为负数*");
        ctx.PayrollAllowanceRecords.Should().HaveCount(0);
    }

    [Fact]
    public async Task SaveMonthAsync_月份参数无效抛()
    {
        using var ctx = CreateDbContext();
        var a = await SeedEmpAsync(ctx, "A001", "张三");
        var svc = new PayrollAllowanceService(ctx);
        var act = () => svc.SaveMonthAsync(1999, 3, new[] { RowIn(a.Id, full: 100) });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("月份参数无效");
        var act2 = () => svc.SaveMonthAsync(2026, 13, new[] { RowIn(a.Id, full: 100) });
        await act2.Should().ThrowAsync<BusinessException>().WithMessage("月份参数无效");
    }

    [Fact]
    public async Task 排序稳定_Code不区分大小写与保存顺序无关()
    {
        using var ctx = CreateDbContext();
        // 打乱插入顺序，期望仍按 Code 序返回
        var c = await SeedEmpAsync(ctx, "C003", "丙");
        var a = await SeedEmpAsync(ctx, "a001", "甲");
        var b = await SeedEmpAsync(ctx, "B002", "乙");

        var svc = new PayrollAllowanceService(ctx);
        var dto = await svc.GetMonthAsync(2026, 3);

        dto.Rows.Select(r => r.EmployeeCode).Should().BeEquivalentTo(new[] { "a001", "B002", "C003" }, options => options.WithStrictOrdering());
    }

    private static void AssertNineNull(AllowanceRowDto r)
    {
        r.FullAttendanceBonus.Should().BeNull();
        r.SeniorityBonus.Should().BeNull();
        r.NightShiftAllowance.Should().BeNull();
        r.PositionAllowance.Should().BeNull();
        r.HighTempAllowance.Should().BeNull();
        r.InjurySubsidy.Should().BeNull();
        r.LeadBonus.Should().BeNull();
        r.Penalty.Should().BeNull();
        r.SocialSecurity.Should().BeNull();
    }
}
