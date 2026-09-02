using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Payroll;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Payroll;

/// <summary>
/// 生产计件类别（2026-09-02 两表模型）接口：类别维护 + 维档整组编辑 + 试算匹配。
/// </summary>
[ApiController]
[Route(ApiEndpoints.PieceRateProductionCategory)]
[Authorize]
public class PieceRateProductionCategoryController : ControllerBase
{
    private readonly IPieceRateProductionCategoryService _service;

    public PieceRateProductionCategoryController(IPieceRateProductionCategoryService service)
    {
        _service = service;
    }

    /// <summary>分页查询类别（filters 为列级筛选 JSON，独立参数手动反序列化）</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<PagedResult<PieceRateProductionCategoryListItemDto>>>> GetPaged(
        [FromQuery] PieceRateProductionCategoryQueryParams query,
        [FromQuery] string? filters = null)
    {
        if (!string.IsNullOrEmpty(filters))
        {
            try
            {
                query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                // 筛选 JSON 非法时忽略，回退为无条件查询
            }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<PieceRateProductionCategoryListItemDto>>.Ok(result));
    }

    /// <summary>按 Id 获取详情（含维档全量，供编辑页）</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<PieceRateProductionCategoryDetailDto>>> GetById(int id)
    {
        var result = await _service.GetDetailAsync(id);
        if (result == null)
            return NotFound(ApiResponse<PieceRateProductionCategoryDetailDto>.Fail($"类别不存在: {id}"));
        return Ok(ApiResponse<PieceRateProductionCategoryDetailDto>.Ok(result));
    }

    /// <summary>创建类别（定义 + 维档整组）</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<PieceRateProductionCategoryDetailDto>>> Create(
        [FromBody] PieceRateProductionCategorySaveRequest request)
    {
        var result = await _service.SaveAsync(null, request);
        return Ok(ApiResponse<PieceRateProductionCategoryDetailDto>.Ok(result));
    }

    /// <summary>更新类别（停用旧/改定义/整组替换维档）</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<PieceRateProductionCategoryDetailDto>>> Update(
        int id, [FromBody] PieceRateProductionCategorySaveRequest request)
    {
        var result = await _service.SaveAsync(id, request);
        return Ok(ApiResponse<PieceRateProductionCategoryDetailDto>.Ok(result));
    }

    /// <summary>删除类别（级联删维档）</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Policies.SalaryDelete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>类别编辑页选项源（工段/工序/产类/阶段/单位/状态/牌号）</summary>
    [HttpGet("options")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<PieceRateProductionCategoryOptionsDto>>> GetOptions()
    {
        var result = await _service.GetOptionsAsync();
        return Ok(ApiResponse<PieceRateProductionCategoryOptionsDto>.Ok(result));
    }

    /// <summary>试算匹配：一条报工 → 命中类别单价；返回 null = 未定价</summary>
    [HttpPost("match-price")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<PieceRateProductionMatchResultDto?>>> MatchPrice(
        [FromBody] PieceRateProductionMatchRequest request)
    {
        var result = await _service.MatchPriceAsync(request);
        return Ok(ApiResponse<PieceRateProductionMatchResultDto?>.Ok(result));
    }
}
