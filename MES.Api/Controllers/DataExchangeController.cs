using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

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
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<EntityInfo>>>> GetEntities()
    {
        var entities = await _service.GetEntitiesAsync();
        return Ok(ApiResponse<List<EntityInfo>>.Ok(entities, "查询成功"));
    }

    /// <summary>
    /// 导出实体数据为 Excel
    /// </summary>
    [HttpGet("export/{entityKey}")]
    [Authorize]
    public async Task<IActionResult> Export(string entityKey)
    {
        try
        {
            var data = await _service.ExportAsync(entityKey);
            var displayName = _service.GetEntityDisplayName(entityKey);
            var fileName = $"{displayName}_{DateTime.Today:yyyyMMdd}.xlsx";

            return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出 {Entity} 失败", entityKey);
            return StatusCode(500, new { message = $"导出失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 下载导入模板
    /// </summary>
    [HttpGet("template/{entityKey}")]
    [Authorize]
    public async Task<IActionResult> Template(string entityKey)
    {
        try
        {
            var data = await _service.GenerateTemplateAsync(entityKey);
            var displayName = _service.GetEntityDisplayName(entityKey);
            var fileName = $"{displayName}_模板.xlsx";

            return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成模板 {Entity} 失败", entityKey);
            return StatusCode(500, new { message = $"生成模板失败: {ex.Message}" });
        }
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
}
