using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Scheduling;
using MES.Services.Extensions;

namespace MES.Services.Scheduling;

/// <summary>
/// 冷轧计划看板服务 — 按规格维度聚合生产批次的时间桶重量分布
/// </summary>
public class ColdRollPlanService : IColdRollPlanService
{
    private readonly AppDbContext _context;

    public ColdRollPlanService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ColdRollPlanRowDto>> GetPlanAsync(string? sectionFilter)
    {
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
                    Solution = pg.Solution,
                    Straighten = pg.Straighten,
                    Cut = pg.Cut,
                    ThicknessMeasure = pg.ThicknessMeasure,
                    Pickle = pg.Pickle,
                    OuterPolish = pg.OuterPolish,
                    InnerGrinding = pg.InnerGrinding,
                    OuterSpotGrinding = pg.OuterSpotGrinding,
                    Inspection = pg.Inspection,
                    WeldingHead = pg.WeldingHead,
                    Lubrication = pg.Lubrication,
                    Warehouse = pg.Warehouse,
                }).ToList()
            })
            .ToListAsync();

        // 2. LEFT JOIN WorkOrderExecutionSummary + WorkOrderSchedule（仅加载相关批次的内存字典）
        var workOrderNos = batchProjections
            .Where(b => !string.IsNullOrEmpty(b.WorkOrderNo))
            .Select(b => b.WorkOrderNo)
            .Distinct()
            .ToList();

        var summaryDict = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Where(s => workOrderNos.Contains(s.WorkOrderNo))
            .ToDictionaryAsync(s => s.WorkOrderNo);

        var scheduleDict = await _context.Set<WorkOrderSchedule>()
            .AsNoTracking()
            .Where(s => workOrderNos.Contains(s.WorkOrderNo))
            .ToDictionaryAsync(s => s.WorkOrderNo);

        // 3. 逐批处理
        var intermediate = new List<BatchAllocation>();

        foreach (var batch in batchProjections)
        {
            var summary = summaryDict.GetValueOrDefault(batch.WorkOrderNo);
            var schedule = scheduleDict.GetValueOrDefault(batch.WorkOrderNo);

            var sortedPgs = batch.ProcessGroups
                .OrderBy(pg => pg.SequenceNumber)
                .ToList();
            if (sortedPgs.Count == 0) continue;

            // 所有冷轧类工序组
            var coldRollPgs = sortedPgs.Where(pg => ProcessNames.IsColdRollOrDraw(pg.ProcessName)).ToList();
            if (coldRollPgs.Count == 0) continue;

            // 确定批次当前所处的工序组序号
            int? currentPgSeq = sortedPgs
                .Where(pg => pg.ProcessName == batch.CurrentGroupName)
                .Select(pg => (int?)pg.SequenceNumber)
                .FirstOrDefault();

            // 确定批次当前执行工段的"生产执行序号"（跨组统一编号）
            int currentGlobalSeq = 0;
            if (currentPgSeq.HasValue)
            {
                var currentPg = sortedPgs.FirstOrDefault(pg => pg.SequenceNumber == currentPgSeq.Value);
                if (currentPg != null)
                {
                    foreach (var (name, seq) in currentPg.GetNonEmptySections())
                    {
                        if (name == batch.CurrentSectionName)
                        {
                            currentGlobalSeq = seq;
                            break;
                        }
                    }
                }
            }

            // ===== 每个冷轧工序组都生成一行 =====
            foreach (var crPg in coldRollPgs)
            {
                // 工段筛选
                if (!string.IsNullOrEmpty(sectionFilter) && crPg.ProcessName != sectionFilter)
                    continue;

                int targetGlobalSeq = crPg.GetSectionSequence(SectionDefs.ColdRollDraw) ?? 0;
                if (targetGlobalSeq <= 0) continue;

                // 批次已在此工序组且已完成或无当前工段 → 视为已过此冷轧
                if (currentPgSeq.HasValue && crPg.SequenceNumber == currentPgSeq.Value)
                {
                    // 无当前工段名（数据上无活动工段）→ 视为本组已完成
                    if (string.IsNullOrEmpty(batch.CurrentSectionName))
                        continue;
                    // 冷轧拔已完成
                    if (batch.CurrentSectionName == SectionDefs.ColdRollDraw
                        && batch.CurrentSectionCompleted == true)
                        continue;
                }

                int diff = targetGlobalSeq - currentGlobalSeq;
                if (diff < 0) continue; // 已过此冷轧，跳过

                // 判断是否正在此工序组做冷轧拔（近日在轧），不使用 diff==0
                bool isProducing = batch.Status == BatchStatus.InProgress
                    && !string.IsNullOrEmpty(batch.CurrentSectionName)
                    && batch.CurrentSectionName == SectionDefs.ColdRollDraw
                    && batch.CurrentSectionCompleted == false
                    && currentPgSeq.HasValue
                    && crPg.SequenceNumber == currentPgSeq.Value;

                // 只有真正在此PG做冷轧拔才能占位0（近日在轧），
                // 否则即使 diff==0 也应归入待轧今日(positionDiff=1)
                int positionDiff = isProducing ? 0 : (diff == 0 ? 1 : diff);

                // 规格维度推导
                var rollingSpec = crPg.ManufacturingSpec ?? "";
                var billetSpec = GetBilletSpec(sortedPgs, crPg, batch.SourceSpecification);
                var isFinished = crPg.SequenceNumber == sortedPgs.Max(pg => pg.SequenceNumber);

                // IsKeyBatch
                var urgency = summary?.UrgencyLevel;
                int scheduleStage = schedule != null ? 2 : (summary?.ScheduleStage ?? 0);
                var attentionProcess = schedule?.ProductionAttentionProcess;
                var pendingProcess = batch.CurrentSectionCompleted == false ? batch.CurrentGroupName : batch.NextProcess;

                bool isKeyBatch = scheduleStage == 2 &&
                    (urgency == "A+急" || urgency == "A急") &&
                    (pendingProcess == "荒管处理" ||
                     pendingProcess == attentionProcess ||
                     attentionProcess == "收尾-成检");

                intermediate.Add(new BatchAllocation
                {
                    WorkOrderNo = batch.WorkOrderNo ?? "",
                    ProcessType = crPg.ProcessName,
                    BilletSpec = billetSpec,
                    RollingSpec = rollingSpec,
                    IsFinished = isFinished,
                    IsKeyBatch = isKeyBatch,
                    PositionDiff = positionDiff,
                    Weight = batch.CurrentValidWeight ?? 0m,
                });
            }
        }

        // 4. 聚合：按 (ProcessType, BilletSpec, RollingSpec, IsFinished) 分组
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
                        if (item.IsKeyBatch)
                            row.WeightProdUrgent += item.Weight;
                    }
                    else if (item.PositionDiff == 1)
                    {
                        row.WeightToday += item.Weight;
                        if (item.IsKeyBatch)
                            row.WeightWaitNearUrgent += item.Weight;
                    }
                    else if (item.PositionDiff == 2)
                    {
                        row.WeightTomorrow += item.Weight;
                        if (item.IsKeyBatch)
                            row.WeightWaitNearUrgent += item.Weight;
                    }
                    else if (item.PositionDiff == 3)
                    {
                        row.WeightDayAfter += item.Weight;
                        if (item.IsKeyBatch)
                            row.WeightWaitNearUrgent += item.Weight;
                    }
                    else if (item.PositionDiff == 4)
                    {
                        row.WeightExt3 += item.Weight;
                        if (item.IsKeyBatch)
                            row.WeightWaitNearUrgent += item.Weight;
                    }
                    else if (item.PositionDiff == 5)
                    {
                        row.WeightExt4 += item.Weight;
                        if (item.IsKeyBatch)
                            row.WeightWaitNearUrgent += item.Weight;
                    }
                    else if (item.PositionDiff == 6)
                    {
                        row.WeightExt5 += item.Weight;
                        if (item.IsKeyBatch)
                            row.WeightWaitNearUrgent += item.Weight;
                    }
                    else if (item.PositionDiff > 6)
                    {
                        row.WeightDistant += item.Weight;
                    }

                    if (item.IsKeyBatch)
                        row.KeyBatchCount++;
                }

                // 计算展示字段
                row.WeightWaitNear = row.WeightToday + row.WeightTomorrow + row.WeightDayAfter
                    + row.WeightExt3 + row.WeightExt4 + row.WeightExt5;

                // 总量 = 近日在轧 + 近日待轧 + 远日量
                row.WeightTotal = row.WeightProd + row.WeightWaitNear + row.WeightDistant;

                row.MergeDisplay = $"{row.BilletSpec}×{row.RollingSpec}-{(row.IsFinished ? "成品" : "中间品")}";
                row.ShortDisplay = GetShortDisplay(row.BilletSpec, row.RollingSpec);

                return row;
            })
            .OrderBy(r => r.ProcessType)
            .ThenBy(r => r.BilletSpec)
            .ThenBy(r => r.RollingSpec)
            .ToList();

        return result;
    }

    // ========== 私有方法 ==========

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
    /// 批次分配中间结构（分组前）
    /// </summary>
    private class BatchAllocation
    {
        public string WorkOrderNo { get; set; } = "";
        public string ProcessType { get; set; } = "";
        public string BilletSpec { get; set; } = "";
        public string RollingSpec { get; set; } = "";
        public bool IsFinished { get; set; }
        public bool IsKeyBatch { get; set; }
        public int PositionDiff { get; set; }
        public decimal Weight { get; set; }
    }
}
