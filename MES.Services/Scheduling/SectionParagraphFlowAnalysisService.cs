using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Batch;
using MES.Services.Helpers;
using MES.Services.Printing;
using MES.Core.Enums;

namespace MES.Services.Scheduling;

/// <summary>
/// 生产段落流转量分析服务 — 按生产段落汇总待在产量数据。
/// 段落包含的(工序组,工段,产类)组合由组合归类表 CombinationGroups 的「归属段落」承载：
/// 同一段落下可含多个归属流转类别（如「切割」含 荒管平头/油断5060/断切5060 等），段落维度 = 组合行按归属段落上卷聚合。
/// </summary>
public class SectionParagraphFlowAnalysisService : ISectionParagraphFlowAnalysisService
{
    private readonly AppDbContext _context;
    private readonly ISectionProductionStatusService _statusService;
    private readonly ISectionParagraphConfigService _paragraphService;
    private readonly IProcessDefinitionService _processDefService;

    public SectionParagraphFlowAnalysisService(
        AppDbContext context,
        ISectionProductionStatusService statusService,
        ISectionParagraphConfigService paragraphService,
        IProcessDefinitionService processDefService)
    {
        _context = context;
        _statusService = statusService;
        _paragraphService = paragraphService;
        _processDefService = processDefService;
    }

    public async Task<List<SectionParagraphFlowAnalysisDto>> GetAnalysisAsync()
    {
        var crKeys = await _processDefService.GetColdRollOrDrawKeysAsync();
        // 1. 获取生产工段待在产量数据（(工序组,工段,产类)三维，含每维度 All 汇总行）
        var statusData = await _statusService.GetStatusAsync();

        // 2. 从段落配置服务加载段落 + 组合归类表（按「归属段落」分组）
        var paragraphs = (await _paragraphService.GetSettingsAsync())
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Id)
            .ToList();

        var combinationGroups = await _context.CombinationGroups.AsNoTracking().ToListAsync();
        var groupsByParagraph = new Dictionary<string, List<CombinationGroup>>();
        foreach (var c in combinationGroups)
        {
            if (string.IsNullOrEmpty(c.ParagraphName)) continue;
            if (!groupsByParagraph.TryGetValue(c.ParagraphName, out var groupList))
                groupsByParagraph[c.ParagraphName] = groupList = new List<CombinationGroup>();
            groupList.Add(c);
        }

        // 3. 逐段落计算：所有段落统一从组合归类表按归属段落聚合
        var results = paragraphs.Select(paragraph =>
        {
            decimal pendingTotal = 0;
            decimal variationTotal = 0;
            decimal planFlowTotal = 0;
            decimal planKeyTotal = 0;

            if (groupsByParagraph.TryGetValue(paragraph.ParagraphName, out var groupRows))
            {
                foreach (var grp in groupRows)
                {
                    decimal baseAmount = 0;
                    decimal planFlowAmount = 0;
                    decimal planKeyAmount = 0;
                    foreach (var match in statusData)
                    {
                        if (!Matches(grp, match.ProcessGroupName, match.SectionName, match.ProductStatus))
                            continue;
                        baseAmount += match.Total ?? 0m;
                        planFlowAmount += match.PlanFlowQuantity ?? 0m;
                        planKeyAmount += match.PlanKeyWeight ?? 0m;
                    }
                    if (baseAmount == 0) continue;
                    pendingTotal += baseAmount;
                    variationTotal += baseAmount;
                    planFlowTotal += planFlowAmount;
                    planKeyTotal += planKeyAmount;
                }
            }

            // 精确吨值（DTO 存精确值：前端单行显示时取整、页脚汇总先精确求和再一次取整，消除逐行取整放大）
            var pendingTonsExact = pendingTotal / 1000m;
            var variationTonsExact = variationTotal / 1000m;
            var planFlowTonsExact = planFlowTotal / 1000m;
            var planKeyTonsExact = planKeyTotal / 1000m;

            // 取整吨（仅用于非零门控/可持续天数/流转判定，保持既有判定口径；存储仍用精确值）
            var pendingTons = Math.Round(pendingTonsExact, 0);
            var variationTons = Math.Round(variationTonsExact, 0);
            var planFlowTons = Math.Round(planFlowTonsExact, 0);
            var planKeyTons = Math.Round(planKeyTonsExact, 0);

            var sustainableDays = paragraph.DailyFlowTarget.HasValue && paragraph.DailyFlowTarget.Value > 0
                ? Math.Round(variationTons / paragraph.DailyFlowTarget.Value, 1)
                : (decimal?)null;

            string? status = null;
            if (sustainableDays.HasValue && paragraph.LowerLimitDays.HasValue && paragraph.UpperLimitDays.HasValue)
            {
                if (sustainableDays.Value < paragraph.LowerLimitDays.Value)
                    status = "偏少";
                else if (sustainableDays.Value > paragraph.UpperLimitDays.Value)
                    status = "过多";
                else
                    status = "正常";
            }

            // 计划流转判定：计划流转量 > 日流转设定 → 加速，否则 -
            var planFlowJudgment = paragraph.DailyFlowTarget.HasValue && planFlowTons > paragraph.DailyFlowTarget.Value
                ? "加速"
                : "-";

            return new SectionParagraphFlowAnalysisDto
            {
                Id = paragraph.Id,
                ParagraphName = paragraph.ParagraphName,
                DisplayOrder = paragraph.DisplayOrder,
                PendingTotal = pendingTons > 0 ? pendingTonsExact : null,
                VariationTotal = variationTons > 0 ? variationTonsExact : null,
                DailyFlowTarget = paragraph.DailyFlowTarget,
                SustainableDays = sustainableDays,
                LowerLimitDays = paragraph.LowerLimitDays,
                UpperLimitDays = paragraph.UpperLimitDays,
                StatusJudgment = status,
                PlanFlowQuantity = planFlowTons > 0 ? planFlowTonsExact : null,
                PlanFlowJudgment = planFlowJudgment,
                PlanKeyWeight = planKeyTons > 0 ? planKeyTonsExact : null,
            };
        }).ToList();

