using MES.Core.DTOs.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services.Payroll;

/// <summary>每日工资（非计件工资 / 个人计件工资）前端服务</summary>
public class PayrollDailyWageService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.PayrollDailyWage;

    public PayrollDailyWageService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<DailyWageMonthDto>> GetMonthAsync(int year, int month, string group, string? keyword = null)
    {
        try
        {
            var url = $"{BaseUrl}/month?year={year}&month={month}&group={group}";
            if (!string.IsNullOrWhiteSpace(keyword))
                url += $"&keyword={Uri.EscapeDataString(keyword)}";
            var response = await _http.GetFromJsonAsync<ApiResponse<DailyWageMonthDto>>(url);
            return response ?? ApiResponse<DailyWageMonthDto>.Fail("获取每日工资数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<DailyWageMonthDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> SaveMonthAsync(SaveDailyWageDto request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<SaveDailyWageDto, ApiResponse<int>>(BaseUrl + "/save", request);
            return response ?? ApiResponse<int>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }
}
