using FluentAssertions;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Tests;

/// <summary>
/// 定尺切割长度匹配标识纯函数测试：
/// 1) Match：命中工单号集合→完全匹配；仅命中主号集合→主号匹配；长度空/≤0/两者皆不中→null；
/// 2) GetText：完全匹配/主号匹配/空白 三态中文。
/// </summary>
public class CutLengthMatchHelperTests
{
    private static readonly HashSet<decimal> WorkOrderLengths = new() { 4000m, 8000m };
    private static readonly HashSet<decimal> MainNoLengths = new() { 4000m, 8000m, 6000m };

    // ========== Match 纯判定 ==========

    [Fact]
    public void Match_长度命中工单号集合_完全匹配()
    {
        CutLengthMatchHelper.Match(WorkOrderLengths, MainNoLengths, 4000m)
            .Should().Be(CutLengthMatchType.FullMatch);
    }

    [Fact]
    public void Match_仅命中主号集合_主号匹配()
    {
        // 6000 在订单+主号集合，但不在本工单号集合 → 主号匹配
        CutLengthMatchHelper.Match(WorkOrderLengths, MainNoLengths, 6000m)
            .Should().Be(CutLengthMatchType.MainNoMatch);
    }

    [Fact]
    public void Match_长度为空_返回null()
    {
        CutLengthMatchHelper.Match(WorkOrderLengths, MainNoLengths, null)
            .Should().BeNull();
    }

    [Fact]
    public void Match_长度为零_返回null()
    {
        CutLengthMatchHelper.Match(WorkOrderLengths, MainNoLengths, 0m)
            .Should().BeNull();
    }

    [Fact]
    public void Match_长度为负_返回null()
    {
        CutLengthMatchHelper.Match(WorkOrderLengths, MainNoLengths, -100m)
            .Should().BeNull();
    }

    [Fact]
    public void Match_两者皆不中_返回null()
    {
        CutLengthMatchHelper.Match(WorkOrderLengths, MainNoLengths, 12000m)
            .Should().BeNull();
    }

    [Fact]
    public void Match_集合为null_返回null()
    {
        CutLengthMatchHelper.Match(null, null, 4000m)
            .Should().BeNull();
    }

    [Fact]
    public void Match_工单号优先于主号_同长度同命中判完全匹配()
    {
        // 长度同时属于两集合时，工单号级优先 → 完全匹配
        CutLengthMatchHelper.Match(new HashSet<decimal> { 6000m }, MainNoLengths, 6000m)
            .Should().Be(CutLengthMatchType.FullMatch);
    }

    // ========== GetText 中文映射 ==========

    [Fact]
    public void GetText_完全匹配()
    {
        CutLengthMatchHelper.GetText(CutLengthMatchType.FullMatch).Should().Be("完全匹配");
    }

    [Fact]
    public void GetText_主号匹配()
    {
        CutLengthMatchHelper.GetText(CutLengthMatchType.MainNoMatch).Should().Be("主号匹配");
    }

    [Fact]
    public void GetText_空值_返回空串()
    {
        CutLengthMatchHelper.GetText(null).Should().BeEmpty();
    }
}
