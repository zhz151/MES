using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Mapping;
using MES.Services.Printing;

namespace MES.Services;

/// <summary>
/// 工单服务实现
/// </summary>
public class WorkOrderService : IWorkOrderService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkOrderService> _logger;
    private static readonly SemaphoreSlim _workOrderNoSemaphore = new SemaphoreSlim(1, 1);

    public WorkOrderService(AppDbContext context, ILogger<WorkOrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region 工单首页（订单状态监控）

    public async Task<PagedResult<OrderWorkOrderStatusDto>> GetOrderWorkOrderStatusPageAsync(WorkOrderQueryParams query)
    {
        // ===== 1. 基础查询：已确认订单 + 客户（DB层面） =====
        var orderQuery = _context.SalesOrders
            .Where(so => so.Status == SalesOrderStatus.Confirmed)
            .Join(
                _context.CustomerProfiles,
                so => so.CustomerId,
                c => c.Id,
                (so, c) => new { SalesOrder = so, Customer = c }
            );

        // ===== 2. 应用DB级文本筛选 =====
        if (!string.IsNullOrEmpty(query.Salesman))
            orderQuery = orderQuery.Where(x => x.Customer.Salesman.Contains(query.Salesman));

        if (!string.IsNullOrEmpty(query.EndCustomer))
            orderQuery = orderQuery.Where(x => x.Customer.EndCustomer != null && x.Customer.EndCustomer.Contains(query.EndCustomer));

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            orderQuery = orderQuery.Where(x =>
                x.SalesOrder.OrderNumber.Contains(keyword) ||
                x.Customer.CustomerUnit.Contains(keyword) ||
                x.Customer.Salesman.Contains(keyword) ||
                (x.Customer.EndCustomer != null && x.Customer.EndCustomer.Contains(keyword)) ||
                (keyword == "是" && _context.OrderItems.Any(oi => oi.SalesOrderId == x.SalesOrder.Id && oi.DelayPenalty)) ||
                (keyword == "否" && _context.OrderItems.Any(oi => oi.SalesOrderId == x.SalesOrder.Id && !oi.DelayPenalty))
            );
        }

        // ===== 3. 工单状态筛选（子查询 → SQL EXISTS，避免全表内存加载） =====
        if (!string.IsNullOrEmpty(query.WorkOrderStatus) && Enum.TryParse<WorkOrderStatus>(query.WorkOrderStatus, out var filterStatus))
        {
            switch (filterStatus)
            {
                case WorkOrderStatus.NotGenerated:
                    orderQuery = orderQuery.Where(x =>
                        !_context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status != WorkOrderStatus.Cancelled));
                    break;
                case WorkOrderStatus.Pending:
                    orderQuery = orderQuery.Where(x =>
                        _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status == WorkOrderStatus.Pending));
                    break;
                case WorkOrderStatus.Confirmed:
                    orderQuery = orderQuery.Where(x =>
                        _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status != WorkOrderStatus.Cancelled) &&
                        !_context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status == WorkOrderStatus.Pending));
                    break;
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
                        ? orderQuery.OrderByDescending(x => x.SalesOrder.OrderNumber)
                        : orderQuery.OrderBy(x => x.SalesOrder.OrderNumber);
                    break;
                case "signdate":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => x.SalesOrder.SignDate)
                        : orderQuery.OrderBy(x => x.SalesOrder.SignDate);
                    break;
                case "salesman":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => x.Customer.Salesman)
                        : orderQuery.OrderBy(x => x.Customer.Salesman);
                    break;
                case "customername":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => x.Customer.CustomerUnit)
                        : orderQuery.OrderBy(x => x.Customer.CustomerUnit);
                    break;
                case "endcustomer":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => x.Customer.EndCustomer ?? "")
                        : orderQuery.OrderBy(x => x.Customer.EndCustomer ?? "");
                    break;
                case "deliverystart":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.SalesOrder.Id).Min(oi => (DateTime?)oi.DeliveryDate))
                        : orderQuery.OrderBy(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.SalesOrder.Id).Min(oi => (DateTime?)oi.DeliveryDate));
                    break;
                case "deliveryend":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.SalesOrder.Id).Max(oi => (DateTime?)oi.DeliveryDate))
                        : orderQuery.OrderBy(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.SalesOrder.Id).Max(oi => (DateTime?)oi.DeliveryDate));
                    break;
                case "hasdelaypenalty":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.OrderItems.Any(oi => oi.SalesOrderId == x.SalesOrder.Id && oi.DelayPenalty))
                        : orderQuery.OrderBy(x => _context.OrderItems.Any(oi => oi.SalesOrderId == x.SalesOrder.Id && oi.DelayPenalty));
                    break;
                case "totalcontractweight":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.SalesOrder.Id).Sum(oi => oi.ContractWeight))
                        : orderQuery.OrderBy(x => _context.OrderItems.Where(oi => oi.SalesOrderId == x.SalesOrder.Id).Sum(oi => oi.ContractWeight));
                    break;
                case "itemcount":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.OrderItems.Count(oi => oi.SalesOrderId == x.SalesOrder.Id))
                        : orderQuery.OrderBy(x => _context.OrderItems.Count(oi => oi.SalesOrderId == x.SalesOrder.Id));
                    break;
                case "workordercount":
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x => _context.WorkOrders.Count(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status != WorkOrderStatus.Cancelled))
                        : orderQuery.OrderBy(x => _context.WorkOrders.Count(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status != WorkOrderStatus.Cancelled));
                    break;
                case "workorderstatus":
                    // 子查询计算状态排序优先级：Pending=1, NotGenerated=2, Confirmed=3
                    orderQuery = sortDesc
                        ? orderQuery.OrderByDescending(x =>
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status == WorkOrderStatus.Pending) ? 1 :
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status != WorkOrderStatus.Cancelled) ? 3 : 2)
                        : orderQuery.OrderBy(x =>
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status == WorkOrderStatus.Pending) ? 1 :
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status != WorkOrderStatus.Cancelled) ? 3 : 2);
                    break;
                default:
                    orderQuery = orderQuery
                        .OrderBy(x =>
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status == WorkOrderStatus.Pending) ? 1 :
                            _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status != WorkOrderStatus.Cancelled) ? 3 : 2)
                        .ThenByDescending(x => x.SalesOrder.SignDate);
                    break;
            }
        }
        else
        {
            // 默认排序：Pending→NotGenerated→Confirmed → 签订日期降序
            orderQuery = orderQuery
                .OrderBy(x =>
                    _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status == WorkOrderStatus.Pending) ? 1 :
                    _context.WorkOrders.Any(wo => wo.SalesOrderNo == x.SalesOrder.OrderNumber && wo.Status != WorkOrderStatus.Cancelled) ? 3 : 2)
                .ThenByDescending(x => x.SalesOrder.SignDate);
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
        var pagedOrderIds = pagedOrders.Select(x => x.SalesOrder.Id).ToList();
        var pagedOrderNumbers = pagedOrders.Select(x => x.SalesOrder.OrderNumber).ToList();

        var workOrderGroups = await _context.WorkOrders
            .Where(wo => pagedOrderNumbers.Contains(wo.SalesOrderNo) && wo.Status != WorkOrderStatus.Cancelled)
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
        var items = pagedOrders.Select(item =>
        {
            var order = item.SalesOrder;
            var customer = item.Customer;
            var woInfo = workOrderDict.GetValueOrDefault(order.OrderNumber);
            var agg = itemAggDict.GetValueOrDefault(order.Id);

            var hasWorkOrder = woInfo != null && woInfo.WorkOrderCount > 0;
            WorkOrderStatus workOrderStatus;
            int? workOrderId = null;

            if (!hasWorkOrder)
            {
                workOrderStatus = WorkOrderStatus.NotGenerated;
            }
            else if (woInfo!.HasPending)
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
                Salesman = customer.Salesman,
                CustomerName = customer.CustomerUnit,
                EndCustomer = customer.EndCustomer,
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

    public async Task<List<CancelledOrderDto>> GetCancelledOrdersAsync()
    {
        var query = from so in _context.SalesOrders
                    join c in _context.CustomerProfiles on so.CustomerId equals c.Id
                    join wo in _context.WorkOrders on so.OrderNumber equals wo.SalesOrderNo
                    where so.Status == SalesOrderStatus.Cancelled
                          && wo.Status != WorkOrderStatus.Cancelled
                    select new CancelledOrderDto
                    {
                        SalesOrderId = so.Id,
                        OrderNumber = so.OrderNumber,
                        SignDate = so.SignDate,
                        Salesman = c.Salesman,
                        CustomerName = c.CustomerUnit,
                        WorkOrderId = wo.Id,
                        WorkOrderNo = wo.WorkOrderNo
                    };

        return await query.ToListAsync();
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
            .Where(wo => wo.SalesOrderNo == salesOrderNo && wo.Status != WorkOrderStatus.Cancelled)
            .ToListAsync();

        // 构建 项次ID -> (原主号, 原次号) 映射，同时建立工单ID查询
        var itemToOriginalNo = new Dictionary<int, (string MainNo, string? SubNo)>();
        var itemToWorkOrder = new Dictionary<int, WorkOrder>(); // 用于后续校验合并字段是否一致
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

        // 单独加载 ProductionStandard
        var psIds = orderItems
            .Where(oi => oi.ProductionStandardId > 0)
            .Select(oi => oi.ProductionStandardId)
            .Distinct()
            .ToList();
        var psDict = psIds.Any()
            ? await _context.ProductionStandards
                .Where(ps => psIds.Contains(ps.Id))
                .ToDictionaryAsync(ps => ps.Id, ps => ps)
            : new Dictionary<int, ProductionStandard>();

        var groups = GroupOrderItemsByMergeFields(orderItems);
        var result = new List<OrderItemForWorkOrderDto>();
        var mainNoCounter = 1;

        foreach (var group in groups)
        {
            var firstItem = group.First();
            var prefix = GetMainNoPrefix(firstItem.MaterialName, firstItem.LengthStatus);
            var suggestedMainNo = $"{prefix}{mainNoCounter++:D2}";

            foreach (var item in group)
            {
                psDict.TryGetValue(item.ProductionStandardId, out var ps);
                var dto = new OrderItemForWorkOrderDto
                {
                    Id = item.Id,
                    OrderNumber = salesOrder.OrderNumber,
                    Sequence = item.Sequence,
                    MaterialName = item.MaterialName,
                    DeliveryDate = item.DeliveryDate,
                    DelayPenalty = item.DelayPenalty,
                    SettlementMethod = item.SettlementMethod,
                    StandardCode = ps?.StandardCode ?? string.Empty,
                    DeliveryState = item.DeliveryState,
                    PlantGrade = item.PlantGrade,
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
                    RequirementType = item.ProductRequirement?.RequirementType.ToString() ?? "Normal",
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
                        && item.MaterialName == originalWo.MaterialName
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
        return $"{item.DeliveryDate:yyyy-MM-dd}|{item.DelayPenalty}|{item.MaterialName}|{item.SettlementMethod}|" +
               $"{item.ProductionStandardId}|{item.DeliveryState}|{item.PlantGrade}|{item.Specification}|" +
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
        if (item1.MaterialName != item2.MaterialName)
            errors.Add($"物料名称 ({item1.MaterialName} ≠ {item2.MaterialName})");
        if (item1.ProductionStandardId != item2.ProductionStandardId)
            errors.Add($"产品标准 ({item1.ProductionStandardId} ≠ {item2.ProductionStandardId})");
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

    private static string GetMainNoPrefix(MaterialName materialName, LengthStatus lengthStatus)
    {
        if (materialName == MaterialName.WeldedPipe)
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

        // 单独加载 Customer
        var salesOrderCustomer = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == salesOrder.CustomerId);

        // 2. 获取订单项次
        var allOrderItems = await _context.OrderItems
            .Include(oi => oi.ProductRequirement)
            .Where(oi => oi.SalesOrderId == salesOrder.Id)
            .ToDictionaryAsync(oi => oi.Sequence, oi => oi);

        // 单独加载 ProductionStandard
        var psIds = allOrderItems.Values
            .Where(oi => oi.ProductionStandardId > 0)
            .Select(oi => oi.ProductionStandardId)
            .Distinct()
            .ToList();
        var psDict = psIds.Any()
            ? await _context.ProductionStandards
                .Where(ps => psIds.Contains(ps.Id))
                .ToDictionaryAsync(ps => ps.Id, ps => ps)
            : new Dictionary<int, ProductionStandard>();

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

            var firstItem = groupItems.First()!;
            foreach (var item in groupItems.Skip(1))
            {
                var (isValid, errors) = ValidateMergeFields(firstItem, item!);
                if (!isValid)
                {
                    mergeFieldErrors.Add($"主号 {workOrderGroup.ProductionMainNo} 下的项次 {item!.Sequence} 与项次 {firstItem!.Sequence} 合并字段不一致:\n  {string.Join("\n  ", errors)}");
                }
            }
        }
        if (mergeFieldErrors.Any())
            throw new BusinessException($"工单分组合并规则验证失败:\n\n{string.Join("\n\n", mergeFieldErrors)}");

        // 5. 使用事务
        using var transaction = await _context.Database.BeginTransactionAsync();
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

                if (semiPlans.Any()) _context.PurchaseSemiPlans.RemoveRange(semiPlans);
                if (finishPlans.Any()) _context.PurchaseFinishedPlans.RemoveRange(finishPlans);
                if (invPlans.Any()) _context.InventoryPlans.RemoveRange(invPlans);
                if (piercingPlans.Any()) _context.RoundBarPiercingPlans.RemoveRange(piercingPlans);

                await _context.SaveChangesAsync();
            }

            _context.WorkOrders.RemoveRange(existingWorkOrders);
            await _context.SaveChangesAsync();
            
            // 清除缓存，确保查询最新数据
            _context.ChangeTracker.Clear();

            var workOrdersToAdd = new List<WorkOrder>();
            var generatedWorkOrders = new List<GeneratedWorkOrderDto>();

            foreach (var workOrderGroup in request.WorkOrders)
            {
                var groupItems = workOrderGroup.OrderItemIds
                    .Select(id => allOrderItems.GetValueOrDefault(id))
                    .Where(item => item != null)
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

                var workOrder = new WorkOrder
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
                    MaterialName = firstItem.MaterialName,
                    SettlementMethod = firstItem.SettlementMethod,
                    StandardCode = psDict.GetValueOrDefault(firstItem.ProductionStandardId)?.StandardCode ?? string.Empty,
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
                    Status = (int)WorkOrderStatus.Confirmed,
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
            
            _logger.LogInformation("生成工单成功: 订单号 {OrderNo}, 生成 {Count} 个工单",
                request.SalesOrderNo, generatedWorkOrders.Count);

            return generatedWorkOrders;
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

        var customer = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == salesOrder.CustomerId);

        // 2. 获取订单项次
        var allOrderItems = await _context.OrderItems
            .Include(oi => oi.ProductRequirement)
            .Where(oi => oi.SalesOrderId == salesOrder.Id)
            .ToDictionaryAsync(oi => oi.Sequence, oi => oi);

        var psIds = allOrderItems.Values
            .Where(oi => oi.ProductionStandardId > 0)
            .Select(oi => oi.ProductionStandardId).Distinct().ToList();
        var psDict = psIds.Any()
            ? await _context.ProductionStandards
                .Where(ps => psIds.Contains(ps.Id))
                .ToDictionaryAsync(ps => ps.Id, ps => ps)
            : new Dictionary<int, ProductionStandard>();

        // 3. 验证项次
        foreach (var workOrderGroup in request.WorkOrders)
        {
            foreach (var itemId in workOrderGroup.OrderItemIds)
            {
                if (!allOrderItems.ContainsKey(itemId))
                    throw new BusinessException($"项次 ID {itemId} 不存在或已被删除");
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
            if (!groupItems.Any()) continue;
            if (groupItems.Count <= 1) continue;

            var firstItem = groupItems.First()!;
            foreach (var item in groupItems.Skip(1))
            {
                var (isValid, errors) = ValidateMergeFields(firstItem, item!);
                if (!isValid)
                {
                    mergeFieldErrors.Add($"主号 {workOrderGroup.ProductionMainNo} 下的项次 {item!.Sequence} 与项次 {firstItem!.Sequence} 合并字段不一致:\n  {string.Join("\n  ", errors)}");
                }
            }
        }
        if (mergeFieldErrors.Any())
            throw new BusinessException($"工单分组合并规则验证失败:\n\n{string.Join("\n\n", mergeFieldErrors)}");

        // 5. 加载现有工单，构建 (主号,次号) → WorkOrder 映射
        var existingWorkOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == request.SalesOrderNo)
            .ToListAsync();

        var existingByKey = new Dictionary<(string mainNo, string? subNo), WorkOrder>();
        foreach (var wo in existingWorkOrders)
        {
            var key = (wo.ProductionMainNo, wo.ProductionSubNo);
            existingByKey.TryAdd(key, wo);
        }

        _logger.LogInformation("订单 {OrderNo} 更新修改: 现有工单 {ExistingCount} 个, 提交分组 {GroupCount} 个",
            request.SalesOrderNo, existingWorkOrders.Count, request.WorkOrders.Count);

        // 6. 事务处理
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = new List<GeneratedWorkOrderDto>();
            var workOrdersToAdd = new List<WorkOrder>();
            var matchedKeys = new HashSet<(string mainNo, string? subNo)>();

            // 7. 遍历提交的分组：匹配现有工单则更新，否则新建
            foreach (var group in request.WorkOrders)
            {
                var groupItems = group.OrderItemIds
                    .Select(id => allOrderItems.GetValueOrDefault(id))
                    .Where(item => item != null)
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

                    // 检测合并关键字段是否变化（即使项次ID没变，交货期/规格等字段也可能变了）
                    var mergeFieldChanged = existingWo.DeliveryDate != firstItem.DeliveryDate
                        || existingWo.DelayPenalty != firstItem.DelayPenalty
                        || existingWo.MaterialName != firstItem.MaterialName
                        || existingWo.SettlementMethod != firstItem.SettlementMethod
                        || existingWo.StandardCode != (psDict.GetValueOrDefault(firstItem.ProductionStandardId)?.StandardCode ?? string.Empty)
                        || existingWo.DeliveryState != firstItem.DeliveryState
                        || existingWo.PlantGrade != firstItem.PlantGrade
                        || existingWo.Specification != firstItem.Specification
                        || existingWo.OuterDiameterNegative != firstItem.OuterDiameterNegative
                        || existingWo.OuterDiameterPositive != firstItem.OuterDiameterPositive
                        || existingWo.WallThicknessNegative != firstItem.WallThicknessNegative
                        || existingWo.WallThicknessPositive != firstItem.WallThicknessPositive
                        || existingWo.LengthStatus != firstItem.LengthStatus;
                    if (mergeFieldChanged)
                    {
                        changed = true;
                    }

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
                        existingWo.DeliveryDate = firstItem.DeliveryDate;
                        existingWo.DelayPenalty = firstItem.DelayPenalty;
                        existingWo.MaterialName = firstItem.MaterialName;
                        existingWo.SettlementMethod = firstItem.SettlementMethod;
                        existingWo.StandardCode = psDict.GetValueOrDefault(firstItem.ProductionStandardId)?.StandardCode ?? string.Empty;
                        existingWo.DeliveryState = firstItem.DeliveryState;
                        existingWo.PlantGrade = firstItem.PlantGrade;
                        existingWo.Specification = firstItem.Specification;
                        existingWo.OuterDiameterNegative = firstItem.OuterDiameterNegative;
                        existingWo.OuterDiameterPositive = firstItem.OuterDiameterPositive;
                        existingWo.WallThicknessNegative = firstItem.WallThicknessNegative;
                        existingWo.WallThicknessPositive = firstItem.WallThicknessPositive;
                        existingWo.LengthStatus = firstItem.LengthStatus;

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
                        Status = (int)WorkOrderStatus.Confirmed,
                        TotalQuantity = groupItems.Sum(i => i.LengthStatus == LengthStatus.Fixed ? (i.Quantity ?? 0) : 0),
                        TotalWeight = groupItems.Sum(i => i.LengthStatus == LengthStatus.Fixed ? i.TheoreticalWeight : i.ContractWeight),
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

                    var workOrder = new WorkOrder
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
                        MaterialName = firstItem.MaterialName,
                        SettlementMethod = firstItem.SettlementMethod,
                        StandardCode = psDict.GetValueOrDefault(firstItem.ProductionStandardId)?.StandardCode ?? string.Empty,
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

                    if (semiPlans.Any()) _context.PurchaseSemiPlans.RemoveRange(semiPlans);
                    if (finishPlans.Any()) _context.PurchaseFinishedPlans.RemoveRange(finishPlans);
                    if (invPlans.Any()) _context.InventoryPlans.RemoveRange(invPlans);
                    if (piercingPlans.Any()) _context.RoundBarPiercingPlans.RemoveRange(piercingPlans);

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
                    Status = (int)WorkOrderStatus.Confirmed,
                    TotalQuantity = workOrdersToAdd[i].TotalQuantity,
                    TotalWeight = workOrdersToAdd[i].TotalWeight,
                    IsModified = true
                });
            }

            await transaction.CommitAsync();

            _logger.LogInformation("更新修改工单成功: 订单号 {OrderNo}, 更新 {UpdatedCount} 个, 新建 {NewCount} 个",
                request.SalesOrderNo, result.Count(r => !r.IsModified || existingByKey.ContainsKey((r.ProductionMainNo, r.ProductionSubNo))),
                workOrdersToAdd.Count);

            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "更新修改工单失败: 订单号 {OrderNo}", request.SalesOrderNo);
            throw;
        }
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

    public async Task<PagedResult<WorkOrderListDto>> GetPagedAsync(WorkOrderQueryParams query)
    {
        var workOrderQuery = _context.WorkOrders.AsQueryable();

        if (!string.IsNullOrEmpty(query.SalesOrderNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.SalesOrderNo.Contains(query.SalesOrderNo));
        if (!string.IsNullOrEmpty(query.ProductionMainNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.ProductionMainNo.Contains(query.ProductionMainNo));
        if (!string.IsNullOrEmpty(query.ProductionSubNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.ProductionSubNo != null && wo.ProductionSubNo.Contains(query.ProductionSubNo));
        if (query.Status.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.Status == (WorkOrderStatus)query.Status.Value);
        if (!string.IsNullOrEmpty(query.MaterialName))
        {
            if (Enum.TryParse<MaterialName>(query.MaterialName, out var materialName))
                workOrderQuery = workOrderQuery.Where(wo => wo.MaterialName == materialName);
        }
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
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;

            // 若关键字可解析为日期，连同计划表日期一起 OR 搜索（只用一个 Where，避免 AND 屏蔽）
            if (DateTime.TryParse(keyword, out var keywordDate))
            {
                var date = keywordDate.Date;
                workOrderQuery = workOrderQuery.Where(wo =>
                    wo.WorkOrderNo.Contains(keyword) ||
                    wo.SalesOrderNo.Contains(keyword) ||
                    wo.ProductionMainNo.Contains(keyword) ||
                    (wo.ProductionSubNo != null && wo.ProductionSubNo.Contains(keyword)) ||
                    wo.Salesman.Contains(keyword) ||
                    (wo.EndCustomer != null && wo.EndCustomer.Contains(keyword)) ||
                    _context.PurchaseSemiPlans.Any(p => p.WorkOrderId == wo.Id && p.PlanDate == date) ||
                    _context.PurchaseFinishedPlans.Any(p => p.WorkOrderId == wo.Id && p.PlanDate == date) ||
                    _context.InventoryPlans.Any(p => p.WorkOrderId == wo.Id && p.PlanDate == date));
            }
            else
            {
                workOrderQuery = workOrderQuery.Where(wo =>
                    wo.WorkOrderNo.Contains(keyword) ||
                    wo.SalesOrderNo.Contains(keyword) ||
                    wo.ProductionMainNo.Contains(keyword) ||
                    (wo.ProductionSubNo != null && wo.ProductionSubNo.Contains(keyword)) ||
                    wo.Salesman.Contains(keyword) ||
                    (wo.EndCustomer != null && wo.EndCustomer.Contains(keyword)) ||
                    wo.PlantGrade.Contains(keyword) ||
                    wo.Specification.Contains(keyword));
            }
        }

        if (query.MaterialPlanStatus.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => (int)wo.MaterialPlanStatus == query.MaterialPlanStatus.Value);

        // 计划类型过滤：仅显示包含指定类型计划的工单
        if (!string.IsNullOrEmpty(query.PlanTypeFilter))
        {
            var planTypes = query.PlanTypeFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .ToHashSet();

            if (planTypes.Count > 0 && planTypes.Count < 5)
            {
                var matchedIds = new HashSet<int>();

                if (planTypes.Contains("semi"))
                {
                    var ids = await _context.PurchaseSemiPlans
                        .Select(p => p.WorkOrderId)
                        .Distinct().ToListAsync();
                    foreach (var id in ids) matchedIds.Add(id);
                }

                if (planTypes.Contains("finish"))
                {
                    var ids = await _context.PurchaseFinishedPlans
                        .Select(p => p.WorkOrderId)
                        .Distinct().ToListAsync();
                    foreach (var id in ids) matchedIds.Add(id);
                }

                if (planTypes.Contains("inventory"))
                {
                    var ids = await _context.InventoryPlans
                        .Where(p => p.ReworkType == null)
                        .Select(p => p.WorkOrderId)
                        .Distinct().ToListAsync();
                    foreach (var id in ids) matchedIds.Add(id);
                }

                if (planTypes.Contains("rework"))
                {
                    var ids = await _context.InventoryPlans
                        .Where(p => p.ReworkType != null)
                        .Select(p => p.WorkOrderId)
                        .Distinct().ToListAsync();
                    foreach (var id in ids) matchedIds.Add(id);
                }

                if (planTypes.Contains("piercing"))
                {
                    var ids = await _context.RoundBarPiercingPlans
                        .Select(p => p.WorkOrderId)
                        .Distinct().ToListAsync();
                    foreach (var id in ids) matchedIds.Add(id);
                }

                if (matchedIds.Count > 0)
                    workOrderQuery = workOrderQuery.Where(wo => matchedIds.Contains(wo.Id));
                else
                    workOrderQuery = workOrderQuery.Where(wo => false);
            }
        }

        var totalCount = await workOrderQuery.CountAsync();

        if (!string.IsNullOrEmpty(query.SortBy))
        {
            switch (query.SortBy.ToLower())
            {
                case "workorderno":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.WorkOrderNo) : workOrderQuery.OrderBy(wo => wo.WorkOrderNo);
                    break;
                case "salesorderno":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.SalesOrderNo) : workOrderQuery.OrderBy(wo => wo.SalesOrderNo);
                    break;
                case "deliverydate":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.DeliveryDate) : workOrderQuery.OrderBy(wo => wo.DeliveryDate);
                    break;
                case "status":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.Status) : workOrderQuery.OrderBy(wo => wo.Status);
                    break;
                case "productionmainno":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.ProductionMainNo) : workOrderQuery.OrderBy(wo => wo.ProductionMainNo);
                    break;
                case "productionsubno":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.ProductionSubNo) : workOrderQuery.OrderBy(wo => wo.ProductionSubNo);
                    break;
                case "signdate":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.SignDate) : workOrderQuery.OrderBy(wo => wo.SignDate);
                    break;
                case "salesman":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.Salesman) : workOrderQuery.OrderBy(wo => wo.Salesman);
                    break;
                case "endcustomer":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.EndCustomer ?? "") : workOrderQuery.OrderBy(wo => wo.EndCustomer ?? "");
                    break;
                case "delaypenalty":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.DelayPenalty) : workOrderQuery.OrderBy(wo => wo.DelayPenalty);
                    break;
                case "settlementmethod":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.SettlementMethod) : workOrderQuery.OrderBy(wo => wo.SettlementMethod);
                    break;
                case "plantgrade":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.PlantGrade) : workOrderQuery.OrderBy(wo => wo.PlantGrade);
                    break;
                case "specification":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.Specification) : workOrderQuery.OrderBy(wo => wo.Specification);
                    break;
                case "lengthstatus":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.LengthStatus) : workOrderQuery.OrderBy(wo => wo.LengthStatus);
                    break;
                case "materialname":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.MaterialName) : workOrderQuery.OrderBy(wo => wo.MaterialName);
                    break;
                case "maxlength":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.MaxLength) : workOrderQuery.OrderBy(wo => wo.MaxLength);
                    break;
                case "minlength":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.MinLength) : workOrderQuery.OrderBy(wo => wo.MinLength);
                    break;
                case "totalquantity":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.TotalQuantity) : workOrderQuery.OrderBy(wo => wo.TotalQuantity);
                    break;
                case "totalweight":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.TotalWeight) : workOrderQuery.OrderBy(wo => wo.TotalWeight);
                    break;
                case "deliverystate":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.DeliveryState) : workOrderQuery.OrderBy(wo => wo.DeliveryState);
                    break;
                case "totalitemcount":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.TotalItemCount) : workOrderQuery.OrderBy(wo => wo.TotalItemCount);
                    break;
                case "materialplanstatus":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.MaterialPlanStatus) : workOrderQuery.OrderBy(wo => wo.MaterialPlanStatus);
                    break;
                case "materialplanrate":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.MaterialPlanRate) : workOrderQuery.OrderBy(wo => wo.MaterialPlanRate);
                    break;
                case "latestplandate":
                    // 5种用料计划中最新的计划日期（取最大值），关联子查询实现
                    workOrderQuery = query.IsDescending
                        ? workOrderQuery.OrderByDescending(wo =>
                            _context.PurchaseSemiPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate)
                                .Concat(_context.PurchaseFinishedPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Concat(_context.InventoryPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Concat(_context.RoundBarPiercingPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Max())
                        : workOrderQuery.OrderBy(wo =>
                            _context.PurchaseSemiPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate)
                                .Concat(_context.PurchaseFinishedPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Concat(_context.InventoryPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Concat(_context.RoundBarPiercingPlans.Where(p => p.WorkOrderId == wo.Id).Select(p => (DateTime?)p.PlanDate))
                                .Max());
                    break;
                default:
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.CreatedTime) : workOrderQuery.OrderBy(wo => wo.CreatedTime);
                    break;
            }
        }
        else
        {
            workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.CreatedTime) : workOrderQuery.OrderBy(wo => wo.CreatedTime);
        }

        // 当有关联主号/订单用料筛选时，需加载全部数据计算聚合后再筛选
        if (query.MainNoMaterialPlanStatus.HasValue || query.OrderMaterialPlanStatus.HasValue)
        {
            var allWorkOrders = await workOrderQuery.AsNoTracking().ToListAsync();
            var allItems = allWorkOrders.Select(wo => wo.ToListDto()).ToList();
            if (allItems.Any())
                await EnrichWithAggregatedStatusAsync(allItems);

            if (query.MainNoMaterialPlanStatus.HasValue)
                allItems = allItems.Where(i => i.MainNoMaterialPlanStatus == query.MainNoMaterialPlanStatus.Value).ToList();
            if (query.OrderMaterialPlanStatus.HasValue)
                allItems = allItems.Where(i => i.OrderMaterialPlanStatus == query.OrderMaterialPlanStatus.Value).ToList();

            return new PagedResult<WorkOrderListDto>
            {
                Items = allItems.Skip(query.Skip).Take(query.PageSize).ToList(),
                TotalCount = allItems.Count,
                PageIndex = query.PageIndex,
                PageSize = query.PageSize
            };
        }

        var workOrders = await workOrderQuery
            .AsNoTracking()
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var items = workOrders.Select(wo => wo.ToListDto()).ToList();

        // 计算主号级和订单级聚合（用于用料计划总览三级展示）
        if (items.Any())
        {
            await EnrichWithAggregatedStatusAsync(items);
        }

        return new PagedResult<WorkOrderListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    #region 用料计划三级聚合

    /// <summary>
    /// 为工单列表补充主号级和订单级聚合状态
    /// </summary>
    private async Task EnrichWithAggregatedStatusAsync(List<WorkOrderListDto> items)
    {
        // 0. 从 CustomerProfile 覆盖冗余快照字段
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
            if (latestDateByWo.TryGetValue(item.Id, out var latestDate)) item.LatestPlanDate = latestDate;
        }

        // 1b. 从用料计划数据实时计算工单级满足率（不依赖 WorkOrder 预计算字段）
        var semiByWo = allSemiPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var finishByWo = allFinishPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var inventoryByWo = allInventoryPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var piercingByWo = allPiercingPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var woById = allWorkOrdersInOrders.ToDictionary(wo => wo.Id);

        foreach (var item in items)
        {
            if (!woById.TryGetValue(item.Id, out var wo)) continue;
            var semi = semiByWo.TryGetValue(item.Id, out var s) ? s : new List<PurchaseSemiPlan>();
            var finish = finishByWo.TryGetValue(item.Id, out var f) ? f : new List<PurchaseFinishedPlan>();
            var inv = inventoryByWo.TryGetValue(item.Id, out var iv) ? iv : new List<InventoryPlan>();
            var pierce = piercingByWo.TryGetValue(item.Id, out var p) ? p : new List<RoundBarPiercingPlan>();
            var (rate, status) = PlanRateCalculator.ComputeWorkOrderRate(wo, semi, finish, inv, pierce);
            item.MaterialPlanRate = rate;
            item.MaterialPlanStatus = status;
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

            var (rate, status) = CalculateMainNoAggregation(groupWorkOrders, groupSemiPlans, groupFinishPlans, groupInventoryPlans, groupReworkPlans, groupPiercingPlans);

            foreach (var item in items.Where(i =>
                i.SalesOrderNo == key.SalesOrderNo && i.ProductionMainNo == key.MainNo))
            {
                item.MainNoMaterialPlanRate = rate;
                item.MainNoMaterialPlanStatus = (int)status;
            }
        }

        // 2. 订单级聚合：只要该订单下所有主号都没有"部分"和"未计划"，即为全部满足
        foreach (var orderNo in orderNos)
        {
            var orderItems = items.Where(i => i.SalesOrderNo == orderNo).ToList();
            var hasPartialOrNotPlanned = orderItems.Any(i =>
                i.MainNoMaterialPlanStatus == (int)MaterialPlanStatus.Partial ||
                i.MainNoMaterialPlanStatus == (int)MaterialPlanStatus.NotPlanned);
            var allNotPlanned = orderItems.All(i =>
                i.MainNoMaterialPlanStatus == (int)MaterialPlanStatus.NotPlanned);

            MaterialPlanStatus orderStatus;
            if (allNotPlanned)
                orderStatus = MaterialPlanStatus.NotPlanned;
            else if (hasPartialOrNotPlanned)
                orderStatus = MaterialPlanStatus.Partial;
            else
                orderStatus = MaterialPlanStatus.Satisfied;

            foreach (var item in orderItems)
                item.OrderMaterialPlanStatus = (int)orderStatus;
        }
    }

    /// <summary>
    /// 计算主号级聚合（使用原始标准，不含"理论满足"）
    /// </summary>
    private (decimal rate, MaterialPlanStatus status) CalculateMainNoAggregation(
        List<WorkOrder> workOrders,
        List<PurchaseSemiPlan> semiPlans,
        List<PurchaseFinishedPlan> finishPlans,
        List<InventoryPlan> inventoryPlans,
        List<InventoryPlan> reworkPlans,
        List<RoundBarPiercingPlan> piercingPlans)
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
            // 成品采购：×1.02
            totalEffective += fixedFinish.Sum(p => p.RequiredPiece ?? 0) * 1.02m;
            // 库存使用：×1.02
            totalEffective += (int)(fixedInventory.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple) * 1.02m);
            // 库料改制：不乘系数
            totalEffective += (int)fixedRework.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple);
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
            // 成品采购：×1.05
            totalEffective += nonFixedFinish.Sum(p => p.RequiredWeight) * 1.05m;
            // 库存使用：×1.05
            totalEffective += nonFixedInventory.Sum(p => p.UsedWeight) * 1.05m;
            // 库料改制：不乘系数
            totalEffective += nonFixedRework.Sum(p => p.UsedWeight);
            // 圆棒穿孔：不乘系数（同原料采购）
            totalEffective += nonFixedPiercing.Sum(p => p.RequiredWeight);
        }

        if (totalDemand <= 0) return (0, MaterialPlanStatus.NotPlanned);

        var rate = Math.Round(totalEffective / totalDemand * 100m, 0);

        // 无任何投料 → 未计划
        if (rate <= 0) return (0, MaterialPlanStatus.NotPlanned);

        // 小批量特殊处理：定尺总支数 ≤ 20 时，≥100% 即视为满足
        var fixedTotalQuantity = fixedOrders.Sum(wo => wo.TotalQuantity);
        if (fixedTotalQuantity > 0 && fixedTotalQuantity <= 20)
        {
            var batchStatus = rate >= 100m ? MaterialPlanStatus.Satisfied : MaterialPlanStatus.Partial;
            return (rate, batchStatus);
        }

        // 使用原始标准（不含理论满足）
        var status = CalculateMainNoStatus(rate, fixedOrders.Any());
        return (rate, status);
    }

    /// <summary>
    /// 主号级状态判定（原标准，无"理论满足"）
    /// </summary>
    private static MaterialPlanStatus CalculateMainNoStatus(decimal rate, bool isFixed)
    {
        if (rate <= 0) return MaterialPlanStatus.NotPlanned;

        if (isFixed)
        {
            if (rate < 102m) return MaterialPlanStatus.Partial;
            if (rate <= 110m) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            if (rate < 105m) return MaterialPlanStatus.Partial;
            if (rate <= 120m) return MaterialPlanStatus.Satisfied;
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

        var dto = workOrder.ToDetailDto();

        // 覆盖冗余快照字段：从 CustomerProfile 取当前最新值
        await PatchCustomerFieldsAsync(dto, workOrder.SalesOrderNo);

        return dto;
    }

    public async Task<WorkOrderDetailDto> GetByWorkOrderNoAsync(string workOrderNo)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.WorkOrderNo == workOrderNo);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var dto = workOrder.ToDetailDto();
        await PatchCustomerFieldsAsync(dto, workOrder.SalesOrderNo);
        return dto;
    }

    public async Task<List<WorkOrderListDto>> GetBySalesOrderNoAsync(string salesOrderNo)
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.SalesOrderNo == salesOrderNo)
            .OrderBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToListAsync();

        var results = workOrders.Select(wo => wo.ToListDto()).ToList();
        await PatchCustomerFieldsAsync(results);
        return results;
    }

    /// <summary>
    /// 从 CustomerProfile 取当前最新 Salesman/EndCustomer，覆盖 WorkOrder 冗余快照
    /// </summary>
    private async Task PatchCustomerFieldsAsync(WorkOrderDetailDto dto, string salesOrderNo)
    {
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .Include(so => so.Customer)
            .FirstOrDefaultAsync(so => so.OrderNumber == salesOrderNo);
        if (salesOrder?.Customer != null)
        {
            dto.Salesman = salesOrder.Customer.Salesman;
            dto.EndCustomer = salesOrder.Customer.EndCustomer;
        }
    }

    /// <summary>
    /// 批量从 CustomerProfile 覆盖 WorkOrderListDto 的冗余快照字段
    /// </summary>
    private async Task PatchCustomerFieldsAsync(List<WorkOrderListDto> items)
    {
        var orderNos = items.Select(i => i.SalesOrderNo).Distinct().ToList();
        if (orderNos.Count == 0) return;

        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .Include(so => so.Customer)
            .Where(so => orderNos.Contains(so.OrderNumber))
            .ToListAsync();

        var customerByOrderNo = salesOrders.ToDictionary(so => so.OrderNumber, so => so.Customer);

        foreach (var item in items)
        {
            if (customerByOrderNo.TryGetValue(item.SalesOrderNo, out var customer))
            {
                item.Salesman = customer.Salesman;
                item.EndCustomer = customer.EndCustomer;
            }
        }
    }

    public async Task<UpdateWorkOrderStatusResponseDto> UpdateStatusAsync(int id, UpdateWorkOrderStatusRequest request)
    {
        var workOrder = await _context.WorkOrders
            .FirstOrDefaultAsync(wo => wo.Id == id);
        if (workOrder == null)
            throw new BusinessException("工单不存在");
        if (!CanTransitionTo(workOrder.Status, (WorkOrderStatus)request.Status))
            throw new BusinessException($"不允许从 {GetStatusText(workOrder.Status)} 变更为 {GetStatusText((WorkOrderStatus)request.Status)}");

        workOrder.Status = (WorkOrderStatus)request.Status;
        _context.Entry(workOrder).Property(x => x.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("工单已被其他用户修改，请刷新后重试");
        }

        _logger.LogInformation("更新工单状态成功: 工单号 {WorkOrderNo}, 新状态 {Status}",
            workOrder.WorkOrderNo, request.Status);
        return new UpdateWorkOrderStatusResponseDto { Id = workOrder.Id, Status = (int)workOrder.Status };
    }

    public async Task DeleteAsync(int id)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        // 级联删除关联的用料计划（无FK约束，需手动清理）
        var semiPlans = await _context.PurchaseSemiPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var finishPlans = await _context.PurchaseFinishedPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var invPlans = await _context.InventoryPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var piercingPlans = await _context.RoundBarPiercingPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        if (semiPlans.Any()) _context.PurchaseSemiPlans.RemoveRange(semiPlans);
        if (finishPlans.Any()) _context.PurchaseFinishedPlans.RemoveRange(finishPlans);
        if (invPlans.Any()) _context.InventoryPlans.RemoveRange(invPlans);
        if (piercingPlans.Any()) _context.RoundBarPiercingPlans.RemoveRange(piercingPlans);

        // 扫描引用该工单号的入库批次，生成通知（已执行数据，不级联）
        var affectedBatches = await _context.InventoryBatches
            .Where(b => b.WorkOrderNo == workOrder.WorkOrderNo)
            .ToListAsync();
        var now = DateTimeOffset.Now;
        foreach (var batch in affectedBatches)
        {
            _context.Notifications.Add(new MES.Data.Entities.Notification
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

        _context.WorkOrders.Remove(workOrder);
        await _context.SaveChangesAsync();
        _logger.LogInformation("删除工单成功: 工单号 {WorkOrderNo}, 清理计划{PC}+{FC}+{IC}条, 生成入库批次通知{N}条",
            workOrder.WorkOrderNo, semiPlans.Count, finishPlans.Count, invPlans.Count, affectedBatches.Count);
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
        await CheckAndUpdateWorkOrderStatusInternalAsync(salesOrderId);
        await _context.SaveChangesAsync();
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

    private async Task<bool> CheckAndUpdateWorkOrderStatusInternalAsync(int salesOrderId)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == salesOrderId);
        if (salesOrder == null || salesOrder.Status != SalesOrderStatus.Confirmed)
            return false;

        var workOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == salesOrder.OrderNumber && wo.Status != WorkOrderStatus.Cancelled)
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
            return true;
        }
        return false;
    }

    #endregion

    #region 订单工单项次追溯

    public async Task<OrderWorkOrderRelationDto> GetOrderWorkOrderRelationAsync(string salesOrderNo)
    {
        // 1. 获取订单信息
        var salesOrderQuery = await _context.SalesOrders
            .Where(so => so.OrderNumber == salesOrderNo)
            .Join(_context.CustomerProfiles,
                so => so.CustomerId,
                c => c.Id,
                (so, c) => new { SalesOrder = so, Customer = c })
            .FirstOrDefaultAsync();

        if (salesOrderQuery == null)
            throw new BusinessException($"订单 {salesOrderNo} 不存在");

        var salesOrder = salesOrderQuery.SalesOrder;
        var customer = salesOrderQuery.Customer;

        // 2. 获取该订单下的所有工单（状态不为已取消的工单）
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.SalesOrderNo == salesOrderNo && wo.Status != WorkOrderStatus.Cancelled)
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
            Salesman = customer.Salesman,
            CustomerName = customer.CustomerUnit,
            EndCustomer = customer.EndCustomer,
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
    LengthStatus = item.LengthStatus.ToString(),
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
    Status = (int)wo.Status,
    StatusText = GetStatusText(wo.Status),
    MaterialName = wo.MaterialName.ToString(),
    StandardGrade = workOrderItems.FirstOrDefault()?.StandardGrade ?? "",
    PlantGrade = wo.PlantGrade,
    Specification = wo.Specification,
    OuterDiameterNegative = wo.OuterDiameterNegative,
    OuterDiameterPositive = wo.OuterDiameterPositive,
    WallThicknessNegative = wo.WallThicknessNegative,
    WallThicknessPositive = wo.WallThicknessPositive,
    DeliveryState = wo.DeliveryState.ToString(),
    LengthStatus = wo.LengthStatus.ToString(),
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
                      && wo.Status != WorkOrderStatus.Cancelled
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
                      && wo.Status != WorkOrderStatus.Cancelled
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
            .Where(so => so.Status == SalesOrderStatus.Confirmed)
            .Join(
                _context.CustomerProfiles,
                so => so.CustomerId,
                c => c.Id,
                (so, c) => new { SalesOrder = so, Customer = c }
            );

        if (!string.IsNullOrEmpty(query.Salesman))
            orderQuery = orderQuery.Where(x => x.Customer.Salesman.Contains(query.Salesman));

        if (!string.IsNullOrEmpty(query.EndCustomer))
            orderQuery = orderQuery.Where(x => x.Customer.EndCustomer != null && x.Customer.EndCustomer.Contains(query.EndCustomer));

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            orderQuery = orderQuery.Where(x =>
                x.SalesOrder.OrderNumber.Contains(keyword) ||
                x.Customer.CustomerUnit.Contains(keyword) ||
                x.Customer.Salesman.Contains(keyword) ||
                (x.Customer.EndCustomer != null && x.Customer.EndCustomer.Contains(keyword)));
        }

        var allOrders = await orderQuery.ToListAsync();
        var allOrderNumbers = allOrders.Select(x => x.SalesOrder.OrderNumber).ToList();

        // 获取关联的所有工单
        var allWorkOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => allOrderNumbers.Contains(wo.SalesOrderNo)
                      && wo.Status != WorkOrderStatus.Cancelled)
            .ToListAsync();

        // 计算每个订单的工单状态并筛选
        var matchedOrderNumbers = new List<string>();
        foreach (var item in allOrders)
        {
            var order = item.SalesOrder;
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

        if (resultWorkOrders.Count == 0)
            throw new BusinessException("没有可打印的工单");

        return WorkOrderPrintHelper.GenerateMultiBatchPdf(resultWorkOrders);
    }

    #endregion

    #region 辅助方法

    private static bool CanTransitionTo(WorkOrderStatus currentStatus, WorkOrderStatus targetStatus)
    {
        if (currentStatus == targetStatus) return true;
        if (currentStatus == WorkOrderStatus.Cancelled) return false;
        if (currentStatus == WorkOrderStatus.NotGenerated) return targetStatus == WorkOrderStatus.Confirmed;
        if (currentStatus == WorkOrderStatus.Confirmed) return targetStatus == WorkOrderStatus.Pending || targetStatus == WorkOrderStatus.Cancelled;
        if (currentStatus == WorkOrderStatus.Pending) return targetStatus == WorkOrderStatus.Confirmed || targetStatus == WorkOrderStatus.Cancelled;
        return false;
    }

    private static string GetStatusText(WorkOrderStatus status)
    {
        return status switch
        {
            WorkOrderStatus.NotGenerated => "未编制",
            WorkOrderStatus.Confirmed => "已确定",
            WorkOrderStatus.Pending => "待修正",
            WorkOrderStatus.Cancelled => "已取消",
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

    #endregion
}