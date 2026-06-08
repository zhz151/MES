namespace MES.Core.DTOs;

/// <summary>
/// 冷轧计划看板行 DTO — 按规格维度聚合的时间桶重量分布
/// </summary>
public class ColdRollPlanRowDto
{
    // ===== 规格维度 =====
    public string ProcessType { get; set; } = "";          // 冷轧类型 (60冷轧/50冷轧/30冷轧/20冷轧/三辊冷轧/冷拔)
    public string BilletSpec { get; set; } = "";           // 轧坯规格（前一工序组制造规格）
    public string RollingSpec { get; set; } = "";          // 轧制规格（当前冷轧工序组制造规格）
    public bool IsFinished { get; set; }                    // 是否成品
    public string MergeDisplay { get; set; } = "";        // 合并: "BilletSpec×RollingSpec-是否成品"
    public string ShortDisplay { get; set; } = "";        // 简化: "外径1-外径2"

    // ===== 时间桶重量 (Kg) =====
    public decimal WeightProd { get; set; }               // 近日在轧
    public decimal WeightProdUrgent { get; set; }         // 近日在轧(急管)
    public decimal WeightWaitNear { get; set; }           // 近日待轧
    public decimal WeightWaitNearUrgent { get; set; }     // 近日待轧(急管)
    public decimal WeightToday { get; set; }              // 待轧今日(diff=1)
    public decimal WeightTomorrow { get; set; }           // 待轧明日(diff=2)
    public decimal WeightDayAfter { get; set; }           // 待轧后日(diff=3)
    public decimal WeightExt3 { get; set; }               // 待轧延3(diff=4)
    public decimal WeightExt4 { get; set; }               // 待轧延4(diff=5)
    public decimal WeightExt5 { get; set; }               // 待轧延5(diff=6)
    public decimal WeightDistant { get; set; }             // 远日量(diff>6)
    public decimal WeightTotal { get; set; }              // 工艺总量

    // ===== 批次统计 =====
    public int BatchCount { get; set; }
    public int KeyBatchCount { get; set; }

    // ===== 冷轧排程 =====
    /// <summary>在轧设备号（从 ProductionBatch 在产设备字段聚合）</summary>
    public string? MachineNo { get; set; }

    /// <summary>在轧要求（排程数据，仅供客户端排序/筛选）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string CompletionType { get; set; } = "None";
    /// <summary>待轧要求（排程数据，仅供客户端排序/筛选）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string RollType { get; set; } = "None";
    /// <summary>待轧序（排程数据，仅供客户端排序/筛选）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int RollOrder { get; set; }
    /// <summary>待轧设备号（排程数据，仅供客户端排序/筛选）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? SchedMachineNo { get; set; }
}
