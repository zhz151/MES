using MES.Core.DTOs.Warehouse;
using MES.Core.Models;

namespace MES.Core.Interfaces.Warehouse;

/// <summary>
/// 待发货订单成品查询服务 — 成品库实时查询
/// </summary>
public interface IPendingDeliveryQueryService
{
    /// <summary>
    /// 获取待发货订单成品列表（无分页，用于创建页引用）
    /// </summary>
    Task<List<PendingDeliveryItemDto>> GetPendingItemsAsync(
        string? orderNo = null,
        string? productStandard = null,
        string? deliveryStatus = null);

    /// <summary>
    /// 分页查询待发货订单成品（用于列表页）
    /// </summary>
    Task<PagedResult<PendingDeliveryItemDto>> GetPagedAsync(QueryParams query);

    /// <summary>
    /// 获取质保书头选择项 — DISTINCT (订单号+客户名称+产品标准+交货状态)
    /// </summary>
    Task<List<CertificateHeaderOptionDto>> GetHeaderOptionsAsync();

    /// <summary>
    /// 获取筛选上下文（各列的 DISTINCT 值，用于 ExcelFilter）
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
