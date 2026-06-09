using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Scheduling;
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
            else if (sectionTab == "过程检验" || sectionTab == "成品检验")
            {
                // 检验类：工段=检验，再按工序值区分
                // 过程检验=所在工序值<本批次最大工序值，成品检验=所在工序值=本批次最大工序值
                if (sectionTab == "过程检验")
                {
                    joined = joined.Where(x =>
                        // Path 1: 当前工序未完工 → CurrentGroupName seq < batch max seq
                        (x.b.CurrentSectionCompleted == false && x.b.CurrentSectionName == "检验" &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) >
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.CurrentGroupName)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()) ||
                        // Path 2: 当前工序已完工/无数据 → NextProcess seq < batch max seq
                        (x.b.CurrentSectionCompleted != false && x.b.NextSectionName == "检验" && x.b.NextProcess != null &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) >
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.NextProcess)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()));
                }
                else // 成品检验
                {
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
            ScheduleStage = x.plan != null && x.plan.ScheduleStage != null ? x.plan.ScheduleStage.Value : (x.s != null ? x.s.ScheduleStage : 0),
            ProductionAttentionProcess = x.plan != null && x.plan.ProductionAttentionProcess != null ? x.plan.ProductionAttentionProcess : (x.s != null ? x.s.ProductionAttentionProcess : null),
            ProductionFlowProperty = x.plan != null && x.plan.ProductionFlowProperty != null ? x.plan.ProductionFlowProperty : (x.s != null ? x.s.ProductionFlowProperty : null),

            // G6（直接从 WorkOrderExecutionSummary 实体读取，无需额外 JOIN）
            IsUrging = x.s != null && x.s.IsUrging,
            IsBatchDelivery = x.s != null && x.s.IsBatchDelivery,
            IsPaused = x.s != null && x.s.IsPaused,
            AdjustmentRemark = x.s != null ? x.s.AdjustmentRemark : null,
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
            x.ProductionAttentionProcess,
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
              pProcess(x.CurrentSectionCompleted, x.CurrentGroupName, x.NextProcess) == x.ProductionAttentionProcess ||
              x.ProductionAttentionProcess is null or "收尾-成检"))
            ||
            // Tier 2：原料锁定 + 催单/分批交货 + 紧急 + pending条件
            (x.ScheduleStage == 1 &&
             (x.IsUrging || x.IsBatchDelivery) &&
             (x.UrgencyLevel == "A+急" || x.UrgencyLevel == "A急") &&
             (pProcess(x.CurrentSectionCompleted, x.CurrentGroupName, x.NextProcess) == "荒管处理" ||
              pProcess(x.CurrentSectionCompleted, x.CurrentGroupName, x.NextProcess) == x.ProductionAttentionProcess ||
              x.ProductionAttentionProcess is null or "收尾-成检"))).ToList();
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
                        item.CR_RollOrder = curSched.RollOrder;
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
                        item.CR_RollOrder = nextSched.RollOrder;
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
                        item.CR_RollOrder = nextNextSched.RollOrder;
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
                    ProductionAttentionProcess = s != null ? s.ProductionAttentionProcess : null,
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
            ["ScheduleStage"] = all.Where(x => x.ScheduleStage.HasValue).Select(x => x.ScheduleStage!.Value.ToString()).Distinct().OrderBy(x => x).ToList(),
            ["ProductionAttentionProcess"] = all.Where(x => x.ProductionAttentionProcess != null).Select(x => x.ProductionAttentionProcess!).Distinct().OrderBy(x => x).ToList(),
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
