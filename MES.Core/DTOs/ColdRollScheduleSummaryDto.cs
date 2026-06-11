namespace MES.Core.DTOs;

/// <summary>
/// 冷轧排程汇总 DTO — 按(外径跨度, 冷轧类型)聚合的流转执行重量报表
/// </summary>
public class ColdRollScheduleSummaryDto
{
    /// <summary>外径跨度（BilletSpec外径 - RollingSpec外径）</summary>
    public string ShortDisplay { get; set; } = "";

    /// <summary>冷轧类型</summary>
    public string ProcessType { get; set; } = "";

    // ===== 总量 =====
    public int TotalBatchCount { get; set; }
    public decimal TotalWeight { get; set; }

    // ===== 流转汇总 =====
    /// <summary>流转批次数</summary>
    public int FlowBatchCount { get; set; }
    /// <summary>流转执行重量</summary>
    public decimal FlowWeight { get; set; }
    /// <summary>其中：完工冷轧重量</summary>
    public decimal CompletionWeight { get; set; }
    /// <summary>其中：冷轧重量</summary>
    public decimal RollWeight { get; set; }
}
