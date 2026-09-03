using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Payroll;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Payroll;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;

namespace MES.Services.Payroll;

/// <summary>
/// 「津贴与处罚」服务（2026-09-04 引入）。
/// 月度金额录入表：宽表固定 9 列（满勤奖/工龄奖/夜班津贴/岗位补贴/高温费/工伤补贴/带班费/处罚/代缴社保），
/// 员工 + 结算月唯一 → 每人每月一行整月 upsert。金额强制整元（用户拍板），空=null 等价 0 元，不允许负数。
/// 月历 = IsActive 在册员工 ∪ 当月已有记录员工（停用员工当月历史行仍显示可改）。
/// 已接入月工资汇总：月汇总读取本表并入员工应发/实发（处罚/代缴以扣减语义参与，见 PayrollMonthlySummaryService）。
/// </summary>
public class PayrollAllowanceService : IPayrollAllowanceService
{
    private readonly AppDbContext _context;

    public PayrollAllowanceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AllowanceMonthDto> GetMonthAsync(int year, int month)
    {
        ValidateMonth(year, month);

        // 1. 在册员工 Id + 当月已有记录（含停用员工历史行）
        var activeIds = await _context.Employees.AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => e.Id)
            .ToListAsync();
        var monthRows = await _context.PayrollAllowanceRecords.AsNoTracking()
            .Where(r => r.Year == year && r.Month == month)
            .ToListAsync();
        var rowByEmp = monthRows.ToDictionary(r => r.EmployeeId);

        // 2. 员工 Code/Name/岗位信息映射：覆盖在册 + 当月有行（含停用）；查不到兜底 "-"
        var allIds = activeIds.Concat(monthRows.Select(r => r.EmployeeId)).Distinct().ToHashSet();
        var employees = new List<Employee>();
        foreach (var chunk in allIds.Chunk(1000))
        {
            employees.AddRange(await _context.Employees.AsNoTracking()
                .Where(e => chunk.Contains(e.Id))
                .ToListAsync());
        }
        var empById = employees.ToDictionary(e => e.Id);

        // 3. 铺行：在册员工（无行则 9 值全空）∪ 当月有行的停用员工，按工号升序
        var rows = allIds
            .Select(empId =>
            {
                empById.TryGetValue(empId, out var emp);
                rowByEmp.TryGetValue(empId, out var rec);
                return new AllowanceRowDto
                {
                    EmployeeId = empId,
                    EmployeeCode = emp?.Code ?? "-",
                    EmployeeName = emp?.Name ?? "-",
                    PositionCategory = emp?.Department,
                    Position = emp?.Position,
                    PositionRemark = emp?.PositionRemark,
                    SalaryMode = emp?.SalaryMode?.ToString(),
                    IsActive = emp?.IsActive ?? false,
                    FullAttendanceBonus = rec?.FullAttendanceBonus,
                    SeniorityBonus = rec?.SeniorityBonus,
                    NightShiftAllowance = rec?.NightShiftAllowance,
                    PositionAllowance = rec?.PositionAllowance,
                    HighTempAllowance = rec?.HighTempAllowance,
                    InjurySubsidy = rec?.InjurySubsidy,
                    LeadBonus = rec?.LeadBonus,
                    Penalty = rec?.Penalty,
                    SocialSecurity = rec?.SocialSecurity,
                };
            })
            .OrderBy(r => r.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.EmployeeId)
            .ToList();

