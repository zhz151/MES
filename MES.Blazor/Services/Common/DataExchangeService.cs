using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 数据导入导出前端服务
/// </summary>
public class DataExchangeService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.DataExchange;

    public DataExchangeService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<List<EntityInfo>>> GetEntitiesAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<EntityInfo>>>($"{BaseUrl}/entities");
            return response ?? ApiResponse<List<EntityInfo>>.Fail("获取实体列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<EntityInfo>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<byte[]> ExportAsync(string entityKey)
    {
        var url = $"{BaseUrl}/export/{entityKey}";
        return await _http.GetByteArrayAsync(url);
    }

    public async Task<byte[]> GetTemplateAsync(string entityKey)
    {
        var url = $"{BaseUrl}/template/{entityKey}";
        return await _http.GetByteArrayAsync(url);
    }

    public async Task<ApiResponse<ImportPreviewResult>> PreviewAsync(string entityKey, byte[] fileData, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(fileData), "file", fileName);
            var url = $"{BaseUrl}/preview/{entityKey}";
            var response = await _http.PostMultipartAsync<ApiResponse<ImportPreviewResult>>(url, content);
            return response ?? ApiResponse<ImportPreviewResult>.Fail("预览失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ImportPreviewResult>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ImportResult>> ImportAsync(string entityKey, byte[] fileData, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(fileData), "file", fileName);
            var url = $"{BaseUrl}/import/{entityKey}";
            var response = await _http.PostMultipartAsync<ApiResponse<ImportResult>>(url, content);
            return response ?? ApiResponse<ImportResult>.Fail("导入失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ImportResult>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 一键修复所有系统计算字段
    /// </summary>
    public async Task<ApiResponse<DataFixReport>> FixAllSystemFieldsAsync()
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object?, ApiResponse<DataFixReport>>($"{BaseUrl}/fix-all-system-fields", null);
            return response ?? ApiResponse<DataFixReport>.Fail("请求失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<DataFixReport>.Fail($"网络错误: {ex.Message}");
        }
    }
}

public class EntityInfo
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>上下文归类（由 DisplayName 前缀解析，如「订单」「扫码」）</summary>
    public string Context { get; set; } = "";
}

public class ImportPreviewResult
{
    public int TotalRows { get; set; }
    public int ValidCount { get; set; }
    public int ErrorCount { get; set; }
    public int DuplicateCount { get; set; }
    public int AddCount { get; set; }
    public int OverwriteCount { get; set; }
    public int InvalidIdCount { get; set; }
    public List<ImportRowResult> RowResults { get; set; } = new();
}

public class ImportRowResult
{
    public int RowNumber { get; set; }
    public string? Key { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool IsDuplicate { get; set; }
    public bool IsValid { get; set; }
    public string RowAction { get; set; } = "新增";
    /// <summary>该行是否携带了 ID 列的值（有 ID 且命中则按 ID 覆盖）</summary>
    public bool HasId { get; set; }
    /// <summary>该行的处理说明（覆盖通道 / 新增原因），供预览界面展示</summary>
    public string? ActionNote { get; set; }
}

public class ImportResult
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public bool HasRolledBack { get; set; }
    public string? RollbackReason { get; set; }
    public List<ImportRowError> Errors { get; set; } = new();
}

public class ImportRowError
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = "";
}
