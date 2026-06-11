using MES.Core.DTOs;
using MES.Shared.Constants;
using System.Net.Http.Json;

namespace MES.Blazor.Services;

/// <summary>
/// 成检计划 Blazor 前端服务
/// </summary>
public class FinalInspectionPlanService
{
    private readonly HttpClient _http;

    public FinalInspectionPlanService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<FinalInspectionPlanDto>> GetKanbanAsync()
    {
        var url = $"{ApiEndpoints.FinalInspectionPlan}/kanban";
        var response = await _http.GetFromJsonAsync<PlanResponse>(url);
        return response?.Data ?? new List<FinalInspectionPlanDto>();
    }

    private class PlanResponse
    {
        public bool Success { get; set; }
        public List<FinalInspectionPlanDto> Data { get; set; } = new();
    }
}
