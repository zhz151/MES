using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Materials;

[ApiController]
[Route("api/supplier")]
[Authorize]
public class SupplierController : ControllerBase
{
    private readonly ISupplierService _service;
    private readonly ILogger<SupplierController> _logger;

    public SupplierController(ISupplierService service, ILogger<SupplierController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<SupplierProfileDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { /* ignore invalid JSON */ }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<SupplierProfileDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<SupplierProfileDto>>>> GetAllList()
    {
        var result = await _service.GetAllListAsync();
        return Ok(ApiResponse<List<SupplierProfileDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SupplierProfileDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<SupplierProfileDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SupplierProfileDto>>> Create([FromBody] CreateSupplierRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SupplierProfileDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<SupplierProfileDto>.Ok(result, "创建成功"));
    }

    [HttpPost("batch")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<SupplierProfileDto>>>> CreateBatch([FromBody] List<CreateSupplierRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<SupplierProfileDto>>.Fail("请求参数无效"));
        if (requests == null || requests.Count == 0)
            return BadRequest(ApiResponse<List<SupplierProfileDto>>.Fail("请求列表不能为空"));
        var result = await _service.CreateBatchAsync(requests);
        return Ok(ApiResponse<List<SupplierProfileDto>>.Ok(result, "批量创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SupplierProfileDto>>> Update(int id, [FromBody] UpdateSupplierRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SupplierProfileDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<SupplierProfileDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    // ========== 筛选上下文 ==========

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    // ========== 打印 ==========

    [HttpGet("{id}/print")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintSupplier(int id)
    {
        var pdfBytes = await _service.PrintSupplierAsync(id);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("{id}/print-file")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<IActionResult> PrintSupplierFile(int id)
    {
        var pdfBytes = await _service.PrintSupplierAsync(id);
        return File(pdfBytes, "application/pdf", $"供应商_{id}.pdf");
    }

    [HttpPost("print-batch")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintSupplierBatch([FromBody] OrderPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintSupplierBatchAsync(request.Ids);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-batch-file")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<IActionResult> PrintSupplierBatchFile([FromBody] OrderPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintSupplierBatchAsync(request.Ids);
        return File(pdfBytes, "application/pdf", $"供应商批量.pdf");
    }

    [HttpPost("print-all")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintSupplierAll([FromBody] OrderPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintSupplierAllAsync(request.Keyword, request.SortBy, request.IsDescending);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<IActionResult> PrintSupplierAllFile([FromBody] OrderPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintSupplierAllAsync(request.Keyword, request.SortBy, request.IsDescending);
        return File(pdfBytes, "application/pdf", $"供应商全部.pdf");
    }

    [HttpGet("active")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<SupplierProfileDto>>>> GetActive()
    {
        var result = await _service.GetActiveAsync();
        return Ok(ApiResponse<List<SupplierProfileDto>>.Ok(result, "查询成功"));
    }
}
