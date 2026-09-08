using MES.Core.DTOs.Order;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 订单进度树客户端（只读）
/// </summary>
public class OrderProgressService
{
    private readonly AuthHttpClient _http;

    public OrderProgressService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<OrderProgressTreeDto?>> GetAsync(string salesOrderNo)
    {
        try
        {
            var url = $"{ApiEndpoints.OrderProgress}?salesOrderNo={Uri.EscapeDataString(salesOrderNo)}";
            var response = await _http.GetFromJsonAsync<ApiResponse<OrderProgressTreeDto?>>(url);
            return response ?? ApiResponse<OrderProgressTreeDto?>.Fail("获取订单进度树失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderProgressTreeDto?>.Fail($"网络错误: {ex.Message}");
        }
    }
}
