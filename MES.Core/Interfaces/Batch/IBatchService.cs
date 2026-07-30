using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Batch;
namespace MES.Core.Interfaces.Batch;

/// <summary>
/// 批次服务接口
/// </summary>
public interface IBatchService
{
    // ========== 生产批次 ==========

    /// <summary>
    /// 分页查询批次列表
    /// </summary>
    Task<PagedResult<ProductionBatchListDto>> GetPagedAsync(BatchQueryParams query);

    /// <summary>
    /// 获取全部批次列表（无分页，供前端全量筛�?排序/搜索�?    /// </summary>
    Task<List<ProductionBatchListDto>> GetAllBatchListAsync();

    /// <summary>
    /// 根据ID获取批次详情
    /// </summary>
    Task<ProductionBatchDetailDto> GetByIdAsync(int id);

    /// <summary>
    /// 创建生产批次（同时从工单复制冗余字段�?    /// </summary>
    Task<ProductionBatchListDto> CreateAsync(CreateProductionBatchRequest request);

    /// <summary>
    /// 更新批次信息
    /// </summary>
    Task<ProductionBatchDetailDto> UpdateAsync(int id, UpdateProductionBatchRequest request);

    /// <summary>
    /// 更新批次状�?    /// </summary>
    Task UpdateStatusAsync(int id, UpdateBatchStatusRequest request);

    /// <summary>
    /// 删除批次
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// 批量保存批次（头更新 + 状�?+ 工序组全量替换，单事务）
    /// </summary>
    Task<SaveBatchResponse> SaveAllAsync(int id, SaveBatchRequest request);

    // ========== 工序�?==========

    /// <summary>
    /// 获取批次的工序组列表
    /// </summary>
    Task<List<ProcessGroupDto>> GetProcessGroupsAsync(int batchId);

    /// <summary>
    /// 添加工序�?    /// </summary>
    Task<ProcessGroupDto> AddProcessGroupAsync(int batchId, CreateProcessGroupRequest request);

    /// <summary>
    /// 删除工序�?    /// </summary>
    Task DeleteProcessGroupAsync(int groupId);

    // ========== 查询 ==========

    /// <summary>
    /// 获取可用的库存批次（已出库生产领用且尚未被生产批次引用）
    /// </summary>
    Task<List<AvailableBatchDto>> GetAvailableBatchesAsync();

    /// <summary>
    /// 获取下一个生产编号（YYMM-XXXX 格式�?    /// </summary>
    Task<string> GetNextBatchNoAsync();

    /// <summary>
    /// 获取上一条生产批次的工序组数据（用于快速复制）
    /// </summary>
    Task<List<CreateProcessGroupRequest>> GetLastBatchProcessGroupsAsync();

    // ========== 打印 ==========

    /// <summary>
    /// 打印批次详情
    /// </summary>
    Task<byte[]> PrintBatchAsync(int id);

    /// <summary>
    /// 打印全部批次
    /// </summary>
    Task<byte[]> PrintBatchAllAsync(BatchPrintAllRequest request);

    /// <summary>
    /// 打印选中批次
    /// </summary>
    Task<byte[]> PrintBatchSelectedAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>
    /// 打印工艺流转卡（A4�?区块，列选择�?    /// </summary>
    Task<byte[]> PrintProcessCardAsync(ProcessCardPrintRequest request);

    /// <summary>
    /// 验证所有生产批次的工单号在工单表中是否存在，返回不匹配的列�?    /// </summary>
    Task<List<BatchWorkOrderMismatchDto>> VerifyWorkOrderNosAsync();

    /// <summary>
    /// 根据批次号获取批次详情（含工序组，用于前端自动填充）
    /// </summary>
    Task<ProductionBatchDetailDto> GetByBatchNoAsync(string batchNo);

    /// <summary>
    /// 根据批次号获取工序组（用于前端自动填充，返回 CreateProcessGroupRequest 列表�?    /// </summary>
    Task<List<CreateProcessGroupRequest>> GetProcessGroupsByBatchNoAsync(string batchNo);

    /// <summary>
    /// 获取相邻批次导航信息（上一�?下一条）
    /// </summary>
    Task<AdjacentBatchDto> GetAdjacentBatchAsync(int currentId);

    /// <summary>
    /// 获取 ExcelFilter 列筛选上下文（各�?distinct 值）
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>
    /// 获取过程检验缺陷率超过 3% 的批次列表（建议调整有效投料量）
    /// </summary>
    Task<List<DefectRateBatchDto>> GetDefectRateAlertsAsync();
}
