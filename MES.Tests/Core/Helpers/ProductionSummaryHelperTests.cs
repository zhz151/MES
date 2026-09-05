using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Helpers;

namespace MES.Tests;

/// <summary>
/// 生产量数据全工段汇总共享 helper（ProductionSummaryHelper）纯函数测试：
/// ResolveAllSectionTabName 归行（冷轧拔按工序分化含 90 冷轧/非轧拔丢弃、内抛+内修磨合并、一般工段直出、未知工段 null）、
/// SummaryAllSectionTabs 规范行集不变量、SectionTabIndex、GenerateDateBuckets 7 桶闭区间边界与标签、
/// CalcPending 待投料（成购缺口/质量补料按流转比折扣/负数归零）、GetCutoffBucket 归桶。
/// </summary>
public class ProductionSummaryHelperTests
{
    private static readonly DateTime Today = new(2026, 9, 4);

    // ========== ResolveAllSectionTabName ==========

    [Fact]
    public void Resolve_冷轧拔按工序分化_命中拆分工序()
    {
        ProductionSummaryHelper.ResolveAllSectionTabName("ColdRoll60", SectionKeys.ColdRollDraw)
            .Should().Be("冷轧拔-60冷轧");
        ProductionSummaryHelper.ResolveAllSectionTabName("ColdDraw", SectionKeys.ColdRollDraw)
            .Should().Be("冷轧拔-冷拔");
        ProductionSummaryHelper.ResolveAllSectionTabName("ThreeRollColdRoll", SectionKeys.ColdRollDraw)
            .Should().Be("冷轧拔-三辊冷轧");
        // 中文工段名入参 → 归一到同一 Key
        ProductionSummaryHelper.ResolveAllSectionTabName("ColdRoll50", "冷轧拔")
            .Should().Be("冷轧拔-50冷轧");
    }

    [Fact]
    public void Resolve_冷轧拔暂未收录90冷轧_特判归一行()
    {
        ProductionSummaryHelper.ResolveAllSectionTabName("ColdRoll90", SectionKeys.ColdRollDraw)
            .Should().Be("冷轧拔-90冷轧");
        // 传中文 90 冷轧同样归一
        ProductionSummaryHelper.ResolveAllSectionTabName("90冷轧", SectionKeys.ColdRollDraw)
            .Should().Be("冷轧拔-90冷轧");
    }

    [Fact]
    public void Resolve_冷轧拔下非轧拔工序_丢弃返回Null()
    {
        // 附加成检/荒管处理 不属于冷轧拔拆分工序 → 该记录不归入任何冷轧拔行
        ProductionSummaryHelper.ResolveAllSectionTabName("AdditionalFinalInspection", SectionKeys.ColdRollDraw)
            .Should().BeNull();
        ProductionSummaryHelper.ResolveAllSectionTabName("RoughTubeProcessing", SectionKeys.ColdRollDraw)
            .Should().BeNull();
        // 未知工序（无法解析中文）同样返回 null
        ProductionSummaryHelper.ResolveAllSectionTabName("UnknownProcess", SectionKeys.ColdRollDraw)
            .Should().BeNull();
    }

    [Fact]
    public void Resolve_内抛与内修磨_合并为一行()
    {
        ProductionSummaryHelper.ResolveAllSectionTabName(null, SectionKeys.InnerPolish)
            .Should().Be("内抛+内修磨");
        ProductionSummaryHelper.ResolveAllSectionTabName(null, SectionKeys.InnerGrinding)
            .Should().Be("内抛+内修磨");
        // 中文别名「内磨」也应归一命中
        ProductionSummaryHelper.ResolveAllSectionTabName(null, "内磨")
            .Should().Be("内抛+内修磨");
    }

    [Fact]
    public void Resolve_一般工段_直出中文()
    {
        ProductionSummaryHelper.ResolveAllSectionTabName(null, SectionKeys.Pickle).Should().Be("酸洗");
        ProductionSummaryHelper.ResolveAllSectionTabName(null, SectionKeys.OuterPolish).Should().Be("外抛光");
    }

    [Fact]
    public void Resolve_未知工段_返回Null()
    {
        ProductionSummaryHelper.ResolveAllSectionTabName(null, "NoSuchSection").Should().BeNull();
    }

    // ========== SummaryAllSectionTabs 规范行集 ==========

    [Fact]
    public void SummaryAllSectionTabs_行序与合并分化不变量()
    {
        var tabs = ProductionSummaryHelper.SummaryAllSectionTabs;

        // 26 工段：去冷轧拔(扩为 7 行) + 去内修磨(并入内抛) + 末尾检验-荒管/在制 2 行
        tabs.Length.Should().Be(SectionDefs.All.Length - 2 + 7 + 2);
        tabs.Should().OnlyHaveUniqueItems();

        tabs[0].Should().Be("冷轧拔-90冷轧");           // 冷轧拔按拆分序展开在最前
        tabs.Should().Contain("内抛+内修磨");
        tabs.Should().NotContain("内抛");
        tabs.Should().NotContain("内修磨");
        foreach (var p in new[] { "90冷轧", "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔" })
            tabs.Should().Contain("冷轧拔-" + p);

        tabs[^2].Should().Be("检验-荒管");
        tabs[^1].Should().Be("检验-在制");
    }

