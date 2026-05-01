using FluentAssertions;
using MES.Services;

namespace MES.Tests.Services;

/// <summary>
/// 规格字符串解析工具测试
/// </summary>
public class SpecificationParserTests
{
    // ========== ParseOuterDiameter ==========

    [Fact]
    public void ParseOuterDiameter_标准格式_返回外径()
    {
        var result = SpecificationParser.ParseOuterDiameter("219*8");
        result.Should().Be(219m);
    }

    [Fact]
    public void ParseOuterDiameter_带小数_返回外径()
    {
        var result = SpecificationParser.ParseOuterDiameter("38.5*3.2");
        result.Should().Be(38.5m);
    }

    [Fact]
    public void ParseOuterDiameter_空字符串_返回零()
    {
        var result = SpecificationParser.ParseOuterDiameter("");
        result.Should().Be(0);
    }

    [Fact]
    public void ParseOuterDiameter_null_返回零()
    {
        var result = SpecificationParser.ParseOuterDiameter(null!);
        result.Should().Be(0);
    }

    [Fact]
    public void ParseOuterDiameter_只有外径无星号_返回外径()
    {
        var result = SpecificationParser.ParseOuterDiameter("159");
        result.Should().Be(159m);
    }

    [Fact]
    public void ParseOuterDiameter_非法格式_返回零()
    {
        var result = SpecificationParser.ParseOuterDiameter("abc*def");
        result.Should().Be(0);
    }

    [Fact]
    public void ParseOuterDiameter_多余段_取第一部分()
    {
        var result = SpecificationParser.ParseOuterDiameter("48*3.5*6000");
        result.Should().Be(48m);
    }

    // ========== ParseWallThickness ==========

    [Fact]
    public void ParseWallThickness_标准格式_返回壁厚()
    {
        var result = SpecificationParser.ParseWallThickness("219*8");
        result.Should().Be(8m);
    }

    [Fact]
    public void ParseWallThickness_带小数_返回壁厚()
    {
        var result = SpecificationParser.ParseWallThickness("38.5*3.2");
        result.Should().Be(3.2m);
    }

    [Fact]
    public void ParseWallThickness_空字符串_返回零()
    {
        var result = SpecificationParser.ParseWallThickness("");
        result.Should().Be(0);
    }

    [Fact]
    public void ParseWallThickness_null_返回零()
    {
        var result = SpecificationParser.ParseWallThickness(null!);
        result.Should().Be(0);
    }

    [Fact]
    public void ParseWallThickness_只有外径无星号_返回零()
    {
        var result = SpecificationParser.ParseWallThickness("159");
        result.Should().Be(0);
    }

    [Fact]
    public void ParseWallThickness_非法格式_返回零()
    {
        var result = SpecificationParser.ParseWallThickness("abc*def");
        result.Should().Be(0);
    }

    [Fact]
    public void ParseWallThickness_多余段_取第二部分()
    {
        var result = SpecificationParser.ParseWallThickness("48*3.5*6000");
        result.Should().Be(3.5m);
    }
}
