namespace MES.Core.DTOs.Payroll;

/// <summary>考勤月视图网格的一行（一个员工）</summary>
public class AttendanceEmployeeRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>岗位类别（存 Employee.Department 的 PositionCategoryKey 英文 Key）</summary>
    public string? PositionCategory { get; set; }

    /// <summary>岗位（PositionKey 英文 Key）</summary>
    public string? Position { get; set; }

    /// <summary>日 1~31 → 出勤小时（null = 无记录 = 未出勤）</summary>
    public Dictionary<int, decimal?> DayHours { get; set; } = new();

    /// <summary>出勤天数（WorkHours &gt; 0 的天数）</summary>
    public int AttendanceDays { get; set; }

    /// <summary>总小时</summary>
    public decimal TotalHours { get; set; }
}

/// <summary>整月考勤数据（月视图网格）</summary>
public class AttendanceMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<AttendanceEmployeeRowDto> Employees { get; set; } = new();
}

/// <summary>保存条目（一个员工某一天）</summary>
public class AttendanceEntryDto
{
    public int EmployeeId { get; set; }
    public int Day { get; set; }

    /// <summary>出勤小时；null 或 0 = 清空该日记录（未出勤）</summary>
    public decimal? WorkHours { get; set; }
}

/// <summary>整月考勤保存请求</summary>
public class SaveAttendanceDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<AttendanceEntryDto> Entries { get; set; } = new();
}
