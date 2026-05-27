// 文件路径: MES.Services/StandardProcessCycleService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Mapping;
using MES.Services.Helpers;

namespace MES.Services;

/// <summary>
/// 标准工艺生产周期服务实现
/// </summary>
public class StandardProcessCycleService : IStandardProcessCycleService
{
    private readonly AppDbContext _context;

    public StandardProcessCycleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<StandardProcessCycleDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.StandardProcessCycles
            .AsNoTracking()
            .AsQueryable();

        // 关键字模糊搜索（多关键词AND）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                queryable = queryable.Where(c =>
                    c.PlantGrade.Contains(keyword) ||
                    c.RawMaterialType.Contains(keyword) ||
                    c.RawSpec.Contains(keyword) ||
                    c.ProductSpec.Contains(keyword) ||
                    c.DeliveryState.Contains(keyword));
            }
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        // 排序（默认按工厂牌号排序）
        var sortBy = string.IsNullOrEmpty(query.SortBy) || query.SortBy.Equals("CreatedTime", StringComparison.OrdinalIgnoreCase)
            ? "PlantGrade"
            : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(c => new StandardProcessCycleDto
            {
                Id = c.Id,
                PlantGrade = c.PlantGrade,
                RawMaterialType = c.RawMaterialType,
                RawSpec = c.RawSpec,
                ProductSpec = c.ProductSpec,
                DeliveryState = c.DeliveryState,
                StandardCycleDays = c.StandardCycleDays
            })
            .ToListAsync();

        return new PagedResult<StandardProcessCycleDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<StandardProcessCycleDto>> GetAllAsync()
    {
        var items = await _context.StandardProcessCycles
            .AsNoTracking()
            .OrderBy(c => c.PlantGrade)
            .Select(c => new StandardProcessCycleDto
            {
                Id = c.Id,
                PlantGrade = c.PlantGrade,
                RawMaterialType = c.RawMaterialType,
                RawSpec = c.RawSpec,
                ProductSpec = c.ProductSpec,
                DeliveryState = c.DeliveryState,
                StandardCycleDays = c.StandardCycleDays
            })
            .ToListAsync();

        return items;
    }

    public async Task<StandardProcessCycleDto?> GetByIdAsync(int id)
    {
        var entity = await _context.StandardProcessCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
            throw new BusinessException("标准工艺生产周期不存在");

        return entity.ToDto();
    }

    public async Task<StandardProcessCycleDto> CreateAsync(CreateStandardProcessCycleRequest request)
    {
        var entity = new StandardProcessCycle
        {
            PlantGrade = request.PlantGrade,
            RawMaterialType = request.RawMaterialType,
            RawSpec = request.RawSpec,
            ProductSpec = request.ProductSpec,
            DeliveryState = request.DeliveryState,
            StandardCycleDays = request.StandardCycleDays
        };

        _context.StandardProcessCycles.Add(entity);
        await _context.SaveChangesAsync();

        return entity.ToDto();
    }

    public async Task<StandardProcessCycleDto> UpdateAsync(int id, UpdateStandardProcessCycleRequest request)
    {
        var entity = await _context.StandardProcessCycles
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
            throw new BusinessException("标准工艺生产周期不存在");

        if (!string.IsNullOrEmpty(request.PlantGrade))
            entity.PlantGrade = request.PlantGrade;

        if (!string.IsNullOrEmpty(request.RawMaterialType))
            entity.RawMaterialType = request.RawMaterialType;

        if (!string.IsNullOrEmpty(request.RawSpec))
            entity.RawSpec = request.RawSpec;

        if (!string.IsNullOrEmpty(request.ProductSpec))
            entity.ProductSpec = request.ProductSpec;

        if (!string.IsNullOrEmpty(request.DeliveryState))
            entity.DeliveryState = request.DeliveryState;

        if (request.StandardCycleDays.HasValue)
            entity.StandardCycleDays = request.StandardCycleDays.Value;

        await _context.SaveChangesAsync();

        return entity.ToDto();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.StandardProcessCycles
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
            throw new BusinessException("标准工艺生产周期不存在");

        _context.StandardProcessCycles.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.StandardProcessCycles.AsNoTracking();
        return new Dictionary<string, List<string>>
        {
            ["PlantGrade"] = await query.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToListAsync(),
            ["RawMaterialType"] = await query.Select(x => x.RawMaterialType).Distinct().OrderBy(x => x).ToListAsync(),
            ["RawSpec"] = await query.Select(x => x.RawSpec).Distinct().OrderBy(x => x).ToListAsync(),
            ["ProductSpec"] = await query.Select(x => x.ProductSpec).Distinct().OrderBy(x => x).ToListAsync(),
            ["DeliveryState"] = await query.Select(x => x.DeliveryState).Distinct().OrderBy(x => x).ToListAsync(),
        };
    }
}
