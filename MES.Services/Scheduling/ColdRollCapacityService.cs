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
/// 冷轧产能配置服务 — 产能档案查询与手工调整。
/// 排程保存反哺（正向）由 ColdRollSpecScheduleService.SaveAllAsync 内联完成；
/// 配置表手工调整（反向）在本服务 SaveAsync 中同步到排程小表已存在维度。
/// </summary>
public class ColdRollCapacityService : IColdRollCapacityService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public ColdRollCapacityService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<ColdRollCapacityDto>> GetAllAsync()
    {
        return await _context.ColdRollCapacities
            .AsNoTracking()
            .OrderBy(c => c.ProcessType)
            .ThenBy(c => c.BilletSpec)
            .ThenBy(c => c.RollingSpec)
            .ThenBy(c => c.IsFinished)
            .Select(c => ToDto(c))
            .ToListAsync();
    }

    public async Task<PagedResult<ColdRollCapacityDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.ColdRollCapacities.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(c =>
                c.ProcessType.Contains(kw) ||
                c.BilletSpec.Contains(kw) ||
                c.RollingSpec.Contains(kw) ||
                (c.MachineNo != null && c.MachineNo.Contains(kw)));
        }

        queryable = ApplyCapacitySort(queryable, query.SortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(c => ToDto(c))
            .ToListAsync();

        return new PagedResult<ColdRollCapacityDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<bool> SaveAsync(ColdRollCapacityDto dto)
    {
        if (dto.Id <= 0)
            throw new BusinessException("产能档案不存在，无法保存");

        var entity = await _context.ColdRollCapacities
            .FirstOrDefaultAsync(x => x.Id == dto.Id);
        if (entity == null)
            throw new BusinessException("产能档案不存在");

        entity.MachineNo = dto.MachineNo;
        entity.DailyOutput = dto.DailyOutput;
        entity.SampleCount++;
        entity.LastConfirmedAt = DateTimeOffset.Now;

        // 反向同步：配置表手工调整 → 排程小表已存在维度同步机台/日产能（不新增小表行）
        var schedule = await _context.ColdRollSpecSchedules
            .FirstOrDefaultAsync(s => s.ProcessType == entity.ProcessType
                && s.BilletSpec == entity.BilletSpec
                && s.RollingSpec == entity.RollingSpec
                && s.IsFinished == entity.IsFinished);
        if (schedule != null)
        {
            schedule.MachineNo = dto.MachineNo;
            schedule.DailyOutput = dto.DailyOutput;
        }

        await _context.SaveChangesAsync();

        // 产能/排程变更 → 失效排机估算与排程建议缓存（两者都依赖机台/单机单日量）
        _cache.Remove(ColdRollPlanService.MachineEstimateCacheKey);
        _cache.Remove(ColdRollPlanService.ScheduleSuggestionCacheKey);
        return true;
    }

    private static IQueryable<ColdRollCapacity> ApplyCapacitySort(IQueryable<ColdRollCapacity> query, string? sortBy, bool isDescending)
    {
        var key = string.IsNullOrEmpty(sortBy) ? "processtype" : sortBy.ToLowerInvariant();
        IOrderedQueryable<ColdRollCapacity> ordered = key switch
        {
            "billetspec" => isDescending ? query.OrderByDescending(c => c.BilletSpec) : query.OrderBy(c => c.BilletSpec),
            "rollingspec" => isDescending ? query.OrderByDescending(c => c.RollingSpec) : query.OrderBy(c => c.RollingSpec),
            "isfinished" => isDescending ? query.OrderByDescending(c => c.IsFinished) : query.OrderBy(c => c.IsFinished),
            "machineno" => isDescending ? query.OrderByDescending(c => c.MachineNo) : query.OrderBy(c => c.MachineNo),
            "dailyoutput" => isDescending ? query.OrderByDescending(c => c.DailyOutput) : query.OrderBy(c => c.DailyOutput),
            "samplecount" => isDescending ? query.OrderByDescending(c => c.SampleCount) : query.OrderBy(c => c.SampleCount),
            "lastconfirmedat" => isDescending ? query.OrderByDescending(c => c.LastConfirmedAt) : query.OrderBy(c => c.LastConfirmedAt),
            "updatedtime" => isDescending ? query.OrderByDescending(c => c.UpdatedTime) : query.OrderBy(c => c.UpdatedTime),
            _ => isDescending ? query.OrderByDescending(c => c.ProcessType) : query.OrderBy(c => c.ProcessType),
        };
        // 默认升序（processtype）追加规格维度二级排序，保持四维自然序
        if (!isDescending && key == "processtype")
            ordered = ordered.ThenBy(c => c.BilletSpec).ThenBy(c => c.RollingSpec).ThenBy(c => c.IsFinished);
        return ordered;
    }

    private static ColdRollCapacityDto ToDto(ColdRollCapacity entity)
    {
        return new ColdRollCapacityDto
        {
            Id = entity.Id,
            ProcessType = entity.ProcessType,
            BilletSpec = entity.BilletSpec,
            RollingSpec = entity.RollingSpec,
            IsFinished = entity.IsFinished,
            MachineNo = entity.MachineNo,
            DailyOutput = entity.DailyOutput,
            SampleCount = entity.SampleCount,
            LastConfirmedAt = entity.LastConfirmedAt?.DateTime,
            UpdatedTime = entity.UpdatedTime.DateTime,
        };
    }
}
