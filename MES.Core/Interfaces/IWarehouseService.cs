using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IWarehouseService
{
    Task<PagedResult<WarehouseDto>> GetPagedAsync(QueryParams query, bool? isActive = null);
    Task<List<WarehouseDto>> GetAllAsync(bool onlyActive = true);
    Task<WarehouseDto> GetByIdAsync(int id);
    Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request);
    Task<WarehouseDto> UpdateAsync(int id, UpdateWarehouseRequest request);
    Task DeleteAsync(int id);
}
