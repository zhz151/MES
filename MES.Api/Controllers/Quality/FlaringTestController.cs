using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;

namespace MES.Api.Controllers.Quality;

[ApiController]
[Route("api/flaring-test")]
[Authorize]
public class FlaringTestController : ControllerBase
{
    private readonly IFlaringTestService _service;

    public FlaringTestController(IFlaringTestService service) => _service = service;

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<FlaringTestDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null) return NotFound(ApiResponse<FlaringTestDto>.Fail("记录不存在"));
        return Ok(ApiResponse<FlaringTestDto>.Ok(result));
    }

    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<PagedResult<FlaringTestDto>>>> GetAll(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null, [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = false,
        [FromQuery] DateTime? inspectionDateFrom = null, [FromQuery] DateTime? inspectionDateTo = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "inspectiondate",
            IsDescending = isDescending,
            InspectionDateFrom = inspectionDateFrom,
            InspectionDateTo = inspectionDateTo
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<FlaringTestDto>>.Ok(result));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<FlaringTestDto>>> Update(int id, [FromBody] UpdateFlaringTestRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<FlaringTestDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<FlaringTestDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.QualityDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpPost("batch")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<List<FlaringTestDto>>>> BatchCreate([FromBody] List<CreateFlaringTestRequest> requests)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<List<FlaringTestDto>>.Fail("请求参数无效"));
        if (requests == null || requests.Count == 0) return BadRequest(ApiResponse<List<FlaringTestDto>>.Fail("请求数据不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<FlaringTestDto>>.Ok(result, "批量创建成功"));
    }

    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintBatchFile([FromBody] FlaringTestPrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));
        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "扩口检验-选中.pdf");
    }

    [HttpPost("print-all-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintAllFile([FromBody] FlaringTestPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns, request.InspectionDateFrom, request.InspectionDateTo);
        return File(pdfBytes, "application/pdf", "扩口检验-全部.pdf");
    }
}
