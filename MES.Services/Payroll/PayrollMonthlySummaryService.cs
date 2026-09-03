using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Payroll;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;
using MES.Services.Printing;

namespace MES.Services.Payroll;

/// <summary>
/// 月工资汇总服务（2026-09-04 引入）。
/// 员工某结算月「完整应发/实发」由各子页已保存金额 + 考勤聚合派生：
/// - 基础工资：Fixed 取 Employee.MonthlyWage；其余按当月已保存来源取数——
///   集体计件 PayrollCollectiveWageRecord → 靠工计件 PayrollAttendanceWageRecord → 每日工资 Σ PayrollDailyWageRecord
///   （按来源存在性择优，兼容员工历史月份曾处不同归口、且每人每月只会写入其中一张工资表）。
/// - 杂辅工资 = 当月 PayrollMiscWorkRecord.Amount 合计；津贴/处罚 = PayrollAllowanceRecord 当月行
///   （处罚/代缴社保 源表正数录入 → 汇总存负）。
/// - 出勤天数 = 当月 AttendanceRecord 去重日期数（(EmployeeId, AttendDate) 唯一索引保证 Count 即天数）。
/// - 应发 = 基础 + 杂辅 + 7 项正津贴（不含处罚/代缴）；实发 = 应发 + 处罚 + 代缴（后两列存负）。
/// 保存 = 整月重算替换快照（每人每月一行，UK_PayrollMonthlySummary_Employee_Month）；
/// 打印与数据工具均读本快照表，保证发放单与冻结口径一致。
/// </summary>
public class PayrollMonthlySummaryService : IPayrollMonthlySummaryService
{
    private readonly AppDbContext _context;

    public PayrollMonthlySummaryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MonthlySummaryMonthDto> GetMonthAsync(int year, int month, string? keyword = null)
    {
        ValidateMonth(year, month);

        var rows = await ComputeRowsAsync(year, month);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            rows = rows
                .Where(r => r.EmployeeCode.Contains(kw, StringComparison.OrdinalIgnoreCase)
                            || r.EmployeeName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var hasSaved = await _context.PayrollMonthlySummaryRecords.AsNoTracking()
            .AnyAsync(r => r.Year == year && r.Month == month);

        return new MonthlySummaryMonthDto
        {
            Year = year,
            Month = month,
            HasSaved = hasSaved,
            Rows = rows,
        };
    }

    public async Task<int> SaveMonthAsync(int year, int month)
    {
        ValidateMonth(year, month);

        var rows = await ComputeRowsAsync(year, month);

        // 整月替换快照（每人每月一行，唯一索引保证不重复）
        var existing = await _context.PayrollMonthlySummaryRecords
            .Where(r => r.Year == year && r.Month == month)
            .ToListAsync();
        if (existing.Count > 0)
            _context.PayrollMonthlySummaryRecords.RemoveRange(existing);

        var records = rows.Select(r => new PayrollMonthlySummaryRecord
        {
            EmployeeId = r.EmployeeId,
            Year = year,
            Month = month,
            AttendanceDays = r.AttendanceDays,
            BaseWage = r.BaseWage,
            MiscWorkAmount = r.MiscWorkAmount,
            PositionAllowance = r.PositionAllowance,
            SeniorityBonus = r.SeniorityBonus,
            FullAttendanceBonus = r.FullAttendanceBonus,
            LeadBonus = r.LeadBonus,
            NightShiftAllowance = r.NightShiftAllowance,
            HighTempAllowance = r.HighTempAllowance,
            InjurySubsidy = r.InjurySubsidy,
            Penalty = r.Penalty,
            SocialSecurity = r.SocialSecurity,
            TotalPayable = r.TotalPayable,
            TotalPaid = r.TotalPaid,
        }).ToList();

        _context.PayrollMonthlySummaryRecords.AddRange(records);
        await _context.SaveChangesAsync();
        return records.Count;
    }

    public async Task<byte[]> PrintAllAsync(int year, int month)
    {
        var rows = await LoadSnapshotPrintRowsAsync(year, month);
        return PayrollSummaryPrintHelper.GenerateAllPdf($"{year}年{month}月工资津贴汇总表", rows);
    }

    public async Task<byte[]> PrintPersonalAsync(int year, int month)
    {
        var rows = await LoadSnapshotPrintRowsAsync(year, month);
        return PayrollSummaryPrintHelper.GeneratePersonalPdf($"{year}年{month}月工资条", rows);
    }

    /// <summary>读取已保存快照并映射为打印行；当月无快照抛业务异常提示先保存（打印严格读冻结口径）</summary>
    private async Task<List<PayrollSummaryPrintRow>> LoadSnapshotPrintRowsAsync(int year, int month)
    {
        var records = await _context.PayrollMonthlySummaryRecords.AsNoTracking()
            .Where(r => r.Year == year && r.Month == month)
            .OrderBy(r => r.EmployeeId)
            .ToListAsync();
        if (records.Count == 0)
            throw new BusinessException("本月尚未生成工资汇总快照，请先在页面「保存本月」后再打印");

        // 关联员工（含停用）出工号/姓名
        var empIds = records.Select(r => r.EmployeeId).Distinct().ToHashSet();
        var empList = new List<Employee>();
        foreach (var chunk in empIds.Chunk(1000))
        {
            empList.AddRange(await _context.Employees.AsNoTracking()
                .Where(e => chunk.Contains(e.Id))
                .ToListAsync());
        }
        var empById = empList.ToDictionary(e => e.Id);

        return records
            .Select(r => PayrollSummaryPrintHelper.ToPrintRow(
                r,
                empById.TryGetValue(r.EmployeeId, out var emp) ? emp.Code : "-",
                empById.TryGetValue(r.EmployeeId, out var e) ? e.Name : "-",
                year, month))
            .ToList();
    }

    /// <summary>重算整月汇总行（IsActive 在册员工 ∪ 当月任一来源有行，按工号升序）</summary>
    private async Task<List<PayrollMonthlySummaryRowDto>> ComputeRowsAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);

