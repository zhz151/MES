using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

/// <summary>
/// 牌号验证控制器
/// </summary>
[ApiController]
[Route("api/chemical-validation-rule")]
[Authorize]
public class ChemicalValidationRuleController : ControllerBase
{
    private readonly IChemicalValidationRuleService _service;
    private readonly ILogger<ChemicalValidationRuleController> _logger;

    public ChemicalValidationRuleController(IChemicalValidationRuleService service, ILogger<ChemicalValidationRuleController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 获取牌号验证规则详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ChemicalValidationRuleDto>>> GetById(int id)
    {
        var result = await _service.GetByPlantGradeAsync("");
        // 简化为先从 GetAllAsync 获取
        var query = new QueryParams { PageIndex = 1, PageSize = 1 };
        var all = await _service.GetAllAsync(query);
        var item = all.Items.FirstOrDefault(x => x.Id == id);
        if (item == null)
            return NotFound(ApiResponse<ChemicalValidationRuleDto>.Fail("记录不存在"));
        return Ok(ApiResponse<ChemicalValidationRuleDto>.Ok(item, "查询成功"));
    }

    /// <summary>
    /// 查询所有牌号验证规则（分页）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<ChemicalValidationRuleDto>>>> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = false)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "plantgrade",
            IsDescending = isDescending
        };
        var result = await _service.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<ChemicalValidationRuleDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据工厂牌号获取验证规则
    /// </summary>
    [HttpGet("by-plant-grade")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ChemicalValidationRuleDto?>>> GetByPlantGrade(
        [FromQuery] string plantGrade)
    {
        var result = await _service.GetByPlantGradeAsync(plantGrade);
        return Ok(ApiResponse<ChemicalValidationRuleDto?>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 批量创建牌号验证规则
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<ChemicalValidationRuleDto>>>> BatchCreate(
        [FromBody] List<CreateChemicalValidationRuleRequest> requests)
    {
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<ChemicalValidationRuleDto>>.Fail("请求列表不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<ChemicalValidationRuleDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 更新牌号验证规则
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ChemicalValidationRuleDto>>> Update(
        int id, [FromBody] UpdateChemicalValidationRuleRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<ChemicalValidationRuleDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<ChemicalValidationRuleDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除牌号验证规则
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }
}
