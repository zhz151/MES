using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Api.Controllers.ProductionStandard;

[Route("api/standard-inspection-requirement")]
[ApiController]
[Authorize]
public class StandardInspectionRequirementController : ControllerBase
{
    private readonly IStandardInspectionRequirementService _service;

    public StandardInspectionRequirementController(IStandardInspectionRequirementService service)
        => _service = service;

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResult<StandardInspectionRequirementDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = false,
        [FromQuery] string? filters = null)
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
            PageSize = Math.Min(pageSize, 5000),
            Keyword = keyword,
            SortBy = sortBy ?? "CreatedTime",
            IsDescending = isDescending,
            Filters = filterList
        };

        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<StandardInspectionRequirementDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<StandardInspectionRequirementDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<StandardInspectionRequirementDto>.Ok(result!));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Directors.Standard}")]
    public async Task<ActionResult<ApiResponse<StandardInspectionRequirementDto>>> Create([FromBody] CreateStandardInspectionRequirementRequest request)
    {
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<StandardInspectionRequirementDto>.Ok(result));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Directors.Standard}")]
    public async Task<ActionResult<ApiResponse<StandardInspectionRequirementDto>>> Update(int id, [FromBody] UpdateStandardInspectionRequirementRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<StandardInspectionRequirementDto>.Ok(result));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("filter-contexts")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }
}
