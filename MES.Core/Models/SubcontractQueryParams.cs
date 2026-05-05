namespace MES.Core.Models;

public class SubcontractQueryParams : QueryParams
{
    /// <summary>
    /// 状态筛选（Sent/PartialReturned/Completed/Cancelled）
    /// </summary>
    public string? Status { get; set; }
}
