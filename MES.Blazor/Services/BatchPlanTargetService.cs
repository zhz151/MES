using MES.Core.DTOs;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 批次计划产量目标前端服务
/// </summary>
public class BatchPlanTargetService
{
    private readonly AuthHttpClient _http;

    public BatchPlanTargetService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<List<BatchPlanTargetDto>> GetAllAsync()
    {
        var response = await _http.GetFromJsonAsync<ApiListResponse>(
            $"{ApiEndpoints.BatchPlanTarget}/all");
        return response?.Data ?? new List<BatchPlanTargetDto>();
    }

    public async Task<bool> SaveAllAsync(List<BatchPlanTargetDto> dtos)
    {
        var response = await _http.PostAsJsonAsync(
            $"{ApiEndpoints.BatchPlanTarget}/save-all", dtos);
        response.EnsureSuccessStatusCode();
        return true;
    }

    private class ApiListResponse
    {
        public bool Success { get; set; }
        public List<BatchPlanTargetDto> Data { get; set; } = new();
    }
}
