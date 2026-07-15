using System.Text.Json;
using MES.Core.DTOs.Warehouse;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

public class PendingDeliveryService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.PendingDelivery;

    public PendingDeliveryService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<List<PendingDeliveryItemDto>>> GetPendingItemsAsync(
        string? orderNo = null,
        string? productStandard = null,
        string? deliveryStatus = null)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(orderNo))
                queryParams.Add($"orderNo={Uri.EscapeDataString(orderNo)}");
            if (!string.IsNullOrEmpty(productStandard))
                queryParams.Add($"productStandard={Uri.EscapeDataString(productStandard)}");
            if (!string.IsNullOrEmpty(deliveryStatus))
                queryParams.Add($"deliveryStatus={Uri.EscapeDataString(deliveryStatus)}");

            var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            var response = await _http.GetFromJsonAsync<ApiResponse<List<PendingDeliveryItemDto>>>(
                $"{BaseUrl}/list{queryString}");
            return response ?? ApiResponse<List<PendingDeliveryItemDto>>.Fail("获取待发货项失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<PendingDeliveryItemDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PagedResult<PendingDeliveryItemDto>>> GetAllAsync(
        QueryParams query,
        DateTime? inboundDateFrom = null,
        DateTime? inboundDateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";

            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            if (inboundDateFrom.HasValue)
                url += $"&inboundDateFrom={inboundDateFrom.Value:yyyy-MM-dd}";
            if (inboundDateTo.HasValue)
                url += $"&inboundDateTo={inboundDateTo.Value:yyyy-MM-dd}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<PendingDeliveryItemDto>>>(url);
            return response ?? ApiResponse<PagedResult<PendingDeliveryItemDto>>.Fail("获取待发货项列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<PendingDeliveryItemDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<CertificateHeaderOptionDto>>> GetHeaderOptionsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<CertificateHeaderOptionDto>>>($"{BaseUrl}/header-options");
            return response ?? ApiResponse<List<CertificateHeaderOptionDto>>.Fail("获取头选项失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<CertificateHeaderOptionDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts");
            return response ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}");
        }
    }
}
