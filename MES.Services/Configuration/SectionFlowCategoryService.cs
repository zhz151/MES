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
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities.Configuration;

namespace MES.Services.Configuration;

/// <summary>
/// 工段流转分类设置服务 — 参数表 CRUD
/// </summary>
public class SectionFlowCategoryService : ISectionFlowCategoryService
{
    private readonly AppDbContext _context;

    public SectionFlowCategoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SectionFlowCategorySettingDto>> GetSettingsAsync()
    {
        var settings = await _context.SectionFlowCategorySettings
            .AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .ToListAsync();

        return settings.Select(s => new SectionFlowCategorySettingDto
        {
            Id = s.Id,
            CategoryName = s.CategoryName,
            DisplayOrder = s.DisplayOrder,
            DailyProductionTarget = s.DailyProductionTarget,
            LowerLimitDays = s.LowerLimitDays,
            UpperLimitDays = s.UpperLimitDays,
            Remark = s.Remark,
        }).ToList();
    }

    public async Task<bool> UpdateSettingAsync(SectionFlowSettingUpdateDto dto)
    {
        var entity = await _context.SectionFlowCategorySettings
            .FirstOrDefaultAsync(s => s.Id == dto.Id);
        if (entity == null)
            return false;

        entity.DailyProductionTarget = dto.DailyProductionTarget;
        entity.LowerLimitDays = dto.LowerLimitDays;
        entity.UpperLimitDays = dto.UpperLimitDays;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CreateSettingAsync(SectionFlowCategorySettingDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CategoryName))
            throw new BusinessException("流转类别不能为空");
        if (await _context.SectionFlowCategorySettings.AnyAsync(s => s.CategoryName == dto.CategoryName))
            throw new BusinessException($"流转类别 \"{dto.CategoryName}\" 已存在");

        _context.SectionFlowCategorySettings.Add(new SectionFlowCategorySetting
        {
            CategoryName = dto.CategoryName,
            DisplayOrder = dto.DisplayOrder,
            DailyProductionTarget = dto.DailyProductionTarget,
            LowerLimitDays = dto.LowerLimitDays,
            UpperLimitDays = dto.UpperLimitDays,
            Remark = dto.Remark,
        });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteSettingAsync(int id)
    {
        var entity = await _context.SectionFlowCategorySettings
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) return false;

        _context.SectionFlowCategorySettings.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SaveSettingAsync(SectionFlowCategorySettingDto dto)
    {
        var entity = await _context.SectionFlowCategorySettings
            .FirstOrDefaultAsync(s => s.Id == dto.Id);
        if (entity == null) return false;

        entity.CategoryName = dto.CategoryName;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.DailyProductionTarget = dto.DailyProductionTarget;
        entity.LowerLimitDays = dto.LowerLimitDays;
        entity.UpperLimitDays = dto.UpperLimitDays;
        entity.Remark = dto.Remark;

        await _context.SaveChangesAsync();
        return true;
    }
}
