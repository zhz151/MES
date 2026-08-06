using FluentAssertions;
using MES.Core.Constants;

namespace MES.Tests;

/// <summary>
/// 责任类型英文 Key 常量映射测试：Key↔中文双向转换、幂等性、厂部判定。
/// </summary>
public class LiabilityTypeKeysTests
{
    // ========== All / KeyToChinese ==========

    [Fact]
    public void All_包含2个内置责任类型Key()
    {
        LiabilityTypeKeys.All.Should().HaveCount(2);
        LiabilityTypeKeys.All.Should().Contain(LiabilityTypeKeys.FactoryDepartment);
        LiabilityTypeKeys.All.Should().Contain(LiabilityTypeKeys.OutsourcedPurchase);
    }

    [Fact]
    public void KeyToChinese_覆盖全部内置Key_值均为规范中文()
    {
        LiabilityTypeKeys.KeyToChinese.Should().HaveCount(2);
        foreach (var kvp in LiabilityTypeKeys.KeyToChinese)
        {
            LiabilityTypeKeys.IsKey(kvp.Key).Should().BeTrue($"'{kvp.Key}' 应为合法 Key");
        }
        LiabilityTypeKeys.KeyToChinese[LiabilityTypeKeys.FactoryDepartment].Should().Be("厂部");
        LiabilityTypeKeys.KeyToChinese[LiabilityTypeKeys.OutsourcedPurchase].Should().Be("外购");
    }

    // ========== IsKey ==========

    [Theory]
    [InlineData("FactoryDepartment", true)]
    [InlineData("OutsourcedPurchase", true)]
    [InlineData("factorydepartment", false)]     // 大小写敏感
    [InlineData("FactoryDepartment ", false)]    // 前后空格不算
    [InlineData("厂部", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKey_判定是否合法Key(string? value, bool expected)
    {
        LiabilityTypeKeys.IsKey(value).Should().Be(expected);
    }

    // ========== ToKey（归一为稳定 Key） ==========

    [Fact]
    public void ToKey_Key幂等()
    {
        LiabilityTypeKeys.ToKey(LiabilityTypeKeys.FactoryDepartment).Should().Be("FactoryDepartment");
        LiabilityTypeKeys.ToKey(LiabilityTypeKeys.OutsourcedPurchase).Should().Be("OutsourcedPurchase");
    }

    [Fact]
    public void ToKey_规范中文反查()
    {
        LiabilityTypeKeys.ToKey("厂部").Should().Be(LiabilityTypeKeys.FactoryDepartment);
        LiabilityTypeKeys.ToKey("外购").Should().Be(LiabilityTypeKeys.OutsourcedPurchase);
    }

    [Fact]
    public void ToKey_未知值返回null()
    {
        LiabilityTypeKeys.ToKey("不存在的责任类型").Should().BeNull();
        LiabilityTypeKeys.ToKey("FactoryDept").Should().BeNull();
    }

    [Fact]
    public void ToKey_null或空返回null()
    {
        LiabilityTypeKeys.ToKey(null).Should().BeNull();
        LiabilityTypeKeys.ToKey("").Should().BeNull();
    }

    [Fact]
    public void ToKey_全部内置中文均可反查()
    {
        foreach (var key in LiabilityTypeKeys.All)
        {
            LiabilityTypeKeys.ToKey(LiabilityTypeKeys.ToChinese(key)!).Should().Be(key);
        }
    }

    // ========== ToChinese（归一为显示中文） ==========

    [Fact]
    public void ToChinese_Key转中文()
    {
        LiabilityTypeKeys.ToChinese(LiabilityTypeKeys.FactoryDepartment).Should().Be("厂部");
        LiabilityTypeKeys.ToChinese("OutsourcedPurchase").Should().Be("外购");
    }

    [Fact]
    public void ToChinese_中文原样返回()
    {
        LiabilityTypeKeys.ToChinese("厂部").Should().Be("厂部");
        LiabilityTypeKeys.ToChinese("外购").Should().Be("外购");
    }

    [Fact]
    public void ToChinese_未知值原样返回()
    {
        LiabilityTypeKeys.ToChinese("不存在的责任类型").Should().Be("不存在的责任类型");
    }

    [Fact]
    public void ToChinese_null或空返回null()
    {
        LiabilityTypeKeys.ToChinese(null).Should().BeNull();
        LiabilityTypeKeys.ToChinese("").Should().BeNull();
    }

    // ========== 厂部判定（代码分支专用） ==========

    [Fact]
    public void IsFactoryDepartment_判定()
    {
        LiabilityTypeKeys.IsFactoryDepartment(LiabilityTypeKeys.FactoryDepartment).Should().BeTrue();
        LiabilityTypeKeys.IsFactoryDepartment(LiabilityTypeKeys.OutsourcedPurchase).Should().BeFalse();
        LiabilityTypeKeys.IsFactoryDepartment(null).Should().BeFalse();
    }
}
