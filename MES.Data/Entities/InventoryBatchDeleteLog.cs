namespace MES.Data.Entities;

/// <summary>
/// 批次删除日志（纯日志实体，不继承BaseEntity）
/// </summary>
public class InventoryBatchDeleteLog
{
    /// <summary>
    /// 主键，自增(bigint)
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 原批次号
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 操作人
    /// </summary>
    public string Operator { get; set; } = null!;

    /// <summary>
    /// 删除时间
    /// </summary>
    public DateTime DeletedTime { get; set; }

    /// <summary>
    /// 被删除批次的完整数据(JSON)
    /// </summary>
    public string BatchData { get; set; } = null!;

    /// <summary>
    /// 删除原因
    /// </summary>
    public string? Reason { get; set; }
}
