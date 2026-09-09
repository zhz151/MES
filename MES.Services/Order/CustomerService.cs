// 文件路径: MES.Services/CustomerService.cs
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
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Services.Printing;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Order;
using MES.Services.Helpers;
using MES.Services.Order;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Order;

/// <summary>
/// Customer profile service implementation
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public CustomerService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>
    /// Get paged customer list
    /// </summary>
    public async Task<PagedResult<CustomerProfileDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.CustomerProfiles
            .AsNoTracking()
            .AsQueryable();

        // Keyword search（多关键词AND + 状态中文映射）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                CustomerStatus? parsedStatus = keyword switch
                {
                    "启用" => CustomerStatus.Active,
                    "停用" => CustomerStatus.Inactive,
                    _ => null
                };
                queryable = queryable.Where(c =>
                    c.CustomerCode.Contains(keyword) ||
                    c.CustomerUnit.Contains(keyword) ||
                    c.Salesman.Contains(keyword) ||
                    (c.EndCustomer != null && c.EndCustomer.Contains(keyword)) ||
                    (parsedStatus.HasValue && c.Status == parsedStatus.Value) ||
                    (c.ContactPerson != null && c.ContactPerson.Contains(keyword)) ||
                    (c.ContactPhone != null && c.ContactPhone.Contains(keyword)) ||
                    (c.Address != null && c.Address.Contains(keyword)) ||
                    (c.Remark != null && c.Remark.Contains(keyword)));
            }
        }

        // 通用筛选（仅实体列筛选；客户统计 8 列为派生列不参与筛选）
        queryable = queryable.ApplyFilters(query.Filters);

        // Sorting
        queryable = queryable.ApplySort(query.SortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(c => new CustomerProfileDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                Salesman = c.Salesman,
                CustomerUnit = c.CustomerUnit,
                EndCustomer = c.EndCustomer,
                ContactPerson = c.ContactPerson,
                ContactPhone = c.ContactPhone,
                Address = c.Address,
                Status = c.Status,  // 直接赋值枚举，不再调用 ToString()
                Remark = c.Remark
            })
            .ToListAsync();

        // 注入客户业务统计 8 列（按「业务员+最终用户」关联订单聚合，仅当前页明细行回填）
        await AttachStatsAsync(items);

        return new PagedResult<CustomerProfileDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    // ========== 客户业务统计 8 列（按「业务员+最终用户」关联订单聚合，金额结算分治） ==========

    /// <summary>为客户列表注入业务统计 8 列（每页加载时按当前自然年计算一次全量订单聚合，量级小）</summary>
    private async Task AttachStatsAsync(List<CustomerProfileDto> items)
    {
        if (items.Count == 0)
            return;

        var year = DateTime.Now.Year;
        var buckets = await BuildStatsAsync(_context, year);

        foreach (var dto in items)
        {
            var key = MakeKey(dto.Salesman, dto.EndCustomer);
            if (string.IsNullOrEmpty(key) || !buckets.TryGetValue(key, out var b))
                continue;

            dto.TotalOrderCount = b.TotalCount;
            dto.TotalOrderWeight = b.TotalWeight;
            dto.TotalOrderAmount = b.TotalAmount;
            dto.YearOrderCount = b.YearCount;
            dto.YearOrderWeight = b.YearWeight;
            dto.YearOrderAmount = b.YearAmount;
            dto.ShippedCompletedCount = b.ShippedDoneCount;
            dto.ShippedCompletedWeight = b.ShippedDoneWeight;
            dto.ShippedCompletedAmount = b.ShippedDoneAmount;
            dto.ShippedOtherCount = b.ShippedOtherCount;
            dto.ShippedOtherWeight = b.ShippedOtherWeight;
            dto.ShippedOtherAmount = b.ShippedOtherAmount;
            dto.StockCompletedCount = b.StockDoneCount;
            dto.StockCompletedWeight = b.StockDoneWeight;
            dto.StockCompletedAmount = b.StockDoneAmount;
            dto.StockOtherCount = b.StockOtherCount;
            dto.StockOtherWeight = b.StockOtherWeight;
            dto.StockOtherAmount = b.StockOtherAmount;
            dto.WipNoneCount = b.WipNoneCount;
            dto.WipNoneWeight = b.WipNoneWeight;
            dto.WipNoneAmount = b.WipNoneAmount;
            dto.WipPartialCount = b.WipPartialCount;
            dto.WipPartialWeight = b.WipPartialWeight;
            dto.WipPartialAmount = b.WipPartialAmount;
        }
    }

    /// <summary>整单级订单→(业务员,最终用户) 统计桶。金额按结算分治：过磅=实际公斤计价不封顶；理算/过磅-负=封顶合同（发货→库存→在产阶梯认领，超产余料不计价）。</summary>
    private static async Task<Dictionary<string, CustomerStatsBucket>> BuildStatsAsync(AppDbContext ctx, int year)
    {
        // 1) 非取消订单基础信息（订单快照客户字段为文本关联键）
        var orders = await ctx.SalesOrders.AsNoTracking()
            .Where(so => so.Status != SalesOrderStatus.Cancelled)
            .Select(so => new { so.Id, so.OrderNumber, so.Salesman, so.EndCustomer, SignYear = so.SignDate.Year })
            .ToListAsync();

        // 2) 订单成品读模型（入库/出库/库存 权威物化源 + 执行关注阶段）
        var summaries = await ctx.Set<OrderListSummary>().AsNoTracking()
            .Select(s => new { s.OrderId, s.ScheduleStage, s.FinishedInboundWeight, s.FinishedOutboundWeight, s.FinishedStockWeight })
            .ToListAsync();
        var summaryById = summaries.ToDictionary(s => s.OrderId);

        // 3) 项次按单/结算池聚合（固定池 = 理算+过磅-负；过磅池 = 单过磅）
        var itemRows = await ctx.OrderItems.AsNoTracking()
            .Select(oi => new { oi.SalesOrderId, IsFixed = oi.SettlementMethod != SettlementMethod.Weighing, oi.ContractWeight, Amount = oi.TotalPrice ?? 0m })
            .ToListAsync();
        var poolsById = new Dictionary<int, (decimal WWeigh, decimal MWeigh, decimal WFixed, decimal MFixed)>();
        foreach (var g in itemRows.GroupBy(x => x.SalesOrderId))
        {
            decimal ww = 0m, mw = 0m, wf = 0m, mf = 0m;
            foreach (var it in g)
            {
                if (it.IsFixed) { wf += it.ContractWeight; mf += it.Amount; }
                else { ww += it.ContractWeight; mw += it.Amount; }
            }
            poolsById[g.Key] = (ww, mw, wf, mf);
        }

        // 4) 本年(自然年)销售出库重量：成品批次(OrderFinished)按 SalesOrderNo 归单，仅 SalesOut + 出库日期在目标年
        var finishedBatches = await ctx.InventoryBatches.AsNoTracking()
            .Where(ib => ib.MaterialType == InventoryMaterialTypes.OrderFinished && ib.SalesOrderNo != null)
            .Select(ib => new { ib.Id, ib.SalesOrderNo })
            .ToListAsync();
        var salesNoByBatchId = new Dictionary<int, string>(finishedBatches.Count);
        foreach (var fb in finishedBatches)
            salesNoByBatchId[fb.Id] = fb.SalesOrderNo!;

        var yearOutByOrder = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (salesNoByBatchId.Count > 0)
        {
            foreach (var chunk in salesNoByBatchId.Keys.Chunk(1000))
            {
                var ids = chunk.ToList();
                var outRows = await ctx.OutboundRecords.AsNoTracking()
                    .Where(r => r.OutboundType == OutboundType.SalesOut
                        && r.OutboundDate.Year == year
                        && ids.Contains(r.InventoryBatchId))
                    .Select(r => new { r.InventoryBatchId, r.OutboundWeight })
                    .ToListAsync();
                foreach (var r in outRows)
                {
                    if (!salesNoByBatchId.TryGetValue(r.InventoryBatchId, out var so))
                        continue;
                    yearOutByOrder.TryGetValue(so, out var cur);
                    yearOutByOrder[so] = cur + r.OutboundWeight;
                }
            }
        }

        // 5) 逐单折算金额并归桶
        var buckets = new Dictionary<string, CustomerStatsBucket>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in orders)
        {
            var key = MakeKey(o.Salesman, o.EndCustomer);
            if (string.IsNullOrEmpty(key))
                continue;
            if (!poolsById.TryGetValue(o.Id, out var pools))
                continue;

            var totalWeight = pools.WWeigh + pools.WFixed;
            var totalAmount = pools.MWeigh + pools.MFixed;
            if (totalWeight <= 0m)
                continue;

            summaryById.TryGetValue(o.Id, out var s);
            var inbound = s?.FinishedInboundWeight ?? 0m;
            var outbound = s?.FinishedOutboundWeight ?? 0m;
            var stock = s?.FinishedStockWeight ?? 0m;

            // 池级金额（重量切片按各池合同重占比近似划池；与报表业务总况同用 SettlementMoneyCalculator，防口径漂移）
            var (shipMoney, stockMoney, wipMoney) = SettlementMoneyCalculator.SplitOrder(
                inbound, outbound, stock,
                pools.WWeigh, pools.MWeigh, pools.WFixed, pools.MFixed);

            var isCompleted = s?.ScheduleStage == 1;
            var isSignedThisYear = o.SignYear == year;

            var yearOutKg = yearOutByOrder.GetValueOrDefault(o.OrderNumber);
            var yearFactor = outbound > 0m ? Math.Min(yearOutKg, outbound) / outbound : 0m;
            var yearShipMoney = shipMoney * yearFactor;

            var b = GetBucket(buckets, key);

            // 接单量（累计 + 本年）
            b.TotalCount++;
            b.TotalWeight += totalWeight;
            b.TotalAmount += totalAmount;
            if (isSignedThisYear)
            {
                b.YearCount++;
                b.YearWeight += totalWeight;
                b.YearAmount += totalAmount;
            }

            // 本年已发货（整单=主号完成 / 非整单；count=落入该桶的订单数）
            if (yearOutKg > 0m)
            {
                if (isCompleted) { b.ShippedDoneCount++; b.ShippedDoneWeight += yearOutKg; b.ShippedDoneAmount += yearShipMoney; }
                else { b.ShippedOtherCount++; b.ShippedOtherWeight += yearOutKg; b.ShippedOtherAmount += yearShipMoney; }
            }

            // 待发货（成品库存，整单/非整单）
            if (stock > 0m)
            {
                if (isCompleted) { b.StockDoneCount++; b.StockDoneWeight += stock; b.StockDoneAmount += stockMoney; }
                else { b.StockOtherCount++; b.StockOtherWeight += stock; b.StockOtherAmount += stockMoney; }
            }

            // 待在产（整单未入库 / 扣除部分入库）：仅主号未完成（阶段≠1）订单计入；
            // 已整单完成(stage1) 即使欠产/未入库也视为订单结束，不再计在产
            if (!isCompleted)
            {
                if (inbound <= 0m)
                {
                    b.WipNoneCount++;
                    b.WipNoneWeight += totalWeight;
                    b.WipNoneAmount += wipMoney;
                }
                else if (inbound < totalWeight)
                {
                    b.WipPartialCount++;
                    b.WipPartialWeight += totalWeight - inbound;
                    b.WipPartialAmount += wipMoney;
                }
            }
        }

        return buckets;
    }

    private static CustomerStatsBucket GetBucket(Dictionary<string, CustomerStatsBucket> buckets, string key)
    {
        if (!buckets.TryGetValue(key, out var b))
        {
            b = new CustomerStatsBucket();
            buckets[key] = b;
        }
        return b;
    }

    /// <summary>关联键：业务员 + 最终用户（文本快照匹配，忽略大小写；两端全空则不参与聚合）</summary>
    private static string MakeKey(string? salesman, string? endCustomer)
    {
        var s = (salesman ?? "").Trim();
        var e = (endCustomer ?? "").Trim();
        return string.IsNullOrEmpty(s) && string.IsNullOrEmpty(e) ? string.Empty : s + "\u001F" + e;
    }

    /// <summary>客户 8 列统计累加桶</summary>
    private sealed class CustomerStatsBucket
    {
        public int TotalCount;
        public decimal TotalWeight;
        public decimal TotalAmount;
        public int YearCount;
        public decimal YearWeight;
        public decimal YearAmount;
        public int ShippedDoneCount;
        public decimal ShippedDoneWeight;
        public decimal ShippedDoneAmount;
        public int ShippedOtherCount;
        public decimal ShippedOtherWeight;
        public decimal ShippedOtherAmount;
        public int StockDoneCount;
        public decimal StockDoneWeight;
        public decimal StockDoneAmount;
        public int StockOtherCount;
        public decimal StockOtherWeight;
        public decimal StockOtherAmount;
        public int WipNoneCount;
        public decimal WipNoneWeight;
        public decimal WipNoneAmount;
        public int WipPartialCount;
        public decimal WipPartialWeight;
        public decimal WipPartialAmount;
    }

    /// <summary>
    /// Get customer details by ID
    /// </summary>
    public async Task<CustomerProfileDto> GetByIdAsync(int id)
    {
        var entity = await _context.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
        {
            throw new BusinessException("客户不存在");
        }

        return ToDto(entity);
    }

    /// <summary>
    /// Create customer
    /// </summary>
    public async Task<CustomerProfileDto> CreateAsync(CreateCustomerRequest request)
    {
        // Check customer code uniqueness
        var exists = await _context.CustomerProfiles
            .AnyAsync(c => c.CustomerCode == request.CustomerCode);

        if (exists)
        {
            throw new BusinessException($"客户代码'{request.CustomerCode}'已存在");
        }

        var entity = new CustomerProfile
        {
            CustomerCode = request.CustomerCode,
            Salesman = request.Salesman,
            CustomerUnit = request.CustomerUnit,
            EndCustomer = string.IsNullOrEmpty(request.EndCustomer) ? request.CustomerUnit : request.EndCustomer,
            ContactPerson = request.ContactPerson,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            Status = request.Status,  // 直接使用枚举
            Remark = request.Remark
        };

        _context.CustomerProfiles.Add(entity);
        await _context.SaveChangesAsync();

        return ToDto(entity);
    }

    /// <summary>
    /// Update customer
    /// </summary>
    public async Task<CustomerProfileDto> UpdateAsync(int id, UpdateCustomerRequest request)
    {
        var entity = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
        {
            throw new BusinessException("客户不存在");
        }

        // Check customer code uniqueness (exclude self)
        if (!string.IsNullOrEmpty(request.CustomerCode) && request.CustomerCode != entity.CustomerCode)
        {
            var exists = await _context.CustomerProfiles
                .AnyAsync(c => c.CustomerCode == request.CustomerCode && c.Id != id);

            if (exists)
            {
                throw new BusinessException($"客户代码'{request.CustomerCode}'已存在");
            }
            entity.CustomerCode = request.CustomerCode;
        }

        if (!string.IsNullOrEmpty(request.Salesman))
        {
            entity.Salesman = request.Salesman;
        }

        if (!string.IsNullOrEmpty(request.CustomerUnit))
        {
            entity.CustomerUnit = request.CustomerUnit;
        }

        if (request.EndCustomer != null)
        {
            entity.EndCustomer = request.EndCustomer;
        }

        if (request.ContactPerson != null)
        {
            entity.ContactPerson = request.ContactPerson;
        }

        if (request.ContactPhone != null)
        {
            entity.ContactPhone = request.ContactPhone;
        }

        if (request.Address != null)
        {
            entity.Address = request.Address;
        }

        if (request.Status.HasValue)
        {
            entity.Status = request.Status.Value;
        }

        if (request.Remark != null)
        {
            entity.Remark = request.Remark;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("客户信息已被其他用户修改，请刷新后重试");
        }

        // 客户信息变更不再刷新订单读模型——订单快照字段独立维护
        return ToDto(entity);
    }

    /// <summary>
    /// Delete customer (物理删除)
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
        {
            throw new BusinessException("客户不存在");
        }

        // CustomerId FK 已移除，订单快照字段独立维护，可直接删除客户
        _context.CustomerProfiles.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<CustomerSelectDto>> GetSelectListAsync()
    {
        return await _context.CustomerProfiles
            .AsNoTracking()
            .OrderBy(c => c.CustomerUnit)
            .Select(c => new CustomerSelectDto
            {
                Id = c.Id,
                CustomerUnit = c.CustomerUnit,
                Salesman = c.Salesman ?? string.Empty,
                EndCustomer = c.EndCustomer
            })
            .ToListAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.CustomerFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            // 注意：枚举列（Status）不在此处返回，
            // 由前端 EnumOptions fallback 直接提供带中文 Display 的选项，避免映射丢失。
            var all = await _context.CustomerProfiles
                .AsNoTracking()
                .Select(c => new
                {
                    c.CustomerCode,
                    c.Salesman,
                    c.CustomerUnit,
                    c.EndCustomer,
                    c.ContactPerson,
                    c.ContactPhone,
                    c.Address,
                    c.Remark
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["CustomerCode"] = all.Select(x => x.CustomerCode).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
                ["Salesman"] = all.Select(x => x.Salesman).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
                ["CustomerUnit"] = all.Select(x => x.CustomerUnit).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
                ["EndCustomer"] = all.Select(x => x.EndCustomer ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["ContactPerson"] = all.Select(x => x.ContactPerson ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["ContactPhone"] = all.Select(x => x.ContactPhone ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Address"] = all.Select(x => x.Address ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Remark"] = all.Select(x => x.Remark ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList()
            };

        }) ?? new Dictionary<string, List<string>>();
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintCustomerBatchAsync(int[] ids, List<PrintColumnDef>? columns = null)
    {
        var result = new List<CustomerProfileDto>();
        foreach (var id in ids)
        {
            try
            {
                result.Add(await GetByIdAsync(id));
            }
            catch (BusinessException) { /* 跳过不存在的客户 */ }
        }
        return TablePrintHelper.GeneratePdf("客户档案列表", result, columns ?? []);
    }

    /// <summary>列表打印（Mode A）：前端已按可见列把当前页转成字典行，服务端仅做表格渲染</summary>
    public Task<byte[]> PrintCustomerListAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        // 与订单列表打印同款：自适应列宽 + 单元格居中 + 表头自动换行
        return Task.FromResult(TablePrintHelper.GeneratePdf(
            string.IsNullOrWhiteSpace(title) ? "客户列表" : title,
            items,
            columns ?? [],
            autoWidth: true,
            alignCenter: true,
            headerMaxLines: 0));
    }

    private static CustomerProfileDto ToDto(CustomerProfile entity) => new()
    {
        Id = entity.Id,
        CustomerCode = entity.CustomerCode,
        Salesman = entity.Salesman,
        CustomerUnit = entity.CustomerUnit,
        EndCustomer = entity.EndCustomer,
        ContactPerson = entity.ContactPerson,
        ContactPhone = entity.ContactPhone,
        Address = entity.Address,
        Status = entity.Status,
        Remark = entity.Remark
    };
}
