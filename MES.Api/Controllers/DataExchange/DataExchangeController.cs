using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.Interfaces.DataExchange;

namespace MES.Api.Controllers.DataExchange;

/// <summary>
/// 数据导入导出控制器
/// </summary>
[ApiController]
[Route("api/data-exchange")]
[Authorize]
public class DataExchangeController : ControllerBase
{
    private readonly IDataExchangeService _service;
    private readonly ILogger<DataExchangeController> _logger;

    public DataExchangeController(IDataExchangeService service, ILogger<DataExchangeController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有支持的实体类型列表
    /// </summary>
    [HttpGet("entities")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<List<EntityInfo>>>> GetEntities()
    {
        var entities = await _service.GetEntitiesAsync();
        return Ok(ApiResponse<List<EntityInfo>>.Ok(entities, "查询成功"));
    }

    /// <summary>
    /// 导出实体数据为 Excel
    /// </summary>
    [HttpGet("export/{entityKey}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Export(string entityKey)
    {
        var data = await _service.ExportAsync(entityKey);
        var displayName = _service.GetEntityDisplayName(entityKey);
        var fileName = $"{displayName}_{DateTime.Today:yyyyMMdd}.xlsx";

        return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// 下载导入模板
    /// </summary>
    [HttpGet("template/{entityKey}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Template(string entityKey)
    {
        var data = await _service.GenerateTemplateAsync(entityKey);
        var displayName = _service.GetEntityDisplayName(entityKey);
        var fileName = $"{displayName}_模板.xlsx";

        return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// 预览导入结果（不写入数据库）
    /// </summary>
    [HttpPost("preview/{entityKey}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<ImportPreviewResult>>> Preview(
        string entityKey, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<ImportPreviewResult>.Fail("请选择文件"));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var data = ms.ToArray();

        var result = await _service.PreviewAsync(entityKey, data, User.Identity?.Name);
        return Ok(ApiResponse<ImportPreviewResult>.Ok(result, "预览完成"));
    }

    /// <summary>
    /// 确认导入
    /// </summary>
    [HttpPost("import/{entityKey}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<ImportResult>>> Import(
        string entityKey, IFormFile file, [FromQuery] string strategy = "skip")
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<ImportResult>.Fail("请选择文件"));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var data = ms.ToArray();

        var result = await _service.ImportAsync(entityKey, data, strategy, User.Identity?.Name);
        var message = result.HasRolledBack
            ? $"导入失败，已回滚。共 {result.TotalRows} 行全部失败。"
            : $"导入完成: 成功 {result.SuccessCount}，失败 {result.FailedCount}";
        return Ok(ApiResponse<ImportResult>.Ok(result, message));
    }

    /// <summary>
    /// 修复现有生产记录中错误的 SequenceNumber（组内序号）
    /// 因旧版缓存键只用了"批次号+工段名称"，修正为"批次号+工序名称+制造规格+工段名称"
    /// </summary>
    [HttpPost("fix-sequence-numbers")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<int>>> FixSequenceNumbers()
    {
        var fixedCount = await _service.FixSequenceNumbersAsync();
        _logger.LogInformation("SequenceNumber 数据修复完成，共修复 {Count} 条", fixedCount);
        return Ok(ApiResponse<int>.Ok(fixedCount, $"修复完成，共修正 {fixedCount} 条记录"));
    }

    /// <summary>
    /// 一键修复所有系统计算字段
    /// </summary>
    [HttpPost("fix-all-system-fields")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<DataFixReport>>> FixAllSystemFields()
    {
        var report = await _service.FixAllSystemFieldsAsync();
        _logger.LogInformation("全字段修复完成，总计 {Total} 条", report.Total);
        return Ok(ApiResponse<DataFixReport>.Ok(report,
            $"修复完成：组内序号 {report.SequenceNumbersFixed} 条，工段委外状态 {report.OutsourceStatusFixed} 条，批次跟踪 {report.BatchTrackingFixed} 条，设备日期 {report.EquipmentFixed} 条"));
    }
}
