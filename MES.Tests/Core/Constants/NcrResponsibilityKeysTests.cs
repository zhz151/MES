using FluentAssertions;
using MES.Core.Constants;

namespace MES.Tests;

/// <summary>
/// NCR 责任类别英文 Key 常量映射测试：Key↔中文双向转换、幂等性、存量枚举名兼容。
/// </summary>
public class NcrResponsibilityKeysTests
{
    // ========== All / KeyToChinese ==========

    [Fact]
    public void All_包含5个内置责任类别Key()
    {
        NcrResponsibilityKeys.All.Should().HaveCount(5);
        NcrResponsibilityKeys.All.Should().Contain(NcrResponsibilityKeys.ProductionInternal);
        NcrResponsibilityKeys.All.Should().Contain(NcrResponsibilityKeys.ProductionOutsource);
        NcrResponsibilityKeys.All.Should().Contain(NcrResponsibilityKeys.MaterialTubeBlank);
        NcrResponsibilityKeys.All.Should().Contain(NcrResponsibilityKeys.MaterialPurchased);
        NcrResponsibilityKeys.All.Should().Contain(NcrResponsibilityKeys.MaterialSurplus);
    }

    [Fact]
    public void KeyToChinese_覆盖全部内置Key_值均为规范中文()
    {
        NcrResponsibilityKeys.KeyToChinese.Should().HaveCount(5);
        foreach (var kvp in NcrResponsibilityKeys.KeyToChinese)
        {
            NcrResponsibilityKeys.IsKey(kvp.Key).Should().BeTrue($"'{kvp.Key}' 应为合法 Key");
        }
        NcrResponsibilityKeys.KeyToChinese[NcrResponsibilityKeys.ProductionInternal].Should().Be("生产-厂内");
        NcrResponsibilityKeys.KeyToChinese[NcrResponsibilityKeys.ProductionOutsource].Should().Be("生产-外协");
        NcrResponsibilityKeys.KeyToChinese[NcrResponsibilityKeys.MaterialTubeBlank].Should().Be("原料-荒管");
        NcrResponsibilityKeys.KeyToChinese[NcrResponsibilityKeys.MaterialPurchased].Should().Be("原料-外购成品");
        NcrResponsibilityKeys.KeyToChinese[NcrResponsibilityKeys.MaterialSurplus].Should().Be("原料-余库料");
    }

    // ========== IsKey ==========

    [Theory]
    [InlineData("ProductionInternal", true)]
    [InlineData("ProductionOutsource", true)]
    [InlineData("MaterialTubeBlank", true)]
    [InlineData("MaterialPurchased", true)]
    [InlineData("MaterialSurplus", true)]
    [InlineData("productioninternal", false)]     // 大小写敏感
    [InlineData("ProductionInternal ", false)]    // 前后空格不算
    [InlineData("生产-厂内", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKey_判定是否合法Key(string? value, bool expected)
    {
        NcrResponsibilityKeys.IsKey(value).Should().Be(expected);
    }

    // ========== ToKey（归一为稳定 Key） ==========

    [Fact]
    public void ToKey_Key幂等()
    {
        foreach (var key in NcrResponsibilityKeys.All)
        {
            NcrResponsibilityKeys.ToKey(key).Should().Be(key);
        }
    }

    [Fact]
    public void ToKey_规范中文反查()
    {
        NcrResponsibilityKeys.ToKey("生产-厂内").Should().Be(NcrResponsibilityKeys.ProductionInternal);
        NcrResponsibilityKeys.ToKey("生产-外协").Should().Be(NcrResponsibilityKeys.ProductionOutsource);
        NcrResponsibilityKeys.ToKey("原料-荒管").Should().Be(NcrResponsibilityKeys.MaterialTubeBlank);
        NcrResponsibilityKeys.ToKey("原料-外购成品").Should().Be(NcrResponsibilityKeys.MaterialPurchased);
        NcrResponsibilityKeys.ToKey("原料-余库料").Should().Be(NcrResponsibilityKeys.MaterialSurplus);
    }

    [Fact]
    public void ToKey_未知值返回null()
    {
        NcrResponsibilityKeys.ToKey("不存在的责任类别").Should().BeNull();
        NcrResponsibilityKeys.ToKey("ProductionDept").Should().BeNull();
    }

    [Fact]
    public void ToKey_null或空返回null()
    {
        NcrResponsibilityKeys.ToKey(null).Should().BeNull();
        NcrResponsibilityKeys.ToKey("").Should().BeNull();
    }

    [Fact]
    public void ToKey_全部内置中文均可反查()
    {
        foreach (var key in NcrResponsibilityKeys.All)
        {
            NcrResponsibilityKeys.ToKey(NcrResponsibilityKeys.ToChinese(key)!).Should().Be(key);
        }
    }

    // ========== ToChinese（归一为显示中文） ==========

    [Fact]
    public void ToChinese_Key转中文()
    {
        NcrResponsibilityKeys.ToChinese(NcrResponsibilityKeys.ProductionInternal).Should().Be("生产-厂内");
        NcrResponsibilityKeys.ToChinese("MaterialSurplus").Should().Be("原料-余库料");
    }

    [Fact]
    public void ToChinese_中文原样返回()
    {
        NcrResponsibilityKeys.ToChinese("生产-厂内").Should().Be("生产-厂内");
        NcrResponsibilityKeys.ToChinese("原料-余库料").Should().Be("原料-余库料");
    }

    [Fact]
    public void ToChinese_未知值原样返回()
    {
        NcrResponsibilityKeys.ToChinese("不存在的责任类别").Should().Be("不存在的责任类别");
    }

    [Fact]
    public void ToChinese_null或空返回null()
    {
        NcrResponsibilityKeys.ToChinese(null).Should().BeNull();
        NcrResponsibilityKeys.ToChinese("").Should().BeNull();
    }
}
