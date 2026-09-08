using MES.Core.DTOs.Shared;

namespace MES.Core.DTOs.Order;

/// <summary>
/// 批量打印请求
/// </summary>
public class OrderPrintBatchRequest
{
    /// <summary>
    /// 订单ID列表
    /// </summary>
    public int[] Ids { get; set; } = Array.Empty<int>();

    /// <summary>
    /// 打印列定义列表（为空则打印全部列）
    /// </summary>
    public List<PrintColumnDef>? Columns { get; set; }

    /// <summary>
    /// 是否含金额模式（true=显示计价单位/单价/总价并合计订单总价；false=不含金额，仅保留结算方式）
    /// 默认 true 兼容旧调用方
    /// </summary>
    public bool IncludeAmounts { get; set; } = true;
}
