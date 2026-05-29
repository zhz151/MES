namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 销售催单（手工填写，一个工单一条记录）
/// </summary>
public class SalesUrging : BaseEntity
{
    /// <summary>工单ID（唯一）</summary>
    public int WorkOrderId { get; set; }

    /// <summary>销售催单</summary>
    public bool IsSalesUrging { get; set; }

    /// <summary>催单备注</summary>
    public string? UrgingRemark { get; set; }
}
