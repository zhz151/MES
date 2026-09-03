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
/// 集体计件月结服务（2026-09-03 引入）。
/// 集体边界 = 员工管理「岗位」：同一 Position 下 SalaryMode=PieceCollective 的员工构成一个集体（同岗位非集体者不参与）。
/// 月结：
/// 1. 岗位计件池 = 当月 5 类产量源（生产记录/酸洗入缸/酸洗完工/过程检验/成检）中凡有 ≥1 名该岗位集体成员写名的行，
///    按 成员份额（行额 / 写名总人头）归集到成员所属岗位池（个人计件等非集体成员的份额不入池，仍归各自通道）；
/// 2. 成员权重 w = 当月考勤实出勤小时(AttendanceRecord 求和，不做加班/半天折算) × 月度分值(1–10)；
/// 3. 个人月得 = 池[岗位] × w ÷ Σ同岗位集体成员 w。无出勤或未评分者 w=0 → 得 0，页面列出补齐后重算。
/// 产量计价走共享采集器 PieceRateCollector（与个人日结同源，防双通道口径漂移）；
/// 历史月按结算时现行单价估算草稿，保存落库 PayrollCollectiveWageRecord 冻结快照后不随改价/改产漂移。
/// </summary>
public class PayrollCollectiveService : IPayrollCollectiveService
{
    private readonly AppDbContext _context;

    public PayrollCollectiveService(AppDbContext context)
    {
        _context = context;
    }

    // ==================== 读取（月结视图） ====================

    public async Task<CollectiveMonthDto> GetMonthAsync(int year, int month)
    {
        if (year < 2000 || month < 1 || month > 12)
            throw new BusinessException("月份参数无效");

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var warnings = new List<string>();

        // 1. 当前在册集体成员（IsActive + PieceCollective + 有岗位）——引擎自动带出对象
        var archiveEmployees = await _context.Employees.AsNoTracking()
            .Where(e => e.IsActive && e.SalaryMode == SalaryMode.PieceCollective
                        && e.Position != null && e.Position != "")
            .OrderBy(e => e.Code)
            .ToListAsync();
        var archiveById = archiveEmployees.ToDictionary(e => e.Id);

        // 2. 当月该月已保存快照记录 → 历史归口员工并入显示（换岗/停用后历史月仍可见可改）
        var savedRecords = await _context.PayrollCollectiveWageRecords.AsNoTracking()
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

        // 4. 该月评分与考勤（引擎分配输入；评分对 archive ∪ history 均取，补录历史可回溯）
        var allIds = archiveById.Keys.Concat(historyById.Keys).Distinct().ToHashSet();
        var scoreByEmp = new Dictionary<int, decimal>();
        var attendanceByEmp = new Dictionary<int, decimal>();
        foreach (var chunk in allIds.Chunk(1000))
        {
            foreach (var s in await _context.PayrollCollectiveScores.AsNoTracking()
                         .Where(s => chunk.Contains(s.EmployeeId) && s.Year == year && s.Month == month)
                         .ToListAsync())
            {
                scoreByEmp[s.EmployeeId] = s.Score;
            }
            foreach (var a in await _context.AttendanceRecords.AsNoTracking()
                         .Where(a => chunk.Contains(a.EmployeeId) && a.AttendDate >= monthStart && a.AttendDate < monthEnd)
                         .ToListAsync())
            {
                attendanceByEmp[a.EmployeeId] = attendanceByEmp.TryGetValue(a.EmployeeId, out var prev)
                    ? prev + a.WorkHours : a.WorkHours;
            }
        }

        // 5. 岗位池：采集器对在册集体成员扫描当月产量源，成员份额归集到其岗位池
        var poolByPos = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var collector = new PieceRateCollector(_context);
        var result = await collector.CollectAsync(monthStart, monthEnd, archiveEmployees);
        if (result.UnpricedCount > 0)
            warnings.Add($"{year}年{month}月有 {result.UnpricedCount} 行产量/检验未能匹配计件单价或缺少数量（按0元计）");
        foreach (var row in result.Rows)
        {
            var share = row.Amount / row.TotalHeadcount;
            foreach (var emp in row.Eligible)
            {
                var pos = emp.Position;
                if (string.IsNullOrWhiteSpace(pos)) continue; // 目标集已保证有岗位，兜底
                poolByPos[pos] = poolByPos.TryGetValue(pos, out var prev) ? prev + share : share;
            }
        }

        // 6. 铺组：成员归属岗位 = 在册用档案 Position，历史快照员工用其当月快照 Position
        var groupByPos = new Dictionary<string, CollectiveGroupDto>(StringComparer.OrdinalIgnoreCase);
        CollectiveGroupDto GroupOf(string position)
        {
            if (!groupByPos.TryGetValue(position, out var g))
            {
                g = new CollectiveGroupDto { Position = position };
                groupByPos[position] = g;
            }
            return g;
        }

        foreach (var e in archiveEmployees.Concat(historyEmployees).DistinctBy(e => e.Id).OrderBy(e => e.Code))
        {
            var inArchive = archiveById.ContainsKey(e.Id);
            var position = inArchive ? e.Position!
                : savedByEmp.TryGetValue(e.Id, out var snap) ? snap.Position : null;
            if (string.IsNullOrWhiteSpace(position)) continue;

            var rec = savedByEmp.TryGetValue(e.Id, out var saved) ? saved : null;
            var hasScore = scoreByEmp.TryGetValue(e.Id, out var scoreVal);
            var hasAtt = attendanceByEmp.TryGetValue(e.Id, out var attVal);

            var member = new CollectiveMemberDto
            {
                EmployeeId = e.Id,
                EmployeeCode = e.Code,
                EmployeeName = e.Name,
                Position = position,
                EngineCovered = inArchive,
                Score = rec?.Score ?? (hasScore ? scoreVal : null),
                AttendanceHours = rec?.AttendanceHours ?? (hasAtt ? attVal : null),
                Weight = inArchive ? WeightOf(attVal, scoreVal) : null,
                SavedAmount = rec?.Amount
            };

            var group = GroupOf(position);
            group.Members.Add(member);
            if (inArchive)
                group.SumWeight += member.Weight ?? 0m;
        }

        // 7. 组内分配：个人月得 = 池 × w / Σw（引擎草稿仅对在册成员；Σw=0 → 得 0 需补齐）
        foreach (var group in groupByPos.Values)
        {
            group.PoolAmount = poolByPos.TryGetValue(group.Position, out var pool) ? pool : 0m;
            foreach (var member in group.Members.Where(m => m.EngineCovered))
            {
                member.EngineAmount = group.SumWeight > 0
                    ? group.PoolAmount * (member.Weight ?? 0m) / group.SumWeight
                    : 0m;
            }
        }

        return new CollectiveMonthDto
        {
            Year = year,
            Month = month,
            HasSaved = hasSaved,
            Groups = groupByPos.Values
                .OrderBy(g => g.Position, StringComparer.Ordinal)
                .ToList(),
            Warnings = warnings
        };
    }

