namespace MES.Core.DTOs.Payroll;

/// <summary>津贴与处罚-月历一行（员工 + 9 个金额项目；金额整元，空=未填）</summary>
public class AllowanceRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>岗位类别（PositionCategoryKey 英文 Key，如 Workshop/QualityInspection，前端字典反查中文）</summary>
    public string? PositionCategory { get; set; }

    /// <summary>岗位（PositionKey 英文 Key，前端字典反查中文）</summary>
    public string? Position { get; set; }

    /// <summary>岗位备注（如 班长/电工/总调）</summary>
    public string? PositionRemark { get; set; }

    /// <summary>工资结算模式（SalaryMode 枚举名，如 PieceRate/CollectivePiece）</summary>
    public string? SalaryMode { get; set; }

    /// <summary>是否在册（false = 当月历史行但员工已停用，行内灰显可改）</summary>
    public bool IsActive { get; set; }

    /// <summary>满勤奖（元，整元，可空）</summary>
    public decimal? FullAttendanceBonus { get; set; }

    /// <summary>工龄奖（元，整元，可空）</summary>
    public decimal? SeniorityBonus { get; set; }

    /// <summary>夜班津贴（元，整元，可空）</summary>
    public decimal? NightShiftAllowance { get; set; }

    /// <summary>岗位补贴（元，整元，可空）</summary>
    public decimal? PositionAllowance { get; set; }

    /// <summary>高温费（元，整元，可空）</summary>
    public decimal? HighTempAllowance { get; set; }

    /// <summary>工伤补贴（元，整元，可空）</summary>
    public decimal? InjurySubsidy { get; set; }

    /// <summary>带班费（元，整元，可空）</summary>
    public decimal? LeadBonus { get; set; }

    /// <summary>处罚（元，整元，正数录入、列名表扣减语义）</summary>
    public decimal? Penalty { get; set; }

    /// <summary>代缴社保（元，整元，正数录入、列名表扣减语义）</summary>
    public decimal? SocialSecurity { get; set; }
}

/// <summary>津贴与处罚-月视图数据（员工月历网格，合计由前端 tfoot 现算不冗余）</summary>
public class AllowanceMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>当月员工行（IsActive 在册员工 ∪ 当月已有记录员工，按工号升序）</summary>
    public List<AllowanceRowDto> Rows { get; set; } = new();
}

/// <summary>津贴与处罚-整月保存请求（Year/Month + Rows；Rows 空=清空整月）</summary>
public class SaveAllowanceMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<AllowanceRowInputDto> Rows { get; set; } = new();
}

/// <summary>津贴与处罚-整月保存的一行（EmployeeId + 9 金额，空=null；全空=删除该员工当月行）</summary>
public class AllowanceRowInputDto
{
    public int EmployeeId { get; set; }

    public decimal? FullAttendanceBonus { get; set; }
    public decimal? SeniorityBonus { get; set; }
    public decimal? NightShiftAllowance { get; set; }
    public decimal? PositionAllowance { get; set; }
    public decimal? HighTempAllowance { get; set; }
    public decimal? InjurySubsidy { get; set; }
    public decimal? LeadBonus { get; set; }
    public decimal? Penalty { get; set; }
    public decimal? SocialSecurity { get; set; }
}
