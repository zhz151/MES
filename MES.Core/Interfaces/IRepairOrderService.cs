using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IRepairOrderService
{
    Task<PagedResult<RepairOrderListDto>> GetPagedAsync(RepairOrderQueryParams query);
    Task<RepairOrderListDto> GetByIdAsync(int id);
    Task<RepairOrderListDto> CreateAsync(CreateRepairOrderRequest request);
    Task<List<RepairOrderListDto>> CreateBatchAsync(List<CreateRepairOrderRequest> requests);
    Task<RepairOrderListDto> UpdateAsync(int id, UpdateRepairOrderRequest request);
    Task DeleteAsync(int id);
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(RepairOrderQueryParams query, List<PrintColumnDef> columns);
}
