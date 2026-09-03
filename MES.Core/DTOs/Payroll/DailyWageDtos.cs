using MES.Core.Constants;

namespace MES.Core.DTOs.Payroll;

/// <summary>每日工资月视图网格的一行（一个员工）</summary>
public class DailyWageEmployeeRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>岗位类别（PositionCategoryKey 英文 Key）</summary>
    public string? PositionCategory { get; set; }

    /// <summary>岗位（PositionKey 英文 Key）</summary>
    public string? Position { get; set; }

    /// <summary>该员工当月是否由引擎自动带出草稿（档案归口属该组的启用员工）；
    /// 仅历史归口并入显示的员工为 false（重算按钮只覆盖引擎覆盖的员工，避免误清历史已存）</summary>
    public bool EngineCovered { get; set; }

    /// <summary>日 1~31 → 已保存每日工资（null = 无记录 = 当日 0 元）</summary>
    public Dictionary<int, decimal?> DaySavedAmount { get; set; } = new();

    /// <summary>日 1~31 → 引擎自动带出每日工资草稿（仅档案归口属该组的员工有值）</summary>
    public Dictionary<int, decimal?> DayEngineAmount { get; set; } = new();

    /// <summary>已保存合计</summary>
    public decimal TotalSaved { get; set; }

    /// <summary>引擎草稿合计</summary>
    public decimal TotalEngine { get; set; }
}

/// <summary>每日工资月视图数据</summary>
public class DailyWageMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>当月该组是否已有保存记录（决定打开默认显示引擎草稿还是已保存快照）</summary>
    public bool HasSaved { get; set; }

    public List<DailyWageEmployeeRowDto> Employees { get; set; } = new();

    /// <summary>提示信息（缺工资标准的员工名、未定价行数等），页面 MudAlert 展示</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>保存条目（一个员工某一天）</summary>
public class DailyWageEntryDto
{
    public int EmployeeId { get; set; }
    public int Day { get; set; }

    /// <summary>当日工资额；null 或 0 = 清空该日记录（0 元）</summary>
    public decimal? Amount { get; set; }
}

/// <summary>每日工资整月保存请求</summary>
public class SaveDailyWageDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>目标分组（决定写入记录的 SalaryMode 快照归口）</summary>
    public PayrollWageGroup Group { get; set; }

    public List<DailyWageEntryDto> Entries { get; set; } = new();
}
