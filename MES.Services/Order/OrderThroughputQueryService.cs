using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Order;
using MES.Core.Enums;
using MES.Core.Interfaces.Order;
using MES.Data;
using MES.Data.Entities.WorkOrder;

namespace MES.Services.Order;

/// <summary>
/// 投料产出总况查询服务（只读）。口径与订单进度树（<see cref="OrderProgressQueryService"/>）完结主号
/// 「投料产出」分支同源，差别只在聚合维度：本服务按「订单完成月」跨订单聚合。
///
/// 1) 订单完成月 = 该订单**全部主号完成**（订单级 ScheduleStage==1，归并序同 OrderService.RefreshByOrderIdAsync）
///    时各主号 WarehousingEndDate 的最大值 —— 即订单列表「执行关注=主号完成」档下「预计完成」列
///    （绿色 Chip）的同一取值，业务上是真实入库完成时点（WarehousingEndDate = Max(InventoryBatch.InboundDate)）。
/// 2) 生产投料 = Σ 该订单生产批次工艺卡领料重 InputWeight，按生产类型范围过滤（不含返整/委外生产/对外加工）。
/// 3) 订单成品入库：
///    - 「全部」口径走 InventoryBatch 自带订单号直取 + MaterialType=OrderFinished（交付态），与订单进度树完全一致；
///    - 其余口径（纯生产/单一生产类型）经 InventoryBatch.ProductionBatchNo 反查生产批次后按生产类型过滤，
///      且物料类型取 OrderFinished + SpecialDeliveryStatus（**不分交付态**）——因为厂内自产（荒管/在制）
///      对 U 型管类订单只能做到「订成-非交付态」，只取交付态会把自产的成品整列取空；交付态那部分由外购/委外
///      批次产出，会被生产类型过滤自然排除，故不会与「全部」口径的交付态重复计入。
/// 4) 余库料入库 / 次品入库 / 备料成品：三类入库行自身不带订单号，一律经 ProductionBatchNo 反查生产批次
///    取订单归属，并按生产类型范围过滤。
/// 5) 退货 = 次品库(DEFECT) 出库类型 ReturnOut，经生产批号反查归属订单后按生产类型范围过滤；
///    按用户拍板，退货分别从「生产投料」与「次品入库」中扣减，同时单列展示供核对。
/// 6) 订单数 = 该完成月中**在本生产类型范围内有生产批次**的订单数（与行内其它列同口径，非「该月完成订单总数」）；
///    「全部」口径下 = 该月完成且有「荒管/在制/库存/外购」四类生产批次的订单数（当前真库与该月完成订单数一致）。
///    因此某月若该口径下无任何相关订单，则该月整行不显示。
/// </summary>
public class OrderThroughputQueryService : IOrderThroughputQueryService
{
    private readonly AppDbContext _context;

    public OrderThroughputQueryService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>默认报表窗口：最近 12 个月（含当月）；未传日期区间时生效</summary>
    private const int WindowMonths = 12;

    /// <summary>次品库叶子物料类型（与 InventoryMaterialTypes.WarehouseAllowedTypes["DEFECT"] 一致）</summary>
    private static readonly string[] DefectMaterialTypeOrder =
    [
        InventoryMaterialTypes.DefectRoundBar,
        InventoryMaterialTypes.DefectRoughTube,
        InventoryMaterialTypes.DefectSemi,
        InventoryMaterialTypes.DefectFinished,
        InventoryMaterialTypes.DefectWIP,
        InventoryMaterialTypes.Scrap,
    ];

    public async Task<OrderThroughputSummaryDto> GetMonthlySummaryAsync(
        string? scope, DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        var key = ProductionScopeKeys.Resolve(scope);
        var types = ProductionScopeKeys.TypesOf(key);
        // 「全部」口径只计交付态成品（与订单进度树同源）；其余口径计「该范围实际产出的订单成品」，不分交付态
        var includeNonDelivered = key != ProductionScopeKeys.All;

        var result = new OrderThroughputSummaryDto { Scope = key };

        // 1) 完成订单 → 完成日（订单级档位归并序与 OrderService.RefreshByOrderIdAsync 一致，确保与订单列表绿 Chip 同源）
        var wes = await _context.WorkOrderExecutionSummaries.AsNoTracking()
            .Select(e => new { e.SalesOrderNo, e.ScheduleStage, e.WarehousingEndDate })
            .ToListAsync();

        // 日期区间模式：任一端有值即启用（按订单真实完成日落入 [from, to] 闭区间）；两端皆空 = 默认最近 12 个月
        var rangeMode = dateFrom.HasValue || dateTo.HasValue;
        var rangeFrom = (dateFrom ?? DateTime.MinValue).Date;
        var rangeTo = (dateTo ?? DateTime.MaxValue).Date;

        var today = DateTime.Today;
        var windowStart = new DateTime(today.Year, today.Month, 1).AddMonths(-(WindowMonths - 1));

        var doneByOrder = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in wes.GroupBy(e => e.SalesOrderNo ?? "", StringComparer.OrdinalIgnoreCase))
        {
            if (group.Key.Length == 0)
                continue;

            var stage = group.Any(e => e.ScheduleStage == 0) ? 0
                : group.Any(e => e.ScheduleStage == 2) ? 2
                : group.Any(e => e.ScheduleStage == 3) ? 3
                : group.Any(e => e.ScheduleStage == 4) ? 4
                : 1;
            if (stage != 1)
                continue;

            var doneAt = group.Max(e => e.WarehousingEndDate);
            if (!doneAt.HasValue)
                continue;

            if (rangeMode)
            {
                // 按订单「真实完成日」直比 [起, 止] 闭区间（2026-09-14 修正）：
                // 原实现以「完成月月首」锚定，起/止日非月初月末时会整月误删/误纳 ——
                // 例 起=2026-01-15 会把 1 月 15 日之后完成的订单因「月首 01-01 < 01-15」整月剔除。
                var doneDate = doneAt.Value.Date;
                if (doneDate < rangeFrom || doneDate > rangeTo)
                    continue;
            }
            else if (doneAt.Value < windowStart)
            {
                continue;
            }

            doneByOrder[group.Key] = doneAt.Value;
        }

