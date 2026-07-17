// 文件路径: MES.Api/Controllers/StandardRegister/GradeMappingController.cs
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.Interfaces.StandardRegister;

namespace MES.Api.Controllers.StandardRegister;

/// <summary>
/// 牌号对照控制器
/// </summary>
[ApiController]
[Route("api/grade-mapping")]
[Authorize]
public class GradeMappingController : ControllerBase
{
    private readonly IGradeMappingService _service;

    public GradeMappingController(IGradeMappingService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页查询牌号对照列表（支持关键字搜索）- 用于 ServerData 模式
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<StandardGradeMappingDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        QueryParams query = new() { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
        {
            try
            {
                var f = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (f != null && f.Count > 0) query.Filters = f;
            }
            catch { }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<StandardGradeMappingDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有牌号对照（用于下拉框）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<StandardGradeMappingDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<StandardGradeMappingDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据ID获取牌号对照详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<StandardGradeMappingDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<StandardGradeMappingDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建牌号对照
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<StandardGradeMappingDto>>> Create([FromBody] CreateGradeMappingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<StandardGradeMappingDto>.Fail("请求参数无效"));
        }

        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<StandardGradeMappingDto>.Ok(result, "创建成功"));
    }

    // ========== 打印 ==========

    /// <summary>
    /// 打印单个牌号对照
    /// </summary>
    [HttpPost("print-single")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintGradeMappingSingle([FromBody] OrderPrintSingleRequest request)
    {
        var pdfBytes = await _service.PrintGradeMappingAsync(request.Id, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-single-file")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<IActionResult> PrintGradeMappingSingleFile([FromBody] OrderPrintSingleRequest request)
    {
        var pdfBytes = await _service.PrintGradeMappingAsync(request.Id, request.Columns);
        return File(pdfBytes, "application/pdf", "牌号对照打印.pdf");
    }

    /// <summary>
    /// 批量打印牌号对照
    /// </summary>
    [HttpPost("print-batch")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintGradeMappingBatch([FromBody] OrderPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintGradeMappingBatchAsync(request.Ids, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 按筛选条件打印全部牌号对照
    /// </summary>
    [HttpPost("print-all")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintGradeMappingAll([FromBody] OrderPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintGradeMappingAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 批量打印牌号对照（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<IActionResult> PrintBatchFile([FromBody] OrderPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintGradeMappingBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "牌号对照打印.pdf");
    }

    /// <summary>
    /// 按筛选条件打印全部牌号对照（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<IActionResult> PrintAllFile([FromBody] OrderPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintGradeMappingAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns);
        return File(pdfBytes, "application/pdf", "牌号对照列表.pdf");
    }

    /// <summary>
    /// 更新牌号对照
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<StandardGradeMappingDto>>> Update(int id, [FromBody] UpdateGradeMappingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<StandardGradeMappingDto>.Fail("请求参数无效"));
        }

        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<StandardGradeMappingDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除牌号对照
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(true, "删除成功"));
    }

    // ========== 筛选上下文 ==========

    /// <summary>
    /// 获取牌号对照筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }
}