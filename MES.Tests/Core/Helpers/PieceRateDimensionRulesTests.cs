using FluentAssertions;
using MES.Core.Helpers;

namespace MES.Tests;

/// <summary>
/// 生产计件维度档规则纯函数测试（2026-09-02）：
/// decimal 区间相切=合法邻接不重叠；int 区间共享整数即重叠；数值落档闭区间；等值 OrdinalIgnoreCase 去重。
/// </summary>
public class PieceRateDimensionRulesTests
{
    // ==================== decimal 区间重叠 ====================

    [Theory]
    [InlineData("54", "null", "41", "54", false)]  // (54,∞) vs (41,54] 相切 54=54 → 不重叠（半开衔接）
    [InlineData("54", "null", "40", "53", false)]  // 上界 53 < 下界 54 → 不重叠
    [InlineData("41", "54", "50", "60", true)]     // 跨段重叠
    [InlineData("41", "54", "54", "60", false)]    // 相切 54 → 不重叠
    public void RangesOverlap_decimal(string? aMinText, string? aMaxText, string? bMinText, string? bMaxText, bool expected)
    {
        var ok = PieceRateDimensionRules.RangesOverlap(
            Parse(aMinText), Parse(aMaxText), Parse(bMinText), Parse(bMaxText));
        ok.Should().Be(expected);
    }

    // ==================== int 区间重叠（定尺：共享整数即重叠） ====================

    [Theory]
    [InlineData("3", "5", "5", "8", true)]   // 共享整数 5
    [InlineData("3", "5", "6", "8", false)]  // 5 < 6 → 不重叠
    [InlineData(null, null, "1", "2", true)] // 全域 vs 有限
    public void RangesOverlapInt(string? aMinText, string? aMaxText, string? bMinText, string? bMaxText, bool expected)
    {
        var ok = PieceRateDimensionRules.RangesOverlapInt(
            ParseInt(aMinText), ParseInt(aMaxText), ParseInt(bMinText), ParseInt(bMaxText));
        ok.Should().Be(expected);
    }

    // ==================== 数值落档 ====================

    [Theory]
    [InlineData("41", "54", 50, true)]
    [InlineData("41", "54", 41, true)]   // 含下界
    [InlineData("41", "54", 54, true)]   // 含上界
    [InlineData("41", "54", 40, false)]
    [InlineData("41", "54", 55, false)]
    [InlineData("54", null, 60, true)]   // 开上侧
    [InlineData(null, "54", 50, true)]   // 开下侧
    public void IsInRange_decimal(string? minText, string? maxText, decimal value, bool expected)
    {
        PieceRateDimensionRules.IsInRange(Parse(minText), Parse(maxText), value).Should().Be(expected);
    }

    [Theory]
    [InlineData("3", "5", 3, true)]
    [InlineData("3", "5", 5, true)]
    [InlineData("3", "5", 6, false)]
    [InlineData("5", null, 8, true)]
    public void IsInRange_int(string? minText, string? maxText, int value, bool expected)
    {
        PieceRateDimensionRules.IsInRange(ParseInt(minText), ParseInt(maxText), value).Should().Be(expected);
    }

    // ==================== 区间宽度 ====================

    [Fact]
    public void SpanWidth_缺侧为正无穷()
    {
        PieceRateDimensionRules.SpanWidth(null, null).Should().Be(decimal.MaxValue);
        PieceRateDimensionRules.SpanWidth(1m, null).Should().Be(decimal.MaxValue);
    }

    [Fact]
    public void SpanWidth_双侧为差值()
    {
        PieceRateDimensionRules.SpanWidth(41m, 54m).Should().Be(13m);
    }

    // ==================== 等值去重 ====================

    [Fact]
    public void FirstDuplicate_忽略大小写返回重复项()
    {
        PieceRateDimensionRules.FirstDuplicateOrdinalIgnoreCase(["Bright", "bright", "Normal"])
            .Should().Be("bright");
    }

    [Fact]
    public void FirstDuplicate_空白与无重复返回null()
    {
        PieceRateDimensionRules.FirstDuplicateOrdinalIgnoreCase(["Bright", "Normal"]).Should().BeNull();
        PieceRateDimensionRules.FirstDuplicateOrdinalIgnoreCase([" ", null]).Should().BeNull();
    }

    private static decimal? Parse(string? text)
        => text == null || text == "null" ? null : decimal.Parse(text);

    private static int? ParseInt(string? text)
        => text == null || text == "null" ? null : int.Parse(text);
}
