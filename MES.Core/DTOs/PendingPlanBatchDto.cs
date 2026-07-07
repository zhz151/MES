namespace MES.Core.DTOs;

/// <summary>
/// 待出库用料计划批次信息
/// </summary>
public class PendingPlanBatchDto
{
    /// <summary>
    /// 批次号
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 工单号
    /// </summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>
    /// 计划类型：库存使用/库料改制
    /// </summary>
    public string PlanType { get; set; } = null!;
}
