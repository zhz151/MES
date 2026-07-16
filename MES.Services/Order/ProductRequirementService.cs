// 文件路径: MES.Services/ProductRequirementService.cs
using Microsoft.EntityFrameworkCore;
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
using MES.Services.Order;

namespace MES.Services.Order;

public class ProductRequirementService : IProductRequirementService
{
    private readonly AppDbContext _context;
    private readonly IOrderService _orderService;

    public ProductRequirementService(AppDbContext context, IOrderService orderService)
    {
        _context = context;
        _orderService = orderService;
    }

    public async Task<ProductRequirementDto?> GetByOrderItemIdAsync(int orderItemId)
    {
        var entity = await _context.ProductRequirements
            .AsNoTracking()
            .FirstOrDefaultAsync(pr => pr.OrderItemId == orderItemId);

        if (entity == null) return null;
        return await MapToDtoWithSequenceAsync(entity);
    }

    public async Task<ProductRequirementDto> CreateOrUpdateAsync(int orderItemId, CreateProductRequirementRequest request)
    {
        var orderItem = await _context.OrderItems
            .FirstOrDefaultAsync(oi => oi.Id == orderItemId);
        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        var orderNo = orderItem.SalesOrder?.OrderNumber ?? await _context.SalesOrders
            .Where(so => so.Id == orderItem.SalesOrderId)
            .Select(so => so.OrderNumber)
            .FirstOrDefaultAsync() ?? "";

        var existing = await _context.ProductRequirements
            .FirstOrDefaultAsync(pr => pr.OrderItemId == orderItemId);

        if (existing != null)
        {
            // 更新现有技术要求
            existing.RequirementType = request.RequirementType;
            existing.ChemicalComposition = request.ChemicalComposition;
            existing.MechanicalProperty = request.MechanicalProperty;
            existing.ToleranceRequirement = request.ToleranceRequirement;
            existing.SurfaceQuality = request.SurfaceQuality;
            existing.NdtRequirement = request.NdtRequirement;
            existing.OtherRequirement = request.OtherRequirement;
            existing.OrderNo = orderNo;
            existing.ItemSequence = orderItem.Sequence;

            await _context.SaveChangesAsync();
            await _orderService.RefreshByOrderIdAsync(orderItem.SalesOrderId);
            return await MapToDtoWithSequenceAsync(existing, orderItem.Sequence);
        }
        else
        {
            // 只有用户主动保存时才创建新记录
            var entity = new ProductRequirement
            {
                OrderItemId = orderItemId,
                OrderNo = orderNo,
                ItemSequence = orderItem.Sequence,
                RequirementType = request.RequirementType,
                ChemicalComposition = request.ChemicalComposition,
                MechanicalProperty = request.MechanicalProperty,
                ToleranceRequirement = request.ToleranceRequirement,
                SurfaceQuality = request.SurfaceQuality,
                NdtRequirement = request.NdtRequirement,
                OtherRequirement = request.OtherRequirement
            };

            _context.ProductRequirements.Add(entity);
            await _context.SaveChangesAsync();
            await _orderService.RefreshByOrderIdAsync(orderItem.SalesOrderId);
            return await MapToDtoWithSequenceAsync(entity, orderItem.Sequence);
        }
    }

    public Task DeleteAsync(int orderItemId)
    {
        throw new BusinessException("技术要求不允许单独删除，请删除对应的订单项次");
    }

    /// <summary>
    /// 根据订单ID获取所有项次的产品要求列表
    /// 注意：只返回真正存在的技术要求，不自动创建空对象
    /// </summary>
    public async Task<List<ProductRequirementDto>> GetByOrderIdAsync(int orderId)
    {
        // 获取订单的所有项次
        var orderItems = await _context.OrderItems
            .Where(oi => oi.SalesOrderId == orderId)
            .OrderBy(oi => oi.Sequence)
            .ToListAsync();

        // 获取所有存在的技术要求
        var orderItemIds = orderItems.Select(oi => oi.Id).ToList();
        var existingRequirements = await _context.ProductRequirements
            .Where(pr => orderItemIds.Contains(pr.OrderItemId))
            .ToDictionaryAsync(pr => pr.OrderItemId, pr => pr);

        var result = new List<ProductRequirementDto>();

        foreach (var item in orderItems)
        {
            if (existingRequirements.TryGetValue(item.Id, out var requirement))
            {
                // 有技术要求，添加到结果
                result.Add(ToDto(requirement, item.Sequence));
            }
            // 没有技术要求：不添加到结果，保持 null/不存在状态
        }

        return result;
    }

    private async Task<ProductRequirementDto> MapToDtoWithSequenceAsync(ProductRequirement entity, int? explicitSequence = null)
    {
        int sequence;
        if (explicitSequence.HasValue)
        {
            sequence = explicitSequence.Value;
        }
        else
        {
            var orderItem = await _context.OrderItems
                .FirstOrDefaultAsync(oi => oi.Id == entity.OrderItemId);
            sequence = orderItem?.Sequence ?? 0;
        }

        return ToDto(entity, sequence);
    }

    private static ProductRequirementDto ToDto(ProductRequirement entity, int sequence) => new()
    {
        Id = entity.Id,
        OrderItemId = entity.OrderItemId,
        Sequence = sequence,
        RequirementType = entity.RequirementType,
        ChemicalComposition = entity.ChemicalComposition,
        MechanicalProperty = entity.MechanicalProperty,
        ToleranceRequirement = entity.ToleranceRequirement,
        SurfaceQuality = entity.SurfaceQuality,
        NdtRequirement = entity.NdtRequirement,
        OtherRequirement = entity.OtherRequirement,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };
}