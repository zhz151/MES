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
/// 成检计件类别（2026-09-03 引入）接口：类别维护（成检项目单选）+ 维档整组编辑 + 试算匹配。
/// 与生产计件类别差异：无「工段/工序/产类/作业阶段」约束，主表直接以成检项目 InpectionItem 定位。
/// </summary>
[ApiController]
[Route(ApiEndpoints.PieceRateFinalInspectionCategory)]
[Authorize]
public class PieceRateFinalInspectionCategoryController : ControllerBase
{
    private readonly IPieceRateFinalInspectionCategoryService _service;
    private readonly IPieceRateFinalInspectionCategoryImportService _importService;

    public PieceRateFinalInspectionCategoryController(
        IPieceRateFinalInspectionCategoryService service,
        IPieceRateFinalInspectionCategoryImportService importService)
    {
        _service = service;
        _importService = importService;
    }

    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>导出全量成检类别标准（Sheet「类别」+「维档」双表）</summary>
    [HttpGet("export-all")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult> ExportAll()
    {
        var bytes = await _importService.ExportAsync();
        return File(bytes, ExcelContentType, $"成检类别全量_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
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
            ? "成检维档模板" : "成检类别模板";
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
    public async Task<ActionResult<ApiResponse<PagedResult<PieceRateFinalInspectionCategoryListItemDto>>>> GetPaged(
        [FromQuery] PieceRateFinalInspectionCategoryQueryParams query,
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
        return Ok(ApiResponse<PagedResult<PieceRateFinalInspectionCategoryListItemDto>>.Ok(result));
    }

    /// <summary>按 Id 获取详情（含维档全量，供编辑页）</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<PieceRateFinalInspectionCategoryDetailDto>>> GetById(int id)
    {
        var result = await _service.GetDetailAsync(id);
        if (result == null)
            return NotFound(ApiResponse<PieceRateFinalInspectionCategoryDetailDto>.Fail($"类别不存在: {id}"));
        return Ok(ApiResponse<PieceRateFinalInspectionCategoryDetailDto>.Ok(result));
    }

    /// <summary>创建类别（定义 + 维档整组）</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<PieceRateFinalInspectionCategoryDetailDto>>> Create(
        [FromBody] PieceRateFinalInspectionCategorySaveRequest request)
    {
        var result = await _service.SaveAsync(null, request);
        return Ok(ApiResponse<PieceRateFinalInspectionCategoryDetailDto>.Ok(result));
    }

    /// <summary>更新类别（改定义/整组替换维档；同成检项目启用唯一）</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<PieceRateFinalInspectionCategoryDetailDto>>> Update(
        int id, [FromBody] PieceRateFinalInspectionCategorySaveRequest request)
    {
        var result = await _service.SaveAsync(id, request);
        return Ok(ApiResponse<PieceRateFinalInspectionCategoryDetailDto>.Ok(result));
    }

    /// <summary>删除类别（级联删维档）</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Policies.SalaryDelete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>类别编辑页选项源（成检项目/单位/长度状态/特殊状态/工厂牌号）</summary>
    [HttpGet("options")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<PieceRateFinalInspectionCategoryOptionsDto>>> GetOptions()
    {
        var result = await _service.GetOptionsAsync();
        return Ok(ApiResponse<PieceRateFinalInspectionCategoryOptionsDto>.Ok(result));
    }

    /// <summary>试算匹配：一条成检 → 命中类别单价；返回 null = 未定价</summary>
    [HttpPost("match-price")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<PieceRateFinalInspectionMatchResultDto?>>> MatchPrice(
        [FromBody] PieceRateFinalInspectionMatchRequest request)
    {
        var result = await _service.MatchPriceAsync(request);
        return Ok(ApiResponse<PieceRateFinalInspectionMatchResultDto?>.Ok(result));
    }

    /// <summary>模拟测算候选成检记录（全局任意记录：成检项目/关键字过滤 + 服务端分页，默认检验日期降序）</summary>
    [HttpGet("trial-records")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<PagedResult<FinalInspectionPriceTrialRecordDto>>>> GetTrialRecords(
        [FromQuery] FinalInspectionPriceTrialRecordQuery query)
    {
        var result = await _service.GetTrialRecordsAsync(query);
        return Ok(ApiResponse<PagedResult<FinalInspectionPriceTrialRecordDto>>.Ok(result));
    }

    /// <summary>模拟测算：按一条真实成检记录计价（与月结采集同 FinalInspectionMatchRequestMapper 单源映射）；返回 null = 未定价</summary>
    [HttpGet("trial-records/{id:int}/price")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<PieceRateFinalInspectionMatchResultDto?>>> MatchByRecord(int id)
    {
        var result = await _service.MatchFinalInspectionRecordAsync(id);
        return Ok(ApiResponse<PieceRateFinalInspectionMatchResultDto?>.Ok(result));
    }
}
