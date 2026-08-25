using Microsoft.EntityFrameworkCore;
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
using MES.Core.Constants;
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
using MES.Core.Helpers;
using MES.Services.Helpers;
using MES.Services.Printing;
using MES.Services.WorkOrder;

namespace MES.Services.Scheduling;

/// <summary>
/// 原锁计划服务（LEFT JOIN 实时查询）
/// </summary>
public class RawMaterialLockPlanAndExecutionService : IRawMaterialLockPlanAndExecutionService
{
    private readonly AppDbContext _context;
    private readonly IConfigParameterService _configService;

    public RawMaterialLockPlanAndExecutionService(AppDbContext context, IConfigParameterService configService)
    {
        _context = context;
        _configService = configService;
    }

    public async Task<PagedResult<RawMaterialLockPlanAndExecutionDto>> GetPagedAsync(QueryParams query)
    {
        // G1-G12+G13: WorkOrderExecutionSummary（仅 ScheduleStage=2 原料锁定）
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking()
            .Where(e => e.ScheduleStage == 2);
        // G3 计算列筛选（到料实投一致性/计划投料总重/现可投料总重/理论缺失总料重）：实体无对应列，通用 ApplyFilters
        // 反射覆盖不到，须在 join 投影前对实体查询内联 WHERE（SQL 可翻译）。其余筛选仍走投影后通用 ApplyFilters。
        summaryQuery = WorkOrderExecutionService.ApplyComputedFilters(summaryQuery, query.Filters);
        // G15: RawMaterialLockPreExecution
        var preExecQuery = _context.Set<RawMaterialLockPreExecution>().AsNoTracking();

        // LEFT JOIN RawMaterialLockPreExecution（G13 直接从实体读取，无需 JOIN）
        var q = from e in summaryQuery
                join p in preExecQuery on e.WorkOrderId equals p.WorkOrderId into pj
                from p in pj.DefaultIfEmpty()
                select new RawMaterialLockPlanAndExecutionDto
                {
                    Id = e.Id,
                    WorkOrderId = e.WorkOrderId,
                    WorkOrderNo = e.WorkOrderNo,

                    // G1
                    Salesman = e.Salesman,
                    CustomerName = e.CustomerName,
                    EndCustomer = e.EndCustomer,
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

                    // G4
                    MaterialPlanStatus = (MaterialPlanStatus)e.MaterialPlanStatus,
                    MainNoMaterialPlanRate = e.MainNoMaterialPlanRate,
                    MainNoMaterialPlanStatus = (MaterialPlanStatus)e.MainNoMaterialPlanStatus,
                    MainNoPlanExecutionStatus = e.MainNoPlanExecutionStatus,
                    MaterialPlanCoveredCount = e.MaterialPlanCoveredCount,
                    MaterialPlanProportion = e.MaterialPlanProportion,
                    TheoreticalCutoffDate = e.TheoreticalCutoffDate,
                    CutoffArrivalDate = e.CutoffArrivalDate,
                    MainNoCutoffArrivalDate = e.MainNoCutoffArrivalDate,

                    // G5-G11 用料计划执行实况
                    PiercingPlanWeight = e.PiercingPlanWeight,
                    PiercingSubOutWeight = e.PiercingSubOutWeight,
                    PiercingSubStatus = e.PiercingSubStatus,
                    PiercingSubInWeight = e.PiercingSubInWeight,
                    PiercingSubPendingWeight = e.PiercingSubPendingWeight,
                    PiercingReturnStatus = e.PiercingReturnStatus,
                    SemiPlanWeight = e.SemiPlanWeight,
                    SemiOrderWeight = e.SemiOrderWeight,
                    SemiOrderStatus = e.SemiOrderStatus,
                    SemiInWeight = e.SemiInWeight,
                    SemiPendingWeight = e.SemiPendingWeight,
                    SemiInStatus = e.SemiInStatus,
                    FinishPlanWeight = e.FinishPlanWeight,
                    FinishOrderWeight = e.FinishOrderWeight,
                    FinishOrderStatus = e.FinishOrderStatus,
                    FinishInWeight = e.FinishInWeight,
                    FinishPendingWeight = e.FinishPendingWeight,
                    FinishInStatus = e.FinishInStatus,
                    InventoryPlanWeight = e.InventoryPlanWeight,
                    InventoryOutWeight = e.InventoryOutWeight,
                    InventoryOutStatus = e.InventoryOutStatus,
                    ReworkPlanWeight = e.ReworkPlanWeight,
                    ReworkPlanInputWeight = e.ReworkPlanInputWeight,
                    ReworkPlanInputStatus = e.ReworkPlanInputStatus,
                    InProcessReworkPlanWeight = e.InProcessReworkPlanWeight,
                    InProcessReworkInputWeight = e.InProcessReworkInputWeight,
                    InProcessReworkInputStatus = e.InProcessReworkInputStatus,
                    InMainPlanWeight = e.InMainPlanWeight,
                    InMainInputWeight = e.InMainInputWeight,
                    InMainInputStatus = e.InMainInputStatus,

                    // G3
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

                    // G13: 直接从实体读取（已由 RefreshAllAsync 同步）
                    IsUrging = e.IsUrging,
                    IsBatchDelivery = e.IsBatchDelivery,
                    IsPaused = e.IsPaused,
                    AdjustmentRemark = e.AdjustmentRemark,

                    // G15: 实时 LEFT JOIN RawMaterialLockPreExecution
                    IsPreInput = p != null && p.IsPreInput,
                    BudgetInputDate = p != null ? p.BudgetInputDate : null,
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
                x.MaterialName.Contains(kw) ||
                (x.UrgencyLevel != null && x.UrgencyLevel.Contains(kw)) ||
                (x.RawMaterialLockRemark != null && x.RawMaterialLockRemark.Contains(kw)) ||
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw)));
        }

        // 筛选（G3 四计算列已由上方 ApplyComputedFilters 在投影前处理，须从 filters 剔除，
        // 否则 DTO 计算属性（如 PlanInputConsistency 是 getter 逻辑）无法被 EF 翻译 → SQL 500）
        q = q.ApplyFilters(ExcludeG3ComputedFilters(query.Filters));

        // 排序
        q = ApplySorting(q, query.SortBy, query.IsDescending);

        var totalCount = await q.CountAsync();

        // 汇总: 待检验到料批次（IsPreInput=true）
        var preInputCount = await q.CountAsync(x => x.IsPreInput);
        var preInputWeight = await q.Where(x => x.IsPreInput).SumAsync(x => x.TotalWeight);

        var items = await q
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<RawMaterialLockPlanAndExecutionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize,
            Extras = new Dictionary<string, object>
            {
                ["preInputCount"] = preInputCount,
                ["preInputWeight"] = preInputWeight
            }
        };
    }

    public async Task<SetPreExecuteFlagsResult> SetPreExecuteFlagsAsync(List<int> workOrderIds, bool? isPreInput, DateTime? budgetInputDate = null)
    {
        // Upsert G15 记录（一个工单一条）
        var existingRecords = await _context.Set<RawMaterialLockPreExecution>()
            .Where(r => workOrderIds.Contains(r.WorkOrderId))
            .ToListAsync();

        foreach (var workOrderId in workOrderIds)
        {
            var record = existingRecords.FirstOrDefault(r => r.WorkOrderId == workOrderId);
            if (record == null)
            {
                record = new RawMaterialLockPreExecution { WorkOrderId = workOrderId };
                _context.Set<RawMaterialLockPreExecution>().Add(record);
            }

            if (isPreInput.HasValue)
                record.IsPreInput = isPreInput.Value;
            if (budgetInputDate.HasValue)
                record.BudgetInputDate = budgetInputDate.Value;
            else if (isPreInput == false)
                record.BudgetInputDate = null;
        }

        var count = await _context.SaveChangesAsync();

        var parts = new List<string>();
        if (isPreInput.HasValue)
            parts.Add(isPreInput.Value ? "执行" : "取消执行");
        if (budgetInputDate.HasValue)
            parts.Add("预算投料日");
        var msg = $"标记完成（{string.Join(",", parts)}），共{count}条";

        return new SetPreExecuteFlagsResult { Count = count, Message = msg };
    }

    /// <summary>G3 计算列（实体无对应列，由 WorkOrderExecutionService.ApplyComputedFilters 在投影前内联 WHERE 处理）</summary>
    private static readonly HashSet<string> G3ComputedFilterFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "TotalPlanWeight", "TotalAvailableWeight", "TotalMissingWeight", "PlanInputConsistency"
    };

    /// <summary>剔除 G3 计算列筛选条件，避免投影后 DTO 计算属性无法被 EF 翻译</summary>
    private static List<FilterDescriptor>? ExcludeG3ComputedFilters(List<FilterDescriptor>? filters)
        => filters?.Where(f => !G3ComputedFilterFields.Contains(f.Field ?? "")).ToList();

    private static IQueryable<RawMaterialLockPlanAndExecutionDto> ApplySorting(
        IQueryable<RawMaterialLockPlanAndExecutionDto> query, string? sortBy, bool isDescending)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? query.OrderByDescending(x => x.WorkOrderNo)
            : query.ApplySort(sortBy, isDescending);
    }

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var pdfBytes = RawMaterialLockPlanPrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }

    /// <summary>
    /// 原锁「待投料量汇总」：标量 + 待投料矩阵（备注 × 计划性）+ 理论待投料截日（类别 × 日期桶）。
    /// 口径 = 前端 RawMaterialLockPlanAndExecution.RecalculateSummary/RecalculateCutoffSummary（2026-08-19 配置化后）：
    /// - PendingCalc 走 ProductionSummaryHelper.CalcPending（质量补料 A 按流转比缺口折算不减已投料，其余减已投料，倍率走 ProcessingDiscount/RawMaterialRatio 默认 1.1）；
    /// - PurchaseCalc = Max(0, 成品计划量 − 成品到货量)；
    /// - 桶边界走 DateBucket 配置（默认 7/15/30/45/60），桶标签为绝对日期样式（与订单负荷总量页同源）。
    /// 全部数值 kg，前端 /1000 转吨 F1。
    /// </summary>
    public async Task<RawMaterialLockPendingSummaryDto> GetPendingSummaryAsync()
    {
        var summaries = await _context.Set<WorkOrderExecutionSummary>().AsNoTracking()
            .Where(e => e.ScheduleStage == 2)
            .Select(e => new
            {
                e.TotalWeight,
                e.FinishPlanWeight,
                e.FinishInWeight,
                e.InputWeight,
                e.FlowOutputRatio,
                e.RawMaterialLockRemark,
                e.UrgencyLevel,
                e.TheoreticalCutoffDate,
            })
            .ToListAsync();

        // 配置：桶边界 + 投料倍率
        var dateBucketMap = await _configService.GetConfigMapAsync("DateBucket");
        var bucket1 = (int)dateBucketMap.GetValueOrDefault("Bucket1", 7m);
        var bucket2 = (int)dateBucketMap.GetValueOrDefault("Bucket2", 15m);
        var bucket3 = (int)dateBucketMap.GetValueOrDefault("Bucket3", 30m);
        var bucket4 = (int)dateBucketMap.GetValueOrDefault("Bucket4", 45m);
        var bucket5 = (int)dateBucketMap.GetValueOrDefault("Bucket5", 60m);
        var rawRatioMap = await _configService.GetConfigMapAsync("ProcessingDiscount");
        var rawRatio = rawRatioMap.GetValueOrDefault("RawMaterialRatio", 1.1m);
        var today = DateTime.Today;
        var buckets = ProductionSummaryHelper.GenerateDateBuckets(today, bucket1, bucket2, bucket3, bucket4, bucket5);

        // 标量（口径 = 前端 RecalculateSummary）
        var totalWeight = summaries.Sum(s => s.TotalWeight);
        var purchaseCount = summaries.Count(s => s.FinishPlanWeight > s.FinishInWeight);
        var purchaseWeight = summaries.Sum(s => Math.Max(0m, s.FinishPlanWeight - s.FinishInWeight));
        var pendingWeight = summaries.Sum(s => ProductionSummaryHelper.CalcPending(
            s.TotalWeight, s.FinishPlanWeight, s.FinishInWeight, s.InputWeight, s.FlowOutputRatio, s.RawMaterialLockRemark, rawRatio));

        // 矩阵：备注 × 计划性（列排除 EPaused 暂停档）
        var matrix = new Dictionary<string, PendingMatrixCellDto>(StringComparer.Ordinal);
        foreach (var s in summaries)
        {
            var remarkKey = RawMaterialLockRemarkKeys.ToKey(s.RawMaterialLockRemark) ?? "";
            var urgencyKey = UrgencyLevelKeys.ToKey(s.UrgencyLevel) ?? "";
            var key = $"{remarkKey}|{urgencyKey}";
            if (!matrix.TryGetValue(key, out var cell))
            {
                cell = new PendingMatrixCellDto();
                matrix[key] = cell;
            }
            var purchase = Math.Max(0m, s.FinishPlanWeight - s.FinishInWeight);
            cell.Count++;
            cell.PendingWeight += ProductionSummaryHelper.CalcPending(
                s.TotalWeight, s.FinishPlanWeight, s.FinishInWeight, s.InputWeight, s.FlowOutputRatio, s.RawMaterialLockRemark, rawRatio);
            cell.PurchaseCount += purchase > 0 ? 1 : 0;
            cell.PurchaseWeight += purchase;
        }

        var remarkRows = RawMaterialLockRemarkKeys.All;
        var urgencyColumns = UrgencyLevelKeys.All.Where(k => k != UrgencyLevelKeys.EPaused).ToArray();
        var matrixRows = new List<PendingMatrixRowDto>();
        foreach (var r in remarkRows)
        {
            var row = new PendingMatrixRowDto();
            foreach (var u in urgencyColumns)
            {
                var cell = matrix.GetValueOrDefault($"{r}|{u}") ?? new PendingMatrixCellDto();
                row.Cells.Add(cell);
                row.RowCount += cell.Count;
                row.RowPendingWeight += cell.PendingWeight;
                row.RowPurchaseCount += cell.PurchaseCount;
                row.RowPurchaseWeight += cell.PurchaseWeight;
            }
            matrixRows.Add(row);
        }

        var columnTotals = urgencyColumns.Select(_ => new PendingMatrixTotalsDto()).ToList();
        var grandTotals = new PendingMatrixTotalsDto();
        foreach (var row in matrixRows)
        {
            for (var ci = 0; ci < row.Cells.Count; ci++)
            {
                var c = row.Cells[ci];
                var col = columnTotals[ci];
                col.Count += c.Count;
                col.PendingWeight += c.PendingWeight;
                col.PurchaseCount += c.PurchaseCount;
                col.PurchaseWeight += c.PurchaseWeight;
            }
            grandTotals.Count += row.RowCount;
            grandTotals.PendingWeight += row.RowPendingWeight;
            grandTotals.PurchaseCount += row.RowPurchaseCount;
            grandTotals.PurchaseWeight += row.RowPurchaseWeight;
        }

        // 理论待投料截日：完善计划/执行计划（各加 PendingCalc）+ 外购成品（全工单 PurchaseCalc）+ 合计
        var cutoffRows = new List<CutoffRowDto>
        {
            new() { Category = "完善计划", Buckets = new List<decimal>(new decimal[buckets.Count]) },
            new() { Category = "执行计划", Buckets = new List<decimal>(new decimal[buckets.Count]) },
            new() { Category = "外购成品", Buckets = new List<decimal>(new decimal[buckets.Count]) },
        };
        foreach (var s in summaries)
        {
            var remarkKey = RawMaterialLockRemarkKeys.ToKey(s.RawMaterialLockRemark);
            var bucket = ProductionSummaryHelper.GetCutoffBucket(s.TheoreticalCutoffDate, buckets);
            var pending = ProductionSummaryHelper.CalcPending(
                s.TotalWeight, s.FinishPlanWeight, s.FinishInWeight, s.InputWeight, s.FlowOutputRatio, s.RawMaterialLockRemark, rawRatio);
            if (remarkKey == RawMaterialLockRemarkKeys.ImprovePlan)
                AddCutoffWeight(cutoffRows[0], pending, bucket);
            else if (remarkKey == RawMaterialLockRemarkKeys.ExecutePlan)
                AddCutoffWeight(cutoffRows[1], pending, bucket);
            // 外购成品 = 全部工单成购缺口（与标量 _purchaseWeight 同口径）
            AddCutoffWeight(cutoffRows[2], Math.Max(0m, s.FinishPlanWeight - s.FinishInWeight), bucket);
        }
        var totalRow = new CutoffRowDto { Category = "合计", Buckets = new List<decimal>(new decimal[buckets.Count]) };
        foreach (var r in cutoffRows)
        {
            totalRow.Total += r.Total;
            for (var i = 0; i < r.Buckets.Count; i++)
                totalRow.Buckets[i] += r.Buckets[i];
        }
        cutoffRows.Add(totalRow);

        return new RawMaterialLockPendingSummaryDto
        {
            TotalOrderCount = summaries.Count,
            TotalWeight = totalWeight,
            PendingWeight = pendingWeight,
            PurchaseCount = purchaseCount,
            PurchaseWeight = purchaseWeight,
            HasPurchaseData = purchaseCount > 0,
            MatrixRowLabels = remarkRows.Select(k => DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, k) ?? k).ToList(),
            MatrixColumnLabels = urgencyColumns.Select(k => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, k) ?? k).ToList(),
            MatrixRows = matrixRows,
            MatrixColumnTotals = columnTotals,
            MatrixGrandTotals = grandTotals,
            CutoffBucketLabels = buckets.Select(b => b.Label).ToList(),
            CutoffRows = cutoffRows,
        };
    }

    private static void AddCutoffWeight(CutoffRowDto row, decimal weight, int bucket)
    {
        row.Total += weight;
        row.Buckets[bucket] += weight;
    }
}
