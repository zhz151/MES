// 文件路径: MES.Services/Order/OrderService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Printing;

namespace MES.Services.Order;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OrderService> _logger;
    private readonly INotificationService _notificationService;

    public OrderService(AppDbContext context, ILogger<OrderService> logger, INotificationService notificationService)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
    }

    #region 订单管理

    public async Task<PagedResult<SalesOrderListDto>> GetPagedAsync(QueryParams query, string? technicalStatus = null, string? orderStatus = null)
    {
        bool? hasTechnicalRequirement = technicalStatus?.ToLower() switch
        {
            "edited" => true,
            "notedited" => false,
            _ => null
        };

        List<SalesOrderStatus>? statuses = null;
        if (!string.IsNullOrEmpty(orderStatus))
        {
            var statusStrings = orderStatus.Split(',', StringSplitOptions.RemoveEmptyEntries);
            statuses = new List<SalesOrderStatus>();
            foreach (var s in statusStrings)
            {
                if (Enum.TryParse<SalesOrderStatus>(s, true, out var status))
                    statuses.Add(status);
            }
        }

        var queryable = _context.SalesOrders
            .Include(so => so.Customer)
            .AsNoTracking()
            .AsQueryable();

        // 订单状态筛选
        if (statuses == null || !statuses.Any())
        {
            statuses = new List<SalesOrderStatus> { SalesOrderStatus.Pending, SalesOrderStatus.Confirmed, SalesOrderStatus.Cancelled };
        }
        queryable = queryable.Where(so => statuses.Contains(so.Status));

        // 关键字模糊搜索（多关键词AND + 状态中文映射）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                SalesOrderStatus? parsedStatus = keyword switch
                {
                    "待处理" => SalesOrderStatus.Pending,
                    "已确认" => SalesOrderStatus.Confirmed,
                    _ => null
                };
                queryable = queryable.Where(so =>
                    so.OrderNumber.Contains(keyword) ||
                    _context.CustomerProfiles.Any(c => c.Id == so.CustomerId && c.CustomerUnit.Contains(keyword)) ||
                    _context.CustomerProfiles.Any(c => c.Id == so.CustomerId && c.Salesman.Contains(keyword)) ||
                    _context.CustomerProfiles.Any(c => c.Id == so.CustomerId && c.EndCustomer != null && c.EndCustomer.Contains(keyword)) ||
                    (parsedStatus.HasValue && so.Status == parsedStatus.Value) ||
                    (keyword == "是" && so.OrderItems.Any(oi => oi.DelayPenalty)) ||
                    (keyword == "否" && so.OrderItems.Any(oi => !oi.DelayPenalty)));
            }
        }

        // 技术要求状态筛选
        if (hasTechnicalRequirement.HasValue)
        {
            if (hasTechnicalRequirement.Value)
            {
                // 已编辑：订单下所有项次都有技术要求
                queryable = queryable.Where(so =>
                    _context.OrderItems.Any(oi => oi.SalesOrderId == so.Id) &&
                    !_context.OrderItems.Any(oi => oi.SalesOrderId == so.Id &&
                        !_context.ProductRequirements.Any(pr => pr.OrderItemId == oi.Id)));
            }
            else
            {
                // 未编辑：至少有一个项次没有技术要求
                queryable = queryable.Where(so =>
                    _context.OrderItems.Any(oi => oi.SalesOrderId == so.Id) &&
                    _context.OrderItems.Any(oi => oi.SalesOrderId == so.Id &&
                        !_context.ProductRequirements.Any(pr => pr.OrderItemId == oi.Id)));
            }
        }

        var totalCount = await queryable.CountAsync();

        // 排序
        if (!string.IsNullOrEmpty(query.SortBy))
        {
            switch (query.SortBy.ToLower())
            {
                case "ordernumber":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.OrderNumber) : queryable.OrderBy(so => so.OrderNumber);
                    break;
                case "signdate":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.SignDate) : queryable.OrderBy(so => so.SignDate);
                    break;
                case "status":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.Status) : queryable.OrderBy(so => so.Status);
                    break;
                case "salesman":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.Customer.Salesman) : queryable.OrderBy(so => so.Customer.Salesman);
                    break;
                case "customername":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.Customer.CustomerUnit) : queryable.OrderBy(so => so.Customer.CustomerUnit);
                    break;
                case "endcustomer":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.Customer.EndCustomer ?? "") : queryable.OrderBy(so => so.Customer.EndCustomer ?? "");
                    break;
                case "deliverystart":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.OrderItems.Min(oi => (DateTime?)oi.DeliveryDate)) : queryable.OrderBy(so => so.OrderItems.Min(oi => (DateTime?)oi.DeliveryDate));
                    break;
                case "deliveryend":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.OrderItems.Max(oi => (DateTime?)oi.DeliveryDate)) : queryable.OrderBy(so => so.OrderItems.Max(oi => (DateTime?)oi.DeliveryDate));
                    break;
                case "hasdelaypenalty":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.OrderItems.Any(oi => oi.DelayPenalty)) : queryable.OrderBy(so => so.OrderItems.Any(oi => oi.DelayPenalty));
                    break;
                case "totalcontractweight":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.OrderItems.Sum(oi => oi.ContractWeight)) : queryable.OrderBy(so => so.OrderItems.Sum(oi => oi.ContractWeight));
                    break;
                case "itemcount":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.OrderItems.Count) : queryable.OrderBy(so => so.OrderItems.Count);
                    break;
                case "lastchangedate":
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.LastItemChangeTime) : queryable.OrderBy(so => so.LastItemChangeTime);
                    break;
                default:
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.CreatedTime) : queryable.OrderBy(so => so.CreatedTime);
                    break;
            }
        }
        else
        {
            queryable = queryable.OrderByDescending(so => so.SignDate);
        }

        var salesOrders = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var orderIds = salesOrders.Select(so => so.Id).ToList();
        var customerIds = salesOrders.Select(so => so.CustomerId).Distinct().ToList();
        var customers = await _context.CustomerProfiles
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c);

        var orderItemCounts = await _context.OrderItems
            .Where(oi => orderIds.Contains(oi.SalesOrderId))
            .GroupBy(oi => oi.SalesOrderId)
            .Select(g => new { OrderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrderId, x => x.Count);

        var orderHasReqCounts = await _context.OrderItems
            .Where(oi => orderIds.Contains(oi.SalesOrderId))
            .GroupJoin(
                _context.ProductRequirements,
                oi => oi.Id,
                pr => pr.OrderItemId,
                (oi, prs) => new { oi.SalesOrderId, HasReq = prs.Any() }
            )
            .Where(x => x.HasReq)
            .GroupBy(x => x.SalesOrderId)
            .Select(g => new { OrderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrderId, x => x.Count);

        var firstOrderItemIds = await _context.OrderItems
            .Where(oi => orderIds.Contains(oi.SalesOrderId))
            .GroupBy(oi => oi.SalesOrderId)
            .Select(g => new { OrderId = g.Key, FirstItemId = g.OrderBy(oi => oi.Sequence).Select(oi => (int?)oi.Id).FirstOrDefault() })
            .ToDictionaryAsync(x => x.OrderId, x => x.FirstItemId);

        var orderItemMaxUpdate = await _context.OrderItems
            .Where(oi => orderIds.Contains(oi.SalesOrderId))
            .GroupBy(oi => oi.SalesOrderId)
            .Select(g => new { OrderId = g.Key, MaxUpdate = g.Max(oi => oi.UpdatedTime) })
            .ToDictionaryAsync(x => x.OrderId, x => x.MaxUpdate);

        // 项次聚合数据：交期起始/截止、延期罚款、合同总重量
        var orderItemAggs = await _context.OrderItems
            .Where(oi => orderIds.Contains(oi.SalesOrderId))
            .GroupBy(oi => oi.SalesOrderId)
            .Select(g => new
            {
                OrderId = g.Key,
                DeliveryStart = g.Min(oi => (DateTime?)oi.DeliveryDate),
                DeliveryEnd = g.Max(oi => (DateTime?)oi.DeliveryDate),
                HasDelayPenalty = g.Any(oi => oi.DelayPenalty),
                TotalContractWeight = g.Sum(oi => oi.ContractWeight)
            })
            .ToListAsync();

        var deliveryStartDict = orderItemAggs.ToDictionary(x => x.OrderId, x => x.DeliveryStart);
        var deliveryEndDict = orderItemAggs.ToDictionary(x => x.OrderId, x => x.DeliveryEnd);
        var delayPenaltyDict = orderItemAggs.ToDictionary(x => x.OrderId, x => x.HasDelayPenalty);
        var totalWeightDict = orderItemAggs.ToDictionary(x => x.OrderId, x => (int)Math.Round(x.TotalContractWeight));

        var items = new List<SalesOrderListDto>();
        foreach (var so in salesOrders)
        {
            customers.TryGetValue(so.CustomerId, out var customer);
            var totalItemCount = orderItemCounts.GetValueOrDefault(so.Id);
            var hasReqCount = orderHasReqCounts.GetValueOrDefault(so.Id);
            var hasTechReqFlag = totalItemCount > 0 && hasReqCount == totalItemCount;
            DateTime? lastChangeDate = null;
            if (orderItemMaxUpdate.TryGetValue(so.Id, out var maxUpdate) && maxUpdate > so.CreatedTime)
            {
                lastChangeDate = maxUpdate.LocalDateTime;
            }

            items.Add(new SalesOrderListDto
            {
                Id = so.Id,
                OrderNumber = so.OrderNumber,
                SignDate = so.SignDate,
                CustomerName = customer?.CustomerUnit ?? string.Empty,
                Salesman = customer?.Salesman ?? string.Empty,
                EndCustomer = customer?.EndCustomer,
                DeliveryStart = deliveryStartDict.GetValueOrDefault(so.Id),
                DeliveryEnd = deliveryEndDict.GetValueOrDefault(so.Id),
                HasDelayPenalty = delayPenaltyDict.GetValueOrDefault(so.Id),
                TotalContractWeight = totalWeightDict.GetValueOrDefault(so.Id),
                ItemCount = orderItemCounts.GetValueOrDefault(so.Id),
                Status = so.Status,
                RowVersion = so.RowVersion,
                HasTechnicalRequirement = hasTechReqFlag,
                FirstOrderItemId = firstOrderItemIds.GetValueOrDefault(so.Id),
                LastChangeDate = lastChangeDate
            });
        }

        return new PagedResult<SalesOrderListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<SalesOrderDetailDto> GetByIdAsync(int id)
    {
        // 先查询订单
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .Include(so => so.OrderItems)
            .FirstOrDefaultAsync(so => so.Id == id);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        // 单独加载 Customer
        var customer = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == salesOrder.CustomerId);

        // 单独加载 ProductionStandard
        var psIds = salesOrder.OrderItems
            .Where(oi => oi.ProductionStandardId > 0)
            .Select(oi => oi.ProductionStandardId)
            .Distinct()
            .ToList();
        var psDict = psIds.Any()
            ? await _context.ProductionStandards
                .Where(ps => psIds.Contains(ps.Id))
                .ToDictionaryAsync(ps => ps.Id, ps => ps)
            : new Dictionary<int, ProductionStandard>();

        return new SalesOrderDetailDto
        {
            Id = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            CustomerId = salesOrder.CustomerId,
            CustomerName = customer?.CustomerUnit ?? "未知客户",
            Salesman = customer?.Salesman ?? string.Empty,
            EndCustomer = customer?.EndCustomer,
            Status = salesOrder.Status,
            RowVersion = salesOrder.RowVersion,
            Items = salesOrder.OrderItems.Select(oi =>
            {
                psDict.TryGetValue(oi.ProductionStandardId, out var ps);
                return new OrderItemDto
                {
                    Id = oi.Id,
                    Sequence = oi.Sequence,
                    DeliveryDate = oi.DeliveryDate,
                    DelayPenalty = oi.DelayPenalty,
                    SettlementMethod = oi.SettlementMethod,
                    MaterialName = oi.MaterialName,
                    ProductionStandardCode = ps?.StandardCode ?? string.Empty,
                    DeliveryState = oi.DeliveryState,
                    StandardGrade = oi.StandardGrade,
                    PlantGrade = oi.PlantGrade,
                    Density = oi.Density,
                    OuterDiameter = oi.OuterDiameter,
                    WallThickness = oi.WallThickness,
                    Specification = oi.Specification,
                    OuterDiameterNegative = oi.OuterDiameterNegative,
                    OuterDiameterPositive = oi.OuterDiameterPositive,
                    WallThicknessNegative = oi.WallThicknessNegative,
                    WallThicknessPositive = oi.WallThicknessPositive,
                    LengthStatus = oi.LengthStatus,
                    MinLength = oi.MinLength,
                    MaxLength = oi.MaxLength,
                    Quantity = oi.Quantity,
                    Meters = oi.Meters,
                    ContractWeight = oi.ContractWeight,
                    TheoreticalWeight = oi.TheoreticalWeight,
                    Remark = oi.Remark,
                    CreatedTime = oi.CreatedTime,
                    UpdatedTime = oi.UpdatedTime
                };
            }).ToList()
        };
    }

    public async Task<int?> GetIdByOrderNumberAsync(string orderNo)
    {
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.OrderNumber == orderNo);
        return salesOrder?.Id;
    }

    public async Task<SalesOrderListDto> CreateAsync(CreateSalesOrderRequest request)
    {
        if (await _context.SalesOrders.AnyAsync(so => so.OrderNumber == request.OrderNumber))
            throw new BusinessException("订单号已存在");

        var customer = await _context.CustomerProfiles.FirstOrDefaultAsync(c => c.Id == request.CustomerId);
        if (customer == null)
            throw new BusinessException("客户不存在");

        var salesOrder = new SalesOrder
        {
            OrderNumber = request.OrderNumber,
            SignDate = request.SignDate,
            CustomerId = request.CustomerId,
            Status = SalesOrderStatus.Pending
        };

        var sequence = 1;
        foreach (var itemRequest in request.Items)
        {
            var orderItem = await CreateOrderItemFromCreateRequestAsync(itemRequest, salesOrder.Id, sequence);
            salesOrder.OrderItems.Add(orderItem);
            sequence++;
        }

        _context.SalesOrders.Add(salesOrder);
        await _context.SaveChangesAsync();

        _logger.LogInformation("创建订单成功: {OrderNumber}", salesOrder.OrderNumber);

        return new SalesOrderListDto
        {
            Id = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            CustomerName = customer.CustomerUnit,
            Salesman = customer.Salesman,
            EndCustomer = customer.EndCustomer,
            Status = salesOrder.Status,
            RowVersion = salesOrder.RowVersion
        };
    }

    public async Task<SalesOrderListDto> UpdateAsync(int id, UpdateSalesOrderRequest request)
    {
        var salesOrder = await _context.SalesOrders
            .Include(so => so.Customer)
            .FirstOrDefaultAsync(so => so.Id == id);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Cancelled)
            throw new BusinessException("已取消的订单不能修改");

        if (!string.IsNullOrEmpty(request.OrderNumber) && request.OrderNumber != salesOrder.OrderNumber)
        {
            if (await _context.SalesOrders.AnyAsync(so => so.OrderNumber == request.OrderNumber && so.Id != id))
                throw new BusinessException("订单号已存在");
            salesOrder.OrderNumber = request.OrderNumber;
        }

        if (request.SignDate.HasValue)
            salesOrder.SignDate = request.SignDate.Value;

        if (request.CustomerId.HasValue && request.CustomerId.Value != salesOrder.CustomerId)
        {
            var customer = await _context.CustomerProfiles.FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value);
            if (customer == null)
                throw new BusinessException("客户不存在");
            salesOrder.CustomerId = request.CustomerId.Value;
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            if (!Enum.TryParse<SalesOrderStatus>(request.Status, true, out var newStatus))
                throw new BusinessException($"无效的订单状态: {request.Status}");

            if (!CanTransitionTo(salesOrder.Status, newStatus))
                throw new BusinessException($"不允许从 {GetStatusText(salesOrder.Status)} 变更为 {GetStatusText(newStatus)}");

            salesOrder.Status = newStatus;
        }

        _context.Entry(salesOrder).Property(x => x.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("订单已被其他用户修改，请刷新后重试");
        }

        var updatedCustomer = await _context.CustomerProfiles.FirstOrDefaultAsync(c => c.Id == salesOrder.CustomerId);

        return new SalesOrderListDto
        {
            Id = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            CustomerName = updatedCustomer?.CustomerUnit ?? string.Empty,
            Salesman = updatedCustomer?.Salesman ?? string.Empty,
            EndCustomer = updatedCustomer?.EndCustomer,
            Status = salesOrder.Status,
            RowVersion = salesOrder.RowVersion
        };
    }

