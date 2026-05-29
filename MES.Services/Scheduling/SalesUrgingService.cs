using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Scheduling;

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
                x.PlantGrade.Contains(kw) ||
                x.Specification.Contains(kw) ||
                x.ProductionMainNo.Contains(kw));
        }

        // 排除无需排产的数据
        q = q.Where(x => x.ScheduleStage != 0);

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
                UrgingRemark = urgingRemark
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    private static IQueryable<SalesUrgingDto> ApplySorting(
        IQueryable<SalesUrgingDto> query, string? sortBy, bool isDescending)
    {
        var key = sortBy?.ToLower() ?? "workorderno";
        return (key, isDescending) switch
        {
            ("workorderno", false) => query.OrderBy(x => x.WorkOrderNo),
            ("workorderno", true) => query.OrderByDescending(x => x.WorkOrderNo),
            ("salesman", false) => query.OrderBy(x => x.Salesman),
            ("salesman", true) => query.OrderByDescending(x => x.Salesman),
            ("customername", false) => query.OrderBy(x => x.CustomerName),
            ("customername", true) => query.OrderByDescending(x => x.CustomerName),
            ("signdate", false) => query.OrderBy(x => x.SignDate),
            ("signdate", true) => query.OrderByDescending(x => x.SignDate),
            ("deliverydate", false) => query.OrderBy(x => x.DeliveryDate),
            ("deliverydate", true) => query.OrderByDescending(x => x.DeliveryDate),
            ("salesorderno", false) => query.OrderBy(x => x.SalesOrderNo),
            ("salesorderno", true) => query.OrderByDescending(x => x.SalesOrderNo),
            ("productionmainno", false) => query.OrderBy(x => x.ProductionMainNo),
            ("productionmainno", true) => query.OrderByDescending(x => x.ProductionMainNo),
            ("plantgrade", false) => query.OrderBy(x => x.PlantGrade),
            ("plantgrade", true) => query.OrderByDescending(x => x.PlantGrade),
            ("specification", false) => query.OrderBy(x => x.Specification),
            ("specification", true) => query.OrderByDescending(x => x.Specification),
            ("schedulestage", false) => query.OrderBy(x => x.ScheduleStage),
            ("schedulestage", true) => query.OrderByDescending(x => x.ScheduleStage),
            ("issalesurging", false) => query.OrderBy(x => x.IsSalesUrging),
            ("issalesurging", true) => query.OrderByDescending(x => x.IsSalesUrging),
            ("deliverypenalty", false) => query.OrderBy(x => x.DelayPenalty),
            ("deliverypenalty", true) => query.OrderByDescending(x => x.DelayPenalty),
            _ => query.OrderByDescending(x => x.ScheduleStage),
        };
    }
}
