using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Scheduling;
using MES.Services.Extensions;
using MES.Services.Helpers;

namespace MES.Services.Scheduling;

/// <summary>
/// 在产明细计划服务 — ProductionBatch LEFT JOIN WorkOrderExecutionSummary + WorkOrderPlan
/// </summary>
public class BatchPlanService : IBatchPlanService
{
    private readonly AppDbContext _context;

    public BatchPlanService(AppDbContext context)
    {
        _context = context;
    }

    // 冷轧类 Tab：工序在此列表中 → 需同时匹配工序名和"冷轧拔"工段
    private static readonly HashSet<string> _coldRollTabs = new()
    {
        "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔"
    };

    public async Task<PagedResult<BatchPlanDto>> GetPagedAsync(QueryParams query)
    {
        var batchQuery = _context.ProductionBatches.AsNoTracking()
            .Where(b => b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress);

        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var planQuery = _context.Set<WorkOrderPlan>().AsNoTracking();

        // ========== 提取并移除工段筛选（__SectionTab），在实体层应用特殊逻辑 ==========
        string? sectionTab = null;
        if (query.Filters != null)
        {
            var sf = query.Filters.FirstOrDefault(f => f.Field == "__SectionTab");
            if (sf != null)
            {
                sectionTab = sf.Value;
                query.Filters.Remove(sf);
            }
        }

        var joined = from b in batchQuery
                     join s in summaryQuery on b.WorkOrderNo equals s.WorkOrderNo into sj
                     from s in sj.DefaultIfEmpty()
                     join plan in planQuery on s.WorkOrderId equals plan.WorkOrderId into planj
                     from plan in planj.DefaultIfEmpty()
                     select new { b, s, plan };

        // 关键词搜索
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            joined = joined.Where(x =>
                x.b.BatchNo.Contains(kw) ||
                (x.b.TagNo != null && x.b.TagNo.Contains(kw)) ||
                x.b.PlantGrade.Contains(kw) ||
                x.b.WorkOrderNo.Contains(kw) ||
                (x.b.Salesman != null && x.b.Salesman.Contains(kw)) ||
                x.b.Specification.Contains(kw) ||
                (x.b.CurrentGroupName != null && x.b.CurrentGroupName.Contains(kw)) ||
                (x.b.CurrentSectionName != null && x.b.CurrentSectionName.Contains(kw)) ||
                (x.b.NextProcess != null && x.b.NextProcess.Contains(kw)) ||
                (x.b.NextSectionName != null && x.b.NextSectionName.Contains(kw)) ||
                (x.s.UrgencyLevel != null && x.s.UrgencyLevel.Contains(kw)));
        }

