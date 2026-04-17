// 文件路径: MES.Blazor/Services/GradeMappingService.cs
using System.Net.Http.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class GradeMappingService : IGradeMappingService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "api/grade-mapping";

    public GradeMappingService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<StandardGradeMappingDto>> GetAllAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<StandardGradeMappingDto>>>($"{BaseUrl}/list");
            if (response != null && response.Success)
            {
                return response.Data ?? new List<StandardGradeMappingDto>();
            }
            return new List<StandardGradeMappingDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllAsync error: {ex.Message}");
            return new List<StandardGradeMappingDto>();
        }
    }

    public async Task<ApiResponse<StandardGradeMappingDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<StandardGradeMappingDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<StandardGradeMappingDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardGradeMappingDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<StandardGradeMappingDto?> GetByStandardGradeAsync(string standardGrade)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(x => x.StandardGrade == standardGrade);
    }

    public async Task<ApiResponse<StandardGradeMappingDto>> CreateAsync(CreateGradeMappingRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(BaseUrl, request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<StandardGradeMappingDto>>();
            return result ?? ApiResponse<StandardGradeMappingDto>.Fail("创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardGradeMappingDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<StandardGradeMappingDto>> UpdateAsync(int id, UpdateGradeMappingRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"{BaseUrl}/{id}", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<StandardGradeMappingDto>>();
            return result ?? ApiResponse<StandardGradeMappingDto>.Fail("更新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardGradeMappingDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    // 修复：返回 ApiResponse（无泛型），与接口保持一致
    public async Task<ApiResponse> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.DeleteAsync($"{BaseUrl}/{id}");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
            return result ?? ApiResponse.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }
}