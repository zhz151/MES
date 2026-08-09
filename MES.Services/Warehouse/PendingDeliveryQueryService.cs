using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.DTOs.Warehouse;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Models;
using MES.Data;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Warehouse;

/// <summary>
/// 待发货订单成品查询服务 — 实时 JOIN 查询
/// </summary>
public class PendingDeliveryQueryService : IPendingDeliveryQueryService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public PendingDeliveryQueryService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<PendingDeliveryItemDto>> GetPendingItemsAsync(
        string? orderNo = null,
        string? productStandard = null,
        string? deliveryStatus = null)
    {
        var dtos = await GetCachedDtosAsync();

        // 订单号筛选（缓存已包含全部数据，内存过滤）
        if (!string.IsNullOrEmpty(orderNo))
            dtos = dtos.Where(d => string.Equals(d.SalesOrderNo, orderNo, StringComparison.OrdinalIgnoreCase)).ToList();

        // 筛选：产品标准
        if (!string.IsNullOrEmpty(productStandard))
            dtos = dtos.Where(d => d.ProductStandard == productStandard).ToList();

        // 筛选：交货状态
        if (!string.IsNullOrEmpty(deliveryStatus))
            dtos = dtos.Where(d => d.DeliveryStatus?.ToString() == deliveryStatus).ToList();

        return dtos;
    }

    public async Task<PagedResult<PendingDeliveryItemDto>> GetPagedAsync(QueryParams query)
    {
        // 1. 从缓存加载 DTO 列表
        var allDtos = await GetCachedDtosAsync();

        if (allDtos.Count == 0)
            return new PagedResult<PendingDeliveryItemDto>
            {
                Items = new List<PendingDeliveryItemDto>(),
                TotalCount = 0,
                PageIndex = query.PageIndex,
                PageSize = query.PageSize
            };

        // 2. Keyword 内存匹配（覆盖 DTO 所有 string 字段，含跨表字段）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            allDtos = allDtos
                .Where(d =>
                    (d.InventoryBatchNo ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.ProductionBatchNo ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.HeatNo ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.PlantGrade ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.Specification ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.LengthStatus?.ToString() ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.SalesOrderNo ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.ProductionMainNo ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.WorkOrderNo ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.CustomerName ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.Salesman ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.EndCustomer ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.ProductStandard ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.DeliveryStatus?.ToString() ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.StandardGrade ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    d.MaterialType.ToString().Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.InboundSource.ToString() ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.SourceName ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // 3. InboundDate 范围筛选
        if (query.InboundDateFrom.HasValue)
        {
            var from = query.InboundDateFrom.Value;
            allDtos = allDtos.Where(d => d.InboundDate >= from).ToList();
        }
        if (query.InboundDateTo.HasValue)
        {
            var to = query.InboundDateTo.Value.AddDays(1); // 包含当日全天
            allDtos = allDtos.Where(d => d.InboundDate < to).ToList();
        }

        // 4. ExcelFilter 筛选（DTO 属性反射匹配）
        if (query.Filters is { Count: > 0 })
        {
            var filtered = allDtos.AsQueryable().ApplyFilters(query.Filters).ToList();
            allDtos = filtered;
        }

        // 5. 排序
        var totalCount = allDtos.Count;
        var sortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "InventoryBatchNo" : query.SortBy;

        // 安全兜底：如果 DTO 没有该属性，按 InventoryBatchNo 排序
        var dtoType = typeof(PendingDeliveryItemDto);
        var sortProp = dtoType.GetProperty(sortBy, System.Reflection.BindingFlags.IgnoreCase
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (sortProp == null)
            sortBy = "InventoryBatchNo";

        allDtos = query.IsDescending
            ? allDtos.AsQueryable().ApplySort(sortBy, true).ToList()
            : allDtos.AsQueryable().ApplySort(sortBy, false).ToList();

        // 6. 分页
        var pagedItems = allDtos
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToList();

        return new PagedResult<PendingDeliveryItemDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<CertificateHeaderOptionDto>> GetHeaderOptionsAsync()
    {
        var dtos = await GetCachedDtosAsync();

        return dtos
            .GroupBy(d => new { d.SalesOrderNo, d.CustomerName, d.ProductStandard, d.DeliveryStatus })
            .Select(g => new CertificateHeaderOptionDto
            {
                OrderNo = g.Key.SalesOrderNo ?? "",
                CustomerName = g.Key.CustomerName,
                ProductStandard = g.Key.ProductStandard,
                DeliveryStatus = g.Key.DeliveryStatus
            })
            .OrderBy(o => o.OrderNo)
            .ToList();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var dtos = await GetCachedDtosAsync();

        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // 收集各列的 DISTINCT 值（排除 null/空）
        void AddDistinct(string key, IEnumerable<string?> values)
        {
            var list = values
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
            if (list.Count > 0)
                result[key] = list;
        }

        AddDistinct("InventoryBatchNo", dtos.Select(d => d.InventoryBatchNo));
        AddDistinct("ProductionBatchNo", dtos.Select(d => d.ProductionBatchNo));
        AddDistinct("HeatNo", dtos.Select(d => d.HeatNo));
        AddDistinct("PlantGrade", dtos.Select(d => d.PlantGrade));
        AddDistinct("Specification", dtos.Select(d => d.Specification));
        AddDistinct("RemainingQuantity", dtos.Select(d => d.RemainingQuantity.ToString()));
        AddDistinct("RemainingMeters", dtos.Select(d => d.RemainingMeters?.ToString("G29")));
        AddDistinct("SalesOrderNo", dtos.Select(d => d.SalesOrderNo));
        AddDistinct("CustomerName", dtos.Select(d => d.CustomerName));
        AddDistinct("ProductStandard", dtos.Select(d => d.ProductStandard));
        AddDistinct("DeliveryStatus", dtos.Select(d => d.DeliveryStatus?.ToString()));
        AddDistinct("MaterialType", dtos.Select(d => d.MaterialType.ToString()));
        AddDistinct("InboundSource", dtos.Select(d => d.InboundSource.ToString()));
        AddDistinct("SourceName", dtos.Select(d => d.SourceName));
        AddDistinct("ProductionMainNo", dtos.Select(d => d.ProductionMainNo));
        AddDistinct("WorkOrderNo", dtos.Select(d => d.WorkOrderNo));
        AddDistinct("LengthStatus", dtos.Select(d => d.LengthStatus?.ToString()));
        AddDistinct("Salesman", dtos.Select(d => d.Salesman));
        AddDistinct("EndCustomer", dtos.Select(d => d.EndCustomer));
        AddDistinct("StandardGrade", dtos.Select(d => d.StandardGrade));

        return result;
    }

    /// <summary>
    /// 缓存键 — 已组装 DTO 缓存（5min 滑动）
    /// InventoryService 在出库/入库操作后通过 Remove(CacheKey) 主动失效
    /// </summary>
    public const string CacheKey = "PendingDeliveryQueryService:LoadDtos";

    /// <summary>
    /// C1: InventoryBatch 原始实体缓存键（10min 滑动）
    /// </summary>
    private const string InventoryBatchCacheKey = "PendingDeliveryQueryService:InventoryBatches";

    /// <summary>
    /// C2: 引用数据缓存键前缀（30min 滑动）
    /// </summary>
    private const string ReferenceDataCacheKeyPrefix = "PendingDeliveryQueryService:RefData:";

    /// <summary>
    /// 已组装 DTO 缓存 — 5min 滑动，从 C1+C2 重建
    /// </summary>
    private async Task<List<PendingDeliveryItemDto>> GetCachedDtosAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);

            var batches = await GetCachedInventoryBatchesAsync();
            if (batches.Count == 0)
                return new List<PendingDeliveryItemDto>();

            // 从 InventoryBatch 提取引用数据所需的标识符
            var orderNos = batches
                .Where(b => !string.IsNullOrEmpty(b.SalesOrderNo))
                .Select(b => b.SalesOrderNo!)
                .Distinct()
                .ToList();

            var woNos = batches
                .Where(b => !string.IsNullOrEmpty(b.WorkOrderNo))
                .Select(b => b.WorkOrderNo!)
                .Distinct()
                .ToList();

            var batchNosForHeat = batches
                .Where(b => string.IsNullOrEmpty(b.HeatNo) && !string.IsNullOrEmpty(b.ProductionBatchNo))
                .Select(b => b.ProductionBatchNo!)
                .Distinct()
                .ToList();

            var batchSequences = batches
                .SelectMany(b => ParseSequences(b.OrderItemIds))
                .Distinct()
                .ToList();

            var refData = await GetCachedReferenceDataAsync(orderNos, woNos, batchNosForHeat, batchSequences);

            return AssembleDtos(batches, refData);
        }) ?? new List<PendingDeliveryItemDto>();
    }

    /// <summary>
    /// C1: InventoryBatch 原始实体缓存 — 10min 滑动
    /// </summary>
    private async Task<List<MES.Data.Entities.Warehouse.InventoryBatch>> GetCachedInventoryBatchesAsync()
    {
        return await _cache.GetOrCreateAsync(InventoryBatchCacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);
            return await _context.InventoryBatches
                .AsNoTracking()
                .Where(ib => ib.MaterialType == InventoryMaterialTypes.OrderFinished
                          && (ib.RemainingQuantity > 0 || ib.RemainingWeight > 0m))
                .ToListAsync();
        }) ?? new List<MES.Data.Entities.Warehouse.InventoryBatch>();
    }

    /// <summary>
    /// C2: 引用数据缓存 — 30min 滑动，复合键基于当前批次的标识符集合
    /// </summary>
    private async Task<ReferenceDataCache> GetCachedReferenceDataAsync(
        List<string> orderNos, List<string> woNos, List<string> batchNosForHeat, List<int> batchSequences)
    {
        var cacheKey = ComputeReferenceCacheKey(orderNos, woNos, batchNosForHeat, batchSequences);

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(30);
            return await LoadReferenceDataAsync(orderNos, woNos, batchNosForHeat, batchSequences);
        }) ?? new ReferenceDataCache();
    }

    private static string ComputeReferenceCacheKey(
        List<string> orderNos, List<string> woNos, List<string> batchNosForHeat, List<int> batchSequences)
    {
        orderNos.Sort(StringComparer.OrdinalIgnoreCase);
        woNos.Sort(StringComparer.OrdinalIgnoreCase);
        batchNosForHeat.Sort(StringComparer.OrdinalIgnoreCase);
        var seqStr = string.Join(",", batchSequences.OrderBy(s => s));
        return $"{ReferenceDataCacheKeyPrefix}{string.Join("|", orderNos)}|{string.Join("|", woNos)}|{string.Join("|", batchNosForHeat)}|{seqStr}";
    }

    private static List<int> ParseSequences(string? idsStr)
    {
        if (string.IsNullOrEmpty(idsStr)) return new List<int>();
        return idsStr
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(idStr => int.TryParse(idStr, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
    }

    /// <summary>
    /// 加载引用数据：WorkOrder + SalesOrder + OrderItem + ProductionBatch（最多 4 次 DB 查询）
    /// </summary>
    private async Task<ReferenceDataCache> LoadReferenceDataAsync(
        List<string> orderNos, List<string> woNos, List<string> batchNosForHeat, List<int> batchSequences)
    {
        var result = new ReferenceDataCache();

        // 1. WorkOrder — 反查权威 SalesOrderNo 和 OrderItemIds
        if (woNos.Count > 0)
        {
            var workOrders = await _context.Set<MES.Data.Entities.WorkOrder.WorkOrder>()
                .AsNoTracking()
                .Where(w => woNos.Contains(w.WorkOrderNo))
                .Select(w => new { w.WorkOrderNo, w.SalesOrderNo, w.OrderItemIds, w.ProductionMainNo })
                .ToListAsync();

            result.WorkOrderDict = workOrders.ToDictionary(
                w => w.WorkOrderNo,
                w => (w.SalesOrderNo, w.OrderItemIds, w.ProductionMainNo),
                StringComparer.OrdinalIgnoreCase);

            // 合并 WorkOrder 中的订单号到查询列表
            foreach (var wo in workOrders)
            {
                if (!string.IsNullOrEmpty(wo.SalesOrderNo) && !orderNos.Contains(wo.SalesOrderNo))
                    orderNos.Add(wo.SalesOrderNo);
            }
        }

        if (orderNos.Count > 0)
        {
            // 2. SalesOrder
            var orders = await _context.Set<MES.Data.Entities.Order.SalesOrder>()
                .AsNoTracking()
                .Where(o => orderNos.Contains(o.OrderNumber))
                .Select(o => new SalesOrderInfo
                {
                    OrderNumber = o.OrderNumber,
                    CustomerName = o.CustomerName,
                    Salesman = o.Salesman,
                    EndCustomer = o.EndCustomer
                })
                .ToListAsync();

            result.OrderDict = orders.ToDictionary(o => o.OrderNumber, o => o, StringComparer.OrdinalIgnoreCase);

            // 3. OrderItem — 合并 batchSequences + workOrder.OrderItemIds 的序列号
            var allSequences = new HashSet<int>(batchSequences);
            foreach (var seq in result.WorkOrderDict.Values
                .SelectMany(v => ParseSequences(v.OrderItemIds)))
            {
                allSequences.Add(seq);
            }

            if (allSequences.Count > 0)
            {
                var seqList = allSequences.ToList();
                var items = await _context.Set<MES.Data.Entities.Order.OrderItem>()
                    .AsNoTracking()
                    .Where(oi => orderNos.Contains(oi.OrderNumber ?? "") && seqList.Contains(oi.Sequence))
                    .Select(oi => new OrderItemInfo
                    {
                        OrderNumber = oi.OrderNumber ?? "",
                        Sequence = oi.Sequence,
                        StandardNo = oi.StandardNo,
                        StandardGrade = oi.StandardGrade,
                        DeliveryState = oi.DeliveryState.ToString()
                    })
                    .ToListAsync();

                result.ItemDict = items.ToDictionary(i => $"{i.OrderNumber}|{i.Sequence}", i => i);
            }
        }

        // 4. ProductionBatch — 补充炉号
        if (batchNosForHeat.Count > 0)
        {
            result.ProductionBatchHeatMap = await _context.Set<MES.Data.Entities.Batch.ProductionBatch>()
                .AsNoTracking()
                .Where(pb => batchNosForHeat.Contains(pb.BatchNo))
                .ToDictionaryAsync(pb => pb.BatchNo, pb => pb.SourceHeatNo, StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// 从缓存的 InventoryBatch + 引用数据组装 DTO（纯内存操作）
    /// </summary>
    private List<PendingDeliveryItemDto> AssembleDtos(
        List<MES.Data.Entities.Warehouse.InventoryBatch> batches,
        ReferenceDataCache refData)
    {
        var result = new List<PendingDeliveryItemDto>();

        foreach (var batch in batches)
        {
            // 优先从 WorkOrder 反推 SalesOrderNo、OrderItemIds 和 ProductionMainNo
            // （OrderItemIds 仅作内部解析「产品标准/标准等级/交货状态」的关联键，查询/显示已改用主号）
            var resolvedSalesOrderNo = batch.SalesOrderNo;
            var resolvedOrderItemIds = batch.OrderItemIds;
            var resolvedProductionMainNo = batch.ProductionMainNo;

            if (!string.IsNullOrEmpty(batch.WorkOrderNo)
                && refData.WorkOrderDict.TryGetValue(batch.WorkOrderNo, out var woInfo))
            {
                if (!string.IsNullOrEmpty(woInfo.SalesOrderNo))
                    resolvedSalesOrderNo = woInfo.SalesOrderNo;
                if (!string.IsNullOrEmpty(woInfo.OrderItemIds))
                    resolvedOrderItemIds = woInfo.OrderItemIds;
                if (!string.IsNullOrEmpty(woInfo.ProductionMainNo))
                    resolvedProductionMainNo = woInfo.ProductionMainNo;
            }

            // 解析该项次的 OrderItem
            string? itemStandardNo = null;
            string? itemStandardGrade = null;
            string? itemDeliveryStatus = null;

            if (!string.IsNullOrEmpty(resolvedOrderItemIds)
                && refData.ItemDict.Count > 0
                && !string.IsNullOrEmpty(resolvedSalesOrderNo))
            {
                var compositeKey = resolvedOrderItemIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(id => int.TryParse(id, out var i) ? i : 0)
                    .Where(id => id > 0)
                    .Select(seq => $"{resolvedSalesOrderNo}|{seq}")
                    .FirstOrDefault(key => refData.ItemDict.ContainsKey(key));

                if (compositeKey != null && refData.ItemDict.TryGetValue(compositeKey, out var itemInfo))
                {
                    itemStandardNo = itemInfo.StandardNo;
                    itemStandardGrade = itemInfo.StandardGrade;

                    if (!string.IsNullOrEmpty(itemInfo.DeliveryState))
                        itemDeliveryStatus = itemInfo.DeliveryState;
                }
            }

            // 订单信息（使用权威的 SalesOrderNo）
            string? customerName = null;
            string? salesman = null;
            string? endCustomer = null;

            if (!string.IsNullOrEmpty(resolvedSalesOrderNo)
                && refData.OrderDict.TryGetValue(resolvedSalesOrderNo, out var orderInfo))
            {
                customerName = orderInfo.CustomerName;
                salesman = orderInfo.Salesman;
                endCustomer = orderInfo.EndCustomer;
            }

            // 炉号回退
            var heatNo = !string.IsNullOrEmpty(batch.HeatNo)
                ? batch.HeatNo
                : refData.ProductionBatchHeatMap.GetValueOrDefault(batch.ProductionBatchNo ?? "");

            result.Add(new PendingDeliveryItemDto
            {
                InventoryBatchNo = batch.BatchNo,
                MaterialType = EnumHelper.TryParse<MaterialType>(batch.MaterialType) ?? default,
                InboundSource = string.IsNullOrEmpty(batch.InboundSource) ? default
                    : EnumHelper.TryParse<InboundSource>(batch.InboundSource) ?? default,
                SourceName = batch.SourceName,
                ProductionBatchNo = batch.ProductionBatchNo,
                HeatNo = heatNo,
                PlantGrade = batch.PlantGrade,
                Specification = batch.Specification,
                LengthStatus = EnumHelper.TryParse<LengthStatus>(batch.LengthStatus),
                MinLength = batch.MinLength,
                MaxLength = batch.MaxLength,
                RemainingQuantity = batch.RemainingQuantity,
                RemainingWeight = batch.RemainingWeight,
                Meters = batch.Meters,
                RemainingMeters = batch.RemainingMeters,
                InboundDate = batch.InboundDate,
                SalesOrderNo = resolvedSalesOrderNo,
                ProductionMainNo = resolvedProductionMainNo,
                WorkOrderNo = batch.WorkOrderNo,
                CustomerName = customerName,
                Salesman = salesman,
                EndCustomer = endCustomer,
                ProductStandard = itemStandardNo,
                DeliveryStatus = EnumHelper.TryParse<DeliveryState>(itemDeliveryStatus),
                StandardGrade = itemStandardGrade,
            });
        }

        return result;
    }

    // ========== 内部辅助类 ==========

    private class SalesOrderInfo
    {
        public string OrderNumber { get; set; } = null!;
        public string? CustomerName { get; set; }
        public string? Salesman { get; set; }
        public string? EndCustomer { get; set; }
    }

    private class OrderItemInfo
    {
        public string OrderNumber { get; set; } = null!;
        public int Sequence { get; set; }
        public string? StandardNo { get; set; }
        public string StandardGrade { get; set; } = null!;
        public string? DeliveryState { get; set; }
    }

    private class ReferenceDataCache
    {
        public Dictionary<string, (string SalesOrderNo, string OrderItemIds, string ProductionMainNo)> WorkOrderDict
            = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SalesOrderInfo> OrderDict
            = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, OrderItemInfo> ItemDict
            = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string?> ProductionBatchHeatMap
            = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<MES.Core.DTOs.Shared.PrintColumnDef> columns)
    {
        var pdfBytes = PendingDeliveryPrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }

    /// <summary>打印全部（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintAllFileAsync(string title, List<Dictionary<string, object>> items, List<MES.Core.DTOs.Shared.PrintColumnDef> columns)
    {
        var pdfBytes = PendingDeliveryPrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }
}
