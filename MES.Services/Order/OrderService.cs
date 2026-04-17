// 文件路径: MES.Services/Order/OrderService.cs
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

/// <summary>
/// 订单服务实现
/// </summary>
public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 分页查询订单列表
    /// </summary>
    public async Task<PagedResult<SalesOrderListDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.SalesOrders
            .Where(so => !so.IsDeleted)
            .Include(so => so.Customer)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            queryable = queryable.Where(so =>
                so.OrderNumber.Contains(query.Keyword) ||
                so.Customer.CustomerUnit.Contains(query.Keyword));
        }

        queryable = query.SortBy?.ToLower() switch
        {
            "ordernumber" => query.IsDescending ? queryable.OrderByDescending(so => so.OrderNumber) : queryable.OrderBy(so => so.OrderNumber),
            "signdate" => query.IsDescending ? queryable.OrderByDescending(so => so.SignDate) : queryable.OrderBy(so => so.SignDate),
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
                Status = so.Status,  // 直接赋值枚举，不再使用 ToString()
                RowVersion = so.RowVersion
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

    /// <summary>
    /// 根据ID获取订单详情
    /// </summary>
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
            Status = salesOrder.Status,  // 直接赋值枚举，不再使用 ToString()
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

    /// <summary>
    /// 创建订单
    /// </summary>
    public async Task<SalesOrderListDto> CreateAsync(CreateSalesOrderRequest request)
    {
        if (await _context.SalesOrders.AnyAsync(so => so.OrderNumber == request.OrderNumber))
            throw new BusinessException("订单号已存在");

        var customer = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId && !c.IsDeleted);
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

        return new SalesOrderListDto
        {
            Id = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            CustomerName = customer.CustomerUnit,
            Status = salesOrder.Status,
            RowVersion = salesOrder.RowVersion
        };
    }

    /// <summary>
    /// 更新订单
    /// </summary>
    public async Task<SalesOrderListDto> UpdateAsync(int id, UpdateSalesOrderRequest request)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Completed)
            throw new BusinessException("已完成的订单不能修改");

        if (!string.IsNullOrEmpty(request.OrderNumber) && request.OrderNumber != salesOrder.OrderNumber)
        {
            if (await _context.SalesOrders.AnyAsync(so => so.OrderNumber == request.OrderNumber && so.Id != id))
                throw new BusinessException("订单号已存在");
            salesOrder.OrderNumber = request.OrderNumber;
        }

        if (request.SignDate.HasValue)
            salesOrder.SignDate = request.SignDate.Value;

        if (request.CustomerId.HasValue)
        {
            var customer = await _context.CustomerProfiles
                .FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value && !c.IsDeleted);
            if (customer == null)
                throw new BusinessException("客户不存在");
            salesOrder.CustomerId = request.CustomerId.Value;
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            if (!Enum.TryParse<SalesOrderStatus>(request.Status, true, out var newStatus))
            {
                throw new BusinessException($"无效的订单状态: {request.Status}");
            }

            // 状态流转验证
            if (!CanTransitionTo(salesOrder.Status, newStatus))
            {
                throw new BusinessException($"不允许从 {salesOrder.Status} 状态变更为 {newStatus} 状态");
            }

            salesOrder.Status = newStatus;
        }

        // 乐观并发控制
        _context.Entry(salesOrder).Property(x => x.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("订单已被其他用户修改，请刷新后重试");
        }

        var updatedCustomer = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == salesOrder.CustomerId);

        return new SalesOrderListDto
        {
            Id = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            CustomerName = updatedCustomer?.CustomerUnit ?? string.Empty,
            Status = salesOrder.Status,
            RowVersion = salesOrder.RowVersion
        };
    }

    /// <summary>
    /// 删除订单（软删除）
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var salesOrder = await _context.SalesOrders
            .Include(so => so.OrderItems)
            .FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Completed)
            throw new BusinessException("已完成的订单不能删除");

        salesOrder.IsDeleted = true;

        foreach (var orderItem in salesOrder.OrderItems.Where(oi => !oi.IsDeleted))
        {
            orderItem.IsDeleted = true;
        }

        await _context.SaveChangesAsync();
    }

    #region 项次管理

    /// <summary>
    /// 添加订单项次
    /// </summary>
    public async Task<OrderItemDto> AddItemAsync(int orderId, AddOrderItemRequest request)
    {
        var salesOrder = await _context.SalesOrders
            .Include(so => so.OrderItems.Where(oi => !oi.IsDeleted))
            .FirstOrDefaultAsync(so => so.Id == orderId && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Completed)
            throw new BusinessException("已完成的订单不能添加项次");

        // 确定项次号
        var maxSequence = salesOrder.OrderItems.Any() 
            ? salesOrder.OrderItems.Max(oi => oi.Sequence) 
            : 0;
        var sequence = request.Sequence ?? maxSequence + 1;

        // 检查项次号是否已存在
        if (salesOrder.OrderItems.Any(oi => oi.Sequence == sequence))
            throw new BusinessException($"项次号 {sequence} 已存在");

        var orderItem = await CreateOrderItemFromAddRequestAsync(request, salesOrder.Id, sequence);
        _context.OrderItems.Add(orderItem);
        await _context.SaveChangesAsync();

        return MapToOrderItemDto(orderItem);
    }

    /// <summary>
    /// 更新订单项次
    /// </summary>
    public async Task<OrderItemDto> UpdateItemAsync(int orderId, int itemId, UpdateOrderItemRequest request)
    {
        // 验证订单存在
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == orderId && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Completed)
            throw new BusinessException("已完成的订单不能修改项次");

        // 获取项次
        var orderItem = await _context.OrderItems
            .Include(oi => oi.ProductionStandard)
            .FirstOrDefaultAsync(oi => oi.Id == itemId && oi.SalesOrderId == orderId && !oi.IsDeleted);

        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        // 如果项次号变更，检查唯一性
        if (request.Sequence != orderItem.Sequence)
        {
            var exists = await _context.OrderItems
                .AnyAsync(oi => oi.SalesOrderId == orderId && oi.Sequence == request.Sequence && oi.Id != itemId && !oi.IsDeleted);
            if (exists)
                throw new BusinessException($"项次号 {request.Sequence} 已存在");
            orderItem.Sequence = request.Sequence;
        }

        // 验证并更新牌号对照
        var gradeMapping = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(sgm => sgm.StandardGrade == request.StandardGrade && !sgm.IsDeleted);
        if (gradeMapping == null)
            throw new BusinessException($"标准牌号 '{request.StandardGrade}' 不存在");

        // 验证长度状态
        ValidateLengthStatus(request.LengthStatus, request.MinLength, request.MaxLength);

        // 计算米数
        var meters = CalculateMeters(request.LengthStatus, request.MinLength, request.MaxLength, request.Quantity, request.Meters);

        // 计算理算重量
        var theoreticalWeight = CalculateTheoreticalWeight(
            gradeMapping.Density,
            request.OuterDiameter,
            request.WallThickness,
            request.OuterDiameterNegative, request.OuterDiameterPositive,
            request.WallThicknessNegative, request.WallThicknessPositive,
            meters ?? orderItem.Meters ?? 0);

        // 更新字段
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
        orderItem.Specification = $"{request.OuterDiameter}*{request.WallThickness}";
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

    /// <summary>
    /// 删除订单项次（软删除）
    /// </summary>
    public async Task DeleteItemAsync(int orderId, int itemId)
    {
        // 验证订单存在
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == orderId && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Completed)
            throw new BusinessException("已完成的订单不能删除项次");

        var orderItem = await _context.OrderItems
            .FirstOrDefaultAsync(oi => oi.Id == itemId && oi.SalesOrderId == orderId && !oi.IsDeleted);

        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        // 软检查：记录日志但不阻止删除（等工单模块开发完成后改为硬检查）
        // 注意：WorkOrderId 当前为 [NotMapped]，待工单模块开发时统一处理
        if (orderItem.WorkOrderId.HasValue)
        {
            _logger.LogWarning("项次 {ItemId} 已关联工单 {WorkOrderId}，仍被删除。订单号: {OrderId}", 
                itemId, orderItem.WorkOrderId, orderId);
        }

        orderItem.IsDeleted = true;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("项次 {ItemId} 已软删除，订单号: {OrderId}", itemId, orderId);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// 从 CreateOrderItemRequest 创建订单项次
    /// </summary>
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

        var theoreticalWeight = CalculateTheoreticalWeight(
            gradeMapping.Density,
            request.OuterDiameter,
            request.WallThickness,
            request.OuterDiameterNegative, request.OuterDiameterPositive,
            request.WallThicknessNegative, request.WallThicknessPositive,
            meters ?? 0);

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
            Specification = $"{request.OuterDiameter}*{request.WallThickness}",
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

    /// <summary>
    /// 从 AddOrderItemRequest 创建订单项次
    /// </summary>
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

        var theoreticalWeight = CalculateTheoreticalWeight(
            gradeMapping.Density,
            request.OuterDiameter,
            request.WallThickness,
            request.OuterDiameterNegative, request.OuterDiameterPositive,
            request.WallThicknessNegative, request.WallThicknessPositive,
            meters ?? 0);

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
            Specification = $"{request.OuterDiameter}*{request.WallThickness}",
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

    private void ValidateLengthStatus(LengthStatus lengthStatus, decimal? minLength, decimal? maxLength)
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
            case LengthStatus.NonFixed:
                break;
        }
    }

    private decimal? CalculateMeters(LengthStatus lengthStatus, decimal? minLength, decimal? maxLength, int? quantity, decimal? meters)
    {
        switch (lengthStatus)
        {
            case LengthStatus.Fixed:
                if (quantity.HasValue && quantity > 0 && maxLength.HasValue && maxLength > 0)
                    return Math.Round(maxLength.Value * quantity.Value / 1000, 2);
                break;
            case LengthStatus.Range:
            case LengthStatus.NonFixed:
                return meters.HasValue ? Math.Round(meters.Value, 2) : 0;
        }

        return 0;
    }

    private decimal CalculateMaxLength(LengthStatus lengthStatus, decimal? minLength, decimal? maxLength)
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

    private decimal CalculateTheoreticalWeight(
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
        if (effectiveOuterDiameter <= effectiveWallThickness) effectiveOuterDiameter = effectiveWallThickness + 0.001m;

        var weight = density * pi * effectiveWallThickness * (effectiveOuterDiameter - effectiveWallThickness) * meters / 1000;

        if (weight < 0) weight = 0;

        return Math.Round(weight, 2);
    }

    private OrderItemDto MapToOrderItemDto(OrderItem orderItem)
    {
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

    private bool CanTransitionTo(SalesOrderStatus current, SalesOrderStatus target)
    {
        // 相同状态不需要转换
        if (current == target) return true;
        
        // 任何状态都可以取消
        if (target == SalesOrderStatus.Cancelled) return true;
        
        // 正常流转
        return (current, target) switch
        {
            (SalesOrderStatus.Pending, SalesOrderStatus.Confirmed) => true,
            (SalesOrderStatus.Confirmed, SalesOrderStatus.Processing) => true,
            (SalesOrderStatus.Processing, SalesOrderStatus.Completed) => true,
            _ => false
        };
    }

    #endregion
}