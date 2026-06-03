using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Helpers;
using MES.Core.Exceptions;

namespace MES.Services.Configuration;

/// <summary>
/// 业务参数配置服务实现
/// </summary>
public class ConfigParameterService : IConfigParameterService
{
    private readonly AppDbContext _context;

    public ConfigParameterService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ConfigParameterDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.ConfigParameters
            .AsNoTracking()
            .AsQueryable();

        // 模糊搜索
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            queryable = queryable.Where(c =>
                c.Category.Contains(kw) ||
                c.ParamKey.Contains(kw) ||
                (c.Remark != null && c.Remark.Contains(kw)));
        }

        // 筛选
        if (query.Filters is { Count: > 0 })
            queryable = queryable.ApplyFilters(query.Filters);

        // 排序
        var sortBy = string.IsNullOrEmpty(query.SortBy) ? "CreatedTime" : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        // 分页
        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(c => new ConfigParameterDto
            {
                Id = c.Id,
                Category = c.Category,
                ParamKey = c.ParamKey,
                ParamValue = c.ParamValue,
                Remark = c.Remark
            })
            .ToListAsync();

        return new PagedResult<ConfigParameterDto>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<ConfigParameterDto?> GetByIdAsync(int id)
    {
        var entity = await _context.ConfigParameters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
            throw new BusinessException("参数配置不存在");

        return new ConfigParameterDto
        {
            Id = entity.Id,
            Category = entity.Category,
            ParamKey = entity.ParamKey,
            ParamValue = entity.ParamValue,
            Remark = entity.Remark
        };
    }

    public async Task<bool> SaveAsync(ConfigParameterDto dto)
    {
        if (dto.Id > 0)
        {
            var entity = await _context.ConfigParameters
                .FirstOrDefaultAsync(c => c.Id == dto.Id);
            if (entity == null)
                throw new BusinessException("参数配置不存在");

            entity.Category = dto.Category;
            entity.ParamKey = dto.ParamKey;
            entity.ParamValue = dto.ParamValue;
            entity.Remark = dto.Remark;
        }
        else
        {
            var entity = new ConfigParameter
            {
                Category = dto.Category,
                ParamKey = dto.ParamKey,
                ParamValue = dto.ParamValue,
                Remark = dto.Remark
            };
            _context.ConfigParameters.Add(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.ConfigParameters
            .FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null)
            throw new BusinessException("参数配置不存在");

        _context.ConfigParameters.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Dictionary<string, decimal>> GetConfigMapAsync(string category)
    {
        return await _context.ConfigParameters
            .AsNoTracking()
            .Where(c => c.Category == category)
            .ToDictionaryAsync(c => c.ParamKey, c => c.ParamValue, StringComparer.OrdinalIgnoreCase);
    }
}
