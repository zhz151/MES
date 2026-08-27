using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.StandardRegister;
using MES.Core.Interfaces.StandardRegister;

namespace MES.Api.Controllers.StandardRegister;

/// <summary>
/// 牌号化学成分控制器
/// </summary>
[ApiController]
[Route("api/chemical-composition")]
[Authorize]
public class ChemicalCompositionController : ControllerBase
{
    private readonly IChemicalCompositionService _service;

    public ChemicalCompositionController(IChemicalCompositionService service)
    {
        _service = service;
    }

    /// <summary>
    /// 查询所有牌号化学成分（分页）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ChemicalCompositionDto>>>> GetAll(
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
        return Ok(ApiResponse<PagedResult<ChemicalCompositionDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有牌号化学成分（无分页）
    /// </summary>
    [HttpGet("all-list")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<ActionResult<ApiResponse<List<ChemicalCompositionDto>>>> GetAllList()
    {
        var result = await _service.GetAllListAsync();
        return Ok(ApiResponse<List<ChemicalCompositionDto>>.Ok(result));
    }

    /// <summary>
    /// 批量创建牌号化学成分
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = Roles.Policies.StandardEdit)]
    public async Task<ActionResult<ApiResponse<List<ChemicalCompositionDto>>>> BatchCreate(
        [FromBody] List<CreateChemicalCompositionRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<ChemicalCompositionDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<ChemicalCompositionDto>>.Fail("请求列表不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<ChemicalCompositionDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 更新牌号化学成分
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.StandardEdit)]
    public async Task<ActionResult<ApiResponse<ChemicalCompositionDto>>> Update(
        int id, [FromBody] UpdateChemicalCompositionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<ChemicalCompositionDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<ChemicalCompositionDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除牌号化学成分
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.StandardDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>
    /// 获取筛选上下文（各列的 DISTINCT 值）
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 下载Excel导入模板
    /// </summary>
    [HttpGet("template")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<IActionResult> DownloadTemplate()
    {
        var data = await _service.GenerateTemplateAsync();
        return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "牌号化学成分_模板.xlsx");
    }

    /// <summary>
    /// 预览Excel导入结果（不写入数据库）
    /// </summary>
    [HttpPost("preview")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<ActionResult<ApiResponse<ImportPreviewResult>>> Preview(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<ImportPreviewResult>.Fail("请选择文件"));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var data = ms.ToArray();

        var result = await _service.PreviewImportAsync(data, file.FileName ?? "");
        return Ok(ApiResponse<ImportPreviewResult>.Ok(result));
    }

    /// <summary>
    /// 上传Excel导入牌号化学成分
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = Roles.Policies.StandardEdit)]
    public async Task<ActionResult<ApiResponse<ImportResult>>> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<ImportResult>.Fail("请选择文件"));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var data = ms.ToArray();

        var result = await _service.ImportAsync(data, file.FileName ?? "", User.Identity?.Name);
        var message = result.HasRolledBack
            ? $"导入失败，已回滚。{result.RollbackReason}"
            : $"导入完成: 成功 {result.SuccessCount}，失败 {result.FailedCount}";
        return Ok(ApiResponse<ImportResult>.Ok(result, message));
    }

    /// <summary>批量打印选中记录（PDF 文件）</summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<IActionResult> PrintBatchFile([FromBody] ChemicalCompositionPrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));
        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "牌号化学成分-选中.pdf");
    }

    /// <summary>按搜索条件打印全部记录（PDF 文件）</summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<IActionResult> PrintAllFile([FromBody] ChemicalCompositionPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns);
        return File(pdfBytes, "application/pdf", "牌号化学成分-全部.pdf");
    }
}
