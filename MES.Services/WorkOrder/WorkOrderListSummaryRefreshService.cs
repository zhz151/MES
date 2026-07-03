using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Configuration;
using WoEntity = MES.Data.Entities.WorkOrder;

namespace MES.Services.WorkOrder;

/// <summary>
/// 用料计划总览读模型刷新服务
/// 在用料计划 CRUD 完成后被调用，按 SalesOrderNo 刷新 WorkOrderListSummary 所有相关行
/// </summary>
public class WorkOrderListSummaryRefreshService : IWorkOrderListSummaryRefreshService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkOrderListSummaryRefreshService> _logger;
    private readonly IConfigParameterService _configService;
    private readonly IDailyOutputEstimateService _dailyOutputService;
    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();

    public WorkOrderListSummaryRefreshService(
        AppDbContext context,
        ILogger<WorkOrderListSummaryRefreshService> logger,
        IConfigParameterService configService,
        IDailyOutputEstimateService dailyOutputService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
        _dailyOutputService = dailyOutputService;
    }

    public async Task RefreshBySalesOrderAsync(string salesOrderNo)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo))
        {
            _logger.LogWarning("RefreshBySalesOrderAsync 收到空的 salesOrderNo，跳过");
            return;
        }

        try
        {
            _logger.LogInformation("开始刷新读模型: SalesOrderNo={SalesOrderNo}", salesOrderNo);

            // 1. 加载指定订单号的所有工单
            var workOrders = await _context.WorkOrders
                .AsNoTracking()
                .Where(wo => wo.SalesOrderNo == salesOrderNo)
                .ToListAsync();

            if (workOrders.Count == 0)
            {
                _logger.LogInformation("SalesOrderNo={SalesOrderNo} 没有工单，清除已有读模型行", salesOrderNo);
                var existing = await _context.Set<WorkOrderListSummary>()
                    .Where(s => s.SalesOrderNo == salesOrderNo)
                    .ToListAsync();
                _context.Set<WorkOrderListSummary>().RemoveRange(existing);
                await _context.SaveChangesAsync();
                return;
            }

            var workOrderIds = workOrders.Select(wo => wo.Id).ToHashSet();

            // 2. 加载所有 4 种计划
            var allSemiPlans = await _context.PurchaseSemiPlans
                .Where(p => workOrderIds.Contains(p.WorkOrderId))
                .ToListAsync();

            var allFinishPlans = await _context.PurchaseFinishedPlans
                .Where(p => workOrderIds.Contains(p.WorkOrderId))
                .ToListAsync();

            var allInventoryPlans = await _context.InventoryPlans
                .Where(p => workOrderIds.Contains(p.WorkOrderId) && p.PlanStatus != InventoryPlanStatus.Cancelled)
                .ToListAsync();

            var allPiercingPlans = await _context.RoundBarPiercingPlans
                .Where(p => workOrderIds.Contains(p.WorkOrderId))
                .ToListAsync();

            // 3. 从 CustomerProfile 取 Salesman/EndCustomer
            var customerFields = await GetCustomerFieldsAsync(salesOrderNo);

            // 4. 计算按工单的计划聚合（重量/支数/日期）
            var semiWeightByWo = allSemiPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredWeight));
            var semiPiecesByWo = allSemiPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple));

            var finishWeightByWo = allFinishPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredWeight));
            var finishPiecesByWo = allFinishPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredPiece ?? 0));

            var normalInv = allInventoryPlans.Where(p => p.ReworkType == null).ToList();
            var reworkInv = allInventoryPlans.Where(p => p.ReworkType != null).ToList();

            var inventoryWeightByWo = normalInv.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.UsedWeight));
            var inventoryPiecesByWo = normalInv.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple));

            var reworkWeightByWo = reworkInv.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.UsedWeight));
            var reworkPiecesByWo = reworkInv.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple));

            var piercingWeightByWo = allPiercingPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredWeight));
            var piercingPiecesByWo = allPiercingPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple));

            // 最新计划日期
            var latestDateByWo = new Dictionary<int, DateTime>();
            void MergeMaxDate(IEnumerable<IGrouping<int, DateTime>> groups)
            {
                foreach (var g in groups)
                {
                    var max = g.Max();
                    if (latestDateByWo.TryGetValue(g.Key, out var existing))
                    { if (max > existing) latestDateByWo[g.Key] = max; }
                    else { latestDateByWo[g.Key] = max; }
                }
            }
            MergeMaxDate(allSemiPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));
            MergeMaxDate(allFinishPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));
            MergeMaxDate(allInventoryPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));
            MergeMaxDate(allPiercingPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));

            // 最新要求到货日
            var latestRequiredDateByWo = new Dictionary<int, DateTime>();
            void MergeMaxRequiredDate(IEnumerable<IGrouping<int, DateTime?>> groups)
            {
                foreach (var g in groups)
                {
                    var nonNull = g.Where(d => d.HasValue).Select(d => d.Value).ToList();
                    if (nonNull.Count == 0) continue;
                    var max = nonNull.Max();
                    if (latestRequiredDateByWo.TryGetValue(g.Key, out var existing))
                    { if (max > existing) latestRequiredDateByWo[g.Key] = max; }
                    else { latestRequiredDateByWo[g.Key] = max; }
                }
            }
            MergeMaxRequiredDate(allSemiPlans.GroupBy(p => p.WorkOrderId, p => (DateTime?)p.RequiredDate));
            MergeMaxRequiredDate(allFinishPlans.GroupBy(p => p.WorkOrderId, p => (DateTime?)p.RequiredDate));
            MergeMaxRequiredDate(allInventoryPlans.GroupBy(p => p.WorkOrderId, p => (DateTime?)p.PlanDate));
            MergeMaxRequiredDate(allPiercingPlans.GroupBy(p => p.WorkOrderId, p => (DateTime?)p.RequiredDate));

            // 配置阈值
            var (fixedFinishRatio, fixedInventoryRatio, nonFixedFinishRatio, nonFixedInventoryRatio,
                 fixedPartial, fixedSatisfied, nonFixedPartial, nonFixedSatisfied,
                 smallBatchMaxQty, smallBatchSatisfiedRate)
                = await LoadConfigThresholdsAsync();

            var semiByWo = allSemiPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
            var finishByWo = allFinishPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
            var inventoryByWo = allInventoryPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
            var piercingByWo = allPiercingPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
            var woById = workOrders.ToDictionary(wo => wo.Id);

            // 5. 构建每个工单的 Summary 行
            var now = DateTimeOffset.Now;
            var currentUser = "system";
            var summaryRows = new List<WorkOrderListSummary>();

            foreach (var wo in workOrders)
            {
                var semi = semiByWo.TryGetValue(wo.Id, out var s) ? s : new List<PurchaseSemiPlan>();
                var finish = finishByWo.TryGetValue(wo.Id, out var f) ? f : new List<PurchaseFinishedPlan>();
                var inv = inventoryByWo.TryGetValue(wo.Id, out var iv) ? iv : new List<InventoryPlan>();
                var pierce = piercingByWo.TryGetValue(wo.Id, out var p) ? p : new List<RoundBarPiercingPlan>();

                // 计算工单级满足率/状态
                var (rate, status) = PlanRateCalculator.ComputeWorkOrderRate(wo, semi, finish, inv, pierce,
                    fixedPartial, fixedSatisfied, nonFixedPartial, nonFixedSatisfied);

                // 计算 MaxStandardCycle
                var allCycles = semi.Select(ps => ps.StandardCycle)
                    .Concat(finish.Select(pf => pf.StandardCycle))
                    .Concat(inv.Select(pi => pi.StandardCycle))
                    .Concat(pierce.Select(rp => rp.StandardCycle))
                    .Where(c => c > 0);

                var maxCycle = allCycles.Any() ? allCycles.Max() : 0;

                // 计算料态种数
                var coveredCount = 0;
                if (semiWeightByWo.TryGetValue(wo.Id, out var semiW) && semiW > 0) coveredCount++;
                if (finishWeightByWo.TryGetValue(wo.Id, out var finW) && finW > 0) coveredCount++;
                if (inventoryWeightByWo.TryGetValue(wo.Id, out var invW) && invW > 0) coveredCount++;
                if (reworkWeightByWo.TryGetValue(wo.Id, out var rewW) && rewW > 0) coveredCount++;
                if (piercingWeightByWo.TryGetValue(wo.Id, out var pW) && pW > 0) coveredCount++;

                // 用料占比文本
                var proportionText = BuildProportionText(wo, semi, finish, normalInv.Where(n => n.WorkOrderId == wo.Id).ToList(),
                    reworkInv.Where(r => r.WorkOrderId == wo.Id).ToList(), pierce);

                var row = new WorkOrderListSummary
                {
                    WorkOrderId = wo.Id,
                    WorkOrderNo = wo.WorkOrderNo,
                    SalesOrderNo = wo.SalesOrderNo,
                    ProductionMainNo = wo.ProductionMainNo,
                    ProductionSubNo = wo.ProductionSubNo,
                    OrderItemIds = wo.OrderItemIds,
                    SignDate = wo.SignDate,
                    Salesman = customerFields.salesman ?? wo.Salesman,
                    EndCustomer = customerFields.endCustomer ?? wo.EndCustomer,
                    DeliveryDate = wo.DeliveryDate,
                    DelayPenalty = wo.DelayPenalty,
                    SettlementMethod = wo.SettlementMethod.ToString(),
                    MaterialName = wo.MaterialName.ToString(),
                    StandardCode = wo.StandardCode,
                    DeliveryState = wo.DeliveryState.ToString(),
                    PlantGrade = wo.PlantGrade,
                    Specification = wo.Specification,
                    OuterDiameterNegative = wo.OuterDiameterNegative,
                    OuterDiameterPositive = wo.OuterDiameterPositive,
                    WallThicknessNegative = wo.WallThicknessNegative,
                    WallThicknessPositive = wo.WallThicknessPositive,
                    LengthStatus = wo.LengthStatus.ToString(),
                    MinLength = wo.MinLength,
                    MaxLength = wo.MaxLength,
                    TotalQuantity = wo.TotalQuantity,
                    TotalMeters = wo.TotalMeters,
                    TotalWeight = wo.TotalWeight,
                    TotalItemCount = wo.TotalItemCount,
                    ItemDetails = wo.ItemDetails,
                    TechnicalRequirements = wo.TechnicalRequirements.ToString(),
                    Status = (int)wo.Status,
                    CreatedTime = wo.CreatedTime,
                    // 预计算计划聚合
                    LatestPlanDate = latestDateByWo.TryGetValue(wo.Id, out var ld) ? ld : null,
                    MaterialPlanRate = rate,
                    MaterialPlanStatus = status,
                    SemiPlanTotalWeight = semiWeightByWo.TryGetValue(wo.Id, out var sw) ? sw : null,
                    SemiPlanTotalPieces = semiPiecesByWo.TryGetValue(wo.Id, out var sp) ? sp : null,
                    FinishedPlanTotalWeight = finishWeightByWo.TryGetValue(wo.Id, out var fw) ? fw : null,
                    FinishedPlanTotalPieces = finishPiecesByWo.TryGetValue(wo.Id, out var fp) ? fp : null,
                    InventoryPlanTotalWeight = inventoryWeightByWo.TryGetValue(wo.Id, out var iw) ? iw : null,
                    InventoryPlanTotalPieces = inventoryPiecesByWo.TryGetValue(wo.Id, out var ip) ? ip : null,
                    ReworkPlanTotalWeight = reworkWeightByWo.TryGetValue(wo.Id, out var rw) ? rw : null,
                    ReworkPlanTotalPieces = reworkPiecesByWo.TryGetValue(wo.Id, out var rp) ? rp : null,
                    PiercingPlanTotalWeight = piercingWeightByWo.TryGetValue(wo.Id, out var pw) ? pw : null,
                    PiercingPlanTotalPieces = piercingPiecesByWo.TryGetValue(wo.Id, out var pp) ? pp : null,
                    MaxStandardCycle = maxCycle,
                    MaterialPlanCoveredCount = coveredCount,
                    MaterialPlanProportion = proportionText,
                    LatestRequiredDate = latestRequiredDateByWo.TryGetValue(wo.Id, out var rd) ? rd : null,
                    LastRefreshTime = DateTime.Now
                };

                summaryRows.Add(row);
            }

            // 6. 计算主号级和订单级聚合后写入
            ComputeMainNoAndOrderAggregation(summaryRows, workOrders, allSemiPlans, allFinishPlans,
                allInventoryPlans, allPiercingPlans,
                fixedFinishRatio, fixedInventoryRatio, nonFixedFinishRatio, nonFixedInventoryRatio,
                smallBatchMaxQty, smallBatchSatisfiedRate,
                fixedPartial, fixedSatisfied, nonFixedPartial, nonFixedSatisfied);

            // 6.5 计算主号级字段：主号最大工艺周期、理论工量、理论截止投料日
            await ComputeMainNoLevelFieldsAsync(summaryRows, allFinishPlans);

            // 7. 全量刷新：删除该订单的所有已有行，重新插入（避免 SetValues 修改主键的 EF Core 异常）
            var existingRows = await _context.Set<WorkOrderListSummary>()
                .Where(s => s.SalesOrderNo == salesOrderNo)
                .ToListAsync();

            _context.Set<WorkOrderListSummary>().RemoveRange(existingRows);

            foreach (var row in summaryRows)
            {
                row.UpdatedTime = now;
                row.UpdatedBy = currentUser;
                row.CreatedBy = currentUser;
                row.CreatedTime = now;
                _context.Set<WorkOrderListSummary>().Add(row);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("读模型刷新完成: SalesOrderNo={SalesOrderNo}, 工单数={Count}",
                salesOrderNo, summaryRows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读模型刷新失败: SalesOrderNo={SalesOrderNo}", salesOrderNo);
            // 不抛异常，避免影响调用方的计划保存
        }
    }

    private async Task<(string? salesman, string? endCustomer)> GetCustomerFieldsAsync(string salesOrderNo)
    {
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .Include(so => so.Customer)
            .FirstOrDefaultAsync(so => so.OrderNumber == salesOrderNo);

        return (salesOrder?.Customer?.Salesman, salesOrder?.Customer?.EndCustomer);
    }

    private async Task<(
        decimal fixedFinishRatio, decimal fixedInventoryRatio,
        decimal nonFixedFinishRatio, decimal nonFixedInventoryRatio,
        decimal fixedPartial, decimal fixedSatisfied,
        decimal nonFixedPartial, decimal nonFixedSatisfied,
        decimal smallBatchMaxQty, decimal smallBatchSatisfiedRate)> LoadConfigThresholdsAsync()
    {
        var ff = await GetConfigAsync("MaterialPlanRatio", "FixedFinishRatio", 1.02m);
        var fi = await GetConfigAsync("MaterialPlanRatio", "FixedInventoryRatio", 1.02m);
        var nf = await GetConfigAsync("MaterialPlanRatio", "NonFixedFinishRatio", 1.05m);
        var ni = await GetConfigAsync("MaterialPlanRatio", "NonFixedInventoryRatio", 1.05m);
        var fp = await GetConfigAsync("MaterialPlanStatus", "FixedPartial", 102m);
        var fs = await GetConfigAsync("MaterialPlanStatus", "FixedSatisfied", 110m);
        var np = await GetConfigAsync("MaterialPlanStatus", "NonFixedPartial", 105m);
        var ns = await GetConfigAsync("MaterialPlanStatus", "NonFixedSatisfied", 120m);
        var sq = await GetConfigAsync("MaterialPlanStatus", "SmallBatchMaxQty", 20m);
        var sr = await GetConfigAsync("MaterialPlanStatus", "SmallBatchSatisfiedRate", 100m);
        return (ff, fi, nf, ni, fp, fs, np, ns, sq, sr);
    }

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        if (!_configMaps.TryGetValue(category, out var map))
        {
            map = await _configService.GetConfigMapAsync(category);
            _configMaps[category] = map;
        }
        return map.TryGetValue(key, out var val) ? val : defaultValue;
    }

    private static void ComputeMainNoAndOrderAggregation(
        List<WorkOrderListSummary> rows,
        List<WoEntity> allWorkOrders,
        List<PurchaseSemiPlan> allSemiPlans,
        List<PurchaseFinishedPlan> allFinishPlans,
        List<InventoryPlan> allInventoryPlans,
        List<RoundBarPiercingPlan> allPiercingPlans,
        decimal fixedFinishRatio, decimal fixedInventoryRatio,
        decimal nonFixedFinishRatio, decimal nonFixedInventoryRatio,
        decimal smallBatchMaxQty, decimal smallBatchSatisfiedRate,
        decimal fixedPartial, decimal fixedSatisfied,
        decimal nonFixedPartial, decimal nonFixedSatisfied)
    {
        // 主号级聚合
        var mainNoKeys = rows
            .Select(r => new { r.SalesOrderNo, MainNo = r.ProductionMainNo })
            .Distinct()
            .ToList();

        foreach (var key in mainNoKeys)
        {
            var groupWorkOrders = allWorkOrders
                .Where(wo => wo.SalesOrderNo == key.SalesOrderNo && wo.ProductionMainNo == key.MainNo)
                .ToList();

            var groupIds = groupWorkOrders.Select(wo => wo.Id).ToHashSet();
            var groupSemiPlans = allSemiPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList();
            var groupFinishPlans = allFinishPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList();
            var groupInventoryAll = allInventoryPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList();
            var groupInventoryPlans = groupInventoryAll.Where(p => p.ReworkType == null).ToList();
            var groupReworkPlans = groupInventoryAll.Where(p => p.ReworkType != null).ToList();
            var groupPiercingPlans = allPiercingPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList();

            var (rate, status) = CalculateMainNoAggregation(groupWorkOrders, groupSemiPlans, groupFinishPlans,
                groupInventoryPlans, groupReworkPlans, groupPiercingPlans,
                fixedFinishRatio, fixedInventoryRatio, nonFixedFinishRatio, nonFixedInventoryRatio,
                smallBatchMaxQty, smallBatchSatisfiedRate, fixedPartial, fixedSatisfied,
                nonFixedPartial, nonFixedSatisfied);

            foreach (var row in rows.Where(r =>
                r.SalesOrderNo == key.SalesOrderNo && r.ProductionMainNo == key.MainNo))
            {
                row.MainNoMaterialPlanRate = rate;
                row.MainNoMaterialPlanStatus = (int)status;
            }
        }

        // 订单级聚合
        var orderNo = rows.FirstOrDefault()?.SalesOrderNo;
        if (orderNo == null) return;

        var orderRows = rows.Where(r => r.SalesOrderNo == orderNo).ToList();
        var hasPartialOrNotPlanned = orderRows.Any(r =>
            r.MainNoMaterialPlanStatus == (int)MaterialPlanStatus.Partial ||
            r.MainNoMaterialPlanStatus == (int)MaterialPlanStatus.NotPlanned);
        var allNotPlanned = orderRows.All(r =>
            r.MainNoMaterialPlanStatus == (int)MaterialPlanStatus.NotPlanned);

        MaterialPlanStatus orderStatus;
        if (allNotPlanned)
            orderStatus = MaterialPlanStatus.NotPlanned;
        else if (hasPartialOrNotPlanned)
            orderStatus = MaterialPlanStatus.Partial;
        else
            orderStatus = MaterialPlanStatus.Satisfied;

        foreach (var row in orderRows)
            row.OrderMaterialPlanStatus = (int)orderStatus;
    }

    private static (decimal rate, MaterialPlanStatus status) CalculateMainNoAggregation(
        List<WoEntity> workOrders,
        List<PurchaseSemiPlan> semiPlans,
        List<PurchaseFinishedPlan> finishPlans,
        List<InventoryPlan> inventoryPlans,
        List<InventoryPlan> reworkPlans,
        List<RoundBarPiercingPlan> piercingPlans,
        decimal fixedFinishRatio, decimal fixedInventoryRatio,
        decimal nonFixedFinishRatio, decimal nonFixedInventoryRatio,
        decimal smallBatchMaxQty, decimal smallBatchSatisfiedRate,
        decimal fixedPartial, decimal fixedSatisfied,
        decimal nonFixedPartial, decimal nonFixedSatisfied)
    {
        var fixedOrders = workOrders.Where(wo => wo.LengthStatus == LengthStatus.Fixed).ToList();
        var nonFixedOrders = workOrders.Where(wo => wo.LengthStatus != LengthStatus.Fixed).ToList();

        decimal totalDemand = 0;
        decimal totalEffective = 0;

        if (fixedOrders.Any())
        {
            var fixedIds = fixedOrders.Select(wo => wo.Id).ToHashSet();
            totalDemand += fixedOrders.Sum(wo => wo.TotalQuantity);

            var fixedSemi = semiPlans.Where(p => fixedIds.Contains(p.WorkOrderId)).ToList();
            var fixedFinish = finishPlans.Where(p => fixedIds.Contains(p.WorkOrderId)).ToList();
            var fixedInventory = inventoryPlans.Where(p => fixedIds.Contains(p.WorkOrderId)).ToList();
            var fixedRework = reworkPlans.Where(p => fixedIds.Contains(p.WorkOrderId)).ToList();
            var fixedPiercing = piercingPlans.Where(p => fixedIds.Contains(p.WorkOrderId)).ToList();

            totalEffective += (int)fixedSemi.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple);
            totalEffective += (int)fixedPiercing.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple);
            totalEffective += fixedFinish.Sum(p => p.RequiredPiece ?? 0) * fixedFinishRatio;
            totalEffective += (int)(fixedInventory.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple) * fixedInventoryRatio);
            totalEffective += (int)fixedRework.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple);
        }

        if (nonFixedOrders.Any())
        {
            var nonFixedIds = nonFixedOrders.Select(wo => wo.Id).ToHashSet();
            totalDemand += nonFixedOrders.Sum(wo => wo.TotalWeight);

            var nonFixedSemi = semiPlans.Where(p => nonFixedIds.Contains(p.WorkOrderId)).ToList();
            var nonFixedFinish = finishPlans.Where(p => nonFixedIds.Contains(p.WorkOrderId)).ToList();
            var nonFixedInventory = inventoryPlans.Where(p => nonFixedIds.Contains(p.WorkOrderId)).ToList();
            var nonFixedRework = reworkPlans.Where(p => nonFixedIds.Contains(p.WorkOrderId)).ToList();
            var nonFixedPiercing = piercingPlans.Where(p => nonFixedIds.Contains(p.WorkOrderId)).ToList();

            totalEffective += nonFixedSemi.Sum(p => p.RequiredWeight);
            totalEffective += nonFixedFinish.Sum(p => p.RequiredWeight) * nonFixedFinishRatio;
            totalEffective += nonFixedInventory.Sum(p => p.UsedWeight) * nonFixedInventoryRatio;
            totalEffective += nonFixedRework.Sum(p => p.UsedWeight);
            totalEffective += nonFixedPiercing.Sum(p => p.RequiredWeight);
        }

        if (totalDemand <= 0) return (0, MaterialPlanStatus.NotPlanned);
        var rate = totalEffective / totalDemand * 100m;
        if (rate <= 0) return (0, MaterialPlanStatus.NotPlanned);

        var fixedTotalQuantity = fixedOrders.Sum(wo => wo.TotalQuantity);
        if (fixedTotalQuantity > 0 && fixedTotalQuantity <= smallBatchMaxQty)
        {
            var batchStatus = rate >= smallBatchSatisfiedRate ? MaterialPlanStatus.Satisfied : MaterialPlanStatus.Partial;
            return (rate, batchStatus);
        }

        var status = fixedOrders.Any()
            ? CalculateStatus(rate, fixedPartial, fixedSatisfied, true)
            : CalculateStatus(rate, nonFixedPartial, nonFixedSatisfied, false);

        return (rate, status);
    }

    private static MaterialPlanStatus CalculateStatus(decimal rate, decimal partialThreshold, decimal satisfiedThreshold, bool isFixed)
    {
        if (rate <= 0) return MaterialPlanStatus.NotPlanned;
        if (isFixed)
        {
            if (rate < partialThreshold) return MaterialPlanStatus.Partial;
            if (rate <= satisfiedThreshold) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            if (rate < partialThreshold) return MaterialPlanStatus.Partial;
            if (rate <= satisfiedThreshold) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
    }

    private static string? BuildProportionText(
        WoEntity wo,
        List<PurchaseSemiPlan> semiPlans,
        List<PurchaseFinishedPlan> finishPlans,
        List<InventoryPlan> inventoryPlans,
        List<InventoryPlan> reworkPlans,
        List<RoundBarPiercingPlan> piercingPlans)
    {
        var isFixed = wo.LengthStatus == LengthStatus.Fixed;
        var parts = new List<string>();

        if (isFixed)
        {
            var totalQty = wo.TotalQuantity;
            if (totalQty <= 0) return null;

            var piercingPieces = (int)piercingPlans.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple);
            if (piercingPieces > 0)
                parts.Add($"穿{piercingPieces / (decimal)totalQty * 100:F0}%");

            var semiPieces = (int)semiPlans.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple);
            if (semiPieces > 0)
                parts.Add($"荒{semiPieces / (decimal)totalQty * 100:F0}%");

            var finishPieces = finishPlans.Sum(p => p.RequiredPiece ?? 0);
            if (finishPieces > 0)
                parts.Add($"成{finishPieces / (decimal)totalQty * 100:F0}%");

            var invPieces = (int)inventoryPlans.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple);
            if (invPieces > 0)
                parts.Add($"库{invPieces / (decimal)totalQty * 100:F0}%");

            var reworkPieces = (int)reworkPlans.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple);
            if (reworkPieces > 0)
                parts.Add($"改{reworkPieces / (decimal)totalQty * 100:F0}%");
        }
        else
        {
            var totalWt = wo.TotalWeight;
            if (totalWt <= 0) return null;

            var piercingWt = piercingPlans.Sum(p => p.RequiredWeight);
            if (piercingWt > 0)
                parts.Add($"穿{piercingWt / totalWt * 100:F0}%");

            var semiWt = semiPlans.Sum(p => p.RequiredWeight);
            if (semiWt > 0)
                parts.Add($"荒{semiWt / totalWt * 100:F0}%");

            var finishWt = finishPlans.Sum(p => p.RequiredWeight);
            if (finishWt > 0)
                parts.Add($"成{finishWt / totalWt * 100:F0}%");

            var invWt = inventoryPlans.Sum(p => p.UsedWeight);
            if (invWt > 0)
                parts.Add($"库{invWt / totalWt * 100:F0}%");

            var reworkWt = reworkPlans.Sum(p => p.UsedWeight);
            if (reworkWt > 0)
                parts.Add($"改{reworkWt / totalWt * 100:F0}%");
        }

        return parts.Any() ? string.Join(" ", parts) : null;
    }

    /// <summary>
    /// 计算主号级字段：主号最大工艺周期、理论工量、理论截止投料日
    /// </summary>
    private async Task ComputeMainNoLevelFieldsAsync(
        List<WorkOrderListSummary> rows,
        List<PurchaseFinishedPlan> allFinishPlans)
    {
        // 加载日产估算配置
        var dailyEstimates = await _dailyOutputService.GetAllAsync();

        // 按主号分组
        var mainNoGroups = rows
            .GroupBy(r => new { r.SalesOrderNo, MainNo = r.ProductionMainNo })
            .ToList();

        foreach (var group in mainNoGroups)
        {
            // 1. 主号最大工艺周期 = Max(同主号所有工单的 MaxStandardCycle)
            var mainNoMaxCycle = group.Max(r => r.MaxStandardCycle);
            if (mainNoMaxCycle == 0)
                mainNoMaxCycle = 22; // 同主号均无计划时默认 22 天（不含缓冲，加3天缓冲后为25天）

            // 2. 产能工量 = Ceiling((主号总重量 - 成品采购重量) / 日产估算)
            var finishWoIds = allFinishPlans.Where(p => p.WorkOrderId > 0)
                .Select(p => p.WorkOrderId).ToHashSet();
            var mainNoTotalWeight = group.Sum(r => r.TotalWeight);
            var mainNoFinishWeight = group.Sum(r => r.FinishedPlanTotalWeight ?? 0);
            var capacityWeight = mainNoTotalWeight - mainNoFinishWeight;

            int? capacityDays = null;
            if (capacityWeight > 0)
            {
                var spec = group.First().Specification;
                var od = ParseOuterDiameter(spec);
                if (od.HasValue)
                {
                    var match = dailyEstimates
                        .Where(e => e.MinOuterDiameter <= od.Value)
                        .OrderByDescending(e => e.MinOuterDiameter)
                        .FirstOrDefault();
                    if (match != null && match.DailyOutputTons > 0)
                    {
                        var totalTons = capacityWeight / 1000m;
                        capacityDays = (int)Math.Ceiling(totalTons / match.DailyOutputTons);
                    }
                }
            }

            // 3. 理论截止投料日 = 最晚交货日期 - 主号最大工艺周期 - 产能工量
            var latestDelivery = group.Max(r => r.DeliveryDate);
            DateTime? cutoffDate = null;
            var totalDays = mainNoMaxCycle + (capacityDays ?? 0);
            if (totalDays > 0)
            {
                cutoffDate = latestDelivery.AddDays(-totalDays);
            }

            // 回填到每个工单行
            foreach (var row in group)
            {
                row.MainNoMaxStandardCycle = mainNoMaxCycle;
                row.CapacityWorkDays = capacityDays ?? 0;
                row.TheoreticalCutoffDate = cutoffDate;
            }
        }
    }

    /// <summary>
    /// 全量刷新所有 WorkOrderListSummary 读模型
    /// </summary>
    public async Task RefreshAllAsync()
    {
        _logger.LogInformation("开始全量刷新用料计划总览读模型");

        var salesOrderNos = await _context.WorkOrders
            .Where(wo => wo.Status != Core.Enums.WorkOrderStatus.NotGenerated)
            .Select(wo => wo.SalesOrderNo)
            .Distinct()
            .ToListAsync();

        _logger.LogInformation("共发现 {Count} 个需要刷新的订单", salesOrderNos.Count);

        foreach (var salesOrderNo in salesOrderNos)
        {
            try
            {
                await RefreshBySalesOrderAsync(salesOrderNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新订单 {SalesOrderNo} 失败", salesOrderNo);
            }
        }

        _logger.LogInformation("全量刷新用料计划总览读模型完成");
    }

    /// <summary>从规格中解析外径（如 "25*2.5" → 25）</summary>
    private static decimal? ParseOuterDiameter(string? specification)
    {
        if (string.IsNullOrWhiteSpace(specification)) return null;
        var sep = new[] { '*', '×', 'x', 'X' };
        var parts = specification.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && decimal.TryParse(parts[0].Trim(), out var od))
            return od;
        return null;
    }
}
