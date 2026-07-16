using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Auth;
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
