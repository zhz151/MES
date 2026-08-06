using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Auth;
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
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Scheduling;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Scheduling;

/// <summary>
/// 工单排程服务（基于 WorkOrderExecutionSummary 实时查询 + WorkOrderPlan 薄表覆盖）
/// </summary>
public class WorkOrderScheduleService : IWorkOrderScheduleService
{
    private readonly AppDbContext _context;

    public WorkOrderScheduleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<WorkOrderScheduleDto>> GetPagedAsync(QueryParams query)
    {
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var planQuery = _context.Set<WorkOrderPlan>().AsNoTracking();

        var q = from e in summaryQuery
                join p in planQuery on e.WorkOrderId equals p.WorkOrderId into pj
                from p in pj.DefaultIfEmpty()
                select new WorkOrderScheduleDto
                {
                    Id = e.Id,
                    WorkOrderId = e.WorkOrderId,
                    WorkOrderNo = e.WorkOrderNo,

                    // G1
                    Salesman = e.Salesman,
                    CustomerName = e.CustomerName,
                    SignDate = e.SignDate,
                    DeliveryDate = e.DeliveryDate,
                    DelayPenalty = e.DelayPenalty,
                    SettlementMethod = string.IsNullOrEmpty(e.SettlementMethod) ? default : Enum.Parse<SettlementMethod>(e.SettlementMethod),
                    SalesOrderNo = e.SalesOrderNo,
                    ProductionMainNo = e.ProductionMainNo,
                    ProductionSubNo = e.ProductionSubNo,
                    MaterialName = e.MaterialName,
                    DeliveryState = string.IsNullOrEmpty(e.DeliveryState) ? default : Enum.Parse<DeliveryState>(e.DeliveryState),
                    PlantGrade = e.PlantGrade,
                    Specification = e.Specification,
                    LengthStatus = string.IsNullOrEmpty(e.LengthStatus) ? default : Enum.Parse<LengthStatus>(e.LengthStatus),
                    MinLength = e.MinLength,
                    MaxLength = e.MaxLength,
                    TotalItemCount = e.TotalItemCount,
                    TotalQuantity = e.TotalQuantity,
                    TotalMeters = e.TotalMeters,
                    TotalWeight = e.TotalWeight,

                    // G7
                    FlowOutputRatio = e.FlowOutputRatio,
                    FlowStatus = e.FlowStatus,
                    MainNoFlowOutputRatio = e.MainNoFlowOutputRatio,
                    MainNoFlowStatus = e.MainNoFlowStatus,
                    FlowTotalBatchCount = e.FlowTotalBatchCount,
                    FlowIncompleteBatchCount = e.FlowIncompleteBatchCount,
                    FlowMaxRemainingWorkDays = e.FlowMaxRemainingWorkDays,

                    // G12
                    ScheduleStage = e.ScheduleStage,
                    TotalRemainingWorkDays = e.TotalRemainingWorkDays,
                    CapacityWorkDays = e.CapacityWorkDays,
                    UrgencyLevel = e.UrgencyLevel,
                    EstimatedProcessCompletionDate = e.EstimatedProcessCompletionDate,
                    DaysDiffFromDelivery = e.DaysDiffFromDelivery,
                    RawMaterialLockRemark = e.RawMaterialLockRemark,

                    // G13（直接从实体读取，已由 RefreshAllAsync 同步）
                    IsUrging = e.IsUrging,
                    IsBatchDelivery = e.IsBatchDelivery,
                    IsPaused = e.IsPaused,
                    AdjustmentRemark = e.AdjustmentRemark,

                    // G14
                    PendingSectionRoughTube = e.PendingSectionRoughTube,
                    PendingSectionWarehouseFix = e.PendingSectionWarehouseFix,
                    PendingSection60Roll = e.PendingSection60Roll,
                    PendingSection50Roll = e.PendingSection50Roll,
                    PendingSection30Roll = e.PendingSection30Roll,
                    PendingSection20Roll = e.PendingSection20Roll,
                    PendingSectionThreeRoll = e.PendingSectionThreeRoll,
                    PendingSectionDrawBench = e.PendingSectionDrawBench,
                    DeformedProcessCompleted = e.DeformedProcessCompleted,
                    ProductionAttentionProcess = e.ProductionAttentionProcess,
                    ProductionFlowProperty = e.ProductionFlowProperty,
                    MaxBatchRemainingWorkDays = e.MaxBatchRemainingWorkDays,
                    MainNoAttentionProcess = e.MainNoAttentionProcess,

                    // G15: 工单计划薄表覆盖值
                    PlanScheduleStage = p != null ? p.ScheduleStage : null,
                    PlanUrgencyLevel = p != null ? p.UrgencyLevel : null,
                    PlanProductionAttentionProcess = p != null ? p.ProductionAttentionProcess : null,
                    PlanProductionFlowProperty = p != null ? p.ProductionFlowProperty : null,
                };

        // 筛选条件：排除"略"（已无关注的工单），即仅显示有关注价值的工单
        q = q.Where(x => x.ProductionFlowProperty != null && x.ProductionFlowProperty != ProductionFlowKeys.Skip);

        // 关键词搜索
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            q = q.Where(x =>
                x.WorkOrderNo.Contains(kw) ||
                x.SalesOrderNo.Contains(kw) ||
                x.Salesman.Contains(kw) ||
                x.CustomerName.Contains(kw) ||
                (x.ProductionSubNo != null && x.ProductionSubNo.Contains(kw)) ||
                x.PlantGrade.Contains(kw) ||
                x.Specification.Contains(kw) ||
                x.ProductionMainNo.Contains(kw) ||
                x.MaterialName.Contains(kw) ||
                (x.UrgencyLevel != null && x.UrgencyLevel.Contains(kw)) ||
                (x.RawMaterialLockRemark != null && x.RawMaterialLockRemark.Contains(kw)) ||
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw)) ||
                (x.ProductionAttentionProcess != null && x.ProductionAttentionProcess.Contains(kw)));
        }

        // 筛选
        q = q.ApplyFilters(query.Filters);

        // 排序
        q = ApplySorting(q, query.SortBy, query.IsDescending);

        var totalCount = await q.CountAsync();
        var items = await q
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        ApplyConsistencyStatus(items);

        return new PagedResult<WorkOrderScheduleDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<WorkOrderScheduleDto>> GetAllAsync()
    {
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var planQuery = _context.Set<WorkOrderPlan>().AsNoTracking();

        var q = from e in summaryQuery
                join p in planQuery on e.WorkOrderId equals p.WorkOrderId into pj
                from p in pj.DefaultIfEmpty()
                select new WorkOrderScheduleDto
                {
                    Id = e.Id,
                    WorkOrderId = e.WorkOrderId,
                    WorkOrderNo = e.WorkOrderNo,

                    // G1
                    Salesman = e.Salesman,
                    CustomerName = e.CustomerName,
                    SignDate = e.SignDate,
                    DeliveryDate = e.DeliveryDate,
                    DelayPenalty = e.DelayPenalty,
                    SettlementMethod = string.IsNullOrEmpty(e.SettlementMethod) ? default : Enum.Parse<SettlementMethod>(e.SettlementMethod),
                    SalesOrderNo = e.SalesOrderNo,
                    ProductionMainNo = e.ProductionMainNo,
                    ProductionSubNo = e.ProductionSubNo,
                    MaterialName = e.MaterialName,
                    DeliveryState = string.IsNullOrEmpty(e.DeliveryState) ? default : Enum.Parse<DeliveryState>(e.DeliveryState),
                    PlantGrade = e.PlantGrade,
                    Specification = e.Specification,
                    LengthStatus = string.IsNullOrEmpty(e.LengthStatus) ? default : Enum.Parse<LengthStatus>(e.LengthStatus),
                    MinLength = e.MinLength,
                    MaxLength = e.MaxLength,
                    TotalItemCount = e.TotalItemCount,
                    TotalQuantity = e.TotalQuantity,
                    TotalMeters = e.TotalMeters,
                    TotalWeight = e.TotalWeight,

                    // G7
                    FlowOutputRatio = e.FlowOutputRatio,
                    FlowStatus = e.FlowStatus,
                    MainNoFlowOutputRatio = e.MainNoFlowOutputRatio,
                    MainNoFlowStatus = e.MainNoFlowStatus,
                    FlowTotalBatchCount = e.FlowTotalBatchCount,
                    FlowIncompleteBatchCount = e.FlowIncompleteBatchCount,
                    FlowMaxRemainingWorkDays = e.FlowMaxRemainingWorkDays,

                    // G12
                    ScheduleStage = e.ScheduleStage,
                    TotalRemainingWorkDays = e.TotalRemainingWorkDays,
                    CapacityWorkDays = e.CapacityWorkDays,
                    UrgencyLevel = e.UrgencyLevel,
                    EstimatedProcessCompletionDate = e.EstimatedProcessCompletionDate,
                    DaysDiffFromDelivery = e.DaysDiffFromDelivery,
                    RawMaterialLockRemark = e.RawMaterialLockRemark,

                    // G13
                    IsUrging = e.IsUrging,
                    IsBatchDelivery = e.IsBatchDelivery,
                    IsPaused = e.IsPaused,
                    AdjustmentRemark = e.AdjustmentRemark,

                    // G14
                    PendingSectionRoughTube = e.PendingSectionRoughTube,
                    PendingSectionWarehouseFix = e.PendingSectionWarehouseFix,
                    PendingSection60Roll = e.PendingSection60Roll,
                    PendingSection50Roll = e.PendingSection50Roll,
                    PendingSection30Roll = e.PendingSection30Roll,
                    PendingSection20Roll = e.PendingSection20Roll,
                    PendingSectionThreeRoll = e.PendingSectionThreeRoll,
                    PendingSectionDrawBench = e.PendingSectionDrawBench,
                    DeformedProcessCompleted = e.DeformedProcessCompleted,
                    ProductionAttentionProcess = e.ProductionAttentionProcess,
                    ProductionFlowProperty = e.ProductionFlowProperty,
                    MaxBatchRemainingWorkDays = e.MaxBatchRemainingWorkDays,
                    MainNoAttentionProcess = e.MainNoAttentionProcess,

                    // G15
                    PlanScheduleStage = p != null ? p.ScheduleStage : null,
                    PlanUrgencyLevel = p != null ? p.UrgencyLevel : null,
                    PlanProductionAttentionProcess = p != null ? p.ProductionAttentionProcess : null,
                    PlanProductionFlowProperty = p != null ? p.ProductionFlowProperty : null,
                };

        q = q.Where(x => x.ProductionFlowProperty != null && x.ProductionFlowProperty != ProductionFlowKeys.Skip);

        var items = await q.OrderByDescending(x => x.DaysDiffFromDelivery).ToListAsync();

        ApplyConsistencyStatus(items);

        return items;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var all = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Select(e => new
            {
                e.WorkOrderNo,
                e.Salesman,
                e.CustomerName,
                e.SalesOrderNo,
                e.ProductionMainNo,
                e.ProductionSubNo,
                e.PlantGrade,
                e.Specification,
                e.UrgencyLevel,
                e.RawMaterialLockRemark,
                e.AdjustmentRemark,
                e.ProductionAttentionProcess,
                e.ProductionFlowProperty,
            })
            .Where(e => e.ProductionFlowProperty != null && e.ProductionFlowProperty != ProductionFlowKeys.Skip)
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["WorkOrderNo"] = all.Select(x => x.WorkOrderNo).Distinct().OrderBy(x => x).ToList(),
            ["Salesman"] = all.Select(x => x.Salesman).Distinct().OrderBy(x => x).ToList(),
            ["CustomerName"] = all.Select(x => x.CustomerName).Distinct().OrderBy(x => x).ToList(),
            ["SalesOrderNo"] = all.Select(x => x.SalesOrderNo).Distinct().OrderBy(x => x).ToList(),
            ["ProductionMainNo"] = all.Select(x => x.ProductionMainNo).Distinct().OrderBy(x => x).ToList(),
            ["ProductionSubNo"] = all.Where(x => x.ProductionSubNo != null).Select(x => x.ProductionSubNo!).Distinct().OrderBy(x => x).ToList(),
            ["PlantGrade"] = all.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
            ["Specification"] = all.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
            ["UrgencyLevel"] = all.Where(x => x.UrgencyLevel != null).Select(x => x.UrgencyLevel!).Distinct().OrderBy(x => x).ToList(),
            ["RawMaterialLockRemark"] = all.Where(x => x.RawMaterialLockRemark != null).Select(x => x.RawMaterialLockRemark!).Distinct().OrderBy(x => x).ToList(),
            ["AdjustmentRemark"] = all.Where(x => x.AdjustmentRemark != null).Select(x => x.AdjustmentRemark!).Distinct().OrderBy(x => x).ToList(),
            ["ProductionAttentionProcess"] = all
                .Select(x => x.ProductionAttentionProcess!)
                .Distinct()
                .OrderBy(x => x)
                .ToList(),
            ["ProductionFlowProperty"] = all
                .Select(x => x.ProductionFlowProperty!)
                .Distinct()
                .OrderBy(x => x)
                .ToList(),
        };
    }

    public async Task<bool> SavePlanAsync(SaveWorkOrderPlanRequest request)
    {
        var record = await _context.Set<WorkOrderPlan>()
            .FirstOrDefaultAsync(p => p.WorkOrderId == request.WorkOrderId);

        if (record == null)
        {
            // 全部为 null → 无需创建
            if (request.ScheduleStage == null && request.UrgencyLevel == null
                && request.ProductionAttentionProcess == null && request.ProductionFlowProperty == null)
                return true;

            record = new WorkOrderPlan { WorkOrderId = request.WorkOrderId };
            _context.Set<WorkOrderPlan>().Add(record);
        }

        record.ScheduleStage = request.ScheduleStage;
        record.UrgencyLevel = request.UrgencyLevel;
        record.ProductionAttentionProcess = request.ProductionAttentionProcess;
        record.ProductionFlowProperty = request.ProductionFlowProperty;

        // 全部为 null → 删除记录（恢复系统值）
        if (record.ScheduleStage == null && record.UrgencyLevel == null
            && record.ProductionAttentionProcess == null && record.ProductionFlowProperty == null)
        {
            _context.Set<WorkOrderPlan>().Remove(record);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PlanScheduleAllAsync(QueryParams query)
    {
        var q = _context.Set<WorkOrderExecutionSummary>().AsNoTracking()
            .Where(e => e.ProductionFlowProperty != null && e.ProductionFlowProperty != ProductionFlowKeys.Skip);

        // 关键词搜索（同 GetPagedAsync）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            q = q.Where(e =>
                e.WorkOrderNo.Contains(kw) ||
                e.SalesOrderNo.Contains(kw) ||
                e.Salesman.Contains(kw) ||
                e.CustomerName.Contains(kw) ||
                (e.ProductionSubNo != null && e.ProductionSubNo.Contains(kw)) ||
                e.PlantGrade.Contains(kw) ||
                e.Specification.Contains(kw) ||
                e.ProductionMainNo.Contains(kw) ||
                e.SettlementMethod.Contains(kw) ||
                e.MaterialName.Contains(kw) ||
                e.DeliveryState.Contains(kw) ||
                e.LengthStatus.Contains(kw) ||
                (e.UrgencyLevel != null && e.UrgencyLevel.Contains(kw)) ||
                (e.RawMaterialLockRemark != null && e.RawMaterialLockRemark.Contains(kw)) ||
                (e.AdjustmentRemark != null && e.AdjustmentRemark.Contains(kw)) ||
                (e.ProductionAttentionProcess != null && e.ProductionAttentionProcess.Contains(kw)));
        }

        q = q.ApplyFilters(query.Filters);

        var matchingData = await q.Select(e => new
        {
            e.WorkOrderId,
            e.ScheduleStage,
            e.UrgencyLevel,
            e.ProductionAttentionProcess,
            e.ProductionFlowProperty,
            e.MaxBatchRemainingWorkDays,
            e.MainNoAttentionProcess,
        }).ToListAsync();

        var matchingIds = matchingData.Select(x => x.WorkOrderId).ToHashSet();
        var matchingIdList = matchingIds.ToList();

        // 加载已有的 Plan 记录
        var existingPlans = new List<WorkOrderPlan>();
        if (matchingIdList.Count > 0)
        {
            existingPlans = await _context.Set<WorkOrderPlan>()
                .Where(p => matchingIdList.Contains(p.WorkOrderId))
                .ToListAsync();
        }

        // Upsert: 匹配的工单设置 Plan = 系统值
        foreach (var data in matchingData)
        {
            var plan = existingPlans.FirstOrDefault(p => p.WorkOrderId == data.WorkOrderId);
            if (plan == null)
            {
                plan = new WorkOrderPlan { WorkOrderId = data.WorkOrderId };
                _context.Set<WorkOrderPlan>().Add(plan);
            }
            plan.ScheduleStage = data.ScheduleStage;
            plan.UrgencyLevel = data.UrgencyLevel;
            plan.ProductionAttentionProcess = data.MainNoAttentionProcess;
            plan.ProductionFlowProperty = data.ProductionFlowProperty;
        }

        // 删除不匹配查询的 Plan 行
        var orphanPlans = await _context.Set<WorkOrderPlan>()
            .Where(p => !matchingIds.Contains(p.WorkOrderId))
            .ToListAsync();
        _context.Set<WorkOrderPlan>().RemoveRange(orphanPlans);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PlanScheduleKeepAttentionAsync(QueryParams query)
    {
        var q = _context.Set<WorkOrderExecutionSummary>().AsNoTracking()
            .Where(e => e.ProductionFlowProperty != null && e.ProductionFlowProperty != ProductionFlowKeys.Skip);

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            q = q.Where(e =>
                e.WorkOrderNo.Contains(kw) ||
                e.SalesOrderNo.Contains(kw) ||
                e.Salesman.Contains(kw) ||
                e.CustomerName.Contains(kw) ||
                (e.ProductionSubNo != null && e.ProductionSubNo.Contains(kw)) ||
                e.PlantGrade.Contains(kw) ||
                e.Specification.Contains(kw) ||
                e.ProductionMainNo.Contains(kw) ||
                e.SettlementMethod.Contains(kw) ||
                e.MaterialName.Contains(kw) ||
                e.DeliveryState.Contains(kw) ||
                e.LengthStatus.Contains(kw) ||
                (e.UrgencyLevel != null && e.UrgencyLevel.Contains(kw)) ||
                (e.RawMaterialLockRemark != null && e.RawMaterialLockRemark.Contains(kw)) ||
                (e.AdjustmentRemark != null && e.AdjustmentRemark.Contains(kw)) ||
                (e.ProductionAttentionProcess != null && e.ProductionAttentionProcess.Contains(kw)));
        }

        q = q.ApplyFilters(query.Filters);

        var matchingData = await q.Select(e => new
        {
            e.WorkOrderId,
            e.ScheduleStage,
            e.UrgencyLevel,
            e.ProductionFlowProperty,
        }).ToListAsync();

        var matchingIds = matchingData.Select(x => x.WorkOrderId).ToHashSet();
        var matchingIdList = matchingIds.ToList();

        var existingPlans = new List<WorkOrderPlan>();
        if (matchingIdList.Count > 0)
        {
            existingPlans = await _context.Set<WorkOrderPlan>()
                .Where(p => matchingIdList.Contains(p.WorkOrderId))
                .ToListAsync();
        }

        // Upsert: 只设置工单状态/紧急性/流转性为系统值，保留生产关注的手工调整
        foreach (var data in matchingData)
        {
            var plan = existingPlans.FirstOrDefault(p => p.WorkOrderId == data.WorkOrderId);
            if (plan == null)
            {
                plan = new WorkOrderPlan { WorkOrderId = data.WorkOrderId };
                _context.Set<WorkOrderPlan>().Add(plan);
            }
            plan.ScheduleStage = data.ScheduleStage;
            plan.UrgencyLevel = data.UrgencyLevel;
            plan.ProductionFlowProperty = data.ProductionFlowProperty;
            // ProductionAttentionProcess 保持不变
        }

        // 删除不匹配查询的 Plan 行
        var orphanPlans = await _context.Set<WorkOrderPlan>()
            .Where(p => !matchingIds.Contains(p.WorkOrderId))
            .ToListAsync();
        _context.Set<WorkOrderPlan>().RemoveRange(orphanPlans);

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 设置 ConsistencyStatus 四值：
    /// - "一致"：4 个 Plan 字段均匹配系统值
    /// - "进度调整"：仅 ProductionAttentionProcess 不一致（人为调进度，合理）
    /// - "值存疑"：工单状态/紧急性/流转性 任一不一致（存在疑问）
    /// - "错误"：同主号下不同工单的计划值不一致（应保持主号级一致）
    /// </summary>
    private static void ApplyConsistencyStatus(List<WorkOrderScheduleDto> items)
    {
        // 先计算每行的个体一致性
        foreach (var item in items)
        {
            // 排程覆盖档位(4档)映射到关注状态档位(5档)后再比较
            bool stageMatch = item.PlanScheduleStage != null
                && MapPlanStageToSummaryStage(item.PlanScheduleStage.Value) == item.ScheduleStage;
            bool urgencyMatch = string.IsNullOrEmpty(item.PlanUrgencyLevel)
                ? string.IsNullOrEmpty(item.UrgencyLevel)
                : item.PlanUrgencyLevel == item.UrgencyLevel;
            bool attentionMatch = string.IsNullOrEmpty(item.PlanProductionAttentionProcess)
                ? string.IsNullOrEmpty(item.MainNoAttentionProcess)
                : item.PlanProductionAttentionProcess == item.MainNoAttentionProcess;
            bool flowMatch = string.IsNullOrEmpty(item.PlanProductionFlowProperty)
                ? string.IsNullOrEmpty(item.ProductionFlowProperty)
                : item.PlanProductionFlowProperty == item.ProductionFlowProperty;

            if (stageMatch && urgencyMatch && attentionMatch && flowMatch)
                item.ConsistencyStatus = "一致";
            else if (stageMatch && urgencyMatch && flowMatch && !attentionMatch)
                item.ConsistencyStatus = "进度调整";
            else
                item.ConsistencyStatus = "值存疑";
        }

        // 再检查跨主号一致性：同主号下所有工单的计划值应当一致
        var mainOrderGroups = items
            .GroupBy(x => new { x.SalesOrderNo, x.ProductionMainNo })
            .Where(g => g.Count() > 1);

        foreach (var group in mainOrderGroups)
        {
            var groupList = group.ToList();

            // 取第一个非 null 值作为"期望值"，检查组内是否有不一致
            var expectedStage = groupList.Select(x => x.PlanScheduleStage).FirstOrDefault(x => x != null);
            var expectedUrgency = groupList.Select(x => x.PlanUrgencyLevel).FirstOrDefault(x => x != null);
            var expectedAttention = groupList.Select(x => x.PlanProductionAttentionProcess).FirstOrDefault(x => x != null);
            var expectedFlow = groupList.Select(x => x.PlanProductionFlowProperty).FirstOrDefault(x => x != null);

            bool hasCrossInconsistency = groupList.Any(x =>
                (expectedStage != null && x.PlanScheduleStage != expectedStage) ||
                (expectedUrgency != null && x.PlanUrgencyLevel != expectedUrgency) ||
                (expectedAttention != null && x.PlanProductionAttentionProcess != expectedAttention) ||
                (expectedFlow != null && x.PlanProductionFlowProperty != expectedFlow));

            if (hasCrossInconsistency)
            {
                foreach (var item in groupList)
                {
                    item.ConsistencyStatus = "错误";
                }
            }
        }
    }

    /// <summary>排程计划覆盖档位(4档) → 关注状态档位(5档)：0 工单完成→1 主号完成、1 原料锁定→2、2 生产执行→3、3 成品检验→4</summary>
    private static int MapPlanStageToSummaryStage(int planStage) => planStage switch
    {
        0 => 1,
        1 => 2,
        2 => 3,
        3 => 4,
        _ => planStage
    };

    private static IQueryable<WorkOrderScheduleDto> ApplySorting(
        IQueryable<WorkOrderScheduleDto> query, string? sortBy, bool isDescending)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? query.OrderByDescending(x => x.WorkOrderNo)
            : query.ApplySort(sortBy, isDescending);
    }

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var pdfBytes = WorkOrderSchedulePrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }
}
