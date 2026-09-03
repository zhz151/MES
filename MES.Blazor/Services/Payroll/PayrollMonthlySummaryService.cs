using MES.Core.DTOs.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services.Payroll;

/// <summary>月工资汇总前端服务（实时重算展示 + 整月保存快照；打印走 print.js openPdfFromApi 直调 -file 端点）</summary>
public class PayrollMonthlySummaryService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.PayrollMonthlySummary;

    public PayrollMonthlySummaryService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<MonthlySummaryMonthDto>> GetMonthAsync(int year, int month)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<MonthlySummaryMonthDto>>($"{BaseUrl}/month?year={year}&month={month}");
            return response ?? ApiResponse<MonthlySummaryMonthDto>.Fail("获取月工资汇总失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<MonthlySummaryMonthDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> SaveMonthAsync(SaveMonthlySummaryDto input)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<SaveMonthlySummaryDto, ApiResponse<int>>(BaseUrl + "/save", input);
            return response ?? ApiResponse<int>.Fail("保存月工资汇总失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }
}
