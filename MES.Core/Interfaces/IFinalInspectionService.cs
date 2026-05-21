using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 成品检验服务接口
/// </summary>
public interface IFinalInspectionService
{
    /// <summary>
    /// 分页查询所有成品检验记录
    /// </summary>
    Task<PagedResult<FinalInspectionDto>> GetAllAsync(QueryParams query);

    /// <summary>
    /// 获取所有成品检验记录（无分页）
    /// </summary>
    Task<List<FinalInspectionDto>> GetAllListAsync();

    /// <summary>
    /// 获取成品检验详情
    /// </summary>
    Task<FinalInspectionDto?> GetByIdAsync(int id);

    /// <summary>
    /// 创建成品检验记录
    /// </summary>
    Task<FinalInspectionDto> CreateAsync(CreateFinalInspectionRequest request);

    /// <summary>
    /// 更新成品检验记录
    /// </summary>
    Task<FinalInspectionDto> UpdateAsync(int id, UpdateFinalInspectionRequest request);

    /// <summary>
    /// 删除成品检验记录
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// 批量创建成品检验记录
    /// </summary>
    Task<List<FinalInspectionDto>> BatchCreateAsync(List<CreateFinalInspectionRequest> requests);

    /// <summary>
    /// 根据生产编号调取批次信息（用于新建页自动填充）
    /// </summary>
    Task<BatchLookupResultDto?> LookupBatchAsync(string batchNo);
}
