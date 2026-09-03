namespace MES.Data.Entities.Payroll;

/// <summary>
/// 集体计件月结记录 — 集体计件工资按月快照（按员工按月的实得额，保存时结算要素冻结落库）。
/// 稀疏存储：仅金额 &gt; 0 的记录落库，空/0 = 无记录 = 当月 0 元。
/// Position/Score/AttendanceHours 为保存当时的结算快照：员工日后换岗/改分/补出勤，历史月仍按快照回溯显示。
/// </summary>
public class PayrollCollectiveWageRecord : BaseEntity
{
    /// <summary>员工ID（关联 Configuration.Employee，跨上下文只存 Id）</summary>
    public int EmployeeId { get; set; }

    /// <summary>结算年</summary>
    public int WageYear { get; set; }

    /// <summary>结算月</summary>
    public int WageMonth { get; set; }

    /// <summary>结算时岗位 Key 快照（Position 字典 Key，nvarchar(50)）</summary>
    public string Position { get; set; } = string.Empty;

    /// <summary>结算时月度分值快照（1–10，1 位小数如 8.5；无评分=null）</summary>
    public decimal? Score { get; set; }

    /// <summary>结算时当月实出勤小时快照</summary>
    public decimal? AttendanceHours { get; set; }

    /// <summary>实得金额（&gt;0 落库，空/0 则删除）</summary>
    public decimal Amount { get; set; }
}
