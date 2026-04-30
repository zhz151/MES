namespace MES.Data.Entities;

/// <summary>
/// 批次删除日志（纯日志实体，不继承 BaseEntity）
/// </summary>
/// <remarks>
/// 不使用 BaseEntity，原因：
/// 1. Id 为 bigint (long)，日志量大会超过 int 范围
/// 2. 纯日志实体，不使用审计字段（已有 DeletedTime/Operator 记录操作信息）
/// 3. 该实体使用物理插入，不参与软删除过滤
/// 4. 不继承 BaseEntity 避免被自动审计和软删除过滤影响
/// </remarks>
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
