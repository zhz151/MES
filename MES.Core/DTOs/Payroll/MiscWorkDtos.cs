namespace MES.Core.DTOs.Payroll;

/// <summary>杂辅工记录-一行（一条杂辅任务登记）</summary>
public class MiscWorkRowDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>杂辅日期</summary>
    public DateTime WorkDate { get; set; }

    /// <summary>杂辅内容（自由文本，可含逗号复合段）</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>小时数（保留 1.5/7.5 半小时等小数）</summary>
    public decimal Hours { get; set; }

    /// <summary>杂辅工资（手工录入金额源头，保留小数）</summary>
    public decimal Amount { get; set; }

    public string? Remark { get; set; }
}

/// <summary>杂辅工记录-月视图数据（台账列表整月口径）</summary>
public class MiscWorkMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>当月记录条数（整月口径，不随页内关键词筛选变化）</summary>
    public int RecordCount { get; set; }

    /// <summary>当月总小时（原样求和，不取整）</summary>
    public decimal TotalHours { get; set; }

    /// <summary>当月杂辅总金额（原样求和，不取整）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>当月记录（按 日期+工号+Id 稳定升序）</summary>
    public List<MiscWorkRowDto> Records { get; set; } = new();
}

/// <summary>杂辅工记录-保存请求（Id=0 新增，>0 编辑；编辑不改员工）</summary>
public class MiscWorkRecordInputDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }

    /// <summary>杂辅日期</summary>
    public DateTime WorkDate { get; set; }

    /// <summary>杂辅内容（trim 后非空）</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>小时数（&gt;=0，可小数）</summary>
    public decimal Hours { get; set; }

    /// <summary>杂辅工资（&gt;=0，可小数，手工录入源头）</summary>
    public decimal Amount { get; set; }

    public string? Remark { get; set; }
}
