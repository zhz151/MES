using Microsoft.EntityFrameworkCore;
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
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Warehouse;
using MES.Services.Helpers;

namespace MES.Services.Warehouse;

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
            .Select(w => new WarehouseDto
            {
                Id = w.Id,
                Code = w.Code,
                Name = w.Name,
                SortOrder = w.SortOrder,
                IsActive = w.IsActive,
                Remark = w.Remark
            })
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
            .Select(w => new WarehouseDto
            {
                Id = w.Id,
                Code = w.Code,
                Name = w.Name,
                SortOrder = w.SortOrder,
                IsActive = w.IsActive,
                Remark = w.Remark
            })
            .ToListAsync();
    }

    public async Task<WarehouseDto> GetByIdAsync(int id)
    {
        var entity = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("仓库不存在");

        return ToDto(entity);
    }

    private static WarehouseDto ToDto(MES.Data.Entities.Warehouse.Warehouse entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        SortOrder = entity.SortOrder,
        IsActive = entity.IsActive,
        Remark = entity.Remark
    };

    public async Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request)
    {
        var exists = await _context.Warehouses
            .AnyAsync(w => w.Code == request.Code);

        if (exists)
            throw new BusinessException($"仓库代码 '{request.Code}' 已存在");

        var entity = new MES.Data.Entities.Warehouse.Warehouse
        {
            Code = request.Code,
            Name = request.Name,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            Remark = request.Remark
        };

        _context.Warehouses.Add(entity);
        await _context.SaveChangesAsync();

        return ToDto(entity);
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
        return ToDto(entity);
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

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.Warehouses.AsNoTracking();

        return new Dictionary<string, List<string>>
        {
            ["Code"] = await query.Select(w => w.Code).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToListAsync(),
            ["Name"] = await query.Select(w => w.Name).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToListAsync(),
            ["IsActive"] = await query.Select(w => w.IsActive.ToString()).Distinct().OrderBy(v => v).ToListAsync(),
            ["Remark"] = await query.Where(w => w.Remark != null).Select(w => w.Remark!).Distinct().OrderBy(v => v).ToListAsync(),
        };
    }
}
