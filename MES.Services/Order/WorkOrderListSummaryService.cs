using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services.Order;

/// <summary>
/// 用料计划总览读模型刷新服务
/// </summary>
public class WorkOrderListSummaryService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkOrderListSummaryService> _logger;

    public WorkOrderListSummaryService(AppDbContext context, ILogger<WorkOrderListSummaryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>全量刷新所有用料计划总览读模型</summary>
    public async Task RefreshAllAsync()
    {
        _logger.LogInformation("开始全量刷新用料计划总览读模型");

        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.Status != WorkOrderStatus.Cancelled)
            .ToListAsync();

        if (workOrders.Count == 0)
        {
            _logger.LogInformation("没有需要刷新的工单");
            return;
        }

        var allWorkOrderIds = workOrders.Select(wo => wo.Id).ToList();
        var orderNos = workOrders.Select(wo => wo.SalesOrderNo).Distinct().ToList();

        // 预加载所有计划数据
        var planData = await LoadPlanDataAsync(allWorkOrderIds);
        var allWorkOrdersInOrders = workOrders; // 已加载全部，直接使用

        // 加载客户数据（从 CustomerProfile 取最新业务员/最终客户）
        var customerByOrderNo = await LoadCustomerByOrderNoAsync(orderNos);

        var summaries = new List<WorkOrderListSummary>();
        var failCount = 0;
        foreach (var wo in workOrders)
        {
            try
            {
                var summary = BuildSummary(wo, planData, allWorkOrdersInOrders, customerByOrderNo);
                summaries.Add(summary);
            }
            catch (Exception ex)
            {
                failCount++;
                _logger.LogWarning(ex, "构建工单 {WorkOrderId}({WorkOrderNo}) 的用料计划总览时失败，已跳过",
                    wo.Id, wo.WorkOrderNo);
            }
        }

        if (summaries.Count > 0)
        {
            var allIds = summaries.Select(s => s.WorkOrderId).ToList();
            await UpsertSummariesAsync(summaries, allIds);
        }
        else
        {
            _logger.LogWarning("全量刷新用料计划总览：所有工单均构建失败");
            return;
        }

        _logger.LogInformation("用料计划总览读模型刷新完成: 共{Count}条, 失败{failCount}个工单", summaries.Count, failCount);
    }

    /// <summary>按工单ID刷新（连带刷新同一 SalesOrder 下所有工单以重算主号/订单聚合）</summary>
    public async Task RefreshByWorkOrderAsync(int workOrderId)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == workOrderId);

        if (workOrder == null || workOrder.Status == WorkOrderStatus.Cancelled)
        {
            var existing = await _context.Set<WorkOrderListSummary>()
                .FirstOrDefaultAsync(s => s.WorkOrderId == workOrderId);
            if (existing != null)
            {
                _context.Set<WorkOrderListSummary>().Remove(existing);
                try { await _context.SaveChangesAsync(); }
                catch (DbUpdateConcurrencyException) { }
            }
            return;
        }

        await RefreshBySalesOrderAsync(workOrder.SalesOrderNo);
    }

    /// <summary>按订单号刷新所有工单的汇总</summary>
    public async Task RefreshBySalesOrderAsync(string salesOrderNo)
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.SalesOrderNo == salesOrderNo && wo.Status != WorkOrderStatus.Cancelled)
            .ToListAsync();

        if (workOrders.Count == 0)
        {
            // 删除该订单下已有的所有汇总
            var existingRecords = await _context.Set<WorkOrderListSummary>()
                .Where(s => s.SalesOrderNo == salesOrderNo)
                .ToListAsync();
            if (existingRecords.Count > 0)
            {
                _context.Set<WorkOrderListSummary>().RemoveRange(existingRecords);
                try { await _context.SaveChangesAsync(); }
                catch (DbUpdateConcurrencyException) { }
            }
            return;
        }

        var orderIds = workOrders.Select(wo => wo.Id).ToList();
        var planData = await LoadPlanDataAsync(orderIds);

        // 需要加载该订单下所有工单（包括已取消的？不，已取消的不参与聚合）
        var allWorkOrdersInOrders = workOrders;

        // 加载客户数据
        var customerByOrderNo = await LoadCustomerByOrderNoAsync(new List<string> { salesOrderNo });

        var summaries = workOrders.Select(wo => BuildSummary(wo, planData, allWorkOrdersInOrders, customerByOrderNo)).ToList();
        await UpsertSummariesAsync(summaries, orderIds);
    }

    /// <summary>按客户刷新</summary>
    public async Task RefreshByCustomerAsync(int customerId)
    {
        var orderNos = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => so.CustomerId == customerId && so.Status == SalesOrderStatus.Confirmed)
            .Select(so => so.OrderNumber)
            .ToListAsync();

        if (orderNos.Count == 0) return;

        foreach (var orderNo in orderNos)
        {
            await RefreshBySalesOrderAsync(orderNo);
        }
    }

    // ========== Private ==========

    private record PlanData(
        List<PurchaseSemiPlan> SemiPlans,
        List<PurchaseFinishedPlan> FinishPlans,
        List<InventoryPlan> InventoryPlans,
        List<RoundBarPiercingPlan> PiercingPlans,
        Dictionary<int, List<PurchaseSemiPlan>> SemiByWo,
        Dictionary<int, List<PurchaseFinishedPlan>> FinishByWo,
        Dictionary<int, List<InventoryPlan>> InventoryByWo,
        Dictionary<int, List<RoundBarPiercingPlan>> PiercingByWo,
        Dictionary<int, decimal> SemiWeightByWo,
        Dictionary<int, int> SemiPiecesByWo,
        Dictionary<int, decimal> FinishWeightByWo,
        Dictionary<int, int> FinishPiecesByWo,
        Dictionary<int, decimal> InventoryWeightByWo,
        Dictionary<int, int> InventoryPiecesByWo,
        Dictionary<int, decimal> ReworkWeightByWo,
        Dictionary<int, int> ReworkPiecesByWo,
        Dictionary<int, decimal> PiercingWeightByWo,
        Dictionary<int, int> PiercingPiecesByWo,
        Dictionary<int, DateTime> LatestDateByWo,
        Dictionary<int, int> MaxCycleByWo);

    private async Task<PlanData> LoadPlanDataAsync(List<int> workOrderIds)
    {
        var semiPlans = await _context.PurchaseSemiPlans
            .Where(p => workOrderIds.Contains(p.WorkOrderId))
            .ToListAsync();

        var finishPlans = await _context.PurchaseFinishedPlans
            .Where(p => workOrderIds.Contains(p.WorkOrderId))
            .ToListAsync();

        var inventoryPlans = await _context.InventoryPlans
            .Where(p => workOrderIds.Contains(p.WorkOrderId) && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .ToListAsync();

        var piercingPlans = await _context.RoundBarPiercingPlans
            .Where(p => workOrderIds.Contains(p.WorkOrderId))
            .ToListAsync();

        // 按工单ID分组
        var semiByWo = semiPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var finishByWo = finishPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var inventoryByWo = inventoryPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());
        var piercingByWo = piercingPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.ToList());

        // 重量/件数汇总
        var semiWeightByWo = semiPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredWeight));
        var semiPiecesByWo = semiPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple));
        var finishWeightByWo = finishPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredWeight));
        var finishPiecesByWo = finishPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredPiece ?? 0));

        var invRegular = inventoryPlans.Where(p => p.ReworkType == null).ToList();
        var invRework = inventoryPlans.Where(p => p.ReworkType != null).ToList();
        var inventoryWeightByWo = invRegular.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.UsedWeight));
        var inventoryPiecesByWo = invRegular.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple));
        var reworkWeightByWo = invRework.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.UsedWeight));
        var reworkPiecesByWo = invRework.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple));

        var piercingWeightByWo = piercingPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.RequiredWeight));
        var piercingPiecesByWo = piercingPlans.GroupBy(p => p.WorkOrderId).ToDictionary(g => g.Key, g => g.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple));

        // 最新计划日期
        var latestDateByWo = new Dictionary<int, DateTime>();

        // 最大工艺周期：取4种计划中 StandardCycle 的最大值
        var maxCycleByWo = new Dictionary<int, int>();
        void MergeMaxCycle(IEnumerable<IGrouping<int, int>> groups)
        {
            foreach (var g in groups)
            {
                var max = g.Max();
                if (maxCycleByWo.TryGetValue(g.Key, out var existing))
                {
                    if (max > existing) maxCycleByWo[g.Key] = max;
                }
                else
                {
                    maxCycleByWo[g.Key] = max;
                }
            }
        }
        MergeMaxCycle(semiPlans.GroupBy(p => p.WorkOrderId, p => p.StandardCycle));
        MergeMaxCycle(finishPlans.GroupBy(p => p.WorkOrderId, p => p.StandardCycle));
        MergeMaxCycle(inventoryPlans.GroupBy(p => p.WorkOrderId, p => p.StandardCycle));
        MergeMaxCycle(piercingPlans.GroupBy(p => p.WorkOrderId, p => p.StandardCycle));
        void MergeMaxDate(IEnumerable<IGrouping<int, DateTime>> groups)
        {
            foreach (var g in groups)
            {
                var max = g.Max();
                if (latestDateByWo.TryGetValue(g.Key, out var existing))
                {
                    if (max > existing) latestDateByWo[g.Key] = max;
                }
                else
                {
                    latestDateByWo[g.Key] = max;
                }
            }
        }
        MergeMaxDate(semiPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));
        MergeMaxDate(finishPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));
        MergeMaxDate(inventoryPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));
        MergeMaxDate(piercingPlans.GroupBy(p => p.WorkOrderId, p => p.PlanDate));

        return new PlanData(
            semiPlans, finishPlans, inventoryPlans, piercingPlans,
            semiByWo, finishByWo, inventoryByWo, piercingByWo,
            semiWeightByWo, semiPiecesByWo,
            finishWeightByWo, finishPiecesByWo,
            inventoryWeightByWo, inventoryPiecesByWo,
            reworkWeightByWo, reworkPiecesByWo,
            piercingWeightByWo, piercingPiecesByWo,
            latestDateByWo,
            maxCycleByWo);
    }

    /// <summary>加载客户数据字典（OrderNumber → (Salesman, EndCustomer)）</summary>
    private async Task<Dictionary<string, (string salesman, string? endCustomer)>> LoadCustomerByOrderNoAsync(List<string> orderNos)
    {
        if (orderNos.Count == 0) return new Dictionary<string, (string, string?)>();

        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => orderNos.Contains(so.OrderNumber))
            .ToListAsync();

        var customerIds = salesOrders.Select(so => so.CustomerId).Distinct().ToList();
        var customers = await _context.CustomerProfiles
            .AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        return salesOrders.ToDictionary(
            so => so.OrderNumber,
            so =>
            {
                customers.TryGetValue(so.CustomerId, out var customer);
                return (customer?.Salesman ?? string.Empty, customer?.EndCustomer);
            });
    }

    private WorkOrderListSummary BuildSummary(
        WorkOrder wo,
        PlanData planData,
        List<WorkOrder> allWorkOrdersInOrder,
        Dictionary<string, (string salesman, string? endCustomer)> customerByOrderNo)
    {
        var woId = wo.Id;

        // 从 CustomerProfile 取最新业务员/最终客户
        customerByOrderNo.TryGetValue(wo.SalesOrderNo, out var customer);
        var salesman = customer.salesman;
        var endCustomer = customer.endCustomer;

        // 计划重量/件数
        planData.SemiWeightByWo.TryGetValue(woId, out var semiW);
        planData.SemiPiecesByWo.TryGetValue(woId, out var semiP);
        planData.FinishWeightByWo.TryGetValue(woId, out var finW);
        planData.FinishPiecesByWo.TryGetValue(woId, out var finP);
        planData.InventoryWeightByWo.TryGetValue(woId, out var invW);
        planData.InventoryPiecesByWo.TryGetValue(woId, out var invP);
        planData.ReworkWeightByWo.TryGetValue(woId, out var rewW);
        planData.ReworkPiecesByWo.TryGetValue(woId, out var rewP);
        planData.PiercingWeightByWo.TryGetValue(woId, out var pW);
        planData.PiercingPiecesByWo.TryGetValue(woId, out var pP);
        planData.LatestDateByWo.TryGetValue(woId, out var latestDate);
        planData.MaxCycleByWo.TryGetValue(woId, out var maxCycle);

        // 满足率
        var semi = planData.SemiByWo.TryGetValue(woId, out var s) ? s : new List<PurchaseSemiPlan>();
        var finish = planData.FinishByWo.TryGetValue(woId, out var f) ? f : new List<PurchaseFinishedPlan>();
        var inv = planData.InventoryByWo.TryGetValue(woId, out var iv) ? iv : new List<InventoryPlan>();
        var pierce = planData.PiercingByWo.TryGetValue(woId, out var p) ? p : new List<RoundBarPiercingPlan>();
        var (rate, status) = PlanRateCalculator.ComputeWorkOrderRate(wo, semi, finish, inv, pierce);

        // 主号级聚合
        var mainNoWorkOrders = allWorkOrdersInOrder
            .Where(w => w.SalesOrderNo == wo.SalesOrderNo && w.ProductionMainNo == wo.ProductionMainNo)
            .ToList();
        var mainNoIds = mainNoWorkOrders.Select(w => w.Id).ToHashSet();
        var (mainNoRate, mainNoStatus) = CalculateMainNoAggregation(
            mainNoWorkOrders,
            planData.SemiPlans.Where(p => mainNoIds.Contains(p.WorkOrderId)).ToList(),
            planData.FinishPlans.Where(p => mainNoIds.Contains(p.WorkOrderId)).ToList(),
            planData.InventoryPlans.Where(p => mainNoIds.Contains(p.WorkOrderId) && p.ReworkType == null).ToList(),
            planData.InventoryPlans.Where(p => mainNoIds.Contains(p.WorkOrderId) && p.ReworkType != null).ToList(),
            planData.PiercingPlans.Where(p => mainNoIds.Contains(p.WorkOrderId)).ToList());

        // 订单级聚合
        var orderMaterialPlanStatus = CalculateOrderMaterialPlanStatus(allWorkOrdersInOrder, planData, wo.SalesOrderNo);

        return new WorkOrderListSummary
        {
            WorkOrderId = wo.Id,
            WorkOrderNo = wo.WorkOrderNo,
            SalesOrderNo = wo.SalesOrderNo,
            ProductionMainNo = wo.ProductionMainNo,
            ProductionSubNo = wo.ProductionSubNo,
            OrderItemIds = wo.OrderItemIds,
            SignDate = wo.SignDate,
            Salesman = salesman ?? wo.Salesman,
            EndCustomer = endCustomer ?? wo.EndCustomer,
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
            LatestPlanDate = latestDate,
            MaterialPlanRate = rate,
            MaterialPlanStatus = status,
            SemiPlanTotalWeight = semiW != 0 ? semiW : null,
            SemiPlanTotalPieces = semiP != 0 ? semiP : null,
            FinishedPlanTotalWeight = finW != 0 ? finW : null,
            FinishedPlanTotalPieces = finP != 0 ? finP : null,
            InventoryPlanTotalWeight = invW != 0 ? invW : null,
            InventoryPlanTotalPieces = invP != 0 ? invP : null,
            ReworkPlanTotalWeight = rewW != 0 ? rewW : null,
            ReworkPlanTotalPieces = rewP != 0 ? rewP : null,
            PiercingPlanTotalWeight = pW != 0 ? pW : null,
            PiercingPlanTotalPieces = pP != 0 ? pP : null,
            MainNoMaterialPlanRate = mainNoRate,
            MainNoMaterialPlanStatus = (int)mainNoStatus,
            OrderMaterialPlanStatus = (int)orderMaterialPlanStatus,
            RowVersion = null,
            MaxStandardCycle = maxCycle,
            LastRefreshTime = DateTime.Now
        };
    }

    private static (decimal rate, MaterialPlanStatus status) CalculateMainNoAggregation(
        List<WorkOrder> workOrders,
        List<PurchaseSemiPlan> semiPlans,
        List<PurchaseFinishedPlan> finishPlans,
        List<InventoryPlan> inventoryPlans,
        List<InventoryPlan> reworkPlans,
        List<RoundBarPiercingPlan> piercingPlans)
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
            totalEffective += fixedFinish.Sum(p => p.RequiredPiece ?? 0) * 1.02m;
            totalEffective += (int)(fixedInventory.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple) * 1.02m);
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
            totalEffective += nonFixedFinish.Sum(p => p.RequiredWeight) * 1.05m;
            totalEffective += nonFixedInventory.Sum(p => p.UsedWeight) * 1.05m;
            totalEffective += nonFixedRework.Sum(p => p.UsedWeight);
            totalEffective += nonFixedPiercing.Sum(p => p.RequiredWeight);
        }

        if (totalDemand <= 0) return (0, MaterialPlanStatus.NotPlanned);

        var rate = Math.Round(totalEffective / totalDemand * 100m, 0);

        if (rate <= 0) return (0, MaterialPlanStatus.NotPlanned);

        var fixedTotalQuantity = fixedOrders.Sum(wo => wo.TotalQuantity);
        if (fixedTotalQuantity > 0 && fixedTotalQuantity <= 20)
        {
            var batchStatus = rate >= 100m ? MaterialPlanStatus.Satisfied : MaterialPlanStatus.Partial;
            return (rate, batchStatus);
        }

        var status = CalculateMainNoStatus(rate, fixedOrders.Any());
        return (rate, status);
    }

    private static MaterialPlanStatus CalculateMainNoStatus(decimal rate, bool isFixed)
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

    private static int CalculateOrderMaterialPlanStatus(
        List<WorkOrder> allWorkOrdersInOrder,
        PlanData planData,
        string salesOrderNo)
    {
        var orderWorkOrders = allWorkOrdersInOrder
            .Where(wo => wo.SalesOrderNo == salesOrderNo)
            .ToList();

        var mainNoGroups = orderWorkOrders
            .GroupBy(wo => wo.ProductionMainNo)
            .ToList();

        var mainNoStatuses = new List<MaterialPlanStatus>();
        foreach (var group in mainNoGroups)
        {
            var groupList = group.ToList();
            var groupIds = groupList.Select(w => w.Id).ToHashSet();
            var (_, mainNoStatus) = CalculateMainNoAggregation(
                groupList,
                planData.SemiPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList(),
                planData.FinishPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList(),
                planData.InventoryPlans.Where(p => groupIds.Contains(p.WorkOrderId) && p.ReworkType == null).ToList(),
                planData.InventoryPlans.Where(p => groupIds.Contains(p.WorkOrderId) && p.ReworkType != null).ToList(),
                planData.PiercingPlans.Where(p => groupIds.Contains(p.WorkOrderId)).ToList());
            mainNoStatuses.Add(mainNoStatus);
        }

        if (mainNoStatuses.Count == 0) return (int)MaterialPlanStatus.NotPlanned;

        var allNotPlanned = mainNoStatuses.All(s => s == MaterialPlanStatus.NotPlanned);
        var hasPartialOrNotPlanned = mainNoStatuses.Any(s =>
            s == MaterialPlanStatus.Partial || s == MaterialPlanStatus.NotPlanned);

        if (allNotPlanned) return (int)MaterialPlanStatus.NotPlanned;
        if (hasPartialOrNotPlanned) return (int)MaterialPlanStatus.Partial;
        return (int)MaterialPlanStatus.Satisfied;
    }

    private async Task UpsertSummariesAsync(List<WorkOrderListSummary> summaries, List<int> workOrderIds)
    {
        var existingRecords = await _context.Set<WorkOrderListSummary>()
            .Where(s => workOrderIds.Contains(s.WorkOrderId))
            .ToListAsync();

        var existingByWoId = existingRecords.ToDictionary(e => e.WorkOrderId);

        foreach (var summary in summaries)
        {
            if (existingByWoId.TryGetValue(summary.WorkOrderId, out var existing))
            {
                existing.WorkOrderId = summary.WorkOrderId;
                existing.WorkOrderNo = summary.WorkOrderNo;
                existing.SalesOrderNo = summary.SalesOrderNo;
                existing.ProductionMainNo = summary.ProductionMainNo;
                existing.ProductionSubNo = summary.ProductionSubNo;
                existing.OrderItemIds = summary.OrderItemIds;
                existing.SignDate = summary.SignDate;
                existing.Salesman = summary.Salesman;
                existing.EndCustomer = summary.EndCustomer;
                existing.DeliveryDate = summary.DeliveryDate;
                existing.DelayPenalty = summary.DelayPenalty;
                existing.SettlementMethod = summary.SettlementMethod;
                existing.MaterialName = summary.MaterialName;
                existing.StandardCode = summary.StandardCode;
                existing.DeliveryState = summary.DeliveryState;
                existing.PlantGrade = summary.PlantGrade;
                existing.Specification = summary.Specification;
                existing.OuterDiameterNegative = summary.OuterDiameterNegative;
                existing.OuterDiameterPositive = summary.OuterDiameterPositive;
                existing.WallThicknessNegative = summary.WallThicknessNegative;
                existing.WallThicknessPositive = summary.WallThicknessPositive;
                existing.LengthStatus = summary.LengthStatus;
                existing.MinLength = summary.MinLength;
                existing.MaxLength = summary.MaxLength;
                existing.TotalQuantity = summary.TotalQuantity;
                existing.TotalMeters = summary.TotalMeters;
                existing.TotalWeight = summary.TotalWeight;
                existing.TotalItemCount = summary.TotalItemCount;
                existing.ItemDetails = summary.ItemDetails;
                existing.TechnicalRequirements = summary.TechnicalRequirements;
                existing.Status = summary.Status;
                existing.CreatedTime = summary.CreatedTime;
                existing.LatestPlanDate = summary.LatestPlanDate;
                existing.MaterialPlanRate = summary.MaterialPlanRate;
                existing.MaterialPlanStatus = summary.MaterialPlanStatus;
                existing.SemiPlanTotalWeight = summary.SemiPlanTotalWeight;
                existing.SemiPlanTotalPieces = summary.SemiPlanTotalPieces;
                existing.FinishedPlanTotalWeight = summary.FinishedPlanTotalWeight;
                existing.FinishedPlanTotalPieces = summary.FinishedPlanTotalPieces;
                existing.InventoryPlanTotalWeight = summary.InventoryPlanTotalWeight;
                existing.InventoryPlanTotalPieces = summary.InventoryPlanTotalPieces;
                existing.ReworkPlanTotalWeight = summary.ReworkPlanTotalWeight;
                existing.ReworkPlanTotalPieces = summary.ReworkPlanTotalPieces;
                existing.PiercingPlanTotalWeight = summary.PiercingPlanTotalWeight;
                existing.PiercingPlanTotalPieces = summary.PiercingPlanTotalPieces;
                existing.MainNoMaterialPlanRate = summary.MainNoMaterialPlanRate;
                existing.MainNoMaterialPlanStatus = summary.MainNoMaterialPlanStatus;
                existing.OrderMaterialPlanStatus = summary.OrderMaterialPlanStatus;
                existing.MaxStandardCycle = summary.MaxStandardCycle;
                existing.LastRefreshTime = summary.LastRefreshTime;
            }
            else
            {
                _context.Set<WorkOrderListSummary>().Add(summary);
            }
        }

        var validIds = summaries.Select(s => s.WorkOrderId).ToHashSet();
        var toDelete = existingRecords.Where(e => !validIds.Contains(e.WorkOrderId)).ToList();
        if (toDelete.Count > 0)
        {
            _context.Set<WorkOrderListSummary>().RemoveRange(toDelete);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "刷新 WorkOrderListSummary 时发生并发冲突，已忽略");
        }
    }
}
