using System.Text.Json;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Scheduling;

namespace MES.Blazor.Services;

/// <summary>
/// 生产段流转量分析前端服务
/// </summary>
public class SectionFlowAnalysisService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.SectionFlowAnalysis;

    public SectionFlowAnalysisService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<List<SectionFlowAnalysisDto>>> GetAnalysisAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<SectionFlowAnalysisDto>>>(BaseUrl);
            return response ?? ApiResponse<List<SectionFlowAnalysisDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SectionFlowAnalysisDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> UpdateSettingAsync(SectionFlowSettingUpdateDto dto)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<SectionFlowSettingUpdateDto, ApiResponse>($"{BaseUrl}/setting", dto);
            return response ?? ApiResponse.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    // ========== 参数表管理 ==========

    public async Task<ApiResponse<List<SectionFlowCategorySettingDto>>> GetSettingsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<SectionFlowCategorySettingDto>>>($"{BaseUrl}/settings");
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
            var response = await _http.PutAsJsonAsync<SectionFlowCategorySettingDto, ApiResponse>($"{BaseUrl}/settings", dto);
            return response ?? ApiResponse.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }
}
