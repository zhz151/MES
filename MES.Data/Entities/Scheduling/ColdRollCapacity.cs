namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 冷轧产能配置表 —— 冷轧排程保存时自动反哺的产能档案（机台×规格 单机单日量）。
/// 粒度与冷轧排程小表 ColdRollSpecSchedule 四维一一对应 (ProcessType, BilletSpec, RollingSpec, IsFinished)，
/// DailyOutput 单位 kg/天/单机，与排程小表 DailyOutput 口径一致。
/// SampleCount/LastConfirmedAt 本期仅记录不消费，作为样本置信度权重为后续产能聚合/排程建议铺路。
/// </summary>
public class ColdRollCapacity : BaseEntity
{
    /// <summary>冷轧类型（ProcessKeys 英文 Key，如 ColdRoll60）</summary>
    public string ProcessType { get; set; } = "";

    /// <summary>轧坯规格（前一工序组制造规格，如 "219*8"）</summary>
    public string BilletSpec { get; set; } = "";

    /// <summary>轧制规格（当前冷轧工序组制造规格）</summary>
    public string RollingSpec { get; set; } = "";

    /// <summary>是否成品（最后工序组）</summary>
    public bool IsFinished { get; set; }

    /// <summary>常用机台设备号（多台分号分隔，与排程小表一致）</summary>
    public string? MachineNo { get; set; }

    /// <summary>单机单日量（kg/天）</summary>
    public decimal? DailyOutput { get; set; }

    /// <summary>该维度以有产能信息被反哺的次数</summary>
    public int SampleCount { get; set; }

    /// <summary>最近一次反哺时间（最近确认/填写该维度产能）</summary>
    public DateTimeOffset? LastConfirmedAt { get; set; }
}
