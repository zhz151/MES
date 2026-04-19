using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Interfaces.Order;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities;
using MES.Core.Exceptions;

namespace MES.Services.Order;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region 订单管理

    public async Task<PagedResult<SalesOrderListDto>> GetPagedAsync(QueryParams query, bool? hasTechnicalRequirement = null, List<SalesOrderStatus>? statuses = null)
    {
        var queryable = _context.SalesOrders
            .Where(so => !so.IsDeleted)
            .Include(so => so.Customer)
            .AsQueryable();

        // 订单状态筛选（默认只显示 Pending, Confirmed, Cancelled）
        if (statuses == null || !statuses.Any())
        {
            statuses = new List<SalesOrderStatus> { SalesOrderStatus.Pending, SalesOrderStatus.Confirmed, SalesOrderStatus.Cancelled };
        }
        queryable = queryable.Where(so => statuses.Contains(so.Status));

        // 技术要求状态筛选
        if (hasTechnicalRequirement.HasValue)
        {
            if (hasTechnicalRequirement.Value)
            {
                queryable = queryable.Where(so => so.OrderItems.Any(oi => !oi.IsDeleted && oi.ProductRequirement != null && !oi.ProductRequirement.IsDeleted));
            }
            else
            {
                queryable = queryable.Where(so => !so.OrderItems.Any(oi => !oi.IsDeleted && oi.ProductRequirement != null && !oi.ProductRequirement.IsDeleted));
            }
        }

        // 关键字模糊搜索
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            queryable = queryable.Where(so =>
                so.OrderNumber.Contains(keyword) ||
                so.Customer.CustomerUnit.Contains(keyword) ||
                so.Customer.Salesman.Contains(keyword) ||
                (so.Customer.EndCustomer != null && so.Customer.EndCustomer.Contains(keyword)));
        }

        // 排序
        queryable = query.SortBy?.ToLower() switch
        {
            "ordernumber" => query.IsDescending ? queryable.OrderByDescending(so => so.OrderNumber) : queryable.OrderBy(so => so.OrderNumber),
            "signdate" => query.IsDescending ? queryable.OrderByDescending(so => so.SignDate) : queryable.OrderBy(so => so.SignDate),
            "customername" => query.IsDescending ? queryable.OrderByDescending(so => so.Customer.CustomerUnit) : queryable.OrderBy(so => so.Customer.CustomerUnit),
            "salesman" => query.IsDescending ? queryable.OrderByDescending(so => so.Customer.Salesman) : queryable.OrderBy(so => so.Customer.Salesman),
            "status" => query.IsDescending ? queryable.OrderByDescending(so => so.Status) : queryable.OrderBy(so => so.Status),
            _ => query.IsDescending ? queryable.OrderByDescending(so => so.CreatedTime) : queryable.OrderBy(so => so.CreatedTime)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(so => new SalesOrderListDto
            {
                Id = so.Id,
                OrderNumber = so.OrderNumber,
                SignDate = so.SignDate,
                CustomerName = so.Customer.CustomerUnit,
                Salesman = so.Customer.Salesman,
                EndCustomer = so.Customer.EndCustomer,
                Status = so.Status,
                RowVersion = so.RowVersion,
                HasTechnicalRequirement = so.OrderItems.Any(oi => !oi.IsDeleted && oi.ProductRequirement != null && !oi.ProductRequirement.IsDeleted),
                FirstOrderItemId = so.OrderItems.Where(oi => !oi.IsDeleted).Select(oi => (int?)oi.Id).FirstOrDefault()
            })
            .ToListAsync();

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
        var salesOrder = await _context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.OrderItems.Where(oi => !oi.IsDeleted))
                .ThenInclude(oi => oi.ProductionStandard)
            .FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        return new SalesOrderDetailDto
        {
            Id = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            CustomerId = salesOrder.CustomerId,
            CustomerName = salesOrder.Customer.CustomerUnit,
            Salesman = salesOrder.Customer.Salesman,
            Status = salesOrder.Status,
            RowVersion = salesOrder.RowVersion,
            Items = salesOrder.OrderItems.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                Sequence = oi.Sequence,
                DeliveryDate = oi.DeliveryDate,
                DelayPenalty = oi.DelayPenalty,
                SettlementMethod = oi.SettlementMethod,
                MaterialName = oi.MaterialName,
                ProductionStandardCode = oi.ProductionStandard.StandardCode,
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
                Remark = oi.Remark
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

        // 已取消的订单不能修改
        if (salesOrder.Status == SalesOrderStatus.Cancelled)
            throw new BusinessException("已取消的订单不能修改");

        // 更新订单号
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

        // 更新状态
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
            .FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        // 只有待处理的订单可以删除
        if (salesOrder.Status != SalesOrderStatus.Pending)
            throw new BusinessException("只有待处理的订单可以删除");

        salesOrder.IsDeleted = true;
        foreach (var orderItem in salesOrder.OrderItems.Where(oi => !oi.IsDeleted))
        {
            orderItem.IsDeleted = true;
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation("删除订单成功: 订单号 {OrderNumber}", salesOrder.OrderNumber);
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

        if (salesOrder.Status != SalesOrderStatus.Pending)
            throw new BusinessException("只有待处理的订单可以添加项次");

        var maxSequence = salesOrder.OrderItems.Any() ? salesOrder.OrderItems.Max(oi => oi.Sequence) : 0;
        var sequence = request.Sequence ?? maxSequence + 1;

        if (salesOrder.OrderItems.Any(oi => oi.Sequence == sequence))
            throw new BusinessException($"项次号 {sequence} 已存在");

        var orderItem = await CreateOrderItemFromAddRequestAsync(request, salesOrder.Id, sequence);
        _context.OrderItems.Add(orderItem);
        await _context.SaveChangesAsync();

        return MapToOrderItemDto(orderItem);
    }

    public async Task<OrderItemDto> UpdateItemAsync(int orderId, int itemId, UpdateOrderItemRequest request)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == orderId && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status != SalesOrderStatus.Pending)
            throw new BusinessException("只有待处理的订单可以修改项次");

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

        var meters = CalculateMeters(request.LengthStatus, request.MinLength, request.MaxLength, request.Quantity, request.Meters);
        var metersValue = meters ?? 0m;
        var theoreticalWeight = CalculateTheoreticalWeight(
            gradeMapping.Density,
            request.OuterDiameter,
            request.WallThickness,
            request.OuterDiameterNegative, request.OuterDiameterPositive,
            request.WallThicknessNegative, request.WallThicknessPositive,
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
        orderItem.OuterDiameter = request.OuterDiameter;
        orderItem.WallThickness = request.WallThickness;
        orderItem.Specification = $"{NormalizeDecimal(request.OuterDiameter)}*{NormalizeDecimal(request.WallThickness)}";
        orderItem.OuterDiameterNegative = request.OuterDiameterNegative;
        orderItem.OuterDiameterPositive = request.OuterDiameterPositive;
        orderItem.WallThicknessNegative = request.WallThicknessNegative;
        orderItem.WallThicknessPositive = request.WallThicknessPositive;
        orderItem.LengthStatus = request.LengthStatus;
        orderItem.MinLength = request.MinLength;
        orderItem.MaxLength = CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength);
        orderItem.Quantity = request.Quantity;
        orderItem.Meters = meters;
        orderItem.ContractWeight = request.ContractWeight;
        orderItem.TheoreticalWeight = theoreticalWeight;
        orderItem.Remark = request.Remark;

        await _context.SaveChangesAsync();

        return MapToOrderItemDto(orderItem);
    }

    public async Task DeleteItemAsync(int orderId, int itemId)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == orderId && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status != SalesOrderStatus.Pending)
            throw new BusinessException("只有待处理的订单可以删除项次");

        var orderItem = await _context.OrderItems
            .FirstOrDefaultAsync(oi => oi.Id == itemId && oi.SalesOrderId == orderId && !oi.IsDeleted);

        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        orderItem.IsDeleted = true;
        await _context.SaveChangesAsync();
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

        var meters = CalculateMeters(request.LengthStatus, request.MinLength, request.MaxLength, request.Quantity, request.Meters);
        var metersValue = meters ?? 0m;
        var theoreticalWeight = CalculateTheoreticalWeight(
            gradeMapping.Density,
            request.OuterDiameter,
            request.WallThickness,
            request.OuterDiameterNegative, request.OuterDiameterPositive,
            request.WallThicknessNegative, request.WallThicknessPositive,
            metersValue);

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
            OuterDiameter = request.OuterDiameter,
            WallThickness = request.WallThickness,
            Specification = $"{NormalizeDecimal(request.OuterDiameter)}*{NormalizeDecimal(request.WallThickness)}",
            OuterDiameterNegative = request.OuterDiameterNegative,
            OuterDiameterPositive = request.OuterDiameterPositive,
            WallThicknessNegative = request.WallThicknessNegative,
            WallThicknessPositive = request.WallThicknessPositive,
            LengthStatus = request.LengthStatus,
            MinLength = request.MinLength,
            MaxLength = CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength),
            Quantity = request.Quantity,
            Meters = meters,
            ContractWeight = request.ContractWeight,
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

        var meters = CalculateMeters(request.LengthStatus, request.MinLength, request.MaxLength, request.Quantity, request.Meters);
        var metersValue = meters ?? 0m;
        var theoreticalWeight = CalculateTheoreticalWeight(
            gradeMapping.Density,
            request.OuterDiameter,
            request.WallThickness,
            request.OuterDiameterNegative, request.OuterDiameterPositive,
            request.WallThicknessNegative, request.WallThicknessPositive,
            metersValue);

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
            OuterDiameter = request.OuterDiameter,
            WallThickness = request.WallThickness,
            Specification = $"{NormalizeDecimal(request.OuterDiameter)}*{NormalizeDecimal(request.WallThickness)}",
            OuterDiameterNegative = request.OuterDiameterNegative,
            OuterDiameterPositive = request.OuterDiameterPositive,
            WallThicknessNegative = request.WallThicknessNegative,
            WallThicknessPositive = request.WallThicknessPositive,
            LengthStatus = request.LengthStatus,
            MinLength = request.MinLength,
            MaxLength = CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength),
            Quantity = request.Quantity,
            Meters = meters,
            ContractWeight = request.ContractWeight,
            TheoreticalWeight = theoreticalWeight,
            Remark = request.Remark
        };
    }

    private static void ValidateLengthStatus(LengthStatus lengthStatus, decimal? minLength, decimal? maxLength)
    {
        switch (lengthStatus)
        {
            case LengthStatus.Fixed:
                if (!minLength.HasValue || minLength <= 0)
                    throw new BusinessException("定尺时必须填写长度");
                break;
            case LengthStatus.Range:
                if (!minLength.HasValue || minLength <= 0 || !maxLength.HasValue || maxLength <= 0 || maxLength <= minLength)
                    throw new BusinessException("范围尺时必须填写最小长度和最大长度，且最大长度必须大于最小长度");
                break;
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

    private static string NormalizeDecimal(decimal value)
    {
        var trimmed = value.ToString("0.###########").TrimEnd('0').TrimEnd('.');
        return trimmed;
    }

    private OrderItemDto MapToOrderItemDto(OrderItem orderItem)
    {
        if (orderItem.ProductionStandard == null && orderItem.ProductionStandardId > 0)
        {
            orderItem.ProductionStandard = _context.ProductionStandards
                .FirstOrDefault(ps => ps.Id == orderItem.ProductionStandardId);
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
            Remark = orderItem.Remark
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

    #endregion
}