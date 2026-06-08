using MES.Core.DTOs;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 冷轧计划看板 Blazor 前端服务
/// </summary>
public class ColdRollPlanService
{
    private readonly AuthHttpClient _http;

    public ColdRollPlanService(AuthHttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// 获取冷轧计划看板数据
    /// </summary>
    public async Task<List<ColdRollPlanRowDto>> GetPlanAsync(string? sectionFilter)
    {
        var url = $"{ApiEndpoints.ColdRollPlan}/plan";
        if (!string.IsNullOrEmpty(sectionFilter))
            url += $"?sectionFilter={Uri.EscapeDataString(sectionFilter)}";

        var response = await _http.GetFromJsonAsync<PlanResponse>(url);
        return response?.Data ?? new List<ColdRollPlanRowDto>();
    }

    private class PlanResponse
    {
        public bool Success { get; set; }
        public List<ColdRollPlanRowDto> Data { get; set; } = new();
    }
}
