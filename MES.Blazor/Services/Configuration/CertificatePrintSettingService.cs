using MES.Core.DTOs.Configuration;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 质量证明书打印配置服务（前端）：质量证明书列表页「打印设置」对话框全量加载/批量保存（全局共享）。
/// </summary>
public class CertificatePrintSettingService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.CertificatePrintSetting;

    public CertificatePrintSettingService(AuthHttpClient http)
    {
        _http = http;
    }

    /// <summary>全量配置（「打印设置」对话框加载），按 Key 升序；失败返回 null 走兜底默认</summary>
    public async Task<List<CertificatePrintSettingDto>?> GetAllAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<CertificatePrintSettingDto>>>($"{BaseUrl}/all");
            return response?.Success == true ? response.Data : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>批量新增/更新（锚点 Key），返回写入行数</summary>
    public async Task<ApiResponse<int>> SaveAllAsync(List<CertificatePrintSettingDto> items)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<List<CertificatePrintSettingDto>, ApiResponse<int>>($"{BaseUrl}/save-all", items);
            return response ?? ApiResponse<int>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }
}