        return new AllowanceMonthDto { Year = year, Month = month, Rows = rows };
    }

    public async Task<int> SaveMonthAsync(int year, int month, IReadOnlyList<AllowanceRowInputDto> rows)
    {
        ValidateMonth(year, month);
        if (rows is null)
            throw new BusinessException("保存数据不能为空");

        // 1. 当月既有行（Tracked 便于增删改）+ 本批涉及的员工（含停用）
        var existing = await _context.PayrollAllowanceRecords
            .Where(r => r.Year == year && r.Month == month)
            .ToListAsync();
        var existingByEmp = existing.ToDictionary(r => r.EmployeeId);

        // 空列表 = 清空整月（前端「清空本月」提交空 Rows）
        if (rows.Count == 0)
        {
            if (existing.Count > 0)
            {
                _context.PayrollAllowanceRecords.RemoveRange(existing);
                await _context.SaveChangesAsync();
            }
            return 0;
        }

        var inputEmpIds = rows.Select(r => r.EmployeeId).Distinct().ToHashSet();
        var employees = new List<Employee>();
        foreach (var chunk in inputEmpIds.Chunk(1000))
        {
            employees.AddRange(await _context.Employees
                .Where(e => chunk.Contains(e.Id))
                .ToListAsync());
        }
        var empById = employees.ToDictionary(e => e.Id);

        var saved = 0;
        foreach (var input in rows)
        {
            var employee = empById.TryGetValue(input.EmployeeId, out var emp)
                ? emp
                : throw new BusinessException("员工不存在");
            existingByEmp.TryGetValue(input.EmployeeId, out var rec);

            // 整元规约：负数抛；0 → null（等价未填）
            var n1 = NormalizeAmount(input.FullAttendanceBonus);
            var n2 = NormalizeAmount(input.SeniorityBonus);
            var n3 = NormalizeAmount(input.NightShiftAllowance);
            var n4 = NormalizeAmount(input.PositionAllowance);
            var n5 = NormalizeAmount(input.HighTempAllowance);
            var n6 = NormalizeAmount(input.InjurySubsidy);
            var n7 = NormalizeAmount(input.LeadBonus);
            var n8 = NormalizeAmount(input.Penalty);
            var n9 = NormalizeAmount(input.SocialSecurity);

            // 全空 → 删除该员工当月行（无行则跳过）
            if (n1 is null && n2 is null && n3 is null && n4 is null && n5 is null
                && n6 is null && n7 is null && n8 is null && n9 is null)
            {
                if (rec is not null)
                {
                    _context.PayrollAllowanceRecords.Remove(rec);
                    existingByEmp.Remove(input.EmployeeId);
                }
                continue;
            }

            // 非空但该员工当月无行且已停用 → 拒绝凭空新增（离职补改走 Update 分支需已有行）
            if (rec is null && !employee.IsActive)
                throw new BusinessException($"员工 {employee.Code} 已停用，不能新增当月津贴");

            if (rec is null)
            {
                rec = new PayrollAllowanceRecord
                {
                    EmployeeId = input.EmployeeId,
                    Year = year,
                    Month = month,
                };
                _context.PayrollAllowanceRecords.Add(rec);
                existingByEmp[input.EmployeeId] = rec;
            }
            rec.FullAttendanceBonus = n1;
            rec.SeniorityBonus = n2;
            rec.NightShiftAllowance = n3;
            rec.PositionAllowance = n4;
            rec.HighTempAllowance = n5;
            rec.InjurySubsidy = n6;
            rec.LeadBonus = n7;
            rec.Penalty = n8;
            rec.SocialSecurity = n9;
            saved++;
        }

        await _context.SaveChangesAsync();
        return saved;
    }

    private static void ValidateMonth(int year, int month)
    {
        if (year < 2000 || month < 1 || month > 12)
            throw new BusinessException("月份参数无效");
    }

    /// <summary>
    /// 金额整元规约：null 原样；负数抛；AwayFromZero 四舍五入到元；0 → null（等价未填）。
    /// 整元为用户拍板的业务规约（非精度截断），统一在服务层单点执行。
    /// </summary>
    private static decimal? NormalizeAmount(decimal? value)
    {
        if (value is null)
            return null;
        if (value < 0)
            throw new BusinessException("津贴与处罚金额不能为负数");
        var rounded = decimal.Round(value.Value, 0, MidpointRounding.AwayFromZero);
        return rounded == 0 ? null : rounded;
    }
}