        if (doneByOrder.Count == 0)
            return result;

        var orders = doneByOrder.Keys.ToList();

        // 2) 生产投料：生产批次自带订单号，零 join
        var inputRows = await _context.ProductionBatches.AsNoTracking()
            .Where(b => b.SalesOrderNo != null && orders.Contains(b.SalesOrderNo)
                && b.ProductionType != null && types.Contains(b.ProductionType!))
            .Select(b => new { b.SalesOrderNo, Weight = b.InputWeight ?? 0m })
            .ToListAsync();

        // 3) 订单成品入库（「全部」直取订单号；其余口径按生产批号反查 + 生产类型过滤，且含非交付态）
        var finishedRows = includeNonDelivered
            ? await (from ib in _context.InventoryBatches.AsNoTracking()
                     join pb in _context.ProductionBatches.AsNoTracking() on ib.ProductionBatchNo equals pb.BatchNo
                     where pb.SalesOrderNo != null && orders.Contains(pb.SalesOrderNo)
                         && pb.ProductionType != null && types.Contains(pb.ProductionType!)
                         && (ib.MaterialType == InventoryMaterialTypes.OrderFinished
                             || ib.MaterialType == InventoryMaterialTypes.SpecialDeliveryStatus)
                     select new { pb.SalesOrderNo, Weight = ib.InitialWeight })
                     .ToListAsync()
            : await (from ib in _context.InventoryBatches.AsNoTracking()
                     where ib.SalesOrderNo != null && orders.Contains(ib.SalesOrderNo)
                         && ib.MaterialType == InventoryMaterialTypes.OrderFinished
                     select new { ib.SalesOrderNo, Weight = ib.InitialWeight })
                     .ToListAsync();

        // 4) 余库料 / 次品 / 备料：三类入库行不带订单号，经生产批号反查归属后按生产类型过滤
        var defectTypes = DefectMaterialTypeOrder; // 局部集合供 EF 参数化
        var outputRows = await (from ib in _context.InventoryBatches.AsNoTracking()
                                join w in _context.Warehouses.AsNoTracking() on ib.WarehouseId equals w.Id
                                join pb in _context.ProductionBatches.AsNoTracking() on ib.ProductionBatchNo equals pb.BatchNo
                                where ib.ProductionBatchNo != null
                                    && pb.SalesOrderNo != null && orders.Contains(pb.SalesOrderNo)
                                    && pb.ProductionType != null && types.Contains(pb.ProductionType!)
                                    && ((w.Code == WarehouseCodes.WorkInProgress && ib.MaterialType == InventoryMaterialTypes.Surplus)
                                        || (w.Code == WarehouseCodes.Defect && defectTypes.Contains(ib.MaterialType))
                                        || (w.Code == WarehouseCodes.FinishedGoods && ib.MaterialType == InventoryMaterialTypes.Finished))
                                select new { pb.SalesOrderNo, w.Code, ib.MaterialType, ib.InitialWeight })
                                .ToListAsync();

        // 5) 退货：次品库 ReturnOut 出库，同样经生产批号反查归属
        var returnRows = await (from o in _context.OutboundRecords.AsNoTracking()
                                join ib in _context.InventoryBatches.AsNoTracking() on o.InventoryBatchId equals ib.Id
                                join w in _context.Warehouses.AsNoTracking() on ib.WarehouseId equals w.Id
                                join pb in _context.ProductionBatches.AsNoTracking() on ib.ProductionBatchNo equals pb.BatchNo
                                where o.OutboundType == OutboundType.ReturnOut
                                    && w.Code == WarehouseCodes.Defect
                                    && ib.ProductionBatchNo != null
                                    && pb.SalesOrderNo != null && orders.Contains(pb.SalesOrderNo)
                                    && pb.ProductionType != null && types.Contains(pb.ProductionType!)
                                select new { pb.SalesOrderNo, o.OutboundWeight })
                                .ToListAsync();

