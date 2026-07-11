namespace MES.Core.DTOs.Batch;

/// <summary>
/// 批量保存生产批次响应
/// </summary>
public class SaveBatchResponse
{
    /// <summary>更新后的 RowVersion（前端需刷新详情页）</summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>更新后的批次状态</summary>
    public string Status { get; set; } = null!;
}
