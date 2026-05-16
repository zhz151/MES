using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IEquipmentService
{
    Task<PagedResult<EquipmentListDto>> GetPagedAsync(EquipmentQueryParams query);
    Task<EquipmentDetailDto> GetByIdAsync(int id);
    Task<List<EquipmentListDto>> GetAllAsync();
    Task<EquipmentDetailDto> CreateAsync(CreateEquipmentRequest request);
    Task<EquipmentDetailDto> UpdateAsync(int id, UpdateEquipmentRequest request);
    Task DeleteAsync(int id);
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(EquipmentQueryParams query, List<PrintColumnDef> columns);
}
