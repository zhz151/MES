using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Payroll;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Payroll;
using MES.Data;
using MES.Data.Entities.Payroll;

namespace MES.Services.Payroll;

/// <summary>
/// 考勤服务 — 考勤表按月的读取与整月保存（稀疏存储：仅出勤记录落库）。
/// 员工信息跨上下文读 Configuration.Employee（只读，不建导航）。
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext _context;

    public AttendanceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AttendanceMonthDto> GetMonthAsync(int year, int month, string? keyword)
    {
        if (year < 2000 || month < 1 || month > 12)
            throw new BusinessException("月份参数无效");

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var empQuery = _context.Employees.AsNoTracking().Where(e => e.IsActive);
        if (!string.IsNullOrWhiteSpace(keyword))
            empQuery = empQuery.Where(e => e.Code.Contains(keyword) || e.Name.Contains(keyword));
        var employees = await empQuery.OrderBy(e => e.Code).ToListAsync();

        var empIds = employees.Select(e => e.Id).ToHashSet();
        // 大 IN 分片，防 SQL Server 2100 参数上限
        var records = new List<AttendanceRecord>();
        foreach (var chunk in empIds.Chunk(1000))
        {
            records.AddRange(await _context.AttendanceRecords.AsNoTracking()
                .Where(r => chunk.Contains(r.EmployeeId) && r.AttendDate >= monthStart && r.AttendDate < monthEnd)
                .ToListAsync());
        }

        var rows = employees.Select(e =>
        {
            var row = new AttendanceEmployeeRowDto
            {
                EmployeeId = e.Id,
                EmployeeCode = e.Code,
                EmployeeName = e.Name,
                PositionCategory = e.Department,
                Position = e.Position,
                DayHours = new Dictionary<int, decimal?>()
            };
            for (var d = 1; d <= daysInMonth; d++)
                row.DayHours[d] = null;

            var dayRecords = records.Where(r => r.EmployeeId == e.Id).ToList();
            foreach (var r in dayRecords)
                row.DayHours[r.AttendDate.Day] = r.WorkHours;

            row.AttendanceDays = dayRecords.Count;
            row.TotalHours = dayRecords.Sum(r => r.WorkHours);
            return row;
        }).ToList();

        return new AttendanceMonthDto { Year = year, Month = month, Employees = rows };
    }

    public async Task<int> SaveMonthAsync(SaveAttendanceDto request)
    {
        if (request.Year < 2000 || request.Month < 1 || request.Month > 12)
            throw new BusinessException("月份参数无效");

        var daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        var monthStart = new DateTime(request.Year, request.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        foreach (var entry in request.Entries)
        {
            if (entry.Day < 1 || entry.Day > daysInMonth)
                throw new BusinessException($"日期 {request.Year}-{request.Month}-{entry.Day} 超出当月范围");
            if (entry.WorkHours is < 0 or > 24)
                throw new BusinessException($"员工 {entry.EmployeeId} 出勤小时 {entry.WorkHours} 必须在 0~24 之间");
        }

        // 加载该月相关员工的现有记录做 upsert（避免逐条查库）
        var empIds = request.Entries.Select(e => e.EmployeeId).Distinct().ToHashSet();
        var existing = new List<AttendanceRecord>();
        foreach (var chunk in empIds.Chunk(1000))
        {
            existing.AddRange(await _context.AttendanceRecords
                .Where(r => chunk.Contains(r.EmployeeId) && r.AttendDate >= monthStart && r.AttendDate < monthEnd)
                .ToListAsync());
        }
        var existingMap = existing.ToDictionary(r => (r.EmployeeId, r.AttendDate.Day));

        foreach (var entry in request.Entries)
        {
            if (entry.WorkHours is > 0)
            {
                if (existingMap.TryGetValue((entry.EmployeeId, entry.Day), out var rec))
                {
                    rec.WorkHours = entry.WorkHours.Value;
                    _context.AttendanceRecords.Update(rec);
                }
                else
                {
                    _context.AttendanceRecords.Add(new AttendanceRecord
                    {
                        EmployeeId = entry.EmployeeId,
                        AttendDate = new DateTime(request.Year, request.Month, entry.Day),
                        WorkHours = entry.WorkHours.Value
                    });
                }
            }
            else
            {
                if (existingMap.TryGetValue((entry.EmployeeId, entry.Day), out var rec))
                    _context.AttendanceRecords.Remove(rec);
            }
        }

        return await _context.SaveChangesAsync();
    }
}
