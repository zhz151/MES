namespace MES.Core.Models;

public class SubcontractQueryParams : QueryParams
{
    /// <summary>
    /// 状态筛选（Sent/PartialReturned/Completed/Cancelled）
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 发出日期起
    /// </summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// 发出日期止
    /// </summary>
    public DateTime? DateTo { get; set; }
}
