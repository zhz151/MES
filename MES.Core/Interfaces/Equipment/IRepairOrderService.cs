using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Equipment;
namespace MES.Core.Interfaces.Equipment;

public interface IRepairOrderService
{
    Task<PagedResult<RepairOrderListDto>> GetPagedAsync(RepairOrderQueryParams query);
    Task<List<RepairOrderListDto>> GetAllListAsync();
    Task<RepairOrderListDto> GetByIdAsync(int id);
    Task<RepairOrderListDto> CreateAsync(CreateRepairOrderRequest request);
    Task<List<RepairOrderListDto>> CreateBatchAsync(List<CreateRepairOrderRequest> requests);
    Task<RepairOrderListDto> UpdateAsync(int id, UpdateRepairOrderRequest request);
    Task DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>获取指定设备的待处理维修工单（Pending/InProgress�?/summary>
    Task<List<RepairOrderListDto>> GetPendingByEquipmentAsync(int equipmentId);

    /// <summary>开始维修（设置维修人和开始时间）</summary>
    Task<RepairOrderListDto> StartRepairAsync(int id, StartRepairRequest request);

    /// <summary>完成维修（填写维修内容、备件、结束时间）</summary>
    Task<RepairOrderListDto> CompleteRepairAsync(int id, CompleteRepairRequest request);
}
