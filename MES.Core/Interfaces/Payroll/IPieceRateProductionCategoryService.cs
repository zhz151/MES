using MES.Core.DTOs.Payroll;
using MES.Core.Models;

namespace MES.Core.Interfaces.Payroll;

/// <summary>
/// 生产计件类别服务（2026-09-02 重构引入，替代旧 IPieceRateStandardService）——工资结算上下文。
/// 类别 = 工段(必选单选) × 工序/产类/作业阶段(可空多选，空=全选) + 基准价 + 结算单位；
/// 维度系数在子表档行（无例外价）。结算单价 = 类别.BasePrice × 命中档 Ratio 连乘。
/// 保存时对同工段启用类别跑「禁止交集」校验 + 档内区间重叠/等值去重校验；匹配唯一性由禁交集保证（命中 ≤1）。
/// </summary>
public interface IPieceRateProductionCategoryService
{
    /// <summary>分页查询（工段/单位/启停筛选 + 模糊搜索 + 全字段排序）</summary>
    Task<PagedResult<PieceRateProductionCategoryListItemDto>> GetPagedAsync(PieceRateProductionCategoryQueryParams query);

    /// <summary>按 Id 获取详情（含维度档全量，供编辑页）</summary>
    Task<PieceRateProductionCategoryDetailDto?> GetDetailAsync(int id);

    /// <summary>编辑页下拉选项（启用工段/工序/固定产类/作业阶段/结算单位/特殊牌号候选）</summary>
    Task<PieceRateProductionCategoryOptionsDto> GetOptionsAsync();

    /// <summary>保存类别（创建/更新合一：Id &gt; 0 = 更新；档行整组替换）</summary>
    Task<PieceRateProductionCategoryDetailDto> SaveAsync(int? id, PieceRateProductionCategorySaveRequest request);

    /// <summary>删除类别（级联删档行）</summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// 试算：按一条报工（工段+工序+产类+阶段+维度值）匹配启用类别并算单价。
    /// 命中不到启用类别返回 null（=未定价）。命中 &gt; 1 = 数据违例防御性报错。
    /// </summary>
    Task<PieceRateProductionMatchResultDto?> MatchPriceAsync(PieceRateProductionMatchRequest request);

    /// <summary>模拟测算候选产量记录（产量源必选 + 关键字过滤 SQL 下推 + 分页；默认记录日期降序）</summary>
    Task<PagedResult<PieceRateProductionTrialRecordDto>> GetTrialRecordsAsync(PieceRateProductionTrialRecordQuery query);

    /// <summary>模拟测算：按一条真实产量记录计价（与月结采集同 ProductionMatchRequestMapper 单源映射）。
    /// 记录不存在抛 BusinessException；命中不到启用类别返回 null（=未定价）。</summary>
    Task<PieceRateProductionMatchResultDto?> MatchProductionRecordAsync(PieceRateProductionTrialSource source, int recordId);
}
