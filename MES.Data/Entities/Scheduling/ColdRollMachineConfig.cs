namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 冷轧设备机台数配置表 —— 按单冷轧类型的机台数参数（排程建议引擎产能平衡输入）。
/// 覆盖关系：60 冷轧可干 50 冷轧的活、30 冷轧可覆盖 20 冷轧的活（机台需求按机台类型组聚合）。
/// </summary>
public class ColdRollMachineConfig : BaseEntity
{
    /// <summary>机型（ProcessKeys 英文 Key：ColdRoll60/50/30/20/ThreeRollColdRoll/ColdDraw，唯一）</summary>
    public string ProcessType { get; set; } = "";

    /// <summary>本厂数量（该机型实有机台数）</summary>
    public int OwnedCount { get; set; }

    /// <summary>最小机台数（排程机台需求下限）</summary>
    public int MinMachines { get; set; }

    /// <summary>最大机台数（排程机台需求上限）</summary>
    public int MaxMachines { get; set; }

    /// <summary>估算单机日产（kg/天，平衡流转方式 A 兜底）</summary>
    public decimal? EstimatedDailyOutput { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
