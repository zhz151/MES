namespace MES.Core.DTOs.Batch;

/// <summary>
/// 过程检验缺陷率超阈值批次信息
/// </summary>
public class DefectRateBatchDto
{
    /// <summary>批次ID</summary>
    public int BatchId { get; set; }

    /// <summary>批次号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>工单号</summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>缺陷率（百分比值，如 3.5 表示 3.5%）</summary>
    public decimal DefectRate { get; set; }
}
