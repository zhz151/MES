using MES.Core.DTOs.Configuration;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 工艺卡打印列布局配置服务（前端）：格式设置面板全量加载/批量保存（全局共享）。
/// </summary>
public class ProcessCardColumnDefinitionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.ProcessCardColumnDefinition;

    public ProcessCardColumnDefinitionService(AuthHttpClient http)
    {
        _http = http;
    }

    /// <summary>全量配置（格式设置面板加载），按 BlockKey 升序 + ColumnIndex 升序；失败返回 null 走兜底默认</summary>
    public async Task<List<ProcessCardColumnDefinitionDto>?> GetAllAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<ProcessCardColumnDefinitionDto>>>($"{BaseUrl}/all");
            return response?.Success == true ? response.Data : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>批量新增/更新（锚点 BlockKey+FieldKey），返回写入行数</summary>
    public async Task<ApiResponse<int>> SaveAllAsync(List<ProcessCardColumnDefinitionDto> items)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<List<ProcessCardColumnDefinitionDto>, ApiResponse<int>>($"{BaseUrl}/save-all", items);
            return response ?? ApiResponse<int>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }
}
