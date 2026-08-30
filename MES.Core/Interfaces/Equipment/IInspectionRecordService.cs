using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Equipment;
namespace MES.Core.Interfaces.Equipment;

public interface IInspectionRecordService
{
    Task<PagedResult<InspectionRecordListDto>> GetPagedAsync(InspectionRecordQueryParams query);
    Task<List<InspectionRecordListDto>> GetAllListAsync();
    Task<InspectionRecordListDto?> GetByIdAsync(int id);
    Task<InspectionRecordListDto> CreateAsync(CreateInspectionRecordRequest request);
    Task<List<InspectionRecordListDto>> CreateBatchAsync(List<CreateInspectionRecordRequest> requests);
    Task<InspectionRecordListDto> UpdateAsync(int id, UpdateInspectionRequest request);
    Task DeleteAsync(int id);
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>
    /// 获取筛选上下文（各列去重值），用�?ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
