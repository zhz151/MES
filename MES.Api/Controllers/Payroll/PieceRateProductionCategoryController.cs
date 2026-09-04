using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Constants;
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
    private readonly IPieceRateCategoryImportService _importService;

    public PieceRateProductionCategoryController(
        IPieceRateProductionCategoryService service,
        IPieceRateCategoryImportService importService)
    {
        _service = service;
        _importService = importService;
    }

    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>导出全量类别标准（Sheet「类别」+「维档」双表）</summary>
    [HttpGet("export-all")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult> ExportAll()
    {
        var bytes = await _importService.ExportAsync();
        return File(bytes, ExcelContentType, $"计件类别全量_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
    }

    /// <summary>生成单 sheet 导入模板（kind=category|tier，中文表头 + 1 示例行）</summary>
    [HttpGet("import/template")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult> GetTemplate([FromQuery] string kind)
    {
        if (!PieceRateImportKinds.IsValid(kind))
            return BadRequest(ApiResponse<bool>.Fail($"无效的导入类型: {kind}"));
        var bytes = await _importService.GenerateTemplateAsync(kind);
        var fileName = string.Equals(kind, PieceRateImportKinds.Tier, StringComparison.OrdinalIgnoreCase)
            ? "计件维档模板" : "计件类别模板";
        return File(bytes, ExcelContentType, $"{fileName}.xlsx");
    }

    /// <summary>解析 + 校验 + 统计（预览结果与导入同口径）</summary>
    [HttpPost("import/preview")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<ImportPreviewResult>>> PreviewImport(
        [FromQuery] string kind, IFormFile? file)
    {
        if (!PieceRateImportKinds.IsValid(kind))
            return BadRequest(ApiResponse<bool>.Fail($"无效的导入类型: {kind}"));
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<bool>.Fail("请上传 .xlsx 文件"));
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var result = await _importService.PreviewImportAsync(kind, ms.ToArray());
        return Ok(ApiResponse<ImportPreviewResult>.Ok(result));
    }

    /// <summary>事务内覆盖更新导入（任一数据行无效 → 整体拒绝）</summary>
    [HttpPost("import")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<ImportResult>>> Import(
        [FromQuery] string kind, IFormFile? file)
    {
        if (!PieceRateImportKinds.IsValid(kind))
            return BadRequest(ApiResponse<bool>.Fail($"无效的导入类型: {kind}"));
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<bool>.Fail("请上传 .xlsx 文件"));
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var result = await _importService.ImportAsync(kind, ms.ToArray());
        return Ok(ApiResponse<ImportResult>.Ok(result));
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

    /// <summary>模拟测算候选产量记录（产量源必选 + 关键字过滤 + 分页；默认记录日期降序）</summary>
    [HttpGet("trial-records")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<PagedResult<PieceRateProductionTrialRecordDto>>>> GetTrialRecords(
        [FromQuery] PieceRateProductionTrialRecordQuery query)
    {
        var result = await _service.GetTrialRecordsAsync(query);
        return Ok(ApiResponse<PagedResult<PieceRateProductionTrialRecordDto>>.Ok(result));
    }

    /// <summary>模拟测算：按一条真实产量记录计价（与月结采集同映射单源）；null = 未定价</summary>
    [HttpGet("trial-records/{source}/{id:int}/price")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<PieceRateProductionMatchResultDto?>>> MatchByRecord(
        string source, int id)
    {
        if (!Enum.TryParse<PieceRateProductionTrialSource>(source, ignoreCase: true, out var trialSource))
            return BadRequest(ApiResponse<bool>.Fail($"无效的产量源: {source}"));
        var result = await _service.MatchProductionRecordAsync(trialSource, id);
        return Ok(ApiResponse<PieceRateProductionMatchResultDto?>.Ok(result));
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
