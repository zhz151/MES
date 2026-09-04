using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Payroll;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;

namespace MES.Services.Payroll;

/// <summary>
/// 靠工计件月结服务（2026-09-03 引入）。
/// 靠工工资（月）= 靠工岗位当月平均小时工资 × 本人当月实出勤小时 × 靠工系数；
/// 平均小时工资 = 一个或多个选中岗位（只计算件的：个人计件 + 集体计件并集，不分档）当月计件总工资
/// ÷ 同批岗位的计件人员总出勤小时（分子分母各自合并成一个总平均，不逐岗重复计酬）。
/// 计件总工资/计件人员出勤由共享采集器 PieceRateCollector + 出勤汇总得到（与个人日结/集体月结同源，防双通道口径漂移）；
/// 数值上 positionPay = 该岗计件员工（个人计件 + 集体计件）从当月产量源所分全部份额，自洽不含靠工/计时写名的空耗。
/// 靠工不与集体/个人分配冲突（靠工按小时参照另计，不抽岗位池份子）——用户定稿口径。
/// 保存落库 PayrollAttendanceWageRecord 冻结快照后不随改产/改薪漂移；无评分维度（靠工不评分）。
/// </summary>
public class PayrollAttendanceService : IPayrollAttendanceService
{
    private readonly AppDbContext _context;

    public PayrollAttendanceService(AppDbContext context)
    {
        _context = context;
    }

    // ==================== 读取（月结视图） ====================

