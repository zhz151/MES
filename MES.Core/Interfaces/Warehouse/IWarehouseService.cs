using MES.Core.Models;

using MES.Core.DTOs.Warehouse;
namespace MES.Core.Interfaces.Warehouse;

public interface IWarehouseService
{
    Task<PagedResult<WarehouseDto>> GetPagedAsync(QueryParams query, bool? isActive = null);
    Task<List<WarehouseDto>> GetAllAsync(bool onlyActive = true);
    Task<WarehouseDto> GetByIdAsync(int id);
    Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request);
    Task<WarehouseDto> UpdateAsync(int id, UpdateWarehouseRequest request);
    Task DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
