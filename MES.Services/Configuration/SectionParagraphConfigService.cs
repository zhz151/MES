using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Configuration;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;

namespace MES.Services.Configuration;

/// <summary>
/// 段落日产配置服务 — 参数表 CRUD
/// </summary>
public class SectionParagraphConfigService : ISectionParagraphConfigService
{
    private readonly AppDbContext _context;

    public SectionParagraphConfigService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SectionParagraphConfigDto>> GetSettingsAsync()
    {
        var settings = await _context.SectionParagraphConfigs
            .AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .ToListAsync();

        return settings.Select(s => new SectionParagraphConfigDto
        {
            Id = s.Id,
            ParagraphName = s.ParagraphName,
            DisplayOrder = s.DisplayOrder,
            DailyFlowTarget = s.DailyFlowTarget,
            LowerLimitDays = s.LowerLimitDays,
            UpperLimitDays = s.UpperLimitDays,
            Remark = s.Remark,
        }).ToList();
    }

    public async Task<bool> CreateSettingAsync(SectionParagraphConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ParagraphName))
            throw new BusinessException("段落类别不能为空");
        if (await _context.SectionParagraphConfigs.AnyAsync(s => s.ParagraphName == dto.ParagraphName))
            throw new BusinessException($"段落类别 \"{dto.ParagraphName}\" 已存在");

        _context.SectionParagraphConfigs.Add(new SectionParagraphConfig
        {
            ParagraphName = dto.ParagraphName,
            DisplayOrder = dto.DisplayOrder,
            DailyFlowTarget = dto.DailyFlowTarget,
            LowerLimitDays = dto.LowerLimitDays,
            UpperLimitDays = dto.UpperLimitDays,
            Remark = dto.Remark,
        });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteSettingAsync(int id)
    {
        var entity = await _context.SectionParagraphConfigs
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) return false;

        // 组合归类表「归属段落」置空，避免孤儿段落名
        var combos = await _context.CombinationGroups
            .Where(c => c.ParagraphName == entity.ParagraphName)
            .ToListAsync();
        foreach (var c in combos)
            c.ParagraphName = null;

        _context.SectionParagraphConfigs.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SaveSettingAsync(SectionParagraphConfigDto dto)
    {
        var entity = await _context.SectionParagraphConfigs
            .FirstOrDefaultAsync(s => s.Id == dto.Id);
        if (entity == null) return false;

        entity.ParagraphName = dto.ParagraphName;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.DailyFlowTarget = dto.DailyFlowTarget;
        entity.LowerLimitDays = dto.LowerLimitDays;
        entity.UpperLimitDays = dto.UpperLimitDays;
        entity.Remark = dto.Remark;

        await _context.SaveChangesAsync();
        return true;
    }
}
