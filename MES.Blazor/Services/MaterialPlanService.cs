using MES.Core.DTOs;
using MES.Shared.Constants;
using MES.Core.Enums;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 用料计划前端服务
/// </summary>
public class MaterialPlanService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.MaterialPlan;

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

    public async Task<ApiResponse<PurchaseSemiPlanDto>> UpdateSemiPlanAsync(int id, CreatePurchaseSemiPlanRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<CreatePurchaseSemiPlanRequest, ApiResponse<PurchaseSemiPlanDto>>($"{BaseUrl}/semi/{id}", request);
            return response ?? ApiResponse<PurchaseSemiPlanDto>.Fail("保存失败");
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

    public async Task<ApiResponse<PurchaseFinishedPlanDto>> UpdateFinishedPlanAsync(int id, CreatePurchaseFinishedPlanRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<CreatePurchaseFinishedPlanRequest, ApiResponse<PurchaseFinishedPlanDto>>($"{BaseUrl}/finished/{id}", request);
            return response ?? ApiResponse<PurchaseFinishedPlanDto>.Fail("更新失败");
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

    public async Task<ApiResponse<List<PurchaseFinishedPlanDto>>> CreateFinishedPlanBatchAsync(List<CreatePurchaseFinishedPlanRequest> requests)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<List<CreatePurchaseFinishedPlanRequest>, ApiResponse<List<PurchaseFinishedPlanDto>>>($"{BaseUrl}/finished/batch", requests);
            return response ?? ApiResponse<List<PurchaseFinishedPlanDto>>.Fail("批量创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<PurchaseFinishedPlanDto>>.Fail($"网络错误: {ex.Message}");
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

    public async Task<ApiResponse<List<AvailableInventoryBatchDto>>> GetAvailableInventoryAsync(int workOrderId, int? excludePlanId = null)
    {
        try
        {
            var url = $"{BaseUrl}/inventory/available/{workOrderId}";
            if (excludePlanId.HasValue)
                url += $"?excludePlanId={excludePlanId.Value}";
            var response = await _http.GetFromJsonAsync<ApiResponse<List<AvailableInventoryBatchDto>>>(url);
            return response ?? ApiResponse<List<AvailableInventoryBatchDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<AvailableInventoryBatchDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<AvailableInventoryBatchDto>>> GetAvailableReworkInventoryAsync(int workOrderId, ReworkType reworkType, int? excludePlanId = null)
    {
        try
        {
            var url = $"{BaseUrl}/rework-inventory/{workOrderId}?reworkType={reworkType}";
            if (excludePlanId.HasValue)
                url += $"&excludePlanId={excludePlanId.Value}";
            var response = await _http.GetFromJsonAsync<ApiResponse<List<AvailableInventoryBatchDto>>>(url);
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

    public async Task<ApiResponse<List<InventoryPlanDto>>> CreateInventoryPlanBatchAsync(List<CreateInventoryPlanRequest> requests)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<List<CreateInventoryPlanRequest>, ApiResponse<List<InventoryPlanDto>>>($"{BaseUrl}/inventory/batch", requests);
            return response ?? ApiResponse<List<InventoryPlanDto>>.Fail("批量创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<InventoryPlanDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<InventoryPlanDto>> GetInventoryPlanByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<InventoryPlanDto>>($"{BaseUrl}/inventory/plan/{id}");
            return response ?? ApiResponse<InventoryPlanDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<InventoryPlanDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<InventoryPlanDto>> UpdateInventoryPlanAsync(int id, CreateInventoryPlanRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<CreateInventoryPlanRequest, ApiResponse<InventoryPlanDto>>($"{BaseUrl}/inventory/{id}", request);
            return response ?? ApiResponse<InventoryPlanDto>.Fail("更新失败");
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

    #region 圆棒穿孔计划

    public async Task<ApiResponse<List<RoundBarPiercingPlanDto>>> GetPiercingPlansAsync(int workOrderId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<RoundBarPiercingPlanDto>>>($"{BaseUrl}/piercing/{workOrderId}");
            return response ?? ApiResponse<List<RoundBarPiercingPlanDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<RoundBarPiercingPlanDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<RoundBarPiercingPlanDto>> GetPiercingPlanByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<RoundBarPiercingPlanDto>>($"{BaseUrl}/piercing/detail/{id}");
            return response ?? ApiResponse<RoundBarPiercingPlanDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<RoundBarPiercingPlanDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<RoundBarPiercingPlanDto>> CreatePiercingPlanAsync(CreateRoundBarPiercingPlanRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreateRoundBarPiercingPlanRequest, ApiResponse<RoundBarPiercingPlanDto>>($"{BaseUrl}/piercing", request);
            return response ?? ApiResponse<RoundBarPiercingPlanDto>.Fail("创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<RoundBarPiercingPlanDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<RoundBarPiercingPlanDto>> UpdatePiercingPlanAsync(int id, UpdateRoundBarPiercingPlanRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<UpdateRoundBarPiercingPlanRequest, ApiResponse<RoundBarPiercingPlanDto>>($"{BaseUrl}/piercing/{id}", request);
            return response ?? ApiResponse<RoundBarPiercingPlanDto>.Fail("更新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<RoundBarPiercingPlanDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> DeletePiercingPlanAsync(int id)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse>($"{BaseUrl}/piercing/{id}");
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

    public async Task<ApiResponse<string>> PrintPiercingPlanAsync(int planId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/print/piercing/{planId}");
            return response ?? ApiResponse<string>.Fail("打印生成失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<string>> PrintSelectedPlansAsync(MaterialPlanBatchPrintRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<MaterialPlanBatchPrintRequest, ApiResponse<string>>($"{BaseUrl}/print/batch", request);
            return response ?? ApiResponse<string>.Fail("打印生成失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }

    #endregion
}