        // 4. 重点批次统计（按段落汇总批次计划中的重点批次计数和重量，单位：吨）
        // 与待在产量聚合同源：批次按(待产工序组,待产工段,批次产类)匹配组合归类表三维行 → 上卷到归属段落
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
                            b.ManufacturingItem,
                            b.Specification,
                            ScheduleStage = s != null ? (int?)s.ScheduleStage : null,
                            UrgencyLevel = s != null ? s.UrgencyLevel : null,
                            MainNoAttentionProcess = s != null ? s.MainNoAttentionProcess : null,
                            IsUrging = s != null && s.IsUrging,
                            IsBatchDelivery = s != null && s.IsBatchDelivery,
                        };

        var batches = await batchJoin.ToListAsync();

        // 加载批次全部工序组（供 ProductStatusHelper 按批次粒度算产类）
        var batchIds = batches.Select(b => b.Id).Distinct().ToList();
        var processGroups = await _context.Set<ProcessGroup>()
            .AsNoTracking()
            .Where(pg => batchIds.Contains(pg.ProductionBatchId))
            .ToListAsync();
        var pgByBatch = processGroups
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var keyBatchStats = results.ToDictionary(r => r.Id, _ => (count: 0, weight: 0m));

        foreach (var b in batches)
        {
            if (b.ScheduleStage == null) continue;

            var pendingProcess = b.CurrentSectionCompleted == false ? b.CurrentGroupName : b.NextProcess;
            var pendingSection = b.CurrentSectionCompleted == false ? b.CurrentSectionName : b.NextSectionName;
            if (string.IsNullOrEmpty(pendingProcess) || string.IsNullOrEmpty(pendingSection))
                continue;

            var pendingProcessKey = ProcessKeys.ToKey(pendingProcess) ?? pendingProcess;
            var pendingSectionKey = SectionKeys.ToKey(pendingSection) ?? pendingSection;
            var attentionProcessKey = ProcessKeys.ToKey(b.MainNoAttentionProcess) ?? b.MainNoAttentionProcess;

            var uLevel = b.UrgencyLevel ?? "";
            var isKeyBatch =
                (b.ScheduleStage == 3 &&
                 UrgencyLevelKeys.IsUrgent(uLevel) &&
                 (pendingProcessKey == ProcessKeys.RoughTubeProcessing ||
                  (attentionProcessKey != null && pendingProcessKey == attentionProcessKey
                      && (!crKeys.Contains(pendingProcessKey) || pendingSectionKey == SectionKeys.ColdRollDraw))))
                ||
                (b.ScheduleStage == 2 &&
                 (b.IsUrging || b.IsBatchDelivery) &&
                 UrgencyLevelKeys.IsUrgent(uLevel) &&
                 (pendingProcessKey == ProcessKeys.RoughTubeProcessing ||
                  (attentionProcessKey != null && pendingProcessKey == attentionProcessKey
                      && (!crKeys.Contains(pendingProcessKey) || pendingSectionKey == SectionKeys.ColdRollDraw))));

            if (!isKeyBatch) continue;

            // 批次产类：复用 ProductStatusHelper（制造规格=待产工序组的制造规格，成品规格=批次 Specification）
            var batchPgList = pgByBatch.TryGetValue(b.Id, out var pgList) ? pgList : new List<ProcessGroup>();
            var pendingPg = batchPgList
                .FirstOrDefault(pg => string.Equals(ProcessKeys.ToKey(pg.ProcessName) ?? pg.ProcessName, pendingProcessKey, StringComparison.OrdinalIgnoreCase));
            var productStatus = ProductStatusHelper.Calculate(
                pendingProcessKey, pendingPg?.ManufacturingSpec, b.ManufacturingItem, batchPgList, b.Specification);

            var weightTons = (b.CurrentValidWeight ?? 0m) / 1000m;

            // 每个重点批次在同一段落维度下只计一次
            foreach (var paragraph in paragraphs)
            {
                if (!groupsByParagraph.TryGetValue(paragraph.ParagraphName, out var groupRows)) continue;
                var matched = false;
                foreach (var grp in groupRows)
                {
                    if (!Matches(grp, pendingProcessKey, pendingSectionKey, productStatus))
                        continue;
                    matched = true;
                    break;
                }
                if (!matched) continue;
                var stats = keyBatchStats[paragraph.Id];
                keyBatchStats[paragraph.Id] = (stats.count + 1, stats.weight + weightTons);
            }
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

    /// <summary>
    /// 组合归类行三维匹配：工序组/工段支持"全部"通配；产类 AllStatus=不限定，否则精确匹配。
    /// </summary>
    private static bool Matches(CombinationGroup grp, string processKey, string sectionKey, string productStatus)
    {
        if (!string.Equals(grp.ProcessGroupName, CombinationWildcards.All, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(grp.ProcessGroupName, processKey, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(grp.SectionName, CombinationWildcards.All, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(grp.SectionName, sectionKey, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(grp.ProductStatus, ProductStatuses.AllStatus, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(grp.ProductStatus, productStatus, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var pdfBytes = TablePrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }
}
