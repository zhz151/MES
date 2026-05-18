using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// 工单执行状况服务（只读查询 + 手动刷新）
/// </summary>
public class WorkOrderExecutionService : IWorkOrderExecutionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkOrderExecutionService> _logger;

    public WorkOrderExecutionService(AppDbContext context, ILogger<WorkOrderExecutionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<WorkOrderExecutionSummaryDto>> GetPagedAsync(QueryParams query)
    {
        var q = _context.Set<WorkOrderExecutionSummary>().AsQueryable();

        // 关键字搜索（匹配工单号/订单号/业务员/客户/规格等）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            q = q.Where(x =>
                x.WorkOrderNo.Contains(kw) ||
                x.SalesOrderNo.Contains(kw) ||
                x.Salesman.Contains(kw) ||
                x.CustomerName.Contains(kw) ||
                x.SettlementMethod.Contains(kw) ||
                x.MaterialName.Contains(kw) ||
                x.DeliveryState.Contains(kw) ||
                x.LengthStatus.Contains(kw) ||
                x.PlantGrade.Contains(kw) ||
                x.Specification.Contains(kw) ||
                x.ProductionMainNo.Contains(kw) ||
                (x.ProductionSubNo != null && x.ProductionSubNo.Contains(kw)));
        }

        // 排序
        q = ApplySorting(q, query.SortBy, query.IsDescending);

        var totalCount = await q.CountAsync();
        var items = await q
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new WorkOrderExecutionSummaryDto
            {
                Id = e.Id,
                WorkOrderId = e.WorkOrderId,
                WorkOrderNo = e.WorkOrderNo,
                LastRefreshTime = e.LastRefreshTime,

                // Group 1
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

                // Group 2
                LatestPlanDate = e.LatestPlanDate,
                MaterialPlanRate = e.MaterialPlanRate,
                MaterialPlanStatus = e.MaterialPlanStatus,
                MainNoMaterialPlanRate = e.MainNoMaterialPlanRate,
                MainNoMaterialPlanStatus = e.MainNoMaterialPlanStatus,

                // Group 3
                InputStartDate = e.InputStartDate,
                InputEndDate = e.InputEndDate,
                TotalBatchCount = e.TotalBatchCount,
                InputQuantity = e.InputQuantity,
                InputWeight = e.InputWeight,
                TheoreticalOutputQty = e.TheoreticalOutputQty,
                TheoreticalOutputWeight = e.TheoreticalOutputWeight,
                InputOutputRatio = e.InputOutputRatio,
                InputStatus = e.InputStatus,
                MainNoInputOutputRatio = e.MainNoInputOutputRatio,
                MainNoInputStatus = e.MainNoInputStatus,

                // Group 4
                ValidBatchCount = e.ValidBatchCount,
                ValidInputQuantity = e.ValidInputQuantity,
                ValidInputWeight = e.ValidInputWeight,
                ValidOutputQty = e.ValidOutputQty,
                ValidOutputWeight = e.ValidOutputWeight,
                ValidInputOutputRatio = e.ValidInputOutputRatio,
                ValidInputStatus = e.ValidInputStatus,
                MainNoValidInputOutputRatio = e.MainNoValidInputOutputRatio,
                MainNoValidInputStatus = e.MainNoValidInputStatus,
            })
            .ToListAsync();

        return new PagedResult<WorkOrderExecutionSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<WorkOrderExecutionRefreshResultDto> RefreshAllAsync()
    {
        _logger.LogInformation("开始全量刷新工单执行状况汇总");

        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.Status != Core.Enums.WorkOrderStatus.NotGenerated
                      && wo.Status != Core.Enums.WorkOrderStatus.Cancelled)
            .ToListAsync();

        if (workOrders.Count == 0)
        {
            _logger.LogInformation("没有需要刷新的工单");
            return new WorkOrderExecutionRefreshResultDto { TotalWorkOrders = 0, RefreshedCount = 0 };
        }

        var workOrderIds = workOrders.Select(wo => wo.Id).ToHashSet();
        var workOrderNos = workOrders.Select(wo => wo.WorkOrderNo).ToHashSet();

        // 批量加载关联的批次数据（批次通过 WorkOrderNo 关联工单）
        var batches = await _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups)
            .Where(b => workOrderNos.Contains(b.WorkOrderNo))
            .ToListAsync();

        // 按 WorkOrderNo 分组
        var batchesByWo = batches
            .GroupBy(b => b.WorkOrderNo)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 构建客户名称字典（WorkOrder.SalesOrderNo → CustomerProfile.CustomerUnit）
        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => workOrders.Select(w => w.SalesOrderNo).Contains(so.OrderNumber))
            .ToListAsync();

        var customerIds = salesOrders.Select(so => so.CustomerId).Distinct().ToList();
        var customers = await _context.CustomerProfiles
            .AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.CustomerUnit);

        var customerNameByWo = new Dictionary<int, string>();
        foreach (var wo in workOrders)
        {
            var salesOrder = salesOrders.FirstOrDefault(so => so.OrderNumber == wo.SalesOrderNo);
            if (salesOrder != null && customers.TryGetValue(salesOrder.CustomerId, out var name))
                customerNameByWo[wo.Id] = name;
            else
                customerNameByWo[wo.Id] = "";
        }

        // 批量加载用料计划日期
        // 注意：先 ToListAsync 再 GroupBy，兼容 EF Core InMemory 测试
        var semiPlanList = await _context.PurchaseSemiPlans
            .AsNoTracking()
            .Where(p => workOrderIds.Contains(p.WorkOrderId))
            .ToListAsync();
        var semiPlanDates = semiPlanList
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Max(p => p.PlanDate));

        var finishPlanList = await _context.PurchaseFinishedPlans
            .AsNoTracking()
            .Where(p => workOrderIds.Contains(p.WorkOrderId))
            .ToListAsync();
        var finishPlanDates = finishPlanList
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Max(p => p.PlanDate));

        var inventoryPlanList = await _context.InventoryPlans
            .AsNoTracking()
            .Where(p => workOrderIds.Contains(p.WorkOrderId))
            .ToListAsync();
        var inventoryPlanDates = inventoryPlanList
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Max(p => p.PlanDate));

        var piercingPlanList = await _context.RoundBarPiercingPlans
            .AsNoTracking()
            .Where(p => workOrderIds.Contains(p.WorkOrderId))
            .ToListAsync();
        var piercingPlanDates = piercingPlanList
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Max(p => p.PlanDate));

        var now = DateTime.UtcNow;
        var summaries = new List<WorkOrderExecutionSummary>();

        foreach (var wo in workOrders)
        {
            var woBatches = batchesByWo.TryGetValue(wo.WorkOrderNo, out var b) ? b : new List<ProductionBatch>();

            var summary = ComputeSummary(wo, customerNameByWo.TryGetValue(wo.Id, out var cn) ? cn : "", woBatches, semiPlanDates, finishPlanDates, inventoryPlanDates, piercingPlanDates);
            summary.LastRefreshTime = now;
            summaries.Add(summary);
        }

        // 计算主号级投料聚合
        ComputeMainNoInputAggregation(summaries, workOrders);

        // 批量 Upsert
        var existingRecords = await _context.Set<WorkOrderExecutionSummary>().ToListAsync();

        var existingByWoId = existingRecords.ToDictionary(e => e.WorkOrderId);

        foreach (var summary in summaries)
        {
            if (existingByWoId.TryGetValue(summary.WorkOrderId, out var existing))
            {
                // 更新已有记录
                CopySummaryToExisting(summary, existing);
                _context.Entry(existing).State = EntityState.Modified;
            }
            else
            {
                // 新增
                _context.Set<WorkOrderExecutionSummary>().Add(summary);
            }
        }

        // 删除不再需要的记录（工单已取消或删除的）
        var toDelete = existingRecords.Where(e => !workOrderIds.Contains(e.WorkOrderId)).ToList();
        if (toDelete.Count > 0)
        {
            _context.Set<WorkOrderExecutionSummary>().RemoveRange(toDelete);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("工单执行状况刷新完成: 共{Count}条", summaries.Count);

        return new WorkOrderExecutionRefreshResultDto
        {
            TotalWorkOrders = workOrders.Count,
            RefreshedCount = summaries.Count
        };
    }

    private static WorkOrderExecutionSummary ComputeSummary(
        WorkOrder wo,
        string customerName,
        List<ProductionBatch> batches,
        Dictionary<int, DateTime> semiPlanDates,
        Dictionary<int, DateTime> finishPlanDates,
        Dictionary<int, DateTime> inventoryPlanDates,
        Dictionary<int, DateTime> piercingPlanDates)
    {
        // Group 1: 直接从工单复制
        var summary = new WorkOrderExecutionSummary
        {
            WorkOrderId = wo.Id,
            WorkOrderNo = wo.WorkOrderNo,
            Salesman = wo.Salesman,
            CustomerName = customerName,
            SignDate = wo.SignDate,
            DeliveryDate = wo.DeliveryDate,
            DelayPenalty = wo.DelayPenalty,
            SettlementMethod = wo.SettlementMethod.ToString(),
            SalesOrderNo = wo.SalesOrderNo,
            ProductionMainNo = wo.ProductionMainNo,
            ProductionSubNo = wo.ProductionSubNo,
            MaterialName = wo.MaterialName.ToString(),
            DeliveryState = wo.DeliveryState.ToString(),
            PlantGrade = wo.PlantGrade,
            Specification = wo.Specification,
            LengthStatus = wo.LengthStatus.ToString(),
            MinLength = wo.MinLength,
            MaxLength = wo.MaxLength,
            TotalItemCount = wo.TotalItemCount,
            TotalQuantity = wo.TotalQuantity,
            TotalMeters = wo.TotalMeters,
            TotalWeight = wo.TotalWeight,
        };

        // Group 2: 用料计划
        var planDates = new List<DateTime?>();
        if (semiPlanDates.TryGetValue(wo.Id, out var semiDate)) planDates.Add(semiDate);
        if (finishPlanDates.TryGetValue(wo.Id, out var finishDate)) planDates.Add(finishDate);
        if (inventoryPlanDates.TryGetValue(wo.Id, out var invDate)) planDates.Add(invDate);
        if (piercingPlanDates.TryGetValue(wo.Id, out var pierceDate)) planDates.Add(pierceDate);

        summary.LatestPlanDate = planDates.Count > 0 ? planDates.Max() : null;
        summary.MaterialPlanRate = wo.MaterialPlanRate;
        summary.MaterialPlanStatus = (int)wo.MaterialPlanStatus;
        // MainNo 级聚合在后续步骤计算

        // Group 3: 所有批次 — 逐批计算理论成品
        var inputDates = batches
            .Where(b => b.InboundDate.HasValue)
            .Select(b => b.InboundDate!.Value)
            .ToList();

        summary.InputStartDate = inputDates.Count > 0 ? inputDates.Min() : null;
        summary.InputEndDate = inputDates.Count > 0 ? inputDates.Max() : null;
        summary.TotalBatchCount = batches.Count;
        summary.InputQuantity = batches.Sum(b => b.InputQuantity ?? 0);
        summary.InputWeight = batches.Sum(b => b.InputWeight ?? 0);

        // 逐批计算理论成品并累加
        decimal theorQty = 0;
        decimal theorWeight = 0;
        foreach (var batch in batches)
        {
            var batchInputQty = batch.InputQuantity ?? 0;
            var batchInputWeight = batch.InputWeight ?? 0m;

            // 理论成品支数 = 投料支数 × 制几率
            if (batch.ProductionRatio > 0)
                theorQty += batchInputQty * batch.ProductionRatio;

            // 理论成品重量 = 投料重量 × (1 - 有效工序组数 × 2.5%)
            var effectiveGroupCount = batch.ProcessGroups?
                .Count(pg => HasAnySection(pg)) ?? 0;
            var discount = 1.0m - effectiveGroupCount * 0.025m;
            if (discount < 0) discount = 0;
            theorWeight += Math.Round(batchInputWeight * discount, 3);
        }
        summary.TheoreticalOutputQty = Math.Round(theorQty, 3);
        summary.TheoreticalOutputWeight = Math.Round(theorWeight, 3);

        // 投料成品比 + 状态
        var (ratio, status) = ComputeInputRatioAndStatus(summary, wo);
        summary.InputOutputRatio = ratio;
        summary.InputStatus = status;

        // Group 4: 排除作废批次 — 逐批计算有效理论成品
        var validBatches = batches.Where(b => b.Status != Core.Enums.BatchStatus.Cancelled).ToList();

        summary.ValidBatchCount = validBatches.Count;
        summary.ValidInputQuantity = validBatches.Sum(b => b.CurrentValidQty ?? 0);
        summary.ValidInputWeight = validBatches.Sum(b => b.CurrentValidWeight ?? 0);

        // 有效理论成品（逐批计算）
        decimal validTheorQty = 0;
        decimal validTheorWeight = 0;
        foreach (var batch in validBatches)
        {
            var batchInputQty = batch.CurrentValidQty ?? 0;
            var batchInputWeight = batch.CurrentValidWeight ?? 0m;

            if (batch.ProductionRatio > 0)
                validTheorQty += batchInputQty * batch.ProductionRatio;

            var effectiveGroupCount = batch.ProcessGroups?
                .Count(pg => HasAnySection(pg)) ?? 0;
            var discount = 1.0m - effectiveGroupCount * 0.025m;
            if (discount < 0) discount = 0;
            validTheorWeight += Math.Round(batchInputWeight * discount, 3);
        }
        summary.ValidOutputQty = Math.Round(validTheorQty, 3);
        summary.ValidOutputWeight = Math.Round(validTheorWeight, 3);

        var (validRatio, validStatus) = ComputeInputRatioAndStatus(
            summary, wo, summary.ValidOutputQty, summary.ValidOutputWeight);
        summary.ValidInputOutputRatio = validRatio;
        summary.ValidInputStatus = validStatus;

        return summary;
    }

    private static (decimal ratio, int status) ComputeInputRatioAndStatus(
        WorkOrderExecutionSummary summary, WorkOrder wo)
    {
        return ComputeInputRatioAndStatus(summary.LengthStatus, summary.TheoreticalOutputQty, summary.TheoreticalOutputWeight, wo.TotalQuantity, wo.TotalWeight);
    }

    private static (decimal ratio, int status) ComputeInputRatioAndStatus(
        WorkOrderExecutionSummary summary, WorkOrder wo, decimal outputQty, decimal outputWeight)
    {
        return ComputeInputRatioAndStatus(summary.LengthStatus, outputQty, outputWeight, wo.TotalQuantity, wo.TotalWeight);
    }

    private static (decimal ratio, int status) ComputeInputRatioAndStatus(
        string lengthStatus, decimal outputQty, decimal outputWeight, int totalQty, decimal totalWeight)
    {
        var isFixed = lengthStatus == "Fixed";
        decimal ratio;

        if (isFixed)
        {
            ratio = totalQty > 0
                ? Math.Round(outputQty / totalQty * 100, 2)
                : 0;
        }
        else
        {
            ratio = totalWeight > 0
                ? Math.Round(outputWeight / totalWeight * 100, 2)
                : 0;
        }

        var status = ratio switch
        {
            <= 0 => 0,      // 未投料
            >= 100 => 2,    // 满足
            _ => 1          // 部分
        };

        return (ratio, status);
    }

    private static bool HasAnySection(ProcessGroup pg)
    {
        return pg.ColdRollDraw.HasValue
            || pg.OilPipeCut.HasValue
            || pg.Degrease.HasValue
            || pg.Solution.HasValue
            || pg.Straighten.HasValue
            || pg.Cut.HasValue
            || pg.ThicknessMeasure.HasValue
            || pg.Pickle.HasValue
            || pg.OuterPolish.HasValue
            || pg.InnerGrinding.HasValue
            || pg.OuterSpotGrinding.HasValue
            || pg.Inspection.HasValue
            || pg.WeldingHead.HasValue
            || pg.Lubrication.HasValue
            || pg.Warehouse.HasValue;
    }

    private static void ComputeMainNoInputAggregation(
        List<WorkOrderExecutionSummary> summaries, List<WorkOrder> workOrders)
    {
        var woDict = workOrders.ToDictionary(wo => wo.Id);

        // 按 (SalesOrderNo, ProductionMainNo) 分组
        var mainNoGroups = summaries
            .Select(s => new
            {
                Summary = s,
                WorkOrder = woDict.TryGetValue(s.WorkOrderId, out var wo) ? wo : null
            })
            .Where(x => x.WorkOrder != null)
            .GroupBy(x => new { x.WorkOrder!.SalesOrderNo, MainNo = x.Summary.ProductionMainNo })
            .ToList();

        // 先计算 Group 2 的 MainNo 级用料计划
        // 再用同组所有工单的 WorkOrder 聚合 Group 3 & 4 的 MainNo 投料比

        foreach (var group in mainNoGroups)
        {
            var groupWorkOrders = group.Select(g => g.WorkOrder!).ToList();
            var groupSummaries = group.Select(g => g.Summary).ToList();

            // Group 2: MainNo 级用料计划 — 率取平均，状态从率重新计算
            if (groupWorkOrders.Count > 0)
            {
                var avgRate = Math.Round(groupWorkOrders.Average(wo => wo.MaterialPlanRate), 2);
                var isFixed = groupSummaries.First().LengthStatus == "Fixed";
                var mainStatus = CalculateMainNoStatusFromRate(avgRate, isFixed);
                foreach (var s in groupSummaries)
                {
                    s.MainNoMaterialPlanRate = avgRate;
                    s.MainNoMaterialPlanStatus = (int)mainStatus;
                }
            }

            // Group 3: MainNo 级投料聚合（使用已修正的理论成品值）
            var totalQty = groupWorkOrders.Sum(wo => wo.TotalQuantity);
            var totalWeight = groupWorkOrders.Sum(wo => wo.TotalWeight);

            if (totalQty > 0 || totalWeight > 0)
            {
                var totalTheorQty = groupSummaries.Sum(s => s.TheoreticalOutputQty);
                var totalTheorWeight = groupSummaries.Sum(s => s.TheoreticalOutputWeight);

                var isFixed = groupSummaries.First().LengthStatus == "Fixed";
                decimal mainRatio;
                if (isFixed)
                {
                    mainRatio = totalQty > 0
                        ? Math.Round(totalTheorQty / totalQty * 100, 2)
                        : 0;
                }
                else
                {
                    mainRatio = totalWeight > 0
                        ? Math.Round(totalTheorWeight / totalWeight * 100, 2)
                        : 0;
                }

                var mainStatus = mainRatio switch
                {
                    <= 0 => 0,
                    >= 100 => 2,
                    _ => 1
                };

                foreach (var s in groupSummaries)
                {
                    s.MainNoInputOutputRatio = mainRatio;
                    s.MainNoInputStatus = mainStatus;
                }
            }

            // Group 4: 有效主号级投料聚合（排除作废批次，使用 ValidOutputQty/Weight）
            var totalValidQty = groupSummaries.Sum(s => s.ValidOutputQty);
            var totalValidWeight = groupSummaries.Sum(s => s.ValidOutputWeight);

            if (totalQty > 0 || totalWeight > 0)
            {
                var isFixed = groupSummaries.First().LengthStatus == "Fixed";
                decimal mainValidRatio;
                if (isFixed)
                {
                    mainValidRatio = totalQty > 0
                        ? Math.Round(totalValidQty / totalQty * 100, 2)
                        : 0;
                }
                else
                {
                    mainValidRatio = totalWeight > 0
                        ? Math.Round(totalValidWeight / totalWeight * 100, 2)
                        : 0;
                }

                var mainValidStatus = mainValidRatio switch
                {
                    <= 0 => 0,       // 未计划
                    >= 100 => 2,     // 满足（rate≥100%即满足）
                    _ => 1           // 部分
                };

                foreach (var s in groupSummaries)
                {
                    s.MainNoValidInputOutputRatio = mainValidRatio;
                    s.MainNoValidInputStatus = mainValidStatus;
                }
            }
        }
    }

    /// <summary>
    /// 从聚合率计算主号级用料计划状态（与 WorkOrderService.CalculateMainNoStatus 阈值一致）
    /// </summary>
    private static MaterialPlanStatus CalculateMainNoStatusFromRate(decimal rate, bool isFixed)
    {
        if (rate <= 0) return MaterialPlanStatus.NotPlanned;

        if (isFixed)
        {
            if (rate < 102m) return MaterialPlanStatus.Partial;
            if (rate <= 110m) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            if (rate < 105m) return MaterialPlanStatus.Partial;
            if (rate <= 120m) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
    }

    private static void CopySummaryToExisting(WorkOrderExecutionSummary source, WorkOrderExecutionSummary target)
    {
        // Group 1
        target.WorkOrderNo = source.WorkOrderNo;
        target.Salesman = source.Salesman;
        target.CustomerName = source.CustomerName;
        target.SignDate = source.SignDate;
        target.DeliveryDate = source.DeliveryDate;
        target.DelayPenalty = source.DelayPenalty;
        target.SettlementMethod = source.SettlementMethod;
        target.SalesOrderNo = source.SalesOrderNo;
        target.ProductionMainNo = source.ProductionMainNo;
        target.ProductionSubNo = source.ProductionSubNo;
        target.MaterialName = source.MaterialName;
        target.DeliveryState = source.DeliveryState;
        target.PlantGrade = source.PlantGrade;
        target.Specification = source.Specification;
        target.LengthStatus = source.LengthStatus;
        target.MinLength = source.MinLength;
        target.MaxLength = source.MaxLength;
        target.TotalItemCount = source.TotalItemCount;
        target.TotalQuantity = source.TotalQuantity;
        target.TotalMeters = source.TotalMeters;
        target.TotalWeight = source.TotalWeight;

        // Group 2
        target.LatestPlanDate = source.LatestPlanDate;
        target.MaterialPlanRate = source.MaterialPlanRate;
        target.MaterialPlanStatus = source.MaterialPlanStatus;
        target.MainNoMaterialPlanRate = source.MainNoMaterialPlanRate;
        target.MainNoMaterialPlanStatus = source.MainNoMaterialPlanStatus;

        // Group 3
        target.InputStartDate = source.InputStartDate;
        target.InputEndDate = source.InputEndDate;
        target.TotalBatchCount = source.TotalBatchCount;
        target.InputQuantity = source.InputQuantity;
        target.InputWeight = source.InputWeight;
        target.TheoreticalOutputQty = source.TheoreticalOutputQty;
        target.TheoreticalOutputWeight = source.TheoreticalOutputWeight;
        target.InputOutputRatio = source.InputOutputRatio;
        target.InputStatus = source.InputStatus;
        target.MainNoInputOutputRatio = source.MainNoInputOutputRatio;
        target.MainNoInputStatus = source.MainNoInputStatus;

        // Group 4
        target.ValidBatchCount = source.ValidBatchCount;
        target.ValidInputQuantity = source.ValidInputQuantity;
        target.ValidInputWeight = source.ValidInputWeight;
        target.ValidOutputQty = source.ValidOutputQty;
        target.ValidOutputWeight = source.ValidOutputWeight;
        target.ValidInputOutputRatio = source.ValidInputOutputRatio;
        target.ValidInputStatus = source.ValidInputStatus;
        target.MainNoValidInputOutputRatio = source.MainNoValidInputOutputRatio;
        target.MainNoValidInputStatus = source.MainNoValidInputStatus;

        // 刷新时间
        target.LastRefreshTime = source.LastRefreshTime;
    }

    private static IQueryable<WorkOrderExecutionSummary> ApplySorting(
        IQueryable<WorkOrderExecutionSummary> query, string sortBy, bool isDescending)
    {
        var key = sortBy?.ToLower() ?? "workorderno";
        return (key, isDescending) switch
        {
            ("workorderno", false) => query.OrderBy(x => x.WorkOrderNo),
            ("workorderno", true) => query.OrderByDescending(x => x.WorkOrderNo),
            ("salesmanno", false) => query.OrderBy(x => x.Salesman),
            ("salesmanno", true) => query.OrderByDescending(x => x.Salesman),
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
            ("totalquantity", false) => query.OrderBy(x => x.TotalQuantity),
            ("totalquantity", true) => query.OrderByDescending(x => x.TotalQuantity),
            ("totalweight", false) => query.OrderBy(x => x.TotalWeight),
            ("totalweight", true) => query.OrderByDescending(x => x.TotalWeight),
            ("inputquantity", false) => query.OrderBy(x => x.InputQuantity),
            ("inputquantity", true) => query.OrderByDescending(x => x.InputQuantity),
            ("inputweight", false) => query.OrderBy(x => x.InputWeight),
            ("inputweight", true) => query.OrderByDescending(x => x.InputWeight),
            ("inputoutputratio", false) => query.OrderBy(x => x.InputOutputRatio),
            ("inputoutputratio", true) => query.OrderByDescending(x => x.InputOutputRatio),
            ("inputstatus", false) => query.OrderBy(x => x.InputStatus),
            ("inputstatus", true) => query.OrderByDescending(x => x.InputStatus),
            ("lastrefreshtime", false) => query.OrderBy(x => x.LastRefreshTime),
            ("lastrefreshtime", true) => query.OrderByDescending(x => x.LastRefreshTime),
            ("delaypenalty", false) => query.OrderBy(x => x.DelayPenalty),
            ("delaypenalty", true) => query.OrderByDescending(x => x.DelayPenalty),
            ("settlementmethod", false) => query.OrderBy(x => x.SettlementMethod),
            ("settlementmethod", true) => query.OrderByDescending(x => x.SettlementMethod),
            ("productionsubno", false) => query.OrderBy(x => x.ProductionSubNo ?? ""),
            ("productionsubno", true) => query.OrderByDescending(x => x.ProductionSubNo ?? ""),
            ("materialname", false) => query.OrderBy(x => x.MaterialName),
            ("materialname", true) => query.OrderByDescending(x => x.MaterialName),
            ("deliverystate", false) => query.OrderBy(x => x.DeliveryState),
            ("deliverystate", true) => query.OrderByDescending(x => x.DeliveryState),
            ("lengthstatus", false) => query.OrderBy(x => x.LengthStatus),
            ("lengthstatus", true) => query.OrderByDescending(x => x.LengthStatus),
            ("minlength", false) => query.OrderBy(x => x.MinLength ?? 0),
            ("minlength", true) => query.OrderByDescending(x => x.MinLength ?? 0),
            ("maxlength", false) => query.OrderBy(x => x.MaxLength ?? 0),
            ("maxlength", true) => query.OrderByDescending(x => x.MaxLength ?? 0),
            ("totalitemcount", false) => query.OrderBy(x => x.TotalItemCount),
            ("totalitemcount", true) => query.OrderByDescending(x => x.TotalItemCount),
            ("totalmeters", false) => query.OrderBy(x => x.TotalMeters),
            ("totalmeters", true) => query.OrderByDescending(x => x.TotalMeters),
            ("latestplandate", false) => query.OrderBy(x => x.LatestPlanDate),
            ("latestplandate", true) => query.OrderByDescending(x => x.LatestPlanDate),
            ("materialplanrate", false) => query.OrderBy(x => x.MaterialPlanRate),
            ("materialplanrate", true) => query.OrderByDescending(x => x.MaterialPlanRate),
            ("mainnomaterialplanrate", false) => query.OrderBy(x => x.MainNoMaterialPlanRate),
            ("mainnomaterialplanrate", true) => query.OrderByDescending(x => x.MainNoMaterialPlanRate),
            ("inputstartdate", false) => query.OrderBy(x => x.InputStartDate),
            ("inputstartdate", true) => query.OrderByDescending(x => x.InputStartDate),
            ("inputenddate", false) => query.OrderBy(x => x.InputEndDate),
            ("inputenddate", true) => query.OrderByDescending(x => x.InputEndDate),
            ("totalbatchcount", false) => query.OrderBy(x => x.TotalBatchCount),
            ("totalbatchcount", true) => query.OrderByDescending(x => x.TotalBatchCount),
            ("theoreticaloutputqty", false) => query.OrderBy(x => x.TheoreticalOutputQty),
            ("theoreticaloutputqty", true) => query.OrderByDescending(x => x.TheoreticalOutputQty),
            ("theoreticaloutputweight", false) => query.OrderBy(x => x.TheoreticalOutputWeight),
            ("theoreticaloutputweight", true) => query.OrderByDescending(x => x.TheoreticalOutputWeight),
            ("mainnoinputratio", false) => query.OrderBy(x => x.MainNoInputOutputRatio),
            ("mainnoinputratio", true) => query.OrderByDescending(x => x.MainNoInputOutputRatio),
            ("validbatchcount", false) => query.OrderBy(x => x.ValidBatchCount),
            ("validbatchcount", true) => query.OrderByDescending(x => x.ValidBatchCount),
            ("validinputquantity", false) => query.OrderBy(x => x.ValidInputQuantity),
            ("validinputquantity", true) => query.OrderByDescending(x => x.ValidInputQuantity),
            ("validinputweight", false) => query.OrderBy(x => x.ValidInputWeight),
            ("validinputweight", true) => query.OrderByDescending(x => x.ValidInputWeight),
            ("validoutputqty", false) => query.OrderBy(x => x.ValidOutputQty),
            ("validoutputqty", true) => query.OrderByDescending(x => x.ValidOutputQty),
            ("validoutputweight", false) => query.OrderBy(x => x.ValidOutputWeight),
            ("validoutputweight", true) => query.OrderByDescending(x => x.ValidOutputWeight),
            ("validinputoutputratio", false) => query.OrderBy(x => x.ValidInputOutputRatio),
            ("validinputoutputratio", true) => query.OrderByDescending(x => x.ValidInputOutputRatio),
            _ => query.OrderByDescending(x => x.LastRefreshTime),
        };
    }
}
