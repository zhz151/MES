using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Batch;
namespace MES.Core.Interfaces.Batch;

/// <summary>
/// 生产记录服务接口（内部生产记录/工段委外/委外回收）
/// </summary>
public interface IProductionRecordService
{
    // ========== 内部生产记录 ==========

    /// <summary>
    /// 获取批次的生产记录列表
    /// </summary>
    Task<PagedResult<ProductionRecordDto>> GetProductionRecordsAsync(int batchId, QueryParams query);

    /// <summary>
    /// 创建内部生产记录（同步更新批次实时跟踪字段）
    /// </summary>
    Task<ProductionRecordDto> CreateProductionRecordAsync(CreateProductionRecordRequest request);

    /// <summary>
    /// 批量创建内部生产记录
    /// </summary>
    Task<List<ProductionRecordDto>> BatchCreateProductionRecordsAsync(List<CreateProductionRecordRequest> requests);

    /// <summary>
    /// 更新内部生产记录
    /// </summary>
    Task<ProductionRecordDto> UpdateProductionRecordAsync(int id, UpdateProductionRecordRequest request);

    /// <summary>
    /// 删除内部生产记录
    /// </summary>
    Task DeleteProductionRecordAsync(int id);

    // ========== 工段委外 ==========

    Task RefreshBatchTrackingFieldsAsync(int batchId);

    /// <summary>
    /// 批量刷新多个批次的实时跟踪字段（一次查询 + 一次SaveChanges）
    /// </summary>
    Task BatchUpdateBatchTrackingAsync(ICollection<int> batchIds);

    /// <summary>
    /// 重算某批次全部生产记录的定尺切割长度匹配标识（CutLengthMatchType），返回更新条数
    /// 供批次编辑（LengthStatus/工单号等上游字段变更）后级联调用，保持派生列一致
    /// </summary>
    Task<int> RecomputeCutLengthMatchByBatchAsync(int batchId);

    /// <summary>
    /// 获取批次跟踪可视化数据（前端进度图展示用）
    /// </summary>
    Task<BatchTrackingVisualDto> GetTrackingVisualAsync(int batchId);

    // ========== 跨批次查询（用于独立页面） ==========

    /// <summary>
    /// 跨批次查询所有内部生产记录
    /// </summary>
    Task<PagedResult<ProductionRecordDto>> GetAllProductionRecordsAsync(QueryParams query);

    /// <summary>
    /// 跨批次查询所有工段委外记录
    /// </summary>
    Task<PagedResult<SectionOutsourceDto>> GetAllSectionOutsourcesAsync(QueryParams query);

    /// <summary>
    /// 跨批次查询所有委外回收记录
    /// </summary>
    Task<PagedResult<OutsourceRecoveryDto>> GetAllOutsourceRecoveriesAsync(QueryParams query);

    /// <summary>
    /// 获取所有内部生产记录（不含分页，用于 ProductionRecords 页面列表展示）
    /// </summary>
    Task<List<ProductionRecordDto>> GetAllProductionRecordListAsync();

    /// <summary>
    /// 获取所有工段委外记录（不含分页，用于 SectionOutsources 页面列表展示）
    /// </summary>
    Task<List<SectionOutsourceDto>> GetAllSectionOutsourceListAsync();

    /// <summary>
    /// 获取所有委外回收记录（不含分页，用于 OutsourceRecoveries 页面列表展示）
    /// </summary>
    Task<List<OutsourceRecoveryDto>> GetAllOutsourceRecoveryListAsync();

    // ========== 筛选上下文 ==========

    /// <summary>
    /// 获取生产记录筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    // ========== 打印 ==========

    /// <summary>
    /// 批量打印生产记录
    /// </summary>
    Task<byte[]> PrintProductionRecordBatchAsync(int[] ids, List<PrintColumnDef> columns);
}
