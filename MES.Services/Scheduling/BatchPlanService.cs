using Microsoft.EntityFrameworkCore;
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
/// 在产明细计划服务 — ProductionBatch LEFT JOIN WorkOrderExecutionSummary + WorkOrderSchedule
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
        var scheduleQuery = _context.Set<WorkOrderSchedule>().AsNoTracking();

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
                     join ws in scheduleQuery on b.WorkOrderNo equals ws.WorkOrderNo into wsj
                     from ws in wsj.DefaultIfEmpty()
                     select new { b, s, ws };

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

            // G4
            UrgencyLevel = x.s != null ? x.s.UrgencyLevel : null,
            ScheduleStage = x.ws != null ? 2 : (x.s != null ? x.s.ScheduleStage : 0),
            ProductionAttentionProcess = x.ws != null ? x.ws.ProductionAttentionProcess : null,
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
            x.CurrentSectionCompleted,
            x.CurrentGroupName,
            x.NextProcess,
            x.ProductionAttentionProcess
        });
        var aggData = await aggQuery.ToListAsync();

        var batchCount = aggData.Count;
        var totalWeight = aggData.Sum(x => x.CurrentValidWeight ?? 0m);

        var keyBatches = aggData.Where(x =>
            x.ScheduleStage == 2 &&
            (x.UrgencyLevel == "A+急" || x.UrgencyLevel == "A急") &&
            ((x.CurrentSectionCompleted == false ? x.CurrentGroupName : x.NextProcess) == "荒管处理" ||
             (x.CurrentSectionCompleted == false ? x.CurrentGroupName : x.NextProcess) == x.ProductionAttentionProcess ||
             x.ProductionAttentionProcess == "收尾-成检")).ToList();
        var keyBatchCount = keyBatches.Count;
        var keyBatchWeight = keyBatches.Sum(x => x.CurrentValidWeight ?? 0m);

        // 分页
        var totalCount = aggData.Count;
        var items = await q
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

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
        var scheduleQuery = _context.Set<WorkOrderSchedule>().AsNoTracking();

        var q = from b in batchQuery
                join s in summaryQuery on b.WorkOrderNo equals s.WorkOrderNo into sj
                from s in sj.DefaultIfEmpty()
                join ws in scheduleQuery on b.WorkOrderNo equals ws.WorkOrderNo into wsj
                from ws in wsj.DefaultIfEmpty()
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
                    ScheduleStage = ws != null ? 2 : (s != null ? s.ScheduleStage : (int?)null),
                    ProductionAttentionProcess = ws != null ? ws.ProductionAttentionProcess : null,
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
