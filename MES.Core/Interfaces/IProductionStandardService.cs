using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IProductionStandardService
{
    // 分页查询（用于 ServerData 模式）
    Task<PagedResult<ProductionStandardDto>> GetPagedAsync(QueryParams query);
    
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
}