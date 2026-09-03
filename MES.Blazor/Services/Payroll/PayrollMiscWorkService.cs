using MES.Core.DTOs.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services.Payroll;

/// <summary>杂辅工记录（台账流水登记）前端服务</summary>
public class PayrollMiscWorkService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.PayrollMiscWork;

    public PayrollMiscWorkService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<MiscWorkMonthDto>> GetMonthAsync(int year, int month)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<MiscWorkMonthDto>>($"{BaseUrl}/month?year={year}&month={month}");
            return response ?? ApiResponse<MiscWorkMonthDto>.Fail("获取杂辅工记录失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<MiscWorkMonthDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> SaveRecordAsync(MiscWorkRecordInputDto input)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<MiscWorkRecordInputDto, ApiResponse<int>>(BaseUrl + "/record", input);
            return response ?? ApiResponse<int>.Fail("保存杂辅记录失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse<bool>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<bool>.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }
}
