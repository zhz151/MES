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
}
