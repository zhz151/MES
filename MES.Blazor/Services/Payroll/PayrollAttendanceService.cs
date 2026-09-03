using MES.Core.DTOs.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services.Payroll;

/// <summary>靠工计件月结（平均小时工资 × 本人出勤 × 靠工系数 × 月结快照）前端服务</summary>
public class PayrollAttendanceService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.PayrollAttendance;

    public PayrollAttendanceService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<AttendanceWageMonthDto>> GetMonthAsync(int year, int month)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<AttendanceWageMonthDto>>($"{BaseUrl}/month?year={year}&month={month}");
            return response ?? ApiResponse<AttendanceWageMonthDto>.Fail("获取靠工月结数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<AttendanceWageMonthDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> SaveMonthAsync(SaveAttendanceWageDto request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<SaveAttendanceWageDto, ApiResponse<int>>(BaseUrl + "/month/save", request);
            return response ?? ApiResponse<int>.Fail("保存靠工月结失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }
}
