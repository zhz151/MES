using MES.Core.DTOs.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services.Payroll;

/// <summary>集体计件月结（集体=岗位 × 月度评分 × 月结快照）前端服务</summary>
public class PayrollCollectiveService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.PayrollCollective;

    public PayrollCollectiveService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<CollectiveMonthDto>> GetMonthAsync(int year, int month)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<CollectiveMonthDto>>($"{BaseUrl}/month?year={year}&month={month}");
            return response ?? ApiResponse<CollectiveMonthDto>.Fail("获取集体月结数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<CollectiveMonthDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> SaveMonthAsync(SaveCollectiveMonthDto request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<SaveCollectiveMonthDto, ApiResponse<int>>(BaseUrl + "/month/save", request);
            return response ?? ApiResponse<int>.Fail("保存月结失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<CollectiveScoresDto>> GetScoresAsync(int year, int month)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<CollectiveScoresDto>>($"{BaseUrl}/scores?year={year}&month={month}");
            return response ?? ApiResponse<CollectiveScoresDto>.Fail("获取月度评分失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<CollectiveScoresDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> SaveScoresAsync(SaveCollectiveScoresDto request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<SaveCollectiveScoresDto, ApiResponse<int>>(BaseUrl + "/scores/save", request);
            return response ?? ApiResponse<int>.Fail("保存评分失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }
}
