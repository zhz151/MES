using FluentAssertions;
using MES.Core.Constants;

namespace MES.Tests;

/// <summary>
/// 生产总览调度行名（DailyProductionCapacities.ProcessName）英文 Key 常量映射测试：Key↔中文双向转换、幂等性。
/// </summary>
public class ProductionOverviewRowKeysTests
{
    // ========== All / KeyToChinese ==========

    [Fact]
    public void All_包含5个行名Key()
    {
        ProductionOverviewRowKeys.All.Should().HaveCount(5);
        ProductionOverviewRowKeys.All.Should().BeEquivalentTo(new[]
        {
            ProductionOverviewRowKeys.Polish, ProductionOverviewRowKeys.Mill50_60,
            ProductionOverviewRowKeys.Mill20_30, ProductionOverviewRowKeys.ThreeRollMill,
            ProductionOverviewRowKeys.DrawBench
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void KeyToChinese_覆盖全部5键_值为规范中文()
    {
        ProductionOverviewRowKeys.KeyToChinese.Should().HaveCount(5);
        ProductionOverviewRowKeys.KeyToChinese[ProductionOverviewRowKeys.Polish].Should().Be("荒管抛光");
        ProductionOverviewRowKeys.KeyToChinese[ProductionOverviewRowKeys.Mill50_60].Should().Be("50,60轧机");
        ProductionOverviewRowKeys.KeyToChinese[ProductionOverviewRowKeys.Mill20_30].Should().Be("20,30轧机");
        ProductionOverviewRowKeys.KeyToChinese[ProductionOverviewRowKeys.ThreeRollMill].Should().Be("三辊轧机");
        ProductionOverviewRowKeys.KeyToChinese[ProductionOverviewRowKeys.DrawBench].Should().Be("拉机");
    }

    // ========== IsKey ==========

    [Theory]
    [InlineData("Polish", true)]
    [InlineData("Mill50_60", true)]
    [InlineData("Mill20_30", true)]
    [InlineData("ThreeRollMill", true)]
    [InlineData("DrawBench", true)]
    [InlineData("polish", false)]        // 大小写敏感
    [InlineData("荒管抛光", false)]       // 中文非 Key
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKey_判定是否合法Key(string? value, bool expected)
    {
        ProductionOverviewRowKeys.IsKey(value).Should().Be(expected);
    }

    // ========== ToChinese（归一为显示中文） ==========

    [Fact]
    public void ToChinese_Key转中文()
    {
        ProductionOverviewRowKeys.ToChinese("Polish").Should().Be("荒管抛光");
        ProductionOverviewRowKeys.ToChinese("Mill50_60").Should().Be("50,60轧机");
        ProductionOverviewRowKeys.ToChinese("Mill20_30").Should().Be("20,30轧机");
        ProductionOverviewRowKeys.ToChinese("ThreeRollMill").Should().Be("三辊轧机");
        ProductionOverviewRowKeys.ToChinese("DrawBench").Should().Be("拉机");
    }

    [Fact]
    public void ToChinese_中文原样返回()
    {
        ProductionOverviewRowKeys.ToChinese("荒管抛光").Should().Be("荒管抛光");
        ProductionOverviewRowKeys.ToChinese("拉机").Should().Be("拉机");
    }

    [Fact]
    public void ToChinese_未知值原样返回()
    {
        ProductionOverviewRowKeys.ToChinese("退火炉").Should().Be("退火炉");
    }

    [Fact]
    public void ToChinese_null或空返回null()
    {
        ProductionOverviewRowKeys.ToChinese(null).Should().BeNull();
        ProductionOverviewRowKeys.ToChinese("").Should().BeNull();
    }
}
