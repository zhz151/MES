namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 批次计划产量目标 — 每个工段在本次计划周期内的日产目标(吨)
/// </summary>
public class BatchPlanTarget : BaseEntity
{
    /// <summary>工段名称（对应 Tab 名称，如"60冷轧"、"荒管检"）</summary>
    public string SectionName { get; set; } = string.Empty;

    /// <summary>日产目标(吨)</summary>
    public decimal DailyTarget { get; set; }
}
