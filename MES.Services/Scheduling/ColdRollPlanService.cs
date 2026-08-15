using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
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

    public ColdRollPlanService(AppDbContext context, IProcessDefinitionService processDefService)
    {
        _context = context;
        _processDefService = processDefService;
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
                    MachineNo = isProducing ? (batch.CurrentEquipmentName ?? batch.CurrentOutsource) : null,
                    ShortDisplay = GetShortDisplay(billetSpec, rollingSpec),
                });
            }
        }

        return intermediate;
    }

    public async Task<List<ColdRollPlanRowDto>> GetPlanAsync(string? sectionFilter)
    {
        var intermediate = await BuildAllocationsAsync(sectionFilter);

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
        public decimal Weight { get; set; }
        /// <summary>在产设备的设备名（仅 PositionDiff==0 时有值）</summary>
        public string? MachineNo { get; set; }
    }
}
