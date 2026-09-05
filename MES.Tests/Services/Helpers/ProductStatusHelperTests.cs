using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data.Entities.Batch;
using MES.Services.Helpers;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// 产类计算共享单源 ProductStatusHelper 纯函数测试：
/// Calculate 各分支（成品优先于荒管 / 荒管处理直判 / 在制修检→荒管回退及末道排除 / 默认在制）与 IsFinishedManufacturingItem 白名单。
/// </summary>
public class ProductStatusHelperTests
{
    private const string Spec = "Φ57×3";

    private static List<ProcessGroup> Groups(params ProcessGroup[] items)
        => items.ToList();

    private static ProcessGroup Pg(string name, int seq, string? spec = null)
        => new() { ProcessName = name, SequenceNumber = seq, ManufacturingSpec = spec };

    // ========== 成品（优先于荒管判定） ==========

    [Fact]
    public void Calculate_制造规格等于成品规格且制造物品为成品_返回Finished()
    {
        var groups = Groups(Pg(ProcessKeys.ColdRoll60, 1), Pg(ProcessKeys.ColdDraw, 2));

        var result = ProductStatusHelper.Calculate(
            ProcessKeys.ColdRoll60, Spec, nameof(MaterialType.Finished), groups, Spec);

        result.Should().Be(ProductStatuses.Finished);
    }

    [Fact]
    public void Calculate_规格匹配忽略大小写_返回Finished()
    {
        // 制造规格用小写希腊字母 φ，成品规格用大写 Φ：OrdinalIgnoreCase 应折叠命中
        var result = ProductStatusHelper.Calculate(
            ProcessKeys.ColdRoll60, "φ57×3", nameof(MaterialType.OrderFinished), new List<ProcessGroup>(), "Φ57×3");

        result.Should().Be(ProductStatuses.Finished);
    }

    [Fact]
    public void Calculate_规格相等但制造物品非成品_不返回Finished()
    {
        var result = ProductStatusHelper.Calculate(
            ProcessKeys.ColdRoll60, Spec, nameof(MaterialType.SemiFinished), new List<ProcessGroup>(), Spec);

        result.Should().Be(ProductStatuses.InProgress);
    }

    [Fact]
    public void Calculate_成品规格非空但制造规格不匹配_不返回Finished()
    {
        var result = ProductStatusHelper.Calculate(
            ProcessKeys.ColdDraw, "Φ60×3", nameof(MaterialType.Finished), new List<ProcessGroup>(), Spec);

        result.Should().Be(ProductStatuses.InProgress);
    }

    [Fact]
    public void Calculate_荒管处理中规格等于成品规格_成品判定优先于荒管()
    {
        // 成品判定必须先于荒管判定：即使工序是荒管处理，规格达成品也返回 Finished
        var result = ProductStatusHelper.Calculate(
            ProcessKeys.RoughTubeProcessing, Spec, nameof(MaterialType.Finished), new List<ProcessGroup>(), Spec);

        result.Should().Be(ProductStatuses.Finished);
    }

    // ========== 荒管 ==========

    [Fact]
    public void Calculate_工序为荒管处理_返回RoughTube()
    {
        var result = ProductStatusHelper.Calculate(
            ProcessKeys.RoughTubeProcessing, Spec, nameof(MaterialType.Surplus), new List<ProcessGroup>());

        result.Should().Be(ProductStatuses.RoughTube);
    }

    [Fact]
    public void Calculate_在制修检非末道且存在荒管工序组规格匹配_返回RoughTube()
    {
        var groups = Groups(Pg(ProcessKeys.RoughTubeProcessing, 1, Spec), Pg(ProcessKeys.ColdDraw, 2, "Φ60×3"));

        var result = ProductStatusHelper.Calculate(
            ProcessKeys.InProcessRepair, Spec, null, groups);

        result.Should().Be(ProductStatuses.RoughTube);
    }

    [Fact]
    public void Calculate_在制修检为末道工序_不返回RoughTube()
    {
        var groups = Groups(Pg(ProcessKeys.RoughTubeProcessing, 1, Spec), Pg(ProcessKeys.InProcessRepair, 2, "Φ60×3"));

        var result = ProductStatusHelper.Calculate(
            ProcessKeys.InProcessRepair, "Φ60×3", null, groups);

        result.Should().Be(ProductStatuses.InProgress);
    }

    [Fact]
    public void Calculate_在制修检非末道但荒管规格不匹配_不返回RoughTube()
    {
        var groups = Groups(Pg(ProcessKeys.RoughTubeProcessing, 1, Spec), Pg(ProcessKeys.ColdDraw, 2, "Φ60×3"));

        var result = ProductStatusHelper.Calculate(
            ProcessKeys.InProcessRepair, "Φ60×3", null, groups);

        result.Should().Be(ProductStatuses.InProgress);
    }

    [Fact]
    public void Calculate_在制修检但无荒管处理工序组_不返回RoughTube()
    {
        var groups = Groups(Pg(ProcessKeys.ColdDraw, 1, Spec), Pg(ProcessKeys.ColdRoll60, 2, Spec));

        var result = ProductStatusHelper.Calculate(
            ProcessKeys.InProcessRepair, Spec, null, groups);

        result.Should().Be(ProductStatuses.InProgress);
    }

    // ========== 默认在制 ==========

    [Fact]
    public void Calculate_普通工序无成品规格_返回InProgress()
    {
        var result = ProductStatusHelper.Calculate(
            ProcessKeys.ColdDraw, Spec, null, new List<ProcessGroup>());

        result.Should().Be(ProductStatuses.InProgress);
    }

    // ========== IsFinishedManufacturingItem 白名单 ==========

    [Fact]
    public void IsFinishedManufacturingItem_成品类物料_返回True()
    {
        ProductStatusHelper.IsFinishedManufacturingItem(nameof(MaterialType.OrderFinished)).Should().BeTrue();
        ProductStatusHelper.IsFinishedManufacturingItem(nameof(MaterialType.Finished)).Should().BeTrue();
        ProductStatusHelper.IsFinishedManufacturingItem(nameof(MaterialType.CriticalFinished)).Should().BeTrue();
        ProductStatusHelper.IsFinishedManufacturingItem(nameof(MaterialType.SpecialDeliveryStatus)).Should().BeTrue();
    }

    [Fact]
    public void IsFinishedManufacturingItem_非成品类及空白_返回False()
    {
        ProductStatusHelper.IsFinishedManufacturingItem(nameof(MaterialType.Surplus)).Should().BeFalse();
        ProductStatusHelper.IsFinishedManufacturingItem(nameof(MaterialType.SemiFinished)).Should().BeFalse();
        ProductStatusHelper.IsFinishedManufacturingItem(nameof(MaterialType.RoughTube)).Should().BeFalse();
        ProductStatusHelper.IsFinishedManufacturingItem(null).Should().BeFalse();
        ProductStatusHelper.IsFinishedManufacturingItem("自定义").Should().BeFalse();
    }
}
