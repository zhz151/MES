namespace MES.Core.Enums;

/// <summary>
/// 库存使用计划状态
/// </summary>
public enum InventoryPlanStatus
{
    /// <summary>
    /// 已计划 - 已创建计划，等待库房确认
    /// </summary>
    Planned = 0,

    /// <summary>
    /// 已确认 - 库房已确认出库
    /// </summary>
    Confirmed = 1,

    /// <summary>
    /// 已取消 - 计划已取消
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// 已完成 - 批次有效量已调整，通知消除
    /// </summary>
    Completed = 3
}
