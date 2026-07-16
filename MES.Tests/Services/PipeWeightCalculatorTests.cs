using FluentAssertions;
using MES.Core.Enums;
using MES.Services;

namespace MES.Tests.Services;

/// <summary>
/// PipeWeightCalculator.CalculateUnitWeight 边界测试
/// </summary>
public class PipeWeightCalculatorTests
{
    // ========== 边界：输入无效 ==========

    [Fact]
    public void CalculateUnitWeight_规格为空_返回null()
    {
        var result = PipeWeightCalculator.CalculateUnitWeight(
            specification: "",
            outerDiameterNegative: 0.5m, outerDiameterPositive: 0.5m,
            wallThicknessNegative: 0.5m, wallThicknessPositive: 0.5m,
            lengthStatus: LengthStatus.Fixed, maxLength: 6000m);

        result.Should().BeNull();
    }

    [Fact]
    public void CalculateUnitWeight_规格格式错误_返回null()
    {
        var result = PipeWeightCalculator.CalculateUnitWeight(
            specification: "219", // 没有 "*" 分隔
            outerDiameterNegative: 0.5m, outerDiameterPositive: 0.5m,
            wallThicknessNegative: 0.5m, wallThicknessPositive: 0.5m,
            lengthStatus: LengthStatus.Fixed, maxLength: 6000m);

        result.Should().BeNull();
    }

    [Fact]
    public void CalculateUnitWeight_OD和WT为0_返回null()
    {
        var result = PipeWeightCalculator.CalculateUnitWeight(
            specification: "0*0",
            outerDiameterNegative: 0.5m, outerDiameterPositive: 0.5m,
            wallThicknessNegative: 0.5m, wallThicknessPositive: 0.5m,
            lengthStatus: LengthStatus.Fixed, maxLength: 6000m);

        result.Should().BeNull();
    }

    [Fact]
    public void CalculateUnitWeight_公差导致OD无效_返回null()
    {
        // odActual = 10 - 0.5*100 + 0.5*0 = 10 - 50 = -40 ≤ 0 → null
        var result = PipeWeightCalculator.CalculateUnitWeight(
            specification: "10*5",
            outerDiameterNegative: 100m, outerDiameterPositive: 0m,
            wallThicknessNegative: 0.5m, wallThicknessPositive: 0.5m,
            lengthStatus: LengthStatus.Fixed, maxLength: 6000m);

        result.Should().BeNull();
    }

    // ========== 定尺计算 ==========

    [Fact]
    public void CalculateUnitWeight_定尺有MaxLength_正确计算()
    {
        // spec=219*8, ODneg=0.5, ODpos=0.5, WTneg=0.5, WTpos=0.5
        // odActual = 219 - 0.5*0.5 + 0.5*0.5 = 219
        // wtActual = 8 - 0.5*0.5 + 0.5*0.5 = 8
        // weightPerMeter = (219-8)*8*0.02466 = 41.62608
        // maxLength = 6000 (Fixed)
        // unitWeight = 41.62608 * 6000 / 1000 = 249.75648 → Math.Round(,3) = 249.756
        var result = PipeWeightCalculator.CalculateUnitWeight(
            specification: "219*8",
            outerDiameterNegative: 0.5m, outerDiameterPositive: 0.5m,
            wallThicknessNegative: 0.5m, wallThicknessPositive: 0.5m,
            lengthStatus: LengthStatus.Fixed, maxLength: 6000m);

        result.Should().Be(249.756m);
    }

    [Fact]
    public void CalculateUnitWeight_定尺MaxLength为null_默认4500()
    {
        // 41.62608 * 4500 / 1000 = 187.31736 → 187.317
        var result = PipeWeightCalculator.CalculateUnitWeight(
            specification: "219*8",
            outerDiameterNegative: 0.5m, outerDiameterPositive: 0.5m,
            wallThicknessNegative: 0.5m, wallThicknessPositive: 0.5m,
            lengthStatus: LengthStatus.Fixed, maxLength: null);

        result.Should().Be(187.317m);
    }

    // ========== 非定尺 ==========

    [Fact]
    public void CalculateUnitWeight_非定尺_Range_使用4500()
    {
        // LengthStatus.Range (非Fixed) → 固定 4500mm
        var result = PipeWeightCalculator.CalculateUnitWeight(
            specification: "219*8",
            outerDiameterNegative: 0.5m, outerDiameterPositive: 0.5m,
            wallThicknessNegative: 0.5m, wallThicknessPositive: 0.5m,
            lengthStatus: LengthStatus.Range, maxLength: 8000m);

        result.Should().Be(187.317m);
    }

    [Fact]
    public void CalculateUnitWeight_非定尺_NonFixed_使用4500()
    {
        var result = PipeWeightCalculator.CalculateUnitWeight(
            specification: "219*8",
            outerDiameterNegative: 0.5m, outerDiameterPositive: 0.5m,
            wallThicknessNegative: 0.5m, wallThicknessPositive: 0.5m,
            lengthStatus: LengthStatus.NonFixed, maxLength: 8000m);

        result.Should().Be(187.317m);
    }

    // ========== 公差边界 ==========

    [Fact]
    public void CalculateUnitWeight_公差为0_正确计算()
    {
        // odActual = 60 - 0 + 0 = 60
        // wtActual = 5 - 0 + 0 = 5
        // weightPerMeter = (60-5)*5*0.02466 = 6.7815
        // unitWeight = 6.7815 * 6000 / 1000 = 40.689 → 40.689
        var result = PipeWeightCalculator.CalculateUnitWeight(
            specification: "60*5",
            outerDiameterNegative: 0m, outerDiameterPositive: 0m,
            wallThicknessNegative: 0m, wallThicknessPositive: 0m,
            lengthStatus: LengthStatus.Fixed, maxLength: 6000m);

        result.Should().Be(40.689m);
    }

    [Fact]
    public void CalculateUnitWeight_负公差非对称_正确计算()
    {
        // odActual = 60 - 0.5*0.3 + 0.5*0.7 = 60 - 0.15 + 0.35 = 60.2
        // wtActual = 5 - 0.5*0.2 + 0.5*0.4 = 5 - 0.1 + 0.2 = 5.1
        // weightPerMeter = (60.2-5.1)*5.1*0.02466 = 55.1*5.1*0.02466 = 6.9297066
        // unitWeight = 6.9297066 * 6000 / 1000 = 41.5782396 → 41.578
        var result = PipeWeightCalculator.CalculateUnitWeight(
            specification: "60*5",
            outerDiameterNegative: 0.3m, outerDiameterPositive: 0.7m,
            wallThicknessNegative: 0.2m, wallThicknessPositive: 0.4m,
            lengthStatus: LengthStatus.Fixed, maxLength: 6000m);

        result.Should().Be(41.578m);
    }
}
