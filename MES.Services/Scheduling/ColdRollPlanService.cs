using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Scheduling;
using MES.Services.Extensions;

namespace MES.Services.Scheduling;

/// <summary>
/// 冷轧计划看板服务 — 按规格维度聚合生产批次的时间桶重量分布
/// </summary>
public class ColdRollPlanService : IColdRollPlanService
{
    private readonly AppDbContext _context;
    private readonly IProcessDefinitionService _processDefService;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// 排机估算缓存键。排程保存/产能档案保存/机台数配置保存·删除时主动失效（四处失效点）；
    /// 批次/生产数据无统一失效入口，采用短 TTL（60 秒）保证新鲜度，兼顾重复展开时的加载性能。
    /// </summary>
    public const string MachineEstimateCacheKey = "ColdRollPlanService:MachineEstimate";

    /// <summary>
    /// 排程建议缓存键。排程保存/产能档案保存/机台数配置保存·删除时主动失效（四处失效点）。
    /// </summary>
    public const string ScheduleSuggestionCacheKey = "ColdRollPlanService:ScheduleSuggestion";

    /// <summary>
    /// 机台组配置缓存键（引擎归组运行时从配置表 ColdRollMachineGroupConfigs 加载）。
    /// 机台组配置保存/删除时主动失效（ColdRollMachineGroupConfigService），短 TTL 60 秒双保险。
    /// </summary>
    public const string MachineGroupCacheKey = "ColdRollPlanService:MachineGroups";

    public ColdRollPlanService(AppDbContext context, IProcessDefinitionService processDefService, IMemoryCache cache)
    {
        _context = context;
        _processDefService = processDefService;
        _cache = cache;
    }

    /// <summary>
    /// 构建中间分配数据（主列表 GetPlanAsync 与排程汇总 GetScheduleSummaryAsync 共用）
    /// 批次 → 每个冷轧工序组一行，含三档分类输入(IsUrgent/IsNormal/AttentionMatchesCurrentCR)与时间桶(PositionDiff)
    /// </summary>
    private async Task<List<BatchAllocation>> BuildAllocationsAsync(string? sectionFilter)
    {
        var crKeys = await _processDefService.GetColdRollOrDrawKeysAsync();
        // 1. 加载所有在产/待产批次（投影仅加载需要的字段，减少数据传输）
        var batchProjections = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress)
            .Select(b => new
            {
                b.WorkOrderNo,
                b.CurrentGroupName,
                b.CurrentSectionName,
                b.CurrentSectionCompleted,
                b.NextProcess,
                b.NextSectionName,
                b.Status,
                b.CurrentValidWeight,
                b.SourceSpecification,
                b.CurrentEquipmentName,
                b.CurrentOutsource,
                ProcessGroups = b.ProcessGroups.Select(pg => new ProcessGroup
                {
                    Id = pg.Id,
                    ProductionBatchId = pg.ProductionBatchId,
                    SequenceNumber = pg.SequenceNumber,
                    ProcessName = pg.ProcessName,
                    ManufacturingSpec = pg.ManufacturingSpec,
                    ColdRollDraw = pg.ColdRollDraw,
                    OilPipeCut = pg.OilPipeCut,
                    Degrease = pg.Degrease,
                    EmulsionWash = pg.EmulsionWash,
                    UltrasonicWash = pg.UltrasonicWash,
                    ClothPolish = pg.ClothPolish,
                    BrightAnnealing = pg.BrightAnnealing,
                    Solution = pg.Solution,
                    Straighten = pg.Straighten,
                    Cut = pg.Cut,
                    ThicknessMeasure = pg.ThicknessMeasure,
                    Pickle = pg.Pickle,
                    OuterPolish = pg.OuterPolish,
                    InnerPolish = pg.InnerPolish,
                    InnerGrinding = pg.InnerGrinding,
                    OuterSpotGrinding = pg.OuterSpotGrinding,
                    SandBlasting = pg.SandBlasting,
                    ShotBlasting = pg.ShotBlasting,
                    Inspection = pg.Inspection,
                    WeldingHead = pg.WeldingHead,
                    Welding = pg.Welding,
                    Lubrication = pg.Lubrication,
                    Packing = pg.Packing,
                    Warehouse = pg.Warehouse,
                    Extra1 = pg.Extra1,
                    Extra2 = pg.Extra2,
                }).ToList()
            })
            .ToListAsync();

        // 2. LEFT JOIN WorkOrderExecutionSummary（仅加载相关批次的内存字典）
        var workOrderNos = batchProjections
            .Where(b => !string.IsNullOrEmpty(b.WorkOrderNo))
            .Select(b => b.WorkOrderNo)
            .Distinct()
            .ToList();

        var summaryDict = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Where(s => workOrderNos.Contains(s.WorkOrderNo))
            .ToDictionaryAsync(s => s.WorkOrderNo!);

        // 2b. 加载 WorkOrderPlan 薄表（按 WorkOrderId 索引）
        var workOrderIds = summaryDict.Values.Select(s => s.WorkOrderId).Distinct().ToList();
        var planDict = await _context.Set<WorkOrderPlan>()
            .AsNoTracking()
            .Where(p => workOrderIds.Contains(p.WorkOrderId))
            .ToDictionaryAsync(p => p.WorkOrderId);

        // 3. 逐批处理
        var intermediate = new List<BatchAllocation>();

        foreach (var batch in batchProjections)
        {
            var summary = summaryDict.GetValueOrDefault(batch.WorkOrderNo!);

            var sortedPgs = batch.ProcessGroups
                .OrderBy(pg => pg.SequenceNumber)
                .ToList();
            if (sortedPgs.Count == 0) continue;

            // 所有冷轧类工序组
            var coldRollPgs = sortedPgs.Where(pg => crKeys.Contains(ProcessKeys.ToKey(pg.ProcessName) ?? pg.ProcessName)).ToList();
            if (coldRollPgs.Count == 0) continue;

            // 确定批次当前所处的工序组序号
            int? currentPgSeq = sortedPgs
                .Where(pg => pg.ProcessName == batch.CurrentGroupName)
                .Select(pg => (int?)pg.SequenceNumber)
                .FirstOrDefault();

            // ===== 构建全局"工作序"：按工序组顺序累加实际非空工段数，得到每个工段在批次全流程中的执行序号 =====
            // 例：荒管处理 3 道工段(工作序 1-3) → 三辊冷轧冷轧拔为第 4 道(工作序 4) → 附加成检(5...)。
            // 目标冷轧拔工作序与批次当前执行工作序对比：当前已越过目标 → 批次已轧完此冷轧，非待轧。
            var sectionGlobalSeq = new Dictionary<(int GroupSeq, string SectionKey), int>();
            int currentGlobalExecSeq = 0;
            {
                int running = 0;
                foreach (var pg in sortedPgs)
                {
                    foreach (var (name, _) in pg.GetNonEmptySections())
                    {
                        running++;
                        var key = SectionKeys.ToKey(name) ?? name;
                        sectionGlobalSeq[(pg.SequenceNumber, key)] = running;
                    }
                    // 批次当前工序组已确定但无当前工段 → 视为该组已完成，当前执行工作序 = 该组最后一道工段
                    if (currentPgSeq.HasValue && pg.SequenceNumber == currentPgSeq.Value
                        && string.IsNullOrEmpty(batch.CurrentSectionName))
                    {
                        currentGlobalExecSeq = running;
                    }
                }
                // 批次有当前工段 → 取其全局工作序（未投产或匹配不到时保持 0）
                if (currentPgSeq.HasValue && !string.IsNullOrEmpty(batch.CurrentSectionName))
                {
                    var currentKey = SectionKeys.ToKey(batch.CurrentSectionName) ?? batch.CurrentSectionName;
                    currentGlobalExecSeq = sectionGlobalSeq.GetValueOrDefault((currentPgSeq.Value, currentKey), 0);
                }
            }

            // ===== 每个冷轧工序组都生成一行 =====
            // 工序组追踪快照（仅排程建议方式B流转折算用，其余消费方只加不读）——每批次惰性构建一次，供其全部冷轧行共享，
            // 避免同一批次 N 个冷轧组重复物化同一份快照
            List<ProcessGroupTrace>? pgTrace = null;
            foreach (var crPg in coldRollPgs)
            {
                // 工段筛选（中文 Tab 名归一为 Key 后与工序组 ProcessName(Key) 匹配）
                if (!string.IsNullOrEmpty(sectionFilter)
                    && crPg.ProcessName != (ProcessKeys.ToKey(sectionFilter) ?? sectionFilter))
                    continue;

                // 目标冷轧拔的全局工作序（该工段在批次全流程中的执行序号）
                int targetGlobalExecSeq = sectionGlobalSeq.GetValueOrDefault(
                    (crPg.SequenceNumber, SectionKeys.ColdRollDraw), 0);
                if (targetGlobalExecSeq <= 0) continue; // 该工序组无冷轧拔工段

                // 批次当前就在目标冷轧工序组内且无当前工段 → 本组已完成，视为已过此冷轧
                if (currentPgSeq.HasValue && crPg.SequenceNumber == currentPgSeq.Value)
                {
                    if (string.IsNullOrEmpty(batch.CurrentSectionName)) continue;
                    // 当前工段是冷轧拔且已完成 → 已过
                    if (batch.CurrentSectionName == SectionKeys.ColdRollDraw
                        && batch.CurrentSectionCompleted == true)
                        continue;
                }

                // 工作序对比：目标冷轧拔工作序 − 批次当前执行工作序（未投产=0）
                int diff = targetGlobalExecSeq - currentGlobalExecSeq;
                if (diff < 0) continue; // 批次当前执行工作序已越过目标冷轧拔工作序 → 已轧完，跳过

                // 判断是否正在此工序组做冷轧拔（近日在轧）
                bool isProducing = batch.Status == BatchStatus.InProgress
                    && !string.IsNullOrEmpty(batch.CurrentSectionName)
                    && batch.CurrentSectionName == SectionKeys.ColdRollDraw
                    && batch.CurrentSectionCompleted == false
                    && currentPgSeq.HasValue
                    && crPg.SequenceNumber == currentPgSeq.Value;

                // 只有真正在此PG做冷轧拔才能占位0（近日在轧），否则即使 diff==0 也应归入待轧今日(positionDiff=1)
                int positionDiff = isProducing ? 0 : (diff == 0 ? 1 : diff);

                // 规格维度推导
                var rollingSpec = crPg.ManufacturingSpec ?? "";
                var billetSpec = GetBilletSpec(sortedPgs, crPg, batch.SourceSpecification);
                var isFinished = crPg.SequenceNumber == sortedPgs.Max(pg => pg.SequenceNumber);

                var plan = summary != null ? planDict.GetValueOrDefault(summary.WorkOrderId) : null;
                var urgency = plan?.UrgencyLevel ?? summary?.UrgencyLevel;
                var productionFlowProperty = plan?.ProductionFlowProperty ?? summary?.ProductionFlowProperty;
                var attentionProcess = plan?.ProductionAttentionProcess ?? summary?.MainNoAttentionProcess;

                // Model B：三档分类器输入（不再使用 IsKeyBatch/IsGeneralKeyBatch）
                // isUrgent = UrgencyLevelKeys.IsUrgent(urgency)（与下方 IsUrgent 字段同源）
                // isNormal = 正常流转；attentionMatchesCurrentCR = 关注工序 == 当前冷轧排程行 ProcessType（ProcessKeys 归一）
                bool isNormal = productionFlowProperty == ProductionFlowKeys.Normal;
                bool attentionMatchesCurrentCR = false;
                if (!string.IsNullOrEmpty(attentionProcess))
                {
                    var attnKey = ProcessKeys.ToKey(attentionProcess) ?? attentionProcess;
                    var pgKey = ProcessKeys.ToKey(crPg.ProcessName) ?? crPg.ProcessName;
                    attentionMatchesCurrentCR = string.Equals(attnKey, pgKey, StringComparison.OrdinalIgnoreCase);
                }

                intermediate.Add(new BatchAllocation
                {
                    WorkOrderNo = batch.WorkOrderNo ?? "",
                    ProcessType = crPg.ProcessName,
                    BilletSpec = billetSpec,
                    RollingSpec = rollingSpec,
                    IsFinished = isFinished,
                    IsUrgent = UrgencyLevelKeys.IsUrgent(urgency),
                    UrgencyLevel = urgency,
                    IsNormal = isNormal,
                    AttentionMatchesCurrentCR = attentionMatchesCurrentCR,
                    PositionDiff = positionDiff,
                    Weight = batch.CurrentValidWeight ?? 0m,
                    // 在轧单位或设备：优先批次「委外单位」，为空再取「设备号」（实际后续基本只有委外单位）
                    MachineNo = isProducing ? (batch.CurrentOutsource ?? batch.CurrentEquipmentName) : null,
                    ShortDisplay = GetShortDisplay(billetSpec, rollingSpec),
                    // 是否批次当前所在工序组（flowDemand 部分一 2030 本组批次判定：5060 批次的下游 30 组不属「2030 本组」）
                    IsCurrentGroup = currentPgSeq.HasValue && crPg.SequenceNumber == currentPgSeq.Value,
                    // 工序组追踪快照（仅排程建议方式B流转折算用，其余消费方只加不读）
                    ProcessGroups = pgTrace ??= sortedPgs.Select(pg => new ProcessGroupTrace
                    {
                        ProcessName = pg.ProcessName,
                        ManufacturingSpec = pg.ManufacturingSpec,
                        SequenceNumber = pg.SequenceNumber,
                        IsFinished = pg.SequenceNumber == sortedPgs.Max(x => x.SequenceNumber),
                    }).ToList(),
                });
            }
        }

