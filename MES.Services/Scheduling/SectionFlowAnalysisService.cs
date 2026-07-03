using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
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

            // 转换为吨
            pendingTotal = Math.Round(pendingTotal / 1000m, 0);
            variationTotal = Math.Round(variationTotal / 1000m, 0);

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

        // 4. 后处理：E(在制检) = Total(全部, 检验) - D.PendingTotal - N.PendingTotal
        var eResult = results.FirstOrDefault(r => r.CategoryCode == "E");
        var nResult = results.FirstOrDefault(r => r.CategoryCode == "N");
        var dResult = results.FirstOrDefault(r => r.CategoryCode == "D");
        if (eResult != null && nResult?.PendingTotal.HasValue == true && dResult?.PendingTotal.HasValue == true)
        {
            var rawE = eResult.PendingTotal ?? 0m;
            var subtract = dResult.PendingTotal.Value + nResult.PendingTotal.Value;
            eResult.PendingTotal = rawE > subtract ? rawE - subtract : null;
            eResult.VariationTotal = eResult.PendingTotal > 0 ? eResult.PendingTotal : null;
        }

        // 5. 重点批次统计（按类别汇总批次计划中的重点批次计数和重量，单位：吨）
        var batchQuery = _context.ProductionBatches.AsNoTracking()
            .Where(b => b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress);
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();

        var batchJoin = from b in batchQuery
                        join s in summaryQuery on b.WorkOrderNo equals s.WorkOrderNo into sj
                        from s in sj.DefaultIfEmpty()
                        select new
                        {
                            b.Id,
                            b.CurrentValidWeight,
                            b.CurrentSectionCompleted,
                            b.CurrentGroupName,
                            b.CurrentSectionName,
                            b.NextProcess,
                            b.NextSectionName,
                            ScheduleStage = s != null ? (int?)s.ScheduleStage : null,
                            UrgencyLevel = s != null ? s.UrgencyLevel : null,
                            MainNoAttentionProcess = s != null ? s.MainNoAttentionProcess : null,
                            IsUrging = s != null && s.IsUrging,
                            IsBatchDelivery = s != null && s.IsBatchDelivery,
                        };

        var batches = await batchJoin.ToListAsync();

        // 加载工序组，推导每个批次的末道工序
        var batchIds = batches.Select(b => b.Id).Distinct().ToList();
        var lastProcessLookup = await _context.Set<ProcessGroup>()
            .AsNoTracking()
            .Where(pg => batchIds.Contains(pg.ProductionBatchId))
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionaryAsync(g => g.Key,
                g => g.OrderByDescending(pg => pg.SequenceNumber)
                    .Select(pg => pg.ProcessName)
                    .FirstOrDefault());

        var keyBatchStats = results.ToDictionary(r => r.Id, _ => (count: 0, weight: 0m));
        // 额外跟踪检验类统计用于 E/N 后处理
        var inspectionStats = (totalCount: 0, totalWeight: 0m, dCount: 0, dWeight: 0m, nCount: 0, nWeight: 0m);
        var dSettingId = settings.FirstOrDefault(s => s.CategoryCode == "D")?.Id;
        var eSettingId = settings.FirstOrDefault(s => s.CategoryCode == "E")?.Id;
        var nSettingId = settings.FirstOrDefault(s => s.CategoryCode == "N")?.Id;

        foreach (var b in batches)
        {
            if (b.ScheduleStage == null) continue;

            var pendingProcess = b.CurrentSectionCompleted == false ? b.CurrentGroupName : b.NextProcess;
            var pendingSection = b.CurrentSectionCompleted == false ? b.CurrentSectionName : b.NextSectionName;
            if (string.IsNullOrEmpty(pendingProcess) || string.IsNullOrEmpty(pendingSection))
                continue;

            var uLevel = b.UrgencyLevel ?? "";
            var isKeyBatch =
                (b.ScheduleStage == 2 &&
                 (uLevel == "A+急" || uLevel == "A急") &&
                 (pendingProcess == "荒管处理" ||
                  (b.MainNoAttentionProcess != null && pendingProcess == b.MainNoAttentionProcess
                      && (!ProcessNames.IsColdRollOrDraw(pendingProcess) || pendingSection == SectionDefs.ColdRollDraw)) ||
                  pendingProcess == "收尾-成检"))
                ||
                (b.ScheduleStage == 1 &&
                 (b.IsUrging || b.IsBatchDelivery) &&
                 (uLevel == "A+急" || uLevel == "A急") &&
                 (pendingProcess == "荒管处理" ||
                  (b.MainNoAttentionProcess != null && pendingProcess == b.MainNoAttentionProcess
                      && (!ProcessNames.IsColdRollOrDraw(pendingProcess) || pendingSection == SectionDefs.ColdRollDraw)) ||
                  pendingProcess == "收尾-成检"));

            if (!isKeyBatch) continue;

            var isInspection = string.Equals(pendingSection, "检验", StringComparison.OrdinalIgnoreCase);
            var isFinalProcess = lastProcessLookup.TryGetValue(b.Id, out var lastProc)
                && string.Equals(pendingProcess, lastProc, StringComparison.OrdinalIgnoreCase);
            var weightTons = (b.CurrentValidWeight ?? 0m) / 1000m;

            // 对于 D（荒管检）：直接匹配固定维度
            if (dSettingId.HasValue
                && string.Equals(pendingProcess, "荒管处理", StringComparison.OrdinalIgnoreCase)
                && isInspection)
            {
                var stats = keyBatchStats[dSettingId.Value];
                keyBatchStats[dSettingId.Value] = (stats.count + 1, stats.weight + weightTons);
            }

            // 对于 N（成品待检）：仅末道工序的检验批次
            if (nSettingId.HasValue && isInspection && isFinalProcess)
            {
                var stats = keyBatchStats[nSettingId.Value];
                keyBatchStats[nSettingId.Value] = (stats.count + 1, stats.weight + weightTons);
            }

            // 通用匹配（A-C, F-M）
            foreach (var setting in settings)
            {
                if (setting.CategoryCode is "D" or "E" or "N") continue; // 已单独处理

                foreach (var item in setting.Items)
                {
                    bool match;
                    if (string.Equals(item.ProcessGroupName, "全部", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(item.SectionName, "全部", StringComparison.OrdinalIgnoreCase))
                        match = true;
                    else if (string.Equals(item.ProcessGroupName, "全部", StringComparison.OrdinalIgnoreCase))
                        match = string.Equals(pendingSection, item.SectionName, StringComparison.OrdinalIgnoreCase);
                    else if (string.Equals(item.SectionName, "全部", StringComparison.OrdinalIgnoreCase))
                        match = string.Equals(pendingProcess, item.ProcessGroupName, StringComparison.OrdinalIgnoreCase);
                    else
                        match = string.Equals(pendingProcess, item.ProcessGroupName, StringComparison.OrdinalIgnoreCase)
                             && string.Equals(pendingSection, item.SectionName, StringComparison.OrdinalIgnoreCase);

                    if (match)
                    {
                        var stats = keyBatchStats[setting.Id];
                        keyBatchStats[setting.Id] = (stats.count + 1, stats.weight + weightTons);
                        break;
                    }
                }
            }

            // 收集检验类统计（用于 E = 全部检验 - D - N）
            if (isInspection)
            {
                inspectionStats.totalCount++;
                inspectionStats.totalWeight += weightTons;
                if (string.Equals(pendingProcess, "荒管处理", StringComparison.OrdinalIgnoreCase))
                {
                    inspectionStats.dCount++;
                    inspectionStats.dWeight += weightTons;
                }
                if (isFinalProcess)
                {
                    inspectionStats.nCount++;
                    inspectionStats.nWeight += weightTons;
                }
            }
        }

        // 计算 E（在制检）= 全部检验 - D - N
        if (eSettingId.HasValue)
        {
            var eCount = inspectionStats.totalCount - inspectionStats.dCount - inspectionStats.nCount;
            var eWeight = inspectionStats.totalWeight - inspectionStats.dWeight - inspectionStats.nWeight;
            keyBatchStats[eSettingId.Value] = (eCount > 0 ? eCount : 0, eWeight > 0 ? eWeight : 0m);
        }

        foreach (var r in results)
        {
            if (keyBatchStats.TryGetValue(r.Id, out var stats) && stats.count > 0)
            {
                r.KeyBatchCount = stats.count;
                r.KeyBatchWeight = stats.weight > 0 ? stats.weight : null;
            }
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
            "D" => match.Total ?? 0m,                              // 荒管检：汇总量
            "E" => match.Total ?? 0m,                              // 在制检：汇总量（后需整体减 N）
            "N" => match.FinalProcessTotal ?? 0m,                  // 成品待检：所有工序组中工段=检验的属成品工序量
            _ => match.Total ?? 0m                                 // A-C, F-M：汇总量
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
