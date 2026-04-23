namespace MES.Core.Enums;

/// <summary>
/// 工单状态枚举（4态）
/// </summary>
public enum WorkOrderStatus
{
    /// <summary>
    /// 未编制（订单从未生成过工单）
    /// </summary>
    NotGenerated = 0,
    
    /// <summary>
    /// 已确定（工单与订单完全一致）
    /// </summary>
    Confirmed = 1,
    
    /// <summary>
    /// 待修正（订单已有工单，但订单发生变更，需重新生成）
    /// </summary>
    Pending = 2,
    
    /// <summary>
    /// 已取消（工单已软删除）
    /// </summary>
    Cancelled = 3
}