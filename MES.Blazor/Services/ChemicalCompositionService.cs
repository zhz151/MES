using System.Text.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class ChemicalCompositionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/chemical-composition";

    public ChemicalCompositionService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<ChemicalCompositionDto>>> GetAllAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, List<FilterDescriptor>? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<ChemicalCompositionDto>>>(url)
                   ?? ApiResponse<PagedResult<ChemicalCompositionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<ChemicalCompositionDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<ChemicalCompositionDto>>> GetAllListAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<ChemicalCompositionDto>>>($"{BaseUrl}/all-list")
                   ?? ApiResponse<List<ChemicalCompositionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<ChemicalCompositionDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<ChemicalCompositionDto>>> BatchCreateAsync(List<CreateChemicalCompositionRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateChemicalCompositionRequest>, ApiResponse<List<ChemicalCompositionDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<ChemicalCompositionDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<ChemicalCompositionDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<ChemicalCompositionDto>> UpdateAsync(int id, UpdateChemicalCompositionRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateChemicalCompositionRequest, ApiResponse<ChemicalCompositionDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<ChemicalCompositionDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<ChemicalCompositionDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<byte[]> GetTemplateAsync()
    {
        var url = $"{BaseUrl}/template";
        return await _http.GetByteArrayAsync(url);
    }

    public async Task<ApiResponse<MES.Core.Models.ImportPreviewResult>> PreviewImportAsync(byte[] fileData, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(fileData), "file", fileName);
            var response = await _http.PostMultipartAsync<ApiResponse<MES.Core.Models.ImportPreviewResult>>($"{BaseUrl}/preview", content);
            return response ?? ApiResponse<MES.Core.Models.ImportPreviewResult>.Fail("预览失败");
        }
        catch (Exception ex) { return ApiResponse<MES.Core.Models.ImportPreviewResult>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<MES.Core.Models.ImportResult>> ImportAsync(byte[] fileData, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(fileData), "file", fileName);
            var response = await _http.PostMultipartAsync<ApiResponse<MES.Core.Models.ImportResult>>($"{BaseUrl}/import", content);
            return response ?? ApiResponse<MES.Core.Models.ImportResult>.Fail("导入失败");
        }
        catch (Exception ex) { return ApiResponse<MES.Core.Models.ImportResult>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts")
                   ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex) { return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}"); }
    }
}
