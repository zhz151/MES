using MES.Core.Constants;

namespace MES.Core.DTOs.Order;

/// <summary>
/// 投料产出总况（订单列表页卡片数据源）。行维度 = 订单完成月
/// —— 订单完成日 = 该订单全部主号完成（ScheduleStage==1）时各主号 WarehousingEndDate 的最大值
/// （与订单列表「预计完成」列主号完成档的绿色 Chip 同源，即真实入库完成时点）。
/// </summary>
public class OrderThroughputSummaryDto
{
    /// <summary>口径（<see cref="ProductionScopeKeys.All"/> 全部四种 / <see cref="ProductionScopeKeys.Pure"/> 纯生产两种）</summary>
    public string Scope { get; set; } = ProductionScopeKeys.All;

    /// <summary>
    /// 月度行（仅含窗口内有完成订单的月，按月份升序；无数据的月不返回）。
    /// **日期区间模式下一次只返回 1 行**——整个所选区间跨订单聚合为单行，
    /// 该行 <see cref="OrderThroughputMonthDto.Month"/> 为所选范围文本。
    /// </summary>
    public List<OrderThroughputMonthDto> Months { get; set; } = [];
}

/// <summary>投料产出总况的单月行（日期区间模式下为区间聚合行）</summary>
public class OrderThroughputMonthDto
{
    /// <summary>
    /// 默认模式 = 完成月（`yyyy-MM`）；**日期区间模式 = 区间聚合行的范围文本**
    /// （`yyyy-MM-dd - yyyy-MM-dd`，未指定的端点显示「不限」）。
    /// </summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>订单数（该完成月中在本生产类型范围内有生产批次的订单数，与本行各列同口径）</summary>
    public int OrderCount { get; set; }

    /// <summary>生产投料（kg，已扣退货）</summary>
    public decimal InputWeight { get; set; }

    /// <summary>订单成品入库（kg）</summary>
    public decimal OrderFinishedWeight { get; set; }

    /// <summary>余库料入库（kg）</summary>
    public decimal SurplusWeight { get; set; }

    /// <summary>次品入库（kg，已扣退货）</summary>
    public decimal DefectWeight { get; set; }

    /// <summary>备料成品（kg）</summary>
    public decimal PreparedWeight { get; set; }

    /// <summary>退货（kg，次品库 ReturnOut 出库量，供核对；已分别从「生产投料」「次品入库」中扣减）</summary>
    public decimal ReturnWeight { get; set; }

    /// <summary>投料产出率（0~1 比率；分母「生产投料净量」≤0 时为 null，前端不渲染该比值）</summary>
    public decimal? InputOutputRate { get; set; }

    /// <summary>产出成品比（0~1 比率；分母「总产出」≤0 时为 null，前端不渲染该比值）</summary>
    public decimal? FinishedOutputRate { get; set; }
}
