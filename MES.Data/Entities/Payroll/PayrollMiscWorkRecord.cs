namespace MES.Data.Entities.Payroll;

/// <summary>
/// 杂辅工记录 — 登记员工每天做的杂项辅助工作（台账流水，一条 = 一段任务）。
/// 允许同一员工同一天多条（每人每天可多条，无唯一约束）。
/// Amount 为手工录入的金额源头（保留小数，不做整元取整）；被月工资汇总按员工当月求和计入应发。
/// </summary>
public class PayrollMiscWorkRecord : BaseEntity
{
    /// <summary>员工ID（关联 Configuration.Employee，跨上下文只存 Id）</summary>
    public int EmployeeId { get; set; }

    /// <summary>杂辅日期</summary>
    public DateTime WorkDate { get; set; }

    /// <summary>杂辅内容（自由文本，可含逗号复合段，如「喷码2小时,修磨3小时」）</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>小时数（保留 1.5/7.5 半小时等小数）</summary>
    public decimal Hours { get; set; }

    /// <summary>杂辅工资（手工录入金额源头，保留小数）</summary>
    public decimal Amount { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
