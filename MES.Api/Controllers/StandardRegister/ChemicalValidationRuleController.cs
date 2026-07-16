using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.StandardRegister;
using MES.Core.Interfaces.StandardRegister;

namespace MES.Api.Controllers.StandardRegister;

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
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ChemicalValidationRuleDto>>> GetById(int id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item == null)
            return NotFound(ApiResponse<ChemicalValidationRuleDto>.Fail("记录不存在"));
        return Ok(ApiResponse<ChemicalValidationRuleDto>.Ok(item, "查询成功"));
    }

    /// <summary>
    /// 查询所有牌号验证规则（分页）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<ChemicalValidationRuleDto>>>> GetAll(
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
            SortBy = sortBy ?? "plantgrade",
            IsDescending = isDescending
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<ChemicalValidationRuleDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有牌号验证规则（无分页）
    /// </summary>
    [HttpGet("all-list")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ApiResponse<List<ChemicalValidationRuleDto>>> GetAllList()
    {
        var result = await _service.GetAllListAsync();
        return ApiResponse<List<ChemicalValidationRuleDto>>.Ok(result);
    }

    /// <summary>
    /// 获取筛选上下文（各列的 DISTINCT 值）
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据工厂牌号获取验证规则
    /// </summary>
    [HttpGet("by-plant-grade")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
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
    [Authorize(Roles = $"{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<ChemicalValidationRuleDto>>>> BatchCreate(
        [FromBody] List<CreateChemicalValidationRuleRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<ChemicalValidationRuleDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<ChemicalValidationRuleDto>>.Fail("请求列表不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<ChemicalValidationRuleDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 更新牌号验证规则
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Standard},{Roles.Admin}")]
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
    [Authorize(Roles = $"{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>批量打印选中记录（PDF 文件）</summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<IActionResult> PrintBatchFile([FromBody] ChemicalValidationRulePrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));
        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "牌号验证规则-选中.pdf");
    }

    /// <summary>按搜索条件打印全部记录（PDF 文件）</summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Standard},{Roles.Directors.Standard},{Roles.Admin}")]
    public async Task<IActionResult> PrintAllFile([FromBody] ChemicalValidationRulePrintAllRequest request)
    {
        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns);
        return File(pdfBytes, "application/pdf", "牌号验证规则-全部.pdf");
    }
}
