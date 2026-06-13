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

        // 按 ProcessGroupName 分组的便捷查询（用于"全部"通配）
        var groupedLookup = statusLookup
            .GroupBy(kv => kv.Key.ProcessGroupName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(kv => kv.Value).ToList(), StringComparer.OrdinalIgnoreCase);

        // 2. 加载分类设置 + 明细
        var settings = await _context.SectionFlowCategorySettings
            .AsNoTracking()
            .Include(s => s.Items.OrderBy(i => i.DisplayOrder))
            .OrderBy(s => s.CategoryCode)
            .ToListAsync();

        // 3. 逐类计算
        var results = settings.Select(setting =>
        {
            decimal pendingTotal = 0;
            decimal variationTotal = 0;

            foreach (var item in setting.Items)
            {
                List<SectionProductionStatusDto> matches;

                if (string.Equals(item.ProcessGroupName, "全部", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.SectionName, "全部", StringComparison.OrdinalIgnoreCase))
                {
                    // 双通配：匹配所有工序组的所有工段
                    matches = statusLookup.Values.ToList();
                }
                else if (string.Equals(item.ProcessGroupName, "全部", StringComparison.OrdinalIgnoreCase))
                {
                    // 工序组通配：匹配所有工序组中指定工段名
                    matches = statusLookup.Values
                        .Where(v => string.Equals(v.SectionName, item.SectionName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
                else if (string.Equals(item.SectionName, "全部", StringComparison.OrdinalIgnoreCase))
                {
                    // 工段通配：匹配该工序组下所有工段
                    matches = groupedLookup.GetValueOrDefault(item.ProcessGroupName, new List<SectionProductionStatusDto>());
                }
                else
                {
                    if (!statusLookup.TryGetValue((item.ProcessGroupName, item.SectionName), out var match))
                        continue;
                    matches = new List<SectionProductionStatusDto> { match };
                }

                foreach (var match in matches)
                {
                    var baseAmount = GetBaseAmount(setting.CategoryCode, match);
                    pendingTotal += baseAmount;
                    variationTotal += item.Coefficient * baseAmount;
                }
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

        // 4. 后处理：L(在制检) = Total(全部, 检验) - K.PendingTotal - M.PendingTotal
        var lResult = results.FirstOrDefault(r => r.CategoryCode == "L");
        var mResult = results.FirstOrDefault(r => r.CategoryCode == "M");
        var kResult = results.FirstOrDefault(r => r.CategoryCode == "K");
        if (lResult != null && mResult?.PendingTotal.HasValue == true && kResult?.PendingTotal.HasValue == true)
        {
            var rawL = lResult.PendingTotal ?? 0m;
            var subtract = kResult.PendingTotal.Value + mResult.PendingTotal.Value;
            lResult.PendingTotal = rawL > subtract ? rawL - subtract : null;
            lResult.VariationTotal = lResult.PendingTotal > 0 ? lResult.PendingTotal : null;
        }

        return results;
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
            "K" => match.Total ?? 0m,                              // 荒管检：汇总量
            "L" => match.Total ?? 0m,                              // 在制检：汇总量（后需整体减 M）
            "M" => match.FinalProcessTotal ?? 0m,                  // 成品待检：所有工序组中工段=检验的属成品工序量
            _ => match.Total ?? 0m                                 // A-J：汇总量
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
