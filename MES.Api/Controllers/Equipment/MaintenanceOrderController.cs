using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Equipment;
using MES.Core.Interfaces.Equipment;

namespace MES.Api.Controllers.Equipment;

[ApiController]
[Route("api/maintenance-order")]
[Authorize]
public class MaintenanceOrderController : ControllerBase
{
    private readonly IMaintenanceOrderService _service;

    public MaintenanceOrderController(IMaintenanceOrderService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<MaintenanceOrderListDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new MaintenanceOrderQueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "Id" : sortBy,
            IsDescending = isDescending
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<MaintenanceOrderListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<MaintenanceOrderListDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<MaintenanceOrderListDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<MaintenanceOrderListDto>>> Create([FromBody] CreateMaintenanceOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<MaintenanceOrderListDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<MaintenanceOrderListDto>.Ok(result, "创建成功"));
    }

    [HttpPost("batch")]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<List<MaintenanceOrderListDto>>>> CreateBatch([FromBody] List<CreateMaintenanceOrderRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<MaintenanceOrderListDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<MaintenanceOrderListDto>>.Fail("请求列表不能为空"));
        var result = await _service.CreateBatchAsync(requests);
        return Ok(ApiResponse<List<MaintenanceOrderListDto>>.Ok(result, "批量创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<MaintenanceOrderListDto>>> Update(int id, [FromBody] UpdateMaintenanceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<MaintenanceOrderListDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<MaintenanceOrderListDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.EquipmentDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 批量打印保养工单（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<IActionResult> PrintBatchFile([FromBody] MaintenanceOrderPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "保养工单打印.pdf");
    }

}
