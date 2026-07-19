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
            dtos = dtos.Where(d => d.DeliveryStatus == deliveryStatus).ToList();

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
                    (d.LengthStatus ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.SalesOrderNo ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.OrderItemIds ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.WorkOrderNo ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.CustomerName ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.Salesman ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.EndCustomer ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.ProductStandard ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (d.DeliveryStatus ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
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
        AddDistinct("DeliveryStatus", dtos.Select(d => d.DeliveryStatus));
        AddDistinct("MaterialType", dtos.Select(d => d.MaterialType.ToString()));
        AddDistinct("InboundSource", dtos.Select(d => d.InboundSource.ToString()));
        AddDistinct("SourceName", dtos.Select(d => d.SourceName));
        AddDistinct("OrderItemIds", dtos.Select(d => d.OrderItemIds));
        AddDistinct("WorkOrderNo", dtos.Select(d => d.WorkOrderNo));
        AddDistinct("LengthStatus", dtos.Select(d => d.LengthStatus));
        AddDistinct("Salesman", dtos.Select(d => d.Salesman));
        AddDistinct("EndCustomer", dtos.Select(d => d.EndCustomer));
        AddDistinct("StandardGrade", dtos.Select(d => d.StandardGrade));

        return result;
    }

    /// <summary>
    /// 缓存键，公开供 InventoryService 在出库/入库操作后主动失效
    /// </summary>
    public const string CacheKey = "PendingDeliveryQueryService:LoadDtos";

    /// <summary>
    /// 缓存包装：5 分钟滑动缓存，配合出库/入库操作主动失效
    /// </summary>
    private async Task<List<PendingDeliveryItemDto>> GetCachedDtosAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            return await LoadDtosAsync();
        }) ?? new List<PendingDeliveryItemDto>();
    }

    /// <summary>
    /// 共享加载逻辑：SQL 查询 InventoryBatch + 内存 JOIN SalesOrder/OrderItem → 组装 DTO
    /// </summary>
    private async Task<List<PendingDeliveryItemDto>> LoadDtosAsync(
        string? orderNo = null,
        string? keyword = null)
    {
        // 1. 筛选成品库存中的待发货项
        var query = _context.InventoryBatches
            .AsNoTracking()
            .Where(ib => ib.MaterialType == InventoryMaterialTypes.OrderFinished
                      && (ib.RemainingQuantity > 0 || ib.RemainingWeight > 0m));

        if (!string.IsNullOrEmpty(orderNo))
            query = query.Where(ib => ib.SalesOrderNo == orderNo);

        // SQL 层 keyword 匹配 InventoryBatch 自有字段
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(ib =>
                ib.BatchNo.Contains(keyword) ||
                (ib.HeatNo != null && ib.HeatNo.Contains(keyword)) ||
                ib.PlantGrade.Contains(keyword) ||
                ib.Specification.Contains(keyword) ||
                (ib.SalesOrderNo != null && ib.SalesOrderNo.Contains(keyword)) ||
                (ib.ProductionBatchNo != null && ib.ProductionBatchNo.Contains(keyword)));
        }

        // 2. 查询匹配的库存批次
        var batches = await query.ToListAsync();
        if (batches.Count == 0)
            return new List<PendingDeliveryItemDto>();

        // 3. 获取关联的订单/工单信息
        var orderNos = batches
            .Where(b => !string.IsNullOrEmpty(b.SalesOrderNo))
            .Select(b => b.SalesOrderNo!)
            .Distinct()
            .ToList();

        // 3a. 通过 WorkOrder 反查权威的 SalesOrderNo 和 OrderItemIds
        var woNos = batches
            .Where(b => !string.IsNullOrEmpty(b.WorkOrderNo))
            .Select(b => b.WorkOrderNo!)
            .Distinct()
            .ToList();

        Dictionary<string, (string SalesOrderNo, string OrderItemIds)>? workOrderDict = null;
        if (woNos.Count > 0)
        {
            var workOrders = await _context.Set<MES.Data.Entities.WorkOrder.WorkOrder>()
                .AsNoTracking()
                .Where(w => woNos.Contains(w.WorkOrderNo))
                .Select(w => new { w.WorkOrderNo, w.SalesOrderNo, w.OrderItemIds })
                .ToListAsync();

            workOrderDict = workOrders.ToDictionary(
                w => w.WorkOrderNo,
                w => (w.SalesOrderNo, w.OrderItemIds),
                StringComparer.OrdinalIgnoreCase);

            // 合并 WorkOrder 中的订单号到查询列表
            foreach (var wo in workOrders)
            {
                if (!string.IsNullOrEmpty(wo.SalesOrderNo) && !orderNos.Contains(wo.SalesOrderNo))
                    orderNos.Add(wo.SalesOrderNo);
            }
        }

        Dictionary<string, SalesOrderInfo>? orderDict = null;
        Dictionary<string, OrderItemInfo>? itemDict = null;

        if (orderNos.Count > 0)
        {
            // 查订单
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

            orderDict = orders.ToDictionary(o => o.OrderNumber, o => o, StringComparer.OrdinalIgnoreCase);

            // 查项次（合并 batch.OrderItemIds + workOrder.OrderItemIds）
            // 注意：OrderItemIds 存储的是 Sequence 值（项次号），不是 Id
            var allSequences = new List<int>();

            void CollectSequences(string? idsStr)
            {
                if (string.IsNullOrEmpty(idsStr)) return;
                foreach (var idStr in idsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (int.TryParse(idStr, out var id) && id > 0)
                        allSequences.Add(id);
                }
            }

            foreach (var batch in batches)
                CollectSequences(batch.OrderItemIds);

            if (workOrderDict != null)
            {
                foreach (var kvp in workOrderDict)
                    CollectSequences(kvp.Value.OrderItemIds);
            }

            allSequences = allSequences.Distinct().ToList();

            if (allSequences.Count > 0)
            {
                var items = await _context.Set<MES.Data.Entities.Order.OrderItem>()
                    .AsNoTracking()
                    .Where(oi => orderNos.Contains(oi.OrderNumber ?? "") && allSequences.Contains(oi.Sequence))
                    .Select(oi => new OrderItemInfo
                    {
                        OrderNumber = oi.OrderNumber ?? "",
                        Sequence = oi.Sequence,
                        StandardNo = oi.StandardNo,
                        StandardGrade = oi.StandardGrade,
                        DeliveryState = oi.DeliveryState.ToString()
                    })
                    .ToListAsync();

                // 用 "OrderNumber|Sequence" 作为复合 key，避免不同订单相同 Sequence 冲突
                itemDict = items.ToDictionary(i => $"{i.OrderNumber}|{i.Sequence}", i => i);
            }
        }

        // 3b. 查询 ProductionBatch 补充炉号（当仓库炉号为空时）
        var batchNosForHeat = batches
            .Where(b => string.IsNullOrEmpty(b.HeatNo) && !string.IsNullOrEmpty(b.ProductionBatchNo))
            .Select(b => b.ProductionBatchNo!)
            .Distinct()
            .ToList();

        Dictionary<string, string?>? productionBatchHeatMap = null;
        if (batchNosForHeat.Count > 0)
        {
            productionBatchHeatMap = await _context.Set<MES.Data.Entities.Batch.ProductionBatch>()
                .AsNoTracking()
                .Where(pb => batchNosForHeat.Contains(pb.BatchNo))
                .ToDictionaryAsync(pb => pb.BatchNo, pb => pb.SourceHeatNo, StringComparer.OrdinalIgnoreCase);
        }

        // 4. 组装 DTO
        var result = new List<PendingDeliveryItemDto>();

        foreach (var batch in batches)
        {
            // 优先从 WorkOrder 反推 SalesOrderNo 和 OrderItemIds
            var resolvedSalesOrderNo = batch.SalesOrderNo;
            var resolvedOrderItemIds = batch.OrderItemIds;

            if (!string.IsNullOrEmpty(batch.WorkOrderNo) && workOrderDict != null
                && workOrderDict.TryGetValue(batch.WorkOrderNo, out var woInfo))
            {
                if (!string.IsNullOrEmpty(woInfo.SalesOrderNo))
                    resolvedSalesOrderNo = woInfo.SalesOrderNo;
                if (!string.IsNullOrEmpty(woInfo.OrderItemIds))
                    resolvedOrderItemIds = woInfo.OrderItemIds;
            }

            // 解析该项次的 OrderItem
            string? itemStandardNo = null;
            string? itemStandardGrade = null;
            string? itemDeliveryStatus = null;

            if (!string.IsNullOrEmpty(resolvedOrderItemIds) && itemDict != null && !string.IsNullOrEmpty(resolvedSalesOrderNo))
            {
                var compositeKey = resolvedOrderItemIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(id => int.TryParse(id, out var i) ? i : 0)
                    .Where(id => id > 0)
                    .Select(seq => $"{resolvedSalesOrderNo}|{seq}")
                    .FirstOrDefault(key => itemDict.ContainsKey(key));

                if (compositeKey != null && itemDict.TryGetValue(compositeKey, out var itemInfo))
                {
                    itemStandardNo = itemInfo.StandardNo;
                    itemStandardGrade = itemInfo.StandardGrade;

                    if (!string.IsNullOrEmpty(itemInfo.DeliveryState))
                        itemDeliveryStatus = EnumHelper.GetDisplayName<DeliveryState>(itemInfo.DeliveryState);
                }
            }

            // 订单信息（使用权威的 SalesOrderNo）
            string? customerName = null;
            string? salesman = null;
            string? endCustomer = null;

            if (!string.IsNullOrEmpty(resolvedSalesOrderNo) && orderDict != null)
            {
                if (orderDict.TryGetValue(resolvedSalesOrderNo, out var orderInfo))
                {
                    customerName = orderInfo.CustomerName;
                    salesman = orderInfo.Salesman;
                    endCustomer = orderInfo.EndCustomer;
                }
            }

            result.Add(new PendingDeliveryItemDto
            {
                InventoryBatchNo = batch.BatchNo,
                MaterialType = EnumHelper.TryParse<MaterialType>(batch.MaterialType) ?? default,
                InboundSource = string.IsNullOrEmpty(batch.InboundSource) ? default : EnumHelper.TryParse<InboundSource>(batch.InboundSource) ?? default,
                SourceName = batch.SourceName,
                ProductionBatchNo = batch.ProductionBatchNo,
                HeatNo = !string.IsNullOrEmpty(batch.HeatNo)
                    ? batch.HeatNo
                    : productionBatchHeatMap?.GetValueOrDefault(batch.ProductionBatchNo ?? ""),
                PlantGrade = batch.PlantGrade,
                Specification = batch.Specification,
                LengthStatus = batch.LengthStatus,
                MinLength = batch.MinLength,
                MaxLength = batch.MaxLength,
                RemainingQuantity = batch.RemainingQuantity,
                RemainingWeight = batch.RemainingWeight,
                Meters = batch.Meters,
                RemainingMeters = batch.RemainingMeters,
                InboundDate = batch.InboundDate,
                SalesOrderNo = resolvedSalesOrderNo,
                OrderItemIds = resolvedOrderItemIds,
                WorkOrderNo = batch.WorkOrderNo,
                CustomerName = customerName,
                Salesman = salesman,
                EndCustomer = endCustomer,
                ProductStandard = itemStandardNo,
                DeliveryStatus = itemDeliveryStatus,
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