    [Fact]
    public void SectionTabIndex_已知行返回序号_未知行放末尾()
    {
        var tabs = ProductionSummaryHelper.SummaryAllSectionTabs;

        ProductionSummaryHelper.SectionTabIndex("冷轧拔-90冷轧").Should().Be(0);
        ProductionSummaryHelper.SectionTabIndex("检验-在制").Should().Be(tabs.Length - 1);
        ProductionSummaryHelper.SectionTabIndex("不存在").Should().Be(tabs.Length);
    }

    // ========== GenerateDateBuckets ==========

    [Fact]
    public void GenerateDateBuckets_7桶闭区间边界连续()
    {
        var buckets = ProductionSummaryHelper.GenerateDateBuckets(Today, 7, 15, 30, 45, 60);

        buckets.Count.Should().Be(7);
        buckets[0].Start.Should().Be(DateTime.MinValue);
        buckets[0].End.Should().Be(Today);                       // ≤今日
        buckets[1].Start.Should().Be(Today.AddDays(1));           // 今日+1
        buckets[1].End.Should().Be(Today.AddDays(7));
        buckets[2].Start.Should().Be(Today.AddDays(8));           // 今日+桶1+1
        buckets[2].End.Should().Be(Today.AddDays(15));
        buckets[5].End.Should().Be(Today.AddDays(60));
        buckets[6].Start.Should().Be(Today.AddDays(61));          // ≥今日+桶5+1
        buckets[6].End.Should().Be(DateTime.MaxValue);

        // 相邻桶无缝隙：前桶 End + 1 天 == 后桶 Start（首桶以今日为界）
        for (var i = 1; i < buckets.Count; i++)
            buckets[i - 1].End.AddDays(1).Should().Be(buckets[i].Start);

        buckets[0].Label.Should().Be("≤" + Today.ToString("yy/M/d"));
        buckets[^1].Label.Should().Be("≥" + Today.AddDays(61).ToString("yy/M/d"));
    }

    // ========== CalcPending ==========

    [Fact]
    public void CalcPending_普通锁定_扣已投料()
    {
        // total 2000, 成品计划 1000 未到货 → 成购缺口 1000；基数 (2000-1000)×1.1=1100；减已投 500 → 600
        var pending = ProductionSummaryHelper.CalcPending(2000m, 1000m, 0m, 500m, 0m, null, 1.1m);

        pending.Should().Be(600m);
    }

    [Fact]
    public void CalcPending_质量补料_按流转比折扣不减已投料()
    {
        // 质量补料口径 = base ×(1 - 流转比/100)，不扣已投料：1100 ×0.6 = 660
        var pending = ProductionSummaryHelper.CalcPending(
            2000m, 1000m, 0m, 500m, 40m, RawMaterialLockRemarkKeys.QualityReplenish, 1.1m);

        pending.Should().Be(660m);
    }

    [Fact]
    public void CalcPending_已到货抵扣成购缺口_待投减少()
    {
        // finishIn 600 → 成购缺口 400；基数 1600×1.1=1760 − 已投 1600 → 160
        var pending = ProductionSummaryHelper.CalcPending(2000m, 1000m, 600m, 1600m, 0m, null, 1.1m);

        pending.Should().Be(160m);
    }

    [Fact]
    public void CalcPending_结果负_归零()
    {
        var pending = ProductionSummaryHelper.CalcPending(2000m, 1000m, 0m, 5000m, 0m, null, 1.1m);

        pending.Should().Be(0m);
    }

    // ========== GetCutoffBucket ==========

    [Fact]
    public void GetCutoffBucket_空桶表_返回0()
    {
        ProductionSummaryHelper.GetCutoffBucket(Today, new List<(DateTime, DateTime, string)>())
            .Should().Be(0);
    }

    [Fact]
    public void GetCutoffBucket_截止日空_落末桶()
    {
        var buckets = ProductionSummaryHelper.GenerateDateBuckets(Today, 7, 15, 30, 45, 60);

        ProductionSummaryHelper.GetCutoffBucket(null, buckets).Should().Be(buckets.Count - 1);
    }

    [Fact]
    public void GetCutoffBucket_闭界首中即返_边界含首尾()
    {
        var buckets = ProductionSummaryHelper.GenerateDateBuckets(Today, 7, 15, 30, 45, 60);

        ProductionSummaryHelper.GetCutoffBucket(DateTime.Today.AddYears(-1), buckets).Should().Be(0); // ≤今日历史 → 桶0
        ProductionSummaryHelper.GetCutoffBucket(Today, buckets).Should().Be(0);                        // 今日（桶0 闭界末）
        ProductionSummaryHelper.GetCutoffBucket(Today.AddDays(1), buckets).Should().Be(1);             // 桶1 起点
        ProductionSummaryHelper.GetCutoffBucket(Today.AddDays(7), buckets).Should().Be(1);             // 桶1 闭界末
        ProductionSummaryHelper.GetCutoffBucket(Today.AddDays(60), buckets).Should().Be(5);            // 桶5 闭界末
        ProductionSummaryHelper.GetCutoffBucket(Today.AddDays(61), buckets).Should().Be(6);            // 末桶起点
    }
}
