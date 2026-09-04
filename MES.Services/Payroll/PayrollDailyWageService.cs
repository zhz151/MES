using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Payroll;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;

namespace MES.Services.Payroll;

/// <summary>
/// 每日工资服务 — 非计件工资 / 个人计件工资 两表的按月读取与整月保存（2026-09-03 引入）。
/// 单元格 = 每日工资额。自动带出草稿（非计件引擎 / 个人计件引擎）+ 人工可改，「保存本月」落库为按归口快照。
/// 存储 PayrollDailyWageRecord：仅 Amount&gt;0 落库，SalaryMode 快照保存当时归口（员工换归口后历史按快照仍可回溯显示）。
/// 历史月份按现行价估算（无历史单价快照）：引擎总是按「当前启用类别 + 现行档位」重算草稿。
/// </summary>
public class PayrollDailyWageService : IPayrollDailyWageService
{
    private readonly AppDbContext _context;

    public PayrollDailyWageService(AppDbContext context)
    {
        _context = context;
    }

    // ==================== 读取（月视图网格） ====================

    public async Task<DailyWageMonthDto> GetMonthAsync(int year, int month, PayrollWageGroup group, string? keyword)
    {
        if (year < 2000 || month < 1 || month > 12)
            throw new BusinessException("月份参数无效");

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var warnings = new List<string>();

        // 1. 档案归口属该组的启用员工（引擎自动带出对象）
        var modes = group.SalaryModes();
        var archiveEmployees = await _context.Employees.AsNoTracking()
            .Where(e => e.IsActive && e.SalaryMode != null && modes.Contains(e.SalaryMode.Value))
            .OrderBy(e => e.Code)
            .ToListAsync();
        var archiveById = archiveEmployees.ToDictionary(e => e.Id);

        // 2. 当月该组已保存快照记录 → 历史归口员工并入显示（换归口员工历史月仍可见可改）
        //    ⚠️ group.ContainsModeKey 是 C# 扩展不能进 SQL：先按月拉全量、再内存按组过滤
        var savedRecords = (await _context.PayrollDailyWageRecords.AsNoTracking()
                .Where(r => r.WageDate >= monthStart && r.WageDate < monthEnd)
                .ToListAsync())
            .Where(r => group.ContainsModeKey(r.SalaryMode))
            .ToList();
        var hasSaved = savedRecords.Count > 0;
        var savedByEmp = savedRecords
            .GroupBy(r => r.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(r => r.WageDate.Day, r => r.Amount));

        // 3. 历史快照员工集合（补进显示）；archive 成员已含
        var historyIds = savedRecords.Select(r => r.EmployeeId)
            .Distinct().Where(id => !archiveById.ContainsKey(id)).ToHashSet();
        List<Employee> historyEmployees = new();
        foreach (var chunk in historyIds.Chunk(1000))
        {
            var part = await _context.Employees.AsNoTracking()
                .Where(e => chunk.Contains(e.Id))
                .OrderBy(e => e.Code)
                .ToListAsync();
            historyEmployees.AddRange(part);
        }

        // 4. 合并显示员工集 + 关键字过滤（工号/姓名，大小写不敏感）
        var displayed = archiveEmployees
            .Concat(historyEmployees)
            .DistinctBy(e => e.Id)
            .OrderBy(e => e.Code)
            .ToList();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            displayed = displayed
                .Where(e => e.Code.Contains(kw, StringComparison.OrdinalIgnoreCase)
                            || e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // 5. 引擎自动带出：仅对「档案归口属该组」的启用员工计算草稿
        var engineByEmp = group switch
        {
            PayrollWageGroup.NonPiece => await ComputeNonPieceEngineAsync(
                year, month, monthStart, monthEnd, archiveEmployees, warnings),
            _ => await ComputeIndividualPieceEngineAsync(
                year, month, monthStart, monthEnd, archiveEmployees, warnings)
        };

        // 6. 铺行
        var rows = displayed.Select(e =>
        {
            var row = new DailyWageEmployeeRowDto
            {
                EmployeeId = e.Id,
                EmployeeCode = e.Code,
                EmployeeName = e.Name,
                PositionCategory = e.Department,
                Position = e.Position,
                EngineCovered = archiveById.ContainsKey(e.Id),
                DaySavedAmount = new Dictionary<int, decimal?>(),
                DayEngineAmount = new Dictionary<int, decimal?>()
            };
            decimal savedTotal = 0, engineTotal = 0;
            for (var d = 1; d <= daysInMonth; d++)
            {
                row.DaySavedAmount[d] = savedByEmp.TryGetValue(e.Id, out var days) && days.TryGetValue(d, out var v)
                    ? v : null;
                row.DayEngineAmount[d] = engineByEmp.TryGetValue(e.Id, out var ed) && ed.TryGetValue(d, out var ev)
                    ? ev : null;
                savedTotal += row.DaySavedAmount[d] ?? 0m;
                engineTotal += row.DayEngineAmount[d] ?? 0m;
            }
            row.TotalSaved = savedTotal;
            row.TotalEngine = engineTotal;
            return row;
        }).ToList();

        return new DailyWageMonthDto
        {
            Year = year,
            Month = month,
            HasSaved = hasSaved,
            Employees = rows,
            Warnings = warnings
        };
    }

    // ==================== 非计件引擎（Hourly / Daily，按当月考勤 × 工资标准） ====================

    private async Task<Dictionary<int, Dictionary<int, decimal>>> ComputeNonPieceEngineAsync(
        int year, int month, DateTime monthStart, DateTime monthEnd,
        List<Employee> archiveEmployees, List<string> warnings)
    {
        var engine = new Dictionary<int, Dictionary<int, decimal>>();
        var empIds = archiveEmployees.Select(e => e.Id).ToHashSet();
        if (empIds.Count == 0) return engine;

        // 当月考勤（仅档案归口属该组员工）
        var records = new List<AttendanceRecord>();
        foreach (var chunk in empIds.Chunk(1000))
        {
            records.AddRange(await _context.AttendanceRecords.AsNoTracking()
                .Where(r => chunk.Contains(r.EmployeeId) && r.AttendDate >= monthStart && r.AttendDate < monthEnd)
                .ToListAsync());
        }
        var recordsByEmp = records.GroupBy(r => r.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 缺工资标准员工名（仅统计有当月考勤的，避免无标准也无出勤也报）
        var missingStandard = new List<string>();
        foreach (var emp in archiveEmployees)
        {
            if (!recordsByEmp.TryGetValue(emp.Id, out var empRecords) || empRecords.Count == 0)
                continue;
            var mode = emp.SalaryMode!.Value;
            decimal? rate = mode switch
            {
                SalaryMode.Hourly => emp.HourlyWage,
                SalaryMode.Daily => emp.DailyWage,
                _ => null
            };
            if (!rate.HasValue || rate <= 0)
            {
                missingStandard.Add(emp.Name);
                continue;
            }
            var dayAmounts = new Dictionary<int, decimal>();
            foreach (var rec in empRecords)
            {
                var dayWage = mode == SalaryMode.Daily
                    ? rate.Value * Math.Min(rec.WorkHours, 8m) / 8m   // 计日：按小时/8 折算（半天=半日薪，超 8h 按 1 日）
                    : rec.WorkHours * rate.Value;                     // 计小时：小时 × 时薪
                if (dayWage <= 0) continue;
                dayAmounts[rec.AttendDate.Day] = (dayAmounts.TryGetValue(rec.AttendDate.Day, out var prev)
                    ? prev : 0m) + dayWage;
            }
            if (dayAmounts.Count > 0)
                engine[emp.Id] = dayAmounts;
        }

        if (missingStandard.Count > 0)
        {
            var shown = missingStandard.Take(5).ToList();
            warnings.Add($"缺{year}年{month}月出勤对应的工资标准（未自动带出）: {string.Join("、", shown)}"
                + (missingStandard.Count > shown.Count ? $" 等 {missingStandard.Count} 人" : ""));
        }
        return engine;
    }

    // ==================== 个人计件引擎（PieceIndividual，当月产量/成检 × 现行单价） ====================

    private async Task<Dictionary<int, Dictionary<int, decimal>>> ComputeIndividualPieceEngineAsync(
        int year, int month, DateTime monthStart, DateTime monthEnd,
        List<Employee> archiveEmployees, List<string> warnings)
    {
        var engine = new Dictionary<int, Dictionary<int, decimal>>();
        var employees = archiveEmployees.Where(e => e.SalaryMode == SalaryMode.PieceIndividual).ToList();
        if (employees.Count == 0) return engine;

        // 5 源计价扫描统一走共享采集器（与集体月结同源，防双通道口径漂移）
        var result = await new PieceRateCollector(_context).CollectAsync(monthStart, monthEnd, employees);
        if (result.UnpricedCount > 0)
            warnings.Add($"{year}年{month}月有 {result.UnpricedCount} 行产量/检验记录到数量但未匹配到计件单价（按0元计）");

        // 逐行份额 = 行额 / 写名总人头，仅个人计件发放对象按行日期归日桶
        foreach (var row in result.Rows)
        {
            var share = row.Amount / row.TotalHeadcount;
            foreach (var emp in row.Eligible)
            {
                if (!engine.TryGetValue(emp.Id, out var days))
                {
                    days = new Dictionary<int, decimal>();
                    engine[emp.Id] = days;
                }
                days[row.Date.Day] = (days.TryGetValue(row.Date.Day, out var prev) ? prev : 0m) + share;
            }
        }
        return engine;
    }

    // ==================== 整月保存 ====================

    public async Task<int> SaveMonthAsync(SaveDailyWageDto request)
    {
        if (request.Year < 2000 || request.Month < 1 || request.Month > 12)
            throw new BusinessException("月份参数无效");
        var daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        var monthStart = new DateTime(request.Year, request.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var group = request.Group;

        foreach (var entry in request.Entries)
        {
            if (entry.Day < 1 || entry.Day > daysInMonth)
                throw new BusinessException($"日期 {request.Year}-{request.Month}-{entry.Day} 超出当月范围");
            if (entry.Amount is < 0)
                throw new BusinessException($"员工 {entry.EmployeeId} 每日工资不可为负: {entry.Amount}");
        }

        // 该组当月既有记录（按归口快照归组；⚠️ ContainsModeKey 是 C# 扩展不能进 SQL，按月拉全量后内存归组）
        var existing = new List<PayrollDailyWageRecord>();
        foreach (var chunk in request.Entries.Select(e => e.EmployeeId).Distinct().Chunk(1000))
        {
            var monthRecs = await _context.PayrollDailyWageRecords
                .Where(r => chunk.Contains(r.EmployeeId) && r.WageDate >= monthStart && r.WageDate < monthEnd)
                .ToListAsync();
            existing.AddRange(monthRecs.Where(r => group.ContainsModeKey(r.SalaryMode)));
        }
        var existingMap = existing.ToDictionary(r => (r.EmployeeId, r.WageDate.Day));
        // 员工当月已有快照归口（历史归口优先保留），供新插行定快照
        var snapshotByEmp = existing
            .GroupBy(r => r.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First().SalaryMode);

        // 员工当前归口（归档在册快照依据）
        var entryIds = request.Entries.Select(e => e.EmployeeId).Distinct().ToHashSet();
        var currentModeById = new Dictionary<int, SalaryMode?>();
        foreach (var chunk in entryIds.Chunk(1000))
        {
            foreach (var emp in await _context.Employees.AsNoTracking()
                         .Where(e => chunk.Contains(e.Id))
                         .Select(e => new { e.Id, e.SalaryMode })
                         .ToListAsync())
            {
                currentModeById[emp.Id] = emp.SalaryMode;
            }
        }

        foreach (var entry in request.Entries)
        {
            if (entry.Amount is > 0)
            {
                if (existingMap.TryGetValue((entry.EmployeeId, entry.Day), out var rec))
                {
                    rec.Amount = entry.Amount.Value;
                    _context.PayrollDailyWageRecords.Update(rec);
                }
                else
                {
                    _context.PayrollDailyWageRecords.Add(new PayrollDailyWageRecord
                    {
                        EmployeeId = entry.EmployeeId,
                        WageDate = new DateTime(request.Year, request.Month, entry.Day),
                        Amount = entry.Amount.Value,
                        SalaryMode = SnapshotFor(entry.EmployeeId, snapshotByEmp, currentModeById, group)
                    });
                }
            }
            else
            {
                if (existingMap.TryGetValue((entry.EmployeeId, entry.Day), out var rec))
                    _context.PayrollDailyWageRecords.Remove(rec);
            }
        }

        return await _context.SaveChangesAsync();
    }

    /// <summary>新插行快照归口：该员工当月已有组内快照 → 沿用；否则当前在册归口属该组 → 用当前；兜底该组首个归口。</summary>
    private static string SnapshotFor(
        int employeeId,
        IReadOnlyDictionary<int, string> snapshotByEmp,
        IReadOnlyDictionary<int, SalaryMode?> currentModeById,
        PayrollWageGroup group)
    {
        if (snapshotByEmp.TryGetValue(employeeId, out var snap))
            return snap;
        var modeKey = currentModeById.TryGetValue(employeeId, out var mode) ? mode?.ToString() : null;
        if (!string.IsNullOrEmpty(modeKey) && group.ContainsModeKey(modeKey))
            return modeKey;
        return group.SalaryModes().First().ToString();
    }
}
