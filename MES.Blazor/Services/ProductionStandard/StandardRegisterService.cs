using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.ProductionStandard;

namespace MES.Blazor.Services;

public class StandardRegisterService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.StandardRegister;

    public StandardRegisterService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<StandardRegisterDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}" +
                      $"&sortBy={query.SortBy}&isDescending={query.IsDescending}";
            if (!string.IsNullOrWhiteSpace(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<StandardRegisterDto>>>(url)
                   ?? ApiResponse<PagedResult<StandardRegisterDto>>.Fail("获取列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<StandardRegisterDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<StandardRegisterDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<StandardRegisterDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<StandardRegisterDto>.Fail("获取详情失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardRegisterDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveAsync(StandardRegisterDto dto)
    {
        try
        {
            return await _http.PostAsJsonAsync<StandardRegisterDto, ApiResponse<bool>>($"{BaseUrl}/save", dto)
                   ?? ApiResponse<bool>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<StandardRegisterDto>>> GetAllAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<StandardRegisterDto>>>($"{BaseUrl}/all")
                   ?? ApiResponse<List<StandardRegisterDto>>.Fail("获取全部标准号失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<StandardRegisterDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts")
                   ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        try
        {
            return await _http.PostAsJsonAsync<object?, ApiResponse<bool>>($"{BaseUrl}/delete/{id}", null)
                   ?? ApiResponse<bool>.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<StandardRegisterItemDto>>> GetItemsAsync(int standardRegisterId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<StandardRegisterItemDto>>>($"{BaseUrl}/{standardRegisterId}/items")
                   ?? ApiResponse<List<StandardRegisterItemDto>>.Fail("获取子项目失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<StandardRegisterItemDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveItemAsync(StandardRegisterItemDto dto)
    {
        try
        {
            return await _http.PostAsJsonAsync<StandardRegisterItemDto, ApiResponse<bool>>($"{BaseUrl}/item/save", dto)
                   ?? ApiResponse<bool>.Fail("保存子项目失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeleteItemAsync(int id)
    {
        try
        {
            return await _http.PostAsJsonAsync<object?, ApiResponse<bool>>($"{BaseUrl}/item/delete/{id}", null)
                   ?? ApiResponse<bool>.Fail("删除子项目失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }
}
