using FluentAssertions;
using MES.Core.Constants;
using MES.Data.Entities.Batch;
using MES.Services.Extensions;

namespace MES.Tests.Services.Extensions;

/// <summary>
/// ProcessGroup 工段提取扩展测试：GetNonEmptySectionKeys 与中文版一致、GetSectionSequence 归一。
/// </summary>
public class ProcessGroupExtensionsTests
{
    private static ProcessGroup CreatePg() => new()
    {
        ProcessName = "60冷轧",
        ColdRollDraw = 3,
        OilPipeCut = 5,
        Cut = 1,
        Pickle = 2,
        Inspection = 4,
    };

    // ========== GetNonEmptySectionKeys ==========

    [Fact]
    public void GetNonEmptySectionKeys_与GetNonEmptySections归一结果一致()
    {
        var pg = CreatePg();

        var keys = pg.GetNonEmptySectionKeys();
        var expected = pg.GetNonEmptySections()
            .Select(s => (SectionKeys.ToKey(s.SectionName)!, s.SequenceNumber))
            .Where(x => x.Item1 != null)
            .ToList();

        keys.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GetNonEmptySectionKeys_返回Key并按序号排序()
    {
        var pg = CreatePg();

        var keys = pg.GetNonEmptySectionKeys();

        keys.Select(k => k.SectionKey).Should().Equal(
            SectionKeys.Cut, SectionKeys.Pickle, SectionKeys.ColdRollDraw, SectionKeys.Inspection, SectionKeys.OilPipeCut);
        keys.Select(k => k.SequenceNumber).Should().BeInAscendingOrder();
    }

    [Fact]
    public void GetNonEmptySectionKeys_空工序组_返回空列表()
    {
        var pg = new ProcessGroup { ProcessName = "空" };

        pg.GetNonEmptySectionKeys().Should().BeEmpty();
    }

    // ========== GetSectionSequence ==========

    [Fact]
    public void GetSectionSequence_Key中文别名均可归一()
    {
        var pg = CreatePg();

        pg.GetSectionSequence(SectionKeys.Cut).Should().Be(1);
        pg.GetSectionSequence(SectionDefs.Cut).Should().Be(1);
        pg.GetSectionSequence("切管").Should().Be(5, "别名'切管'应归一到 OilPipeCut");
        pg.GetSectionSequence("脱脂").Should().BeNull("未设置的工段返回 null");
    }

    [Fact]
    public void GetSectionSequence_未知值返回null()
    {
        var pg = CreatePg();

        pg.GetSectionSequence("不存在的工段").Should().BeNull();
        pg.GetSectionSequence(null).Should().BeNull();
        pg.GetSectionSequence("").Should().BeNull();
    }
}
