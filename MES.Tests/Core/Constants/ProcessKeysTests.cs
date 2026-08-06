using FluentAssertions;
using MES.Core.Constants;

namespace MES.Tests;

/// <summary>
/// 工序英文 Key 常量映射测试：Key↔中文双向转换、幂等性、冷轧类判定。
/// </summary>
public class ProcessKeysTests
{
    // ========== All 与 ProcessNames 一致性 ==========

    [Fact]
    public void All_包含9个工序Key_与ProcessNames一一对应()
    {
        ProcessKeys.All.Should().HaveCount(9);
        ProcessKeys.All.Should().HaveCount(ProcessNames.All.Length);
        for (int i = 0; i < ProcessKeys.All.Length; i++)
        {
            // 对应位置的中文与 ProcessNames.All 一致（双表顺序契约）
            ProcessKeys.ToChinese(ProcessKeys.All[i]).Should().Be(ProcessNames.All[i]);
        }
    }

    [Fact]
    public void KeyToChinese_覆盖全部9键_值均为规范中文()
    {
        ProcessKeys.KeyToChinese.Should().HaveCount(9);
        foreach (var kvp in ProcessKeys.KeyToChinese)
        {
            ProcessKeys.IsKey(kvp.Key).Should().BeTrue($"'{kvp.Key}' 应为合法 Key");
            ProcessNames.PropertyToName[kvp.Key].Should().Be(kvp.Value);
        }
    }

    // ========== IsKey ==========

    [Theory]
    [InlineData("ColdRoll60", true)]
    [InlineData("ColdDraw", true)]
    [InlineData("AdditionalFinalInspection", true)]
    [InlineData("coldroll60", false)]        // 大小写敏感
    [InlineData("ColdRoll60 ", false)]       // 前后空格不算
    [InlineData("60冷轧", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKey_判定是否合法Key(string? value, bool expected)
    {
        ProcessKeys.IsKey(value).Should().Be(expected);
    }

    // ========== ToKey（归一为稳定 Key） ==========

    [Fact]
    public void ToKey_Key幂等()
    {
        ProcessKeys.ToKey(ProcessKeys.ColdRoll60).Should().Be("ColdRoll60");
        ProcessKeys.ToKey(ProcessKeys.ColdDraw).Should().Be("ColdDraw");
    }

    [Fact]
    public void ToKey_规范中文反查()
    {
        ProcessKeys.ToKey(ProcessNames.ColdRoll60).Should().Be(ProcessKeys.ColdRoll60);
        ProcessKeys.ToKey(ProcessNames.ColdRoll50).Should().Be(ProcessKeys.ColdRoll50);
        ProcessKeys.ToKey(ProcessNames.ColdDraw).Should().Be(ProcessKeys.ColdDraw);
        ProcessKeys.ToKey(ProcessNames.AdditionalFinalInspection).Should().Be(ProcessKeys.AdditionalFinalInspection);
    }

    [Fact]
    public void ToKey_未知值返回null()
    {
        ProcessKeys.ToKey("不存在的工序").Should().BeNull();
        ProcessKeys.ToKey("ColdRollX").Should().BeNull();
    }

    [Fact]
    public void ToKey_null或空返回null()
    {
        ProcessKeys.ToKey(null).Should().BeNull();
        ProcessKeys.ToKey("").Should().BeNull();
    }

    [Fact]
    public void ToKey_全部9个规范中文均可反查()
    {
        foreach (var key in ProcessKeys.All)
        {
            ProcessKeys.ToKey(ProcessKeys.ToChinese(key)!).Should().Be(key);
        }
    }

    // ========== ToChinese（归一为显示中文） ==========

    [Fact]
    public void ToChinese_Key转中文()
    {
        ProcessKeys.ToChinese(ProcessKeys.ColdRoll60).Should().Be(ProcessNames.ColdRoll60);
        ProcessKeys.ToChinese("ColdRoll60").Should().Be("60冷轧");
        ProcessKeys.ToChinese(ProcessKeys.ColdDraw).Should().Be("冷拔");
    }

    [Fact]
    public void ToChinese_中文原样返回()
    {
        ProcessKeys.ToChinese("60冷轧").Should().Be("60冷轧");
        ProcessKeys.ToChinese("冷拔").Should().Be("冷拔");
    }

    [Fact]
    public void ToChinese_未知值原样返回()
    {
        ProcessKeys.ToChinese("不存在的工序").Should().Be("不存在的工序");
    }

    [Fact]
    public void ToChinese_null或空返回null()
    {
        ProcessKeys.ToChinese(null).Should().BeNull();
        ProcessKeys.ToChinese("").Should().BeNull();
    }

    // ========== 冷轧类判定 ==========

    [Fact]
    public void IsColdRoll_五档冷轧判定()
    {
        ProcessKeys.IsColdRoll(ProcessKeys.ColdRoll60).Should().BeTrue();
        ProcessKeys.IsColdRoll(ProcessKeys.ColdRoll50).Should().BeTrue();
        ProcessKeys.IsColdRoll(ProcessKeys.ColdRoll30).Should().BeTrue();
        ProcessKeys.IsColdRoll(ProcessKeys.ColdRoll20).Should().BeTrue();
        ProcessKeys.IsColdRoll(ProcessKeys.ThreeRollColdRoll).Should().BeTrue();
        // 冷拔/荒管/在制/附加成检非冷轧
        ProcessKeys.IsColdRoll(ProcessKeys.ColdDraw).Should().BeFalse();
        ProcessKeys.IsColdRoll(ProcessKeys.RoughTubeProcessing).Should().BeFalse();
        ProcessKeys.IsColdRoll(ProcessKeys.InProcessRepair).Should().BeFalse();
        ProcessKeys.IsColdRoll(ProcessKeys.AdditionalFinalInspection).Should().BeFalse();
        ProcessKeys.IsColdRoll(null).Should().BeFalse();
        ProcessKeys.IsColdRoll("").Should().BeFalse();
        // 中文名不判冷轧（匹配基于 Key）
        ProcessKeys.IsColdRoll("60冷轧").Should().BeFalse();
    }

    [Fact]
    public void IsColdRollOrColdDraw_冷轧加冷拔()
    {
        ProcessKeys.IsColdRollOrColdDraw(ProcessKeys.ColdRoll60).Should().BeTrue();
        ProcessKeys.IsColdRollOrColdDraw(ProcessKeys.ColdDraw).Should().BeTrue();
        ProcessKeys.IsColdRollOrColdDraw(ProcessKeys.RoughTubeProcessing).Should().BeFalse();
        ProcessKeys.IsColdRollOrColdDraw(null).Should().BeFalse();
    }
}
