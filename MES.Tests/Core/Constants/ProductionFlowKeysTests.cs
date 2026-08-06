using FluentAssertions;
using MES.Core.Constants;

namespace MES.Tests;

/// <summary>
/// 生产流转性英文 Key 常量映射测试：Key↔中文双向转换、幂等性。
/// </summary>
public class ProductionFlowKeysTests
{
    // ========== All / KeyToChinese ==========

    [Fact]
    public void All_包含5个流转Key()
    {
        ProductionFlowKeys.All.Should().HaveCount(5);
        ProductionFlowKeys.All.Should().BeEquivalentTo(new[]
        {
            ProductionFlowKeys.Normal, ProductionFlowKeys.Paused, ProductionFlowKeys.Waiting,
            ProductionFlowKeys.Doubt, ProductionFlowKeys.Skip
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void KeyToChinese_覆盖全部5键_值为规范中文()
    {
        ProductionFlowKeys.KeyToChinese.Should().HaveCount(5);
        foreach (var kvp in ProductionFlowKeys.KeyToChinese)
        {
            ProductionFlowKeys.IsKey(kvp.Key).Should().BeTrue($"'{kvp.Key}' 应为合法 Key");
        }
        ProductionFlowKeys.KeyToChinese[ProductionFlowKeys.Normal].Should().Be("正常");
        ProductionFlowKeys.KeyToChinese[ProductionFlowKeys.Paused].Should().Be("暂停");
        ProductionFlowKeys.KeyToChinese[ProductionFlowKeys.Waiting].Should().Be("待料");
        ProductionFlowKeys.KeyToChinese[ProductionFlowKeys.Doubt].Should().Be("疑问");
        ProductionFlowKeys.KeyToChinese[ProductionFlowKeys.Skip].Should().Be("略");
    }

    // ========== IsKey ==========

    [Theory]
    [InlineData("Normal", true)]
    [InlineData("Paused", true)]
    [InlineData("Waiting", true)]
    [InlineData("Doubt", true)]
    [InlineData("Skip", true)]
    [InlineData("normal", false)]   // 大小写敏感
    [InlineData("正常", false)]      // 中文非 Key
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKey_判定是否合法Key(string? value, bool expected)
    {
        ProductionFlowKeys.IsKey(value).Should().Be(expected);
    }

    // ========== ToKey（归一为稳定 Key） ==========

    [Fact]
    public void ToKey_Key幂等()
    {
        ProductionFlowKeys.ToKey(ProductionFlowKeys.Normal).Should().Be("Normal");
        ProductionFlowKeys.ToKey(ProductionFlowKeys.Doubt).Should().Be("Doubt");
    }

    [Fact]
    public void ToKey_规范中文反查()
    {
        ProductionFlowKeys.ToKey("正常").Should().Be(ProductionFlowKeys.Normal);
        ProductionFlowKeys.ToKey("暂停").Should().Be(ProductionFlowKeys.Paused);
        ProductionFlowKeys.ToKey("待料").Should().Be(ProductionFlowKeys.Waiting);
        ProductionFlowKeys.ToKey("疑问").Should().Be(ProductionFlowKeys.Doubt);
        ProductionFlowKeys.ToKey("略").Should().Be(ProductionFlowKeys.Skip);
    }

    [Fact]
    public void ToKey_未知值返回null()
    {
        ProductionFlowKeys.ToKey("未知流转").Should().BeNull();
    }

    [Fact]
    public void ToKey_null或空返回null()
    {
        ProductionFlowKeys.ToKey(null).Should().BeNull();
        ProductionFlowKeys.ToKey("").Should().BeNull();
    }

    [Fact]
    public void ToKey_全部5个规范中文均可反查()
    {
        foreach (var key in ProductionFlowKeys.All)
        {
            ProductionFlowKeys.ToKey(ProductionFlowKeys.ToChinese(key)!).Should().Be(key);
        }
    }

    // ========== ToChinese（归一为显示中文） ==========

    [Fact]
    public void ToChinese_Key转中文()
    {
        ProductionFlowKeys.ToChinese("Normal").Should().Be("正常");
        ProductionFlowKeys.ToChinese("Paused").Should().Be("暂停");
        ProductionFlowKeys.ToChinese("Waiting").Should().Be("待料");
        ProductionFlowKeys.ToChinese("Doubt").Should().Be("疑问");
        ProductionFlowKeys.ToChinese("Skip").Should().Be("略");
    }

    [Fact]
    public void ToChinese_中文原样返回()
    {
        ProductionFlowKeys.ToChinese("正常").Should().Be("正常");
        ProductionFlowKeys.ToChinese("疑问").Should().Be("疑问");
    }

    [Fact]
    public void ToChinese_未知值原样返回()
    {
        ProductionFlowKeys.ToChinese("未知流转").Should().Be("未知流转");
    }

    [Fact]
    public void ToChinese_null或空返回null()
    {
        ProductionFlowKeys.ToChinese(null).Should().BeNull();
        ProductionFlowKeys.ToChinese("").Should().BeNull();
    }
}
