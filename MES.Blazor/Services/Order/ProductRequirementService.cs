// 文件路径: MES.Blazor/Services/ProductRequirementService.cs
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Order;

namespace MES.Blazor.Services;

public class ProductRequirementService
{
    private readonly AuthHttpClient _http;

    public ProductRequirementService(AuthHttpClient http)
    {
        _http = http;
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
                $"{ApiEndpoints.Order}/{orderId}/items/{itemId}/requirement", request);
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
                $"{ApiEndpoints.Order}/{orderId}/items/{itemId}/requirement");
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
                $"{ApiEndpoints.Order}/{orderId}/requirements");

            if (response != null && response.Success && response.Data == null)
            {
                response.Data = new List<ProductRequirementDto>();
            }

            return response ?? ApiResponse<List<ProductRequirementDto>>.Fail("获取技术要求列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<ProductRequirementDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 按销售订单号 + 工单关联订单项次序号列表（逗号分隔）取质量备注（各项次技术要求「其他要求」按项次号拼接）
    /// </summary>
    public async Task<ApiResponse<string>> GetQualityRemarkByOrderItemIdsAsync(string? salesOrderNo, string? orderItemIds)
    {
        try
        {
            var url = $"{ApiEndpoints.Order}/requirements/quality-remark";
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(salesOrderNo))
                query.Add($"salesOrderNo={Uri.EscapeDataString(salesOrderNo)}");
            if (!string.IsNullOrWhiteSpace(orderItemIds))
                query.Add($"orderItemIds={Uri.EscapeDataString(orderItemIds)}");
            if (query.Count > 0)
                url += "?" + string.Join("&", query);
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>(url);
            return response ?? ApiResponse<string>.Fail("获取质量备注失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 按标准号获取新建技术要求的默认值（工厂检验项要求含"必检"→true）
    /// </summary>
    public async Task<ApiResponse<ProductRequirementDefaultsDto>> GetDefaultsByStandardNoAsync(int orderId, string? standardNo)
    {
        try
        {
            var url = $"{ApiEndpoints.Order}/{orderId}/requirements/defaults";
            if (!string.IsNullOrWhiteSpace(standardNo))
                url += $"?standardNo={Uri.EscapeDataString(standardNo)}";
            var response = await _http.GetFromJsonAsync<ApiResponse<ProductRequirementDefaultsDto>>(url);
            return response ?? ApiResponse<ProductRequirementDefaultsDto>.Fail("获取默认值失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ProductRequirementDefaultsDto>.Fail($"网络错误: {ex.Message}");
        }
    }
}