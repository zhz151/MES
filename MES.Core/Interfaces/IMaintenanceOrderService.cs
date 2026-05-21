using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IMaintenanceOrderService
{
    Task<PagedResult<MaintenanceOrderListDto>> GetPagedAsync(MaintenanceOrderQueryParams query);
    Task<List<MaintenanceOrderListDto>> GetAllListAsync();
    Task<MaintenanceOrderListDto> GetByIdAsync(int id);
    Task<MaintenanceOrderListDto> CreateAsync(CreateMaintenanceOrderRequest request);
    Task<List<MaintenanceOrderListDto>> CreateBatchAsync(List<CreateMaintenanceOrderRequest> requests);
    Task<MaintenanceOrderListDto> UpdateAsync(int id, UpdateMaintenanceRequest request);
    Task DeleteAsync(int id);
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(MaintenanceOrderQueryParams query, List<PrintColumnDef> columns);
}
