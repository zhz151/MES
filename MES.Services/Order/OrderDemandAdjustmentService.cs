using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Scheduling;

using MES.Services.Helpers;

namespace MES.Services.Order;

/// <summary>
/// 订单需求调整服务
/// </summary>
public class OrderDemandAdjustmentService : IOrderDemandAdjustmentService
{
    private readonly AppDbContext _context;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;

    public OrderDemandAdjustmentService(AppDbContext context, IWorkOrderExecutionService workOrderExecutionService)
    {
        _context = context;
        _workOrderExecutionService = workOrderExecutionService;
    }

    public async Task<PagedResult<OrderDemandAdjustmentDto>> GetPagedAsync(QueryParams query)
    {
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var urgingQuery = _context.Set<OrderDemandAdjustment>().AsNoTracking();

        // LEFT JOIN: WorkOrderExecutionSummary LEFT JOIN OrderDemandAdjustment
        var q = from e in summaryQuery
                join u in urgingQuery on e.WorkOrderId equals u.WorkOrderId into uj
                from u in uj.DefaultIfEmpty()
                select new OrderDemandAdjustmentDto
                {
                    Id = e.Id,
                    WorkOrderId = e.WorkOrderId,
                    WorkOrderNo = e.WorkOrderNo,
                    Salesman = e.Salesman,
                    CustomerName = e.CustomerName,
                    SignDate = e.SignDate,
                    DeliveryDate = e.DeliveryDate,
                    DelayPenalty = e.DelayPenalty,
                    SettlementMethod = e.SettlementMethod,
                    SalesOrderNo = e.SalesOrderNo,
                    ProductionMainNo = e.ProductionMainNo,
                    ProductionSubNo = e.ProductionSubNo,
                    MaterialName = e.MaterialName,
                    DeliveryState = e.DeliveryState,
                    PlantGrade = e.PlantGrade,
                    Specification = e.Specification,
                    LengthStatus = e.LengthStatus,
                    MinLength = e.MinLength,
                    MaxLength = e.MaxLength,
                    TotalItemCount = e.TotalItemCount,
                    TotalQuantity = e.TotalQuantity,
                    TotalMeters = e.TotalMeters,
                    TotalWeight = e.TotalWeight,
                    ScheduleStage = e.ScheduleStage,
                    TotalRemainingWorkDays = e.TotalRemainingWorkDays,
                    CapacityWorkDays = e.CapacityWorkDays,
                    UrgencyLevel = e.UrgencyLevel,
                    EstimatedProcessCompletionDate = e.EstimatedProcessCompletionDate,
                    DaysDiffFromDelivery = e.DaysDiffFromDelivery,
                    RawMaterialLockRemark = e.RawMaterialLockRemark,
                    FlowOutputRatio = e.FlowOutputRatio,
                    FlowStatus = e.FlowStatus,
                    MainNoFlowOutputRatio = e.MainNoFlowOutputRatio,
                    MainNoFlowStatus = e.MainNoFlowStatus,
                    FlowTotalBatchCount = e.FlowTotalBatchCount,
                    FlowIncompleteBatchCount = e.FlowIncompleteBatchCount,
                    FlowMaxRemainingWorkDays = e.FlowMaxRemainingWorkDays,
                    IsUrging = u != null && u.IsUrging,
                    IsBatchDelivery = u != null && u.IsBatchDelivery,
                    IsPaused = u != null && u.IsPaused,
                    AdjustmentRemark = u != null ? u.AdjustmentRemark : null,
                };

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
                (x.SettlementMethod != null && x.SettlementMethod.Contains(kw)) ||
                x.MaterialName.Contains(kw) ||
                x.DeliveryState.Contains(kw) ||
                x.LengthStatus.Contains(kw) ||
                (x.UrgencyLevel != null && x.UrgencyLevel.Contains(kw)) ||
                (x.RawMaterialLockRemark != null && x.RawMaterialLockRemark.Contains(kw)) ||
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw)));
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

        return new PagedResult<OrderDemandAdjustmentDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<bool> SaveUrgingAsync(int workOrderId, bool isUrging, bool isBatchDelivery, bool isPaused, string? adjustmentRemark)
    {
        var existing = await _context.Set<OrderDemandAdjustment>()
            .FirstOrDefaultAsync(u => u.WorkOrderId == workOrderId);

        if (existing != null)
        {
            existing.IsUrging = isUrging;
            existing.IsBatchDelivery = isBatchDelivery;
            existing.IsPaused = isPaused;
            existing.AdjustmentRemark = adjustmentRemark;
            _context.Entry(existing).State = EntityState.Modified;
        }
        else
        {
            _context.Set<OrderDemandAdjustment>().Add(new OrderDemandAdjustment
            {
                WorkOrderId = workOrderId,
                IsUrging = isUrging,
                IsBatchDelivery = isBatchDelivery,
                IsPaused = isPaused,
                AdjustmentRemark = adjustmentRemark,
            });
        }

        await _context.SaveChangesAsync();

        // 实时同步读模型：IsPaused 变化需立即反映到 WorkOrderExecutionSummary.UrgencyLevel（E停）
        // 增量刷新：仅刷新该工单及其同 SalesOrderNo 的兄弟工单
        var workOrderNo = await _context.WorkOrders
            .AsNoTracking()
            .Where(w => w.Id == workOrderId)
            .Select(w => w.WorkOrderNo)
            .FirstOrDefaultAsync();
        if (!string.IsNullOrEmpty(workOrderNo))
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(new List<string> { workOrderNo });
        }

        return true;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();

        var all = await query
            .Select(s => new
            {
                s.WorkOrderId,
                s.WorkOrderNo,
                s.Salesman,
                s.CustomerName,
                s.SalesOrderNo,
                s.ProductionMainNo,
                s.ProductionSubNo,
                s.PlantGrade,
                s.Specification,
                s.UrgencyLevel,
                s.RawMaterialLockRemark,
            })
            .ToListAsync();

        // AdjustmentRemark 来自 OrderDemandAdjustment 表（LEFT JOIN）
        var workOrderIds = all.Select(x => x.WorkOrderId).Distinct().ToHashSet();
        var adjustmentRemarks = workOrderIds.Count > 0
            ? await _context.Set<OrderDemandAdjustment>()
                .Where(u => workOrderIds.Contains(u.WorkOrderId))
                .Where(u => u.AdjustmentRemark != null)
                .Select(u => u.AdjustmentRemark!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync()
            : new List<string>();

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
            ["AdjustmentRemark"] = adjustmentRemarks,
        };
    }

    private static IQueryable<OrderDemandAdjustmentDto> ApplySorting(
        IQueryable<OrderDemandAdjustmentDto> query, string? sortBy, bool isDescending)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? query.OrderByDescending(x => x.ScheduleStage)
            : query.ApplySort(sortBy, isDescending);
    }
}
