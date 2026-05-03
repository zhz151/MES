namespace MES.Core.Models;

public class PurchaseOrderQueryParams : QueryParams
{
    /// <summary>
    /// 状态筛选（Open/Partial/Completed/Cancelled）
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

    /// <summary>
    /// 要求到货日起
    /// </summary>
    public DateTime? RequiredDateFrom { get; set; }

    /// <summary>
    /// 要求到货日止
    /// </summary>
    public DateTime? RequiredDateTo { get; set; }
}