        return intermediate;
    }

    public async Task<List<ColdRollPlanRowDto>> GetPlanAsync(string? sectionFilter)
    {
        var intermediate = await BuildAllocationsAsync(sectionFilter);

        // 排程设置（判定「在档」标记：实际批次是否命中在轧/待轧档位，客户端据此决定「在轧要求/待轧要求」是否显示）
        var scheduleDict = await _context.ColdRollSpecSchedules
            .AsNoTracking()
            .Select(s => new { s.ProcessType, s.BilletSpec, s.RollingSpec, s.IsFinished, s.CompletionType, s.RollType })
            .ToDictionaryAsync(
                s => $"{s.ProcessType}|{s.BilletSpec}|{s.RollingSpec}|{s.IsFinished}",
                s => new { s.CompletionType, s.RollType },
                StringComparer.OrdinalIgnoreCase);

        // 5. 聚合：按 (ProcessType, BilletSpec, RollingSpec, IsFinished) 分组
        var result = intermediate
            .GroupBy(r => new { r.ProcessType, r.BilletSpec, r.RollingSpec, r.IsFinished })
            .Select(g =>
            {
                var row = new ColdRollPlanRowDto
                {
                    ProcessType = g.Key.ProcessType,
                    BilletSpec = g.Key.BilletSpec,
                    RollingSpec = g.Key.RollingSpec,
                    IsFinished = g.Key.IsFinished,
                    BatchCount = g.Count(),
                };

                // 在档标记：仅当该规格存在「实际命中排程行档位」的在轧/待轧批次时才标记，客户端据此决定
                // 「在轧要求/待轧要求」是否显示。命中判定与排程建议「计划流转量」同口径（MatchesScheduleType）：
                // 批次急/流转属性须与排程行档位匹配，否则即使规格有待流转量、排程行也有档位，该规格也不属于
                // 本次排程计划内 → 留空，人工可区分哪些规格真正在本次排程建议中（如 67-48 规格虽有待流转量，
                // 但未设入本次流转计划，则「待轧要求」不显示档位）。
                var allocList = g.ToList();
                var inProdAlloc = allocList.Where(x => x.PositionDiff == 0).ToList();
                var inWaitAlloc = allocList.Where(x => x.PositionDiff >= 1 && x.PositionDiff <= 6).ToList();
                var schedKey = $"{g.Key.ProcessType}|{g.Key.BilletSpec}|{g.Key.RollingSpec}|{g.Key.IsFinished}";
                if (scheduleDict.TryGetValue(schedKey, out var sched))
                {
                    row.ProdTierMatched = inProdAlloc.Any(a =>
                        !string.IsNullOrEmpty(sched.CompletionType)
                        && MatchesScheduleType(sched.CompletionType, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel));
                    row.WaitTierMatched = inWaitAlloc.Any(a =>
                        !string.IsNullOrEmpty(sched.RollType)
                        && MatchesScheduleType(sched.RollType, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel));
                }

                foreach (var item in g)
                {
                    if (item.PositionDiff == 0)
                    {
                        row.WeightProd += item.Weight;
                        if (item.IsUrgent && item.IsNormal && item.AttentionMatchesCurrentCR)
                            row.WeightProdUrgent += item.Weight;
                        else if (item.IsUrgent && item.IsNormal)
                            row.WeightProdUrgentSub += item.Weight;
                        else if (item.IsUrgent)
                            row.WeightProdUrgentOther += item.Weight;
                    }
                    else if (item.PositionDiff == 1)
                    {
                        row.WeightToday += item.Weight;
                        AccumulateWaitUrgent(row, item);
                    }
                    else if (item.PositionDiff == 2)
                    {
                        row.WeightTomorrow += item.Weight;
                        AccumulateWaitUrgent(row, item);
                    }
                    else if (item.PositionDiff == 3)
                    {
                        row.WeightDayAfter += item.Weight;
                        AccumulateWaitUrgent(row, item);
                    }
                    else if (item.PositionDiff == 4)
                    {
                        row.WeightExt3 += item.Weight;
                        AccumulateWaitUrgent(row, item);
                    }
                    else if (item.PositionDiff == 5)
                    {
                        row.WeightExt4 += item.Weight;
                        AccumulateWaitUrgent(row, item);
                    }
                    else if (item.PositionDiff == 6)
                    {
                        row.WeightExt5 += item.Weight;
                        AccumulateWaitUrgent(row, item);
                    }
                    else if (item.PositionDiff > 6)
                    {
                        row.WeightDistant += item.Weight;
                    }

                }

                // 计算展示字段
                row.WeightWaitNear = row.WeightToday + row.WeightTomorrow + row.WeightDayAfter
                    + row.WeightExt3 + row.WeightExt4 + row.WeightExt5;

                // 总量 = 近日在轧 + 近日待轧 + 远日量
                row.WeightTotal = row.WeightProd + row.WeightWaitNear + row.WeightDistant;

                row.MergeDisplay = $"{row.BilletSpec}×{row.RollingSpec}-{(row.IsFinished ? "成品" : "在制品")}";
                row.ShortDisplay = GetShortDisplay(row.BilletSpec, row.RollingSpec);

                // 在轧设备号：从近日在轧批次的设备字段聚合（去重）
                var prodMachineNos = g.Where(x => x.PositionDiff == 0 && !string.IsNullOrEmpty(x.MachineNo))
                    .Select(x => x.MachineNo)
                    .Distinct()
                    .ToList();
                if (prodMachineNos.Any())
                    row.MachineNo = string.Join("；", prodMachineNos);

                return row;
            })
            .OrderBy(r => r.ProcessType)
            .ThenBy(r => r.BilletSpec)
            .ThenBy(r => r.RollingSpec)
            .ToList();

        return result;
    }

    /// <summary>
    /// 冷轧排程汇总：复用主列表中间数据，按 (冷轧类型, 外径跨度) 聚合分档
    /// 在轧(PositionDiff==0) 与 待轧(PositionDiff 1~6) 均按三档分类器对称分档：
    ///   特急 = IsUrgent ∧ 正常流转 ∧ 关注==当前冷轧；特急- = IsUrgent ∧ 正常流转 ∧ 关注≠当前冷轧；急 = IsUrgent ∧ 非正常流转
    /// 余量 = 该侧总量 − 特急 − 特急- − 急
    /// 仅统计"排程设置"中按档位排程的批次：在轧侧按 CompletionType(在轧要求)、待轧侧按 RollType(待轧要求) 匹配档位，
    /// 档位不匹配的批次不计入（如要求=特急，则只统计特急档，特急-/急/普通批次不显示）
    /// </summary>
    public async Task<List<ColdRollPlanSummaryDto>> GetScheduleSummaryAsync(string? sectionFilter, int? maxDiff = null)
    {
        var allocations = await BuildAllocationsAsync(sectionFilter);

        // 仅统计"排程设置"中按档位排程的批次：
        // 在轧侧(PositionDiff==0)按 CompletionType(在轧要求)、待轧侧按 RollType(待轧要求) 匹配档位。
        // 档位语义（与批次计划 BatchPlanScheduleService/BatchPlanDto 一致）：
        //   All/Subsequent → 该侧全部批次；CrOnly → 特急(正常流转∧关注==当前冷轧)；
        //   Urgent/Partial1 → 特急/特急-(正常流转)；Partial2 → A+/A急(IsUrgent)；Partial3 → A+/A急 或 B顺；None/无排程记录 → 该侧不统计
        var scheduleDict = await _context.ColdRollSpecSchedules
            .AsNoTracking()
            .Select(s => new { s.ProcessType, s.BilletSpec, s.RollingSpec, s.IsFinished, s.CompletionType, s.RollType })
            .ToDictionaryAsync(
                s => $"{s.ProcessType}|{s.BilletSpec}|{s.RollingSpec}|{s.IsFinished}",
                s => new { s.CompletionType, s.RollType },
                StringComparer.OrdinalIgnoreCase);

        allocations = allocations
            .Where(a =>
            {
                if (!scheduleDict.TryGetValue($"{a.ProcessType}|{a.BilletSpec}|{a.RollingSpec}|{a.IsFinished}", out var sched))
                    return false;
                return a.PositionDiff == 0
                    ? MatchesScheduleType(sched.CompletionType, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel)
                    : MatchesScheduleType(sched.RollType, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel);
            })
            .ToList();

        // maxDiff 过滤：在轧(0)恒包含；全部(null)=待轧近(1~6，不含远日)；近2天=≤2；近4天=≤4
        allocations = maxDiff.HasValue
            ? allocations.Where(a => a.PositionDiff == 0 || a.PositionDiff <= maxDiff.Value).ToList()
            : allocations.Where(a => a.PositionDiff <= 6).ToList();

        if (allocations.Count == 0) return new List<ColdRollPlanSummaryDto>();

        var result = allocations
            .GroupBy(a => new { a.ProcessType, a.ShortDisplay })
            .Select(g =>
            {
                var inProd = g.Where(a => a.PositionDiff == 0).ToList();
                var inWait = g.Where(a => a.PositionDiff > 0).ToList();

                decimal prodTotal = inProd.Sum(a => a.Weight);
                decimal prodUrgent = inProd.Where(a => a.IsUrgent && a.IsNormal && a.AttentionMatchesCurrentCR).Sum(a => a.Weight);
                decimal prodUrgentSub = inProd.Where(a => a.IsUrgent && a.IsNormal && !a.AttentionMatchesCurrentCR).Sum(a => a.Weight);
                decimal prodOther = inProd.Where(a => a.IsUrgent && !a.IsNormal).Sum(a => a.Weight);

                decimal waitTotal = inWait.Sum(a => a.Weight);
                decimal waitUrgent = inWait.Where(a => a.IsUrgent && a.IsNormal && a.AttentionMatchesCurrentCR).Sum(a => a.Weight);
                decimal waitUrgentSub = inWait.Where(a => a.IsUrgent && a.IsNormal && !a.AttentionMatchesCurrentCR).Sum(a => a.Weight);
                decimal waitOther = inWait.Where(a => a.IsUrgent && !a.IsNormal).Sum(a => a.Weight);

                return new ColdRollPlanSummaryDto
                {
                    ProcessType = g.Key.ProcessType,
                    ShortDisplay = g.Key.ShortDisplay,
                    BatchCount = g.Count(),
                    TotalFlowWeight = prodTotal + waitTotal,

                    ProdTotalWeight = prodTotal,
                    ProdUrgentWeight = prodUrgent,
                    ProdUrgentSubWeight = prodUrgentSub,
                    ProdOtherWeight = prodOther,
                    ProdRestWeight = prodTotal - prodUrgent - prodUrgentSub - prodOther,

                    WaitTotalWeight = waitTotal,
                    WaitUrgentWeight = waitUrgent,
                    WaitUrgentSubWeight = waitUrgentSub,
                    WaitOtherWeight = waitOther,
                    WaitRestWeight = waitTotal - waitUrgent - waitUrgentSub - waitOther,
                };
            })
            .OrderBy(r => r.ProcessType)
            .ThenBy(r => r.ShortDisplay)
            .ToList();

        return result;
    }

    /// <summary>
    /// 获取冷轧排程排机估算：按轧机类型聚合 4 行（冷轧5060/冷轧2030/冷轧三辊/冷拔）
    /// 口径与排程汇总一致：仅已排程且匹配档位的批次，近6天（在轧+待轧，PositionDiff≤6，不含远日）
    /// 在制品/成品按排程产出形态拆分（IsFinished=最后工序组为成品），成品+在制=流转总量
    /// 机台需求数 = Σ(规格流转量 ÷ 单机单日量) ÷ 6天，对总数四舍五入（AwayFromZero）得每日台数；单机单日量为空/≤0 的规格不计入
    /// </summary>
    public async Task<List<ColdRollMachineEstimateDto>> GetMachineEstimateAsync()
    {
        // 缓存 60 秒：排程保存会主动失效（见 ColdRollSpecScheduleService.SaveAllAsync），批次/生产数据靠短 TTL 保新鲜
        return await _cache.GetOrCreateAsync(MachineEstimateCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await GetMachineEstimateCoreAsync();
        }) ?? new List<ColdRollMachineEstimateDto>();
    }

    private async Task<List<ColdRollMachineEstimateDto>> GetMachineEstimateCoreAsync()
    {
        var allocations = await BuildAllocationsAsync(null);

        // 加载排程设置（含档位 + 兜底单机单日量），键 = ProcessType|BilletSpec|RollingSpec|IsFinished
        var scheduleDict = await _context.ColdRollSpecSchedules
            .AsNoTracking()
            .Select(s => new { s.ProcessType, s.BilletSpec, s.RollingSpec, s.IsFinished, s.CompletionType, s.RollType, s.DailyOutput })
            .ToDictionaryAsync(
                s => $"{s.ProcessType}|{s.BilletSpec}|{s.RollingSpec}|{s.IsFinished}",
                s => new { s.CompletionType, s.RollType, s.DailyOutput },
                StringComparer.OrdinalIgnoreCase);

        // 产能档案（参数表）优先：单机单日量的权威来源（排程保存反哺 + 手工调整，双向同步）
        var capacityDict = await _context.ColdRollCapacities
            .AsNoTracking()
            .ToDictionaryAsync(
                c => $"{c.ProcessType}|{c.BilletSpec}|{c.RollingSpec}|{c.IsFinished}",
                c => c.DailyOutput,
                StringComparer.OrdinalIgnoreCase);

        // 档位过滤 + 固定近6天（在轧0恒包含，待轧1~6，不含远日）——与排程汇总 GetScheduleSummaryAsync 一致
        var scheduled = allocations
            .Where(a =>
            {
                if (!scheduleDict.TryGetValue($"{a.ProcessType}|{a.BilletSpec}|{a.RollingSpec}|{a.IsFinished}", out var sched))
                    return false;
                return a.PositionDiff == 0
                    ? MatchesScheduleType(sched.CompletionType, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel)
                    : MatchesScheduleType(sched.RollType, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel);
            })
            .Where(a => a.PositionDiff <= 6)
            .ToList();

        // 轧机类型归并（ProcessType 英文 Key，机台组定义配置表驱动：LoadMachineGroupsAsync）
        var machineTypeGroups = await LoadMachineGroupsAsync();

        var result = new List<ColdRollMachineEstimateDto>();
        foreach (var group in machineTypeGroups)
        {
            var groupAlloc = scheduled.Where(a => group.ContainsKey(a.ProcessType)).ToList();

            decimal flowTotal = groupAlloc.Sum(a => a.Weight);
            decimal finished = groupAlloc.Where(a => a.IsFinished).Sum(a => a.Weight);
            decimal inProcess = flowTotal - finished;

            // 机台需求：Σ(规格流转量 ÷ 单机单日量) ÷ 6天，四舍五入；单机单日量为空/≤0 的规格贡献 0
            // 单机单日量取值：产能档案（参数表）优先，缺失/无效回退排程小表
            // 供给方组（配置了 SupplyTargetGroupKey）与排程建议同口径：在制/成品分档各自四舍五入再相加（其余组整组一次取整）
            int MachineCountOf(IEnumerable<BatchAllocation> allocs)
            {
                decimal machineDays = 0m;
                foreach (var a in allocs)
                {
                    var key = $"{a.ProcessType}|{a.BilletSpec}|{a.RollingSpec}|{a.IsFinished}";
                    decimal? dailyOutput = null;
                    if (capacityDict.TryGetValue(key, out var capOutput) && capOutput.HasValue && capOutput.Value > 0)
                        dailyOutput = capOutput;
                    else if (scheduleDict.TryGetValue(key, out var sched) && sched.DailyOutput.HasValue && sched.DailyOutput.Value > 0)
                        dailyOutput = sched.DailyOutput;

                    if (dailyOutput.HasValue)
                        machineDays += a.Weight / (dailyOutput.Value * 6m);
                }
                return (int)Math.Round(machineDays, MidpointRounding.AwayFromZero);
            }

            var machineCount = !string.IsNullOrEmpty(group.SupplyTargetGroupKey)
                ? MachineCountOf(groupAlloc.Where(a => !a.IsFinished)) + MachineCountOf(groupAlloc.Where(a => a.IsFinished))
                : MachineCountOf(groupAlloc);

            result.Add(new ColdRollMachineEstimateDto
            {
                MachineType = group.Display,
                FlowTotalWeight = flowTotal,
                InProcessWeight = inProcess,
                FinishedWeight = finished,
                MachineCount = machineCount,
            });
        }

        return result;
    }

    /// <summary>
    /// 获取冷轧排程建议（半自动）：机台类型组级 特急锁定 → 流转保底 → 产能平衡 三步决策。
    /// 只读不写，矛盾（A/A'/B）标注交人；一键采用由前端走既有 save-all 通道回填小表。
    /// </summary>
    public async Task<List<ColdRollScheduleSuggestionDto>> GetScheduleSuggestionAsync()
    {
        // 缓存 60 秒：排程保存/产能档案保存/机台数配置保存·删除会主动失效（四处失效点）
        return await _cache.GetOrCreateAsync(ScheduleSuggestionCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await BuildScheduleSuggestionCoreAsync();
        }) ?? new List<ColdRollScheduleSuggestionDto>();
    }

    private async Task<List<ColdRollScheduleSuggestionDto>> BuildScheduleSuggestionCoreAsync()
    {
        var allocations = await BuildAllocationsAsync(null);

        // 排程设置（含档位 + 单机单日量 + 机台/备注），键 = ProcessType|BilletSpec|RollingSpec|IsFinished
        var scheduleDict = await _context.ColdRollSpecSchedules
            .AsNoTracking()
            .Select(s => new { s.ProcessType, s.BilletSpec, s.RollingSpec, s.IsFinished, s.CompletionType, s.RollType, s.DailyOutput, s.MachineNo, s.MergeDisplay, s.Remark })
            .ToDictionaryAsync(
                s => $"{s.ProcessType}|{s.BilletSpec}|{s.RollingSpec}|{s.IsFinished}",
                s => new ScheduleRow
                {
                    CompletionType = s.CompletionType,
                    RollType = s.RollType,
                    DailyOutput = s.DailyOutput,
                    MachineNo = s.MachineNo,
                    MergeDisplay = s.MergeDisplay,
                    Remark = s.Remark,
                },
                StringComparer.OrdinalIgnoreCase);

        // 产能档案（参数表）优先：单机单日量的权威来源（排程保存反哺 + 手工调整，双向同步）
        var capacityDict = await _context.ColdRollCapacities
            .AsNoTracking()
            .ToDictionaryAsync(
                c => $"{c.ProcessType}|{c.BilletSpec}|{c.RollingSpec}|{c.IsFinished}",
                c => c.DailyOutput,
                StringComparer.OrdinalIgnoreCase);

        // 机台数配置（按单冷轧类型）
        var machineConfigDict = (await _context.ColdRollMachineConfigs.AsNoTracking().ToListAsync())
            .ToDictionary(c => c.ProcessType, StringComparer.OrdinalIgnoreCase);

        // 轧机类型归并（覆盖关系：60 可干 50、30 覆盖 20，机台需求按组聚合；组定义配置表驱动：LoadMachineGroupsAsync）
        var machineTypeGroups = await LoadMachineGroupsAsync();

        // 四维 key 合并：scheduleDict 现有行必保（save-all 按 incoming 删僵尸）+ allocations 新维度必提（尤其急+行）
        var allKeys = scheduleDict.Keys
            .Concat(allocations.Select(a => KeyOf(a)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 供需链（2026-08-29 方案 A）：按链对计算流入/承接，允许多条并行链、多级链（替代原硬编码 5060→2030 全局单链）。
        // demandInflow[需求组Key] = (FromSupplier=Σ所有指向它的供给流入, Total=FromSupplier+本组未定流转, ...)
        // supplierOutflow[供给组Key] = (FromSupplier=本组流向目标组流入, Total=目标组总承接, ...)
        var (demandInflow, supplierOutflow) =
            ComputeFlowDemand(allocations, scheduleDict, capacityDict, machineConfigDict, machineTypeGroups);

        var result = new List<ColdRollScheduleSuggestionDto>();
        foreach (var group in machineTypeGroups)
        {
            var groupAlloc = allocations.Where(a => group.ContainsKey(a.ProcessType)).ToList();
            var groupKeys = allKeys
                .Where(k => group.ContainsKey(ProcessTypeOf(k)))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 组内机型配置之和（无配置机型=0）
            int minMachines = group.Keys.Sum(k => machineConfigDict.GetValueOrDefault(k)?.MinMachines ?? 0);
            int maxMachines = group.Keys.Sum(k => machineConfigDict.GetValueOrDefault(k)?.MaxMachines ?? 0);

            // 组近6天可流转量（全部批次，不论是否排程）/ 在制量（展示）
            // 「本次计划流转量」在行级建议构建后按建议档位求和（= 明细行计划量之和，见 result.Add 处 PlannedFlowWeight）
            var groupNear = groupAlloc.Where(a => a.PositionDiff <= 6).ToList();
            decimal flowTotalWeight = groupNear.Sum(a => a.Weight);
            decimal inProcessWeight = groupNear.Where(a => a.PositionDiff == 0).Sum(a => a.Weight);

            // 当前档位（组内有排程行的最宽档；无则 "-"）与现状机台数
            string currentTier = ComputeCurrentTier(groupKeys, scheduleDict);
            int currentCount = CountAtCurrentTiers(groupAlloc, scheduleDict, capacityDict);

            // 流转保底（优先3）：需求方组（被供给组指向）/ 供给方组（配置了 SupplyTargetGroupKey）；组目标机台数
            bool isDemander = demandInflow.ContainsKey(group.Key);
            bool isSupplier = !string.IsNullOrEmpty(group.SupplyTargetGroupKey);
            var targetGroup = isSupplier
                ? machineTypeGroups.FirstOrDefault(g => string.Equals(g.Key, group.SupplyTargetGroupKey, StringComparison.OrdinalIgnoreCase))
                : null;
            int targetMinMachines = targetGroup?.Keys.Sum(k => machineConfigDict.GetValueOrDefault(k)?.MinMachines ?? 0) ?? 0;
            FlowStateDto? flowState = null;
            if (isDemander && isSupplier)
            {
                // 多级链中间节点：既承接上游供给，又再供给下游
                var din = demandInflow[group.Key];
                var sout = supplierOutflow[group.Key];
                flowState = new FlowStateDto
                {
                    Role = "Both",
                    SupplyMachines = din.Total,
                    TotalWeight = din.TotalWeight,
                    NeedMachines = minMachines,
                    Balanced = din.Total >= minMachines,
                    TargetGroupDisplay = targetGroup?.Display ?? group.SupplyTargetGroupKey ?? "",
                    SupplyToTargetMachines = sout.FromSupplier,
                    SupplyToTargetWeight = sout.FromSupplierWeight,
                    Text = $"{group.Display}：承接流转 {din.Total} 台 / {din.TotalWeight.ToString("G29")}kg（供给流入 {din.FromSupplier} 台 / {din.FromSupplierWeight.ToString("G29")}kg + 本组未定流转 {din.Total - din.FromSupplier} 台 / {(din.TotalWeight - din.FromSupplierWeight).ToString("G29")}kg），再供给下游 {sout.FromSupplier} 台 / {sout.FromSupplierWeight.ToString("G29")}kg，最小需 {minMachines} 台，流转{(din.Total >= minMachines ? "平衡" : "不足")}",
                };
            }
            else if (isDemander)
            {
                var din = demandInflow[group.Key];
                flowState = new FlowStateDto
                {
                    Role = "Demander",
                    SupplyMachines = din.Total,
                    TotalWeight = din.TotalWeight,
                    NeedMachines = minMachines,
                    Balanced = din.Total >= minMachines,
                    Text = $"{group.Display} 下次承接流转 {din.Total} 台 / {din.TotalWeight.ToString("G29")}kg（供给流入 {din.FromSupplier} 台 / {din.FromSupplierWeight.ToString("G29")}kg + 本次未定流转 {din.Total - din.FromSupplier} 台 / {(din.TotalWeight - din.FromSupplierWeight).ToString("G29")}kg），{group.Display} 最小需 {minMachines} 台，流转{(din.Total >= minMachines ? "平衡" : "不足")}",
                };
            }
            else if (isSupplier)
            {
                var sout = supplierOutflow[group.Key];
                var tgtName = targetGroup?.Display ?? group.SupplyTargetGroupKey ?? "";
                flowState = new FlowStateDto
                {
                    Role = "Supplier",
                    SupplyMachines = sout.FromSupplier,
                    TotalWeight = sout.FromSupplierWeight,
                    NeedMachines = targetMinMachines,
                    Balanced = sout.Total >= targetMinMachines,
                    TargetGroupDisplay = tgtName,
                    SupplyToTargetMachines = sout.FromSupplier,
                    SupplyToTargetWeight = sout.FromSupplierWeight,
                    Text = $"{group.Display} 本次流转可供给 {tgtName} 流入 {sout.FromSupplier} 台 / {sout.FromSupplierWeight.ToString("G29")}kg（{tgtName} 下次总承接 {sout.Total} 台 / {sout.TotalWeight.ToString("G29")}kg 含本次未定流转），{tgtName} 最小需 {targetMinMachines} 台，流转{(sout.Total >= targetMinMachines ? "平衡" : "不足")}",
                };
            }
            int minTarget = isDemander ? Math.Max(minMachines, demandInflow[group.Key].Total) : minMachines;

            // ===== v2 档位决策：默认「急+/急/急-」起步，双向调整（产能平衡优先2）=====
            string suggestedTier;
            string status = "OK";
            var conflicts = new List<string>();
            string? inProdTier = null;      // 5060 ②流转平衡：在制行（IsFinished=false）档位
            string? finishedTier = null;    // 5060 ②流转平衡：成品行（IsFinished=true）档位

            if (groupKeys.Count == 0)
            {
                suggestedTier = "-"; // 无排程行无批次，无可建议
            }
            else if (maxMachines <= 0)
            {
                // 未配置机台数上限：无区间约束，保持默认原始档（特急锁定在行级仍生效）
                suggestedTier = "Partial2";
            }
            else
            {
                // 统一产能平衡（2026-08-29）：凡配了 MaxMachines 的组（无论有无供需链）都受机台数上限约束。
                // 无供需组（三辊/冷拔独立池、或取消供给目标组后的 5060）不再无条件固定 Partial2——
                // 超上限向窄收（Urgent→CrOnly）并标注矛盾 A'，不足向宽放（Partial3→All）并标注矛盾 A。
                var chosen = ChooseTierBidirectional(group.Display, groupAlloc, minTarget, maxMachines, scheduleDict, capacityDict);
                suggestedTier = chosen.tier;
                status = chosen.status;
                conflicts.AddRange(chosen.conflicts);
            }

            // ===== 供给方组 ②流转平衡：两阶段——先只放宽在制喂饱目标需求组，再压成品防总负荷超上限 =====
            // 判据统一目标组基准：在制品堆按「目标组产能档案 daily」折算的供给机台 < 本组流向目标组流入才触发阶段1
            // （本组未定流转由目标组自身承接，不由本组供给决定，故不进判据；只有真喂不饱目标组时才动成品）
            var flowToTarget = isSupplier ? supplierOutflow[group.Key].FromSupplier : 0;
            if (isSupplier && targetGroup != null && suggestedTier != "-" && flowToTarget > 0)
            {
                inProdTier = suggestedTier;
                finishedTier = suggestedTier;
                var inProdAlloc = groupAlloc.Where(a => !a.IsFinished).ToList();
                var finishedAlloc = groupAlloc.Where(a => a.IsFinished).ToList();
                if (inProdAlloc.Count > 0 && finishedAlloc.Count > 0)
                {
                    // 阶段1：先只放宽在制档位，直到在制供给（目标组产能折算）≥ 本组流向目标组流入 或放宽到 All；成品不动
                    int guard1 = 0;
                    while (CountAtTierFlowTo(inProdAlloc, inProdTier, capacityDict, machineConfigDict, group, targetGroup) < flowToTarget
                        && inProdTier != "All" && guard1++ < 5)
                    {
                        var nextIn = TierStepWide(inProdTier);
                        if (nextIn == null) break;
                        inProdTier = nextIn;
                    }

                    // 阶段2：放宽后若本组总机台（在制+成品，本组产能档案 daily 口径）拉出组上限 → 压缩成品档位让路
                    int guard2 = 0;
                    while (CountAtTier(inProdAlloc, inProdTier, scheduleDict, capacityDict)
                            + CountAtTier(finishedAlloc, finishedTier, scheduleDict, capacityDict) > maxMachines
                        && finishedTier != "CrOnly" && guard2++ < 5)
                    {
                        var nextFin = TierStepNarrow(finishedTier);
                        if (nextFin == null) break;
                        finishedTier = nextFin;
                    }
                }
            }

            // 流转保底矛盾 B（可叠加 A/A'）
            bool flowUnbalanced = false;
            if (isDemander)
            {
                var din = demandInflow[group.Key];
                flowUnbalanced = din.Total > 0 && din.Total < minMachines;
            }
            if (flowUnbalanced)
            {
                var din = demandInflow[group.Key];
                status = status == "OK" ? "B" : status + ",B";
                conflicts.Add($"{group.Display} 下次承接流转 {din.Total} 台（供给流入 {din.FromSupplier} + 本次未定流转 {din.Total - din.FromSupplier}）< {group.Display} 最小机台数 {minMachines} 台，请人工平衡供给/需求流转");
            }

            // 行级建议（按决策对象回填：三辊/冷拔→Partial2；2030→组档；5060→在制/成品档；矛盾→null 仅特急锁定）
            var items = groupKeys
                .Select(k =>
                {
                    string? itemTier = null;
                    if (suggestedTier != "-")
                    {
                        if (isSupplier && inProdTier != null && finishedTier != null)
                        {
                            bool isFin = string.Equals(k.Split('|')[3], "True", StringComparison.OrdinalIgnoreCase);
                            itemTier = isFin ? finishedTier : inProdTier;
                        }
                        else
                        {
                            itemTier = suggestedTier;
                        }
                    }
                    return BuildSuggestionItem(k, scheduleDict, allocations, itemTier);
                })
                .ToList();

            bool tierChanged = suggestedTier != "-"
                && !string.Equals(suggestedTier, currentTier, StringComparison.OrdinalIgnoreCase);

            // 档位显示名：CurrentTier=最宽档名；SuggestedTier=建议档名（始终显示档位名，不显示"保持"——与当前一致时档位名即建议值，避免语义不清）
            string currentTierDisplay = currentTier == "-" ? "-" : TierDisplay(currentTier);
            string suggestedTierDisplay = suggestedTier == "-" ? "-" : TierDisplay(suggestedTier);

            // 组机台数：5060 拆档后 = 在制档 + 成品档 机台之和；其余 = 组档下机台
            int machineCount = currentCount;
            if (suggestedTier != "-")
            {
                if (isSupplier && inProdTier != null && finishedTier != null)
                {
                    var inProdAlloc = groupAlloc.Where(a => !a.IsFinished).ToList();
                    var finishedAlloc = groupAlloc.Where(a => a.IsFinished).ToList();
                    machineCount = CountAtTier(inProdAlloc, inProdTier, scheduleDict, capacityDict)
                                 + CountAtTier(finishedAlloc, finishedTier, scheduleDict, capacityDict);
                }
                else
                {
                    machineCount = CountAtTier(groupAlloc, suggestedTier, scheduleDict, capacityDict);
                }
            }

            result.Add(new ColdRollScheduleSuggestionDto
            {
                MachineType = group.Display,
                MemberProcessTypes = group.Keys,
                MinMachines = minMachines,
                MaxMachines = maxMachines,
                MachineCount = machineCount,
                CurrentTier = currentTierDisplay,
                SuggestedTier = suggestedTierDisplay,
                TierChanged = tierChanged,
                HasUrgentPlus = groupAlloc.Any(a => a.IsUrgent && a.IsNormal && a.AttentionMatchesCurrentCR),
                Status = status,
                Conflicts = conflicts,
                FlowState = flowState,
                FlowTotalWeight = flowTotalWeight,
                // 本次计划流转量 = 明细行计划量之和（按建议档位命中，与行级「计划在轧量+计划待轧量」口径一致）
                PlannedFlowWeight = items.Sum(i => i.PlannedInProdWeight + i.PlannedInWaitWeight),
                InProcessWeight = inProcessWeight,
                InProdTier = inProdTier,
                FinishedTier = finishedTier,
                Items = items,
            });
        }

        return result;
    }

    // ========== 机台组配置（配置表驱动，替代 ColdRollMachineGroupKeys.Groups 硬编码） ==========

    /// <summary>机台组定义（引擎归组运行时内存模型）：Key/Display/Keys[]/SupplyTargetGroupKey（配置表 ColdRollMachineGroupConfigs 映射，组角色字段已移除）</summary>
    internal sealed class MachineGroupDef
    {
        public string Key { get; init; } = "";
        public string Display { get; init; } = "";
        public string[] Keys { get; init; } = Array.Empty<string>();

        /// <summary>供给目标组 Key（可空）：本组为供给方时指向的下游需求组；引擎 isSupplier 判定 + 按链对流转折算依据</summary>
        public string? SupplyTargetGroupKey { get; init; }

        /// <summary>组内工序判定（OrdinalIgnoreCase：SQL 大小写不敏感，内存比较须忽略大小写）</summary>
        public bool ContainsKey(string? processType)
            => !string.IsNullOrEmpty(processType)
               && Keys.Any(k => string.Equals(k, processType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 加载机台组定义（配置表按 DisplayOrder 排序，缓存 60 秒）。
    /// 组配置保存/删除由 ColdRollMachineGroupConfigService 主动失效；短 TTL 双保险。
    /// </summary>
    private async Task<List<MachineGroupDef>> LoadMachineGroupsAsync()
    {
        return await _cache.GetOrCreateAsync(MachineGroupCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            var rows = await _context.ColdRollMachineGroupConfigs
                .AsNoTracking()
                .OrderBy(g => g.DisplayOrder)
                .ToListAsync();
            return rows.Select(g => new MachineGroupDef
            {
                Key = g.GroupKey,
                Display = g.DisplayName,
                Keys = SplitProcessKeys(g.ProcessKeys),
                SupplyTargetGroupKey = g.SupplyTargetGroupKey,
            }).ToList();
        }) ?? new List<MachineGroupDef>();
    }

    /// <summary>逗号分隔工序串 → 工序 Key 数组（Trim + 去空）</summary>
    private static string[] SplitProcessKeys(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // ========== 私有方法 ==========

    /// <summary>
    /// 排程要求档位是否匹配批次（与批次计划 BatchPlanDto._trigger 档位语义一致，Model B）：
    /// All/Subsequent=该侧全部；CrOnly=特急(正常流转∧关注==当前冷轧)；Urgent/Partial1=特急/特急-(正常流转)；
    /// Partial2=A+/A急(IsUrgent)；Partial3=A+/A急 或 B顺；None/未知=不匹配
    /// </summary>
    private static bool MatchesScheduleType(string? scheduleType, bool isUrgent, bool isNormal, bool attentionMatchesThisCR, string? urgencyLevel)
    {
        return scheduleType switch
        {
            "All" or "Subsequent" => true,
            "CrOnly" => isUrgent && isNormal && attentionMatchesThisCR,
            "Urgent" or "Partial1" => isUrgent && isNormal,
            "Partial2" => isUrgent,
            "Partial3" => isUrgent || urgencyLevel == UrgencyLevelKeys.BOrder,
            _ => false,
        };
    }

    /// <summary>
    /// 获取轧坯规格：冷轧工序组的前一个工序组的制造规格
    /// </summary>
    private static string GetBilletSpec(
        List<ProcessGroup> pgList, ProcessGroup targetPg, string? sourceSpec)
    {
        var prevPg = pgList
            .Where(pg => pg.SequenceNumber < targetPg.SequenceNumber)
            .OrderByDescending(pg => pg.SequenceNumber)
            .FirstOrDefault();
        return prevPg?.ManufacturingSpec ?? sourceSpec ?? "";
    }

    /// <summary>
    /// 从规格中提取外径部分，生成简化显示
    /// </summary>
    private static string GetShortDisplay(string billetSpec, string rollingSpec)
    {
        var outer1 = billetSpec.Split('*', '×').FirstOrDefault()?.Trim() ?? "";
        var outer2 = rollingSpec.Split('*', '×').FirstOrDefault()?.Trim() ?? "";
        return string.IsNullOrEmpty(outer1) || string.IsNullOrEmpty(outer2) ? "" : $"{outer1}-{outer2}";
    }

    /// <summary>
    /// 待轧紧急批次三层拆分累加（三档分类器）：
    /// 特急 = 正常流转 ∧ 关注==当前冷轧（关注工序==当前冷轧行 ProcessType）
    /// 特急- = 正常流转 ∧ 关注≠当前冷轧（含非冷轧关注/无关注）
    /// 急 = 非正常流转
    /// </summary>
    private static void AccumulateWaitUrgent(ColdRollPlanRowDto row, BatchAllocation item)
    {
        if (!item.IsUrgent) return;

        if (item.IsNormal && item.AttentionMatchesCurrentCR)
            row.WeightWaitNearUrgent += item.Weight;
        else if (item.IsNormal)
            row.WeightWaitNearBackUrgent += item.Weight;
        else
            row.WeightWaitNearOtherUrgent += item.Weight;
    }

    // ========== 排程建议私有方法 ==========

    /// <summary>档位放宽阶梯（窄 → 宽，机台需求单调不减）</summary>
    private static readonly string[] TierLadder = { "CrOnly", "Urgent", "Partial2", "Partial3", "All" };

    /// <summary>四维 key（ProcessType|BilletSpec|RollingSpec|IsFinished，OrdinalIgnoreCase）</summary>
    private static string KeyOf(BatchAllocation a)
        => $"{a.ProcessType}|{a.BilletSpec}|{a.RollingSpec}|{a.IsFinished}";

    private static string KeyOf(string processType, string billetSpec, string rollingSpec, bool isFinished)
        => $"{processType}|{billetSpec}|{rollingSpec}|{isFinished}";

    private static string ProcessTypeOf(string key)
        => key.Split('|')[0];

    /// <summary>存储档位规范化：Partial1→Urgent、Subsequent→All</summary>
    private static string NormalizeTier(string? tier) => tier switch
    {
        "Partial1" => "Urgent",
        "Subsequent" => "All",
        _ => tier ?? "None",
    };

    /// <summary>档位宽度（越宽值越大；None/-= -1 无档）</summary>
    private static int TierWidth(string? tier) => NormalizeTier(tier) switch
    {
        "CrOnly" => 0,
        "Urgent" => 1,
        "Partial2" => 2,
        "Partial3" => 3,
        "All" => 4,
        _ => -1,
    };

    /// <summary>档位显示名（存储值→中文）</summary>
    private static string TierDisplay(string? tier) => NormalizeTier(tier) switch
    {
        "CrOnly" => "急+",
        "Urgent" => "急+/急",
        "Partial2" => "急+/急/急-",
        "Partial3" => "急+/急/急-/顺",
        "All" => "全量",
        _ => "-",
    };

    /// <summary>档位往宽走一步（CrOnly→...→All；已是 All 返回 null）</summary>
    private static string? TierStepWide(string? tier)
    {
        int idx = Array.IndexOf(TierLadder, NormalizeTier(tier));
        return idx >= 0 && idx < TierLadder.Length - 1 ? TierLadder[idx + 1] : null;
    }

    /// <summary>档位往窄走一步（All→...→CrOnly；已是 CrOnly 返回 null）</summary>
    private static string? TierStepNarrow(string? tier)
    {
        int idx = Array.IndexOf(TierLadder, NormalizeTier(tier));
        return idx > 0 ? TierLadder[idx - 1] : null;
    }

    /// <summary>
    /// v2 产能平衡：默认「急+/急/急-」起步双向调整。
    /// 需求>上限 → 向窄收（急+/急 → 急+）；需求<下限 → 向宽放（加顺 → 全量）；区间内保持默认档。
    /// 收窄/放宽均无达标档 → 矛盾标注交人（A 全量不足 / A' 急+超上限 / 跨区间）。
    /// </summary>
    private static (string tier, string status, List<string> conflicts) ChooseTierBidirectional(
        string groupDisplay,
        List<BatchAllocation> groupAlloc,
        int minTarget,
        int maxMachines,
        IReadOnlyDictionary<string, ScheduleRow> scheduleDict,
        IReadOnlyDictionary<string, decimal?> capacityDict)
    {
        var conflicts = new List<string>();
        int cPartial2 = CountAtTier(groupAlloc, "Partial2", scheduleDict, capacityDict);

        if (cPartial2 > maxMachines)
        {
            // 过度 → 向窄收：Urgent → CrOnly
            foreach (var t in new[] { "Urgent", "CrOnly" })
            {
                int c = CountAtTier(groupAlloc, t, scheduleDict, capacityDict);
                if (c <= maxMachines)
                {
                    if (c >= minTarget) return (t, "OK", conflicts);
                    conflicts.Add($"冷轧{groupDisplay}：产能平衡无法在最大 {maxMachines} 台内兼顾最小需求 {minTarget} 台（收窄到「{TierDisplay(t)}」后仅 {c} 台），请人工调整产能档案或机台配置");
                    return ("-", "A", conflicts);
                }
            }
            int crOnlyCount = CountAtTier(groupAlloc, "CrOnly", scheduleDict, capacityDict);
            conflicts.Add($"冷轧{groupDisplay}：急+锁定已超最大机台数（{crOnlyCount} 台 > 最大 {maxMachines} 台），请人工决策加急/转外协");
            return ("-", "A'", conflicts);
        }

        if (cPartial2 < minTarget)
        {
            // 过少 → 向宽放：Partial3 → All
            foreach (var t in new[] { "Partial3", "All" })
            {
                int c = CountAtTier(groupAlloc, t, scheduleDict, capacityDict);
                if (c >= minTarget)
                {
                    if (c <= maxMachines) return (t, "OK", conflicts);
                    conflicts.Add($"冷轧{groupDisplay}：产能平衡无法在最大 {maxMachines} 台内满足最小需求 {minTarget} 台（放宽到「{TierDisplay(t)}」后 {c} 台超上限），请人工调整产能档案或机台配置");
                    return ("-", "A", conflicts);
                }
            }
            int allCount = CountAtTier(groupAlloc, "All", scheduleDict, capacityDict);
            conflicts.Add($"冷轧{groupDisplay}：全量排程仍不足机台需求（需求 {minTarget} 台，全量仅 {allCount} 台），请人工调整产能档案或机台配置");
            return ("-", "A", conflicts);
        }

        // 区间内 → 保持默认原始档
        return ("Partial2", "OK", conflicts);
    }

    /// <summary>
    /// 当前档位：组内有排程行的最宽档（在轧 CompletionType / 待轧 RollType 各自 Normalize 后取宽）；无则 "-"
    /// </summary>
    private static string ComputeCurrentTier(IEnumerable<string> groupKeys, IReadOnlyDictionary<string, ScheduleRow> scheduleDict)
    {
        string current = "-";
        foreach (var key in groupKeys)
        {
            if (!scheduleDict.TryGetValue(key, out var sched)) continue;
            var comp = NormalizeTier(sched.CompletionType);
            var roll = NormalizeTier(sched.RollType);
            if (TierWidth(comp) > TierWidth(current)) current = comp;
            if (TierWidth(roll) > TierWidth(current)) current = roll;
        }
        return current;
    }

    /// <summary>现状机台数：每行按自身现有档位（在轧 CompletionType/待轧 RollType）分侧匹配</summary>
    private static int CountAtCurrentTiers(
        List<BatchAllocation> groupAlloc,
        IReadOnlyDictionary<string, ScheduleRow> scheduleDict,
        IReadOnlyDictionary<string, decimal?> capacityDict)
    {
        var matched = groupAlloc.Where(a =>
        {
            if (a.PositionDiff > 6) return false;
            if (!scheduleDict.TryGetValue(KeyOf(a), out var sched)) return false;
            var type = a.PositionDiff == 0 ? sched.CompletionType : sched.RollType;
            return MatchesScheduleType(type, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel);
        }).ToList();
        return ComputeMachineCount(matched, scheduleDict, capacityDict);
    }

    /// <summary>统一档位下机台数：组内 PositionDiff≤6 且已排程按该档位匹配（在轧/待轧同档位，与 GetMachineEstimateCoreAsync 同公式）</summary>
    private static int CountAtTier(
        List<BatchAllocation> groupAlloc,
        string tier,
        IReadOnlyDictionary<string, ScheduleRow> scheduleDict,
        IReadOnlyDictionary<string, decimal?> capacityDict)
    {
        var matched = groupAlloc.Where(a =>
            a.PositionDiff <= 6
            && scheduleDict.ContainsKey(KeyOf(a))
            && MatchesScheduleType(tier, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel)).ToList();
        return ComputeMachineCount(matched, scheduleDict, capacityDict);
    }

    /// <summary>机台需求数：Σ(规格流转量 ÷ 单机单日量) ÷ 6天，四舍五入（AwayFromZero）；单机单日量为空/≤0 贡献 0</summary>
    private static int ComputeMachineCount(
        List<BatchAllocation> matched,
        IReadOnlyDictionary<string, ScheduleRow> scheduleDict,
        IReadOnlyDictionary<string, decimal?> capacityDict)
    {
        decimal machineDays = 0m;
        foreach (var a in matched)
        {
            var key = KeyOf(a);
            decimal? dailyOutput = null;
            if (capacityDict.TryGetValue(key, out var capOutput) && capOutput.HasValue && capOutput.Value > 0)
                dailyOutput = capOutput;
            else if (scheduleDict.TryGetValue(key, out var sched) && sched.DailyOutput.HasValue && sched.DailyOutput.Value > 0)
                dailyOutput = sched.DailyOutput;
            if (dailyOutput.HasValue)
                machineDays += a.Weight / (dailyOutput.Value * 6m);
        }
        return (int)Math.Round(machineDays, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 供需链流转保底（方式 B → 方式 A，2026-08-29 方案 A 按链对）：对每个需求组算「下次计划（7-12天）的机台承接需求」，
    /// 对每个供给组算「本组流向目标需求组的流入」。需求组承接两部分：
    /// ① 供给组在制/待轧（PositionDiff≤6，档位命中=有流转要求）向下游延伸本组规格折算的流入机台 FromSupplier；
    /// ② 本组在制/待轧（PositionDiff≤6）本次计划未定流转的料（当前档位不命中，如急-/顺 未被本次档位覆盖）留待下次承接的机台。
    /// 两者相加为 Total（下次总承接）；产能档案有单机单日量用方式 B，否则回退机台配置 EstimatedDailyOutput（方式 A），皆无则贡献 0。
    /// 同时返回各组成部分的料重（kg，与机台数同批次口径）。
    /// 多链/多级链：supplierOutflow[供给组] 仅计本组→本组 SupplyTarget 的流入（多级链中间节点既承接又再供给，互不重复）。
    /// </summary>
    private static (Dictionary<string, ChainFlow> demandInflow, Dictionary<string, ChainFlow> supplierOutflow) ComputeFlowDemand(
        List<BatchAllocation> allocations,
        IReadOnlyDictionary<string, ScheduleRow> scheduleDict,
        IReadOnlyDictionary<string, decimal?> capacityDict,
        IReadOnlyDictionary<string, ColdRollMachineConfig> machineConfigDict,
        List<MachineGroupDef> groups)
    {
        var targetGroupByKey = groups.ToDictionary(g => g.Key, StringComparer.OrdinalIgnoreCase);
        var supplierGroups = groups.Where(g => !string.IsNullOrEmpty(g.SupplyTargetGroupKey)).ToList();
        // 需求组集合 = 所有被供给组指向的组（含多级链中间节点；工序全局唯一归属一组，跨组不重叠）
        var demanderGroups = supplierGroups
            .Select(s => targetGroupByKey.GetValueOrDefault(s.SupplyTargetGroupKey!))
            .Where(g => g != null)
            .DistinctBy(g => g!.Key)
            .Select(g => g!)
            .ToList();

        // ===== 部分二：供给组本次安排流转（档位命中）→ 延伸其目标组折算流入机台（按链对累积） =====
        var chainMachine = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var chainWeight = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in allocations)
        {
            if (a.PositionDiff > 6) continue;

            // 只算有流转要求（排程档位命中）的料：本批供给组排程行档位须匹配（在轧 CompletionType/待轧 RollType）
            if (!scheduleDict.TryGetValue(KeyOf(a), out var sched)) continue;
            var schedType = a.PositionDiff == 0 ? sched.CompletionType : sched.RollType;
            if (!MatchesScheduleType(schedType, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel)) continue;

            var sourceKey = ProcessKeys.ToKey(a.ProcessType) ?? a.ProcessType;
            var srcGroup = supplierGroups.FirstOrDefault(g => g.ContainsKey(sourceKey));
            if (srcGroup == null) continue;

            var pgs = a.ProcessGroups.OrderBy(pg => pg.SequenceNumber).ToList();
            if (pgs.Count == 0) continue;
            int idx = -1;
            for (int i = 0; i < pgs.Count; i++)
            {
                var k = ProcessKeys.ToKey(pgs[i].ProcessName) ?? pgs[i].ProcessName;
                if (string.Equals(k, sourceKey, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
            }
            if (idx < 0) continue;

            ProcessGroupTrace? next = null;
            for (int i = idx + 1; i < pgs.Count; i++)
            {
                var k = ProcessKeys.ToKey(pgs[i].ProcessName) ?? pgs[i].ProcessName;
                if (ProcessKeys.IsColdRollOrColdDraw(k)) { next = pgs[i]; break; }
            }
            if (next == null) continue;

            var nextKey = ProcessKeys.ToKey(next.ProcessName) ?? next.ProcessName;
            var tgtGroup = targetGroupByKey.GetValueOrDefault(srcGroup.SupplyTargetGroupKey!);
            if (tgtGroup == null || !tgtGroup.ContainsKey(nextKey)) continue;

            // 目标组规格维度：ProcessType=下一冷轧组、BilletSpec=供给组轧坯(a.RollingSpec)、RollingSpec=next 制造规格、IsFinished=next 是否最后工序组
            var key = KeyOf(next.ProcessName, a.RollingSpec, next.ManufacturingSpec ?? "", next.IsFinished);

            decimal? daily = null;
            if (capacityDict.TryGetValue(key, out var capOutput) && capOutput.HasValue && capOutput.Value > 0)
                daily = capOutput;
            else if (machineConfigDict.TryGetValue(nextKey, out var cfg)
                && cfg.EstimatedDailyOutput.HasValue && cfg.EstimatedDailyOutput.Value > 0)
                daily = cfg.EstimatedDailyOutput;

            if (daily.HasValue)
            {
                var chainKey = $"{srcGroup.Key}|{tgtGroup.Key}";
                chainMachine[chainKey] = chainMachine.GetValueOrDefault(chainKey) + a.Weight / (daily.Value * 6m);
                chainWeight[chainKey] = chainWeight.GetValueOrDefault(chainKey) + a.Weight;
            }
        }

        // ===== 部分一：需求组本组本次未定流转（当前档位不命中）→ 留待下次承接 =====
        var selfMachine = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var selfWeight = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in allocations)
        {
            if (a.PositionDiff > 6) continue;
            // 仅「需求组本组批次」（批次当前所在工序组属于某被指向的需求组）：供给组批次的下游延伸已由部分二（供给流入）计入，不得重复
            if (!a.IsCurrentGroup) continue;
            var sourceKey = ProcessKeys.ToKey(a.ProcessType) ?? a.ProcessType;
            var dGroup = demanderGroups.FirstOrDefault(g => g.ContainsKey(sourceKey));
            if (dGroup == null) continue;

            // 当前档位命中（本次已安排流转）→ 不计；无排程行或档位不命中（急-/顺 等未被覆盖）→ 本次未定流转，计入下次承接
            bool matched = false;
            if (scheduleDict.TryGetValue(KeyOf(a), out var sched))
            {
                var schedType = a.PositionDiff == 0 ? sched.CompletionType : sched.RollType;
                matched = MatchesScheduleType(schedType, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel);
            }
            if (matched) continue;

            decimal? daily = null;
            if (capacityDict.TryGetValue(KeyOf(a), out var capOutput) && capOutput.HasValue && capOutput.Value > 0)
                daily = capOutput;
            else if (machineConfigDict.TryGetValue(sourceKey, out var cfg)
                && cfg.EstimatedDailyOutput.HasValue && cfg.EstimatedDailyOutput.Value > 0)
                daily = cfg.EstimatedDailyOutput;

            if (daily.HasValue)
            {
                selfMachine[dGroup.Key] = selfMachine.GetValueOrDefault(dGroup.Key) + a.Weight / (daily.Value * 6m);
                selfWeight[dGroup.Key] = selfWeight.GetValueOrDefault(dGroup.Key) + a.Weight;
            }
        }

        // 聚合：demandInflow（按需求组：Σ 所有指向它的供给流入 + 本组未定流转）
        var demandInflow = new Dictionary<string, ChainFlow>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in demanderGroups)
        {
            decimal fromM = 0m, fromW = 0m;
            foreach (var s in supplierGroups)
            {
                var t = s.SupplyTargetGroupKey!;
                if (!string.Equals(t, d.Key, StringComparison.OrdinalIgnoreCase)) continue;
                fromM += chainMachine.GetValueOrDefault($"{s.Key}|{t}");
                fromW += chainWeight.GetValueOrDefault($"{s.Key}|{t}");
            }
            decimal selfM = selfMachine.GetValueOrDefault(d.Key);
            decimal selfW = selfWeight.GetValueOrDefault(d.Key);
            demandInflow[d.Key] = new ChainFlow(
                FromSupplier: (int)Math.Round(fromM, MidpointRounding.AwayFromZero),
                Total: (int)Math.Round(fromM + selfM, MidpointRounding.AwayFromZero),
                FromSupplierWeight: fromW,
                TotalWeight: fromW + selfW);
        }

        // 聚合：supplierOutflow（按供给组：本组→目标组流入；Total=目标组总承接）
        var supplierOutflow = new Dictionary<string, ChainFlow>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in supplierGroups)
        {
            var t = s.SupplyTargetGroupKey!;
            decimal fromM = chainMachine.GetValueOrDefault($"{s.Key}|{t}");
            decimal fromW = chainWeight.GetValueOrDefault($"{s.Key}|{t}");
            var tgtInflow = demandInflow.GetValueOrDefault(t) ?? new ChainFlow(0, 0, 0, 0);
            supplierOutflow[s.Key] = new ChainFlow(
                FromSupplier: (int)Math.Round(fromM, MidpointRounding.AwayFromZero),
                Total: tgtInflow.Total,
                FromSupplierWeight: fromW,
                TotalWeight: tgtInflow.TotalWeight);
        }

        return (demandInflow, supplierOutflow);
    }

    /// <summary>供需链流动记录：FromSupplier=供给流入机台数，Total=总承接（含本组未定流转）；重量同构（kg）。</summary>
    private sealed record ChainFlow(int FromSupplier, int Total, decimal FromSupplierWeight, decimal TotalWeight);

    /// <summary>
    /// 供给方组 ②在制品供给机台（统一目标组基准）：在制品堆（IsFinished=false，调用处已过滤）按档位匹配后，
    /// 经 ProcessGroups 追踪下一目标组工序，用目标组规格产能档案 daily（方式 B → 方式 A）折算供给机台——
    /// 与 ComputeFlowDemand 部分二（本组流向目标组流入）同基准、同窗口(PositionDiff≤6)；
    /// 对倒判据用本组流向目标组流入：供给随档位放宽逐档计入——放宽至 All 时供给 = 流入 → 对倒自然停止；
    /// 无目标组延伸的批次贡献 0。
    /// </summary>
    private static int CountAtTierFlowTo(
        List<BatchAllocation> inProdAlloc,
        string tier,
        IReadOnlyDictionary<string, decimal?> capacityDict,
        IReadOnlyDictionary<string, ColdRollMachineConfig> machineConfigDict,
        MachineGroupDef supplierGroup,
        MachineGroupDef targetGroup)
    {
        decimal machineDays = 0m;
        foreach (var a in inProdAlloc)
        {
            if (a.PositionDiff > 6) continue;
            if (!MatchesScheduleType(tier, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel)) continue;

            var sourceKey = ProcessKeys.ToKey(a.ProcessType) ?? a.ProcessType;
            if (!supplierGroup.ContainsKey(sourceKey)) continue;

            var pgs = a.ProcessGroups.OrderBy(pg => pg.SequenceNumber).ToList();
            if (pgs.Count == 0) continue;
            int idx = -1;
            for (int i = 0; i < pgs.Count; i++)
            {
                var k = ProcessKeys.ToKey(pgs[i].ProcessName) ?? pgs[i].ProcessName;
                if (string.Equals(k, sourceKey, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
            }
            if (idx < 0) continue;

            ProcessGroupTrace? next = null;
            for (int i = idx + 1; i < pgs.Count; i++)
            {
                var k = ProcessKeys.ToKey(pgs[i].ProcessName) ?? pgs[i].ProcessName;
                if (ProcessKeys.IsColdRollOrColdDraw(k)) { next = pgs[i]; break; }
            }
            if (next == null) continue;

            var nextKey = ProcessKeys.ToKey(next.ProcessName) ?? next.ProcessName;
            if (!targetGroup.ContainsKey(nextKey)) continue;

            var key = KeyOf(next.ProcessName, a.RollingSpec, next.ManufacturingSpec ?? "", next.IsFinished);

            decimal? daily = null;
            if (capacityDict.TryGetValue(key, out var capOutput) && capOutput.HasValue && capOutput.Value > 0)
                daily = capOutput;
            else if (machineConfigDict.TryGetValue(nextKey, out var cfg)
                && cfg.EstimatedDailyOutput.HasValue && cfg.EstimatedDailyOutput.Value > 0)
                daily = cfg.EstimatedDailyOutput;

            if (daily.HasValue)
                machineDays += a.Weight / (daily.Value * 6m);
        }
        return (int)Math.Round(machineDays, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 四维行级建议（v2）：itemTier = 决策对象档位（三辊/冷拔=Partial2；2030=组档；5060=在制/成品档；null=矛盾仅特急锁定）。
    /// 自动分配不考虑人工已设档位；特急锁定（有急+批次 → 恒 ≥ CrOnly，天然满足）标记"锁定"；有批次无排程行 → 新增行。
    /// </summary>
    private static ColdRollScheduleSuggestionItemDto BuildSuggestionItem(
        string key,
        IReadOnlyDictionary<string, ScheduleRow> scheduleDict,
        List<BatchAllocation> allocations,
        string? itemTier)
    {
        var parts = key.Split('|');
        var processType = parts[0];
        var billetSpec = parts[1];
        var rollingSpec = parts[2];
        var isFinished = string.Equals(parts[3], "True", StringComparison.OrdinalIgnoreCase);

        var existing = scheduleDict.GetValueOrDefault(key);

        // 行内批次（四维匹配，OrdinalIgnoreCase）
        var rowAlloc = allocations.Where(a =>
            string.Equals(a.ProcessType, processType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.BilletSpec, billetSpec, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.RollingSpec, rollingSpec, StringComparison.OrdinalIgnoreCase)
            && a.IsFinished == isFinished).ToList();
        var inProd = rowAlloc.Where(a => a.PositionDiff == 0).ToList();
        var inWait = rowAlloc.Where(a => a.PositionDiff >= 1 && a.PositionDiff <= 6).ToList();
        bool hasUrgentPlus = rowAlloc.Any(a => a.IsUrgent && a.IsNormal && a.AttentionMatchesCurrentCR);

        var item = new ColdRollScheduleSuggestionItemDto
        {
            ProcessType = processType,
            BilletSpec = billetSpec,
            RollingSpec = rollingSpec,
            IsFinished = isFinished,
            ShortDisplay = GetShortDisplay(billetSpec, rollingSpec),
            MergeDisplay = existing?.MergeDisplay ?? $"{billetSpec}×{rollingSpec}-{(isFinished ? "成品" : "在制品")}",
            HasUrgentPlus = hasUrgentPlus,
            InProdExists = inProd.Count > 0,
            InWaitExists = inWait.Count > 0,
            DailyOutput = existing?.DailyOutput,
            MachineNo = existing?.MachineNo,
            Remark = existing?.Remark,
        };

        // v2 档位建议：itemTier = 决策对象档位（三辊/冷拔=Partial2；2030=组档；5060=在制/成品档；null=矛盾仅特急锁定）
        string suggestedCompletion;
        string suggestedRoll;
        string currentCompletion = existing?.CompletionType ?? "None";
        string currentRoll = existing?.RollType ?? "None";
        if (itemTier == null)
        {
            // 无组建议（矛盾/无批次维度）：仅特急锁定硬约束，其余保持现状
            if (hasUrgentPlus)
            {
                suggestedCompletion = NormalizeTier(currentCompletion) == "None" ? "CrOnly" : currentCompletion;
                suggestedRoll = NormalizeTier(currentRoll) == "None" ? "CrOnly" : currentRoll;
                item.RowStatus = "锁定";
            }
            else
            {
                suggestedCompletion = currentCompletion;
                suggestedRoll = currentRoll;
            }
        }
        else
        {
            // 决策对象档位直接作为建议（自动分配不考虑人工已设档位）；急+行天然满足 ≥ CrOnly，标记锁定
            suggestedCompletion = itemTier;
            suggestedRoll = itemTier;
            if (hasUrgentPlus) item.RowStatus = "锁定";
        }

        item.SuggestedCompletionType = suggestedCompletion;
        item.SuggestedRollType = suggestedRoll;

        // 新增行标注：无排程行但有批次 → 新增（特急锁定行保留「锁定」）
        if (existing == null && item.RowStatus != "锁定") item.RowStatus = "新增";

        // 计划在轧/待轧量 = 该侧批次中命中「建议档位」的重量（本次计划流转分侧展开，与建议引擎「计划流转量」同口径）
        item.PlannedInProdWeight = inProd
            .Where(a => MatchesScheduleType(suggestedCompletion, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel))
            .Sum(a => a.Weight);
        item.PlannedInWaitWeight = inWait
            .Where(a => MatchesScheduleType(suggestedRoll, a.IsUrgent, a.IsNormal, a.AttentionMatchesCurrentCR, a.UrgencyLevel))
            .Sum(a => a.Weight);

        // 实际流转档（一键采用写入排程设置的最终值）：锁定行强制两侧按建议填入（均非空）；
        // 其余按「对应侧计划量>0 才设档」——计划量=0 侧留空（该规格不在本次流转计划，不写入档位）
        if (item.RowStatus == "锁定")
        {
            item.ActualCompletionTier = suggestedCompletion;
            item.ActualRollTier = suggestedRoll;
        }
        else
        {
            item.ActualCompletionTier = item.PlannedInProdWeight > 0 ? suggestedCompletion : "";
            item.ActualRollTier = item.PlannedInWaitWeight > 0 ? suggestedRoll : "";
        }

        return item;
    }

    /// <summary>排程设置行快照（建议引擎用）</summary>
    private class ScheduleRow
    {
        public string? CompletionType { get; set; }
        public string? RollType { get; set; }
        public decimal? DailyOutput { get; set; }
        public string? MachineNo { get; set; }
        public string? MergeDisplay { get; set; }
        public string? Remark { get; set; }
    }

    /// <summary>
    /// 批次分配中间结构（分组前）
    /// </summary>
    private class BatchAllocation
    {
        public string WorkOrderNo { get; set; } = "";
        public string ProcessType { get; set; } = "";
        public string BilletSpec { get; set; } = "";
        public string RollingSpec { get; set; } = "";
        public bool IsFinished { get; set; }
        public bool IsUrgent { get; set; }          // (A+急/A急)
        public string? UrgencyLevel { get; set; }   // 批次紧急性（B顺判定用）
        public bool IsNormal { get; set; }          // ProductionFlowProperty==正常
        public bool AttentionMatchesCurrentCR { get; set; } // 关注工序==当前冷轧行 ProcessType
        public string ShortDisplay { get; set; } = ""; // 外径跨度
        public int PositionDiff { get; set; }
        /// <summary>该分配对应的工序组是否为批次当前所在工序组（区分「本组批次」与下游延伸组，flowDemand 部分一 2030 本组判定用）</summary>
        public bool IsCurrentGroup { get; set; }
        public decimal Weight { get; set; }
        /// <summary>在产设备的设备名（仅 PositionDiff==0 时有值）</summary>
        public string? MachineNo { get; set; }
        /// <summary>批次全工序组追踪（排程建议方式B流转折算用，仅建议引擎读取）</summary>
        public List<ProcessGroupTrace> ProcessGroups { get; set; } = new();
    }

    /// <summary>
    /// 工序组追踪快照（仅排程建议方式B流转折算用：5060 在制批次向下游延伸 2030 规格）
    /// </summary>
    private class ProcessGroupTrace
    {
        public string ProcessName { get; set; } = "";
        public string? ManufacturingSpec { get; set; }
        public int SequenceNumber { get; set; }
        public bool IsFinished { get; set; }
    }
}