        // 6) 按订单聚合，再滚入完成月
        var inputByOrder = SumByOrder(inputRows, r => r.SalesOrderNo, r => r.Weight);
        var finishedByOrder = SumByOrder(finishedRows, r => r.SalesOrderNo, r => r.Weight);
        var returnByOrder = SumByOrder(returnRows, r => r.SalesOrderNo, r => r.OutboundWeight);

        var surplusByOrder = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var defectByOrder = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var preparedByOrder = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in outputRows)
        {
            var order = r.SalesOrderNo ?? "";
            var bucket = r.Code == WarehouseCodes.WorkInProgress ? surplusByOrder
                : r.Code == WarehouseCodes.Defect ? defectByOrder
                : preparedByOrder;
            bucket[order] = bucket.GetValueOrDefault(order) + r.InitialWeight;
        }

        // 区间模式：整个所选区间聚合为**同一行**（首列显示所选范围）；默认模式：按「完成月」分行
        var byMonth = new Dictionary<string, OrderThroughputMonthDto>(StringComparer.Ordinal);
        foreach (var (order, doneAt) in doneByOrder)
        {
            var month = rangeMode ? RangeRowKey : doneAt.ToString("yyyy-MM");
            if (!byMonth.TryGetValue(month, out var row))
            {
                row = new OrderThroughputMonthDto { Month = month };
                byMonth[month] = row;
            }

            // 退货按用户拍板分别从「生产投料」与「次品入库」中扣减（退货列保留原值供核对）
            var returns = returnByOrder.GetValueOrDefault(order);
            // 订单数只计「本口径内有生产批次」的订单（inputByOrder 的键集恰好 = 本口径下有生产批次的订单集合，
            // 因投料查询已按生产类型范围过滤且 InputWeight 为空也会成行）；产出/退货经同一批次反查，必为该集合子集。
            if (inputByOrder.ContainsKey(order))
                row.OrderCount++;
            row.InputWeight += inputByOrder.GetValueOrDefault(order) - returns;
            row.OrderFinishedWeight += finishedByOrder.GetValueOrDefault(order);
            row.SurplusWeight += surplusByOrder.GetValueOrDefault(order);
            row.DefectWeight += defectByOrder.GetValueOrDefault(order) - returns;
            row.PreparedWeight += preparedByOrder.GetValueOrDefault(order);
            row.ReturnWeight += returns;
        }

        // 7) 比率：投料产出率 = 总产出 ÷ 生产投料净量；产出成品比 = 订单成品 ÷ 总产出（分母 ≤0 不渲染）
        foreach (var row in byMonth.Values)
        {
            var totalOutput = row.OrderFinishedWeight + row.SurplusWeight + row.DefectWeight + row.PreparedWeight;
            row.InputOutputRate = row.InputWeight > 0m ? totalOutput / row.InputWeight : null;
            row.FinishedOutputRate = totalOutput > 0m ? row.OrderFinishedWeight / totalOutput : null;
        }

        // 该口径下无相关订单的月整行不显示（非全部口径下某月可能全是外购/委外等不在范围内的批次）
        result.Months = byMonth.Values
            .Where(r => r.OrderCount > 0)
            .OrderBy(r => r.Month, StringComparer.Ordinal)
            .ToList();

        // 区间模式：聚合行首列改显示所选范围文本（未指定端显示「不限」）
        if (rangeMode && byMonth.TryGetValue(RangeRowKey, out var rangeRow))
            rangeRow.Month = BuildRangeText(dateFrom, dateTo);

        return result;
    }

    /// <summary>区间模式聚合行的临时键（不参与排序展示，最终替换为范围文本）</summary>
    private const string RangeRowKey = "__range__";

    /// <summary>所选范围文本（如 `2026-02-01 - 2026-09-14`；未指定的端点显示「不限」）</summary>
    private static string BuildRangeText(DateTime? from, DateTime? to)
        => $"{(from.HasValue ? from.Value.ToString("yyyy-MM-dd") : "不限")} - {(to.HasValue ? to.Value.ToString("yyyy-MM-dd") : "不限")}";

    /// <summary>按订单号累加重量（订单号跨表比较后分桶，内存比较用 OrdinalIgnoreCase）</summary>
    private static Dictionary<string, decimal> SumByOrder<T>(
        IEnumerable<T> rows, Func<T, string?> orderSelector, Func<T, decimal> weightSelector)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            var order = orderSelector(r) ?? "";
            result[order] = result.GetValueOrDefault(order) + weightSelector(r);
        }
        return result;
    }
}
