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
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Scheduling;
using MES.Services.Extensions;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Scheduling;

/// <summary>
/// 在产明细计划服务 — ProductionBatch LEFT JOIN WorkOrderExecutionSummary + WorkOrderPlan
///
/// CROSS-MODULE: reads WorkOrder.WorkOrderExecutionSummary + WorkOrderPlan via direct DbContext
/// (read-only queries, no business rules bypassed). See docs/04_开发规范.md §9.5.
/// </summary>
public class BatchPlanService : IBatchPlanService
{
    private readonly AppDbContext _context;
    private readonly IProcessDefinitionService _processDefService;

    public BatchPlanService(AppDbContext context, IProcessDefinitionService processDefService)
    {
        _context = context;
        _processDefService = processDefService;
    }

    // 冷轧类 Tab：工序 Key 在此列表中 → 需同时匹配工序名和SectionKeys.ColdRollDraw工段
    private static readonly HashSet<string> _coldRollTabs = new()
    {
        ProcessKeys.ColdRoll60, ProcessKeys.ColdRoll50, ProcessKeys.ColdRoll30, ProcessKeys.ColdRoll20,
        ProcessKeys.ThreeRollColdRoll, ProcessKeys.ColdDraw
    };

    /// <summary>调度工段 Tab 归一：中文 Tab 名 → 稳定 Key（工序优先，工段次之）；检验类/内抛+内修磨特殊 Tab 名保持中文。</summary>
    private static string? NormalizeSectionTab(string? sectionTab)
    {
        if (string.IsNullOrEmpty(sectionTab)) return sectionTab;
        return ProcessKeys.ToKey(sectionTab) ?? SectionKeys.ToKey(sectionTab) ?? sectionTab;
    }

