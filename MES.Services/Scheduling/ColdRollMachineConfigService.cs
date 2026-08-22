using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.DTOs.Scheduling;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Scheduling;

namespace MES.Services.Scheduling;

/// <summary>
/// 冷轧机台数配置服务 —— 机台数参数表查询与维护（纯手工参数）。
/// 保存/删除后失效排机估算与排程建议缓存（机台数/估算日产影响两者输出）。
/// </summary>
public class ColdRollMachineConfigService : IColdRollMachineConfigService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public ColdRollMachineConfigService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<ColdRollMachineConfigDto>> GetAllAsync()
    {
        return await _context.ColdRollMachineConfigs
            .AsNoTracking()
            .OrderBy(c => c.ProcessType)
            .Select(c => ToDto(c))
            .ToListAsync();
    }

    public async Task<PagedResult<ColdRollMachineConfigDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.ColdRollMachineConfigs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(c =>
                c.ProcessType.Contains(kw) ||
                (c.Remark != null && c.Remark.Contains(kw)));
        }

        queryable = ApplySort(queryable, query.SortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(c => ToDto(c))
            .ToListAsync();

        return new PagedResult<ColdRollMachineConfigDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<bool> SaveAsync(ColdRollMachineConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProcessType))
            throw new BusinessException("机型不能为空");
        if (dto.OwnedCount < 0 || dto.MinMachines < 0 || dto.MaxMachines < 0)
            throw new BusinessException("机台数不能为负");
        if (dto.MinMachines > dto.MaxMachines)
            throw new BusinessException("最小机台数不能大于最大机台数");

        if (dto.Id > 0)
        {
            var entity = await _context.ColdRollMachineConfigs
                .FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (entity == null)
                throw new BusinessException("机台数配置不存在");

            // 机型唯一冲突：已存在其他行使用同一机型
            var dup = await _context.ColdRollMachineConfigs
                .AnyAsync(x => x.ProcessType == dto.ProcessType && x.Id != dto.Id);
            if (dup)
                throw new BusinessException($"机型 {dto.ProcessType} 已存在配置");

            entity.ProcessType = dto.ProcessType;
            entity.OwnedCount = dto.OwnedCount;
            entity.MinMachines = dto.MinMachines;
            entity.MaxMachines = dto.MaxMachines;
            entity.EstimatedDailyOutput = dto.EstimatedDailyOutput;
            entity.Remark = dto.Remark;
        }
        else
        {
            var dup = await _context.ColdRollMachineConfigs
                .AnyAsync(x => x.ProcessType == dto.ProcessType);
            if (dup)
                throw new BusinessException($"机型 {dto.ProcessType} 已存在配置");

            _context.ColdRollMachineConfigs.Add(new ColdRollMachineConfig
            {
                ProcessType = dto.ProcessType,
                OwnedCount = dto.OwnedCount,
                MinMachines = dto.MinMachines,
                MaxMachines = dto.MaxMachines,
                EstimatedDailyOutput = dto.EstimatedDailyOutput,
                Remark = dto.Remark,
            });
        }

        await _context.SaveChangesAsync();

        // 机台数/估算日产变更 → 失效排机估算 + 排程建议缓存
        _cache.Remove(ColdRollPlanService.MachineEstimateCacheKey);
        _cache.Remove(ColdRollPlanService.ScheduleSuggestionCacheKey);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.ColdRollMachineConfigs
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            throw new BusinessException("机台数配置不存在");

        _context.ColdRollMachineConfigs.Remove(entity);
        await _context.SaveChangesAsync();

        _cache.Remove(ColdRollPlanService.MachineEstimateCacheKey);
        _cache.Remove(ColdRollPlanService.ScheduleSuggestionCacheKey);
        return true;
    }

    private static IQueryable<ColdRollMachineConfig> ApplySort(IQueryable<ColdRollMachineConfig> query, string? sortBy, bool isDescending)
    {
        var key = string.IsNullOrEmpty(sortBy) ? "processtype" : sortBy.ToLowerInvariant();
        return key switch
        {
            "ownedcount" => isDescending ? query.OrderByDescending(c => c.OwnedCount) : query.OrderBy(c => c.OwnedCount),
            "minmachines" => isDescending ? query.OrderByDescending(c => c.MinMachines) : query.OrderBy(c => c.MinMachines),
            "maxmachines" => isDescending ? query.OrderByDescending(c => c.MaxMachines) : query.OrderBy(c => c.MaxMachines),
            "estimateddailyoutput" => isDescending ? query.OrderByDescending(c => c.EstimatedDailyOutput) : query.OrderBy(c => c.EstimatedDailyOutput),
            "updatedtime" => isDescending ? query.OrderByDescending(c => c.UpdatedTime) : query.OrderBy(c => c.UpdatedTime),
            _ => isDescending ? query.OrderByDescending(c => c.ProcessType) : query.OrderBy(c => c.ProcessType),
        };
    }

    private static ColdRollMachineConfigDto ToDto(ColdRollMachineConfig entity)
    {
        return new ColdRollMachineConfigDto
        {
            Id = entity.Id,
            ProcessType = entity.ProcessType,
            OwnedCount = entity.OwnedCount,
            MinMachines = entity.MinMachines,
            MaxMachines = entity.MaxMachines,
            EstimatedDailyOutput = entity.EstimatedDailyOutput,
            Remark = entity.Remark,
            UpdatedTime = entity.UpdatedTime.DateTime,
        };
    }
}
