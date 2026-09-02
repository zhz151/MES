using System.Text.Json;
using MES.Core.DTOs.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services.Payroll;

/// <summary>
/// 成检计件类别（2026-09-03 引入）前端服务：类别 = 成检项目(InspectionItem 单选) + 基准价 + 结算单位。
/// 接口与生产计件类别（PieceRateProductionCategoryService）镜像：分页/详情/保存/删除/选项/试算。
/// </summary>
public class PieceRateFinalInspectionCategoryService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.PieceRateFinalInspectionCategory;

    public PieceRateFinalInspectionCategoryService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<PieceRateFinalInspectionCategoryListItemDto>>> GetPagedAsync(
        PieceRateFinalInspectionCategoryQueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrWhiteSpace(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (!string.IsNullOrWhiteSpace(query.ItemKey))
                url += $"&itemKey={Uri.EscapeDataString(query.ItemKey)}";
            if (!string.IsNullOrWhiteSpace(query.Unit))
                url += $"&unit={Uri.EscapeDataString(query.Unit)}";
            if (query.IsActive.HasValue)
                url += $"&isActive={query.IsActive.Value}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<PieceRateFinalInspectionCategoryListItemDto>>>(url);
            return response ?? ApiResponse<PagedResult<PieceRateFinalInspectionCategoryListItemDto>>.Fail("获取类别失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<PieceRateFinalInspectionCategoryListItemDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PieceRateFinalInspectionCategoryDetailDto?>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<PieceRateFinalInspectionCategoryDetailDto?>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<PieceRateFinalInspectionCategoryDetailDto?>.Fail("获取类别失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PieceRateFinalInspectionCategoryDetailDto?>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PieceRateFinalInspectionCategoryDetailDto?>> SaveAsync(
        int? id, PieceRateFinalInspectionCategorySaveRequest request)
    {
        try
        {
            var response = id.HasValue
                ? await _http.PutAsJsonAsync<PieceRateFinalInspectionCategorySaveRequest, ApiResponse<PieceRateFinalInspectionCategoryDetailDto?>>($"{BaseUrl}/{id}", request)
                : await _http.PostAsJsonAsync<PieceRateFinalInspectionCategorySaveRequest, ApiResponse<PieceRateFinalInspectionCategoryDetailDto?>>(BaseUrl, request);
            return response ?? ApiResponse<PieceRateFinalInspectionCategoryDetailDto?>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PieceRateFinalInspectionCategoryDetailDto?>.Fail($"网络错误: {ex.Message}");
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

    public async Task<ApiResponse<PieceRateFinalInspectionCategoryOptionsDto>> GetOptionsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<PieceRateFinalInspectionCategoryOptionsDto>>(BaseUrl + "/options");
            return response ?? ApiResponse<PieceRateFinalInspectionCategoryOptionsDto>.Fail("获取选项失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PieceRateFinalInspectionCategoryOptionsDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PieceRateFinalInspectionMatchResultDto?>> MatchPriceAsync(PieceRateFinalInspectionMatchRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<PieceRateFinalInspectionMatchRequest, ApiResponse<PieceRateFinalInspectionMatchResultDto?>>(BaseUrl + "/match-price", request);
            return response ?? ApiResponse<PieceRateFinalInspectionMatchResultDto?>.Fail("试算失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PieceRateFinalInspectionMatchResultDto?>.Fail($"网络错误: {ex.Message}");
        }
    }
}
