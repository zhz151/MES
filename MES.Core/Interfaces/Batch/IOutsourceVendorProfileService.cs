using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Shared;
using MES.Core.Models;

namespace MES.Core.Interfaces.Batch;

/// <summary>
/// 委外单位档案服务（工段委外单位主档，仿供应商档案 SupplierService）
/// </summary>
public interface IOutsourceVendorProfileService
{
    Task<PagedResult<OutsourceVendorProfileDto>> GetPagedAsync(QueryParams query);
    Task<OutsourceVendorProfileDto> GetByIdAsync(int id);
    Task<List<OutsourceVendorProfileDto>> GetActiveAsync();
    Task<OutsourceVendorProfileDto> CreateAsync(CreateOutsourceVendorRequest request);
    Task<List<OutsourceVendorProfileDto>> CreateBatchAsync(List<CreateOutsourceVendorRequest> requests);
    Task<OutsourceVendorProfileDto> UpdateAsync(int id, UpdateOutsourceVendorRequest request);
    Task DeleteAsync(int id);

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>
    /// 列表打印（Mode A）：按当前可见列渲染列表 PDF（前端已把 ② 往来信息 统计列转文本带出）
    /// </summary>
    Task<byte[]> PrintOutsourceVendorListAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns);
}
