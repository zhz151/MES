using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IInspectionRecordService
{
    Task<PagedResult<InspectionRecordListDto>> GetPagedAsync(InspectionRecordQueryParams query);
    Task<InspectionRecordListDto> GetByIdAsync(int id);
    Task<InspectionRecordListDto> CreateAsync(CreateInspectionRecordRequest request);
    Task<List<InspectionRecordListDto>> CreateBatchAsync(List<CreateInspectionRecordRequest> requests);
    Task<InspectionRecordListDto> UpdateAsync(int id, UpdateInspectionRequest request);
    Task DeleteAsync(int id);
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(InspectionRecordQueryParams query, List<PrintColumnDef> columns);
}
