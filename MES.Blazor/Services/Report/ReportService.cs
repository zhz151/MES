using MES.Shared.Constants;
using MES.Core.DTOs.Report;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class ReportService
{
    private readonly AuthHttpClient _http;

    public ReportService(AuthHttpClient http) => _http = http;

    /// <summary>
    /// 获取产量报表
    /// </summary>
    public async Task<ApiResponse<DailyProductionReportResponse>> GetDailyProductionReportAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            var url = $"{ApiEndpoints.ReportDailyOutput}?fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}";
            var response = await _http.GetFromJsonAsync<ApiResponse<DailyProductionReportResponse>>(url);
            return response ?? ApiResponse<DailyProductionReportResponse>.Fail("获取报表数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<DailyProductionReportResponse>.Fail($"网络错误: {ex.Message}");
        }
    }
}
