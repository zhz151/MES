// 文件路径: MES.Services/Order/ProductRequirementService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Order;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services.Order;

/// <summary>
/// 产品要求服务实现
/// </summary>
public class ProductRequirementService : IProductRequirementService
{
    private readonly AppDbContext _context;

    public ProductRequirementService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 根据订单项次ID获取产品要求
    /// </summary>
    public async Task<ProductRequirementDto?> GetByOrderItemIdAsync(int orderItemId)
    {
        var entity = await _context.ProductRequirements
            .FirstOrDefaultAsync(pr => pr.OrderItemId == orderItemId && !pr.IsDeleted);

        if (entity == null)
        {
            return null;
        }

        return MapToDto(entity);
    }

    /// <summary>
    /// 创建或更新产品要求
    /// </summary>
    public async Task<ProductRequirementDto> CreateOrUpdateAsync(int orderItemId, CreateProductRequirementRequest request)
    {
        // 验证订单项次是否存在
        var orderItem = await _context.OrderItems
            .FirstOrDefaultAsync(oi => oi.Id == orderItemId && !oi.IsDeleted);

        if (orderItem == null)
        {
            throw new BusinessException("订单项次不存在");
        }

        // 查找是否已存在产品要求
        var existing = await _context.ProductRequirements
            .FirstOrDefaultAsync(pr => pr.OrderItemId == orderItemId && !pr.IsDeleted);

        if (existing != null)
        {
            // 更新
            existing.RequirementType = request.RequirementType;
            existing.ChemicalComposition = request.ChemicalComposition;
            existing.MechanicalProperty = request.MechanicalProperty;
            existing.ToleranceRequirement = request.ToleranceRequirement;
            existing.SurfaceQuality = request.SurfaceQuality;
            existing.NdtRequirement = request.NdtRequirement;
            existing.OtherRequirement = request.OtherRequirement;

            await _context.SaveChangesAsync();

            return MapToDto(existing);
        }
        else
        {
            // 创建
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

            return MapToDto(entity);
        }
    }

    /// <summary>
    /// 删除产品要求
    /// </summary>
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

    private static ProductRequirementDto MapToDto(ProductRequirement entity)
    {
        return new ProductRequirementDto
        {
            Id = entity.Id,
            OrderItemId = entity.OrderItemId,
            RequirementType = entity.RequirementType,
            ChemicalComposition = entity.ChemicalComposition,
            MechanicalProperty = entity.MechanicalProperty,
            ToleranceRequirement = entity.ToleranceRequirement,
            SurfaceQuality = entity.SurfaceQuality,
            NdtRequirement = entity.NdtRequirement,
            OtherRequirement = entity.OtherRequirement
        };
    }
}