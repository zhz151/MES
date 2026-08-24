using FluentAssertions;
using MES.Core.Helpers;
using Xunit;

namespace MES.Tests;

/// <summary>
/// 到料实投一致性容差静态快照测试：默认值、Apply 配置联动、null 保持。
/// </summary>
public class MaterialPlanToleranceProviderTests
{
    [Fact]
    public void 默认值_与MaterialPlanToleranceDefaults一致()
    {
        MaterialPlanToleranceProvider.InputConsistencyTolerance.Should().Be(0.03m);
        MaterialPlanToleranceProvider.InputConsistencyUpper.Should().Be(1.03m);
        MaterialPlanToleranceProvider.InputConsistencyLower.Should().Be(0.97m);
    }

    [Fact]
    public void Apply_配置值_上下界联动()
    {
        MaterialPlanToleranceProvider.Apply(0.03m);
        try
        {
            MaterialPlanToleranceProvider.Apply(0.05m);
            MaterialPlanToleranceProvider.InputConsistencyTolerance.Should().Be(0.05m);
            MaterialPlanToleranceProvider.InputConsistencyUpper.Should().Be(1.05m);
            MaterialPlanToleranceProvider.InputConsistencyLower.Should().Be(0.95m);
        }
        finally
        {
            MaterialPlanToleranceProvider.Apply(0.03m);
        }
    }

    [Fact]
    public void Apply_null_保持当前值()
    {
        MaterialPlanToleranceProvider.Apply(0.03m);
        try
        {
            MaterialPlanToleranceProvider.Apply(null);
            MaterialPlanToleranceProvider.InputConsistencyTolerance.Should().Be(0.03m);
        }
        finally
        {
            MaterialPlanToleranceProvider.Apply(0.03m);
        }
    }
}
