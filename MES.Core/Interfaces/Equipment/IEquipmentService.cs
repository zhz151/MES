using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Equipment;
namespace MES.Core.Interfaces.Equipment;

public interface IEquipmentService
{
    Task<PagedResult<EquipmentListDto>> GetPagedAsync(EquipmentQueryParams query);
    Task<EquipmentDetailDto> GetByIdAsync(int id);
    Task<List<EquipmentListDto>> GetAllAsync();

    /// <summary>设备下拉选项（精简字段，供维修工单建单等「仅需登录」页面使用）</summary>
    Task<List<EquipmentOptionDto>> GetOptionsAsync();
    Task<EquipmentDetailDto> CreateAsync(CreateEquipmentRequest request);
    Task<EquipmentDetailDto> UpdateAsync(int id, UpdateEquipmentRequest request);
    Task DeleteAsync(int id);
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
