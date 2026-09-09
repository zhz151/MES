namespace MES.Core.DTOs.Order;

/// <summary>
/// 订单接单·出库及现负荷汇总 DTO（按本年 1~12 月）
/// </summary>
public class OrderInOutSummaryDto
{
    /// <summary>汇总年份</summary>
    public int Year { get; set; }

    /// <summary>月度标签（如 "2026年1月"~"2026年12月"，共 12 项）</summary>
    public string[] MonthLabels { get; set; } = new string[12];

    /// <summary>接单量（本年签订、排除已取消订单的合同重量，kg，按签订月份）</summary>
    public decimal[] OrderWeightByMonth { get; set; } = new decimal[12];

    /// <summary>接单额（与接单量同源订单的项次总价合计，元，按签订月份；未计价订单计 0）</summary>
    public decimal[] OrderAmountByMonth { get; set; } = new decimal[12];

    /// <summary>出库量（本年成品销售出库 SalesOut 重量，kg，按出库月份）</summary>
    public decimal[] OutboundWeightByMonth { get; set; } = new decimal[12];

    /// <summary>出库额（与出库量同源，按订单结算分治口径折算，元，按出库月份；无档案/未计价订单计 0）</summary>
    public decimal[] OutboundAmountByMonth { get; set; } = new decimal[12];

    /// <summary>成品库存-完工（执行关注=主号完成 的订单成品库存量，kg，当前存量）</summary>
    public decimal FinishedStockCompleted { get; set; }

    /// <summary>成品库存-完工金额（对应订单按结算分治口径折算的库存额，元，当前存量）</summary>
    public decimal FinishedStockCompletedAmount { get; set; }

    /// <summary>成品库存-未完工（执行关注&lt;&gt;主号完成 的订单成品库存量，kg，当前存量）</summary>
    public decimal FinishedStockUncompleted { get; set; }

    /// <summary>成品库存-未完工金额（对应订单按结算分治口径折算的库存额，元，当前存量）</summary>
    public decimal FinishedStockUncompletedAmount { get; set; }

    /// <summary>订单负荷量-实时（执行关注&lt;&gt;主号完成 的订单合同重量 − 成品库存-未完工，kg，当前存量）</summary>
    public decimal TurnoverTotal { get; set; }

    /// <summary>订单负荷量-实时金额（未完工订单项次总价合计 − 未完工库存额，元，当前存量）</summary>
    public decimal TurnoverAmount { get; set; }
}