    private static decimal WeightOf(decimal attendanceHours, decimal score)
        => attendanceHours > 0 ? attendanceHours * score : 0m;

    // ==================== 整月保存（月结快照） ====================

    public async Task<int> SaveMonthAsync(SaveCollectiveMonthDto request)
    {
        if (request.Year < 2000 || request.Month < 1 || request.Month > 12)
            throw new BusinessException("月份参数无效");

        var monthStart = new DateTime(request.Year, request.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var entryIds = request.Entries.Select(e => e.EmployeeId).Distinct().ToHashSet();
        var existing = new List<PayrollCollectiveWageRecord>();
        foreach (var chunk in entryIds.Chunk(1000))
        {
            existing.AddRange(await _context.PayrollCollectiveWageRecords
                .Where(r => chunk.Contains(r.EmployeeId) && r.WageYear == request.Year && r.WageMonth == request.Month)
                .ToListAsync());
        }
        var existingMap = existing.ToDictionary(r => r.EmployeeId);

        // 新插行快照要素：员工当前档案（在册岗位）、当月评分、当月实出勤小时（结算时冻结）
        var positionById = new Dictionary<int, string?>();
        var currentScoreById = new Dictionary<int, decimal?>();
        var attendanceById = new Dictionary<int, decimal>();
        foreach (var chunk in entryIds.Chunk(1000))
        {
            foreach (var emp in await _context.Employees.AsNoTracking()
                         .Where(e => chunk.Contains(e.Id))
                         .Select(e => new { e.Id, e.Position })
                         .ToListAsync())
            {
                positionById[emp.Id] = emp.Position;
            }
            foreach (var s in await _context.PayrollCollectiveScores.AsNoTracking()
                         .Where(s => chunk.Contains(s.EmployeeId) && s.Year == request.Year && s.Month == request.Month)
                         .ToListAsync())
            {
                currentScoreById[s.EmployeeId] = s.Score;
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
                if (existingMap.TryGetValue(entry.EmployeeId, out var rec))
                {
                    rec.Amount = entry.Amount.Value;
                    _context.PayrollCollectiveWageRecords.Update(rec);
                }
                else
                {
                    _context.PayrollCollectiveWageRecords.Add(new PayrollCollectiveWageRecord
                    {
                        EmployeeId = entry.EmployeeId,
                        WageYear = request.Year,
                        WageMonth = request.Month,
                        Position = positionById.TryGetValue(entry.EmployeeId, out var pos) && !string.IsNullOrWhiteSpace(pos)
                            ? pos!.Trim()
                            : string.Empty,
                        Score = currentScoreById.TryGetValue(entry.EmployeeId, out var sc) ? sc : null,
                        AttendanceHours = attendanceById.TryGetValue(entry.EmployeeId, out var att) && att > 0 ? att : null,
                        Amount = entry.Amount.Value
                    });
                }
            }
            else
            {
                if (existingMap.TryGetValue(entry.EmployeeId, out var rec))
                    _context.PayrollCollectiveWageRecords.Remove(rec);
            }
        }

        return await _context.SaveChangesAsync();
    }

    // ==================== 月度评分 ====================

    public async Task<CollectiveScoresDto> GetScoresAsync(int year, int month)
    {
        if (year < 2000 || month < 1 || month > 12)
            throw new BusinessException("月份参数无效");

        // 员工集合 = 当前在册集体成员 ∪ 当月已有评分员工（换岗/停用后历史评分仍可回溯补录）
        var archive = await _context.Employees.AsNoTracking()
            .Where(e => e.IsActive && e.SalaryMode == SalaryMode.PieceCollective
                        && e.Position != null && e.Position != "")
            .Select(e => new { e.Id, e.Code, e.Name, e.Position })
            .ToListAsync();
        var archiveById = archive.ToDictionary(e => e.Id);
        var existing = await _context.PayrollCollectiveScores.AsNoTracking()
            .Where(s => s.Year == year && s.Month == month)
            .Select(s => new { s.EmployeeId, s.Score })
            .ToListAsync();
        var scoreByEmp = existing.ToDictionary(s => s.EmployeeId, s => s.Score);

        var rows = new List<CollectiveScoreRowDto>();
        var seen = new HashSet<int>();
        foreach (var a in archive.OrderBy(e => e.Code))
        {
            seen.Add(a.Id);
            rows.Add(new CollectiveScoreRowDto
            {
                EmployeeId = a.Id,
                EmployeeCode = a.Code,
                EmployeeName = a.Name,
                Position = a.Position,
                Score = scoreByEmp.TryGetValue(a.Id, out var s) ? s : null
            });
        }
        // 历史评分员工（不在当前在册）补集
        var historyIds = existing.Select(e => e.EmployeeId)
            .Distinct().Where(id => !archiveById.ContainsKey(id)).ToHashSet();
        if (historyIds.Count > 0)
        {
            var history = await _context.Employees.AsNoTracking()
                .Where(e => historyIds.Contains(e.Id))
                .Select(e => new { e.Id, e.Code, e.Name, e.Position })
                .ToListAsync();
            foreach (var h in history.OrderBy(e => e.Code))
            {
                if (!seen.Add(h.Id)) continue;
                rows.Add(new CollectiveScoreRowDto
                {
                    EmployeeId = h.Id,
                    EmployeeCode = h.Code,
                    EmployeeName = h.Name,
                    Position = h.Position,
                    Score = scoreByEmp.TryGetValue(h.Id, out var s) ? s : null
                });
            }
        }

        return new CollectiveScoresDto
        {
            Year = year,
            Month = month,
            Rows = rows
        };
    }

    public async Task<int> SaveScoresAsync(SaveCollectiveScoresDto request)
    {
        if (request.Year < 2000 || request.Month < 1 || request.Month > 12)
            throw new BusinessException("月份参数无效");

        foreach (var entry in request.Entries)
        {
            // 分值范围 1–10，且最多 1 位小数（评分可如 8.5，不可 8.55）
            if (entry.Score is { } s && (s < 1m || s > 10m || (s * 10m) != Math.Floor(s * 10m)))
                throw new BusinessException($"员工 {entry.EmployeeId} 月度分值须在 1–10 且最多 1 位小数: {entry.Score}");
        }

        var entryIds = request.Entries.Select(e => e.EmployeeId).Distinct().ToHashSet();
        var existing = new List<PayrollCollectiveScore>();
        foreach (var chunk in entryIds.Chunk(1000))
        {
            existing.AddRange(await _context.PayrollCollectiveScores
                .Where(s => chunk.Contains(s.EmployeeId) && s.Year == request.Year && s.Month == request.Month)
                .ToListAsync());
        }
        var existingMap = existing.ToDictionary(s => s.EmployeeId);

        foreach (var entry in request.Entries)
        {
            if (entry.Score.HasValue)
            {
                if (existingMap.TryGetValue(entry.EmployeeId, out var rec))
                {
                    if (rec.Score != entry.Score.Value)
                    {
                        rec.Score = entry.Score.Value;
                        _context.PayrollCollectiveScores.Update(rec);
                    }
                }
                else
                {
                    _context.PayrollCollectiveScores.Add(new PayrollCollectiveScore
                    {
                        EmployeeId = entry.EmployeeId,
                        Year = request.Year,
                        Month = request.Month,
                        Score = entry.Score.Value
                    });
                }
            }
            else
            {
                if (existingMap.TryGetValue(entry.EmployeeId, out var rec))
                    _context.PayrollCollectiveScores.Remove(rec);
            }
        }

        return await _context.SaveChangesAsync();
    }
}
