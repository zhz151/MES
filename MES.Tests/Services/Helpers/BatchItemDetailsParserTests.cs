using FluentAssertions;
using MES.Core.Helpers;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// 批次 ItemDetails 去重定尺长度种数解析纯函数测试（2026-09-04 引入）。
/// 生产计件定尺维（FixedLengthCount）结算数据源；容忍 "5项," 与 "5," 两形态与 G29 小数。
/// </summary>
public class BatchItemDetailsParserTests
{
    [Fact]
    public void CountDistinctLengthsMm_真库四形态_去重种数()
    {
        var count = BatchItemDetailsParser.CountDistinctLengthsMm(
            "5,14154mm,30支;6,14241mm,24支;7,14328mm,14支;8,14415mm,12支;");
        count.Should().Be(4);
    }

    [Fact]
    public void CountDistinctLengthsMm_writer带项后缀_容忍解析()
    {
        var count = BatchItemDetailsParser.CountDistinctLengthsMm(
            "5项,14154mm,30支;6项,14241mm,24支;");
        count.Should().Be(2);
    }

    [Fact]
    public void CountDistinctLengthsMm_同长重复_去重为1()
    {
        var count = BatchItemDetailsParser.CountDistinctLengthsMm(
            "1项,14154mm,30支;2项,14154mm,20支;");
        count.Should().Be(1);
    }

    [Fact]
    public void CountDistinctLengthsMm_G29小数长度_解析()
    {
        var count = BatchItemDetailsParser.CountDistinctLengthsMm(
            "3,14154.5mm,10支;4,14241mm,8支;");
        count.Should().Be(2);
    }

    [Fact]
    public void CountDistinctLengthsMm_空白与null与无mm_均返回null()
    {
        BatchItemDetailsParser.CountDistinctLengthsMm(null).Should().BeNull();
        BatchItemDetailsParser.CountDistinctLengthsMm("").Should().BeNull();
        BatchItemDetailsParser.CountDistinctLengthsMm("   ").Should().BeNull();
        BatchItemDetailsParser.CountDistinctLengthsMm("1项,无长度,30支;").Should().BeNull();
    }
}
