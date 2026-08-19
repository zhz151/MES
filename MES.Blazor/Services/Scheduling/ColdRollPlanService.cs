using MES.Shared.Constants;
using MES.Core.DTOs.Scheduling;

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

    /// <summary>
    /// 获取冷轧排程汇总（分档与主列表统一：在轧/待轧 各分 总量/急+/急/急-/余量）
    /// </summary>
    public async Task<List<ColdRollPlanSummaryDto>> GetScheduleSummaryAsync(string? sectionFilter, int? maxDiff = null)
    {
        var url = $"{ApiEndpoints.ColdRollPlan}/summary";
        var query = new List<string>();
        if (!string.IsNullOrEmpty(sectionFilter))
            query.Add($"sectionFilter={Uri.EscapeDataString(sectionFilter)}");
        if (maxDiff.HasValue)
            query.Add($"maxDiff={maxDiff.Value}");
        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        var response = await _http.GetFromJsonAsync<SummaryResponse>(url);
        return response?.Data ?? new List<ColdRollPlanSummaryDto>();
    }

    /// <summary>
    /// 获取冷轧排程排机估算（按轧机类型聚合：冷轧5060/冷轧2030/冷轧三辊/冷拔）
    /// </summary>
    public async Task<List<ColdRollMachineEstimateDto>> GetMachineEstimateAsync()
    {
        var url = $"{ApiEndpoints.ColdRollPlan}/machine-estimate";
        var response = await _http.GetFromJsonAsync<MachineEstimateResponse>(url);
        return response?.Data ?? new List<ColdRollMachineEstimateDto>();
    }

    private class PlanResponse
    {
        public bool Success { get; set; }
        public List<ColdRollPlanRowDto> Data { get; set; } = new();
    }

    private class SummaryResponse
    {
        public bool Success { get; set; }
        public List<ColdRollPlanSummaryDto> Data { get; set; } = new();
    }

    private class MachineEstimateResponse
    {
        public bool Success { get; set; }
        public List<ColdRollMachineEstimateDto> Data { get; set; } = new();
    }
}
