using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Batch;
namespace MES.Core.Interfaces.Batch;

/// <summary>
/// 去油/酸洗服务接口（入缸记�?+ 完工记录�?/// </summary>
public interface IPicklingService
{
    // ========== 入缸记录 ==========

    /// <summary>
    /// 跨批次分页查询入缸记�?    /// </summary>
    Task<PagedResult<PicklingInRecordDto>> GetPagedAsync(QueryParams query);

    /// <summary>
    /// 创建入缸记录（使�?BatchNo 模式�?    /// </summary>
    Task<PicklingInRecordDto> CreateAsync(CreatePicklingInRecordRequest request);

    /// <summary>
    /// 批量创建入缸记录
    /// </summary>
    Task<List<PicklingInRecordDto>> BatchCreateAsync(List<CreatePicklingInRecordRequest> requests);

    /// <summary>
    /// 更新入缸记录（内联编辑）
    /// </summary>
    Task<PicklingInRecordDto> UpdateAsync(int id, UpdatePicklingInRecordRequest request);

    /// <summary>
    /// 删除入缸记录（已有完工时禁止删除�?    /// </summary>
    Task DeleteAsync(int id);

    // ========== 完工记录 ==========

    /// <summary>
    /// 获取指定入缸的完工记�?    /// </summary>
    Task<PicklingOutRecordDto?> GetOutRecordByInIdAsync(int picklingInRecordId);

    /// <summary>
    /// 跨批次分页查询完工记�?    /// </summary>
    Task<PagedResult<PicklingOutRecordDto>> GetOutRecordsPagedAsync(QueryParams query);

    /// <summary>
    /// 创建完工记录（自动更新入缸状态为 Completed�?    /// </summary>
    Task<PicklingOutRecordDto> CreateOutRecordAsync(CreatePicklingOutRecordRequest request);

    /// <summary>
    /// 更新完工记录
    /// </summary>
    Task<PicklingOutRecordDto> UpdateOutRecordAsync(int id, UpdatePicklingOutRecordRequest request);

    /// <summary>
    /// 删除完工记录（自动恢复入缸状态为 Soaking�?    /// </summary>
    Task DeleteOutRecordAsync(int id);

    // ========== 打印 ==========

    /// <summary>
    /// 批量打印入缸记录（选中�?    /// </summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>
    /// 按筛选条件打印全部入缸记�?    /// </summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending,
        DateTime? inDateFrom, DateTime? inDateTo,
        DateTime? completeDateFrom, DateTime? completeDateTo,
        List<PrintColumnDef> columns);

    /// <summary>
    /// 批量打印完工记录（选中�?    /// </summary>
    Task<byte[]> PrintOutBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>
    /// 按筛选条件打印全部完工记�?    /// </summary>
    Task<byte[]> PrintOutAllAsync(string? keyword, string? sortBy, bool isDescending,
        DateTime? completeDateFrom, DateTime? completeDateTo,
        List<PrintColumnDef> columns);

    // ========== 筛选上下文 ==========

    /// <summary>
    /// 获取入缸记录筛选上下文（各列去重值），用�?ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>
    /// 获取完工记录筛选上下文
    /// </summary>
    Task<Dictionary<string, List<string>>> GetOutRecordFilterContextsAsync();

    /// <summary>
    /// 按批次号查询入缸记录（用于出缸扫码时选择关联的入缸记录）
    /// </summary>
    Task<List<PicklingInRecordDto>> GetByBatchAsync(string batchNo);
}
