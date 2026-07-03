namespace MES.Core.DTOs;

/// <summary>
/// 冷轧排程汇总 DTO — 按(冷轧类型, 外径跨度)聚合的流转重量报表
/// 数据源为 G7 实时值（IsFlow/FlowTarget/FlowCRType/IsKeyBatch），不再依赖 G13 持久化
/// </summary>
public class ColdRollScheduleSummaryDto
{
    /// <summary>外径跨度（BilletSpec外径 - RollingSpec外径）</summary>
    public string ShortDisplay { get; set; } = "";

    /// <summary>冷轧类型</summary>
    public string ProcessType { get; set; } = "";

    // ===== 流转汇总 =====

    /// <summary>流转批次数</summary>
    public int FlowBatchCount { get; set; }

    /// <summary>总流转重量(kg)</summary>
    public decimal TotalFlowWeight { get; set; }

    /// <summary>在轧重点批Kg（FlowTarget=完工冷轧 + IsKeyBatch 且 1A）</summary>
    public decimal ProdKeyWeight { get; set; }

    /// <summary>在轧1B批Kg（FlowTarget=完工冷轧 + IsKeyBatch 且 1B）</summary>
    public decimal ProdLevel1BWeight { get; set; }

    /// <summary>在轧非重点批Kg（FlowTarget=完工冷轧 + !IsKeyBatch）</summary>
    public decimal ProdNonKeyWeight { get; set; }

    /// <summary>在轧流转汇总(kg)</summary>
    public decimal ProdTotalWeight { get; set; }

    /// <summary>待轧重点批Kg（FlowTarget=冷轧 + IsKeyBatch 且 1A）</summary>
    public decimal WaitKeyWeight { get; set; }

    /// <summary>待轧1B批Kg（FlowTarget=冷轧 + IsKeyBatch 且 1B）</summary>
    public decimal WaitLevel1BWeight { get; set; }

    /// <summary>待轧非重点批Kg（FlowTarget=冷轧 + !IsKeyBatch）</summary>
    public decimal WaitNonKeyWeight { get; set; }

    /// <summary>待轧流转汇总(kg)</summary>
    public decimal WaitTotalWeight { get; set; }
}
