using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// 来料炉号登记控制器
/// </summary>
[ApiController]
[Route("api/furnace-registration")]
[Authorize]
public class FurnaceRegistrationController : ControllerBase
{
    private readonly IFurnaceRegistrationService _service;
    private readonly ILogger<FurnaceRegistrationController> _logger;

    public FurnaceRegistrationController(IFurnaceRegistrationService service, ILogger<FurnaceRegistrationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 获取来料炉号登记详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<FurnaceRegistrationDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<FurnaceRegistrationDto>.Fail("记录不存在"));
        return Ok(ApiResponse<FurnaceRegistrationDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有来料炉号登记（无分页）
    /// </summary>
    [HttpGet("all-list")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ApiResponse<List<FurnaceRegistrationDto>>> GetAllList()
    {
        var result = await _service.GetAllListAsync();
        return ApiResponse<List<FurnaceRegistrationDto>>.Ok(result);
    }

    /// <summary>
    /// 查询所有来料炉号登记（分页）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<FurnaceRegistrationDto>>>> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = false,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "furnacenumber",
            IsDescending = isDescending
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<FurnaceRegistrationDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 批量创建来料炉号登记
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<FurnaceRegistrationDto>>>> BatchCreate(
        [FromBody] List<CreateFurnaceRegistrationRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<FurnaceRegistrationDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<FurnaceRegistrationDto>>.Fail("请求列表不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<FurnaceRegistrationDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 更新来料炉号登记
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<FurnaceRegistrationDto>>> Update(
        int id, [FromBody] UpdateFurnaceRegistrationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<FurnaceRegistrationDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<FurnaceRegistrationDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 获取筛选上下文（各列的 DISTINCT 值）
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据登记牌号查询关联工厂牌号
    /// </summary>
    [HttpGet("lookup-grade")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string?>>> LookupPlantGrade(
        [FromQuery] string registeredGrade)
    {
        if (string.IsNullOrWhiteSpace(registeredGrade))
            return Ok(ApiResponse<string?>.Ok(null, "查询成功"));
        var result = await _service.LookupPlantGradeAsync(registeredGrade);
        return Ok(ApiResponse<string?>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 删除来料炉号登记
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }
}
