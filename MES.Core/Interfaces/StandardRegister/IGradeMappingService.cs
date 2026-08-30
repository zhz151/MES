using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.StandardRegister;
namespace MES.Core.Interfaces.StandardRegister;

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

    // ========== 打印 ==========
    Task<byte[]> PrintGradeMappingBatchAsync(int[] ids, List<PrintColumnDef>? columns = null);

    // ========== 筛选上下文 ==========
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
