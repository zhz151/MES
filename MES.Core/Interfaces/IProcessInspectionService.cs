using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 过程检验服务接口
/// </summary>
public interface IProcessInspectionService
{
    /// <summary>
    /// 跨批次查询所有过程检验记录（分页）
    /// </summary>
    Task<PagedResult<ProcessInspectionDto>> GetAllAsync(QueryParams query);

    /// <summary>
    /// 批量创建过程检验记录
    /// </summary>
    Task<List<ProcessInspectionDto>> BatchCreateAsync(List<CreateProcessInspectionRequest> requests);

    /// <summary>
    /// 更新过程检验记录
    /// </summary>
    Task<ProcessInspectionDto> UpdateAsync(int id, UpdateProcessInspectionRequest request);

    /// <summary>
    /// 删除过程检验记录
    /// </summary>
    Task DeleteAsync(int id);
}
