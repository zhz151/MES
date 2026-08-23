using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

/// <summary>
/// NCR 不合格品报告服务接口
/// </summary>
public interface INcrService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<NcrDto>> GetAllAsync(QueryParams query);

    /// <summary>获取全部（无分页�?/summary>
    Task<List<NcrDto>> GetAllListAsync();

    /// <summary>获取详情</summary>
    Task<NcrDto?> GetByIdAsync(int id);

    /// <summary>创建</summary>
    Task<NcrDto> CreateAsync(CreateNcrRequest request);

    /// <summary>更新</summary>
    Task<NcrDto> UpdateAsync(int id, UpdateNcrRequest request);

    /// <summary>删除</summary>
    Task DeleteAsync(int id);

    /// <summary>状态变�?/summary>
    Task<NcrDto> UpdateStatusAsync(int id, UpdateNcrStatusRequest request);

    /// <summary>根据生产编号调取批次信息（用于新建页自动填充�?/summary>
    Task<NcrLookupResultDto?> LookupBatchAsync(string batchNo);

    /// <summary>获取筛选上下文</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>获取待处理批次卡片数据（分析过程检�?成品检验）</summary>
    Task<List<NcrPendingCheckDto>> GetPendingChecksAsync();

    /// <summary>获取不合格品月度汇总（责任类别→责任部门→处置方式 三级，12 个月次品支数/重量矩阵）</summary>
    Task<NcrMonthlySummaryDto> GetMonthlySummaryAsync();

    /// <summary>打印选中 NCR（生成 PDF）</summary>
    Task<byte[]> PrintSelectedAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>打印全部 NCR（生成 PDF）</summary>
    Task<byte[]> PrintAllAsync(NcrPrintAllRequest request);
}
