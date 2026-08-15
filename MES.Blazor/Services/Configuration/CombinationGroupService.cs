using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Services;

/// <summary>
/// 组合归类前端服务
/// </summary>
public class CombinationGroupService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.CombinationGroup;

    public CombinationGroupService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<List<CombinationGroupDto>>> GetListAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<CombinationGroupDto>>>($"{BaseUrl}/list");
            return response ?? ApiResponse<List<CombinationGroupDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<CombinationGroupDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> SaveAsync(CombinationGroupDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CombinationGroupDto, ApiResponse>($"{BaseUrl}/save", dto);
            return response ?? ApiResponse.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse>($"{BaseUrl}/{id}");
            return response ?? ApiResponse.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }
}
