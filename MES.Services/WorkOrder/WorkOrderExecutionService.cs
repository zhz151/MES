using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Order;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.WorkOrder;
using MES.Services.Helpers;
using MES.Services.Printing;
using WoEntity = MES.Data.Entities.WorkOrder.WorkOrder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace MES.Services.WorkOrder;

/// <summary>
/// 工单执行状况服务（只读查询 + 手动刷新）
/// </summary>
public class WorkOrderExecutionService : IWorkOrderExecutionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkOrderExecutionService> _logger;
    private readonly IConfigParameterService _configService;
    private readonly IDailyOutputEstimateService _dailyOutputService;
    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public WorkOrderExecutionService(AppDbContext context, ILogger<WorkOrderExecutionService> logger,
        IConfigParameterService configService,
        IDailyOutputEstimateService dailyOutputService,
        IMemoryCache cache,
        IServiceScopeFactory serviceScopeFactory)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
        _dailyOutputService = dailyOutputService;
        _cache = cache;
        _serviceScopeFactory = serviceScopeFactory;
    }

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        if (!_configMaps.TryGetValue(category, out var map))
        {
            map = await _configService.GetConfigMapAsync(category);
            _configMaps[category] = map;
        }
        return map.GetValueOrDefault(key, defaultValue);
    }

    public async Task<PagedResult<WorkOrderExecutionSummaryDto>> GetPagedAsync(QueryParams query, DateTime? signDateFrom = null, DateTime? signDateTo = null)
    {
        var q = _context.Set<WorkOrderExecutionSummary>().AsQueryable();

        // 签订日期范围筛选
        if (signDateFrom.HasValue)
            q = q.Where(x => x.SignDate >= signDateFrom.Value);
        if (signDateTo.HasValue)
            q = q.Where(x => x.SignDate < signDateTo.Value.AddDays(1));

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
                (x.ProductionSubNo != null && x.ProductionSubNo.Contains(kw)) ||
                (x.UrgencyLevel != null && x.UrgencyLevel.Contains(kw)) ||
                (x.RawMaterialLockRemark != null && x.RawMaterialLockRemark.Contains(kw)) ||
                (x.ProductionAttentionProcess != null && x.ProductionAttentionProcess.Contains(kw)) ||
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw)) ||
                (x.ProductionFlowProperty != null && x.ProductionFlowProperty.Contains(kw)) ||
                (x.MainNoAttentionProcess != null && x.MainNoAttentionProcess.Contains(kw)));
        }

        // 排序
        q = q.ApplyFilters(query.Filters);

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

                // Group 2
                LatestPlanDate = e.LatestPlanDate,
                MaterialPlanRate = e.MaterialPlanRate,
                MaterialPlanStatus = (MaterialPlanStatus)e.MaterialPlanStatus,
                MainNoMaterialPlanRate = e.MainNoMaterialPlanRate,
                MainNoMaterialPlanStatus = (MaterialPlanStatus)e.MainNoMaterialPlanStatus,
                ProcessCycle = e.ProcessCycle,
                MaterialPlanCoveredCount = e.MaterialPlanCoveredCount,
                MaterialPlanProportion = e.MaterialPlanProportion,
                LatestRequiredDate = e.LatestRequiredDate,

                // Group 5
                PendingRoughTubeQty = e.PendingRoughTubeQty,
                PendingRoughTubeWeight = e.PendingRoughTubeWeight,
                PendingOutsourceFinishQty = e.PendingOutsourceFinishQty,
                PendingOutsourceFinishWeight = e.PendingOutsourceFinishWeight,
                TheoreticalFinishQty = e.TheoreticalFinishQty,
                TheoreticalFinishWeight = e.TheoreticalFinishWeight,

                // Group 6
                ReworkInputEndDate = e.ReworkInputEndDate,
                ReworkBatchCount = e.ReworkBatchCount,
                ReworkInputQuantity = e.ReworkInputQuantity,
                ReworkInputWeight = e.ReworkInputWeight,
                ReworkTheoreticalOutputQty = e.ReworkTheoreticalOutputQty,
                ReworkTheoreticalOutputWeight = e.ReworkTheoreticalOutputWeight,

                // Group 7
                FlowOutputRatio = e.FlowOutputRatio,
                FlowStatus = e.FlowStatus,
                MainNoFlowOutputRatio = e.MainNoFlowOutputRatio,
                MainNoFlowStatus = e.MainNoFlowStatus,
                FlowTotalBatchCount = e.FlowTotalBatchCount,
                FlowIncompleteBatchCount = e.FlowIncompleteBatchCount,
                FlowMaxRemainingWorkDays = e.FlowMaxRemainingWorkDays,

                // Group 8
                DefectiveRawQty = e.DefectiveRawQty,
                DefectiveRawWeight = e.DefectiveRawWeight,
                DefectiveOutputQty = e.DefectiveOutputQty,
                DefectiveOutputWeight = e.DefectiveOutputWeight,
                DefectiveRatio = e.DefectiveRatio,

                // Group 9
                InspectionDefectQty = e.InspectionDefectQty,
                InspectionDefectWeight = e.InspectionDefectWeight,
                InspectionDefectRatio = e.InspectionDefectRatio,
                InspectionStartDate = e.InspectionStartDate,
                InspectionEndDate = e.InspectionEndDate,

                // Group 10
                GeneralDefectWeight = e.GeneralDefectWeight,
                GeneralDefectRatio = e.GeneralDefectRatio,
                SeriousDefectWeight = e.SeriousDefectWeight,
                SeriousDefectRatio = e.SeriousDefectRatio,
                ScrapWeight = e.ScrapWeight,
                ScrapRatio = e.ScrapRatio,

                // Group 11
                WarehousingStartDate = e.WarehousingStartDate,
                WarehousingEndDate = e.WarehousingEndDate,
                WarehousingTotalQty = e.WarehousingTotalQty,
                WarehousingTotalWeight = e.WarehousingTotalWeight,
                WoWarehousingStatus = e.WoWarehousingStatus,
                MainNoWarehousingStatus = e.MainNoWarehousingStatus,
                OrderWarehousingStatus = e.OrderWarehousingStatus,
                ScheduleStage = e.ScheduleStage,
                TotalRemainingWorkDays = e.TotalRemainingWorkDays,
                CapacityWorkDays = e.CapacityWorkDays,
                UrgencyLevel = e.UrgencyLevel,
                EstimatedProcessCompletionDate = e.EstimatedProcessCompletionDate,
                DaysDiffFromDelivery = e.DaysDiffFromDelivery,
                RawMaterialLockRemark = e.RawMaterialLockRemark,

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

                // Group 14
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
                MaxBatchRemainingWorkDays = e.MaxBatchRemainingWorkDays,
                MainNoAttentionProcess = e.MainNoAttentionProcess,

                // Group 13
                IsUrging = e.IsUrging,
                IsBatchDelivery = e.IsBatchDelivery,
                IsPaused = e.IsPaused,
                AdjustmentRemark = e.AdjustmentRemark,
                ProductionFlowProperty = e.ProductionFlowProperty,
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

        // ===== 加载配置参数（替换硬编码常量） =====
        var warehouseConfig = await _configService.GetConfigMapAsync("WarehouseThreshold");
        var workOrderDaysConfig = await _configService.GetConfigMapAsync("WorkOrderDays");
        var urgencyConfig = await _configService.GetConfigMapAsync("UrgencyThreshold");
        var processingDiscountConfig = await _configService.GetConfigMapAsync("ProcessingDiscount");
        var materialPlanStatusConfig = await _configService.GetConfigMapAsync("MaterialPlanStatus");

        var completeRatio = warehouseConfig.GetValueOrDefault("CompleteRatio", 0.95m);
        var completeDeviation = warehouseConfig.GetValueOrDefault("CompleteDeviation", 100m);
        var bufferDays = workOrderDaysConfig.GetValueOrDefault("BufferDays", 3m);
        var inspectionFixedDays = workOrderDaysConfig.GetValueOrDefault("InspectionFixedDays", 3m);
        var urgencyAPlus = urgencyConfig.GetValueOrDefault("APlus", 7m);
        var urgencyA = urgencyConfig.GetValueOrDefault("A", -3m);
        var urgencyB = urgencyConfig.GetValueOrDefault("B", -10m);
        var urgencyC = urgencyConfig.GetValueOrDefault("C", -17m);
        var groupDiscountRate = processingDiscountConfig.GetValueOrDefault("GroupDiscountRate", 0.025m);
        var supplySatisfiedRate = materialPlanStatusConfig.GetValueOrDefault("SupplySatisfiedRate", 100m);
        var fixedPartial = materialPlanStatusConfig.GetValueOrDefault("FixedPartial", 102m);
        var fixedSatisfied = materialPlanStatusConfig.GetValueOrDefault("FixedSatisfied", 110m);
        var nonFixedPartial = materialPlanStatusConfig.GetValueOrDefault("NonFixedPartial", 105m);
        var nonFixedSatisfied = materialPlanStatusConfig.GetValueOrDefault("NonFixedSatisfied", 120m);
        var defaultValueConfig = await _configService.GetConfigMapAsync("DefaultValue");
        var roughTubeFinishRatio = defaultValueConfig.GetValueOrDefault("RoughTubeFinishRatio", 0.92m);

        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.Status != Core.Enums.WorkOrderStatus.NotGenerated)
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

        // 按 WorkOrderNo 分组（OrdinalIgnoreCase 防止 SQL 大小写不敏感与内存查找不一致）
        var batchesByWo = batches
            .GroupBy(b => b.WorkOrderNo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 构建客户名称字典（直接从 SalesOrder 快照字段读取）
        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => workOrders.Select(w => w.SalesOrderNo).Contains(so.OrderNumber))
            .ToListAsync();

        var customerNameByWo = new Dictionary<int, string>();
        var customerSalesmanByWo = new Dictionary<int, string>();
        foreach (var wo in workOrders)
        {
            var so = salesOrders.FirstOrDefault(s => s.OrderNumber.Equals(wo.SalesOrderNo, StringComparison.OrdinalIgnoreCase));
            customerNameByWo[wo.Id] = so?.CustomerName ?? "";
            customerSalesmanByWo[wo.Id] = so?.Salesman ?? "";
        }

        // 批量加载采购订单（用于 Group 5 物料执行实时信息）
        var purchaseOrders = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.SourceWorkOrderNo != null
                      && workOrderNos.Contains(po.SourceWorkOrderNo)
                      && po.Status != Core.Enums.PurchaseOrderStatus.Completed)
            .ToListAsync();

        var poByWoNo = purchaseOrders
            .GroupBy(po => po.SourceWorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 批量加载委外回收明细（用于 Group 5 物料执行实时信息，与采购订单逻辑相同）
        var returnItems = await _context.SubcontractReturnItems
            .AsNoTracking()
            .Where(ri => ri.SourceWorkOrderNo != null
                      && workOrderNos.Contains(ri.SourceWorkOrderNo))
            .ToListAsync();

        var riByWoNo = returnItems
            .GroupBy(ri => ri.SourceWorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 批量加载用料计划总览读模型（G2 字段已预计算，直接读取避免重算）
        var workOrderListSummaries = await _context.Set<WorkOrderListSummary>()
            .AsNoTracking()
            .Where(s => workOrderIds.Contains(s.WorkOrderId))
            .ToListAsync();
        var execSummaryByWoId = workOrderListSummaries.ToDictionary(s => s.WorkOrderId);

        // 批量加载成品检验数据（用于 Group 9 成检不合格，仅 "订单成品" 物料）
        var finalInspections = await _context.FinalInspections
            .AsNoTracking()
            .Where(fi => fi.MaterialName == "订单成品"
                      && fi.WorkOrderNo != null
                      && workOrderNos.Contains(fi.WorkOrderNo))
            .ToListAsync();
        var fiByWoNo = finalInspections
            .GroupBy(fi => fi.WorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 批量加载成品入库数据（InventoryBatch，用于 Group 11 成品入库）
        // MaterialType 存储枚举名 "OrderFinished"
        var inventoryBatches = await _context.InventoryBatches
            .AsNoTracking()
            .Where(ib => ib.MaterialType == InventoryMaterialTypes.OrderFinished
                      && ib.WorkOrderNo != null
                      && workOrderNos.Contains(ib.WorkOrderNo))
            .ToListAsync();
        var ibByWoNo = inventoryBatches
            .GroupBy(ib => ib.WorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var now = DateTime.Now;
        var summaries = new List<WorkOrderExecutionSummary>();

        foreach (var wo in workOrders)
        {
            var woBatches = batchesByWo.TryGetValue(wo.WorkOrderNo, out var b) ? b : new List<ProductionBatch>();

            // 聚合成品检验数据（用于 G9/G10）
            fiByWoNo.TryGetValue(wo.WorkOrderNo, out var woFiList);
            var totalInspectionQty = woFiList?.Sum(fi => fi.Quantity ?? 0) ?? 0;
            var totalQualifiedQty = woFiList?.Sum(fi => fi.QualifiedQuantity ?? 0) ?? 0;
            var totalScrapQty = woFiList?.Sum(fi => fi.DefectScrapQuantity ?? 0) ?? 0;
            var fiDates = woFiList?.Select(fi => fi.InspectionDate).ToList();
            var inspectionStartDate = fiDates?.Count > 0 ? fiDates.Min() : (DateTime?)null;
            var inspectionEndDate = fiDates?.Count > 0 ? fiDates.Max() : (DateTime?)null;

            var summary = ComputeSummary(wo, customerNameByWo.TryGetValue(wo.Id, out var cn) ? cn : "", customerSalesmanByWo.TryGetValue(wo.Id, out var sm) ? sm : "", woBatches, totalInspectionQty, totalQualifiedQty, totalScrapQty, inspectionStartDate, inspectionEndDate, completeRatio, completeDeviation, groupDiscountRate, supplySatisfiedRate);

            // G2: 从用料计划总览读预计算值（避免重算 4 张原始计划表）
            if (execSummaryByWoId.TryGetValue(wo.Id, out var listSummary))
            {
                summary.LatestPlanDate = listSummary.LatestPlanDate;
                summary.MaterialPlanRate = listSummary.MaterialPlanRate;
                summary.MaterialPlanStatus = listSummary.MaterialPlanStatus;
                summary.MainNoMaterialPlanRate = listSummary.MainNoMaterialPlanRate;
                summary.MainNoMaterialPlanStatus = listSummary.MainNoMaterialPlanStatus;
                summary.ProcessCycle = listSummary.MaxStandardCycle;
                summary.MaterialPlanCoveredCount = listSummary.MaterialPlanCoveredCount;
                summary.MaterialPlanProportion = listSummary.MaterialPlanProportion;
                summary.LatestRequiredDate = listSummary.LatestRequiredDate;
            }

            // Group 5: 物料执行实时信息（从采购订单 + 委外回收明细聚合）
            poByWoNo.TryGetValue(wo.WorkOrderNo, out var woPos);
            riByWoNo.TryGetValue(wo.WorkOrderNo, out var woRis);
            if ((woPos?.Count ?? 0) > 0 || (woRis?.Count ?? 0) > 0)
            {
                var safePos = woPos ?? new List<PurchaseOrder>();
                var safeRis = woRis ?? new List<SubcontractReturnItem>();

                // 荒管组：荒管 + 半成品（数据库存储中文值）
                var roughTubePos = safePos.Where(po =>
                    po.MaterialCategory == "RoughTube" || po.MaterialCategory == "SemiFinished").ToList();
                var roughTubeRis = safeRis.Where(ri =>
                    ri.MaterialCategory == "RoughTube" || ri.MaterialCategory == "SemiFinished").ToList();
                summary.PendingRoughTubeQty = roughTubePos.Sum(po => (po.Quantity ?? 0) - po.ReceivedQuantity)
                    + roughTubeRis.Sum(ri => (ri.RequiredQuantity ?? 0) - ri.ReturnedQuantity);
                summary.PendingRoughTubeWeight = roughTubePos.Sum(po => po.Weight - po.ReceivedWeight)
                    + roughTubeRis.Sum(ri => (ri.RequiredWeight ?? 0) - ri.ReturnedWeight);

                // 外购成组：临界成品 + 订单成品（数据库存储中文值）
                var finishPos = safePos.Where(po =>
                    po.MaterialCategory == "CriticalFinished" || po.MaterialCategory == "OrderFinished").ToList();
                var finishRis = safeRis.Where(ri =>
                    ri.MaterialCategory == "CriticalFinished" || ri.MaterialCategory == "OrderFinished").ToList();
                summary.PendingOutsourceFinishQty = finishPos.Sum(po => (po.Quantity ?? 0) - po.ReceivedQuantity)
                    + finishRis.Sum(ri => (ri.RequiredQuantity ?? 0) - ri.ReturnedQuantity);
                summary.PendingOutsourceFinishWeight = finishPos.Sum(po => po.Weight - po.ReceivedWeight)
                    + finishRis.Sum(ri => (ri.RequiredWeight ?? 0) - ri.ReturnedWeight);

                // 理论成品支：Σ(每笔待回收支 × 投料倍率)
                summary.TheoreticalFinishQty = roughTubePos.Concat(finishPos)
                    .Sum(po =>
                    {
                        var pendingQty = (po.Quantity ?? 0) - po.ReceivedQuantity;
                        return pendingQty * (po.InputMultiple ?? 1);
                    })
                    + roughTubeRis.Concat(finishRis)
                    .Sum(ri =>
                    {
                        var pendingQty = (ri.RequiredQuantity ?? 0) - ri.ReturnedQuantity;
                        return pendingQty * (ri.InputMultiple ?? 1);
                    });

                // 理论成品重：待回荒管重量 × 荒管转成品系数 + 待回外购成重
                summary.TheoreticalFinishWeight = Math.Round(
                    summary.PendingRoughTubeWeight * roughTubeFinishRatio + summary.PendingOutsourceFinishWeight, 2);
            }

            // ========== Group 11: 成品入库（从 InventoryBatch 聚合） ==========
            ibByWoNo.TryGetValue(wo.WorkOrderNo, out var woIbList);
            if (woIbList?.Count > 0)
            {
                summary.WarehousingStartDate = woIbList.Min(ib => ib.InboundDate);
                summary.WarehousingEndDate = woIbList.Max(ib => ib.InboundDate);
                summary.WarehousingTotalQty = woIbList.Sum(ib => ib.InitialQuantity);
                summary.WarehousingTotalWeight = woIbList.Sum(ib => ib.InitialWeight);

                // 工单入库状态
                var isFixed = summary.LengthStatus == LengthStatus.Fixed.ToString();
                bool isComplete;
                if (isFixed)
                    isComplete = summary.WarehousingTotalQty >= wo.TotalQuantity;
                else
                    isComplete = summary.WarehousingTotalWeight >= wo.TotalWeight * completeRatio
                              && summary.WarehousingTotalWeight >= wo.TotalWeight - completeDeviation;

                summary.WoWarehousingStatus = (summary.WarehousingTotalQty == 0 && summary.WarehousingTotalWeight == 0)
                    ? 0  // 无入库
                    : isComplete
                        ? 2  // 入库完结
                        : 1; // 入库部分
            }
            else
            {
                summary.WarehousingTotalQty = 0;
                summary.WarehousingTotalWeight = 0;
                summary.WoWarehousingStatus = 0; // 无入库
            }

            summary.LastRefreshTime = now;
            summaries.Add(summary);
        }

        // 主号级 ProcessCycle 兜底：同主号所有工单均无计划时默认 25 天
        var mainNoProcessCycleGroups = summaries
            .GroupBy(s => new { s.SalesOrderNo, s.ProductionMainNo })
            .ToList();
        foreach (var group in mainNoProcessCycleGroups)
        {
            if (group.All(s => s.ProcessCycle == 0))
            {
                foreach (var s in group)
                    s.ProcessCycle = 22;
            }
        }

        // 计算主号级投料聚合
        ComputeMainNoInputAggregation(summaries, workOrders, supplySatisfiedRate);

        // 计算主号/订单级入库状态聚合
        ComputeWarehousingAggregation(summaries, workOrders);

        // ========== G12: 计算关注状态 ==========
        foreach (var summary in summaries)
        {
            if (summary.WoWarehousingStatus == 2)
                summary.ScheduleStage = 0;          // 工单完成
            else if (summary.MainNoFlowStatus != 2)
                summary.ScheduleStage = 1;          // 原料锁定
            else if (summary.FlowIncompleteBatchCount > 0)
                summary.ScheduleStage = 2;          // 生产执行
            else
                summary.ScheduleStage = 3;          // 成品检验
        }

        // ProductionAttentionProcess 兜底调整：仅 ScheduleStage==2 时显示"收尾-成检"，其余保持 null
        foreach (var summary in summaries)
        {
            if (summary.ProductionAttentionProcess == null && summary.ScheduleStage == 2)
                summary.ProductionAttentionProcess = "收尾-成检";
        }

        // MainNoAttentionProcess: 同(订单号+主号)下，取剩余工量最大值所在工单的生产关注工序
        var mainNoAttentionMap = summaries
            .Where(s => s.MaxBatchRemainingWorkDays.HasValue && s.ProductionAttentionProcess != null)
            .GroupBy(s => new { s.SalesOrderNo, s.ProductionMainNo })
            .ToDictionary(
                g => (g.Key.SalesOrderNo, g.Key.ProductionMainNo),
                g => g.OrderByDescending(s => s.MaxBatchRemainingWorkDays)
                      .First().ProductionAttentionProcess);
        foreach (var summary in summaries)
        {
            var key = (summary.SalesOrderNo, summary.ProductionMainNo);
            summary.MainNoAttentionProcess = mainNoAttentionMap.GetValueOrDefault(key);
        }

        // ========== G12: 加载暂停工单数据 ==========
        var pausedIdList = await _context.Set<OrderDemandAdjustment>()
            .AsNoTracking()
            .Where(u => workOrderIds.Contains(u.WorkOrderId) && u.IsPaused)
            .Select(u => u.WorkOrderId)
            .ToListAsync();

        var pausedIds = pausedIdList.ToHashSet();

        // ========== G13: 加载工单需求调整数据 ==========
        var adjustments = await _context.Set<OrderDemandAdjustment>()
            .AsNoTracking()
            .Where(a => workOrderIds.Contains(a.WorkOrderId))
            .ToDictionaryAsync(a => a.WorkOrderId);

        // ========== 填充 G13 字段 ==========
        foreach (var summary in summaries)
        {
            if (adjustments.TryGetValue(summary.WorkOrderId, out var adj))
            {
                summary.IsUrging = adj.IsUrging;
                summary.IsBatchDelivery = adj.IsBatchDelivery;
                summary.IsPaused = adj.IsPaused;
                summary.AdjustmentRemark = adj.AdjustmentRemark;
            }
            else
            {
                summary.IsUrging = false;
                summary.IsBatchDelivery = false;
                summary.IsPaused = false;
                summary.AdjustmentRemark = null;
            }
        }

        // 主号级聚合：主号下任一工单催单/分批交货/暂停，整主号标记（用于生产流转性判定）
        var mainNoUrgencyFlags = summaries
            .GroupBy(s => new { s.SalesOrderNo, s.ProductionMainNo })
            .ToDictionary(
                g => g.Key,
                g => new { MainNoUrging = g.Any(s => s.IsUrging), MainNoBatchDelivery = g.Any(s => s.IsBatchDelivery), MainNoPaused = g.Any(s => s.IsPaused) });

        // ========== 计算生产流转性 ==========
        foreach (var summary in summaries)
        {
            var flags = mainNoUrgencyFlags[new { summary.SalesOrderNo, summary.ProductionMainNo }];

            if (flags.MainNoPaused)
                summary.ProductionFlowProperty = "暂停";
            else if (summary.ScheduleStage == 2 || (summary.ScheduleStage == 1 && (flags.MainNoUrging || flags.MainNoBatchDelivery)))
                summary.ProductionFlowProperty = "正常";
            else if (summary.ScheduleStage == 1)
                summary.ProductionFlowProperty = "待料";
            else if (summary.ScheduleStage == 0 || summary.ScheduleStage == 3)
                summary.ProductionFlowProperty = summary.FlowIncompleteBatchCount == 0 ? "略" : "疑问";
            else
                summary.ProductionFlowProperty = null;
        }

        // ========== G12: 计算剩余总工量 & 工单计划性 ==========
        var mainNoAgg = summaries
            .GroupBy(s => new { s.SalesOrderNo, s.ProductionMainNo })
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    MaxProcessCycle = g.Max(s => s.ProcessCycle),
                    MaxRemainingDays = g.Max(s => s.FlowMaxRemainingWorkDays),
                    MainNoTotalWeight = g.Sum(s => s.TotalWeight)
                });

        // 加载日产估算配置
        var dailyEstimates = await _dailyOutputService.GetAllAsync();

        // 计算已完成批次（Status=Completed）的有效成品重量，用于产能工量扣减
        // 公式：Σ(现有效原料重量 × (1 - 有效工序组数 × 2.5%))
        var completedBatchOutputByMainNo = batchesByWo.Values
            .SelectMany(b => b)
            .Where(b => b.Status == BatchStatus.Completed
                     && b.ProductionType != "Rework"
                     && b.ManufacturingItem == "OrderFinished")
            .GroupBy(b => new { b.SalesOrderNo, b.ProductionMainNo })
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    decimal total = 0;
                    foreach (var batch in g)
                    {
                        var inputWeight = batch.CurrentValidWeight ?? 0m;
                        var effectiveGroups = batch.ProcessGroups?
                            .Count(pg => HasAnySection(pg)) ?? 0;
                        var discount = 1.0m - effectiveGroups * groupDiscountRate;
                        if (discount < 0) discount = 0;
                        total += Math.Round(inputWeight * discount, 3);
                    }
                    return Math.Round(total, 3);
                });

        foreach (var summary in summaries)
        {
            if (summary.ScheduleStage == 0)
            {
                summary.TotalRemainingWorkDays = null;
                summary.CapacityWorkDays = null;
                summary.UrgencyLevel = null;
                continue;
            }

            // 查找主号级聚合值
            var key = new { summary.SalesOrderNo, summary.ProductionMainNo };
            mainNoAgg.TryGetValue(key, out var agg);

            // 工艺剩余总工量
            summary.TotalRemainingWorkDays = summary.ScheduleStage switch
            {
                1 => (agg?.MaxProcessCycle ?? 0) + (int)bufferDays,
                2 => agg?.MaxRemainingDays ?? 0,
                3 => (int)inspectionFixedDays,
                _ => null
            };

            // 产能工量 = 剩余成品重量(kg) / 1000 / 日产估算(吨/天)
            // 剩余成品重量 = 主号计划成品总量 - 已完成批次的有效成品重量
            if (summary.TotalRemainingWorkDays.HasValue && agg!.MainNoTotalWeight > 0)
            {
                var completedOutput = completedBatchOutputByMainNo.TryGetValue(key, out var co) ? co : 0m;
                var remainingWeight = agg.MainNoTotalWeight - completedOutput;
                if (remainingWeight <= 0)
                {
                    summary.CapacityWorkDays = 0;
                }
                else
                {
                    var od = ParseOuterDiameter(summary.Specification);
                    if (od.HasValue)
                    {
                        var match = dailyEstimates
                            .Where(e => e.MinOuterDiameter <= od.Value)
                            .OrderByDescending(e => e.MinOuterDiameter)
                            .FirstOrDefault();
                        if (match != null && match.DailyOutputTons > 0)
                        {
                            var totalTons = remainingWeight / 1000m;
                            summary.CapacityWorkDays = (int)Math.Ceiling(totalTons / match.DailyOutputTons);
                        }
                    }
                }
            }

            // 总工量 = 工艺剩余总工量 + 产能工量
            var totalDays = (summary.TotalRemainingWorkDays ?? 0) + (summary.CapacityWorkDays ?? 0);

            // 工单计划性
            var todayDays = DateOnly.FromDateTime(DateTime.Today).DayNumber;
            var deliveryDays = DateOnly.FromDateTime(summary.DeliveryDate).DayNumber;
            var diff = totalDays + todayDays - deliveryDays;

            summary.UrgencyLevel = diff > urgencyAPlus ? "A+急"
                : diff > urgencyA ? "A急"
                : diff > urgencyB ? "B顺"
                : diff > urgencyC ? "C缓"
                : "D缓";

            // 暂停工单 → UrgencyLevel 覆盖为"E停"
            if (pausedIds.Contains(summary.WorkOrderId))
                summary.UrgencyLevel = "E停";

            // 预计完成日 & 交期相差天数
            summary.EstimatedProcessCompletionDate = DateTime.Today.AddDays(totalDays);
            summary.DaysDiffFromDelivery = (summary.EstimatedProcessCompletionDate.Value.Date - summary.DeliveryDate.Date).Days;
        }

        // ========== G12: 原锁备注（仅 ScheduleStage=1 时计算） ==========
        // Type B 需按主号聚合，先预计算
        var stageOneSummaries = summaries.Where(s => s.ScheduleStage == 1).ToList();
        var typeBLookup = new Dictionary<(string SalesOrderNo, string MainNo), bool>();
        if (stageOneSummaries.Count > 0)
        {
            var mainNoGroups = stageOneSummaries
                .GroupBy(s => new { s.SalesOrderNo, s.ProductionMainNo })
                .ToList();

            foreach (var group in mainNoGroups)
            {
                var key = (group.Key.SalesOrderNo, group.Key.ProductionMainNo);
                var isFixed = group.First().LengthStatus == LengthStatus.Fixed.ToString();

                var totalTheorFinishQty = group.Sum(s => s.TheoreticalFinishQty);
                var totalTheorFinishWeight = group.Sum(s => s.TheoreticalFinishWeight);
                var totalQty = group.Sum(s => s.TotalQuantity);
                var totalWeight = group.Sum(s => s.TotalWeight);

                decimal g5Ratio;
                if (isFixed)
                    g5Ratio = totalQty > 0 ? Math.Round(totalTheorFinishQty / totalQty * 100, 2) : 0;
                else
                    g5Ratio = totalWeight > 0 ? Math.Round(totalTheorFinishWeight / totalWeight * 100, 2) : 0;

                // 主号流转比所有同组工单相同，取首个
                var mainNoFlowRatio = group.First().MainNoFlowOutputRatio;
                typeBLookup[key] = (g5Ratio + mainNoFlowRatio) >= supplySatisfiedRate;
            }
        }

        foreach (var summary in summaries)
        {
            if (summary.ScheduleStage != 1)
            {
                summary.RawMaterialLockRemark = null;
                continue;
            }

            // Type A: 质量影响 — G3 原始主号状态=满足(2) AND G7 有效主号状态≠满足(≠2)
            if (summary.MainNoInputStatus == 2 && summary.MainNoFlowStatus != 2)
            {
                summary.RawMaterialLockRemark = "A质量影响";
                continue;
            }

            // Type B: 已购未回 — G5理论成品(主号聚合比值) + G7有效主号流转比 ≥ 100%
            var bKey = (summary.SalesOrderNo, summary.ProductionMainNo);
            if (typeBLookup.TryGetValue(bKey, out var isTypeB) && isTypeB)
            {
                summary.RawMaterialLockRemark = "B已购未回";
                continue;
            }

            // Type C: 计划未执行 — G2 主号计划状态=满足(3)或超量(4)
            if ((MaterialPlanStatus)summary.MainNoMaterialPlanStatus == MaterialPlanStatus.Satisfied || (MaterialPlanStatus)summary.MainNoMaterialPlanStatus == MaterialPlanStatus.Excess)
            {
                summary.RawMaterialLockRemark = "C计划未执行";
                continue;
            }

            // Type D: 未完善计划 — G2 主号计划状态=未计划(0)或部分(1)
            if ((MaterialPlanStatus)summary.MainNoMaterialPlanStatus == MaterialPlanStatus.NotPlanned || (MaterialPlanStatus)summary.MainNoMaterialPlanStatus == MaterialPlanStatus.Partial)
            {
                summary.RawMaterialLockRemark = "D未完善计划";
                continue;
            }
        }

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

    public async Task RefreshByWorkOrderNosAsync(List<string> workOrderNos)
    {
        if (workOrderNos == null || workOrderNos.Count == 0) return;

        _logger.LogInformation("开始增量刷新工单执行状况: {Count} 个工单", workOrderNos.Count);

        // ===== 加载配置参数 =====
        var warehouseConfig = await _configService.GetConfigMapAsync("WarehouseThreshold");
        var workOrderDaysConfig = await _configService.GetConfigMapAsync("WorkOrderDays");
        var urgencyConfig = await _configService.GetConfigMapAsync("UrgencyThreshold");
        var processingDiscountConfig = await _configService.GetConfigMapAsync("ProcessingDiscount");
        var materialPlanStatusConfig = await _configService.GetConfigMapAsync("MaterialPlanStatus");
        var completeRatio = warehouseConfig.GetValueOrDefault("CompleteRatio", 0.95m);
        var completeDeviation = warehouseConfig.GetValueOrDefault("CompleteDeviation", 100m);
        var bufferDays = workOrderDaysConfig.GetValueOrDefault("BufferDays", 3m);
        var inspectionFixedDays = workOrderDaysConfig.GetValueOrDefault("InspectionFixedDays", 3m);
        var urgencyAPlus = urgencyConfig.GetValueOrDefault("APlus", 7m);
        var urgencyA = urgencyConfig.GetValueOrDefault("A", -3m);
        var urgencyB = urgencyConfig.GetValueOrDefault("B", -10m);
        var urgencyC = urgencyConfig.GetValueOrDefault("C", -17m);
        var groupDiscountRate = processingDiscountConfig.GetValueOrDefault("GroupDiscountRate", 0.025m);
        var supplySatisfiedRate = materialPlanStatusConfig.GetValueOrDefault("SupplySatisfiedRate", 100m);
        var fixedPartial = materialPlanStatusConfig.GetValueOrDefault("FixedPartial", 102m);
        var fixedSatisfied = materialPlanStatusConfig.GetValueOrDefault("FixedSatisfied", 110m);
        var nonFixedPartial = materialPlanStatusConfig.GetValueOrDefault("NonFixedPartial", 105m);
        var nonFixedSatisfied = materialPlanStatusConfig.GetValueOrDefault("NonFixedSatisfied", 120m);
        var defaultValueConfig = await _configService.GetConfigMapAsync("DefaultValue");
        var roughTubeFinishRatio = defaultValueConfig.GetValueOrDefault("RoughTubeFinishRatio", 0.92m);

        // 1. 查找目标工单及同订单的家族工单（用于主号级聚合）
        var targetWoList = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => workOrderNos.Contains(wo.WorkOrderNo) && wo.Status != WorkOrderStatus.NotGenerated)
            .ToListAsync();

        if (targetWoList.Count == 0) return;

        var targetWoIds = targetWoList.Select(wo => wo.Id).ToHashSet();
        var targetWoNos = targetWoList.Select(wo => wo.WorkOrderNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var salesOrderNos = targetWoList.Select(wo => wo.SalesOrderNo).Distinct().ToList();

        // 2. 加载同订单所有工单（用于正确的聚合计算）
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => salesOrderNos.Contains(wo.SalesOrderNo) && wo.Status != WorkOrderStatus.NotGenerated)
            .ToListAsync();

        var allWoIds = workOrders.Select(wo => wo.Id).ToHashSet();
        var allWoNos = workOrders.Select(wo => wo.WorkOrderNo).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("增量刷新: 目标 {TargetCount} 个, 同订单家族 {FamilyCount} 个",
            targetWoList.Count, workOrders.Count);

        // 3. 批量加载关联数据（按家族工单范围）
        var batches = await _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups)
            .Where(b => allWoNos.Contains(b.WorkOrderNo))
            .ToListAsync();
        var batchesByWo = batches
            .GroupBy(b => b.WorkOrderNo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => salesOrderNos.Contains(so.OrderNumber))
            .ToListAsync();
        var customerNameByWo = new Dictionary<int, string>();
        var customerSalesmanByWo = new Dictionary<int, string>();
        foreach (var wo in workOrders)
        {
            var so = salesOrders.FirstOrDefault(s => s.OrderNumber.Equals(wo.SalesOrderNo, StringComparison.OrdinalIgnoreCase));
            customerNameByWo[wo.Id] = so?.CustomerName ?? "";
            customerSalesmanByWo[wo.Id] = so?.Salesman ?? "";
        }

        var purchaseOrders = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.SourceWorkOrderNo != null && allWoNos.Contains(po.SourceWorkOrderNo)
                      && po.Status != PurchaseOrderStatus.Completed)
            .ToListAsync();
        var poByWoNo = purchaseOrders
            .GroupBy(po => po.SourceWorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var returnItems = await _context.SubcontractReturnItems
            .AsNoTracking()
            .Where(ri => ri.SourceWorkOrderNo != null && allWoNos.Contains(ri.SourceWorkOrderNo))
            .ToListAsync();
        var riByWoNo = returnItems
            .GroupBy(ri => ri.SourceWorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var workOrderListSummaries = await _context.Set<WorkOrderListSummary>()
            .AsNoTracking()
            .Where(s => allWoIds.Contains(s.WorkOrderId))
            .ToListAsync();
        var execSummaryByWoId = workOrderListSummaries.ToDictionary(s => s.WorkOrderId);

        var finalInspections = await _context.FinalInspections
            .AsNoTracking()
            .Where(fi => fi.MaterialName == "订单成品" && fi.WorkOrderNo != null && allWoNos.Contains(fi.WorkOrderNo))
            .ToListAsync();
        var fiByWoNo = finalInspections
            .GroupBy(fi => fi.WorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var inventoryBatches = await _context.InventoryBatches
            .AsNoTracking()
            .Where(ib => ib.MaterialType == InventoryMaterialTypes.OrderFinished && ib.WorkOrderNo != null && allWoNos.Contains(ib.WorkOrderNo))
            .ToListAsync();
        var ibByWoNo = inventoryBatches
            .GroupBy(ib => ib.WorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 4. 逐工单计算
        var now = DateTime.Now;
        var summaries = new List<WorkOrderExecutionSummary>();
        foreach (var wo in workOrders)
        {
            var woBatches = batchesByWo.TryGetValue(wo.WorkOrderNo, out var b) ? b : new List<ProductionBatch>();
            fiByWoNo.TryGetValue(wo.WorkOrderNo, out var woFiList);
            var totalInspectionQty = woFiList?.Sum(fi => fi.Quantity ?? 0) ?? 0;
            var totalQualifiedQty = woFiList?.Sum(fi => fi.QualifiedQuantity ?? 0) ?? 0;
            var totalScrapQty = woFiList?.Sum(fi => fi.DefectScrapQuantity ?? 0) ?? 0;
            var fiDates = woFiList?.Select(fi => fi.InspectionDate).ToList();
            var inspectionStartDate = fiDates?.Count > 0 ? fiDates.Min() : (DateTime?)null;
            var inspectionEndDate = fiDates?.Count > 0 ? fiDates.Max() : (DateTime?)null;

            var summary = ComputeSummary(wo,
                customerNameByWo.TryGetValue(wo.Id, out var cn) ? cn : "",
                customerSalesmanByWo.TryGetValue(wo.Id, out var sm) ? sm : "",
                woBatches, totalInspectionQty, totalQualifiedQty, totalScrapQty,
                inspectionStartDate, inspectionEndDate,
                completeRatio, completeDeviation, groupDiscountRate, supplySatisfiedRate);

            // G2: 从用料计划读模型取值
            if (execSummaryByWoId.TryGetValue(wo.Id, out var ls))
            {
                summary.LatestPlanDate = ls.LatestPlanDate;
                summary.MaterialPlanRate = ls.MaterialPlanRate;
                summary.MaterialPlanStatus = ls.MaterialPlanStatus;
                summary.MainNoMaterialPlanRate = ls.MainNoMaterialPlanRate;
                summary.MainNoMaterialPlanStatus = ls.MainNoMaterialPlanStatus;
                summary.ProcessCycle = ls.MaxStandardCycle;
                summary.MaterialPlanCoveredCount = ls.MaterialPlanCoveredCount;
                summary.MaterialPlanProportion = ls.MaterialPlanProportion;
                summary.LatestRequiredDate = ls.LatestRequiredDate;
            }

            // Group 5: 物料执行
            poByWoNo.TryGetValue(wo.WorkOrderNo, out var woPos);
            riByWoNo.TryGetValue(wo.WorkOrderNo, out var woRis);
            if ((woPos?.Count ?? 0) > 0 || (woRis?.Count ?? 0) > 0)
            {
                var safePos = woPos ?? new List<PurchaseOrder>();
                var safeRis = woRis ?? new List<SubcontractReturnItem>();
                var roughTubePos = safePos.Where(po => po.MaterialCategory == "RoughTube" || po.MaterialCategory == "SemiFinished").ToList();
                var roughTubeRis = safeRis.Where(ri => ri.MaterialCategory == "RoughTube" || ri.MaterialCategory == "SemiFinished").ToList();
                summary.PendingRoughTubeQty = roughTubePos.Sum(po => (po.Quantity ?? 0) - po.ReceivedQuantity)
                    + roughTubeRis.Sum(ri => (ri.RequiredQuantity ?? 0) - ri.ReturnedQuantity);
                summary.PendingRoughTubeWeight = roughTubePos.Sum(po => po.Weight - po.ReceivedWeight)
                    + roughTubeRis.Sum(ri => (ri.RequiredWeight ?? 0) - ri.ReturnedWeight);
                var finishPos = safePos.Where(po => po.MaterialCategory == "CriticalFinished" || po.MaterialCategory == "OrderFinished").ToList();
                var finishRis = safeRis.Where(ri => ri.MaterialCategory == "CriticalFinished" || ri.MaterialCategory == "OrderFinished").ToList();
                summary.PendingOutsourceFinishQty = finishPos.Sum(po => (po.Quantity ?? 0) - po.ReceivedQuantity)
                    + finishRis.Sum(ri => (ri.RequiredQuantity ?? 0) - ri.ReturnedQuantity);
                summary.PendingOutsourceFinishWeight = finishPos.Sum(po => po.Weight - po.ReceivedWeight)
                    + finishRis.Sum(ri => (ri.RequiredWeight ?? 0) - ri.ReturnedWeight);
                summary.TheoreticalFinishQty = roughTubePos.Concat(finishPos)
                    .Sum(po => ((po.Quantity ?? 0) - po.ReceivedQuantity) * (po.InputMultiple ?? 1))
                    + roughTubeRis.Concat(finishRis)
                    .Sum(ri => ((ri.RequiredQuantity ?? 0) - ri.ReturnedQuantity) * (ri.InputMultiple ?? 1));
                summary.TheoreticalFinishWeight = Math.Round(
                    summary.PendingRoughTubeWeight * roughTubeFinishRatio + summary.PendingOutsourceFinishWeight, 2);
            }

            // Group 11: 成品入库
            ibByWoNo.TryGetValue(wo.WorkOrderNo, out var woIbList);
            if (woIbList?.Count > 0)
            {
                summary.WarehousingStartDate = woIbList.Min(ib => ib.InboundDate);
                summary.WarehousingEndDate = woIbList.Max(ib => ib.InboundDate);
                summary.WarehousingTotalQty = woIbList.Sum(ib => ib.InitialQuantity);
                summary.WarehousingTotalWeight = woIbList.Sum(ib => ib.InitialWeight);
                var isFixed = summary.LengthStatus == LengthStatus.Fixed.ToString();
                var isComplete = isFixed
                    ? summary.WarehousingTotalQty >= wo.TotalQuantity
                    : summary.WarehousingTotalWeight >= wo.TotalWeight * completeRatio
                      && summary.WarehousingTotalWeight >= wo.TotalWeight - completeDeviation;
                summary.WoWarehousingStatus = (summary.WarehousingTotalQty == 0 && summary.WarehousingTotalWeight == 0)
                    ? 0 : isComplete ? 2 : 1;
            }
            else
            {
                summary.WarehousingTotalQty = 0;
                summary.WarehousingTotalWeight = 0;
                summary.WoWarehousingStatus = 0;
            }

            summary.LastRefreshTime = now;
            summaries.Add(summary);
        }

        // 5. 主号级聚合计算（使用全家族数据，确保聚合正确）
        ComputeMainNoInputAggregation(summaries, workOrders, supplySatisfiedRate);
        ComputeWarehousingAggregation(summaries, workOrders);

        // G12: 关注状态
        foreach (var summary in summaries)
        {
            summary.ScheduleStage = summary.WoWarehousingStatus == 2 ? 0
                : summary.MainNoFlowStatus != 2 ? 1
                : summary.FlowIncompleteBatchCount > 0 ? 2 : 3;
        }
        foreach (var summary in summaries)
        {
            if (summary.ProductionAttentionProcess == null && summary.ScheduleStage == 2)
                summary.ProductionAttentionProcess = "收尾-成检";
        }

        // MainNoAttentionProcess: 同(订单号+主号)下，取剩余工量最大值所在工单的生产关注工序
        var mainNoAttentionMap = summaries
            .Where(s => s.MaxBatchRemainingWorkDays.HasValue && s.ProductionAttentionProcess != null)
            .GroupBy(s => new { s.SalesOrderNo, s.ProductionMainNo })
            .ToDictionary(
                g => (g.Key.SalesOrderNo, g.Key.ProductionMainNo),
                g => g.OrderByDescending(s => s.MaxBatchRemainingWorkDays)
                      .First().ProductionAttentionProcess);
        foreach (var summary in summaries)
        {
            var key = (summary.SalesOrderNo, summary.ProductionMainNo);
            summary.MainNoAttentionProcess = mainNoAttentionMap.GetValueOrDefault(key);
        }

        // G12/G13: 加载暂停和需求调整数据
        var pausedIdList = await _context.Set<OrderDemandAdjustment>()
            .AsNoTracking()
            .Where(u => allWoIds.Contains(u.WorkOrderId) && u.IsPaused)
            .Select(u => u.WorkOrderId)
            .ToListAsync();
        var pausedIds = pausedIdList.ToHashSet();
        var adjustments = await _context.Set<OrderDemandAdjustment>()
            .AsNoTracking()
            .Where(a => allWoIds.Contains(a.WorkOrderId))
            .ToDictionaryAsync(a => a.WorkOrderId);
        foreach (var summary in summaries)
        {
            if (adjustments.TryGetValue(summary.WorkOrderId, out var adj))
            {
                summary.IsUrging = adj.IsUrging;
                summary.IsBatchDelivery = adj.IsBatchDelivery;
                summary.IsPaused = adj.IsPaused;
                summary.AdjustmentRemark = adj.AdjustmentRemark;
            }
        }

        // 生产流转性
        var mainNoUrgencyFlags = summaries
            .GroupBy(s => new { s.SalesOrderNo, s.ProductionMainNo })
            .ToDictionary(g => g.Key,
                g => new
                {
                    MainNoUrging = g.Any(s => s.IsUrging),
                    MainNoBatchDelivery = g.Any(s => s.IsBatchDelivery),
                    MainNoPaused = g.Any(s => s.IsPaused)
                });
        foreach (var summary in summaries)
        {
            var flags = mainNoUrgencyFlags[new { summary.SalesOrderNo, summary.ProductionMainNo }];
            summary.ProductionFlowProperty = flags.MainNoPaused ? "暂停"
                : (summary.ScheduleStage == 2 || (summary.ScheduleStage == 1 && (flags.MainNoUrging || flags.MainNoBatchDelivery))) ? "正常"
                : summary.ScheduleStage == 1 ? "待料"
                : (summary.ScheduleStage == 0 || summary.ScheduleStage == 3) ? (summary.FlowIncompleteBatchCount == 0 ? "略" : "疑问")
                : null;
        }

        // G12: 计算剩余工量 & 工单计划性
        var dailyEstimates = await _dailyOutputService.GetAllAsync();
        var completedBatchOutputByMainNo = batchesByWo.Values
            .SelectMany(b => b)
            .Where(b => b.Status == BatchStatus.Completed && b.ProductionType != "Rework" && b.ManufacturingItem == "OrderFinished")
            .GroupBy(b => new { b.SalesOrderNo, b.ProductionMainNo })
            .ToDictionary(g => g.Key, g =>
            {
                decimal total = 0;
                foreach (var batch in g)
                {
                    var inputWeight = batch.CurrentValidWeight ?? 0m;
                    var effectiveGroups = batch.ProcessGroups?.Count(pg => HasAnySection(pg)) ?? 0;
                    var discount = 1.0m - effectiveGroups * groupDiscountRate;
                    if (discount < 0) discount = 0;
                    total += Math.Round(inputWeight * discount, 3);
                }
                return Math.Round(total, 3);
            });

        foreach (var summary in summaries)
        {
            if (summary.ScheduleStage == 0) continue;
            var key = new { summary.SalesOrderNo, summary.ProductionMainNo };
            // ...剩余工量计算
            var agg = summaries
                .GroupBy(s => new { s.SalesOrderNo, s.ProductionMainNo })
                .Where(g => g.Key.Equals(key))
                .Select(g => new { MaxProcessCycle = g.Max(s => s.ProcessCycle), MaxRemainingDays = g.Max(s => s.FlowMaxRemainingWorkDays), MainNoTotalWeight = g.Sum(s => s.TotalWeight) })
                .FirstOrDefault();

            summary.TotalRemainingWorkDays = summary.ScheduleStage switch
            {
                1 => (agg?.MaxProcessCycle ?? 0) + (int)bufferDays,
                2 => agg?.MaxRemainingDays ?? 0,
                3 => (int)inspectionFixedDays,
                _ => null
            };

            if (summary.TotalRemainingWorkDays.HasValue && agg?.MainNoTotalWeight > 0)
            {
                var completedOutput = completedBatchOutputByMainNo.TryGetValue(key, out var co) ? co : 0m;
                var remainingWeight = agg.MainNoTotalWeight - completedOutput;
                if (remainingWeight > 0)
                {
                    var od = ParseOuterDiameter(summary.Specification);
                    if (od.HasValue)
                    {
                        var match = dailyEstimates
                            .Where(e => e.MinOuterDiameter <= od.Value)
                            .OrderByDescending(e => e.MinOuterDiameter)
                            .FirstOrDefault();
                        if (match != null && match.DailyOutputTons > 0)
                            summary.CapacityWorkDays = (int)Math.Ceiling(remainingWeight / 1000m / match.DailyOutputTons);
                    }
                }
                else
                    summary.CapacityWorkDays = 0;
            }

            var totalDays = (summary.TotalRemainingWorkDays ?? 0) + (summary.CapacityWorkDays ?? 0);
            var todayDays = DateOnly.FromDateTime(DateTime.Today).DayNumber;
            var deliveryDays = DateOnly.FromDateTime(summary.DeliveryDate).DayNumber;
            var diff = totalDays + todayDays - deliveryDays;
            summary.UrgencyLevel = diff > urgencyAPlus ? "A+急"
                : diff > urgencyA ? "A急" : diff > urgencyB ? "B顺" : diff > urgencyC ? "C缓" : "D缓";
            if (pausedIds.Contains(summary.WorkOrderId)) summary.UrgencyLevel = "E停";
            summary.EstimatedProcessCompletionDate = DateTime.Today.AddDays(totalDays);
            summary.DaysDiffFromDelivery = (summary.EstimatedProcessCompletionDate.Value.Date - summary.DeliveryDate.Date).Days;
        }

        // G12: 原锁备注
        foreach (var summary in summaries.Where(s => s.ScheduleStage == 1))
        {
            if (summary.MainNoInputStatus == 2 && summary.MainNoFlowStatus != 2)
                summary.RawMaterialLockRemark = "A质量影响";
            else
            {
                var isFixed = summary.LengthStatus == LengthStatus.Fixed.ToString();
                var totalTheorFinishQty = summaries.Where(s => s.SalesOrderNo == summary.SalesOrderNo && s.ProductionMainNo == summary.ProductionMainNo).Sum(s => s.TheoreticalFinishQty);
                var totalQty = summaries.Where(s => s.SalesOrderNo == summary.SalesOrderNo && s.ProductionMainNo == summary.ProductionMainNo).Sum(s => s.TotalQuantity);
                var totalTheorFinishWeight = summaries.Where(s => s.SalesOrderNo == summary.SalesOrderNo && s.ProductionMainNo == summary.ProductionMainNo).Sum(s => s.TheoreticalFinishWeight);
                var totalWeight = summaries.Where(s => s.SalesOrderNo == summary.SalesOrderNo && s.ProductionMainNo == summary.ProductionMainNo).Sum(s => s.TotalWeight);
                var g5Ratio = isFixed ? (totalQty > 0 ? totalTheorFinishQty / totalQty * 100 : 0)
                    : (totalWeight > 0 ? totalTheorFinishWeight / totalWeight * 100 : 0);
                var mainNoFlowRatio = summaries.Where(s => s.SalesOrderNo == summary.SalesOrderNo && s.ProductionMainNo == summary.ProductionMainNo).First().MainNoFlowOutputRatio;
                var isTypeB = (g5Ratio + mainNoFlowRatio) >= supplySatisfiedRate;
                if (isTypeB)
                    summary.RawMaterialLockRemark = "B已购未回";
                else if ((MaterialPlanStatus)summary.MainNoMaterialPlanStatus == MaterialPlanStatus.Satisfied || (MaterialPlanStatus)summary.MainNoMaterialPlanStatus == MaterialPlanStatus.Excess)
                    summary.RawMaterialLockRemark = "C计划未执行";
                else if ((MaterialPlanStatus)summary.MainNoMaterialPlanStatus == MaterialPlanStatus.NotPlanned || (MaterialPlanStatus)summary.MainNoMaterialPlanStatus == MaterialPlanStatus.Partial)
                    summary.RawMaterialLockRemark = "D未完善计划";
            }
        }

        // 6. 仅 upsert 目标工单
        var existingRecords = await _context.Set<WorkOrderExecutionSummary>()
            .Where(e => targetWoIds.Contains(e.WorkOrderId))
            .ToListAsync();
        var existingByWoId = existingRecords.ToDictionary(e => e.WorkOrderId);

        foreach (var summary in summaries.Where(s => targetWoIds.Contains(s.WorkOrderId)))
        {
            if (existingByWoId.TryGetValue(summary.WorkOrderId, out var existing))
            {
                CopySummaryToExisting(summary, existing);
                _context.Entry(existing).State = EntityState.Modified;
            }
            else
            {
                _context.Set<WorkOrderExecutionSummary>().Add(summary);
            }
        }

        await _context.SaveChangesAsync();

        // 7. 同步刷新订单列表读模型（ScheduleStage/UrgencyLevel/EstimatedCompletionDate 已更新）
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var orderIds = await _context.SalesOrders
                .AsNoTracking()
                .Where(so => salesOrderNos.Contains(so.OrderNumber))
                .Select(so => so.Id)
                .ToListAsync();
            foreach (var orderId in orderIds)
            {
                await orderService.RefreshByOrderIdAsync(orderId);
            }
        }

        _logger.LogInformation("增量刷新完成: 目标 {TargetCount} 个工单", targetWoList.Count);
    }

    private static WorkOrderExecutionSummary ComputeSummary(
        WoEntity wo,
        string customerName,
        string salesman,
        List<ProductionBatch> batches,
        int totalInspectionQty = 0,
        int totalQualifiedQty = 0,
        int totalScrapQty = 0,
        DateTime? inspectionStartDate = null,
        DateTime? inspectionEndDate = null,
        decimal completeRatio = 0m,
        decimal completeDeviation = 0m,
        decimal groupDiscountRate = 0m,
        decimal supplySatisfiedRate = 0m)
    {
        // Group 1: 直接从工单复制（Salesman 从 SalesOrder 快照字段读取，已由调用方传入）
        var summary = new WorkOrderExecutionSummary
        {
            WorkOrderId = wo.Id,
            WorkOrderNo = wo.WorkOrderNo,
            Salesman = salesman,
            CustomerName = customerName,
            SignDate = wo.SignDate,
            DeliveryDate = wo.DeliveryDate,
            DelayPenalty = wo.DelayPenalty,
            SettlementMethod = wo.SettlementMethod.ToString(),
            SalesOrderNo = wo.SalesOrderNo,
            ProductionMainNo = wo.ProductionMainNo,
            ProductionSubNo = wo.ProductionSubNo,
            MaterialName = wo.PipeManufacturingType.ToString(),
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

        // G2 字段（LatestPlanDate/MaterialPlanRate/MaterialPlanStatus/ProcessCycle/
        //   MaterialPlanCoveredCount/MaterialPlanProportion/LatestRequiredDate）
        // 已从 WorkOrderListSummary 预计算读取，由调用方在外层设置

        // Group 3: 目标批次（生产类型≠返整 且 制造物品=订单成品）
        // 注意：DB 存储的是英文枚举值（如 "Rework"、"OrderFinished"），非中文
        var targetBatches = batches
            .Where(b => b.ProductionType != "Rework" && b.ManufacturingItem == "OrderFinished")
            .ToList();

        // 投料起止日取批次的创建时间（非仓库入库日期）
        var inputDates = targetBatches
            .Select(b => b.CreatedTime.DateTime)
            .ToList();

        summary.InputStartDate = inputDates.Count > 0 ? inputDates.Min() : null;
        summary.InputEndDate = inputDates.Count > 0 ? inputDates.Max() : null;
        summary.TotalBatchCount = targetBatches.Count;
        summary.InputQuantity = targetBatches.Sum(b => b.InputQuantity ?? 0);
        summary.InputWeight = targetBatches.Sum(b => b.InputWeight ?? 0);

        // 逐批计算理论成品并累加
        decimal theorQty = 0;
        decimal theorWeight = 0;
        foreach (var batch in targetBatches)
        {
            var batchInputQty = batch.InputQuantity ?? 0;
            var batchInputWeight = batch.InputWeight ?? 0m;

            // 理论成品支数 = 投料支数 × 制成倍数
            if (batch.ProductionRatio > 0)
                theorQty += batchInputQty * batch.ProductionRatio;

            // 理论成品重量 = 投料重量 × (1 - 有效工序组数 × 2.5%)
            var effectiveGroupCount = batch.ProcessGroups?
                .Count(pg => HasAnySection(pg)) ?? 0;
            var discount = 1.0m - effectiveGroupCount * groupDiscountRate;
            if (discount < 0) discount = 0;
            theorWeight += Math.Round(batchInputWeight * discount, 3);
        }
        summary.TheoreticalOutputQty = Math.Round(theorQty, 3);
        summary.TheoreticalOutputWeight = Math.Round(theorWeight, 3);

        // 投料成品比 + 状态
        var (ratio, status) = ComputeInputRatioAndStatus(summary, wo, supplySatisfiedRate);
        summary.InputOutputRatio = ratio;
        summary.InputStatus = status;

        // Group 4: 有效批次（在目标批次基础上排除作废）
        var validBatches = targetBatches.Where(b => b.Status != Core.Enums.BatchStatus.Cancelled).ToList();

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
            var discount = 1.0m - effectiveGroupCount * groupDiscountRate;
            if (discount < 0) discount = 0;
            validTheorWeight += Math.Round(batchInputWeight * discount, 3);
        }
        summary.ValidOutputQty = Math.Round(validTheorQty, 3);
        summary.ValidOutputWeight = Math.Round(validTheorWeight, 3);

        // Group 6: 返整执行数据（ProductionType=Rework 且 ManufacturingItem=OrderFinished）
        var reworkBatches = batches
            .Where(b => b.ProductionType == "Rework" && b.ManufacturingItem == "OrderFinished")
            .ToList();

        var reworkDates = reworkBatches
            .Select(b => b.CreatedTime.DateTime)
            .ToList();
        summary.ReworkInputEndDate = reworkDates.Count > 0 ? reworkDates.Max() : null;
        summary.ReworkBatchCount = reworkBatches.Count;
        summary.ReworkInputQuantity = reworkBatches.Sum(b => b.CurrentValidQty ?? 0);
        summary.ReworkInputWeight = reworkBatches.Sum(b => b.CurrentValidWeight ?? 0);

        decimal reworkTheorQty = 0;
        decimal reworkTheorWeight = 0;
        foreach (var batch in reworkBatches)
        {
            var batchInputQty = batch.CurrentValidQty ?? 0;
            var batchInputWeight = batch.CurrentValidWeight ?? 0m;
            if (batch.ProductionRatio > 0)
                reworkTheorQty += batchInputQty * batch.ProductionRatio;

            var effectiveGroupCount = batch.ProcessGroups?
                .Count(pg => HasAnySection(pg)) ?? 0;
            var discount = 1.0m - effectiveGroupCount * groupDiscountRate;
            if (discount < 0) discount = 0;
            reworkTheorWeight += Math.Round(batchInputWeight * discount, 3);
        }
        summary.ReworkTheoreticalOutputQty = Math.Round(reworkTheorQty, 3);
        summary.ReworkTheoreticalOutputWeight = Math.Round(reworkTheorWeight, 3);

        // Group 7: 有效流转（合格流转 + 返整执行 合并比值）
        var combinedFlowQty = summary.ValidOutputQty + summary.ReworkTheoreticalOutputQty;
        var combinedFlowWeight = summary.ValidOutputWeight + summary.ReworkTheoreticalOutputWeight;
        var (flowRatio, flowStatus) = ComputeInputRatioAndStatus(
            summary.LengthStatus, combinedFlowQty, combinedFlowWeight, wo.TotalQuantity, wo.TotalWeight, supplySatisfiedRate);
        summary.FlowOutputRatio = flowRatio;
        summary.FlowStatus = flowStatus;

        // G7: 总批次数 & 未完成批数（制造物品=订单成品的所有批次）
        var allTargetBatches = targetBatches.Concat(reworkBatches).ToList();
        summary.FlowTotalBatchCount = allTargetBatches.Count;
        summary.FlowIncompleteBatchCount = allTargetBatches.Count(b => b.Status != BatchStatus.Completed);
        summary.FlowMaxRemainingWorkDays = allTargetBatches.Count > 0
            ? allTargetBatches.Max(b => b.RemainingWorkDays)
            : 0;

        // ========== Group 8: 过程不合格（G3 − G4，负值归零） ==========
        var defectiveRawQty = Math.Max(0, summary.InputQuantity - summary.ValidInputQuantity);
        var defectiveRawWeight = Math.Max(0, summary.InputWeight - summary.ValidInputWeight);
        summary.DefectiveRawQty = defectiveRawQty;
        summary.DefectiveRawWeight = defectiveRawWeight;
        summary.DefectiveOutputQty = Math.Max(0, summary.TheoreticalOutputQty - summary.ValidOutputQty);
        summary.DefectiveOutputWeight = Math.Max(0, summary.TheoreticalOutputWeight - summary.ValidOutputWeight);
        summary.DefectiveRatio = summary.InputWeight > 0
            ? Math.Round(defectiveRawWeight / summary.InputWeight * 100, 2)
            : 0;

        // ========== Group 9: 成检不合格（从 FinalInspection 聚合） ==========
        summary.InspectionStartDate = inspectionStartDate;
        summary.InspectionEndDate = inspectionEndDate;
        var inspectionDefectQty = Math.Max(0, totalInspectionQty - totalQualifiedQty);
        var unitWeight = summary.TheoreticalOutputQty > 0
            ? summary.TheoreticalOutputWeight / summary.TheoreticalOutputQty
            : 0m;
        summary.InspectionDefectQty = inspectionDefectQty;
        summary.InspectionDefectWeight = Math.Round(inspectionDefectQty * unitWeight, 3);
        summary.InspectionDefectRatio = summary.TheoreticalOutputWeight > 0
            ? Math.Round(summary.InspectionDefectWeight / summary.TheoreticalOutputWeight * 100, 2)
            : 0;

        // 成检报废（用于 G10）
        var scrapWeight = Math.Round(totalScrapQty * unitWeight, 3);
        var scrapRatio = summary.TheoreticalOutputWeight > 0
            ? Math.Round(scrapWeight / summary.TheoreticalOutputWeight * 100, 2)
            : 0;

        // ========== Group 10: 汇总不合格 ==========
        summary.GeneralDefectWeight = summary.ReworkTheoreticalOutputWeight;
        summary.GeneralDefectRatio = summary.TheoreticalOutputWeight > 0
            ? Math.Round(summary.ReworkTheoreticalOutputWeight / summary.TheoreticalOutputWeight * 100, 2)
            : 0;

        var seriousDefectWeight = summary.DefectiveOutputWeight + summary.InspectionDefectWeight
            - summary.ReworkTheoreticalOutputWeight;
        if (seriousDefectWeight < 0) seriousDefectWeight = 0;
        summary.SeriousDefectWeight = Math.Round(seriousDefectWeight, 3);
        summary.SeriousDefectRatio = summary.TheoreticalOutputWeight > 0
            ? Math.Round(seriousDefectWeight / summary.TheoreticalOutputWeight * 100, 2)
            : 0;

        summary.ScrapWeight = scrapWeight;
        summary.ScrapRatio = scrapRatio;

        // ========== Group 14: 在产节点待量 ==========
        // 8个固定节点定义：(工序组名称, 工段名称)
        var nodeDefs = new (string ProcessName, string SectionName)[]
        {
            ("荒管处理", SectionDefs.OuterPolish),
            ("在制修检", SectionDefs.Inspection),
            ("60冷轧", SectionDefs.ColdRollDraw),
            ("50冷轧", SectionDefs.ColdRollDraw),
            ("30冷轧", SectionDefs.ColdRollDraw),
            ("20冷轧", SectionDefs.ColdRollDraw),
            ("三辊冷轧", SectionDefs.ColdRollDraw),
            ("冷拔", SectionDefs.ColdRollDraw),
        };

        // 使用所有非作废批次（含正常 + 返整）
        var group14Batches = batches.Where(b => b.Status != BatchStatus.Cancelled).ToList();

        // 预置 pending 字段容器
        var pendingValues = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (pn, _) in nodeDefs)
            pendingValues[pn] = 0m;

        foreach (var batch in group14Batches)
        {
            if (batch.ProcessGroups == null || batch.ProcessGroups.Count == 0)
                continue;

            // 已完成的批次视为到达所有节点
            if (batch.Status == BatchStatus.Completed)
                continue;

            // 构建本批次 ProcessName → SequenceNumber 映射
            var pgMap = batch.ProcessGroups
                .Where(pg => !string.IsNullOrEmpty(pg.ProcessName))
                .ToDictionary(pg => pg.ProcessName, pg => pg.SequenceNumber, StringComparer.OrdinalIgnoreCase);

            // 获取批次当前工序的 SequenceNumber（未投产 = 0）
            int batchCurrentSeq = 0;
            if (!string.IsNullOrEmpty(batch.CurrentGroupName))
            {
                var currentSeqOpt = batch.ProcessGroups
                    .Where(pg => pg.ProcessName.Equals(batch.CurrentGroupName, StringComparison.OrdinalIgnoreCase))
                    .Select(pg => pg.SequenceNumber)
                    .Cast<int?>()
                    .FirstOrDefault();
                batchCurrentSeq = currentSeqOpt ?? 0;
            }

            var batchWeight = batch.CurrentValidWeight ?? 0m;

            foreach (var (pn, sn) in nodeDefs)
            {
                // 该批次无此工序组 → 节点不适用
                if (!pgMap.TryGetValue(pn, out var targetSeq))
                    continue;

                if (batchCurrentSeq < targetSeq)
                {
                    // 批次未到达此工序组 → 检查该工序组是否确实包含目标工段
                    // 仅当该工序组定义了目标工段时才计入待量
                    var targetPg = batch.ProcessGroups
                        .FirstOrDefault(pg => pg.ProcessName.Equals(pn, StringComparison.OrdinalIgnoreCase));
                    if (targetPg == null) continue;

                    var targetSectionSeq = GetSectionSequence(targetPg, sn);
                    if (targetSectionSeq == null) continue; // 该工序组不含此工段

                    pendingValues[pn] += batchWeight;
                }
                else if (batchCurrentSeq == targetSeq)
                {
                    // === 1. 工段级到达检查：荒管处理·外抛光、在制修检·检验 ===
                    // 批次已到达此工序组但尚未到达指定工段时，仍需计入待量
                    if (pn is "荒管处理" or "在制修检")
                    {
                        var targetPg = batch.ProcessGroups
                            .FirstOrDefault(pg => pg.ProcessName.Equals(pn, StringComparison.OrdinalIgnoreCase));
                        if (targetPg == null) continue;

                        // 获取目标工段在该工序组中的执行序号（如 OuterPolish=5）
                        var targetSectionSeq = GetSectionSequence(targetPg, sn);
                        if (targetSectionSeq == null) continue; // 该工序组不含此工段

                        // 批次无当前工段 → 在工序组内但未开始任何工段 → 计入待量
                        if (string.IsNullOrEmpty(batch.CurrentSectionName))
                        {
                            pendingValues[pn] += batchWeight;
                            continue;
                        }

                        // 批次当前不在该工序组 → 已越过（例如已到后续工序）→ 不计
                        if (batch.CurrentGroupName == null ||
                            !batch.CurrentGroupName.Equals(pn, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // 均在同一工序组内，比较工段执行序号
                        var currentSectionSeq = GetSectionSequence(targetPg, batch.CurrentSectionName);
                        if (currentSectionSeq == null || currentSectionSeq.Value < targetSectionSeq.Value)
                        {
                            // 当前工段序号 < 目标工段序号 → 尚未到达目标工段 → 计入待量
                            pendingValues[pn] += batchWeight;
                        }
                        continue;
                    }

                    // === 3. 冷轧/冷拔系列 — 检查批次是否正在做指定工段且未完成 ===
                    var isAtSection = batch.CurrentGroupName != null
                        && batch.CurrentGroupName.Equals(pn, StringComparison.OrdinalIgnoreCase)
                        && batch.CurrentSectionName == sn;

                    if (isAtSection && batch.CurrentSectionCompleted != true)
                        pendingValues[pn] += batchWeight;
                }
            }
        }

        // 将 pendingValues 赋值到 summary 字段
        summary.PendingSectionRoughTube = pendingValues["荒管处理"] > 0 ? pendingValues["荒管处理"] : null;
        summary.PendingSectionWarehouseFix = pendingValues["在制修检"] > 0 ? pendingValues["在制修检"] : null;
        summary.PendingSection60Roll = pendingValues["60冷轧"] > 0 ? pendingValues["60冷轧"] : null;
        summary.PendingSection50Roll = pendingValues["50冷轧"] > 0 ? pendingValues["50冷轧"] : null;
        summary.PendingSection30Roll = pendingValues["30冷轧"] > 0 ? pendingValues["30冷轧"] : null;
        summary.PendingSection20Roll = pendingValues["20冷轧"] > 0 ? pendingValues["20冷轧"] : null;
        summary.PendingSectionThreeRoll = pendingValues["三辊冷轧"] > 0 ? pendingValues["三辊冷轧"] : null;
        summary.PendingSectionDrawBench = pendingValues["冷拔"] > 0 ? pendingValues["冷拔"] : null;

        // DeformedProcessCompleted: 后6项（全部冷轧/冷拔）之和=0 → true
        var rollingSum = pendingValues["60冷轧"] + pendingValues["50冷轧"] + pendingValues["30冷轧"]
            + pendingValues["20冷轧"] + pendingValues["三辊冷轧"] + pendingValues["冷拔"];
        summary.DeformedProcessCompleted = rollingSum == 0m;

        // ProductionAttentionProcess: 前8项中值>0 且 SequenceNumber 最小的工序名称
        // 取第一个有 ProcessGroup 的批次作为 SequenceNumber 参照
        var refPgMap = group14Batches
            .Where(b => b.ProcessGroups != null && b.ProcessGroups.Count > 0)
            .SelectMany(b => b.ProcessGroups)
            .Where(pg => !string.IsNullOrEmpty(pg.ProcessName))
            .GroupBy(pg => pg.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Min(pg => pg.SequenceNumber), StringComparer.OrdinalIgnoreCase);

        var attentionProcess = nodeDefs
            .Where(n => pendingValues[n.ProcessName] > 0 && refPgMap.ContainsKey(n.ProcessName))
            .OrderBy(n => refPgMap[n.ProcessName])
            .Select(n => n.ProcessName)
            .FirstOrDefault();
        summary.ProductionAttentionProcess = attentionProcess;

        // MaxBatchRemainingWorkDays: 此工单号下所有批次中 RemainingWorkDays 最大值
        summary.MaxBatchRemainingWorkDays = batches.Count > 0
            ? batches.Max(b => b.RemainingWorkDays)
            : (int?)null;

        return summary;
    }

    private static (decimal ratio, int status) ComputeInputRatioAndStatus(
        WorkOrderExecutionSummary summary, WoEntity wo, decimal satisfiedRate)
    {
        return ComputeInputRatioAndStatus(summary.LengthStatus, summary.TheoreticalOutputQty, summary.TheoreticalOutputWeight, wo.TotalQuantity, wo.TotalWeight, satisfiedRate);
    }

    private static (decimal ratio, int status) ComputeInputRatioAndStatus(
        string lengthStatus, decimal outputQty, decimal outputWeight, int totalQty, decimal totalWeight, decimal satisfiedRate)
    {
        var isFixed = lengthStatus == LengthStatus.Fixed.ToString();
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

        int status;
        if (ratio <= 0)
            status = 0;      // 未投料
        else if (ratio >= satisfiedRate)
            status = 2;      // 满足
        else
            status = 1;      // 部分

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
        List<WorkOrderExecutionSummary> summaries, List<WoEntity> workOrders, decimal supplySatisfiedRate)
    {
        var woDict = workOrders.ToDictionary(wo => wo.Id);

        // 按 (SalesOrderNo, ProductionMainNo) 分组
        var mainNoGroups = summaries
            .Where(s => woDict.ContainsKey(s.WorkOrderId))
            .Select(s => new { Summary = s, WorkOrder = woDict[s.WorkOrderId] })
            .GroupBy(x => new { x.WorkOrder.SalesOrderNo, MainNo = x.Summary.ProductionMainNo })
            .ToList();

        foreach (var group in mainNoGroups)
        {
            var groupWorkOrders = group.Select(g => g.WorkOrder).ToList();
            var groupSummaries = group.Select(g => g.Summary).ToList();

            // Group 2 的 MainNo 级用料计划（满足率/状态）已从 WorkOrderListSummary 预读，不再重算

            // Group 3: MainNo 级投料聚合（使用已修正的理论成品值）
            var totalQty = groupWorkOrders.Sum(wo => wo.TotalQuantity);
            var totalWeight = groupWorkOrders.Sum(wo => wo.TotalWeight);

            if (totalQty > 0 || totalWeight > 0)
            {
                var totalTheorQty = groupSummaries.Sum(s => s.TheoreticalOutputQty);
                var totalTheorWeight = groupSummaries.Sum(s => s.TheoreticalOutputWeight);

                var isFixed = groupSummaries.First().LengthStatus == LengthStatus.Fixed.ToString();
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

                int mainStatus;
                if (mainRatio <= 0)
                    mainStatus = 0;
                else if (mainRatio >= supplySatisfiedRate)
                    mainStatus = 2;
                else
                    mainStatus = 1;

                foreach (var s in groupSummaries)
                {
                    s.MainNoInputOutputRatio = mainRatio;
                    s.MainNoInputStatus = mainStatus;
                }
            }

            // Group 7: 有效流转主号级聚合（合格流转 + 返整执行）
            var totalFlowQty = groupSummaries.Sum(s => s.ValidOutputQty + s.ReworkTheoreticalOutputQty);
            var totalFlowWeight = groupSummaries.Sum(s => s.ValidOutputWeight + s.ReworkTheoreticalOutputWeight);

            if (totalQty > 0 || totalWeight > 0)
            {
                var isFixed = groupSummaries.First().LengthStatus == LengthStatus.Fixed.ToString();
                decimal mainFlowRatio;
                if (isFixed)
                {
                    mainFlowRatio = totalQty > 0
                        ? Math.Round(totalFlowQty / totalQty * 100, 2)
                        : 0;
                }
                else
                {
                    mainFlowRatio = totalWeight > 0
                        ? Math.Round(totalFlowWeight / totalWeight * 100, 2)
                        : 0;
                }

                int mainFlowStatus;
                if (mainFlowRatio <= 0)
                    mainFlowStatus = 0;
                else if (mainFlowRatio >= supplySatisfiedRate)
                    mainFlowStatus = 2;
                else
                    mainFlowStatus = 1;

                foreach (var s in groupSummaries)
                {
                    s.MainNoFlowOutputRatio = mainFlowRatio;
                    s.MainNoFlowStatus = mainFlowStatus;
                }
            }
        }
    }

    /// <summary>
    /// 计算主号级和订单级入库状态聚合
    /// 主号/订单下所有工单都是"入库完结"→入库完结
    /// 所有工单都是"无入库"→无入库
    /// 否则→入库部分
    /// </summary>
    private static void ComputeWarehousingAggregation(
        List<WorkOrderExecutionSummary> summaries, List<WoEntity> workOrders)
    {
        var woDict = workOrders.ToDictionary(wo => wo.Id);

        // 按 (SalesOrderNo, ProductionMainNo) 分组 → 主号级
        var mainNoGroups = summaries
            .Where(s => woDict.ContainsKey(s.WorkOrderId))
            .Select(s => new { Summary = s, WorkOrder = woDict[s.WorkOrderId] })
            .GroupBy(x => new { x.WorkOrder.SalesOrderNo, MainNo = x.Summary.ProductionMainNo })
            .ToList();

        foreach (var group in mainNoGroups)
        {
            var groupSummaries = group.Select(g => g.Summary).ToList();
            var statuses = groupSummaries.Select(s => s.WoWarehousingStatus).Distinct().ToList();

            int mainNoStatus;
            if (statuses.Count == 1 && statuses[0] == 2)
                mainNoStatus = 2; // 全部入库完结
            else if (statuses.Count == 1 && statuses[0] == 0)
                mainNoStatus = 0; // 全部无入库
            else
                mainNoStatus = 1; // 入库部分

            foreach (var s in groupSummaries)
                s.MainNoWarehousingStatus = mainNoStatus;
        }

        // 按 SalesOrderNo 分组 → 订单级
        var orderGroups = summaries
            .Where(s => woDict.ContainsKey(s.WorkOrderId))
            .Select(s => new { Summary = s, WorkOrder = woDict[s.WorkOrderId] })
            .GroupBy(x => x.WorkOrder.SalesOrderNo)
            .ToList();

        foreach (var group in orderGroups)
        {
            var groupSummaries = group.Select(g => g.Summary).ToList();
            var statuses = groupSummaries.Select(s => s.WoWarehousingStatus).Distinct().ToList();

            int orderStatus;
            if (statuses.Count == 1 && statuses[0] == 2)
                orderStatus = 2; // 全部入库完结
            else if (statuses.Count == 1 && statuses[0] == 0)
                orderStatus = 0; // 全部无入库
            else
                orderStatus = 1; // 入库部分

            foreach (var s in groupSummaries)
                s.OrderWarehousingStatus = orderStatus;
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
        target.ProcessCycle = source.ProcessCycle;
        target.MaterialPlanCoveredCount = source.MaterialPlanCoveredCount;
        target.MaterialPlanProportion = source.MaterialPlanProportion;
        target.LatestRequiredDate = source.LatestRequiredDate;

        // Group 5
        target.PendingRoughTubeQty = source.PendingRoughTubeQty;
        target.PendingRoughTubeWeight = source.PendingRoughTubeWeight;
        target.PendingOutsourceFinishQty = source.PendingOutsourceFinishQty;
        target.PendingOutsourceFinishWeight = source.PendingOutsourceFinishWeight;
        target.TheoreticalFinishQty = source.TheoreticalFinishQty;
        target.TheoreticalFinishWeight = source.TheoreticalFinishWeight;

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
        // Group 6
        target.ReworkInputEndDate = source.ReworkInputEndDate;
        target.ReworkBatchCount = source.ReworkBatchCount;
        target.ReworkInputQuantity = source.ReworkInputQuantity;
        target.ReworkInputWeight = source.ReworkInputWeight;
        target.ReworkTheoreticalOutputQty = source.ReworkTheoreticalOutputQty;
        target.ReworkTheoreticalOutputWeight = source.ReworkTheoreticalOutputWeight;

        // Group 7
        target.FlowOutputRatio = source.FlowOutputRatio;
        target.FlowStatus = source.FlowStatus;
        target.MainNoFlowOutputRatio = source.MainNoFlowOutputRatio;
        target.MainNoFlowStatus = source.MainNoFlowStatus;
        target.FlowTotalBatchCount = source.FlowTotalBatchCount;
        target.FlowIncompleteBatchCount = source.FlowIncompleteBatchCount;
        target.FlowMaxRemainingWorkDays = source.FlowMaxRemainingWorkDays;

        // Group 8
        target.DefectiveRawQty = source.DefectiveRawQty;
        target.DefectiveRawWeight = source.DefectiveRawWeight;
        target.DefectiveOutputQty = source.DefectiveOutputQty;
        target.DefectiveOutputWeight = source.DefectiveOutputWeight;
        target.DefectiveRatio = source.DefectiveRatio;

        // Group 9
        target.InspectionDefectQty = source.InspectionDefectQty;
        target.InspectionDefectWeight = source.InspectionDefectWeight;
        target.InspectionDefectRatio = source.InspectionDefectRatio;
        target.InspectionStartDate = source.InspectionStartDate;
        target.InspectionEndDate = source.InspectionEndDate;

        // Group 10
        target.GeneralDefectWeight = source.GeneralDefectWeight;
        target.GeneralDefectRatio = source.GeneralDefectRatio;
        target.SeriousDefectWeight = source.SeriousDefectWeight;
        target.SeriousDefectRatio = source.SeriousDefectRatio;
        target.ScrapWeight = source.ScrapWeight;
        target.ScrapRatio = source.ScrapRatio;

        // Group 11
        target.WarehousingStartDate = source.WarehousingStartDate;
        target.WarehousingEndDate = source.WarehousingEndDate;
        target.WarehousingTotalQty = source.WarehousingTotalQty;
        target.WarehousingTotalWeight = source.WarehousingTotalWeight;
        target.WoWarehousingStatus = source.WoWarehousingStatus;
        target.MainNoWarehousingStatus = source.MainNoWarehousingStatus;
        target.OrderWarehousingStatus = source.OrderWarehousingStatus;

        // G12
        target.ScheduleStage = source.ScheduleStage;
        target.TotalRemainingWorkDays = source.TotalRemainingWorkDays;
        target.CapacityWorkDays = source.CapacityWorkDays;
        target.UrgencyLevel = source.UrgencyLevel;
        target.EstimatedProcessCompletionDate = source.EstimatedProcessCompletionDate;
        target.DaysDiffFromDelivery = source.DaysDiffFromDelivery;
        target.RawMaterialLockRemark = source.RawMaterialLockRemark;

        // Group 14
        target.PendingSectionRoughTube = source.PendingSectionRoughTube;
        target.PendingSectionWarehouseFix = source.PendingSectionWarehouseFix;
        target.PendingSection60Roll = source.PendingSection60Roll;
        target.PendingSection50Roll = source.PendingSection50Roll;
        target.PendingSection30Roll = source.PendingSection30Roll;
        target.PendingSection20Roll = source.PendingSection20Roll;
        target.PendingSectionThreeRoll = source.PendingSectionThreeRoll;
        target.PendingSectionDrawBench = source.PendingSectionDrawBench;
        target.DeformedProcessCompleted = source.DeformedProcessCompleted;
        target.ProductionAttentionProcess = source.ProductionAttentionProcess;
        target.MaxBatchRemainingWorkDays = source.MaxBatchRemainingWorkDays;
        target.MainNoAttentionProcess = source.MainNoAttentionProcess;

        // Group 13
        target.IsUrging = source.IsUrging;
        target.IsBatchDelivery = source.IsBatchDelivery;
        target.IsPaused = source.IsPaused;
        target.AdjustmentRemark = source.AdjustmentRemark;
        target.ProductionFlowProperty = source.ProductionFlowProperty;

        // 刷新时间
        target.LastRefreshTime = source.LastRefreshTime;
    }

    public async Task<List<WorkOrderExecutionDashboardItem>> GetDashboardSummaryAsync()
    {
        var result = new List<WorkOrderExecutionDashboardItem>();

        // ========== Stage 1: 原料锁定 ==========
        // 待投料 = (TotalWeight - PendingOutsourceFinishWeight) × RawMaterialRatio - InputWeight
        // 参考 RawMaterialLockPlanAndExecution.RecalculateSummary()
        var rawMaterialRatio = await GetConfigAsync("ProcessingDiscount", "RawMaterialRatio", 1.1m);
        var stage1Data = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Where(x => x.ScheduleStage == 1 && x.UrgencyLevel != null)
            .Select(x => new
            {
                UrgencyLevel = x.UrgencyLevel ?? "",
                PendingWeight = (x.TotalWeight - x.PendingOutsourceFinishWeight) * rawMaterialRatio - x.InputWeight
            })
            .ToListAsync();

        var stage1Grouped = stage1Data
            .GroupBy(x => x.UrgencyLevel)
            .Select(g => new WorkOrderExecutionDashboardItem
            {
                ScheduleStage = 1,
                UrgencyLevel = g.Key,
                OrderCount = g.Count(),
                TotalWeight = g.Sum(x => Math.Max(0, x.PendingWeight))
            });
        result.AddRange(stage1Grouped);

        // ========== Stage 2: 生产在产 ==========
        // 批次级重量 CurrentValidWeight 按紧急程度分组
        // COALESCE: WorkOrderPlan.UrgencyLevel 优先，回退 WorkOrderExecutionSummary.UrgencyLevel
        var stage2Data = await (from b in _context.ProductionBatches.AsNoTracking()
                                where b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress
                                join s in _context.Set<WorkOrderExecutionSummary>().AsNoTracking()
                                    on b.WorkOrderNo equals s.WorkOrderNo into sj
                                from s in sj.DefaultIfEmpty()
                                join plan in _context.Set<WorkOrderPlan>().AsNoTracking()
                                    on s.WorkOrderId equals plan.WorkOrderId into planj
                                from plan in planj.DefaultIfEmpty()
                                select new
                                {
                                    b.WorkOrderNo,
                                    Weight = b.CurrentValidWeight ?? 0m,
                                    PlanUrgency = plan != null ? plan.UrgencyLevel : null,
                                    SummaryUrgency = s != null ? s.UrgencyLevel : null
                                }).ToListAsync();

        var stage2Grouped = stage2Data
            .Select(x => new
            {
                x.WorkOrderNo,
                x.Weight,
                UrgencyLevel = x.PlanUrgency ?? x.SummaryUrgency
            })
            .Where(x => x.UrgencyLevel != null)
            .GroupBy(x => x.UrgencyLevel!)
            .Select(g => new WorkOrderExecutionDashboardItem
            {
                ScheduleStage = 2,
                UrgencyLevel = g.Key,
                OrderCount = g.Select(x => x.WorkOrderNo).Distinct().Count(),
                TotalWeight = g.Sum(x => x.Weight)
            });
        result.AddRange(stage2Grouped);

        // ========== Stage 3: 成品检验 ==========
        // 成检阶段 = "待检验" + "检验中"
        // 逻辑同 FinalInspectionPlanService.BuildInProcessAsync

        // 1. MaterialReceiveCheck（排除强制完成）
        var receiveBatchIds = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(rc => !rc.IsForceCompleted)
            .Select(rc => rc.ProductionBatchId)
            .Distinct()
            .ToListAsync();
        var receivedSet = receiveBatchIds.ToHashSet();

        if (receivedSet.Count > 0)
        {
            // 2. FinalInspections 已检批次
            var inspectedIds = await _context.FinalInspections
                .AsNoTracking()
                .Select(fi => fi.ProductionBatchId)
                .Distinct()
                .ToListAsync();
            var inspectedSet = inspectedIds.ToHashSet();

            // 3. 已入库批次
            var warehousedNos = await _context.InventoryBatches
                .AsNoTracking()
                .Where(ib => ib.ProductionBatchNo != null)
                .Select(ib => ib.ProductionBatchNo!)
                .Distinct()
                .ToListAsync();
            var warehousedSet = warehousedNos.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 4. 加载已到料批次数据
            var batchData = await _context.ProductionBatches.AsNoTracking()
                .Where(b => receivedSet.Contains(b.Id))
                .Select(b => new
                {
                    b.Id,
                    b.BatchNo,
                    b.WorkOrderNo,
                    Weight = b.CurrentValidWeight ?? 0m
                })
                .ToListAsync();

            // 5. 过滤：排除已入库 → 仅保留 待检验 + 检验中
            var validBatches = batchData
                .Where(b => b.BatchNo == null || !warehousedSet.Contains(b.BatchNo))
                .ToList();

            if (validBatches.Count > 0)
            {
                var woNos = validBatches.Select(b => b.WorkOrderNo).Distinct().ToList();
                var summaries = await _context.Set<WorkOrderExecutionSummary>()
                    .AsNoTracking()
                    .Where(s => woNos.Contains(s.WorkOrderNo))
                    .Select(s => new { s.WorkOrderNo, s.UrgencyLevel })
                    .ToListAsync();

                var urgencyLookup = summaries
                    .GroupBy(s => s.WorkOrderNo)
                    .ToDictionary(g => g.Key, g => g.First().UrgencyLevel, StringComparer.OrdinalIgnoreCase);

                var stage3Items = validBatches
                    .Select(b => new
                    {
                        UrgencyLevel = b.WorkOrderNo != null && urgencyLookup.TryGetValue(b.WorkOrderNo, out var u) ? u : null,
                        b.Weight,
                        b.WorkOrderNo
                    })
                    .Where(x => x.UrgencyLevel != null)
                    .GroupBy(x => x.UrgencyLevel!)
                    .Select(g => new WorkOrderExecutionDashboardItem
                    {
                        ScheduleStage = 3,
                        UrgencyLevel = g.Key,
                        OrderCount = g.Select(x => x.WorkOrderNo).Distinct().Count(),
                        TotalWeight = g.Sum(x => x.Weight)
                    })
                    .ToList();

                result.AddRange(stage3Items);
            }
        }

        return result;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("WorkOrderExecutionService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var query = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();

            var all = await query
                .Select(s => new
                {
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
                    s.ProductionFlowProperty,
                    s.ProductionAttentionProcess,
                    s.MainNoAttentionProcess,
                    s.AdjustmentRemark,
                })
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
                ["ProductionFlowProperty"] = new List<string> { "暂停", "正常", "待料", "疑问", "略" },
                ["ProductionAttentionProcess"] = all
                    .Where(x => x.ProductionAttentionProcess != null)
                    .Select(x => x.ProductionAttentionProcess!)
                    .Distinct()
                    .OrderBy(x => x)
                    .Union(new[] { "收尾-成检" })
                    .ToList(),
                ["AdjustmentRemark"] = all.Where(x => x.AdjustmentRemark != null).Select(x => x.AdjustmentRemark!).Distinct().OrderBy(x => x).ToList(),
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    private static IQueryable<WorkOrderExecutionSummary> ApplySorting(
        IQueryable<WorkOrderExecutionSummary> query, string sortBy, bool isDescending)
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
            ("materialplanstatus", false) => query.OrderBy(x => x.MaterialPlanStatus),
            ("materialplanstatus", true) => query.OrderByDescending(x => x.MaterialPlanStatus),
            ("mainnomaterialplanstatus", false) => query.OrderBy(x => x.MainNoMaterialPlanStatus),
            ("mainnomaterialplanstatus", true) => query.OrderByDescending(x => x.MainNoMaterialPlanStatus),
            ("processcycle", false) => query.OrderBy(x => x.ProcessCycle),
            ("processcycle", true) => query.OrderByDescending(x => x.ProcessCycle),
            ("materialplancoveredcount", false) => query.OrderBy(x => x.MaterialPlanCoveredCount),
            ("materialplancoveredcount", true) => query.OrderByDescending(x => x.MaterialPlanCoveredCount),
            ("materialplanproportion", false) => query.OrderBy(x => x.MaterialPlanProportion ?? ""),
            ("materialplanproportion", true) => query.OrderByDescending(x => x.MaterialPlanProportion ?? ""),
            ("latestrequireddate", false) => query.OrderBy(x => x.LatestRequiredDate),
            ("latestrequireddate", true) => query.OrderByDescending(x => x.LatestRequiredDate),
            ("pendingroughtubeqty", false) => query.OrderBy(x => x.PendingRoughTubeQty),
            ("pendingroughtubeqty", true) => query.OrderByDescending(x => x.PendingRoughTubeQty),
            ("pendingroughtubeweight", false) => query.OrderBy(x => x.PendingRoughTubeWeight),
            ("pendingroughtubeweight", true) => query.OrderByDescending(x => x.PendingRoughTubeWeight),
            ("pendingoutsourcefinishqty", false) => query.OrderBy(x => x.PendingOutsourceFinishQty),
            ("pendingoutsourcefinishqty", true) => query.OrderByDescending(x => x.PendingOutsourceFinishQty),
            ("pendingoutsourcefinishweight", false) => query.OrderBy(x => x.PendingOutsourceFinishWeight),
            ("pendingoutsourcefinishweight", true) => query.OrderByDescending(x => x.PendingOutsourceFinishWeight),
            ("theoreticalfinishqty", false) => query.OrderBy(x => x.TheoreticalFinishQty),
            ("theoreticalfinishqty", true) => query.OrderByDescending(x => x.TheoreticalFinishQty),
            ("theoreticalfinishweight", false) => query.OrderBy(x => x.TheoreticalFinishWeight),
            ("theoreticalfinishweight", true) => query.OrderByDescending(x => x.TheoreticalFinishWeight),

            // G6
            ("reworkinputenddate", false) => query.OrderBy(x => x.ReworkInputEndDate),
            ("reworkinputenddate", true) => query.OrderByDescending(x => x.ReworkInputEndDate),
            ("reworkbatchcount", false) => query.OrderBy(x => x.ReworkBatchCount),
            ("reworkbatchcount", true) => query.OrderByDescending(x => x.ReworkBatchCount),
            ("reworkinputquantity", false) => query.OrderBy(x => x.ReworkInputQuantity),
            ("reworkinputquantity", true) => query.OrderByDescending(x => x.ReworkInputQuantity),
            ("reworkinputweight", false) => query.OrderBy(x => x.ReworkInputWeight),
            ("reworkinputweight", true) => query.OrderByDescending(x => x.ReworkInputWeight),
            ("reworktheoreticaloutputqty", false) => query.OrderBy(x => x.ReworkTheoreticalOutputQty),
            ("reworktheoreticaloutputqty", true) => query.OrderByDescending(x => x.ReworkTheoreticalOutputQty),
            ("reworktheoreticaloutputweight", false) => query.OrderBy(x => x.ReworkTheoreticalOutputWeight),
            ("reworktheoreticaloutputweight", true) => query.OrderByDescending(x => x.ReworkTheoreticalOutputWeight),

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
            ("mainnoinputstatus", false) => query.OrderBy(x => x.MainNoInputStatus),
            ("mainnoinputstatus", true) => query.OrderByDescending(x => x.MainNoInputStatus),
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
            // G7
            ("flowoutputratio", false) => query.OrderBy(x => x.FlowOutputRatio),
            ("flowoutputratio", true) => query.OrderByDescending(x => x.FlowOutputRatio),
            ("mainnoflowoutputratio", false) => query.OrderBy(x => x.MainNoFlowOutputRatio),
            ("mainnoflowoutputratio", true) => query.OrderByDescending(x => x.MainNoFlowOutputRatio),
            ("flowtotalbatchcount", false) => query.OrderBy(x => x.FlowTotalBatchCount),
            ("flowtotalbatchcount", true) => query.OrderByDescending(x => x.FlowTotalBatchCount),
            ("flowincompletebatchcount", false) => query.OrderBy(x => x.FlowIncompleteBatchCount),
            ("flowincompletebatchcount", true) => query.OrderByDescending(x => x.FlowIncompleteBatchCount),
            ("flowmaxremainingworkdays", false) => query.OrderBy(x => x.FlowMaxRemainingWorkDays),
            ("flowmaxremainingworkdays", true) => query.OrderByDescending(x => x.FlowMaxRemainingWorkDays),
            ("flowstatus", false) => query.OrderBy(x => x.FlowStatus),
            ("flowstatus", true) => query.OrderByDescending(x => x.FlowStatus),
            ("mainnoflowstatus", false) => query.OrderBy(x => x.MainNoFlowStatus),
            ("mainnoflowstatus", true) => query.OrderByDescending(x => x.MainNoFlowStatus),

            // G8
            ("defectiverawqty", false) => query.OrderBy(x => x.DefectiveRawQty),
            ("defectiverawqty", true) => query.OrderByDescending(x => x.DefectiveRawQty),
            ("defectiverawweight", false) => query.OrderBy(x => x.DefectiveRawWeight),
            ("defectiverawweight", true) => query.OrderByDescending(x => x.DefectiveRawWeight),
            ("defectiveoutputqty", false) => query.OrderBy(x => x.DefectiveOutputQty),
            ("defectiveoutputqty", true) => query.OrderByDescending(x => x.DefectiveOutputQty),
            ("defectiveoutputweight", false) => query.OrderBy(x => x.DefectiveOutputWeight),
            ("defectiveoutputweight", true) => query.OrderByDescending(x => x.DefectiveOutputWeight),
            ("defectiveratio", false) => query.OrderBy(x => x.DefectiveRatio),
            ("defectiveratio", true) => query.OrderByDescending(x => x.DefectiveRatio),

            // G9
            ("inspectiondefectqty", false) => query.OrderBy(x => x.InspectionDefectQty),
            ("inspectiondefectqty", true) => query.OrderByDescending(x => x.InspectionDefectQty),
            ("inspectiondefectweight", false) => query.OrderBy(x => x.InspectionDefectWeight),
            ("inspectiondefectweight", true) => query.OrderByDescending(x => x.InspectionDefectWeight),
            ("inspectiondefectratio", false) => query.OrderBy(x => x.InspectionDefectRatio),
            ("inspectiondefectratio", true) => query.OrderByDescending(x => x.InspectionDefectRatio),
            ("inspectionstartdate", false) => query.OrderBy(x => x.InspectionStartDate),
            ("inspectionstartdate", true) => query.OrderByDescending(x => x.InspectionStartDate),
            ("inspectionenddate", false) => query.OrderBy(x => x.InspectionEndDate),
            ("inspectionenddate", true) => query.OrderByDescending(x => x.InspectionEndDate),

            // G10
            ("generaldefectweight", false) => query.OrderBy(x => x.GeneralDefectWeight),
            ("generaldefectweight", true) => query.OrderByDescending(x => x.GeneralDefectWeight),
            ("generaldefectratio", false) => query.OrderBy(x => x.GeneralDefectRatio),
            ("generaldefectratio", true) => query.OrderByDescending(x => x.GeneralDefectRatio),
            ("seriousdefectweight", false) => query.OrderBy(x => x.SeriousDefectWeight),
            ("seriousdefectweight", true) => query.OrderByDescending(x => x.SeriousDefectWeight),
            ("seriousdefectratio", false) => query.OrderBy(x => x.SeriousDefectRatio),
            ("seriousdefectratio", true) => query.OrderByDescending(x => x.SeriousDefectRatio),
            ("scrapweight", false) => query.OrderBy(x => x.ScrapWeight),
            ("scrapweight", true) => query.OrderByDescending(x => x.ScrapWeight),
            ("scrapratio", false) => query.OrderBy(x => x.ScrapRatio),
            ("scrapratio", true) => query.OrderByDescending(x => x.ScrapRatio),

            // G11
            ("warehousingstartdate", false) => query.OrderBy(x => x.WarehousingStartDate),
            ("warehousingstartdate", true) => query.OrderByDescending(x => x.WarehousingStartDate),
            ("warehousingenddate", false) => query.OrderBy(x => x.WarehousingEndDate),
            ("warehousingenddate", true) => query.OrderByDescending(x => x.WarehousingEndDate),
            ("warehousingtotalqty", false) => query.OrderBy(x => x.WarehousingTotalQty),
            ("warehousingtotalqty", true) => query.OrderByDescending(x => x.WarehousingTotalQty),
            ("warehousingtotalweight", false) => query.OrderBy(x => x.WarehousingTotalWeight),
            ("warehousingtotalweight", true) => query.OrderByDescending(x => x.WarehousingTotalWeight),
            ("wowarehousingstatus", false) => query.OrderBy(x => x.WoWarehousingStatus),
            ("wowarehousingstatus", true) => query.OrderByDescending(x => x.WoWarehousingStatus),
            ("mainnowarehousingstatus", false) => query.OrderBy(x => x.MainNoWarehousingStatus),
            ("mainnowarehousingstatus", true) => query.OrderByDescending(x => x.MainNoWarehousingStatus),
            ("orderwarehousingstatus", false) => query.OrderBy(x => x.OrderWarehousingStatus),
            ("orderwarehousingstatus", true) => query.OrderByDescending(x => x.OrderWarehousingStatus),

            // G12
            ("schedulestage", false) => query.OrderBy(x => x.ScheduleStage),
            ("schedulestage", true) => query.OrderByDescending(x => x.ScheduleStage),
            ("totalremainingworkdays", false) => query.OrderBy(x => x.TotalRemainingWorkDays),
            ("totalremainingworkdays", true) => query.OrderByDescending(x => x.TotalRemainingWorkDays),
            ("urgencylevel", false) => query.OrderBy(x => x.UrgencyLevel),
            ("urgencylevel", true) => query.OrderByDescending(x => x.UrgencyLevel),
            ("estimatedprocesscompletiondate", false) => query.OrderBy(x => x.EstimatedProcessCompletionDate),
            ("estimatedprocesscompletiondate", true) => query.OrderByDescending(x => x.EstimatedProcessCompletionDate),
            ("daysdifffromdelivery", false) => query.OrderBy(x => x.DaysDiffFromDelivery),
            ("daysdifffromdelivery", true) => query.OrderByDescending(x => x.DaysDiffFromDelivery),
            ("capacityworkdays", false) => query.OrderBy(x => x.CapacityWorkDays),
            ("capacityworkdays", true) => query.OrderByDescending(x => x.CapacityWorkDays),
            ("rawmateriallockremark", false) => query.OrderBy(x => x.RawMaterialLockRemark ?? ""),
            ("rawmateriallockremark", true) => query.OrderByDescending(x => x.RawMaterialLockRemark ?? ""),

            // G14
            ("pendingsectionroughtube", false) => query.OrderBy(x => x.PendingSectionRoughTube),
            ("pendingsectionroughtube", true) => query.OrderByDescending(x => x.PendingSectionRoughTube),
            ("pendingsectionwarehousefix", false) => query.OrderBy(x => x.PendingSectionWarehouseFix),
            ("pendingsectionwarehousefix", true) => query.OrderByDescending(x => x.PendingSectionWarehouseFix),
            ("pendingsection60roll", false) => query.OrderBy(x => x.PendingSection60Roll),
            ("pendingsection60roll", true) => query.OrderByDescending(x => x.PendingSection60Roll),
            ("pendingsection50roll", false) => query.OrderBy(x => x.PendingSection50Roll),
            ("pendingsection50roll", true) => query.OrderByDescending(x => x.PendingSection50Roll),
            ("pendingsection30roll", false) => query.OrderBy(x => x.PendingSection30Roll),
            ("pendingsection30roll", true) => query.OrderByDescending(x => x.PendingSection30Roll),
            ("pendingsection20roll", false) => query.OrderBy(x => x.PendingSection20Roll),
            ("pendingsection20roll", true) => query.OrderByDescending(x => x.PendingSection20Roll),
            ("pendingsectionthreeroll", false) => query.OrderBy(x => x.PendingSectionThreeRoll),
            ("pendingsectionthreeroll", true) => query.OrderByDescending(x => x.PendingSectionThreeRoll),
            ("pendingsectiondrawbench", false) => query.OrderBy(x => x.PendingSectionDrawBench),
            ("pendingsectiondrawbench", true) => query.OrderByDescending(x => x.PendingSectionDrawBench),
            ("deformedprocesscompleted", false) => query.OrderBy(x => x.DeformedProcessCompleted),
            ("deformedprocesscompleted", true) => query.OrderByDescending(x => x.DeformedProcessCompleted),
            ("productionattentionprocess", false) => query.OrderBy(x => x.ProductionAttentionProcess ?? ""),
            ("productionattentionprocess", true) => query.OrderByDescending(x => x.ProductionAttentionProcess ?? ""),

            // Group 13
            ("isurging", false) => query.OrderBy(x => x.IsUrging),
            ("isurging", true) => query.OrderByDescending(x => x.IsUrging),
            ("isbatchdelivery", false) => query.OrderBy(x => x.IsBatchDelivery),
            ("isbatchdelivery", true) => query.OrderByDescending(x => x.IsBatchDelivery),
            ("ispaused", false) => query.OrderBy(x => x.IsPaused),
            ("ispaused", true) => query.OrderByDescending(x => x.IsPaused),
            ("adjustmentremark", false) => query.OrderBy(x => x.AdjustmentRemark ?? ""),
            ("adjustmentremark", true) => query.OrderByDescending(x => x.AdjustmentRemark ?? ""),
            ("productionflowproperty", false) => query.OrderBy(x => x.ProductionFlowProperty ?? ""),
            ("productionflowproperty", true) => query.OrderByDescending(x => x.ProductionFlowProperty ?? ""),

            _ => query.OrderByDescending(x => x.LastRefreshTime),
        };
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

    /// <summary>
    /// 获取指定工段在工序组中的执行序号（用于工段级到达判断）
    /// 对应 ProductionOverviewService.ProcessGroupInfo.GetSectionSequence
    /// </summary>
    private static int? GetSectionSequence(ProcessGroup pg, string sectionName) => sectionName switch
    {
        SectionDefs.ColdRollDraw => pg.ColdRollDraw,
        SectionDefs.OilPipeCut => pg.OilPipeCut,
        SectionDefs.Degrease => pg.Degrease,
        SectionDefs.Solution => pg.Solution,
        SectionDefs.Straighten => pg.Straighten,
        SectionDefs.Cut => pg.Cut,
        SectionDefs.ThicknessMeasure => pg.ThicknessMeasure,
        SectionDefs.Pickle => pg.Pickle,
        SectionDefs.OuterPolish => pg.OuterPolish,
        SectionDefs.InnerGrinding => pg.InnerGrinding,
        SectionDefs.OuterSpotGrinding => pg.OuterSpotGrinding,
        SectionDefs.Inspection => pg.Inspection,
        SectionDefs.WeldingHead => pg.WeldingHead,
        SectionDefs.Lubrication => pg.Lubrication,
        SectionDefs.Warehouse => pg.Warehouse,
        _ => null
    };

    // ========== 打印 ==========

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, DateTime? signDateFrom, DateTime? signDateTo, List<PrintColumnDef> columns)
    {
        var q = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();

        // 签订日期范围筛选
        if (signDateFrom.HasValue)
            q = q.Where(x => x.SignDate >= signDateFrom.Value);
        if (signDateTo.HasValue)
            q = q.Where(x => x.SignDate < signDateTo.Value.AddDays(1));

        // 关键字搜索
        if (!string.IsNullOrEmpty(keyword))
        {
            var kw = keyword;
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
                (x.ProductionSubNo != null && x.ProductionSubNo.Contains(kw)) ||
                (x.UrgencyLevel != null && x.UrgencyLevel.Contains(kw)) ||
                (x.RawMaterialLockRemark != null && x.RawMaterialLockRemark.Contains(kw)) ||
                (x.ProductionAttentionProcess != null && x.ProductionAttentionProcess.Contains(kw)) ||
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw)) ||
                (x.ProductionFlowProperty != null && x.ProductionFlowProperty.Contains(kw)) ||
                (x.MainNoAttentionProcess != null && x.MainNoAttentionProcess.Contains(kw)));
        }

        // 排序
        q = ApplySorting(q, sortBy ?? "LastRefreshTime", isDescending);

        var rawEntities = await q.ToListAsync();

        var items = rawEntities.Select(e => new WorkOrderExecutionSummaryDto
        {
            Id = e.Id,
            WorkOrderId = e.WorkOrderId,
            WorkOrderNo = e.WorkOrderNo,
            LastRefreshTime = e.LastRefreshTime,
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
            LatestPlanDate = e.LatestPlanDate,
            MaterialPlanRate = e.MaterialPlanRate,
            MaterialPlanStatus = (MaterialPlanStatus)e.MaterialPlanStatus,
            MainNoMaterialPlanRate = e.MainNoMaterialPlanRate,
            MainNoMaterialPlanStatus = (MaterialPlanStatus)e.MainNoMaterialPlanStatus,
            ProcessCycle = e.ProcessCycle,
            MaterialPlanCoveredCount = e.MaterialPlanCoveredCount,
            MaterialPlanProportion = e.MaterialPlanProportion,
            LatestRequiredDate = e.LatestRequiredDate,
            PendingRoughTubeQty = e.PendingRoughTubeQty,
            PendingRoughTubeWeight = e.PendingRoughTubeWeight,
            PendingOutsourceFinishQty = e.PendingOutsourceFinishQty,
            PendingOutsourceFinishWeight = e.PendingOutsourceFinishWeight,
            TheoreticalFinishQty = e.TheoreticalFinishQty,
            TheoreticalFinishWeight = e.TheoreticalFinishWeight,
            ReworkInputEndDate = e.ReworkInputEndDate,
            ReworkBatchCount = e.ReworkBatchCount,
            ReworkInputQuantity = e.ReworkInputQuantity,
            ReworkInputWeight = e.ReworkInputWeight,
            ReworkTheoreticalOutputQty = e.ReworkTheoreticalOutputQty,
            ReworkTheoreticalOutputWeight = e.ReworkTheoreticalOutputWeight,
            FlowOutputRatio = e.FlowOutputRatio,
            FlowStatus = e.FlowStatus,
            MainNoFlowOutputRatio = e.MainNoFlowOutputRatio,
            MainNoFlowStatus = e.MainNoFlowStatus,
            FlowTotalBatchCount = e.FlowTotalBatchCount,
            FlowIncompleteBatchCount = e.FlowIncompleteBatchCount,
            FlowMaxRemainingWorkDays = e.FlowMaxRemainingWorkDays,
            DefectiveRawQty = e.DefectiveRawQty,
            DefectiveRawWeight = e.DefectiveRawWeight,
            DefectiveOutputQty = e.DefectiveOutputQty,
            DefectiveOutputWeight = e.DefectiveOutputWeight,
            DefectiveRatio = e.DefectiveRatio,
            InspectionStartDate = e.InspectionStartDate,
            InspectionEndDate = e.InspectionEndDate,
            InspectionDefectQty = e.InspectionDefectQty,
            InspectionDefectWeight = e.InspectionDefectWeight,
            InspectionDefectRatio = e.InspectionDefectRatio,
            GeneralDefectWeight = e.GeneralDefectWeight,
            GeneralDefectRatio = e.GeneralDefectRatio,
            SeriousDefectWeight = e.SeriousDefectWeight,
            SeriousDefectRatio = e.SeriousDefectRatio,
            ScrapWeight = e.ScrapWeight,
            ScrapRatio = e.ScrapRatio,
            WarehousingStartDate = e.WarehousingStartDate,
            WarehousingEndDate = e.WarehousingEndDate,
            WarehousingTotalQty = e.WarehousingTotalQty,
            WarehousingTotalWeight = e.WarehousingTotalWeight,
            WoWarehousingStatus = e.WoWarehousingStatus,
            MainNoWarehousingStatus = e.MainNoWarehousingStatus,
            OrderWarehousingStatus = e.OrderWarehousingStatus,
            ScheduleStage = e.ScheduleStage,
            TotalRemainingWorkDays = e.TotalRemainingWorkDays,
            CapacityWorkDays = e.CapacityWorkDays,
            UrgencyLevel = e.UrgencyLevel,
            EstimatedProcessCompletionDate = e.EstimatedProcessCompletionDate,
            DaysDiffFromDelivery = e.DaysDiffFromDelivery,
            RawMaterialLockRemark = e.RawMaterialLockRemark,
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
            ValidBatchCount = e.ValidBatchCount,
            ValidInputQuantity = e.ValidInputQuantity,
            ValidInputWeight = e.ValidInputWeight,
            ValidOutputQty = e.ValidOutputQty,
            ValidOutputWeight = e.ValidOutputWeight,
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
            MaxBatchRemainingWorkDays = e.MaxBatchRemainingWorkDays,
            MainNoAttentionProcess = e.MainNoAttentionProcess,
            IsUrging = e.IsUrging,
            IsBatchDelivery = e.IsBatchDelivery,
            IsPaused = e.IsPaused,
            AdjustmentRemark = e.AdjustmentRemark,
            ProductionFlowProperty = e.ProductionFlowProperty,
        }).ToList();

        var resolvedItems = items.Select(item =>
        {
            var dict = new Dictionary<string, object>();
            foreach (var col in columns)
            {
                dict[col.Key] = ResolvePrintValue(item, col.Key);
            }
            return dict;
        }).ToList();

        return WorkOrderExecutionPrintHelper.GeneratePdf("工单执行状况", resolvedItems, columns);
    }

    private static object ResolvePrintValue(WorkOrderExecutionSummaryDto item, string key) => key switch
    {
        // 枚举→中文
        "SettlementMethod" => GetSettlementMethodText(item.SettlementMethod.ToString()),
        "MaterialName" => GetPipeManufacturingTypeText(item.MaterialName),
        "DeliveryState" => GetDeliveryStateText(item.DeliveryState.ToString()),
        "LengthStatus" => GetLengthStatusText(item.LengthStatus.ToString()),
        // Bool→中文
        "DelayPenalty" => item.DelayPenaltyText,
        "IsUrging" => item.IsUrging ? "是" : "否",
        "IsBatchDelivery" => item.IsBatchDelivery ? "是" : "否",
        "IsPaused" => item.IsPaused ? "是" : "否",
        "DeformedProcessCompleted" => item.DeformedProcessCompleted ? "是" : "否",
        // 状态 int→中文
        "MaterialPlanStatus" => item.MaterialPlanStatusText,
        "MainNoMaterialPlanStatus" => item.MainNoMaterialPlanStatusText,
        "InputStatus" => item.InputStatusText,
        "MainNoInputStatus" => item.MainNoInputStatusText,
        "FlowStatus" => item.FlowStatusText,
        "MainNoFlowStatus" => item.MainNoFlowStatusText,
        "WoWarehousingStatus" => item.WoWarehousingStatusText,
        "MainNoWarehousingStatus" => item.MainNoWarehousingStatusText,
        "OrderWarehousingStatus" => item.OrderWarehousingStatusText,
        "ScheduleStage" => item.ScheduleStageText,
        // 日期格式
        "SignDate" => item.SignDate.ToString("yyyy-MM-dd"),
        "DeliveryDate" => item.DeliveryDate.ToString("yyyy-MM-dd"),
        "LatestPlanDate" => item.LatestPlanDate?.ToString("yyyy-MM-dd") ?? "",
        "LatestRequiredDate" => item.LatestRequiredDate?.ToString("yyyy-MM-dd") ?? "",
        "InputStartDate" => item.InputStartDate?.ToString("yyyy-MM-dd") ?? "",
        "InputEndDate" => item.InputEndDate?.ToString("yyyy-MM-dd") ?? "",
        "ReworkInputEndDate" => item.ReworkInputEndDate?.ToString("yyyy-MM-dd") ?? "",
        "InspectionStartDate" => item.InspectionStartDate?.ToString("yyyy-MM-dd") ?? "",
        "InspectionEndDate" => item.InspectionEndDate?.ToString("yyyy-MM-dd") ?? "",
        "WarehousingStartDate" => item.WarehousingStartDate?.ToString("yyyy-MM-dd") ?? "",
        "WarehousingEndDate" => item.WarehousingEndDate?.ToString("yyyy-MM-dd") ?? "",
        "EstimatedProcessCompletionDate" => item.EstimatedProcessCompletionDate?.ToString("yyyy-MM-dd") ?? "",
        // 比率→百分比格式
        "MaterialPlanRate" => item.MaterialPlanRate.ToString("F1") + "%",
        "MainNoMaterialPlanRate" => item.MainNoMaterialPlanRate.ToString("F1") + "%",
        "FlowOutputRatio" => item.FlowOutputRatio.ToString("F1") + "%",
        "MainNoFlowOutputRatio" => item.MainNoFlowOutputRatio.ToString("F1") + "%",
        "InputOutputRatio" => item.InputOutputRatio.ToString("F1") + "%",
        "MainNoInputRatio" => item.MainNoInputOutputRatio.ToString("F1") + "%",
        "DefectiveRatio" => item.DefectiveRatio.ToString("F1") + "%",
        "InspectionDefectRatio" => item.InspectionDefectRatio.ToString("F1") + "%",
        "GeneralDefectRatio" => item.GeneralDefectRatio.ToString("F1") + "%",
        "SeriousDefectRatio" => item.SeriousDefectRatio.ToString("F1") + "%",
        "ScrapRatio" => item.ScrapRatio.ToString("F1") + "%",
        // 通用字符串/数值
        _ => GetRawPrintValue(item, key)
    };

    private static object GetRawPrintValue(WorkOrderExecutionSummaryDto item, string key) => (key switch
    {
        "WorkOrderNo" => item.WorkOrderNo ?? "",
        "Salesman" => item.Salesman ?? "",
        "CustomerName" => item.CustomerName ?? "",
        "SalesOrderNo" => item.SalesOrderNo ?? "",
        "ProductionMainNo" => item.ProductionMainNo ?? "",
        "ProductionSubNo" => item.ProductionSubNo ?? "",
        "PlantGrade" => item.PlantGrade ?? "",
        "Specification" => item.Specification ?? "",
        "MinLength" => item.MinLength,
        "MaxLength" => item.MaxLength,
        "TotalItemCount" => item.TotalItemCount,
        "TotalQuantity" => item.TotalQuantity,
        "TotalMeters" => item.TotalMeters,
        "TotalWeight" => item.TotalWeight,
        "ProcessCycle" => item.ProcessCycle,
        "MaterialPlanCoveredCount" => item.MaterialPlanCoveredCount,
        "MaterialPlanProportion" => item.MaterialPlanProportion ?? "",
        "PendingRoughTubeQty" => item.PendingRoughTubeQty,
        "PendingRoughTubeWeight" => item.PendingRoughTubeWeight,
        "PendingOutsourceFinishQty" => item.PendingOutsourceFinishQty,
        "PendingOutsourceFinishWeight" => item.PendingOutsourceFinishWeight,
        "TheoreticalFinishQty" => item.TheoreticalFinishQty,
        "TheoreticalFinishWeight" => item.TheoreticalFinishWeight,
        "ReworkBatchCount" => item.ReworkBatchCount,
        "ReworkInputQuantity" => item.ReworkInputQuantity,
        "ReworkInputWeight" => item.ReworkInputWeight,
        "ReworkTheoreticalOutputQty" => item.ReworkTheoreticalOutputQty,
        "ReworkTheoreticalOutputWeight" => item.ReworkTheoreticalOutputWeight,
        "TotalBatchCount" => item.TotalBatchCount,
        "InputQuantity" => item.InputQuantity,
        "InputWeight" => item.InputWeight,
        "TheoreticalOutputQty" => item.TheoreticalOutputQty,
        "TheoreticalOutputWeight" => item.TheoreticalOutputWeight,
        "ValidBatchCount" => item.ValidBatchCount,
        "ValidInputQuantity" => item.ValidInputQuantity,
        "ValidInputWeight" => item.ValidInputWeight,
        "ValidOutputQty" => item.ValidOutputQty,
        "ValidOutputWeight" => item.ValidOutputWeight,
        "FlowTotalBatchCount" => item.FlowTotalBatchCount,
        "FlowIncompleteBatchCount" => item.FlowIncompleteBatchCount,
        "FlowMaxRemainingWorkDays" => item.FlowMaxRemainingWorkDays,
        "DefectiveRawQty" => item.DefectiveRawQty,
        "DefectiveRawWeight" => item.DefectiveRawWeight,
        "DefectiveOutputQty" => item.DefectiveOutputQty,
        "DefectiveOutputWeight" => item.DefectiveOutputWeight,
        "InspectionDefectQty" => item.InspectionDefectQty,
        "InspectionDefectWeight" => item.InspectionDefectWeight,
        "GeneralDefectWeight" => item.GeneralDefectWeight,
        "SeriousDefectWeight" => item.SeriousDefectWeight,
        "ScrapWeight" => item.ScrapWeight,
        "WarehousingTotalQty" => item.WarehousingTotalQty,
        "WarehousingTotalWeight" => item.WarehousingTotalWeight,
        "TotalRemainingWorkDays" => item.TotalRemainingWorkDays,
        "CapacityWorkDays" => item.CapacityWorkDays,
        "UrgencyLevel" => item.UrgencyLevel ?? "",
        "DaysDiffFromDelivery" => item.DaysDiffFromDelivery,
        "RawMaterialLockRemark" => item.RawMaterialLockRemark ?? "",
        "AdjustmentRemark" => item.AdjustmentRemark ?? "",
        "PendingSectionRoughTube" => item.PendingSectionRoughTube,
        "PendingSectionWarehouseFix" => item.PendingSectionWarehouseFix,
        "PendingSection60Roll" => item.PendingSection60Roll,
        "PendingSection50Roll" => item.PendingSection50Roll,
        "PendingSection30Roll" => item.PendingSection30Roll,
        "PendingSection20Roll" => item.PendingSection20Roll,
        "PendingSectionThreeRoll" => item.PendingSectionThreeRoll,
        "PendingSectionDrawBench" => item.PendingSectionDrawBench,
        "ProductionAttentionProcess" => item.ProductionAttentionProcess ?? "",
        "ProductionFlowProperty" => item.ProductionFlowProperty ?? "",
        "MaxBatchRemainingWorkDays" => item.MaxBatchRemainingWorkDays,
        "MainNoAttentionProcess" => item.MainNoAttentionProcess ?? "",
        // 主号流转比（ColumnDef Key 与 DTO 属性名不一致：Key=MainNoFlowRatio, DTO=MainNoFlowOutputRatio）
        "MainNoFlowRatio" => item.MainNoFlowOutputRatio,
        _ => ""
    })!;

    private static string GetPipeManufacturingTypeText(string? pipeManufacturingType) => pipeManufacturingType switch
    {
        "SeamlessPipe" => "无缝管",
        "WeldedPipe" => "焊管",
        _ => pipeManufacturingType ?? ""
    };

    private static string GetDeliveryStateText(string? deliveryState) => deliveryState switch
    {
        "SolutionAnnealedAndPickled" => "固溶酸洗",
        "SolutionAnnealedAndPickledUTube" => "固溶酸洗-U型管",
        "SolutionAnnealedAndPickledExternalPolished" => "固溶酸洗-外抛光",
        "SolutionAnnealedAndPickledInternalPolished" => "固溶酸洗-内抛光",
        "SolutionAnnealedAndPickledBothPolished" => "固溶酸洗-内外抛光",
        "SolutionAnnealedAndPickledCoiled" => "固溶酸洗-盘管",
        "Bright" => "光亮",
        "BrightUTube" => "光亮-U型管",
        "BrightCoiled" => "光亮-盘管",
        "Hard" => "硬态",
        _ => deliveryState ?? ""
    };

    private static string GetSettlementMethodText(string? method) => method switch
    {
        "Theoretical" => "理算",
        "Weighing" => "过磅",
        "WeighingNegative" => "过磅-负",
        _ => method ?? ""
    };

    private static string GetLengthStatusText(string? lengthStatus) => lengthStatus switch
    {
        "Fixed" => "定尺",
        "Range" => "范围尺",
        "NonFixed" => "非定尺",
        _ => lengthStatus ?? ""
    };

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var pdfBytes = WorkOrderExecutionPrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }
}
