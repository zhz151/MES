namespace MES.Data.Entities.Infrastructure;

/// <summary>
/// 统一操作日志 — 覆盖 Batch / Order / WorkOrder 模块的关键操作记录
/// </summary>
public class OperationLog : BaseEntity
{
    /// <summary>
    /// 模块名称（Batch / Order / WorkOrder）
    /// </summary>
    public string Module { get; set; } = null!;

    /// <summary>
    /// 关联业务主键 ID
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    /// 操作类型（创建 / 变更 / 删除）
    /// </summary>
    public string OperationType { get; set; } = null!;

    /// <summary>
    /// 操作详情（文本格式，记录变更前后的具体差异）
    /// </summary>
    public string? Detail { get; set; }
}
