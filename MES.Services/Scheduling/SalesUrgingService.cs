using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Scheduling;

using MES.Services.Helpers;

namespace MES.Services.Scheduling;

/// <summary>
/// 销售催单服务
/// </summary>
public class SalesUrgingService : ISalesUrgingService
{
    private readonly AppDbContext _context;

    public SalesUrgingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<SalesUrgingDto>> GetPagedAsync(QueryParams query)
    {
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var urgingQuery = _context.Set<SalesUrging>().AsNoTracking();

        // LEFT JOIN: WorkOrderExecutionSummary LEFT JOIN SalesUrging
        var q = from e in summaryQuery
                join u in urgingQuery on e.WorkOrderId equals u.WorkOrderId into uj
                from u in uj.DefaultIfEmpty()
                select new SalesUrgingDto
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
                    UrgencyLevel = e.UrgencyLevel,
                    EstimatedProcessCompletionDate = e.EstimatedProcessCompletionDate,
                    DaysDiffFromDelivery = e.DaysDiffFromDelivery,
                    RawMaterialLockRemark = e.RawMaterialLockRemark,
                    IsSalesUrging = u != null && u.IsSalesUrging,
                    UrgingRemark = u != null ? u.UrgingRemark : null,
                    EstimatedArrivalDate = u != null ? u.EstimatedArrivalDate : null,
                    IsMainNoMaterialComplete = u != null && u.IsMainNoMaterialComplete,
                    IsLockConfirmed = u != null && u.IsLockConfirmed,
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
                (x.UrgingRemark != null && x.UrgingRemark.Contains(kw)));
        }

        // 排除无需排产的数据
        q = q.Where(x => x.ScheduleStage != 0);

        // 筛选
        q = q.ApplyFilters(query.Filters);

        // 排序
        q = ApplySorting(q, query.SortBy, query.IsDescending);

        var totalCount = await q.CountAsync();
        var items = await q
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<SalesUrgingDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<bool> SaveUrgingAsync(int workOrderId, bool isSalesUrging, string? urgingRemark)
    {
        var existing = await _context.Set<SalesUrging>()
            .FirstOrDefaultAsync(u => u.WorkOrderId == workOrderId);

        if (existing != null)
        {
            existing.IsSalesUrging = isSalesUrging;
            existing.UrgingRemark = urgingRemark;
            _context.Entry(existing).State = EntityState.Modified;
        }
        else
        {
            _context.Set<SalesUrging>().Add(new SalesUrging
            {
                WorkOrderId = workOrderId,
                IsSalesUrging = isSalesUrging,
                UrgingRemark = urgingRemark,
                IsLockConfirmed = false,
                IsMainNoMaterialComplete = false,
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SaveLockConfirmationAsync(int workOrderId, DateTime? estimatedArrivalDate, bool isMainNoMaterialComplete)
    {
        var existing = await _context.Set<SalesUrging>()
            .FirstOrDefaultAsync(u => u.WorkOrderId == workOrderId);

        if (existing != null)
        {
            existing.EstimatedArrivalDate = estimatedArrivalDate;
            existing.IsMainNoMaterialComplete = isMainNoMaterialComplete;
            existing.IsLockConfirmed = true;
            _context.Entry(existing).State = EntityState.Modified;
        }
        else
        {
            _context.Set<SalesUrging>().Add(new SalesUrging
            {
                WorkOrderId = workOrderId,
                EstimatedArrivalDate = estimatedArrivalDate,
                IsMainNoMaterialComplete = isMainNoMaterialComplete,
                IsLockConfirmed = true,
            });
        }

        // 判断是否推进到生产执行：原料齐全 或 已催单
        var summary = await _context.Set<WorkOrderExecutionSummary>()
            .FirstOrDefaultAsync(e => e.WorkOrderId == workOrderId);

        if (summary != null)
        {
            var isSalesUrging = existing?.IsSalesUrging ?? false;
            if (isMainNoMaterialComplete || isSalesUrging)
            {
                summary.ScheduleStage = 2;
                _context.Entry(summary).State = EntityState.Modified;
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnlockAsync(int workOrderId)
    {
        var existing = await _context.Set<SalesUrging>()
            .FirstOrDefaultAsync(u => u.WorkOrderId == workOrderId);

        if (existing != null)
        {
            existing.IsLockConfirmed = false;
            _context.Entry(existing).State = EntityState.Modified;
        }

        // 回退到原料锁定状态
        var summary = await _context.Set<WorkOrderExecutionSummary>()
            .FirstOrDefaultAsync(e => e.WorkOrderId == workOrderId);

        if (summary != null && summary.ScheduleStage == 2)
        {
            summary.ScheduleStage = 1;
            _context.Entry(summary).State = EntityState.Modified;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.Set<WorkOrderExecutionSummary>().AsNoTracking()
            .Where(e => e.ScheduleStage != 0);

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

        // UrgingRemark 来自 SalesUrging 表（LEFT JOIN）
        var workOrderIds = all.Select(x => x.WorkOrderId).Distinct().ToHashSet();
        var urgingRemarks = workOrderIds.Count > 0
            ? await _context.Set<SalesUrging>()
                .Where(u => workOrderIds.Contains(u.WorkOrderId))
                .Where(u => u.UrgingRemark != null)
                .Select(u => u.UrgingRemark!)
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
            ["UrgingRemark"] = urgingRemarks,
        };
    }

    private static IQueryable<SalesUrgingDto> ApplySorting(
        IQueryable<SalesUrgingDto> query, string? sortBy, bool isDescending)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? query.OrderByDescending(x => x.ScheduleStage)
            : query.ApplySort(sortBy, isDescending);
    }
}