    public async Task<AttendanceWageMonthDto> GetMonthAsync(int year, int month)
    {
        if (year < 2000 || month < 1 || month > 12)
            throw new BusinessException("月份参数无效");

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var warnings = new List<string>();

        // 1. 当前在册靠工员工（IsActive + PieceAttendance）——含未配岗者（便于提示补岗），引擎自动带出对象
        var archiveEmployees = await _context.Employees.AsNoTracking()
            .Where(e => e.IsActive && e.SalaryMode == SalaryMode.PieceAttendance)
            .OrderBy(e => e.Code)
            .ToListAsync();
        var archiveById = archiveEmployees.ToDictionary(e => e.Id);

        // 2. 当月该月已保存快照记录 → 历史归口员工并入显示（停用/换模式后历史月仍可见可改）
        var savedRecords = await _context.PayrollAttendanceWageRecords.AsNoTracking()
            .Where(r => r.WageYear == year && r.WageMonth == month)
            .ToListAsync();
        var hasSaved = savedRecords.Count > 0;
        var savedByEmp = savedRecords.ToDictionary(r => r.EmployeeId);

        // 3. 历史快照员工集合（补进显示）；archive 成员已含
        var historyIds = savedRecords.Select(r => r.EmployeeId)
            .Distinct().Where(id => !archiveById.ContainsKey(id)).ToHashSet();
        var historyEmployees = new List<Employee>();
        foreach (var chunk in historyIds.Chunk(1000))
        {
            historyEmployees.AddRange(await _context.Employees.AsNoTracking()
                .Where(e => chunk.Contains(e.Id))
                .ToListAsync());
        }
        var historyById = historyEmployees.ToDictionary(e => e.Id);

        // 4. 靠工员工当月出勤（引擎输入之一；对 archive ∪ history 均取，补录历史可回溯）
        var attendanceIds = archiveById.Keys.Concat(historyById.Keys).Distinct().ToHashSet();
        var attendanceByEmp = new Dictionary<int, decimal>();
        foreach (var chunk in attendanceIds.Chunk(1000))
        {
            foreach (var a in await _context.AttendanceRecords.AsNoTracking()
                         .Where(a => chunk.Contains(a.EmployeeId) && a.AttendDate >= monthStart && a.AttendDate < monthEnd)
                         .ToListAsync())
            {
                attendanceByEmp[a.EmployeeId] = attendanceByEmp.TryGetValue(a.EmployeeId, out var prev)
                    ? prev + a.WorkHours : a.WorkHours;
            }
        }

        // 5. 计件员工全集 P = 当前在册 个人计件/集体计件 + 有岗位 → 岗位计件总工资 positionPay（采集器一次扫描，按档案岗位归集成员份额）
        //    数值上 = 该岗个人计件总额 + 该岗集体 Pool（计件人员从当月 5 类产量源所得全部份额），与集体 Pool 口径一致自洽
        var pieceEmployees = await _context.Employees.AsNoTracking()
            .Where(e => e.IsActive
                        && (e.SalaryMode == SalaryMode.PieceIndividual || e.SalaryMode == SalaryMode.PieceCollective)
                        && e.Position != null && e.Position != "")
            .ToListAsync();
        var positionPay = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (pieceEmployees.Count > 0)
        {
            var collector = new PieceRateCollector(_context);
            var result = await collector.CollectAsync(monthStart, monthEnd, pieceEmployees);
            if (result.UnpricedCount > 0)
                warnings.Add($"{year}年{month}月有 {result.UnpricedCount} 行产量/检验记录到数量但未匹配到计件单价（按0元计，可能影响靠工参照时薪）");
            foreach (var row in result.Rows)
            {
                var share = row.Amount / row.TotalHeadcount;
                foreach (var emp in row.Eligible)
                {
                    var pos = emp.Position;
                    if (string.IsNullOrWhiteSpace(pos)) continue; // 目标集已保证有岗位，兜底
                    positionPay[pos] = positionPay.TryGetValue(pos, out var prev) ? prev + share : share;
                }
            }
        }

        // 6. 岗位计件人员出勤 positionHours：P 中该岗位员工当月 AttendanceRecord 求和（仅计件人员，靠工/计时/日薪/月薪不进分母）
        var positionHours = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var pieceById = pieceEmployees.ToDictionary(e => e.Id);
        foreach (var chunk in pieceById.Keys.Chunk(1000))
        {
            var hoursByEmp = new Dictionary<int, decimal>();
            foreach (var a in await _context.AttendanceRecords.AsNoTracking()
                         .Where(a => chunk.Contains(a.EmployeeId) && a.AttendDate >= monthStart && a.AttendDate < monthEnd)
                         .ToListAsync())
            {
                hoursByEmp[a.EmployeeId] = hoursByEmp.TryGetValue(a.EmployeeId, out var prev)
                    ? prev + a.WorkHours : a.WorkHours;
            }
            foreach (var kv in hoursByEmp)
            {
                var pos = pieceById[kv.Key].Position;
                if (string.IsNullOrWhiteSpace(pos)) continue;
                positionHours[pos] = positionHours.TryGetValue(pos, out var prev) ? prev + kv.Value : kv.Value;
            }
        }

        // 7. 铺行：靠工员工行；在册行引擎按 实时考勤 × 实时系数 重算草稿，历史快照行仅回溯已保存
        var rows = new List<AttendanceWageRowDto>();
        foreach (var e in archiveEmployees.Concat(historyEmployees).DistinctBy(e => e.Id).OrderBy(e => e.Code))
        {
            var inArchive = archiveById.ContainsKey(e.Id);
            var rec = savedByEmp.TryGetValue(e.Id, out var saved) ? saved : null;

            // 选中岗位合并时薪：Σ分子(岗位计件总工资) / Σ分母(岗位计件人员出勤)；分母 0 → null 需补齐数据
            var positions = SplitPositions(e.AttendancePositions);
            decimal? avg = null;
            decimal sumPay = 0m, sumHours = 0m;
            if (positions.Count == 0)
            {
                warnings.Add($"{e.Code} {e.Name} 未设置靠工岗位，无法计算靠工工资");
            }
            else
            {
                foreach (var p in positions)
                {
                    sumPay += positionPay.TryGetValue(p, out var pay) ? pay : 0m;
                    sumHours += positionHours.TryGetValue(p, out var hrs) ? hrs : 0m;
                }
                // 分母 0（无计件人员出勤）→ 平均时薪留 null（行级备注提示），不再做整页提醒
                if (sumHours > 0)
                    avg = sumPay / sumHours;
            }

            var actualAtt = attendanceByEmp.TryGetValue(e.Id, out var attVal) ? attVal : 0m;
            var actualCoeff = e.AttendanceCoefficient ?? 1.0m;
            decimal? engineAmount = null;
            if (inArchive && avg.HasValue)
            {
                engineAmount = avg.Value * actualAtt * actualCoeff;
            }

            rows.Add(new AttendanceWageRowDto
            {
                EmployeeId = e.Id,
                EmployeeCode = e.Code,
                EmployeeName = e.Name,
                EngineCovered = inArchive,
                AttendancePositions = inArchive ? e.AttendancePositions
                    : (rec?.AttendancePositions),
                AttendanceHours = inArchive
                    ? (rec?.AttendanceHours ?? (actualAtt > 0 ? actualAtt : null))
                    : rec?.AttendanceHours,
                AttendanceCoefficient = inArchive
                    ? (rec?.AttendanceCoefficient ?? e.AttendanceCoefficient)
                    : rec?.AttendanceCoefficient,
                AvgHourlyWage = inArchive ? avg : null,
                EngineAmount = engineAmount,
                SavedAmount = rec?.Amount
            });
        }

        return new AttendanceWageMonthDto
        {
            Year = year,
            Month = month,
            HasSaved = hasSaved,
            Rows = rows,
            Warnings = warnings
        };
    }

