using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Services;

/// <summary>
/// 段落日产配置前端服务
/// </summary>
public class SectionParagraphConfigService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.SectionParagraphConfigSettings;

    public SectionParagraphConfigService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<List<SectionParagraphConfigDto>>> GetSettingsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<SectionParagraphConfigDto>>>(BaseUrl);
            return response ?? ApiResponse<List<SectionParagraphConfigDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SectionParagraphConfigDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> SaveSettingAsync(SectionParagraphConfigDto dto)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<SectionParagraphConfigDto, ApiResponse>(BaseUrl, dto);
            return response ?? ApiResponse.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }
}
