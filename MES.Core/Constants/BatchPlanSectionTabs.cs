namespace MES.Core.Constants;

/// <summary>
/// 批次计划工段筛选 Tab 列表（不含"全部"），前后端共享。
/// 与列表工段筛选口径一致：冷轧类（60冷轧…冷拔）按工序名+冷拔工段匹配，检验类（荒管检/在制检）按产类区分（工段=检验），
/// "内抛+内修磨"匹配内抛/内修磨两工段，其余按待执行工段精确匹配。
/// 汇总表（GetSummaryAsync）按此列表逐工段归桶统计。
/// </summary>
public static class BatchPlanSectionTabs
{
    public static readonly string[] All = new[]
    {
        "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔",
        "固溶", "矫直", "断切", "油管断", "去油", "酸洗", "外抛光", "内抛+内修磨", "外点磨",
        "荒管检", "在制检"
    };
}
