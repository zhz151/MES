using FluentAssertions;
using MES.Core.Constants;

namespace MES.Tests;

/// <summary>
/// 紧急性英文 Key 常量映射测试：Key↔中文双向转换、幂等性、特急判定。
/// </summary>
public class UrgencyLevelKeysTests
{
    // ========== All / KeyToChinese ==========

    [Fact]
    public void All_包含6个紧急Key()
    {
        UrgencyLevelKeys.All.Should().HaveCount(6);
        UrgencyLevelKeys.All.Should().BeEquivalentTo(new[]
        {
            UrgencyLevelKeys.APlusUrgent, UrgencyLevelKeys.AUrgent, UrgencyLevelKeys.BOrder,
            UrgencyLevelKeys.CSlow, UrgencyLevelKeys.DSlow, UrgencyLevelKeys.EPaused
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void KeyToChinese_覆盖全部6键_值为规范中文()
    {
        UrgencyLevelKeys.KeyToChinese.Should().HaveCount(6);
        foreach (var kvp in UrgencyLevelKeys.KeyToChinese)
        {
            UrgencyLevelKeys.IsKey(kvp.Key).Should().BeTrue($"'{kvp.Key}' 应为合法 Key");
        }
        UrgencyLevelKeys.KeyToChinese[UrgencyLevelKeys.APlusUrgent].Should().Be("A+急");
        UrgencyLevelKeys.KeyToChinese[UrgencyLevelKeys.AUrgent].Should().Be("A急");
        UrgencyLevelKeys.KeyToChinese[UrgencyLevelKeys.BOrder].Should().Be("B顺");
        UrgencyLevelKeys.KeyToChinese[UrgencyLevelKeys.CSlow].Should().Be("C缓");
        UrgencyLevelKeys.KeyToChinese[UrgencyLevelKeys.DSlow].Should().Be("D缓");
        UrgencyLevelKeys.KeyToChinese[UrgencyLevelKeys.EPaused].Should().Be("E停");
    }

    // ========== IsKey ==========

    [Theory]
    [InlineData("APlusUrgent", true)]
    [InlineData("AUrgent", true)]
    [InlineData("EPaused", true)]
    [InlineData("aplusurgent", false)]   // 大小写敏感
    [InlineData("A急", false)]            // 中文非 Key
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKey_判定是否合法Key(string? value, bool expected)
    {
        UrgencyLevelKeys.IsKey(value).Should().Be(expected);
    }

    // ========== ToKey（归一为稳定 Key） ==========

    [Fact]
    public void ToKey_Key幂等()
    {
        UrgencyLevelKeys.ToKey(UrgencyLevelKeys.APlusUrgent).Should().Be("APlusUrgent");
        UrgencyLevelKeys.ToKey(UrgencyLevelKeys.DSlow).Should().Be("DSlow");
    }

    [Fact]
    public void ToKey_规范中文反查()
    {
        UrgencyLevelKeys.ToKey("A+急").Should().Be(UrgencyLevelKeys.APlusUrgent);
        UrgencyLevelKeys.ToKey("A急").Should().Be(UrgencyLevelKeys.AUrgent);
        UrgencyLevelKeys.ToKey("B顺").Should().Be(UrgencyLevelKeys.BOrder);
        UrgencyLevelKeys.ToKey("C缓").Should().Be(UrgencyLevelKeys.CSlow);
        UrgencyLevelKeys.ToKey("D缓").Should().Be(UrgencyLevelKeys.DSlow);
        UrgencyLevelKeys.ToKey("E停").Should().Be(UrgencyLevelKeys.EPaused);
    }

    [Fact]
    public void ToKey_未知值返回null()
    {
        UrgencyLevelKeys.ToKey("B常").Should().BeNull();
        UrgencyLevelKeys.ToKey("X急").Should().BeNull();
    }

    [Fact]
    public void ToKey_null或空返回null()
    {
        UrgencyLevelKeys.ToKey(null).Should().BeNull();
        UrgencyLevelKeys.ToKey("").Should().BeNull();
    }

    [Fact]
    public void ToKey_全部6个规范中文均可反查()
    {
        foreach (var key in UrgencyLevelKeys.All)
        {
            UrgencyLevelKeys.ToKey(UrgencyLevelKeys.ToChinese(key)!).Should().Be(key);
        }
    }

    // ========== ToChinese（归一为显示中文） ==========

    [Fact]
    public void ToChinese_Key转中文()
    {
        UrgencyLevelKeys.ToChinese("APlusUrgent").Should().Be("A+急");
        UrgencyLevelKeys.ToChinese("AUrgent").Should().Be("A急");
        UrgencyLevelKeys.ToChinese("BOrder").Should().Be("B顺");
        UrgencyLevelKeys.ToChinese("CSlow").Should().Be("C缓");
        UrgencyLevelKeys.ToChinese("DSlow").Should().Be("D缓");
        UrgencyLevelKeys.ToChinese("EPaused").Should().Be("E停");
    }

    [Fact]
    public void ToChinese_中文原样返回()
    {
        UrgencyLevelKeys.ToChinese("A急").Should().Be("A急");
        UrgencyLevelKeys.ToChinese("B顺").Should().Be("B顺");
    }

    [Fact]
    public void ToChinese_未知值原样返回()
    {
        UrgencyLevelKeys.ToChinese("B常").Should().Be("B常");
    }

    [Fact]
    public void ToChinese_null或空返回null()
    {
        UrgencyLevelKeys.ToChinese(null).Should().BeNull();
        UrgencyLevelKeys.ToChinese("").Should().BeNull();
    }

    // ========== IsUrgent（特急判定） ==========

    [Fact]
    public void IsUrgent_仅特急两档()
    {
        UrgencyLevelKeys.IsUrgent(UrgencyLevelKeys.APlusUrgent).Should().BeTrue();
        UrgencyLevelKeys.IsUrgent(UrgencyLevelKeys.AUrgent).Should().BeTrue();
        UrgencyLevelKeys.IsUrgent(UrgencyLevelKeys.BOrder).Should().BeFalse();
        UrgencyLevelKeys.IsUrgent(UrgencyLevelKeys.CSlow).Should().BeFalse();
        UrgencyLevelKeys.IsUrgent(UrgencyLevelKeys.DSlow).Should().BeFalse();
        UrgencyLevelKeys.IsUrgent(UrgencyLevelKeys.EPaused).Should().BeFalse();
        UrgencyLevelKeys.IsUrgent(null).Should().BeFalse();
        UrgencyLevelKeys.IsUrgent("").Should().BeFalse();
        // 中文存量不判特急（匹配基于 Key）
        UrgencyLevelKeys.IsUrgent("A急").Should().BeFalse();
    }
}
