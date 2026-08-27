namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 来源单号关联工单号已变更的入库批次信息（实时扫描）
/// </summary>
public class SourceOrderChangedBatchDto
{
    /// <summary>批次ID</summary>
    public int BatchId { get; set; }

    /// <summary>仓库批次号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>来源单号（采购单号/委外单号）</summary>
    public string SourceOrderNo { get; set; } = null!;

    /// <summary>委外序号（委外来源时为明细序号，采购来源为空）</summary>
    public int? SourceOrderSequence { get; set; }

    /// <summary>来源单当前关联的工单号（期望值：已变更时为来源单当前工单号；已取消时为批次上残留的旧工单号，供提示展示）</summary>
    public string ExpectedWorkOrderNo { get; set; } = null!;

    /// <summary>是否为「工单已取消」（来源单已清空工单号、或来源单指向的工单已被删除）；false=「工单已变更」</summary>
    public bool IsCancelled { get; set; }
}
