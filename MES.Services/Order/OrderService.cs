// 文件路径: MES.Services/Order/OrderService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Interfaces.Order;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

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

    public async Task<PagedResult<SalesOrderListDto>> GetPagedAsync(QueryParams query, bool? hasTechReq = null, List<SalesOrderStatus>? statuses = null)
    {
        var queryable = _context.SalesOrders
            .Where(so => !so.IsDeleted)
            .AsNoTracking()
            .AsQueryable();

        // 订单状态筛选
        if (statuses == null || !statuses.Any())
        {
            statuses = new List<SalesOrderStatus> { SalesOrderStatus.Pending, SalesOrderStatus.Confirmed, SalesOrderStatus.Cancelled };
        }
        queryable = queryable.Where(so => statuses.Contains(so.Status));

        // 关键字模糊搜索
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            queryable = queryable.Where(so =>
                so.OrderNumber.Contains(keyword) ||
                _context.CustomerProfiles.Any(c => c.Id == so.CustomerId && c.CustomerUnit.Contains(keyword)) ||
                _context.CustomerProfiles.Any(c => c.Id == so.CustomerId && c.Salesman.Contains(keyword)) ||
                _context.CustomerProfiles.Any(c => c.Id == so.CustomerId && c.EndCustomer != null && c.EndCustomer.Contains(keyword)));
        }

        // 技术要求状态筛选
        if (hasTechReq.HasValue)
        {
            if (hasTechReq.Value)
            {
                // 已编辑：订单下所有项次都有技术要求
                queryable = queryable.Where(so =>
                    _context.OrderItems.Any(oi => oi.SalesOrderId == so.Id && !oi.IsDeleted) &&
                    !_context.OrderItems.Any(oi => oi.SalesOrderId == so.Id && !oi.IsDeleted &&
                        !_context.ProductRequirements.Any(pr => pr.OrderItemId == oi.Id && !pr.IsDeleted)));
            }
            else
            {
                // 未编辑：至少有一个项次没有技术要求
                queryable = queryable.Where(so =>
                    _context.OrderItems.Any(oi => oi.SalesOrderId == so.Id && !oi.IsDeleted) &&
                    _context.OrderItems.Any(oi => oi.SalesOrderId == so.Id && !oi.IsDeleted &&
                        !_context.ProductRequirements.Any(pr => pr.OrderItemId == oi.Id && !pr.IsDeleted)));
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
                default:
                    queryable = query.IsDescending ? queryable.OrderByDescending(so => so.CreatedTime) : queryable.OrderBy(so => so.CreatedTime);
                    break;
            }
        }
        else
        {
            queryable = query.IsDescending ? queryable.OrderByDescending(so => so.CreatedTime) : queryable.OrderBy(so => so.CreatedTime);
        }

        var salesOrders = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var orderIds = salesOrders.Select(so => so.Id).ToList();
        var customerIds = salesOrders.Select(so => so.CustomerId).Distinct().ToList();
        var customers = await _context.CustomerProfiles
            .Where(c => customerIds.Contains(c.Id) && !c.IsDeleted)
            .ToDictionaryAsync(c => c.Id, c => c);

        var orderItemCounts = await _context.OrderItems
            .Where(oi => orderIds.Contains(oi.SalesOrderId) && !oi.IsDeleted)
            .GroupBy(oi => oi.SalesOrderId)
            .Select(g => new { OrderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrderId, x => x.Count);

        var orderHasReqCounts = await _context.OrderItems
            .Where(oi => orderIds.Contains(oi.SalesOrderId) && !oi.IsDeleted)
            .GroupJoin(
                _context.ProductRequirements.Where(pr => !pr.IsDeleted),
                oi => oi.Id,
                pr => pr.OrderItemId,
                (oi, prs) => new { oi.SalesOrderId, HasReq = prs.Any() }
            )
            .Where(x => x.HasReq)
            .GroupBy(x => x.SalesOrderId)
            .Select(g => new { OrderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrderId, x => x.Count);

        var firstOrderItemIds = await _context.OrderItems
            .Where(oi => orderIds.Contains(oi.SalesOrderId) && !oi.IsDeleted)
            .GroupBy(oi => oi.SalesOrderId)
            .Select(g => new { OrderId = g.Key, FirstItemId = g.OrderBy(oi => oi.Sequence).Select(oi => (int?)oi.Id).FirstOrDefault() })
            .ToDictionaryAsync(x => x.OrderId, x => x.FirstItemId);

        var orderItemMaxUpdate = await _context.OrderItems
            .Where(oi => orderIds.Contains(oi.SalesOrderId) && !oi.IsDeleted)
            .GroupBy(oi => oi.SalesOrderId)
            .Select(g => new { OrderId = g.Key, MaxUpdate = g.Max(oi => oi.UpdatedTime) })
            .ToDictionaryAsync(x => x.OrderId, x => x.MaxUpdate);

        var items = new List<SalesOrderListDto>();
        foreach (var so in salesOrders)
        {
            customers.TryGetValue(so.CustomerId, out var customer);
            var totalItemCount = orderItemCounts.GetValueOrDefault(so.Id);
            var hasReqCount = orderHasReqCounts.GetValueOrDefault(so.Id);
            var hasTechnicalRequirement = totalItemCount > 0 && hasReqCount == totalItemCount;
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
                Status = so.Status,
                RowVersion = so.RowVersion,
                HasTechnicalRequirement = hasTechnicalRequirement,
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
        // 先查询订单（不 Include 导航属性，避免全局软删除过滤器将 LEFT JOIN 转为 INNER JOIN）
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .Include(so => so.OrderItems.Where(oi => !oi.IsDeleted))
            .FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        // 单独加载 Customer（避免全局软删除过滤器冲突）
        var customer = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == salesOrder.CustomerId);

        // 单独加载 ProductionStandard（避免全局软删除过滤器冲突）
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

    public async Task<SalesOrderListDto> CreateAsync(CreateSalesOrderRequest request)
    {
        if (await _context.SalesOrders.AnyAsync(so => so.OrderNumber == request.OrderNumber && !so.IsDeleted))
            throw new BusinessException("订单号已存在");

        var customer = await _context.CustomerProfiles.FirstOrDefaultAsync(c => c.Id == request.CustomerId && !c.IsDeleted);
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
            .FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Cancelled)
            throw new BusinessException("已取消的订单不能修改");

        if (!string.IsNullOrEmpty(request.OrderNumber) && request.OrderNumber != salesOrder.OrderNumber)
        {
            if (await _context.SalesOrders.AnyAsync(so => so.OrderNumber == request.OrderNumber && so.Id != id && !so.IsDeleted))
                throw new BusinessException("订单号已存在");
            salesOrder.OrderNumber = request.OrderNumber;
        }

        if (request.SignDate.HasValue)
            salesOrder.SignDate = request.SignDate.Value;

        if (request.CustomerId.HasValue && request.CustomerId.Value != salesOrder.CustomerId)
        {
            var customer = await _context.CustomerProfiles.FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value && !c.IsDeleted);
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

        var updatedCustomer = await _context.CustomerProfiles.FirstOrDefaultAsync(c => c.Id == salesOrder.CustomerId && !c.IsDeleted);

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
        .FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);

    if (salesOrder == null)
        throw new BusinessException("订单不存在");

    if (salesOrder.Status == SalesOrderStatus.Cancelled)
        throw new BusinessException("已取消的订单不能删除");

    // 1. 软删除订单
    salesOrder.IsDeleted = true;

    // 2. 软删除订单项次和产品要求
    foreach (var orderItem in salesOrder.OrderItems.Where(oi => !oi.IsDeleted))
    {
        orderItem.IsDeleted = true;
        if (orderItem.ProductRequirement != null && !orderItem.ProductRequirement.IsDeleted)
            orderItem.ProductRequirement.IsDeleted = true;
    }

    // 3. 物理删除关联工单
    var workOrders = await _context.WorkOrders
        .Where(wo => wo.SalesOrderNo == salesOrder.OrderNumber)
        .ToListAsync();

    var workOrderCount = workOrders.Count;

    if (workOrderCount > 0)
    {
        _context.WorkOrders.RemoveRange(workOrders);
    }

    // 4. 使用事务确保数据一致性
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        await _context.SaveChangesAsync();

        // 5. 生成通知（告知已自动清理工单）
        if (workOrderCount > 0)
        {
            var notification = new OrderChangeNotification
            {
                OrderNumber = salesOrder.OrderNumber,
                ChangeType = NotificationChangeType.Deleted,
                WorkOrderCount = workOrderCount,
                IsRead = false
            };
            _context.OrderChangeNotifications.Add(notification);
            await _context.SaveChangesAsync();
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
            .Include(so => so.OrderItems.Where(oi => !oi.IsDeleted))
            .FirstOrDefaultAsync(so => so.Id == orderId && !so.IsDeleted);

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
            .FirstOrDefaultAsync(so => so.Id == orderId && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Cancelled)
            throw new BusinessException("已取消的订单不能修改项次");

        var orderItem = await _context.OrderItems
            .Include(oi => oi.ProductionStandard)
            .FirstOrDefaultAsync(oi => oi.Id == itemId && oi.SalesOrderId == orderId && !oi.IsDeleted);

        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        if (request.Sequence != orderItem.Sequence)
        {
            var exists = await _context.OrderItems
                .AnyAsync(oi => oi.SalesOrderId == orderId && oi.Sequence == request.Sequence && oi.Id != itemId && !oi.IsDeleted);
            if (exists)
                throw new BusinessException($"项次号 {request.Sequence} 已存在");
            orderItem.Sequence = request.Sequence;
        }

        var gradeMapping = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(sgm => sgm.StandardGrade == request.StandardGrade && !sgm.IsDeleted);
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
            .FirstOrDefaultAsync(ps => ps.Id == request.ProductionStandardId && !ps.IsDeleted);
        if (productionStandard == null)
            throw new BusinessException("产品标准不存在");

        orderItem.DeliveryDate = request.DeliveryDate;
        orderItem.DelayPenalty = request.DelayPenalty;
        orderItem.SettlementMethod = request.SettlementMethod;
        orderItem.MaterialName = request.MaterialName;
        orderItem.ProductionStandardId = request.ProductionStandardId;
        orderItem.DeliveryState = request.DeliveryState;
        orderItem.StandardGrade = request.StandardGrade;
        orderItem.PlantGrade = gradeMapping.PlantGrade;
        orderItem.Density = gradeMapping.Density;
        orderItem.OuterDiameter = normalizedOuterDiameter;
        orderItem.WallThickness = normalizedWallThickness;
        orderItem.Specification = $"{normalizedOuterDiameter}*{normalizedWallThickness}";
        orderItem.OuterDiameterNegative = normalizedOuterDiameterNegative;
        orderItem.OuterDiameterPositive = normalizedOuterDiameterPositive;
        orderItem.WallThicknessNegative = normalizedWallThicknessNegative;
        orderItem.WallThicknessPositive = normalizedWallThicknessPositive;
        orderItem.LengthStatus = request.LengthStatus;
        orderItem.MinLength = request.MinLength;
        orderItem.MaxLength = CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength);
        orderItem.Quantity = request.Quantity;
        orderItem.Meters = meters;
        orderItem.ContractWeight = normalizedContractWeight;
        orderItem.TheoreticalWeight = theoreticalWeight;
        orderItem.Remark = request.Remark;

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
            .FirstOrDefaultAsync(so => so.Id == orderId && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Cancelled)
            throw new BusinessException("已取消的订单不能删除项次");

        var orderItem = await _context.OrderItems
            .Include(oi => oi.ProductRequirement)
            .FirstOrDefaultAsync(oi => oi.Id == itemId && oi.SalesOrderId == orderId && !oi.IsDeleted);

        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        orderItem.IsDeleted = true;
        if (orderItem.ProductRequirement != null && !orderItem.ProductRequirement.IsDeleted)
            orderItem.ProductRequirement.IsDeleted = true;

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

    #endregion

    #region Private Methods

    private async Task<OrderItem> CreateOrderItemFromCreateRequestAsync(CreateOrderItemRequest request, int salesOrderId, int sequence)
    {
        var productionStandard = await _context.ProductionStandards
            .FirstOrDefaultAsync(ps => ps.Id == request.ProductionStandardId && !ps.IsDeleted);
        if (productionStandard == null)
            throw new BusinessException("产品标准不存在");

        var gradeMapping = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(sgm => sgm.StandardGrade == request.StandardGrade && !sgm.IsDeleted);
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

        return new OrderItem
        {
            SalesOrderId = salesOrderId,
            Sequence = sequence,
            DeliveryDate = request.DeliveryDate,
            DelayPenalty = request.DelayPenalty,
            SettlementMethod = request.SettlementMethod,
            MaterialName = request.MaterialName,
            ProductionStandardId = request.ProductionStandardId,
            DeliveryState = request.DeliveryState,
            StandardGrade = request.StandardGrade,
            PlantGrade = gradeMapping.PlantGrade,
            Density = gradeMapping.Density,
            OuterDiameter = normalizedOuterDiameter,
            WallThickness = normalizedWallThickness,
            Specification = $"{normalizedOuterDiameter}*{normalizedWallThickness}",
            OuterDiameterNegative = normalizedOuterDiameterNegative,
            OuterDiameterPositive = normalizedOuterDiameterPositive,
            WallThicknessNegative = normalizedWallThicknessNegative,
            WallThicknessPositive = normalizedWallThicknessPositive,
            LengthStatus = request.LengthStatus,
            MinLength = request.MinLength,
            MaxLength = CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength),
            Quantity = request.Quantity,
            Meters = meters,
            ContractWeight = normalizedContractWeight,
            TheoreticalWeight = theoreticalWeight,
            Remark = request.Remark
        };
    }

    private async Task<OrderItem> CreateOrderItemFromAddRequestAsync(AddOrderItemRequest request, int salesOrderId, int sequence)
    {
        var productionStandard = await _context.ProductionStandards
            .FirstOrDefaultAsync(ps => ps.Id == request.ProductionStandardId && !ps.IsDeleted);
        if (productionStandard == null)
            throw new BusinessException("产品标准不存在");

        var gradeMapping = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(sgm => sgm.StandardGrade == request.StandardGrade && !sgm.IsDeleted);
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

        return new OrderItem
        {
            SalesOrderId = salesOrderId,
            Sequence = sequence,
            DeliveryDate = request.DeliveryDate,
            DelayPenalty = request.DelayPenalty,
            SettlementMethod = request.SettlementMethod,
            MaterialName = request.MaterialName,
            ProductionStandardId = request.ProductionStandardId,
            DeliveryState = request.DeliveryState,
            StandardGrade = request.StandardGrade,
            PlantGrade = gradeMapping.PlantGrade,
            Density = gradeMapping.Density,
            OuterDiameter = normalizedOuterDiameter,
            WallThickness = normalizedWallThickness,
            Specification = $"{normalizedOuterDiameter}*{normalizedWallThickness}",
            OuterDiameterNegative = normalizedOuterDiameterNegative,
            OuterDiameterPositive = normalizedOuterDiameterPositive,
            WallThicknessNegative = normalizedWallThicknessNegative,
            WallThicknessPositive = normalizedWallThicknessPositive,
            LengthStatus = request.LengthStatus,
            MinLength = request.MinLength,
            MaxLength = CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength),
            Quantity = request.Quantity,
            Meters = meters,
            ContractWeight = normalizedContractWeight,
            TheoreticalWeight = theoreticalWeight,
            Remark = request.Remark
        };
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
            throw new BusinessException($"合同重量 {contractWeight:F2} kg 低于理算重量 {theoreticalWeight:F2} kg 的94%，可能亏损");
        }
        
        if (ratio > upperBound)
        {
            throw new BusinessException($"合同重量 {contractWeight:F2} kg 高于理算重量 {theoreticalWeight:F2} kg 的106%");
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
            .FirstOrDefaultAsync(so => so.Id == salesOrderId && !so.IsDeleted);
        if (salesOrder == null || salesOrder.Status != SalesOrderStatus.Confirmed)
            return;

        var hasRecent = await _notificationService.HasRecentItemChangedNotificationAsync(salesOrder.OrderNumber, 5);
        if (hasRecent) return;

        var notification = new OrderChangeNotification
        {
            OrderNumber = salesOrder.OrderNumber,
            ChangeType = NotificationChangeType.ItemChanged,
            WorkOrderCount = 0,
            IsRead = false
        };
        _context.OrderChangeNotifications.Add(notification);
        await _context.SaveChangesAsync();
    }

    #endregion
}