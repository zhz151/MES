using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Exceptions;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Materials;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Materials;

public class SupplierService : ISupplierService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public SupplierService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<SupplierProfileDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.SupplierProfiles
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(s =>
                s.SupplierCode.Contains(kw) ||
                s.SupplierName.Contains(kw) ||
                (s.MaterialCategory != null && s.MaterialCategory.Contains(kw)) ||
                (s.ContactPerson != null && s.ContactPerson.Contains(kw)) ||
                (s.ContactPhone != null && s.ContactPhone.Contains(kw)) ||
                (s.Address != null && s.Address.Contains(kw)) ||
                (s.Remark != null && s.Remark.Contains(kw)));
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        queryable = queryable.ApplySort(query.SortBy, query.IsDescending);

        // 区间模式（出单区间 / 到货区间任一有值）：按「激活列有数据」过滤供应商行后再分页（见下）
        var orderRangeMode = query.SupplierOrderDateFrom.HasValue || query.SupplierOrderDateTo.HasValue;
        var arrivalRangeMode = query.SupplierArrivalDateFrom.HasValue || query.SupplierArrivalDateTo.HasValue;
        if (orderRangeMode || arrivalRangeMode)
            return await GetPagedInRangeModeAsync(query, queryable, orderRangeMode, arrivalRangeMode);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(s => new
            {
                s.Id,
                s.SupplierCode,
                s.SupplierName,
                s.MaterialCategory,
                s.ContactPerson,
                s.ContactPhone,
                s.Address,
                s.IsActive,
                s.Remark,
                s.CreatedTime
            })
            .ToListAsync();

        var dtos = items.Select(s => ToDto(s.Id, s.SupplierCode, s.SupplierName, s.MaterialCategory,
            s.ContactPerson, s.ContactPhone, s.Address, s.IsActive, s.Remark, s.CreatedTime)).ToList();

        // ② 往来信息统计回填（采购+委外合并，仅当前页明细行聚合）
        await AttachTradeStatsAsync(dtos);

        return new PagedResult<SupplierProfileDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    private static SupplierProfileDto ToDto(int id, string code, string name, string? materialCategory,
        string? contactPerson, string? contactPhone, string? address, bool isActive, string? remark, DateTimeOffset createdTime)
        => new()
        {
            Id = id,
            SupplierCode = code,
            SupplierName = name,
            MaterialCategory = EnumHelper.TryParse<MaterialType>(materialCategory),
            ContactPerson = contactPerson,
            ContactPhone = contactPhone,
            Address = address,
            IsActive = isActive,
            Remark = remark,
            CreatedTime = createdTime
        };

    /// <summary>
    /// 区间模式分页：出单区间 / 到货区间任一生效时，只保留「激活列有数据」的供应商行
    /// （与报表总览「供应商往来数据」卡的行过滤同口径）。行过滤须在分页之前完成，故内存过滤后再切页——
    /// 供应商档案量级小（百位数量级），代价可忽略；<c>TotalCount</c> 返回过滤后条数，前端「共 N 条记录」与显示行一致。
    /// </summary>
    private async Task<PagedResult<SupplierProfileDto>> GetPagedInRangeModeAsync(
        QueryParams query, IQueryable<SupplierProfile> queryable, bool orderRangeMode, bool arrivalRangeMode)
    {
        var rows = await queryable.Select(s => new
        {
            s.Id,
            s.SupplierCode,
            s.SupplierName,
            s.MaterialCategory,
            s.ContactPerson,
            s.ContactPhone,
            s.Address,
            s.IsActive,
            s.Remark,
            s.CreatedTime
        }).ToListAsync();

        var statsRows = rows.Select(r => new SupplierStatsRow
        {
            Id = r.Id,
            SupplierName = r.SupplierName,
            MaterialCategory = r.MaterialCategory ?? ""
        }).ToList();

        var buckets = await BuildTradeStatsAsync(_context, DateTime.Today.Year, statsRows,
            query.SupplierOrderDateFrom, query.SupplierOrderDateTo,
            query.SupplierArrivalDateFrom, query.SupplierArrivalDateTo);

        var activeColumns = ActiveStatsColumns(orderRangeMode, arrivalRangeMode);

        var filtered = rows.Where(r => HasDataInActiveColumns(buckets, r.Id, activeColumns)).ToList();

        var dtos = filtered
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => ToDto(r.Id, r.SupplierCode, r.SupplierName, r.MaterialCategory,
                r.ContactPerson, r.ContactPhone, r.Address, r.IsActive, r.Remark, r.CreatedTime))
            .ToList();

        // ② 往来信息统计回填（同一份桶，随行过滤复用，不重复聚合）
        foreach (var dto in dtos)
        {
            if (buckets.TryGetValue(dto.Id, out var b))
                ApplyStats(dto, b);
        }

        return new PagedResult<SupplierProfileDto>
        {
            Items = dtos,
            TotalCount = filtered.Count,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    // ========== ② 供应商往来统计（采购 + 委外合并，列表回填） ==========

    /// <summary>供应商统计累加桶（键=供应商档案行 Id，可变字段避免逐单建 DTO）</summary>
    private sealed class SupplierStatsBucket
    {
        public int TotalCount;
        public decimal TotalWeight;
        public decimal TotalAmount;
        public int YearCount;
        public decimal YearWeight;
        public decimal YearAmount;
        public decimal ArrivedWeight;
        public decimal PendingWeight;
        public decimal YearReturnWeight;
        public decimal ArrivedAmount;      // 到货货款（循环先累毛货款，收尾扣本年退货货款净额化）
        public decimal PendingAmount;      // 待收货款（未完成单欠交货款）
        public decimal YearReturnAmount;   // 本年退货货款（仅用于净额化，不输出 DTO）
    }

    /// <summary>本页供应商档案行 =「供应商名称 + 物料分类」组合（真实场景同名供应商跨分类多档案，列表每行一行）</summary>
    private sealed class SupplierStatsRow
    {
        public int Id;
        public string SupplierName = null!;
        public string MaterialCategory = null!;
    }

    /// <summary>
    /// 为当前页供应商明细回填 ② 往来信息 9 字段。
    /// 列表每行 =「供应商名 + 物料分类」档案，单据按同名+同分类精确分流到该行，
    /// 与列表行标签一致（真实库 217 采购单 105 单 SupplierId 只指向同名档案之一但单头分类各异 → 不能按 SupplierId 混算）。
    /// 两个**互相独立**的区间，可单独或叠加使用：<paramref name="orderFrom"/>/<paramref name="orderTo"/>（出单）、
    /// <paramref name="arrivalFrom"/>/<paramref name="arrivalTo"/>（到货）。未指定者仍按自然年 <c>year</c> 统计。
    /// </summary>
    private async Task AttachTradeStatsAsync(List<SupplierProfileDto> items,
        DateTime? orderFrom = null, DateTime? orderTo = null,
        DateTime? arrivalFrom = null, DateTime? arrivalTo = null)
    {
        if (items.Count == 0)
            return;

        var year = DateTime.Today.Year;
        var rows = items.Select(x => new SupplierStatsRow
        {
            Id = x.Id,
            SupplierName = x.SupplierName,
            MaterialCategory = x.MaterialCategory?.ToString() ?? ""
        }).ToList();

        var buckets = await BuildTradeStatsAsync(_context, year, rows, orderFrom, orderTo, arrivalFrom, arrivalTo);

        foreach (var dto in items)
        {
            if (buckets.TryGetValue(dto.Id, out var b))
                ApplyStats(dto, b);
        }
    }

    /// <summary>把统计桶回填到 DTO 的 9 个往来字段（出单类 3 + 到货 2 + 待收 2 + 退货 1，另含累计类由桶直接带出）</summary>
    private static void ApplyStats(SupplierProfileDto dto, SupplierStatsBucket b)
    {
        dto.TotalOrderCount = b.TotalCount;
        dto.TotalWeight = b.TotalWeight;
        dto.TotalAmount = b.TotalAmount;
        dto.YearOrderCount = b.YearCount;
        dto.YearWeight = b.YearWeight;
        dto.YearAmount = b.YearAmount;
        dto.ArrivedWeight = b.ArrivedWeight;
        dto.ArrivedAmount = b.ArrivedAmount;
        dto.PendingWeight = b.PendingWeight;
        dto.PendingAmount = b.PendingAmount;
        dto.YearReturnWeight = b.YearReturnWeight;
    }

    // ========== 区间模式下的「激活列」（与报表总览「供应商往来数据」卡的列激活同口径，两处须同步） ==========

    /// <summary>出单区间生效时唯一有数据的统计列</summary>
    private static readonly string[] OrderRangeColumns = ["YearOrder"];
    /// <summary>到货区间生效时有数据的统计列（到货净重 + 本年退货，两者同源同一区间窗口）</summary>
    private static readonly string[] ArrivalRangeColumns = ["Arrived", "YearReturn"];

    private static string[] ActiveStatsColumns(bool orderRangeMode, bool arrivalRangeMode)
    {
        var list = new List<string>(3);
        if (orderRangeMode) list.AddRange(OrderRangeColumns);
        if (arrivalRangeMode) list.AddRange(ArrivalRangeColumns);
        return list.ToArray();
    }

    /// <summary>该供应商档案行在激活列上是否有数据（单数或重量任一 &gt; 0；无桶即无数据）</summary>
    private static bool HasDataInActiveColumns(
        Dictionary<int, SupplierStatsBucket> buckets, int id, string[] activeColumns)
    {
        if (!buckets.TryGetValue(id, out var b))
            return false;

        foreach (var col in activeColumns)
        {
            var (count, weight) = col switch
            {
                "YearOrder" => (b.YearCount, b.YearWeight),
                "Arrived" => (0, b.ArrivedWeight),
                "YearReturn" => (0, b.YearReturnWeight),
                _ => (0, 0m)
            };
            if (count > 0 || weight > 0m)
                return true;
        }
        return false;
    }

    private static string BuildRowKey(string name, string category) => name + "\u0001" + category;

    /// <summary>按「供应商名 + 单头分类」解析页内档案行 Id（分类在本页无对应档案行 → null，单据不属本页任何行）</summary>
    private static int? ResolveProfileId(Dictionary<string, int> idByKey, string? name, string? category)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(category)) return null;
        return idByKey.TryGetValue(BuildRowKey(name!, category!), out var id) ? id : null;
    }

    /// <summary>
    /// 按「供应商名称 + 物料分类」归档案行：采购主表(PO) + 委外主表(WW) 两类单据合并统计（物理删除即无行）。
    /// 口径（唯一事实源）：
    /// - 累计/本年出单：单数、吨(kg)、元(采购 TotalAmount + 委外 Σ 子项 ProcessTotalAmount，主表无落库合计须子表 group 一次带出)；
    /// - 本年到货[扣退货]净(kg) = 本年到货毛 − 本年退货；本年到货毛=仓库入厂批(InventoryBatch.InboundSource∈{Purchase,Subcontract} 且 InboundDate.Year==year)按单累加 InitialWeight 归档案行；退货扣减与「本年退货」列同源(退货出库年==year)；
    /// - 本年到货货款(元，参考) = Σ 单据级认领：命中档案行的各采购/委外单 该单金额 × 该单(本年到货毛重−本年退货重)/应到重，负数截 0——**按单号聚合，非笼统全局单价**；单金额=采购 TotalAmount、委外 Σ子项 ProcessTotalAmount；
    /// - 待收货(kg) = 仅 !IsForceCompleted 且 Status∉{Completed,OverReceived} 的单计 Max(0, 应到 − 累计净到)：采购应到=Weight、累计净到=ReceivedWeight−跨年退货快照；委外应到=OutWeight、累计净到=InWeight−跨年退货快照（待收货为当前时点欠交，非年份口径）；
    /// - 待收货货款(元，参考) = 同批未完成单 该单金额 × 欠交净重/应到重；
    /// - 本年退货(kg) = 退货出库 OutboundRecord(OutboundType==ReturnOut) 按 OutboundDate.Year==year 累计。
    /// 归行键：采购=PO.SupplierName+PO.MaterialCategory；委外=WW.SupplierName+WW.OutMaterialCategory（与供应商档案行名+分类精确相等，OrdinalIgnoreCase）。
    /// 退货关联链：ReturnOut.ReturnSourceBatchNo → InventoryBatch.BatchNo → InventoryBatch.SourceOrderNo(CG=采购/WW=委外) → 订单号 → 订单(名,分类) → 档案行。
    /// <para>
    /// **双区间（与客户往来同构，两个互相独立可叠加的窗口）**：
    /// <list type="bullet">
    /// <item><b>出单区间</b>（<paramref name="orderFrom"/>/<paramref name="orderTo"/> 任一有值）：出单类列（<c>YearOrder*</c>）改按 <c>OrderDate ∈ 区间</c>。</item>
    /// <item><b>到货区间</b>（<paramref name="arrivalFrom"/>/<paramref name="arrivalTo"/> 任一有值）：到货类列（到货毛/净重、到货货款）与「本年退货」列改按 <c>InboundDate</c> / <c>OutboundDate ∈ 区间</c>（两者同源同一窗口）。</item>
    /// </list>
    /// 未指定区间的维度仍按自然年 <paramref name="year"/> 统计；累计出单与待收货（存量口径）恒为原语义。
    /// 前端在任一区间生效时，把不属于该区间的列渲染为「—」（详见 <c>ReportOverview</c> 的列激活判定）。结束日按闭区间含当天处理。
    /// </para>
    /// </summary>
    private static async Task<Dictionary<int, SupplierStatsBucket>> BuildTradeStatsAsync(
        AppDbContext ctx, int year, List<SupplierStatsRow> rows,
        DateTime? orderFrom = null, DateTime? orderTo = null,
        DateTime? arrivalFrom = null, DateTime? arrivalTo = null)
    {
        var buckets = new Dictionary<int, SupplierStatsBucket>();
        if (rows.Count == 0)
            return buckets;

        // 出单窗口：区间模式 ? [orderFrom, orderTo] : 自然年
        var orderRangeMode = orderFrom.HasValue || orderTo.HasValue;
        var orderFromBound = orderFrom?.Date;
        var orderToBoundExclusive = orderTo?.Date.AddDays(1);
        // 到货窗口：区间模式 ? [arrivalFrom, arrivalTo] : 自然年（退货归属同该窗口，因到货净额 = 到货毛 − 退货）
        var arrivalRangeMode = arrivalFrom.HasValue || arrivalTo.HasValue;
        var arrivalFromBound = arrivalFrom?.Date;
        var arrivalToBoundExclusive = arrivalTo?.Date.AddDays(1);

        // 入厂批是否落在到货窗口（区间模式按闭区间，否则按自然年）
        bool InArrivalWindow(DateTimeOffset d) => arrivalRangeMode
            ? (!arrivalFromBound.HasValue || d.Date >= arrivalFromBound.Value)
              && (!arrivalToBoundExclusive.HasValue || d.Date < arrivalToBoundExclusive.Value)
            : d.Year == year;

        // 页内供应商名（同名跨分类档案共享，再由分类精确分流到行）
        var names = rows.Select(r => r.SupplierName).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
        if (names.Count == 0)
            return buckets;

        // 页内档案行 名+分类 → Id
        var idByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
            if (!string.IsNullOrEmpty(r.SupplierName) && !string.IsNullOrEmpty(r.MaterialCategory))
                idByKey.TryAdd(BuildRowKey(r.SupplierName, r.MaterialCategory), r.Id);

        var poRows = await ctx.PurchaseOrders.AsNoTracking()
            .Where(p => names.Contains(p.SupplierName!))
            .Select(p => new
            {
                p.OrderNo,
                p.SupplierName,
                MaterialCategory = p.MaterialCategory,
                p.OrderDate,
                p.Status,
                p.IsForceCompleted,
                p.Weight,
                p.ReceivedWeight,
                Amount = p.TotalAmount ?? 0m
            })
            .ToListAsync();

        var wwRows = await ctx.SubcontractOrders.AsNoTracking()
            .Where(w => names.Contains(w.SupplierName!))
            .Select(w => new
            {
                w.Id,
                w.OrderNo,
                w.SupplierName,
                OutMaterialCategory = w.OutMaterialCategory,
                w.OrderDate,
                w.Status,
                w.IsForceCompleted,
                w.OutWeight,
                InWeight = w.InWeight ?? 0m
            })
            .ToListAsync();

        // 委外加工费：主表无落库合计，仅本页委外单的子表一次 group 带出（避免 N+1 / 全表扫描）
        Dictionary<int, decimal> wwAmounts = new();
        if (wwRows.Count > 0)
        {
            var wwIds = wwRows.Select(w => w.Id).ToList();
            wwAmounts = await ctx.SubcontractReturnItems.AsNoTracking()
                .Where(i => i.ProcessTotalAmount.HasValue && wwIds.Contains(i.SubcontractOrderId))
                .GroupBy(i => i.SubcontractOrderId)
                .Select(g => new { OrderId = g.Key, Amount = g.Sum(i => i.ProcessTotalAmount) ?? 0m })
                .ToDictionaryAsync(x => x.OrderId, x => x.Amount);
        }

        // 订单号 →（供应商名, 分类）：退货出库归单后仍需按 (名,分类) 落到档案行
        var orderKey = new Dictionary<string, (string Name, string Category)>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in poRows)
            if (!string.IsNullOrEmpty(p.SupplierName))
                orderKey.TryAdd(p.OrderNo, (p.SupplierName!, p.MaterialCategory));
        foreach (var w in wwRows)
            if (!string.IsNullOrEmpty(w.SupplierName))
                orderKey.TryAdd(w.OrderNo, (w.SupplierName!, w.OutMaterialCategory));

        // 单次仓库批查询做两件事（一次带出避免重复扫）：
        // ① 退货归单：ReturnOut.ReturnSourceBatchNo → 批.BatchNo → 批.SourceOrderNo(采购单/委外单号)
        // ② 到货毛：批.InboundSource∈{Purchase,Subcontract} 且批.InboundDate 落入到货窗口 → 按单累计 InitialWeight（到货发生在到货窗口，不按下单年）
        var orderReturn = new Dictionary<string, (decimal All, decimal Year)>(StringComparer.OrdinalIgnoreCase);
        var arrivalByOrder = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (orderKey.Count > 0)
        {
            var orderNos = orderKey.Keys.ToList();
            var batches = await ctx.InventoryBatches.AsNoTracking()
                .Where(b => b.SourceOrderNo != null && orderNos.Contains(b.SourceOrderNo))
                .Select(b => new { b.BatchNo, b.SourceOrderNo, b.InboundSource, b.InboundDate, b.InitialWeight })
                .ToListAsync();

            var batchNoToOrderNo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in batches)
            {
                if (string.IsNullOrEmpty(b.SourceOrderNo)) continue;

                if (!string.IsNullOrEmpty(b.BatchNo))
                    batchNoToOrderNo.TryAdd(b.BatchNo!, b.SourceOrderNo!);

                if (InArrivalWindow(b.InboundDate)
                    && (b.InboundSource == "Purchase" || b.InboundSource == "Subcontract"))
                {
                    arrivalByOrder[b.SourceOrderNo!] = arrivalByOrder.GetValueOrDefault(b.SourceOrderNo!) + b.InitialWeight;
                }
            }

            if (batchNoToOrderNo.Count > 0)
            {
                foreach (var chunk in batchNoToOrderNo.Keys.Chunk(1000))
                {
                    var ids = chunk.ToList();
                    var returns = await ctx.OutboundRecords.AsNoTracking()
                        .Where(o => o.OutboundType == OutboundType.ReturnOut
                                 && o.ReturnSourceBatchNo != null
                                 && ids.Contains(o.ReturnSourceBatchNo))
                        .Select(o => new { o.ReturnSourceBatchNo, o.OutboundDate, o.OutboundWeight })
                        .ToListAsync();

                    foreach (var r in returns)
                    {
                        if (!batchNoToOrderNo.TryGetValue(r.ReturnSourceBatchNo!, out var orderNo))
                            continue;
                        var (all, yr) = orderReturn.GetValueOrDefault(orderNo);
                        all += r.OutboundWeight;
                        if (InArrivalWindow(r.OutboundDate)) yr += r.OutboundWeight;
                        orderReturn[orderNo] = (all, yr);
                    }
                }
            }
        }

        // 采购订单分流（应到=Weight、毛到货=ReceivedWeight）—— 归到 (SupplierName, MaterialCategory) 命中的档案行
        foreach (var p in poRows)
        {
            var targetId = ResolveProfileId(idByKey, p.SupplierName, p.MaterialCategory);
            if (targetId == null) continue; // 该单分类在本页无对应档案行 → 不属本页任何行

            var ret = orderReturn.GetValueOrDefault(p.OrderNo);
            var netArrived = Math.Max(0m, p.ReceivedWeight - ret.All);
            var b = GetSupplierBucket(buckets, targetId.Value);

            b.TotalCount++;
            b.TotalWeight += p.Weight;
            b.TotalAmount += p.Amount;
            // 出单窗口判定：出单区间模式 = OrderDate 落 [orderFrom, orderTo]（闭区间）；否则 = 自然年
            var isOrderedInWindow = orderRangeMode
                ? (!orderFromBound.HasValue || p.OrderDate.Date >= orderFromBound.Value)
                  && (!orderToBoundExclusive.HasValue || p.OrderDate.Date < orderToBoundExclusive.Value)
                : p.OrderDate.Year == year;
            if (isOrderedInWindow)
            {
                b.YearCount++;
                b.YearWeight += p.Weight;
                b.YearAmount += p.Amount;
            }
            // 到货货款（毛）与本年退货货款：按本单金额 × 重量份额认领（单号级聚合，非全局单价）；收尾统一净额化
            var windowArrival = arrivalByOrder.GetValueOrDefault(p.OrderNo);
            b.ArrivedWeight += windowArrival; // 窗口到货毛（入厂批落在到货窗口，非 ReceivedWeight 累计快照）
            if (p.Weight > 0m && p.Amount > 0m)
            {
                if (windowArrival > 0m)
                    b.ArrivedAmount += p.Amount * windowArrival / p.Weight;
                if (ret.Year > 0m)
                    b.YearReturnAmount += p.Amount * ret.Year / p.Weight;
            }
            if (!p.IsForceCompleted && p.Status != PurchaseOrderStatus.Completed && p.Status != PurchaseOrderStatus.OverReceived)
            {
                var pendingKg = Math.Max(0m, p.Weight - netArrived);
                b.PendingWeight += pendingKg;
                if (pendingKg > 0m && p.Weight > 0m && p.Amount > 0m)
                    b.PendingAmount += p.Amount * pendingKg / p.Weight;
            }
            b.YearReturnWeight += ret.Year;
        }

        // 委外订单分流（应到=OutWeight 发出、毛到货=InWeight 收回快照，与委外完成判定 RecalcSubcontractStatus 同源）
        foreach (var w in wwRows)
        {
            var targetId = ResolveProfileId(idByKey, w.SupplierName, w.OutMaterialCategory);
            if (targetId == null) continue;

            var ret = orderReturn.GetValueOrDefault(w.OrderNo);
            var netArrived = Math.Max(0m, w.InWeight - ret.All);
            var amount = wwAmounts.GetValueOrDefault(w.Id);
            var b = GetSupplierBucket(buckets, targetId.Value);

            b.TotalCount++;
            b.TotalWeight += w.OutWeight;
            b.TotalAmount += amount;
            // 出单窗口判定：出单区间模式 = OrderDate 落 [orderFrom, orderTo]（闭区间）；否则 = 自然年
            var isOrderedInWindow = orderRangeMode
                ? (!orderFromBound.HasValue || w.OrderDate.Date >= orderFromBound.Value)
                  && (!orderToBoundExclusive.HasValue || w.OrderDate.Date < orderToBoundExclusive.Value)
                : w.OrderDate.Year == year;
            if (isOrderedInWindow)
            {
                b.YearCount++;
                b.YearWeight += w.OutWeight;
                b.YearAmount += amount;
            }
            // 委外到货/退货/待收货款：加工费(amount=Σ子项) × 重量份额认领（分母=发出 OutWeight）
            var windowArrival = arrivalByOrder.GetValueOrDefault(w.OrderNo);
            b.ArrivedWeight += windowArrival; // 窗口到货毛（收回入厂批落在到货窗口）
            if (w.OutWeight > 0m && amount > 0m)
            {
                if (windowArrival > 0m)
                    b.ArrivedAmount += amount * windowArrival / w.OutWeight;
                if (ret.Year > 0m)
                    b.YearReturnAmount += amount * ret.Year / w.OutWeight;
            }
            if (!w.IsForceCompleted && w.Status != SubcontractOrderStatus.Completed && w.Status != SubcontractOrderStatus.OverReceived)
            {
                var pendingKg = Math.Max(0m, w.OutWeight - netArrived);
                b.PendingWeight += pendingKg;
                if (pendingKg > 0m && w.OutWeight > 0m && amount > 0m)
                    b.PendingAmount += amount * pendingKg / w.OutWeight;
            }
            b.YearReturnWeight += ret.Year;
        }

        // 到货净 = 窗口到货毛 − 窗口退货；货款同步净额化（与「本年退货」列同源：退货出库落在到货窗口）；负数截 0
        foreach (var bucket in buckets.Values)
        {
            bucket.ArrivedWeight = Math.Max(0m, bucket.ArrivedWeight - bucket.YearReturnWeight);
            bucket.ArrivedAmount = Math.Max(0m, bucket.ArrivedAmount - bucket.YearReturnAmount);
        }

        return buckets;
    }

    private static SupplierStatsBucket GetSupplierBucket(Dictionary<int, SupplierStatsBucket> buckets, int supplierId)
    {
        if (!buckets.TryGetValue(supplierId, out var b))
        {
            b = new SupplierStatsBucket();
            buckets[supplierId] = b;
        }
        return b;
    }

    public async Task<SupplierProfileDto> GetByIdAsync(int id)
    {
        var entity = await _context.SupplierProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("供应商不存在");
        return ToDto(entity);
    }

    public async Task<List<SupplierProfileDto>> GetActiveAsync()
    {
        var items = await _context.SupplierProfiles
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SupplierName)
            .Select(s => new
            {
                s.Id,
                s.SupplierCode,
                s.SupplierName,
                s.MaterialCategory,
                s.ContactPerson,
                s.ContactPhone,
                s.Address,
                s.IsActive,
                s.Remark,
                s.CreatedTime
            })
            .ToListAsync();

        return items.Select(s => new SupplierProfileDto
        {
            Id = s.Id,
            SupplierCode = s.SupplierCode,
            SupplierName = s.SupplierName,
            MaterialCategory = EnumHelper.TryParse<MaterialType>(s.MaterialCategory),
            ContactPerson = s.ContactPerson,
            ContactPhone = s.ContactPhone,
            Address = s.Address,
            IsActive = s.IsActive,
            Remark = s.Remark,
            CreatedTime = s.CreatedTime
        }).ToList();
    }

    public async Task<SupplierProfileDto> CreateAsync(CreateSupplierRequest request)
    {
        var supplierCode = await CodeGenerator.GenerateNextAsync(
            _context.SupplierProfiles.Select(s => s.SupplierCode), "SU");

        var entity = new SupplierProfile
        {
            SupplierCode = supplierCode,
            SupplierName = request.SupplierName,
            MaterialCategory = request.MaterialCategory?.ToString(),
            ContactPerson = request.ContactPerson,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            IsActive = request.IsActive,
            Remark = request.Remark
        };

        _context.SupplierProfiles.Add(entity);
        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<List<SupplierProfileDto>> CreateBatchAsync(List<CreateSupplierRequest> requests)
    {
        if (requests.Count == 0) return new List<SupplierProfileDto>();

        // 预生成编码
        var maxCode = await _context.SupplierProfiles
            .Where(s => s.SupplierCode.StartsWith("SU") && s.SupplierCode.Length == 6)
            .OrderByDescending(s => s.SupplierCode)
            .Select(s => s.SupplierCode)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (maxCode != null && int.TryParse(maxCode[2..], out var lastSeq))
            sequence = lastSeq + 1;

        var entities = new List<SupplierProfile>(requests.Count);
        for (int i = 0; i < requests.Count; i++)
        {
            var r = requests[i];
            var code = $"SU{sequence + i:D4}";
            entities.Add(new SupplierProfile
            {
                SupplierCode = code,
                SupplierName = r.SupplierName,
                MaterialCategory = r.MaterialCategory?.ToString(),
                ContactPerson = r.ContactPerson,
                ContactPhone = r.ContactPhone,
                Address = r.Address,
                IsActive = r.IsActive,
                Remark = r.Remark
            });
        }

        _context.SupplierProfiles.AddRange(entities);
        await _context.SaveChangesAsync();
        return entities.Select(ToDto).ToList();
    }

    public async Task<SupplierProfileDto> UpdateAsync(int id, UpdateSupplierRequest request)
    {
        var entity = await _context.SupplierProfiles
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("供应商不存在");

        if (request.SupplierName != null) entity.SupplierName = request.SupplierName;
        if (request.MaterialCategory != null) entity.MaterialCategory = request.MaterialCategory.Value.ToString();
        if (request.ContactPerson != null) entity.ContactPerson = request.ContactPerson;
        if (request.ContactPhone != null) entity.ContactPhone = request.ContactPhone;
        if (request.Address != null) entity.Address = request.Address;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        if (request.Remark != null) entity.Remark = request.Remark;

        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.SupplierProfiles
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("供应商不存在");

        _context.SupplierProfiles.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // ========== 打印 ==========

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.SupplierFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            var query = _context.SupplierProfiles.AsNoTracking();
            return new Dictionary<string, List<string>>
            {
                ["SupplierCode"] = await query.Where(s => s.SupplierCode != null).Select(s => s.SupplierCode).Distinct().OrderBy(x => x).ToListAsync(),
                ["SupplierName"] = await query.Where(s => s.SupplierName != null).Select(s => s.SupplierName).Distinct().OrderBy(x => x).ToListAsync(),
                ["MaterialCategory"] = await query.Where(s => s.MaterialCategory != null).Select(s => s.MaterialCategory!).Distinct().OrderBy(x => x).ToListAsync(),
                ["ContactPerson"] = await query.Where(s => s.ContactPerson != null).Select(s => s.ContactPerson!).Distinct().OrderBy(x => x).ToListAsync(),
                ["ContactPhone"] = await query.Where(s => s.ContactPhone != null).Select(s => s.ContactPhone!).Distinct().OrderBy(x => x).ToListAsync(),
                ["Address"] = await query.Where(s => s.Address != null).Select(s => s.Address!).Distinct().OrderBy(x => x).ToListAsync(),
                ["Remark"] = await query.Where(s => s.Remark != null).Select(s => s.Remark!).Distinct().OrderBy(x => x).ToListAsync(),
                ["IsActive"] = await query.Select(s => s.IsActive.ToString()).Distinct().OrderBy(x => x).ToListAsync(),
            };

        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<byte[]> PrintSupplierBatchAsync(int[] ids, List<PrintColumnDef>? columns = null)
    {
        var result = new List<SupplierProfileDto>();
        foreach (var id in ids)
        {
            try
            {
                result.Add(await GetByIdAsync(id));
            }
            catch (BusinessException) { /* 跳过不存在的供应商 */ }
        }
        // TablePrintHelper 对强类型 DTO 反射取值（FormatValue 自动处理枚举中文/日期/数值 G29），
        // 仅对口径异于通用格式的列配 resolver，避免手写逐 key 白名单（加列漏同步即静默空白）。
        return TablePrintHelper.GeneratePdf("供应商档案列表", result, columns ?? [],
            new Dictionary<string, Func<object?, string>>
            {
                ["IsActive"] = v => v is bool b && b ? "启用" : "停用",
            });
    }

    /// <summary>列表打印（Mode A）：前端已按可见列把当前页转成字典行，服务端仅做表格渲染（统计列文本 Mode A 才带出，Mode B 反射不到派生字段）</summary>
    public Task<byte[]> PrintSupplierListAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        // 与客户/订单列表打印同款：自适应列宽 + 单元格居中 + 表头自动换行
        return Task.FromResult(TablePrintHelper.GeneratePdf(
            string.IsNullOrWhiteSpace(title) ? "供应商列表" : title,
            items,
            columns ?? [],
            autoWidth: true,
            alignCenter: true,
            headerMaxLines: 0));
    }

    private static SupplierProfileDto ToDto(SupplierProfile entity) => new()
    {
        Id = entity.Id,
        SupplierCode = entity.SupplierCode,
        SupplierName = entity.SupplierName,
        MaterialCategory = EnumHelper.TryParse<MaterialType>(entity.MaterialCategory),
        ContactPerson = entity.ContactPerson,
        ContactPhone = entity.ContactPhone,
        Address = entity.Address,
        IsActive = entity.IsActive,
        Remark = entity.Remark,
        CreatedTime = entity.CreatedTime
    };
}
