using MES.Core.Models;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Shared;
namespace MES.Core.Interfaces.Materials;

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderDto>> GetPagedAsync(PurchaseOrderQueryParams query);
    Task<List<PurchaseOrderDto>> GetAllListAsync();
    Task<PurchaseOrderDto> GetByIdAsync(int id);
    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request);
    Task<List<PurchaseOrderDto>> CreateBatchAsync(List<CreatePurchaseOrderRequest> requests);
    Task<PurchaseOrderDto> UpdateAsync(int id, UpdatePurchaseOrderRequest request, bool isAdmin = false);
    Task SyncAllAsync();
    Task SyncSingleAsync(int id);
    Task UpdateStatusAsync(int id, UpdateOrderStatusRequest request);
    Task DeleteAsync(int id, bool isAdmin = false);
    Task<List<ProcurementStatusDto>> GetProcurementStatusAsync();
    Task<List<ProcurementStatusDto>> GetPiercingProcurementStatusAsync();
    Task<List<OrderMismatchInfo>> GetMismatchedPurchaseOrdersAsync();
    Task<PlanDetailDto?> GetPlanDetailAsync(string workOrderNo, string materialCategory);

    // ========== 筛选上下文 ==========
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    // ========== 打印 ==========
    Task<byte[]> PrintOrderAsync(int id, List<PrintColumnDef>? columns = null);
    Task<byte[]> PrintOrderBatchAsync(int[] ids, List<PrintColumnDef>? columns = null);
    Task<byte[]> PrintOrderAllAsync(string? keyword, string? sortBy = null, bool isDescending = false, DateTime? dateFrom = null, DateTime? dateTo = null, List<PrintColumnDef>? columns = null);
}
