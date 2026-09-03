namespace MES.Data.Entities.Payroll;

/// <summary>
/// 月度评分 — 集体计件月结的成员权重分（1–10，可 1 位小数如 8.5；评定机制业务自理，系统仅录入保存）。
/// 同一员工同一结算月仅一条（EmployeeId + Year + Month 唯一），供月结分配权重 w = 出勤小时 × 分值 使用。
/// </summary>
public class PayrollCollectiveScore : BaseEntity
{
    /// <summary>员工ID（关联 Configuration.Employee，跨上下文只存 Id）</summary>
    public int EmployeeId { get; set; }

    /// <summary>结算年</summary>
    public int Year { get; set; }

    /// <summary>结算月</summary>
    public int Month { get; set; }

    /// <summary>月度分值（1–10，1 位小数，decimal(3,1)）</summary>
    public decimal Score { get; set; }
}
