namespace MES.Core.DTOs.Batch;

/// <summary>月度委外数据行：委外单位 × 工段 组合（末行「合计」）。</summary>
public class SectionOutsourceMonthlyRowDto
{
    /// <summary>委外单位（合计行为"合计"）</summary>
    public string OutsourceVendor { get; set; } = string.Empty;

    /// <summary>工段（生产记录月度表全工段归行样式；合计行为"合计"）</summary>
    public string SectionName { get; set; } = string.Empty;

    /// <summary>各月发/回/退，长度恒 12，索引 0=1月…11=12月</summary>
    public List<SectionOutsourceMonthValueDto> Months { get; set; } = new();

    /// <summary>合计列（12 月发/回/退各自求和，仍三合一）</summary>
    public decimal TotalSend { get; set; }

    /// <summary>合计列（12 月回求和）</summary>
    public decimal TotalRecover { get; set; }

    /// <summary>合计列（12 月退求和）</summary>
    public decimal TotalUnprocessed { get; set; }

    /// <summary>「现在产」：当前未回收（回收+退回未达发出×0.99）的非厂内发出记录发出重量合计(kg)，与发出年度无关的实时存量</summary>
    public decimal NowInProduction { get; set; }
}

/// <summary>单月发/回/退三值（发=SectionOutsource.SendWeight、回=OutsourceRecovery.RecoveryWeight、退=OutsourceRecovery.UnprocessedWeight）。</summary>
public class SectionOutsourceMonthValueDto
{
    /// <summary>发出重量(kg)</summary>
    public decimal Send { get; set; }

    /// <summary>回收重量(kg)</summary>
    public decimal Recover { get; set; }

    /// <summary>非正常回收(退)重量(kg)</summary>
    public decimal Unprocessed { get; set; }
}