    private static List<string> SplitPositions(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

    // ==================== 整月保存（月结快照） ====================

    public async Task<int> SaveMonthAsync(SaveAttendanceWageDto request)
    {
        if (request.Year < 2000 || request.Month < 1 || request.Month > 12)
            throw new BusinessException("月份参数无效");

        var monthStart = new DateTime(request.Year, request.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var entryIds = request.Entries.Select(e => e.EmployeeId).Distinct().ToHashSet();
        var existing = new List<PayrollAttendanceWageRecord>();
        foreach (var chunk in entryIds.Chunk(1000))
        {
            existing.AddRange(await _context.PayrollAttendanceWageRecords
                .Where(r => chunk.Contains(r.EmployeeId) && r.WageYear == request.Year && r.WageMonth == request.Month)
                .ToListAsync());
        }
        var existingMap = existing.ToDictionary(r => r.EmployeeId);

        // 新插行快照要素：员工当前档案（靠工岗位、靠工系数）、当月实出勤小时（结算时冻结）
        var positionsById = new Dictionary<int, string?>();
        var coefficientById = new Dictionary<int, decimal?>();
        var attendanceById = new Dictionary<int, decimal>();
        foreach (var chunk in entryIds.Chunk(1000))
        {
            foreach (var emp in await _context.Employees.AsNoTracking()
                         .Where(e => chunk.Contains(e.Id))
                         .Select(e => new { e.Id, e.AttendancePositions, e.AttendanceCoefficient })
                         .ToListAsync())
            {
                positionsById[emp.Id] = emp.AttendancePositions;
                coefficientById[emp.Id] = emp.AttendanceCoefficient;
            }
            foreach (var a in await _context.AttendanceRecords.AsNoTracking()
                         .Where(a => chunk.Contains(a.EmployeeId)
                                     && a.AttendDate >= monthStart && a.AttendDate < monthEnd)
                         .ToListAsync())
            {
                attendanceById[a.EmployeeId] = attendanceById.TryGetValue(a.EmployeeId, out var prev)
                    ? prev + a.WorkHours : a.WorkHours;
            }
        }

        foreach (var entry in request.Entries)
        {
            if (entry.Amount is > 0)
            {
                var pos = positionsById.TryGetValue(entry.EmployeeId, out var p) ? p : null;
                if (!string.IsNullOrWhiteSpace(pos)) pos = pos.Trim();
                var att = attendanceById.TryGetValue(entry.EmployeeId, out var h) && h > 0 ? h : (decimal?)null;
                var coeff = coefficientById.TryGetValue(entry.EmployeeId, out var c) ? c : null;

                if (existingMap.TryGetValue(entry.EmployeeId, out var rec))
                {
                    rec.Amount = entry.Amount.Value;
                    _context.PayrollAttendanceWageRecords.Update(rec);
                }
                else
                {
                    _context.PayrollAttendanceWageRecords.Add(new PayrollAttendanceWageRecord
                    {
                        EmployeeId = entry.EmployeeId,
                        WageYear = request.Year,
                        WageMonth = request.Month,
                        AttendancePositions = pos,
                        AttendanceHours = att,
                        AttendanceCoefficient = coeff,
                        Amount = entry.Amount.Value
                    });
                }
            }
            else
            {
                if (existingMap.TryGetValue(entry.EmployeeId, out var rec))
                    _context.PayrollAttendanceWageRecords.Remove(rec);
            }
        }

        return await _context.SaveChangesAsync();
    }
}
