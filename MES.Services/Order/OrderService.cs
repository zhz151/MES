using Microsoft.EntityFrameworkCore;
using MES.Core.Interfaces;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities;
using Microsoft.AspNetCore.Http;

namespace MES.Services.Order;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrderService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PagedResult<SalesOrderListDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.SalesOrders
            .Where(so => !so.IsDeleted)
            .Include(so => so.Customer)
            .AsQueryable();

        // 关键词搜索
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            queryable = queryable.Where(so => 
                so.OrderNumber.Contains(query.Keyword) || 
                so.Customer.CustomerUnit.Contains(query.Keyword));
        }

        // 排序
        queryable = query.SortBy.ToLower() switch
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
                Status = so.Status.ToString()
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
            Status = salesOrder.Status.ToString(),
            Items = salesOrder.OrderItems.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                Sequence = oi.Sequence,
                DeliveryDate = oi.DeliveryDate,
                DelayPenalty = oi.DelayPenalty,
                SettlementMethod = oi.SettlementMethod.ToString(),
                MaterialName = oi.MaterialName.ToString(),
                ProductionStandardCode = oi.ProductionStandard.StandardCode,
                DeliveryState = oi.DeliveryState.ToString(),
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
                LengthStatus = oi.LengthStatus.ToString(),
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
        // 1. 验证订单号唯一性
        if (await _context.SalesOrders.AnyAsync(so => so.OrderNumber == request.OrderNumber))
            throw new BusinessException("订单号已存在");

        // 2. 验证客户存在性
        var customer = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId && !c.IsDeleted);
        if (customer == null)
            throw new BusinessException("客户不存在");

        // 3. 创建销售单
        var salesOrder = new SalesOrder
        {
            OrderNumber = request.OrderNumber,
            SignDate = request.SignDate,
            CustomerId = request.CustomerId,
            Status = SalesOrderStatus.Pending,
            CreatedBy = GetCurrentUser(),
            CreatedTime = DateTimeOffset.UtcNow,
            UpdatedBy = GetCurrentUser(),
            UpdatedTime = DateTimeOffset.UtcNow
        };

        // 4. 处理订单项次
        var sequence = 1;
        foreach (var itemRequest in request.Items)
        {
            var orderItem = await CreateOrderItemAsync(itemRequest, salesOrder.Id, sequence);
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
            Status = salesOrder.Status.ToString()
        };
    }

    public async Task<SalesOrderListDto> UpdateAsync(int id, UpdateSalesOrderRequest request)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        // 验证状态
        if (salesOrder.Status == SalesOrderStatus.Completed)
            throw new BusinessException("已完成的订单不能修改");

        // 更新字段
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
            if (Enum.TryParse<SalesOrderStatus>(request.Status, out var status))
            {
                salesOrder.Status = status;
            }
        }

        // 乐观并发控制
        if (salesOrder.RowVersion.SequenceEqual(request.RowVersion))
        {
            salesOrder.UpdatedBy = GetCurrentUser();
            salesOrder.UpdatedTime = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();

            var customer = await _context.CustomerProfiles
                .FirstOrDefaultAsync(c => c.Id == salesOrder.CustomerId);

            return new SalesOrderListDto
            {
                Id = salesOrder.Id,
                OrderNumber = salesOrder.OrderNumber,
                SignDate = salesOrder.SignDate,
                CustomerName = customer?.CustomerUnit ?? string.Empty,
                Status = salesOrder.Status.ToString()
            };
        }
        else
        {
            throw new BusinessException("订单已被其他用户修改，请刷新后重试");
        }
    }

    public async Task DeleteAsync(int id)
    {
        var salesOrder = await _context.SalesOrders
            .Include(so => so.OrderItems)
            .FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (salesOrder.Status == SalesOrderStatus.Completed)
            throw new BusinessException("已完成的订单不能删除");

        // 软删除销售单
        salesOrder.IsDeleted = true;
        salesOrder.UpdatedBy = GetCurrentUser();
        salesOrder.UpdatedTime = DateTimeOffset.UtcNow;

        // 级联软删除所有项次
        foreach (var orderItem in salesOrder.OrderItems.Where(oi => !oi.IsDeleted))
        {
            orderItem.IsDeleted = true;
            orderItem.UpdatedBy = GetCurrentUser();
            orderItem.UpdatedTime = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<OrderItem> CreateOrderItemAsync(CreateOrderItemRequest request, int salesOrderId, int sequence)
    {
        // 验证产品标准
        var productionStandard = await _context.ProductionStandards
            .FirstOrDefaultAsync(ps => ps.Id == request.ProductionStandardId && !ps.IsDeleted);
        if (productionStandard == null)
            throw new BusinessException("产品标准不存在");

        // 验证牌号对照
        var gradeMapping = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(sgm => sgm.StandardGrade == request.StandardGrade && !sgm.IsDeleted);
        if (gradeMapping == null)
            throw new BusinessException("标准牌号不存在");

        // 验证长度
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
            meters);

        return new OrderItem
        {
            SalesOrderId = salesOrderId,
            Sequence = sequence,
            DeliveryDate = request.DeliveryDate,
            DelayPenalty = request.DelayPenalty,
            SettlementMethod = Enum.Parse<SettlementMethod>(request.SettlementMethod),
            MaterialName = Enum.Parse<MaterialName>(request.MaterialName),
            ProductionStandardId = request.ProductionStandardId,
            DeliveryState = Enum.Parse<DeliveryState>(request.DeliveryState),
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
            LengthStatus = Enum.Parse<LengthStatus>(request.LengthStatus),
            MinLength = request.MinLength,
            MaxLength = CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength),
            Quantity = request.Quantity,
            Meters = meters,
            ContractWeight = request.ContractWeight,
            TheoreticalWeight = theoreticalWeight,
            Remark = request.Remark,
            CreatedBy = GetCurrentUser(),
            CreatedTime = DateTimeOffset.UtcNow,
            UpdatedBy = GetCurrentUser(),
            UpdatedTime = DateTimeOffset.UtcNow
        };
    }

    private void ValidateLengthStatus(string lengthStatus, decimal? minLength, decimal? maxLength)
    {
        var status = Enum.Parse<LengthStatus>(lengthStatus);
        
        switch (status)
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

    private decimal? CalculateMeters(string lengthStatus, decimal? minLength, decimal? maxLength, int? quantity, decimal? meters)
    {
        var status = Enum.Parse<LengthStatus>(lengthStatus);
        
        switch (status)
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

    private decimal CalculateMaxLength(string lengthStatus, decimal? minLength, decimal? maxLength)
    {
        var status = Enum.Parse<LengthStatus>(lengthStatus);
        
        switch (status)
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
        decimal outerDiameterNegative, decimal outerDiameterPositive,
        decimal wallThicknessNegative, decimal wallThicknessPositive,
        decimal? meters)
    {
        // 计算有效值
        var effectiveWallThickness = wallThickness - 0.5m * wallThicknessNegative + 0.5m * wallThicknessPositive;
        var effectiveOuterDiameter = outerDiameter - 0.5m * outerDiameterNegative + 0.5m * outerDiameterPositive;

        // 计算理算重量
        var weight = density * 3.1416m * effectiveWallThickness * (effectiveOuterDiameter - effectiveWallThickness) * (meters ?? 0) / 1000;
        
        // 边界处理
        if (weight < 0) weight = 0;
        
        return Math.Round(weight, 2);
    }

    private string GetCurrentUser()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
    }
}

public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}