// 文件路径: MES.Blazor/Services/ProductRequirementService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 产品要求服务
/// </summary>
public class ProductRequirementService
{
    private readonly AuthHttpClient _http;

    public ProductRequirementService(AuthHttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// 获取订单项次的产品要求
    /// </summary>
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

    /// <summary>
    /// 创建或更新产品要求
    /// </summary>
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

    /// <summary>
    /// 删除产品要求
    /// </summary>
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
}