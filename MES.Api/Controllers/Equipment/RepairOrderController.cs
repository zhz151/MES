using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Equipment;
using MES.Core.Interfaces.Equipment;

namespace MES.Api.Controllers.Equipment;

[ApiController]
[Route("api/repair-order")]
[Authorize]
public class RepairOrderController : ControllerBase
{
    private readonly IRepairOrderService _service;

    public RepairOrderController(IRepairOrderService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<RepairOrderListDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new RepairOrderQueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "ReportTime" : sortBy,
            IsDescending = isDescending
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<RepairOrderListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<RepairOrderListDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<RepairOrderListDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<RepairOrderListDto>>> Create([FromBody] CreateRepairOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<RepairOrderListDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<RepairOrderListDto>.Ok(result, "报修成功"));
    }

    [HttpPost("batch")]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<List<RepairOrderListDto>>>> CreateBatch([FromBody] List<CreateRepairOrderRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<RepairOrderListDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<RepairOrderListDto>>.Fail("请求列表不能为空"));
        var result = await _service.CreateBatchAsync(requests);
        return Ok(ApiResponse<List<RepairOrderListDto>>.Ok(result, "批量创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<RepairOrderListDto>>> Update(int id, [FromBody] UpdateRepairOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<RepairOrderListDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<RepairOrderListDto>.Ok(result, "更新成功"));
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
    /// 批量打印维修工单（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<IActionResult> PrintBatchFile([FromBody] RepairOrderPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "维修工单打印.pdf");
    }

    /// <summary>
    /// 获取指定设备的待处理维修工单
    /// </summary>
    [HttpGet("by-equipment/{equipmentId}")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<List<RepairOrderListDto>>>> GetPendingByEquipment(int equipmentId)
    {
        var result = await _service.GetPendingByEquipmentAsync(equipmentId);
        return Ok(ApiResponse<List<RepairOrderListDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 开始维修
    /// </summary>
    [HttpPut("{id}/start")]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<RepairOrderListDto>>> StartRepair(int id, [FromBody] StartRepairRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<RepairOrderListDto>.Fail("请求参数无效"));
        var result = await _service.StartRepairAsync(id, request);
        return Ok(ApiResponse<RepairOrderListDto>.Ok(result, "开始维修成功"));
    }

    /// <summary>
    /// 完成维修
    /// </summary>
    [HttpPut("{id}/complete")]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<RepairOrderListDto>>> CompleteRepair(int id, [FromBody] CompleteRepairRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<RepairOrderListDto>.Fail("请求参数无效"));
        var result = await _service.CompleteRepairAsync(id, request);
        return Ok(ApiResponse<RepairOrderListDto>.Ok(result, "维修完成"));
    }
}
