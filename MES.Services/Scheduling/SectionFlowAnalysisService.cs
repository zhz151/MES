using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities.Scheduling;

namespace MES.Services.Scheduling;

/// <summary>
/// 生产段流转量分析服务 — 按段落类别汇总生产工段待产量数据
/// </summary>
public class SectionFlowAnalysisService : ISectionFlowAnalysisService
{
    private readonly AppDbContext _context;
    private readonly ISectionProductionStatusService _statusService;

    public SectionFlowAnalysisService(AppDbContext context, ISectionProductionStatusService statusService)
    {
        _context = context;
        _statusService = statusService;
    }

    public async Task<List<SectionFlowAnalysisDto>> GetAnalysisAsync()
    {
        // 1. 获取生产工段待产量数据
        var statusData = await _statusService.GetStatusAsync();
        var statusLookup = new Dictionary<(string ProcessGroupName, string SectionName), SectionProductionStatusDto>();
        foreach (var item in statusData)
            statusLookup[(item.ProcessGroupName, item.SectionName)] = item;

        // 2. 加载分类设置 + 明细
        var settings = await _context.SectionFlowCategorySettings
            .AsNoTracking()
            .Include(s => s.Items.OrderBy(i => i.DisplayOrder))
            .OrderBy(s => s.CategoryCode)
            .ToListAsync();

        // 3. 逐类计算
        return settings.Select(setting =>
        {
            decimal pendingTotal = 0;
            decimal variationTotal = 0;

            foreach (var item in setting.Items)
            {
                if (!statusLookup.TryGetValue((item.ProcessGroupName, item.SectionName), out var match))
                    continue;

                var baseAmount = GetBaseAmount(setting.CategoryCode, match);

                pendingTotal += baseAmount;
                variationTotal += item.Coefficient * baseAmount;
            }

            var sustainableDays = setting.DailyProductionTarget.HasValue && setting.DailyProductionTarget.Value > 0
                ? Math.Round(variationTotal / setting.DailyProductionTarget.Value, 1)
                : (decimal?)null;

            string? status = null;
            if (sustainableDays.HasValue && setting.LowerLimitDays.HasValue && setting.UpperLimitDays.HasValue)
            {
                if (sustainableDays.Value < setting.LowerLimitDays.Value)
                    status = "偏少";
                else if (sustainableDays.Value > setting.UpperLimitDays.Value)
                    status = "过多";
                else
                    status = "正常";
            }

            return new SectionFlowAnalysisDto
            {
                Id = setting.Id,
                CategoryCode = setting.CategoryCode,
                CategoryName = setting.CategoryName,
                PendingTotal = pendingTotal > 0 ? pendingTotal : null,
                VariationTotal = variationTotal > 0 ? variationTotal : null,
                DailyProductionTarget = setting.DailyProductionTarget,
                SustainableDays = sustainableDays,
                LowerLimitDays = setting.LowerLimitDays,
                UpperLimitDays = setting.UpperLimitDays,
                StatusJudgment = status,
            };
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

    private static decimal GetBaseAmount(string categoryCode, SectionProductionStatusDto match)
    {
        return categoryCode switch
        {
            "K" => (match.Total ?? 0m) - (match.FinalProcessTotal ?? 0m),
            "L" => match.FinalProcessTotal ?? 0m,
            _ => match.Total ?? 0m
        };
    }

    // ========== 参数表管理 ==========

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
