// 文件路径: MES.Blazor/Services/ProductRequirementService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class ProductRequirementService
{
    private readonly AuthHttpClient _http;

    public ProductRequirementService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<ProductRequirementDto>> GetByOrderItemIdAsync(int orderId, int itemId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<ProductRequirementDto>>(
                $"api/order/{orderId}/items/{itemId}/requirement");
            return response ?? ApiResponse<ProductRequirementDto>.Fail("获取产品要求失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ProductRequirementDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ProductRequirementDto>> CreateOrUpdateAsync(
        int orderId,
        int itemId,
        CreateProductRequirementRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreateProductRequirementRequest, ApiResponse<ProductRequirementDto>>(
                $"api/order/{orderId}/items/{itemId}/requirement", request);
            return response ?? ApiResponse<ProductRequirementDto>.Fail("保存产品要求失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ProductRequirementDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<object>> DeleteAsync(int orderId, int itemId)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse<object>>(
                $"api/order/{orderId}/items/{itemId}/requirement");
            return response ?? ApiResponse<object>.Fail("删除产品要求失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取订单下所有项次的产品要求列表
    /// </summary>
    public async Task<ApiResponse<List<ProductRequirementDto>>> GetByOrderIdAsync(int orderId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<ProductRequirementDto>>>(
                $"api/order/{orderId}/requirements");
            return response ?? ApiResponse<List<ProductRequirementDto>>.Fail("获取技术要求列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<ProductRequirementDto>>.Fail($"网络错误: {ex.Message}");
        }
    }
}