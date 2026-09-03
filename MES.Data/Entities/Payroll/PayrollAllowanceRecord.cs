namespace MES.Data.Entities.Payroll;

/// <summary>
/// 津贴与处罚 — 月度金额录入表（宽表固定 9 列，每人每月一行）。
/// 列语义与 Excel《津贴与处罚.xlsx》一致：满勤奖/工龄奖/夜班津贴/岗位补贴/高温费/工伤补贴/带班费/处罚/代缴社保。
/// 金额强制整元（用户拍板，decimal.Round 到元 AwayFromZero），空 = null（等价 0 元），不允许负数。
/// 员工 + 结算年月唯一 → 整月 upsert（月历 = IsActive 在册员工 ∪ 当月已有记录员工，含停用历史行仍可改）。
/// 被月工资汇总读取并入员工应发/实发（处罚/代缴以扣减语义参与）。
/// </summary>
public class PayrollAllowanceRecord : BaseEntity
{
    /// <summary>员工ID（关联 Configuration.Employee，跨上下文只存 Id）</summary>
    public int EmployeeId { get; set; }

    /// <summary>结算年</summary>
    public int Year { get; set; }

    /// <summary>结算月</summary>
    public int Month { get; set; }

    /// <summary>满勤奖（元，整元）</summary>
    public decimal? FullAttendanceBonus { get; set; }

    /// <summary>工龄奖（元，整元）</summary>
    public decimal? SeniorityBonus { get; set; }

    /// <summary>夜班津贴（元，整元）</summary>
    public decimal? NightShiftAllowance { get; set; }

    /// <summary>岗位补贴（元，整元）</summary>
    public decimal? PositionAllowance { get; set; }

    /// <summary>高温费（元，整元）</summary>
    public decimal? HighTempAllowance { get; set; }

    /// <summary>工伤补贴（元，整元）</summary>
    public decimal? InjurySubsidy { get; set; }

    /// <summary>带班费（元，整元）</summary>
    public decimal? LeadBonus { get; set; }

    /// <summary>处罚（元，整元，正数录入、列名表扣减语义）</summary>
    public decimal? Penalty { get; set; }

    /// <summary>代缴社保（元，整元，正数录入、列名表扣减语义）</summary>
    public decimal? SocialSecurity { get; set; }
}
