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

    /// <summary>出库量（本年成品销售出库 SalesOut 重量，kg，按出库月份）</summary>
    public decimal[] OutboundWeightByMonth { get; set; } = new decimal[12];

    /// <summary>成品库存-完工（执行关注=主号完成 的订单成品库存量，kg，当前存量）</summary>
    public decimal FinishedStockCompleted { get; set; }

    /// <summary>成品库存-未完工（执行关注&lt;&gt;主号完成 的订单成品库存量，kg，当前存量）</summary>
    public decimal FinishedStockUncompleted { get; set; }

    /// <summary>订单负荷量-实时（执行关注&lt;&gt;主号完成 的订单合同重量 − 成品库存-未完工，kg，当前存量）</summary>
    public decimal TurnoverTotal { get; set; }
}
