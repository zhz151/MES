namespace MES.Core.DTOs.Batch;

/// <summary>
/// 相邻批次导航DTO
/// </summary>
public class AdjacentBatchDto
{
    /// <summary>上一个批次ID（null表示无上一个）</summary>
    public int? PrevId { get; set; }
    /// <summary>上一个批次号</summary>
    public string? PrevBatchNo { get; set; }
    /// <summary>下一个批次ID（null表示无下一个）</summary>
    public int? NextId { get; set; }
    /// <summary>下一个批次号</summary>
    public string? NextBatchNo { get; set; }
}
