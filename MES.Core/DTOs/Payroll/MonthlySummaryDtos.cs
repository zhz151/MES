namespace MES.Core.DTOs.Payroll;

/// <summary>
/// 月工资汇总-月视图一行（员工某结算月完整应发/实发）。
/// 列语义与《工资条及打印.xlsx》一致：处罚/代缴社保为负值（源表正数录入、扣减语义）。
/// 页面网格显示：金额列 0 留空（贴近 Excel 样式），工号/姓名/月份/出勤/应发/实发恒显。
/// </summary>
public class PayrollMonthlySummaryRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>岗位类别（PositionCategoryKey 英文 Key，前端字典反查中文）</summary>
    public string? PositionCategory { get; set; }

    /// <summary>岗位（PositionKey 英文 Key，前端字典反查中文）</summary>
    public string? Position { get; set; }

    /// <summary>工资结算模式（SalaryMode 枚举名，如 PieceRate/CollectivePiece）</summary>
    public string? SalaryMode { get; set; }

    /// <summary>是否在册（false = 当月历史行但员工已停用，行内灰显）</summary>
    public bool IsActive { get; set; }

    /// <summary>出勤天数（当月有考勤记录的日期数）</summary>
    public int AttendanceDays { get; set; }

    /// <summary>本月基础工资（按薪酬归口取当月已保存金额；Fixed=Employee.MonthlyWage）</summary>
    public decimal BaseWage { get; set; }

    /// <summary>本月杂辅工资（当月杂辅台账合计）</summary>
    public decimal MiscWorkAmount { get; set; }

    /// <summary>岗位补贴（元，正项）</summary>
    public decimal PositionAllowance { get; set; }

    /// <summary>工龄奖（元，正项）</summary>
    public decimal SeniorityBonus { get; set; }

    /// <summary>满勤奖（元，正项）</summary>
    public decimal FullAttendanceBonus { get; set; }

    /// <summary>带班费（元，正项）</summary>
    public decimal LeadBonus { get; set; }

    /// <summary>夜班津贴（元，正项）</summary>
    public decimal NightShiftAllowance { get; set; }

    /// <summary>高温费（元，正项）</summary>
    public decimal HighTempAllowance { get; set; }

    /// <summary>工伤补贴（元，正项）</summary>
    public decimal InjurySubsidy { get; set; }

    /// <summary>处罚（元，负值）</summary>
    public decimal Penalty { get; set; }

    /// <summary>代缴社保（元，负值）</summary>
    public decimal SocialSecurity { get; set; }

    /// <summary>应发工资及津贴 = 基础 + 杂辅 + 7 项正津贴（不含处罚/代缴）</summary>
    public decimal TotalPayable { get; set; }

    /// <summary>实发工资及津贴 = 应发 + 处罚 + 代缴（后两列存负）</summary>
    public decimal TotalPaid { get; set; }
}

/// <summary>月工资汇总-月视图数据（页面一次拉整月行；打印/数据工具读已保存快照）</summary>
public class MonthlySummaryMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>本月是否已生成保存快照（打印须先保存本月）</summary>
    public bool HasSaved { get; set; }

    /// <summary>当月汇总行（IsActive 在册员工 ∪ 当月任一来源有行，按工号升序）</summary>
    public List<PayrollMonthlySummaryRowDto> Rows { get; set; } = new();
}

/// <summary>月工资汇总-保存请求（仅年月；服务端重算整月并替换快照）</summary>
public class SaveMonthlySummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
}
