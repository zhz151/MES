// 文件路径: MES.Services/ProductionStandardService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;  
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// Production standard service implementation
/// </summary>
public class ProductionStandardService : IProductionStandardService
{
    private readonly AppDbContext _context;

    public ProductionStandardService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all production standards (for dropdown)
    /// </summary>
    /// <param name="onlyActive">Whether to return only active standards, default true</param>
    public async Task<List<ProductionStandardDto>> GetAllAsync(bool onlyActive = true)
    {
        var query = _context.ProductionStandards
            .Where(p => !p.IsDeleted);

        if (onlyActive)
        {
            query = query.Where(p => p.IsActive);
        }

        var items = await query
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.StandardCode)
            .Select(p => new ProductionStandardDto
            {
                Id = p.Id,
                StandardCode = p.StandardCode,
                StandardName = p.StandardName,
                Remark = p.Remark,
                SortOrder = p.SortOrder,
                IsActive = p.IsActive
            })
            .ToListAsync();

        return items;
    }

    /// <summary>
    /// Get production standard details by ID
    /// </summary>
    public async Task<ProductionStandardDto> GetByIdAsync(int id)
    {
        var entity = await _context.ProductionStandards
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Production standard does not exist");
        }

        return new ProductionStandardDto
        {
            Id = entity.Id,
            StandardCode = entity.StandardCode,
            StandardName = entity.StandardName,
            Remark = entity.Remark,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive
        };
    }

    /// <summary>
    /// Create production standard
    /// </summary>
    public async Task<ProductionStandardDto> CreateAsync(CreateProductionStandardRequest request)
    {
        // Check standard code uniqueness
        var exists = await _context.ProductionStandards
            .AnyAsync(p => p.StandardCode == request.StandardCode && !p.IsDeleted);

        if (exists)
        {
            throw new BusinessException($"Standard code '{request.StandardCode}' already exists");
        }

        var entity = new ProductionStandard
        {
            StandardCode = request.StandardCode,
            StandardName = request.StandardName,
            Remark = request.Remark,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };

        _context.ProductionStandards.Add(entity);
        await _context.SaveChangesAsync();

        return new ProductionStandardDto
        {
            Id = entity.Id,
            StandardCode = entity.StandardCode,
            StandardName = entity.StandardName,
            Remark = entity.Remark,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive
        };
    }

    /// <summary>
    /// Update production standard
    /// </summary>
    public async Task<ProductionStandardDto> UpdateAsync(int id, UpdateProductionStandardRequest request)
    {
        var entity = await _context.ProductionStandards
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Production standard does not exist");
        }

        // Check standard code uniqueness (exclude self)
        if (!string.IsNullOrEmpty(request.StandardCode) && request.StandardCode != entity.StandardCode)
        {
            var exists = await _context.ProductionStandards
                .AnyAsync(p => p.StandardCode == request.StandardCode && p.Id != id && !p.IsDeleted);

            if (exists)
            {
                throw new BusinessException($"Standard code '{request.StandardCode}' already exists");
            }
            entity.StandardCode = request.StandardCode;
        }

        if (!string.IsNullOrEmpty(request.StandardName))
        {
            entity.StandardName = request.StandardName;
        }

        if (request.Remark != null)
        {
            entity.Remark = request.Remark;
        }

        if (request.SortOrder.HasValue)
        {
            entity.SortOrder = request.SortOrder.Value;
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        await _context.SaveChangesAsync();

        return new ProductionStandardDto
        {
            Id = entity.Id,
            StandardCode = entity.StandardCode,
            StandardName = entity.StandardName,
            Remark = entity.Remark,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive
        };
    }

    /// <summary>
    /// Delete production standard (soft delete)
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.ProductionStandards
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Production standard does not exist");
        }

        entity.IsDeleted = true;
        await _context.SaveChangesAsync();
    }
}