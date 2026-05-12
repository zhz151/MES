namespace MES.Core.DTOs;

/// <summary>
/// 批次工单号不匹配信息
/// </summary>
public class BatchWorkOrderMismatchDto
{
    /// <summary>批次ID</summary>
    public int BatchId { get; set; }

    /// <summary>批次号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>不存在的工单号</summary>
    public string WorkOrderNo { get; set; } = null!;
}
