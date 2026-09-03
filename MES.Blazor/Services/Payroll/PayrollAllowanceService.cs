using MES.Core.DTOs.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services.Payroll;

/// <summary>「津贴与处罚」月度金额录入表（宽表 9 列）前端服务</summary>
public class PayrollAllowanceService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.PayrollAllowance;

    public PayrollAllowanceService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<AllowanceMonthDto>> GetMonthAsync(int year, int month)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<AllowanceMonthDto>>($"{BaseUrl}/month?year={year}&month={month}");
            return response ?? ApiResponse<AllowanceMonthDto>.Fail("获取津贴与处罚失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<AllowanceMonthDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> SaveMonthAsync(SaveAllowanceMonthDto input)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<SaveAllowanceMonthDto, ApiResponse<int>>(BaseUrl + "/month/save", input);
            return response ?? ApiResponse<int>.Fail("保存津贴与处罚失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }
}
