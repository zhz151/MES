using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Scheduling;

namespace MES.Blazor.Services;

/// <summary>
/// 成检计划 Blazor 前端服务
/// </summary>
public class FinalInspectionPlanService
{
    private readonly AuthHttpClient _http;

    public FinalInspectionPlanService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<List<FinalInspectionPlanDto>> GetKanbanAsync()
    {
        var url = $"{ApiEndpoints.FinalInspectionPlan}/kanban";
        var response = await _http.GetFromJsonAsync<PlanResponse>(url);
        return response?.Data ?? new List<FinalInspectionPlanDto>();
    }

    public async Task<List<FinalInspectionPlanSummaryRowDto>> GetSummaryAsync()
    {
        try
        {
            var url = $"{ApiEndpoints.FinalInspectionPlan}/summary";
            var response = await _http.GetFromJsonAsync<ApiResponse<List<FinalInspectionPlanSummaryRowDto>>>(url);
            return response?.Data ?? new List<FinalInspectionPlanSummaryRowDto>();
        }
        catch
        {
            return new List<FinalInspectionPlanSummaryRowDto>();
        }
    }

    private class PlanResponse
    {
        public bool Success { get; set; }
        public List<FinalInspectionPlanDto> Data { get; set; } = new();
    }
}