        // ========== 工段筛选（特殊逻辑） ==========
        if (!string.IsNullOrEmpty(sectionTab))
        {
            if (_coldRollTabs.Contains(sectionTab))
            {
                // 冷轧类：待在产执行工序=Tab名 AND 待在产执行工段=冷轧拔
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     x.b.CurrentGroupName != null && x.b.CurrentGroupName.Contains(sectionTab) &&
                     x.b.CurrentSectionName == "冷轧拔") ||
                    (x.b.CurrentSectionCompleted != false &&
                     x.b.NextProcess != null && x.b.NextProcess.Contains(sectionTab) &&
                     x.b.NextSectionName == "冷轧拔"));
            }
            else if (sectionTab == "过程检验" || sectionTab == "成品检验" || sectionTab == "荒管检" || sectionTab == "在制检")
            {
                if (sectionTab == "成品检验")
                {
                    // 成品检验：工段=检验，且是本批次最大工序值
                    joined = joined.Where(x =>
                        (x.b.CurrentSectionCompleted == false && x.b.CurrentSectionName == "检验" &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) ==
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.CurrentGroupName)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()) ||
                        (x.b.CurrentSectionCompleted != false && x.b.NextSectionName == "检验" && x.b.NextProcess != null &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) ==
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.NextProcess)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()));
                }
                else
                {
                    // 过程检验/荒管检/在制检：工段=检验，且非本批次最大工序值
                    joined = joined.Where(x =>
                        (x.b.CurrentSectionCompleted == false && x.b.CurrentSectionName == "检验" &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) >
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.CurrentGroupName)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()) ||
                        (x.b.CurrentSectionCompleted != false && x.b.NextSectionName == "检验" && x.b.NextProcess != null &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) >
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.NextProcess)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()));

                    // 荒管检/在制检：额外按工序名过滤
                    if (sectionTab == "荒管检")
                    {
                        joined = joined.Where(x =>
                            (x.b.CurrentSectionCompleted == false && x.b.CurrentGroupName == ProcessNames.RoughTubeProcessing) ||
                            (x.b.CurrentSectionCompleted != false && x.b.NextProcess == ProcessNames.RoughTubeProcessing));
                    }
                    else if (sectionTab == "在制检")
                    {
                        joined = joined.Where(x =>
                            (x.b.CurrentSectionCompleted == false && x.b.CurrentGroupName == ProcessNames.InProcessRepair) ||
                            (x.b.CurrentSectionCompleted != false && x.b.NextProcess == ProcessNames.InProcessRepair));
                    }
                }
            }
            else
            {
                // 其它：待在产执行工段=Tab名
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     x.b.CurrentSectionName != null && x.b.CurrentSectionName.Contains(sectionTab)) ||
                    (x.b.CurrentSectionCompleted != false &&
                     x.b.NextSectionName != null && x.b.NextSectionName.Contains(sectionTab)));
            }
        }

        // 投影到 DTO
        var q = joined.Select(x => new BatchPlanDto
        {
            // Internal
            BatchId = x.b.Id,

            // G1
            BatchNo = x.b.BatchNo,
            TagNo = x.b.TagNo,
            PlantGrade = x.b.PlantGrade,
            CurrentValidWeight = x.b.CurrentValidWeight,

            // G2
            WorkOrderNo = x.b.WorkOrderNo,
            Salesman = x.b.Salesman,
            DeliveryDate = x.b.DeliveryDate,
            DeliveryState = x.b.DeliveryState,
            Specification = x.b.Specification,
            LengthStatus = x.b.LengthStatus,
            MinLength = x.b.MinLength,
            MaxLength = x.b.MaxLength,

            // G3
            CurrentExecDate = x.b.CurrentExecDate,
            CurrentSectionCompleted = x.b.CurrentSectionCompleted,
            CurrentGroupName = x.b.CurrentGroupName,
            CurrentSectionName = x.b.CurrentSectionName,
            CurrentSpec = x.b.CurrentSpec,
            CurrentEquipmentName = x.b.CurrentEquipmentName,
            CurrentOutsource = x.b.CurrentOutsource,
            NextSectionName = x.b.NextSectionName,
            NextProcess = x.b.NextProcess,
            CorrespondingSpec = x.b.CorrespondingSpec,

            // G4（COALESCE：工单计划薄表优先，无覆盖则回退系统值）
            UrgencyLevel = x.plan != null && x.plan.UrgencyLevel != null ? x.plan.UrgencyLevel : (x.s != null ? x.s.UrgencyLevel : null),
            ScheduleStage = x.plan != null && x.plan.ScheduleStage != null ? x.plan.ScheduleStage.Value : (x.s != null ? x.s.ScheduleStage : (x.b.WorkOrderNo == "非工单" ? 4 : -1)),
            MainNoAttentionProcess = x.plan != null && x.plan.ProductionAttentionProcess != null ? x.plan.ProductionAttentionProcess : (x.s != null ? x.s.MainNoAttentionProcess : null),
            ProductionFlowProperty = x.plan != null && x.plan.ProductionFlowProperty != null ? x.plan.ProductionFlowProperty : (x.s != null ? x.s.ProductionFlowProperty : null),

            // G6（直接从 WorkOrderExecutionSummary 实体读取，无需额外 JOIN）
            IsUrging = x.s != null && x.s.IsUrging,
            IsBatchDelivery = x.s != null && x.s.IsBatchDelivery,
            IsPaused = x.s != null && x.s.IsPaused,
            AdjustmentRemark = x.s != null ? x.s.AdjustmentRemark : null,
            MaxBatchRemainingWorkDays = x.s != null ? x.s.MaxBatchRemainingWorkDays : null,
        });

        // 通用列筛选
        q = q.ApplyFilters(query.Filters);

        // 排序
        q = ApplySorting(q, query.SortBy, query.IsDescending);

        // ========== 计算 Tab 汇总（全量筛选后的聚合，分页前） ==========
        var aggQuery = q.Select(x => new
        {
            x.CurrentValidWeight,
            x.ScheduleStage,
            x.UrgencyLevel,
            x.MainNoAttentionProcess,
            x.ProductionFlowProperty,
            x.CurrentSectionCompleted,
            x.CurrentGroupName,
            x.NextProcess,
            x.IsUrging,
            x.IsBatchDelivery,
        });
        var aggData = await aggQuery.ToListAsync();

        var batchCount = aggData.Count;
        var totalWeight = aggData.Sum(x => x.CurrentValidWeight ?? 0m);

        var pProcess = (bool? completed, string? groupName, string? nextProcess) =>
            completed == false ? groupName : nextProcess;

        var keyBatches = aggData.Where(x =>
            // Tier 1：生产执行 + 紧急 + pending条件
            (x.ScheduleStage == 2 &&
             (x.UrgencyLevel == "A+急" || x.UrgencyLevel == "A急") &&
             (pProcess(x.CurrentSectionCompleted, x.CurrentGroupName, x.NextProcess) == "荒管处理" ||
              pProcess(x.CurrentSectionCompleted, x.CurrentGroupName, x.NextProcess) == x.MainNoAttentionProcess ||
              x.MainNoAttentionProcess is null or "收尾-成检"))
            ||
            // Tier 2：原料锁定 + 催单/分批交货 + 紧急 + pending条件
            (x.ScheduleStage == 1 &&
             (x.IsUrging || x.IsBatchDelivery) &&
             (x.UrgencyLevel == "A+急" || x.UrgencyLevel == "A急") &&
             (pProcess(x.CurrentSectionCompleted, x.CurrentGroupName, x.NextProcess) == "荒管处理" ||
              pProcess(x.CurrentSectionCompleted, x.CurrentGroupName, x.NextProcess) == x.MainNoAttentionProcess ||
              x.MainNoAttentionProcess is null or "收尾-成检"))).ToList();
        var keyBatchCount = keyBatches.Count;
        var keyBatchWeight = keyBatches.Sum(x => x.CurrentValidWeight ?? 0m);

        // 分页
        var totalCount = aggData.Count;
        var items = await q
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        // ========== 冷轧排程维度推导 + 小表匹配 ==========
        if (items.Count > 0)
        {
            var batchIds = items.Select(i => i.BatchId).Distinct().ToList();

            // 加载当前页批次的 ProcessGroups
            var allPgs = await _context.Set<ProcessGroup>()
                .AsNoTracking()
                .Where(pg => batchIds.Contains(pg.ProductionBatchId))
                .OrderBy(pg => pg.ProductionBatchId)
                .ThenBy(pg => pg.SequenceNumber)
                .ToListAsync();
            var pgLookup = allPgs.GroupBy(pg => pg.ProductionBatchId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 加载冷轧排程小表（全量，小表只有几百条）
            var scheduleAll = await _context.ColdRollSpecSchedules
                .AsNoTracking()
                .ToListAsync();
            var scheduleLookup = scheduleAll.ToDictionary(
                s => $"{s.ProcessType}|{s.BilletSpec}|{s.RollingSpec}|{s.IsFinished}",
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                if (!pgLookup.TryGetValue(item.BatchId, out var pgs) || pgs.Count == 0)
                    continue;

                var pendingProcess = item.CurrentSectionCompleted == false
                    ? item.CurrentGroupName
                    : item.NextProcess;

                var pendingPg = pgs.FirstOrDefault(pg => pg.ProcessName == pendingProcess);
                if (pendingPg == null) continue;

                var pendingIdx = pgs.IndexOf(pendingPg);
                var maxSeq = pgs.Max(pg => pg.SequenceNumber);

                // 本层 — 是否冷轧
                if (!string.IsNullOrEmpty(pendingProcess) && ProcessNames.IsColdRollOrDraw(pendingProcess))
                {
                    item.CurrentCR_ProcessType = pendingProcess;
                    item.CurrentCR_RollingSpec = pendingPg.ManufacturingSpec;
                    if (pendingIdx > 0)
                        item.CurrentCR_BilletSpec = pgs[pendingIdx - 1].ManufacturingSpec;
                    item.CurrentCR_IsFinished = pendingPg.SequenceNumber == maxSeq;

                    // 在轧要求：仅在批次实际在轧（在轧设备不为空）时匹配
                    if (!string.IsNullOrEmpty(item.PendingEquipment))
                    {
                        var curKey = $"{item.CurrentCR_ProcessType}|{item.CurrentCR_BilletSpec}|{item.CurrentCR_RollingSpec}|{item.CurrentCR_IsFinished}";
                        if (scheduleLookup.TryGetValue(curKey, out var curSched))
                            item.CR_CompletionType = curSched.CompletionType;
                    }
                }

                // 下层 — 是否冷轧
                if (pendingIdx + 1 < pgs.Count)
                {
                    var nextPg = pgs[pendingIdx + 1];
                    if (ProcessNames.IsColdRollOrDraw(nextPg.ProcessName))
                    {
                        item.NextCR_ProcessType = nextPg.ProcessName;
                        item.NextCR_RollingSpec = nextPg.ManufacturingSpec;
                        item.NextCR_BilletSpec = pendingPg.ManufacturingSpec;
                        item.NextCR_IsFinished = nextPg.SequenceNumber == maxSeq;
                    }
                }

                // 下下层 — 是否冷轧
                if (pendingIdx + 2 < pgs.Count)
                {
                    var nextNextPg = pgs[pendingIdx + 2];
                    if (ProcessNames.IsColdRollOrDraw(nextNextPg.ProcessName))
                    {
                        item.NextNextCR_ProcessType = nextNextPg.ProcessName;
                        item.NextNextCR_RollingSpec = nextNextPg.ManufacturingSpec;
                        item.NextNextCR_BilletSpec = pgs[pendingIdx + 1].ManufacturingSpec;
                        item.NextNextCR_IsFinished = nextNextPg.SequenceNumber == maxSeq;
                    }
                }

                // 待轧要求（场景1）：本层冷轧 + 在轧设备为空 → 匹配本层维度
                if (!string.IsNullOrEmpty(item.CurrentCR_ProcessType)
                    && string.IsNullOrEmpty(item.PendingEquipment))
                {
                    var curKey = $"{item.CurrentCR_ProcessType}|{item.CurrentCR_BilletSpec}|{item.CurrentCR_RollingSpec}|{item.CurrentCR_IsFinished}";
                    if (scheduleLookup.TryGetValue(curKey, out var curSched))
                    {
                        item.CR_RollType = curSched.RollType;
                        item.CR_SchedMachineNo = curSched.MachineNo;
                    }
                }
                // 待轧要求（场景2）：下层冷轧 + 在轧设备为空（未被场景1覆盖时）→ 匹配下层维度
                else if (!string.IsNullOrEmpty(item.NextCR_ProcessType)
                    && string.IsNullOrEmpty(item.PendingEquipment))
                {
                    var nextKey = $"{item.NextCR_ProcessType}|{item.NextCR_BilletSpec}|{item.NextCR_RollingSpec}|{item.NextCR_IsFinished}";
                    if (scheduleLookup.TryGetValue(nextKey, out var nextSched))
                    {
                        item.CR_RollType = nextSched.RollType;
                        item.CR_SchedMachineNo = nextSched.MachineNo;
                    }
                }
                // 待轧要求（场景3）：下下层冷轧 + 在轧设备为空（未被场景1/2覆盖时）→ 匹配下下层维度
                else if (!string.IsNullOrEmpty(item.NextNextCR_ProcessType)
                    && string.IsNullOrEmpty(item.PendingEquipment))
                {
                    var nextNextKey = $"{item.NextNextCR_ProcessType}|{item.NextNextCR_BilletSpec}|{item.NextNextCR_RollingSpec}|{item.NextNextCR_IsFinished}";
                    if (scheduleLookup.TryGetValue(nextNextKey, out var nextNextSched))
                    {
                        item.CR_RollType = nextNextSched.RollType;
                        item.CR_SchedMachineNo = nextNextSched.MachineNo;
                    }
                }
            }
        }

        return new PagedResult<BatchPlanDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize,
            Extras = new Dictionary<string, object>
            {
                ["batchCount"] = batchCount,
                ["totalWeight"] = totalWeight,
                ["keyBatchCount"] = keyBatchCount,
                ["keyBatchWeight"] = keyBatchWeight,
            }
        };
    }

    public async Task<List<BatchPlanDto>> GetAllAsync(string? sectionTab)
    {
        var batchQuery = _context.ProductionBatches.AsNoTracking()
            .Where(b => b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress);

        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var planQuery = _context.Set<WorkOrderPlan>().AsNoTracking();
        var batchPlanQuery = _context.Set<BatchPlanSchedule>().AsNoTracking();

        var joined = from b in batchQuery
                     join s in summaryQuery on b.WorkOrderNo equals s.WorkOrderNo into sj
                     from s in sj.DefaultIfEmpty()
                     join plan in planQuery on s.WorkOrderId equals plan.WorkOrderId into planj
                     from plan in planj.DefaultIfEmpty()
                     join bp in batchPlanQuery on b.Id equals bp.BatchId into bpj
                     from bp in bpj.DefaultIfEmpty()
                     select new { b, s, plan, bp };

        // ========== 工段筛选 ==========
        if (!string.IsNullOrEmpty(sectionTab))
        {
            if (_coldRollTabs.Contains(sectionTab))
            {
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     x.b.CurrentGroupName != null && x.b.CurrentGroupName.Contains(sectionTab) &&
                     x.b.CurrentSectionName == "冷轧拔") ||
                    (x.b.CurrentSectionCompleted != false &&
                     x.b.NextProcess != null && x.b.NextProcess.Contains(sectionTab) &&
                     x.b.NextSectionName == "冷轧拔"));
            }
            else if (sectionTab == "过程检验" || sectionTab == "成品检验" || sectionTab == "荒管检" || sectionTab == "在制检")
            {
                if (sectionTab == "成品检验")
                {
                    // 成品检验：工段=检验，且是本批次最大工序值
                    joined = joined.Where(x =>
                        (x.b.CurrentSectionCompleted == false && x.b.CurrentSectionName == "检验" &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) ==
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.CurrentGroupName)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()) ||
                        (x.b.CurrentSectionCompleted != false && x.b.NextSectionName == "检验" && x.b.NextProcess != null &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) ==
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.NextProcess)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()));
                }
                else
                {
                    // 过程检验/荒管检/在制检：工段=检验，且非本批次最大工序值
                    joined = joined.Where(x =>
                        (x.b.CurrentSectionCompleted == false && x.b.CurrentSectionName == "检验" &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) >
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.CurrentGroupName)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()) ||
                        (x.b.CurrentSectionCompleted != false && x.b.NextSectionName == "检验" && x.b.NextProcess != null &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) >
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.NextProcess)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()));

                    // 荒管检/在制检：额外按工序名过滤
                    if (sectionTab == "荒管检")
                    {
                        joined = joined.Where(x =>
                            (x.b.CurrentSectionCompleted == false && x.b.CurrentGroupName == ProcessNames.RoughTubeProcessing) ||
                            (x.b.CurrentSectionCompleted != false && x.b.NextProcess == ProcessNames.RoughTubeProcessing));
                    }
                    else if (sectionTab == "在制检")
                    {
                        joined = joined.Where(x =>
                            (x.b.CurrentSectionCompleted == false && x.b.CurrentGroupName == ProcessNames.InProcessRepair) ||
                            (x.b.CurrentSectionCompleted != false && x.b.NextProcess == ProcessNames.InProcessRepair));
                    }
                }
            }
            else
            {
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     x.b.CurrentSectionName != null && x.b.CurrentSectionName.Contains(sectionTab)) ||
                    (x.b.CurrentSectionCompleted != false &&
                     x.b.NextSectionName != null && x.b.NextSectionName.Contains(sectionTab)));
            }
        }

        // 投影到 DTO
        var q = joined.Select(x => new BatchPlanDto
        {
            BatchId = x.b.Id,
            BatchNo = x.b.BatchNo,
            TagNo = x.b.TagNo,
            PlantGrade = x.b.PlantGrade,
            CurrentValidWeight = x.b.CurrentValidWeight,
            WorkOrderNo = x.b.WorkOrderNo,
            Salesman = x.b.Salesman,
            DeliveryDate = x.b.DeliveryDate,
            DeliveryState = x.b.DeliveryState,
            Specification = x.b.Specification,
            LengthStatus = x.b.LengthStatus,
            MinLength = x.b.MinLength,
            MaxLength = x.b.MaxLength,
            CurrentExecDate = x.b.CurrentExecDate,
            CurrentSectionCompleted = x.b.CurrentSectionCompleted,
            CurrentGroupName = x.b.CurrentGroupName,
            CurrentSectionName = x.b.CurrentSectionName,
            CurrentSpec = x.b.CurrentSpec,
            CurrentEquipmentName = x.b.CurrentEquipmentName,
            CurrentOutsource = x.b.CurrentOutsource,
            NextSectionName = x.b.NextSectionName,
            NextProcess = x.b.NextProcess,
            CorrespondingSpec = x.b.CorrespondingSpec,
            UrgencyLevel = x.plan != null && x.plan.UrgencyLevel != null ? x.plan.UrgencyLevel : (x.s != null ? x.s.UrgencyLevel : null),
            ScheduleStage = x.plan != null && x.plan.ScheduleStage != null ? x.plan.ScheduleStage.Value : (x.s != null ? x.s.ScheduleStage : (x.b.WorkOrderNo == "非工单" ? 4 : -1)),
            MainNoAttentionProcess = x.plan != null && x.plan.ProductionAttentionProcess != null ? x.plan.ProductionAttentionProcess : (x.s != null ? x.s.MainNoAttentionProcess : null),
            ProductionFlowProperty = x.plan != null && x.plan.ProductionFlowProperty != null ? x.plan.ProductionFlowProperty : (x.s != null ? x.s.ProductionFlowProperty : null),
            IsUrging = x.s != null && x.s.IsUrging,
            IsBatchDelivery = x.s != null && x.s.IsBatchDelivery,
            IsPaused = x.s != null && x.s.IsPaused,
            AdjustmentRemark = x.s != null ? x.s.AdjustmentRemark : null,
            MaxBatchRemainingWorkDays = x.s != null ? x.s.MaxBatchRemainingWorkDays : null,

            // 批次计划薄表
            PlanIsFlow = x.bp != null && x.bp.IsFlow,
            PlanFlowLevel = x.bp != null ? x.bp.FlowLevel : 5,
            PlanFlowTarget = x.bp != null ? x.bp.FlowTarget : null,
            PlanFlowCRType = x.bp != null ? x.bp.FlowCRType : null,
            PlanFlowExecSpec = x.bp != null ? x.bp.FlowExecSpec : null,
            PlanExecutionSequence = x.bp != null ? x.bp.ExecutionSequence : null,
            PlanTargetSequence = x.bp != null ? x.bp.TargetSequence : null,
            IsGrabOrder = x.bp != null && x.bp.IsGrabOrder,
            PlanRemark = x.bp != null ? x.bp.PlanRemark : null,
        });

        var items = await q.ToListAsync();

        // ========== 冷轧排程维度推导 + 小表匹配 ==========
        if (items.Count > 0)
        {
            var batchIds = items.Select(i => i.BatchId).Distinct().ToList();
            var allPgs = await _context.Set<ProcessGroup>()
                .AsNoTracking()
                .Where(pg => batchIds.Contains(pg.ProductionBatchId))
                .OrderBy(pg => pg.ProductionBatchId)
                .ThenBy(pg => pg.SequenceNumber)
                .ToListAsync();
            var pgLookup = allPgs.GroupBy(pg => pg.ProductionBatchId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var scheduleAll = await _context.ColdRollSpecSchedules
                .AsNoTracking()
                .ToListAsync();
            var scheduleLookup = scheduleAll.ToDictionary(
                s => $"{s.ProcessType}|{s.BilletSpec}|{s.RollingSpec}|{s.IsFinished}",
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                if (!pgLookup.TryGetValue(item.BatchId, out var pgs) || pgs.Count == 0)
                    continue;

                var pendingProcess = item.CurrentSectionCompleted == false
                    ? item.CurrentGroupName
                    : item.NextProcess;
                var pendingSectionName = item.CurrentSectionCompleted == false
                    ? item.CurrentSectionName
                    : item.NextSectionName;

                var pendingPg = pgs.FirstOrDefault(pg => pg.ProcessName == pendingProcess);
                if (pendingPg == null) continue;

                var pendingIdx = pgs.IndexOf(pendingPg);
                var maxSeq = pgs.Max(pg => pg.SequenceNumber);

                // 冷轧排程（本层）：仅当执行工段=冷轧拔时才填充
                if (!string.IsNullOrEmpty(pendingProcess) && ProcessNames.IsColdRollOrDraw(pendingProcess)
                    && pendingSectionName == SectionDefs.ColdRollDraw)
                {
                    item.CurrentCR_ProcessType = pendingProcess;
                    item.CurrentCR_RollingSpec = pendingPg.ManufacturingSpec;
                    if (pendingIdx > 0)
                        item.CurrentCR_BilletSpec = pgs[pendingIdx - 1].ManufacturingSpec;
                    item.CurrentCR_IsFinished = pendingPg.SequenceNumber == maxSeq;

                    if (!string.IsNullOrEmpty(item.PendingEquipment))
                    {
                        var curKey = $"{item.CurrentCR_ProcessType}|{item.CurrentCR_BilletSpec}|{item.CurrentCR_RollingSpec}|{item.CurrentCR_IsFinished}";
                        if (scheduleLookup.TryGetValue(curKey, out var curSched))
                            item.CR_CompletionType = curSched.CompletionType;
                    }
                }

                if (pendingIdx + 1 < pgs.Count)
                {
                    var nextPg = pgs[pendingIdx + 1];
                    if (ProcessNames.IsColdRollOrDraw(nextPg.ProcessName))
                    {
                        item.NextCR_ProcessType = nextPg.ProcessName;
                        item.NextCR_RollingSpec = nextPg.ManufacturingSpec;
                        item.NextCR_BilletSpec = pendingPg.ManufacturingSpec;
                        item.NextCR_IsFinished = nextPg.SequenceNumber == maxSeq;
                    }
                }

                if (pendingIdx + 2 < pgs.Count)
                {
                    var nextNextPg = pgs[pendingIdx + 2];
                    if (ProcessNames.IsColdRollOrDraw(nextNextPg.ProcessName))
                    {
                        item.NextNextCR_ProcessType = nextNextPg.ProcessName;
                        item.NextNextCR_RollingSpec = nextNextPg.ManufacturingSpec;
                        item.NextNextCR_BilletSpec = pgs[pendingIdx + 1].ManufacturingSpec;
                        item.NextNextCR_IsFinished = nextNextPg.SequenceNumber == maxSeq;
                    }
                }

                if (!string.IsNullOrEmpty(item.CurrentCR_ProcessType)
                    && string.IsNullOrEmpty(item.PendingEquipment))
                {
                    var curKey = $"{item.CurrentCR_ProcessType}|{item.CurrentCR_BilletSpec}|{item.CurrentCR_RollingSpec}|{item.CurrentCR_IsFinished}";
                    if (scheduleLookup.TryGetValue(curKey, out var curSched))
                    {
                        item.CR_RollType = curSched.RollType;
                        item.CR_SchedMachineNo = curSched.MachineNo;
                    }
                }
                else if (!string.IsNullOrEmpty(item.NextCR_ProcessType)
                    && string.IsNullOrEmpty(item.PendingEquipment))
                {
                    var nextKey = $"{item.NextCR_ProcessType}|{item.NextCR_BilletSpec}|{item.NextCR_RollingSpec}|{item.NextCR_IsFinished}";
                    if (scheduleLookup.TryGetValue(nextKey, out var nextSched))
                    {
                        item.CR_RollType = nextSched.RollType;
                        item.CR_SchedMachineNo = nextSched.MachineNo;
                    }
                }
                else if (!string.IsNullOrEmpty(item.NextNextCR_ProcessType)
                    && string.IsNullOrEmpty(item.PendingEquipment))
                {
                    var nextNextKey = $"{item.NextNextCR_ProcessType}|{item.NextNextCR_BilletSpec}|{item.NextNextCR_RollingSpec}|{item.NextNextCR_IsFinished}";
                    if (scheduleLookup.TryGetValue(nextNextKey, out var nextNextSched))
                    {
                        item.CR_RollType = nextNextSched.RollType;
                        item.CR_SchedMachineNo = nextNextSched.MachineNo;
                    }
                }

                // ====== 执行序（取工序组中对应工段的字段值） ======
                item.ExecutionSequence = pendingPg.GetSectionSequence(item.PendingSectionName);

                // ====== 目标序（取工序组中对应工段的字段值） ======
                item.TargetSequence = ComputeTargetSequence(pgs, item.FlowTarget, item.FlowCRType);
            }
        }

        return items;
    }

    /// <summary>
    /// 获取冷轧排程流转汇总 — 基于批次计划薄表 PlanIsFlow（含手工调整），按(FlowCRType, 外径跨度)聚合
    /// </summary>
    public async Task<List<ColdRollScheduleSummaryDto>> GetFlowSummaryAsync(string? sectionTab, int? maxDiff = null)
    {
        // 1. 加载全量批次计划数据（复用 GetAllAsync 的完整逻辑，含 IsFlow 判定）
        var allItems = await GetAllAsync(sectionTab);

        // 2. 按原工量差筛选（PlanTargetSequence - PlanExecutionSequence）
        if (maxDiff.HasValue)
        {
            allItems = allItems.Where(x =>
                !x.OriginalDiff.HasValue ||
                x.OriginalDiff.Value <= maxDiff.Value).ToList();
        }

        // 3. 仅取流转批次
        var flowItems = allItems.Where(x => x.PlanIsFlow).ToList();
        if (flowItems.Count == 0) return new List<ColdRollScheduleSummaryDto>();

        // 3. 为每个流转批次确定(PlanFlowCRType, 外径跨度)用于聚合
        var raw = new List<(string FlowCRType, string ShortDisplay, decimal Weight, string FlowTarget)>();

        foreach (var item in flowItems)
        {
            if (string.IsNullOrEmpty(item.PlanFlowCRType)) continue;

            // 根据批次计划组中的流转目标和冷轧类型，匹配实时冷轧层维度推导→外径跨度
            string? billetSpec = null;
            string? rollingSpec = null;

            if (item.PlanFlowTarget == "完工冷轧")
            {
                // CompletionType 流转：使用当前冷轧层维度
                billetSpec = item.CurrentCR_BilletSpec;
                rollingSpec = item.CurrentCR_RollingSpec;
            }
            else if (item.PlanFlowTarget == "冷轧")
            {
                // RollType 流转：匹配 PlanFlowCRType 找到对应的冷轧层
                if (item.PlanFlowCRType == item.CurrentCR_ProcessType)
                { billetSpec = item.CurrentCR_BilletSpec; rollingSpec = item.CurrentCR_RollingSpec; }
                else if (item.PlanFlowCRType == item.NextCR_ProcessType)
                { billetSpec = item.NextCR_BilletSpec; rollingSpec = item.NextCR_RollingSpec; }
                else if (item.PlanFlowCRType == item.NextNextCR_ProcessType)
                { billetSpec = item.NextNextCR_BilletSpec; rollingSpec = item.NextNextCR_RollingSpec; }
            }

            if (string.IsNullOrEmpty(billetSpec) || string.IsNullOrEmpty(rollingSpec)) continue;

            // 从规格提取外径跨度
            var outer1 = billetSpec.Split('*', '×').FirstOrDefault()?.Trim() ?? "";
            var outer2 = rollingSpec.Split('*', '×').FirstOrDefault()?.Trim() ?? "";
            var shortDisplay = string.IsNullOrEmpty(outer1) || string.IsNullOrEmpty(outer2)
                ? ""
                : $"{outer1}-{outer2}";

            raw.Add((item.PlanFlowCRType, shortDisplay, item.CurrentValidWeight ?? 0m, item.PlanFlowTarget ?? ""));
        }

        // 4. 按 (PlanFlowCRType, ShortDisplay) 聚合，区分流转目标
        var result = raw
            .GroupBy(r => new { r.FlowCRType, r.ShortDisplay })
            .Select(g => new ColdRollScheduleSummaryDto
            {
                ShortDisplay = g.Key.ShortDisplay,
                ProcessType = g.Key.FlowCRType,
                TotalBatchCount = g.Count(),
                TotalWeight = g.Sum(x => x.Weight),
                FlowBatchCount = g.Count(),
                FlowWeight = g.Sum(x => x.Weight),
                CompletionWeight = g.Where(x => x.FlowTarget == "完工冷轧").Sum(x => x.Weight),
                RollWeight = g.Where(x => x.FlowTarget == "冷轧").Sum(x => x.Weight),
            })
            .OrderBy(r => r.ProcessType)
            .ThenBy(r => r.ShortDisplay)
            .ToList();

        return result;
    }

    public static int? ComputeTargetSequence(List<ProcessGroup> pgs, string? flowTarget, string? flowCRType)
    {
        if (string.IsNullOrEmpty(flowTarget) || pgs.Count == 0)
            return null;

        return flowTarget switch
        {
            // 成检：取工段"检验"的最大工序内序号
            "成检" => pgs.Where(pg => pg.Inspection.HasValue)
                        .Select(pg => (int?)pg.Inspection)
                        .Max(),

            // 完工冷轧：匹配冷轧类型+工段"冷轧拔"，字段值+1
            "完工冷轧" => pgs.FirstOrDefault(pg =>
                              pg.ProcessName == flowCRType && pg.ColdRollDraw.HasValue)
                          ?.ColdRollDraw + 1,

            // 冷轧：匹配冷轧类型+工段"冷轧拔"的字段值
            "冷轧" => pgs.FirstOrDefault(pg =>
                          pg.ProcessName == flowCRType && pg.ColdRollDraw.HasValue)
                      ?.ColdRollDraw,

            _ => null,
        };
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var batchQuery = _context.ProductionBatches.AsNoTracking()
            .Where(b => b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress);

        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();

        var q = from b in batchQuery
                join s in summaryQuery on b.WorkOrderNo equals s.WorkOrderNo into sj
                from s in sj.DefaultIfEmpty()
                select new
                {
                    b.BatchNo,
                    b.TagNo,
                    b.PlantGrade,
                    b.WorkOrderNo,
                    b.Salesman,
                    b.DeliveryState,
                    b.Specification,
                    b.LengthStatus,
                    b.CurrentGroupName,
                    b.CurrentSectionName,
                    b.NextProcess,
                    b.NextSectionName,
                    UrgencyLevel = s != null ? s.UrgencyLevel : null,
                    ScheduleStage = s != null ? s.ScheduleStage : (int?)null,
                    MainNoAttentionProcess = s != null ? s.MainNoAttentionProcess : null,
                    ProductionFlowProperty = s != null ? s.ProductionFlowProperty : null,
                };

        var all = await q.ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["BatchNo"] = all.Select(x => x.BatchNo).Distinct().OrderBy(x => x).ToList(),
            ["TagNo"] = all.Where(x => x.TagNo != null).Select(x => x.TagNo!).Distinct().OrderBy(x => x).ToList(),
            ["PlantGrade"] = all.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
            ["WorkOrderNo"] = all.Select(x => x.WorkOrderNo).Distinct().OrderBy(x => x).ToList(),
            ["Salesman"] = all.Where(x => x.Salesman != null).Select(x => x.Salesman!).Distinct().OrderBy(x => x).ToList(),
            ["DeliveryState"] = all.Where(x => x.DeliveryState != null).Select(x => x.DeliveryState!).Distinct().OrderBy(x => x).ToList(),
            ["Specification"] = all.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
            ["LengthStatus"] = all.Where(x => x.LengthStatus != null).Select(x => x.LengthStatus!).Distinct().OrderBy(x => x).ToList(),
            ["CurrentGroupName"] = all.Where(x => x.CurrentGroupName != null).Select(x => x.CurrentGroupName!).Distinct().OrderBy(x => x).ToList(),
            ["CurrentSectionName"] = all.Where(x => x.CurrentSectionName != null).Select(x => x.CurrentSectionName!).Distinct().OrderBy(x => x).ToList(),
            ["NextProcess"] = all.Where(x => x.NextProcess != null).Select(x => x.NextProcess!).Distinct().OrderBy(x => x).ToList(),
            ["NextSectionName"] = all.Where(x => x.NextSectionName != null).Select(x => x.NextSectionName!).Distinct().OrderBy(x => x).ToList(),
            ["UrgencyLevel"] = all.Where(x => x.UrgencyLevel != null).Select(x => x.UrgencyLevel!).Distinct().OrderBy(x => x).ToList(),
            ["ScheduleStage"] = all.Select(x => x.WorkOrderNo == "非工单" ? "4" : (x.ScheduleStage.HasValue ? x.ScheduleStage!.Value.ToString() : null))
                .Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["MainNoAttentionProcess"] = all.Where(x => x.MainNoAttentionProcess != null).Select(x => x.MainNoAttentionProcess!).Distinct().OrderBy(x => x).ToList(),
            ["ProductionFlowProperty"] = all.Where(x => x.ProductionFlowProperty != null).Select(x => x.ProductionFlowProperty!).Distinct().OrderBy(x => x).ToList(),
        };
    }

    private static IQueryable<BatchPlanDto> ApplySorting(
        IQueryable<BatchPlanDto> query, string? sortBy, bool isDescending)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? query.OrderByDescending(x => x.BatchNo)
            : query.ApplySort(sortBy, isDescending);
    }
}
