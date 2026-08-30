using MES.Core.Models;

using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 在产明细计划服务接口 —— 三表 LEFT JOIN 实时查询
/// </summary>
public interface IBatchPlanService
{
    /// <summary>
    /// 分页查询在产+未产批次计划
    /// </summary>
    Task<PagedResult<BatchPlanDto>> GetPagedAsync(QueryParams query);

    /// <summary>
    /// 获取列筛选上下文
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>
    /// 全量加载（含冷轧排程维度），按工段筛选后返回全部记录
    /// </summary>
    Task<List<BatchPlanDto>> GetAllAsync(string? sectionTab);

    /// <summary>
    /// 跨工段汇总（实时查询）：按工段 Tab 逐工段归桶统计批次数/总重量/流转/重点/等级分布，末尾追加"合计"行（全量唯一批次）。
    /// 每工段行口径与 GetAllAsync(sectionTab) 完全一致；一个批次可能命中多个工段，故各工段行批次数之和可能大于合计。
    /// </summary>
    Task<List<BatchPlanSummaryRowDto>> GetSummaryAsync();

    /// <summary>
    /// 月度生产量数据（实时查询）：全库跨批次按工段统计本年 1月~12月各月产量重量。
    /// 统计口径与「近日生产量数据」（GetSummaryAsync）一致，仅日期窗口改为 [本年1月1日, 次年1月1日)。
    /// </summary>
    Task<List<BatchPlanMonthlySummaryRowDto>> GetMonthlySummaryAsync();

    /// <summary>
    /// 实时委外在产汇总（实时查询）：按「在产单位 × 工段」二维表统计批次有效投料重量。
    /// 口径：状态为在产/未产且有当前委外单位（CurrentOutsource 非空）的批次，取有效投料重量（CurrentValidWeight），
    /// 按（在产单位, 当前工段归列）聚合；不依赖委外发出/回收。
    /// 每个单元格三值：总量 = 该格所有批次有效投料；流转 = 其中实时 IsFlow（批次计划流转=是）批次之和；
    /// 特急 = 其中批次计划等级=急+（PlanFlowLevel 1）批次之和（与批次计划页"特急批重量"口径一致）。
    /// 行 = 在产单位（合计降序），列 = 有数据的工段（近日生产量数据工段 Tab 规范序），末尾追加"合计"行。
    /// </summary>
    Task<BatchPlanOutsourcePendingDto> GetOutsourcePendingAsync();

    /// <summary>
    /// 获取工段筛选 Tab 选项（配置驱动）：冷轧冷拔类 = ProcessDefinitions 启用工序，
    /// 普通工段 = StandardWorkDays 启用工段（扣除冷轧拔/检验/入库），末尾固定「荒管检」「在制检」。
    /// 前端 Tab 渲染与委外在产列排序（Display→序）共用。
    /// </summary>
    Task<List<BatchPlanSectionTabDto>> GetSectionTabOptionsAsync();

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<MES.Core.DTOs.Shared.PrintColumnDef> columns);
}
