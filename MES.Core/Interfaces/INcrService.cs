using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// NCR 不合格品报告服务接口
/// </summary>
public interface INcrService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<NcrDto>> GetAllAsync(QueryParams query);

    /// <summary>获取全部（无分页）</summary>
    Task<List<NcrDto>> GetAllListAsync();

    /// <summary>获取详情</summary>
    Task<NcrDto?> GetByIdAsync(int id);

    /// <summary>创建</summary>
    Task<NcrDto> CreateAsync(CreateNcrRequest request);

    /// <summary>更新</summary>
    Task<NcrDto> UpdateAsync(int id, UpdateNcrRequest request);

    /// <summary>删除</summary>
    Task DeleteAsync(int id);

    /// <summary>状态变更</summary>
    Task<NcrDto> UpdateStatusAsync(int id, UpdateNcrStatusRequest request);

    /// <summary>根据生产编号调取批次信息（用于新建页自动填充）</summary>
    Task<NcrLookupResultDto?> LookupBatchAsync(string batchNo);

    /// <summary>获取筛选上下文</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>获取待处理批次卡片数据（分析过程检验+成品检验）</summary>
    Task<List<NcrPendingCheckDto>> GetPendingChecksAsync();
}
