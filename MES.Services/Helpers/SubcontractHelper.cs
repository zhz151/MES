using MES.Core.Enums;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Warehouse;

namespace MES.Services.Helpers;

/// <summary>
/// 委外模块共享逻辑，消除 SubcontractOrderService 与 InventorySyncService 之间的重复。
/// </summary>
public static class SubcontractHelper
{
    /// <summary>
    /// 从退货出库记录 + 回收入库仓库批，聚合出「委外单号(OrdinalIgnoreCase) → 序号 → 退货量/退货重」。
    /// returnOutbounds 需已按 OutboundType==ReturnOut 且 ReturnSourceBatchNo 非空过滤；
    /// batches 提供 SourceOrderNo/SourceOrderSequence（SourceOrderSequence 为空 → 归入序号 0，不匹配任何子项序号 1..n）。
    /// 供 SubcontractOrderService 与 InventorySyncService 共用，保证退货口径一致。
    /// </summary>
    public static Dictionary<string, Dictionary<int, (int Quantity, decimal Weight)>> AggregateReturnsBySequence(
        IReadOnlyCollection<OutboundRecord> returnOutbounds,
        IEnumerable<(string BatchNo, string? SourceOrderNo, int? SourceOrderSequence)> batches)
    {
        var batchNoToKey = new Dictionary<string, (string OrderNo, int Sequence)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (batchNo, orderNo, seq) in batches)
        {
            if (string.IsNullOrEmpty(batchNo) || string.IsNullOrEmpty(orderNo)) continue;
            batchNoToKey[batchNo] = (orderNo, seq ?? 0);
        }

        var result = new Dictionary<string, Dictionary<int, (int Quantity, decimal Weight)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in returnOutbounds)
        {
            if (string.IsNullOrEmpty(o.ReturnSourceBatchNo)) continue;
            if (!batchNoToKey.TryGetValue(o.ReturnSourceBatchNo!, out var key)) continue;
            if (!result.TryGetValue(key.OrderNo, out var bySeq))
            {
                bySeq = new Dictionary<int, (int, decimal)>();
                result[key.OrderNo] = bySeq;
            }
            if (!bySeq.TryGetValue(key.Sequence, out var cur))
                bySeq[key.Sequence] = (o.OutboundQuantity, o.OutboundWeight);
            else
                bySeq[key.Sequence] = (cur.Quantity + o.OutboundQuantity, cur.Weight + o.OutboundWeight);
        }
        return result;
    }

    /// <summary>
    /// 根据库存批次同步委外回收项的数量/重量，并自动重算状态。
    /// batches 需已按 SourceOrderNo 过滤（由调用方 SyncSourceOrdersAsync 保证）。
    /// 匹配键为 SourceOrderSequence → SubcontractReturnItem.Sequence。
    /// returnBySequence（可选）= 序号 → 退货量/退货重，用于状态判定时按「净回收 = 回收 - 退货」计算；
    /// ReturnedQuantity/ReturnedWeight 实体值仍存「总回收」（不扣退货，退货另列显示）。
    /// </summary>
    public static void SyncReturnItemFromBatches(SubcontractReturnItem item, List<InventoryBatch> batches, decimal overRatio, decimal overDeviation,
        IReadOnlyDictionary<int, (int Quantity, decimal Weight)>? returnBySequence = null)
    {
        var itemBatches = batches
            .Where(b => b.SourceOrderSequence.HasValue && b.SourceOrderSequence.Value == item.Sequence)
            .ToList();

        item.ReturnedQuantity = itemBatches.Sum(b => b.InitialQuantity);
        item.ReturnedWeight = itemBatches.Sum(b => b.InitialWeight);

        var returnQuantity = 0;
        var returnWeight = 0m;
        if (returnBySequence != null && returnBySequence.TryGetValue(item.Sequence, out var ret))
        {
            returnQuantity = ret.Quantity;
            returnWeight = ret.Weight;
        }

        if (!item.IsForceCompleted)
            RecalcReturnItemStatus(item, overRatio, overDeviation, returnQuantity, returnWeight);
    }

    /// <summary>
    /// 根据实际净回收量（回收 - 退货）计算子项状态：Sent / PartialReturned / Completed / OverReceived。
    /// 超量判定按重量口径：净回收重量&gt;需求重量×超额比率 且 超出量&gt;超额偏差 → OverReceived（优先于完成判定）。
    /// </summary>
    public static void RecalcReturnItemStatus(SubcontractReturnItem item, decimal overRatio, decimal overDeviation, int returnQuantity = 0, decimal returnWeight = 0m)
    {
        var netQuantity = Math.Max(0, item.ReturnedQuantity - returnQuantity);
        var netWeight = Math.Max(0, item.ReturnedWeight - returnWeight);

        if (netQuantity <= 0 && netWeight <= 0)
            item.ProcessStatus = SubcontractOrderStatus.Sent.ToString();
        else if (item.RequiredWeight.HasValue
                 && netWeight > item.RequiredWeight.Value * overRatio
                 && netWeight - item.RequiredWeight.Value > overDeviation)
            item.ProcessStatus = SubcontractOrderStatus.OverReceived.ToString();
        else if (item.RequiredQuantity.HasValue && netQuantity >= item.RequiredQuantity.Value)
            item.ProcessStatus = SubcontractOrderStatus.Completed.ToString();
        else if (item.RequiredWeight.HasValue && netWeight >= item.RequiredWeight.Value)
            item.ProcessStatus = SubcontractOrderStatus.Completed.ToString();
        else
            item.ProcessStatus = SubcontractOrderStatus.PartialReturned.ToString();
    }
}
