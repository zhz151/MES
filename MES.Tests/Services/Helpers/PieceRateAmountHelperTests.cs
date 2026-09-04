using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Helpers;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// 计件整行金额折算共享单源 PieceRateAmountHelper 纯函数测试（2026-09-04 引入）。
/// 结算采集（PieceRateCollector）与成检试算端点共用该助手；此处直接覆盖各单位分支与长度折算兜底口径。
/// </summary>
public class PieceRateAmountHelperTests
{
    // ==================== AmountForUnit ====================

    [Fact]
    public void AmountForUnit_元吨按重量千克千折算()
    {
        var amount = PieceRateAmountHelper.AmountForUnit(PieceRateUnitKeys.PerTon, 3500m, 2000m, null, null);
        amount.Should().Be(7000m); // 2000kg / 1000 = 2 吨 × 3500
    }

    [Fact]
    public void AmountForUnit_元吨缺重量返回null()
    {
        var amount = PieceRateAmountHelper.AmountForUnit(PieceRateUnitKeys.PerTon, 3500m, null, 10, 6000m);
        amount.Should().BeNull();
    }

    [Fact]
    public void AmountForUnit_元千米按支乘毫米百万折算()
    {
        var amount = PieceRateAmountHelper.AmountForUnit(PieceRateUnitKeys.PerKm, 10m, null, 500, 6000m);
        amount.Should().Be(30m); // 500 支 × 6000mm / 1e6 = 3 km × 10
    }

    [Fact]
    public void AmountForUnit_元千米缺支数或长度返回null()
    {
        PieceRateAmountHelper.AmountForUnit(PieceRateUnitKeys.PerKm, 10m, null, null, 6000m).Should().BeNull();
        PieceRateAmountHelper.AmountForUnit(PieceRateUnitKeys.PerKm, 10m, null, 500, null).Should().BeNull();
    }

    [Fact]
    public void AmountForUnit_元支乘支数()
    {
        var amount = PieceRateAmountHelper.AmountForUnit(PieceRateUnitKeys.PerPiece, 0.5m, null, 80, null);
        amount.Should().Be(40m);
    }

    [Fact]
    public void AmountForUnit_元支缺支数返回null()
    {
        var amount = PieceRateAmountHelper.AmountForUnit(PieceRateUnitKeys.PerPiece, 0.5m, null, null, null);
        amount.Should().BeNull();
    }

    [Fact]
    public void AmountForUnit_元头乘支数乘平头数()
    {
        // 元/头：头数 = 加工支数 × 平头数（FaceCutCount）；平头数空默认 1
        var amount = PieceRateAmountHelper.AmountForUnit(PieceRateUnitKeys.PerHead, 0.4m, 1000m, 10, 6000m, 2);
        amount.Should().Be(8m); // 10 支 × 2 平头 = 20 头 × 0.4
    }

    [Fact]
    public void AmountForUnit_元头平头数空默认1()
    {
        var amount = PieceRateAmountHelper.AmountForUnit(PieceRateUnitKeys.PerHead, 0.4m, 1000m, 5, 6000m);
        amount.Should().Be(2m); // 5 支 × 1 × 0.4
    }

    [Fact]
    public void AmountForUnit_元头缺支数返回null()
    {
        var amount = PieceRateAmountHelper.AmountForUnit(PieceRateUnitKeys.PerHead, 0.4m, 1000m, null, 6000m, 2);
        amount.Should().BeNull();
    }

    // ==================== ResolveLengthMm（结算采集单支长兜底） ====================

    [Theory]
    [InlineData("9150mm", "Fixed", 9150)]
    [InlineData("11036 mm", "Fixed", 11036)]
    [InlineData(null, "Fixed", 6000)]      // Fixed 但无定尺文本 → 6000
    [InlineData("abc", "Fixed", 6000)]     // Fixed 文本无数字 → 6000
    [InlineData(null, "Range", 6000)]
    [InlineData(null, "NonFixed", 6000)]
    [InlineData(null, null, 6000)]
    public void ResolveLengthMm_各状态折算(string? fixedLength, string? status, decimal expected)
    {
        PieceRateAmountHelper.ResolveLengthMm(fixedLength, status).Should().Be(expected);
    }

    // ==================== DefaultTrialLengthMm（试算长度缺省） ====================

    [Theory]
    [InlineData("Range")]
    [InlineData("NonFixed")]
    [InlineData("range")]    // 不区分大小写
    [InlineData("范围尺")]     // 中文归一
    [InlineData("非定尺")]
    public void DefaultTrialLengthMm_范围非定尺按6000兜底(string? status)
    {
        PieceRateAmountHelper.DefaultTrialLengthMm(status).Should().Be(6000m);
    }

    [Theory]
    [InlineData("Fixed")]
    [InlineData("定尺")]      // Fixed 不兜底
    [InlineData(null)]
    [InlineData("")]
    public void DefaultTrialLengthMm_Fixed与空不兜底(string? status)
    {
        PieceRateAmountHelper.DefaultTrialLengthMm(status).Should().BeNull();
    }
}
