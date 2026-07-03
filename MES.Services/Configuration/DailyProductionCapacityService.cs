using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Helpers;

namespace MES.Services.Configuration;

public class DailyProductionCapacityService : IDailyProductionCapacityService
{
    private readonly AppDbContext _context;

    public DailyProductionCapacityService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<DailyProductionCapacityDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.DailyProductionCapacities
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(x =>
                x.ProcessName.Contains(kw) ||
                (x.Remark != null && x.Remark.Contains(kw)));
        }

        queryable = queryable.ApplyFilters(query.Filters);

        var sortBy = string.IsNullOrEmpty(query.SortBy) || query.SortBy.Equals("CreatedTime", StringComparison.OrdinalIgnoreCase)
            ? "ProcessName"
            : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(x => new DailyProductionCapacityDto
            {
                Id = x.Id,
                ProcessName = x.ProcessName,
                DailyCapacity = x.DailyCapacity,
                Remark = x.Remark
            })
            .ToListAsync();

        return new PagedResult<DailyProductionCapacityDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<DailyProductionCapacityDto>> GetAllAsync()
    {
        return await _context.DailyProductionCapacities
            .AsNoTracking()
            .OrderBy(x => x.ProcessName)
            .Select(x => new DailyProductionCapacityDto
            {
                Id = x.Id,
                ProcessName = x.ProcessName,
                DailyCapacity = x.DailyCapacity,
                Remark = x.Remark
            })
            .ToListAsync();
    }

    public async Task<bool> SaveAsync(DailyProductionCapacityDto dto)
    {
        if (dto.Id > 0)
        {
            var entity = await _context.DailyProductionCapacities
                .FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (entity == null)
                throw new BusinessException("记录不存在");

            entity.ProcessName = dto.ProcessName;
            entity.DailyCapacity = dto.DailyCapacity;
            entity.Remark = dto.Remark;
        }
        else
        {
            var entity = new DailyProductionCapacity
            {
                ProcessName = dto.ProcessName,
                DailyCapacity = dto.DailyCapacity,
                Remark = dto.Remark
            };
            _context.DailyProductionCapacities.Add(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.DailyProductionCapacities
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            throw new BusinessException("记录不存在");

        _context.DailyProductionCapacities.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}
