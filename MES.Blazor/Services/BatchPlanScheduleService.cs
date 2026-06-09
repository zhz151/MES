using MES.Core.DTOs;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 批次计划薄表前端服务
/// </summary>
public class BatchPlanScheduleService
{
    private readonly AuthHttpClient _http;

    public BatchPlanScheduleService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<List<BatchPlanScheduleDto>> GetAllAsync()
    {
        var response = await _http.GetFromJsonAsync<ApiListResponse>(
            $"{ApiEndpoints.BatchPlanSchedule}/all");
        return response?.Data ?? new List<BatchPlanScheduleDto>();
    }

    public async Task<bool> SaveAsync(BatchPlanScheduleDto dto)
    {
        var response = await _http.PostAsJsonAsync(
            $"{ApiEndpoints.BatchPlanSchedule}/save", dto);
        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> PlanAllAsync(string? sectionTab)
    {
        var url = $"{ApiEndpoints.BatchPlanSchedule}/plan-all";
        if (!string.IsNullOrEmpty(sectionTab))
            url += $"?sectionTab={Uri.EscapeDataString(sectionTab)}";
        var response = await _http.PostAsJsonAsync<object?>(url, null);
        response.EnsureSuccessStatusCode();
        return true;
    }

    private class ApiListResponse
    {
        public bool Success { get; set; }
        public List<BatchPlanScheduleDto> Data { get; set; } = new();
    }
}
