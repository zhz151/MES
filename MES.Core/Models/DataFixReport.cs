namespace MES.Core.Models;

/// <summary>
/// 数据修复报告
/// </summary>
public class DataFixReport
{
    /// <summary>组内序号修复条数</summary>
    public int SequenceNumbersFixed { get; set; }

    /// <summary>工序组工段步骤号连续化重排条数（步骤号变更的工段数）</summary>
    public int ProcessGroupSectionNumbersFixed { get; set; }

    /// <summary>工段委外状态修复条数</summary>
    public int OutsourceStatusFixed { get; set; }

    /// <summary>批次跟踪字段修复条数</summary>
    public int BatchTrackingFixed { get; set; }

    /// <summary>设备日期字段修复条数</summary>
    public int EquipmentFixed { get; set; }

    /// <summary>订单客户快照字段修复条数</summary>
    public int SalesOrderSnapshotFixed { get; set; }

    /// <summary>总修复条数</summary>
    public int Total => SequenceNumbersFixed + ProcessGroupSectionNumbersFixed + OutsourceStatusFixed + BatchTrackingFixed
                        + EquipmentFixed + SalesOrderSnapshotFixed;
}
