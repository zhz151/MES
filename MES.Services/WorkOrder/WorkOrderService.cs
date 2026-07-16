using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Auth;
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
using MES.Core.Enums;
using MES.Core.Exceptions;
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
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.WorkOrder;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.WorkOrder;

using WoEntity = MES.Data.Entities.WorkOrder.WorkOrder;

/// <summary>
/// 工单服务实现
/// </summary>
public class WorkOrderService : IWorkOrderService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkOrderService> _logger;
    private readonly IConfigParameterService _configService;
    private readonly IWorkOrderListSummaryRefreshService? _listSummaryService;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly IMemoryCache _cache;
    private static readonly SemaphoreSlim _workOrderNoSemaphore = new SemaphoreSlim(1, 1);

    public WorkOrderService(AppDbContext context, ILogger<WorkOrderService> logger,
        IConfigParameterService configService,
        IMemoryCache cache,
        IWorkOrderListSummaryRefreshService? listSummaryService = null,
        IWorkOrderExecutionService? workOrderExecutionService = null)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
        _listSummaryService = listSummaryService;
        _workOrderExecutionService = workOrderExecutionService!;
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

    private async Task TryRefreshExecutionSummariesAsync(List<string> workOrderNos)
    {
        var validNos = workOrderNos.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (validNos.Count == 0) return;
        try
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(validNos);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "批量刷新工单执行状况失败（不影响主流程）: Count={Count}", validNos.Count);
        }
    }

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        var cacheKey = $"WorkOrderService:ConfigMap:{category}";
        var map = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _configService.GetConfigMapAsync(category);
        });
        return map?.GetValueOrDefault(key, defaultValue) ?? defaultValue;
    }

    #region 工单首页（订单状态监控）

    public async Task<PagedResult<OrderWorkOrderStatusDto>> GetOrderWorkOrderStatusPageAsync(WorkOrderQueryParams query)
    {
        // 实时查询
        return await GetOrderWorkOrderStatusPageLegacyAsync(query);
    }

    /// <summary>
    /// 获取所有工单首页订单状态数据（无分页，供客户端筛选排序）
    /// </summary>
    public async Task<List<OrderWorkOrderStatusDto>> GetAllOrderStatusListAsync()
    {
        // ===== 1. 基础查询：已确认订单 =====
        var orders = await _context.SalesOrders
            .Where(so => so.Status == SalesOrderStatus.Confirmed)
            .ToListAsync();

        if (!orders.Any()) return new List<OrderWorkOrderStatusDto>();

        var orderIds = orders.Select(x => x.Id).ToList();
        var orderNumbers = orders.Select(x => x.OrderNumber).ToList();

        var orderItemAggs = await _context.OrderItems
            .Where(oi => orderIds.Contains(oi.SalesOrderId))
            .GroupBy(oi => oi.SalesOrderId)
            .Select(g => new
            {
                OrderId = g.Key,
                DeliveryStart = g.Min(oi => (DateTime?)oi.DeliveryDate),
                DeliveryEnd = g.Max(oi => (DateTime?)oi.DeliveryDate),
                HasDelayPenalty = g.Any(oi => oi.DelayPenalty),
                TotalContractWeight = g.Sum(oi => oi.ContractWeight),
                ItemCount = g.Count()
            })
            .ToListAsync();
        var itemAggDict = orderItemAggs.ToDictionary(x => x.OrderId);

        var workOrderGroups = await _context.WorkOrders
            .Where(wo => orderNumbers.Contains(wo.SalesOrderNo))
            .GroupBy(wo => wo.SalesOrderNo)
            .Select(g => new
            {
                SalesOrderNo = g.Key,
                WorkOrderCount = g.Count(),
                HasPending = g.Any(wo => wo.Status == WorkOrderStatus.Pending),
                FirstWorkOrderId = g.OrderBy(wo => wo.Status == WorkOrderStatus.Pending ? 0 : 1).Select(wo => (int?)wo.Id).FirstOrDefault()
            })
            .ToListAsync();
        var workOrderDict = workOrderGroups.ToDictionary(x => x.SalesOrderNo);

        return orders.Select(order =>
        {
            var woInfo = workOrderDict.GetValueOrDefault(order.OrderNumber);
            var agg = itemAggDict.GetValueOrDefault(order.Id);

            var hasWorkOrder = woInfo != null && woInfo.WorkOrderCount > 0;
            WorkOrderStatus workOrderStatus;
            int? workOrderId = null;

            if (woInfo == null || woInfo.WorkOrderCount == 0)
            {
                workOrderStatus = WorkOrderStatus.NotGenerated;
            }
            else if (woInfo.HasPending)
            {
                workOrderStatus = WorkOrderStatus.Pending;
                workOrderId = woInfo.FirstWorkOrderId;
            }
            else
            {
                workOrderStatus = WorkOrderStatus.Confirmed;
                workOrderId = woInfo.FirstWorkOrderId;
            }

            return new OrderWorkOrderStatusDto
            {
                SalesOrderId = order.Id,
                OrderNumber = order.OrderNumber,
                SignDate = order.SignDate,
                Salesman = order.Salesman,
                CustomerName = order.CustomerName,
                EndCustomer = order.EndCustomer,
                DeliveryStart = agg?.DeliveryStart,
                DeliveryEnd = agg?.DeliveryEnd,
                HasDelayPenalty = agg?.HasDelayPenalty ?? false,
                TotalContractWeight = agg != null ? (int)Math.Round(agg.TotalContractWeight) : 0,
                ItemCount = agg?.ItemCount ?? 0,
                WorkOrderCount = woInfo?.WorkOrderCount ?? 0,
                WorkOrderStatus = workOrderStatus,
                HasWorkOrder = hasWorkOrder,
                WorkOrderId = workOrderId
            };
        }).ToList();
    }

    /// <summary>
    /// 降级回退：读模型未就绪时使用原始的实时查询
    /// </summary>
    private async Task<PagedResult<OrderWorkOrderStatusDto>> GetOrderWorkOrderStatusPageLegacyAsync(WorkOrderQueryParams query)
    {
        // ===== 1. 基础查询：已确认订单 =====
        var orderQuery = _context.SalesOrders
            .Where(so => so.Status == SalesOrderStatus.Confirmed);

        // ===== 2. 应用DB级文本筛选 =====
        if (!string.IsNullOrEmpty(query.Salesman))
            orderQuery = orderQuery.Where(x => x.Salesman.Contains(query.Salesman));

        if (!string.IsNullOrEmpty(query.EndCustomer))
            orderQuery = orderQuery.Where(x => x.EndCustomer != null && x.EndCustomer.Contains(query.EndCustomer));

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            orderQuery = orderQuery.Where(x =>
                x.OrderNumber.Contains(keyword) ||
                x.CustomerName.Contains(keyword) ||
                x.Salesman.Contains(keyword) ||
                (x.EndCustomer != null && x.EndCustomer.Contains(keyword)) ||
                (keyword == "是" && _context.OrderItems.Any(oi => oi.SalesOrderId == x.Id && oi.DelayPenalty)) ||
                (keyword == "否" && _context.OrderItems.Any(oi => oi.SalesOrderId == x.Id && !oi.DelayPenalty))
            );
        }

        // ===== 3. 工单状态筛选（子查询 → SQL EXISTS，避免全表内存加载） =====
        if (!string.IsNullOrEmpty(query.WorkOrderStatus) && Enum.TryParse<WorkOrderStatus>(query.WorkOrderStatus, out var filterStatus))
        {
            switch (filterStatus)
            {
                case WorkOrderStatus.NotGenerated:
                    orderQuery = orderQuery.Where(x =>
                        !_context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber));
                    break;
                case WorkOrderStatus.Pending:
                    orderQuery = orderQuery.Where(x =>
                        _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber && wo.Status == WorkOrderStatus.Pending));
                    break;
                case WorkOrderStatus.Confirmed:
                    orderQuery = orderQuery.Where(x =>
                        _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber) &&
                        !_context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber && wo.Status == WorkOrderStatus.Pending));
                    break;
            }
        }

        orderQuery = orderQuery.ApplyFilters(query.Filters);
        // 计算字段筛选（匿名类型非直接属性）：交期范围、延期罚款、重量、项次、工单数
        if (query.Filters is { Count: > 0 })
        {
            foreach (var filter in query.Filters)
            {
                if (string.IsNullOrWhiteSpace(filter.Field)) continue;
                switch (filter.Field.ToLower())
                {
                    case "deliverystart":
                        if (DateTime.TryParse(filter.From?.ToString(), out var dsFrom))
                            orderQuery = orderQuery.Where(x => x.OrderItems.Min(oi => (DateTime?)oi.DeliveryDate) >= dsFrom);
                        if (DateTime.TryParse(filter.To?.ToString(), out var dsTo))
                            orderQuery = orderQuery.Where(x => x.OrderItems.Min(oi => (DateTime?)oi.DeliveryDate) <= dsTo);
                        break;
                    case "deliveryend":
                        if (DateTime.TryParse(filter.From?.ToString(), out var deFrom))
                            orderQuery = orderQuery.Where(x => x.OrderItems.Max(oi => (DateTime?)oi.DeliveryDate) >= deFrom);
                        if (DateTime.TryParse(filter.To?.ToString(), out var deTo))
                            orderQuery = orderQuery.Where(x => x.OrderItems.Max(oi => (DateTime?)oi.DeliveryDate) <= deTo);
                        break;
                    case "hasdelaypenalty":
                        if (filter.Value == "是")
                            orderQuery = orderQuery.Where(x => x.OrderItems.Any(oi => oi.DelayPenalty));
                        else if (filter.Value == "否")
                            orderQuery = orderQuery.Where(x => !x.OrderItems.Any(oi => oi.DelayPenalty));
                        break;
                    case "totalcontractweight":
                        if (int.TryParse(filter.From?.ToString(), out var tcwMin))
                            orderQuery = orderQuery.Where(x => x.OrderItems.Sum(oi => oi.ContractWeight) >= tcwMin);
                        if (int.TryParse(filter.To?.ToString(), out var tcwMax))
                            orderQuery = orderQuery.Where(x => x.OrderItems.Sum(oi => oi.ContractWeight) <= tcwMax);
                        break;
                    case "itemcount":
                        if (int.TryParse(filter.From?.ToString(), out var icMin))
                            orderQuery = orderQuery.Where(x => x.OrderItems.Count >= icMin);
                        if (int.TryParse(filter.To?.ToString(), out var icMax))
                            orderQuery = orderQuery.Where(x => x.OrderItems.Count <= icMax);
                        break;
                    case "workordercount":
                        if (int.TryParse(filter.From?.ToString(), out var wcMin))
                            orderQuery = orderQuery.Where(x => _context.WorkOrders.Count(wo => wo.SalesOrderNo == x.OrderNumber) >= wcMin);
                        if (int.TryParse(filter.To?.ToString(), out var wcMax))
                            orderQuery = orderQuery.Where(x => _context.WorkOrders.Count(wo => wo.SalesOrderNo == x.OrderNumber) <= wcMax);
                        break;
                }
            }
        }

        // ===== 4. 总记录数（DB级 COUNT，高效） =====
        var totalCount = await orderQuery.CountAsync();

        // ===== 5. 排序（推送到DB级，含子查询排序） =====
        if (!string.IsNullOrEmpty(query.SortBy))
        {
            var sortDesc = query.IsDescending;
            switch (query.SortBy.ToLower())
            {
                case "ordernumber":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => x.OrderNumber)
                        : orderQuery.OrderBy(x => x.OrderNumber);
                    break;
                case "signdate":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => x.SignDate)
                        : orderQuery.OrderBy(x => x.SignDate);
                    break;
                case "salesman":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => x.Salesman)
                        : orderQuery.OrderBy(x => x.Salesman);
                    break;
                case "customername":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => x.CustomerName)
                        : orderQuery.OrderBy(x => x.CustomerName);
                    break;
                case "endcustomer":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => x.EndCustomer ?? "")
                        : orderQuery.OrderBy(x => x.EndCustomer ?? "");
                    break;
                case "deliverystart":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.Id).Min(oi => (DateTime?)oi.DeliveryDate))
                        : orderQuery.OrderBy(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.Id).Min(oi => (DateTime?)oi.DeliveryDate));
                    break;
                case "deliveryend":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.Id).Max(oi => (DateTime?)oi.DeliveryDate))
                        : orderQuery.OrderBy(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.Id).Max(oi => (DateTime?)oi.DeliveryDate));
                    break;
                case "hasdelaypenalty":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.OrderItems.Any(oi => oi.SalesOrderId == x.Id && oi.DelayPenalty))
                        : orderQuery.OrderBy(x => _context.OrderItems.Any(oi => oi.SalesOrderId == x.Id && oi.DelayPenalty));
                    break;
                case "totalcontractweight":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.Id).Sum(oi => oi.ContractWeight))
                        : orderQuery.OrderBy(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.Id).Sum(oi => oi.ContractWeight));
                    break;
                case "itemcount":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.OrderItems.Count(oi => oi.SalesOrderId == x.Id))
                        : orderQuery.OrderBy(x => _context.OrderItems.Count(oi => oi.SalesOrderId == x.Id));
                    break;
                case "workordercount":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.WorkOrders.Count(wo => wo.SalesOrderNo == x.OrderNumber))
                        : orderQuery.OrderBy(x => _context.WorkOrders.Count(wo => wo.SalesOrderNo == x.OrderNumber));
                    break;
                case "workorderstatus":
                    // 子查询计算状态排序优先级：Pending=1, NotGenerated=2, Confirmed=3
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x =>
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber && wo.Status == WorkOrderStatus.Pending) ? 1 :
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber) ? 3 : 2)
                        : orderQuery.OrderBy(x =>
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber && wo.Status == WorkOrderStatus.Pending) ? 1 :
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber) ? 3 : 2);
                    break;
                default:
                    orderQuery = orderQuery
                        .OrderBy(x =>
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber && wo.Status == WorkOrderStatus.Pending) ? 1 :
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber) ? 3 : 2)
                        .ThenByDescending(x => x.SignDate);
                    break;
            }
        }
        else
        {
            // 默认排序：Pending→NotGenerated→Confirmed → 签订日期降序
            orderQuery = orderQuery
                .OrderBy(x =>
                    _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber && wo.Status == WorkOrderStatus.Pending) ? 1 :
                    _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.OrderNumber) ? 3 : 2)
                .ThenByDescending(x => x.SignDate);
        }

        // ===== 6. 分页（DB级 Skip/Take，只加载当前页数据到内存） =====
        var pagedOrders = await orderQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        if (!pagedOrders.Any())
        {
            return new PagedResult<OrderWorkOrderStatusDto>
            {
                Items = new List<OrderWorkOrderStatusDto>(),
                TotalCount = totalCount,
                PageIndex = query.PageIndex,
                PageSize = query.PageSize
            };
        }

        // ===== 7. 仅查询当前页订单的聚合数据 =====
        var pagedOrderIds = pagedOrders.Select(x => x.Id).ToList();
        var pagedOrderNumbers = pagedOrders.Select(x => x.OrderNumber).ToList();

        var workOrderGroups = await _context.WorkOrders
            .Where(wo => pagedOrderNumbers.Contains(wo.SalesOrderNo))
            .GroupBy(wo => wo.SalesOrderNo)
            .Select(g => new
            {
                SalesOrderNo = g.Key,
                WorkOrderCount = g.Count(),
                HasPending = g.Any(wo => wo.Status == WorkOrderStatus.Pending),
                FirstWorkOrderId = g.OrderBy(wo => wo.Status == WorkOrderStatus.Pending ? 0 : 1).Select(wo => (int?)wo.Id).FirstOrDefault()
            })
            .ToListAsync();
        var workOrderDict = workOrderGroups.ToDictionary(x => x.SalesOrderNo);

        var orderItemAggs = await _context.OrderItems
            .Where(oi => pagedOrderIds.Contains(oi.SalesOrderId))
            .GroupBy(oi => oi.SalesOrderId)
            .Select(g => new
            {
                OrderId = g.Key,
                DeliveryStart = g.Min(oi => (DateTime?)oi.DeliveryDate),
                DeliveryEnd = g.Max(oi => (DateTime?)oi.DeliveryDate),
                HasDelayPenalty = g.Any(oi => oi.DelayPenalty),
                TotalContractWeight = g.Sum(oi => oi.ContractWeight),
                ItemCount = g.Count()
            })
            .ToListAsync();
        var itemAggDict = orderItemAggs.ToDictionary(x => x.OrderId);

        // ===== 8. 组装 DTO =====
        var items = pagedOrders.Select(order =>
        {
            var woInfo = workOrderDict.GetValueOrDefault(order.OrderNumber);
            var agg = itemAggDict.GetValueOrDefault(order.Id);

            var hasWorkOrder = woInfo != null && woInfo.WorkOrderCount > 0;
            WorkOrderStatus workOrderStatus;
            int? workOrderId = null;

            if (woInfo == null || woInfo.WorkOrderCount == 0)
            {
                workOrderStatus = WorkOrderStatus.NotGenerated;
            }
            else if (woInfo.HasPending)
            {
                workOrderStatus = WorkOrderStatus.Pending;
                workOrderId = woInfo.FirstWorkOrderId;
            }
            else
            {
                workOrderStatus = WorkOrderStatus.Confirmed;
                workOrderId = woInfo.FirstWorkOrderId;
            }

            return new OrderWorkOrderStatusDto
            {
                SalesOrderId = order.Id,
                OrderNumber = order.OrderNumber,
                SignDate = order.SignDate,
                Salesman = order.Salesman,
                CustomerName = order.CustomerName,
                EndCustomer = order.EndCustomer,
                DeliveryStart = agg?.DeliveryStart,
                DeliveryEnd = agg?.DeliveryEnd,
                HasDelayPenalty = agg?.HasDelayPenalty ?? false,
                TotalContractWeight = agg != null ? (int)Math.Round(agg.TotalContractWeight) : 0,
                ItemCount = agg?.ItemCount ?? 0,
                WorkOrderCount = woInfo?.WorkOrderCount ?? 0,
                WorkOrderStatus = workOrderStatus,
                HasWorkOrder = hasWorkOrder,
                WorkOrderId = workOrderId
            };
        }).ToList();

        return new PagedResult<OrderWorkOrderStatusDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    /// <summary>
    /// 获取已确认但无工单的订单列表（待生成工单）
    /// </summary>
    public async Task<List<WorkOrderListItemDto>> GetPendingOrdersAsync()
    {
        var rawData = await _context.SalesOrders
            .Where(so => so.Status == SalesOrderStatus.Confirmed
                && !_context.WorkOrders.Any(wo => wo.SalesOrderNo == so.OrderNumber))
            .OrderByDescending(so => so.SignDate)
            .Select(so => new
            {
                so.Id,
                so.OrderNumber,
                so.SignDate,
                so.CreatedTime,
                so.Salesman,
                so.EndCustomer
            })
            .ToListAsync();

        return rawData.Select(r => new WorkOrderListItemDto
        {
            Id = 0,
            WorkOrderNo = "",
            SalesOrderNo = r.OrderNumber,
            ProductionMainNo = "",
            ProductionSubNo = null,
            SignDate = r.SignDate,
            Salesman = r.Salesman,
            EndCustomer = r.EndCustomer,
            DeliveryDate = default,
            DelayPenalty = false,
            SettlementMethod = default,
            PlantGrade = "",
            PipeManufacturingType = default,
            Specification = "",
            LengthStatus = default,
            MinLength = null,
            MaxLength = null,
            TotalQuantity = 0,
            TotalWeight = 0,
            DeliveryState = default,
            TotalItemCount = 0,
            Status = WorkOrderStatus.NotGenerated,
            CreatedTime = r.CreatedTime
        }).ToList();
    }

    #endregion

    #region 工单生成

    public async Task<List<OrderItemForWorkOrderDto>> GetOrderItemsForWorkOrderAsync(string salesOrderNo)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.OrderNumber == salesOrderNo);
        if (salesOrder == null)
            throw new BusinessException($"订单 {salesOrderNo} 不存在");
        if (salesOrder.Status != SalesOrderStatus.Confirmed)
            throw new BusinessException($"订单 {salesOrderNo} 状态不是已确认，无法生成工单");

        // 获取该订单下所有状态不为已取消的工单（用于提取原主号/次号）
        var existingWorkOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == salesOrderNo)
            .ToListAsync();

        // 构建 项次ID -> (原主号, 原次号) 映射，同时建立工单ID查询
        var itemToOriginalNo = new Dictionary<int, (string MainNo, string? SubNo)>();
        var itemToWorkOrder = new Dictionary<int, WoEntity>(); // 用于后续校验合并字段是否一致
        foreach (var wo in existingWorkOrders)
        {
            var itemIds = wo.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(id => int.TryParse(id, out var parsed) ? parsed : -1)
                             .Where(id => id > 0);
            foreach (var itemId in itemIds)
            {
                if (!itemToOriginalNo.ContainsKey(itemId))
                {
                    itemToOriginalNo[itemId] = (wo.ProductionMainNo, wo.ProductionSubNo);
                    itemToWorkOrder[itemId] = wo;
                }
            }
        }

        var orderItems = await _context.OrderItems
            .AsNoTracking()
            .Include(oi => oi.ProductRequirement)
            .Where(oi => oi.SalesOrderId == salesOrder.Id)
            .OrderBy(oi => oi.Sequence)
            .ToListAsync();

        if (!orderItems.Any())
            throw new BusinessException($"订单 {salesOrderNo} 没有有效的项次");

        // 单独加载 StandardRegister
        var standardNos = orderItems
            .Where(oi => !string.IsNullOrEmpty(oi.StandardNo))
            .Select(oi => oi.StandardNo)
            .Distinct()
            .ToList();
        var srDict = standardNos.Any()
            ? await _context.StandardRegisters
                .Where(sr => standardNos.Contains(sr.StandardNo))
                .ToDictionaryAsync(sr => sr.StandardNo, sr => sr, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, MES.Data.Entities.StandardRegister.StandardRegister>(StringComparer.OrdinalIgnoreCase);

        // 加载牌号映射（从 StandardGradeMapping 取最新 PlantGrade/Density）
        var gradeNames = orderItems.Select(oi => oi.StandardGrade).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var gradeDict = gradeNames.Any()
            ? (await _context.StandardGradeMappings
                .Where(sgm => gradeNames.Contains(sgm.StandardGrade))
                .ToListAsync())
                .GroupBy(sgm => sgm.StandardGrade, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, StandardGradeMapping>();

        var groups = GroupOrderItemsByMergeFields(orderItems);
        var result = new List<OrderItemForWorkOrderDto>();
        var mainNoCounter = 1;

        foreach (var group in groups)
        {
            var firstItem = group.First();
            var prefix = GetMainNoPrefix(firstItem.PipeManufacturingType, firstItem.LengthStatus);
            var suggestedMainNo = $"{prefix}{mainNoCounter++:D2}";

            foreach (var item in group)
            {
                var standardNo = item.StandardNo ?? string.Empty;
                srDict.TryGetValue(standardNo, out var sr);
                var dto = new OrderItemForWorkOrderDto
                {
                    Id = item.Id,
                    OrderNumber = salesOrder.OrderNumber,
                    Sequence = item.Sequence,
                    PipeManufacturingType = item.PipeManufacturingType,
                    DeliveryDate = item.DeliveryDate,
                    DelayPenalty = item.DelayPenalty,
                    SettlementMethod = item.SettlementMethod,
                    StandardNo = sr?.StandardNo ?? standardNo,
                    DeliveryState = item.DeliveryState,
                    PlantGrade = gradeDict.TryGetValue(item.StandardGrade, out var gm) ? gm.PlantGrade : item.PlantGrade,
                    Specification = item.Specification,
                    OuterDiameterNegative = item.OuterDiameterNegative,
                    OuterDiameterPositive = item.OuterDiameterPositive,
                    WallThicknessNegative = item.WallThicknessNegative,
                    WallThicknessPositive = item.WallThicknessPositive,
                    LengthStatus = item.LengthStatus,
                    MinLength = item.MinLength,
                    MaxLength = item.MaxLength,
                    Quantity = item.Quantity,
                    Meters = item.Meters,
                    ContractWeight = item.ContractWeight,
                    TheoreticalWeight = item.TheoreticalWeight,
                    RequirementType = item.ProductRequirement?.RequirementType ?? RequirementType.Normal,
                    SuggestedMainNo = suggestedMainNo
                };

                // 尝试获取原主号/次号（若存在则填充，供覆盖生成时预填使用）
                // 注意：仅当项次的合并关键字段与原工单一致时才保留 OriginalMainNo
                // 若交货期/规格等关键字段变了，视为新增项次，不自动归入原工单
                if (itemToOriginalNo.TryGetValue(item.Sequence, out var original)
                    && itemToWorkOrder.TryGetValue(item.Sequence, out var originalWo))
                {
                    bool stillMatches = item.DeliveryDate == originalWo.DeliveryDate
                        && item.DelayPenalty == originalWo.DelayPenalty
                        && item.PipeManufacturingType == originalWo.PipeManufacturingType
                        && item.SettlementMethod == originalWo.SettlementMethod
                        && item.DeliveryState == originalWo.DeliveryState
                        && item.PlantGrade == originalWo.PlantGrade
                        && item.Specification == originalWo.Specification
                        && item.OuterDiameterNegative == originalWo.OuterDiameterNegative
                        && item.OuterDiameterPositive == originalWo.OuterDiameterPositive
                        && item.WallThicknessNegative == originalWo.WallThicknessNegative
                        && item.WallThicknessPositive == originalWo.WallThicknessPositive
                        && item.LengthStatus == originalWo.LengthStatus;

                    if (stillMatches)
                    {
                        dto.OriginalMainNo = original.MainNo;
                        dto.OriginalSubNo = original.SubNo;
                    }
                }
                result.Add(dto);
            }
        }
        return result;
    }

    private string GetMergeKey(OrderItem item)
    {
        return $"{item.DeliveryDate:yyyy-MM-dd}|{item.DelayPenalty}|{item.PipeManufacturingType}|{item.SettlementMethod}|" +
               $"{item.StandardNo}|{item.DeliveryState}|{item.PlantGrade}|{item.Specification}|" +
               $"{item.OuterDiameter}|{item.WallThickness}|" +
               $"{item.OuterDiameterNegative}|{item.OuterDiameterPositive}|" +
               $"{item.WallThicknessNegative}|{item.WallThicknessPositive}|" +
               $"{item.LengthStatus}";
    }

    private (bool IsValid, List<string> Errors) ValidateMergeFields(OrderItem item1, OrderItem item2)
    {
        var errors = new List<string>();

        if (item1.DeliveryDate != item2.DeliveryDate)
            errors.Add($"交货日期 ({item1.DeliveryDate:yyyy-MM-dd} ≠ {item2.DeliveryDate:yyyy-MM-dd})");
        if (item1.DelayPenalty != item2.DelayPenalty)
            errors.Add($"延期罚款 ({item1.DelayPenalty} ≠ {item2.DelayPenalty})");
        if (item1.SettlementMethod != item2.SettlementMethod)
            errors.Add($"结算方式 ({item1.SettlementMethod} ≠ {item2.SettlementMethod})");
        if (item1.PipeManufacturingType != item2.PipeManufacturingType)
            errors.Add($"物料名称 ({item1.PipeManufacturingType} ≠ {item2.PipeManufacturingType})");
        if (item1.StandardNo != item2.StandardNo)
            errors.Add($"标准号 ({item1.StandardNo} ≠ {item2.StandardNo})");
        if (item1.DeliveryState != item2.DeliveryState)
            errors.Add($"交货状态 ({item1.DeliveryState} ≠ {item2.DeliveryState})");
        if (item1.StandardGrade != item2.StandardGrade)
            errors.Add($"标准牌号 ({item1.StandardGrade} ≠ {item2.StandardGrade})");
        if (item1.OuterDiameter != item2.OuterDiameter)
            errors.Add($"外径 ({item1.OuterDiameter} ≠ {item2.OuterDiameter})");
        if (item1.WallThickness != item2.WallThickness)
            errors.Add($"壁厚 ({item1.WallThickness} ≠ {item2.WallThickness})");
        if (item1.OuterDiameterNegative != item2.OuterDiameterNegative)
            errors.Add($"外径下偏差 ({item1.OuterDiameterNegative} ≠ {item2.OuterDiameterNegative})");
        if (item1.OuterDiameterPositive != item2.OuterDiameterPositive)
            errors.Add($"外径上偏差 ({item1.OuterDiameterPositive} ≠ {item2.OuterDiameterPositive})");
        if (item1.WallThicknessNegative != item2.WallThicknessNegative)
            errors.Add($"壁厚下偏差 ({item1.WallThicknessNegative} ≠ {item2.WallThicknessNegative})");
        if (item1.WallThicknessPositive != item2.WallThicknessPositive)
            errors.Add($"壁厚上偏差 ({item1.WallThicknessPositive} ≠ {item2.WallThicknessPositive})");
        if (item1.LengthStatus != item2.LengthStatus)
            errors.Add($"长度状态 ({item1.LengthStatus} ≠ {item2.LengthStatus})");

        return (errors.Count == 0, errors);
    }

    private List<List<OrderItem>> GroupOrderItemsByMergeFields(List<OrderItem> orderItems)
    {
        var groups = new Dictionary<string, List<OrderItem>>();

        foreach (var item in orderItems)
        {
            var key = GetMergeKey(item);
            if (!groups.ContainsKey(key))
                groups[key] = new List<OrderItem>();
            groups[key].Add(item);
        }
        return groups.Values.ToList();
    }

    private static string GetMainNoPrefix(PipeManufacturingType pipeManufacturingType, LengthStatus lengthStatus)
    {
        if (pipeManufacturingType == PipeManufacturingType.WeldedPipe)
            return "H";
        else
            return lengthStatus switch
            {
                LengthStatus.Fixed => "D",
                LengthStatus.Range => "F",
                LengthStatus.NonFixed => "L",
                _ => "D"
            };
    }

    private static void ValidateSubNo(LengthStatus lengthStatus, string? productionSubNo)
    {
        if (lengthStatus == LengthStatus.Fixed)
        {
            if (string.IsNullOrEmpty(productionSubNo))
                throw new BusinessException("定尺模式下次号不能为空");
            if (!System.Text.RegularExpressions.Regex.IsMatch(productionSubNo, @"^C\d{2}$"))
                throw new BusinessException($"次号格式必须为C+两位数字，当前值：{productionSubNo}");
        }
        else
        {
            if (!string.IsNullOrEmpty(productionSubNo))
                throw new BusinessException($"{GetLengthStatusText(lengthStatus)}模式下不允许填写次号");
        }
    }

    public async Task<List<GeneratedWorkOrderDto>> GenerateWorkOrdersAsync(CreateWorkOrderRequest request)
    {
        // 使用信号量确保同一时间只有一个工单生成操作
        await _workOrderNoSemaphore.WaitAsync();
        try
        {
            if (request.GenerateMode == WorkOrderGenerateMode.Update)
            {
                return await UpdateWorkOrdersAsync(request);
            }
            return await GenerateWorkOrdersCoreAsync(request);
        }
        finally
        {
            _workOrderNoSemaphore.Release();
        }
    }

    private async Task<List<GeneratedWorkOrderDto>> GenerateWorkOrdersCoreAsync(CreateWorkOrderRequest request)
    {
        // 1. 获取订单信息
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.OrderNumber == request.SalesOrderNo);

        if (salesOrder == null)
            throw new BusinessException($"订单 {request.SalesOrderNo} 不存在");

        if (salesOrder.Status != SalesOrderStatus.Confirmed)
            throw new BusinessException($"订单 {request.SalesOrderNo} 状态不是已确认，无法生成工单");

        // 单独加载 Customer（从 SalesOrder 快照字段读取）
        var salesOrderCustomer = salesOrder;

        // 2. 获取订单项次
        var allOrderItems = await _context.OrderItems
            .Include(oi => oi.ProductRequirement)
            .Where(oi => oi.SalesOrderId == salesOrder.Id)
            .ToDictionaryAsync(oi => oi.Sequence, oi => oi);

        // 单独加载 StandardRegister
        var standardNos = allOrderItems.Values
            .Where(oi => !string.IsNullOrEmpty(oi.StandardNo))
            .Select(oi => oi.StandardNo)
            .Distinct()
            .ToList();
        var srDict = standardNos.Any()
            ? await _context.StandardRegisters
                .Where(sr => standardNos.Contains(sr.StandardNo))
                .ToDictionaryAsync(sr => sr.StandardNo, sr => sr, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, MES.Data.Entities.StandardRegister.StandardRegister>(StringComparer.OrdinalIgnoreCase);

        // 3. 验证项次
        foreach (var workOrderGroup in request.WorkOrders)
        {
            foreach (var itemId in workOrderGroup.OrderItemIds)
            {
                if (!allOrderItems.ContainsKey(itemId))
                    throw new BusinessException($"项次序号 {itemId} 不存在或已被删除");
            }
        }

        // 4. 验证合并规则
        var mergeFieldErrors = new List<string>();
        foreach (var workOrderGroup in request.WorkOrders)
        {
            var groupItems = workOrderGroup.OrderItemIds
                .Select(id => allOrderItems.GetValueOrDefault(id))
                .OfType<OrderItem>()
                .ToList();
            if (!groupItems.Any())
                throw new BusinessException($"工单分组 {workOrderGroup.ProductionMainNo} 没有有效的项次");
            if (groupItems.Count <= 1) continue;

            var firstItem = groupItems.First();
            foreach (var item in groupItems.Skip(1))
            {
                var (isValid, errors) = ValidateMergeFields(firstItem, item);
                if (!isValid)
                {
                    mergeFieldErrors.Add($"主号 {workOrderGroup.ProductionMainNo} 下的项次 {item.Sequence} 与项次 {firstItem.Sequence} 合并字段不一致:\n  {string.Join("\n  ", errors)}");
                }
            }
        }
        if (mergeFieldErrors.Any())
            throw new BusinessException($"工单分组合并规则验证失败:\n\n{string.Join("\n\n", mergeFieldErrors)}");

        var generatedWorkOrders = new List<GeneratedWorkOrderDto>();

        // 5. 使用事务
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                // 6. 物理删除现有工单及其关联计划
                var existingWorkOrders = await _context.WorkOrders
                    .Where(wo => wo.SalesOrderNo == request.SalesOrderNo)
                    .ToListAsync();

                _logger.LogInformation("订单 {OrderNo} 原有工单数量: {Count}，执行物理删除",
                    request.SalesOrderNo, existingWorkOrders.Count);

                // 先清理关联的用料计划（避免孤立记录导致采购状态显示空工单号）
                var existingIds = existingWorkOrders.Select(wo => wo.Id).ToList();
                if (existingIds.Count > 0)
                {
                    var semiPlans = await _context.PurchaseSemiPlans
                        .Where(p => existingIds.Contains(p.WorkOrderId)).ToListAsync();
                    var finishPlans = await _context.PurchaseFinishedPlans
                        .Where(p => existingIds.Contains(p.WorkOrderId)).ToListAsync();
                    var invPlans = await _context.InventoryPlans
                        .Where(p => existingIds.Contains(p.WorkOrderId)).ToListAsync();
                    var piercingPlans = await _context.RoundBarPiercingPlans
                        .Where(p => existingIds.Contains(p.WorkOrderId)).ToListAsync();
                    var inProcessReworkPlans = await _context.InProcessReworkPlans
                        .Where(p => existingIds.Contains(p.WorkOrderId)).ToListAsync();

                    if (semiPlans.Any()) _context.PurchaseSemiPlans.RemoveRange(semiPlans);
                    if (finishPlans.Any()) _context.PurchaseFinishedPlans.RemoveRange(finishPlans);
                    if (invPlans.Any()) _context.InventoryPlans.RemoveRange(invPlans);
                    if (piercingPlans.Any()) _context.RoundBarPiercingPlans.RemoveRange(piercingPlans);
                    if (inProcessReworkPlans.Any()) _context.InProcessReworkPlans.RemoveRange(inProcessReworkPlans);

                    // 清理读模型行
                    var delListRows = await _context.Set<WorkOrderListSummary>()
                        .Where(s => existingIds.Contains(s.WorkOrderId)).ToListAsync();
                    if (delListRows.Count != 0)
                        _context.Set<WorkOrderListSummary>().RemoveRange(delListRows);
                    var delExecRows = await _context.Set<WorkOrderExecutionSummary>()
                        .Where(s => existingIds.Contains(s.WorkOrderId)).ToListAsync();
                    if (delExecRows.Count != 0)
                        _context.Set<WorkOrderExecutionSummary>().RemoveRange(delExecRows);

                    await _context.SaveChangesAsync();
                }

                _context.WorkOrders.RemoveRange(existingWorkOrders);
                await _context.SaveChangesAsync();

                // 清除缓存，确保查询最新数据
                _context.ChangeTracker.Clear();

                var workOrdersToAdd = new List<WoEntity>();
                generatedWorkOrders = new List<GeneratedWorkOrderDto>();

                foreach (var workOrderGroup in request.WorkOrders)
                {
                    var groupItems = workOrderGroup.OrderItemIds
                        .Select(id => allOrderItems.GetValueOrDefault(id))
                        .Where(item => item != null)
                        .Select(x => x!)
                        .ToList();
                    if (!groupItems.Any()) continue;

                    var firstItem = groupItems.First()!;

                    ValidateSubNo(firstItem.LengthStatus, workOrderGroup.ProductionSubNo);

                    var (minLength, maxLength, totalQuantity, totalMeters, totalWeight, itemDetails, technicalRequirements) =
                        CalculateAggregates(groupItems, firstItem.LengthStatus);

                    // 工单号格式: {订单号}-{主号}[-{次号}]
                    var subPart = string.IsNullOrEmpty(workOrderGroup.ProductionSubNo) ? "" : $"-{workOrderGroup.ProductionSubNo}";
                    var workOrderNo = $"{request.SalesOrderNo}-{workOrderGroup.ProductionMainNo}{subPart}";

                    _logger.LogWarning($"生成工单号: {workOrderNo}");

                    var workOrder = new WoEntity
                    {
                        WorkOrderNo = workOrderNo,
                        SalesOrderNo = request.SalesOrderNo,
                        ProductionMainNo = workOrderGroup.ProductionMainNo,
                        ProductionSubNo = workOrderGroup.ProductionSubNo,
                        OrderItemIds = string.Join(",", workOrderGroup.OrderItemIds.Distinct()),
                        Status = WorkOrderStatus.Confirmed,
                        SignDate = salesOrder.SignDate,
                        Salesman = salesOrderCustomer?.Salesman ?? string.Empty,
                        EndCustomer = salesOrderCustomer?.EndCustomer,
                        DeliveryDate = firstItem.DeliveryDate,
                        DelayPenalty = firstItem.DelayPenalty,
                        PipeManufacturingType = firstItem.PipeManufacturingType,
                        SettlementMethod = firstItem.SettlementMethod,
                        StandardCode = srDict.GetValueOrDefault(firstItem.StandardNo ?? string.Empty)?.StandardNo ?? firstItem.StandardNo ?? string.Empty,
                        DeliveryState = firstItem.DeliveryState,
                        PlantGrade = firstItem.PlantGrade,
                        Specification = firstItem.Specification,
                        OuterDiameterNegative = firstItem.OuterDiameterNegative,
                        OuterDiameterPositive = firstItem.OuterDiameterPositive,
                        WallThicknessNegative = firstItem.WallThicknessNegative,
                        WallThicknessPositive = firstItem.WallThicknessPositive,
                        LengthStatus = firstItem.LengthStatus,
                        MinLength = minLength,
                        MaxLength = maxLength,
                        TotalQuantity = totalQuantity,
                        TotalMeters = totalMeters,
                        TotalWeight = totalWeight,
                        TotalItemCount = groupItems.Count,
                        ItemDetails = itemDetails,
                        TechnicalRequirements = technicalRequirements
                    };

                    workOrdersToAdd.Add(workOrder);

                    generatedWorkOrders.Add(new GeneratedWorkOrderDto
                    {
                        Id = 0,
                        WorkOrderNo = workOrderNo,
                        SalesOrderNo = request.SalesOrderNo,
                        ProductionMainNo = workOrderGroup.ProductionMainNo,
                        ProductionSubNo = workOrderGroup.ProductionSubNo,
                        Status = WorkOrderStatus.Confirmed,
                        TotalQuantity = totalQuantity,
                        TotalWeight = totalWeight
                    });
                }

                await _context.WorkOrders.AddRangeAsync(workOrdersToAdd);
                await _context.SaveChangesAsync();

                for (int i = 0; i < workOrdersToAdd.Count; i++)
                {
                    generatedWorkOrders[i].Id = workOrdersToAdd[i].Id;
                }

                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("UK_WorkOrder_WorkOrderNo") == true)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "生成工单时发生唯一键冲突，订单号 {OrderNo}", request.SalesOrderNo);
                throw new BusinessException("生成工单时发生工单号冲突，请稍后重试");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "生成工单失败: 订单号 {OrderNo}", request.SalesOrderNo);
                throw;
            }
        }

        // 刷新读模型（事务已提交，在 using 块之外执行）
        if (_listSummaryService != null) await _listSummaryService.RefreshBySalesOrderAsync(request.SalesOrderNo);
        foreach (var wo in generatedWorkOrders)
            await TryRefreshExecutionSummaryAsync(wo.WorkOrderNo);

        _logger.LogInformation("生成工单成功: 订单号 {OrderNo}, 生成 {Count} 个工单",
            request.SalesOrderNo, generatedWorkOrders.Count);

        return generatedWorkOrders;
    }

    /// <summary>
    /// 更新修改模式：保留现有工单号/主号/次号，仅增删项次并重算汇总
    /// </summary>
    private async Task<List<GeneratedWorkOrderDto>> UpdateWorkOrdersAsync(CreateWorkOrderRequest request)
    {
        // 1. 获取订单信息
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.OrderNumber == request.SalesOrderNo);
        if (salesOrder == null)
            throw new BusinessException($"订单 {request.SalesOrderNo} 不存在");
        if (salesOrder.Status != SalesOrderStatus.Confirmed)
            throw new BusinessException($"订单 {request.SalesOrderNo} 状态不是已确认，无法修改工单");

        var customer = salesOrder;

        // 2. 获取订单项次
        var allOrderItems = await _context.OrderItems
            .Include(oi => oi.ProductRequirement)
            .Where(oi => oi.SalesOrderId == salesOrder.Id)
            .ToDictionaryAsync(oi => oi.Sequence, oi => oi);

        var standardNos = allOrderItems.Values
            .Where(oi => !string.IsNullOrEmpty(oi.StandardNo))
            .Select(oi => oi.StandardNo).Distinct().ToList();
        var srDict = standardNos.Any()
            ? await _context.StandardRegisters
                .Where(sr => standardNos.Contains(sr.StandardNo))
                .ToDictionaryAsync(sr => sr.StandardNo, sr => sr, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, MES.Data.Entities.StandardRegister.StandardRegister>(StringComparer.OrdinalIgnoreCase);

        // 3. 验证项次
        foreach (var workOrderGroup in request.WorkOrders)
        {
            foreach (var itemId in workOrderGroup.OrderItemIds)
            {
                if (!allOrderItems.ContainsKey(itemId))
                    throw new BusinessException($"项次 ID {itemId} 不存在或已被删除");
            }
        }

        // 3b. 验证所有项次都已分配到某个工单（防止删除工单后孤儿项次被静默遗漏）
        var assignedItemIds = request.WorkOrders
            .SelectMany(wo => wo.OrderItemIds)
            .ToHashSet();
        var unassignedItems = allOrderItems.Values
            .Where(oi => !assignedItemIds.Contains(oi.Sequence))
            .ToList();
        if (unassignedItems.Count != 0)
        {
            var desc = string.Join("、", unassignedItems.Select(i => $"项次 {i.Sequence}"));
            throw new BusinessException($"以下项次未分配到任何工单，请将其合并到某个工单后提交: {desc}");
        }

        // 4. 验证合并规则
        var mergeFieldErrors = new List<string>();
        foreach (var workOrderGroup in request.WorkOrders)
        {
            var groupItems = workOrderGroup.OrderItemIds
                .Select(id => allOrderItems.GetValueOrDefault(id))
                .OfType<OrderItem>()
                .ToList();
            if (!groupItems.Any()) continue;
            if (groupItems.Count <= 1) continue;

            var firstItem = groupItems.First();
            foreach (var item in groupItems.Skip(1))
            {
                var (isValid, errors) = ValidateMergeFields(firstItem, item);
                if (!isValid)
                {
                    mergeFieldErrors.Add($"主号 {workOrderGroup.ProductionMainNo} 下的项次 {item.Sequence} 与项次 {firstItem.Sequence} 合并字段不一致:\n  {string.Join("\n  ", errors)}");
                }
            }
        }
        if (mergeFieldErrors.Any())
            throw new BusinessException($"工单分组合并规则验证失败:\n\n{string.Join("\n\n", mergeFieldErrors)}");

        // 5. 加载现有工单，构建 (主号,次号) → WorkOrder 映射
        var existingWorkOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == request.SalesOrderNo)
            .ToListAsync();

        var existingByKey = new Dictionary<(string mainNo, string? subNo), WoEntity>();
        foreach (var wo in existingWorkOrders)
        {
            var key = (wo.ProductionMainNo, wo.ProductionSubNo);
            existingByKey.TryAdd(key, wo);
        }

        _logger.LogInformation("订单 {OrderNo} 更新修改: 现有工单 {ExistingCount} 个, 提交分组 {GroupCount} 个",
            request.SalesOrderNo, existingWorkOrders.Count, request.WorkOrders.Count);

        var result = new List<GeneratedWorkOrderDto>();
        var workOrdersToAdd = new List<WoEntity>();

        // 6. 事务处理
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                var matchedKeys = new HashSet<(string mainNo, string? subNo)>();

                // 7. 遍历提交的分组：匹配现有工单则更新，否则新建
                foreach (var group in request.WorkOrders)
                {
                    var groupItems = group.OrderItemIds
                        .Select(id => allOrderItems.GetValueOrDefault(id))
                        .Where(item => item != null)
                        .Select(x => x!)
                        .ToList();
                    if (!groupItems.Any()) continue;

                    var firstItem = groupItems.First()!;
                    var key = (group.ProductionMainNo, group.ProductionSubNo);

                    if (existingByKey.TryGetValue(key, out var existingWo))
                    {
                        // 7a. 匹配到现有工单 → 更新
                        matchedKeys.Add(key);

                        var oldItemIds = existingWo.OrderItemIds
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(id => int.TryParse(id, out var parsed) ? parsed : -1)
                            .Where(id => id > 0)
                            .ToHashSet();
                        var newItemIds = group.OrderItemIds.ToHashSet();

                        bool changed = !oldItemIds.SetEquals(newItemIds);

                        // 始终同步订单级字段（客户名称/业务员/最终客户等变化时也需要更新）
                        var orderLevelChanged = existingWo.Salesman != (customer?.Salesman ?? string.Empty)
                            || existingWo.EndCustomer != customer?.EndCustomer
                            || existingWo.SignDate != salesOrder.SignDate;
                        if (orderLevelChanged)
                        {
                            existingWo.Salesman = customer?.Salesman ?? string.Empty;
                            existingWo.EndCustomer = customer?.EndCustomer;
                            existingWo.SignDate = salesOrder.SignDate;
                            changed = true;
                        }

                        if (changed)
                        {
                            ValidateSubNo(firstItem.LengthStatus, group.ProductionSubNo);

                            existingWo.OrderItemIds = string.Join(",", group.OrderItemIds.Distinct());

                            _logger.LogInformation("更新修改工单: {WorkOrderNo}, 项次 {OldCount}→{NewCount}",
                                existingWo.WorkOrderNo, oldItemIds.Count, newItemIds.Count);
                        }

                        // 始终重算聚合字段（即使项次无变化，也要修复原始生成时的错误值）
                        var (minLength, maxLength, totalQuantity, totalMeters, totalWeight, itemDetails, technicalRequirements) =
                            CalculateAggregates(groupItems, firstItem.LengthStatus);

                        existingWo.MinLength = minLength;
                        existingWo.MaxLength = maxLength;
                        existingWo.TotalQuantity = totalQuantity;
                        existingWo.TotalMeters = totalMeters;
                        existingWo.TotalWeight = totalWeight;
                        existingWo.TotalItemCount = groupItems.Count;
                        existingWo.ItemDetails = itemDetails;
                        existingWo.TechnicalRequirements = technicalRequirements;
                        existingWo.Status = WorkOrderStatus.Confirmed;

                        if (!changed) changed = true;

                        result.Add(new GeneratedWorkOrderDto
                        {
                            Id = existingWo.Id,
                            WorkOrderNo = existingWo.WorkOrderNo,
                            SalesOrderNo = request.SalesOrderNo,
                            ProductionMainNo = group.ProductionMainNo,
                            ProductionSubNo = group.ProductionSubNo,
                            Status = WorkOrderStatus.Confirmed,
                            TotalQuantity = groupItems.Sum(i => i!.LengthStatus == LengthStatus.Fixed ? (i.Quantity ?? 0) : 0),
                            TotalWeight = groupItems.Sum(i => i!.LengthStatus == LengthStatus.Fixed ? i.TheoreticalWeight : i.ContractWeight),
                            IsModified = changed
                        });
                    }
                    else
                    {
                        // 7b. 未匹配到现有工单 → 新建工单
                        ValidateSubNo(firstItem.LengthStatus, group.ProductionSubNo);

                        var (newMinLength, newMaxLength, newTotalQuantity, newTotalMeters, newTotalWeight, newItemDetails, newTechRequirements) =
                            CalculateAggregates(groupItems, firstItem.LengthStatus);

                        // 工单号格式: {订单号}-{主号}[-{次号}]
                        var subPart = string.IsNullOrEmpty(group.ProductionSubNo) ? "" : $"-{group.ProductionSubNo}";
                        var workOrderNo = $"{request.SalesOrderNo}-{group.ProductionMainNo}{subPart}";

                        var workOrder = new WoEntity
                        {
                            WorkOrderNo = workOrderNo,
                            SalesOrderNo = request.SalesOrderNo,
                            ProductionMainNo = group.ProductionMainNo,
                            ProductionSubNo = group.ProductionSubNo,
                            OrderItemIds = string.Join(",", group.OrderItemIds.Distinct()),
                            Status = WorkOrderStatus.Confirmed,
                            SignDate = salesOrder.SignDate,
                            Salesman = customer?.Salesman ?? string.Empty,
                            EndCustomer = customer?.EndCustomer,
                            DeliveryDate = firstItem.DeliveryDate,
                            DelayPenalty = firstItem.DelayPenalty,
                            PipeManufacturingType = firstItem.PipeManufacturingType,
                            SettlementMethod = firstItem.SettlementMethod,
                            StandardCode = srDict.GetValueOrDefault(firstItem.StandardNo ?? string.Empty)?.StandardNo ?? firstItem.StandardNo ?? string.Empty,
                            DeliveryState = firstItem.DeliveryState,
                            PlantGrade = firstItem.PlantGrade,
                            Specification = firstItem.Specification,
                            OuterDiameterNegative = firstItem.OuterDiameterNegative,
                            OuterDiameterPositive = firstItem.OuterDiameterPositive,
                            WallThicknessNegative = firstItem.WallThicknessNegative,
                            WallThicknessPositive = firstItem.WallThicknessPositive,
                            LengthStatus = firstItem.LengthStatus,
                            MinLength = newMinLength,
                            MaxLength = newMaxLength,
                            TotalQuantity = newTotalQuantity,
                            TotalMeters = newTotalMeters,
                            TotalWeight = newTotalWeight,
                            TotalItemCount = groupItems.Count,
                            ItemDetails = newItemDetails,
                            TechnicalRequirements = newTechRequirements
                        };

                        workOrdersToAdd.Add(workOrder);

                        _logger.LogInformation("更新修改新建工单: {WorkOrderNo}, 主号 {MainNo}", workOrderNo, group.ProductionMainNo);
                    }
                }

                // 8. 删除未匹配到的现有工单（项次全部移除）
                foreach (var wo in existingWorkOrders)
                {
                    var key = (wo.ProductionMainNo, wo.ProductionSubNo);
                    if (!matchedKeys.Contains(key))
                    {
                        // 清理关联用料计划
                        var semiPlans = await _context.PurchaseSemiPlans
                            .Where(p => p.WorkOrderId == wo.Id).ToListAsync();
                        var finishPlans = await _context.PurchaseFinishedPlans
                            .Where(p => p.WorkOrderId == wo.Id).ToListAsync();
                        var invPlans = await _context.InventoryPlans
                            .Where(p => p.WorkOrderId == wo.Id).ToListAsync();
                        var piercingPlans = await _context.RoundBarPiercingPlans
                            .Where(p => p.WorkOrderId == wo.Id).ToListAsync();
                        var inProcessReworkPlans = await _context.InProcessReworkPlans
                            .Where(p => p.WorkOrderId == wo.Id).ToListAsync();

                        if (semiPlans.Any()) _context.PurchaseSemiPlans.RemoveRange(semiPlans);
                        if (finishPlans.Any()) _context.PurchaseFinishedPlans.RemoveRange(finishPlans);
                        if (invPlans.Any()) _context.InventoryPlans.RemoveRange(invPlans);
                        if (piercingPlans.Any()) _context.RoundBarPiercingPlans.RemoveRange(piercingPlans);
                        if (inProcessReworkPlans.Any()) _context.InProcessReworkPlans.RemoveRange(inProcessReworkPlans);

                        // 清理读模型行（删除工单的执行状况不会在后续增量刷新中被清除）
                        var delListSummary = await _context.Set<WorkOrderListSummary>()
                            .FirstOrDefaultAsync(s => s.WorkOrderId == wo.Id);
                        if (delListSummary != null)
                            _context.Set<WorkOrderListSummary>().Remove(delListSummary);
                        var delExecSummary = await _context.Set<WorkOrderExecutionSummary>()
                            .FirstOrDefaultAsync(s => s.WorkOrderId == wo.Id);
                        if (delExecSummary != null)
                            _context.Set<WorkOrderExecutionSummary>().Remove(delExecSummary);

                        _context.WorkOrders.Remove(wo);
                        _logger.LogInformation("更新修改删除工单（无项次）: {WorkOrderNo}", wo.WorkOrderNo);
                    }
                }

                // 9. 保存新建的工单
                if (workOrdersToAdd.Any())
                {
                    await _context.WorkOrders.AddRangeAsync(workOrdersToAdd);
                }

                await _context.SaveChangesAsync();

                // 10. 为新建的工单填充ID
                for (int i = 0; i < workOrdersToAdd.Count; i++)
                {
                    result.Add(new GeneratedWorkOrderDto
                    {
                        Id = workOrdersToAdd[i].Id,
                        WorkOrderNo = workOrdersToAdd[i].WorkOrderNo,
                        SalesOrderNo = request.SalesOrderNo,
                        ProductionMainNo = workOrdersToAdd[i].ProductionMainNo,
                        ProductionSubNo = workOrdersToAdd[i].ProductionSubNo,
                        Status = WorkOrderStatus.Confirmed,
                        TotalQuantity = workOrdersToAdd[i].TotalQuantity,
                        TotalWeight = workOrdersToAdd[i].TotalWeight,
                        IsModified = true
                    });
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "更新修改工单失败: 订单号 {OrderNo}", request.SalesOrderNo);
                throw;
            }
        }

        // 刷新读模型（事务已提交，在 using 块之外执行）
        if (_listSummaryService != null) await _listSummaryService.RefreshBySalesOrderAsync(request.SalesOrderNo);
        var woNos = result.Select(r => r.WorkOrderNo).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
        if (woNos.Count != 0) await TryRefreshExecutionSummariesAsync(woNos);

        _logger.LogInformation("更新修改工单成功: 订单号 {OrderNo}, 更新 {UpdatedCount} 个, 新建 {NewCount} 个",
            request.SalesOrderNo, result.Count(r => !r.IsModified || existingByKey.ContainsKey((r.ProductionMainNo, r.ProductionSubNo))),
            workOrdersToAdd.Count);

        return result;
    }

    private (decimal? MinLength, decimal? MaxLength, int TotalQuantity, decimal TotalMeters,
             decimal TotalWeight, string? ItemDetails, RequirementType TechnicalRequirements)
        CalculateAggregates(List<OrderItem> items, LengthStatus lengthStatus)
    {
        decimal? minLength = null;
        decimal? maxLength = null;
        int totalQuantity = 0;
        decimal totalMeters = 0;
        decimal totalWeight = 0;
        var itemDetailsBuilder = new System.Text.StringBuilder();
        bool hasSpecialRequirement = false;

        foreach (var item in items)
        {
            if (item.MinLength.HasValue)
            {
                if (!minLength.HasValue || item.MinLength < minLength) minLength = item.MinLength;
            }
            if (item.MaxLength.HasValue)
            {
                if (!maxLength.HasValue || item.MaxLength > maxLength) maxLength = item.MaxLength;
            }

            if (item.ProductRequirement != null && item.ProductRequirement.RequirementType == RequirementType.Special)
                hasSpecialRequirement = true;

            if (lengthStatus == LengthStatus.Fixed)
            {
                totalQuantity += item.Quantity ?? 0;
                totalMeters += item.Meters ?? 0;
                totalWeight += item.TheoreticalWeight;

                if (item.Quantity.HasValue && item.Quantity > 0 && item.MaxLength.HasValue && item.MaxLength > 0)
                {
                    itemDetailsBuilder.Append($"{item.Sequence}项,{item.MaxLength}mm,{item.Quantity}支;");
                }
            }
            else
            {
                totalWeight += item.ContractWeight;
            }
        }

        var technicalRequirements = hasSpecialRequirement ? RequirementType.Special : RequirementType.Normal;

        return (minLength, maxLength, totalQuantity, totalMeters, totalWeight,
                itemDetailsBuilder.Length > 0 ? itemDetailsBuilder.ToString() : null, technicalRequirements);
    }

    #endregion

    #region 工单管理

    public async Task<List<WorkOrderListDto>> GetAllListAsync()
    {
        var items = await _context.Set<WorkOrderListSummary>()
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedTime)
            .Select(s => new WorkOrderListDto
            {
                Id = s.WorkOrderId,
                WorkOrderNo = s.WorkOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                ProductionMainNo = s.ProductionMainNo,
                ProductionSubNo = s.ProductionSubNo,
                SignDate = s.SignDate,
                Salesman = s.Salesman,
                EndCustomer = s.EndCustomer,
                DeliveryDate = s.DeliveryDate,
                DelayPenalty = s.DelayPenalty,
                SettlementMethod = Enum.Parse<SettlementMethod>(s.SettlementMethod),
                PlantGrade = s.PlantGrade,
                PipeManufacturingType = Enum.Parse<PipeManufacturingType>(s.MaterialName),
                Specification = s.Specification,
                LengthStatus = Enum.Parse<LengthStatus>(s.LengthStatus),
                MinLength = s.MinLength,
                MaxLength = s.MaxLength,
                TotalQuantity = s.TotalQuantity,
                TotalWeight = s.TotalWeight,
                DeliveryState = Enum.Parse<DeliveryState>(s.DeliveryState),
                TotalItemCount = s.TotalItemCount,
                Status = (WorkOrderStatus)s.Status,
                CreatedTime = s.CreatedTime,
                MaterialPlanStatus = (MaterialPlanStatus)s.MaterialPlanStatus,
                MaterialPlanRate = s.MaterialPlanRate,
                MainNoMaterialPlanStatus = (MaterialPlanStatus)s.MainNoMaterialPlanStatus,
                MainNoMaterialPlanRate = s.MainNoMaterialPlanRate,
                OrderMaterialPlanStatus = (MaterialPlanStatus)s.OrderMaterialPlanStatus,
                LatestPlanDate = s.LatestPlanDate,
                SemiPlanTotalWeight = s.SemiPlanTotalWeight,
                SemiPlanTotalPieces = s.SemiPlanTotalPieces,
                FinishedPlanTotalWeight = s.FinishedPlanTotalWeight,
                FinishedPlanTotalPieces = s.FinishedPlanTotalPieces,
                InventoryPlanTotalWeight = s.InventoryPlanTotalWeight,
                InventoryPlanTotalPieces = s.InventoryPlanTotalPieces,
                ReworkPlanTotalWeight = s.ReworkPlanTotalWeight,
                ReworkPlanTotalPieces = s.ReworkPlanTotalPieces,
                PiercingPlanTotalWeight = s.PiercingPlanTotalWeight,
                PiercingPlanTotalPieces = s.PiercingPlanTotalPieces,
                InProcessReworkPlanTotalWeight = s.InProcessReworkPlanTotalWeight,
                InProcessReworkPlanTotalPieces = s.InProcessReworkPlanTotalPieces,
                MaxStandardCycle = s.MaxStandardCycle,
                MainNoMaxStandardCycle = s.MainNoMaxStandardCycle,
                CapacityWorkDays = s.CapacityWorkDays,
                TheoreticalCutoffDate = s.TheoreticalCutoffDate,
                MaterialPlanCoveredCount = s.MaterialPlanCoveredCount,
                MaterialPlanProportion = s.MaterialPlanProportion,
                LatestRequiredDate = s.LatestRequiredDate
            })
            .ToListAsync();

        return items;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("WorkOrderService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var items = await _context.Set<WorkOrderListSummary>()
                .AsNoTracking()
                .Select(s => new
                {
                    s.WorkOrderNo,
                    s.SalesOrderNo,
                    s.ProductionMainNo,
                    s.ProductionSubNo,
                    s.SignDate,
                    s.Salesman,
                    s.EndCustomer,
                    s.DeliveryDate,
                    s.PlantGrade,
                    s.Specification,
                    s.LatestPlanDate
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["WorkOrderNo"] = items.Select(x => x.WorkOrderNo).Distinct().OrderBy(x => x).ToList(),
                ["SalesOrderNo"] = items.Select(x => x.SalesOrderNo).Distinct().OrderBy(x => x).ToList(),
                ["ProductionMainNo"] = items.Select(x => x.ProductionMainNo).Distinct().OrderBy(x => x).ToList(),
                ["ProductionSubNo"] = items.Select(x => x.ProductionSubNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SignDate"] = items.Select(x => x.SignDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["Salesman"] = items.Select(x => x.Salesman).Distinct().OrderBy(x => x).ToList(),
                ["EndCustomer"] = items.Select(x => x.EndCustomer).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["DeliveryDate"] = items.Select(x => x.DeliveryDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["PlantGrade"] = items.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
                ["Specification"] = items.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
                ["LatestPlanDate"] = items.Select(x => x.LatestPlanDate?.ToString("yyyy-MM-dd")).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<PagedResult<WorkOrderListItemDto>> GetPagedAsync(WorkOrderQueryParams query)
    {
        return await GetPagedBasicAsync(query);
    }

    public async Task<PagedResult<WorkOrderListDto>> GetPagedWithPlansAsync(WorkOrderQueryParams query)
    {
        return await GetPagedEnrichedAsync(query);
    }


    #region 工单分页查询基础方法

    /// <summary>
    /// 基础分页查询：直接查 WorkOrder 表，返回精简 DTO（不含用料计划聚合数据）
    /// </summary>
    private async Task<PagedResult<WorkOrderListItemDto>> GetPagedBasicAsync(WorkOrderQueryParams query)
    {
        var workOrderQuery = _context.WorkOrders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(query.SalesOrderNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.SalesOrderNo.Contains(query.SalesOrderNo));
        if (!string.IsNullOrEmpty(query.ProductionMainNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.ProductionMainNo.Contains(query.ProductionMainNo));
        if (!string.IsNullOrEmpty(query.ProductionSubNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.ProductionSubNo != null && wo.ProductionSubNo.Contains(query.ProductionSubNo));
        if (query.Status.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.Status == (WorkOrderStatus)query.Status.Value);
        if (!string.IsNullOrEmpty(query.Specification))
            workOrderQuery = workOrderQuery.Where(wo => wo.Specification.Contains(query.Specification));
        if (!string.IsNullOrEmpty(query.PlantGrade))
            workOrderQuery = workOrderQuery.Where(wo => wo.PlantGrade.Contains(query.PlantGrade));
        if (!string.IsNullOrEmpty(query.Salesman))
            workOrderQuery = workOrderQuery.Where(wo => wo.Salesman.Contains(query.Salesman));
        if (!string.IsNullOrEmpty(query.EndCustomer))
            workOrderQuery = workOrderQuery.Where(wo => wo.EndCustomer != null && wo.EndCustomer.Contains(query.EndCustomer));
        if (query.DeliveryDateStart.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.DeliveryDate >= query.DeliveryDateStart.Value);
        if (query.DeliveryDateEnd.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.DeliveryDate <= query.DeliveryDateEnd.Value);
        if (query.SignDateFrom.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.SignDate >= query.SignDateFrom.Value);
        if (query.SignDateTo.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.SignDate < query.SignDateTo.Value.AddDays(1));

        // 关键字模糊搜索：工单号/订单号/业务员/牌号/规格/主号/次号/最终用户
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            workOrderQuery = workOrderQuery.Where(wo =>
                wo.WorkOrderNo.Contains(keyword) ||
                wo.SalesOrderNo.Contains(keyword) ||
                wo.Salesman.Contains(keyword) ||
                wo.PlantGrade.Contains(keyword) ||
                wo.Specification.Contains(keyword) ||
                wo.ProductionMainNo.Contains(keyword) ||
                (wo.ProductionSubNo != null && wo.ProductionSubNo.Contains(keyword)) ||
                (wo.EndCustomer != null && wo.EndCustomer.Contains(keyword))
            );
        }

        // ===== 应用 ExcelFilter 筛选条件（JSON 筛选器） =====
        workOrderQuery = workOrderQuery.ApplyFilters(query.Filters);

        var totalCount = await workOrderQuery.CountAsync();
        workOrderQuery = workOrderQuery.ApplySort(query.SortBy ?? "CreatedTime", query.IsDescending);

        var workOrders = await workOrderQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var items = workOrders.Select(wo => ToListItemDto(wo)).ToList();

        return new PagedResult<WorkOrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    #endregion

    #region 用料计划三级聚合

    /// <summary>
    /// 含用料计划聚合的分页查询（从 WorkOrderListSummary 读模型查询）
    /// </summary>
    private async Task<PagedResult<WorkOrderListDto>> GetPagedEnrichedAsync(WorkOrderQueryParams query)
    {
        var summaryQuery = _context.Set<WorkOrderListSummary>().AsNoTracking().AsQueryable();

        // 只查询 WorkOrders 表中仍存在的工单（避免已物理删除的工单脏数据残留）
        summaryQuery = summaryQuery.Where(s => _context.WorkOrders.Any(wo => wo.Id == s.WorkOrderId));

        if (!string.IsNullOrEmpty(query.SalesOrderNo))
            summaryQuery = summaryQuery.Where(s => s.SalesOrderNo.Contains(query.SalesOrderNo));
        if (!string.IsNullOrEmpty(query.ProductionMainNo))
            summaryQuery = summaryQuery.Where(s => s.ProductionMainNo.Contains(query.ProductionMainNo));
        if (!string.IsNullOrEmpty(query.ProductionSubNo))
            summaryQuery = summaryQuery.Where(s => s.ProductionSubNo != null && s.ProductionSubNo.Contains(query.ProductionSubNo));
        if (query.Status.HasValue)
            summaryQuery = summaryQuery.Where(s => s.Status == query.Status.Value);
        if (!string.IsNullOrEmpty(query.Specification))
            summaryQuery = summaryQuery.Where(s => s.Specification.Contains(query.Specification));
        if (!string.IsNullOrEmpty(query.PlantGrade))
            summaryQuery = summaryQuery.Where(s => s.PlantGrade.Contains(query.PlantGrade));
        if (!string.IsNullOrEmpty(query.Salesman))
            summaryQuery = summaryQuery.Where(s => s.Salesman.Contains(query.Salesman));
        if (!string.IsNullOrEmpty(query.EndCustomer))
            summaryQuery = summaryQuery.Where(s => s.EndCustomer != null && s.EndCustomer.Contains(query.EndCustomer));
        if (query.DeliveryDateStart.HasValue)
            summaryQuery = summaryQuery.Where(s => s.DeliveryDate >= query.DeliveryDateStart.Value);
        if (query.DeliveryDateEnd.HasValue)
            summaryQuery = summaryQuery.Where(s => s.DeliveryDate <= query.DeliveryDateEnd.Value);
        if (query.SignDateFrom.HasValue)
            summaryQuery = summaryQuery.Where(s => s.SignDate >= query.SignDateFrom.Value);
        if (query.SignDateTo.HasValue)
            summaryQuery = summaryQuery.Where(s => s.SignDate < query.SignDateTo.Value.AddDays(1));

        // 关键字模糊搜索：工单号/订单号/主号/次号/业务员/最终用户/牌号/规格
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            summaryQuery = summaryQuery.Where(s =>
                s.WorkOrderNo.Contains(keyword) ||
                s.SalesOrderNo.Contains(keyword) ||
                s.Salesman.Contains(keyword) ||
                s.PlantGrade.Contains(keyword) ||
                s.Specification.Contains(keyword) ||
                s.ProductionMainNo.Contains(keyword) ||
                (s.ProductionSubNo != null && s.ProductionSubNo.Contains(keyword)) ||
                (s.EndCustomer != null && s.EndCustomer.Contains(keyword))
            );
        }

        // ===== 计划类型筛选（前端复选框：未勾选的计划类型需排除） =====
        if (!string.IsNullOrEmpty(query.PlanTypeFilter))
        {
            var selectedTypes = query.PlanTypeFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .ToHashSet();

            // 未勾选的计划类型：对应重量/支数必须为 0（表示该工单不使用此计划类型）
            if (!selectedTypes.Contains("piercing"))
                summaryQuery = summaryQuery.Where(s => (s.PiercingPlanTotalWeight == null || s.PiercingPlanTotalWeight == 0) &&
                                                       (s.PiercingPlanTotalPieces == null || s.PiercingPlanTotalPieces == 0));
            if (!selectedTypes.Contains("semi"))
                summaryQuery = summaryQuery.Where(s => (s.SemiPlanTotalWeight == null || s.SemiPlanTotalWeight == 0) &&
                                                       (s.SemiPlanTotalPieces == null || s.SemiPlanTotalPieces == 0));
            if (!selectedTypes.Contains("finish"))
                summaryQuery = summaryQuery.Where(s => (s.FinishedPlanTotalWeight == null || s.FinishedPlanTotalWeight == 0) &&
                                                       (s.FinishedPlanTotalPieces == null || s.FinishedPlanTotalPieces == 0));
            if (!selectedTypes.Contains("inventory"))
                summaryQuery = summaryQuery.Where(s => (s.InventoryPlanTotalWeight == null || s.InventoryPlanTotalWeight == 0) &&
                                                       (s.InventoryPlanTotalPieces == null || s.InventoryPlanTotalPieces == 0));
            if (!selectedTypes.Contains("rework"))
                summaryQuery = summaryQuery.Where(s => (s.ReworkPlanTotalWeight == null || s.ReworkPlanTotalWeight == 0) &&
                                                       (s.ReworkPlanTotalPieces == null || s.ReworkPlanTotalPieces == 0));
            if (!selectedTypes.Contains("inprocess"))
                summaryQuery = summaryQuery.Where(s => (s.InProcessReworkPlanTotalWeight == null || s.InProcessReworkPlanTotalWeight == 0) &&
                                                       (s.InProcessReworkPlanTotalPieces == null || s.InProcessReworkPlanTotalPieces == 0));
        }

        // ===== 应用 ExcelFilter 筛选条件 =====
        summaryQuery = summaryQuery.ApplyFilters(query.Filters);

        var totalCount = await summaryQuery.CountAsync();

        // ===== 排序 =====
        summaryQuery = summaryQuery.ApplySort(query.SortBy ?? "CreatedTime", query.IsDescending);

        // ===== 分页 + 投影到 DTO =====
        var items = await summaryQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(s => new WorkOrderListDto
            {
                Id = s.WorkOrderId,
                WorkOrderNo = s.WorkOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                ProductionMainNo = s.ProductionMainNo,
                ProductionSubNo = s.ProductionSubNo,
                SignDate = s.SignDate,
                Salesman = s.Salesman,
                EndCustomer = s.EndCustomer,
                DeliveryDate = s.DeliveryDate,
                DelayPenalty = s.DelayPenalty,
                SettlementMethod = Enum.Parse<SettlementMethod>(s.SettlementMethod),
                PlantGrade = s.PlantGrade,
                PipeManufacturingType = Enum.Parse<PipeManufacturingType>(s.MaterialName),
                Specification = s.Specification,
                LengthStatus = Enum.Parse<LengthStatus>(s.LengthStatus),
                MinLength = s.MinLength,
                MaxLength = s.MaxLength,
                TotalQuantity = s.TotalQuantity,
                TotalWeight = s.TotalWeight,
                DeliveryState = Enum.Parse<DeliveryState>(s.DeliveryState),
                TotalItemCount = s.TotalItemCount,
                Status = (WorkOrderStatus)s.Status,
                CreatedTime = s.CreatedTime,
                MaterialPlanStatus = (MaterialPlanStatus)s.MaterialPlanStatus,
                MaterialPlanRate = s.MaterialPlanRate,
                MainNoMaterialPlanStatus = (MaterialPlanStatus)s.MainNoMaterialPlanStatus,
                MainNoMaterialPlanRate = s.MainNoMaterialPlanRate,
                OrderMaterialPlanStatus = (MaterialPlanStatus)s.OrderMaterialPlanStatus,
                LatestPlanDate = s.LatestPlanDate,
                SemiPlanTotalWeight = s.SemiPlanTotalWeight,
                SemiPlanTotalPieces = s.SemiPlanTotalPieces,
                FinishedPlanTotalWeight = s.FinishedPlanTotalWeight,
                FinishedPlanTotalPieces = s.FinishedPlanTotalPieces,
                InventoryPlanTotalWeight = s.InventoryPlanTotalWeight,
                InventoryPlanTotalPieces = s.InventoryPlanTotalPieces,
                ReworkPlanTotalWeight = s.ReworkPlanTotalWeight,
                ReworkPlanTotalPieces = s.ReworkPlanTotalPieces,
                PiercingPlanTotalWeight = s.PiercingPlanTotalWeight,
                PiercingPlanTotalPieces = s.PiercingPlanTotalPieces,
                InProcessReworkPlanTotalWeight = s.InProcessReworkPlanTotalWeight,
                InProcessReworkPlanTotalPieces = s.InProcessReworkPlanTotalPieces,
                MaxStandardCycle = s.MaxStandardCycle,
                MainNoMaxStandardCycle = s.MainNoMaxStandardCycle,
                CapacityWorkDays = s.CapacityWorkDays,
                TheoreticalCutoffDate = s.TheoreticalCutoffDate,
                MaterialPlanCoveredCount = s.MaterialPlanCoveredCount,
                MaterialPlanProportion = s.MaterialPlanProportion,
                LatestRequiredDate = s.LatestRequiredDate
            })
            .ToListAsync();
        return new PagedResult<WorkOrderListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    /// <summary>
    /// 为工单列表补充主号级和订单级聚合状态
    /// </summary>
    private async Task EnrichWithAggregatedStatusAsync(List<WorkOrderListDto> items)
    {
        // 0. 覆盖冗余快照字段（直接从 SalesOrder 快照字段读取）
        await PatchCustomerFieldsAsync(items);

        var orderNos = items.Select(i => i.SalesOrderNo).Distinct().ToList();

        var allWorkOrdersInOrders = await _context.WorkOrders
            .Where(wo => orderNos.Contains(wo.SalesOrderNo))
            .ToListAsync();

        var allWorkOrderIds = allWorkOrdersInOrders.Select(wo => wo.Id).ToList();

        var allSemiPlans = await _context.PurchaseSemiPlans
            .Where(p => allWorkOrderIds.Contains(p.WorkOrderId))
            .ToListAsync();

        var allFinishPlans = await _context.PurchaseFinishedPlans
            .Where(p => allWorkOrderIds.Contains(p.WorkOrderId))
            .ToListAsync();

        var allInventoryPlans = await _context.InventoryPlans
            .Where(p => allWorkOrderIds.Contains(p.WorkOrderId) && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .ToListAsync();

        var allPiercingPlans = await _context.RoundBarPiercingPlans
            .Where(p => allWorkOrderIds.Contains(p.WorkOrderId))
            .ToListAsync();

        var allInProcessReworkPlans = await _context.InProcessReworkPlans
            .Where(p => allWorkOrderIds.Contains(p.WorkOrderId) && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .ToListAsync();

        // 1. 填充各计划类型重量汇总（按工单ID）
        var semiWeightByWo = allSemiPlans
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredWeight));
        var semiPiecesByWo = allSemiPlans
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple));

        var finishWeightByWo = allFinishPlans
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredWeight));
        var finishPiecesByWo = allFinishPlans
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredPiece ?? 0));

        var inventoryWeightByWo = allInventoryPlans
            .Where(p => p.ReworkType == null)
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.UsedWeight));
        var inventoryPiecesByWo = allInventoryPlans
            .Where(p => p.ReworkType == null)
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple));

        var reworkWeightByWo = allInventoryPlans
            .Where(p => p.ReworkType != null)
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.UsedWeight));
        var reworkPiecesByWo = allInventoryPlans
            .Where(p => p.ReworkType != null)
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple));

        var piercingWeightByWo = allPiercingPlans
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredWeight));
        var piercingPiecesByWo = allPiercingPlans
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple));

        var inProcessReworkWeightByWo = allInProcessReworkPlans
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.UsedWeight));
        var inProcessReworkPiecesByWo = allInProcessReworkPlans
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple));

        // 计算最新计划日期（所有计划中最晚的 PlanDate）
        var latestDateByWo = new Dictionary<int, DateTime>();
        void MergeMaxDate(IEnumerable<IGrouping<int, DateTime>> groups)
        {
            foreach (var g in groups)
            {
                var max = g.Max();
                if (latestDateByWo.TryGetValue(g.Key, out var existing))
                {
                    if (max > existing) latestDateByWo[g.Key] = max;
                }
                else
                {
                    latestDateByWo[g.Key] = max;
                }
            }
        }
        MergeMaxDate(allSemiPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));
        MergeMaxDate(allFinishPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));
        MergeMaxDate(allInventoryPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));
        MergeMaxDate(allPiercingPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));
        MergeMaxDate(allInProcessReworkPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));

        foreach (var item in items)
        {
            if (semiWeightByWo.TryGetValue(item.Id, out var semiW)) item.SemiPlanTotalWeight = semiW;
            if (semiPiecesByWo.TryGetValue(item.Id, out var semiP)) item.SemiPlanTotalPieces = semiP;
            if (finishWeightByWo.TryGetValue(item.Id, out var finW)) item.FinishedPlanTotalWeight = finW;
            if (finishPiecesByWo.TryGetValue(item.Id, out var finP)) item.FinishedPlanTotalPieces = finP;
            if (inventoryWeightByWo.TryGetValue(item.Id, out var invW)) item.InventoryPlanTotalWeight = invW;
            if (inventoryPiecesByWo.TryGetValue(item.Id, out var invP)) item.InventoryPlanTotalPieces = invP;
            if (reworkWeightByWo.TryGetValue(item.Id, out var rewW)) item.ReworkPlanTotalWeight = rewW;
            if (reworkPiecesByWo.TryGetValue(item.Id, out var rewP)) item.ReworkPlanTotalPieces = rewP;
            if (piercingWeightByWo.TryGetValue(item.Id, out var pW)) item.PiercingPlanTotalWeight = pW;
            if (piercingPiecesByWo.TryGetValue(item.Id, out var pP)) item.PiercingPlanTotalPieces = pP;
            if (inProcessReworkWeightByWo.TryGetValue(item.Id, out var ipW)) item.InProcessReworkPlanTotalWeight = ipW;
            if (inProcessReworkPiecesByWo.TryGetValue(item.Id, out var ipP)) item.InProcessReworkPlanTotalPieces = ipP;
            if (latestDateByWo.TryGetValue(item.Id, out var latestDate)) item.LatestPlanDate = latestDate;
        }

        // 1b. 从用料计划数据实时计算工单级满足率（不依赖 WorkOrder 预计算字段）
        var semiByWo = allSemiPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var finishByWo = allFinishPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var inventoryByWo = allInventoryPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var piercingByWo = allPiercingPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var inProcessReworkByWo = allInProcessReworkPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var woById = allWorkOrdersInOrders.ToDictionary(wo => wo.Id);

        // 读取物料计划状态阈值配置（用在工单级和主号级计算）
        var fixedFinishRatio = await GetConfigAsync("MaterialPlanRatio", "FixedFinishRatio", 1.02m);
        var fixedInventoryRatio = await GetConfigAsync("MaterialPlanRatio", "FixedInventoryRatio", 1.02m);
        var nonFixedFinishRatio = await GetConfigAsync("MaterialPlanRatio", "NonFixedFinishRatio", 1.05m);
        var nonFixedInventoryRatio = await GetConfigAsync("MaterialPlanRatio", "NonFixedInventoryRatio", 1.05m);
        var fixedPartial = await GetConfigAsync("MaterialPlanStatus", "FixedPartial", 102m);
        var fixedSatisfied = await GetConfigAsync("MaterialPlanStatus", "FixedSatisfied", 110m);
        var nonFixedPartial = await GetConfigAsync("MaterialPlanStatus", "NonFixedPartial", 105m);
        var nonFixedSatisfied = await GetConfigAsync("MaterialPlanStatus", "NonFixedSatisfied", 120m);
        var smallBatchMaxQty = await GetConfigAsync("MaterialPlanStatus", "SmallBatchMaxQty", 20m);
        var smallBatchSatisfiedRate = await GetConfigAsync("MaterialPlanStatus", "SmallBatchSatisfiedRate", 100m);

        foreach (var item in items)
        {
            if (!woById.TryGetValue(item.Id, out var wo)) continue;
            var semi = semiByWo.TryGetValue(item.Id, out var s) ? s : new List<PurchaseSemiPlan>();
            var finish = finishByWo.TryGetValue(item.Id, out var f) ? f : new List<PurchaseFinishedPlan>();
            var inv = inventoryByWo.TryGetValue(item.Id, out var iv) ? iv : new List<InventoryPlan>();
            var pierce = piercingByWo.TryGetValue(item.Id, out var p) ? p : new List<RoundBarPiercingPlan>();
            var inProcess = inProcessReworkByWo.TryGetValue(item.Id, out var ip) ? ip : new List<InProcessReworkPlan>();
            var (rate, status) = PlanRateCalculator.ComputeWorkOrderRate(wo, semi, finish, inv, pierce, inProcess,
                fixedPartial, fixedSatisfied, nonFixedPartial, nonFixedSatisfied);
            item.MaterialPlanRate = rate;
            item.MaterialPlanStatus = (MaterialPlanStatus)status;
        }

        // 2. 主号级聚合

        var mainNoKeys = items
            .Select(i => new { i.SalesOrderNo, MainNo = i.ProductionMainNo })
            .Distinct()
            .ToList();

        foreach (var key in mainNoKeys)
        {
            var groupWorkOrders = allWorkOrdersInOrders
                .Where(wo => wo.SalesOrderNo == key.SalesOrderNo && wo.ProductionMainNo == key.MainNo)
                .ToList();

            var groupIds = groupWorkOrders.Select(wo => wo.Id).ToHashSet();
            var groupSemiPlans = allSemiPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList();
            var groupFinishPlans = allFinishPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList();
            var groupInventoryAll = allInventoryPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList();
            var groupInventoryPlans = groupInventoryAll.Where(p => p.ReworkType == null).ToList();
            var groupReworkPlans = groupInventoryAll.Where(p => p.ReworkType != null).ToList();
            var groupPiercingPlans = allPiercingPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList();
            var groupInProcessRework = allInProcessReworkPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList();

            var (rate, status) = CalculateMainNoAggregation(groupWorkOrders, groupSemiPlans, groupFinishPlans, groupInventoryPlans, groupReworkPlans, groupPiercingPlans, groupInProcessRework,
                fixedFinishRatio, fixedInventoryRatio, nonFixedFinishRatio, nonFixedInventoryRatio,
                smallBatchMaxQty, smallBatchSatisfiedRate, fixedPartial, fixedSatisfied, nonFixedPartial, nonFixedSatisfied);

            foreach (var item in items.Where(i =>
                i.SalesOrderNo == key.SalesOrderNo && i.ProductionMainNo == key.MainNo))
            {
                item.MainNoMaterialPlanRate = rate;
                item.MainNoMaterialPlanStatus = status;
            }
        }

        // 2. 订单级聚合：只要该订单下所有主号都没有"部分"和"未计划"，即为全部满足
        foreach (var orderNo in orderNos)
        {
            var orderItems = items.Where(i => i.SalesOrderNo == orderNo).ToList();
            var hasPartialOrNotPlanned = orderItems.Any(i =>
                i.MainNoMaterialPlanStatus == MaterialPlanStatus.Partial ||
                i.MainNoMaterialPlanStatus == MaterialPlanStatus.NotPlanned);
            var allNotPlanned = orderItems.All(i =>
                i.MainNoMaterialPlanStatus == MaterialPlanStatus.NotPlanned);

            MaterialPlanStatus orderStatus;
            if (allNotPlanned)
                orderStatus = MaterialPlanStatus.NotPlanned;
            else if (hasPartialOrNotPlanned)
                orderStatus = MaterialPlanStatus.Partial;
            else
                orderStatus = MaterialPlanStatus.Satisfied;

            foreach (var item in orderItems)
                item.OrderMaterialPlanStatus = orderStatus;
        }
    }

    /// <summary>
    /// 计算主号级聚合（使用原始标准，不含"理论满足"）
    /// </summary>
    private (decimal rate, MaterialPlanStatus status) CalculateMainNoAggregation(
        List<WoEntity> workOrders,
        List<PurchaseSemiPlan> semiPlans,
        List<PurchaseFinishedPlan> finishPlans,
        List<InventoryPlan> inventoryPlans,
        List<InventoryPlan> reworkPlans,
        List<RoundBarPiercingPlan> piercingPlans,
        List<InProcessReworkPlan> inProcessReworkPlans,
        decimal fixedFinishRatio, decimal fixedInventoryRatio,
        decimal nonFixedFinishRatio, decimal nonFixedInventoryRatio,
        decimal smallBatchMaxQty, decimal smallBatchSatisfiedRate,
        decimal fixedPartial, decimal fixedSatisfied,
        decimal nonFixedPartial, decimal nonFixedSatisfied)
    {
        var fixedOrders = workOrders.Where(wo => wo.LengthStatus == LengthStatus.Fixed).ToList();
        var nonFixedOrders = workOrders.Where(wo => wo.LengthStatus != LengthStatus.Fixed).ToList();

        decimal totalDemand = 0;
        decimal totalEffective = 0;

        // 定尺：按支数
        if (fixedOrders.Any())
        {
            var fixedIds = fixedOrders.Select(wo => wo.Id).ToHashSet();
            totalDemand += fixedOrders.Sum(wo => wo.TotalQuantity);

            var fixedSemi = semiPlans.Where(p => fixedIds.Contains(p.WorkOrderId)).ToList();
            var fixedFinish = finishPlans.Where(p => fixedIds.Contains(p.WorkOrderId)).ToList();
            var fixedInventory = inventoryPlans.Where(p => fixedIds.Contains(p.WorkOrderId)).ToList();
            var fixedRework = reworkPlans.Where(p => fixedIds.Contains(p.WorkOrderId)).ToList();
            var fixedPiercing = piercingPlans.Where(p => fixedIds.Contains(p.WorkOrderId)).ToList();

            // 原料采购：原料支数 × 投料倍率，不乘系数
            totalEffective += (int)fixedSemi.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple);
            // 圆棒穿孔：原料支数 × 投料倍率，不乘系数（同原料采购）
            totalEffective += (int)fixedPiercing.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple);
            // 成品采购：× fixedFinishRatio
            totalEffective += fixedFinish.Sum(p => p.RequiredPiece ?? 0) * fixedFinishRatio;
            // 库存使用：× fixedInventoryRatio
            totalEffective += (int)(fixedInventory.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple) * fixedInventoryRatio);
            // 库料改制：不乘系数
            totalEffective += (int)fixedRework.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple);

            var fixedInProcess = inProcessReworkPlans.Where(p => fixedIds.Contains(p.WorkOrderId)).ToList();
            totalEffective += (int)fixedInProcess.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple);
        }

        // 范围尺/非定尺：按重量
        if (nonFixedOrders.Any())
        {
            var nonFixedIds = nonFixedOrders.Select(wo => wo.Id).ToHashSet();
            totalDemand += nonFixedOrders.Sum(wo => wo.TotalWeight);

            var nonFixedSemi = semiPlans.Where(p => nonFixedIds.Contains(p.WorkOrderId)).ToList();
            var nonFixedFinish = finishPlans.Where(p => nonFixedIds.Contains(p.WorkOrderId)).ToList();
            var nonFixedInventory = inventoryPlans.Where(p => nonFixedIds.Contains(p.WorkOrderId)).ToList();
            var nonFixedRework = reworkPlans.Where(p => nonFixedIds.Contains(p.WorkOrderId)).ToList();
            var nonFixedPiercing = piercingPlans.Where(p => nonFixedIds.Contains(p.WorkOrderId)).ToList();

            totalEffective += nonFixedSemi.Sum(p => p.RequiredWeight);
            // 成品采购：× nonFixedFinishRatio
            totalEffective += nonFixedFinish.Sum(p => p.RequiredWeight) * nonFixedFinishRatio;
            // 库存使用：× nonFixedInventoryRatio
            totalEffective += nonFixedInventory.Sum(p => p.UsedWeight) * nonFixedInventoryRatio;
            // 库料改制：不乘系数
            totalEffective += nonFixedRework.Sum(p => p.UsedWeight);
            // 圆棒穿孔：不乘系数（同原料采购）
            totalEffective += nonFixedPiercing.Sum(p => p.RequiredWeight);

            var nonFixedInProcess = inProcessReworkPlans.Where(p => nonFixedIds.Contains(p.WorkOrderId)).ToList();
            totalEffective += nonFixedInProcess.Sum(p => p.UsedWeight);
        }

        if (totalDemand <= 0) return (0, MaterialPlanStatus.NotPlanned);

        var rate = totalEffective / totalDemand * 100m;

        // 无任何投料 → 未计划
        if (rate <= 0) return (0, MaterialPlanStatus.NotPlanned);

        // 小批量特殊处理：定尺总支数 ≤ smallBatchMaxQty 时，≥ smallBatchSatisfiedRate 即视为满足
        var fixedTotalQuantity = fixedOrders.Sum(wo => wo.TotalQuantity);
        if (fixedTotalQuantity > 0 && fixedTotalQuantity <= smallBatchMaxQty)
        {
            var batchStatus = rate >= smallBatchSatisfiedRate ? MaterialPlanStatus.Satisfied : MaterialPlanStatus.Partial;
            return (rate, batchStatus);
        }

        // 使用原始标准（不含理论满足）
        var status = CalculateMainNoStatus(rate, fixedOrders.Any(), fixedPartial, fixedSatisfied, nonFixedPartial, nonFixedSatisfied);
        return (rate, status);
    }

    /// <summary>
    /// 主号级状态判定（原标准，无"理论满足"）
    /// </summary>
    private static MaterialPlanStatus CalculateMainNoStatus(decimal rate, bool isFixed,
        decimal fixedPartial, decimal fixedSatisfied,
        decimal nonFixedPartial, decimal nonFixedSatisfied)
    {
        if (rate <= 0) return MaterialPlanStatus.NotPlanned;

        if (isFixed)
        {
            if (rate < fixedPartial) return MaterialPlanStatus.Partial;
            if (rate <= fixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            if (rate < nonFixedPartial) return MaterialPlanStatus.Partial;
            if (rate <= nonFixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
    }

    #endregion

    public async Task<WorkOrderDetailDto> GetByIdAsync(int id)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == id);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var dto = ToDetailDto(workOrder);

        // 覆盖冗余快照字段：从 SalesOrder 快照字段读取
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.OrderNumber == workOrder.SalesOrderNo);
        if (salesOrder != null)
        {
            dto.Salesman = salesOrder.Salesman;
            dto.EndCustomer = salesOrder.EndCustomer;
        }

        return dto;
    }

    public async Task<WorkOrderDetailDto> GetByWorkOrderNoAsync(string workOrderNo)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.WorkOrderNo == workOrderNo);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var dto = ToDetailDto(workOrder);

        // 覆盖冗余快照字段：从 SalesOrder 快照字段读取
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.OrderNumber == workOrder.SalesOrderNo);
        if (salesOrder != null)
        {
            dto.Salesman = salesOrder.Salesman;
            dto.EndCustomer = salesOrder.EndCustomer;
        }

        return dto;
    }

    public async Task<List<WorkOrderListItemDto>> GetBySalesOrderNoAsync(string salesOrderNo)
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.SalesOrderNo == salesOrderNo)
            .OrderBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToListAsync();

        return workOrders.Select(wo => ToListItemDto(wo)).ToList();
    }

    /// <summary>
    /// 从 SalesOrder 快照字段读取 Salesman/EndCustomer，覆盖 WorkOrder 冗余快照
    /// </summary>
    private async Task PatchCustomerFieldsAsync(WorkOrderDetailDto dto, string salesOrderNo)
    {
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.OrderNumber == salesOrderNo);
        if (salesOrder != null)
        {
            dto.Salesman = salesOrder.Salesman;
            dto.EndCustomer = salesOrder.EndCustomer;
        }
    }

    /// <summary>
    /// 批量从 SalesOrder 快照字段覆盖 WorkOrderListDto 的冗余快照字段
    /// </summary>
    private async Task PatchCustomerFieldsAsync(List<WorkOrderListDto> items)
    {
        var orderNos = items.Select(i => i.SalesOrderNo).Distinct().ToList();
        if (orderNos.Count == 0) return;

        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => orderNos.Contains(so.OrderNumber))
            .ToListAsync();

        var salesOrderByNo = salesOrders.ToDictionary(so => so.OrderNumber, so => so, StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (salesOrderByNo.TryGetValue(item.SalesOrderNo, out var so))
            {
                item.Salesman = so.Salesman;
                item.EndCustomer = so.EndCustomer;
            }
        }
    }

    public async Task<UpdateWorkOrderStatusResponseDto> UpdateStatusAsync(int id, UpdateWorkOrderStatusRequest request)
    {
        var workOrder = await _context.WorkOrders
            .FirstOrDefaultAsync(wo => wo.Id == id);
        if (workOrder == null)
            throw new BusinessException("工单不存在");
        if (!CanTransitionTo(workOrder.Status, request.Status))
            throw new BusinessException($"不允许从 {GetStatusText(workOrder.Status)} 变更为 {GetStatusText(request.Status)}");

        workOrder.Status = request.Status;
        _context.Entry(workOrder).Property(x => x.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("工单已被其他用户修改，请刷新后重试");
        }

        // 增量更新读模型 Status（只改一个字段，无需全量刷新）
        var summaryRow = await _context.Set<WorkOrderListSummary>()
            .FirstOrDefaultAsync(s => s.WorkOrderId == id);
        if (summaryRow != null)
        {
            summaryRow.Status = (int)request.Status;
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("更新工单状态成功: 工单号 {WorkOrderNo}, 新状态 {Status}",
            workOrder.WorkOrderNo, request.Status);

        await TryRefreshExecutionSummaryAsync(workOrder.WorkOrderNo);

        return new UpdateWorkOrderStatusResponseDto { Id = workOrder.Id, Status = workOrder.Status };
    }

    public async Task DeleteAsync(int id)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder == null)
        {
            _logger.LogWarning("工单 {Id} 不存在（可能已被删除）", id);
            return;
        }

        // 级联删除关联的用料计划（无FK约束，需手动清理）
        var semiPlans = await _context.PurchaseSemiPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var finishPlans = await _context.PurchaseFinishedPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var invPlans = await _context.InventoryPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var piercingPlans = await _context.RoundBarPiercingPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var inProcessReworkPlans = await _context.InProcessReworkPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        if (semiPlans.Any()) _context.PurchaseSemiPlans.RemoveRange(semiPlans);
        if (finishPlans.Any()) _context.PurchaseFinishedPlans.RemoveRange(finishPlans);
        if (invPlans.Any()) _context.InventoryPlans.RemoveRange(invPlans);
        if (piercingPlans.Any()) _context.RoundBarPiercingPlans.RemoveRange(piercingPlans);
        if (inProcessReworkPlans.Any()) _context.InProcessReworkPlans.RemoveRange(inProcessReworkPlans);

        // 直接清理该工单的读模型行（双重保险，即使后续完整刷新失败也不会留下脏数据）
        var summaryRow = await _context.Set<WorkOrderListSummary>()
            .FirstOrDefaultAsync(s => s.WorkOrderId == id);
        if (summaryRow != null)
            _context.Set<WorkOrderListSummary>().Remove(summaryRow);
        var execSummaryRow = await _context.Set<WorkOrderExecutionSummary>()
            .FirstOrDefaultAsync(s => s.WorkOrderId == id);
        if (execSummaryRow != null)
            _context.Set<WorkOrderExecutionSummary>().Remove(execSummaryRow);

        // 扫描引用该工单号的入库批次，生成通知（已执行数据，不级联）
        var affectedBatches = await _context.InventoryBatches
            .Where(b => b.WorkOrderNo == workOrder.WorkOrderNo)
            .ToListAsync();
        var now = DateTimeOffset.Now;
        foreach (var batch in affectedBatches)
        {
            _context.Notifications.Add(new MES.Data.Entities.WorkOrder.Notification
            {
                NotificationType = "WorkOrderDeleted",
                TargetId = batch.Id,
                Title = $"工单 {workOrder.WorkOrderNo} 已删除",
                Content = $"入库批次 {batch.BatchNo}（{batch.MaterialType} {batch.Specification}）仍引用该工单，请及时处理",
                IsRead = false,
                Receiver = string.Empty,
                CreatedTime = now
            });
        }

        // 先标记剩余工单为待修正（在删除之前做，避免中间状态异常导致脏数据）
        var remainingWorkOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == workOrder.SalesOrderNo && wo.Status == WorkOrderStatus.Confirmed)
            .ToListAsync();
        foreach (var wo in remainingWorkOrders)
        {
            wo.Status = WorkOrderStatus.Pending;
        }

        // 删除工单
        _context.WorkOrders.Remove(workOrder);
        await _context.SaveChangesAsync();

        _logger.LogInformation("删除工单成功: 工单号 {WorkOrderNo}, 关联订单 {OrderNo} 剩余 {Count} 个工单已标记为待修正",
            workOrder.WorkOrderNo, workOrder.SalesOrderNo, remainingWorkOrders.Count);

        // 刷新读模型（放在 finally 中确保即使标记待修正失败也能执行，或放在 SaveChanges 后无条件执行）
        try
        {
            if (_listSummaryService != null) await _listSummaryService.RefreshBySalesOrderAsync(workOrder.SalesOrderNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读模型刷新失败: SalesOrderNo={SalesOrderNo}", workOrder.SalesOrderNo);
        }

        // 增量刷新工单执行状况（更新同订单内剩余工单的聚合值，删除当前工单的行由 ReadModelService 内处理）
        var remainingWoNos = remainingWorkOrders.Select(w => w.WorkOrderNo).ToList();
        foreach (var woNo in remainingWoNos) await TryRefreshExecutionSummaryAsync(woNo);
    }

    public async Task SoftDeleteAsync(int id)
    {
        // 工单使用物理删除，SoftDeleteAsync 直接调用 DeleteAsync
        await DeleteAsync(id);
    }

    #endregion

    #region 订单变更检测

    public async Task CheckAndUpdateWorkOrderStatusAsync(int salesOrderId)
    {
        var salesOrder = await _context.SalesOrders.AsNoTracking()
            .FirstOrDefaultAsync(so => so.Id == salesOrderId);
        await CheckAndUpdateWorkOrderStatusInternalAsync(salesOrderId);
        await _context.SaveChangesAsync();

        // 刷新读模型
        if (salesOrder != null)
        {
            if (_listSummaryService != null) await _listSummaryService.RefreshBySalesOrderAsync(salesOrder.OrderNumber);
            var woNos = await _context.WorkOrders
                .Where(wo => wo.SalesOrderNo == salesOrder.OrderNumber)
                .Select(wo => wo.WorkOrderNo)
                .ToListAsync();
            foreach (var woNo in woNos) await TryRefreshExecutionSummaryAsync(woNo);
        }
    }

    public async Task CheckAllOrdersChangeAsync()
    {
        _logger.LogInformation("开始执行订单变更检测定时任务");
        var confirmedOrders = await _context.SalesOrders
            .Where(so => so.Status == SalesOrderStatus.Confirmed)
            .Select(so => new { so.Id, so.OrderNumber, so.LastItemChangeTime })
            .ToListAsync();

        int updatedCount = 0;
        foreach (var order in confirmedOrders)
        {
            if (await CheckAndUpdateWorkOrderStatusInternalAsync(order.Id))
                updatedCount++;
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("订单变更检测完成，共更新 {Count} 个订单的工单状态", updatedCount);
    }

    public async Task RefreshMaterialPlanReadModelAsync()
    {
        _logger.LogInformation("开始全量刷新用料计划读模型");

        // 获取所有有工单的订单号（去重）
        var salesOrderNos = await _context.WorkOrders
            .Select(wo => wo.SalesOrderNo)
            .Distinct()
            .ToListAsync();

        int successCount = 0;
        foreach (var salesOrderNo in salesOrderNos)
        {
            try
            {
                if (_listSummaryService != null)
                    await _listSummaryService.RefreshBySalesOrderAsync(salesOrderNo);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "全量刷新时订单 {SalesOrderNo} 刷新失败", salesOrderNo);
            }
        }

        _logger.LogInformation("全量刷新完成: 共 {Total} 个订单, 成功 {Success} 个",
            salesOrderNos.Count, successCount);

        // 清理已物理删除工单的脏数据
        var staleRows = await _context.Set<WorkOrderListSummary>()
            .Where(s => !_context.WorkOrders.Any(wo => wo.Id == s.WorkOrderId))
            .ToListAsync();

        if (staleRows.Count > 0)
        {
            _context.Set<WorkOrderListSummary>().RemoveRange(staleRows);
            await _context.SaveChangesAsync();
            _logger.LogInformation("清理脏数据 {Count} 行", staleRows.Count);
        }
    }

    private async Task<bool> CheckAndUpdateWorkOrderStatusInternalAsync(int salesOrderId)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == salesOrderId);
        if (salesOrder == null || salesOrder.Status != SalesOrderStatus.Confirmed)
            return false;

        var workOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == salesOrder.OrderNumber)
            .ToListAsync();

        if (!workOrders.Any())
            return false;

        bool anyUpdated = false;
        foreach (var workOrder in workOrders)
        {
            if (workOrder.Status != WorkOrderStatus.Confirmed)
                continue;

            // 使用 Max(CreatedTime, UpdatedTime) 判断：若工单被"更新修改"过，UpdatedTime 会更新到最新
            // 避免更新修改后的工单再次被标记为"待修正"
            var lastSyncTime = workOrder.UpdatedTime > workOrder.CreatedTime
                ? workOrder.UpdatedTime
                : workOrder.CreatedTime;
            if (salesOrder.LastItemChangeTime.HasValue && salesOrder.LastItemChangeTime > lastSyncTime)
            {
                workOrder.Status = WorkOrderStatus.Pending;
                anyUpdated = true;
                _logger.LogInformation("工单 {WorkOrderNo}({Id}) 检测到项次变更，状态已更新为待修正",
                    workOrder.WorkOrderNo, workOrder.Id);
            }
        }

        if (anyUpdated)
        {
            await _context.SaveChangesAsync();

            // 工单状态变更后刷新用料计划总览读模型
            if (_listSummaryService != null)
                await _listSummaryService.RefreshBySalesOrderAsync(salesOrder.OrderNumber);

            // 增量刷新工单执行状况
            foreach (var wo in workOrders)
                await TryRefreshExecutionSummaryAsync(wo.WorkOrderNo);

            return true;
        }
        return false;
    }

    #endregion

    #region 订单工单项次追溯

    public async Task<OrderWorkOrderRelationDto> GetOrderWorkOrderRelationAsync(string salesOrderNo)
    {
        // 1. 获取订单信息
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.OrderNumber == salesOrderNo);

        if (salesOrder == null)
            throw new BusinessException($"订单 {salesOrderNo} 不存在");

        // 2. 获取该订单下的所有工单（状态不为已取消的工单）
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.SalesOrderNo == salesOrderNo)
            .OrderBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToListAsync();

        // 3. 收集所有工单包含的项次ID
        var allOrderItemIds = new List<int>();
        foreach (var wo in workOrders)
        {
            var ids = wo.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(id => int.TryParse(id, out var parsed) ? parsed : -1)
                         .Where(id => id > 0);
            allOrderItemIds.AddRange(ids);
        }

        // 4. 批量查询订单项次（一次性加载所有相关项次，按销售订单过滤避免不同订单Sequence冲突）
        var orderItems = await _context.OrderItems
            .Where(oi => oi.SalesOrderId == salesOrder.Id && allOrderItemIds.Contains(oi.Sequence))
            .ToDictionaryAsync(oi => oi.Sequence, oi => oi);

        // 5. 构建 DTO
        var result = new OrderWorkOrderRelationDto
        {
            SalesOrderId = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            Salesman = salesOrder.Salesman,
            CustomerName = salesOrder.CustomerName,
            EndCustomer = salesOrder.EndCustomer,
            WorkOrders = new List<WorkOrderRelationDto>()
        };

        foreach (var wo in workOrders)
        {
            var itemIds = wo.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(id => int.TryParse(id, out var parsed) ? parsed : -1)
                             .Where(id => id > 0)
                             .ToList();

            var workOrderItems = new List<OrderItemBriefDto>();
            foreach (var itemId in itemIds)
            {
                if (orderItems.TryGetValue(itemId, out var item))
                {
                    workOrderItems.Add(new OrderItemBriefDto
                    {
                        Sequence = item.Sequence,
                        StandardGrade = item.StandardGrade,
                        Specification = item.Specification,
                        LengthStatus = item.LengthStatus,
                        MinLength = item.MinLength,
                        MaxLength = item.MaxLength,
                        Quantity = item.Quantity,
                        Meters = item.Meters,
                        ContractWeight = item.ContractWeight,
                        TheoreticalWeight = item.TheoreticalWeight
                    });
                }
            }

            result.WorkOrders.Add(new WorkOrderRelationDto
            {
                WorkOrderId = wo.Id,
                WorkOrderNo = wo.WorkOrderNo,
                ProductionMainNo = wo.ProductionMainNo,
                ProductionSubNo = wo.ProductionSubNo,
                Status = wo.Status,
                StatusText = GetStatusText(wo.Status),
                MaterialName = wo.PipeManufacturingType.ToString(),
                StandardGrade = workOrderItems.FirstOrDefault()?.StandardGrade ?? "",
                PlantGrade = wo.PlantGrade,
                Specification = wo.Specification,
                OuterDiameterNegative = wo.OuterDiameterNegative,
                OuterDiameterPositive = wo.OuterDiameterPositive,
                WallThicknessNegative = wo.WallThicknessNegative,
                WallThicknessPositive = wo.WallThicknessPositive,
                DeliveryState = wo.DeliveryState,
                LengthStatus = wo.LengthStatus,
                DeliveryDate = wo.DeliveryDate,
                TotalQuantity = wo.TotalQuantity,
                TotalWeight = wo.TotalWeight,
                OrderItems = workOrderItems
            });
        }

        return result;
    }

    public async Task<byte[]> PrintWorkOrderAsync(int id)
    {
        var entity = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == id)
            ?? throw new BusinessException("工单不存在");

        return WorkOrderPrintHelper.GeneratePdf(entity);
    }

    public async Task<byte[]> PrintWorkOrdersByOrderAsync(string salesOrderNo)
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.SalesOrderNo == salesOrderNo
                     )
            .OrderBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToListAsync();

        if (workOrders.Count == 0)
            throw new BusinessException($"订单 {salesOrderNo} 下没有可打印的工单");

        return WorkOrderPrintHelper.GenerateBatchPdf(salesOrderNo, workOrders);
    }

    public async Task<byte[]> PrintWorkOrdersByOrderBatchAsync(string[] salesOrderNos)
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => salesOrderNos.Contains(wo.SalesOrderNo)
                     )
            .OrderBy(wo => wo.SalesOrderNo)
            .ThenBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToListAsync();

        if (workOrders.Count == 0)
            throw new BusinessException("所选订单下没有可打印的工单");

        return WorkOrderPrintHelper.GenerateMultiBatchPdf(workOrders);
    }

    public async Task<byte[]> PrintWorkOrdersByOrderAllAsync(WorkOrderQueryParams query)
    {
        // 复用首页筛选逻辑：获取所有已确认订单
        var orderQuery = _context.SalesOrders
            .Where(so => so.Status == SalesOrderStatus.Confirmed);

        if (!string.IsNullOrEmpty(query.Salesman))
            orderQuery = orderQuery.Where(x => x.Salesman.Contains(query.Salesman));

        if (!string.IsNullOrEmpty(query.EndCustomer))
            orderQuery = orderQuery.Where(x => x.EndCustomer != null && x.EndCustomer.Contains(query.EndCustomer));

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            orderQuery = orderQuery.Where(x =>
                x.OrderNumber.Contains(keyword) ||
                x.CustomerName.Contains(keyword) ||
                x.Salesman.Contains(keyword) ||
                (x.EndCustomer != null && x.EndCustomer.Contains(keyword)));
        }

        var allOrders = await orderQuery.ToListAsync();
        var allOrderNumbers = allOrders.Select(x => x.OrderNumber).ToList();

        // 获取关联的所有工单
        var allWorkOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => allOrderNumbers.Contains(wo.SalesOrderNo))
            .ToListAsync();

        // 计算每个订单的工单状态并筛选
        var matchedOrderNumbers = new List<string>();
        foreach (var order in allOrders)
        {
            var orderWorkOrders = allWorkOrders.Where(wo => wo.SalesOrderNo == order.OrderNumber).ToList();

            string workOrderStatus;
            if (!orderWorkOrders.Any())
            {
                workOrderStatus = WorkOrderStatus.NotGenerated.ToString();
            }
            else
            {
                workOrderStatus = orderWorkOrders.Any(wo => wo.Status == WorkOrderStatus.Pending)
                    ? WorkOrderStatus.Pending.ToString()
                    : WorkOrderStatus.Confirmed.ToString();
            }

            if (string.IsNullOrEmpty(query.WorkOrderStatus) || workOrderStatus == query.WorkOrderStatus)
            {
                matchedOrderNumbers.Add(order.OrderNumber);
            }
        }

        // 加载匹配订单的所有工单
        var resultWorkOrders = allWorkOrders
            .Where(wo => matchedOrderNumbers.Contains(wo.SalesOrderNo))
            .OrderBy(wo => wo.SalesOrderNo)
            .ThenBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToList();

        // 签订日期筛选
        if (query.SignDateFrom.HasValue)
            resultWorkOrders = resultWorkOrders.Where(wo => wo.SignDate >= query.SignDateFrom.Value).ToList();
        if (query.SignDateTo.HasValue)
            resultWorkOrders = resultWorkOrders.Where(wo => wo.SignDate < query.SignDateTo.Value.AddDays(1)).ToList();

        if (resultWorkOrders.Count == 0)
            throw new BusinessException("没有可打印的工单");

        return WorkOrderPrintHelper.GenerateMultiBatchPdf(resultWorkOrders);
    }

    #endregion

    #region 辅助方法

    private static bool CanTransitionTo(WorkOrderStatus currentStatus, WorkOrderStatus targetStatus)
    {
        if (currentStatus == targetStatus) return true;
        if (currentStatus == WorkOrderStatus.NotGenerated) return targetStatus == WorkOrderStatus.Confirmed;
        if (currentStatus == WorkOrderStatus.Confirmed) return targetStatus == WorkOrderStatus.Pending;
        if (currentStatus == WorkOrderStatus.Pending) return targetStatus == WorkOrderStatus.Confirmed;
        return false;
    }

    private static string GetStatusText(WorkOrderStatus status)
    {
        return status switch
        {
            WorkOrderStatus.NotGenerated => "未编制",
            WorkOrderStatus.Confirmed => "已确定",
            WorkOrderStatus.Pending => "待修正",
            _ => "未知"
        };
    }

    private static string GetLengthStatusText(LengthStatus status)
    {
        return status switch
        {
            LengthStatus.Fixed => "定尺",
            LengthStatus.Range => "范围尺",
            LengthStatus.NonFixed => "非定尺",
            _ => status.ToString()
        };
    }

    private IQueryable<WoEntity> ApplyWorkOrderComputedFilters(IQueryable<WoEntity> queryable, List<FilterDescriptor>? filters)
    {
        if (filters == null || filters.Count == 0)
            return queryable;

        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field))
                continue;

            switch (filter.Field.ToLower())
            {
                case "latestplandate":
                    if (DateTime.TryParse(filter.From?.ToString(), out var lpFrom))
                        queryable = queryable.Where(wo =>
                            _context.PurchaseSemiPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate)
                                .Concat(_context.PurchaseFinishedPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Concat(_context.InventoryPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Concat(_context.RoundBarPiercingPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Max() >= lpFrom);
                    if (DateTime.TryParse(filter.To?.ToString(), out var lpTo))
                        queryable = queryable.Where(wo =>
                            _context.PurchaseSemiPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate)
                                .Concat(_context.PurchaseFinishedPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Concat(_context.InventoryPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Concat(_context.RoundBarPiercingPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Max() <= lpTo);
                    break;
            }
        }
        return queryable;
    }

    #endregion

    #region 数据回填

    public async Task<ApiResponse<BackfillResultDto>> BackfillOrderItemIdsAsync()
    {
        var result = new BackfillResultDto();

        try
        {
            // 1. 找出所有 OrderItemIds 为空的工单
            var workOrders = await _context.WorkOrders
                .Where(wo => wo.OrderItemIds == null || wo.OrderItemIds == "")
                .ToListAsync();

            result.TotalProcessed = workOrders.Count;
            if (workOrders.Count == 0)
            {
                return ApiResponse<BackfillResultDto>.Ok(result, "所有工单的 OrderItemIds 已填充，无需回填");
            }

            // 2. 按订单号分组，批量加载对应的 OrderItems
            var orderNos = workOrders
                .Select(wo => wo.SalesOrderNo)
                .Distinct()
                .ToList();

            // 按 SalesOrderNo 分组加载 OrderItems 减少查询次数
            var orderItemsByOrder = new Dictionary<string, List<MES.Data.Entities.Order.OrderItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (var orderNo in orderNos)
            {
                var items = await _context.OrderItems
                    .Where(oi => oi.OrderNumber == orderNo)
                    .ToListAsync();
                orderItemsByOrder[orderNo] = items;
            }

            // 3. 逐工单匹配 OrderItem.Sequence
            foreach (var wo in workOrders)
            {
                if (!orderItemsByOrder.TryGetValue(wo.SalesOrderNo, out var orderItems) || orderItems.Count == 0)
                {
                    result.UnmatchedCount++;
                    result.Errors.Add($"工单 {wo.WorkOrderNo}: 订单 {wo.SalesOrderNo} 下无项次");
                    continue;
                }

                // 用关键字段匹配：标准号、交货状态、牌号、规格、长度状态
                var matchedItems = orderItems.Where(oi =>
                    (oi.StandardNo == wo.StandardCode || (oi.StandardNo == null && wo.StandardCode == null)) &&
                    oi.DeliveryState == wo.DeliveryState &&
                    oi.PlantGrade == wo.PlantGrade &&
                    oi.Specification == wo.Specification &&
                    oi.LengthStatus == wo.LengthStatus
                ).ToList();

                if (matchedItems.Count == 0)
                {
                    result.UnmatchedCount++;
                    result.Errors.Add($"工单 {wo.WorkOrderNo}: 订单 {wo.SalesOrderNo} 下未匹配到项次");
                    continue;
                }

                // 按顺序去重收集 Sequence
                var sequences = matchedItems
                    .Select(oi => oi.Sequence)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();

                wo.OrderItemIds = string.Join(",", sequences);
                result.SuccessCount++;
            }

            // 4. 批量保存
            await _context.SaveChangesAsync();

            var msg = $"回填完成：共处理 {result.TotalProcessed} 条，成功 {result.SuccessCount} 条，未匹配 {result.UnmatchedCount} 条";
            _logger.LogInformation(msg);

            return ApiResponse<BackfillResultDto>.Ok(result, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "回填 OrderItemIds 失败");
            result.Errors.Add($"系统异常: {ex.Message}");
            return ApiResponse<BackfillResultDto>.Fail("回填失败: " + ex.Message);
        }
    }

    #endregion

    private static WorkOrderListItemDto ToListItemDto(WoEntity entity) => new()
    {
        Id = entity.Id,
        WorkOrderNo = entity.WorkOrderNo,
        SalesOrderNo = entity.SalesOrderNo,
        ProductionMainNo = entity.ProductionMainNo,
        ProductionSubNo = entity.ProductionSubNo,
        SignDate = entity.SignDate,
        Salesman = entity.Salesman,
        EndCustomer = entity.EndCustomer,
        DeliveryDate = entity.DeliveryDate,
        DelayPenalty = entity.DelayPenalty,
        SettlementMethod = entity.SettlementMethod,
        PlantGrade = entity.PlantGrade,
        PipeManufacturingType = entity.PipeManufacturingType,
        Specification = entity.Specification,
        LengthStatus = entity.LengthStatus,
        MinLength = entity.MinLength,
        MaxLength = entity.MaxLength,
        TotalQuantity = entity.TotalQuantity,
        TotalWeight = entity.TotalWeight,
        DeliveryState = entity.DeliveryState,
        TotalItemCount = entity.TotalItemCount,
        Status = entity.Status,
        CreatedTime = entity.CreatedTime
    };

    private static WorkOrderDetailDto ToDetailDto(WoEntity entity) => new()
    {
        Id = entity.Id,
        WorkOrderNo = entity.WorkOrderNo,
        SalesOrderNo = entity.SalesOrderNo,
        ProductionMainNo = entity.ProductionMainNo,
        ProductionSubNo = entity.ProductionSubNo,
        OrderItemIds = entity.OrderItemIds,
        Status = entity.Status,
        SignDate = entity.SignDate,
        Salesman = entity.Salesman,
        EndCustomer = entity.EndCustomer,
        DeliveryDate = entity.DeliveryDate,
        DelayPenalty = entity.DelayPenalty,
        PipeManufacturingType = entity.PipeManufacturingType,
        SettlementMethod = entity.SettlementMethod,
        StandardCode = entity.StandardCode,
        DeliveryState = entity.DeliveryState,
        PlantGrade = entity.PlantGrade,
        Specification = entity.Specification,
        OuterDiameterNegative = entity.OuterDiameterNegative,
        OuterDiameterPositive = entity.OuterDiameterPositive,
        WallThicknessNegative = entity.WallThicknessNegative,
        WallThicknessPositive = entity.WallThicknessPositive,
        LengthStatus = entity.LengthStatus,
        MinLength = entity.MinLength,
        MaxLength = entity.MaxLength,
        TotalQuantity = entity.TotalQuantity,
        TotalMeters = entity.TotalMeters,
        TotalWeight = entity.TotalWeight,
        TotalItemCount = entity.TotalItemCount,
        ItemDetails = entity.ItemDetails,
        TechnicalRequirements = entity.TechnicalRequirements.ToString(),
        RowVersion = entity.RowVersion,
        CreatedTime = entity.CreatedTime,
        CreatedBy = entity.CreatedBy,
        UpdatedTime = entity.UpdatedTime,
        UpdatedBy = entity.UpdatedBy,
        MaterialPlanStatus = entity.MaterialPlanStatus,
        MaterialPlanRate = entity.MaterialPlanRate,
        UnitWeight = PipeWeightCalculator.CalculateUnitWeight(
            entity.Specification,
            entity.OuterDiameterNegative,
            entity.OuterDiameterPositive,
            entity.WallThicknessNegative,
            entity.WallThicknessPositive,
            entity.LengthStatus,
            entity.MaxLength)
    };
}