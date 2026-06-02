using MES.Core.DTOs;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 用料计划工序组前端服务
/// </summary>
public class MaterialPlanProcessGroupService
{
    private readonly AuthHttpClient _http;

    public MaterialPlanProcessGroupService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<List<MaterialPlanProcessGroupDto>>> GetByPlanAsync(int planType, int planId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<MaterialPlanProcessGroupDto>>>(
                $"{ApiEndpoints.MaterialPlan}/{planType}/process-groups/{planId}");
            return response ?? ApiResponse<List<MaterialPlanProcessGroupDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<MaterialPlanProcessGroupDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 保存用料计划工序组（全量替换）
    /// </summary>
    public async Task<ApiResponse<object>> SaveAsync(int planType, int planId, List<SavePlanProcessGroupItem> items)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<List<SavePlanProcessGroupItem>, ApiResponse<object>>(
                $"{ApiEndpoints.MaterialPlan}/{planType}/process-groups/{planId}/save", items);
            return response ?? ApiResponse<object>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }
}
