namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 生产工段待产量现况 DTO — 按(工序组, 工段)维度汇总各批次的有效原料重量
/// </summary>
public class SectionProductionStatusDto
{
    /// <summary>工序组名称</summary>
    public string ProcessGroupName { get; set; } = null!;

    /// <summary>工段名称</summary>
    public string SectionName { get; set; } = null!;

    /// <summary>生产中：批次的当前工序/工段匹配此维度且工段未完工的现有效原料重量汇总</summary>
    public decimal? InProduction { get; set; }

    /// <summary>待产量：批次的下一工序/工段匹配此维度且工段已完工的现有效原料重量汇总</summary>
    public decimal? PendingProduction { get; set; }

    /// <summary>工段生产与待产汇总量 = 生产中 + 待产量</summary>
    public decimal? Total { get; set; }

    /// <summary>
    /// 属成品工序量：逻辑同汇总量，但仅统计涉及该批次最后一道工序（最大 SequenceNumber 的工序组）的数据
    /// </summary>
    public decimal? FinalProcessTotal { get; set; }
}
