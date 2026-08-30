using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.WorkOrder;

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

}
