namespace MES.Data.Entities.Batch;

/// <summary>
/// 批次操作日志 — 记录批次的关键操作（创建/修改/暂停/恢复/强制完成/删除等）
/// </summary>
public class BatchOperationLog : BaseEntity
{
    /// <summary>
    /// 关联生产批次ID
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 操作类型（创建/修改/暂停/恢复/强制完成/删除/更新工单）
    /// </summary>
    public string OperationType { get; set; } = null!;

    /// <summary>
    /// 操作详情（JSON格式或文本描述）
    /// </summary>
    public string? Detail { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属生产批次
    /// </summary>
    public ProductionBatch ProductionBatch { get; set; } = null!;
}
