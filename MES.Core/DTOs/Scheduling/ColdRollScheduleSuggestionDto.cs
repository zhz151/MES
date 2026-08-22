namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 冷轧排程建议（机台类型组级）：特急锁定 → 流转保底 → 产能平衡 三步决策输出
/// </summary>
public class ColdRollScheduleSuggestionDto
{
    /// <summary>机台类型组显示名（冷轧5060/冷轧2030/冷轧三辊/冷拔）</summary>
    public string MachineType { get; set; } = "";

    /// <summary>组内机型 Key（覆盖关系聚合）</summary>
    public string[] MemberProcessTypes { get; set; } = Array.Empty<string>();

    /// <summary>组最小机台数（组内机型配置之和，无配置=0）</summary>
    public int MinMachines { get; set; }

    /// <summary>组最大机台数（组内机型配置之和，无配置=0）</summary>
    public int MaxMachines { get; set; }

    /// <summary>建议后机台数（决策档位下机台需求）</summary>
    public int MachineCount { get; set; }

    /// <summary>当前档位显示名（组内有排程行的最宽档；无则 "-"）</summary>
    public string CurrentTier { get; set; } = "-";

    /// <summary>建议档位显示名（急+ / 急+/急 / 急+/急/急- / 急+/急/急-/顺 / 全量 / -）</summary>
    public string SuggestedTier { get; set; } = "-";

    /// <summary>是否建议变更档位</summary>
    public bool TierChanged { get; set; }

    /// <summary>组内是否存在急+批次（档1）</summary>
    public bool HasUrgentPlus { get; set; }

    /// <summary>状态：OK / A(达不到最小) / A'(超上限) / B(流转不足) / B2(流转过剩)，可叠加如 A,B</summary>
    public string Status { get; set; } = "OK";

    /// <summary>中文矛盾文案</summary>
    public List<string> Conflicts { get; set; } = new();

    /// <summary>流转状态（5060=供给方 / 2030=需求方 / 其余 null）</summary>
    public FlowStateDto? FlowState { get; set; }

    /// <summary>组近6天可流转量（全部在轧/待轧批次重量，不论是否排程，展示）</summary>
    public decimal FlowTotalWeight { get; set; }

    /// <summary>组本次计划流转量（近6天批次按「建议档位」命中的重量 = 明细行计划量之和，展示）</summary>
    public decimal PlannedFlowWeight { get; set; }

    /// <summary>组在制量（PositionDiff==0，展示）</summary>
    public decimal InProcessWeight { get; set; }

    /// <summary>5060 组②流转平衡：在制行（IsFinished=false）档位（存储值；未拆档=null）</summary>
    public string? InProdTier { get; set; }

    /// <summary>5060 组②流转平衡：成品行（IsFinished=true）档位（存储值；未拆档=null）</summary>
    public string? FinishedTier { get; set; }

    /// <summary>四维行级建议（一键采用回填 save-all 用）</summary>
    public List<ColdRollScheduleSuggestionItemDto> Items { get; set; } = new();
}

/// <summary>
/// 冷轧排程建议（四维行级）
/// </summary>
public class ColdRollScheduleSuggestionItemDto
{
    public string ProcessType { get; set; } = "";
    public string BilletSpec { get; set; } = "";
    public string RollingSpec { get; set; } = "";
    public bool IsFinished { get; set; }
    public string ShortDisplay { get; set; } = "";
    public string MergeDisplay { get; set; } = "";

    /// <summary>本行有急+批次（任意 PositionDiff）</summary>
    public bool HasUrgentPlus { get; set; }

    /// <summary>建议在轧要求</summary>
    public string SuggestedCompletionType { get; set; } = "None";

    /// <summary>建议待轧要求</summary>
    public string SuggestedRollType { get; set; } = "None";

    /// <summary>有在轧批次（PositionDiff==0）</summary>
    public bool InProdExists { get; set; }

    /// <summary>有待轧批次（PositionDiff 1~6）</summary>
    public bool InWaitExists { get; set; }

    /// <summary>可流转在轧量（该规格全部在轧批次重量，待流转总量，不过滤档位）</summary>
    public decimal FlowInProdWeight { get; set; }

    /// <summary>可流转待轧量（该规格全部待轧批次重量，待流转总量，不过滤档位）</summary>
    public decimal FlowInWaitWeight { get; set; }

    /// <summary>计划在轧量（在轧批次中命中「建议在轧要求」档位的重量，本次计划流转分侧）</summary>
    public decimal PlannedInProdWeight { get; set; }

    /// <summary>计划待轧量（待轧批次中命中「建议待轧要求」档位的重量，本次计划流转分侧）</summary>
    public decimal PlannedInWaitWeight { get; set; }

    /// <summary>实际在轧流转档（一键采用写入排程设置「在轧要求」的最终值；计划在轧量=0 时留空）</summary>
    public string ActualCompletionTier { get; set; } = "";

    /// <summary>实际待轧流转档（一键采用写入排程设置「待轧要求」的最终值；计划待轧量=0 时留空）</summary>
    public string ActualRollTier { get; set; } = "";

    /// <summary>现有排程单机单日量（采用原样保留）</summary>
    public decimal? DailyOutput { get; set; }

    /// <summary>现有排程机台号（采用原样保留）</summary>
    public string? MachineNo { get; set; }

    /// <summary>现有排程备注（采用原样保留）</summary>
    public string? Remark { get; set; }

    /// <summary>行状态：OK / 锁定 / 新增</summary>
    public string RowStatus { get; set; } = "OK";
}

/// <summary>
/// 冷轧流转状态（5060 在制 → 2030 投入折算）
/// </summary>
public class FlowStateDto
{
    /// <summary>Supplier(5060) / Demander(2030) / ""</summary>
    public string Role { get; set; } = "";

    /// <summary>5060在制折算 2030 机台需求（方式B→方式A）</summary>
    public int SupplyMachines { get; set; }

    /// <summary>5060 流入机台（仅部分二，对倒判据）</summary>
    public int From5060Machines { get; set; }

    /// <summary>2030 下次承接总料重（kg：5060 流入 + 2030 本组本次未定流转，同机台数口径）</summary>
    public decimal TotalWeight { get; set; }

    /// <summary>5060 流入料重（kg，仅部分二）</summary>
    public decimal From5060Weight { get; set; }

    /// <summary>2030 组最小机台数</summary>
    public int NeedMachines { get; set; }

    /// <summary>流转是否平衡（SupplyMachines >= NeedMachines）</summary>
    public bool Balanced { get; set; }

    /// <summary>中文说明（前端直接显示）</summary>
    public string Text { get; set; } = "";
}
