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
using MES.Data.Entities.Configuration;
using MES.Services.Helpers;
using MES.Core.Exceptions;
using MES.Core.Helpers;

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
                (c.CategoryDisplay != null && c.CategoryDisplay.Contains(kw)) ||
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
                CategoryDisplay = c.CategoryDisplay,
                Context = c.Context,
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
            CategoryDisplay = entity.CategoryDisplay,
            Context = entity.Context,
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
            entity.CategoryDisplay = dto.CategoryDisplay;
            entity.Context = dto.Context;
            entity.ParamKey = dto.ParamKey;
            entity.ParamValue = dto.ParamValue;
            entity.Remark = dto.Remark;
        }
        else
        {
            var entity = new ConfigParameter
            {
                Category = dto.Category,
                CategoryDisplay = dto.CategoryDisplay,
                Context = dto.Context,
                ParamKey = dto.ParamKey,
                ParamValue = dto.ParamValue,
                Remark = dto.Remark
            };
            _context.ConfigParameters.Add(entity);
        }

        await _context.SaveChangesAsync();
        await RefreshMaterialPlanToleranceSnapshotAsync(dto.Category);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.ConfigParameters
            .FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null)
            throw new BusinessException("参数配置不存在");

        var category = entity.Category;
        _context.ConfigParameters.Remove(entity);
        await _context.SaveChangesAsync();
        await RefreshMaterialPlanToleranceSnapshotAsync(category);
        return true;
    }

    public async Task<Dictionary<string, decimal>> GetConfigMapAsync(string category)
    {
        return await _context.ConfigParameters
            .AsNoTracking()
            .Where(c => c.Category == category)
            .ToDictionaryAsync(c => c.ParamKey, c => c.ParamValue, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var contexts = await _context.ConfigParameters
            .AsNoTracking()
            .Where(c => c.Context != null)
            .Select(c => c.Context!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["Context"] = contexts
        };
    }

    private const string MaterialPlanToleranceCategory = "MaterialPlanTolerance";

    /// <summary>
    /// MaterialPlanTolerance 类目写操作后刷新 MaterialPlanToleranceProvider 静态快照，
    /// 使到料实投一致性容差改配置表保存即生效（与 DictValueDefinitionService.RefreshStaticSnapshotAsync 同模式）。
    /// </summary>
    private async Task RefreshMaterialPlanToleranceSnapshotAsync(string? category)
    {
        if (!string.Equals(category, MaterialPlanToleranceCategory, StringComparison.OrdinalIgnoreCase))
            return;
        var map = await GetConfigMapAsync(MaterialPlanToleranceCategory);
        MaterialPlanToleranceProvider.Apply(map.GetValueOrDefault("InputConsistencyTolerance"));
    }
}
