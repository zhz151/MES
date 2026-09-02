using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Helpers;

namespace MES.Tests;

/// <summary>
/// 生产计件类别「禁止交集」覆盖规则测试（2026-09-02）：
/// 覆盖 = Section × Processes × ProductStatuses × Stages，空集=全域；同工段且三集 OverlapsOrUniversal 即相交。
/// </summary>
public class CategoryCoverageRuleTests
{
    private static readonly HashSet<string> Rough = new([ProductStatuses.RoughTube], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> InProg = new([ProductStatuses.InProgress], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Finished = new([ProductStatuses.Finished], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> InProgFinished = new([ProductStatuses.InProgress, ProductStatuses.Finished], StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> Set(params string[] keys)
        => new(keys, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void 不同工段永不冲突()
    {
        var a = CategoryCoverageRule.Create(SectionKeys.Pickle, Set(ProcessKeys.ColdRoll50), Rough, null);
        var b = CategoryCoverageRule.Create(SectionKeys.Degrease, Set(ProcessKeys.ColdRoll50), Rough, null);
        a.Intersects(b).Should().BeFalse();
    }

    [Fact]
    public void 同工段三集各不相交不冲突()
    {
        var a = CategoryCoverageRule.Create(SectionKeys.Pickle, null, Rough, Set(PieceRateStageKeys.InTank));
        var b = CategoryCoverageRule.Create(SectionKeys.Pickle, null, InProgFinished, Set(PieceRateStageKeys.OutTank));
        // 产类不相交（荒管 vs 在制·成品）→ 不冲突
        a.Intersects(b).Should().BeFalse();
    }

    [Fact]
    public void 阶段互斥同产类不冲突()
    {
        var a = CategoryCoverageRule.Create(SectionKeys.Pickle, null, InProgFinished, Set(PieceRateStageKeys.InTank));
        var b = CategoryCoverageRule.Create(SectionKeys.Pickle, null, InProgFinished, Set(PieceRateStageKeys.OutTank));
        a.Intersects(b).Should().BeFalse();
    }

    [Fact]
    public void 空集合全域与具体集相交()
    {
        // 阶段空=全域（含无阶段/入缸/出缸）→ 与只覆盖入缸的类别相交
        var a = CategoryCoverageRule.Create(SectionKeys.Pickle, null, InProgFinished, null);
        var b = CategoryCoverageRule.Create(SectionKeys.Pickle, null, InProgFinished, Set(PieceRateStageKeys.InTank));
        a.Intersects(b).Should().BeTrue();
    }

    [Fact]
    public void 完全相同覆盖冲突()
    {
        var a = CategoryCoverageRule.Create(SectionKeys.Pickle, null, InProgFinished, Set(PieceRateStageKeys.InTank));
        var b = CategoryCoverageRule.Create(SectionKeys.Pickle, null, InProgFinished, Set(PieceRateStageKeys.InTank));
        a.Intersects(b).Should().BeTrue();
    }

    [Fact]
    public void 产类相交阶段相交工序全域相交判定()
    {
        var a = CategoryCoverageRule.Create(SectionKeys.Pickle, null, InProgFinished, Set(PieceRateStageKeys.InTank));
        var b = CategoryCoverageRule.Create(SectionKeys.Pickle, Set(ProcessKeys.ColdDraw), InProg, Set(PieceRateStageKeys.InTank));
        // 工序侧 b 有限集 ∩ a 全域 → 相交；产类 在制 亦 ∩ 在制·成品 → 冲突
        a.Intersects(b).Should().BeTrue();
    }

    [Fact]
    public void 工序有限集互斥不冲突()
    {
        var a = CategoryCoverageRule.Create(SectionKeys.ColdRollDraw, Set(ProcessKeys.ColdRoll50), InProgFinished, null);
        var b = CategoryCoverageRule.Create(SectionKeys.ColdRollDraw, Set(ProcessKeys.ColdDraw), InProgFinished, null);
        a.Intersects(b).Should().BeFalse();
    }

    [Fact]
    public void Create_空集合归一为null全域()
    {
        var coverage = CategoryCoverageRule.Create(SectionKeys.Pickle, new HashSet<string>(), new HashSet<string>(), new HashSet<string>());
        coverage.Processes.Should().BeNull();
        coverage.ProductStatuses.Should().BeNull();
        coverage.Stages.Should().BeNull();
    }
}
