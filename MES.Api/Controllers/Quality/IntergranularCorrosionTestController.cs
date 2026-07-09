using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Quality;

[ApiController]
[Route("api/intergranular-corrosion-test")]
[Authorize]
public class IntergranularCorrosionTestController : ControllerBase
{
    private readonly IIntergranularCorrosionTestService _service;

    public IntergranularCorrosionTestController(IIntergranularCorrosionTestService service) => _service = service;

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<IntergranularCorrosionTestDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null) return NotFound(ApiResponse<IntergranularCorrosionTestDto>.Fail("记录不存在"));
        return Ok(ApiResponse<IntergranularCorrosionTestDto>.Ok(result));
    }

    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<IntergranularCorrosionTestDto>>>> GetAll(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null, [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = false,
        [FromQuery] DateTime? inspectionDateFrom = null, [FromQuery] DateTime? inspectionDateTo = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword,
            SortBy = sortBy ?? "inspectiondate", IsDescending = isDescending,
            InspectionDateFrom = inspectionDateFrom, InspectionDateTo = inspectionDateTo
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<IntergranularCorrosionTestDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<IntergranularCorrosionTestDto>>> Create([FromBody] CreateIntergranularCorrosionTestRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<IntergranularCorrosionTestDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<IntergranularCorrosionTestDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<IntergranularCorrosionTestDto>>> Update(int id, [FromBody] UpdateIntergranularCorrosionTestRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<IntergranularCorrosionTestDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<IntergranularCorrosionTestDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpPost("batch")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<IntergranularCorrosionTestDto>>>> BatchCreate([FromBody] List<CreateIntergranularCorrosionTestRequest> requests)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<List<IntergranularCorrosionTestDto>>.Fail("请求参数无效"));
        if (requests == null || requests.Count == 0) return BadRequest(ApiResponse<List<IntergranularCorrosionTestDto>>.Fail("请求数据不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<IntergranularCorrosionTestDto>>.Ok(result, "批量创建成功"));
    }

    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    [HttpPost("print-batch-file")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<IActionResult> PrintBatchFile([FromBody] IntergranularCorrosionTestPrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));
        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "晶间腐蚀检验-选中.pdf");
    }

    [HttpPost("print-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<IActionResult> PrintAllFile([FromBody] IntergranularCorrosionTestPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns, request.InspectionDateFrom, request.InspectionDateTo);
        return File(pdfBytes, "application/pdf", "晶间腐蚀检验-全部.pdf");
    }
}
