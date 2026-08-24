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
using MES.Core.Constants;
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
using MES.Services.Extensions;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Scheduling;

/// <summary>
/// 生产工段待产量现况服务 — 按(工序组, 工段, 产类)三维汇总批次现有效原料重量。
/// 维度由配置表驱动：启用工序组（工序组定义 ProcessDefinitions）× 启用工段（工段工量天数 StandardWorkDays，排除"入库"）全笛卡尔 × 产类三态；
/// 每个(工序组,工段)输出产类三态行（RoughTube/InProgress/Finished，与组合归类表前 3 字段口径一致）。
/// 产类按批次粒度复用 <see cref="ProductStatusHelper.Calculate"/> 判定（各批次 Specification 不同，产类不可降级到维度级硬编码）。
/// </summary>
public class SectionProductionStatusService : ISectionProductionStatusService
{
    private readonly AppDbContext _context;
    private readonly IProcessDefinitionService _processDefService;
    private readonly IStandardWorkDayService _standardWorkDayService;
    private readonly IBatchPlanService _batchPlanService;

    public SectionProductionStatusService(
        AppDbContext context,
        IProcessDefinitionService processDefService,
        IStandardWorkDayService standardWorkDayService,
        IBatchPlanService batchPlanService)
    {
        _context = context;
        _processDefService = processDefService;
        _standardWorkDayService = standardWorkDayService;
        _batchPlanService = batchPlanService;
    }

