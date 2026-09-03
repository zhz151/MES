namespace MES.Data.Entities.Payroll;

/// <summary>
/// 每日工资记录 — 非计件工资 / 个人计件工资的按月快照（按员工按日的工资额，保存时归口快照落库）。
/// 稀疏存储：仅当日金额 &gt; 0 的记录落库，空白日 = 无记录 = 当日 0 元。
/// SalaryMode 为保存当时的薪酬归口快照：员工日后切换归口，历史记录仍按快照归口显示，可回溯。
/// </summary>
public class PayrollDailyWageRecord : BaseEntity
{
    /// <summary>员工ID（关联 Configuration.Employee，跨上下文只存 Id）</summary>
    public int EmployeeId { get; set; }

    /// <summary>工资日期</summary>
    public DateTime WageDate { get; set; }

    /// <summary>当日工资额（&gt;0 落库，空/0 则删除）</summary>
    public decimal Amount { get; set; }

    /// <summary>保存时归口快照（SalaryMode 枚举英文名，nvarchar(20)）</summary>
    public string SalaryMode { get; set; } = string.Empty;

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
