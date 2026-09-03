namespace MES.Data.Entities.Payroll;

/// <summary>
/// 靠工计件月结记录 — 靠工计件工资按月快照（按员工按月的实得额，保存时结算要素冻结落库）。
/// 稀疏存储：仅金额 &gt; 0 的记录落库，空/0 = 无记录 = 当月 0 元。
/// AttendancePositions/AttendanceHours/AttendanceCoefficient 为保存当时的结算快照：
/// 员工日后改靠工岗位/补出勤/调系数，历史月仍按快照回溯显示。
/// </summary>
public class PayrollAttendanceWageRecord : BaseEntity
{
    /// <summary>员工ID（关联 Configuration.Employee，跨上下文只存 Id）</summary>
    public int EmployeeId { get; set; }

    /// <summary>结算年</summary>
    public int WageYear { get; set; }

    /// <summary>结算月</summary>
    public int WageMonth { get; set; }

    /// <summary>结算时靠工岗位快照（岗位英文 Key 逗号串，仅靠工计件模式使用）</summary>
    public string? AttendancePositions { get; set; }

    /// <summary>结算时当月实出勤小时快照</summary>
    public decimal? AttendanceHours { get; set; }

    /// <summary>结算时靠工系数快照（默认 1.0）</summary>
    public decimal? AttendanceCoefficient { get; set; }

    /// <summary>实得金额（&gt;0 落库，空/0 则删除）</summary>
    public decimal Amount { get; set; }
}
