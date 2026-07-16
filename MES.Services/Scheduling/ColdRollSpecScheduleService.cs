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
/// 冷轧排程服务 — 全量同步模式
/// </summary>
public class ColdRollSpecScheduleService : IColdRollSpecScheduleService
{
    private readonly AppDbContext _context;

    public ColdRollSpecScheduleService(AppDbContext context)
    {
        _context = context;
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

        await _context.SaveChangesAsync();
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
            CompletionType = entity.CompletionType,
            RollType = entity.RollType,
            MergeDisplay = entity.MergeDisplay,
            Remark = entity.Remark,
            UpdatedTime = entity.UpdatedTime.DateTime,
        };
    }
}
