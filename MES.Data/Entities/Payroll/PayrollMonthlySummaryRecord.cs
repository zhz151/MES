namespace MES.Data.Entities.Payroll;

/// <summary>
/// 月工资汇总快照 — 员工某结算月「完整应发/实发」汇总（每人每月一行，整月 upsert 替换）。
/// 由各子页（每日工资/集体计件月结/靠工计件月结/杂辅/津贴与处罚）+ 考勤天数聚合派生：
/// 基础工资按员工薪酬归口取当月各月页已保存金额（Fixed 取 Employee.MonthlyWage）；
/// 杂辅工资 = 当月杂辅台账合计；7 项正津贴取自津贴与处罚表当月行。
/// 列语义与《工资条及打印.xlsx》一致：处罚/代缴社保 **存负数**（正数录入、扣减语义），
/// 应发 = 基础 + 杂辅 + 7 项正津贴（不含处罚/代缴）；实发 = 应发 + 处罚 + 代缴。
/// 打印（全部/个人工资条）与数据工具均读本快照表，保证发放单与冻结口径一致。
/// </summary>
public class PayrollMonthlySummaryRecord : BaseEntity
{
    /// <summary>员工ID（关联 Configuration.Employee，跨上下文只存 Id）</summary>
    public int EmployeeId { get; set; }

    /// <summary>结算年</summary>
    public int Year { get; set; }

    /// <summary>结算月</summary>
    public int Month { get; set; }

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

    /// <summary>处罚（元，**存负数**，源表正数录入扣减语义）</summary>
    public decimal Penalty { get; set; }

    /// <summary>代缴社保（元，**存负数**，源表正数录入扣减语义）</summary>
    public decimal SocialSecurity { get; set; }

    /// <summary>应发工资及津贴 = 基础 + 杂辅 + 7 项正津贴（不含处罚/代缴）</summary>
    public decimal TotalPayable { get; set; }

    /// <summary>实发工资及津贴 = 应发 + 处罚 + 代缴（后两列存负）</summary>
    public decimal TotalPaid { get; set; }
}
