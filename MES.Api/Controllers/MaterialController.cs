using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

[ApiController]
[Route("api/material")]
[Authorize]
public class MaterialController : ControllerBase
{
    private readonly IMaterialService _service;
    private readonly ILogger<MaterialController> _logger;

    public MaterialController(IMaterialService service, ILogger<MaterialController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<MaterialDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true)
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
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<MaterialDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<MaterialDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<MaterialDto>.Ok(result, "查询成功"));
    }

    [HttpGet("match")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<MaterialDto?>>> Match(
        [FromQuery] string category,
        [FromQuery] string grade,
        [FromQuery] string spec)
    {
        var result = await _service.MatchAsync(category, grade, spec);
        return Ok(ApiResponse<MaterialDto?>.Ok(result, result != null ? "匹配成功" : "未找到匹配物料"));
    }

    [HttpPost("batch-match")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<BatchMaterialMatchItem>>>> BatchMatch([FromBody] List<BatchMaterialMatchItem> items)
    {
        if (items == null || items.Count == 0)
            return Ok(ApiResponse<List<BatchMaterialMatchItem>>.Ok(new List<BatchMaterialMatchItem>(), "无匹配项"));
        var result = await _service.BatchMatchAsync(items);
        return Ok(ApiResponse<List<BatchMaterialMatchItem>>.Ok(result, "批量匹配完成"));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<MaterialDto>>> Create([FromBody] CreateMaterialRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<MaterialDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<MaterialDto>.Ok(result, "创建成功"));
    }

    [HttpPost("batch")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<MaterialDto>>>> CreateBatch([FromBody] List<CreateMaterialRequest> requests)
    {
        if (requests == null || requests.Count == 0)
            return BadRequest(ApiResponse<List<MaterialDto>>.Fail("请求列表不能为空"));
        var result = await _service.CreateBatchAsync(requests);
        return Ok(ApiResponse<List<MaterialDto>>.Ok(result, "批量创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<MaterialDto>>> Update(int id, [FromBody] UpdateMaterialRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<MaterialDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<MaterialDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    // ========== 打印 ==========

    [HttpGet("{id}/print")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintMaterial(int id)
    {
        var pdfBytes = await _service.PrintMaterialAsync(id);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-batch")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintMaterialBatch([FromBody] OrderPrintBatchRequest request)
    {
        var pdfBytes = await _service.PrintMaterialBatchAsync(request.Ids);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-all")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintMaterialAll([FromBody] OrderPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintMaterialAllAsync(request.Keyword, request.SortBy, request.IsDescending);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpGet("categories")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetCategories()
    {
        var result = await _service.GetCategoriesAsync();
        return Ok(ApiResponse<List<string>>.Ok(result, "查询成功"));
    }

    [HttpGet("active")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<MaterialDto>>>> GetActive()
    {
        var result = await _service.GetActiveAsync();
        return Ok(ApiResponse<List<MaterialDto>>.Ok(result, "查询成功"));
    }
}
