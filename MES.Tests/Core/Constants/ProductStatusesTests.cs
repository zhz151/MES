using FluentAssertions;
using MES.Core.Constants;

namespace MES.Tests;

/// <summary>
/// 产类英文 Key 常量映射测试：Key↔中文双向转换、幂等性、成品判定。
/// </summary>
public class ProductStatusesTests
{
    // ========== All / KeyToChinese ==========

    [Fact]
    public void All_包含3个产类Key()
    {
        ProductStatuses.All.Should().HaveCount(3);
        ProductStatuses.All.Should().Contain(ProductStatuses.RoughTube);
        ProductStatuses.All.Should().Contain(ProductStatuses.InProgress);
        ProductStatuses.All.Should().Contain(ProductStatuses.Finished);
    }

    [Fact]
    public void KeyToChinese_覆盖全部3键_值均为规范中文()
    {
        ProductStatuses.KeyToChinese.Should().HaveCount(3);
        ProductStatuses.KeyToChinese[ProductStatuses.RoughTube].Should().Be("荒管");
        ProductStatuses.KeyToChinese[ProductStatuses.InProgress].Should().Be("在制");
        ProductStatuses.KeyToChinese[ProductStatuses.Finished].Should().Be("成品");
    }

    // ========== ToKey（归一为稳定 Key） ==========

    [Fact]
    public void ToKey_Key幂等()
    {
        ProductStatuses.ToKey(ProductStatuses.Finished).Should().Be("Finished");
        ProductStatuses.ToKey(ProductStatuses.RoughTube).Should().Be("RoughTube");
    }

    [Fact]
    public void ToKey_规范中文反查()
    {
        ProductStatuses.ToKey("成品").Should().Be(ProductStatuses.Finished);
        ProductStatuses.ToKey("荒管").Should().Be(ProductStatuses.RoughTube);
        ProductStatuses.ToKey("在制").Should().Be(ProductStatuses.InProgress);
    }

    [Fact]
    public void ToKey_未知值返回null()
    {
        ProductStatuses.ToKey("不存在的产类").Should().BeNull();
        ProductStatuses.ToKey("FinishedX").Should().BeNull();
    }

    [Fact]
    public void ToKey_null或空返回null()
    {
        ProductStatuses.ToKey(null).Should().BeNull();
        ProductStatuses.ToKey("").Should().BeNull();
    }

    [Fact]
    public void ToKey_全部规范中文均可反查()
    {
        foreach (var key in ProductStatuses.All)
        {
            ProductStatuses.ToKey(ProductStatuses.ToChinese(key)!).Should().Be(key);
        }
    }

    // ========== ToChinese（归一为显示中文） ==========

    [Fact]
    public void ToChinese_Key转中文()
    {
        ProductStatuses.ToChinese(ProductStatuses.Finished).Should().Be("成品");
        ProductStatuses.ToChinese("RoughTube").Should().Be("荒管");
        ProductStatuses.ToChinese("InProgress").Should().Be("在制");
    }

    [Fact]
    public void ToChinese_中文原样返回()
    {
        ProductStatuses.ToChinese("成品").Should().Be("成品");
        ProductStatuses.ToChinese("荒管").Should().Be("荒管");
    }

    [Fact]
    public void ToChinese_未知值原样返回()
    {
        ProductStatuses.ToChinese("不存在的产类").Should().Be("不存在的产类");
    }

    [Fact]
    public void ToChinese_null或空返回null()
    {
        ProductStatuses.ToChinese(null).Should().BeNull();
        ProductStatuses.ToChinese("").Should().BeNull();
    }

    // ========== 成品判定（代码分支专用） ==========

    [Fact]
    public void IsFinished_判定()
    {
        ProductStatuses.IsFinished(ProductStatuses.Finished).Should().BeTrue();
        ProductStatuses.IsFinished(ProductStatuses.InProgress).Should().BeFalse();
        ProductStatuses.IsFinished(null).Should().BeFalse();
    }
}
