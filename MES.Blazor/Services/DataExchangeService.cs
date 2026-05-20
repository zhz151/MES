using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 数据导入导出前端服务
/// </summary>
public class DataExchangeService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/data-exchange";

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
            var response = await _http.PostMultipartAsync<ApiResponse<ImportPreviewResult>>($"{BaseUrl}/preview/{entityKey}", content);
            return response ?? ApiResponse<ImportPreviewResult>.Fail("预览失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ImportPreviewResult>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ImportResult>> ImportAsync(string entityKey, byte[] fileData, string fileName, string strategy)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(fileData), "file", fileName);
            var url = $"{BaseUrl}/import/{entityKey}?strategy={strategy}";
            var response = await _http.PostMultipartAsync<ApiResponse<ImportResult>>(url, content);
            return response ?? ApiResponse<ImportResult>.Fail("导入失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ImportResult>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 一键修复组内序号（修正生产记录/过程检验/工段委外中错误的 SequenceNumber）
    /// </summary>
    public async Task<ApiResponse<int>> FixSequenceNumbersAsync()
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object?, ApiResponse<int>>($"{BaseUrl}/fix-sequence-numbers", null);
            return response ?? ApiResponse<int>.Fail("请求失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
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
}

public class ImportPreviewResult
{
    public int TotalRows { get; set; }
    public int ValidCount { get; set; }
    public int ErrorCount { get; set; }
    public int DuplicateCount { get; set; }
    public List<ImportRowResult> RowResults { get; set; } = new();
}

public class ImportRowResult
{
    public int RowNumber { get; set; }
    public string? Key { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool IsDuplicate { get; set; }
    public bool IsValid { get; set; }
}

public class ImportResult
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public string Strategy { get; set; } = "skip";
    public bool HasRolledBack { get; set; }
    public string? RollbackReason { get; set; }
    public List<ImportRowError> Errors { get; set; } = new();
}

public class ImportRowError
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = "";
}
