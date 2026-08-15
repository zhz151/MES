namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 冷轧排程汇总 DTO — 按(冷轧类型, 外径跨度)聚合，分档与冷轧计划主列表统一
/// 数据源复用主列表中间数据（IsUrgent/IsNormal/AttentionMatchesCurrentCR），maxDiff 按 PositionDiff 过滤
/// 三档分类器：特急 = IsUrgent ∧ 正常流转 ∧ 关注==当前冷轧；特急- = IsUrgent ∧ 正常流转 ∧ 关注≠当前冷轧；急 = IsUrgent ∧ 非正常流转
/// </summary>
public class ColdRollPlanSummaryDto
{
    /// <summary>冷轧类型（英文 Key，如 ColdRoll60，前端转中文）</summary>
    public string ProcessType { get; set; } = "";

    /// <summary>外径跨度（BilletSpec外径 - RollingSpec外径）</summary>
    public string ShortDisplay { get; set; } = "";

    /// <summary>流转批次数</summary>
    public int BatchCount { get; set; }

    /// <summary>总流转重量(kg) = 在轧总量 + 待轧总量（不含远日量）</summary>
    public decimal TotalFlowWeight { get; set; }

    // ===== 在轧分档（PositionDiff==0）=====

    /// <summary>在轧总量</summary>
    public decimal ProdTotalWeight { get; set; }

    /// <summary>在轧(特急) = 正常流转∧关注==当前冷轧</summary>
    public decimal ProdUrgentWeight { get; set; }

    /// <summary>在轧(特急-) = 正常流转∧关注≠当前冷轧</summary>
    public decimal ProdUrgentSubWeight { get; set; }

    /// <summary>在轧(急) = 非正常流转</summary>
    public decimal ProdOtherWeight { get; set; }

    /// <summary>在轧(一般) = 在轧总量 − 特急 − 特急- − 急，对应等级「一般」档（其余流转）</summary>
    public decimal ProdRestWeight { get; set; }

    // ===== 待轧分档（PositionDiff 1~6）=====

    /// <summary>待轧总量</summary>
    public decimal WaitTotalWeight { get; set; }

    /// <summary>待轧(特急) = 正常流转∧关注==当前冷轧</summary>
    public decimal WaitUrgentWeight { get; set; }

    /// <summary>待轧(特急-) = 正常流转∧关注≠当前冷轧</summary>
    public decimal WaitUrgentSubWeight { get; set; }

    /// <summary>待轧(急) = 非正常流转</summary>
    public decimal WaitOtherWeight { get; set; }

    /// <summary>待轧(一般) = 待轧总量 − 特急 − 特急- − 急，对应等级「一般」档（其余流转）</summary>
    public decimal WaitRestWeight { get; set; }
}
