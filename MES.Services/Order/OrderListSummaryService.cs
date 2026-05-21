using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services.Order;

/// <summary>
/// 订单列表读模型刷新服务
/// </summary>
public class OrderListSummaryService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OrderListSummaryService> _logger;

    public OrderListSummaryService(AppDbContext context, ILogger<OrderListSummaryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>全量刷新所有订单汇总</summary>
    public async Task RefreshAllAsync()
    {
        _logger.LogInformation("开始全量刷新订单列表读模型");

        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .ToListAsync();

        if (salesOrders.Count == 0)
        {
            _logger.LogInformation("没有需要刷新的订单");
            return;
        }

        var orderIds = salesOrders.Select(so => so.Id).ToList();
        var customerIds = salesOrders.Select(so => so.CustomerId).Distinct().ToList();

        // 客户字典
        var customers = await _context.CustomerProfiles
            .AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        // 项次聚合
        var itemAggs = await GetOrderItemAggregationsAsync(orderIds);

        // 构建摘要
        var summaries = new List<OrderListSummary>();
        foreach (var so in salesOrders)
        {
            summaries.Add(BuildSummary(so, customers, itemAggs));
        }

        await UpsertSummariesAsync(summaries, orderIds);

        _logger.LogInformation("订单列表读模型刷新完成: 共{Count}条", summaries.Count);
    }

    /// <summary>刷新单个订单的汇总</summary>
    public async Task RefreshByOrderAsync(int orderId)
    {
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.Id == orderId);

        if (salesOrder == null)
        {
            // 如果订单不存在或是草稿，删除已有汇总
            var existing = await _context.Set<OrderListSummary>()
                .FirstOrDefaultAsync(s => s.OrderId == orderId);
            if (existing != null)
            {
                _context.Set<OrderListSummary>().Remove(existing);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // 记录已被其他请求删除，忽略
                }
            }
            return;
        }

        var customer = await _context.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == salesOrder.CustomerId);

        var itemAggs = await GetOrderItemAggregationsAsync(new List<int> { orderId });

        var summary = BuildSummary(salesOrder,
            customer != null
                ? new Dictionary<int, CustomerProfile> { { customer.Id, customer } }
                : new Dictionary<int, CustomerProfile>(),
            itemAggs);

        await UpsertSummariesAsync(new List<OrderListSummary> { summary }, new List<int> { orderId });
    }

    /// <summary>刷新某客户的所有订单汇总（客户名称/业务员变更时调用）</summary>
    public async Task RefreshByCustomerAsync(int customerId)
    {
        var orderIds = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => so.CustomerId == customerId)
            .Select(so => so.Id)
            .ToListAsync();

        if (orderIds.Count == 0) return;

        _logger.LogInformation("客户 {CustomerId} 变更，刷新 {Count} 个订单汇总", customerId, orderIds.Count);

        var customer = await _context.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId);

        var itemAggs = await GetOrderItemAggregationsAsync(orderIds);

        var customerDict = customer != null
            ? new Dictionary<int, CustomerProfile> { { customer.Id, customer } }
            : new Dictionary<int, CustomerProfile>();

        var orders = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => orderIds.Contains(so.Id))
            .ToListAsync();

        var summaries = orders.Select(so => BuildSummary(so, customerDict, itemAggs)).ToList();
        await UpsertSummariesAsync(summaries, orderIds);
    }

    // ========== Private ==========

    private async Task<Dictionary<int, OrderItemAggregation>> GetOrderItemAggregationsAsync(List<int> orderIds)
    {
        // 预加载项次 + 技术要求，在内存中分组（避免 EF Core GroupBy 内无法引用 _context）
        var orderItems = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => orderIds.Contains(oi.SalesOrderId))
            .ToListAsync();

        var reqOrderItemIds = (await _context.ProductRequirements
            .AsNoTracking()
            .Where(pr => orderItems.Select(oi => oi.Id).Contains(pr.OrderItemId))
            .Select(pr => pr.OrderItemId)
            .ToListAsync())
            .ToHashSet();

        var groups = orderItems
            .GroupBy(oi => oi.SalesOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return groups.ToDictionary(
            g => g.Key,
            g => new OrderItemAggregation
            {
                DeliveryStart = g.Value.Min(oi => (DateTime?)oi.DeliveryDate),
                DeliveryEnd = g.Value.Max(oi => (DateTime?)oi.DeliveryDate),
                HasDelayPenalty = g.Value.Any(oi => oi.DelayPenalty),
                TotalContractWeight = g.Value.Sum(oi => oi.ContractWeight),
                ItemCount = g.Value.Count,
                HasTechReqCount = g.Value.Count(oi => reqOrderItemIds.Contains(oi.Id)),
                FirstItemId = g.Value.OrderBy(oi => oi.Sequence).Select(oi => (int?)oi.Id).FirstOrDefault(),
                MaxUpdatedTime = g.Value.Max(oi => (DateTimeOffset?)oi.UpdatedTime)
            });
    }

    private static OrderListSummary BuildSummary(
        SalesOrder so,
        Dictionary<int, CustomerProfile> customers,
        Dictionary<int, OrderItemAggregation> itemAggs)
    {
        customers.TryGetValue(so.CustomerId, out var customer);
        itemAggs.TryGetValue(so.Id, out var agg);

        DateTime? lastChangeDate = null;
        if (agg?.MaxUpdatedTime.HasValue == true && agg.MaxUpdatedTime.Value.DateTime > so.CreatedTime)
        {
            lastChangeDate = agg.MaxUpdatedTime.Value.LocalDateTime;
        }

        return new OrderListSummary
        {
            OrderId = so.Id,
            OrderNumber = so.OrderNumber,
            SignDate = so.SignDate,
            CustomerName = customer?.CustomerUnit ?? string.Empty,
            Salesman = customer?.Salesman ?? string.Empty,
            EndCustomer = customer?.EndCustomer,
            DeliveryStart = agg?.DeliveryStart,
            DeliveryEnd = agg?.DeliveryEnd,
            HasDelayPenalty = agg?.HasDelayPenalty ?? false,
            TotalContractWeight = agg != null ? (int)Math.Round(agg.TotalContractWeight) : 0,
            ItemCount = agg?.ItemCount ?? 0,
            HasTechReqCount = agg?.HasTechReqCount ?? 0,
            Status = so.Status,
            RowVersion = null,
            LastChangeDate = lastChangeDate,
            FirstOrderItemId = agg?.FirstItemId
        };
    }

    private async Task UpsertSummariesAsync(List<OrderListSummary> summaries, List<int> orderIds)
    {
        var existingRecords = await _context.Set<OrderListSummary>()
            .Where(s => orderIds.Contains(s.OrderId))
            .ToListAsync();

        var existingByOrderId = existingRecords.ToDictionary(e => e.OrderId);

        foreach (var summary in summaries)
        {
            if (existingByOrderId.TryGetValue(summary.OrderId, out var existing))
            {
                // 保留基础字段 + 手动复制所有业务字段（排除 RowVersion，由 SQL Server 自动管理）
                existing.OrderId = summary.OrderId;
                existing.OrderNumber = summary.OrderNumber;
                existing.SignDate = summary.SignDate;
                existing.CustomerName = summary.CustomerName;
                existing.Salesman = summary.Salesman;
                existing.EndCustomer = summary.EndCustomer;
                existing.DeliveryStart = summary.DeliveryStart;
                existing.DeliveryEnd = summary.DeliveryEnd;
                existing.HasDelayPenalty = summary.HasDelayPenalty;
                existing.TotalContractWeight = summary.TotalContractWeight;
                existing.ItemCount = summary.ItemCount;
                existing.HasTechReqCount = summary.HasTechReqCount;
                existing.Status = summary.Status;
                existing.LastChangeDate = summary.LastChangeDate;
                existing.FirstOrderItemId = summary.FirstOrderItemId;
                // 注意：CreatedTime/CreatedBy 保持不变（新实体不覆盖旧实体的审计字段）
            }
            else
            {
                _context.Set<OrderListSummary>().Add(summary);
            }
        }

        // 删除不再需要的记录（已被删除或变为草稿的订单）
        var validOrderIds = summaries.Select(s => s.OrderId).ToHashSet();
        var toDelete = existingRecords.Where(e => !validOrderIds.Contains(e.OrderId)).ToList();
        if (toDelete.Count > 0)
        {
            _context.Set<OrderListSummary>().RemoveRange(toDelete);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // 刷新读模型时如果发生并发冲突（其他请求同时刷新），忽略并记录日志
            // 下次刷新会自动覆盖
            _logger.LogWarning(ex, "刷新 OrderListSummary 时发生并发冲突，已忽略");
        }
    }

    private class OrderItemAggregation
    {
        public DateTime? DeliveryStart { get; set; }
        public DateTime? DeliveryEnd { get; set; }
        public bool HasDelayPenalty { get; set; }
        public decimal TotalContractWeight { get; set; }
        public int ItemCount { get; set; }
        public int HasTechReqCount { get; set; }
        public int? FirstItemId { get; set; }
        public DateTimeOffset? MaxUpdatedTime { get; set; }
    }
}
