using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// 工单首页读模型刷新服务
/// </summary>
public class WorkOrderStatusSummaryService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkOrderStatusSummaryService> _logger;

    public WorkOrderStatusSummaryService(AppDbContext context, ILogger<WorkOrderStatusSummaryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>全量刷新所有工单首页读模型</summary>
    public async Task RefreshAllAsync()
    {
        _logger.LogInformation("开始全量刷新工单首页读模型");

        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => so.Status == SalesOrderStatus.Confirmed)
            .ToListAsync();

        if (salesOrders.Count == 0)
        {
            _logger.LogInformation("没有需要刷新的已确认订单");
            return;
        }

        var orderIds = salesOrders.Select(so => so.Id).ToList();
        var customerIds = salesOrders.Select(so => so.CustomerId).Distinct().ToList();
        var orderNumbers = salesOrders.Select(so => so.OrderNumber).ToList();

        // 客户字典
        var customers = await _context.CustomerProfiles
            .AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        // 项次聚合
        var itemAggs = await GetOrderItemAggregationsAsync(orderIds);

        // 工单聚合
        var woAggs = await GetWorkOrderAggregationsAsync(orderNumbers);

        // 构建摘要
        var summaries = salesOrders.Select(so =>
            BuildSummary(so, customers, itemAggs, woAggs)).ToList();

        await UpsertSummariesAsync(summaries, orderIds);

        _logger.LogInformation("工单首页读模型刷新完成: 共{Count}条", summaries.Count);
    }

    /// <summary>刷新单个订单的工单首页读模型</summary>
    public async Task RefreshByOrderAsync(int orderId)
    {
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.Id == orderId);

        if (salesOrder == null || salesOrder.Status != SalesOrderStatus.Confirmed)
        {
            var existing = await _context.Set<WorkOrderStatusSummary>()
                .FirstOrDefaultAsync(s => s.SalesOrderId == orderId);
            if (existing != null)
            {
                _context.Set<WorkOrderStatusSummary>().Remove(existing);
                try { await _context.SaveChangesAsync(); }
                catch (DbUpdateConcurrencyException) { }
            }
            return;
        }

        var customer = await _context.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == salesOrder.CustomerId);

        var itemAggs = await GetOrderItemAggregationsAsync(new List<int> { orderId });
        var woAggs = await GetWorkOrderAggregationsAsync(new List<string> { salesOrder.OrderNumber });

        var summary = BuildSummary(salesOrder,
            customer != null
                ? new Dictionary<int, CustomerProfile> { { customer.Id, customer } }
                : new Dictionary<int, CustomerProfile>(),
            itemAggs, woAggs);

        await UpsertSummariesAsync(new List<WorkOrderStatusSummary> { summary }, new List<int> { orderId });
    }

    /// <summary>刷新某客户的所有工单首页读模型</summary>
    public async Task RefreshByCustomerAsync(int customerId)
    {
        var orderIds = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => so.CustomerId == customerId && so.Status == SalesOrderStatus.Confirmed)
            .Select(so => so.Id)
            .ToListAsync();

        if (orderIds.Count == 0) return;

        _logger.LogInformation("客户 {CustomerId} 变更，刷新 {Count} 个工单首页汇总", customerId, orderIds.Count);

        var customer = await _context.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId);

        var orders = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => orderIds.Contains(so.Id))
            .ToListAsync();

        var orderNumbers = orders.Select(so => so.OrderNumber).ToList();

        var itemAggs = await GetOrderItemAggregationsAsync(orderIds);
        var woAggs = await GetWorkOrderAggregationsAsync(orderNumbers);

        var customerDict = customer != null
            ? new Dictionary<int, CustomerProfile> { { customer.Id, customer } }
            : new Dictionary<int, CustomerProfile>();

        var summaries = orders.Select(so => BuildSummary(so, customerDict, itemAggs, woAggs)).ToList();
        await UpsertSummariesAsync(summaries, orderIds);
    }

    /// <summary>按工单号刷新关联的工单首页读模型</summary>
    public async Task RefreshByWorkOrderAsync(string salesOrderNo)
    {
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.OrderNumber == salesOrderNo);

        if (salesOrder == null) return;

        await RefreshByOrderAsync(salesOrder.Id);
    }

    // ========== Private ==========

    private async Task<Dictionary<int, OrderItemAggregation>> GetOrderItemAggregationsAsync(List<int> orderIds)
    {
        var orderItems = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => orderIds.Contains(oi.SalesOrderId))
            .ToListAsync();

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
                ItemCount = g.Value.Count
            });
    }

    private async Task<Dictionary<string, WorkOrderAggregation>> GetWorkOrderAggregationsAsync(List<string> orderNumbers)
    {
        if (orderNumbers.Count == 0) return new Dictionary<string, WorkOrderAggregation>();

        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => orderNumbers.Contains(wo.SalesOrderNo))
            .ToListAsync();

        var groups = workOrders
            .GroupBy(wo => wo.SalesOrderNo)
            .ToDictionary(g => g.Key, g => g.ToList());

        return groups.ToDictionary(
            g => g.Key,
            g =>
            {
                var nonCancelled = g.Value.Where(wo => wo.Status != WorkOrderStatus.Cancelled).ToList();
                var count = nonCancelled.Count;
                var hasPending = nonCancelled.Any(wo => wo.Status == WorkOrderStatus.Pending);
                var firstId = nonCancelled.OrderBy(wo => wo.Id).Select(wo => (int?)wo.Id).FirstOrDefault();
                var maxUpdated = nonCancelled.Any()
                    ? nonCancelled.Max(wo => (DateTimeOffset?)wo.UpdatedTime)
                    : null;

                WorkOrderStatus status;
                if (count == 0) status = WorkOrderStatus.NotGenerated;
                else if (hasPending) status = WorkOrderStatus.Pending;
                else status = WorkOrderStatus.Confirmed;

                return new WorkOrderAggregation
                {
                    WorkOrderCount = count,
                    WorkOrderStatus = status,
                    HasWorkOrder = count > 0,
                    FirstWorkOrderId = firstId,
                    MaxUpdatedTime = maxUpdated
                };
            });
    }

    private static WorkOrderStatusSummary BuildSummary(
        SalesOrder so,
        Dictionary<int, CustomerProfile> customers,
        Dictionary<int, OrderItemAggregation> itemAggs,
        Dictionary<string, WorkOrderAggregation> woAggs)
    {
        customers.TryGetValue(so.CustomerId, out var customer);
        itemAggs.TryGetValue(so.Id, out var itemAgg);
        woAggs.TryGetValue(so.OrderNumber, out var woAgg);

        DateTime? lastChangeDate = null;
        if (woAgg?.MaxUpdatedTime.HasValue == true && woAgg.MaxUpdatedTime.Value.DateTime > so.CreatedTime)
        {
            lastChangeDate = woAgg.MaxUpdatedTime.Value.LocalDateTime;
        }

        return new WorkOrderStatusSummary
        {
            SalesOrderId = so.Id,
            OrderNumber = so.OrderNumber,
            SignDate = so.SignDate,
            CustomerName = customer?.CustomerUnit ?? string.Empty,
            Salesman = customer?.Salesman ?? string.Empty,
            EndCustomer = customer?.EndCustomer,
            DeliveryStart = itemAgg?.DeliveryStart,
            DeliveryEnd = itemAgg?.DeliveryEnd,
            HasDelayPenalty = itemAgg?.HasDelayPenalty ?? false,
            TotalContractWeight = itemAgg != null ? (int)Math.Round(itemAgg.TotalContractWeight) : 0,
            ItemCount = itemAgg?.ItemCount ?? 0,
            WorkOrderCount = woAgg?.WorkOrderCount ?? 0,
            WorkOrderStatus = woAgg?.WorkOrderStatus ?? WorkOrderStatus.NotGenerated,
            HasWorkOrder = woAgg?.HasWorkOrder ?? false,
            WorkOrderId = woAgg?.FirstWorkOrderId,
            RowVersion = null,
            LastChangeDate = lastChangeDate
        };
    }

    private async Task UpsertSummariesAsync(List<WorkOrderStatusSummary> summaries, List<int> orderIds)
    {
        var existingRecords = await _context.Set<WorkOrderStatusSummary>()
            .Where(s => orderIds.Contains(s.SalesOrderId))
            .ToListAsync();

        var existingByOrderId = existingRecords.ToDictionary(e => e.SalesOrderId);

        foreach (var summary in summaries)
        {
            if (existingByOrderId.TryGetValue(summary.SalesOrderId, out var existing))
            {
                existing.SalesOrderId = summary.SalesOrderId;
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
                existing.WorkOrderCount = summary.WorkOrderCount;
                existing.WorkOrderStatus = summary.WorkOrderStatus;
                existing.HasWorkOrder = summary.HasWorkOrder;
                existing.WorkOrderId = summary.WorkOrderId;
                existing.LastChangeDate = summary.LastChangeDate;
            }
            else
            {
                _context.Set<WorkOrderStatusSummary>().Add(summary);
            }
        }

        var validOrderIds = summaries.Select(s => s.SalesOrderId).ToHashSet();
        var toDelete = existingRecords.Where(e => !validOrderIds.Contains(e.SalesOrderId)).ToList();
        if (toDelete.Count > 0)
        {
            _context.Set<WorkOrderStatusSummary>().RemoveRange(toDelete);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "刷新 WorkOrderStatusSummary 时发生并发冲突，已忽略");
        }
    }

    private class OrderItemAggregation
    {
        public DateTime? DeliveryStart { get; set; }
        public DateTime? DeliveryEnd { get; set; }
        public bool HasDelayPenalty { get; set; }
        public decimal TotalContractWeight { get; set; }
        public int ItemCount { get; set; }
    }

    private class WorkOrderAggregation
    {
        public int WorkOrderCount { get; set; }
        public WorkOrderStatus WorkOrderStatus { get; set; }
        public bool HasWorkOrder { get; set; }
        public int? FirstWorkOrderId { get; set; }
        public DateTimeOffset? MaxUpdatedTime { get; set; }
    }
}
