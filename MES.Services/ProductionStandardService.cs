// 文件路径: MES.Services/ProductionStandardService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// 产品标准服务实现
/// </summary>
public class ProductionStandardService : IProductionStandardService
{
    private readonly AppDbContext _context;

    public ProductionStandardService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取所有产品标准（用于下拉框）
    /// </summary>
    /// <param name="onlyActive">是否只返回启用的标准，默认true</param>
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
    /// 根据ID获取产品标准详情
    /// </summary>
    public async Task<ProductionStandardDto> GetByIdAsync(int id)
    {
        var entity = await _context.ProductionStandards
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("产品标准不存在");
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
    /// 创建产品标准
    /// </summary>
    public async Task<ProductionStandardDto> CreateAsync(CreateProductionStandardRequest request)
    {
        // 检查标准编码唯一性
        var exists = await _context.ProductionStandards
            .AnyAsync(p => p.StandardCode == request.StandardCode && !p.IsDeleted);

        if (exists)
        {
            throw new BusinessException($"标准编码 '{request.StandardCode}' 已存在");
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
    /// 更新产品标准
    /// </summary>
    public async Task<ProductionStandardDto> UpdateAsync(int id, UpdateProductionStandardRequest request)
    {
        var entity = await _context.ProductionStandards
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("产品标准不存在");
        }

        // 检查标准编码唯一性（排除自身）
        if (!string.IsNullOrEmpty(request.StandardCode) && request.StandardCode != entity.StandardCode)
        {
            var exists = await _context.ProductionStandards
                .AnyAsync(p => p.StandardCode == request.StandardCode && p.Id != id && !p.IsDeleted);

            if (exists)
            {
                throw new BusinessException($"标准编码 '{request.StandardCode}' 已存在");
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
    /// 删除产品标准（软删除）
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.ProductionStandards
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("产品标准不存在");
        }

        entity.IsDeleted = true;
        await _context.SaveChangesAsync();
    }
}