using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Warehouse;
using MES.Core.Enums;
using MES.Core.Interfaces.Configuration;
using MES.Services.Helpers;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Data;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Warehouse;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Warehouse;

public class InventorySyncService : IInventorySyncService
{
    private readonly AppDbContext _context;
    private readonly IConfigParameterService _configService;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly ILogger<InventorySyncService> _logger;
    private readonly IMemoryCache _cache;

    public InventorySyncService(
        AppDbContext context,
        IConfigParameterService configService,
        IWorkOrderExecutionService workOrderExecutionService,
        ILogger<InventorySyncService> logger,
        IMemoryCache cache)
    {
        _context = context;
        _configService = configService;
        _workOrderExecutionService = workOrderExecutionService;
        _logger = logger;
        _cache = cache;
    }

    private async Task TryRefreshExecutionSummaryAsync(string? workOrderNo)
    {
        if (string.IsNullOrWhiteSpace(workOrderNo)) return;
        try
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(new List<string> { workOrderNo });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工单执行状况刷新失败（不影响主流程）: WorkOrderNo={WorkOrderNo}", workOrderNo);
        }
    }

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        var cacheKey = $"InventoryService:ConfigMap:{category}";
        var map = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _configService.GetConfigMapAsync(category);
        });
        return map?.GetValueOrDefault(key, defaultValue) ?? defaultValue;
    }

    public async Task<SourceOrderValidationResult> ValidateSourceOrderAsync(string sourceOrderNo, string inboundSource, int? sourceOrderSequence = null)
    {
        var result = new SourceOrderValidationResult { IsValid = true };

        if (string.IsNullOrEmpty(sourceOrderNo))
        {
            result.Warnings.Add("来源单号为空");
            result.IsValid = false;
            return result;
        }

        if (inboundSource == InboundSource.Purchase.ToString())
        {
            var order = await _context.PurchaseOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.OrderNo == sourceOrderNo);

            if (order == null)
            {
                result.Warnings.Add($"来源单号「{sourceOrderNo}」在采购订单中不存在");
                result.IsValid = false;
            }
            else
            {
                if (!string.IsNullOrEmpty(order.SourceWorkOrderNo))
                    result.ExpectedWorkOrderNo = order.SourceWorkOrderNo;
                result.MaterialCategory = order.MaterialCategory;
                result.PlantGrade = order.PlantGrade;
                result.Specification = order.Specification;
                if (order.SupplierId > 0)
                {
                    var supplier = await _context.SupplierProfiles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == order.SupplierId);
                    result.SupplierName = supplier?.SupplierName;
                }
            }
        }
        else if (inboundSource == InboundSource.Subcontract.ToString())
        {
            var order = await _context.SubcontractOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrderNo == sourceOrderNo);

            if (order == null)
            {
                result.Warnings.Add($"来源单号「{sourceOrderNo}」在委外订单中不存在");
                result.IsValid = false;
            }
            else if (sourceOrderSequence.HasValue)
            {
                var item = await _context.SubcontractReturnItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.SubcontractOrderId == order.Id && i.Sequence == sourceOrderSequence.Value);

                if (item == null)
                {
                    result.Warnings.Add($"委外单「{sourceOrderNo}」中未找到序号 {sourceOrderSequence.Value} 的明细");
                    result.IsValid = false;
                }
                else
                {
                    result.MaterialCategory = item.MaterialCategory;
                    result.PlantGrade = item.PlantGrade;
                    result.Specification = item.ProcessSpecification;
                    if (!string.IsNullOrEmpty(item.SourceWorkOrderNo))
                        result.ExpectedWorkOrderNo = item.SourceWorkOrderNo;
                    if (order.SupplierId > 0)
                    {
                        var supplier = await _context.SupplierProfiles
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.Id == order.SupplierId);
                        result.SupplierName = supplier?.SupplierName;
                    }
                }
            }
            else
            {
                result.MaterialCategory = order.OutMaterialCategory;
                result.PlantGrade = order.OutPlantGrade;
                result.Specification = order.OutSpecification;
                if (order.SupplierId > 0)
                {
                    var supplier = await _context.SupplierProfiles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == order.SupplierId);
                    result.SupplierName = supplier?.SupplierName;
                }
            }
        }

        return result;
    }

    public async Task<List<string>> ValidateWarehouseWorkOrderNosAsync(int warehouseId)
    {
        var workOrderNos = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.WarehouseId == warehouseId
                     && b.WorkOrderNo != null
                     && b.WorkOrderNo != string.Empty)
            .Select(b => b.WorkOrderNo!)
            .Distinct()
            .ToListAsync();

        if (workOrderNos.Count == 0)
            return new List<string>();

        var existingWorkOrderNos = await _context.WorkOrders
            .AsNoTracking()
            .Where(w => workOrderNos.Contains(w.WorkOrderNo))
            .Select(w => w.WorkOrderNo)
            .ToListAsync();

        var existingSet = existingWorkOrderNos.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return workOrderNos.Where(woNo => !existingSet.Contains(woNo)).ToList();
    }

    public async Task<List<BatchWorkOrderMismatchDto>> GetMismatchedWorkOrderBatchesAsync(int? warehouseId = null)
    {
        var query = _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.WorkOrderNo != null
                     && b.WorkOrderNo != string.Empty
                     && b.WorkOrderNo != "非工单");

        if (warehouseId.HasValue)
            query = query.Where(b => b.WarehouseId == warehouseId.Value);

        var batchWorkOrders = await query
            .Select(b => new { b.Id, b.BatchNo, WorkOrderNo = b.WorkOrderNo ?? string.Empty })
            .ToListAsync();

        if (batchWorkOrders.Count == 0)
            return new List<BatchWorkOrderMismatchDto>();

        var workOrderNos = batchWorkOrders.Select(b => b.WorkOrderNo).Distinct().ToList();
        var existingNos = await _context.WorkOrders
            .AsNoTracking()
            .Where(w => workOrderNos.Contains(w.WorkOrderNo))
            .Select(w => w.WorkOrderNo)
            .ToListAsync();

        var existingSet = existingNos.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return batchWorkOrders
            .Where(b => !existingSet.Contains(b.WorkOrderNo))
            .Select(b => new BatchWorkOrderMismatchDto
            {
                BatchId = b.Id,
                BatchNo = b.BatchNo,
                WorkOrderNo = b.WorkOrderNo
            })
            .ToList();
    }

    public async Task SyncSourceOrdersAsync(List<string> sourceOrderNos)
    {
        if (sourceOrderNos.Count == 0) return;

        var changed = false;

        var purchaseOrders = await _context.PurchaseOrders
            .Where(p => sourceOrderNos.Contains(p.OrderNo))
            .ToListAsync();
        if (purchaseOrders.Count > 0)
        {
            var allBatchData = await _context.InventoryBatches
                .AsNoTracking()
                .Where(b => b.SourceOrderNo != null && sourceOrderNos.Contains(b.SourceOrderNo))
                .GroupBy(b => b.SourceOrderNo)
                .Select(g => new
                {
                    OrderNo = g.Key!,
                    TotalQty = g.Sum(b => b.InitialQuantity),
                    TotalWt = g.Sum(b => b.InitialWeight),
                    MaxDate = g.Max(b => (DateTime?)b.InboundDate)
                })
                .ToListAsync();

            var batchDict = allBatchData.ToDictionary(x => x.OrderNo, x => x, StringComparer.OrdinalIgnoreCase);
            foreach (var order in purchaseOrders)
            {
                if (!batchDict.TryGetValue(order.OrderNo, out var data)) continue;
                order.ReceivedQuantity = data.TotalQty;
                order.ReceivedWeight = data.TotalWt;
                order.LastArrivalDate = data.MaxDate;

                if (!order.IsForceCompleted)
                {
                    if (order.ReceivedQuantity == 0)
                        order.Status = PurchaseOrderStatus.Open;
                    else if (order.Quantity.HasValue && order.ReceivedQuantity >= order.Quantity.Value)
                        order.Status = PurchaseOrderStatus.Completed;
                    else
                        order.Status = PurchaseOrderStatus.Partial;
                }
                changed = true;
            }
        }

        var subcontractOrders = await _context.SubcontractOrders
            .Include(s => s.ReturnItems)
            .Where(s => sourceOrderNos.Contains(s.OrderNo))
            .ToListAsync();
        if (subcontractOrders.Count > 0)
        {
            var subcontractCompleteRatio = await GetConfigAsync("WarehouseThreshold", "SubcontractCompleteRatio", 0.95m);
            var allBatches = await _context.InventoryBatches
                .AsNoTracking()
                .Where(b => b.SourceOrderNo != null && sourceOrderNos.Contains(b.SourceOrderNo))
                .ToListAsync();

            foreach (var order in subcontractOrders)
            {
                var orderBatches = allBatches.Where(b => b.SourceOrderNo == order.OrderNo).ToList();
                order.InQuantity = orderBatches.Sum(b => b.InitialQuantity);
                order.InWeight = orderBatches.Sum(b => b.InitialWeight);

                foreach (var item in order.ReturnItems)
                    SubcontractHelper.SyncReturnItemFromBatches(item, orderBatches);

                if (order.IsForceCompleted)
                {
                    order.Status = SubcontractOrderStatus.Completed;
                    foreach (var item in order.ReturnItems)
                    {
                        item.IsForceCompleted = true;
                        item.ProcessStatus = SubcontractOrderStatus.Completed.ToString();
                    }
                }
                else
                {
                    if (order.InWeight == null || order.InWeight == 0)
                        order.Status = SubcontractOrderStatus.Sent;
                    else if (order.InWeight >= order.OutWeight * subcontractCompleteRatio)
                        order.Status = SubcontractOrderStatus.Completed;
                    else
                        order.Status = SubcontractOrderStatus.PartialReturned;
                }
                changed = true;
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync();

            foreach (var order in subcontractOrders)
                foreach (var item in order.ReturnItems)
                    await TryRefreshExecutionSummaryAsync(item.SourceWorkOrderNo);
        }
    }

}
