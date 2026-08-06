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

    public async Task<List<ColdRollPlanRowDto>> GetPlanAsync(string? sectionFilter)
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
                // 工段筛选（中文 Tab 名归一为 Key 后与工序组 ProcessName(Key) 匹配）
                if (!string.IsNullOrEmpty(sectionFilter)
                    && crPg.ProcessName != (ProcessKeys.ToKey(sectionFilter) ?? sectionFilter))
                    continue;

                int targetGlobalSeq = crPg.GetSectionSequence(SectionKeys.ColdRollDraw) ?? 0;
                if (targetGlobalSeq <= 0) continue;

                // 批次已在此工序组且已完成或无当前工段 → 视为已过此冷轧
                if (currentPgSeq.HasValue && crPg.SequenceNumber == currentPgSeq.Value)
                {
                    // 无当前工段名（数据上无活动工段）→ 视为本组已完成
                    if (string.IsNullOrEmpty(batch.CurrentSectionName))
                        continue;
                    // 冷轧拔已完成
                    if (batch.CurrentSectionName == SectionKeys.ColdRollDraw
                        && batch.CurrentSectionCompleted == true)
                        continue;
                }

                int diff = targetGlobalSeq - currentGlobalSeq;
                if (diff < 0) continue; // 已过此冷轧，跳过

                // 判断是否正在此工序组做冷轧拔（近日在轧），不使用 diff==0
                bool isProducing = batch.Status == BatchStatus.InProgress
                    && !string.IsNullOrEmpty(batch.CurrentSectionName)
                    && batch.CurrentSectionName == SectionKeys.ColdRollDraw
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

                // IsKeyBatch（新逻辑，与批次计划 V5.10 同步）
                var plan = summary != null ? planDict.GetValueOrDefault(summary.WorkOrderId) : null;
                var urgency = plan?.UrgencyLevel ?? summary?.UrgencyLevel;
                var productionFlowProperty = plan?.ProductionFlowProperty ?? summary?.ProductionFlowProperty;
                var attentionProcess = plan?.ProductionAttentionProcess ?? summary?.MainNoAttentionProcess;

                bool isKeyBatch = false;
                bool isGeneralKeyBatch = false; // 总的特急（不区分冷轧/非冷轧）
                if (UrgencyLevelKeys.IsUrgent(urgency)
                    && productionFlowProperty == ProductionFlowKeys.Normal)
                {
                    if (crKeys.Contains(ProcessKeys.ToKey(attentionProcess) ?? attentionProcess ?? ""))
                    {
                        var attentionPg = sortedPgs.FirstOrDefault(pg => pg.ProcessName == attentionProcess);
                        var attentionSectionSeq = attentionPg?.GetSectionSequence(SectionKeys.ColdRollDraw);
                        if (attentionSectionSeq.HasValue)
                        {
                            isKeyBatch = currentGlobalSeq < attentionSectionSeq.Value + 1;
                            isGeneralKeyBatch = isKeyBatch;
                        }
                    }
                    else
                    {
                        // 非冷轧类(荒管处理/在制修检)：满足 Urgent+正常 即视为总的特急
                        isGeneralKeyBatch = true;
                    }
                }

                intermediate.Add(new BatchAllocation
                {
                    WorkOrderNo = batch.WorkOrderNo ?? "",
                    ProcessType = crPg.ProcessName,
                    BilletSpec = billetSpec,
                    RollingSpec = rollingSpec,
                    IsFinished = isFinished,
                    IsKeyBatch = isKeyBatch,
                    IsGeneralKeyBatch = isGeneralKeyBatch,
                    IsUrgent = UrgencyLevelKeys.IsUrgent(urgency),
                    IsAttentionColdRoll = crKeys.Contains(ProcessKeys.ToKey(attentionProcess) ?? attentionProcess ?? ""),
                    PositionDiff = positionDiff,
                    Weight = batch.CurrentValidWeight ?? 0m,
                    MachineNo = isProducing ? (batch.CurrentEquipmentName ?? batch.CurrentOutsource) : null,
                });
            }
        }

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
                        if (item.IsKeyBatch)
                            row.WeightProdUrgent += item.Weight;
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

                    if (item.IsKeyBatch)
                        row.KeyBatchCount++;
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
    /// 待轧紧急批次三层拆分累加：
    /// 特急管 = 总的特急 ∩ 冷轧类关注工序（IsGeneralKeyBatch + IsAttentionColdRoll）
    /// 后特急 = 总的特急 ∩ 非冷轧类关注工序（荒管处理/在制修检）
    /// 其它急管 = 紧急(A+急/A急) - 总的特急
    /// </summary>
    private static void AccumulateWaitUrgent(ColdRollPlanRowDto row, BatchAllocation item)
    {
        if (!item.IsUrgent) return;

        if (item.IsGeneralKeyBatch && item.IsAttentionColdRoll)
            row.WeightWaitNearUrgent += item.Weight;
        else if (item.IsGeneralKeyBatch && !item.IsAttentionColdRoll)
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
        public bool IsKeyBatch { get; set; }
        public bool IsGeneralKeyBatch { get; set; } // 总的特急（不区分冷轧/非冷轧）
        public bool IsUrgent { get; set; }          // (A+急/A急)
        public bool IsAttentionColdRoll { get; set; } // IsColdRollOrDraw(attentionProcess)
        public int PositionDiff { get; set; }
        public decimal Weight { get; set; }
        /// <summary>在产设备的设备名（仅 PositionDiff==0 时有值）</summary>
        public string? MachineNo { get; set; }
    }
}
