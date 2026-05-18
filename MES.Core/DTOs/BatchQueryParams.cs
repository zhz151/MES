using MES.Core.Models;

namespace MES.Core.DTOs;

/// <summary>
/// 批次查询参数
/// </summary>
public class BatchQueryParams : QueryParams
{
    /// <summary>
    /// 工单号筛选
    /// </summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>
    /// 状态筛选
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 挂牌号筛选
    /// </summary>
    public string? TagNo { get; set; }

    /// <summary>
    /// 生产编号（批次号）筛选
    /// </summary>
    public string? BatchNo { get; set; }

    /// <summary>
    /// 订单号筛选
    /// </summary>
    public string? SalesOrderNo { get; set; }

    /// <summary>
    /// 主号筛选
    /// </summary>
    public string? ProductionMainNo { get; set; }

    /// <summary>
    /// 次号筛选
    /// </summary>
    public string? ProductionSubNo { get; set; }

    /// <summary>
    /// 有效投料疑问筛选（正常/疑问）
    /// </summary>
    public string? ValidInputQuestion { get; set; }

    /// <summary>
    /// </summary>
    public DateTime? StartDateFrom { get; set; }

    /// <summary>
    /// 开始日期范围结束
    /// </summary>
    public DateTime? StartDateTo { get; set; }
}
