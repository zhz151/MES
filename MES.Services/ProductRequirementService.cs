// 文件路径: MES.Services/Order/ProductRequirementService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Order;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Mapping;

namespace MES.Services.Order;

public class ProductRequirementService : IProductRequirementService
{
    private readonly AppDbContext _context;

    public ProductRequirementService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ProductRequirementDto?> GetByOrderItemIdAsync(int orderItemId)
    {
        var entity = await _context.ProductRequirements
            .AsNoTracking()
            .FirstOrDefaultAsync(pr => pr.OrderItemId == orderItemId && !pr.IsDeleted);

        if (entity == null) return null;
        return await MapToDtoWithSequenceAsync(entity);
    }

    public async Task<ProductRequirementDto> CreateOrUpdateAsync(int orderItemId, CreateProductRequirementRequest request)
    {
        var orderItem = await _context.OrderItems
            .FirstOrDefaultAsync(oi => oi.Id == orderItemId && !oi.IsDeleted);
        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        var existing = await _context.ProductRequirements
            .FirstOrDefaultAsync(pr => pr.OrderItemId == orderItemId && !pr.IsDeleted);

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

            await _context.SaveChangesAsync();
            return await MapToDtoWithSequenceAsync(existing, orderItem.Sequence);
        }
        else
        {
            // 只有用户主动保存时才创建新记录
            var entity = new ProductRequirement
            {
                OrderItemId = orderItemId,
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
            .Where(oi => oi.SalesOrderId == orderId && !oi.IsDeleted)
            .OrderBy(oi => oi.Sequence)
            .ToListAsync();

        // 获取所有存在的技术要求
        var orderItemIds = orderItems.Select(oi => oi.Id).ToList();
        var existingRequirements = await _context.ProductRequirements
            .Where(pr => orderItemIds.Contains(pr.OrderItemId) && !pr.IsDeleted)
            .ToDictionaryAsync(pr => pr.OrderItemId, pr => pr);

        var result = new List<ProductRequirementDto>();
        
        foreach (var item in orderItems)
        {
            if (existingRequirements.TryGetValue(item.Id, out var requirement))
            {
                // 有技术要求，添加到结果
                result.Add(requirement.ToDto(item.Sequence));
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
                .FirstOrDefaultAsync(oi => oi.Id == entity.OrderItemId && !oi.IsDeleted);
            sequence = orderItem?.Sequence ?? 0;
        }

        return entity.ToDto(sequence);
    }
}