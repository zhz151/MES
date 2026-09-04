using MES.Core.DTOs.Payroll;
using MES.Core.Models;

namespace MES.Core.Interfaces.Payroll;

/// <summary>
/// 成检计件类别服务（2026-09-03 引入）——工资结算上下文。
/// 类别 = 成检项目(InspectionItem 单选) + 基准价 + 结算单位 + 维度系数档（无约束子表）。
/// 同一成检项目同时仅一条启用类别（过滤唯一索引兜底）；档内区间重叠/等值去重校验。
/// 结算单价 = 类别.BasePrice × 命中档 Ratio 连乘；某维配档但记录值不落任何档 → 该维系数 1。
/// </summary>
public interface IPieceRateFinalInspectionCategoryService
{
    /// <summary>分页查询（成检项目/单位/启停筛选 + 模糊搜索 + 全字段排序）</summary>
    Task<PagedResult<PieceRateFinalInspectionCategoryListItemDto>> GetPagedAsync(PieceRateFinalInspectionCategoryQueryParams query);

    /// <summary>按 Id 获取详情（含维度档全量，供编辑页）</summary>
    Task<PieceRateFinalInspectionCategoryDetailDto?> GetDetailAsync(int id);

    /// <summary>编辑页下拉选项（成检项目/结算单位/长度状态/特殊制造状态/工厂牌号）</summary>
    Task<PieceRateFinalInspectionCategoryOptionsDto> GetOptionsAsync();

    /// <summary>保存类别（创建/更新合一：Id &gt; 0 = 更新；档行整组替换；启用须同项目唯一）</summary>
    Task<PieceRateFinalInspectionCategoryDetailDto> SaveAsync(int? id, PieceRateFinalInspectionCategorySaveRequest request);

    /// <summary>删除类别（级联删档行）</summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// 试算：按一条成检（成检项目 + 规格维值）匹配启用类别并算单价。
    /// 命中不到启用类别返回 null（=未定价）；同项目多余启用 = 数据违例防御性报错。
    /// </summary>
    Task<PieceRateFinalInspectionMatchResultDto?> MatchPriceAsync(PieceRateFinalInspectionMatchRequest request);

    /// <summary>模拟测算候选成检记录（全局任意记录：分页 + 成检项目/关键字过滤，服务端 SQL 下推）</summary>
    Task<PagedResult<FinalInspectionPriceTrialRecordDto>> GetTrialRecordsAsync(FinalInspectionPriceTrialRecordQuery query);

    /// <summary>模拟测算：按一条真实成检记录计价（与月结采集同 FinalInspectionMatchRequestMapper 单源映射）。
    /// 记录不存在抛 BusinessException；命中不到启用类别返回 null（=未定价）。</summary>
    Task<PieceRateFinalInspectionMatchResultDto?> MatchFinalInspectionRecordAsync(int recordId);
}
