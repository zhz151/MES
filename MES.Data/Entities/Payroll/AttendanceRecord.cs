namespace MES.Data.Entities.Payroll;

/// <summary>
/// 考勤记录 — 员工按日出勤小时数（工资结算上下文的出勤基础数据，人工录入或 Excel 导入）。
/// 稀疏存储：仅出勤（WorkHours > 0）的记录落库，空白日 = 无记录 = 未出勤。
/// 出勤天数 = COUNT(*)，总小时 = SUM(WorkHours)。
/// </summary>
public class AttendanceRecord : BaseEntity
{
    /// <summary>员工ID（关联 Configuration.Employee，跨上下文只存 Id）</summary>
    public int EmployeeId { get; set; }

    /// <summary>出勤日期</summary>
    public DateTime AttendDate { get; set; }

    /// <summary>出勤小时（0~24，支持 0.5 半天）</summary>
    public decimal WorkHours { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
