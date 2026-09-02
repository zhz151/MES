using MES.Core.DTOs.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services.Payroll;

/// <summary>考勤表前端服务</summary>
public class AttendanceService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Attendance;

    public AttendanceService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<AttendanceMonthDto>> GetMonthAsync(int year, int month, string? keyword = null)
    {
        try
        {
            var url = $"{BaseUrl}/month?year={year}&month={month}";
            if (!string.IsNullOrWhiteSpace(keyword))
                url += $"&keyword={Uri.EscapeDataString(keyword)}";
            var response = await _http.GetFromJsonAsync<ApiResponse<AttendanceMonthDto>>(url);
            return response ?? ApiResponse<AttendanceMonthDto>.Fail("获取考勤数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<AttendanceMonthDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> SaveMonthAsync(SaveAttendanceDto request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<SaveAttendanceDto, ApiResponse<int>>(BaseUrl + "/save", request);
            return response ?? ApiResponse<int>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }
}
