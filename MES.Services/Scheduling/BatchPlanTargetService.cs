using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities.Scheduling;

namespace MES.Services.Scheduling;

/// <summary>
/// 批次计划产量目标服务
/// </summary>
public class BatchPlanTargetService : IBatchPlanTargetService
{
    private readonly AppDbContext _context;

    public BatchPlanTargetService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<BatchPlanTargetDto>> GetAllAsync()
    {
        return await _context.BatchPlanTargets
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => ToDto(t))
            .ToListAsync();
    }

    public async Task<bool> SaveAllAsync(List<BatchPlanTargetDto> dtos)
    {
        // 全量覆盖：删除全部旧记录，写入新记录
        var existing = await _context.BatchPlanTargets.ToListAsync();
        _context.BatchPlanTargets.RemoveRange(existing);

        foreach (var dto in dtos)
        {
            _context.BatchPlanTargets.Add(new BatchPlanTarget
            {
                SectionName = dto.SectionName,
                DailyTarget = dto.DailyTarget,
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    private static BatchPlanTargetDto ToDto(BatchPlanTarget entity) => new()
    {
        Id = entity.Id,
        SectionName = entity.SectionName,
        DailyTarget = entity.DailyTarget,
    };
}
