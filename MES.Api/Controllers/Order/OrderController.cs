using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Services.Order;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Api.Controllers.Order;

[ApiController]
[Route("api/order")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    #region 订单管理

    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<SalesOrderListDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? technicalStatus = null,
        [FromQuery] string? orderStatus = null,
        [FromQuery] DateTime? signDateFrom = null,
        [FromQuery] DateTime? signDateTo = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        QueryParams query = new() { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _orderService.GetPagedAsync(query, technicalStatus, orderStatus, signDateFrom, signDateTo);
        return Ok(ApiResponse<PagedResult<SalesOrderListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("list-all")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<SalesOrderListDto>>>> GetAllList()
    {
        var result = await _orderService.GetAllListAsync();
        return Ok(ApiResponse<List<SalesOrderListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SalesOrderDetailDto>>> GetById(int id)
    {
        var result = await _orderService.GetByIdAsync(id);
        return Ok(ApiResponse<SalesOrderDetailDto>.Ok(result, "查询成功"));
    }

    [HttpGet("by-number/{orderNo}")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<int?>>> GetIdByOrderNumber(string orderNo)
    {
        var id = await _orderService.GetIdByOrderNumberAsync(orderNo);
        return Ok(ApiResponse<int?>.Ok(id, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SalesOrderListDto>>> Create([FromBody] CreateSalesOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SalesOrderListDto>.Fail("请求参数无效"));

        var result = await _orderService.CreateAsync(request);
        return Ok(ApiResponse<SalesOrderListDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SalesOrderListDto>>> Update(int id, [FromBody] UpdateSalesOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SalesOrderListDto>.Fail("请求参数无效"));

        var result = await _orderService.UpdateAsync(id, request);
        return Ok(ApiResponse<SalesOrderListDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _orderService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    #endregion

    #region 项次管理

    [HttpPost("{id}/items")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<OrderItemDto>>> AddItem(int id, [FromBody] AddOrderItemRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<OrderItemDto>.Fail("请求参数无效"));

        var result = await _orderService.AddItemAsync(id, request);
        return Ok(ApiResponse<OrderItemDto>.Ok(result, "添加成功"));
    }

    [HttpPut("{id}/items/{itemId}")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<OrderItemDto>>> UpdateItem(int id, int itemId, [FromBody] UpdateOrderItemRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<OrderItemDto>.Fail("请求参数无效"));

        var result = await _orderService.UpdateItemAsync(id, itemId, request);
        return Ok(ApiResponse<OrderItemDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}/items/{itemId}")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> DeleteItem(int id, int itemId)
    {
        await _orderService.DeleteItemAsync(id, itemId);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpPost("{id}/save-all")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SaveAllOrderResponse>>> SaveAll(int id, [FromBody] SaveAllOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SaveAllOrderResponse>.Fail("请求参数无效"));

        var result = await _orderService.SaveAllAsync(id, request);
        return Ok(ApiResponse<SaveAllOrderResponse>.Ok(result, "批量保存成功"));
    }

    #endregion

    #region 打印

    [HttpPost("print-file")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<IActionResult> PrintFile([FromBody] OrderPrintBatchRequest request)
    {
        var pdfBytes = await _orderService.PrintOrderBatchAsync(request.Ids);
        return File(pdfBytes, "application/pdf", "订单打印.pdf");
    }

    [HttpGet("{id}/print")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintOrder(int id)
    {
        var pdfBytes = await _orderService.PrintOrderAsync(id);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<IActionResult> PrintAllFile([FromBody] OrderPrintAllRequest request)
    {
        var pdfBytes = await _orderService.PrintOrderAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.DateFrom, request.DateTo);
        return File(pdfBytes, "application/pdf", "订单列表.pdf");
    }

    [HttpPost("print-batch")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintOrderBatch([FromBody] OrderPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _orderService.PrintOrderBatchAsync(request.Ids);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpGet("{orderId}/requirements/print")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintOrderRequirements(int orderId)
    {
        var pdfBytes = await _orderService.PrintOrderRequirementsAsync(orderId);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("{orderId}/requirements/print-file")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<IActionResult> PrintOrderRequirementsFile(int orderId)
    {
        var pdfBytes = await _orderService.PrintOrderRequirementsAsync(orderId);
        return File(pdfBytes, "application/pdf", $"技术要求_{orderId}.pdf");
    }

    #endregion

    #region 读模型刷新

    /// <summary>
    /// 刷新全部订单读模型（从源表重新聚合 OrderListSummary）
    /// </summary>
    [HttpPost("refresh")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> Refresh()
    {
        await _orderService.RefreshAllAsync();
        return Ok(ApiResponse.Ok("读模型刷新成功"));
    }

    #endregion

    #region 筛选上下文

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _orderService.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    #endregion
}