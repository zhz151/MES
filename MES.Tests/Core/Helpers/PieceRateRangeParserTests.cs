using FluentAssertions;
using MES.Core.Helpers;

namespace MES.Tests;

/// <summary>
/// 计件标准维度区间解析器测试：空/无数字 → false；双数字 → 双向闭区间取 min/max；
/// 单数字带方向（&gt;/&lt;/≥/≤、数字在前、字母在前）与纯数字 → 单侧/等值边界；首尾空白容忍。
/// </summary>
public class PieceRateRangeParserTests
{
    // ========== 空 / 无数字 → false ==========

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseRange_空文本_返回false(string? text)
    {
        PieceRateRangeParser.TryParseRange(text, out var min, out var max).Should().BeFalse();
        min.Should().BeNull();
        max.Should().BeNull();
    }

    [Fact]
    public void TryParseRange_无数字_返回false()
    {
        PieceRateRangeParser.TryParseRange("未启用-无档", out var min, out var max).Should().BeFalse();
        min.Should().BeNull();
        max.Should().BeNull();
    }

    // ========== 双数字及以上 → 双向闭区间（取 min/max） ==========

    [Fact]
    public void TryParseRange_双数字区间_取最小最大()
    {
        // 2001-5000
        PieceRateRangeParser.TryParseRange("2001-5000", out var min, out var max).Should().BeTrue();
        min.Should().Be(2001m);
        max.Should().Be(5000m);
    }

    [Fact]
    public void TryParseRange_双数字闭合带_取最小最大()
    {
        // 数据层改写后的闭合整数带（如 FixedLengthCount 1-2 / 9-9999）
        PieceRateRangeParser.TryParseRange("9-9999", out var min, out var max).Should().BeTrue();
        min.Should().Be(9m);
        max.Should().Be(9999m);
    }

    [Fact]
    public void TryParseRange_字母夹区间_顺序无关取最小最大()
    {
        // 3000≥D>820 → D ∈ [820, 3000]
        PieceRateRangeParser.TryParseRange("3000≥D>820", out var min, out var max).Should().BeTrue();
        min.Should().Be(820m);
        max.Should().Be(3000m);
    }

    // ========== 单数字 + 方向操作符 → 单侧边界 ==========

    [Theory]
    [InlineData(">16000", 16000d, null)]
    [InlineData("≥16000", 16000d, null)]
    [InlineData(">3", 3d, null)]
    public void TryParseRange_大于_仅min(string text, double minVal, double? maxVal)
    {
        PieceRateRangeParser.TryParseRange(text, out var min, out var max).Should().BeTrue();
        min.Should().Be((decimal)minVal);
        max.Should().Be((decimal?)maxVal);
    }

    [Theory]
    [InlineData("<2000", null, 2000d)]
    [InlineData("≤2000", null, 2000d)]
    public void TryParseRange_小于_仅max(string text, double? minVal, double maxVal)
    {
        PieceRateRangeParser.TryParseRange(text, out var min, out var max).Should().BeTrue();
        min.Should().Be((decimal?)minVal);
        max.Should().Be((decimal)maxVal);
    }

    // ========== 单数字 + 方向但字母参与（区间式与变量式） ==========

    [Fact]
    public void TryParseRange_数字在前小于字母_仅max()
    {
        // 10.5>D = D<10.5 → max
        PieceRateRangeParser.TryParseRange("10.5>D", out var min, out var max).Should().BeTrue();
        min.Should().BeNull();
        max.Should().Be(10.5m);
    }

    [Theory]
    [InlineData("D>52")]
    [InlineData("D≥52")]
    public void TryParseRange_字母大于数字_仅min(string text)
    {
        PieceRateRangeParser.TryParseRange(text, out var min, out var max).Should().BeTrue();
        min.Should().Be(52m);
        max.Should().BeNull();
    }

    [Fact]
    public void TryParseRange_字母小于数字_仅max()
    {
        PieceRateRangeParser.TryParseRange("D<13.5", out var min, out var max).Should().BeTrue();
        min.Should().BeNull();
        max.Should().Be(13.5m);
    }

    // ========== 纯数字 → 等值边界 ==========

    [Fact]
    public void TryParseRange_纯整数_等值()
    {
        // 断切率 "1" → min=max=1
        PieceRateRangeParser.TryParseRange("1", out var min, out var max).Should().BeTrue();
        min.Should().Be(1m);
        max.Should().Be(1m);
    }

    [Fact]
    public void TryParseRange_纯小数_等值()
    {
        PieceRateRangeParser.TryParseRange("10.5", out var min, out var max).Should().BeTrue();
        min.Should().Be(10.5m);
        max.Should().Be(10.5m);
    }

    // ========== 首尾空白容忍 ==========

    [Fact]
    public void TryParseRange_首尾空白_容忍()
    {
        PieceRateRangeParser.TryParseRange("  >16000  ", out var min, out var max).Should().BeTrue();
        min.Should().Be(16000m);
        max.Should().BeNull();
    }
}
