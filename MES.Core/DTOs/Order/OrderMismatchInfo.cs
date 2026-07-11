namespace MES.Core.DTOs.Order;

/// <summary>
/// 采购单/委外单与工单用料计划关联异常信息
/// </summary>
public class OrderMismatchInfo
{
    /// <summary>
    /// 采购单号或委外单号
    /// </summary>
    public string OrderNo { get; set; } = null!;

    /// <summary>
    /// 已不关联采购用料计划的来源工单号列表
    /// </summary>
    public List<string> MismatchedWorkOrderNos { get; set; } = new();
}
