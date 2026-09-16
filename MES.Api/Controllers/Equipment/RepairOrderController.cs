using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Equipment;
using MES.Core.Interfaces.Equipment;

namespace MES.Api.Controllers.Equipment;

/// <summary>
/// 维修工单控制器。
///
/// <b>2026-09-16 起整域放开为「仅需登录」，不带设备角色档</b>：本域是「设备扫码」链路的落地形态
/// （扫码 → 报修 / 扫码 → 维修 / 扫码 → 工单列表），现场一线岗位账号只配 Scan 档，
/// 若挂 EquipmentView/Edit/Delete 会出现「账号能进扫码页却干不了活」。
/// 操作人身份由页面内实名选择约束，不由角色约束（同 8 类扫码报工链口径）。
///
/// ⚠️ 设备域其余部分仍受角色档控制：设备台账/保养工单/点检记录（EquipmentController 等）。
/// 建单页所需的设备下拉走 <c>GET api/equipment/options</c>（仅登录、精简字段），
/// <c>GET api/equipment/all</c> 保持 EquipmentView 不得顺手放宽。
/// </summary>
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
    [Authorize]
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
    [Authorize]
    public async Task<ActionResult<ApiResponse<RepairOrderListDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<RepairOrderListDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponse<RepairOrderListDto>>> Create([FromBody] CreateRepairOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<RepairOrderListDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<RepairOrderListDto>.Ok(result, "报修成功"));
    }

    [HttpPost("batch")]
    [Authorize]
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
    [Authorize]
    public async Task<ActionResult<ApiResponse<RepairOrderListDto>>> Update(int id, [FromBody] UpdateRepairOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<RepairOrderListDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<RepairOrderListDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("filter-contexts")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 批量打印维修工单（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-batch-file")]
    [Authorize]
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
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<RepairOrderListDto>>>> GetPendingByEquipment(int equipmentId)
    {
        var result = await _service.GetPendingByEquipmentAsync(equipmentId);
        return Ok(ApiResponse<List<RepairOrderListDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 开始维修
    /// </summary>
    [HttpPut("{id}/start")]
    [Authorize]
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
    [Authorize]
    public async Task<ActionResult<ApiResponse<RepairOrderListDto>>> CompleteRepair(int id, [FromBody] CompleteRepairRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<RepairOrderListDto>.Fail("请求参数无效"));
        var result = await _service.CompleteRepairAsync(id, request);
        return Ok(ApiResponse<RepairOrderListDto>.Ok(result, "维修完成"));
    }
}
