using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Order;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Order;

[ApiController]
[Route("api/pending-delivery")]
[Authorize]
public class PendingDeliveryController : ControllerBase
{
    private readonly IPendingDeliveryQueryService _service;

    public PendingDeliveryController(IPendingDeliveryQueryService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页查询待发货订单成品（用于列表页）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.OrderView)]
    public async Task<ActionResult<ApiResponse<PagedResult<PendingDeliveryItemDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = false,
        [FromQuery] string? filters = null,
        [FromQuery] DateTime? inboundDateFrom = null,
        [FromQuery] DateTime? inboundDateTo = null)
    {
        List<FilterDescriptor>? filterList = null;
        if (!string.IsNullOrEmpty(filters))
        {
            try { filterList = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters); }
            catch { }
        }

        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "InventoryBatchNo",
            IsDescending = isDescending,
            Filters = filterList,
            InboundDateFrom = inboundDateFrom,
            InboundDateTo = inboundDateTo
        };

        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<PendingDeliveryItemDto>>.Ok(result));
    }

    /// <summary>
    /// 获取质保书头选择项 — DISTINCT (订单号+客户名称+产品标准+交货状态)
    /// </summary>
    [HttpGet("header-options")]
    [Authorize(Roles = Roles.Policies.PendingDeliveryView)]
    public async Task<ActionResult<ApiResponse<List<CertificateHeaderOptionDto>>>> GetHeaderOptions()
    {
        var result = await _service.GetHeaderOptionsAsync();
        return Ok(ApiResponse<List<CertificateHeaderOptionDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取筛选上下文（各列的 DISTINCT 值，用于 ExcelFilter）
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.OrderView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>
    /// 打印选中行
    /// </summary>
    [HttpPost("print-file")]
    [Authorize(Roles = Roles.Policies.OrderView)]
    public async Task<IActionResult> PrintFile([FromBody] PendingDeliveryPrintRequest request)
    {
        var pdfBytes = await _service.PrintFileAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "订单成品(实时库存).pdf");
    }
}
