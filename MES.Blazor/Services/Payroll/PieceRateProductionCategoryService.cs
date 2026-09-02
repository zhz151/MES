using System.Text.Json;
using MES.Core.DTOs.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services.Payroll;

/// <summary>
/// 生产计件类别（2026-09-02 两表模型）前端服务。
/// </summary>
public class PieceRateProductionCategoryService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.PieceRateProductionCategory;

    public PieceRateProductionCategoryService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<PieceRateProductionCategoryListItemDto>>> GetPagedAsync(
        PieceRateProductionCategoryQueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrWhiteSpace(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (!string.IsNullOrWhiteSpace(query.SectionKey))
                url += $"&sectionKey={Uri.EscapeDataString(query.SectionKey)}";
            if (!string.IsNullOrWhiteSpace(query.Unit))
                url += $"&unit={Uri.EscapeDataString(query.Unit)}";
            if (query.IsActive.HasValue)
                url += $"&isActive={query.IsActive.Value}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<PieceRateProductionCategoryListItemDto>>>(url);
            return response ?? ApiResponse<PagedResult<PieceRateProductionCategoryListItemDto>>.Fail("获取类别失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<PieceRateProductionCategoryListItemDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PieceRateProductionCategoryDetailDto?>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<PieceRateProductionCategoryDetailDto?>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<PieceRateProductionCategoryDetailDto?>.Fail("获取类别失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PieceRateProductionCategoryDetailDto?>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PieceRateProductionCategoryDetailDto?>> SaveAsync(
        int? id, PieceRateProductionCategorySaveRequest request)
    {
        try
        {
            var response = id.HasValue
                ? await _http.PutAsJsonAsync<PieceRateProductionCategorySaveRequest, ApiResponse<PieceRateProductionCategoryDetailDto?>>($"{BaseUrl}/{id}", request)
                : await _http.PostAsJsonAsync<PieceRateProductionCategorySaveRequest, ApiResponse<PieceRateProductionCategoryDetailDto?>>(BaseUrl, request);
            return response ?? ApiResponse<PieceRateProductionCategoryDetailDto?>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PieceRateProductionCategoryDetailDto?>.Fail($"网络错误: {ex.Message}");
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

    public async Task<ApiResponse<PieceRateProductionCategoryOptionsDto>> GetOptionsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<PieceRateProductionCategoryOptionsDto>>(BaseUrl + "/options");
            return response ?? ApiResponse<PieceRateProductionCategoryOptionsDto>.Fail("获取选项失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PieceRateProductionCategoryOptionsDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PieceRateProductionMatchResultDto?>> MatchPriceAsync(PieceRateProductionMatchRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<PieceRateProductionMatchRequest, ApiResponse<PieceRateProductionMatchResultDto?>>(BaseUrl + "/match-price", request);
            return response ?? ApiResponse<PieceRateProductionMatchResultDto?>.Fail("试算失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PieceRateProductionMatchResultDto?>.Fail($"网络错误: {ex.Message}");
        }
    }
}
