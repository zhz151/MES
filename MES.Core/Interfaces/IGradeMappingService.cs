using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IGradeMappingService
{
    // 分页查询（用于 ServerData 模式）
    Task<PagedResult<StandardGradeMappingDto>> GetPagedAsync(QueryParams query);
    
    // 获取所有（用于下拉框）
    Task<List<StandardGradeMappingDto>> GetAllAsync();
    
    // 根据 ID 获取详情
    Task<StandardGradeMappingDto> GetByIdAsync(int id);
    
    // 创建
    Task<StandardGradeMappingDto> CreateAsync(CreateGradeMappingRequest request);
    
    // 更新
    Task<StandardGradeMappingDto> UpdateAsync(int id, UpdateGradeMappingRequest request);
    
    // 删除
    Task DeleteAsync(int id);
}