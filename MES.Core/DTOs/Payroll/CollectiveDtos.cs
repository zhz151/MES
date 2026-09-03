namespace MES.Core.DTOs.Payroll;

/// <summary>集体计件月结-成员行（一个员工某岗位集体成员）</summary>
public class CollectiveMemberDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>归属岗位（PositionKey 英文 Key；历史快照员工 = 其当月快照岗位）</summary>
    public string Position { get; set; } = string.Empty;

    /// <summary>该员工当月是否由引擎自动带出草稿（当前在册集体计件员工）；
    /// 仅历史快照并入显示的员工为 false（引擎重算/分配只覆盖在册员工，避免误清历史已存）</summary>
    public bool EngineCovered { get; set; }

    /// <summary>当月分值（1–10，1 位小数如 8.5；未评分 = null）</summary>
    public decimal? Score { get; set; }

    /// <summary>当月实出勤小时（历史快照员工 = 结算时冻结值）</summary>
    public decimal? AttendanceHours { get; set; }

    /// <summary>权重 w = 出勤小时 × 分值（无出勤或未评分 = 0，得 0 需补齐后重算）</summary>
    public decimal? Weight { get; set; }

    /// <summary>引擎分配月得草稿 = 岗位池 × w / Σ同岗位 w（历史快照员工无引擎草稿 = null）</summary>
    public decimal? EngineAmount { get; set; }

    /// <summary>当月已保存月结金额（无记录 = null）</summary>
    public decimal? SavedAmount { get; set; }
}

/// <summary>集体计件月结-一个岗位集体（结算卡片数据）</summary>
public class CollectiveGroupDto
{
    /// <summary>岗位（PositionKey 英文 Key）</summary>
    public string Position { get; set; } = string.Empty;

    /// <summary>岗位计件池总额 = 当月 5 类产量源中该岗位成员写名行的成员份额合计</summary>
    public decimal PoolAmount { get; set; }

    /// <summary>同岗位集体成员权重和 Σ(出勤×分值)（仅当前在册成员参与分配）</summary>
    public decimal SumWeight { get; set; }

    public List<CollectiveMemberDto> Members { get; set; } = new();
}

/// <summary>集体计件月结月视图数据</summary>
public class CollectiveMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>当月是否已有保存记录（决定打开默认显示引擎草稿还是已保存快照）</summary>
    public bool HasSaved { get; set; }

    /// <summary>按岗位分组的结算卡片</summary>
    public List<CollectiveGroupDto> Groups { get; set; } = new();

    /// <summary>提示信息（未定价行数等），页面 MudAlert 展示</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>保存条目（一个员工整月一笔金额）</summary>
public class CollectiveMonthEntryDto
{
    public int EmployeeId { get; set; }

    /// <summary>实得金额；null 或 0 = 清空该员工当月记录（0 元）</summary>
    public decimal? Amount { get; set; }
}

/// <summary>集体计件月结整月保存请求</summary>
public class SaveCollectiveMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<CollectiveMonthEntryDto> Entries { get; set; } = new();
}

/// <summary>月度评分-一行（一个员工当月分值）</summary>
public class CollectiveScoreRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>岗位（PositionKey 英文 Key）</summary>
    public string? Position { get; set; }

    /// <summary>当月分值（1–10，1 位小数如 8.5；未评分 = null）</summary>
    public decimal? Score { get; set; }
}

/// <summary>月度评分读取结果</summary>
public class CollectiveScoresDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<CollectiveScoreRowDto> Rows { get; set; } = new();
}

/// <summary>月度评分保存条目（一个员工当月分值）</summary>
public class CollectiveScoreEntryDto
{
    public int EmployeeId { get; set; }

    /// <summary>分值（1–10，1 位小数如 8.5）；null = 清空该员工当月评分</summary>
    public decimal? Score { get; set; }
}

/// <summary>月度评分整月保存请求</summary>
public class SaveCollectiveScoresDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<CollectiveScoreEntryDto> Entries { get; set; } = new();
}
