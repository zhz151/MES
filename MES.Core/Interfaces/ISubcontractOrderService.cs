using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface ISubcontractOrderService
{
    Task<PagedResult<SubcontractOrderDto>> GetPagedAsync(SubcontractQueryParams query);
    Task<SubcontractOrderDto> GetByIdAsync(int id);
    Task<SubcontractOrderDto> CreateAsync(CreateSubcontractOrderRequest request);
    Task<SubcontractOrderDto> UpdateAsync(int id, UpdateSubcontractOrderRequest request);
    Task SyncAllAsync();
    Task SyncSingleAsync(int id);
    Task UpdateStatusAsync(int id, UpdateOrderStatusRequest request);
    Task DeleteAsync(int id);
}