        // 1. 当月源数据
        var activeIds = await _context.Employees.AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => e.Id)
            .ToListAsync();

        // 考勤：去重日期数（(EmployeeId, AttendDate) 唯一 → Count 即天数）
        var attendanceDaysByEmp = (await _context.AttendanceRecords.AsNoTracking()
                .Where(r => r.AttendDate >= start && r.AttendDate < end)
                .Select(r => r.EmployeeId)
                .ToListAsync())
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        // 每日工资：逐日求和
        var dailyByEmp = (await _context.PayrollDailyWageRecords.AsNoTracking()
                .Where(r => r.WageDate >= start && r.WageDate < end)
                .Select(r => new { r.EmployeeId, r.Amount })
                .ToListAsync())
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        // 集体计件月结快照（员工+年月唯一）
        var collectiveByEmp = (await _context.PayrollCollectiveWageRecords.AsNoTracking()
                .Where(r => r.WageYear == year && r.WageMonth == month)
                .Select(r => new { r.EmployeeId, r.Amount })
                .ToListAsync())
            .ToDictionary(x => x.EmployeeId, x => x.Amount);

        // 靠工计件月结快照（员工+年月唯一）
        var attendanceWageByEmp = (await _context.PayrollAttendanceWageRecords.AsNoTracking()
                .Where(r => r.WageYear == year && r.WageMonth == month)
                .Select(r => new { r.EmployeeId, r.Amount })
                .ToListAsync())
            .ToDictionary(x => x.EmployeeId, x => x.Amount);

        // 杂辅台账：当月求和
        var miscByEmp = (await _context.PayrollMiscWorkRecords.AsNoTracking()
                .Where(r => r.WorkDate >= start && r.WorkDate < end)
                .Select(r => new { r.EmployeeId, r.Amount })
                .ToListAsync())
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        // 津贴与处罚当月行（每人每月一行）
        var allowanceByEmp = (await _context.PayrollAllowanceRecords.AsNoTracking()
                .Where(r => r.Year == year && r.Month == month)
                .ToListAsync())
            .ToDictionary(r => r.EmployeeId);

        // 2. 行集 = 在册员工 ∪ 各来源涉及员工（含停用历史行）
        var allIds = new HashSet<int>(activeIds);
        foreach (var id in attendanceDaysByEmp.Keys) allIds.Add(id);
        foreach (var id in dailyByEmp.Keys) allIds.Add(id);
        foreach (var id in collectiveByEmp.Keys) allIds.Add(id);
        foreach (var id in attendanceWageByEmp.Keys) allIds.Add(id);
        foreach (var id in miscByEmp.Keys) allIds.Add(id);
        foreach (var id in allowanceByEmp.Keys) allIds.Add(id);

        var empList = new List<Employee>();
        foreach (var chunk in allIds.Chunk(1000))
        {
            empList.AddRange(await _context.Employees.AsNoTracking()
                .Where(e => chunk.Contains(e.Id))
                .ToListAsync());
        }
        var empById = empList.ToDictionary(e => e.Id);

        // 3. 铺行
        var rows = allIds
            .Select(id =>
            {
                empById.TryGetValue(id, out var emp);
                var isFixed = emp is { IsActive: true, SalaryMode: SalaryMode.Fixed };
                var rec = allowanceByEmp.TryGetValue(id, out var a) ? a : null;

                var baseWage = isFixed
                    ? emp!.MonthlyWage ?? 0m
                    : (collectiveByEmp.TryGetValue(id, out var col) ? col
                        : attendanceWageByEmp.TryGetValue(id, out var att) ? att
                        : dailyByEmp.TryGetValue(id, out var day) ? day : 0m);

                var posAllowance = rec?.PositionAllowance ?? 0m;
                var seniorityBonus = rec?.SeniorityBonus ?? 0m;
                var fullAttendanceBonus = rec?.FullAttendanceBonus ?? 0m;
                var leadBonus = rec?.LeadBonus ?? 0m;
                var nightShiftAllowance = rec?.NightShiftAllowance ?? 0m;
                var highTempAllowance = rec?.HighTempAllowance ?? 0m;
                var injurySubsidy = rec?.InjurySubsidy ?? 0m;
                var miscWorkAmount = miscByEmp.TryGetValue(id, out var m) ? m : 0m;
                // 处罚/代缴社保：源表正数录入、扣减语义 → 汇总存负
                var penalty = rec?.Penalty is { } p ? -p : 0m;
                var socialSecurity = rec?.SocialSecurity is { } s ? -s : 0m;

                var totalPayable = baseWage + miscWorkAmount + posAllowance + seniorityBonus
                    + fullAttendanceBonus + leadBonus + nightShiftAllowance + highTempAllowance + injurySubsidy;
                var totalPaid = totalPayable + penalty + socialSecurity;

                return new PayrollMonthlySummaryRowDto
                {
                    EmployeeId = id,
                    EmployeeCode = emp?.Code ?? "-",
                    EmployeeName = emp?.Name ?? "-",
                    PositionCategory = emp?.Department,
                    Position = emp?.Position,
                    SalaryMode = emp?.SalaryMode?.ToString(),
                    IsActive = emp?.IsActive ?? false,
                    AttendanceDays = attendanceDaysByEmp.TryGetValue(id, out var days) ? days : 0,
                    BaseWage = baseWage,
                    MiscWorkAmount = miscWorkAmount,
                    PositionAllowance = posAllowance,
                    SeniorityBonus = seniorityBonus,
                    FullAttendanceBonus = fullAttendanceBonus,
                    LeadBonus = leadBonus,
                    NightShiftAllowance = nightShiftAllowance,
                    HighTempAllowance = highTempAllowance,
                    InjurySubsidy = injurySubsidy,
                    Penalty = penalty,
                    SocialSecurity = socialSecurity,
                    TotalPayable = totalPayable,
                    TotalPaid = totalPaid,
                };
            })
            .OrderBy(r => r.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.EmployeeId)
            .ToList();

        return rows;
    }

    private static void ValidateMonth(int year, int month)
    {
        if (year < 2000 || month < 1 || month > 12)
            throw new BusinessException("月份参数无效");
    }
}
