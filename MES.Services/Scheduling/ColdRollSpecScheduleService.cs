using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
using MES.Data;
using MES.Data.Entities.Scheduling;

namespace MES.Services.Scheduling;

/// <summary>
/// 冷轧排程服务 — 全量同步模式
/// </summary>
public class ColdRollSpecScheduleService : IColdRollSpecScheduleService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public ColdRollSpecScheduleService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<ColdRollSpecScheduleDto>> GetAllAsync()
    {
        return await _context.ColdRollSpecSchedules
            .AsNoTracking()
            .OrderBy(s => s.ProcessType)
            .ThenBy(s => s.RollingSpec)
            .Select(s => ToDto(s))
            .ToListAsync();
    }

    public async Task SaveAllAsync(List<ColdRollSpecScheduleDto> dtos)
    {
        var existingAll = await _context.ColdRollSpecSchedules.ToListAsync();
        var existingLookup = existingAll.ToDictionary(
            e => $"{e.ProcessType}|{e.BilletSpec}|{e.RollingSpec}|{e.IsFinished}",
            StringComparer.OrdinalIgnoreCase);

        var incomingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dto in dtos)
        {
            var key = $"{dto.ProcessType}|{dto.BilletSpec}|{dto.RollingSpec}|{dto.IsFinished}";
            incomingKeys.Add(key);

            if (existingLookup.TryGetValue(key, out var existing))
            {
                // 覆盖
                existing.MachineNo = dto.MachineNo;
                existing.DailyOutput = dto.DailyOutput;
                existing.CompletionType = string.IsNullOrEmpty(dto.CompletionType) ? "None" : dto.CompletionType;
                existing.RollType = dto.RollType;
                existing.MergeDisplay = dto.MergeDisplay;
                existing.Remark = dto.Remark;
            }
            else
            {
                // 新增
                _context.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
                {
                    ProcessType = dto.ProcessType,
                    BilletSpec = dto.BilletSpec,
                    RollingSpec = dto.RollingSpec,
                    IsFinished = dto.IsFinished,
                    MachineNo = dto.MachineNo,
                    DailyOutput = dto.DailyOutput,
                    CompletionType = string.IsNullOrEmpty(dto.CompletionType) ? "None" : dto.CompletionType,
                    RollType = string.IsNullOrEmpty(dto.RollType) ? "None" : dto.RollType,
                    MergeDisplay = dto.MergeDisplay,
                    Remark = dto.Remark,
                });
            }
        }

        // 删除僵尸数据：不在当前页面维度中的旧记录
        var toDelete = existingAll.Where(e =>
            !incomingKeys.Contains($"{e.ProcessType}|{e.BilletSpec}|{e.RollingSpec}|{e.IsFinished}"));
        _context.ColdRollSpecSchedules.RemoveRange(toDelete);

        // 排程保存 → 自动反哺产能档案（有产能信息的行 upsert 到 ColdRollCapacity，随主保存同事务提交）
        await ReverseFillCapacityAsync(dtos);

        await _context.SaveChangesAsync();

        // 排程变更 → 失效排机估算与排程建议缓存（两者都依赖排程档位/单机单日量）
        _cache.Remove(ColdRollPlanService.MachineEstimateCacheKey);
        _cache.Remove(ColdRollPlanService.ScheduleSuggestionCacheKey);
    }

    /// <summary>
    /// 反哺产能档案：遍历排程行，有产能信息（日产能或机台任一非空）的按四维键 upsert ColdRollCapacity。
    /// 无产能信息跳过（清空产能是暂态，不覆盖不清除）；产能档案累积，不随排程小表僵尸清理删除。
    /// 查重用请求内局部字典（新增后回写），防同请求重复维度触发唯一索引冲突。
    /// </summary>
    private async Task ReverseFillCapacityAsync(List<ColdRollSpecScheduleDto> dtos)
    {
        var capacityAll = await _context.ColdRollCapacities.ToListAsync();
        var capacityLookup = capacityAll.ToDictionary(
            c => $"{c.ProcessType}|{c.BilletSpec}|{c.RollingSpec}|{c.IsFinished}",
            StringComparer.OrdinalIgnoreCase);

        foreach (var dto in dtos)
        {
            if (!dto.DailyOutput.HasValue && string.IsNullOrWhiteSpace(dto.MachineNo))
                continue;

            var key = $"{dto.ProcessType}|{dto.BilletSpec}|{dto.RollingSpec}|{dto.IsFinished}";
            if (capacityLookup.TryGetValue(key, out var existing))
            {
                existing.MachineNo = dto.MachineNo;
                existing.DailyOutput = dto.DailyOutput;
                existing.SampleCount++;
                existing.LastConfirmedAt = DateTimeOffset.Now;
            }
            else
            {
                var added = new ColdRollCapacity
                {
                    ProcessType = dto.ProcessType,
                    BilletSpec = dto.BilletSpec,
                    RollingSpec = dto.RollingSpec,
                    IsFinished = dto.IsFinished,
                    MachineNo = dto.MachineNo,
                    DailyOutput = dto.DailyOutput,
                    SampleCount = 1,
                    LastConfirmedAt = DateTimeOffset.Now,
                };
                _context.ColdRollCapacities.Add(added);
                capacityLookup[key] = added;
            }
        }
    }

    private static ColdRollSpecScheduleDto ToDto(ColdRollSpecSchedule entity)
    {
        return new ColdRollSpecScheduleDto
        {
            Id = entity.Id,
            ProcessType = entity.ProcessType,
            BilletSpec = entity.BilletSpec,
            RollingSpec = entity.RollingSpec,
            IsFinished = entity.IsFinished,
            MachineNo = entity.MachineNo,
            DailyOutput = entity.DailyOutput,
            CompletionType = entity.CompletionType,
            RollType = entity.RollType,
            MergeDisplay = entity.MergeDisplay,
            Remark = entity.Remark,
            UpdatedTime = entity.UpdatedTime.DateTime,
        };
    }
}
