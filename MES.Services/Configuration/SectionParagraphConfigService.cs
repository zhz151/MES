using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;

namespace MES.Services.Configuration;

/// <summary>
/// 段落日产配置服务 — 段落由 3 类配置自动生成（冷轧拔=机台组显示名 / 普通工段=StandardWorkDays / 检验=固定），
/// 仅参数可编辑。GetSettingsAsync 内部先同步期望段落集（缺失补齐、多余删除、显示名/顺序联动）再返回。
/// </summary>
public class SectionParagraphConfigService : ISectionParagraphConfigService
{
    private readonly AppDbContext _context;
    private readonly IStandardWorkDayService _standardWorkDayService;

    public SectionParagraphConfigService(AppDbContext context, IStandardWorkDayService standardWorkDayService)
    {
        _context = context;
        _standardWorkDayService = standardWorkDayService;
    }

    /// <summary>
    /// 构建期望段落集（3 类配置展开）：冷轧拔=机台组（DisplayOrder 排序）、普通工段=启用工段（扣冷轧拔/检验/入库）、
    /// 检验=固定「荒管检」「在制检」。返回按展示顺序排列的 (类型, 稳定Key, 显示名)。
    /// </summary>
    private async Task<List<(string CategoryType, string ParagraphKey, string DisplayName)>> BuildExpectedParagraphsAsync()
    {
        var expected = new List<(string, string, string)>();

        // 冷轧拔：机台组显示名
        var groups = await _context.ColdRollMachineGroupConfigs.AsNoTracking()
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.Id)
            .ToListAsync();
        foreach (var g in groups)
        {
            expected.Add((ParagraphCategoryTypes.Cold, g.GroupKey,
                string.IsNullOrWhiteSpace(g.DisplayName) ? g.GroupKey : g.DisplayName));
        }

        // 普通工段：启用工段，扣除冷轧拔/检验/入库
        var sections = await _standardWorkDayService.GetEnabledSectionsAsync();
        foreach (var s in sections)
        {
            if (s.SectionKey == SectionKeys.ColdRollDraw
                || s.SectionKey == SectionKeys.Inspection
                || s.SectionKey == SectionKeys.Warehouse)
                continue;
            expected.Add((ParagraphCategoryTypes.Section, s.SectionKey, s.SectionName));
        }

        // 固定检验
        expected.Add((ParagraphCategoryTypes.Fixed, BatchPlanSectionTabs.RoughTubeInspection, BatchPlanSectionTabs.RoughTubeInspection));
        expected.Add((ParagraphCategoryTypes.Fixed, BatchPlanSectionTabs.InProcessInspection, BatchPlanSectionTabs.InProcessInspection));

        return expected;
    }

    /// <summary>
    /// 同步段落集与期望集：缺失补齐（参数默认空）、多余删除（含 CategoryType=null 的存量旧段落）、
    /// 已存在则更新显示名与展示顺序（机台组/工段改名或调整顺序联动）。段落随配置增减。
    /// </summary>
    private async Task EnsureSyncedAsync()
    {
        var expected = await BuildExpectedParagraphsAsync();
        var existing = await _context.SectionParagraphConfigs.AsNoTracking().ToListAsync();
        var expectedKeys = new HashSet<(string? CategoryType, string? ParagraphKey)>(
            expected.Select(e => ((string?)e.CategoryType, (string?)e.ParagraphKey)));

        var changed = false;
        foreach (var e in existing)
        {
            if (!expectedKeys.Contains((e.CategoryType, e.ParagraphKey)))
            {
                var del = await _context.SectionParagraphConfigs.FirstOrDefaultAsync(x => x.Id == e.Id);
                if (del != null)
                {
                    _context.SectionParagraphConfigs.Remove(del);
                    changed = true;
                }
            }
        }

        var order = 1;
        foreach (var e in expected)
        {
            var found = existing.FirstOrDefault(x => x.CategoryType == e.CategoryType && x.ParagraphKey == e.ParagraphKey);
            if (found == null)
            {
                _context.SectionParagraphConfigs.Add(new SectionParagraphConfig
                {
                    ParagraphKey = e.ParagraphKey,
                    CategoryType = e.CategoryType,
                    ParagraphName = e.DisplayName,
                    DisplayOrder = order,
                });
                changed = true;
            }
            else if (found.ParagraphName != e.DisplayName || found.DisplayOrder != order)
            {
                var upd = await _context.SectionParagraphConfigs.FirstOrDefaultAsync(x => x.Id == found.Id);
                if (upd != null)
                {
                    upd.ParagraphName = e.DisplayName;
                    upd.DisplayOrder = order;
                    changed = true;
                }
            }
            order++;
        }

        if (changed)
            await _context.SaveChangesAsync();
    }

    public async Task<List<SectionParagraphConfigDto>> GetSettingsAsync()
    {
        await EnsureSyncedAsync();

        var settings = await _context.SectionParagraphConfigs
            .AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .ToListAsync();

        return settings.Select(s => new SectionParagraphConfigDto
        {
            Id = s.Id,
            ParagraphName = s.ParagraphName,
            ParagraphKey = s.ParagraphKey,
            CategoryType = s.CategoryType,
            DisplayOrder = s.DisplayOrder,
            DailyFlowTarget = s.DailyFlowTarget,
            LowerLimitDays = s.LowerLimitDays,
            UpperLimitDays = s.UpperLimitDays,
            Remark = s.Remark,
        }).ToList();
    }

    public async Task<bool> SaveSettingAsync(SectionParagraphConfigDto dto)
    {
        var entity = await _context.SectionParagraphConfigs
            .FirstOrDefaultAsync(s => s.Id == dto.Id);
        if (entity == null) return false;

        // 仅允许更新参数（段落名/顺序/类型由配置同步驱动，不允许手改）
        entity.DailyFlowTarget = dto.DailyFlowTarget;
        entity.LowerLimitDays = dto.LowerLimitDays;
        entity.UpperLimitDays = dto.UpperLimitDays;
        entity.Remark = dto.Remark;

        await _context.SaveChangesAsync();
        return true;
    }
}
