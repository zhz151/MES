using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 用料计划前端服务
/// </summary>
public class MaterialPlanService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/material-plan";

    public MaterialPlanService(AuthHttpClient http)
    {
        _http = http;
    }

    #region 原料采购计划

    public async Task<ApiResponse<List<PurchaseSemiPlanDto>>> GetSemiPlansAsync(int workOrderId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<PurchaseSemiPlanDto>>>($"{BaseUrl}/semi/{workOrderId}");
            return response ?? ApiResponse<List<PurchaseSemiPlanDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<PurchaseSemiPlanDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PurchaseSemiPlanDto>> GetSemiPlanByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<PurchaseSemiPlanDto>>($"{BaseUrl}/semi/detail/{id}");
            return response ?? ApiResponse<PurchaseSemiPlanDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PurchaseSemiPlanDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PurchaseSemiPlanDto>> CreateSemiPlanAsync(CreatePurchaseSemiPlanRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreatePurchaseSemiPlanRequest, ApiResponse<PurchaseSemiPlanDto>>($"{BaseUrl}/semi", request);
            return response ?? ApiResponse<PurchaseSemiPlanDto>.Fail("创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PurchaseSemiPlanDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> DeleteSemiPlanAsync(int id)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse>($"{BaseUrl}/semi/{id}");
            return response ?? ApiResponse.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    #endregion

    #region 成品采购计划

    public async Task<ApiResponse<List<PurchaseFinishedPlanDto>>> GetFinishedPlansAsync(int workOrderId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<PurchaseFinishedPlanDto>>>($"{BaseUrl}/finished/{workOrderId}");
            return response ?? ApiResponse<List<PurchaseFinishedPlanDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<PurchaseFinishedPlanDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PurchaseFinishedPlanDto>> GetFinishedPlanByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<PurchaseFinishedPlanDto>>($"{BaseUrl}/finished/detail/{id}");
            return response ?? ApiResponse<PurchaseFinishedPlanDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PurchaseFinishedPlanDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PurchaseFinishedPlanDto>> CreateFinishedPlanAsync(CreatePurchaseFinishedPlanRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreatePurchaseFinishedPlanRequest, ApiResponse<PurchaseFinishedPlanDto>>($"{BaseUrl}/finished", request);
            return response ?? ApiResponse<PurchaseFinishedPlanDto>.Fail("创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PurchaseFinishedPlanDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> DeleteFinishedPlanAsync(int id)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse>($"{BaseUrl}/finished/{id}");
            return response ?? ApiResponse.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    #endregion

    #region 库存使用计划

    public async Task<ApiResponse<List<InventoryPlanDto>>> GetInventoryPlansAsync(int workOrderId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<InventoryPlanDto>>>($"{BaseUrl}/inventory/{workOrderId}");
            return response ?? ApiResponse<List<InventoryPlanDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<InventoryPlanDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<InventoryPlanDto>>> GetReworkPlansAsync(int workOrderId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<InventoryPlanDto>>>($"{BaseUrl}/rework/{workOrderId}");
            return response ?? ApiResponse<List<InventoryPlanDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<InventoryPlanDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<AvailableInventoryBatchDto>>> GetAvailableInventoryAsync(int workOrderId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<AvailableInventoryBatchDto>>>($"{BaseUrl}/inventory/available/{workOrderId}");
            return response ?? ApiResponse<List<AvailableInventoryBatchDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<AvailableInventoryBatchDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<AvailableInventoryBatchDto>>> GetAvailableReworkInventoryAsync(int workOrderId, string reworkType)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<AvailableInventoryBatchDto>>>($"{BaseUrl}/rework-inventory/{workOrderId}?reworkType={reworkType}");
            return response ?? ApiResponse<List<AvailableInventoryBatchDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<AvailableInventoryBatchDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<InventoryPlanDto>> CreateInventoryPlanAsync(CreateInventoryPlanRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreateInventoryPlanRequest, ApiResponse<InventoryPlanDto>>($"{BaseUrl}/inventory", request);
            return response ?? ApiResponse<InventoryPlanDto>.Fail("创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<InventoryPlanDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> DeleteInventoryPlanAsync(int id)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse>($"{BaseUrl}/inventory/{id}");
            return response ?? ApiResponse.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    #endregion

    #region 测算

    public async Task<ApiResponse<MaterialCalculateResult>> CalculateAsync(MaterialCalculateRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<MaterialCalculateRequest, ApiResponse<MaterialCalculateResult>>($"{BaseUrl}/calculate", request);
            return response ?? ApiResponse<MaterialCalculateResult>.Fail("测算失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<MaterialCalculateResult>.Fail($"网络错误: {ex.Message}");
        }
    }

    #endregion

    #region 状态

    public async Task<ApiResponse<WorkOrderMaterialPlanDto>> GetSummaryAsync(int workOrderId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<WorkOrderMaterialPlanDto>>($"{BaseUrl}/summary/{workOrderId}");
            return response ?? ApiResponse<WorkOrderMaterialPlanDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<WorkOrderMaterialPlanDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> RefreshStatusAsync(int workOrderId)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object, ApiResponse>($"{BaseUrl}/refresh-status/{workOrderId}", new { });
            return response ?? ApiResponse.Fail("刷新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    #endregion

    #region 打印

    public async Task<ApiResponse<string>> PrintSemiPlanAsync(int planId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/print/semi/{planId}");
            return response ?? ApiResponse<string>.Fail("打印生成失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<string>> PrintFinishedPlanAsync(int planId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/print/finished/{planId}");
            return response ?? ApiResponse<string>.Fail("打印生成失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<string>> PrintInventoryPlanAsync(int planId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/print/inventory/{planId}");
            return response ?? ApiResponse<string>.Fail("打印生成失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<string>> PrintReworkPlanAsync(int planId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/print/rework/{planId}");
            return response ?? ApiResponse<string>.Fail("打印生成失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }

    #endregion
}
