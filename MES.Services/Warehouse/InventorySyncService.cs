using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Warehouse;
using MES.Core.Enums;
using MES.Core.Constants;
using MES.Core.Helpers;
using MES.Core.Interfaces.Configuration;
using MES.Services.Helpers;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Data;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Batch;
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

    /// <summary>
    /// 旧枚举名 → 新枚举名映射（物料枚举合并/重命名后，DB 可能仍存旧值）
    /// </summary>
    private static readonly Dictionary<string, string> MaterialTypeNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OrderFinishedProduct"] = InventoryMaterialTypes.OrderFinished,
        ["PreparedMaterial"] = InventoryMaterialTypes.Finished,
        ["PreparedFinished"] = InventoryMaterialTypes.Finished,
        ["SurplusStock"] = InventoryMaterialTypes.Surplus,
        ["IntermediateProduct"] = InventoryMaterialTypes.SemiFinished,
        ["StockFinished"] = InventoryMaterialTypes.Finished,
    };

    /// <summary>映射旧 MaterialType 枚举名为新名称</summary>
    private static string MapMaterialTypeName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return name ?? "";
        return MaterialTypeNameMap.TryGetValue(name, out var mapped) ? mapped : name;
    }

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
                result.MaterialCategory = EnumHelper.TryParse<MaterialType>(MapMaterialTypeName(order.MaterialCategory));
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
                    result.MaterialCategory = EnumHelper.TryParse<MaterialType>(MapMaterialTypeName(item.MaterialCategory));
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
                result.MaterialCategory = EnumHelper.TryParse<MaterialType>(MapMaterialTypeName(order.OutMaterialCategory));
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

        // 来源工单解析出后，回填权威「订单号+主号」用于自动填充（与定尺核查口径一致）
        if (!string.IsNullOrEmpty(result.ExpectedWorkOrderNo))
        {
            var sourceWo = await _context.WorkOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.WorkOrderNo == result.ExpectedWorkOrderNo);
            if (sourceWo != null)
            {
                result.SalesOrderNo = sourceWo.SalesOrderNo;
                result.ProductionMainNo = sourceWo.ProductionMainNo;
            }
        }

        return result;
    }

    public async Task<SourceOrderValidationResult> ValidateProductionBatchAsync(string productionBatchNo)
    {
        var result = new SourceOrderValidationResult { IsValid = true };

        if (string.IsNullOrEmpty(productionBatchNo))
        {
            result.Warnings.Add("生产批号为空");
            result.IsValid = false;
            return result;
        }

        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BatchNo == productionBatchNo);

        if (batch == null)
        {
            result.Warnings.Add($"生产批号「{productionBatchNo}」不存在");
            result.IsValid = false;
            return result;
        }

        if (!string.IsNullOrEmpty(batch.WorkOrderNo))
            result.ExpectedWorkOrderNo = batch.WorkOrderNo;
        result.MaterialCategory = EnumHelper.TryParse<MaterialType>(MapMaterialTypeName(batch.ManufacturingItem));
        result.PlantGrade = batch.PlantGrade;
        result.Specification = batch.Specification;
        result.SalesOrderNo = batch.SalesOrderNo;
        result.ProductionMainNo = batch.ProductionMainNo;
        result.OrderItemIds = batch.OrderItemIds;
        result.HeatNo = batch.SourceHeatNo;
        result.ManufacturingStatus = EnumHelper.TryParse<DeliveryState>(batch.ManufacturingStatus);
        result.SupplierName = batch.SourceName;
        return result;
    }

    /// <summary>
    /// 按入库批次来源（采购单号/委外单号+序号/生产批号）解析应关联的工单号+订单号+主号。
    /// 用于入库更正页点击「关联工单=是」时即时回填；来源单未关联工单时 IsValid 仍为 true 但 ExpectedWorkOrderNo 为空。
    /// </summary>
    public async Task<SourceOrderValidationResult> ResolveLinkedWorkOrderAsync(int inventoryBatchId)
    {
        var batch = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.Id == inventoryBatchId)
            .Select(b => new { b.InboundSource, b.SourceOrderNo, b.SourceOrderSequence, b.ProductionBatchNo })
            .FirstOrDefaultAsync();

        if (batch == null)
            return new SourceOrderValidationResult { IsValid = false, Warnings = { "入库批次不存在" } };

        // 外购 → 采购单号；委外 → 委外单号+序号；检验/生产入库 → 生产批号
        if (batch.InboundSource == InboundSource.Purchase.ToString())
        {
            if (string.IsNullOrEmpty(batch.SourceOrderNo))
                return new SourceOrderValidationResult { IsValid = false, Warnings = { "批次未填写来源单号，无法匹配采购订单" } };
            return await ValidateSourceOrderAsync(batch.SourceOrderNo, batch.InboundSource, batch.SourceOrderSequence);
        }

        if (batch.InboundSource == InboundSource.Subcontract.ToString())
        {
            if (string.IsNullOrEmpty(batch.SourceOrderNo) || !batch.SourceOrderSequence.HasValue)
                return new SourceOrderValidationResult { IsValid = false, Warnings = { "批次未填写委外单号或序号，无法匹配圆棒穿孔" } };
            return await ValidateSourceOrderAsync(batch.SourceOrderNo, batch.InboundSource, batch.SourceOrderSequence);
        }

        if (batch.InboundSource == InboundSource.InspectionInbound.ToString()
            || batch.InboundSource == InboundSource.ProductionInbound.ToString())
        {
            if (string.IsNullOrEmpty(batch.ProductionBatchNo))
                return new SourceOrderValidationResult { IsValid = false, Warnings = { "批次未填写生产批号，无法匹配生产批次" } };
            return await ValidateProductionBatchAsync(batch.ProductionBatchNo);
        }

        return new SourceOrderValidationResult
        {
            IsValid = false,
            Warnings = { $"入库来源「{batch.InboundSource}」暂不支持关联工单匹配" }
        };
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
                     && b.WorkOrderNo != WorkOrderNoSentinel.NotWorkOrder);

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

    public async Task<List<SourceOrderChangedBatchDto>> GetSourceOrderChangedBatchesAsync(int? warehouseId = null)
    {
        // 批次工单号为空/未填也纳入比对（来源单当前有工单号时提示同步填写）；
        // 仅排除明确标记「非工单」的哨兵值。
        var query = _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.SourceOrderNo != null
                     && b.SourceOrderNo != string.Empty
                     && (b.WorkOrderNo == null || b.WorkOrderNo != WorkOrderNoSentinel.NotWorkOrder));

        if (warehouseId.HasValue)
            query = query.Where(b => b.WarehouseId == warehouseId.Value);

        var batches = await query
            .Select(b => new
            {
                b.Id,
                b.BatchNo,
                SourceOrderNo = b.SourceOrderNo ?? string.Empty,
                b.SourceOrderSequence,
                WorkOrderNo = b.WorkOrderNo ?? string.Empty
            })
            .ToListAsync();

        if (batches.Count == 0)
            return new List<SourceOrderChangedBatchDto>();

        var sourceOrderNos = batches.Select(b => b.SourceOrderNo).Distinct().ToList();

        // 采购单：OrderNo → 当前关联工单号（含空值，用于识别「来源单已清空工单号=已取消」）
        var purchaseMap = (await _context.PurchaseOrders
                .AsNoTracking()
                .Where(p => sourceOrderNos.Contains(p.OrderNo))
                .Select(p => new { p.OrderNo, p.SourceWorkOrderNo })
                .ToListAsync())
            .ToDictionary(x => x.OrderNo, x => x.SourceWorkOrderNo ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        // 委外单：OrderNo → 主表 Id
        var subOrders = await _context.SubcontractOrders
            .AsNoTracking()
            .Where(s => sourceOrderNos.Contains(s.OrderNo))
            .Select(s => new { s.Id, s.OrderNo })
            .ToListAsync();
        var subOrderIdByNo = subOrders.ToDictionary(x => x.OrderNo, x => x.Id, StringComparer.OrdinalIgnoreCase);
        var subOrderIds = subOrders.Select(s => s.Id).ToList();

        // 委外明细：(主表Id, 序号) → 当前关联工单号（含空值，用于识别「明细已清空工单号=已取消」）
        var subItemMap = new Dictionary<(int SubOrderId, int Sequence), string>();
        if (subOrderIds.Count > 0)
        {
            var items = await _context.SubcontractReturnItems
                .AsNoTracking()
                .Where(i => subOrderIds.Contains(i.SubcontractOrderId))
                .Select(i => new { i.SubcontractOrderId, i.Sequence, i.SourceWorkOrderNo })
                .ToListAsync();
            foreach (var it in items)
                subItemMap[(it.SubcontractOrderId, it.Sequence)] = it.SourceWorkOrderNo ?? string.Empty;
        }

        // 工单存在集：批次冗余工单号与来源单当前工单号中，仍存在于工单管理（未被删除=未取消）的工单号
        var candidateWorkOrderNos = batches
            .Select(b => b.WorkOrderNo)
            .Concat(purchaseMap.Values)
            .Concat(subItemMap.Values)
            .Where(w => !string.IsNullOrEmpty(w))
            .Distinct()
            .ToList();
        var existingWorkOrderNos = candidateWorkOrderNos.Count == 0
            ? new List<string>()
            : await _context.WorkOrders
                .AsNoTracking()
                .Where(w => candidateWorkOrderNos.Contains(w.WorkOrderNo))
                .Select(w => w.WorkOrderNo)
                .ToListAsync();
        var existingWorkOrderSet = existingWorkOrderNos.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new List<SourceOrderChangedBatchDto>();
        foreach (var b in batches)
        {
            string? expected = null;

            // 委外来源（带序号）：以委外明细工单号为准
            if (b.SourceOrderSequence.HasValue
                && subOrderIdByNo.TryGetValue(b.SourceOrderNo, out var subId)
                && subItemMap.TryGetValue((subId, b.SourceOrderSequence.Value), out var subWo))
                expected = subWo;

            // 采购来源（或无序号委外）：以采购单工单号为准
            if (expected == null && purchaseMap.TryGetValue(b.SourceOrderNo, out var poWo))
                expected = poWo;

            var batchWo = b.WorkOrderNo;

            // 场景一：来源单当前无工单号（已清空/来源单不存在）→ 批次残留工单号判定为「已取消」
            if (string.IsNullOrEmpty(expected))
            {
                if (!string.IsNullOrEmpty(batchWo))
                {
                    result.Add(new SourceOrderChangedBatchDto
                    {
                        BatchId = b.Id,
                        BatchNo = b.BatchNo,
                        SourceOrderNo = b.SourceOrderNo,
                        SourceOrderSequence = b.SourceOrderSequence,
                        ExpectedWorkOrderNo = batchWo,
                        IsCancelled = true
                    });
                }
                continue;
            }

            // 场景二：来源单当前工单号 ≠ 批次冗余工单号 → 「已变更」（含批次未填工单号需同步填写）
            if (!string.Equals(batchWo, expected, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new SourceOrderChangedBatchDto
                {
                    BatchId = b.Id,
                    BatchNo = b.BatchNo,
                    SourceOrderNo = b.SourceOrderNo,
                    SourceOrderSequence = b.SourceOrderSequence,
                    ExpectedWorkOrderNo = expected
                });
                continue;
            }

            // 场景三：批次与来源单一致，但来源单指向的工单已被删除（取消）→ 「已取消」
            if (!existingWorkOrderSet.Contains(expected))
            {
                result.Add(new SourceOrderChangedBatchDto
                {
                    BatchId = b.Id,
                    BatchNo = b.BatchNo,
                    SourceOrderNo = b.SourceOrderNo,
                    SourceOrderSequence = b.SourceOrderSequence,
                    ExpectedWorkOrderNo = expected,
                    IsCancelled = true
                });
            }
        }

        return result;
    }

    public async Task<List<string>> GetDistinctWorkOrderNosByWarehouseAsync(int warehouseId)
    {
        return await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.WarehouseId == warehouseId
                     && b.WorkOrderNo != null
                     && b.WorkOrderNo != string.Empty
                     && b.WorkOrderNo != WorkOrderNoSentinel.NotWorkOrder)
            .Select(b => b.WorkOrderNo!)
            .Distinct()
            .ToListAsync();
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
                // 关联批次可能已删光（无匹配）→ 到货字段回退为 0，避免残留快照
                var receivedQty = 0;
                var receivedWt = 0m;
                DateTime? maxDate = null;
                if (batchDict.TryGetValue(order.OrderNo, out var data))
                {
                    receivedQty = data.TotalQty;
                    receivedWt = data.TotalWt;
                    maxDate = data.MaxDate;
                }

                order.ReceivedQuantity = receivedQty;
                order.ReceivedWeight = receivedWt;
                order.LastArrivalDate = maxDate;

                if (!order.IsForceCompleted)
                {
                    if (receivedQty == 0)
                        order.Status = PurchaseOrderStatus.Open;
                    else if (order.Quantity.HasValue && receivedQty >= order.Quantity.Value)
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
            var subcontractOverRatio = await GetConfigAsync("WarehouseThreshold", "SubcontractOverRatio", 1.05m);
            var subcontractOverDeviation = await GetConfigAsync("WarehouseThreshold", "SubcontractOverDeviation", 100m);
            var allBatches = await _context.InventoryBatches
                .AsNoTracking()
                .Where(b => b.SourceOrderNo != null && sourceOrderNos.Contains(b.SourceOrderNo))
                .ToListAsync();

            // 退货量（序号级）：状态判定按「净回收 = 回收 - 退货」；聚合口径与 SubcontractOrderService 一致
            var subcontractBatchNos = allBatches.Where(b => !string.IsNullOrEmpty(b.BatchNo)).Select(b => b.BatchNo!).ToList();
            var returnOutbounds = new List<OutboundRecord>();
            foreach (var chunk in subcontractBatchNos.Chunk(1000))
            {
                returnOutbounds.AddRange(await _context.OutboundRecords.AsNoTracking()
                    .Where(o => o.OutboundType == OutboundType.ReturnOut
                             && o.ReturnSourceBatchNo != null
                             && chunk.Contains(o.ReturnSourceBatchNo))
                    .ToListAsync());
            }
            var bySeqMap = SubcontractHelper.AggregateReturnsBySequence(
                returnOutbounds,
                allBatches.Select(b => (b.BatchNo, b.SourceOrderNo, b.SourceOrderSequence)));

            foreach (var order in subcontractOrders)
            {
                // SQL 查询后内存过滤须忽略大小写（SQL 排序规则不区分大小写，C# 默认 == 区分，委外单号手输可能大小写不一）
                var orderBatches = allBatches.Where(b => string.Equals(b.SourceOrderNo, order.OrderNo, StringComparison.OrdinalIgnoreCase)).ToList();
                order.InQuantity = orderBatches.Sum(b => b.InitialQuantity);
                order.InWeight = orderBatches.Sum(b => b.InitialWeight);

                var returnBySequence = bySeqMap.TryGetValue(order.OrderNo, out var rs) ? rs : null;
                var orderReturnWeight = returnBySequence?.Values.Sum(x => x.Weight) ?? 0m;

                foreach (var item in order.ReturnItems)
                    SubcontractHelper.SyncReturnItemFromBatches(item, orderBatches, subcontractOverRatio, subcontractOverDeviation, returnBySequence);

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
                    var netRecover = order.InWeight.HasValue ? Math.Max(0m, order.InWeight.Value - orderReturnWeight) : 0m;
                    if (netRecover <= 0m)
                        order.Status = SubcontractOrderStatus.Sent;
                    else if (netRecover >= order.OutWeight * subcontractCompleteRatio)
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

            // 刷新采购单关联的工单执行状况
            foreach (var woNo in purchaseOrders.Where(o => !string.IsNullOrWhiteSpace(o.SourceWorkOrderNo))
                                     .Select(o => o.SourceWorkOrderNo)
                                     .Distinct(StringComparer.OrdinalIgnoreCase))
                await TryRefreshExecutionSummaryAsync(woNo!);

            // 刷新委外单关联的工单执行状况
            foreach (var order in subcontractOrders)
                foreach (var item in order.ReturnItems)
                    await TryRefreshExecutionSummaryAsync(item.SourceWorkOrderNo);
        }
    }

}
