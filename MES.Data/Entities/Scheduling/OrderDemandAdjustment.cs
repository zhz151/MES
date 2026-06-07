namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 工单需求调整（手工填写，一个工单一条记录）
/// </summary>
public class OrderDemandAdjustment : BaseEntity
{
    /// <summary>工单ID（唯一）</summary>
    public int WorkOrderId { get; set; }

    /// <summary>催单</summary>
    public bool IsUrging { get; set; }

    /// <summary>分批交货（工单需求调整 + 分批交货的工单，原料未齐也可纳入排程）</summary>
    public bool IsBatchDelivery { get; set; }

    /// <summary>工单暂停（标记为 E停，不纳入排程计算）</summary>
    public bool IsPaused { get; set; }

    /// <summary>调整备注</summary>
    public string? AdjustmentRemark { get; set; }
}
