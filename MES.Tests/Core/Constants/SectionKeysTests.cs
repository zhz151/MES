using FluentAssertions;
using MES.Core.Constants;

namespace MES.Tests;

/// <summary>
/// 工段英文 Key 常量映射测试：Key↔中文双向转换、别名归一、幂等性。
/// </summary>
public class SectionKeysTests
{
    // ========== All 与 SectionDefs 一致性 ==========

    [Fact]
    public void All_包含26个工段Key_与SectionDefs一一对应()
    {
        SectionKeys.All.Should().HaveCount(26);
        SectionKeys.All.Should().HaveCount(SectionDefs.All.Length);
        for (int i = 0; i < SectionKeys.All.Length; i++)
        {
            // 对应位置的中文与 SectionDefs.All 一致（双表顺序契约）
            SectionKeys.ToChinese(SectionKeys.All[i]).Should().Be(SectionDefs.All[i]);
        }
    }

    [Fact]
    public void KeyToChinese_覆盖全部26键_值均为规范中文()
    {
        SectionKeys.KeyToChinese.Should().HaveCount(26);
        foreach (var kvp in SectionKeys.KeyToChinese)
        {
            SectionKeys.IsKey(kvp.Key).Should().BeTrue($"'{kvp.Key}' 应为合法 Key");
            SectionDefs.PropertyToName[kvp.Key].Should().Be(kvp.Value);
        }
    }

    // ========== IsKey ==========

    [Theory]
    [InlineData("Cut", true)]
    [InlineData("ColdRollDraw", true)]
    [InlineData("Extra2", true)]
    [InlineData("cut", false)]          // 大小写敏感
    [InlineData("Cut ", false)]         // 前后空格不算
    [InlineData("断切", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKey_判定是否合法Key(string? value, bool expected)
    {
        SectionKeys.IsKey(value).Should().Be(expected);
    }

    // ========== ToKey（归一为稳定 Key） ==========

    [Fact]
    public void ToKey_Key幂等()
    {
        SectionKeys.ToKey(SectionKeys.Cut).Should().Be("Cut");
        SectionKeys.ToKey(SectionKeys.ColdRollDraw).Should().Be("ColdRollDraw");
    }

    [Fact]
    public void ToKey_规范中文反查()
    {
        SectionKeys.ToKey(SectionDefs.Cut).Should().Be(SectionKeys.Cut);
        SectionKeys.ToKey(SectionDefs.ColdRollDraw).Should().Be(SectionKeys.ColdRollDraw);
        SectionKeys.ToKey(SectionDefs.Pickle).Should().Be(SectionKeys.Pickle);
        SectionKeys.ToKey(SectionDefs.Degrease).Should().Be(SectionKeys.Degrease);
    }

    [Fact]
    public void ToKey_别名归一()
    {
        SectionKeys.ToKey("切管").Should().Be(SectionKeys.OilPipeCut);
        SectionKeys.ToKey("脱脂").Should().Be(SectionKeys.Degrease);
        SectionKeys.ToKey("测厚").Should().Be(SectionKeys.ThicknessMeasure);
        SectionKeys.ToKey("外抛").Should().Be(SectionKeys.OuterPolish);
        SectionKeys.ToKey("内磨").Should().Be(SectionKeys.InnerGrinding);
        SectionKeys.ToKey("探伤").Should().Be(SectionKeys.Inspection);
        SectionKeys.ToKey("焊头").Should().Be(SectionKeys.WeldingHead);
        SectionKeys.ToKey("打焊头").Should().Be(SectionKeys.WeldingHead);
        SectionKeys.ToKey("喷砂丸").Should().Be(SectionKeys.SandBlasting);
    }

    [Fact]
    public void ToKey_未知值返回null()
    {
        SectionKeys.ToKey("不存在的工段").Should().BeNull();
        SectionKeys.ToKey("CutX").Should().BeNull();
    }

    [Fact]
    public void ToKey_null或空返回null()
    {
        SectionKeys.ToKey(null).Should().BeNull();
        SectionKeys.ToKey("").Should().BeNull();
    }

    [Fact]
    public void ToKey_全部26个规范中文均可反查()
    {
        foreach (var key in SectionKeys.All)
        {
            SectionKeys.ToKey(SectionKeys.ToChinese(key)!).Should().Be(key);
        }
    }

    // ========== ToChinese（归一为显示中文） ==========

    [Fact]
    public void ToChinese_Key转中文()
    {
        SectionKeys.ToChinese(SectionKeys.Cut).Should().Be(SectionDefs.Cut);
        SectionKeys.ToChinese("Cut").Should().Be("断切");
        SectionKeys.ToChinese(SectionKeys.ColdRollDraw).Should().Be("冷轧拔");
    }

    [Fact]
    public void ToChinese_中文原样返回()
    {
        SectionKeys.ToChinese("断切").Should().Be("断切");
        SectionKeys.ToChinese("冷轧拔").Should().Be("冷轧拔");
        // 别名也原样返回（兼容迁移前存量显示）
        SectionKeys.ToChinese("切管").Should().Be("切管");
    }

    [Fact]
    public void ToChinese_未知值原样返回()
    {
        SectionKeys.ToChinese("不存在的工段").Should().Be("不存在的工段");
    }

    [Fact]
    public void ToChinese_null或空返回null()
    {
        SectionKeys.ToChinese(null).Should().BeNull();
        SectionKeys.ToChinese("").Should().BeNull();
    }
}
