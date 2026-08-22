using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
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
using MES.Core.Enums;
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
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Configuration;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Scheduling;

/// <summary>
/// 生产段流转量分析服务 — 按流转类别汇总生产工段待在产量数据。
/// 全部类别（含 D/E/N 检验类）统一由组合归类表 CombinationGroups 的(工序组,工段,产类)三维行驱动，
/// 不再有任何类别类型硬编码；检验三态通过 (全部, 检验, 产类) 通配行自然划分。
/// </summary>
public class SectionFlowAnalysisService : ISectionFlowAnalysisService
{
    private readonly AppDbContext _context;
    private readonly ISectionProductionStatusService _statusService;
    private readonly ISectionFlowCategoryService _categoryService;
    private readonly IProcessDefinitionService _processDefService;

    public SectionFlowAnalysisService(
        AppDbContext context,
        ISectionProductionStatusService statusService,
        ISectionFlowCategoryService categoryService,
        IProcessDefinitionService processDefService)
    {
        _context = context;
        _statusService = statusService;
        _categoryService = categoryService;
        _processDefService = processDefService;
    }

    public async Task<List<SectionFlowAnalysisDto>> GetAnalysisAsync()
    {
        var crKeys = await _processDefService.GetColdRollOrDrawKeysAsync();
        // 1. 获取生产工段待产量数据（(工序组,工段,产类)三维，含每维度 All 汇总行）
        var statusData = await _statusService.GetStatusAsync();

        // 2. 从 Configuration 服务加载分类设置 + 组合归类表
        var allSettings = await _categoryService.GetSettingsAsync();
        var settings = allSettings.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).ToList();

        // 组合归类表：(工序组,工段,产类)三维唯一归属映射，未归属行不参与聚合
        var combinationGroups = await _context.CombinationGroups.AsNoTracking().ToListAsync();
        var groupsBySetting = new Dictionary<int, List<CombinationGroup>>();
        foreach (var c in combinationGroups)
        {
            if (!c.FlowCategoryId.HasValue) continue;
            var id = c.FlowCategoryId.Value;
            if (!groupsBySetting.TryGetValue(id, out var groupList))
                groupsBySetting[id] = groupList = new List<CombinationGroup>();
            groupList.Add(c);
        }

        // 3. 逐类计算：所有类别统一从组合归类表聚合（含 D/E/N 检验类通配行）
        var results = settings.Select(setting =>
        {
            decimal pendingTotal = 0;
            decimal variationTotal = 0;
            decimal planFlowTotal = 0;
            decimal planKeyTotal = 0;

            if (groupsBySetting.TryGetValue(setting.Id, out var groupRows))
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

            var sustainableDays = setting.DailyProductionTarget.HasValue && setting.DailyProductionTarget.Value > 0
                ? Math.Round(variationTons / setting.DailyProductionTarget.Value, 1)
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

            // 计划流转判定：计划流转量 > 日产设定 → 加速，否则 -
            var planFlowJudgment = setting.DailyProductionTarget.HasValue && planFlowTons > setting.DailyProductionTarget.Value
                ? "加速"
                : "-";

            return new SectionFlowAnalysisDto
            {
                Id = setting.Id,
                CategoryName = setting.CategoryName,
                DisplayOrder = setting.DisplayOrder,
                PendingTotal = pendingTons > 0 ? pendingTonsExact : null,
                VariationTotal = variationTons > 0 ? variationTonsExact : null,
                DailyProductionTarget = setting.DailyProductionTarget,
                SustainableDays = sustainableDays,
                LowerLimitDays = setting.LowerLimitDays,
                UpperLimitDays = setting.UpperLimitDays,
                StatusJudgment = status,
                PlanFlowQuantity = planFlowTons > 0 ? planFlowTonsExact : null,
                PlanFlowJudgment = planFlowJudgment,
                PlanKeyWeight = planKeyTons > 0 ? planKeyTonsExact : null,
            };
        }).ToList();

        // 4. 重点批次统计（按类别汇总批次计划中的重点批次计数和重量，单位：吨）
        // 与待产量聚合同源：批次按(待产工序组,待产工段,批次产类)匹配组合归类表三维行
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

            // 每个重点批次在同一类别下只计一次（与旧实现一致）
            foreach (var setting in settings)
            {
                if (!groupsBySetting.TryGetValue(setting.Id, out var groupRows)) continue;
                var matched = false;
                foreach (var grp in groupRows)
                {
                    if (!Matches(grp, pendingProcessKey, pendingSectionKey, productStatus))
                        continue;
                    matched = true;
                    break;
                }
                if (!matched) continue;
                var stats = keyBatchStats[setting.Id];
                keyBatchStats[setting.Id] = (stats.count + 1, stats.weight + weightTons);
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

    public async Task<bool> UpdateSettingAsync(SectionFlowSettingUpdateDto dto)
    {
        return await _categoryService.UpdateSettingAsync(dto);
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
