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
            .Include(s => s.Items.OrderBy(i => i.DisplayOrder))
            .OrderBy(s => s.CategoryCode)
            .ToListAsync();

        return settings.Select(s => new SectionFlowCategorySettingDto
        {
            Id = s.Id,
            CategoryCode = s.CategoryCode,
            CategoryName = s.CategoryName,
            DailyProductionTarget = s.DailyProductionTarget,
            LowerLimitDays = s.LowerLimitDays,
            UpperLimitDays = s.UpperLimitDays,
            Remark = s.Remark,
            Items = s.Items.Select(i => new SectionFlowCategoryItemDto
            {
                Id = i.Id,
                SettingId = i.SettingId,
                ProcessGroupName = i.ProcessGroupName,
                SectionName = i.SectionName,
                Coefficient = i.Coefficient,
                DisplayOrder = i.DisplayOrder,
            }).ToList(),
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

    public async Task<bool> SaveSettingAsync(SectionFlowCategorySettingDto dto)
    {
        var entity = await _context.SectionFlowCategorySettings
            .FirstOrDefaultAsync(s => s.Id == dto.Id);
        if (entity == null) return false;

        entity.CategoryCode = dto.CategoryCode;
        entity.CategoryName = dto.CategoryName;
        entity.DailyProductionTarget = dto.DailyProductionTarget;
        entity.LowerLimitDays = dto.LowerLimitDays;
        entity.UpperLimitDays = dto.UpperLimitDays;
        entity.Remark = dto.Remark;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SaveItemAsync(int itemId, SectionFlowCategoryItemDto dto)
    {
        var entity = await _context.SectionFlowCategoryItems
            .FirstOrDefaultAsync(i => i.Id == itemId);
        if (entity == null) return false;

        entity.Coefficient = dto.Coefficient;
        entity.DisplayOrder = dto.DisplayOrder;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteItemAsync(int itemId)
    {
        var entity = await _context.SectionFlowCategoryItems
            .FirstOrDefaultAsync(i => i.Id == itemId);
        if (entity == null) return false;

        _context.SectionFlowCategoryItems.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CreateItemAsync(int settingId, SectionFlowCategoryItemDto dto)
    {
        var setting = await _context.SectionFlowCategorySettings
            .AnyAsync(s => s.Id == settingId);
        if (!setting) return false;

        var entity = new SectionFlowCategoryItem
        {
            SettingId = settingId,
            ProcessGroupName = dto.ProcessGroupName,
            SectionName = dto.SectionName,
            Coefficient = dto.Coefficient,
            DisplayOrder = dto.DisplayOrder,
        };

        _context.SectionFlowCategoryItems.Add(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}
