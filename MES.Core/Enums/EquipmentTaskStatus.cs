namespace MES.Core.Enums;

/// <summary>
/// 设备点检/保养状况（动态计算，不存字段）
/// </summary>
public enum EquipmentTaskStatus
{
    /// <summary>
    /// 不适用（设备不需点检/保养）
    /// </summary>
    NotApplicable = 0,

    /// <summary>
    /// 待执行
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 正常（已执行）
    /// </summary>
    Normal = 2,

    /// <summary>
    /// 逾期
    /// </summary>
    Overdue = 3
}