public async Task DeleteAsync(int id)
{
    var salesOrder = await _context.SalesOrders
        .Include(so => so.OrderItems)
            .ThenInclude(oi => oi.ProductRequirement)
        .FirstOrDefaultAsync(so => so.Id == id);

    if (salesOrder == null)
        throw new BusinessException("订单不存在");

    if (salesOrder.Status == SalesOrderStatus.Cancelled)
        throw new BusinessException("已取消的订单不能删除");

    // 1. 使用事务确保数据一致性（包含查询和写入）
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // 2. 物理删除订单（级联删除订单项次和产品要求）
        _context.SalesOrders.Remove(salesOrder);

        // 4. 物理删除关联工单（在事务内查询和删除，避免并发窗口）
        var workOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == salesOrder.OrderNumber)
            .ToListAsync();

        var workOrderCount = workOrders.Count;

        if (workOrderCount > 0)
        {
            // 先级联删除工单关联的用料计划（无FK约束，需手动清理）
            var woIds = workOrders.Select(w => w.Id).ToList();
            var workOrderNos = workOrders.Select(w => w.WorkOrderNo).ToHashSet();
            var semiPlans = await _context.PurchaseSemiPlans.Where(p => woIds.Contains(p.WorkOrderId)).ToListAsync();
            var finishPlans = await _context.PurchaseFinishedPlans.Where(p => woIds.Contains(p.WorkOrderId)).ToListAsync();
            var invPlans = await _context.InventoryPlans.Where(p => woIds.Contains(p.WorkOrderId)).ToListAsync();
            var piercingPlans = await _context.RoundBarPiercingPlans.Where(p => woIds.Contains(p.WorkOrderId)).ToListAsync();
            if (semiPlans.Any()) _context.PurchaseSemiPlans.RemoveRange(semiPlans);
            if (finishPlans.Any()) _context.PurchaseFinishedPlans.RemoveRange(finishPlans);
            if (invPlans.Any()) _context.InventoryPlans.RemoveRange(invPlans);
            if (piercingPlans.Any()) _context.RoundBarPiercingPlans.RemoveRange(piercingPlans);

            // 扫描引用这些工单号的入库批次，生成通知（已执行数据，不级联）
            var affectedBatches = await _context.InventoryBatches
                .Where(b => b.WorkOrderNo != null && workOrderNos.Contains(b.WorkOrderNo))
                .ToListAsync();
            var now = DateTimeOffset.Now;
            foreach (var batch in affectedBatches)
            {
                _context.Notifications.Add(new MES.Data.Entities.Notification
                {
                    NotificationType = "WorkOrderDeleted",
                    TargetId = batch.Id,
                    Title = $"工单 {batch.WorkOrderNo} 已删除（订单 {salesOrder.OrderNumber} 被删除）",
                    Content = $"入库批次 {batch.BatchNo}（{batch.MaterialType} {batch.Specification}）仍引用该工单，请及时处理",
                    IsRead = false,
                    Receiver = string.Empty,
                    CreatedTime = now
                });
            }

            _context.WorkOrders.RemoveRange(workOrders);
        }

        await _context.SaveChangesAsync();

        // 5. 生成统一通知（告知已自动清理工单）
        if (workOrderCount > 0)
        {
            _context.Notifications.Add(new Notification
            {
                NotificationType = "OrderDeleted",
                Title = string.Empty,
                Content = $"⚠️ 订单 {salesOrder.OrderNumber} 已删除，已自动清理 {workOrderCount} 个关联工单。",
                IsRead = false,
                Receiver = string.Empty,
                CreatedTime = DateTimeOffset.Now
            });
        }

        await transaction.CommitAsync();

        _logger.LogInformation("订单 {OrderNumber} 已被删除，同时自动清理了 {Count} 个关联工单",
            salesOrder.OrderNumber, workOrderCount);
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}

    #endregion

    #region 项次管理

    public async Task<OrderItemDto> AddItemAsync(int orderId, AddOrderItemRequest request)
    {
        var salesOrder = await _context.SalesOrders
            .Include(so => so.OrderItems)
            .FirstOrDefaultAsync(so => so.Id == orderId);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        var allSequences = await _context.OrderItems
            .Where(oi => oi.SalesOrderId == orderId)
            .Select(oi => oi.Sequence)
            .ToListAsync();

        int sequence;
        if (request.Sequence.HasValue && request.Sequence.Value > 0)
        {
            sequence = request.Sequence.Value;
            if (allSequences.Contains(sequence))
                throw new BusinessException($"项次号 {sequence} 已存在");
        }
        else
        {
            sequence = 1;
            while (allSequences.Contains(sequence))
                sequence++;
        }

        var orderItem = await CreateOrderItemFromAddRequestAsync(request, salesOrder.Id, sequence);
        _context.OrderItems.Add(orderItem);

        // 更新订单的最后项次变更时间
        salesOrder.LastItemChangeTime = DateTimeOffset.Now;
        _context.Entry(salesOrder).Property(x => x.LastItemChangeTime).IsModified = true;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();

            if (orderItem.ProductionStandard == null && orderItem.ProductionStandardId > 0)
            {
                orderItem.ProductionStandard = await _context.ProductionStandards
                    .FirstOrDefaultAsync(ps => ps.Id == orderItem.ProductionStandardId) ?? null!;
            }

            await CreateItemChangedNotificationIfNeededAsync(orderId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return await MapToOrderItemDto(orderItem);
    }

    public async Task<OrderItemDto> UpdateItemAsync(int orderId, int itemId, UpdateOrderItemRequest request)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == orderId);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Cancelled)
            throw new BusinessException("已取消的订单不能修改项次");

        var orderItem = await _context.OrderItems
            .Include(oi => oi.ProductionStandard)
            .FirstOrDefaultAsync(oi => oi.Id == itemId && oi.SalesOrderId == orderId);

        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        if (request.Sequence != orderItem.Sequence)
        {
            var exists = await _context.OrderItems
                .AnyAsync(oi => oi.SalesOrderId == orderId && oi.Sequence == request.Sequence && oi.Id != itemId);
            if (exists)
                throw new BusinessException($"项次号 {request.Sequence} 已存在");
            orderItem.Sequence = request.Sequence;
        }

        var gradeMapping = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(sgm => sgm.StandardGrade == request.StandardGrade);
        if (gradeMapping == null)
            throw new BusinessException($"标准牌号 '{request.StandardGrade}' 不存在");

        ValidateLengthStatus(request.LengthStatus, request.MinLength, request.MaxLength);

        var normalizedOuterDiameter = NormalizeDecimalValue(request.OuterDiameter);
        var normalizedWallThickness = NormalizeDecimalValue(request.WallThickness);
        var normalizedOuterDiameterNegative = NormalizeDecimalValue(request.OuterDiameterNegative);
        var normalizedOuterDiameterPositive = NormalizeDecimalValue(request.OuterDiameterPositive);
        var normalizedWallThicknessNegative = NormalizeDecimalValue(request.WallThicknessNegative);
        var normalizedWallThicknessPositive = NormalizeDecimalValue(request.WallThicknessPositive);
        var normalizedContractWeight = NormalizeDecimalValue(request.ContractWeight);

        var meters = CalculateMeters(request.LengthStatus, request.MinLength, request.MaxLength, request.Quantity, request.Meters);
        var metersValue = meters ?? 0m;
        var theoreticalWeight = CalculateTheoreticalWeight(
            gradeMapping.Density,
            normalizedOuterDiameter,
            normalizedWallThickness,
            normalizedOuterDiameterNegative, normalizedOuterDiameterPositive,
            normalizedWallThicknessNegative, normalizedWallThicknessPositive,
            metersValue);

        var productionStandard = await _context.ProductionStandards
            .FirstOrDefaultAsync(ps => ps.Id == request.ProductionStandardId);
        if (productionStandard == null)
            throw new BusinessException("产品标准不存在");

        SetOrderItemFields(orderItem,
            deliveryDate: request.DeliveryDate,
            delayPenalty: request.DelayPenalty,
            settlementMethod: request.SettlementMethod,
            materialName: request.MaterialName,
            productionStandardId: request.ProductionStandardId,
            deliveryState: request.DeliveryState,
            standardGrade: request.StandardGrade,
            plantGrade: gradeMapping.PlantGrade,
            density: gradeMapping.Density,
            outerDiameter: normalizedOuterDiameter,
            wallThickness: normalizedWallThickness,
            specification: $"{normalizedOuterDiameter}*{normalizedWallThickness}",
            outerDiameterNegative: normalizedOuterDiameterNegative,
            outerDiameterPositive: normalizedOuterDiameterPositive,
            wallThicknessNegative: normalizedWallThicknessNegative,
            wallThicknessPositive: normalizedWallThicknessPositive,
            lengthStatus: request.LengthStatus,
            minLength: request.MinLength,
            maxLength: CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength),
            quantity: request.Quantity,
            meters: meters,
            contractWeight: normalizedContractWeight,
            theoreticalWeight: theoreticalWeight,
            remark: request.Remark);

        // 更新订单的最后项次变更时间
        salesOrder.LastItemChangeTime = DateTimeOffset.Now;
        _context.Entry(salesOrder).Property(x => x.LastItemChangeTime).IsModified = true;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();
            await CreateItemChangedNotificationIfNeededAsync(orderId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return await MapToOrderItemDto(orderItem);
    }

    public async Task DeleteItemAsync(int orderId, int itemId)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == orderId);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Cancelled)
            throw new BusinessException("已取消的订单不能删除项次");

        var orderItem = await _context.OrderItems
            .Include(oi => oi.ProductRequirement)
            .FirstOrDefaultAsync(oi => oi.Id == itemId && oi.SalesOrderId == orderId);

        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        _context.OrderItems.Remove(orderItem);

        // 更新订单的最后项次变更时间
        salesOrder.LastItemChangeTime = DateTimeOffset.Now;
        _context.Entry(salesOrder).Property(x => x.LastItemChangeTime).IsModified = true;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();
            await CreateItemChangedNotificationIfNeededAsync(orderId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SaveAllOrderResponse> SaveAllAsync(int id, SaveAllOrderRequest request)
    {
        // 1. 加载订单（含全部现有项次）
        var salesOrder = await _context.SalesOrders
            .Include(so => so.OrderItems)
            .FirstOrDefaultAsync(so => so.Id == id);
        if (salesOrder == null)
            throw new BusinessException("订单不存在");
        if (salesOrder.Status == SalesOrderStatus.Cancelled)
            throw new BusinessException("已取消的订单不能修改");

        // 2. RowVersion 乐观并发检查
        _context.Entry(salesOrder).Property(x => x.RowVersion).OriginalValue = request.RowVersion;

        // 3. 批量加载引用数据（消除 N+1）
        var allPsIds = request.NewItems.Concat(request.UpdatedItems)
            .Select(i => i.ProductionStandardId).Distinct().ToList();
        var allGradeNames = request.NewItems.Concat(request.UpdatedItems)
            .Select(i => i.StandardGrade).Distinct().ToList();

        var psDict = allPsIds.Any()
            ? await _context.ProductionStandards.Where(ps => allPsIds.Contains(ps.Id))
                .ToDictionaryAsync(ps => ps.Id, ps => ps)
            : new Dictionary<int, ProductionStandard>();
        var gradeDict = allGradeNames.Any()
            ? await _context.StandardGradeMappings.Where(sgm => allGradeNames.Contains(sgm.StandardGrade))
                .ToDictionaryAsync(sgm => sgm.StandardGrade, sgm => sgm)
            : new Dictionary<string, StandardGradeMapping>();

        var existingItems = salesOrder.OrderItems.ToDictionary(oi => oi.Id);

        // 4. 验证
        foreach (var deleteId in request.DeletedItemIds)
            if (!existingItems.ContainsKey(deleteId))
                throw new BusinessException($"要删除的项次 ID={deleteId} 不存在");

        var remainingCount = existingItems.Count - request.DeletedItemIds.Count + request.NewItems.Count;
        if (remainingCount < 1)
            throw new BusinessException("订单至少需要包含一个项次");

        foreach (var itemReq in request.NewItems.Concat(request.UpdatedItems))
        {
            if (!psDict.ContainsKey(itemReq.ProductionStandardId))
                throw new BusinessException($"产品标准 ID={itemReq.ProductionStandardId} 不存在");
            if (!gradeDict.ContainsKey(itemReq.StandardGrade))
                throw new BusinessException($"标准牌号 '{itemReq.StandardGrade}' 不存在");
            ValidateLengthStatus(itemReq.LengthStatus, itemReq.MinLength, itemReq.MaxLength);
        }

        // 5. 单事务处理
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 5a. 删除项次
            foreach (var deleteId in request.DeletedItemIds)
                _context.OrderItems.Remove(existingItems[deleteId]);

            // 5b. 更新现有项次
            var keptItemIds = existingItems.Keys.Where(id => !request.DeletedItemIds.Contains(id)).ToHashSet();
            var existingSequences = existingItems.Values
                .Where(oi => keptItemIds.Contains(oi.Id))
                .Select(oi => oi.Sequence)
                .ToHashSet();

            foreach (var updateReq in request.UpdatedItems)
            {
                if (!existingItems.TryGetValue(updateReq.Id, out var orderItem))
                    throw new BusinessException($"要更新的项次 ID={updateReq.Id} 不存在");

                var gradeMapping = gradeDict[updateReq.StandardGrade];

                // Sequence 冲突检查（排除自身）
                if (updateReq.Sequence != orderItem.Sequence)
                {
                    var otherOccupied = existingItems.Values
                        .Any(oi => oi.Id != updateReq.Id && keptItemIds.Contains(oi.Id) && oi.Sequence == updateReq.Sequence);
                    if (otherOccupied)
                        throw new BusinessException($"项次号 {updateReq.Sequence} 已被其他项次占用");
                    orderItem.Sequence = updateReq.Sequence;
                }

                // 归一化 + 计算
                var normalizedOd = NormalizeDecimalValue(updateReq.OuterDiameter);
                var normalizedWt = NormalizeDecimalValue(updateReq.WallThickness);
                var normalizedOdNeg = NormalizeDecimalValue(updateReq.OuterDiameterNegative);
                var normalizedOdPos = NormalizeDecimalValue(updateReq.OuterDiameterPositive);
                var normalizedWtNeg = NormalizeDecimalValue(updateReq.WallThicknessNegative);
                var normalizedWtPos = NormalizeDecimalValue(updateReq.WallThicknessPositive);
                var normalizedCw = NormalizeDecimalValue(updateReq.ContractWeight);

                var meters = CalculateMeters(updateReq.LengthStatus, updateReq.MinLength, updateReq.MaxLength, updateReq.Quantity, updateReq.Meters);
                var metersValue = meters ?? 0m;
                var theoreticalWeight = CalculateTheoreticalWeight(
                    gradeMapping.Density, normalizedOd, normalizedWt,
                    normalizedOdNeg, normalizedOdPos, normalizedWtNeg, normalizedWtPos, metersValue);

                if (updateReq.LengthStatus == LengthStatus.Fixed && theoreticalWeight > 0)
                    ValidateContractWeightAgainstTheoreticalWeight(normalizedCw, theoreticalWeight);

                SetOrderItemFields(orderItem,
                    deliveryDate: updateReq.DeliveryDate, delayPenalty: updateReq.DelayPenalty,
                    settlementMethod: updateReq.SettlementMethod, materialName: updateReq.MaterialName,
                    productionStandardId: updateReq.ProductionStandardId, deliveryState: updateReq.DeliveryState,
                    standardGrade: updateReq.StandardGrade, plantGrade: gradeMapping.PlantGrade,
                    density: gradeMapping.Density, outerDiameter: normalizedOd, wallThickness: normalizedWt,
                    specification: $"{normalizedOd}*{normalizedWt}",
                    outerDiameterNegative: normalizedOdNeg, outerDiameterPositive: normalizedOdPos,
                    wallThicknessNegative: normalizedWtNeg, wallThicknessPositive: normalizedWtPos,
                    lengthStatus: updateReq.LengthStatus, minLength: updateReq.MinLength,
                    maxLength: CalculateMaxLength(updateReq.LengthStatus, updateReq.MinLength, updateReq.MaxLength),
                    quantity: updateReq.Quantity, meters: meters, contractWeight: normalizedCw,
                    theoreticalWeight: theoreticalWeight, remark: updateReq.Remark);
            }

            // 5c. 新增项次
            var newItemIdMap = new Dictionary<int, int>();
            var allNewItems = new List<(int Index, OrderItem Entity)>();
            var nextSequence = existingSequences.Any() ? existingSequences.Max() + 1 : 1;
            // 考虑更新后的 Sequence 可能占用更大的值
            foreach (var u in request.UpdatedItems)
                if (u.Sequence >= nextSequence) nextSequence = u.Sequence + 1;

            for (int i = 0; i < request.NewItems.Count; i++)
            {
                var newReq = request.NewItems[i];
                var gradeMapping = gradeDict[newReq.StandardGrade];
                var sequence = nextSequence + i;

                var orderItem = new OrderItem { SalesOrderId = salesOrder.Id, Sequence = sequence };
                var normalizedOd = NormalizeDecimalValue(newReq.OuterDiameter);
                var normalizedWt = NormalizeDecimalValue(newReq.WallThickness);
                var normalizedOdNeg = NormalizeDecimalValue(newReq.OuterDiameterNegative);
                var normalizedOdPos = NormalizeDecimalValue(newReq.OuterDiameterPositive);
                var normalizedWtNeg = NormalizeDecimalValue(newReq.WallThicknessNegative);
                var normalizedWtPos = NormalizeDecimalValue(newReq.WallThicknessPositive);
                var normalizedCw = NormalizeDecimalValue(newReq.ContractWeight);

                var meters = CalculateMeters(newReq.LengthStatus, newReq.MinLength, newReq.MaxLength, newReq.Quantity, newReq.Meters);
                var metersValue = meters ?? 0m;
                var theoreticalWeight = CalculateTheoreticalWeight(
                    gradeMapping.Density, normalizedOd, normalizedWt,
                    normalizedOdNeg, normalizedOdPos, normalizedWtNeg, normalizedWtPos, metersValue);

                if (newReq.LengthStatus == LengthStatus.Fixed && theoreticalWeight > 0)
                    ValidateContractWeightAgainstTheoreticalWeight(normalizedCw, theoreticalWeight);

                SetOrderItemFields(orderItem,
                    deliveryDate: newReq.DeliveryDate, delayPenalty: newReq.DelayPenalty,
                    settlementMethod: newReq.SettlementMethod, materialName: newReq.MaterialName,
                    productionStandardId: newReq.ProductionStandardId, deliveryState: newReq.DeliveryState,
                    standardGrade: newReq.StandardGrade, plantGrade: gradeMapping.PlantGrade,
                    density: gradeMapping.Density, outerDiameter: normalizedOd, wallThickness: normalizedWt,
                    specification: $"{normalizedOd}*{normalizedWt}",
                    outerDiameterNegative: normalizedOdNeg, outerDiameterPositive: normalizedOdPos,
                    wallThicknessNegative: normalizedWtNeg, wallThicknessPositive: normalizedWtPos,
                    lengthStatus: newReq.LengthStatus, minLength: newReq.MinLength,
                    maxLength: CalculateMaxLength(newReq.LengthStatus, newReq.MinLength, newReq.MaxLength),
                    quantity: newReq.Quantity, meters: meters, contractWeight: normalizedCw,
                    theoreticalWeight: theoreticalWeight, remark: newReq.Remark);

                _context.OrderItems.Add(orderItem);
                allNewItems.Add((i, orderItem));
            }

            // 5d. 更新订单头
            if (!string.IsNullOrEmpty(request.OrderNumber) && request.OrderNumber != salesOrder.OrderNumber)
            {
                if (await _context.SalesOrders.AnyAsync(so => so.OrderNumber == request.OrderNumber && so.Id != id))
                    throw new BusinessException("订单号已存在");
                salesOrder.OrderNumber = request.OrderNumber;
            }
            if (request.SignDate.HasValue)
                salesOrder.SignDate = request.SignDate.Value;
            if (request.CustomerId.HasValue && request.CustomerId.Value != salesOrder.CustomerId)
            {
                var customer = await _context.CustomerProfiles.FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value);
                if (customer == null) throw new BusinessException("客户不存在");
                salesOrder.CustomerId = request.CustomerId.Value;
            }

            salesOrder.LastItemChangeTime = DateTimeOffset.Now;
            _context.Entry(salesOrder).Property(x => x.LastItemChangeTime).IsModified = true;

            // 5e. SaveChanges（触发 RowVersion 乐观并发检查）
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new BusinessException("订单已被其他用户修改，请刷新后重试");
            }

            // 5f. 构建新项次 ID 映射（SaveChanges 后 EF 自动填入 Id）
            foreach (var (index, entity) in allNewItems)
                newItemIdMap[index] = entity.Id;

            // 5g. 统一创建通知（仅在订单状态为 Confirmed 时，同一事务内）
            await CreateItemChangedNotificationIfNeededAsync(salesOrder.Id);

            await transaction.CommitAsync();

            // 6. 构建响应
            _logger.LogInformation("批量保存订单成功: {OrderNumber}, 新增={NewCount}, 更新={UpdateCount}, 删除={DeleteCount}",
                salesOrder.OrderNumber, request.NewItems.Count, request.UpdatedItems.Count, request.DeletedItemIds.Count);

            var resultItems = salesOrder.OrderItems
                .Where(oi => !request.DeletedItemIds.Contains(oi.Id))
                .Select(oi => new OrderItemSaveResult
                {
                    Id = oi.Id,
                    Sequence = oi.Sequence,
                    Meters = oi.Meters ?? 0m,
                    TheoreticalWeight = oi.TheoreticalWeight
                })
                .OrderBy(r => r.Sequence)
                .ToList();

            return new SaveAllOrderResponse
            {
                RowVersion = salesOrder.RowVersion,
                NewItemIdMap = newItemIdMap,
                Items = resultItems
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    #endregion

    #region Private Methods

    private async Task<OrderItem> CreateOrderItemFromCreateRequestAsync(CreateOrderItemRequest request, int salesOrderId, int sequence)
    {
        var productionStandard = await _context.ProductionStandards
            .FirstOrDefaultAsync(ps => ps.Id == request.ProductionStandardId);
        if (productionStandard == null)
            throw new BusinessException("产品标准不存在");

        var gradeMapping = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(sgm => sgm.StandardGrade == request.StandardGrade);
        if (gradeMapping == null)
            throw new BusinessException($"标准牌号 '{request.StandardGrade}' 不存在");

        ValidateLengthStatus(request.LengthStatus, request.MinLength, request.MaxLength);

        var normalizedOuterDiameter = NormalizeDecimalValue(request.OuterDiameter);
        var normalizedWallThickness = NormalizeDecimalValue(request.WallThickness);
        var normalizedOuterDiameterNegative = NormalizeDecimalValue(request.OuterDiameterNegative);
        var normalizedOuterDiameterPositive = NormalizeDecimalValue(request.OuterDiameterPositive);
        var normalizedWallThicknessNegative = NormalizeDecimalValue(request.WallThicknessNegative);
        var normalizedWallThicknessPositive = NormalizeDecimalValue(request.WallThicknessPositive);
        var normalizedContractWeight = NormalizeDecimalValue(request.ContractWeight);

        var meters = CalculateMeters(request.LengthStatus, request.MinLength, request.MaxLength, request.Quantity, request.Meters);
        var metersValue = meters ?? 0m;
        var theoreticalWeight = CalculateTheoreticalWeight(
            gradeMapping.Density,
            normalizedOuterDiameter,
            normalizedWallThickness,
            normalizedOuterDiameterNegative, normalizedOuterDiameterPositive,
            normalizedWallThicknessNegative, normalizedWallThicknessPositive,
            metersValue);

        // 验证合同重量与理算重量的关系
        if (request.LengthStatus == LengthStatus.Fixed && theoreticalWeight > 0)
        {
            ValidateContractWeightAgainstTheoreticalWeight(normalizedContractWeight, theoreticalWeight);
        }

        var item = new OrderItem { SalesOrderId = salesOrderId, Sequence = sequence };
        SetOrderItemFields(item,
            deliveryDate: request.DeliveryDate,
            delayPenalty: request.DelayPenalty,
            settlementMethod: request.SettlementMethod,
            materialName: request.MaterialName,
            productionStandardId: request.ProductionStandardId,
            deliveryState: request.DeliveryState,
            standardGrade: request.StandardGrade,
            plantGrade: gradeMapping.PlantGrade,
            density: gradeMapping.Density,
            outerDiameter: normalizedOuterDiameter,
            wallThickness: normalizedWallThickness,
            specification: $"{normalizedOuterDiameter}*{normalizedWallThickness}",
            outerDiameterNegative: normalizedOuterDiameterNegative,
            outerDiameterPositive: normalizedOuterDiameterPositive,
            wallThicknessNegative: normalizedWallThicknessNegative,
            wallThicknessPositive: normalizedWallThicknessPositive,
            lengthStatus: request.LengthStatus,
            minLength: request.MinLength,
            maxLength: CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength),
            quantity: request.Quantity,
            meters: meters,
            contractWeight: normalizedContractWeight,
            theoreticalWeight: theoreticalWeight,
            remark: request.Remark);
        return item;
    }

    private async Task<OrderItem> CreateOrderItemFromAddRequestAsync(AddOrderItemRequest request, int salesOrderId, int sequence)
    {
        var productionStandard = await _context.ProductionStandards
            .FirstOrDefaultAsync(ps => ps.Id == request.ProductionStandardId);
        if (productionStandard == null)
            throw new BusinessException("产品标准不存在");

        var gradeMapping = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(sgm => sgm.StandardGrade == request.StandardGrade);
        if (gradeMapping == null)
            throw new BusinessException($"标准牌号 '{request.StandardGrade}' 不存在");

        ValidateLengthStatus(request.LengthStatus, request.MinLength, request.MaxLength);

        var normalizedOuterDiameter = NormalizeDecimalValue(request.OuterDiameter);
        var normalizedWallThickness = NormalizeDecimalValue(request.WallThickness);
        var normalizedOuterDiameterNegative = NormalizeDecimalValue(request.OuterDiameterNegative);
        var normalizedOuterDiameterPositive = NormalizeDecimalValue(request.OuterDiameterPositive);
        var normalizedWallThicknessNegative = NormalizeDecimalValue(request.WallThicknessNegative);
        var normalizedWallThicknessPositive = NormalizeDecimalValue(request.WallThicknessPositive);
        var normalizedContractWeight = NormalizeDecimalValue(request.ContractWeight);

        var meters = CalculateMeters(request.LengthStatus, request.MinLength, request.MaxLength, request.Quantity, request.Meters);
        var metersValue = meters ?? 0m;
        var theoreticalWeight = CalculateTheoreticalWeight(
            gradeMapping.Density,
            normalizedOuterDiameter,
            normalizedWallThickness,
            normalizedOuterDiameterNegative, normalizedOuterDiameterPositive,
            normalizedWallThicknessNegative, normalizedWallThicknessPositive,
            metersValue);

        // 验证合同重量与理算重量的关系
        if (request.LengthStatus == LengthStatus.Fixed && theoreticalWeight > 0)
        {
            ValidateContractWeightAgainstTheoreticalWeight(normalizedContractWeight, theoreticalWeight);
        }

        var item = new OrderItem { SalesOrderId = salesOrderId, Sequence = sequence };
        SetOrderItemFields(item,
            deliveryDate: request.DeliveryDate,
            delayPenalty: request.DelayPenalty,
            settlementMethod: request.SettlementMethod,
            materialName: request.MaterialName,
            productionStandardId: request.ProductionStandardId,
            deliveryState: request.DeliveryState,
            standardGrade: request.StandardGrade,
            plantGrade: gradeMapping.PlantGrade,
            density: gradeMapping.Density,
            outerDiameter: normalizedOuterDiameter,
            wallThickness: normalizedWallThickness,
            specification: $"{normalizedOuterDiameter}*{normalizedWallThickness}",
            outerDiameterNegative: normalizedOuterDiameterNegative,
            outerDiameterPositive: normalizedOuterDiameterPositive,
            wallThicknessNegative: normalizedWallThicknessNegative,
            wallThicknessPositive: normalizedWallThicknessPositive,
            lengthStatus: request.LengthStatus,
            minLength: request.MinLength,
            maxLength: CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength),
            quantity: request.Quantity,
            meters: meters,
            contractWeight: normalizedContractWeight,
            theoreticalWeight: theoreticalWeight,
            remark: request.Remark);
        return item;
    }

    private static decimal NormalizeDecimalValue(decimal value)
    {
        return decimal.Parse(value.ToString("G29"));
    }

    /// <summary>
    /// 验证长度状态
    /// </summary>
    private static void ValidateLengthStatus(LengthStatus lengthStatus, decimal? minLength, decimal? maxLength)
    {
        switch (lengthStatus)
        {
            case LengthStatus.Fixed:
                if (!minLength.HasValue || minLength <= 0)
                    throw new BusinessException("定尺时必须填写长度");
                
                // 新增：定尺模式下最小长度必须等于最大长度
                if (!maxLength.HasValue || maxLength.Value != minLength.Value)
                    throw new BusinessException("定尺模式下最小长度必须等于最大长度");
                break;
                
            case LengthStatus.Range:
                if (!minLength.HasValue || minLength <= 0 || !maxLength.HasValue || maxLength <= 0 || maxLength <= minLength)
                    throw new BusinessException("范围尺时必须填写最小长度和最大长度，且最大长度必须大于最小长度");
                break;
        }
    }

    /// <summary>
    /// 验证合同重量与理算重量的关系
    /// </summary>
    private static void ValidateContractWeightAgainstTheoreticalWeight(decimal contractWeight, decimal theoreticalWeight)
    {
        if (theoreticalWeight <= 0) return;
        
        var ratio = contractWeight / theoreticalWeight;
        var lowerBound = 0.94m;   // 94%
        var upperBound = 1.06m;   // 106%
        
        if (ratio < lowerBound)
        {
            throw new BusinessException($"合同重量 {contractWeight:G29} kg 低于理算重量 {theoreticalWeight:G29} kg 的94%，可能亏损");
        }
        
        if (ratio > upperBound)
        {
            throw new BusinessException($"合同重量 {contractWeight:G29} kg 高于理算重量 {theoreticalWeight:G29} kg 的106%");
        }
    }

    private static decimal? CalculateMeters(LengthStatus lengthStatus, decimal? minLength, decimal? maxLength, int? quantity, decimal? meters)
    {
        switch (lengthStatus)
        {
            case LengthStatus.Fixed:
                if (quantity.HasValue && quantity > 0 && maxLength.HasValue && maxLength > 0)
                    return Math.Round(maxLength.Value * quantity.Value / 1000, 2);
                return null;
            case LengthStatus.Range:
            case LengthStatus.NonFixed:
                return meters.HasValue ? Math.Round(meters.Value, 2) : 0;
            default:
                return 0;
        }
    }

    private static decimal CalculateMaxLength(LengthStatus lengthStatus, decimal? minLength, decimal? maxLength)
    {
        switch (lengthStatus)
        {
            case LengthStatus.Fixed:
                return minLength ?? 0;
            case LengthStatus.Range:
                return maxLength ?? 0;
            default:
                return 0;
        }
    }

    private static decimal CalculateTheoreticalWeight(
        decimal density,
        decimal outerDiameter,
        decimal wallThickness,
        decimal outerDiameterNegative,
        decimal outerDiameterPositive,
        decimal wallThicknessNegative,
        decimal wallThicknessPositive,
        decimal meters)
    {
        const decimal pi = 3.1416m;
        var effectiveWallThickness = wallThickness - 0.5m * wallThicknessNegative + 0.5m * wallThicknessPositive;
        var effectiveOuterDiameter = outerDiameter - 0.5m * outerDiameterNegative + 0.5m * outerDiameterPositive;

        if (effectiveWallThickness < 0) effectiveWallThickness = 0;
        if (effectiveOuterDiameter <= effectiveWallThickness)
            effectiveOuterDiameter = effectiveWallThickness + 0.001m;

        var weight = density * pi * effectiveWallThickness * (effectiveOuterDiameter - effectiveWallThickness) * meters / 1000;
        if (weight < 0) weight = 0;
        return Math.Round(weight, 2);
    }

    private static void SetOrderItemFields(OrderItem item,
        DateTime deliveryDate, bool delayPenalty, SettlementMethod settlementMethod, MaterialName materialName,
        int productionStandardId, DeliveryState deliveryState, string standardGrade, string plantGrade,
        decimal density, decimal outerDiameter, decimal wallThickness, string specification,
        decimal outerDiameterNegative, decimal outerDiameterPositive, decimal wallThicknessNegative,
        decimal wallThicknessPositive, LengthStatus lengthStatus, decimal? minLength, decimal? maxLength,
        int? quantity, decimal? meters, decimal contractWeight, decimal theoreticalWeight, string? remark)
    {
        item.DeliveryDate = deliveryDate;
        item.DelayPenalty = delayPenalty;
        item.SettlementMethod = settlementMethod;
        item.MaterialName = materialName;
        item.ProductionStandardId = productionStandardId;
        item.DeliveryState = deliveryState;
        item.StandardGrade = standardGrade;
        item.PlantGrade = plantGrade;
        item.Density = density;
        item.OuterDiameter = outerDiameter;
        item.WallThickness = wallThickness;
        item.Specification = specification;
        item.OuterDiameterNegative = outerDiameterNegative;
        item.OuterDiameterPositive = outerDiameterPositive;
        item.WallThicknessNegative = wallThicknessNegative;
        item.WallThicknessPositive = wallThicknessPositive;
        item.LengthStatus = lengthStatus;
        item.MinLength = minLength ?? item.MinLength;
        item.MaxLength = maxLength ?? item.MaxLength;
        item.Quantity = quantity ?? item.Quantity;
        item.Meters = meters ?? item.Meters;
        item.ContractWeight = contractWeight;
        item.TheoreticalWeight = theoreticalWeight;
        item.Remark = remark ?? item.Remark;
    }

    private async Task<OrderItemDto> MapToOrderItemDto(OrderItem orderItem)
    {
        if (orderItem.ProductionStandard == null && orderItem.ProductionStandardId > 0)
        {
            orderItem.ProductionStandard = await _context.ProductionStandards
                .FirstOrDefaultAsync(ps => ps.Id == orderItem.ProductionStandardId) ?? null!;
        }

        return new OrderItemDto
        {
            Id = orderItem.Id,
            Sequence = orderItem.Sequence,
            DeliveryDate = orderItem.DeliveryDate,
            DelayPenalty = orderItem.DelayPenalty,
            SettlementMethod = orderItem.SettlementMethod,
            MaterialName = orderItem.MaterialName,
            ProductionStandardCode = orderItem.ProductionStandard?.StandardCode ?? string.Empty,
            DeliveryState = orderItem.DeliveryState,
            StandardGrade = orderItem.StandardGrade,
            PlantGrade = orderItem.PlantGrade,
            Density = orderItem.Density,
            OuterDiameter = orderItem.OuterDiameter,
            WallThickness = orderItem.WallThickness,
            Specification = orderItem.Specification,
            OuterDiameterNegative = orderItem.OuterDiameterNegative,
            OuterDiameterPositive = orderItem.OuterDiameterPositive,
            WallThicknessNegative = orderItem.WallThicknessNegative,
            WallThicknessPositive = orderItem.WallThicknessPositive,
            LengthStatus = orderItem.LengthStatus,
            MinLength = orderItem.MinLength,
            MaxLength = orderItem.MaxLength,
            Quantity = orderItem.Quantity,
            Meters = orderItem.Meters,
            ContractWeight = orderItem.ContractWeight,
            TheoreticalWeight = orderItem.TheoreticalWeight,
            Remark = orderItem.Remark,
            CreatedTime = orderItem.CreatedTime,
            UpdatedTime = orderItem.UpdatedTime
        };
    }

    private static bool CanTransitionTo(SalesOrderStatus current, SalesOrderStatus target)
    {
        if (current == target) return true;
        if (current == SalesOrderStatus.Cancelled) return false;
        if (current == SalesOrderStatus.Pending)
            return target == SalesOrderStatus.Confirmed || target == SalesOrderStatus.Cancelled;
        if (current == SalesOrderStatus.Confirmed)
            return target == SalesOrderStatus.Cancelled;
        return false;
    }

    private static string GetStatusText(SalesOrderStatus status) => status switch
    {
        SalesOrderStatus.Pending => "待处理",
        SalesOrderStatus.Confirmed => "已确认",
        SalesOrderStatus.Cancelled => "已取消",
        _ => status.ToString()
    };

    private async Task CreateItemChangedNotificationIfNeededAsync(int salesOrderId)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == salesOrderId);
        if (salesOrder == null || salesOrder.Status != SalesOrderStatus.Confirmed)
            return;

        var hasRecent = await _notificationService.HasRecentItemChangedNotificationAsync(salesOrder.OrderNumber, 5);
        if (hasRecent) return;

        _context.Notifications.Add(new Notification
        {
            NotificationType = "OrderChanged",
            Title = string.Empty,
            Content = $"⚠️ 订单 {salesOrder.OrderNumber} 已更新，关联工单需要同步更新。",
            IsRead = false,
            Receiver = string.Empty,
            CreatedTime = DateTimeOffset.Now
        });
        await _context.SaveChangesAsync();
    }

    #endregion

    #region 打印

    public async Task<SalesOrderDetailDto> GetByIdForPrintAsync(int id)
    {
        return await GetByIdAsync(id);
    }

    public async Task<List<SalesOrderDetailDto>> GetByIdsForPrintAsync(int[] ids)
    {
        return await GetByIdsAsync(ids);
    }

    public async Task<List<SalesOrderDetailDto>> GetByIdsAsync(int[] ids)
    {
        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .Include(so => so.OrderItems)
            .Where(so => ids.Contains(so.Id))
            .ToListAsync();

        var customerIds = salesOrders.Select(so => so.CustomerId).Distinct().ToList();
        var customers = await _context.CustomerProfiles
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c);

        var psIds = salesOrders.SelectMany(so => so.OrderItems)
            .Where(oi => oi.ProductionStandardId > 0)
            .Select(oi => oi.ProductionStandardId)
            .Distinct()
            .ToList();
        var psDict = psIds.Any()
            ? await _context.ProductionStandards
                .Where(ps => psIds.Contains(ps.Id))
                .ToDictionaryAsync(ps => ps.Id, ps => ps)
            : new Dictionary<int, ProductionStandard>();

        return salesOrders.Select(so =>
        {
            customers.TryGetValue(so.CustomerId, out var customer);
            return new SalesOrderDetailDto
            {
                Id = so.Id,
                OrderNumber = so.OrderNumber,
                SignDate = so.SignDate,
                CustomerId = so.CustomerId,
                CustomerName = customer?.CustomerUnit ?? "未知客户",
                Salesman = customer?.Salesman ?? string.Empty,
                EndCustomer = customer?.EndCustomer,
                Status = so.Status,
                RowVersion = so.RowVersion,
                Items = so.OrderItems.Select(oi =>
                {
                    psDict.TryGetValue(oi.ProductionStandardId, out var ps);
                    return new OrderItemDto
                    {
                        Id = oi.Id,
                        Sequence = oi.Sequence,
                        DeliveryDate = oi.DeliveryDate,
                        DelayPenalty = oi.DelayPenalty,
                        SettlementMethod = oi.SettlementMethod,
                        MaterialName = oi.MaterialName,
                        ProductionStandardCode = ps?.StandardCode ?? string.Empty,
                        DeliveryState = oi.DeliveryState,
                        StandardGrade = oi.StandardGrade,
                        PlantGrade = oi.PlantGrade,
                        Density = oi.Density,
                        OuterDiameter = oi.OuterDiameter,
                        WallThickness = oi.WallThickness,
                        Specification = oi.Specification,
                        OuterDiameterNegative = oi.OuterDiameterNegative,
                        OuterDiameterPositive = oi.OuterDiameterPositive,
                        WallThicknessNegative = oi.WallThicknessNegative,
                        WallThicknessPositive = oi.WallThicknessPositive,
                        LengthStatus = oi.LengthStatus,
                        MinLength = oi.MinLength,
                        MaxLength = oi.MaxLength,
                        Quantity = oi.Quantity,
                        Meters = oi.Meters,
                        ContractWeight = oi.ContractWeight,
                        TheoreticalWeight = oi.TheoreticalWeight,
                        Remark = oi.Remark,
                        CreatedTime = oi.CreatedTime,
                        UpdatedTime = oi.UpdatedTime
                    };
                }).ToList()
            };
        }).ToList();
    }

    public async Task<List<SalesOrderDetailDto>> GetAllByFilterForPrintAsync(string? keyword, string? technicalStatus, string? orderStatus, string? sortBy = null, bool isDescending = false)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "CreatedTime",
            IsDescending = isDescending
        };

        var paged = await GetPagedAsync(query, technicalStatus, orderStatus);
        var ids = paged.Items.Select(i => i.Id).ToArray();
        return await GetByIdsAsync(ids);
    }

    public async Task<byte[]> PrintOrderAsync(int id)
    {
        var order = await GetByIdForPrintAsync(id);
        return SalesOrderPrintHelper.GenerateOrderPdf(order);
    }

    public async Task<byte[]> PrintOrderBatchAsync(int[] ids)
    {
        var orders = await GetByIdsForPrintAsync(ids);
        return SalesOrderPrintHelper.GenerateBatchOrderPdf(orders);
    }

    public async Task<byte[]> PrintOrderAllAsync(string? keyword, string? technicalStatus, string? orderStatus, string? sortBy = null, bool isDescending = false)
    {
        var orders = await GetAllByFilterForPrintAsync(keyword, technicalStatus, orderStatus, sortBy, isDescending);
        return SalesOrderPrintHelper.GenerateBatchOrderPdf(orders);
    }

    public async Task<byte[]> PrintOrderRequirementsAsync(int orderId)
    {
        var order = await GetByIdForPrintAsync(orderId);

        // 加载技术要求
        var reqResult = await _context.ProductRequirements
            .Where(pr => pr.OrderItem != null && pr.OrderItem.SalesOrderId == orderId)
            .Include(pr => pr.OrderItem)
            .ToListAsync();

        var requirements = reqResult.Select(pr => new ProductRequirementDto
        {
            Id = pr.Id,
            OrderItemId = pr.OrderItemId,
            RequirementType = pr.RequirementType,
            ChemicalComposition = pr.ChemicalComposition,
            MechanicalProperty = pr.MechanicalProperty,
            ToleranceRequirement = pr.ToleranceRequirement,
            SurfaceQuality = pr.SurfaceQuality,
            NdtRequirement = pr.NdtRequirement,
            OtherRequirement = pr.OtherRequirement,
            Sequence = pr.OrderItem?.Sequence ?? 0,
            CreatedTime = pr.CreatedTime,
            UpdatedTime = pr.UpdatedTime
        }).OrderBy(r => r.Sequence).ToList();

        return SalesOrderPrintHelper.GenerateRequirementsPdf(order, requirements);
    }

    #endregion
}