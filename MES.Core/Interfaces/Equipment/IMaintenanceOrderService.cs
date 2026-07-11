using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Equipment;
namespace MES.Core.Interfaces.Equipment;

public interface IMaintenanceOrderService
{
    Task<PagedResult<MaintenanceOrderListDto>> GetPagedAsync(MaintenanceOrderQueryParams query);
    Task<List<MaintenanceOrderListDto>> GetAllListAsync();
    Task<MaintenanceOrderListDto> GetByIdAsync(int id);
    Task<MaintenanceOrderListDto> CreateAsync(CreateMaintenanceOrderRequest request);
    Task<List<MaintenanceOrderListDto>> CreateBatchAsync(List<CreateMaintenanceOrderRequest> requests);
    Task<MaintenanceOrderListDto> UpdateAsync(int id, UpdateMaintenanceRequest request);
    Task DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(MaintenanceOrderQueryParams query, List<PrintColumnDef> columns);
}
