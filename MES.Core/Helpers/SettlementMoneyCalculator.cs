// 文件路径: MES.Core/Helpers/SettlementMoneyCalculator.cs
namespace MES.Core.Helpers;

/// <summary>
/// 订单结算金额折算器（客户往来统计与报表业务总况共用同一口径，防两条代码路径金额漂移）。
/// 口径（结算分治）：
/// 1. 过磅(Weighing)：按实称公斤计价、不封顶，超产照付；
/// 2. 固定池(理算 Theoretical / 过磅-负 WeighingNegative)：发货→库存阶梯认领，公斤认领池上限=合同池重，超产余料只显公斤不计价。
/// 整单发货/库存/在制金额按「入库/出库/库存」重量在两池间按合同重占比切片近似折算（与逐池分别认领等价）。
/// </summary>
public static class SettlementMoneyCalculator
{
    /// <summary>
    /// 整单金额折算（kg→元）。
    /// </summary>
    /// <param name="inboundKg">成品入库重量(kg，整单)</param>
    /// <param name="outboundKg">成品出库重量(kg，整单)</param>
    /// <param name="stockKg">成品库存重量(kg，整单)</param>
    /// <param name="weighPoolWeightKg">过磅池合同重量(kg)</param>
    /// <param name="weighPoolMoney">过磅池总价(元)</param>
    /// <param name="fixedPoolWeightKg">固定池(理算+过磅-负)合同重量(kg)</param>
    /// <param name="fixedPoolMoney">固定池总价(元)</param>
    public static (decimal ShipMoney, decimal StockMoney, decimal WipMoney) SplitOrder(
        decimal inboundKg, decimal outboundKg, decimal stockKg,
        decimal weighPoolWeightKg, decimal weighPoolMoney,
        decimal fixedPoolWeightKg, decimal fixedPoolMoney)
    {
        var totalWeight = weighPoolWeightKg + fixedPoolWeightKg;
        if (totalWeight <= 0m)
            return (0m, 0m, 0m);

        decimal ship = 0m, stock = 0m, wip = 0m;
        if (weighPoolWeightKg > 0m)
        {
            var (sm, stm, wm) = AllocatePool(
                inboundKg * weighPoolWeightKg / totalWeight,
                outboundKg * weighPoolWeightKg / totalWeight,
                stockKg * weighPoolWeightKg / totalWeight,
                weighPoolWeightKg, weighPoolMoney, isWeighing: true);
            ship += sm; stock += stm; wip += wm;
        }
        if (fixedPoolWeightKg > 0m)
        {
            var (sm, stm, wm) = AllocatePool(
                inboundKg * fixedPoolWeightKg / totalWeight,
                outboundKg * fixedPoolWeightKg / totalWeight,
                stockKg * fixedPoolWeightKg / totalWeight,
                fixedPoolWeightKg, fixedPoolMoney, isWeighing: false);
            ship += sm; stock += stm; wip += wm;
        }
        return (ship, stock, wip);
    }

    /// <summary>
    /// 单结算池金额折算。
    /// 过磅(Weighing)：计价重量=实际公斤，不封顶（超产照付）；
    /// 固定池(理算/过磅-负)：发货→库存阶梯认领、公斤认领池上限=合同池重，超产余料只显公斤不计价。
    /// </summary>
    private static (decimal ShipMoney, decimal StockMoney, decimal WipMoney) AllocatePool(
        decimal inboundShare, decimal outboundShare, decimal stockShare,
        decimal poolWeight, decimal poolMoney, bool isWeighing)
    {
        if (poolWeight <= 0m)
            return (0m, 0m, 0m);

        var rate = poolMoney / poolWeight;
        var wipMoney = Math.Max(poolWeight - inboundShare, 0m) * rate;

        if (isWeighing)
            return (outboundShare * rate, stockShare * rate, wipMoney);

        // 固定池：发货先认领合同，库存只能吃发货后的合同余量；超产部分不计价
        var shippedContract = Math.Min(outboundShare, poolWeight);
        var stockContract = Math.Min(stockShare, Math.Max(poolWeight - shippedContract, 0m));
        return (shippedContract * rate, stockContract * rate, wipMoney);
    }
}
