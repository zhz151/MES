namespace MES.Core.Models;

public class SubcontractQueryParams : QueryParams
{
    /// <summary>
    /// 状态筛选（Sent/PartialReturned/Completed/Cancelled）
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 下单日期起
    /// </summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// 下单日期止
    /// </summary>
    public DateTime? DateTo { get; set; }
}
