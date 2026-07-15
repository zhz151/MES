using MES.Core.DTOs.Quality;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>
/// 质量证明书前端服务
/// </summary>
public class CertificateService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Certificate;

    public CertificateService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<CertificateDto>>> GetAllAsync(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        string? sortBy = null,
        bool isDescending = true,
        string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<CertificateDto>>>(url)
                   ?? ApiResponse<PagedResult<CertificateDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<CertificateDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<CertificateDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<CertificateDetailDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<CertificateDetailDto>.Fail("获取详情失败");
        }
        catch (Exception ex) { return ApiResponse<CertificateDetailDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<CertificateDetailDto>> CreateAsync(CertificateCreateRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CertificateCreateRequest, ApiResponse<CertificateDetailDto>>(BaseUrl, request)
                   ?? ApiResponse<CertificateDetailDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<CertificateDetailDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<CertificateDetailDto>> UpdateAsync(int id, CertificateUpdateRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<CertificateUpdateRequest, ApiResponse<CertificateDetailDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<CertificateDetailDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<CertificateDetailDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts")
                   ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex) { return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<CertificateItemDto>>> AutoFillInspectionDataAsync(
        List<AutoFillInspectionItem> items)
    {
        try
        {
            var request = new AutoFillInspectionRequest { Items = items };
            return await _http.PostAsJsonAsync<AutoFillInspectionRequest, ApiResponse<List<CertificateItemDto>>>(
                       $"{BaseUrl}/auto-fill", request)
                   ?? ApiResponse<List<CertificateItemDto>>.Fail("自动填充失败");
        }
        catch (Exception ex) { return ApiResponse<List<CertificateItemDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> GetNextCertificateNoAsync(string orderNo)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/next-no?orderNo={Uri.EscapeDataString(orderNo)}")
                   ?? ApiResponse<string>.Fail("获取编号失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }
}
