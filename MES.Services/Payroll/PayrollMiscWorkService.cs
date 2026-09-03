using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Payroll;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Payroll;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;

namespace MES.Services.Payroll;

/// <summary>
/// 杂辅工记录服务（2026-09-03 引入）。
/// 登记员工每天做的杂项辅助工作（台账流水，行=一条任务登记），金额为手工录入源头（保留小数不整元）；
/// 允许同一员工同一天多条（无唯一约束）。员工关联跨上下文只存 EmployeeId，读取时补工号/姓名。
/// 已接入月工资汇总：月汇总对当月杂辅求和计入员工应发（见 PayrollMonthlySummaryService），本台账仍以逐条录入为准。
/// </summary>
public class PayrollMiscWorkService : IPayrollMiscWorkService
{
    private readonly AppDbContext _context;

    public PayrollMiscWorkService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MiscWorkMonthDto> GetMonthAsync(int year, int month)
    {
        if (year < 2000 || month < 1 || month > 12)
            throw new BusinessException("月份参数无效");

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);

        // 1. 当月记录（date 区间谓词进 SQL）
        var records = await _context.PayrollMiscWorkRecords.AsNoTracking()
            .Where(r => r.WorkDate >= monthStart && r.WorkDate < monthEnd)
            .ToListAsync();

        // 2. 员工 Code/Name 映射：不过滤 IsActive（停用员工的历史行也要正常显示）；查不到兜底 "-"
        var employeeIds = records.Select(r => r.EmployeeId).Distinct().ToHashSet();
        var employees = new List<Employee>();
        foreach (var chunk in employeeIds.Chunk(1000))
        {
            employees.AddRange(await _context.Employees.AsNoTracking()
                .Where(e => chunk.Contains(e.Id))
                .ToListAsync());
        }
        var empById = employees.ToDictionary(e => e.Id);

        // 3. 铺行：日期+工号+Id 稳定升序
        var rows = records
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => empById.TryGetValue(r.EmployeeId, out var e) ? e.Code : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id)
            .Select(r => new MiscWorkRowDto
            {
                Id = r.Id,
                EmployeeId = r.EmployeeId,
                EmployeeCode = empById.TryGetValue(r.EmployeeId, out var e) ? e.Code : "-",
                EmployeeName = empById.TryGetValue(r.EmployeeId, out var n) ? n.Name : "-",
                WorkDate = r.WorkDate,
                Content = r.Content,
                Hours = r.Hours,
                Amount = r.Amount,
                Remark = r.Remark,
            })
            .ToList();

        // 4. 整月汇总（原样求和，不做整元取整）
        return new MiscWorkMonthDto
        {
            Year = year,
            Month = month,
            RecordCount = rows.Count,
            TotalHours = rows.Sum(r => r.Hours),
            TotalAmount = rows.Sum(r => r.Amount),
            Records = rows,
        };
    }

    public async Task<int> SaveRecordAsync(MiscWorkRecordInputDto input)
    {
        var content = input.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new BusinessException("杂辅内容不能为空");
        if (content.Length > 500)
            throw new BusinessException("杂辅内容不能超过 500 字");
        if (input.Remark?.Length > 200)
            throw new BusinessException("备注不能超过 200 字");
        if (input.WorkDate.Year < 2000)
            throw new BusinessException("日期无效");
        if (input.Hours < 0)
            throw new BusinessException("小时数不能为负数");
        if (input.Amount < 0)
            throw new BusinessException("杂辅工资不能为负数");

        // 员工必须存在；新增强制 IsActive，编辑允许停用员工（离职后仍可改历史行）
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId)
            ?? throw new BusinessException("员工不存在");
        if (input.Id == 0 && !employee.IsActive)
            throw new BusinessException("员工已停用，不能新增杂辅记录");

        if (input.Id == 0)
        {
            var record = new PayrollMiscWorkRecord
            {
                EmployeeId = input.EmployeeId,
                WorkDate = input.WorkDate.Date,
                Content = content,
                Hours = input.Hours,
                Amount = input.Amount,
                Remark = string.IsNullOrWhiteSpace(input.Remark) ? null : input.Remark.Trim(),
            };
            _context.PayrollMiscWorkRecords.Add(record);
            await _context.SaveChangesAsync();
            return record.Id;
        }

        var existing = await _context.PayrollMiscWorkRecords.FirstOrDefaultAsync(r => r.Id == input.Id)
            ?? throw new BusinessException("记录不存在或已删除");
        // 编辑只改内容/日期/小时/金额/备注，不改员工归属
        existing.WorkDate = input.WorkDate.Date;
        existing.Content = content;
        existing.Hours = input.Hours;
        existing.Amount = input.Amount;
        existing.Remark = string.IsNullOrWhiteSpace(input.Remark) ? null : input.Remark.Trim();
        await _context.SaveChangesAsync();
        return existing.Id;
    }

    public async Task DeleteRecordAsync(int id)
    {
        var record = await _context.PayrollMiscWorkRecords.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new BusinessException("记录不存在或已删除");
        _context.PayrollMiscWorkRecords.Remove(record);
        await _context.SaveChangesAsync();
    }
}
