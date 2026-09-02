using MES.Core.Constants;

namespace MES.Core.Helpers;

/// <summary>
/// 生产计件类别「禁止交集」纯函数（2026-09-02 重构引入，§3.3）。
/// 覆盖(C) = SectionKey × Procs(C) × Prods(C) × Stages(C)，其中任一约束集合为 null（空=全选）即该维全域。
/// 阶段域含「无阶段（普通报工）」合法取值：类别 StageKeys 非空（如 {InTank}）不覆盖无阶段记录，
/// StageKeys 为空（null）即全域（含无阶段）——由 null 与非 null 天然表达，无需额外存「无阶段」键。
/// 匹配唯一性由禁交集保证：任意一条报工命中启用类别 ≤ 1。
/// </summary>
public static class CategoryCoverageRule
{
    /// <summary>
    /// 一条类别的覆盖空间。Processes/ProductStatuses/Stages 为 null 表示该维全域（空=全选）。
    /// </summary>
    public readonly record struct CategoryCoverage(
        string SectionKey,
        HashSet<string>? Processes,
        HashSet<string>? ProductStatuses,
        HashSet<string>? Stages)
    {
        /// <summary>两条覆盖是否相交：同工段 且 三个可选键域各自相交（任一侧全域则相交）。</summary>
        public bool Intersects(CategoryCoverage other)
            => string.Equals(SectionKey, other.SectionKey, StringComparison.OrdinalIgnoreCase)
               && OverlapsOrUniversal(Processes, other.Processes)
               && OverlapsOrUniversal(ProductStatuses, other.ProductStatuses)
               && OverlapsOrUniversal(Stages, other.Stages);

        /// <summary>覆盖交集提示用中文描述（Section 中文｜产类｜工序｜阶段）。</summary>
        public string Describe()
        {
            var sectionCn = SectionKeys.ToChinese(SectionKey) ?? SectionKey;
            var prodCn = ProductStatusesText(ProductStatuses);
            var procCn = Text(Processes, "全部工序");
            var stageCn = Text(Stages, "全部阶段");
            return $"{sectionCn}｜{prodCn}｜{procCn}｜{stageCn}";
        }

        private static string ProductStatusesText(HashSet<string>? keys)
        {
            if (keys is null || keys.Count == 0) return "全部产类";
            var ordered = keys
                .Select(k => MES.Core.Constants.ProductStatuses.ToChinese(k) ?? k)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            return string.Join("·", ordered);
        }

        private static string Text(HashSet<string>? keys, string allText)
        {
            if (keys is null || keys.Count == 0) return allText;
            return string.Join("·", keys.OrderBy(x => x, StringComparer.Ordinal));
        }
    }

    /// <summary>由「可为 null 的键集合」构造覆盖（null/空 = 全域）。</summary>
    public static CategoryCoverage Create(
        string sectionKey,
        HashSet<string>? processes,
        HashSet<string>? productStatuses,
        HashSet<string>? stages)
    {
        return new CategoryCoverage(
            sectionKey,
            processes is { Count: > 0 } ? processes : null,
            productStatuses is { Count: > 0 } ? productStatuses : null,
            stages is { Count: > 0 } ? stages : null);
    }

    /// <summary>两键集合是否相交：任一侧全域（null/空）即相交；否则看集合是否 OrdinalIgnoreCase 有交集。</summary>
    public static bool OverlapsOrUniversal(HashSet<string>? a, HashSet<string>? b)
    {
        if (a is null || a.Count == 0) return true;
        if (b is null || b.Count == 0) return true;
        return a.Overlaps(b);
    }
}
