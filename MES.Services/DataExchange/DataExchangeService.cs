using Microsoft.Extensions.Logging;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Models;
using MES.Data;

namespace MES.Services.DataExchange;

/// <summary>
/// 数据导入导出服务 Facade
/// </summary>
public class DataExchangeService : IDataExchangeService
{
    private readonly IDataImportService _importService;
    private readonly IDataExportService _exportService;
    private readonly IDataFixService _fixService;
    private readonly ILogger<DataExchangeService> _logger;

    public DataExchangeService(
        IDataImportService importService,
        IDataExportService exportService,
        IDataFixService fixService,
        ILogger<DataExchangeService> logger)
    {
        _importService = importService;
        _exportService = exportService;
        _fixService = fixService;
        _logger = logger;
    }

    public Task<List<EntityInfo>> GetEntitiesAsync()
    {
        return Task.FromResult(DataExchangeRegistry.GetEntities());
    }

    public string GetEntityDisplayName(string entityKey)
    {
        return DataExchangeRegistry.GetEntityDisplayName(entityKey);
    }

    public Task<byte[]> ExportAsync(string entityKey)
        => _exportService.ExportAsync(entityKey);

    public Task<byte[]> GenerateTemplateAsync(string entityKey)
        => _exportService.GenerateTemplateAsync(entityKey);

    public Task<ImportPreviewResult> PreviewAsync(string entityKey, byte[] fileData, string? userName)
        => _importService.PreviewAsync(entityKey, fileData, userName);

    public Task<ImportResult> ImportAsync(string entityKey, byte[] fileData, string strategy, string? userName)
        => _importService.ImportAsync(entityKey, fileData, strategy, userName);

    /// <summary>
    /// 修复生产记录中错误的 SequenceNumber 值（委托给 DataFixService）
    /// </summary>
    public async Task<int> FixSequenceNumbersAsync()
    {
        _logger.LogInformation("FixSequenceNumbersAsync 已委托给 DataFixService.FixAllAsync");
        var report = await _fixService.FixAllAsync();
        return report.SequenceNumbersFixed;
    }

    /// <summary>
    /// 一键修复所有系统计算字段
    /// </summary>
    public async Task<DataFixReport> FixAllSystemFieldsAsync()
    {
        return await _fixService.FixAllAsync();
    }
}
