using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 生产记录服务接口（内部生产记录/工段委外/委外回收/检验到料）
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

    /// <summary>
    /// 获取批次的工段委外列表
    /// </summary>
    Task<PagedResult<SectionOutsourceDto>> GetSectionOutsourcesAsync(int batchId, QueryParams query);

    /// <summary>
    /// 创建工段委外（同步更新批次实时跟踪字段）
    /// </summary>
    Task<SectionOutsourceDto> CreateSectionOutsourceAsync(CreateSectionOutsourceRequest request);

    /// <summary>
    /// 删除工段委外
    /// </summary>
    Task DeleteSectionOutsourceAsync(int id);

    // ========== 委外回收 ==========

    /// <summary>
    /// 获取委外记录的回收列表
    /// </summary>
    Task<List<OutsourceRecoveryDto>> GetOutsourceRecoveriesAsync(int outsourceId);

    /// <summary>
    /// 创建委外回收（自动更新委外状态）
    /// </summary>
    Task<OutsourceRecoveryDto> CreateOutsourceRecoveryAsync(CreateOutsourceRecoveryRequest request);

    /// <summary>
    /// 删除委外回收（自动更新委外状态）
    /// </summary>
    Task DeleteOutsourceRecoveryAsync(int id);

    // ========== 检验到料 ==========

    /// <summary>
    /// 获取批次的检验到料记录
    /// </summary>
    Task<MaterialReceiveCheckDto?> GetMaterialReceiveCheckAsync(int batchId);

    /// <summary>
    /// 创建检验到料（将批次状态设为Completed）
    /// </summary>
    Task<MaterialReceiveCheckDto> CreateMaterialReceiveCheckAsync(CreateMaterialReceiveCheckRequest request);

    /// <summary>
    /// 批量创建检验到料（一次查询 + 一次SaveChanges + 一次批量刷新）
    /// </summary>
    Task<List<MaterialReceiveCheckDto>> BatchCreateMaterialReceiveChecksAsync(List<CreateMaterialReceiveCheckRequest> requests);

    /// <summary>
    /// 更新检验到料
    /// </summary>
    Task<MaterialReceiveCheckDto> UpdateMaterialReceiveCheckAsync(int id, UpdateMaterialReceiveCheckRequest request);

    /// <summary>
    /// 删除检验到料
    /// </summary>
    Task DeleteMaterialReceiveCheckAsync(int id);

    // ========== 批次状态查询 ==========

    /// <summary>
    /// 刷新批次的实时跟踪字段（基于已有记录重新计算）
    /// </summary>
    Task RefreshBatchTrackingFieldsAsync(int batchId);

    /// <summary>
    /// 批量刷新多个批次的实时跟踪字段（一次查询 + 一次SaveChanges）
    /// </summary>
    Task BatchUpdateBatchTrackingAsync(ICollection<int> batchIds);

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
    /// 跨批次查询所有检验到料记录
    /// </summary>
    Task<PagedResult<MaterialReceiveCheckDto>> GetAllMaterialReceiveChecksAsync(QueryParams query);

    /// <summary>
    /// 获取所有内部生产记录（不含分页，用于 ProductionRecords 页面列表展示）
    /// </summary>
    Task<List<ProductionRecordDto>> GetAllProductionRecordListAsync();

    /// <summary>
    /// 获取所有检验到料记录（不含分页，用于 MaterialChecks 页面列表展示）
    /// </summary>
    Task<List<MaterialReceiveCheckDto>> GetAllMaterialReceiveCheckListAsync();

    /// <summary>
    /// 获取所有工段委外记录（不含分页，用于 SectionOutsources 页面列表展示）
    /// </summary>
    Task<List<SectionOutsourceDto>> GetAllSectionOutsourceListAsync();

    /// <summary>
    /// 获取所有委外回收记录（不含分页，用于 OutsourceRecoveries 页面列表展示）
    /// </summary>
    Task<List<OutsourceRecoveryDto>> GetAllOutsourceRecoveryListAsync();

    /// <summary>
    /// 批量创建工段委外
    /// </summary>
    Task<List<SectionOutsourceDto>> BatchCreateSectionOutsourcesAsync(List<CreateSectionOutsourceRequest> requests);

    /// <summary>
    /// 批量创建委外回收
    /// </summary>
    Task<List<OutsourceRecoveryDto>> BatchCreateOutsourceRecoveriesAsync(List<CreateOutsourceRecoveryRequest> requests);

    // ========== 打印 ==========

    /// <summary>
    /// 批量打印生产记录
    /// </summary>
    Task<byte[]> PrintProductionRecordBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>
    /// 按筛选条件打印全部生产记录
    /// </summary>
    Task<byte[]> PrintProductionRecordAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? execDateFrom, DateTime? execDateTo);

    /// <summary>
    /// 批量打印检验到料
    /// </summary>
    Task<byte[]> PrintMaterialCheckBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>
    /// 按筛选条件打印全部检验到料
    /// </summary>
    Task<byte[]> PrintMaterialCheckAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? receiveDateFrom, DateTime? receiveDateTo);
}
