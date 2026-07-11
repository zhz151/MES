using MES.Shared.Constants;
using MES.Core.DTOs.Scheduling;

namespace MES.Blazor.Services;

/// <summary>
/// 冷轧排程 Blazor 前端服务（全量同步模式）
/// </summary>
public class ColdRollSpecScheduleService
{
    private readonly AuthHttpClient _http;

    public ColdRollSpecScheduleService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<List<ColdRollSpecScheduleDto>> GetAllAsync()
    {
        var response = await _http.GetFromJsonAsync<ApiListResponse>(
            $"{ApiEndpoints.ColdRollSpecSchedule}/all");
        return response?.Data ?? new List<ColdRollSpecScheduleDto>();
    }

    public async Task SaveAllAsync(List<ColdRollSpecScheduleDto> dtos)
    {
        var response = await _http.PostAsJsonAsync(
            $"{ApiEndpoints.ColdRollSpecSchedule}/save-all", dtos);
        response.EnsureSuccessStatusCode();
    }

    private class ApiListResponse
    {
        public bool Success { get; set; }
        public List<ColdRollSpecScheduleDto> Data { get; set; } = new();
    }
}
