using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IProductionStandardService
{
    // 分页查询（用于 ServerData 模式）
    Task<PagedResult<ProductionStandardDto>> GetPagedAsync(QueryParams query, bool? isActive = null);
    
    // 获取所有（用于下拉框）
    Task<List<ProductionStandardDto>> GetAllAsync(bool onlyActive = true);
    
    // 根据 ID 获取详情
    Task<ProductionStandardDto> GetByIdAsync(int id);
    
    // 创建
    Task<ProductionStandardDto> CreateAsync(CreateProductionStandardRequest request);
    
    // 更新
    Task<ProductionStandardDto> UpdateAsync(int id, UpdateProductionStandardRequest request);
    
    // 删除
    Task DeleteAsync(int id);

    // ========== 打印 ==========
    Task<byte[]> PrintStandardAsync(int id);
    Task<byte[]> PrintStandardBatchAsync(int[] ids);
    Task<byte[]> PrintStandardAllAsync(string? keyword, bool? isActive, string? sortBy = null, bool isDescending = false);

    // ========== 筛选上下文 ==========
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}