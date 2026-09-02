using FluentAssertions;
using MES.Core.Helpers;

namespace MES.Tests;

/// <summary>
/// 生产计件类别 4 键 JSON 序列化/归一/匹配纯函数测试（2026-09-02）。
/// null/空数组 = 全选；「显式全列表」归一为 null；内存比较一律 OrdinalIgnoreCase。
/// </summary>
public class PieceRateJsonKeysTests
{
    private static readonly string[] Domain = ["ColdRoll50", "ColdDraw", "ColdRoll60"];

    [Fact]
    public void Deserialize_null返回空集()
    {
        var set = PieceRateJsonKeys.Deserialize(null);
        set.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_json数组去重忽略大小写()
    {
        var set = PieceRateJsonKeys.Deserialize("[\"ColdRoll50\",\"coldroll50\",\"ColdDraw\"]");
        set.Should().HaveCount(2);
        set.Should().Contain("ColdRoll50");
        set.Should().Contain("ColdDraw");
    }

    [Fact]
    public void SerializeNormalized_空集合返回null()
    {
        PieceRateJsonKeys.SerializeNormalized([], Domain).Should().BeNull();
        PieceRateJsonKeys.SerializeNormalized(null, Domain).Should().BeNull();
    }

    [Fact]
    public void SerializeNormalized_显式全列表归一为null()
    {
        // 与 domain 全等（忽略大小写）→ null
        PieceRateJsonKeys.SerializeNormalized(["ColdRoll50", "colddraw", "COLDROLL60"], Domain)
            .Should().BeNull();
    }

    [Fact]
    public void SerializeNormalized_非全集保留为排序JSON()
    {
        var json = PieceRateJsonKeys.SerializeNormalized(["ColdDraw", "ColdRoll50"], Domain);
        json.Should().Be("[\"ColdDraw\",\"ColdRoll50\"]");
    }

    [Fact]
    public void ContainsKey_null或空集恒true()
    {
        PieceRateJsonKeys.ContainsKey(null, "whatever").Should().BeTrue();
        PieceRateJsonKeys.ContainsKey(new HashSet<string>(StringComparer.OrdinalIgnoreCase), "whatever").Should().BeTrue();
    }

    [Fact]
    public void ContainsKey_大小写不敏感()
    {
        var set = new HashSet<string>(["ColdRoll50"], StringComparer.OrdinalIgnoreCase);
        PieceRateJsonKeys.ContainsKey(set, "coldroll50").Should().BeTrue();
        PieceRateJsonKeys.ContainsKey(set, "ColdDraw").Should().BeFalse();
    }

    [Fact]
    public void ContainsKey_集合非空但值为null返回false()
    {
        var set = new HashSet<string>(["ColdRoll50"], StringComparer.OrdinalIgnoreCase);
        PieceRateJsonKeys.ContainsKey(set, null).Should().BeFalse();
    }

    [Fact]
    public void Deserialize_非JSON残值原样单元素()
    {
        var set = PieceRateJsonKeys.Deserialize("ColdRoll50");
        set.Should().Contain("ColdRoll50");
    }
}
