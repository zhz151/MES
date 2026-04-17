// 文件路径: MES.Blazor/Services/ProductionStandardService.cs
using System.Net.Http.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class ProductionStandardService : IProductionStandardService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "api/standard";

    public ProductionStandardService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ProductionStandardDto>> GetAllAsync(bool onlyActive = true)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<ProductionStandardDto>>>(
                $"{BaseUrl}/list?onlyActive={onlyActive}");

            if (response != null && response.Success)
            {
                return response.Data ?? new List<ProductionStandardDto>();
            }
            return new List<ProductionStandardDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllAsync error: {ex.Message}");
            return new List<ProductionStandardDto>();
        }
    }

    public async Task<ApiResponse<ProductionStandardDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<ProductionStandardDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<ProductionStandardDto>.Fail("Failed to get data");
        }
        catch (Exception ex)
        {
            return ApiResponse<ProductionStandardDto>.Fail($"Network error: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ProductionStandardDto>> CreateAsync(CreateProductionStandardRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(BaseUrl, request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductionStandardDto>>();
            return result ?? ApiResponse<ProductionStandardDto>.Fail("Create failed");
        }
        catch (Exception ex)
        {
            return ApiResponse<ProductionStandardDto>.Fail($"Network error: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ProductionStandardDto>> UpdateAsync(int id, UpdateProductionStandardRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"{BaseUrl}/{id}", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductionStandardDto>>();
            return result ?? ApiResponse<ProductionStandardDto>.Fail("Update failed");
        }
        catch (Exception ex)
        {
            return ApiResponse<ProductionStandardDto>.Fail($"Network error: {ex.Message}");
        }
    }

    // 修改：返回类型改为 Task<ApiResponse<object>>
    public async Task<ApiResponse<object>> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.DeleteAsync($"{BaseUrl}/{id}");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            return result ?? ApiResponse<object>.Fail("Delete failed");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"Network error: {ex.Message}");
        }
    }
}