    public async Task<PagedResult<BatchPlanDto>> GetPagedAsync(QueryParams query)
    {
        // 预加载冷轧/冷拔类 Key 集合（配置表驱动，替代硬编码 IsColdRollOrDraw）
        var crKeys = await _processDefService.GetColdRollOrDrawKeysAsync();

        var batchQuery = _context.ProductionBatches.AsNoTracking()
            .Where(b => b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress);

        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var planQuery = _context.Set<WorkOrderPlan>().AsNoTracking();
        var workOrderQuery = _context.Set<MES.Data.Entities.WorkOrder.WorkOrder>().AsNoTracking();

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
        // 中文 Tab 名归一为 Key（工序/工段），检验类特殊 Tab 名保持中文
        sectionTab = NormalizeSectionTab(sectionTab);

        var joined = from b in batchQuery
                     join wo in workOrderQuery on b.WorkOrderNo equals wo.WorkOrderNo into woj
                     from wo in woj.DefaultIfEmpty()
                     join s in summaryQuery on b.WorkOrderNo equals s.WorkOrderNo into sj
                     from s in sj.DefaultIfEmpty()
                     join plan in planQuery on s.WorkOrderId equals plan.WorkOrderId into planj
                     from plan in planj.DefaultIfEmpty()
                     select new { b, wo, s, plan };

        // 主号暂停（IsPaused）批次排除，不参与批次计划
        joined = joined.Where(x => x.s == null || !x.s.IsPaused);

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
                (x.b.ProductionType != null && x.b.ProductionType.Contains(kw)) ||
                (x.b.ManufacturingItem != null && x.b.ManufacturingItem.Contains(kw)) ||
                (x.b.ManufacturingStatus != null && x.b.ManufacturingStatus.Contains(kw)) ||
                (x.wo != null && x.wo.SalesOrderNo != null && x.wo.SalesOrderNo.Contains(kw)) ||
                (x.wo != null && x.wo.ProductionMainNo != null && x.wo.ProductionMainNo.Contains(kw)) ||
                (x.wo != null && x.wo.EndCustomer != null && x.wo.EndCustomer.Contains(kw)) ||
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
                     x.b.CurrentSectionName == SectionKeys.ColdRollDraw) ||
                    (x.b.CurrentSectionCompleted != false &&
                     x.b.NextProcess != null && x.b.NextProcess.Contains(sectionTab) &&
                     x.b.NextSectionName == SectionKeys.ColdRollDraw));
            }
            else if (sectionTab == "荒管检" || sectionTab == "在制检")
            {
                // 检验类：工段=检验（不区分最大/非最大工序）。产类（荒管/在制）需批次全部工序组内存计算，
                // SQL 不可翻译 → 此处只粗过滤"待在产执行工段=检验"缩小候选范围，ToList 后按产类精过滤。
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false && x.b.CurrentSectionName == SectionKeys.Inspection) ||
                    (x.b.CurrentSectionCompleted != false && x.b.NextSectionName == SectionKeys.Inspection && x.b.NextProcess != null));
            }
            else if (sectionTab == "内抛+内修磨")
            {
                // 内抛+内修磨：两工段任一命中（当前/下一工段皆可）
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     (x.b.CurrentSectionName == SectionKeys.InnerPolish || x.b.CurrentSectionName == SectionKeys.InnerGrinding)) ||
                    (x.b.CurrentSectionCompleted != false &&
                     (x.b.NextSectionName == SectionKeys.InnerPolish || x.b.NextSectionName == SectionKeys.InnerGrinding)));
            }
            else
            {
                // 其它：待在产执行工段=Tab名（精确匹配英文 Key / 兼容存量中文，避免 "断切"→"Cut" 子串误匹配 "OilPipeCut"）
                var tabKey = sectionTab;
                var tabCn = SectionKeys.ToChinese(sectionTab);
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     x.b.CurrentSectionName != null &&
                     (x.b.CurrentSectionName == tabKey || x.b.CurrentSectionName == tabCn)) ||
                    (x.b.CurrentSectionCompleted != false &&
                     x.b.NextSectionName != null &&
                     (x.b.NextSectionName == tabKey || x.b.NextSectionName == tabCn)));
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
            SalesOrderNo = x.wo != null ? x.wo.SalesOrderNo : null,
            ProductionMainNo = x.wo != null ? x.wo.ProductionMainNo : null,
            EndCustomer = x.wo != null ? x.wo.EndCustomer : null,
            Salesman = x.b.Salesman,
            DeliveryDate = x.b.DeliveryDate,
            DeliveryState = string.IsNullOrEmpty(x.b.DeliveryState) ? null : Enum.Parse<DeliveryState>(x.b.DeliveryState),
            Specification = x.b.Specification,
            ManufacturingItem = x.b.ManufacturingItem,
            ProductionType = x.b.ProductionType,
            ManufacturingStatus = x.b.ManufacturingStatus,
            LengthStatus = string.IsNullOrEmpty(x.b.LengthStatus) ? null : Enum.Parse<LengthStatus>(x.b.LengthStatus),
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
            // G4（COALESCE：工单计划薄表优先，无覆盖则回退系统值；summary 关注状态 5 档映射到排程 4 档：0/1→0 完成、2→1 原料锁定、3→2 生产执行、4→3 成品检验）
            ScheduleStage = x.plan != null && x.plan.ScheduleStage != null
                ? x.plan.ScheduleStage.Value
                : (x.s != null
                    ? (x.s.ScheduleStage == 0 || x.s.ScheduleStage == 1 ? 0
                        : x.s.ScheduleStage == 2 ? 1
                        : x.s.ScheduleStage == 3 ? 2
                        : x.s.ScheduleStage == 4 ? 3
                        : x.s.ScheduleStage)
                    : (x.b.WorkOrderNo == WorkOrderNoSentinel.NotWorkOrder ? 4 : -1)),
            MainNoAttentionProcess = x.plan != null && x.plan.ProductionAttentionProcess != null ? x.plan.ProductionAttentionProcess : (x.s != null ? x.s.MainNoAttentionProcess : null),
            ProductionFlowProperty = x.plan != null && x.plan.ProductionFlowProperty != null ? x.plan.ProductionFlowProperty : (x.s != null ? x.s.ProductionFlowProperty : (x.b.WorkOrderNo == WorkOrderNoSentinel.NotWorkOrder ? ProductionFlowKeys.Skip : ProductionFlowKeys.Doubt)),

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
            x.BatchId,
            x.CurrentValidWeight,
            x.ScheduleStage,
            x.UrgencyLevel,
            x.MainNoAttentionProcess,
            x.ProductionFlowProperty,
            x.CurrentSectionCompleted,
            x.CurrentGroupName,
            x.CurrentSectionName,
            x.NextProcess,
            x.NextSectionName,
            x.ManufacturingItem,
            x.Specification,
            x.IsUrging,
            x.IsBatchDelivery,
        });
        var aggData = await aggQuery.ToListAsync();

        // 检验类（荒管检/在制检）：按产类内存过滤（产类需批次全部工序组计算，SQL 不可翻译）。
        // SQL 层已按"工段=检验"粗过滤，此处对全量候选加载工序组计算产类，保证分页/汇总口径准确。
        HashSet<int>? productStatusFilteredIds = null;
        if (sectionTab == "荒管检" || sectionTab == "在制检")
        {
            var expectedStatus = sectionTab == "荒管检" ? ProductStatuses.RoughTube : ProductStatuses.InProgress;
            var candidateIds = aggData.Select(x => x.BatchId).Distinct().ToList();
            var candidatePgs = await _context.Set<ProcessGroup>()
                .AsNoTracking()
                .Where(pg => candidateIds.Contains(pg.ProductionBatchId))
                .ToListAsync();
            var candidateLookup = candidatePgs.GroupBy(pg => pg.ProductionBatchId)
                .ToDictionary(g => g.Key, g => g.ToList());
            productStatusFilteredIds = new HashSet<int>();
            foreach (var x in aggData)
            {
                var pendingProcess = x.CurrentSectionCompleted == false ? x.CurrentGroupName : x.NextProcess;
                if (candidateLookup.TryGetValue(x.BatchId, out var pgs) && pgs.Count > 0
                    && ComputePendingProductStatus(pendingProcess, x.ManufacturingItem, pgs, x.Specification) == expectedStatus)
                {
                    productStatusFilteredIds.Add(x.BatchId);
                }
            }
            aggData = aggData.Where(x => productStatusFilteredIds.Contains(x.BatchId)).ToList();
        }

        var batchCount = aggData.Count;
        var totalWeight = aggData.Sum(x => x.CurrentValidWeight ?? 0m);

        var keyBatches = aggData.Where(x =>
            (x.UrgencyLevel == UrgencyLevelKeys.APlusUrgent || x.UrgencyLevel == UrgencyLevelKeys.AUrgent) &&
            x.ProductionFlowProperty == ProductionFlowKeys.Normal &&
            !string.IsNullOrEmpty(x.MainNoAttentionProcess)
        ).ToList();
        var keyBatchCount = keyBatches.Count;
        var keyBatchWeight = keyBatches.Sum(x => x.CurrentValidWeight ?? 0m);

        // 分页（荒管检/在制检：产类内存过滤后仅取匹配批次）
        var totalCount = aggData.Count;
        var items = await q
            .Where(x => productStatusFilteredIds == null || productStatusFilteredIds.Contains(x.BatchId))
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
                // ====== 重点生产批次判定（未产批次也纳入；GetPagedAsync 与 GetAllAsync 共用 ComputeIsKeyBatch，防漂移） ======
                // 执行序：始终取当前工段在当前工序组中的序号（无工序组/未产批次保持 null，ComputeIsKeyBatch 内视为 0）
                pgLookup.TryGetValue(item.BatchId, out var pgs);
                if (pgs != null)
                {
                    var currentPgForSeq = pgs.FirstOrDefault(pg => pg.ProcessName == item.CurrentGroupName);
                    item.ExecutionSequence = currentPgForSeq?.GetSectionSequence(item.CurrentSectionName);
                }

                // 相应工段序：根据主号关注工序从 ProcessGroups 推导（生产收尾分支见共享方法，未产/无当前工序组批次同样计算）
                if (pgs != null && pgs.Count > 0)
                {
                    item.AttentionProcessSectionSequence = ComputeAttentionProcessSectionSequence(pgs, item.MainNoAttentionProcess, crKeys);
                }
                item.IsKeyBatch = ComputeIsKeyBatch(item, crKeys);

                // ====== 冷轧排程维度推导 + 小表匹配（依赖当前工序组，未产/无工序组批次不参与） ======
                if (pgs == null || pgs.Count == 0)
                    continue;

                // 冷轧排程维度推导 + 小表匹配 + 关注==当前冷轧 + 目标序（共享方法，与薄表 PlanAllAsync 口径一致）
                ComputeColdRollDimensions(item, pgs, scheduleLookup, crKeys);
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
        // 中文 Tab 名归一为 Key（工序/工段），检验类特殊 Tab 名保持中文
        sectionTab = NormalizeSectionTab(sectionTab);

        var crKeys = await _processDefService.GetColdRollOrDrawKeysAsync();
        var batchQuery = _context.ProductionBatches.AsNoTracking()
            .Where(b => b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress);

        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var planQuery = _context.Set<WorkOrderPlan>().AsNoTracking();
        var batchPlanQuery = _context.Set<BatchPlanSchedule>().AsNoTracking();
        var workOrderQuery = _context.Set<MES.Data.Entities.WorkOrder.WorkOrder>().AsNoTracking();

        var joined = from b in batchQuery
                     join wo in workOrderQuery on b.WorkOrderNo equals wo.WorkOrderNo into woj
                     from wo in woj.DefaultIfEmpty()
                     join s in summaryQuery on b.WorkOrderNo equals s.WorkOrderNo into sj
                     from s in sj.DefaultIfEmpty()
                     join plan in planQuery on s.WorkOrderId equals plan.WorkOrderId into planj
                     from plan in planj.DefaultIfEmpty()
                     join bp in batchPlanQuery on b.Id equals bp.BatchId into bpj
                     from bp in bpj.DefaultIfEmpty()
                     select new { b, wo, s, plan, bp };

        // 主号暂停（IsPaused）批次排除，不参与批次计划
        joined = joined.Where(x => x.s == null || !x.s.IsPaused);

        // ========== 工段筛选 ==========
        if (!string.IsNullOrEmpty(sectionTab))
        {
            if (_coldRollTabs.Contains(sectionTab))
            {
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     x.b.CurrentGroupName != null && x.b.CurrentGroupName.Contains(sectionTab) &&
                     x.b.CurrentSectionName == SectionKeys.ColdRollDraw) ||
                    (x.b.CurrentSectionCompleted != false &&
                     x.b.NextProcess != null && x.b.NextProcess.Contains(sectionTab) &&
                     x.b.NextSectionName == SectionKeys.ColdRollDraw));
            }
            else if (sectionTab == "荒管检" || sectionTab == "在制检")
            {
                // 检验类：工段=检验（不区分最大/非最大工序）。产类（荒管/在制）需批次全部工序组内存计算，
                // SQL 不可翻译 → 此处只粗过滤"待在产执行工段=检验"缩小候选范围，ToList 后按产类精过滤。
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false && x.b.CurrentSectionName == SectionKeys.Inspection) ||
                    (x.b.CurrentSectionCompleted != false && x.b.NextSectionName == SectionKeys.Inspection && x.b.NextProcess != null));
            }
            else if (sectionTab == "内抛+内修磨")
            {
                // 内抛+内修磨：两工段任一命中（当前/下一工段皆可）
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     (x.b.CurrentSectionName == SectionKeys.InnerPolish || x.b.CurrentSectionName == SectionKeys.InnerGrinding)) ||
                    (x.b.CurrentSectionCompleted != false &&
                     (x.b.NextSectionName == SectionKeys.InnerPolish || x.b.NextSectionName == SectionKeys.InnerGrinding)));
            }
            else
            {
                // 其它：待在产执行工段=Tab名（精确匹配英文 Key / 兼容存量中文，避免 "断切"→"Cut" 子串误匹配 "OilPipeCut"）
                var tabKey = sectionTab;
                var tabCn = SectionKeys.ToChinese(sectionTab);
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     x.b.CurrentSectionName != null &&
                     (x.b.CurrentSectionName == tabKey || x.b.CurrentSectionName == tabCn)) ||
                    (x.b.CurrentSectionCompleted != false &&
                     x.b.NextSectionName != null &&
                     (x.b.NextSectionName == tabKey || x.b.NextSectionName == tabCn)));
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
            SalesOrderNo = x.wo != null ? x.wo.SalesOrderNo : null,
            ProductionMainNo = x.wo != null ? x.wo.ProductionMainNo : null,
            EndCustomer = x.wo != null ? x.wo.EndCustomer : null,
            Salesman = x.b.Salesman,
            DeliveryDate = x.b.DeliveryDate,
            DeliveryState = string.IsNullOrEmpty(x.b.DeliveryState) ? null : Enum.Parse<DeliveryState>(x.b.DeliveryState),
            Specification = x.b.Specification,
            ManufacturingItem = x.b.ManufacturingItem,
            ProductionType = x.b.ProductionType,
            ManufacturingStatus = x.b.ManufacturingStatus,
            LengthStatus = string.IsNullOrEmpty(x.b.LengthStatus) ? null : Enum.Parse<LengthStatus>(x.b.LengthStatus),
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
            // G4（COALESCE：工单计划薄表优先，无覆盖则回退系统值；summary 关注状态 5 档映射到排程 4 档：0/1→0 完成、2→1 原料锁定、3→2 生产执行、4→3 成品检验）
            ScheduleStage = x.plan != null && x.plan.ScheduleStage != null
                ? x.plan.ScheduleStage.Value
                : (x.s != null
                    ? (x.s.ScheduleStage == 0 || x.s.ScheduleStage == 1 ? 0
                        : x.s.ScheduleStage == 2 ? 1
                        : x.s.ScheduleStage == 3 ? 2
                        : x.s.ScheduleStage == 4 ? 3
                        : x.s.ScheduleStage)
                    : (x.b.WorkOrderNo == WorkOrderNoSentinel.NotWorkOrder ? 4 : -1)),
            MainNoAttentionProcess = x.plan != null && x.plan.ProductionAttentionProcess != null ? x.plan.ProductionAttentionProcess : (x.s != null ? x.s.MainNoAttentionProcess : null),
            ProductionFlowProperty = x.plan != null && x.plan.ProductionFlowProperty != null ? x.plan.ProductionFlowProperty : (x.s != null ? x.s.ProductionFlowProperty : (x.b.WorkOrderNo == WorkOrderNoSentinel.NotWorkOrder ? ProductionFlowKeys.Skip : ProductionFlowKeys.Doubt)),
            IsUrging = x.s != null && x.s.IsUrging,
            IsBatchDelivery = x.s != null && x.s.IsBatchDelivery,
            IsPaused = x.s != null && x.s.IsPaused,
            AdjustmentRemark = x.s != null ? x.s.AdjustmentRemark : null,

            // 批次计划薄表（读时覆盖：暂停=是 → 流转字段强制非流转，DB 保留原值，切回"否"自动恢复）
            PlanIsPaused = x.bp != null && x.bp.IsPaused,
            PlanIsFlow = x.bp != null && x.bp.IsFlow && !x.bp.IsPaused,
            PlanFlowLevel = x.bp != null ? (x.bp.IsPaused ? 5 : x.bp.FlowLevel) : 5,
            PlanFlowTarget = x.bp != null ? (x.bp.IsPaused ? null : x.bp.FlowTarget) : null,
            PlanFlowCRType = x.bp != null ? (x.bp.IsPaused ? null : x.bp.FlowCRType) : null,
            PlanOuterDiameterSpan = x.bp != null ? (x.bp.IsPaused ? null : x.bp.PlanOuterDiameterSpan) : null,
            PlanFlowExecSpec = x.bp != null ? (x.bp.IsPaused ? null : x.bp.FlowExecSpec) : null,
            PlanExecutionSequence = x.bp != null ? (x.bp.IsPaused ? null : x.bp.ExecutionSequence) : null,
            PlanTargetSequence = x.bp != null ? (x.bp.IsPaused ? null : x.bp.TargetSequence) : null,
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

            // 检验类（荒管检/在制检）：按产类内存过滤（产类需批次全部工序组计算，SQL 层已按"工段=检验"粗过滤）
            if (sectionTab == "荒管检" || sectionTab == "在制检")
            {
                var expectedStatus = sectionTab == "荒管检" ? ProductStatuses.RoughTube : ProductStatuses.InProgress;
                items = items.Where(i =>
                {
                    if (!pgLookup.TryGetValue(i.BatchId, out var pgs) || pgs.Count == 0)
                        return false;
                    return ComputePendingProductStatus(i.PendingProcess, i.ManufacturingItem, pgs, i.Specification) == expectedStatus;
                }).ToList();
            }

            var scheduleAll = await _context.ColdRollSpecSchedules
                .AsNoTracking()
                .ToListAsync();
            var scheduleLookup = scheduleAll.ToDictionary(
                s => $"{s.ProcessType}|{s.BilletSpec}|{s.RollingSpec}|{s.IsFinished}",
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                // ====== 重点生产批次判定（未产批次也纳入；GetPagedAsync 与 GetAllAsync 共用 ComputeIsKeyBatch，防漂移） ======
                // 执行序：始终取当前工段在当前工序组中的序号（无工序组/未产批次保持 null，ComputeIsKeyBatch 内视为 0）
                pgLookup.TryGetValue(item.BatchId, out var pgs);
                if (pgs != null)
                {
                    var currentPgForSeq = pgs.FirstOrDefault(pg => pg.ProcessName == item.CurrentGroupName);
                    item.ExecutionSequence = currentPgForSeq?.GetSectionSequence(item.CurrentSectionName);
                }

                // 相应工段序：根据主号关注工序从 ProcessGroups 推导（生产收尾分支见共享方法，未产/无当前工序组批次同样计算）
                if (pgs != null && pgs.Count > 0)
                {
                    item.AttentionProcessSectionSequence = ComputeAttentionProcessSectionSequence(pgs, item.MainNoAttentionProcess, crKeys);
                }
                item.IsKeyBatch = ComputeIsKeyBatch(item, crKeys);

                // ====== 冷轧排程维度推导 + 小表匹配（依赖当前工序组，未产/无工序组批次不参与） ======
                if (pgs == null || pgs.Count == 0)
                    continue;

                // 冷轧排程维度推导 + 小表匹配 + 关注==当前冷轧 + 目标序（共享方法，与 G11/薄表 PlanAllAsync 口径一致）
                ComputeColdRollDimensions(item, pgs, scheduleLookup, crKeys);
            }
        }

        return items;
    }

    /// <summary>
    /// 跨工段汇总（实时查询）：一次全量加载，按工段 Tab 逐工段归桶统计（内存过滤，口径与 GetAllAsync(sectionTab) 完全一致），末尾追加"合计"行。
    /// </summary>
    public async Task<List<BatchPlanSummaryRowDto>> GetSummaryAsync()
    {
        var allItems = await GetAllAsync(null);
        if (allItems.Count == 0)
        {
            return new List<BatchPlanSummaryRowDto> { BuildSummaryRow("合计", allItems) };
        }

        // 检验类 Tab 需按产类（荒管/在制/成品）判定（与 GetAllAsync 检验类分支一致），加载 ProcessGroups
        var batchIds = allItems.Select(i => i.BatchId).Distinct().ToList();
        var pgLookup = (await _context.Set<ProcessGroup>()
                .AsNoTracking()
                .Where(pg => batchIds.Contains(pg.ProductionBatchId))
                .ToListAsync())
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<BatchPlanSummaryRowDto>();
        foreach (var tab in BatchPlanSectionTabs.All)
        {
            var filtered = allItems.Where(x => SectionTabMatches(x, tab, pgLookup)).ToList();
            rows.Add(BuildSummaryRow(tab, filtered));
        }
        rows.Add(BuildSummaryRow("合计", allItems));
        return rows;
    }

    /// <summary>
    /// 工段 Tab 内存过滤谓词（复刻 GetAllAsync 的 IQueryable 过滤逻辑，保证汇总口径与列表工段筛选完全一致）：
    /// 冷轧类（工序 Key 命中）→ 工序名包含 + 当前/下一工段为冷拔；检验类（荒管检/在制检）→ 工段=检验 + 按产类区分；
    /// 其余 → 待在产执行工段精确匹配英文 Key / 兼容存量中文。
    /// </summary>
    private static bool SectionTabMatches(BatchPlanDto item, string sectionTab, Dictionary<int, List<ProcessGroup>> pgLookup)
    {
        var norm = NormalizeSectionTab(sectionTab) ?? sectionTab;

        // 冷轧类 Tab：工序 Key 命中 → 需工序名包含 + 当前/下一工段为冷拔
        if (_coldRollTabs.Contains(norm))
        {
            return (item.CurrentSectionCompleted == false &&
                    item.CurrentGroupName != null && item.CurrentGroupName.Contains(norm) &&
                    item.CurrentSectionName == SectionKeys.ColdRollDraw) ||
                   (item.CurrentSectionCompleted != false &&
                    item.NextProcess != null && item.NextProcess.Contains(norm) &&
                    item.NextSectionName == SectionKeys.ColdRollDraw);
        }

        // 检验类 Tab：工段=检验（无论最大/非最大工序），再按产类（荒管/在制）区分
        if (norm == "荒管检" || norm == "在制检")
        {
            var inInspection =
                (item.CurrentSectionCompleted == false && item.CurrentSectionName == SectionKeys.Inspection) ||
                (item.CurrentSectionCompleted != false && item.NextSectionName == SectionKeys.Inspection && item.NextProcess != null);
            if (!inInspection) return false;

            var expectedStatus = norm == "荒管检" ? ProductStatuses.RoughTube : ProductStatuses.InProgress;
            return pgLookup.TryGetValue(item.BatchId, out var statusPgs) && statusPgs.Count > 0 &&
                   ComputePendingProductStatus(item.PendingProcess, item.ManufacturingItem, statusPgs, item.Specification) == expectedStatus;
        }

        // 内抛+内修磨 Tab：两工段任一命中（当前/下一工段皆可）
        if (norm == "内抛+内修磨")
        {
            return (item.CurrentSectionCompleted == false &&
                    (item.CurrentSectionName == SectionKeys.InnerPolish || item.CurrentSectionName == SectionKeys.InnerGrinding)) ||
                   (item.CurrentSectionCompleted != false &&
                    (item.NextSectionName == SectionKeys.InnerPolish || item.NextSectionName == SectionKeys.InnerGrinding));
        }

        // 其它：待在产执行工段=Tab名（精确匹配英文 Key / 兼容存量中文，避免 "断切"→"Cut" 子串误匹配 "OilPipeCut"）
        var tabKey = norm;
        var tabCn = SectionKeys.ToChinese(norm);
        return (item.CurrentSectionCompleted == false && item.CurrentSectionName != null &&
                (item.CurrentSectionName == tabKey || item.CurrentSectionName == tabCn)) ||
               (item.CurrentSectionCompleted != false && item.NextSectionName != null &&
                (item.NextSectionName == tabKey || item.NextSectionName == tabCn));
    }

    /// <summary>按工段 Tab 口径构建一行汇总（批次数/总重量/流转/重点/等级分布）</summary>
    private static BatchPlanSummaryRowDto BuildSummaryRow(string sectionName, List<BatchPlanDto> items)
    {
        var totalWeight = items.Sum(x => (decimal)(x.CurrentValidWeight ?? 0));
        var flowBatches = items.Where(x => x.PlanIsFlow).ToList();
        // 重点批次 = 批次计划等级 == 急+（PlanFlowLevel 1），与 G13 等级列口径一致
        var keyBatches = items.Where(x => x.PlanFlowLevel == 1).ToList();
        return new BatchPlanSummaryRowDto
        {
            SectionName = sectionName,
            BatchCount = items.Count,
            TotalWeight = totalWeight,
            FlowBatchCount = flowBatches.Count,
            FlowBatchWeight = flowBatches.Sum(x => (decimal)(x.CurrentValidWeight ?? 0)),
            KeyBatchCount = keyBatches.Count,
            KeyBatchWeight = keyBatches.Sum(x => (decimal)(x.CurrentValidWeight ?? 0)),
            Level1Count = keyBatches.Count,
            Level2Count = items.Count(x => x.PlanFlowLevel == 2),
            Level3Count = items.Count(x => x.PlanFlowLevel == 3),
            Level4Count = items.Count(x => x.PlanFlowLevel == 4),
            Level5Count = items.Count(x => x.PlanFlowLevel == 5),
        };
    }

    public static int? ComputeTargetSequence(List<ProcessGroup> pgs, string? flowTarget, string? flowCRType)
    {
        if (string.IsNullOrEmpty(flowTarget) || pgs.Count == 0)
            return null;

        return flowTarget switch
        {
            // 成检：取工段SectionKeys.Inspection的最大工序内序号
            FlowTargetKeys.Inspection => pgs.Where(pg => pg.Inspection.HasValue)
                        .Select(pg => (int?)pg.Inspection)
                        .Max(),

            // 完工冷轧：匹配冷轧类型+工段SectionKeys.ColdRollDraw，字段值+1
            FlowTargetKeys.CompletionColdRoll => pgs.FirstOrDefault(pg =>
                              pg.ProcessName == flowCRType && pg.ColdRollDraw.HasValue)
                          ?.ColdRollDraw + 1,

            // 冷轧：匹配冷轧类型+工段SectionKeys.ColdRollDraw的字段值
            FlowTargetKeys.ColdRoll => pgs.FirstOrDefault(pg =>
                          pg.ProcessName == flowCRType && pg.ColdRollDraw.HasValue)
                      ?.ColdRollDraw,

            _ => null,
        };
    }

    /// <summary>
    /// 相应工段序：根据主号关注工序从 ProcessGroups 推导（V5.28 补充生产收尾分支，GetPagedAsync/GetAllAsync/PlanAllAsync 三处共用）：
    /// (1) 主号关注工序=='生产收尾' → 从最大 SequenceNumber 工序组向下找第一个有检验工段的工序组，取检验工段序-1（成品检验衔接位；Inspection==1 时取 null；无检验工段则 null）
    /// (2) '荒管处理'/'在制修检' → 对应工序组的检验工段序（Inspection）
    /// (3) 其余冷轧类（含三辊冷轧/冷拔）→ 对应工序组的冷轧拔工段序（ColdRollDraw，原逻辑）
    /// </summary>
    public static int? ComputeAttentionProcessSectionSequence(List<ProcessGroup> pgs, string? mainNoAttentionProcess, HashSet<string> crKeys)
    {
        if (string.IsNullOrEmpty(mainNoAttentionProcess) || pgs.Count == 0)
            return null;

        // (1) 生产收尾：最大工序组检验工段序-1
        if (mainNoAttentionProcess == ProductionAttentionKeys.Finish)
        {
            foreach (var pg in pgs.OrderByDescending(pg => pg.SequenceNumber))
            {
                if (pg.Inspection.HasValue)
                    return pg.Inspection.Value == 1 ? null : (int?)(pg.Inspection.Value - 1);
            }
            return null; // 无任何检验工段
        }

        // (2)(3) 荒管/在制修检 → 检验工段序；冷轧类 → 冷轧拔工段序
        ProcessGroup? targetPg = null;
        if (mainNoAttentionProcess is ProcessKeys.RoughTubeProcessing or ProcessKeys.InProcessRepair)
        {
            targetPg = pgs.FirstOrDefault(pg => pg.ProcessName == mainNoAttentionProcess);
        }
        else if (crKeys.Contains(ProcessKeys.ToKey(mainNoAttentionProcess) ?? mainNoAttentionProcess))
        {
            targetPg = pgs.FirstOrDefault(pg => pg.ProcessName == mainNoAttentionProcess);
        }

        if (targetPg == null) return null;
        return crKeys.Contains(ProcessKeys.ToKey(mainNoAttentionProcess) ?? mainNoAttentionProcess)
            ? targetPg.ColdRollDraw
            : targetPg.Inspection;
    }

    /// <summary>
    /// 冷轧排程维度推导 + 小表匹配 + 关注==当前冷轧 + 目标序（G11 关联冷轧排程字段填充，Model B，V5.25+）。
    /// 供 GetPagedAsync/GetAllAsync/PlanAllAsync 三处共用，保证"流转==是"口径一致。
    /// 注意：本层冷轧维度不要求执行工段=冷轧拔（与 G11 主显示口径一致）。
    /// </summary>
    internal static void ComputeColdRollDimensions(
        BatchPlanDto item,
        List<ProcessGroup> pgs,
        IReadOnlyDictionary<string, ColdRollSpecSchedule> scheduleLookup,
        HashSet<string> crKeys)
    {
        var pendingProcess = item.CurrentSectionCompleted == false
            ? item.CurrentGroupName
            : item.NextProcess;
        var pendingPg = pgs.FirstOrDefault(pg => pg.ProcessName == pendingProcess);
        if (pendingPg == null)
        {
            // 未产批次（无待产工序组）：无排程匹配（_trigger=None），重点兜底仍须填充（与薄表规则(2) 一致）
            ApplyKeyBatchFallback(item, pgs, crKeys);
            return;
        }

        var pendingIdx = pgs.IndexOf(pendingPg);
        var maxSeq = pgs.Max(pg => pg.SequenceNumber);

        // 命中冷轧排程行 ProcessType（AttentionMatchesCurrentCR 判定输入）
        string? matchedCRType = null;

        // 本层冷轧拔工段是否已轧过（V5.35 在轧对齐：本层冷轧拔已完工 → 本层排程要求已完成，转待轧匹配下一冷轧拔层）
        bool curCrPassDone = false;

        // 本层 — 是否冷轧
        if (!string.IsNullOrEmpty(pendingProcess) && crKeys.Contains(ProcessKeys.ToKey(pendingProcess) ?? pendingProcess))
        {
            item.CurrentCR_ProcessType = pendingProcess;
            item.CurrentCR_RollingSpec = pendingPg.ManufacturingSpec;
            if (pendingIdx > 0)
                item.CurrentCR_BilletSpec = pgs[pendingIdx - 1].ManufacturingSpec;
            item.CurrentCR_IsFinished = pendingPg.SequenceNumber == maxSeq;

            // 变形序完成（本层冷轧拔工段是否已轧过，V5.32 用户决策）：与 V5.31 IsColdRollPassDone 同口径，
            // 该层无冷轧拔工段 → 默认完成（IsColdRollPassDone 内部返回 true）
            item.CurrentCR_DeformedSeqCompleted = IsColdRollPassDone(pendingPg, pgs, item);

            // 本层冷轧拔工段是否已轧过（V5.35 在轧对齐：本层冷轧拔已完工 → 本层冷轧排程要求已完成，
            // 不在轧匹配本层 CompletionType，转下一冷轧拔层走待轧匹配——与排程侧 BuildAllocationsAsync 本层 diff<0 跳过一致）
            if (!string.IsNullOrEmpty(item.CurrentCR_ProcessType))
            {
                var curPgForPass = pgs.FirstOrDefault(pg => pg.ProcessName == item.CurrentCR_ProcessType);
                curCrPassDone = curPgForPass != null && IsColdRollPassDone(curPgForPass, pgs, item);
            }

            // 在轧要求：仅在批次实际在轧（在轧设备不为空）且本层冷轧拔未完工时匹配本层
            if (item.IsProducing && !curCrPassDone)
            {
                var curKey = $"{item.CurrentCR_ProcessType}|{item.CurrentCR_BilletSpec}|{item.CurrentCR_RollingSpec}|{item.CurrentCR_IsFinished}";
                if (scheduleLookup.TryGetValue(curKey, out var curSched))
                {
                    item.CR_CompletionType = curSched.CompletionType;
                    matchedCRType = item.CurrentCR_ProcessType;
                }
            }
        }

        // 下层 — 是否冷轧
        if (pendingIdx + 1 < pgs.Count)
        {
            var nextPg = pgs[pendingIdx + 1];
            if (crKeys.Contains(ProcessKeys.ToKey(nextPg.ProcessName) ?? nextPg.ProcessName))
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
            if (crKeys.Contains(ProcessKeys.ToKey(nextNextPg.ProcessName) ?? nextNextPg.ProcessName))
            {
                item.NextNextCR_ProcessType = nextNextPg.ProcessName;
                item.NextNextCR_RollingSpec = nextNextPg.ManufacturingSpec;
                item.NextNextCR_BilletSpec = pgs[pendingIdx + 1].ManufacturingSpec;
                item.NextNextCR_IsFinished = nextNextPg.SequenceNumber == maxSeq;
            }
        }

        // 待轧要求：在轧设备为空 或 本层冷轧拔已完工（V5.35 在轧对齐）时，逐层尝试匹配（本层→下层→下下层）。
        // ⚠️ 规则（V5.34 与冷轧排程 BuildAllocationsAsync 逐层独立一致）：逐层前先判断该层冷轧拔工段是否已轧过——
        //   本层冷轧拔已轧过（当前工段序 > 冷轧拔序，或正处冷轧拔工段且已完工，如本层仅剩检验/酸洗等工段）→
        //   本层不是批次的「下一个冷轧拔层」，跳过；找到的第一个「下一个冷轧拔层」档位对该批次生效 → 停止逐层尝试。
        //   （V5.34 去锁定，用户决策）该层有档位记录但档位不生效（如 Waiting 急-批次本层设 Urgent 需正常流转）→
        //   不锁定、继续尝试下层——与排程侧每层独立匹配一致（排程侧本层行不匹配、仍会流到下层生效层）。
        // 原因：批次计划侧「本层」=待产工序（下一工序组），但批次的下一冷轧拔由冷轧拔工段位置决定，二者可能不一致
        //   （实证批次366：ColdRoll50 组冷轧拔序6已轧完、剩余仅检验，下一冷轧拔在 ColdRoll30 序14）。
        //   锁定已轧过层会误判"本层档位不生效即不流转"，而排程侧按冷轧拔全局工作序把批次计入 ColdRoll30 → 两侧不一致。
        //   V5.35：本层冷轧拔已完工时即使本层检验/酸洗在轧（PendingEquipment 非空）也走待轧匹配（curCrPassDone=true），
        //   与排程侧一致——本层冷轧排程要求已完成，不在轧匹配本层 CompletionType。
        bool rollMatched = false;
        var rollUrgencyLevel = item.UrgencyLevel;
        var rollIsUrgent = UrgencyLevelKeys.IsUrgent(rollUrgencyLevel);
        var rollIsNormal = item.ProductionFlowProperty == ProductionFlowKeys.Normal;

        if (!string.IsNullOrEmpty(item.CurrentCR_ProcessType)
            && (!item.IsProducing || curCrPassDone))
        {
            // 本层冷轧拔已轧过 → 本层不是下一个冷轧拔层，跳过（待轧看下一冷轧拔层，与排程侧对齐）
            var curPg = pgs.FirstOrDefault(pg => pg.ProcessName == item.CurrentCR_ProcessType);
            if (curPg == null || !IsColdRollPassDone(curPg, pgs, item))
            {
                var curKey = $"{item.CurrentCR_ProcessType}|{item.CurrentCR_BilletSpec}|{item.CurrentCR_RollingSpec}|{item.CurrentCR_IsFinished}";
                if (scheduleLookup.TryGetValue(curKey, out var curSched))
                {
                    item.CR_RollType = curSched.RollType;
                    item.CR_SchedMachineNo = curSched.MachineNo;
                    // 本层（=下一个冷轧拔层）档位对该批次生效 → 停止逐层尝试（V5.34 去锁定：不生效→继续下层）
                    if (RollTypeMatchesBatch(curSched.RollType, rollUrgencyLevel, rollIsUrgent, rollIsNormal,
                        item.MainNoAttentionProcess, item.CurrentCR_ProcessType))
                    {
                        matchedCRType = item.CurrentCR_ProcessType;
                        rollMatched = true;
                    }
                }
            }
        }
        if (!rollMatched && !string.IsNullOrEmpty(item.NextCR_ProcessType)
            && (!item.IsProducing || curCrPassDone))
        {
            var nextPg = pgs.FirstOrDefault(pg => pg.ProcessName == item.NextCR_ProcessType);
            if (nextPg == null || !IsColdRollPassDone(nextPg, pgs, item))
            {
                var nextKey = $"{item.NextCR_ProcessType}|{item.NextCR_BilletSpec}|{item.NextCR_RollingSpec}|{item.NextCR_IsFinished}";
                if (scheduleLookup.TryGetValue(nextKey, out var nextSched))
                {
                    item.CR_RollType = nextSched.RollType;
                    item.CR_SchedMachineNo = nextSched.MachineNo;
                    // 下层档位对该批次生效 → 停止逐层尝试（V5.34 去锁定：不生效→继续下下层）
                    if (RollTypeMatchesBatch(nextSched.RollType, rollUrgencyLevel, rollIsUrgent, rollIsNormal,
                        item.MainNoAttentionProcess, item.NextCR_ProcessType))
                    {
                        matchedCRType = item.NextCR_ProcessType;
                        rollMatched = true;
                    }
                }
            }
        }
        if (!rollMatched && !string.IsNullOrEmpty(item.NextNextCR_ProcessType)
            && (!item.IsProducing || curCrPassDone))
        {
            var nextNextPg = pgs.FirstOrDefault(pg => pg.ProcessName == item.NextNextCR_ProcessType);
            if (nextNextPg == null || !IsColdRollPassDone(nextNextPg, pgs, item))
            {
                var nextNextKey = $"{item.NextNextCR_ProcessType}|{item.NextNextCR_BilletSpec}|{item.NextNextCR_RollingSpec}|{item.NextNextCR_IsFinished}";
                if (scheduleLookup.TryGetValue(nextNextKey, out var nextNextSched))
                {
                    item.CR_RollType = nextNextSched.RollType;
                    item.CR_SchedMachineNo = nextNextSched.MachineNo;
                    // 下下层档位对该批次生效 → 停止逐层尝试（V5.34 去锁定：最后一层不生效则整体不流转）
                    if (RollTypeMatchesBatch(nextNextSched.RollType, rollUrgencyLevel, rollIsUrgent, rollIsNormal,
                        item.MainNoAttentionProcess, item.NextNextCR_ProcessType))
                    {
                        matchedCRType = item.NextNextCR_ProcessType;
                        rollMatched = true;
                    }
                }
            }
        }

        // ====== 关注==当前冷轧（AttentionMatchesCurrentCR，Model B 特急档判定输入） ======
        if (!string.IsNullOrEmpty(item.MainNoAttentionProcess) && !string.IsNullOrEmpty(matchedCRType))
        {
            var attnKey = ProcessKeys.ToKey(item.MainNoAttentionProcess) ?? item.MainNoAttentionProcess;
            var matchedKey = ProcessKeys.ToKey(matchedCRType) ?? matchedCRType;
            item.AttentionMatchesCurrentCR = string.Equals(attnKey, matchedKey, StringComparison.OrdinalIgnoreCase);
        }

        // ====== 冷轧排程(实时)：批次的「下一个冷轧拔层」规格信息（V5.32 用户决策） ======
        // 取值：本层变形序未完成（null=本层非冷轧/无冷轧拔，按"完成"处理）→ 取本层；
        //   本层已完成 → 下层（有数据取下层，否则下下层）。
        // 与待轧分支「找到第一个冷轧拔未完成的层」口径一致（本层完成时下层/下下层冷轧拔必然未开始）。
        if (item.CurrentCR_DeformedSeqCompleted != false)
        {
            if (!string.IsNullOrEmpty(item.NextCR_ProcessType))
            {
                item.RealTimeCR_ProcessType = item.NextCR_ProcessType;
                item.RealTimeCR_BilletSpec = item.NextCR_BilletSpec;
                item.RealTimeCR_RollingSpec = item.NextCR_RollingSpec;
                item.RealTimeCR_IsFinished = item.NextCR_IsFinished;
            }
            else if (!string.IsNullOrEmpty(item.NextNextCR_ProcessType))
            {
                item.RealTimeCR_ProcessType = item.NextNextCR_ProcessType;
                item.RealTimeCR_BilletSpec = item.NextNextCR_BilletSpec;
                item.RealTimeCR_RollingSpec = item.NextNextCR_RollingSpec;
                item.RealTimeCR_IsFinished = item.NextNextCR_IsFinished;
            }
        }
        else
        {
            item.RealTimeCR_ProcessType = item.CurrentCR_ProcessType;
            item.RealTimeCR_BilletSpec = item.CurrentCR_BilletSpec;
            item.RealTimeCR_RollingSpec = item.CurrentCR_RollingSpec;
            item.RealTimeCR_IsFinished = item.CurrentCR_IsFinished;
        }

        // ====== 目标序（必须在 AttentionMatchesCurrentCR 之后，因为 FlowTarget 的 _trigger 依赖它） ======
        item.TargetSequence = ComputeTargetSequence(pgs, item.FlowTarget, item.FlowCRType);

        // ====== 实时重点兜底（V5.35 用户决策：实时也加重点兜底，与薄表规则(2) 一致） ======
        ApplyKeyBatchFallback(item, pgs, crKeys);
    }

    /// <summary>
    /// 实时重点兜底（V5.35 用户决策，与薄表规则(2) 一致）：重点生产批次且冷轧排程未命中（KeyBatchFallback）→
    /// 实时 G11 按主号关注工序兜底流转：执行规格=收尾→待产规格、其余→主号关注工序对应工序组规格；
    /// 目标序=相应工段序（与薄表 planTargetSequence 一致）。
    /// 须在排程匹配完成后调用（KeyBatchFallback 依赖 _trigger，即 CR_CompletionType/CR_RollType 的排程命中结果）；
    /// 未产批次（pendingPg==null）在 ComputeColdRollDimensions 提前 return 前也调用（此时无排程匹配、_trigger=None）。
    /// </summary>
    private static void ApplyKeyBatchFallback(BatchPlanDto item, List<ProcessGroup> pgs, HashSet<string> crKeys)
    {
        item.IsKeyBatch = ComputeIsKeyBatch(item, crKeys);
        if (item.KeyBatchFallback)
        {
            item.KeyBatchFallbackExecSpec = item.MainNoAttentionProcess == ProductionAttentionKeys.Finish
                ? item.PendingSpec
                : pgs.FirstOrDefault(pg => pg.ProcessName == item.MainNoAttentionProcess)?.ManufacturingSpec;
            item.TargetSequence = item.AttentionProcessSectionSequence;
        }
    }

    /// <summary>
    /// 待轧要求档位是否对批次生效（与冷轧排程 MatchesScheduleType / BatchPlanDto._trigger 档位语义一致，Model B）：
    /// All/Subsequent=全量；CrOnly=特急(正常流转∧关注==该冷轧层)；Urgent/Partial1=特急/特急-(正常流转)；
    /// Partial2=A+急/A急(IsUrgent)；Partial3=A+急/A急 或 B顺；None/未知/无排程记录=不生效。
    /// </summary>
    private static bool RollTypeMatchesBatch(
        string? rollType, string? urgencyLevel, bool isUrgent, bool isNormal,
        string? attentionProcess, string? layerProcessType)
    {
        bool attentionMatchesThisLayer = false;
        if (!string.IsNullOrEmpty(attentionProcess) && !string.IsNullOrEmpty(layerProcessType))
        {
            var attnKey = ProcessKeys.ToKey(attentionProcess) ?? attentionProcess;
            var layerKey = ProcessKeys.ToKey(layerProcessType) ?? layerProcessType;
            attentionMatchesThisLayer = string.Equals(attnKey, layerKey, StringComparison.OrdinalIgnoreCase);
        }
        return rollType switch
        {
            "All" or "Subsequent" => true,
            "CrOnly" => isUrgent && isNormal && attentionMatchesThisLayer,
            "Urgent" or "Partial1" => isUrgent && isNormal,
            "Partial2" => isUrgent,
            "Partial3" => isUrgent || urgencyLevel == UrgencyLevelKeys.BOrder,
            _ => false,
        };
    }

    /// <summary>
    /// 批次是否已轧过某冷轧层的冷轧拔工段（与冷轧排程 BuildAllocationsAsync「工作序对比」口径一致，V5.31）：
    /// 该层无冷轧拔工段 / 层在当前工序组之前 / 当前组内已越过冷轧拔工段（当前工段序 &gt; 冷轧拔序，
    /// 或正处冷轧拔工段且已完工）/ 当前工序组无工段（组已整体完成）→ 已轧过。
    /// 已轧过的层不是批次的「下一个冷轧拔层」，待轧不应锁定它（待轧应看下一冷轧拔层）。
    /// </summary>
    private static bool IsColdRollPassDone(ProcessGroup layerPg, List<ProcessGroup> pgs, BatchPlanDto item)
    {
        if (!layerPg.ColdRollDraw.HasValue)
            return true; // 该层无冷轧拔工段 → 排程侧 targetGlobalExecSeq&lt;=0 continue，视为已过

        var currentPg = pgs.FirstOrDefault(pg => pg.ProcessName == item.CurrentGroupName);
        if (currentPg == null)
            return false; // 未投产（无当前工序组）→ 尚未轧任何冷轧层

        if (layerPg.SequenceNumber < currentPg.SequenceNumber)
            return true; // 层在当前工序组之前 → 本组已整体完成，冷轧拔已轧过

        if (layerPg.SequenceNumber > currentPg.SequenceNumber)
            return false; // 层在当前工序组之后 → 未到

        // 层 == 当前工序组：比较当前工段在组内执行序与冷轧拔工段序
        if (string.IsNullOrEmpty(item.CurrentSectionName))
            return true; // 当前组无工段 → 本组已全部完成，冷轧拔已轧过

        var currentSeq = layerPg.GetSectionSequence(item.CurrentSectionName);
        if (!currentSeq.HasValue)
            return false; // 找不到当前工段序 → 保守视为未轧过（不跳过该层）

        if (currentSeq.Value > layerPg.ColdRollDraw.Value)
            return true; // 当前工段在冷轧拔之后（如酸洗/检验）→ 冷轧拔已轧过

        if (currentSeq.Value == layerPg.ColdRollDraw.Value)
            return item.CurrentSectionCompleted == true; // 正处冷轧拔工段：完工=已轧过，未完工=在轧（未轧过）

        return false; // 当前工段在冷轧拔之前 → 冷轧拔未轧过
    }

    /// <summary>
    /// 计算批次待执行工序对应的产类（荒管/在制/成品，英文 Key）。
    /// 参照 SectionProductionStatusService：待执行工序名 → 组内首工序制造规格 → ProductStatusHelper.Calculate。
    /// 供荒管检/在制检 Tab 按产类区分（产类需批次全部工序组，SQL 不可翻译，须内存计算）。
    /// </summary>
    private static string ComputePendingProductStatus(
        string? pendingProcess, string? manufacturingItem, List<ProcessGroup> pgs, string? specification)
    {
        if (string.IsNullOrEmpty(pendingProcess) || pgs == null || pgs.Count == 0)
            return ProductStatuses.InProgress;

        var groupKey = ProcessKeys.ToKey(pendingProcess) ?? pendingProcess;
        var pgByKey = pgs
            .GroupBy(pg => ProcessKeys.ToKey(pg.ProcessName) ?? pg.ProcessName)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var spec = pgByKey.TryGetValue(groupKey, out var pg) ? pg.ManufacturingSpec : null;
        return ProductStatusHelper.Calculate(groupKey, spec, manufacturingItem, pgs, specification);
    }

    /// 重点生产批次判定（G4 工单计划组，GetPagedAsync 与 GetAllAsync 共用，防双路径漂移）：
    /// 前置条件：主号关注工序非空 + 紧急级别 A+急/A急 + 生产流转性==正常。
    /// 生产收尾（变形工序已完成，与成品检验衔接）→ 直接重点（不要求相应工段序/执行序）。
    /// 其余：执行序缺省时未产批次（无当前工序组）视为执行序 0（尚未开始执行）纳入比较；
    ///   冷轧类（含三辊冷轧/冷拔）ExecutionSequence &lt; AttentionProcessSectionSequence + 1；
    ///   其他（荒管/在制修检/收尾成检）ExecutionSequence &lt; AttentionProcessSectionSequence。
    /// </summary>
    internal static bool ComputeIsKeyBatch(BatchPlanDto item, HashSet<string> crKeys)
    {
        if (string.IsNullOrEmpty(item.MainNoAttentionProcess)
            || item.UrgencyLevel is not (UrgencyLevelKeys.APlusUrgent or UrgencyLevelKeys.AUrgent)
            || item.ProductionFlowProperty != ProductionFlowKeys.Normal)
            return false;

        // 生产收尾：变形工序已完成，处于与成品检验衔接的收尾阶段 → 直接重点
        if (item.MainNoAttentionProcess == ProductionAttentionKeys.Finish)
            return true;

        // 执行序：未产批次（无当前工序组）视为执行序 0（尚未开始执行），纳入比较
        var execSeq = item.ExecutionSequence;
        if (execSeq == null && string.IsNullOrEmpty(item.CurrentGroupName))
            execSeq = 0;

        if (!execSeq.HasValue || !item.AttentionProcessSectionSequence.HasValue)
            return false;

        if (crKeys.Contains(ProcessKeys.ToKey(item.MainNoAttentionProcess) ?? item.MainNoAttentionProcess))
            return execSeq.Value < item.AttentionProcessSectionSequence.Value + 1;
        return execSeq.Value < item.AttentionProcessSectionSequence.Value;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var batchQuery = _context.ProductionBatches.AsNoTracking()
            .Where(b => b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress);

        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var workOrderQuery = _context.Set<MES.Data.Entities.WorkOrder.WorkOrder>().AsNoTracking();

        var q = from b in batchQuery
                join wo in workOrderQuery on b.WorkOrderNo equals wo.WorkOrderNo into woj
                from wo in woj.DefaultIfEmpty()
                join s in summaryQuery on b.WorkOrderNo equals s.WorkOrderNo into sj
                from s in sj.DefaultIfEmpty()
                where s == null || !s.IsPaused // 主号暂停（IsPaused）批次排除，与主列表口径一致
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
                    b.ProductionType,
                    b.ManufacturingItem,
                    b.ManufacturingStatus,
                    SalesOrderNo = wo != null ? wo.SalesOrderNo : null,
                    ProductionMainNo = wo != null ? wo.ProductionMainNo : null,
                    EndCustomer = wo != null ? wo.EndCustomer : null,
                    b.CurrentGroupName,
                    b.CurrentSectionName,
                    b.NextProcess,
                    b.NextSectionName,
                    UrgencyLevel = s != null ? s.UrgencyLevel : null,
                    // 筛选上下文：与主列表一致，summary 关注状态 5 档映射到排程 4 档
                    ScheduleStage = s != null
                        ? (s.ScheduleStage == 0 || s.ScheduleStage == 1 ? 0
                            : s.ScheduleStage == 2 ? 1
                            : s.ScheduleStage == 3 ? 2
                            : s.ScheduleStage == 4 ? 3
                            : s.ScheduleStage)
                        : (int?)null,
                    MainNoAttentionProcess = s != null ? s.MainNoAttentionProcess : null,
                    ProductionFlowProperty = s != null ? s.ProductionFlowProperty : (b.WorkOrderNo == WorkOrderNoSentinel.NotWorkOrder ? ProductionFlowKeys.Skip : ProductionFlowKeys.Doubt),
                };

        var all = await q.ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["BatchNo"] = all.Select(x => x.BatchNo).Distinct().OrderBy(x => x).ToList(),
            ["TagNo"] = all.Where(x => x.TagNo != null).Select(x => x.TagNo!).Distinct().OrderBy(x => x).ToList(),
            ["PlantGrade"] = all.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
            ["WorkOrderNo"] = all.Select(x => x.WorkOrderNo).Distinct().OrderBy(x => x).ToList(),
            ["SalesOrderNo"] = all.Where(x => x.SalesOrderNo != null).Select(x => x.SalesOrderNo!).Distinct().OrderBy(x => x).ToList(),
            ["ProductionMainNo"] = all.Where(x => x.ProductionMainNo != null).Select(x => x.ProductionMainNo!).Distinct().OrderBy(x => x).ToList(),
            ["EndCustomer"] = all.Where(x => x.EndCustomer != null).Select(x => x.EndCustomer!).Distinct().OrderBy(x => x).ToList(),
            ["Salesman"] = all.Where(x => x.Salesman != null).Select(x => x.Salesman!).Distinct().OrderBy(x => x).ToList(),
            ["DeliveryState"] = all.Where(x => x.DeliveryState != null).Select(x => x.DeliveryState!).Distinct().OrderBy(x => x).ToList(),
            ["Specification"] = all.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
            ["LengthStatus"] = all.Where(x => x.LengthStatus != null).Select(x => x.LengthStatus!).Distinct().OrderBy(x => x).ToList(),
            ["ProductionType"] = all.Where(x => x.ProductionType != null).Select(x => x.ProductionType!).Distinct().OrderBy(x => x).ToList(),
            ["ManufacturingItem"] = all.Where(x => x.ManufacturingItem != null).Select(x => x.ManufacturingItem!).Distinct().OrderBy(x => x).ToList(),
            ["ManufacturingStatus"] = all.Where(x => x.ManufacturingStatus != null).Select(x => x.ManufacturingStatus!).Distinct().OrderBy(x => x).ToList(),
            ["CurrentGroupName"] = all.Where(x => x.CurrentGroupName != null).Select(x => x.CurrentGroupName!).Distinct().OrderBy(x => x).ToList(),
            ["CurrentSectionName"] = all.Where(x => x.CurrentSectionName != null).Select(x => x.CurrentSectionName!).Distinct().OrderBy(x => x).ToList(),
            ["NextProcess"] = all.Where(x => x.NextProcess != null).Select(x => x.NextProcess!).Distinct().OrderBy(x => x).ToList(),
            ["NextSectionName"] = all.Where(x => x.NextSectionName != null).Select(x => x.NextSectionName!).Distinct().OrderBy(x => x).ToList(),
            ["UrgencyLevel"] = all.Where(x => x.UrgencyLevel != null).Select(x => x.UrgencyLevel!).Distinct().OrderBy(x => x).ToList(),
            ["ScheduleStage"] = all.Select(x => x.WorkOrderNo == WorkOrderNoSentinel.NotWorkOrder ? "4" : (x.ScheduleStage.HasValue ? x.ScheduleStage!.Value.ToString() : null))
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

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var pdfBytes = BatchPlanPrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }
}
