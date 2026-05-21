using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;
using MES.Services.Mapping;

namespace MES.Services;

public class WarehouseService : IWarehouseService
{
    private readonly AppDbContext _context;

    public WarehouseService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<WarehouseDto>> GetPagedAsync(QueryParams query, bool? isActive = null)
    {
        var queryable = _context.Warehouses
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            queryable = queryable.Where(w =>
                w.Code.Contains(query.Keyword) ||
                w.Name.Contains(query.Keyword) ||
                (w.Remark != null && w.Remark.Contains(query.Keyword)));
        }

        if (isActive.HasValue)
        {
            queryable = queryable.Where(w => w.IsActive == isActive.Value);
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        // 排序（默认按 SortOrder 排序）
        var sortBy = query.SortBy ?? "sortorder";
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(w => w.ToDto())
            .ToListAsync();

        return new PagedResult<WarehouseDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<WarehouseDto>> GetAllAsync(bool onlyActive = true)
    {
        var query = _context.Warehouses
            .AsNoTracking();

        if (onlyActive)
        {
            query = query.Where(w => w.IsActive);
        }

        return await query
            .OrderBy(w => w.SortOrder)
            .Select(w => w.ToDto())
            .ToListAsync();
    }

    public async Task<WarehouseDto> GetByIdAsync(int id)
    {
        var entity = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("仓库不存在");

        return entity.ToDto();
    }

    public async Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request)
    {
        var exists = await _context.Warehouses
            .AnyAsync(w => w.Code == request.Code);

        if (exists)
            throw new BusinessException($"仓库代码 '{request.Code}' 已存在");

        var entity = new Warehouse
        {
            Code = request.Code,
            Name = request.Name,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            Remark = request.Remark
        };

        _context.Warehouses.Add(entity);
        await _context.SaveChangesAsync();

        return entity.ToDto();
    }

    public async Task<WarehouseDto> UpdateAsync(int id, UpdateWarehouseRequest request)
    {
        var entity = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("仓库不存在");

        if (!string.IsNullOrEmpty(request.Code) && request.Code != entity.Code)
        {
            var exists = await _context.Warehouses
                .AnyAsync(w => w.Code == request.Code && w.Id != id);
            if (exists)
                throw new BusinessException($"仓库代码 '{request.Code}' 已存在");
            entity.Code = request.Code;
        }

        if (!string.IsNullOrEmpty(request.Name))
            entity.Name = request.Name;
        if (request.SortOrder.HasValue)
            entity.SortOrder = request.SortOrder.Value;
        if (request.IsActive.HasValue)
            entity.IsActive = request.IsActive.Value;
        if (request.Remark != null)
            entity.Remark = request.Remark;

        await _context.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("仓库不存在");

        var hasBatches = await _context.InventoryBatches
            .AnyAsync(b => b.WarehouseId == id);

        if (hasBatches)
            throw new BusinessException("该仓库下存在库存批次，无法删除");

        _context.Warehouses.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
