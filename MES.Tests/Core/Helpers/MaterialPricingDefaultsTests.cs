using FluentAssertions;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Tests;

/// <summary>
/// 物料计价默认值与总价口径（MaterialPricingDefaults）纯函数测试：
/// 荒管族/成品族判定、采购默认单价 18/26、委外穿孔 1.2、ComputeTotal 按计价单位三分支取量与保留 2 位。
/// ⚠️ 常量与 EF 迁移回填 SQL（AddPurchaseAndSubcontractPricing）人工保持一致。
/// </summary>
public class MaterialPricingDefaultsTests
{
    // ========== 族判定 ==========

    [Fact]
    public void IsRoughTubeFamily_荒管族_命中()
    {
        MaterialPricingDefaults.IsRoughTubeFamily(MaterialType.RoughTube).Should().BeTrue();
        MaterialPricingDefaults.IsRoughTubeFamily(MaterialType.DefectRoughTube).Should().BeTrue();
        // 成品族不落入荒管族
        MaterialPricingDefaults.IsRoughTubeFamily(MaterialType.CriticalFinished).Should().BeFalse();
        MaterialPricingDefaults.IsRoughTubeFamily(MaterialType.RoundBar).Should().BeFalse();
    }

    [Fact]
    public void IsFinishedFamily_成品族_命中()
    {
        foreach (var cat in new[]
                 {
                     MaterialType.Finished, MaterialType.OrderFinished, MaterialType.CriticalFinished,
                     MaterialType.DefectFinished, MaterialType.SpecialDeliveryStatus
                 })
        {
            MaterialPricingDefaults.IsFinishedFamily(cat).Should().BeTrue($"分类 {cat} 应属成品族");
        }
        MaterialPricingDefaults.IsFinishedFamily(MaterialType.RoughTube).Should().BeFalse();
    }

    // ========== 采购默认单价 ==========

    [Fact]
    public void DefaultPurchaseUnitPrice_荒管族18_成品族26_其它空()
    {
        MaterialPricingDefaults.DefaultPurchaseUnitPrice(MaterialType.RoughTube).Should().Be(18m);
        MaterialPricingDefaults.DefaultPurchaseUnitPrice(MaterialType.DefectRoughTube).Should().Be(18m);
        MaterialPricingDefaults.DefaultPurchaseUnitPrice(MaterialType.CriticalFinished).Should().Be(26m);
        MaterialPricingDefaults.DefaultPurchaseUnitPrice(MaterialType.Finished).Should().Be(26m);
        MaterialPricingDefaults.DefaultPurchaseUnitPrice(MaterialType.RoundBar).Should().BeNull();
        MaterialPricingDefaults.DefaultPurchaseUnitPrice(null).Should().BeNull();
    }

    [Fact]
    public void 委外穿孔默认_单价_1_2_元_kg()
    {
        MaterialPricingDefaults.DefaultPricingUnit.Should().Be(PricingUnit.PerKg);
        MaterialPricingDefaults.PiercingUnitPrice.Should().Be(1.2m);
        MaterialPricingDefaults.RoughTubeUnitPrice.Should().Be(18m);
        MaterialPricingDefaults.FinishedUnitPrice.Should().Be(26m);
    }

    // ========== ComputeTotal ==========

    [Fact]
    public void ComputeTotal_PerKg_按重量算_保留2位()
    {
        // 委外穿孔：100kg × 1.2 = 120.00
        MaterialPricingDefaults.ComputeTotal(PricingUnit.PerKg, 1.2m, 100m, null, null).Should().Be(120.00m);
        // 荒管采购：12.5kg × 18 = 225.00
        MaterialPricingDefaults.ComputeTotal(PricingUnit.PerKg, 18m, 12.5m, 30, null).Should().Be(225.00m);
        // 重量为 null → 无法计算
        MaterialPricingDefaults.ComputeTotal(PricingUnit.PerKg, 18m, null, 30, null).Should().BeNull();
    }

    [Fact]
    public void ComputeTotal_PerPiece_按支数算()
    {
        MaterialPricingDefaults.ComputeTotal(PricingUnit.PerPiece, 26m, null, 3, null).Should().Be(78.00m);
        MaterialPricingDefaults.ComputeTotal(PricingUnit.PerPiece, 26m, 100m, null, null).Should().BeNull();
    }

    [Fact]
    public void ComputeTotal_PerMeter_按米数算()
    {
        MaterialPricingDefaults.ComputeTotal(PricingUnit.PerMeter, 10m, null, null, 5.5m).Should().Be(55.00m);
        MaterialPricingDefaults.ComputeTotal(PricingUnit.PerMeter, 10m, null, null, null).Should().BeNull();
    }

    [Fact]
    public void ComputeTotal_单价为空_返回空()
    {
        MaterialPricingDefaults.ComputeTotal(PricingUnit.PerKg, null, 100m, null, null).Should().BeNull();
    }
}
