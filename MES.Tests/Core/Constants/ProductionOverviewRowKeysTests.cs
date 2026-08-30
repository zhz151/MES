using FluentAssertions;
using MES.Core.Constants;

namespace MES.Tests;

/// <summary>
/// 生产总览调度行名（DailyProductionCapacities.ProcessName）英文 Key 常量映射测试：Key↔中文双向转换、幂等性。
/// </summary>
public class ProductionOverviewRowKeysTests
{
    // ========== All / KeyToChinese ==========

    [Fact]
    public void All_仅含荒管抛光固定行()
    {
        // 冷轧/冷拔行由机台组配置表 ColdRollMachineGroupConfig 动态驱动（2026-08-30 起），常量仅保留荒管抛光
        ProductionOverviewRowKeys.All.Should().HaveCount(1);
        ProductionOverviewRowKeys.All.Should().BeEquivalentTo(new[]
        {
            ProductionOverviewRowKeys.Polish
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void KeyToChinese_覆盖荒管抛光_值为规范中文()
    {
        ProductionOverviewRowKeys.KeyToChinese.Should().HaveCount(1);
        ProductionOverviewRowKeys.KeyToChinese[ProductionOverviewRowKeys.Polish].Should().Be("荒管抛光");
    }

    // ========== IsKey ==========

    [Theory]
    [InlineData("Polish", true)]
    [InlineData("5060", false)]          // 机台组 Key 由配置表动态校验，常量仅识荒管抛光
    [InlineData("polish", false)]        // 大小写敏感
    [InlineData("荒管抛光", false)]       // 中文非 Key
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKey_判定是否合法Key(string? value, bool expected)
    {
        ProductionOverviewRowKeys.IsKey(value).Should().Be(expected);
    }

    // ========== ToChinese（归一为显示中文） ==========

    [Fact]
    public void ToChinese_Key转中文()
    {
        ProductionOverviewRowKeys.ToChinese("Polish").Should().Be("荒管抛光");
    }

    [Fact]
    public void ToChinese_中文原样返回()
    {
        ProductionOverviewRowKeys.ToChinese("荒管抛光").Should().Be("荒管抛光");
    }

    [Fact]
    public void ToChinese_未知值原样返回()
    {
        ProductionOverviewRowKeys.ToChinese("退火炉").Should().Be("退火炉");
    }

    [Fact]
    public void ToChinese_null或空返回null()
    {
        ProductionOverviewRowKeys.ToChinese(null).Should().BeNull();
        ProductionOverviewRowKeys.ToChinese("").Should().BeNull();
    }
}
