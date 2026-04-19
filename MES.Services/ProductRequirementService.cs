// 文件路径: MES.Services/Order/ProductRequirementService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Order;
using MES.Data;
using MES.Data.Entities;

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
            .FirstOrDefaultAsync(pr => pr.OrderItemId == orderItemId && !pr.IsDeleted);

        if (entity == null) return null;
        return await MapToDtoWithSequence(entity);
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
            existing.RequirementType = request.RequirementType;
            existing.ChemicalComposition = request.ChemicalComposition;
            existing.MechanicalProperty = request.MechanicalProperty;
            existing.ToleranceRequirement = request.ToleranceRequirement;
            existing.SurfaceQuality = request.SurfaceQuality;
            existing.NdtRequirement = request.NdtRequirement;
            existing.OtherRequirement = request.OtherRequirement;

            await _context.SaveChangesAsync();
            return await MapToDtoWithSequence(existing);
        }
        else
        {
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
            return await MapToDtoWithSequence(entity);
        }
    }

    public async Task DeleteAsync(int orderItemId)
    {
        var entity = await _context.ProductRequirements
            .FirstOrDefaultAsync(pr => pr.OrderItemId == orderItemId && !pr.IsDeleted);
        if (entity != null)
        {
            entity.IsDeleted = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<ProductRequirementDto>> GetByOrderIdAsync(int orderId)
    {
        var orderItems = await _context.OrderItems
            .Where(oi => oi.SalesOrderId == orderId && !oi.IsDeleted)
            .Include(oi => oi.ProductRequirement)
            .OrderBy(oi => oi.Sequence)
            .ToListAsync();

        var result = new List<ProductRequirementDto>();
        foreach (var item in orderItems)
        {
            var requirement = item.ProductRequirement;
            if (requirement != null && !requirement.IsDeleted)
            {
                result.Add(await MapToDtoWithSequence(requirement, item.Sequence));
            }
            else
            {
                // 没有技术要求时，返回一个空的 DTO（仅包含项次号）
                result.Add(new ProductRequirementDto
                {
                    Id = 0,
                    OrderItemId = item.Id,
                    Sequence = item.Sequence,
                    RequirementType = RequirementType.Normal,
                    ChemicalComposition = null,
                    MechanicalProperty = null,
                    ToleranceRequirement = null,
                    SurfaceQuality = null,
                    NdtRequirement = null,
                    OtherRequirement = null,
                    CreatedTime = item.CreatedTime,
                    UpdatedTime = item.UpdatedTime
                });
            }
        }
        return result;
    }

    private async Task<ProductRequirementDto> MapToDtoWithSequence(ProductRequirement entity, int? explicitSequence = null)
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

        return new ProductRequirementDto
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
}