    public async Task<List<SectionProductionStatusDto>> GetStatusAsync()
    {
        // 1. 加载生产中的批次（含工序组），用于聚合计算
        // 仅统计生产：排除"完成"（Completed）与"成检"（InFinalInspection）——成检属质量检验阶段，不属生产
        var allBatches = await _context.Set<ProductionBatch>()
            .AsNoTracking()
            .Include(b => b.ProcessGroups)
            .Where(b => b.Status != BatchStatus.Completed
                     && b.Status != BatchStatus.InFinalInspection)
            .ToListAsync();

        // 1.5 批次计划流转/重点档位（口径=批次计划 GetAllAsync(null)：Status in None/InProgress + 排除暂停）
        // 计划流转量=流转=是的现有效原料重量；重点批重量=流转=是 且 等级=急+（ScheduleTier==1）的重量
        var planFlowByBatch = new Dictionary<int, (bool IsFlow, bool IsKeyPlus)>();
        var planItems = await _batchPlanService.GetAllAsync(null);
        foreach (var pi in planItems)
        {
            planFlowByBatch[pi.BatchId] = (pi.IsFlow, pi.IsFlow && pi.ScheduleTier == 1);
        }

        // 2. 配置表驱动维度：启用工序组 × 启用工段（全笛卡尔，排除"入库"工段）
        // 维度键用英文稳定 Key（ProcessKey/SectionKey），与批次派生字段（CurrentGroupName/NextProcess/CurrentSectionName/NextSectionName 迁移后存 Key）一致匹配；
        // 前端 ProcessDisplayHelper/SectionDisplayHelper 幂等（Key→中文/中文原样），显示不受影响
        var processes = await _processDefService.GetEnabledProcessesAsync();
        var sections = (await _standardWorkDayService.GetEnabledSectionsAsync())
            .Where(s => !string.Equals(s.SectionKey, SectionKeys.Warehouse, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // GetEnabledProcessesAsync / GetEnabledSectionsAsync 均已按 DisplayOrder 升序，逐层嵌套即工序序×工段序
        var dimensions = new List<(string ProcessGroupName, string SectionName)>(processes.Count * sections.Count);
        foreach (var p in processes)
        {
            foreach (var s in sections)
            {
                dimensions.Add((p.ProcessKey, s.SectionKey));
            }
        }

        if (dimensions.Count == 0)
            return new List<SectionProductionStatusDto>();

        // 3. 按批次粒度算产类并预聚合（O(1) 查找替代 O(dimensions × batches) 嵌套扫描）
        // 生产中：(当前工序组, 当前工段, 产类) → 重量合计；待产量：(下一工序, 下一工段, 产类) → 重量合计
        // 产类输入：当前工序组/下一工序的工序组 ManufacturingSpec 为制造规格，批次 Specification 为成品规格
        // 待产判定：CurrentSectionCompleted==false 归生产中；true 或 null 归待产量——null 的未产批次（无当前工序组，
        // CurrentGroupName 为空）仍持有 NextProcess/NextSectionName 下一步待产动作，须计入待产维度，避免汇总低估未产重量。
        var inProdLookup = new Dictionary<(string Group, string Section, string Status), decimal>();
        var pendingLookup = new Dictionary<(string Group, string Section, string Status), decimal>();
        var planFlowInLookup = new Dictionary<(string Group, string Section, string Status), decimal>();
        var planKeyInLookup = new Dictionary<(string Group, string Section, string Status), decimal>();
        var planFlowPendingLookup = new Dictionary<(string Group, string Section, string Status), decimal>();
        var planKeyPendingLookup = new Dictionary<(string Group, string Section, string Status), decimal>();

        foreach (var b in allBatches)
        {
            var weight = b.CurrentValidWeight ?? 0m;
            if (weight == 0m)
                continue;

            // 计划流转/重点批（口径=批次计划 GetAllAsync(null)：仅批次计划范围内的批次计入）
            var hasFlow = planFlowByBatch.TryGetValue(b.Id, out var flowInfo) && flowInfo.IsFlow;
            var planFlowWeight = hasFlow ? weight : 0m;
            var planKeyWeight = hasFlow && flowInfo.IsKeyPlus ? weight : 0m;

            // 批次内工序组按归一 Key 索引（取制造规格、供 ProductStatusHelper 判定）
            var pgByProcessKey = b.ProcessGroups
                .GroupBy(pg => ProcessKeys.ToKey(pg.ProcessName) ?? pg.ProcessName)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // 生产中
            if (b.CurrentSectionCompleted == false
                && !string.IsNullOrEmpty(b.CurrentGroupName)
                && !string.IsNullOrEmpty(b.CurrentSectionName))
            {
                var groupKey = ProcessKeys.ToKey(b.CurrentGroupName) ?? b.CurrentGroupName;
                var sectionKey = SectionKeys.ToKey(b.CurrentSectionName) ?? b.CurrentSectionName;
                var spec = pgByProcessKey.TryGetValue(groupKey, out var pg) ? pg.ManufacturingSpec : null;
                var status = ProductStatusHelper.Calculate(groupKey, spec, b.ManufacturingItem, b.ProcessGroups, b.Specification);
                var key = (Group: groupKey, Section: sectionKey, Status: status);
                inProdLookup[key] = inProdLookup.GetValueOrDefault(key) + weight;
                if (planFlowWeight > 0)
                {
                    planFlowInLookup[key] = planFlowInLookup.GetValueOrDefault(key) + planFlowWeight;
                    if (planKeyWeight > 0)
                        planKeyInLookup[key] = planKeyInLookup.GetValueOrDefault(key) + planKeyWeight;
                }
            }

            // 待产量（CurrentSectionCompleted != false：true=当前工段已完工；null=未产批次无当前工序组，二者均按下一工序/下一工段归待产维度）
            if (b.CurrentSectionCompleted != false
                && !string.IsNullOrEmpty(b.NextProcess)
                && !string.IsNullOrEmpty(b.NextSectionName))
            {
                var groupKey = ProcessKeys.ToKey(b.NextProcess) ?? b.NextProcess;
                var sectionKey = SectionKeys.ToKey(b.NextSectionName) ?? b.NextSectionName;
                var spec = pgByProcessKey.TryGetValue(groupKey, out var pg) ? pg.ManufacturingSpec : null;
                var status = ProductStatusHelper.Calculate(groupKey, spec, b.ManufacturingItem, b.ProcessGroups, b.Specification);
                var key = (Group: groupKey, Section: sectionKey, Status: status);
                pendingLookup[key] = pendingLookup.GetValueOrDefault(key) + weight;
                if (planFlowWeight > 0)
                {
                    planFlowPendingLookup[key] = planFlowPendingLookup.GetValueOrDefault(key) + planFlowWeight;
                    if (planKeyWeight > 0)
                        planKeyPendingLookup[key] = planKeyPendingLookup.GetValueOrDefault(key) + planKeyWeight;
                }
            }
        }

        // 4. 按维度填充：每个(工序组,工段)输出产类三态行（荒管/在制/成品）
        var result = new List<SectionProductionStatusDto>(dimensions.Count * 3);
        foreach (var (processGroupName, sectionName) in dimensions)
        {
            var roughIn = inProdLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.RoughTube));
            var inProgIn = inProdLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.InProgress));
            var finIn = inProdLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.Finished));
            var roughP = pendingLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.RoughTube));
            var inProgP = pendingLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.InProgress));
            var finP = pendingLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.Finished));

            var roughFlow = planFlowInLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.RoughTube)) +
                            planFlowPendingLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.RoughTube));
            var inProgFlow = planFlowInLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.InProgress)) +
                             planFlowPendingLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.InProgress));
            var finFlow = planFlowInLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.Finished)) +
                          planFlowPendingLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.Finished));

            var roughKey = planKeyInLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.RoughTube)) +
                           planKeyPendingLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.RoughTube));
            var inProgKey = planKeyInLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.InProgress)) +
                            planKeyPendingLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.InProgress));
            var finKey = planKeyInLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.Finished)) +
                         planKeyPendingLookup.GetValueOrDefault((processGroupName, sectionName, ProductStatuses.Finished));

            result.Add(MakeRow(processGroupName, sectionName, ProductStatuses.RoughTube, roughIn, roughP, roughFlow, roughKey));
            result.Add(MakeRow(processGroupName, sectionName, ProductStatuses.InProgress, inProgIn, inProgP, inProgFlow, inProgKey));
            result.Add(MakeRow(processGroupName, sectionName, ProductStatuses.Finished, finIn, finP, finFlow, finKey));
        }

        return result;
    }

    private static SectionProductionStatusDto MakeRow(string processGroupName, string sectionName, string productStatus, decimal inProduction, decimal pendingProduction, decimal planFlow, decimal planKey)
    {
        var total = inProduction + pendingProduction;
        return new SectionProductionStatusDto
        {
            ProcessGroupName = processGroupName,
            SectionName = sectionName,
            ProductStatus = productStatus,
            InProduction = inProduction > 0 ? inProduction : null,
            PendingProduction = pendingProduction > 0 ? pendingProduction : null,
            Total = total > 0 ? total : null,
            PlanFlowQuantity = planFlow > 0 ? planFlow : null,
            PlanKeyWeight = planKey > 0 ? planKey : null,
        };
    }

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var pdfBytes = TablePrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }
}
