using System.Text.Json;
using System.Text.Json.Serialization;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Quality;

namespace MES.Blazor.Services;

public class QualityProcessTrackingService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.QualityProcessTracking;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public QualityProcessTrackingService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<QualityProcessTrackingDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.ReceiveDateFrom.HasValue)
                url += $"&receiveDateFrom={query.ReceiveDateFrom.Value:yyyy-MM-dd}";
            if (query.ReceiveDateTo.HasValue)
                url += $"&receiveDateTo={query.ReceiveDateTo.Value:yyyy-MM-dd}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters, _jsonOptions))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<QualityProcessTrackingDto>>>(url);
            return response ?? ApiResponse<PagedResult<QualityProcessTrackingDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<QualityProcessTrackingDto>>.Fail($"网络错误: {ex.Message}");
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
