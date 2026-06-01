using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 订单总况前端服务
/// </summary>
public class ProductionOverviewService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/production-overview";

    public ProductionOverviewService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<ProductionOverviewDto>> GetOverviewAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<ProductionOverviewDto>>($"{BaseUrl}/overview");
            return response ?? ApiResponse<ProductionOverviewDto>.Fail("获取订单总况失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ProductionOverviewDto>.Fail($"网络错误: {ex.Message}");
        }
    }
}
