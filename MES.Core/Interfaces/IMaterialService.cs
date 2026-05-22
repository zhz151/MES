using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IMaterialService
{
    Task<PagedResult<MaterialDto>> GetPagedAsync(QueryParams query);
    Task<List<MaterialDto>> GetAllListAsync();
    Task<MaterialDto> GetByIdAsync(int id);
    Task<List<MaterialDto>> GetActiveAsync();
    Task<List<string>> GetCategoriesAsync();
    Task<MaterialDto?> MatchAsync(string category, string grade, string spec);

    /// <summary>
    /// 批量匹配物料，返回不存在的物料列表
    /// </summary>
    Task<List<BatchMaterialMatchItem>> BatchMatchAsync(List<BatchMaterialMatchItem> items);
    Task<MaterialDto> CreateAsync(CreateMaterialRequest request);
    Task<List<MaterialDto>> CreateBatchAsync(List<CreateMaterialRequest> requests);
    Task<MaterialDto> UpdateAsync(int id, UpdateMaterialRequest request);
    Task DeleteAsync(int id);

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    // ========== 打印 ==========
    Task<byte[]> PrintMaterialAsync(int id);
    Task<byte[]> PrintMaterialBatchAsync(int[] ids);
    Task<byte[]> PrintMaterialAllAsync(string? keyword, string? sortBy = null, bool isDescending = false);
}
