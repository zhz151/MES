using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Batch;
namespace MES.Core.Interfaces.Batch;

/// <summary>
/// 工段委外服务接口（委外发�?+ 委外回收�?/// </summary>
public interface ISectionOutsourceService
{
    // ========== 工段委外 ==========

    /// <summary>
    /// 跨批次分页查询委外发出记�?    /// </summary>
    Task<PagedResult<SectionOutsourceDto>> GetPagedAsync(QueryParams query);

    /// <summary>
    /// 根据ID列表获取委外发出记录（用于批量回收）
    /// </summary>
    Task<List<SectionOutsourceDto>> GetByIdsAsync(string ids);

    /// <summary>
    /// 创建委外发出（使�?BatchNo 模式�?    /// </summary>
    Task<SectionOutsourceDto> CreateAsync(CreateSectionOutsourceRequest request);

    /// <summary>
    /// 批量创建委外发出
    /// </summary>
    Task<List<SectionOutsourceDto>> BatchCreateAsync(List<CreateSectionOutsourceRequest> requests);

    /// <summary>
    /// 更新委外发出（内联编辑）
    /// </summary>
    Task<SectionOutsourceDto> UpdateAsync(int id, UpdateSectionOutsourceRequest request);

    /// <summary>
    /// 删除委外发出（已有回收时禁止删除�?    /// </summary>
    Task DeleteAsync(int id);

    // ========== 委外回收 ==========

    /// <summary>
    /// 获取指定委外发出的回收明细列�?    /// </summary>
    Task<List<OutsourceRecoveryDto>> GetRecoveriesAsync(int outsourceId);

    /// <summary>
    /// 跨批次分页查询回收记�?    /// </summary>
    Task<PagedResult<OutsourceRecoveryDto>> GetRecoveriesPagedAsync(QueryParams query);

    /// <summary>
    /// 创建委外回收（自动按重量99%阈值更新委外状态）
    /// </summary>
    Task<OutsourceRecoveryDto> CreateRecoveryAsync(CreateOutsourceRecoveryRequest request);

    /// <summary>
    /// 批量创建委外回收
    /// </summary>
    Task<List<OutsourceRecoveryDto>> BatchCreateRecoveriesAsync(List<CreateOutsourceRecoveryRequest> requests);

    /// <summary>
    /// 更新委外回收
    /// </summary>
    Task<OutsourceRecoveryDto> UpdateRecoveryAsync(int id, UpdateOutsourceRecoveryRequest request);

    /// <summary>
    /// 删除委外回收（自动重新计算委外状态）
    /// </summary>
    Task DeleteRecoveryAsync(int id);

    // ========== 打印 ==========

    /// <summary>
    /// 批量打印委外发出记录（选中�?    /// </summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>
    /// 按筛选条件打印全部委外发�?    /// </summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending,
        DateTime? sendOutDateFrom, DateTime? sendOutDateTo,
        DateTime? actualRecoveryDateFrom, DateTime? actualRecoveryDateTo,
        List<PrintColumnDef> columns);

    /// <summary>
    /// 批量打印回收记录（选中�?    /// </summary>
    Task<byte[]> PrintRecoveryBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>
    /// 按筛选条件打印全部回收记�?    /// </summary>
    Task<byte[]> PrintRecoveryAllAsync(string? keyword, string? sortBy, bool isDescending,
        DateTime? recoveryDateFrom, DateTime? recoveryDateTo,
        List<PrintColumnDef> columns);

    /// <summary>
    /// 获取委外回收筛选上下文（各列去重值），用�?ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetOutsourceRecoveryFilterContextsAsync();

    /// <summary>
    /// 获取工段委外发出筛选上下文（各列去重值），用�?ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>
    /// 根据批次号和工段名查询待回收（PendingRecovery）的委外记录
    /// </summary>
    Task<List<SectionOutsourceDto>> GetPendingByBatchAsync(string batchNo, string sectionName);

    /// <summary>
    /// 模糊搜索委外单位（用于 MudAutocomplete）
    /// </summary>
    Task<List<string>> SearchVendorsAsync(string? keyword);

    // ========== 汇总 ==========

    /// <summary>
    /// 月度委外数据汇总（不含厂内单位）：发/回/退按 (委外单位 × 工段) 按月聚合本年 1月~12月。
    /// 工段归行按生产记录「月度生产量数据」表全工段样式（ProductionSummaryHelper.ResolveAllSectionTabName）；
    /// 行 =（委外单位 × 工段）组合 + 合计行，整行全 0 隐藏；排序 = 单位总重量降序，组内工段规范序。
    /// </summary>
    Task<List<SectionOutsourceMonthlyRowDto>> GetMonthlyOutsourceAsync();

    /// <summary>
    /// 获取厂内单位集合（IsInternal=true 的委外单位，用于「实时委外在产」/「月度委外数据」过滤厂内行）
    /// </summary>
    Task<List<string>> GetInternalVendorsAsync();
}
