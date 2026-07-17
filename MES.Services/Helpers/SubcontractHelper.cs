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
    /// 根据库存批次同步委外回收项的数量/重量，并自动重算状态。
    /// </summary>
    public static void SyncReturnItemFromBatches(SubcontractReturnItem item, List<InventoryBatch> batches)
    {
        if (string.IsNullOrEmpty(item.SourceWorkOrderNo)) return;

        var itemBatches = batches
            .Where(b => b.WorkOrderNo == item.SourceWorkOrderNo)
            .ToList();

        item.ReturnedQuantity = itemBatches.Sum(b => b.InitialQuantity);
        item.ReturnedWeight = itemBatches.Sum(b => b.InitialWeight);

        if (!item.IsForceCompleted)
            RecalcReturnItemStatus(item);
    }

    /// <summary>
    /// 根据实际回收量计算子项状态：Sent / PartialReturned / Completed。
    /// </summary>
    public static void RecalcReturnItemStatus(SubcontractReturnItem item)
    {
        if (item.ReturnedQuantity <= 0 && item.ReturnedWeight <= 0)
            item.ProcessStatus = SubcontractOrderStatus.Sent.ToString();
        else if (item.RequiredQuantity.HasValue && item.ReturnedQuantity >= item.RequiredQuantity.Value)
            item.ProcessStatus = SubcontractOrderStatus.Completed.ToString();
        else if (item.RequiredWeight.HasValue && item.ReturnedWeight >= item.RequiredWeight.Value)
            item.ProcessStatus = SubcontractOrderStatus.Completed.ToString();
        else
            item.ProcessStatus = SubcontractOrderStatus.PartialReturned.ToString();
    }
}
