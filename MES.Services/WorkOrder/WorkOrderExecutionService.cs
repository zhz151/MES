using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
using MES.Core.Helpers;
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
                (x.EndCustomer != null && x.EndCustomer.Contains(kw)) ||
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

        // G3 计算列（计划/可投/缺失总重、到料实投一致性）为 DTO 计算属性，通用反射筛选覆盖不到，单独表达式筛选
        q = ApplyComputedFilters(q, query.Filters);

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

                // Group 3
                MaterialPlanStatus = (MaterialPlanStatus)e.MaterialPlanStatus,
                MainNoMaterialPlanRate = e.MainNoMaterialPlanRate,
                MainNoMaterialPlanStatus = (MaterialPlanStatus)e.MainNoMaterialPlanStatus,
                MainNoPlanExecutionStatus = e.MainNoPlanExecutionStatus,
                MaterialPlanCoveredCount = e.MaterialPlanCoveredCount,
                MaterialPlanProportion = e.MaterialPlanProportion,
                TheoreticalCutoffDate = e.TheoreticalCutoffDate,
                CutoffArrivalDate = e.CutoffArrivalDate,
                MainNoCutoffArrivalDate = e.MainNoCutoffArrivalDate,

                // G4~G10: 7 种用料计划执行状况
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

                // Group 14
                ReworkTheoreticalProduceQty = e.ReworkTheoreticalProduceQty,
                ReworkTheoreticalProduceWeight = e.ReworkTheoreticalProduceWeight,
                PendingReworkOutputQty = e.PendingReworkOutputQty,
                PendingReworkOutputWeight = e.PendingReworkOutputWeight,
                ReworkMainNoStatus = e.ReworkMainNoStatus,
                ReworkInputConsistency = e.ReworkInputConsistency,
                ReworkInputEndDate = e.ReworkInputEndDate,
                ReworkBatchCount = e.ReworkBatchCount,
                ReworkInputQuantity = e.ReworkInputQuantity,
                ReworkInputWeight = e.ReworkInputWeight,
                ReworkTheoreticalOutputQty = e.ReworkTheoreticalOutputQty,
                ReworkTheoreticalOutputWeight = e.ReworkTheoreticalOutputWeight,

                // Group 15 次品总量
                ProcessInspectionDefectWeight = e.ProcessInspectionDefectWeight,
                ProcessInspectionReworkWeight = e.ProcessInspectionReworkWeight,
                ProcessInspectionWarehouseWeight = e.ProcessInspectionWarehouseWeight,
                ProcessInspectionScrapWeight = e.ProcessInspectionScrapWeight,
                FinalInspectionDefectQty = e.FinalInspectionDefectQty,
                FinalInspectionDefectWeight = e.FinalInspectionDefectWeight,
                FinalInspectionReworkWeight = e.FinalInspectionReworkWeight,
                FinalInspectionWarehouseWeight = e.FinalInspectionWarehouseWeight,
                FinalInspectionScrapWeight = e.FinalInspectionScrapWeight,

                // Group 12
                FlowOutputRatio = e.FlowOutputRatio,
                FlowStatus = e.FlowStatus,
                MainNoFlowOutputRatio = e.MainNoFlowOutputRatio,
                MainNoFlowStatus = e.MainNoFlowStatus,
                FlowTotalBatchCount = e.FlowTotalBatchCount,
                FlowIncompleteBatchCount = e.FlowIncompleteBatchCount,
                FlowMaxRemainingWorkDays = e.FlowMaxRemainingWorkDays,

                // Group 15
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

                // Group 11
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

                // Group 13
                ValidBatchCount = e.ValidBatchCount,
                ValidInputQuantity = e.ValidInputQuantity,
                ValidInputWeight = e.ValidInputWeight,
                ValidOutputQty = e.ValidOutputQty,
                ValidOutputWeight = e.ValidOutputWeight,

                // Group 17
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

                // Group 2
                IsUrging = e.IsUrging,
                IsBatchDelivery = e.IsBatchDelivery,
                IsPaused = e.IsPaused,
                IsForceCompleted = e.IsForceCompleted,
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
        var completeOverRatio = warehouseConfig.GetValueOrDefault("CompleteOverRatio", 1.05m);
        var bufferDays = workOrderDaysConfig.GetValueOrDefault("BufferDays", 3m);
        var inspectionFixedDays = workOrderDaysConfig.GetValueOrDefault("InspectionFixedDays", 3m);
        var urgencyAPlus = urgencyConfig.GetValueOrDefault("APlus", 7m);
        var urgencyA = urgencyConfig.GetValueOrDefault("A", -3m);
        var urgencyB = urgencyConfig.GetValueOrDefault("B", -10m);
        var urgencyC = urgencyConfig.GetValueOrDefault("C", -17m);
        var groupDiscountRate = processingDiscountConfig.GetValueOrDefault("GroupDiscountRate", 0.025m);
        var supplySatisfiedRate = materialPlanStatusConfig.GetValueOrDefault("SupplySatisfiedRate", 100m);
        var fixedSatisfied = materialPlanStatusConfig.GetValueOrDefault("FixedSatisfied", 110m);
        var nonFixedSatisfied = materialPlanStatusConfig.GetValueOrDefault("NonFixedSatisfied", 120m);
        var qualifiedRate = materialPlanStatusConfig.GetValueOrDefault("QualifiedRate", 98m) / 100m;
        var defaultValueConfig = await _configService.GetConfigMapAsync("DefaultValue");
        var roughTubeFinishRatio = defaultValueConfig.GetValueOrDefault("RoughTubeFinishRatio", 0.92m);
        var defaultProcessCycle = (int)defaultValueConfig.GetValueOrDefault("DefaultProcessCycle", 22m);

        var planToleranceConfig = await _configService.GetConfigMapAsync("MaterialPlanTolerance");
        var externalLower = planToleranceConfig.GetValueOrDefault("ExternalLower", 0.97m);
        var externalUpper = planToleranceConfig.GetValueOrDefault("ExternalUpper", 1.03m);
        var warehouseLower = planToleranceConfig.GetValueOrDefault("WarehouseLower", 0.95m);
        var warehouseUpper = planToleranceConfig.GetValueOrDefault("WarehouseUpper", 1.50m);
        var productionLower = planToleranceConfig.GetValueOrDefault("ProductionLower", 0.90m);
        var productionUpper = planToleranceConfig.GetValueOrDefault("ProductionUpper", 1.50m);

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
        var endCustomerByWo = new Dictionary<int, string>();
        foreach (var wo in workOrders)
        {
            var so = salesOrders.FirstOrDefault(s => s.OrderNumber.Equals(wo.SalesOrderNo, StringComparison.OrdinalIgnoreCase));
            customerNameByWo[wo.Id] = so?.CustomerName ?? "";
            customerSalesmanByWo[wo.Id] = so?.Salesman ?? "";
            endCustomerByWo[wo.Id] = so?.EndCustomer ?? "";
        }

        // 批量加载采购订单（用于 Group 10 物料执行实时信息）
        var purchaseOrders = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.SourceWorkOrderNo != null
                      && workOrderNos.Contains(po.SourceWorkOrderNo)
                      && po.Status != Core.Enums.PurchaseOrderStatus.Completed)
            .ToListAsync();

        var poByWoNo = purchaseOrders
            .GroupBy(po => po.SourceWorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 批量加载委外回收明细（用于 Group 10 物料执行实时信息，与采购订单逻辑相同）
        var returnItems = await _context.SubcontractReturnItems
            .AsNoTracking()
            .Where(ri => ri.SourceWorkOrderNo != null
                      && workOrderNos.Contains(ri.SourceWorkOrderNo))
            .ToListAsync();

        var riByWoNo = returnItems
            .GroupBy(ri => ri.SourceWorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 加载全部采购订单（含已完成，用于 G5~G6 用料计划执行状况）
        var allPurchaseOrders = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.SourceWorkOrderNo != null
                      && workOrderNos.Contains(po.SourceWorkOrderNo))
            .ToListAsync();
        var allPoByWoNo = allPurchaseOrders
            .GroupBy(po => po.SourceWorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 批量加载用料计划总览读模型（G3 字段已预计算，直接读取避免重算）
        var workOrderListSummaries = await _context.Set<WorkOrderListSummary>()
            .AsNoTracking()
            .Where(s => workOrderIds.Contains(s.WorkOrderId))
            .ToListAsync();
        var execSummaryByWoId = workOrderListSummaries.ToDictionary(s => s.WorkOrderId);

        // ===== 批量加载 7 种用料计划（用于 G4~G10 用料计划执行状况） =====
        var piercingPlans = await _context.RoundBarPiercingPlans
            .AsNoTracking().Where(p => workOrderIds.Contains(p.WorkOrderId)).ToListAsync();
        var piercingByWoId = piercingPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var semiPlans = await _context.PurchaseSemiPlans
            .AsNoTracking().Where(p => workOrderIds.Contains(p.WorkOrderId)).ToListAsync();
        var semiByWoId = semiPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var finishPlans = await _context.PurchaseFinishedPlans
            .AsNoTracking().Where(p => workOrderIds.Contains(p.WorkOrderId)).ToListAsync();
        var finishByWoId = finishPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allInventoryPlans = await _context.InventoryPlans
            .AsNoTracking().Where(p => workOrderIds.Contains(p.WorkOrderId)).ToListAsync();
        var invByWoId = allInventoryPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var inProcessReworkPlans = await _context.InProcessReworkPlans
            .AsNoTracking().Where(p => workOrderIds.Contains(p.WorkOrderId)).ToListAsync();
        var iprByWoId = inProcessReworkPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var inMainPlans = await _context.InMainWorkOrderPlans
            .AsNoTracking().Where(p => workOrderIds.Contains(p.WorkOrderId)).ToListAsync();
        var inMainByWoId = inMainPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 批量加载生产领用出库记录（用于 G7/G8 库存使用/库料改制的执行量与截止到料日，按 出库工单号+仓库批 两级匹配，同第4/5类完成口径）
        var invPlanBatchNos = allInventoryPlans.Select(p => p.InventoryBatchNo).Distinct().ToList();
        var productionPickRecords = invPlanBatchNos.Count > 0
            ? await _context.OutboundRecords
                .AsNoTracking()
                .Where(or => or.OutboundType == OutboundType.ProductionPick
                          && or.BatchNo != null
                          && or.WorkOrderNo != null
                          && workOrderNos.Contains(or.WorkOrderNo)
                          && invPlanBatchNos.Contains(or.BatchNo))
                .ToListAsync()
            : new List<OutboundRecord>();
        // 出库量 = 各工单号下 各仓库批 的生产领用出库重量（外层工单号/内层批次号均忽略大小写，空工单号不计入）
        var outboundWeightByWoNoAndBatchNo = productionPickRecords
            .GroupBy(or => or.WorkOrderNo!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(or => or.BatchNo!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(bg => bg.Key, bg => bg.Sum(or => or.OutboundWeight)));

        // G7/G8 截止到料日出库日期：各工单号下 各仓库批 的生产领用出库记录最大出库日期
        var outboundDateByWoNoAndBatchNo = productionPickRecords
            .GroupBy(or => or.WorkOrderNo!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(or => or.BatchNo!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(bg => bg.Key, bg => bg.Max(or => or.OutboundDate)));

        // 批量加载仓库进库批次（G4~G6 截止到料日：SourceOrderNo 非空，经采购/委外来源单号映射回工单号）
        var arrivalDateByWoNo = await BuildArrivalDateByWorkOrderNoAsync(allPurchaseOrders, returnItems);

        // 批量加载批次字典（用于 G8 通过 FK 匹配投料量）

        // 批量加载成品检验数据（用于 Group 20 成检不合格，仅 "订单成品" 物料）
        // 通过 ProductionBatch 关联获取工单信息（避免实体冗余字段）
        var finalInspections = await _context.FinalInspections
            .AsNoTracking()
            .Include(fi => fi.ProductionBatch)
            .Where(fi => (fi.ProductionBatch.ManufacturingItem == "OrderFinished" || fi.ProductionBatch.ManufacturingItem == "SpecialDeliveryStatus")
                      && fi.ProductionBatch.WorkOrderNo != null
                      && workOrderNos.Contains(fi.ProductionBatch.WorkOrderNo))
            .ToListAsync();
        var fiByWoNo = finalInspections
            .GroupBy(fi => fi.ProductionBatch.WorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 批量加载过程检验数据（用于 G15 次品总量「过程检侧」，仅订单成品物料批次，经批次关联工单）
        var processInspections = await _context.ProcessInspections
            .AsNoTracking()
            .Include(pi => pi.ProductionBatch)
            .Where(pi => (pi.ProductionBatch.ManufacturingItem == "OrderFinished" || pi.ProductionBatch.ManufacturingItem == "SpecialDeliveryStatus")
                      && pi.ProductionBatch.WorkOrderNo != null
                      && workOrderNos.Contains(pi.ProductionBatch.WorkOrderNo))
            .ToListAsync();
        var piByWoNo = processInspections
            .GroupBy(pi => pi.ProductionBatch.WorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 批量加载成品入库数据（InventoryBatch，用于 Group 15 成品入库）
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

            fiByWoNo.TryGetValue(wo.WorkOrderNo, out var woFiList);

            // G15 次品总量：过程检/成检次品聚合（仅订单成品批次）
            piByWoNo.TryGetValue(wo.WorkOrderNo, out var woPiList);
            var processInspectionReworkWeight = woPiList?.Sum(pi => pi.TheoreticalReworkWeight ?? 0) ?? 0;
            var processInspectionWarehouseWeight = woPiList?.Sum(pi => pi.TheoreticalWarehouseWeight ?? 0) ?? 0;
            var processInspectionScrapWeight = woPiList?.Sum(pi => pi.TheoreticalScrapWeight ?? 0) ?? 0;
            var processInspectionDefectWeight = processInspectionReworkWeight + processInspectionWarehouseWeight + processInspectionScrapWeight;
            var finalInspectionReworkWeight = woFiList?.Sum(fi => fi.DefectReworkWeight ?? 0) ?? 0;
            var finalInspectionWarehouseWeight = woFiList?.Sum(fi => fi.DefectWarehouseWeight ?? 0) ?? 0;
            var finalInspectionScrapWeight = woFiList?.Sum(fi => fi.DefectScrapWeight ?? 0) ?? 0;
            var finalInspectionDefectQty = woFiList?.Sum(fi => (fi.DefectReworkQuantity ?? 0) + (fi.DefectWarehouseQuantity ?? 0) + (fi.DefectScrapQuantity ?? 0)) ?? 0;
            var finalInspectionDefectWeight = finalInspectionReworkWeight + finalInspectionWarehouseWeight + finalInspectionScrapWeight;
            var reworkSourceEntries = BuildReworkSourceEntries(woPiList, woFiList);

            var summary = ComputeSummary(wo, customerNameByWo.TryGetValue(wo.Id, out var cn) ? cn : "", customerSalesmanByWo.TryGetValue(wo.Id, out var sm) ? sm : "", endCustomerByWo.TryGetValue(wo.Id, out var ec) ? ec : null, woBatches, completeRatio, completeDeviation, completeOverRatio, groupDiscountRate, supplySatisfiedRate, fixedSatisfied, nonFixedSatisfied, qualifiedRate, processInspectionReworkWeight, processInspectionWarehouseWeight, processInspectionScrapWeight, processInspectionDefectWeight, finalInspectionReworkWeight, finalInspectionWarehouseWeight, finalInspectionScrapWeight, finalInspectionDefectQty, finalInspectionDefectWeight, reworkSourceEntries);

            // G3: 从用料计划总览读预计算值（避免重算 4 张原始计划表）
            if (execSummaryByWoId.TryGetValue(wo.Id, out var listSummary))
            {
                summary.MaterialPlanStatus = listSummary.MaterialPlanStatus;
                summary.MainNoMaterialPlanRate = listSummary.MainNoMaterialPlanRate;
                summary.MainNoMaterialPlanStatus = listSummary.MainNoMaterialPlanStatus;
                summary.ProcessCycle = listSummary.MaxStandardCycle;
                summary.MaterialPlanCoveredCount = listSummary.MaterialPlanCoveredCount;
                summary.MaterialPlanProportion = listSummary.MaterialPlanProportion;
                summary.TheoreticalCutoffDate = listSummary.TheoreticalCutoffDate;
            }

            // Group（已废弃）: 物料执行实时信息（从采购订单 + 委外回收明细聚合）
            poByWoNo.TryGetValue(wo.WorkOrderNo, out var woPos);
            riByWoNo.TryGetValue(wo.WorkOrderNo, out var woRis);
            if ((woPos?.Count ?? 0) > 0 || (woRis?.Count ?? 0) > 0)
            {
                var safePos = woPos ?? new List<PurchaseOrder>();
                var safeRis = woRis ?? new List<SubcontractReturnItem>();

                // 荒管组：荒管 + 半成品（采购单按 MaterialType 枚举名过滤）
                var roughTubePos = safePos.Where(po =>
                    po.MaterialCategory == "RoughTube" || po.MaterialCategory == "SemiFinished").ToList();
                var roughTubeRis = safeRis.Where(ri =>
                    ri.MaterialCategory == "RoughTube" || ri.MaterialCategory == "SemiFinished").ToList();
                summary.PendingRoughTubeQty = roughTubePos.Sum(po => (po.Quantity ?? 0) - po.ReceivedQuantity)
                    + roughTubeRis.Sum(ri => (ri.RequiredQuantity ?? 0) - ri.ReturnedQuantity);
                summary.PendingRoughTubeWeight = roughTubePos.Sum(po => po.Weight - po.ReceivedWeight)
                    + roughTubeRis.Sum(ri => (ri.RequiredWeight ?? 0) - ri.ReturnedWeight);

                // 外购成组：临界成品 + 订单成品 + 订成-非交付态（采购单按 MaterialType 枚举名过滤）
                var finishPos = safePos.Where(po =>
                    po.MaterialCategory == "CriticalFinished" || po.MaterialCategory == "OrderFinished" || po.MaterialCategory == "SpecialDeliveryStatus").ToList();
                var finishRis = safeRis.Where(ri =>
                    ri.MaterialCategory == "CriticalFinished" || ri.MaterialCategory == "OrderFinished" || ri.MaterialCategory == "SpecialDeliveryStatus").ToList();
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

            // ========== G4~G10: 7 种用料计划执行状况 ==========

            var allPoList = allPoByWoNo.TryGetValue(wo.WorkOrderNo, out var apo) ? apo : new List<PurchaseOrder>();
            var allRiList = riByWoNo.TryGetValue(wo.WorkOrderNo, out var ari) ? ari : new List<SubcontractReturnItem>();

            // G4: 圆棒穿孔（对外 ±3%）
            if (piercingByWoId.TryGetValue(wo.Id, out var piercingList))
            {
                summary.PiercingPlanWeight = piercingList.Sum(p => p.RequiredWeight);

                var piercingOrderWeight = allRiList.Where(ri => ri.MaterialCategory == "RoughTube").Sum(ri => ri.RequiredWeight ?? 0m);
                var piercingReturnWeight = allRiList.Where(ri => ri.MaterialCategory == "RoughTube").Sum(ri => ri.ReturnedWeight);

                summary.PiercingSubOutWeight = piercingOrderWeight;
                summary.PiercingSubStatus = ComputePlanStatus(piercingOrderWeight, summary.PiercingPlanWeight, externalLower, externalUpper);

                summary.PiercingSubInWeight = piercingReturnWeight;
                summary.PiercingSubPendingWeight = Math.Max(0m, piercingOrderWeight - piercingReturnWeight);
                summary.PiercingReturnStatus = piercingOrderWeight > 0
                    ? ComputePlanStatus(piercingReturnWeight, piercingOrderWeight, externalLower, externalUpper, treatZeroActualAsPartial: true)
                    : 0;
            }

            // G5: 荒管采购（对外 ±3%）
            if (semiByWoId.TryGetValue(wo.Id, out var semiList))
            {
                summary.SemiPlanWeight = semiList.Sum(p => p.RequiredWeight);

                var semiOrderWeight = allPoList.Where(po => po.MaterialCategory is "RoughTube" or "SemiFinished").Sum(po => po.Weight);
                var semiInWeight = allPoList.Where(po => po.MaterialCategory is "RoughTube" or "SemiFinished").Sum(po => po.ReceivedWeight);

                summary.SemiOrderWeight = semiOrderWeight;
                summary.SemiOrderStatus = ComputePlanStatus(semiOrderWeight, summary.SemiPlanWeight, externalLower, externalUpper);

                summary.SemiInWeight = semiInWeight;
                summary.SemiPendingWeight = Math.Max(0m, semiOrderWeight - semiInWeight);
                summary.SemiInStatus = semiOrderWeight > 0
                    ? ComputePlanStatus(semiInWeight, semiOrderWeight, externalLower, externalUpper, treatZeroActualAsPartial: true)
                    : 0;
            }

            // G6: 成品采购（对外 ±3%）
            if (finishByWoId.TryGetValue(wo.Id, out var finishList))
            {
                summary.FinishPlanWeight = finishList.Sum(p => p.RequiredWeight);

                var finishOrderWeight = allPoList.Where(po => po.MaterialCategory is "CriticalFinished" or "OrderFinished" or "SpecialDeliveryStatus").Sum(po => po.Weight);
                var finishInWeight = allPoList.Where(po => po.MaterialCategory is "CriticalFinished" or "OrderFinished" or "SpecialDeliveryStatus").Sum(po => po.ReceivedWeight);

                summary.FinishOrderWeight = finishOrderWeight;
                summary.FinishOrderStatus = ComputePlanStatus(finishOrderWeight, summary.FinishPlanWeight, externalLower, externalUpper);

                summary.FinishInWeight = finishInWeight;
                summary.FinishPendingWeight = Math.Max(0m, finishOrderWeight - finishInWeight);
                summary.FinishInStatus = finishOrderWeight > 0
                    ? ComputePlanStatus(finishInWeight, finishOrderWeight, externalLower, externalUpper, treatZeroActualAsPartial: true)
                    : 0;
            }

            // G7: 库存使用（对内-仓库 95%~150%）
            if (invByWoId.TryGetValue(wo.Id, out var invList))
            {
                var plainInvPlans = invList.Where(p => p.ReworkType == null).ToList();
                summary.InventoryPlanWeight = plainInvPlans.Sum(p => p.UsedWeight);
                // 出库量 = 本工单号下 关联仓库批 的生产领用出库重量（与完成匹配同口径：出库工单号==本工单号）
                var uniqueBatchNos = plainInvPlans.Select(p => p.InventoryBatchNo).Distinct().ToList();
                var outWeight = uniqueBatchNos
                    .Select(bn => outboundWeightByWoNoAndBatchNo.TryGetValue(wo.WorkOrderNo, out var byBatch)
                        && byBatch.TryGetValue(bn, out var w) ? w : 0m)
                    .Sum();
                summary.InventoryOutWeight = outWeight;
                summary.InventoryOutStatus = ComputePlanStatus(outWeight, summary.InventoryPlanWeight, warehouseLower, warehouseUpper);
            }

            // G8: 库料改制（对内-仓库 95%~150%）
            if (invByWoId.TryGetValue(wo.Id, out var invList2))
            {
                var reworkInvPlans = invList2.Where(p => p.ReworkType != null).ToList();
                summary.ReworkPlanWeight = reworkInvPlans.Sum(p => p.UsedWeight);
                // 投料量 = 本工单号下 关联仓库批 的生产领用出库重量（与完成匹配同口径：出库工单号==本工单号）
                var uniqueBatchNos = reworkInvPlans.Select(p => p.InventoryBatchNo).Distinct().ToList();
                var inputWeight = uniqueBatchNos
                    .Select(bn => outboundWeightByWoNoAndBatchNo.TryGetValue(wo.WorkOrderNo, out var byBatch)
                        && byBatch.TryGetValue(bn, out var w) ? w : 0m)
                    .Sum();
                summary.ReworkPlanInputWeight = inputWeight;
                summary.ReworkPlanInputStatus = ComputePlanStatus(inputWeight, summary.ReworkPlanWeight, warehouseLower, warehouseUpper);
            }

            // G9: 在产改制（对内-生产 90%~150%）
            if (iprByWoId.TryGetValue(wo.Id, out var iprList))
            {
                summary.InProcessReworkPlanWeight = iprList.Sum(p => p.UsedWeight);
                // 投料量：Type B 按 ProductionBatchId 匹配源批次（工单号已变更），Type A 按 SourceProductionNo 匹配新建批次
                var relatedBatchIds = iprList.Select(p => p.ProductionBatchId).ToHashSet();
                var sourceBatchNos = iprList.Select(p => p.BatchNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var iprInputWeight = woBatches
                    .Where(b => relatedBatchIds.Contains(b.Id)
                             || (b.SourceProductionNo != null && sourceBatchNos.Contains(b.SourceProductionNo)))
                    .Sum(b => b.InputWeight ?? 0m);
                summary.InProcessReworkInputWeight = iprInputWeight;
                summary.InProcessReworkInputStatus = ComputePlanStatus(iprInputWeight, summary.InProcessReworkPlanWeight, productionLower, productionUpper);
            }

            // G10: 在产主工单（对内-生产 90%~150%）
            if (inMainByWoId.TryGetValue(wo.Id, out var inMainList))
            {
                summary.InMainPlanWeight = inMainList.Sum(p => p.AllocatedWeight);
                // 投料量 = 分工单新建批次（SourceProductionNo 匹配主批次号）
                var mainBatchNos = inMainList.Select(p => p.BatchNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var g9InputWeight = woBatches
                    .Where(b => b.SourceProductionNo != null && mainBatchNos.Contains(b.SourceProductionNo))
                    .Sum(b => b.InputWeight ?? 0m);
                summary.InMainInputWeight = g9InputWeight;
                summary.InMainInputStatus = ComputePlanStatus(g9InputWeight, summary.InMainPlanWeight, productionLower, productionUpper);
            }

            // ========== 截止到料日：G4~G6 仓库到料（SourceOrderNo 关联进库批次最大入库日期）∪ G7/G8 出库日期 的最大值 ==========
            var arrivalDate = arrivalDateByWoNo.TryGetValue(wo.WorkOrderNo, out var arrD) ? (DateTime?)arrD : null;
            DateTime? outboundDate = null;
            if (invByWoId.TryGetValue(wo.Id, out var invAllList))
            {
                var allInvBatchNos = invAllList.Select(p => p.InventoryBatchNo).Distinct().ToList();
                var outDates = allInvBatchNos
                    .Select(bn => outboundDateByWoNoAndBatchNo.TryGetValue(wo.WorkOrderNo, out var byBatch)
                        && byBatch.TryGetValue(bn, out var od) ? (DateTime?)od : null)
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .ToList();
                if (outDates.Count > 0) outboundDate = outDates.Max();
            }
            summary.CutoffArrivalDate = new DateTime?[] { arrivalDate, outboundDate }.Max();

            // ========== Group 18: 成品入库（从 InventoryBatch 聚合） ==========
            ibByWoNo.TryGetValue(wo.WorkOrderNo, out var woIbList);
            if (woIbList?.Count > 0)
            {
                summary.WarehousingStartDate = woIbList.Min(ib => ib.InboundDate);
                summary.WarehousingEndDate = woIbList.Max(ib => ib.InboundDate);
                summary.WarehousingTotalQty = woIbList.Sum(ib => ib.InitialQuantity);
                summary.WarehousingTotalWeight = woIbList.Sum(ib => ib.InitialWeight);

                // 工单入库状态（4 档：0=无入库 1=入库部分 2=入库完结 3=入库超额）
                // 定尺按支数：超额=入库支数>需求支数（与主号一致）；重量口径：超额=入库重>需求重×105%
                var isFixed = summary.LengthStatus == LengthStatus.Fixed.ToString();
                bool isOver;
                bool isComplete;
                if (isFixed)
                {
                    isOver = summary.WarehousingTotalQty > wo.TotalQuantity;
                    isComplete = summary.WarehousingTotalQty == wo.TotalQuantity;
                }
                else
                {
                    isOver = summary.WarehousingTotalWeight > wo.TotalWeight * completeOverRatio;
                    isComplete = summary.WarehousingTotalWeight >= wo.TotalWeight * completeRatio
                              && summary.WarehousingTotalWeight >= wo.TotalWeight - completeDeviation;
                }

                summary.WoWarehousingStatus = (summary.WarehousingTotalQty == 0 && summary.WarehousingTotalWeight == 0)
                    ? 0  // 无入库
                    : isOver
                        ? 3  // 入库超额
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
                    s.ProcessCycle = defaultProcessCycle;
            }
        }

        // 计算主号级投料聚合
        ComputeMainNoInputAggregation(summaries, workOrders, supplySatisfiedRate, fixedSatisfied, nonFixedSatisfied, externalLower);

        // 计算主号/订单级入库状态聚合
        ComputeWarehousingAggregation(summaries, workOrders, completeRatio, completeDeviation, completeOverRatio);

        // ========== G16/G2: 先加载暂停与需求调整数据（须在主号关注之前，供「主号暂停」档判定） ==========
        var pausedIdList = await _context.Set<OrderDemandAdjustment>()
            .AsNoTracking()
            .Where(u => workOrderIds.Contains(u.WorkOrderId) && u.IsPaused)
            .Select(u => u.WorkOrderId)
            .ToListAsync();
        var pausedIds = pausedIdList.ToHashSet();
        var adjustments = await _context.Set<OrderDemandAdjustment>()
            .AsNoTracking()
            .Where(a => workOrderIds.Contains(a.WorkOrderId))
            .ToDictionaryAsync(a => a.WorkOrderId);
        foreach (var summary in summaries)
        {
            if (adjustments.TryGetValue(summary.WorkOrderId, out var adj))
            {
                summary.IsUrging = adj.IsUrging;
                summary.IsBatchDelivery = adj.IsBatchDelivery;
                summary.IsPaused = adj.IsPaused;
                summary.IsForceCompleted = adj.IsForceCompleted;
                summary.AdjustmentRemark = adj.AdjustmentRemark;
            }
        }

        // ========== G16: 计算主号关注（0=主号暂停/1=主号完成/2=原料锁定/3=生产执行/4=成品检验，全部主号级） ==========
        // 档3/4 主号级判定：主号下任一工单有活动批次（未产/在产/暂停）→ 3 生产执行；主号下全部工单仅成检/已完成批次 → 4 成品检验
        var producingByWo = batchesByWo.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Any(b =>
                b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress || b.Status == BatchStatus.Suspended),
            StringComparer.OrdinalIgnoreCase);
        var producingByMainNo = summaries
            .GroupBy(s => new { s.SalesOrderNo, s.ProductionMainNo })
            .ToDictionary(g => g.Key,
                g => g.Any(s => producingByWo.GetValueOrDefault(s.WorkOrderNo)));
        foreach (var summary in summaries)
        {
            var hasProducingBatch = producingByMainNo.TryGetValue(new { summary.SalesOrderNo, summary.ProductionMainNo }, out var pb) && pb;
            summary.ScheduleStage = summary.IsPaused ? 0
                : summary.IsForceCompleted ? 1
                : summary.MainNoWarehousingStatus >= 2 ? 1
                : summary.MainNoFlowStatus is 0 or 1 ? 2
                : hasProducingBatch ? 3
                : 4;
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

            if (summary.IsPaused)
                summary.ProductionFlowProperty = ProductionFlowKeys.Paused;
            else if (summary.ScheduleStage == 3 || (summary.ScheduleStage == 2 && (flags.MainNoUrging || flags.MainNoBatchDelivery)))
                summary.ProductionFlowProperty = ProductionFlowKeys.Normal;
            else if (summary.ScheduleStage == 2)
                summary.ProductionFlowProperty = ProductionFlowKeys.Waiting;
            else if (summary.ScheduleStage == 4)
                // 档4 成品检验：有成检中批次为正常流程（非疑问），全完成则无关注
                summary.ProductionFlowProperty = summary.FlowIncompleteBatchCount == 0 ? ProductionFlowKeys.Skip : ProductionFlowKeys.Normal;
            else if (summary.ScheduleStage == 1)
                // 档1 主号完成：仍有未完成批次才是疑问
                summary.ProductionFlowProperty = summary.FlowIncompleteBatchCount == 0 ? ProductionFlowKeys.Skip : ProductionFlowKeys.Doubt;
            else
                summary.ProductionFlowProperty = null;
        }

        // ========== G16: 计算剩余总工量 & 主号计划性 ==========
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
                     && (b.ManufacturingItem == "OrderFinished" || b.ManufacturingItem == "SpecialDeliveryStatus"))
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
            if (summary.ScheduleStage == 1)
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
                0 => agg?.MaxRemainingDays ?? 0,   // 主号暂停：剩余工量停滞展示（恢复后继续）
                2 => (agg?.MaxProcessCycle ?? 0) + (int)bufferDays,   // 原料锁定
                3 => agg?.MaxRemainingDays ?? 0,   // 生产执行
                4 => (int)inspectionFixedDays,     // 成品检验
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

            // 主号计划性
            var todayDays = DateOnly.FromDateTime(DateTime.Today).DayNumber;
            var deliveryDays = DateOnly.FromDateTime(summary.DeliveryDate).DayNumber;
            var diff = totalDays + todayDays - deliveryDays;

            summary.UrgencyLevel = diff > urgencyAPlus ? UrgencyLevelKeys.APlusUrgent
                : diff > urgencyA ? UrgencyLevelKeys.AUrgent
                : diff > urgencyB ? UrgencyLevelKeys.BOrder
                : diff > urgencyC ? UrgencyLevelKeys.CSlow
                : UrgencyLevelKeys.DSlow;

            // 暂停工单 → UrgencyLevel 覆盖为"E停"
            if (pausedIds.Contains(summary.WorkOrderId))
                summary.UrgencyLevel = UrgencyLevelKeys.EPaused;

            // 预计完成日 & 交期相差天数
            summary.EstimatedProcessCompletionDate = DateTime.Today.AddDays(totalDays);
            summary.DaysDiffFromDelivery = (summary.EstimatedProcessCompletionDate.Value.Date - summary.DeliveryDate.Date).Days;
        }

        // ========== G16: 原锁备注（仅 ScheduleStage=2 原料锁定时计算，主号级判定） ==========
        // 说明：原料锁定阶段（档2）的判定前提即 MainNoFlowStatus!=2，故 A/B 不再重复判断"有效流转≠满足"，
        //       仅按「投料状态 + 附返整主号状态 / 主号计划状态」区分四类锁定原因。
        foreach (var summary in summaries)
        {
            if (summary.ScheduleStage != 2)
            {
                summary.RawMaterialLockRemark = null;
                continue;
            }

            // 投料满足 → 按附返整主号状态分 A 质量补料 / B 执行返整
            if (summary.MainNoInputStatus >= 2)
            {
                // B 执行返整：附返整满足（缺口可由返整量补齐，处于返整执行）
                // A 质量补料：附返整不满足（连返整量算上仍不足，真缺料需补料）
                summary.RawMaterialLockRemark = summary.ReworkMainNoStatus >= 2 ? RawMaterialLockRemarkKeys.ExecuteRework : RawMaterialLockRemarkKeys.QualityReplenish;
                continue;
            }

            // 投料不满足 → 按主号计划状态分 C 执行计划 / D 完善计划
            var planStatus = (MaterialPlanStatus)summary.MainNoMaterialPlanStatus;
            summary.RawMaterialLockRemark = planStatus is MaterialPlanStatus.Satisfied or MaterialPlanStatus.Excess
                ? RawMaterialLockRemarkKeys.ExecutePlan
                : RawMaterialLockRemarkKeys.ImprovePlan;
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

        // 同步刷新订单列表读模型（ScheduleStage/UrgencyLevel/EstimatedCompletionDate 已更新）
        // 说明：全量刷新同样会改变工单档位/紧急性/预计完成时间，若不联动刷新，订单页读模型将停留在旧值
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var orderIds = salesOrders.Select(so => so.Id).ToList();
            foreach (var orderId in orderIds)
            {
                await orderService.RefreshByOrderIdAsync(orderId);
            }
        }

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
        var completeOverRatio = warehouseConfig.GetValueOrDefault("CompleteOverRatio", 1.05m);
        var bufferDays = workOrderDaysConfig.GetValueOrDefault("BufferDays", 3m);
        var inspectionFixedDays = workOrderDaysConfig.GetValueOrDefault("InspectionFixedDays", 3m);
        var urgencyAPlus = urgencyConfig.GetValueOrDefault("APlus", 7m);
        var urgencyA = urgencyConfig.GetValueOrDefault("A", -3m);
        var urgencyB = urgencyConfig.GetValueOrDefault("B", -10m);
        var urgencyC = urgencyConfig.GetValueOrDefault("C", -17m);
        var groupDiscountRate = processingDiscountConfig.GetValueOrDefault("GroupDiscountRate", 0.025m);
        var supplySatisfiedRate = materialPlanStatusConfig.GetValueOrDefault("SupplySatisfiedRate", 100m);
        var fixedSatisfied = materialPlanStatusConfig.GetValueOrDefault("FixedSatisfied", 110m);
        var nonFixedSatisfied = materialPlanStatusConfig.GetValueOrDefault("NonFixedSatisfied", 120m);
        var qualifiedRate = materialPlanStatusConfig.GetValueOrDefault("QualifiedRate", 98m) / 100m;
        var defaultValueConfig = await _configService.GetConfigMapAsync("DefaultValue");
        var roughTubeFinishRatio = defaultValueConfig.GetValueOrDefault("RoughTubeFinishRatio", 0.92m);
        var defaultProcessCycle = (int)defaultValueConfig.GetValueOrDefault("DefaultProcessCycle", 22m);

        // 用料计划容差配置（用于 G4~G10 状态计算）
        var planToleranceConfig = await _configService.GetConfigMapAsync("MaterialPlanTolerance");
        var externalLower = planToleranceConfig.GetValueOrDefault("ExternalLower", 0.97m);
        var externalUpper = planToleranceConfig.GetValueOrDefault("ExternalUpper", 1.03m);
        var warehouseLower = planToleranceConfig.GetValueOrDefault("WarehouseLower", 0.95m);
        var warehouseUpper = planToleranceConfig.GetValueOrDefault("WarehouseUpper", 1.50m);
        var productionLower = planToleranceConfig.GetValueOrDefault("ProductionLower", 0.90m);
        var productionUpper = planToleranceConfig.GetValueOrDefault("ProductionUpper", 1.50m);

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
        var endCustomerByWo = new Dictionary<int, string>();
        foreach (var wo in workOrders)
        {
            var so = salesOrders.FirstOrDefault(s => s.OrderNumber.Equals(wo.SalesOrderNo, StringComparison.OrdinalIgnoreCase));
            customerNameByWo[wo.Id] = so?.CustomerName ?? "";
            customerSalesmanByWo[wo.Id] = so?.Salesman ?? "";
            endCustomerByWo[wo.Id] = so?.EndCustomer ?? "";
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

        // 加载全部采购订单（含已完成，用于 G5~G6 用料计划执行状况）
        var allPurchaseOrders = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.SourceWorkOrderNo != null && allWoNos.Contains(po.SourceWorkOrderNo))
            .ToListAsync();
        var allPoByWoNo = allPurchaseOrders
            .GroupBy(po => po.SourceWorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var workOrderListSummaries = await _context.Set<WorkOrderListSummary>()
            .AsNoTracking()
            .Where(s => allWoIds.Contains(s.WorkOrderId))
            .ToListAsync();
        var execSummaryByWoId = workOrderListSummaries.ToDictionary(s => s.WorkOrderId);

        var finalInspections = await _context.FinalInspections
            .AsNoTracking()
            .Include(fi => fi.ProductionBatch)
            .Where(fi => (fi.ProductionBatch.ManufacturingItem == "OrderFinished" || fi.ProductionBatch.ManufacturingItem == "SpecialDeliveryStatus") && fi.ProductionBatch.WorkOrderNo != null && allWoNos.Contains(fi.ProductionBatch.WorkOrderNo))
            .ToListAsync();
        var fiByWoNo = finalInspections
            .GroupBy(fi => fi.ProductionBatch.WorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 批量加载过程检验数据（用于 G15 次品总量「过程检侧」，仅订单成品物料批次，经批次关联工单）
        var processInspections = await _context.ProcessInspections
            .AsNoTracking()
            .Include(pi => pi.ProductionBatch)
            .Where(pi => (pi.ProductionBatch.ManufacturingItem == "OrderFinished" || pi.ProductionBatch.ManufacturingItem == "SpecialDeliveryStatus")
                      && pi.ProductionBatch.WorkOrderNo != null
                      && allWoNos.Contains(pi.ProductionBatch.WorkOrderNo))
            .ToListAsync();
        var piByWoNo = processInspections
            .GroupBy(pi => pi.ProductionBatch.WorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var inventoryBatches = await _context.InventoryBatches
            .AsNoTracking()
            .Where(ib => ib.MaterialType == InventoryMaterialTypes.OrderFinished && ib.WorkOrderNo != null && allWoNos.Contains(ib.WorkOrderNo))
            .ToListAsync();
        var ibByWoNo = inventoryBatches
            .GroupBy(ib => ib.WorkOrderNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 批量加载 7 种用料计划（用于 G4~G10）
        var piercingPlans = await _context.RoundBarPiercingPlans
            .AsNoTracking().Where(p => allWoIds.Contains(p.WorkOrderId)).ToListAsync();
        var piercingByWoId = piercingPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var semiPlans = await _context.PurchaseSemiPlans
            .AsNoTracking().Where(p => allWoIds.Contains(p.WorkOrderId)).ToListAsync();
        var semiByWoId = semiPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var finishPlans = await _context.PurchaseFinishedPlans
            .AsNoTracking().Where(p => allWoIds.Contains(p.WorkOrderId)).ToListAsync();
        var finishByWoId = finishPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allInventoryPlans = await _context.InventoryPlans
            .AsNoTracking().Where(p => allWoIds.Contains(p.WorkOrderId)).ToListAsync();
        var invByWoId = allInventoryPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var inProcessReworkPlans = await _context.InProcessReworkPlans
            .AsNoTracking().Where(p => allWoIds.Contains(p.WorkOrderId)).ToListAsync();
        var iprByWoId = inProcessReworkPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var inMainPlans = await _context.InMainWorkOrderPlans
            .AsNoTracking().Where(p => allWoIds.Contains(p.WorkOrderId)).ToListAsync();
        var inMainByWoId = inMainPlans.GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 批量加载生产领用出库记录（用于 G7/G8 库存使用/库料改制的执行量与截止到料日，按 出库工单号+仓库批 两级匹配，同第4/5类完成口径）
        var invPlanBatchNos = allInventoryPlans.Select(p => p.InventoryBatchNo).Distinct().ToList();
        var productionPickRecords = invPlanBatchNos.Count > 0
            ? await _context.OutboundRecords
                .AsNoTracking()
                .Where(or => or.OutboundType == OutboundType.ProductionPick
                          && or.BatchNo != null
                          && or.WorkOrderNo != null
                          && workOrderNos.Contains(or.WorkOrderNo)
                          && invPlanBatchNos.Contains(or.BatchNo))
                .ToListAsync()
            : new List<OutboundRecord>();
        // 出库量 = 各工单号下 各仓库批 的生产领用出库重量（外层工单号/内层批次号均忽略大小写，空工单号不计入）
        var outboundWeightByWoNoAndBatchNo = productionPickRecords
            .GroupBy(or => or.WorkOrderNo!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(or => or.BatchNo!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(bg => bg.Key, bg => bg.Sum(or => or.OutboundWeight)));

        // G7/G8 截止到料日出库日期：各工单号下 各仓库批 的生产领用出库记录最大出库日期
        var outboundDateByWoNoAndBatchNo = productionPickRecords
            .GroupBy(or => or.WorkOrderNo!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(or => or.BatchNo!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(bg => bg.Key, bg => bg.Max(or => or.OutboundDate)));

        // 批量加载仓库进库批次（G4~G6 截止到料日：SourceOrderNo 非空，经采购/委外来源单号映射回工单号）
        var arrivalDateByWoNo = await BuildArrivalDateByWorkOrderNoAsync(allPurchaseOrders, returnItems);

        // 4. 逐工单计算
        var now = DateTime.Now;
        var summaries = new List<WorkOrderExecutionSummary>();
        foreach (var wo in workOrders)
        {
            var woBatches = batchesByWo.TryGetValue(wo.WorkOrderNo, out var b) ? b : new List<ProductionBatch>();
            fiByWoNo.TryGetValue(wo.WorkOrderNo, out var woFiList);

            // G15 次品总量：过程检/成检次品聚合（仅订单成品批次）
            piByWoNo.TryGetValue(wo.WorkOrderNo, out var woPiList);
            var processInspectionReworkWeight = woPiList?.Sum(pi => pi.TheoreticalReworkWeight ?? 0) ?? 0;
            var processInspectionWarehouseWeight = woPiList?.Sum(pi => pi.TheoreticalWarehouseWeight ?? 0) ?? 0;
            var processInspectionScrapWeight = woPiList?.Sum(pi => pi.TheoreticalScrapWeight ?? 0) ?? 0;
            var processInspectionDefectWeight = processInspectionReworkWeight + processInspectionWarehouseWeight + processInspectionScrapWeight;
            var finalInspectionReworkWeight = woFiList?.Sum(fi => fi.DefectReworkWeight ?? 0) ?? 0;
            var finalInspectionWarehouseWeight = woFiList?.Sum(fi => fi.DefectWarehouseWeight ?? 0) ?? 0;
            var finalInspectionScrapWeight = woFiList?.Sum(fi => fi.DefectScrapWeight ?? 0) ?? 0;
            var finalInspectionDefectQty = woFiList?.Sum(fi => (fi.DefectReworkQuantity ?? 0) + (fi.DefectWarehouseQuantity ?? 0) + (fi.DefectScrapQuantity ?? 0)) ?? 0;
            var finalInspectionDefectWeight = finalInspectionReworkWeight + finalInspectionWarehouseWeight + finalInspectionScrapWeight;
            var reworkSourceEntries = BuildReworkSourceEntries(woPiList, woFiList);

            var summary = ComputeSummary(wo,
                customerNameByWo.TryGetValue(wo.Id, out var cn) ? cn : "",
                customerSalesmanByWo.TryGetValue(wo.Id, out var sm) ? sm : "",
                endCustomerByWo.TryGetValue(wo.Id, out var ec) ? ec : null,
                woBatches,
                completeRatio, completeDeviation, completeOverRatio, groupDiscountRate, supplySatisfiedRate, fixedSatisfied, nonFixedSatisfied, qualifiedRate,
                processInspectionReworkWeight, processInspectionWarehouseWeight, processInspectionScrapWeight, processInspectionDefectWeight,
                finalInspectionReworkWeight, finalInspectionWarehouseWeight, finalInspectionScrapWeight, finalInspectionDefectQty, finalInspectionDefectWeight,
                reworkSourceEntries);

            // G3: 从用料计划读模型取值
            if (execSummaryByWoId.TryGetValue(wo.Id, out var ls))
            {
                summary.MaterialPlanStatus = ls.MaterialPlanStatus;
                summary.MainNoMaterialPlanRate = ls.MainNoMaterialPlanRate;
                summary.MainNoMaterialPlanStatus = ls.MainNoMaterialPlanStatus;
                summary.ProcessCycle = ls.MaxStandardCycle;
                summary.MaterialPlanCoveredCount = ls.MaterialPlanCoveredCount;
                summary.MaterialPlanProportion = ls.MaterialPlanProportion;
                summary.TheoreticalCutoffDate = ls.TheoreticalCutoffDate;
            }

            // Group（已废弃）: 物料执行
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
                var finishPos = safePos.Where(po => po.MaterialCategory == "CriticalFinished" || po.MaterialCategory == "OrderFinished" || po.MaterialCategory == "SpecialDeliveryStatus").ToList();
                var finishRis = safeRis.Where(ri => ri.MaterialCategory == "CriticalFinished" || ri.MaterialCategory == "OrderFinished" || ri.MaterialCategory == "SpecialDeliveryStatus").ToList();
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

            // ========== G4~G10: 7 种用料计划执行状况 ==========

            var allPoList = allPoByWoNo.TryGetValue(wo.WorkOrderNo, out var apo) ? apo : new List<PurchaseOrder>();
            var allRiList = riByWoNo.TryGetValue(wo.WorkOrderNo, out var ari) ? ari : new List<SubcontractReturnItem>();

            // G4: 圆棒穿孔（对外 ±3%）
            if (piercingByWoId.TryGetValue(wo.Id, out var piercingList))
            {
                summary.PiercingPlanWeight = piercingList.Sum(p => p.RequiredWeight);
                var piercingOrderWeight = allRiList.Where(ri => ri.MaterialCategory == "RoughTube").Sum(ri => ri.RequiredWeight ?? 0m);
                var piercingReturnWeight = allRiList.Where(ri => ri.MaterialCategory == "RoughTube").Sum(ri => ri.ReturnedWeight);
                summary.PiercingSubOutWeight = piercingOrderWeight;
                summary.PiercingSubStatus = ComputePlanStatus(piercingOrderWeight, summary.PiercingPlanWeight, externalLower, externalUpper);
                summary.PiercingSubInWeight = piercingReturnWeight;
                summary.PiercingSubPendingWeight = Math.Max(0m, piercingOrderWeight - piercingReturnWeight);
                summary.PiercingReturnStatus = piercingOrderWeight > 0
                    ? ComputePlanStatus(piercingReturnWeight, piercingOrderWeight, externalLower, externalUpper, treatZeroActualAsPartial: true) : 0;
            }

            // G5: 荒管采购（对外 ±3%）
            if (semiByWoId.TryGetValue(wo.Id, out var semiList))
            {
                summary.SemiPlanWeight = semiList.Sum(p => p.RequiredWeight);
                var semiOrderWeight = allPoList.Where(po => po.MaterialCategory is "RoughTube" or "SemiFinished").Sum(po => po.Weight);
                var semiInWeight = allPoList.Where(po => po.MaterialCategory is "RoughTube" or "SemiFinished").Sum(po => po.ReceivedWeight);
                summary.SemiOrderWeight = semiOrderWeight;
                summary.SemiOrderStatus = ComputePlanStatus(semiOrderWeight, summary.SemiPlanWeight, externalLower, externalUpper);
                summary.SemiInWeight = semiInWeight;
                summary.SemiPendingWeight = Math.Max(0m, semiOrderWeight - semiInWeight);
                summary.SemiInStatus = semiOrderWeight > 0
                    ? ComputePlanStatus(semiInWeight, semiOrderWeight, externalLower, externalUpper) : 0;
            }

            // G6: 成品采购（对外 ±3%）
            if (finishByWoId.TryGetValue(wo.Id, out var finishList))
            {
                summary.FinishPlanWeight = finishList.Sum(p => p.RequiredWeight);
                var finishOrderWeight = allPoList.Where(po => po.MaterialCategory is "CriticalFinished" or "OrderFinished" or "SpecialDeliveryStatus").Sum(po => po.Weight);
                var finishInWeight = allPoList.Where(po => po.MaterialCategory is "CriticalFinished" or "OrderFinished" or "SpecialDeliveryStatus").Sum(po => po.ReceivedWeight);
                summary.FinishOrderWeight = finishOrderWeight;
                summary.FinishOrderStatus = ComputePlanStatus(finishOrderWeight, summary.FinishPlanWeight, externalLower, externalUpper);
                summary.FinishInWeight = finishInWeight;
                summary.FinishPendingWeight = Math.Max(0m, finishOrderWeight - finishInWeight);
                summary.FinishInStatus = finishOrderWeight > 0
                    ? ComputePlanStatus(finishInWeight, finishOrderWeight, externalLower, externalUpper) : 0;
            }

            // G7: 库存使用（对内-仓库 95%~150%）
            if (invByWoId.TryGetValue(wo.Id, out var invList))
            {
                var plainInvPlans = invList.Where(p => p.ReworkType == null).ToList();
                summary.InventoryPlanWeight = plainInvPlans.Sum(p => p.UsedWeight);
                // 出库量 = 本工单号下 关联仓库批 的生产领用出库重量（与完成匹配同口径：出库工单号==本工单号）
                var uniqueBatchNos = plainInvPlans.Select(p => p.InventoryBatchNo).Distinct().ToList();
                var outWeight = uniqueBatchNos
                    .Select(bn => outboundWeightByWoNoAndBatchNo.TryGetValue(wo.WorkOrderNo, out var byBatch)
                        && byBatch.TryGetValue(bn, out var w) ? w : 0m)
                    .Sum();
                summary.InventoryOutWeight = outWeight;
                summary.InventoryOutStatus = ComputePlanStatus(outWeight, summary.InventoryPlanWeight, warehouseLower, warehouseUpper);
            }

            // G8: 库料改制（对内-仓库 95%~150%）
            if (invByWoId.TryGetValue(wo.Id, out var invList2))
            {
                var reworkInvPlans = invList2.Where(p => p.ReworkType != null).ToList();
                summary.ReworkPlanWeight = reworkInvPlans.Sum(p => p.UsedWeight);
                // 投料量 = 本工单号下 关联仓库批 的生产领用出库重量（与完成匹配同口径：出库工单号==本工单号）
                var uniqueBatchNos = reworkInvPlans.Select(p => p.InventoryBatchNo).Distinct().ToList();
                var inputWeight = uniqueBatchNos
                    .Select(bn => outboundWeightByWoNoAndBatchNo.TryGetValue(wo.WorkOrderNo, out var byBatch)
                        && byBatch.TryGetValue(bn, out var w) ? w : 0m)
                    .Sum();
                summary.ReworkPlanInputWeight = inputWeight;
                summary.ReworkPlanInputStatus = ComputePlanStatus(inputWeight, summary.ReworkPlanWeight, warehouseLower, warehouseUpper);
            }

            // G9: 在产改制（对内-生产 90%~150%）
            if (iprByWoId.TryGetValue(wo.Id, out var iprList))
            {
                summary.InProcessReworkPlanWeight = iprList.Sum(p => p.UsedWeight);
                // 投料量：Type B 按 ProductionBatchId 匹配源批次（工单号已变更），Type A 按 SourceProductionNo 匹配新建批次
                var relatedBatchIds = iprList.Select(p => p.ProductionBatchId).ToHashSet();
                var sourceBatchNos = iprList.Select(p => p.BatchNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var iprInputWeight = woBatches
                    .Where(b => relatedBatchIds.Contains(b.Id)
                             || (b.SourceProductionNo != null && sourceBatchNos.Contains(b.SourceProductionNo)))
                    .Sum(b => b.InputWeight ?? 0m);
                summary.InProcessReworkInputWeight = iprInputWeight;
                summary.InProcessReworkInputStatus = ComputePlanStatus(iprInputWeight, summary.InProcessReworkPlanWeight, productionLower, productionUpper);
            }

            // G10: 在产主工单（对内-生产 90%~150%）
            if (inMainByWoId.TryGetValue(wo.Id, out var inMainList))
            {
                summary.InMainPlanWeight = inMainList.Sum(p => p.AllocatedWeight);
                // 投料量 = 分工单新建批次（SourceProductionNo 匹配主批次号）
                var mainBatchNos = inMainList.Select(p => p.BatchNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var g9InputWeight = woBatches
                    .Where(b => b.SourceProductionNo != null && mainBatchNos.Contains(b.SourceProductionNo))
                    .Sum(b => b.InputWeight ?? 0m);
                summary.InMainInputWeight = g9InputWeight;
                summary.InMainInputStatus = ComputePlanStatus(g9InputWeight, summary.InMainPlanWeight, productionLower, productionUpper);
            }

            // ========== 截止到料日：G4~G6 仓库到料（SourceOrderNo 关联进库批次最大入库日期）∪ G7/G8 出库日期 的最大值 ==========
            var arrivalDate = arrivalDateByWoNo.TryGetValue(wo.WorkOrderNo, out var arrD) ? (DateTime?)arrD : null;
            DateTime? outboundDate = null;
            if (invByWoId.TryGetValue(wo.Id, out var invAllList))
            {
                var allInvBatchNos = invAllList.Select(p => p.InventoryBatchNo).Distinct().ToList();
                var outDates = allInvBatchNos
                    .Select(bn => outboundDateByWoNoAndBatchNo.TryGetValue(wo.WorkOrderNo, out var byBatch)
                        && byBatch.TryGetValue(bn, out var od) ? (DateTime?)od : null)
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .ToList();
                if (outDates.Count > 0) outboundDate = outDates.Max();
            }
            summary.CutoffArrivalDate = new DateTime?[] { arrivalDate, outboundDate }.Max();

            // Group 15: 成品入库
            ibByWoNo.TryGetValue(wo.WorkOrderNo, out var woIbList);
            if (woIbList?.Count > 0)
            {
                summary.WarehousingStartDate = woIbList.Min(ib => ib.InboundDate);
                summary.WarehousingEndDate = woIbList.Max(ib => ib.InboundDate);
                summary.WarehousingTotalQty = woIbList.Sum(ib => ib.InitialQuantity);
                summary.WarehousingTotalWeight = woIbList.Sum(ib => ib.InitialWeight);
                // 工单入库状态（4 档：0=无入库 1=入库部分 2=入库完结 3=入库超额；定尺按支数/重量口径超 105% 为超额，与主号一致）
                var isFixed = summary.LengthStatus == LengthStatus.Fixed.ToString();
                var isOver = isFixed
                    ? summary.WarehousingTotalQty > wo.TotalQuantity
                    : summary.WarehousingTotalWeight > wo.TotalWeight * completeOverRatio;
                var isComplete = isFixed
                    ? summary.WarehousingTotalQty == wo.TotalQuantity
                    : summary.WarehousingTotalWeight >= wo.TotalWeight * completeRatio
                      && summary.WarehousingTotalWeight >= wo.TotalWeight - completeDeviation;
                summary.WoWarehousingStatus = (summary.WarehousingTotalQty == 0 && summary.WarehousingTotalWeight == 0)
                    ? 0 : isOver ? 3 : isComplete ? 2 : 1;
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
        ComputeMainNoInputAggregation(summaries, workOrders, supplySatisfiedRate, fixedSatisfied, nonFixedSatisfied, externalLower);
        ComputeWarehousingAggregation(summaries, workOrders, completeRatio, completeDeviation, completeOverRatio);

        // G16/G2: 先加载暂停与需求调整数据（须在主号关注之前，供「主号暂停」档判定）
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
                summary.IsForceCompleted = adj.IsForceCompleted;
                summary.AdjustmentRemark = adj.AdjustmentRemark;
            }
        }

        // G16: 主号关注（0=主号暂停/1=主号完成/2=原料锁定/3=生产执行/4=成品检验，全部主号级）
        // 主号暂停：该工单 IsPaused（工单需求调整，联动连带保证同主号未入库完结工单一致）
        // 主号完成：主号入库=完结/超额（真正闭环），或该工单 IsForceCompleted（强制完成，与暂停互斥）
        // 档3/4 主号级判定：主号下任一工单有活动批次（未产/在产/临时暂停 Suspended）→ 3 生产执行；主号下全部工单仅成检/已完成批次 → 4 成品检验
        var producingByWo = batchesByWo.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Any(b =>
                b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress || b.Status == BatchStatus.Suspended),
            StringComparer.OrdinalIgnoreCase);
        var producingByMainNo = summaries
            .GroupBy(s => new { s.SalesOrderNo, s.ProductionMainNo })
            .ToDictionary(g => g.Key,
                g => g.Any(s => producingByWo.GetValueOrDefault(s.WorkOrderNo)));
        foreach (var summary in summaries)
        {
            var hasProducingBatch = producingByMainNo.TryGetValue(new { summary.SalesOrderNo, summary.ProductionMainNo }, out var pb) && pb;
            summary.ScheduleStage = summary.IsPaused ? 0
                : summary.IsForceCompleted ? 1
                : summary.MainNoWarehousingStatus >= 2 ? 1
                : summary.MainNoFlowStatus is 0 or 1 ? 2
                : hasProducingBatch ? 3
                : 4;
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
            summary.ProductionFlowProperty = summary.IsPaused ? ProductionFlowKeys.Paused
                : (summary.ScheduleStage == 3 || (summary.ScheduleStage == 2 && (flags.MainNoUrging || flags.MainNoBatchDelivery))) ? ProductionFlowKeys.Normal
                : summary.ScheduleStage == 2 ? ProductionFlowKeys.Waiting
                // 档4 成品检验：有成检中批次为正常流程（非疑问），全完成则无关注
                : summary.ScheduleStage == 4 ? (summary.FlowIncompleteBatchCount == 0 ? ProductionFlowKeys.Skip : ProductionFlowKeys.Normal)
                // 档1 主号完成：仍有未完成批次才是疑问
                : summary.ScheduleStage == 1 ? (summary.FlowIncompleteBatchCount == 0 ? ProductionFlowKeys.Skip : ProductionFlowKeys.Doubt)
                : null;
        }

        // G16: 计算剩余工量 & 主号计划性
        var dailyEstimates = await _dailyOutputService.GetAllAsync();
        var completedBatchOutputByMainNo = batchesByWo.Values
            .SelectMany(b => b)
            .Where(b => b.Status == BatchStatus.Completed && b.ProductionType != "Rework" && (b.ManufacturingItem == "OrderFinished" || b.ManufacturingItem == "SpecialDeliveryStatus"))
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
            if (summary.ScheduleStage == 1) continue;
            var key = new { summary.SalesOrderNo, summary.ProductionMainNo };
            // ...剩余工量计算
            var agg = summaries
                .GroupBy(s => new { s.SalesOrderNo, s.ProductionMainNo })
                .Where(g => g.Key.Equals(key))
                .Select(g => new { MaxProcessCycle = g.Max(s => s.ProcessCycle), MaxRemainingDays = g.Max(s => s.FlowMaxRemainingWorkDays), MainNoTotalWeight = g.Sum(s => s.TotalWeight) })
                .FirstOrDefault();

            summary.TotalRemainingWorkDays = summary.ScheduleStage switch
            {
                0 => agg?.MaxRemainingDays ?? 0,   // 主号暂停：剩余工量停滞展示（恢复后继续）
                2 => (agg?.MaxProcessCycle ?? 0) + (int)bufferDays,   // 原料锁定
                3 => agg?.MaxRemainingDays ?? 0,   // 生产执行
                4 => (int)inspectionFixedDays,     // 成品检验
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
            summary.UrgencyLevel = diff > urgencyAPlus ? UrgencyLevelKeys.APlusUrgent
                : diff > urgencyA ? UrgencyLevelKeys.AUrgent
                : diff > urgencyB ? UrgencyLevelKeys.BOrder
                : diff > urgencyC ? UrgencyLevelKeys.CSlow
                : UrgencyLevelKeys.DSlow;
            if (pausedIds.Contains(summary.WorkOrderId)) summary.UrgencyLevel = UrgencyLevelKeys.EPaused;
            summary.EstimatedProcessCompletionDate = DateTime.Today.AddDays(totalDays);
            summary.DaysDiffFromDelivery = (summary.EstimatedProcessCompletionDate.Value.Date - summary.DeliveryDate.Date).Days;
        }

        // G16: 原锁备注（仅 ScheduleStage=2 原料锁定时计算，主号级判定）
        // 说明：原料锁定阶段（档2）的判定前提即 MainNoFlowStatus!=2，故 A/B 不再重复判断"有效流转≠满足"，
        //       仅按「投料状态 + 附返整主号状态 / 主号计划状态」区分四类锁定原因。
        foreach (var summary in summaries.Where(s => s.ScheduleStage == 2))
        {
            // 投料满足 → 按附返整主号状态分 A 质量补料 / B 执行返整
            if (summary.MainNoInputStatus >= 2)
            {
                // B 执行返整：附返整满足（缺口可由返整量补齐，处于返整执行）
                // A 质量补料：附返整不满足（连返整量算上仍不足，真缺料需补料）
                summary.RawMaterialLockRemark = summary.ReworkMainNoStatus >= 2 ? RawMaterialLockRemarkKeys.ExecuteRework : RawMaterialLockRemarkKeys.QualityReplenish;
                continue;
            }

            // 投料不满足 → 按主号计划状态分 C 执行计划 / D 完善计划
            var planStatus = (MaterialPlanStatus)summary.MainNoMaterialPlanStatus;
            summary.RawMaterialLockRemark = planStatus is MaterialPlanStatus.Satisfied or MaterialPlanStatus.Excess
                ? RawMaterialLockRemarkKeys.ExecutePlan
                : RawMaterialLockRemarkKeys.ImprovePlan;
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
        string? endCustomer,
        List<ProductionBatch> batches,
        decimal completeRatio = 0m,
        decimal completeDeviation = 0m,
        decimal completeOverRatio = 1.05m,
        decimal groupDiscountRate = 0m,
        decimal supplySatisfiedRate = 0m,
        decimal fixedSatisfied = 110m,
        decimal nonFixedSatisfied = 120m,
        decimal qualifiedRate = 1m,
        int processInspectionReworkWeight = 0,
        int processInspectionWarehouseWeight = 0,
        int processInspectionScrapWeight = 0,
        int processInspectionDefectWeight = 0,
        int finalInspectionReworkWeight = 0,
        int finalInspectionWarehouseWeight = 0,
        int finalInspectionScrapWeight = 0,
        int finalInspectionDefectQty = 0,
        int finalInspectionDefectWeight = 0,
        List<(decimal Weight, decimal UnitWeight)>? reworkSourceEntries = null)
    {
        // Group 1: 直接从工单复制（Salesman 从 SalesOrder 快照字段读取，已由调用方传入）
        var summary = new WorkOrderExecutionSummary
        {
            WorkOrderId = wo.Id,
            WorkOrderNo = wo.WorkOrderNo,
            Salesman = salesman,
            CustomerName = customerName,
            EndCustomer = endCustomer,
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

        // G3 字段（MaterialPlanStatus/MainNoMaterialPlanRate/MainNoMaterialPlanStatus/
        //   MaterialPlanCoveredCount/MaterialPlanProportion/TheoreticalCutoffDate）
        // 已从 WorkOrderListSummary 预计算读取，由调用方在外层设置

        // Group 11: 目标批次（生产类型≠返整 且 制造物品=订单成品）
        // 注意：DB 存储的是英文枚举值（如 "Rework"、"OrderFinished"），非中文
        var targetBatches = batches
            .Where(b => b.ProductionType != "Rework" && (b.ManufacturingItem == "OrderFinished" || b.ManufacturingItem == "SpecialDeliveryStatus"))
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
        var isFixed = summary.LengthStatus == LengthStatus.Fixed.ToString();
        foreach (var batch in targetBatches)
        {
            var batchInputQty = batch.InputQuantity ?? 0;
            var batchInputWeight = batch.InputWeight ?? 0m;

            // 合格率：定尺按批次生产类型（库存/外购按 100%，其它投料类型按配置 QualifiedRate）；
            // 非定尺已有工序组扣损（×2.5%/组），按产出的 100% 合格率计算
            var batchQualifiedRate = isFixed
                ? (batch.ProductionType == "Inventory" || batch.ProductionType == "OutsourcedPurchased"
                    ? 1.0m : qualifiedRate)
                : 1.0m;

            // 理论成品支数 = 投料支数 × 制成倍数 × 合格率
            if (batch.ProductionRatio > 0)
                theorQty += batchInputQty * batch.ProductionRatio * batchQualifiedRate;

            // 理论成品重量 = 投料重量 × (1 - 有效工序组数 × 2.5%)
            var effectiveGroupCount = batch.ProcessGroups?
                .Count(pg => HasAnySection(pg)) ?? 0;
            var discount = 1.0m - effectiveGroupCount * groupDiscountRate;
            if (discount < 0) discount = 0;
            theorWeight += Math.Round(batchInputWeight * discount, 3);
        }
        // 理论成品支数为整数值，四舍五入
        summary.TheoreticalOutputQty = Math.Round(theorQty, 0, MidpointRounding.AwayFromZero);
        summary.TheoreticalOutputWeight = Math.Round(theorWeight, 3);

        // 投料成品比 + 状态（4 档：0=未投料 1=部分 2=满足 3=超量）
        var inputExceedRate = summary.LengthStatus == LengthStatus.Fixed.ToString() ? fixedSatisfied : nonFixedSatisfied;
        var (ratio, status) = ComputeInputRatioAndStatus(summary, wo, supplySatisfiedRate, inputExceedRate);
        summary.InputOutputRatio = ratio;
        summary.InputStatus = status;

        // Group 13: 有效批次（批次范围与 G11 目标批次一致：非返整 + 全部，含成检/完成）
        var validBatches = targetBatches.ToList();

        summary.ValidBatchCount = validBatches.Count;
        summary.ValidInputQuantity = validBatches.Sum(b => b.CurrentValidQty ?? 0);
        summary.ValidInputWeight = validBatches.Sum(b => b.CurrentValidWeight ?? 0);

        // 有效理论成品（逐批计算）—— 与 G11 原始理论成品支同逻辑，仅基准换为有效投料（CurrentValidQty/Weight）
        decimal validTheorQty = 0;
        decimal validTheorWeight = 0;
        foreach (var batch in validBatches)
        {
            var batchInputQty = batch.CurrentValidQty ?? 0;
            var batchInputWeight = batch.CurrentValidWeight ?? 0m;

            // 合格率：定尺按批次生产类型（库存/外购按 100%，其它投料类型按配置 QualifiedRate）；
            // 非定尺已有工序组扣损（×2.5%/组），按产出的 100% 合格率计算
            var batchQualifiedRate = isFixed
                ? (batch.ProductionType == "Inventory" || batch.ProductionType == "OutsourcedPurchased"
                    ? 1.0m : qualifiedRate)
                : 1.0m;

            // 流转成品支数 = 有效投料支数 × 制成倍数 × 合格率
            if (batch.ProductionRatio > 0)
                validTheorQty += batchInputQty * batch.ProductionRatio * batchQualifiedRate;

            var effectiveGroupCount = batch.ProcessGroups?
                .Count(pg => HasAnySection(pg)) ?? 0;
            var discount = 1.0m - effectiveGroupCount * groupDiscountRate;
            if (discount < 0) discount = 0;
            validTheorWeight += Math.Round(batchInputWeight * discount, 3);
        }
        // 流转成品支数为整数值，四舍五入
        summary.ValidOutputQty = Math.Round(validTheorQty, 0, MidpointRounding.AwayFromZero);
        summary.ValidOutputWeight = Math.Round(validTheorWeight, 3);

        // Group 14: 返整执行数据（ProductionType=Rework 且 ManufacturingItem=OrderFinished）
        var reworkBatches = batches
            .Where(b => b.ProductionType == "Rework" && (b.ManufacturingItem == "OrderFinished" || b.ManufacturingItem == "SpecialDeliveryStatus"))
            .ToList();

        var reworkDates = reworkBatches
            .Select(b => b.CreatedTime.DateTime)
            .ToList();
        summary.ReworkInputEndDate = reworkDates.Count > 0 ? reworkDates.Max() : null;
        summary.ReworkBatchCount = reworkBatches.Count;
        summary.ReworkInputQuantity = reworkBatches.Sum(b => b.CurrentValidQty ?? 0);
        summary.ReworkInputWeight = reworkBatches.Sum(b => b.CurrentValidWeight ?? 0);

        // G15 次品总量：过程检/成检次品聚合（仅订单成品批次，0 时不显示）
        summary.ProcessInspectionDefectWeight = processInspectionDefectWeight > 0 ? processInspectionDefectWeight : null;
        summary.ProcessInspectionReworkWeight = processInspectionReworkWeight > 0 ? processInspectionReworkWeight : null;
        summary.ProcessInspectionWarehouseWeight = processInspectionWarehouseWeight > 0 ? processInspectionWarehouseWeight : null;
        summary.ProcessInspectionScrapWeight = processInspectionScrapWeight > 0 ? processInspectionScrapWeight : null;
        summary.FinalInspectionDefectQty = finalInspectionDefectQty > 0 ? finalInspectionDefectQty : null;
        summary.FinalInspectionDefectWeight = finalInspectionDefectWeight > 0 ? finalInspectionDefectWeight : null;
        summary.FinalInspectionReworkWeight = finalInspectionReworkWeight > 0 ? finalInspectionReworkWeight : null;
        summary.FinalInspectionWarehouseWeight = finalInspectionWarehouseWeight > 0 ? finalInspectionWarehouseWeight : null;
        summary.FinalInspectionScrapWeight = finalInspectionScrapWeight > 0 ? finalInspectionScrapWeight : null;
        var reworkDefectQty = (decimal)(processInspectionReworkWeight + finalInspectionReworkWeight);

        // 理论返整可产成支 = Σ(每条返整记录重量 ÷ 该记录原批次单支重)
        // 原批次单支重：统一按批次「理论单支重」（无论定尺）；理论单支重缺失时兜底按「领料重量 ÷ (领料支数 × 制成倍数)」估算；
        // 单支重仍缺失的返整记录不贡献可产成支（但返整量合计照常计入）
        if (reworkSourceEntries is { Count: > 0 })
        {
            var produceQty = reworkSourceEntries.Sum(e => e.Weight / e.UnitWeight);
            summary.ReworkTheoreticalProduceQty = produceQty > 0
                ? (int)Math.Round(produceQty, 0, MidpointRounding.AwayFromZero)
                : null;
        }
        else
        {
            summary.ReworkTheoreticalProduceQty = null;
        }

        // 是否必返整（ReworkInputConsistency）由主号级聚合在 ComputeMainNoInputAggregation 统一判定，此处不设置

        decimal reworkTheorQty = 0;
        decimal reworkTheorWeight = 0;
        foreach (var batch in reworkBatches)
        {
            var batchInputQty = batch.CurrentValidQty ?? 0;
            var batchInputWeight = batch.CurrentValidWeight ?? 0m;
            // 支数折算与合格流转一致：定尺时库存/外购投料按 100%，其它投料类型按配置 QualifiedRate；非定尺按 100%
            if (batch.ProductionRatio > 0)
            {
                var batchQualifiedRate = isFixed
                    ? (batch.ProductionType == "Inventory" || batch.ProductionType == "OutsourcedPurchased"
                        ? 1.0m : qualifiedRate)
                    : 1.0m;
                reworkTheorQty += batchInputQty * batch.ProductionRatio * batchQualifiedRate;
            }

            var effectiveGroupCount = batch.ProcessGroups?
                .Count(pg => HasAnySection(pg)) ?? 0;
            var discount = 1.0m - effectiveGroupCount * groupDiscountRate;
            if (discount < 0) discount = 0;
            reworkTheorWeight += Math.Round(batchInputWeight * discount, 3);
        }
        summary.ReworkTheoreticalOutputQty = Math.Round(reworkTheorQty, 3);
        summary.ReworkTheoreticalOutputWeight = Math.Round(reworkTheorWeight, 3);

        // 理论返整可产成重 = 过程检返整量×0.92 + 成品检返整量×0.96（无返整量为空）
        summary.ReworkTheoreticalProduceWeight = reworkDefectQty > 0
            ? Math.Round(processInspectionReworkWeight * 0.92m + finalInspectionReworkWeight * 0.96m, 3)
            : null;

        // 待返整成支 = 理论返整可产成支 − 返整理论成品支（无可产成支为空，负值归0）
        summary.PendingReworkOutputQty = summary.ReworkTheoreticalProduceQty.HasValue
            ? Math.Max(0m, summary.ReworkTheoreticalProduceQty.Value - summary.ReworkTheoreticalOutputQty)
            : null;

        // 待返整成重 = 理论返整可产成重 − 返整理论成品重（无可产成重为空，负值归0）
        summary.PendingReworkOutputWeight = summary.ReworkTheoreticalProduceWeight.HasValue
            ? Math.Max(0m, summary.ReworkTheoreticalProduceWeight.Value - summary.ReworkTheoreticalOutputWeight)
            : null;

        // Group 12: 有效流转（合格流转 + 返整执行 − 成检次品）
        // 合格流转×98%过程合格率与返整产出均为「成检前」理论值，需扣除实际成检次品（返整+入库+报废 支/重）；负值归零
        var combinedFlowQty = Math.Max(0, summary.ValidOutputQty + summary.ReworkTheoreticalOutputQty - finalInspectionDefectQty);
        var combinedFlowWeight = Math.Max(0, summary.ValidOutputWeight + summary.ReworkTheoreticalOutputWeight - finalInspectionDefectWeight);
        var flowExceedRate = summary.LengthStatus == LengthStatus.Fixed.ToString() ? fixedSatisfied : nonFixedSatisfied;
        var (flowRatio, flowStatus) = ComputeInputRatioAndStatus(
            summary.LengthStatus, combinedFlowQty, combinedFlowWeight, wo.TotalQuantity, wo.TotalWeight, supplySatisfiedRate, flowExceedRate);
        summary.FlowOutputRatio = flowRatio;
        summary.FlowStatus = flowStatus;

        // G12: 总批次数 & 未完成批数（制造物品=订单成品的所有批次）
        var allTargetBatches = targetBatches.Concat(reworkBatches).ToList();
        summary.FlowTotalBatchCount = allTargetBatches.Count;
        summary.FlowIncompleteBatchCount = allTargetBatches.Count(b => b.Status != BatchStatus.Completed);
        summary.FlowMaxRemainingWorkDays = allTargetBatches.Count > 0
            ? allTargetBatches.Max(b => b.RemainingWorkDays)
            : 0;

        // ========== Group 17: 在产节点待量 ==========
        // 8个固定节点定义：(工序组名称, 工段名称)
        var nodeDefs = new (string ProcessName, string SectionName)[]
        {
            (ProcessKeys.RoughTubeProcessing, SectionKeys.OuterPolish),
            (ProcessKeys.InProcessRepair, SectionKeys.Inspection),
            (ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw),
            (ProcessKeys.ColdRoll50, SectionKeys.ColdRollDraw),
            (ProcessKeys.ColdRoll30, SectionKeys.ColdRollDraw),
            (ProcessKeys.ColdRoll20, SectionKeys.ColdRollDraw),
            (ProcessKeys.ThreeRollColdRoll, SectionKeys.ColdRollDraw),
            (ProcessKeys.ColdDraw, SectionKeys.ColdRollDraw),
        };

        // 使用所有非完成/成检批次（含正常 + 返整）
        var group14Batches = batches.Where(b => b.Status != BatchStatus.InFinalInspection && b.Status != BatchStatus.Completed).ToList();

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
                    if (pn is ProcessKeys.RoughTubeProcessing or ProcessKeys.InProcessRepair)
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
        summary.PendingSectionRoughTube = pendingValues[ProcessKeys.RoughTubeProcessing] > 0 ? pendingValues[ProcessKeys.RoughTubeProcessing] : null;
        summary.PendingSectionWarehouseFix = pendingValues[ProcessKeys.InProcessRepair] > 0 ? pendingValues[ProcessKeys.InProcessRepair] : null;
        summary.PendingSection60Roll = pendingValues[ProcessKeys.ColdRoll60] > 0 ? pendingValues[ProcessKeys.ColdRoll60] : null;
        summary.PendingSection50Roll = pendingValues[ProcessKeys.ColdRoll50] > 0 ? pendingValues[ProcessKeys.ColdRoll50] : null;
        summary.PendingSection30Roll = pendingValues[ProcessKeys.ColdRoll30] > 0 ? pendingValues[ProcessKeys.ColdRoll30] : null;
        summary.PendingSection20Roll = pendingValues[ProcessKeys.ColdRoll20] > 0 ? pendingValues[ProcessKeys.ColdRoll20] : null;
        summary.PendingSectionThreeRoll = pendingValues[ProcessKeys.ThreeRollColdRoll] > 0 ? pendingValues[ProcessKeys.ThreeRollColdRoll] : null;
        summary.PendingSectionDrawBench = pendingValues[ProcessKeys.ColdDraw] > 0 ? pendingValues[ProcessKeys.ColdDraw] : null;

        // ========== 变形工序完成三档 + 生产关注工序（先判变形工序完成，再生成关注工序） ==========
        // 「无在产批次」= 没投料（无批次）或 生产编号既不在产也未产（批次全成检/完成）→ group14Batches 为空
        if (group14Batches.Count == 0)
        {
            // 略：无在产批次，与生产情况不相干 → 关注工序显示 "-"
            summary.DeformedProcessCompleted = null;
            summary.ProductionAttentionProcess = null;
        }
        else
        {
            // 变形工序完成 = 后6项（全部冷轧/冷拔）之和=0 → 是（收尾）；否则 → 否
            var rollingSum = pendingValues[ProcessKeys.ColdRoll60] + pendingValues[ProcessKeys.ColdRoll50] + pendingValues[ProcessKeys.ColdRoll30]
                + pendingValues[ProcessKeys.ColdRoll20] + pendingValues[ProcessKeys.ThreeRollColdRoll] + pendingValues[ProcessKeys.ColdDraw];
            summary.DeformedProcessCompleted = rollingSum == 0m;

            if (rollingSum == 0m)
            {
                // 是：变形工序全部完成，处于与成品检验衔接的收尾状态 → 关注工序「生产收尾」
                summary.ProductionAttentionProcess = ProductionAttentionKeys.Finish;
            }
            else
            {
                // 否：仍有变形待量 → 原逻辑，取待量>0 且 SequenceNumber 最小的工序名称
                // 取第一个有 ProcessGroup 的批次作为 SequenceNumber 参照
                var refPgMap = group14Batches
                    .Where(b => b.ProcessGroups != null && b.ProcessGroups.Count > 0)
                    .SelectMany(b => b.ProcessGroups)
                    .Where(pg => !string.IsNullOrEmpty(pg.ProcessName))
                    .GroupBy(pg => pg.ProcessName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Min(pg => pg.SequenceNumber), StringComparer.OrdinalIgnoreCase);

                summary.ProductionAttentionProcess = nodeDefs
                    .Where(n => pendingValues[n.ProcessName] > 0 && refPgMap.ContainsKey(n.ProcessName))
                    .OrderBy(n => refPgMap[n.ProcessName])
                    .Select(n => n.ProcessName)
                    .FirstOrDefault();
            }
        }

        // MaxBatchRemainingWorkDays: 此工单号下所有批次中 RemainingWorkDays 最大值
        summary.MaxBatchRemainingWorkDays = batches.Count > 0
            ? batches.Max(b => b.RemainingWorkDays)
            : (int?)null;

        return summary;
    }

    private static (decimal ratio, int status) ComputeInputRatioAndStatus(
        WorkOrderExecutionSummary summary, WoEntity wo, decimal satisfiedRate, decimal exceedRate)
    {
        return ComputeInputRatioAndStatus(summary.LengthStatus, summary.TheoreticalOutputQty, summary.TheoreticalOutputWeight, wo.TotalQuantity, wo.TotalWeight, satisfiedRate, exceedRate);
    }

    private static (decimal ratio, int status) ComputeInputRatioAndStatus(
        string lengthStatus, decimal outputQty, decimal outputWeight, int totalQty, decimal totalWeight, decimal satisfiedRate, decimal exceedRate)
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
        else if (ratio > exceedRate)
            status = 3;      // 超量
        else if (ratio >= satisfiedRate)
            status = 2;      // 满足
        else
            status = 1;      // 部分

        return (ratio, status);
    }

    /// <summary>
    /// 获取批次单支重(kg/支)：无论是否定尺，统一按批次「理论单支重」（TheoreticalUnitWeight）；
    /// 理论单支重缺失时，兜底按「领料重量 ÷ (领料支数 × 制成倍数)」估算。
    /// </summary>
    private static decimal? GetBatchUnitWeight(ProductionBatch? batch)
    {
        if (batch?.TheoreticalUnitWeight.HasValue == true)
            return batch.TheoreticalUnitWeight.Value;

        // 兜底：领料重量 / (领料支数 × 制成倍数)
        if (batch != null
            && batch.InputQuantity is > 0 && batch.ProductionRatio > 0 && batch.InputWeight.HasValue)
            return batch.InputWeight.Value / ((decimal)batch.InputQuantity.Value * batch.ProductionRatio);

        return null;
    }

    /// <summary>
    /// 构建返整可产成支明细：遍历过程检/成品检返整记录，按「该记录原批次单支重」折算。
    /// 仅收录返整重量 &gt; 0 且原批次单支重可得的记录（单支重 = 理论单支重，缺失兜底领料计算）。
    /// </summary>
    private static List<(decimal Weight, decimal UnitWeight)> BuildReworkSourceEntries(
        List<ProcessInspection>? processInspections,
        List<FinalInspection>? finalInspections)
    {
        var entries = new List<(decimal Weight, decimal UnitWeight)>();
        if (processInspections != null)
        {
            foreach (var pi in processInspections)
            {
                var w = pi.TheoreticalReworkWeight ?? 0;
                if (w <= 0) continue;
                var uw = GetBatchUnitWeight(pi.ProductionBatch);
                if (uw.HasValue) entries.Add((w, uw.Value));
            }
        }
        if (finalInspections != null)
        {
            foreach (var fi in finalInspections)
            {
                var w = fi.DefectReworkWeight ?? 0;
                if (w <= 0) continue;
                var uw = GetBatchUnitWeight(fi.ProductionBatch);
                if (uw.HasValue) entries.Add((w, uw.Value));
            }
        }
        return entries;
    }

    private static bool HasAnySection(ProcessGroup pg)
    {
        // "在制修检"和"附加成检"不计入有效工序组，不参与理论重量扣除
        if (pg.ProcessName == ProcessKeys.InProcessRepair || pg.ProcessName == ProcessKeys.AdditionalFinalInspection)
            return false;

        return pg.ColdRollDraw.HasValue
            || pg.OilPipeCut.HasValue
            || pg.Degrease.HasValue
            || pg.EmulsionWash.HasValue
            || pg.UltrasonicWash.HasValue
            || pg.ClothPolish.HasValue
            || pg.BrightAnnealing.HasValue
            || pg.Solution.HasValue
            || pg.Straighten.HasValue
            || pg.Cut.HasValue
            || pg.ThicknessMeasure.HasValue
            || pg.Pickle.HasValue
            || pg.OuterPolish.HasValue
            || pg.InnerPolish.HasValue
            || pg.InnerGrinding.HasValue
            || pg.OuterSpotGrinding.HasValue
            || pg.SandBlasting.HasValue
            || pg.ShotBlasting.HasValue
            || pg.Inspection.HasValue
            || pg.WeldingHead.HasValue
            || pg.Welding.HasValue
            || pg.Lubrication.HasValue
            || pg.Packing.HasValue
            || pg.Warehouse.HasValue
            || pg.Extra1.HasValue
            || pg.Extra2.HasValue;
    }

    private static void ComputeMainNoInputAggregation(
        List<WorkOrderExecutionSummary> summaries, List<WoEntity> workOrders,
        decimal supplySatisfiedRate, decimal fixedSatisfied, decimal nonFixedSatisfied, decimal planExecLowerBound)
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

            // Group 3 的 MainNo 级用料计划（满足率/状态）已从 WorkOrderListSummary 预读，不再重算

            // Group 3: MainNo 级计划执行状态（4 档：0=无计划 1=未执行 2=执行中 3=计划落实）
            // 同主号所有工单的 G4~G10 计划量/现可量（到货量口径）求和后按比例判定
            var mainNoTotalPlan = groupSummaries.Sum(s => s.PiercingPlanWeight + s.SemiPlanWeight + s.FinishPlanWeight
                + s.InventoryPlanWeight + s.ReworkPlanWeight + s.InProcessReworkPlanWeight + s.InMainPlanWeight);
            var mainNoTotalAvail = groupSummaries.Sum(s => s.PiercingSubInWeight + s.SemiInWeight + s.FinishInWeight
                + s.InventoryOutWeight + s.ReworkPlanInputWeight + s.InProcessReworkInputWeight + s.InMainInputWeight);
            var mainNoTotalMissing = Math.Max(0m, mainNoTotalPlan - mainNoTotalAvail);

            int mainNoPlanExec;
            if (mainNoTotalPlan <= 0) mainNoPlanExec = 0;                                      // 无计划
            else if (mainNoTotalAvail <= 0) mainNoPlanExec = 1;                                // 未执行
            else if (mainNoTotalMissing / mainNoTotalPlan <= 1 - planExecLowerBound) mainNoPlanExec = 3;  // 计划落实（缺口≤3%）
            else mainNoPlanExec = 2;                                                           // 执行中
            foreach (var s in groupSummaries) s.MainNoPlanExecutionStatus = mainNoPlanExec;

            // Group 11: MainNo 级投料聚合（使用已修正的理论成品值）
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

                var mainExceedRate = isFixed ? fixedSatisfied : nonFixedSatisfied;
                int mainStatus;
                if (mainRatio <= 0)
                    mainStatus = 0;
                else if (mainRatio > mainExceedRate)
                    mainStatus = 3;      // 超量
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

            // Group 12: 有效流转主号级聚合（合格流转 + 返整执行 − 成检次品，负值归零）
            var totalFlowQty = Math.Max(0, groupSummaries.Sum(s => s.ValidOutputQty + s.ReworkTheoreticalOutputQty - (s.FinalInspectionDefectQty ?? 0)));
            var totalFlowWeight = Math.Max(0, groupSummaries.Sum(s => s.ValidOutputWeight + s.ReworkTheoreticalOutputWeight - (s.FinalInspectionDefectWeight ?? 0)));

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

                var mainFlowExceedRate = isFixed ? fixedSatisfied : nonFixedSatisfied;
                int mainFlowStatus;
                if (mainFlowRatio <= 0)
                    mainFlowStatus = 0;
                else if (mainFlowRatio > mainFlowExceedRate)
                    mainFlowStatus = 3;      // 超量
                else if (mainFlowRatio >= supplySatisfiedRate)
                    mainFlowStatus = 2;
                else
                    mainFlowStatus = 1;

                foreach (var s in groupSummaries)
                {
                    s.MainNoFlowOutputRatio = mainFlowRatio;
                    s.MainNoFlowStatus = mainFlowStatus;
                }

                // Group 14: 附返整主号状态 —— 有效流转基础上加「待返整」（= 合格流转 + 理论返整可产成 − 成检次品），负值归零
                var totalWithReworkQty = Math.Max(0, groupSummaries.Sum(s => s.ValidOutputQty + (s.ReworkTheoreticalProduceQty ?? 0) - (s.FinalInspectionDefectQty ?? 0)));
                var totalWithReworkWeight = Math.Max(0, groupSummaries.Sum(s => s.ValidOutputWeight + (s.ReworkTheoreticalProduceWeight ?? 0) - (s.FinalInspectionDefectWeight ?? 0)));

                decimal reworkMainRatio;
                if (isFixed)
                {
                    reworkMainRatio = totalQty > 0
                        ? Math.Round(totalWithReworkQty / totalQty * 100, 2)
                        : 0;
                }
                else
                {
                    reworkMainRatio = totalWeight > 0
                        ? Math.Round(totalWithReworkWeight / totalWeight * 100, 2)
                        : 0;
                }

                var reworkExceedRate = isFixed ? fixedSatisfied : nonFixedSatisfied;
                int reworkMainStatus;
                if (reworkMainRatio <= 0)
                    reworkMainStatus = 0;
                else if (reworkMainRatio > reworkExceedRate)
                    reworkMainStatus = 3;      // 超量
                else if (reworkMainRatio >= supplySatisfiedRate)
                    reworkMainStatus = 2;
                else
                    reworkMainStatus = 1;

                // 是否必返整：附返整主号状态=满足/超量 且 有效主号状态未满足（未投料/部分；超量视为满足）
                var mustRework = reworkMainStatus >= 2 && mainFlowStatus is 0 or 1;
                foreach (var s in groupSummaries)
                {
                    s.ReworkMainNoStatus = reworkMainStatus;
                    s.ReworkInputConsistency = mustRework ? "是" : "否";
                }
            }

            // 主号截止到料日 = 组内各工单截止到料日的最大值
            var mainNoCutoffDates = groupSummaries.Where(s => s.CutoffArrivalDate.HasValue).Select(s => s.CutoffArrivalDate!.Value).ToList();
            var mainNoCutoffArrivalDate = mainNoCutoffDates.Count > 0 ? (DateTime?)mainNoCutoffDates.Max() : null;
            foreach (var s in groupSummaries) s.MainNoCutoffArrivalDate = mainNoCutoffArrivalDate;
        }
    }

    /// <summary>
    /// 批量构建「工单号 → 最大到料日期」：G4~G6 的仓库进库（InventoryBatch）按来源单号（采购单/委外单）映射回工单号，
    /// 取该工单所有来源进库批次 InboundDate 的最大值。仅统计 SourceOrderNo 非空的进库批次（委外/荒管/成品均有来源单号）。
    /// </summary>
    private async Task<Dictionary<string, DateTime>> BuildArrivalDateByWorkOrderNoAsync(
        List<PurchaseOrder> allPurchaseOrders, List<SubcontractReturnItem> returnItems)
    {
        // 来源单号 → 工单号（采购单按 OrderNo，委外按 ReturnItem 冗余的 OrderNo）
        var sourceOrderToWoNo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var po in allPurchaseOrders)
        {
            if (string.IsNullOrEmpty(po.OrderNo) || string.IsNullOrEmpty(po.SourceWorkOrderNo)) continue;
            sourceOrderToWoNo[po.OrderNo] = po.SourceWorkOrderNo;
        }
        foreach (var ri in returnItems)
        {
            if (string.IsNullOrEmpty(ri.OrderNo) || string.IsNullOrEmpty(ri.SourceWorkOrderNo)) continue;
            sourceOrderToWoNo[ri.OrderNo] = ri.SourceWorkOrderNo;
        }

        var result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        if (sourceOrderToWoNo.Count == 0) return result;

        var sourceOrderNos = sourceOrderToWoNo.Keys.ToList();
        var arrivalDates = await _context.InventoryBatches
            .AsNoTracking()
            .Where(ib => ib.SourceOrderNo != null && sourceOrderNos.Contains(ib.SourceOrderNo))
            .Select(ib => new { ib.SourceOrderNo, ib.InboundDate })
            .ToListAsync();

        foreach (var a in arrivalDates)
        {
            if (string.IsNullOrEmpty(a.SourceOrderNo) || !sourceOrderToWoNo.TryGetValue(a.SourceOrderNo, out var woNo)) continue;
            if (result.TryGetValue(woNo, out var cur))
            {
                if (a.InboundDate > cur) result[woNo] = a.InboundDate;
            }
            else
            {
                result[woNo] = a.InboundDate;
            }
        }
        return result;
    }

    /// <summary>
    /// 计算主号级和订单级入库状态
    /// 主号级：按「主号聚合入库量 vs 主号总需求」判定（定尺按支数/非定尺按重量，非定尺沿用 completeRatio/completeDeviation 容差）
    ///         四档 0=无入库（聚合量=0）/1=入库部分（0&lt;聚合量&lt;需求）/2=入库完结（聚合量=需求或达容差）/3=入库超额（定尺聚合量&gt;需求，或重量口径聚合量&gt;需求×completeOverRatio）
    /// 订单级：按主号入库状态上卷（全部无入库→0；全部达成（完结/超额）→2；否则→1），不再直接上卷工单状态
    /// </summary>
    private static void ComputeWarehousingAggregation(
        List<WorkOrderExecutionSummary> summaries, List<WoEntity> workOrders,
        decimal completeRatio, decimal completeDeviation, decimal completeOverRatio)
    {
        var woDict = workOrders.ToDictionary(wo => wo.Id);

        // 按 (SalesOrderNo, ProductionMainNo) 分组 → 主号级：聚合入库量 vs 主号总需求
        var mainNoGroups = summaries
            .Where(s => woDict.ContainsKey(s.WorkOrderId))
            .Select(s => new { Summary = s, WorkOrder = woDict[s.WorkOrderId] })
            .GroupBy(x => new { x.WorkOrder.SalesOrderNo, MainNo = x.Summary.ProductionMainNo })
            .ToList();

        foreach (var group in mainNoGroups)
        {
            var groupSummaries = group.Select(g => g.Summary).ToList();
            // 主号下工单长度状态应一致，全为定尺按支数口径，否则按重量口径（保守）
            var isFixed = groupSummaries.All(s => s.LengthStatus == LengthStatus.Fixed.ToString());

            // 主号聚合入库量（各工单 WarehousingTotal* 之和）
            var inboundQty = groupSummaries.Sum(s => s.WarehousingTotalQty);
            var inboundWeight = groupSummaries.Sum(s => s.WarehousingTotalWeight);
            // 主号总需求（各工单 TotalQuantity/TotalWeight 之和）
            var requireQty = group.Sum(g => g.WorkOrder.TotalQuantity);
            var requireWeight = group.Sum(g => g.WorkOrder.TotalWeight);

            int mainNoStatus;
            if (isFixed)
            {
                if (inboundQty == 0)
                    mainNoStatus = 0; // 无入库
                else if (inboundQty > requireQty)
                    mainNoStatus = 3; // 超额
                else if (inboundQty == requireQty)
                    mainNoStatus = 2; // 完结
                else
                    mainNoStatus = 1; // 部分
            }
            else
            {
                if (inboundWeight == 0)
                    mainNoStatus = 0; // 无入库
                else if (inboundWeight > requireWeight * completeOverRatio)
                    mainNoStatus = 3; // 超额（重量口径：超过需求×105%，与工单级一致）
                else if (inboundWeight >= requireWeight * completeRatio
                      && inboundWeight >= requireWeight - completeDeviation)
                    mainNoStatus = 2; // 完结（含容差）
                else
                    mainNoStatus = 1; // 部分
            }

            foreach (var s in groupSummaries)
                s.MainNoWarehousingStatus = mainNoStatus;
        }

        // 按 SalesOrderNo 分组 → 订单级：从主号入库状态上卷（全0→0；全达成(2/3)→2；否则→1）
        var orderGroups = summaries
            .Where(s => woDict.ContainsKey(s.WorkOrderId))
            .Select(s => new { Summary = s, WorkOrder = woDict[s.WorkOrderId] })
            .GroupBy(x => x.WorkOrder.SalesOrderNo)
            .ToList();

        foreach (var group in orderGroups)
        {
            var groupSummaries = group.Select(g => g.Summary).ToList();
            var mainNoStatuses = groupSummaries.Select(s => s.MainNoWarehousingStatus).Distinct().ToList();

            int orderStatus;
            if (mainNoStatuses.All(st => st == 0))
                orderStatus = 0; // 全部无入库
            else if (mainNoStatuses.All(st => st == 2 || st == 3))
                orderStatus = 2; // 全部达成（完结/超额）
            else
                orderStatus = 1; // 入库部分

            foreach (var s in groupSummaries)
                s.OrderWarehousingStatus = orderStatus;
        }
    }

    /// <summary>
    /// 计算计划执行状态（5档）
    /// </summary>
    /// <param name="actual">实际执行量</param>
    /// <param name="plan">计划量</param>
    /// <param name="lower">容差下限</param>
    /// <param name="upper">容差上限</param>
    /// <param name="treatZeroActualAsPartial">
    /// 两个维度区分开关：
    /// - false（动作类：采购/出库/投料）—— actual≤0 视为「未执行」（有计划但没开始动作）
    /// - true（结果类：到货/回收）—— actual≤0 视为「部分」（上游已下单/外发，结果未反馈=执行中）
    /// </param>
    /// <returns>0=无计划 1=未执行 2=部分 3=已完成 4=异常</returns>
    private static int ComputePlanStatus(decimal actual, decimal plan, decimal lower, decimal upper, bool treatZeroActualAsPartial = false)
    {
        if (plan <= 0) return 0; // 无计划
        if (actual <= 0) return treatZeroActualAsPartial ? 2 : 1; // 结果类：已启动待反馈→部分；动作类：未开始→未执行

        var ratio = actual / plan;
        if (ratio < lower) return 2;  // 部分
        if (ratio <= upper) return 3; // 已完成
        return 4; // 异常
    }

    private static void CopySummaryToExisting(WorkOrderExecutionSummary source, WorkOrderExecutionSummary target)
    {
        // Group 1
        target.WorkOrderNo = source.WorkOrderNo;
        target.Salesman = source.Salesman;
        target.CustomerName = source.CustomerName;
        target.EndCustomer = source.EndCustomer;
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

        // Group 3
        target.MaterialPlanStatus = source.MaterialPlanStatus;
        target.MainNoMaterialPlanRate = source.MainNoMaterialPlanRate;
        target.MainNoMaterialPlanStatus = source.MainNoMaterialPlanStatus;
        target.MainNoPlanExecutionStatus = source.MainNoPlanExecutionStatus;
        target.ProcessCycle = source.ProcessCycle;
        target.MaterialPlanCoveredCount = source.MaterialPlanCoveredCount;
        target.MaterialPlanProportion = source.MaterialPlanProportion;
        target.TheoreticalCutoffDate = source.TheoreticalCutoffDate;
        target.CutoffArrivalDate = source.CutoffArrivalDate;
        target.MainNoCutoffArrivalDate = source.MainNoCutoffArrivalDate;

        // Group（已废弃）: 物料执行
        target.PendingRoughTubeQty = source.PendingRoughTubeQty;
        target.PendingRoughTubeWeight = source.PendingRoughTubeWeight;
        target.PendingOutsourceFinishQty = source.PendingOutsourceFinishQty;
        target.PendingOutsourceFinishWeight = source.PendingOutsourceFinishWeight;
        target.TheoreticalFinishQty = source.TheoreticalFinishQty;
        target.TheoreticalFinishWeight = source.TheoreticalFinishWeight;

        // G4~G10: 7 种用料计划执行状况
        target.PiercingPlanWeight = source.PiercingPlanWeight;
        target.PiercingSubOutWeight = source.PiercingSubOutWeight;
        target.PiercingSubStatus = source.PiercingSubStatus;
        target.PiercingSubInWeight = source.PiercingSubInWeight;
        target.PiercingSubPendingWeight = source.PiercingSubPendingWeight;
        target.PiercingReturnStatus = source.PiercingReturnStatus;
        target.SemiPlanWeight = source.SemiPlanWeight;
        target.SemiOrderWeight = source.SemiOrderWeight;
        target.SemiOrderStatus = source.SemiOrderStatus;
        target.SemiInWeight = source.SemiInWeight;
        target.SemiPendingWeight = source.SemiPendingWeight;
        target.SemiInStatus = source.SemiInStatus;
        target.FinishPlanWeight = source.FinishPlanWeight;
        target.FinishOrderWeight = source.FinishOrderWeight;
        target.FinishOrderStatus = source.FinishOrderStatus;
        target.FinishInWeight = source.FinishInWeight;
        target.FinishPendingWeight = source.FinishPendingWeight;
        target.FinishInStatus = source.FinishInStatus;
        target.InventoryPlanWeight = source.InventoryPlanWeight;
        target.InventoryOutWeight = source.InventoryOutWeight;
        target.InventoryOutStatus = source.InventoryOutStatus;
        target.ReworkPlanWeight = source.ReworkPlanWeight;
        target.ReworkPlanInputWeight = source.ReworkPlanInputWeight;
        target.ReworkPlanInputStatus = source.ReworkPlanInputStatus;
        target.InProcessReworkPlanWeight = source.InProcessReworkPlanWeight;
        target.InProcessReworkInputWeight = source.InProcessReworkInputWeight;
        target.InProcessReworkInputStatus = source.InProcessReworkInputStatus;
        target.InMainPlanWeight = source.InMainPlanWeight;
        target.InMainInputWeight = source.InMainInputWeight;
        target.InMainInputStatus = source.InMainInputStatus;

        // Group 11
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

        // Group 13
        target.ValidBatchCount = source.ValidBatchCount;
        target.ValidInputQuantity = source.ValidInputQuantity;
        target.ValidInputWeight = source.ValidInputWeight;
        target.ValidOutputQty = source.ValidOutputQty;
        target.ValidOutputWeight = source.ValidOutputWeight;
        // Group 14
        target.ReworkTheoreticalProduceQty = source.ReworkTheoreticalProduceQty;
        target.ReworkTheoreticalProduceWeight = source.ReworkTheoreticalProduceWeight;
        target.PendingReworkOutputQty = source.PendingReworkOutputQty;
        target.PendingReworkOutputWeight = source.PendingReworkOutputWeight;
        target.ReworkMainNoStatus = source.ReworkMainNoStatus;
        target.ReworkInputConsistency = source.ReworkInputConsistency;
        target.ReworkInputEndDate = source.ReworkInputEndDate;
        target.ReworkBatchCount = source.ReworkBatchCount;
        target.ReworkInputQuantity = source.ReworkInputQuantity;
        target.ReworkInputWeight = source.ReworkInputWeight;
        target.ReworkTheoreticalOutputQty = source.ReworkTheoreticalOutputQty;
        target.ReworkTheoreticalOutputWeight = source.ReworkTheoreticalOutputWeight;

        // Group 15 次品总量
        target.ProcessInspectionDefectWeight = source.ProcessInspectionDefectWeight;
        target.ProcessInspectionReworkWeight = source.ProcessInspectionReworkWeight;
        target.ProcessInspectionWarehouseWeight = source.ProcessInspectionWarehouseWeight;
        target.ProcessInspectionScrapWeight = source.ProcessInspectionScrapWeight;
        target.FinalInspectionDefectQty = source.FinalInspectionDefectQty;
        target.FinalInspectionDefectWeight = source.FinalInspectionDefectWeight;
        target.FinalInspectionReworkWeight = source.FinalInspectionReworkWeight;
        target.FinalInspectionWarehouseWeight = source.FinalInspectionWarehouseWeight;
        target.FinalInspectionScrapWeight = source.FinalInspectionScrapWeight;

        // Group 12
        target.FlowOutputRatio = source.FlowOutputRatio;
        target.FlowStatus = source.FlowStatus;
        target.MainNoFlowOutputRatio = source.MainNoFlowOutputRatio;
        target.MainNoFlowStatus = source.MainNoFlowStatus;
        target.FlowTotalBatchCount = source.FlowTotalBatchCount;
        target.FlowIncompleteBatchCount = source.FlowIncompleteBatchCount;
        target.FlowMaxRemainingWorkDays = source.FlowMaxRemainingWorkDays;

        // Group 15
        target.WarehousingStartDate = source.WarehousingStartDate;
        target.WarehousingEndDate = source.WarehousingEndDate;
        target.WarehousingTotalQty = source.WarehousingTotalQty;
        target.WarehousingTotalWeight = source.WarehousingTotalWeight;
        target.WoWarehousingStatus = source.WoWarehousingStatus;
        target.MainNoWarehousingStatus = source.MainNoWarehousingStatus;
        target.OrderWarehousingStatus = source.OrderWarehousingStatus;

        // G16
        target.ScheduleStage = source.ScheduleStage;
        target.TotalRemainingWorkDays = source.TotalRemainingWorkDays;
        target.CapacityWorkDays = source.CapacityWorkDays;
        target.UrgencyLevel = source.UrgencyLevel;
        target.EstimatedProcessCompletionDate = source.EstimatedProcessCompletionDate;
        target.DaysDiffFromDelivery = source.DaysDiffFromDelivery;
        target.RawMaterialLockRemark = source.RawMaterialLockRemark;

        // Group 17
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

        // Group 2
        target.IsUrging = source.IsUrging;
        target.IsBatchDelivery = source.IsBatchDelivery;
        target.IsPaused = source.IsPaused;
        target.IsForceCompleted = source.IsForceCompleted;
        target.AdjustmentRemark = source.AdjustmentRemark;
        target.ProductionFlowProperty = source.ProductionFlowProperty;

        // 刷新时间
        target.LastRefreshTime = source.LastRefreshTime;
    }

    public async Task<List<WorkOrderExecutionDashboardItem>> GetDashboardSummaryAsync()
    {
        var result = new List<WorkOrderExecutionDashboardItem>();

        // ========== Stage 1: 原料锁定 ==========
        // 待投料 = (TotalWeight - 成购缺口) × RawMaterialRatio - InputWeight
        // 成购缺口 = Max(0, FinishPlanWeight - FinishInWeight)（外购成品由供应商生产、本厂不投料，计划缺口口径，与原锁页一致）
        var rawMaterialRatio = await GetConfigAsync("ProcessingDiscount", "RawMaterialRatio", 1.1m);
        var stage1Data = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Where(x => x.ScheduleStage == 2 && x.UrgencyLevel != null)
            .Select(x => new
            {
                UrgencyLevel = x.UrgencyLevel ?? "",
                PendingWeight = (x.TotalWeight - (x.FinishPlanWeight > x.FinishInWeight ? x.FinishPlanWeight - x.FinishInWeight : 0m)) * rawMaterialRatio - x.InputWeight
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
                    // 字符串
                    s.WorkOrderNo,
                    s.Salesman,
                    s.CustomerName,
                    s.EndCustomer,
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
                    s.ReworkInputConsistency,
                    // 日期
                    s.SignDate,
                    s.DeliveryDate,
                    s.TheoreticalCutoffDate,
                    s.CutoffArrivalDate,
                    s.MainNoCutoffArrivalDate,
                    s.InputStartDate,
                    s.InputEndDate,
                    s.ReworkInputEndDate,
                    s.WarehousingStartDate,
                    s.WarehousingEndDate,
                    s.EstimatedProcessCompletionDate,
                    // 整数（含可空）
                    s.TotalItemCount,
                    s.TotalQuantity,
                    s.MaterialPlanCoveredCount,
                    s.MainNoPlanExecutionStatus,
                    s.TotalBatchCount,
                    s.InputQuantity,
                    s.ValidBatchCount,
                    s.ValidInputQuantity,
                    s.FlowTotalBatchCount,
                    s.FlowIncompleteBatchCount,
                    s.FlowMaxRemainingWorkDays,
                    s.ReworkBatchCount,
                    s.ReworkInputQuantity,
                    s.WarehousingTotalQty,
                    s.TotalRemainingWorkDays,
                    s.CapacityWorkDays,
                    s.DaysDiffFromDelivery,
                    s.MaxBatchRemainingWorkDays,
                    s.ReworkTheoreticalProduceQty,
                    s.ProcessInspectionDefectWeight,
                    s.ProcessInspectionReworkWeight,
                    s.ProcessInspectionWarehouseWeight,
                    s.ProcessInspectionScrapWeight,
                    s.FinalInspectionDefectQty,
                    s.FinalInspectionDefectWeight,
                    s.FinalInspectionReworkWeight,
                    s.FinalInspectionWarehouseWeight,
                    s.FinalInspectionScrapWeight,
                    // 小数（含可空）
                    s.MinLength,
                    s.MaxLength,
                    s.TotalMeters,
                    s.TotalWeight,
                    s.MainNoMaterialPlanRate,
                    s.InputWeight,
                    s.TheoreticalOutputQty,
                    s.TheoreticalOutputWeight,
                    s.InputOutputRatio,
                    s.MainNoInputOutputRatio,
                    s.ValidInputWeight,
                    s.ValidOutputQty,
                    s.ValidOutputWeight,
                    s.FlowOutputRatio,
                    s.MainNoFlowOutputRatio,
                    s.WarehousingTotalWeight,
                    s.ReworkTheoreticalProduceWeight,
                    s.PendingReworkOutputQty,
                    s.PendingReworkOutputWeight,
                    s.ReworkInputWeight,
                    s.ReworkTheoreticalOutputQty,
                    s.ReworkTheoreticalOutputWeight,
                    s.PiercingPlanWeight,
                    s.PiercingSubOutWeight,
                    s.PiercingSubInWeight,
                    s.PiercingSubPendingWeight,
                    s.SemiPlanWeight,
                    s.SemiOrderWeight,
                    s.SemiInWeight,
                    s.SemiPendingWeight,
                    s.FinishPlanWeight,
                    s.FinishOrderWeight,
                    s.FinishInWeight,
                    s.FinishPendingWeight,
                    s.InventoryPlanWeight,
                    s.InventoryOutWeight,
                    s.ReworkPlanWeight,
                    s.ReworkPlanInputWeight,
                    s.InProcessReworkPlanWeight,
                    s.InProcessReworkInputWeight,
                    s.InMainPlanWeight,
                    s.InMainInputWeight,
                    s.PendingSectionRoughTube,
                    s.PendingSectionWarehouseFix,
                    s.PendingSection60Roll,
                    s.PendingSection50Roll,
                    s.PendingSection30Roll,
                    s.PendingSection20Roll,
                    s.PendingSectionThreeRoll,
                    s.PendingSectionDrawBench,
                })
                .ToListAsync();

            // 数值/日期列 DISTINCT 格式化辅助（与列表页单元格显示口径一致：小数 G29、日期 yyyy-MM-dd）
            List<string> DistinctInts(IEnumerable<int> vals) =>
                vals.Distinct().OrderBy(v => v).Select(v => v.ToString()).ToList();
            List<string> DistinctNullableInts(IEnumerable<int?> vals) =>
                vals.Where(v => v.HasValue).Select(v => v!.Value).Distinct().OrderBy(v => v).Select(v => v.ToString()).ToList();
            List<string> DistinctDecimals(IEnumerable<decimal> vals) =>
                vals.Distinct().OrderBy(v => v).Select(v => v.ToString("G29")).ToList();
            List<string> DistinctNullableDecimals(IEnumerable<decimal?> vals) =>
                vals.Where(v => v.HasValue).Select(v => v!.Value).Distinct().OrderBy(v => v).Select(v => v.ToString("G29")).ToList();
            List<string> DistinctDates(IEnumerable<DateTime> vals) =>
                vals.Select(v => v.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList();
            List<string> DistinctNullableDates(IEnumerable<DateTime?> vals) =>
                vals.Where(v => v.HasValue).Select(v => v!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList();

            return new Dictionary<string, List<string>>
            {
                // ===== 字符串 =====
                ["WorkOrderNo"] = all.Select(x => x.WorkOrderNo).Distinct().OrderBy(x => x).ToList(),
                ["Salesman"] = all.Select(x => x.Salesman).Distinct().OrderBy(x => x).ToList(),
                ["CustomerName"] = all.Select(x => x.CustomerName).Distinct().OrderBy(x => x).ToList(),
                ["EndCustomer"] = all.Where(x => x.EndCustomer != null).Select(x => x.EndCustomer!).Distinct().OrderBy(x => x).ToList(),
                ["SalesOrderNo"] = all.Select(x => x.SalesOrderNo).Distinct().OrderBy(x => x).ToList(),
                ["ProductionMainNo"] = all.Select(x => x.ProductionMainNo).Distinct().OrderBy(x => x).ToList(),
                ["ProductionSubNo"] = all.Where(x => x.ProductionSubNo != null).Select(x => x.ProductionSubNo!).Distinct().OrderBy(x => x).ToList(),
                ["PlantGrade"] = all.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
                ["Specification"] = all.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
                ["UrgencyLevel"] = all.Where(x => x.UrgencyLevel != null).Select(x => x.UrgencyLevel!).Distinct().OrderBy(x => x).ToList(),
                ["RawMaterialLockRemark"] = all.Where(x => x.RawMaterialLockRemark != null).Select(x => x.RawMaterialLockRemark!).Distinct().OrderBy(x => x).ToList(),
                ["ProductionFlowProperty"] = new List<string> { ProductionFlowKeys.Paused, ProductionFlowKeys.Normal, ProductionFlowKeys.Waiting, ProductionFlowKeys.Doubt, ProductionFlowKeys.Skip },
                ["ProductionAttentionProcess"] = all
                    .Where(x => x.ProductionAttentionProcess != null)
                    .Select(x => x.ProductionAttentionProcess!)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList(),
                ["MainNoAttentionProcess"] = all
                    .Where(x => x.MainNoAttentionProcess != null)
                    .Select(x => x.MainNoAttentionProcess!)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList(),
                ["AdjustmentRemark"] = all.Where(x => x.AdjustmentRemark != null).Select(x => x.AdjustmentRemark!).Distinct().OrderBy(x => x).ToList(),
                ["ReworkInputConsistency"] = all.Where(x => x.ReworkInputConsistency != null).Select(x => x.ReworkInputConsistency!).Distinct().OrderBy(x => x).ToList(),

                // ===== 日期 =====
                ["SignDate"] = DistinctDates(all.Select(x => x.SignDate)),
                ["DeliveryDate"] = DistinctDates(all.Select(x => x.DeliveryDate)),
                ["TheoreticalCutoffDate"] = DistinctNullableDates(all.Select(x => x.TheoreticalCutoffDate)),
                ["CutoffArrivalDate"] = DistinctNullableDates(all.Select(x => x.CutoffArrivalDate)),
                ["MainNoCutoffArrivalDate"] = DistinctNullableDates(all.Select(x => x.MainNoCutoffArrivalDate)),
                ["InputStartDate"] = DistinctNullableDates(all.Select(x => x.InputStartDate)),
                ["InputEndDate"] = DistinctNullableDates(all.Select(x => x.InputEndDate)),
                ["ReworkInputEndDate"] = DistinctNullableDates(all.Select(x => x.ReworkInputEndDate)),
                ["WarehousingStartDate"] = DistinctNullableDates(all.Select(x => x.WarehousingStartDate)),
                ["WarehousingEndDate"] = DistinctNullableDates(all.Select(x => x.WarehousingEndDate)),
                ["EstimatedProcessCompletionDate"] = DistinctNullableDates(all.Select(x => x.EstimatedProcessCompletionDate)),

                // ===== 整数 =====
                ["TotalItemCount"] = DistinctInts(all.Select(x => x.TotalItemCount)),
                ["TotalQuantity"] = DistinctInts(all.Select(x => x.TotalQuantity)),
                ["MaterialPlanCoveredCount"] = DistinctInts(all.Select(x => x.MaterialPlanCoveredCount)),
                // 主号计划执行状态：不返回 DISTINCT，前端回退 IntStatusDisplayHelper.GetMainNoPlanExecutionStatusOptions() 中文下拉（4 档）
                ["TotalBatchCount"] = DistinctInts(all.Select(x => x.TotalBatchCount)),
                ["InputQuantity"] = DistinctInts(all.Select(x => x.InputQuantity)),
                ["ValidBatchCount"] = DistinctInts(all.Select(x => x.ValidBatchCount)),
                ["ValidInputQuantity"] = DistinctInts(all.Select(x => x.ValidInputQuantity)),
                ["FlowTotalBatchCount"] = DistinctInts(all.Select(x => x.FlowTotalBatchCount)),
                ["FlowIncompleteBatchCount"] = DistinctInts(all.Select(x => x.FlowIncompleteBatchCount)),
                ["FlowMaxRemainingWorkDays"] = DistinctInts(all.Select(x => x.FlowMaxRemainingWorkDays)),
                ["ReworkBatchCount"] = DistinctInts(all.Select(x => x.ReworkBatchCount)),
                ["ReworkInputQuantity"] = DistinctInts(all.Select(x => x.ReworkInputQuantity)),
                ["WarehousingTotalQty"] = DistinctInts(all.Select(x => x.WarehousingTotalQty)),
                ["TotalRemainingWorkDays"] = DistinctNullableInts(all.Select(x => x.TotalRemainingWorkDays)),
                ["CapacityWorkDays"] = DistinctNullableInts(all.Select(x => x.CapacityWorkDays)),
                ["DaysDiffFromDelivery"] = DistinctNullableInts(all.Select(x => x.DaysDiffFromDelivery)),
                ["MaxBatchRemainingWorkDays"] = DistinctNullableInts(all.Select(x => x.MaxBatchRemainingWorkDays)),
                ["ReworkTheoreticalProduceQty"] = DistinctNullableInts(all.Select(x => x.ReworkTheoreticalProduceQty)),
                ["ProcessInspectionDefectWeight"] = DistinctNullableInts(all.Select(x => x.ProcessInspectionDefectWeight)),
                ["ProcessInspectionReworkWeight"] = DistinctNullableInts(all.Select(x => x.ProcessInspectionReworkWeight)),
                ["ProcessInspectionWarehouseWeight"] = DistinctNullableInts(all.Select(x => x.ProcessInspectionWarehouseWeight)),
                ["ProcessInspectionScrapWeight"] = DistinctNullableInts(all.Select(x => x.ProcessInspectionScrapWeight)),
                ["FinalInspectionDefectQty"] = DistinctNullableInts(all.Select(x => x.FinalInspectionDefectQty)),
                ["FinalInspectionDefectWeight"] = DistinctNullableInts(all.Select(x => x.FinalInspectionDefectWeight)),
                ["FinalInspectionReworkWeight"] = DistinctNullableInts(all.Select(x => x.FinalInspectionReworkWeight)),
                ["FinalInspectionWarehouseWeight"] = DistinctNullableInts(all.Select(x => x.FinalInspectionWarehouseWeight)),
                ["FinalInspectionScrapWeight"] = DistinctNullableInts(all.Select(x => x.FinalInspectionScrapWeight)),

                // ===== 小数 =====
                ["MinLength"] = DistinctNullableDecimals(all.Select(x => x.MinLength)),
                ["MaxLength"] = DistinctNullableDecimals(all.Select(x => x.MaxLength)),
                ["TotalMeters"] = DistinctDecimals(all.Select(x => x.TotalMeters)),
                ["TotalWeight"] = DistinctDecimals(all.Select(x => x.TotalWeight)),
                ["MainNoMaterialPlanRate"] = DistinctDecimals(all.Select(x => x.MainNoMaterialPlanRate)),
                ["InputWeight"] = DistinctDecimals(all.Select(x => x.InputWeight)),
                ["TheoreticalOutputQty"] = DistinctDecimals(all.Select(x => x.TheoreticalOutputQty)),
                ["TheoreticalOutputWeight"] = DistinctDecimals(all.Select(x => x.TheoreticalOutputWeight)),
                ["InputOutputRatio"] = DistinctDecimals(all.Select(x => x.InputOutputRatio)),
                ["MainNoInputOutputRatio"] = DistinctDecimals(all.Select(x => x.MainNoInputOutputRatio)),
                ["ValidInputWeight"] = DistinctDecimals(all.Select(x => x.ValidInputWeight)),
                ["ValidOutputQty"] = DistinctDecimals(all.Select(x => x.ValidOutputQty)),
                ["ValidOutputWeight"] = DistinctDecimals(all.Select(x => x.ValidOutputWeight)),
                ["FlowOutputRatio"] = DistinctDecimals(all.Select(x => x.FlowOutputRatio)),
                ["MainNoFlowOutputRatio"] = DistinctDecimals(all.Select(x => x.MainNoFlowOutputRatio)),
                ["WarehousingTotalWeight"] = DistinctDecimals(all.Select(x => x.WarehousingTotalWeight)),
                ["ReworkTheoreticalProduceWeight"] = DistinctNullableDecimals(all.Select(x => x.ReworkTheoreticalProduceWeight)),
                ["PendingReworkOutputQty"] = DistinctNullableDecimals(all.Select(x => x.PendingReworkOutputQty)),
                ["PendingReworkOutputWeight"] = DistinctNullableDecimals(all.Select(x => x.PendingReworkOutputWeight)),
                ["ReworkInputWeight"] = DistinctDecimals(all.Select(x => x.ReworkInputWeight)),
                ["ReworkTheoreticalOutputQty"] = DistinctDecimals(all.Select(x => x.ReworkTheoreticalOutputQty)),
                ["ReworkTheoreticalOutputWeight"] = DistinctDecimals(all.Select(x => x.ReworkTheoreticalOutputWeight)),
                ["PiercingPlanWeight"] = DistinctDecimals(all.Select(x => x.PiercingPlanWeight)),
                ["PiercingSubOutWeight"] = DistinctDecimals(all.Select(x => x.PiercingSubOutWeight)),
                ["PiercingSubInWeight"] = DistinctDecimals(all.Select(x => x.PiercingSubInWeight)),
                ["PiercingSubPendingWeight"] = DistinctDecimals(all.Select(x => x.PiercingSubPendingWeight)),
                ["SemiPlanWeight"] = DistinctDecimals(all.Select(x => x.SemiPlanWeight)),
                ["SemiOrderWeight"] = DistinctDecimals(all.Select(x => x.SemiOrderWeight)),
                ["SemiInWeight"] = DistinctDecimals(all.Select(x => x.SemiInWeight)),
                ["SemiPendingWeight"] = DistinctDecimals(all.Select(x => x.SemiPendingWeight)),
                ["FinishPlanWeight"] = DistinctDecimals(all.Select(x => x.FinishPlanWeight)),
                ["FinishOrderWeight"] = DistinctDecimals(all.Select(x => x.FinishOrderWeight)),
                ["FinishInWeight"] = DistinctDecimals(all.Select(x => x.FinishInWeight)),
                ["FinishPendingWeight"] = DistinctDecimals(all.Select(x => x.FinishPendingWeight)),
                ["InventoryPlanWeight"] = DistinctDecimals(all.Select(x => x.InventoryPlanWeight)),
                ["InventoryOutWeight"] = DistinctDecimals(all.Select(x => x.InventoryOutWeight)),
                ["ReworkPlanWeight"] = DistinctDecimals(all.Select(x => x.ReworkPlanWeight)),
                ["ReworkPlanInputWeight"] = DistinctDecimals(all.Select(x => x.ReworkPlanInputWeight)),
                ["InProcessReworkPlanWeight"] = DistinctDecimals(all.Select(x => x.InProcessReworkPlanWeight)),
                ["InProcessReworkInputWeight"] = DistinctDecimals(all.Select(x => x.InProcessReworkInputWeight)),
                ["InMainPlanWeight"] = DistinctDecimals(all.Select(x => x.InMainPlanWeight)),
                ["InMainInputWeight"] = DistinctDecimals(all.Select(x => x.InMainInputWeight)),
                ["PendingSectionRoughTube"] = DistinctNullableDecimals(all.Select(x => x.PendingSectionRoughTube)),
                ["PendingSectionWarehouseFix"] = DistinctNullableDecimals(all.Select(x => x.PendingSectionWarehouseFix)),
                ["PendingSection60Roll"] = DistinctNullableDecimals(all.Select(x => x.PendingSection60Roll)),
                ["PendingSection50Roll"] = DistinctNullableDecimals(all.Select(x => x.PendingSection50Roll)),
                ["PendingSection30Roll"] = DistinctNullableDecimals(all.Select(x => x.PendingSection30Roll)),
                ["PendingSection20Roll"] = DistinctNullableDecimals(all.Select(x => x.PendingSection20Roll)),
                ["PendingSectionThreeRoll"] = DistinctNullableDecimals(all.Select(x => x.PendingSectionThreeRoll)),
                ["PendingSectionDrawBench"] = DistinctNullableDecimals(all.Select(x => x.PendingSectionDrawBench)),

                // ===== G3 计算列（DTO 计算属性，实体无列，前端按表达式值筛选） =====
                ["TotalPlanWeight"] = DistinctDecimals(all.Select(x =>
                    x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                    + x.InventoryPlanWeight + x.ReworkPlanWeight + x.InProcessReworkPlanWeight + x.InMainPlanWeight)),
                ["TotalAvailableWeight"] = DistinctDecimals(all.Select(x =>
                    x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                    + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight)),
                ["TotalMissingWeight"] = DistinctDecimals(all.Select(x => Math.Max(0m,
                    (x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                        + x.InventoryPlanWeight + x.ReworkPlanWeight + x.InProcessReworkPlanWeight + x.InMainPlanWeight)
                    - (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                        + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight)))),
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    // ====== G3 计算列排序表达式（SQL 可翻译，供 ApplySorting 复用） ======

    /// <summary>计划投料总重量 = G4~G10 七个计划量之和</summary>
    private static readonly System.Linq.Expressions.Expression<Func<WorkOrderExecutionSummary, decimal>> G3TotalPlanWeightExpr =
        x => x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
            + x.InventoryPlanWeight + x.ReworkPlanWeight
            + x.InProcessReworkPlanWeight + x.InMainPlanWeight;

    /// <summary>现可投料总重量 = G4委外到货 + G5采购到货 + G6采购到货 + G7出库量 + G8投料量 + G9投料量 + G10投料量（到货量口径：下单≠到货，未收货的量不视为"现可投料"）</summary>
    private static readonly System.Linq.Expressions.Expression<Func<WorkOrderExecutionSummary, decimal>> G3TotalAvailableWeightExpr =
        x => x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
            + x.InventoryOutWeight + x.ReworkPlanInputWeight
            + x.InProcessReworkInputWeight + x.InMainInputWeight;

    /// <summary>理论缺失总料重量 = Max(0, 计划投料总重 − 现可投料总重)。
    /// ⚠️ 不能用 Math.Max —— EF Core 无法翻译 System.Math.Max（SQL 排序/筛选会 500），必须写成 SQL 可翻译的内联三元。</summary>
    private static readonly System.Linq.Expressions.Expression<Func<WorkOrderExecutionSummary, decimal>> G3TotalMissingWeightExpr =
        x => (x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                + x.InventoryPlanWeight + x.ReworkPlanWeight + x.InProcessReworkPlanWeight + x.InMainPlanWeight)
                - (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight)
            > 0m
            ? (x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                + x.InventoryPlanWeight + x.ReworkPlanWeight + x.InProcessReworkPlanWeight + x.InMainPlanWeight)
                - (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight)
            : 0m;

    /// <summary>
    /// 到料实投一致性：0=一致 1=待投 2=疑问-到料少投 3=疑问-到料超投 4=错误-无料已投 5=错误-无需投料 6=略（与 DTO 计算属性一致）
    /// 阶段门控（最外层）：主号关注=生产执行(3)/成品检验(4)/主号完成(1) 已过投料期 → 理论缺失总料重(计划-现可) &gt; 计划投料总重×3% → 5 错误-无需投料（缺口率&gt;3% 计划严重未落实需修正）；其余（含缺口≤3% 容差内）→ 6 略
    /// 否则走原有五态（实际已投料量 vs 现可投料总重，现可=到货量口径）：
    /// 错误(4)：已投&gt;0 且 现可=0（无到料却投料）；疑问-到料超投(3)：已投&gt;现可×1.03；
    /// 投料滞后（已投&lt;现可×0.97）按下料到位时点细分：截止到料日=今天→1（操作时间差）；早于今天→2（需投未投）；
    /// 晚于今天或空→0（料未到位，投料滞后正常）；一致(0)：已投≈现可（±3% 内）或双零
    /// </summary>
    private static readonly System.Linq.Expressions.Expression<Func<WorkOrderExecutionSummary, int>> G3PlanInputConsistencyExpr =
        x => (x.ScheduleStage == 1 || x.ScheduleStage == 3 || x.ScheduleStage == 4)
            ? ((x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                    + x.InventoryPlanWeight + x.ReworkPlanWeight + x.InProcessReworkPlanWeight + x.InMainPlanWeight)
                - (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                    + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight)
                > (x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                    + x.InventoryPlanWeight + x.ReworkPlanWeight + x.InProcessReworkPlanWeight + x.InMainPlanWeight) * 0.03m
                ? 5 : 6)
            : x.InputWeight > 0 && (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                    + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) <= 0
                ? 4
                : (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                    + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) <= 0
                    ? 0
                    : x.InputWeight > (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                        + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) * 1.03m
                        ? 3
                        : x.InputWeight < (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                            + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) * 0.97m
                            ? (x.CutoffArrivalDate == null
                                ? 0
                                : x.CutoffArrivalDate.Value.Date < DateTime.Today
                                    ? 2
                                    : x.CutoffArrivalDate.Value.Date == DateTime.Today
                                        ? 1
                                        : 0)
                            : 0;

    /// <summary>
    /// G3 计算列筛选：计划投料总重/现可投料总重/理论缺失总料重（decimal）与到料实投一致性（int 5 档 0一致/1待投/2疑问-到料少投/3疑问-到料超投/4错误-无料已投）。
    /// 这些是 DTO 计算属性，实体无对应列，通用反射筛选（ApplyFilters）覆盖不到，故内联表达式 WHERE（EF 可翻译为 CASE/IN）。
    /// 仅支持 "in" 操作符（与前端 ExcelFilter 一致）。
    /// </summary>
    internal static IQueryable<WorkOrderExecutionSummary> ApplyComputedFilters(
        IQueryable<WorkOrderExecutionSummary> query, List<FilterDescriptor>? filters)
    {
        if (filters == null || filters.Count == 0) return query;

        foreach (var f in filters)
        {
            if (string.IsNullOrWhiteSpace(f.Field) || f.Values == null || f.Values.Count == 0)
                continue;

            switch (f.Field.ToLowerInvariant())
            {
                case "totalplanweight":
                {
                    var vals = ParseDecimalValues(f.Values);
                    if (vals.Count == 0) break;
                    query = query.Where(x => vals.Contains(
                        x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                        + x.InventoryPlanWeight + x.ReworkPlanWeight
                        + x.InProcessReworkPlanWeight + x.InMainPlanWeight));
                    break;
                }
                case "totalavailableweight":
                {
                    var vals = ParseDecimalValues(f.Values);
                    if (vals.Count == 0) break;
                    query = query.Where(x => vals.Contains(
                        x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                        + x.InventoryOutWeight + x.ReworkPlanInputWeight
                        + x.InProcessReworkInputWeight + x.InMainInputWeight));
                    break;
                }
                case "totalmissingweight":
                {
                    var vals = ParseDecimalValues(f.Values);
                    if (vals.Count == 0) break;
                    // ⚠️ 不能用 Math.Max —— EF Core 无法翻译，SQL 筛选会 500，必须写成内联三元
                    query = query.Where(x => vals.Contains(
                        (x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                            + x.InventoryPlanWeight + x.ReworkPlanWeight
                            + x.InProcessReworkPlanWeight + x.InMainPlanWeight)
                            - (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                                + x.InventoryOutWeight + x.ReworkPlanInputWeight
                                + x.InProcessReworkInputWeight + x.InMainInputWeight)
                            > 0m
                            ? (x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                                + x.InventoryPlanWeight + x.ReworkPlanWeight
                                + x.InProcessReworkPlanWeight + x.InMainPlanWeight)
                                - (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                                    + x.InventoryOutWeight + x.ReworkPlanInputWeight
                                    + x.InProcessReworkInputWeight + x.InMainInputWeight)
                            : 0m));
                    break;
                }
                case "planinputconsistency":
                {
                    var vals = ParseIntValues(f.Values);
                    if (vals.Count == 0) break;
                    query = query.Where(x => vals.Contains(
                        (x.ScheduleStage == 1 || x.ScheduleStage == 3 || x.ScheduleStage == 4)
                            ? ((x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                                    + x.InventoryPlanWeight + x.ReworkPlanWeight + x.InProcessReworkPlanWeight + x.InMainPlanWeight)
                                - (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                                    + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight)
                                > (x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                                    + x.InventoryPlanWeight + x.ReworkPlanWeight + x.InProcessReworkPlanWeight + x.InMainPlanWeight) * 0.03m
                                ? 5 : 6)
                            : x.InputWeight > 0 && (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                                    + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) <= 0
                                ? 4
                                : (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                                    + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) <= 0
                                    ? 0
                                    : x.InputWeight > (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                                        + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) * 1.03m
                                        ? 3
                                        : x.InputWeight < (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                                            + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) * 0.97m
                                            ? (x.CutoffArrivalDate == null
                                                ? 0
                                                : x.CutoffArrivalDate.Value.Date < DateTime.Today
                                                    ? 2
                                                    : x.CutoffArrivalDate.Value.Date == DateTime.Today
                                                        ? 1
                                                        : 0)
                                            : 0));
                    break;
                }
            }
        }

        return query;
    }

    private static List<decimal> ParseDecimalValues(List<string> values)
    {
        var list = new List<decimal>();
        foreach (var v in values)
            if (decimal.TryParse(v, out var d)) list.Add(d);
        return list;
    }

    private static List<int> ParseIntValues(List<string> values)
    {
        var list = new List<int>();
        foreach (var v in values)
            if (int.TryParse(v, out var n)) list.Add(n);
        return list;
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
            ("endcustomer", false) => query.OrderBy(x => x.EndCustomer),
            ("endcustomer", true) => query.OrderByDescending(x => x.EndCustomer),
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
            ("mainnomaterialplanrate", false) => query.OrderBy(x => x.MainNoMaterialPlanRate),
            ("mainnomaterialplanrate", true) => query.OrderByDescending(x => x.MainNoMaterialPlanRate),
            ("materialplanstatus", false) => query.OrderBy(x => x.MaterialPlanStatus),
            ("materialplanstatus", true) => query.OrderByDescending(x => x.MaterialPlanStatus),
            ("mainnomaterialplanstatus", false) => query.OrderBy(x => x.MainNoMaterialPlanStatus),
            ("mainnomaterialplanstatus", true) => query.OrderByDescending(x => x.MainNoMaterialPlanStatus),
            ("mainnoplanexecutionstatus", false) => query.OrderBy(x => x.MainNoPlanExecutionStatus),
            ("mainnoplanexecutionstatus", true) => query.OrderByDescending(x => x.MainNoPlanExecutionStatus),
            ("processcycle", false) => query.OrderBy(x => x.ProcessCycle),
            ("processcycle", true) => query.OrderByDescending(x => x.ProcessCycle),
            ("materialplancoveredcount", false) => query.OrderBy(x => x.MaterialPlanCoveredCount),
            ("materialplancoveredcount", true) => query.OrderByDescending(x => x.MaterialPlanCoveredCount),
            ("materialplanproportion", false) => query.OrderBy(x => x.MaterialPlanProportion ?? ""),
            ("materialplanproportion", true) => query.OrderByDescending(x => x.MaterialPlanProportion ?? ""),
            ("theoreticalcutoffdate", false) => query.OrderBy(x => x.TheoreticalCutoffDate),
            ("theoreticalcutoffdate", true) => query.OrderByDescending(x => x.TheoreticalCutoffDate),
            ("cutoffarrivaldate", false) => query.OrderBy(x => x.CutoffArrivalDate),
            ("cutoffarrivaldate", true) => query.OrderByDescending(x => x.CutoffArrivalDate),
            ("mainnocutoffarrivaldate", false) => query.OrderBy(x => x.MainNoCutoffArrivalDate),
            ("mainnocutoffarrivaldate", true) => query.OrderByDescending(x => x.MainNoCutoffArrivalDate),

            // G14
            ("reworktheoreticalproduceqty", false) => query.OrderBy(x => x.ReworkTheoreticalProduceQty),
            ("reworktheoreticalproduceqty", true) => query.OrderByDescending(x => x.ReworkTheoreticalProduceQty),
            ("reworktheoreticalproduceweight", false) => query.OrderBy(x => x.ReworkTheoreticalProduceWeight),
            ("reworktheoreticalproduceweight", true) => query.OrderByDescending(x => x.ReworkTheoreticalProduceWeight),
            ("pendingreworkoutputqty", false) => query.OrderBy(x => x.PendingReworkOutputQty),
            ("pendingreworkoutputqty", true) => query.OrderByDescending(x => x.PendingReworkOutputQty),
            ("pendingreworkoutputweight", false) => query.OrderBy(x => x.PendingReworkOutputWeight),
            ("pendingreworkoutputweight", true) => query.OrderByDescending(x => x.PendingReworkOutputWeight),
            ("reworkmainnostatus", false) => query.OrderBy(x => x.ReworkMainNoStatus),
            ("reworkmainnostatus", true) => query.OrderByDescending(x => x.ReworkMainNoStatus),
            ("reworkinputconsistency", false) => query.OrderBy(x => x.ReworkInputConsistency),
            ("reworkinputconsistency", true) => query.OrderByDescending(x => x.ReworkInputConsistency),
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

            // G15 次品总量
            ("processinspectiondefectweight", false) => query.OrderBy(x => x.ProcessInspectionDefectWeight),
            ("processinspectiondefectweight", true) => query.OrderByDescending(x => x.ProcessInspectionDefectWeight),
            ("processinspectionreworkweight", false) => query.OrderBy(x => x.ProcessInspectionReworkWeight),
            ("processinspectionreworkweight", true) => query.OrderByDescending(x => x.ProcessInspectionReworkWeight),
            ("processinspectionwarehouseweight", false) => query.OrderBy(x => x.ProcessInspectionWarehouseWeight),
            ("processinspectionwarehouseweight", true) => query.OrderByDescending(x => x.ProcessInspectionWarehouseWeight),
            ("processinspectionscrapweight", false) => query.OrderBy(x => x.ProcessInspectionScrapWeight),
            ("processinspectionscrapweight", true) => query.OrderByDescending(x => x.ProcessInspectionScrapWeight),
            ("finalinspectiondefectqty", false) => query.OrderBy(x => x.FinalInspectionDefectQty),
            ("finalinspectiondefectqty", true) => query.OrderByDescending(x => x.FinalInspectionDefectQty),
            ("finalinspectiondefectweight", false) => query.OrderBy(x => x.FinalInspectionDefectWeight),
            ("finalinspectiondefectweight", true) => query.OrderByDescending(x => x.FinalInspectionDefectWeight),
            ("finalinspectionreworkweight", false) => query.OrderBy(x => x.FinalInspectionReworkWeight),
            ("finalinspectionreworkweight", true) => query.OrderByDescending(x => x.FinalInspectionReworkWeight),
            ("finalinspectionwarehouseweight", false) => query.OrderBy(x => x.FinalInspectionWarehouseWeight),
            ("finalinspectionwarehouseweight", true) => query.OrderByDescending(x => x.FinalInspectionWarehouseWeight),
            ("finalinspectionscrapweight", false) => query.OrderBy(x => x.FinalInspectionScrapWeight),
            ("finalinspectionscrapweight", true) => query.OrderByDescending(x => x.FinalInspectionScrapWeight),

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
            // G12
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

            // G15
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

            // G16
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

            // G17
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

            // Group 2
            ("isurging", false) => query.OrderBy(x => x.IsUrging),
            ("isurging", true) => query.OrderByDescending(x => x.IsUrging),
            ("isbatchdelivery", false) => query.OrderBy(x => x.IsBatchDelivery),
            ("isbatchdelivery", true) => query.OrderByDescending(x => x.IsBatchDelivery),
            ("ispaused", false) => query.OrderBy(x => x.IsPaused),
            ("ispaused", true) => query.OrderByDescending(x => x.IsPaused),
            ("isforcecompleted", false) => query.OrderBy(x => x.IsForceCompleted),
            ("isforcecompleted", true) => query.OrderByDescending(x => x.IsForceCompleted),
            ("adjustmentremark", false) => query.OrderBy(x => x.AdjustmentRemark ?? ""),
            ("adjustmentremark", true) => query.OrderByDescending(x => x.AdjustmentRemark ?? ""),
            ("productionflowproperty", false) => query.OrderBy(x => x.ProductionFlowProperty ?? ""),
            ("productionflowproperty", true) => query.OrderByDescending(x => x.ProductionFlowProperty ?? ""),

            // G18 在产节点待量
            ("maxbatchremainingworkdays", false) => query.OrderBy(x => x.MaxBatchRemainingWorkDays),
            ("maxbatchremainingworkdays", true) => query.OrderByDescending(x => x.MaxBatchRemainingWorkDays),
            ("mainnoattentionprocess", false) => query.OrderBy(x => x.MainNoAttentionProcess ?? ""),
            ("mainnoattentionprocess", true) => query.OrderByDescending(x => x.MainNoAttentionProcess ?? ""),

            // G3 计算列（DTO 计算属性，实体无对应列 → SQL 可翻译表达式排序）
            ("totalplanweight", false) => query.OrderBy(G3TotalPlanWeightExpr),
            ("totalplanweight", true) => query.OrderByDescending(G3TotalPlanWeightExpr),
            ("totalavailableweight", false) => query.OrderBy(G3TotalAvailableWeightExpr),
            ("totalavailableweight", true) => query.OrderByDescending(G3TotalAvailableWeightExpr),
            ("totalmissingweight", false) => query.OrderBy(G3TotalMissingWeightExpr),
            ("totalmissingweight", true) => query.OrderByDescending(G3TotalMissingWeightExpr),
            ("actualinputweight", false) => query.OrderBy(x => x.InputWeight),
            ("actualinputweight", true) => query.OrderByDescending(x => x.InputWeight),
            ("actualmainnoinputstatus", false) => query.OrderBy(x => x.MainNoInputStatus),
            ("actualmainnoinputstatus", true) => query.OrderByDescending(x => x.MainNoInputStatus),
            ("planinputconsistency", false) => query.OrderBy(G3PlanInputConsistencyExpr),
            ("planinputconsistency", true) => query.OrderByDescending(G3PlanInputConsistencyExpr),

            // G5 圆棒穿孔
            ("piercingplanweight", false) => query.OrderBy(x => x.PiercingPlanWeight),
            ("piercingplanweight", true) => query.OrderByDescending(x => x.PiercingPlanWeight),
            ("piercingsuboutweight", false) => query.OrderBy(x => x.PiercingSubOutWeight),
            ("piercingsuboutweight", true) => query.OrderByDescending(x => x.PiercingSubOutWeight),
            ("piercingsubstatus", false) => query.OrderBy(x => x.PiercingSubStatus),
            ("piercingsubstatus", true) => query.OrderByDescending(x => x.PiercingSubStatus),
            ("piercingsubinweight", false) => query.OrderBy(x => x.PiercingSubInWeight),
            ("piercingsubinweight", true) => query.OrderByDescending(x => x.PiercingSubInWeight),
            ("piercingsubpendingweight", false) => query.OrderBy(x => x.PiercingSubPendingWeight),
            ("piercingsubpendingweight", true) => query.OrderByDescending(x => x.PiercingSubPendingWeight),
            ("piercingreturnstatus", false) => query.OrderBy(x => x.PiercingReturnStatus),
            ("piercingreturnstatus", true) => query.OrderByDescending(x => x.PiercingReturnStatus),

            // G6 荒管采购
            ("semiplanweight", false) => query.OrderBy(x => x.SemiPlanWeight),
            ("semiplanweight", true) => query.OrderByDescending(x => x.SemiPlanWeight),
            ("semiorderweight", false) => query.OrderBy(x => x.SemiOrderWeight),
            ("semiorderweight", true) => query.OrderByDescending(x => x.SemiOrderWeight),
            ("semiorderstatus", false) => query.OrderBy(x => x.SemiOrderStatus),
            ("semiorderstatus", true) => query.OrderByDescending(x => x.SemiOrderStatus),
            ("semiinweight", false) => query.OrderBy(x => x.SemiInWeight),
            ("semiinweight", true) => query.OrderByDescending(x => x.SemiInWeight),
            ("semipendingweight", false) => query.OrderBy(x => x.SemiPendingWeight),
            ("semipendingweight", true) => query.OrderByDescending(x => x.SemiPendingWeight),
            ("semiinstatus", false) => query.OrderBy(x => x.SemiInStatus),
            ("semiinstatus", true) => query.OrderByDescending(x => x.SemiInStatus),

            // G7 成品采购
            ("finishplanweight", false) => query.OrderBy(x => x.FinishPlanWeight),
            ("finishplanweight", true) => query.OrderByDescending(x => x.FinishPlanWeight),
            ("finishorderweight", false) => query.OrderBy(x => x.FinishOrderWeight),
            ("finishorderweight", true) => query.OrderByDescending(x => x.FinishOrderWeight),
            ("finishorderstatus", false) => query.OrderBy(x => x.FinishOrderStatus),
            ("finishorderstatus", true) => query.OrderByDescending(x => x.FinishOrderStatus),
            ("finishinweight", false) => query.OrderBy(x => x.FinishInWeight),
            ("finishinweight", true) => query.OrderByDescending(x => x.FinishInWeight),
            ("finishpendingweight", false) => query.OrderBy(x => x.FinishPendingWeight),
            ("finishpendingweight", true) => query.OrderByDescending(x => x.FinishPendingWeight),
            ("finishinstatus", false) => query.OrderBy(x => x.FinishInStatus),
            ("finishinstatus", true) => query.OrderByDescending(x => x.FinishInStatus),

            // G8 库存使用
            ("inventoryplanweight", false) => query.OrderBy(x => x.InventoryPlanWeight),
            ("inventoryplanweight", true) => query.OrderByDescending(x => x.InventoryPlanWeight),
            ("inventoryoutweight", false) => query.OrderBy(x => x.InventoryOutWeight),
            ("inventoryoutweight", true) => query.OrderByDescending(x => x.InventoryOutWeight),
            ("inventoryoutstatus", false) => query.OrderBy(x => x.InventoryOutStatus),
            ("inventoryoutstatus", true) => query.OrderByDescending(x => x.InventoryOutStatus),

            // G9 库料改制
            ("reworkplanweight", false) => query.OrderBy(x => x.ReworkPlanWeight),
            ("reworkplanweight", true) => query.OrderByDescending(x => x.ReworkPlanWeight),
            ("reworkplaninputweight", false) => query.OrderBy(x => x.ReworkPlanInputWeight),
            ("reworkplaninputweight", true) => query.OrderByDescending(x => x.ReworkPlanInputWeight),
            ("reworkplaninputstatus", false) => query.OrderBy(x => x.ReworkPlanInputStatus),
            ("reworkplaninputstatus", true) => query.OrderByDescending(x => x.ReworkPlanInputStatus),

            // G10 在产改制
            ("inprocessreworkplanweight", false) => query.OrderBy(x => x.InProcessReworkPlanWeight),
            ("inprocessreworkplanweight", true) => query.OrderByDescending(x => x.InProcessReworkPlanWeight),
            ("inprocessreworkinputweight", false) => query.OrderBy(x => x.InProcessReworkInputWeight),
            ("inprocessreworkinputweight", true) => query.OrderByDescending(x => x.InProcessReworkInputWeight),
            ("inprocessreworkinputstatus", false) => query.OrderBy(x => x.InProcessReworkInputStatus),
            ("inprocessreworkinputstatus", true) => query.OrderByDescending(x => x.InProcessReworkInputStatus),

            // G11 在产主工单
            ("inmainplanweight", false) => query.OrderBy(x => x.InMainPlanWeight),
            ("inmainplanweight", true) => query.OrderByDescending(x => x.InMainPlanWeight),
            ("inmaininputweight", false) => query.OrderBy(x => x.InMainInputWeight),
            ("inmaininputweight", true) => query.OrderByDescending(x => x.InMainInputWeight),
            ("inmaininputstatus", false) => query.OrderBy(x => x.InMainInputStatus),
            ("inmaininputstatus", true) => query.OrderByDescending(x => x.InMainInputStatus),

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
    private static int? GetSectionSequence(ProcessGroup pg, string? sectionName)
    {
        var key = SectionKeys.ToKey(sectionName);
        if (key == null) return null;
        return key switch
        {
            SectionKeys.ColdRollDraw => pg.ColdRollDraw,
            SectionKeys.OilPipeCut => pg.OilPipeCut,
            SectionKeys.Degrease => pg.Degrease,
            SectionKeys.EmulsionWash => pg.EmulsionWash,
            SectionKeys.UltrasonicWash => pg.UltrasonicWash,
            SectionKeys.ClothPolish => pg.ClothPolish,
            SectionKeys.BrightAnnealing => pg.BrightAnnealing,
            SectionKeys.Solution => pg.Solution,
            SectionKeys.Straighten => pg.Straighten,
            SectionKeys.Cut => pg.Cut,
            SectionKeys.ThicknessMeasure => pg.ThicknessMeasure,
            SectionKeys.Pickle => pg.Pickle,
            SectionKeys.OuterPolish => pg.OuterPolish,
            SectionKeys.InnerPolish => pg.InnerPolish,
            SectionKeys.InnerGrinding => pg.InnerGrinding,
            SectionKeys.OuterSpotGrinding => pg.OuterSpotGrinding,
            SectionKeys.SandBlasting => pg.SandBlasting,
            SectionKeys.ShotBlasting => pg.ShotBlasting,
            SectionKeys.Inspection => pg.Inspection,
            SectionKeys.WeldingHead => pg.WeldingHead,
            SectionKeys.Welding => pg.Welding,
            SectionKeys.Lubrication => pg.Lubrication,
            SectionKeys.Packing => pg.Packing,
            SectionKeys.Warehouse => pg.Warehouse,
            SectionKeys.Extra1 => pg.Extra1,
            SectionKeys.Extra2 => pg.Extra2,
            _ => null
        };
    }

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
                (x.EndCustomer != null && x.EndCustomer.Contains(kw)) ||
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
            MaterialPlanStatus = (MaterialPlanStatus)e.MaterialPlanStatus,
            MainNoMaterialPlanRate = e.MainNoMaterialPlanRate,
            MainNoMaterialPlanStatus = (MaterialPlanStatus)e.MainNoMaterialPlanStatus,
            MainNoPlanExecutionStatus = e.MainNoPlanExecutionStatus,
            MaterialPlanCoveredCount = e.MaterialPlanCoveredCount,
            MaterialPlanProportion = e.MaterialPlanProportion,
            TheoreticalCutoffDate = e.TheoreticalCutoffDate,
            CutoffArrivalDate = e.CutoffArrivalDate,
            MainNoCutoffArrivalDate = e.MainNoCutoffArrivalDate,

            // G4~G10: 7 种用料计划执行状况
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
            IsForceCompleted = e.IsForceCompleted,
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

    private static object ResolvePrintValue(WorkOrderExecutionSummaryDto item, string key) => (key switch
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
        "IsForceCompleted" => item.IsForceCompleted ? "是" : "否",
        "DeformedProcessCompleted" => item.DeformedProcessCompleted switch { true => "是", false => "否", null => "略" },
        // 状态 int→中文
        "MaterialPlanStatus" => item.MaterialPlanStatusText,
        "MainNoMaterialPlanStatus" => item.MainNoMaterialPlanStatusText,
        "MainNoPlanExecutionStatus" => item.MainNoPlanExecutionStatusText,
        "InputStatus" => item.InputStatusText,
        "MainNoInputStatus" => item.MainNoInputStatusText,
        "FlowStatus" => item.FlowStatusText,
        "MainNoFlowStatus" => item.MainNoFlowStatusText,
        "WoWarehousingStatus" => item.WoWarehousingStatusText,
        "MainNoWarehousingStatus" => item.MainNoWarehousingStatusText,
        "OrderWarehousingStatus" => item.OrderWarehousingStatusText,
        // G4~G10 状态文本
        "PiercingSubStatus" => item.PiercingSubStatusText,
        "PiercingReturnStatus" => item.PiercingReturnStatusText,
        "SemiOrderStatus" => item.SemiOrderStatusText,
        "SemiInStatus" => item.SemiInStatusText,
        "FinishOrderStatus" => item.FinishOrderStatusText,
        "FinishInStatus" => item.FinishInStatusText,
        "InventoryOutStatus" => item.InventoryOutStatusText,
        "ReworkPlanInputStatus" => item.ReworkPlanInputStatusText,
        "InProcessReworkInputStatus" => item.InProcessReworkInputStatusText,
        "InMainInputStatus" => item.InMainInputStatusText,
        "ScheduleStage" => item.ScheduleStageText,
        // G3 汇总字段
        "TotalPlanWeight" => ((int)item.TotalPlanWeight).ToString(),
        "TotalAvailableWeight" => ((int)item.TotalAvailableWeight).ToString(),
        "TotalMissingWeight" => ((int)item.TotalMissingWeight).ToString(),
        "ActualMainNoInputStatus" => item.ActualMainNoInputStatusText,
        "PlanInputConsistency" => item.PlanInputConsistencyText,
        "ReworkInputConsistency" => item.ReworkInputConsistencyText,
        // 日期格式
        "SignDate" => item.SignDate.ToString("yyyy-MM-dd"),
        "DeliveryDate" => item.DeliveryDate.ToString("yyyy-MM-dd"),
        "TheoreticalCutoffDate" => item.TheoreticalCutoffDate?.ToString("yyyy-MM-dd") ?? "",
        "CutoffArrivalDate" => item.CutoffArrivalDate?.ToString("yyyy-MM-dd") ?? "",
        "MainNoCutoffArrivalDate" => item.MainNoCutoffArrivalDate?.ToString("yyyy-MM-dd") ?? "",
        "InputStartDate" => item.InputStartDate?.ToString("yyyy-MM-dd") ?? "",
        "InputEndDate" => item.InputEndDate?.ToString("yyyy-MM-dd") ?? "",
        "ReworkInputEndDate" => item.ReworkInputEndDate?.ToString("yyyy-MM-dd") ?? "",
        "WarehousingStartDate" => item.WarehousingStartDate?.ToString("yyyy-MM-dd") ?? "",
        "WarehousingEndDate" => item.WarehousingEndDate?.ToString("yyyy-MM-dd") ?? "",
        "EstimatedProcessCompletionDate" => item.EstimatedProcessCompletionDate?.ToString("yyyy-MM-dd") ?? "",
        // 比率→百分比格式
        "MainNoMaterialPlanRate" => item.MainNoMaterialPlanRate.ToString("F1") + "%",
        "FlowOutputRatio" => item.FlowOutputRatio.ToString("F1") + "%",
        "MainNoFlowOutputRatio" => item.MainNoFlowOutputRatio.ToString("F1") + "%",
        "InputOutputRatio" => item.InputOutputRatio.ToString("F1") + "%",
        "MainNoInputRatio" => item.MainNoInputOutputRatio.ToString("F1") + "%",
        // 通用字符串/数值
        _ => GetRawPrintValue(item, key)
    }) ?? "";

    private static object GetRawPrintValue(WorkOrderExecutionSummaryDto item, string key) => (key switch
    {
        "WorkOrderNo" => item.WorkOrderNo ?? "",
        "Salesman" => item.Salesman ?? "",
        "CustomerName" => item.CustomerName ?? "",
        "EndCustomer" => item.EndCustomer ?? "",
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
        "MaterialPlanCoveredCount" => item.MaterialPlanCoveredCount,
        "MaterialPlanProportion" => item.MaterialPlanProportion ?? "",
        "TotalPlanWeight" => item.TotalPlanWeight,
        "TotalAvailableWeight" => item.TotalAvailableWeight,
        "TotalMissingWeight" => item.TotalMissingWeight,
        "ActualInputWeight" => item.ActualInputWeight,
        "MainNoPlanExecutionStatus" => item.MainNoPlanExecutionStatus,
        "ActualMainNoInputStatus" => item.ActualMainNoInputStatus,
        "PlanInputConsistency" => item.PlanInputConsistency,

        // G4~G10 用料计划执行状况
        "PiercingPlanWeight" => item.PiercingPlanWeight,
        "PiercingSubOutWeight" => item.PiercingSubOutWeight,
        "PiercingSubInWeight" => item.PiercingSubInWeight,
        "PiercingSubPendingWeight" => item.PiercingSubPendingWeight,
        "SemiPlanWeight" => item.SemiPlanWeight,
        "SemiOrderWeight" => item.SemiOrderWeight,
        "SemiInWeight" => item.SemiInWeight,
        "SemiPendingWeight" => item.SemiPendingWeight,
        "FinishPlanWeight" => item.FinishPlanWeight,
        "FinishOrderWeight" => item.FinishOrderWeight,
        "FinishInWeight" => item.FinishInWeight,
        "FinishPendingWeight" => item.FinishPendingWeight,
        "InventoryPlanWeight" => item.InventoryPlanWeight,
        "InventoryOutWeight" => item.InventoryOutWeight,
        "ReworkPlanWeight" => item.ReworkPlanWeight,
        "ReworkPlanInputWeight" => item.ReworkPlanInputWeight,
        "InProcessReworkPlanWeight" => item.InProcessReworkPlanWeight,
        "InProcessReworkInputWeight" => item.InProcessReworkInputWeight,
        "InMainPlanWeight" => item.InMainPlanWeight,
        "InMainInputWeight" => item.InMainInputWeight,

        "ReworkTheoreticalProduceQty" => item.ReworkTheoreticalProduceQty,
        "ReworkTheoreticalProduceWeight" => item.ReworkTheoreticalProduceWeight,
        "PendingReworkOutputQty" => item.PendingReworkOutputQty,
        "PendingReworkOutputWeight" => item.PendingReworkOutputWeight,
        "ReworkMainNoStatus" => item.ReworkMainNoStatusText,
        "ReworkInputConsistency" => item.ReworkInputConsistency ?? "",
        "ReworkBatchCount" => item.ReworkBatchCount,
        "ReworkInputQuantity" => item.ReworkInputQuantity,
        "ReworkInputWeight" => item.ReworkInputWeight,
        "ReworkTheoreticalOutputQty" => item.ReworkTheoreticalOutputQty,
        "ReworkTheoreticalOutputWeight" => item.ReworkTheoreticalOutputWeight,
        "ProcessInspectionDefectWeight" => item.ProcessInspectionDefectWeight,
        "ProcessInspectionReworkWeight" => item.ProcessInspectionReworkWeight,
        "ProcessInspectionWarehouseWeight" => item.ProcessInspectionWarehouseWeight,
        "ProcessInspectionScrapWeight" => item.ProcessInspectionScrapWeight,
        "FinalInspectionDefectQty" => item.FinalInspectionDefectQty,
        "FinalInspectionDefectWeight" => item.FinalInspectionDefectWeight,
        "FinalInspectionReworkWeight" => item.FinalInspectionReworkWeight,
        "FinalInspectionWarehouseWeight" => item.FinalInspectionWarehouseWeight,
        "FinalInspectionScrapWeight" => item.FinalInspectionScrapWeight,
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
        "WarehousingTotalQty" => item.WarehousingTotalQty,
        "WarehousingTotalWeight" => item.WarehousingTotalWeight,
        "TotalRemainingWorkDays" => item.TotalRemainingWorkDays,
        "CapacityWorkDays" => item.CapacityWorkDays,
        "UrgencyLevel" => UrgencyLevelKeys.ToChinese(item.UrgencyLevel) ?? "",
        "DaysDiffFromDelivery" => item.DaysDiffFromDelivery,
        "RawMaterialLockRemark" => RawMaterialLockRemarkKeys.ToChinese(item.RawMaterialLockRemark) ?? "",
        "AdjustmentRemark" => item.AdjustmentRemark ?? "",
        "PendingSectionRoughTube" => item.PendingSectionRoughTube,
        "PendingSectionWarehouseFix" => item.PendingSectionWarehouseFix,
        "PendingSection60Roll" => item.PendingSection60Roll,
        "PendingSection50Roll" => item.PendingSection50Roll,
        "PendingSection30Roll" => item.PendingSection30Roll,
        "PendingSection20Roll" => item.PendingSection20Roll,
        "PendingSectionThreeRoll" => item.PendingSectionThreeRoll,
        "PendingSectionDrawBench" => item.PendingSectionDrawBench,
        "ProductionAttentionProcess" => ProcessKeys.ToChinese(item.ProductionAttentionProcess) ?? "",
        "ProductionFlowProperty" => ProductionFlowKeys.ToChinese(item.ProductionFlowProperty) ?? "",
        "MaxBatchRemainingWorkDays" => item.MaxBatchRemainingWorkDays,
        "MainNoAttentionProcess" => ProcessKeys.ToChinese(item.MainNoAttentionProcess) ?? "",
        // 主号流转比（ColumnDef Key 与 DTO 属性名不一致：Key=MainNoFlowRatio, DTO=MainNoFlowOutputRatio）
        "MainNoFlowRatio" => item.MainNoFlowOutputRatio,
        _ => ""
    })!;

    private static string GetPipeManufacturingTypeText(string? pipeManufacturingType) => EnumHelper.GetDisplayName<PipeManufacturingType>(pipeManufacturingType);

    private static string GetDeliveryStateText(string? deliveryState) => EnumHelper.GetDisplayName<DeliveryState>(deliveryState);

    private static string GetSettlementMethodText(string? method) => EnumHelper.GetDisplayName<SettlementMethod>(method);

    private static string GetLengthStatusText(string? lengthStatus) => EnumHelper.GetDisplayName<LengthStatus>(lengthStatus);

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var pdfBytes = WorkOrderExecutionPrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }
}
