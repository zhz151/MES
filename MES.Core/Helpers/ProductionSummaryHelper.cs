using MES.Core.Constants;

namespace MES.Core.Helpers;

/// <summary>
/// 生产量数据全工段汇总归行共享 helper。
/// 供 BatchPlanService（近日/月度生产量汇总）与 SectionOutsourceService（月度委外数据）共用，
/// 保证「工段归行」口径完全一致：
/// - 一般工段按工段 Key 归行（内抛/内修磨合并为「内抛+内修磨」）；
/// - 冷轧拔工段按所在工序组分化为「冷轧拔-&lt;工序&gt;」行（含暂未启用的 90 冷轧），非冷轧/冷拔工序的冷轧拔记录丢弃；
/// - 末尾追加「检验-荒管」「检验-在制」。
/// </summary>
public static class ProductionSummaryHelper
{
    // 冷轧拔分化工序（显示序）：90 冷轧暂未启用（ProcessKeys 未收录，按字符串 ColdRoll90/90冷轧 兜底匹配），
    // 其余对应 ProcessKeys 冷轧/冷拔类（60/50/30/20/三辊/冷拔）。
    // ⚠️ 必须声明在 SummaryAllSectionTabs 之前：静态字段初始化按文本顺序执行，BuildSummaryAllSectionTabs 依赖它。
    private static readonly string[] ColdRollDrawSplitProcesses = new[]
    {
        "90冷轧", "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔"
    };

    /// <summary>
    /// 近日/月度生产量数据全工段汇总行集合：SectionDefs 全部 26 个工段中文（内抛+内修磨合并为一行），
    /// 其中冷轧拔工段按所在工序组分化为多行（含暂未启用的 90 冷轧）+ 检验-荒管/检验-在制。
    /// 与 BatchPlanSectionTabs（工段筛选 Tab，17 项）解耦：汇总按全工段归行，筛选 Tab 保持原状。
    /// 行序 = SectionDefs.All 顺序（冷轧拔位置展开分化行）+ 检验-荒管/检验-在制末尾。
    /// </summary>
    public static readonly string[] SummaryAllSectionTabs = BuildSummaryAllSectionTabs();

    private static string[] BuildSummaryAllSectionTabs()
    {
        var tabs = new List<string>(SectionDefs.All.Length + 10);
        foreach (var cn in SectionDefs.All)
        {
            if (cn == SectionDefs.ColdRollDraw)
            {
                foreach (var p in ColdRollDrawSplitProcesses)
                    tabs.Add("冷轧拔-" + p);
                continue;
            }
            if (cn == SectionDefs.InnerGrinding) continue;   // 与内抛合并为「内抛+内修磨」
            tabs.Add(cn == SectionDefs.InnerPolish ? "内抛+内修磨" : cn);
        }
        tabs.Add("检验-荒管");
        tabs.Add("检验-在制");
        return tabs.ToArray();
    }

    /// <summary>
    /// 全工段汇总归行：一般工段按工段 Key 归行（内抛/内修磨合并为「内抛+内修磨」）；
    /// 冷轧拔工段按所在工序组分化为「冷轧拔-&lt;工序&gt;」行（含暂未启用的 90 冷轧），非冷轧/冷拔工序的冷轧拔记录丢弃。
    /// 供近日/月度生产量汇总（GetSummaryAsync/GetMonthlySummaryAsync）与月度委外汇总（GetMonthlyOutsourceAsync）使用；
    /// 无对应行返回 null。
    /// 注意：实时委外在产（GetOutsourcePendingAsync）仍用 BatchPlanService.ResolveSummaryTabName（按工序分化列序），勿改。
    /// </summary>
    public static string? ResolveAllSectionTabName(string? processName, string? sectionName)
    {
        var sKey = SectionKeys.ToKey(sectionName);
        if (sKey == null) return null;
        if (sKey == SectionKeys.ColdRollDraw)
        {
            // 冷轧拔 → 按工序分化；90 冷轧暂未收录 ProcessKeys，ColdRoll90/90冷轧 均归一为「90冷轧」
            var display = ProcessKeys.ToChinese(processName);
            if (processName == "ColdRoll90") display = "90冷轧";
            return display != null && ColdRollDrawSplitProcesses.Contains(display, StringComparer.Ordinal)
                ? "冷轧拔-" + display
                : null;
        }
        return sKey is SectionKeys.InnerPolish or SectionKeys.InnerGrinding
            ? "内抛+内修磨"
            : SectionKeys.ToChinese(sKey) ?? sKey;
    }

    /// <summary>
    /// 全工段汇总行索引（列排序用：月度委外数据工段列按 SummaryAllSectionTabs 规范序展示，未知 Tab 放末尾）。
    /// </summary>
    public static int SectionTabIndex(string tab)
    {
        for (var i = 0; i < SummaryAllSectionTabs.Length; i++)
            if (string.Equals(SummaryAllSectionTabs[i], tab, StringComparison.Ordinal))
                return i;
        return SummaryAllSectionTabs.Length;
    }
}
