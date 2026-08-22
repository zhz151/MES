namespace MES.Core.DTOs.Scheduling;

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
    public decimal WeightProdUrgent { get; set; }         // 近日在轧(特急) = 正常流转∧关注==当前冷轧
    public decimal WeightProdUrgentSub { get; set; }      // 近日在轧(特急-) = 正常流转∧关注≠当前冷轧
    public decimal WeightProdUrgentOther { get; set; }    // 近日在轧(急) = 非正常流转
    public decimal WeightWaitNear { get; set; }           // 近日待轧
    public decimal WeightWaitNearUrgent { get; set; }     // 近日待轧(特急)
    public decimal WeightWaitNearBackUrgent { get; set; } // 近日待轧(特急-)
    public decimal WeightWaitNearOtherUrgent { get; set; } // 近日待轧(急)
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

    // ===== 冷轧排程 =====
    /// <summary>在轧单位或设备（优先批次委外单位 CurrentOutsource，为空回退在产设备号 CurrentEquipmentName，聚合去重）</summary>
    public string? MachineNo { get; set; }

    /// <summary>在轧要求（排程数据，仅供客户端排序/筛选）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string CompletionType { get; set; } = "None";
    /// <summary>待轧要求（排程数据，仅供客户端排序/筛选）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string RollType { get; set; } = "None";
    /// <summary>在轧侧是否存在待排实际批次且排程行档位非空（客户端据此决定「在轧要求」是否显示，否则留空便于区分在/不在排程计划）</summary>
    public bool ProdTierMatched { get; set; }
    /// <summary>待轧侧是否存在待排实际批次且排程行档位非空（客户端据此决定「待轧要求」是否显示）</summary>
    public bool WaitTierMatched { get; set; }
    /// <summary>待轧设备号（排程数据，仅供客户端排序/筛选）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? SchedMachineNo { get; set; }
    /// <summary>单机单日量（kg/天，排程数据，仅供客户端排序/筛选）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public decimal? DailyOutput { get; set; }
}
