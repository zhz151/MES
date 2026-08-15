using FluentAssertions;
using MES.Core.Constants;

namespace MES.Tests;

/// <summary>
/// 流转目标英文 Key 常量映射测试：Key↔中文双向转换、幂等性。
/// </summary>
public class FlowTargetKeysTests
{
    // ========== KeyToChinese ==========

    [Fact]
    public void KeyToChinese_覆盖全部6键_值为规范中文()
    {
        FlowTargetKeys.KeyToChinese.Should().HaveCount(6);
        FlowTargetKeys.KeyToChinese[FlowTargetKeys.Inspection].Should().Be("成检");
        FlowTargetKeys.KeyToChinese[FlowTargetKeys.CompletionColdRoll].Should().Be("完工冷轧");
        FlowTargetKeys.KeyToChinese[FlowTargetKeys.ColdRoll].Should().Be("冷轧");
        FlowTargetKeys.KeyToChinese[FlowTargetKeys.RoughTubeCheck].Should().Be("荒管检");
        FlowTargetKeys.KeyToChinese[FlowTargetKeys.InProcessCheck].Should().Be("在制检");
        FlowTargetKeys.KeyToChinese[FlowTargetKeys.FinalCheck].Should().Be("成品检验");
    }

    // ========== ToChinese（归一为显示中文） ==========

    [Fact]
    public void ToChinese_Key转中文()
    {
        FlowTargetKeys.ToChinese("Inspection").Should().Be("成检");
        FlowTargetKeys.ToChinese("CompletionColdRoll").Should().Be("完工冷轧");
        FlowTargetKeys.ToChinese("ColdRoll").Should().Be("冷轧");
        FlowTargetKeys.ToChinese("RoughTubeCheck").Should().Be("荒管检");
        FlowTargetKeys.ToChinese("InProcessCheck").Should().Be("在制检");
        FlowTargetKeys.ToChinese("FinalCheck").Should().Be("成品检验");
    }

    [Fact]
    public void ToChinese_中文原样返回()
    {
        FlowTargetKeys.ToChinese("成检").Should().Be("成检");
        FlowTargetKeys.ToChinese("完工冷轧").Should().Be("完工冷轧");
    }

    [Fact]
    public void ToChinese_未知值原样返回()
    {
        FlowTargetKeys.ToChinese("退火").Should().Be("退火");
    }

    [Fact]
    public void ToChinese_null或空返回null()
    {
        FlowTargetKeys.ToChinese(null).Should().BeNull();
        FlowTargetKeys.ToChinese("").Should().BeNull();
    }
}
