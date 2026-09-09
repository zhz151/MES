using MES.Core.Constants;
using MES.Core.Enums;

namespace MES.Core.Helpers;

/// <summary>
/// 物料计价默认值与总价口径（采购/委外唯一事实源）。
/// ⚠️ EF 迁移回填 SQL（AddPurchaseAndSubcontractPricing 中的 18/26/1.2；AddSectionOutsourcePricing 中的 1.4/0.8）
/// 常量与族判断清单须与本类人工保持一致（SQL 无法引用 C#），修改本类时务必同步迁移脚本与注释。
/// </summary>
public static class MaterialPricingDefaults
{
    /// <summary>采购/委外默认计价单位：元/Kg</summary>
    public const PricingUnit DefaultPricingUnit = PricingUnit.PerKg;

    /// <summary>工段委外默认单价（元/kg）：冷轧拔工段</summary>
    public const decimal ColdRollDrawSectionUnitPrice = 1.4m;

    /// <summary>工段委外默认单价（元/kg）：其余工段</summary>
    public const decimal OtherSectionUnitPrice = 0.8m;

    /// <summary>荒管族采购单价（元/kg）</summary>
    public const decimal RoughTubeUnitPrice = 18m;

    /// <summary>成品族采购单价（元/kg）</summary>
    public const decimal FinishedUnitPrice = 26m;

    /// <summary>委外穿孔加工单价（元/kg）</summary>
    public const decimal PiercingUnitPrice = 1.2m;

    /// <summary>荒管族：RoughTube / 次品荒管</summary>
    public static bool IsRoughTubeFamily(MaterialType category) =>
        category is MaterialType.RoughTube or MaterialType.DefectRoughTube;

    /// <summary>成品族：备料成品/订单成品/临界成品/次品成品/订成-非交付态</summary>
    public static bool IsFinishedFamily(MaterialType category) =>
        category is MaterialType.Finished or MaterialType.OrderFinished or MaterialType.CriticalFinished
            or MaterialType.DefectFinished or MaterialType.SpecialDeliveryStatus;

    /// <summary>
    /// 采购单默认单价（元/kg）：荒管族→18，成品族→26，其余分类未定价返回 null（待出现再定）。
    /// </summary>
    public static decimal? DefaultPurchaseUnitPrice(MaterialType? category)
    {
        if (category == null) return null;
        if (IsRoughTubeFamily(category.Value)) return RoughTubeUnitPrice;
        if (IsFinishedFamily(category.Value)) return FinishedUnitPrice;
        return null;
    }

    /// <summary>
    /// 工段委外默认单价（元/kg）：冷轧拔工段→1.4，其余工段→0.8。
    /// 厂内（本厂车间 IsWorkshop）行无价由调用方置空，不走本方法。
    /// </summary>
    public static decimal? DefaultSectionOutsourceUnitPrice(string? sectionKey)
        => sectionKey == SectionKeys.ColdRollDraw ? ColdRollDrawSectionUnitPrice : OtherSectionUnitPrice;

    /// <summary>
    /// 按计价单位自动计算总价（元，保留 2 位小数）：PerKg→kg、PerPiece→支、PerMeter→米。
    /// 单价或对应取量为空返回 null；采购无米数时 PerMeter 亦返回 null（按实收走前端/服务端留待人工）。
    /// </summary>
    public static decimal? ComputeTotal(PricingUnit unit, decimal? unitPrice, decimal? weightKg, int? qty, decimal? meters)
    {
        if (unitPrice == null) return null;

        decimal? amount = unit switch
        {
            PricingUnit.PerKg => weightKg,
            PricingUnit.PerPiece => qty,
            PricingUnit.PerMeter => meters,
            _ => null
        };
        if (amount == null) return null;

        return decimal.Round(unitPrice.Value * amount.Value, 2, MidpointRounding.AwayFromZero);
    }
}
