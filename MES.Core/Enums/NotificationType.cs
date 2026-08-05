namespace MES.Core.Enums;

/// <summary>
/// 通知类型枚举
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// 新物料待审核
    /// </summary>
    NewMaterial,

    /// <summary>
    /// 删除被阻止
    /// </summary>
    DeleteBlocked,

    /// <summary>
    /// 出库预警
    /// </summary>
    OutboundAlert,

    /// <summary>
    /// 工单已删除（已废弃，仅兼容存量数据，不再主动写入）
    /// </summary>
    WorkOrderDeleted,

    /// <summary>
    /// 订单已删除
    /// </summary>
    OrderDeleted,

    /// <summary>
    /// 订单已变更
    /// </summary>
    OrderChanged,

    /// <summary>
    /// 工单内容已变更（工单号不变，ManufacturingItem/PlantGrade/数量等变）
    /// </summary>
    WorkOrderChanged,

    /// <summary>
    /// 批次变更导致关联用料计划自动完成（在产改制/在产主工单）
    /// </summary>
    BatchPlanAutoCompleted,

    /// <summary>
    /// 入库制造状态不一致（同生产批号+同制造物品匹配，但制造状态与生产批次不一致）
    /// </summary>
    InboundMismatchAlert
}
