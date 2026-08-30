using MES.Core.DTOs.Order;
using MES.Core.Models;

namespace MES.Core.Interfaces.Order;

/// <summary>
/// 待发货订单成品查询服务 — 成品库实时查询
/// </summary>
public interface IPendingDeliveryQueryService
{
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

    /// <summary>
    /// 失效全部待发货缓存（C0/C1/C2），出入库、订单头变更等写操作后调用
    /// </summary>
    Task InvalidateCachesAsync();

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<MES.Core.DTOs.Shared.PrintColumnDef> columns);

}
