using MES.Core.Models;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
namespace MES.Core.Interfaces.Materials;

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

    // ========== 子项执行查询 ==========
    Task<PagedResult<SubcontractReturnItemListDto>> GetReturnItemListAsync(QueryParams query, string? status = null);
    Task<Dictionary<string, List<string>>> GetReturnItemFilterContextsAsync();

    // ========== 圆钢穿孔汇总（按子项聚合） ==========
    Task<List<SubcontractPiercingPendingDto>> GetPiercingPendingAsync();
    Task<SubcontractPiercingInProgressResultDto> GetPiercingInProgressAsync();
    Task<SubcontractPiercingMonthlyResultDto> GetPiercingMonthlyAsync();

    // ========== 筛选上下文 ==========
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    // ========== 打印 ==========
    Task<byte[]> PrintReturnItemSelectedAsync(int[] ids, List<PrintColumnDef>? columns);
    Task<byte[]> PrintOrderAsync(int id);
    Task<byte[]> PrintOrderBatchAsync(int[] ids);
    Task<byte[]> PrintSubcontractOrderListAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns);
    Task<byte[]> PrintOrderAllAsync(string? keyword, string? sortBy = null, bool isDescending = false, DateTime? dateFrom = null, DateTime? dateTo = null);
}
