using System.Text.Json;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Services;

/// <summary>
/// 工段流转分类设置前端服务
/// </summary>
public class SectionFlowCategoryService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.SectionFlowCategorySettings;

    public SectionFlowCategoryService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<List<SectionFlowCategorySettingDto>>> GetSettingsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<SectionFlowCategorySettingDto>>>(BaseUrl);
            return response ?? ApiResponse<List<SectionFlowCategorySettingDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SectionFlowCategorySettingDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> SaveSettingAsync(SectionFlowCategorySettingDto dto)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<SectionFlowCategorySettingDto, ApiResponse>(BaseUrl, dto);
            return response ?? ApiResponse.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> CreateItemAsync(int settingId, SectionFlowCategoryItemDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<SectionFlowCategoryItemDto, ApiResponse>($"{BaseUrl}/{settingId}/items", dto);
            return response ?? ApiResponse.Fail("新增失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> SaveItemAsync(int itemId, SectionFlowCategoryItemDto dto)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<SectionFlowCategoryItemDto, ApiResponse>($"{BaseUrl}/items/{itemId}", dto);
            return response ?? ApiResponse.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> DeleteItemAsync(int itemId)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse>($"{BaseUrl}/items/{itemId}");
            return response ?? ApiResponse.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }
}
