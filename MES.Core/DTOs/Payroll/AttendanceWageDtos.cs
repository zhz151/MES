namespace MES.Core.DTOs.Payroll;

/// <summary>靠工计件月结-员工行</summary>
public class AttendanceWageRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>靠工岗位（岗位英文 Key 逗号串；历史快照员工 = 其当月快照值）</summary>
    public string? AttendancePositions { get; set; }

    /// <summary>该员工当月是否由引擎自动带出草稿（当前在册靠工计件员工）；
    /// 仅历史快照并入显示的员工为 false（引擎重算/全量重算只覆盖在册员工，避免误清历史已存）</summary>
    public bool EngineCovered { get; set; }

    /// <summary>当月实出勤小时（历史快照员工 = 结算时冻结值；无记录 = null）</summary>
    public decimal? AttendanceHours { get; set; }

    /// <summary>靠工系数（默认 1.0；历史快照员工 = 结算时冻结值）</summary>
    public decimal? AttendanceCoefficient { get; set; }

    /// <summary>靠工岗位当月合并平均小时工资 = Σ选中岗位计件总工资 ÷ Σ选中岗位计件人员总出勤小时
    /// （仅引擎可算时填充；未配岗/分母为 0 = null）</summary>
    public decimal? AvgHourlyWage { get; set; }

    /// <summary>引擎月得草稿 = 平均小时工资 × 本人当月出勤 × 靠工系数（历史快照员工无引擎草稿 = null）</summary>
    public decimal? EngineAmount { get; set; }

    /// <summary>当月已保存月结金额（无记录 = null）</summary>
    public decimal? SavedAmount { get; set; }
}

/// <summary>靠工计件月结月视图数据</summary>
public class AttendanceWageMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>当月是否已有保存记录（决定打开默认显示引擎草稿还是已保存快照）</summary>
    public bool HasSaved { get; set; }

    /// <summary>靠工员工结算行（当前在册靠工员工 ∪ 当月已有月结快照员工）</summary>
    public List<AttendanceWageRowDto> Rows { get; set; } = new();

    /// <summary>提示信息（未配岗/岗位计件数据缺失等），页面 MudAlert 展示</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>靠工月结保存条目（一个员工整月一笔金额）</summary>
public class AttendanceWageEntryDto
{
    public int EmployeeId { get; set; }

    /// <summary>实得金额；null 或 0 = 清空该员工当月记录（0 元）</summary>
    public decimal? Amount { get; set; }
}

/// <summary>靠工月结整月保存请求</summary>
public class SaveAttendanceWageDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<AttendanceWageEntryDto> Entries { get; set; } = new();
}
