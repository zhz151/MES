using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface ISubcontractOrderService
{
    Task<PagedResult<SubcontractOrderDto>> GetPagedAsync(SubcontractQueryParams query);
    Task<List<SubcontractOrderDto>> GetAllListAsync();
    Task<SubcontractOrderDto> GetByIdAsync(int id);
    Task<SubcontractOrderDto> CreateAsync(CreateSubcontractOrderRequest request);
    Task<SubcontractOrderDto> UpdateAsync(int id, UpdateSubcontractOrderRequest request);
    Task SyncAllAsync();
    Task SyncSingleAsync(int id);
    Task UpdateStatusAsync(int id, UpdateOrderStatusRequest request);
    Task DeleteAsync(int id);

    // ========== 用料计划执行状态 ==========
    Task<List<ProcurementStatusDto>> GetProcurementStatusAsync();
    Task<List<OrderMismatchInfo>> GetMismatchedSubcontractOrdersAsync();
    Task<PlanDetailDto?> GetPlanDetailAsync(string workOrderNo, string materialCategory);

    // ========== 筛选上下文 ==========
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    // ========== 打印 ==========
    Task<byte[]> PrintOrderAsync(int id);
    Task<byte[]> PrintOrderBatchAsync(int[] ids);
    Task<byte[]> PrintOrderAllAsync(string? keyword, string? sortBy = null, bool isDescending = false, DateTime? dateFrom = null, DateTime? dateTo = null);
}
