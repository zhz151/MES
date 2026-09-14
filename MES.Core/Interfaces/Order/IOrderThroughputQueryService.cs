using MES.Core.DTOs.Order;

namespace MES.Core.Interfaces.Order;

/// <summary>
/// 投料产出总况查询服务（只读）：按「订单完成月」聚合各完成订单的投料与产出。
/// 口径与订单进度树（<see cref="IOrderProgressQueryService"/>）完结主号「投料产出」分支一致：
/// 投料 = Σ 生产批次工艺卡领料重 InputWeight（排除返整/委外生产/对外加工），产出 = 各仓库实收，退货按次品库 ReturnOut 扣减。
/// 支持两档生产类型范围（见 MES.Core.Constants.ProductionScopeKeys）：全部四种 / 纯生产两种。
/// </summary>
public interface IOrderThroughputQueryService
{
    /// <summary>
    /// 获取投料产出总况月度行（仅返回有完成订单的月，按月升序）。
    /// 日期区间与默认窗口二选一：<paramref name="dateFrom"/> / <paramref name="dateTo"/> 任一端有值即按
    /// 「订单真实完成日落入 [起, 止] 闭区间」取数（可查近 12 个月之外的历史区间），且**整个区间聚合为 1 行**
    /// （该行 <c>Month</c> 为所选范围文本）；两端皆空则沿用默认「最近 12 个月（含当月）」窗口、按完成月分行。
    /// 注：各列数值为命中订单的**全生命周期合计**（与完成日不相关），区间外的投料/入库量也会计入该行。
    /// </summary>
    /// <param name="scope">生产类型范围（ProductionScopeKeys.All / Pure / 单一生产类型 Key），空值或未知值回退 All</param>
    /// <param name="dateFrom">完成日期区间-起（yyyy-MM-dd，含当天；为空表示不限起点）</param>
    /// <param name="dateTo">完成日期区间-止（yyyy-MM-dd，含当天；为空表示不限终点）</param>
    Task<OrderThroughputSummaryDto> GetMonthlySummaryAsync(string? scope, DateTime? dateFrom = null, DateTime? dateTo = null);
}
