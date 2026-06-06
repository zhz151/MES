namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 原锁预执行（G15 独立表，LEFT JOIN 实时查询）
/// 存储用户手工标记的执行状态 + 系统计算的主号齐全
/// </summary>
public class RawMaterialLockPreExecution : BaseEntity
{
    /// <summary>工单ID（唯一，一个工单一条记录）</summary>
    public int WorkOrderId { get; set; }

    /// <summary>执行：用户手动标注"近几日会投料"的工单</summary>
    public bool IsPreInput { get; set; }

    /// <summary>预算投料日：用户手动输入，仅当执行为"是"时可输入且不能为空</summary>
    public DateTime? BudgetInputDate { get; set; }

    /// <summary>主号齐全：系统计算（同主号+同备注全部执行 或 质量影响执行）</summary>
    public bool IsMainNoMaterialComplete { get; set; }
}
