using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
using MES.Core.Constants;
using MES.Core.Exceptions;
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
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.WorkOrder;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.WorkOrder;

using WoEntity = MES.Data.Entities.WorkOrder.WorkOrder;

/// <summary>
/// 工单服务实现
///
/// CROSS-MODULE NOTE: 本服务因历史和性能原因，通过 DbContext 直查以下跨模块表：
///   - SalesOrders, OrderItems (Order 模块) — 订单-工单联动查询
///   - StandardRegisters (StandardRegister 模块) — 标准号引用数据读取
///   - InventoryBatches (Warehouse 模块) — 删除校验时检查入库数据
/// 这些均为只读查询，不涉及业务规则的绕过。写入跨模块数据必须通过对应的 Service 接口。
/// 详见 docs/04_开发规范.md §9.5 架构层面禁止事项
/// </summary>
public class WorkOrderService : IWorkOrderService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkOrderService> _logger;
    private readonly IWorkOrderListSummaryRefreshService? _listSummaryService;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly IOperationLogService _operationLogService;
    private readonly IMemoryCache _cache;
    private static readonly SemaphoreSlim _workOrderNoSemaphore = new SemaphoreSlim(1, 1);

    public WorkOrderService(AppDbContext context, ILogger<WorkOrderService> logger,
        IOperationLogService operationLogService,
        IMemoryCache cache,
        IWorkOrderListSummaryRefreshService? listSummaryService = null,
        IWorkOrderExecutionService? workOrderExecutionService = null)
    {
        _context = context;
        _logger = logger;
        _listSummaryService = listSummaryService;
        _workOrderExecutionService = workOrderExecutionService!;
        _operationLogService = operationLogService;
        _cache = cache;
    }

    private async Task TryRefreshExecutionSummaryAsync(string? workOrderNo)
    {
        if (string.IsNullOrWhiteSpace(workOrderNo)) return;
        try
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(new List<string> { workOrderNo });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工单执行状况刷新失败（不影响主流程）: WorkOrderNo={WorkOrderNo}", workOrderNo);
        }
    }

    private async Task TryRefreshExecutionSummariesAsync(List<string> workOrderNos)
    {
        var validNos = workOrderNos.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (validNos.Count == 0) return;
        try
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(validNos);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "批量刷新工单执行状况失败（不影响主流程）: Count={Count}", validNos.Count);
        }
    }

    /// <summary>
    /// 工单内容变更通知：查找引用变更工单号的批次，按工单聚合写入通知（24h 去重）
    /// </summary>
    private async Task NotifyWorkOrderChangedAsync(List<GeneratedWorkOrderDto> workOrders)
    {
        if (workOrders.Count == 0) return;

        var woNos = workOrders
            .Select(w => w.WorkOrderNo)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .ToList();
        if (woNos.Count == 0) return;

        try
        {
            // 查找引用这些工单号的生产批次，按工单聚合计数
            var batchCounts = await _context.ProductionBatches
                .Where(b => b.WorkOrderNo != null && woNos.Contains(b.WorkOrderNo) && b.WorkOrderNo != WorkOrderNoSentinel.NotWorkOrder)
                .GroupBy(b => b.WorkOrderNo)
                .Select(g => new { WorkOrderNo = g.Key, Count = g.Count() })
                .ToListAsync();

            if (batchCounts.Count == 0) return;

            var cutoff = DateTimeOffset.Now.AddHours(-24);
            var now = DateTimeOffset.Now;
            var hasNewNotification = false;

            foreach (var item in batchCounts)
            {
                // 24 小时内同一工单号不重复发通知
                var recent = await _context.Notifications
                    .AnyAsync(n => n.NotificationType == "WorkOrderChanged"
                                   && n.Title != null
                                   && n.Title.Contains(item.WorkOrderNo!)
                                   && n.CreatedTime >= cutoff);
                if (recent) continue;

                _context.Notifications.Add(new MES.Data.Entities.WorkOrder.Notification
                {
                    NotificationType = "WorkOrderChanged",
                    Title = $"工单 {item.WorkOrderNo} 内容已变更",
                    Content = $"涉及 {item.Count} 条关联记录，请检查是否需要处理",
                    IsRead = false,
                    Receiver = string.Empty,
                    CreatedTime = now
                });
                hasNewNotification = true;
            }

            if (hasNewNotification)
                await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工单变更通知写入失败（不影响主流程）");
        }
    }

    /// <summary>
    /// 获取已确认但无工单的订单列表（待生成工单）
    /// </summary>
    public async Task<List<WorkOrderListItemDto>> GetPendingOrdersAsync()
    {
        // CROSS-MODULE: reads Order.SalesOrders to find confirmed orders without work orders
        var rawData = await _context.SalesOrders
            .Where(so => so.Status == SalesOrderStatus.Confirmed
                && !_context.WorkOrders.Any(wo => wo.SalesOrderNo == so.OrderNumber))
            .OrderByDescending(so => so.SignDate)
            .Select(so => new
            {
                so.Id,
                so.OrderNumber,
                so.SignDate,
                so.CreatedTime,
                so.CreatedBy,
                so.UpdatedBy,
                so.UpdatedTime,
                so.Salesman,
                so.EndCustomer
            })
            .ToListAsync();

        return rawData.Select(r => new WorkOrderListItemDto
        {
            Id = 0,
            WorkOrderNo = "",
            SalesOrderNo = r.OrderNumber,
            ProductionMainNo = "",
            ProductionSubNo = null,
            SignDate = r.SignDate,
            Salesman = r.Salesman,
            EndCustomer = r.EndCustomer,
            DeliveryDate = default,
            DelayPenalty = false,
            SettlementMethod = default,
            PlantGrade = "",
            PipeManufacturingType = default,
            Specification = "",
            LengthStatus = default,
            MinLength = null,
            MaxLength = null,
            TotalQuantity = 0,
            TotalWeight = 0,
            DeliveryState = default,
            TotalItemCount = 0,
            Status = WorkOrderStatus.NotGenerated,
            CreatedBy = r.CreatedBy,
            CreatedTime = r.CreatedTime,
            UpdatedBy = r.UpdatedBy,
            UpdatedTime = r.UpdatedTime
        }).ToList();
    }

    #region 工单生成

    public async Task<List<OrderItemForWorkOrderDto>> GetOrderItemsForWorkOrderAsync(string salesOrderNo)
    {
        // CROSS-MODULE: reads Order.SalesOrders + OrderItems + StandardRegister
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.OrderNumber == salesOrderNo);
        if (salesOrder == null)
            throw new BusinessException($"订单 {salesOrderNo} 不存在");
        if (salesOrder.Status != SalesOrderStatus.Confirmed)
            throw new BusinessException($"订单 {salesOrderNo} 状态不是已确认，无法生成工单");

        // 获取该订单下所有状态不为已取消的工单（用于提取原主号/次号）
        var existingWorkOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == salesOrderNo)
            .ToListAsync();

        // 构建 项次ID -> (原主号, 原次号) 映射，同时建立工单ID查询
        var itemToOriginalNo = new Dictionary<int, (string MainNo, string? SubNo)>();
        var itemToWorkOrder = new Dictionary<int, WoEntity>(); // 用于后续校验合并字段是否一致
        foreach (var wo in existingWorkOrders)
        {
            var itemIds = wo.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(id => int.TryParse(id, out var parsed) ? parsed : -1)
                             .Where(id => id > 0);
            foreach (var itemId in itemIds)
            {
                if (!itemToOriginalNo.ContainsKey(itemId))
                {
                    itemToOriginalNo[itemId] = (wo.ProductionMainNo, wo.ProductionSubNo);
                    itemToWorkOrder[itemId] = wo;
                }
            }
        }

        var orderItems = await _context.OrderItems
            .AsNoTracking()
            .Include(oi => oi.ProductRequirement)
            .Where(oi => oi.SalesOrderId == salesOrder.Id)
            .OrderBy(oi => oi.Sequence)
            .ToListAsync();

        if (!orderItems.Any())
            throw new BusinessException($"订单 {salesOrderNo} 没有有效的项次");

        // 单独加载 StandardRegister
        var standardNos = orderItems
            .Where(oi => !string.IsNullOrEmpty(oi.StandardNo))
            .Select(oi => oi.StandardNo)
            .Distinct()
            .ToList();
        var srDict = standardNos.Any()
            ? await _context.StandardRegisters
                .Where(sr => standardNos.Contains(sr.StandardNo))
                .ToDictionaryAsync(sr => sr.StandardNo, sr => sr, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, MES.Data.Entities.StandardRegister.StandardRegister>(StringComparer.OrdinalIgnoreCase);

        // 加载牌号映射（从 StandardGradeMapping 取最新 PlantGrade/Density）
        var gradeNames = orderItems.Select(oi => oi.StandardGrade).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var gradeDict = gradeNames.Any()
            ? (await _context.StandardGradeMappings
                .Where(sgm => gradeNames.Contains(sgm.StandardGrade))
                .ToListAsync())
                .GroupBy(sgm => sgm.StandardGrade, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, StandardGradeMapping>();

        var groups = GroupOrderItemsByMergeFields(orderItems);
        var result = new List<OrderItemForWorkOrderDto>();
        var mainNoCounter = 1;

        foreach (var group in groups)
        {
            var firstItem = group.First();
            var prefix = GetMainNoPrefix(firstItem.PipeManufacturingType);
            var suggestedMainNo = $"{prefix}{mainNoCounter++:D2}";

            foreach (var item in group)
            {
                var standardNo = item.StandardNo ?? string.Empty;
                srDict.TryGetValue(standardNo, out var sr);
                var dto = new OrderItemForWorkOrderDto
                {
                    Id = item.Id,
                    OrderNumber = salesOrder.OrderNumber,
                    Sequence = item.Sequence,
                    PipeManufacturingType = item.PipeManufacturingType,
                    DeliveryDate = item.DeliveryDate,
                    DelayPenalty = item.DelayPenalty,
                    SettlementMethod = item.SettlementMethod,
                    StandardNo = sr?.StandardNo ?? standardNo,
                    DeliveryState = item.DeliveryState,
                    PlantGrade = gradeDict.TryGetValue(item.StandardGrade, out var gm) ? gm.PlantGrade : item.PlantGrade,
                    Specification = item.Specification,
                    OuterDiameterNegative = item.OuterDiameterNegative,
                    OuterDiameterPositive = item.OuterDiameterPositive,
                    WallThicknessNegative = item.WallThicknessNegative,
                    WallThicknessPositive = item.WallThicknessPositive,
                    LengthStatus = item.LengthStatus,
                    MinLength = item.MinLength,
                    MaxLength = item.MaxLength,
                    Quantity = item.Quantity,
                    Meters = item.Meters,
                    ContractWeight = item.ContractWeight,
                    TheoreticalWeight = item.TheoreticalWeight,
                    RequirementType = item.ProductRequirement?.RequirementType ?? RequirementType.Normal,
                    SuggestedMainNo = suggestedMainNo
                };

                // 尝试获取原主号/次号（若存在则填充，供覆盖生成时预填使用）
                // 注意：仅当项次的合并关键字段与原工单一致时才保留 OriginalMainNo
                // 若交货期/规格等关键字段变了，视为新增项次，不自动归入原工单
                if (itemToOriginalNo.TryGetValue(item.Sequence, out var original)
                    && itemToWorkOrder.TryGetValue(item.Sequence, out var originalWo))
                {
                    bool stillMatches = item.DeliveryDate == originalWo.DeliveryDate
                        && item.DelayPenalty == originalWo.DelayPenalty
                        && item.PipeManufacturingType == originalWo.PipeManufacturingType
                        && item.SettlementMethod == originalWo.SettlementMethod
                        && item.DeliveryState == originalWo.DeliveryState
                        && item.PlantGrade == originalWo.PlantGrade
                        && item.Specification == originalWo.Specification
                        && item.OuterDiameterNegative == originalWo.OuterDiameterNegative
                        && item.OuterDiameterPositive == originalWo.OuterDiameterPositive
                        && item.WallThicknessNegative == originalWo.WallThicknessNegative
                        && item.WallThicknessPositive == originalWo.WallThicknessPositive
                        && item.LengthStatus == originalWo.LengthStatus;

                    if (stillMatches)
                    {
                        dto.OriginalMainNo = original.MainNo;
                        dto.OriginalSubNo = original.SubNo;
                    }
                }
                result.Add(dto);
            }
        }
        return result;
    }

    private string GetMergeKey(OrderItem item)
    {
        return $"{item.DeliveryDate:yyyy-MM-dd}|{item.DelayPenalty}|{item.PipeManufacturingType}|{item.SettlementMethod}|" +
               $"{item.StandardNo}|{item.DeliveryState}|{item.PlantGrade}|{item.Specification}|" +
               $"{item.OuterDiameter}|{item.WallThickness}|" +
               $"{item.OuterDiameterNegative}|{item.OuterDiameterPositive}|" +
               $"{item.WallThicknessNegative}|{item.WallThicknessPositive}|" +
               $"{item.LengthStatus}";
    }

    private (bool IsValid, List<string> Errors) ValidateMergeFields(OrderItem item1, OrderItem item2)
    {
        var errors = new List<string>();

        if (item1.DeliveryDate != item2.DeliveryDate)
            errors.Add($"交货日期 ({item1.DeliveryDate:yyyy-MM-dd} ≠ {item2.DeliveryDate:yyyy-MM-dd})");
        if (item1.DelayPenalty != item2.DelayPenalty)
            errors.Add($"延期罚款 ({item1.DelayPenalty} ≠ {item2.DelayPenalty})");
        if (item1.SettlementMethod != item2.SettlementMethod)
            errors.Add($"结算方式 ({item1.SettlementMethod} ≠ {item2.SettlementMethod})");
        if (item1.PipeManufacturingType != item2.PipeManufacturingType)
            errors.Add($"物料名称 ({item1.PipeManufacturingType} ≠ {item2.PipeManufacturingType})");
        if (item1.StandardNo != item2.StandardNo)
            errors.Add($"标准号 ({item1.StandardNo} ≠ {item2.StandardNo})");
        if (item1.DeliveryState != item2.DeliveryState)
            errors.Add($"交货状态 ({item1.DeliveryState} ≠ {item2.DeliveryState})");
        if (item1.StandardGrade != item2.StandardGrade)
            errors.Add($"标准牌号 ({item1.StandardGrade} ≠ {item2.StandardGrade})");
        if (item1.OuterDiameter != item2.OuterDiameter)
            errors.Add($"外径 ({item1.OuterDiameter} ≠ {item2.OuterDiameter})");
        if (item1.WallThickness != item2.WallThickness)
            errors.Add($"壁厚 ({item1.WallThickness} ≠ {item2.WallThickness})");
        if (item1.OuterDiameterNegative != item2.OuterDiameterNegative)
            errors.Add($"外径下偏差 ({item1.OuterDiameterNegative} ≠ {item2.OuterDiameterNegative})");
        if (item1.OuterDiameterPositive != item2.OuterDiameterPositive)
            errors.Add($"外径上偏差 ({item1.OuterDiameterPositive} ≠ {item2.OuterDiameterPositive})");
        if (item1.WallThicknessNegative != item2.WallThicknessNegative)
            errors.Add($"壁厚下偏差 ({item1.WallThicknessNegative} ≠ {item2.WallThicknessNegative})");
        if (item1.WallThicknessPositive != item2.WallThicknessPositive)
            errors.Add($"壁厚上偏差 ({item1.WallThicknessPositive} ≠ {item2.WallThicknessPositive})");
        if (item1.LengthStatus != item2.LengthStatus)
            errors.Add($"长度状态 ({item1.LengthStatus} ≠ {item2.LengthStatus})");

        return (errors.Count == 0, errors);
    }

    private List<List<OrderItem>> GroupOrderItemsByMergeFields(List<OrderItem> orderItems)
    {
        var groups = new Dictionary<string, List<OrderItem>>();

        foreach (var item in orderItems)
        {
            var key = GetMergeKey(item);
            if (!groups.ContainsKey(key))
                groups[key] = new List<OrderItem>();
            groups[key].Add(item);
        }
        return groups.Values.ToList();
    }

    private static string GetMainNoPrefix(PipeManufacturingType pipeManufacturingType)
    {
        // 主号前缀：焊管 H / 无缝管 X，后接两位序号（如 X01）
        return pipeManufacturingType switch
        {
            PipeManufacturingType.WeldedPipe => "H",
            _ => "X"
        };
    }

    private static void ValidateSubNo(LengthStatus lengthStatus, string? productionSubNo)
    {
        // 新规则：次号 2 位，全模式非空
        if (string.IsNullOrEmpty(productionSubNo))
            throw new BusinessException("次号不能为空");

        if (lengthStatus == LengthStatus.Fixed)
        {
            // 定尺：01~99（两位数字）
            if (!System.Text.RegularExpressions.Regex.IsMatch(productionSubNo, @"^\d{2}$"))
                throw new BusinessException($"定尺模式下次号格式必须为两位数字（如 01），当前值：{productionSubNo}");
        }
        else
        {
            // 范围尺/非定尺：固定 F0
            if (productionSubNo != "F0")
                throw new BusinessException($"{GetLengthStatusText(lengthStatus)}模式下次号必须为 F0，当前值：{productionSubNo}");
        }
    }

    public async Task<List<GeneratedWorkOrderDto>> GenerateWorkOrdersAsync(CreateWorkOrderRequest request)
    {
        // 使用信号量确保同一时间只有一个工单生成操作
        await _workOrderNoSemaphore.WaitAsync();
        try
        {
            List<GeneratedWorkOrderDto> result;
            if (request.GenerateMode == WorkOrderGenerateMode.Update)
            {
                result = await UpdateWorkOrdersAsync(request);
            }
            else
            {
                result = await GenerateWorkOrdersCoreAsync(request);
            }

            // 工单内容变更通知：查找引用这些工单号的批次，按工单聚合通知
            await NotifyWorkOrderChangedAsync(result);

            return result;
        }
        finally
        {
            _workOrderNoSemaphore.Release();
        }
    }

    private async Task<List<GeneratedWorkOrderDto>> GenerateWorkOrdersCoreAsync(CreateWorkOrderRequest request)
    {
        // CROSS-MODULE: reads Order.SalesOrders + OrderItems + StandardRegister
        // 1. 获取订单信息
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.OrderNumber == request.SalesOrderNo);

        if (salesOrder == null)
            throw new BusinessException($"订单 {request.SalesOrderNo} 不存在");

        if (salesOrder.Status != SalesOrderStatus.Confirmed)
            throw new BusinessException($"订单 {request.SalesOrderNo} 状态不是已确认，无法生成工单");

        // 单独加载 Customer（从 SalesOrder 快照字段读取）
        var salesOrderCustomer = salesOrder;

        // 2. 获取订单项次
        var allOrderItems = await _context.OrderItems
            .Include(oi => oi.ProductRequirement)
            .Where(oi => oi.SalesOrderId == salesOrder.Id)
            .ToDictionaryAsync(oi => oi.Sequence, oi => oi);

        // 单独加载 StandardRegister
        var standardNos = allOrderItems.Values
            .Where(oi => !string.IsNullOrEmpty(oi.StandardNo))
            .Select(oi => oi.StandardNo)
            .Distinct()
            .ToList();
        var srDict = standardNos.Any()
            ? await _context.StandardRegisters
                .Where(sr => standardNos.Contains(sr.StandardNo))
                .ToDictionaryAsync(sr => sr.StandardNo, sr => sr, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, MES.Data.Entities.StandardRegister.StandardRegister>(StringComparer.OrdinalIgnoreCase);

        // 3. 验证项次
        foreach (var workOrderGroup in request.WorkOrders)
        {
            foreach (var itemId in workOrderGroup.OrderItemIds)
            {
                if (!allOrderItems.ContainsKey(itemId))
                    throw new BusinessException($"项次序号 {itemId} 不存在或已被删除");
            }
        }

        // 4. 验证合并规则
        var mergeFieldErrors = new List<string>();
        foreach (var workOrderGroup in request.WorkOrders)
        {
            var groupItems = workOrderGroup.OrderItemIds
                .Select(id => allOrderItems.GetValueOrDefault(id))
                .OfType<OrderItem>()
                .ToList();
            if (!groupItems.Any())
                throw new BusinessException($"工单分组 {workOrderGroup.ProductionMainNo} 没有有效的项次");
            if (groupItems.Count <= 1) continue;

            var firstItem = groupItems.First();
            foreach (var item in groupItems.Skip(1))
            {
                var (isValid, errors) = ValidateMergeFields(firstItem, item);
                if (!isValid)
                {
                    mergeFieldErrors.Add($"主号 {workOrderGroup.ProductionMainNo} 下的项次 {item.Sequence} 与项次 {firstItem.Sequence} 合并字段不一致:\n  {string.Join("\n  ", errors)}");
                }
            }
        }
        if (mergeFieldErrors.Any())
            throw new BusinessException($"工单分组合并规则验证失败:\n\n{string.Join("\n\n", mergeFieldErrors)}");

        var generatedWorkOrders = new List<GeneratedWorkOrderDto>();

        // 5. 使用事务
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                // 6. 物理删除现有工单及其关联计划
                var existingWorkOrders = await _context.WorkOrders
                    .Where(wo => wo.SalesOrderNo == request.SalesOrderNo)
                    .ToListAsync();

                _logger.LogInformation("订单 {OrderNo} 原有工单数量: {Count}，执行物理删除",
                    request.SalesOrderNo, existingWorkOrders.Count);

                // 先清理关联的用料计划（避免孤立记录导致采购状态显示空工单号）
                var existingIds = existingWorkOrders.Select(wo => wo.Id).ToList();
                if (existingIds.Count > 0)
                {
                    var semiPlans = await _context.PurchaseSemiPlans
                        .Where(p => existingIds.Contains(p.WorkOrderId)).ToListAsync();
                    var finishPlans = await _context.PurchaseFinishedPlans
                        .Where(p => existingIds.Contains(p.WorkOrderId)).ToListAsync();
                    var invPlans = await _context.InventoryPlans
                        .Where(p => existingIds.Contains(p.WorkOrderId)).ToListAsync();
                    var piercingPlans = await _context.RoundBarPiercingPlans
                        .Where(p => existingIds.Contains(p.WorkOrderId)).ToListAsync();
                    var inProcessReworkPlans = await _context.InProcessReworkPlans
                        .Where(p => existingIds.Contains(p.WorkOrderId)).ToListAsync();
                    var fixedLengthRows = await _context.FixedLengthWorkOrders
                        .Where(p => existingIds.Contains(p.WorkOrderId)).ToListAsync();

                    if (semiPlans.Any()) _context.PurchaseSemiPlans.RemoveRange(semiPlans);
                    if (finishPlans.Any()) _context.PurchaseFinishedPlans.RemoveRange(finishPlans);
                    if (invPlans.Any()) _context.InventoryPlans.RemoveRange(invPlans);
                    if (piercingPlans.Any()) _context.RoundBarPiercingPlans.RemoveRange(piercingPlans);
                    if (inProcessReworkPlans.Any()) _context.InProcessReworkPlans.RemoveRange(inProcessReworkPlans);
                    if (fixedLengthRows.Any()) _context.FixedLengthWorkOrders.RemoveRange(fixedLengthRows);

                    // 清理读模型行
                    var delListRows = await _context.Set<WorkOrderListSummary>()
                        .Where(s => existingIds.Contains(s.WorkOrderId)).ToListAsync();
                    if (delListRows.Count != 0)
                        _context.Set<WorkOrderListSummary>().RemoveRange(delListRows);
                    var delExecRows = await _context.Set<WorkOrderExecutionSummary>()
                        .Where(s => existingIds.Contains(s.WorkOrderId)).ToListAsync();
                    if (delExecRows.Count != 0)
                        _context.Set<WorkOrderExecutionSummary>().RemoveRange(delExecRows);

                    await _context.SaveChangesAsync();
                }

                _context.WorkOrders.RemoveRange(existingWorkOrders);
                await _context.SaveChangesAsync();

                // 清除缓存，确保查询最新数据
                _context.ChangeTracker.Clear();

                var workOrdersToAdd = new List<WoEntity>();
                generatedWorkOrders = new List<GeneratedWorkOrderDto>();
                var newGroupItemsList = new List<List<OrderItem>>();

                foreach (var workOrderGroup in request.WorkOrders)
                {
                    var groupItems = workOrderGroup.OrderItemIds
                        .Select(id => allOrderItems.GetValueOrDefault(id))
                        .Where(item => item != null)
                        .Select(x => x!)
                        .ToList();
                    if (!groupItems.Any()) continue;

                    var firstItem = groupItems.First()!;

                    ValidateSubNo(firstItem.LengthStatus, workOrderGroup.ProductionSubNo);

                    var (minLength, maxLength, totalQuantity, totalMeters, totalWeight, itemDetails, technicalRequirements) =
                        CalculateAggregates(groupItems, firstItem.LengthStatus);

                    // 工单号格式: {订单号}-{主号}[-{次号}]
                    var subPart = string.IsNullOrEmpty(workOrderGroup.ProductionSubNo) ? "" : $"-{workOrderGroup.ProductionSubNo}";
                    var workOrderNo = $"{request.SalesOrderNo}-{workOrderGroup.ProductionMainNo}{subPart}";

                    _logger.LogWarning($"生成工单号: {workOrderNo}");

                    var workOrder = new WoEntity
                    {
                        WorkOrderNo = workOrderNo,
                        SalesOrderNo = request.SalesOrderNo,
                        ProductionMainNo = workOrderGroup.ProductionMainNo,
                        ProductionSubNo = workOrderGroup.ProductionSubNo,
                        OrderItemIds = string.Join(",", workOrderGroup.OrderItemIds.Distinct()),
                        Status = WorkOrderStatus.Confirmed,
                        SignDate = salesOrder.SignDate,
                        Salesman = salesOrderCustomer?.Salesman ?? string.Empty,
                        EndCustomer = salesOrderCustomer?.EndCustomer,
                        DeliveryDate = firstItem.DeliveryDate,
                        DelayPenalty = firstItem.DelayPenalty,
                        PipeManufacturingType = firstItem.PipeManufacturingType,
                        SettlementMethod = firstItem.SettlementMethod,
                        StandardCode = srDict.GetValueOrDefault(firstItem.StandardNo ?? string.Empty)?.StandardNo ?? firstItem.StandardNo ?? string.Empty,
                        DeliveryState = firstItem.DeliveryState,
                        PlantGrade = firstItem.PlantGrade,
                        Specification = firstItem.Specification,
                        OuterDiameterNegative = firstItem.OuterDiameterNegative,
                        OuterDiameterPositive = firstItem.OuterDiameterPositive,
                        WallThicknessNegative = firstItem.WallThicknessNegative,
                        WallThicknessPositive = firstItem.WallThicknessPositive,
                        LengthStatus = firstItem.LengthStatus,
                        MinLength = minLength,
                        MaxLength = maxLength,
                        TotalQuantity = totalQuantity,
                        TotalMeters = totalMeters,
                        TotalWeight = totalWeight,
                        TotalItemCount = groupItems.Count,
                        ItemDetails = itemDetails,
                        TechnicalRequirements = technicalRequirements
                    };

                    workOrdersToAdd.Add(workOrder);
                    newGroupItemsList.Add(groupItems);

                    generatedWorkOrders.Add(new GeneratedWorkOrderDto
                    {
                        Id = 0,
                        WorkOrderNo = workOrderNo,
                        SalesOrderNo = request.SalesOrderNo,
                        ProductionMainNo = workOrderGroup.ProductionMainNo,
                        ProductionSubNo = workOrderGroup.ProductionSubNo,
                        Status = WorkOrderStatus.Confirmed,
                        TotalQuantity = totalQuantity,
                        TotalWeight = totalWeight
                    });
                }

                await _context.WorkOrders.AddRangeAsync(workOrdersToAdd);
                await _context.SaveChangesAsync();

                for (int i = 0; i < workOrdersToAdd.Count; i++)
                {
                    generatedWorkOrders[i].Id = workOrdersToAdd[i].Id;
                }

                // 构建定尺工单（按长度聚合，从长到短）
                var builtFixedLengthRows = new List<FixedLengthWorkOrder>();
                for (int i = 0; i < workOrdersToAdd.Count; i++)
                {
                    builtFixedLengthRows.AddRange(BuildFixedLengthWorkOrders(
                        workOrdersToAdd[i].Id, workOrdersToAdd[i].WorkOrderNo,
                        workOrdersToAdd[i].SalesOrderNo, workOrdersToAdd[i].ProductionMainNo, newGroupItemsList[i]));
                }
                if (builtFixedLengthRows.Any())
                {
                    _context.FixedLengthWorkOrders.AddRange(builtFixedLengthRows);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                // 在事务内记录创建日志（workOrdersToAdd 在此作用域内）
                foreach (var wo in workOrdersToAdd)
                    await _operationLogService.AddLogAsync("WorkOrder", wo.Id, "创建",
                        $"工单号={wo.WorkOrderNo}, 订单号={request.SalesOrderNo}, 交货日期={wo.DeliveryDate:yyyy-MM-dd}, 交货状态={EnumHelper.GetDisplayName(wo.DeliveryState)}, 规格={wo.Specification}, 长度状态={EnumHelper.GetDisplayName(wo.LengthStatus)}, 最小长度={wo.MinLength?.ToString("G29")}, 最大长度={wo.MaxLength?.ToString("G29")}, 总支数={wo.TotalQuantity}, 总重量={wo.TotalWeight:G29}kg, 项次数={wo.TotalItemCount}");
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("UK_WorkOrder_WorkOrderNo") == true)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "生成工单时发生唯一键冲突，订单号 {OrderNo}", request.SalesOrderNo);
                throw new BusinessException("生成工单时发生工单号冲突，请稍后重试");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "生成工单失败: 订单号 {OrderNo}", request.SalesOrderNo);
                throw;
            }
        }

        // 刷新读模型（事务已提交，在 using 块之外执行）
        if (_listSummaryService != null) await _listSummaryService.RefreshBySalesOrderAsync(request.SalesOrderNo);
        foreach (var wo in generatedWorkOrders)
            await TryRefreshExecutionSummaryAsync(wo.WorkOrderNo);

        _logger.LogInformation("生成工单成功: 订单号 {OrderNo}, 生成 {Count} 个工单",
            request.SalesOrderNo, generatedWorkOrders.Count);

        return generatedWorkOrders;
    }

    /// <summary>
    /// 更新修改模式：保留现有工单号/主号/次号，仅增删项次并重算汇总
    /// </summary>
    private async Task<List<GeneratedWorkOrderDto>> UpdateWorkOrdersAsync(CreateWorkOrderRequest request)
    {
        // CROSS-MODULE: reads Order.SalesOrders + OrderItems + StandardRegister
        // 1. 获取订单信息
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.OrderNumber == request.SalesOrderNo);
        if (salesOrder == null)
            throw new BusinessException($"订单 {request.SalesOrderNo} 不存在");
        if (salesOrder.Status != SalesOrderStatus.Confirmed)
            throw new BusinessException($"订单 {request.SalesOrderNo} 状态不是已确认，无法修改工单");

        var customer = salesOrder;

        // 2. 获取订单项次
        var allOrderItems = await _context.OrderItems
            .Include(oi => oi.ProductRequirement)
            .Where(oi => oi.SalesOrderId == salesOrder.Id)
            .ToDictionaryAsync(oi => oi.Sequence, oi => oi);

        var standardNos = allOrderItems.Values
            .Where(oi => !string.IsNullOrEmpty(oi.StandardNo))
            .Select(oi => oi.StandardNo).Distinct().ToList();
        var srDict = standardNos.Any()
            ? await _context.StandardRegisters
                .Where(sr => standardNos.Contains(sr.StandardNo))
                .ToDictionaryAsync(sr => sr.StandardNo, sr => sr, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, MES.Data.Entities.StandardRegister.StandardRegister>(StringComparer.OrdinalIgnoreCase);

        // 3. 验证项次
        foreach (var workOrderGroup in request.WorkOrders)
        {
            foreach (var itemId in workOrderGroup.OrderItemIds)
            {
                if (!allOrderItems.ContainsKey(itemId))
                    throw new BusinessException($"项次 ID {itemId} 不存在或已被删除");
            }
        }

        // 3b. 验证所有项次都已分配到某个工单（防止删除工单后孤儿项次被静默遗漏）
        var assignedItemIds = request.WorkOrders
            .SelectMany(wo => wo.OrderItemIds)
            .ToHashSet();
        var unassignedItems = allOrderItems.Values
            .Where(oi => !assignedItemIds.Contains(oi.Sequence))
            .ToList();
        if (unassignedItems.Count != 0)
        {
            var desc = string.Join("、", unassignedItems.Select(i => $"项次 {i.Sequence}"));
            throw new BusinessException($"以下项次未分配到任何工单，请将其合并到某个工单后提交: {desc}");
        }

        // 4. 验证合并规则
        var mergeFieldErrors = new List<string>();
        foreach (var workOrderGroup in request.WorkOrders)
        {
            var groupItems = workOrderGroup.OrderItemIds
                .Select(id => allOrderItems.GetValueOrDefault(id))
                .OfType<OrderItem>()
                .ToList();
            if (!groupItems.Any()) continue;
            if (groupItems.Count <= 1) continue;

            var firstItem = groupItems.First();
            foreach (var item in groupItems.Skip(1))
            {
                var (isValid, errors) = ValidateMergeFields(firstItem, item);
                if (!isValid)
                {
                    mergeFieldErrors.Add($"主号 {workOrderGroup.ProductionMainNo} 下的项次 {item.Sequence} 与项次 {firstItem.Sequence} 合并字段不一致:\n  {string.Join("\n  ", errors)}");
                }
            }
        }
        if (mergeFieldErrors.Any())
            throw new BusinessException($"工单分组合并规则验证失败:\n\n{string.Join("\n\n", mergeFieldErrors)}");

        // 5. 加载现有工单，构建 (主号,次号) → WorkOrder 映射
        var existingWorkOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == request.SalesOrderNo)
            .ToListAsync();

        var existingByKey = new Dictionary<(string mainNo, string? subNo), WoEntity>();
        foreach (var wo in existingWorkOrders)
        {
            var key = (wo.ProductionMainNo, wo.ProductionSubNo);
            existingByKey.TryAdd(key, wo);
        }

        _logger.LogInformation("订单 {OrderNo} 更新修改: 现有工单 {ExistingCount} 个, 提交分组 {GroupCount} 个",
            request.SalesOrderNo, existingWorkOrders.Count, request.WorkOrders.Count);

        var result = new List<GeneratedWorkOrderDto>();
        var workOrdersToAdd = new List<WoEntity>();
        var rebuildFixedEntries = new List<(int WorkOrderId, string WorkOrderNo, string SalesOrderNo, string ProductionMainNo, List<OrderItem> Items)>();
        var newGroupItemsList = new List<List<OrderItem>>();

        // 6. 事务处理
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                var matchedKeys = new HashSet<(string mainNo, string? subNo)>();

                // 7. 遍历提交的分组：匹配现有工单则更新，否则新建
                foreach (var group in request.WorkOrders)
                {
                    var groupItems = group.OrderItemIds
                        .Select(id => allOrderItems.GetValueOrDefault(id))
                        .Where(item => item != null)
                        .Select(x => x!)
                        .ToList();
                    if (!groupItems.Any()) continue;

                    var firstItem = groupItems.First()!;
                    var key = (group.ProductionMainNo, group.ProductionSubNo);

                    if (existingByKey.TryGetValue(key, out var existingWo))
                    {
                        // 7a. 匹配到现有工单 → 更新
                        matchedKeys.Add(key);

                        var oldItemIds = existingWo.OrderItemIds
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(id => int.TryParse(id, out var parsed) ? parsed : -1)
                            .Where(id => id > 0)
                            .ToHashSet();
                        var newItemIds = group.OrderItemIds.ToHashSet();

                        bool changed = !oldItemIds.SetEquals(newItemIds);

                        // 始终同步订单级字段（客户名称/业务员/最终客户等变化时也需要更新）
                        var orderLevelChanged = existingWo.Salesman != (customer?.Salesman ?? string.Empty)
                            || existingWo.EndCustomer != customer?.EndCustomer
                            || existingWo.SignDate != salesOrder.SignDate;
                        if (orderLevelChanged)
                        {
                            existingWo.Salesman = customer?.Salesman ?? string.Empty;
                            existingWo.EndCustomer = customer?.EndCustomer;
                            existingWo.SignDate = salesOrder.SignDate;
                            changed = true;
                        }

                        if (changed)
                        {
                            ValidateSubNo(firstItem.LengthStatus, group.ProductionSubNo);

                            existingWo.OrderItemIds = string.Join(",", group.OrderItemIds.Distinct());

                            _logger.LogInformation("更新修改工单: {WorkOrderNo}, 项次 {OldCount}→{NewCount}",
                                existingWo.WorkOrderNo, oldItemIds.Count, newItemIds.Count);
                        }

                        // 始终重算聚合字段（即使项次无变化，也要修复原始生成时的错误值）
                        var (minLength, maxLength, totalQuantity, totalMeters, totalWeight, itemDetails, technicalRequirements) =
                            CalculateAggregates(groupItems, firstItem.LengthStatus);

                        existingWo.MinLength = minLength;
                        existingWo.MaxLength = maxLength;
                        existingWo.TotalQuantity = totalQuantity;
                        existingWo.TotalMeters = totalMeters;
                        existingWo.TotalWeight = totalWeight;
                        existingWo.TotalItemCount = groupItems.Count;
                        existingWo.ItemDetails = itemDetails;
                        existingWo.TechnicalRequirements = technicalRequirements;
                        existingWo.Status = WorkOrderStatus.Confirmed;

                        // 记录定尺工单重建项（更新分支，existingWo.Id 已有）
                        rebuildFixedEntries.Add((existingWo.Id, existingWo.WorkOrderNo, existingWo.SalesOrderNo, group.ProductionMainNo, groupItems));

                        if (!changed) changed = true;

                        result.Add(new GeneratedWorkOrderDto
                        {
                            Id = existingWo.Id,
                            WorkOrderNo = existingWo.WorkOrderNo,
                            SalesOrderNo = request.SalesOrderNo,
                            ProductionMainNo = group.ProductionMainNo,
                            ProductionSubNo = group.ProductionSubNo,
                            Status = WorkOrderStatus.Confirmed,
                            TotalQuantity = groupItems.Sum(i => i!.LengthStatus == LengthStatus.Fixed ? (i.Quantity ?? 0) : 0),
                            TotalWeight = groupItems.Sum(i => i!.LengthStatus == LengthStatus.Fixed ? i.TheoreticalWeight : i.ContractWeight),
                            IsModified = changed
                        });
                    }
                    else
                    {
                        // 7b. 未匹配到现有工单 → 新建工单
                        ValidateSubNo(firstItem.LengthStatus, group.ProductionSubNo);

                        var (newMinLength, newMaxLength, newTotalQuantity, newTotalMeters, newTotalWeight, newItemDetails, newTechRequirements) =
                            CalculateAggregates(groupItems, firstItem.LengthStatus);

                        // 工单号格式: {订单号}-{主号}[-{次号}]
                        var subPart = string.IsNullOrEmpty(group.ProductionSubNo) ? "" : $"-{group.ProductionSubNo}";
                        var workOrderNo = $"{request.SalesOrderNo}-{group.ProductionMainNo}{subPart}";

                        var workOrder = new WoEntity
                        {
                            WorkOrderNo = workOrderNo,
                            SalesOrderNo = request.SalesOrderNo,
                            ProductionMainNo = group.ProductionMainNo,
                            ProductionSubNo = group.ProductionSubNo,
                            OrderItemIds = string.Join(",", group.OrderItemIds.Distinct()),
                            Status = WorkOrderStatus.Confirmed,
                            SignDate = salesOrder.SignDate,
                            Salesman = customer?.Salesman ?? string.Empty,
                            EndCustomer = customer?.EndCustomer,
                            DeliveryDate = firstItem.DeliveryDate,
                            DelayPenalty = firstItem.DelayPenalty,
                            PipeManufacturingType = firstItem.PipeManufacturingType,
                            SettlementMethod = firstItem.SettlementMethod,
                            StandardCode = srDict.GetValueOrDefault(firstItem.StandardNo ?? string.Empty)?.StandardNo ?? firstItem.StandardNo ?? string.Empty,
                            DeliveryState = firstItem.DeliveryState,
                            PlantGrade = firstItem.PlantGrade,
                            Specification = firstItem.Specification,
                            OuterDiameterNegative = firstItem.OuterDiameterNegative,
                            OuterDiameterPositive = firstItem.OuterDiameterPositive,
                            WallThicknessNegative = firstItem.WallThicknessNegative,
                            WallThicknessPositive = firstItem.WallThicknessPositive,
                            LengthStatus = firstItem.LengthStatus,
                            MinLength = newMinLength,
                            MaxLength = newMaxLength,
                            TotalQuantity = newTotalQuantity,
                            TotalMeters = newTotalMeters,
                            TotalWeight = newTotalWeight,
                            TotalItemCount = groupItems.Count,
                            ItemDetails = newItemDetails,
                            TechnicalRequirements = newTechRequirements
                        };

                        workOrdersToAdd.Add(workOrder);
                        newGroupItemsList.Add(groupItems);

                        _logger.LogInformation("更新修改新建工单: {WorkOrderNo}, 主号 {MainNo}", workOrderNo, group.ProductionMainNo);
                    }
                }

                // 8. 删除未匹配到的现有工单（项次全部移除）
                foreach (var wo in existingWorkOrders)
                {
                    var key = (wo.ProductionMainNo, wo.ProductionSubNo);
                    if (!matchedKeys.Contains(key))
                    {
                        // 清理关联用料计划
                        var semiPlans = await _context.PurchaseSemiPlans
                            .Where(p => p.WorkOrderId == wo.Id).ToListAsync();
                        var finishPlans = await _context.PurchaseFinishedPlans
                            .Where(p => p.WorkOrderId == wo.Id).ToListAsync();
                        var invPlans = await _context.InventoryPlans
                            .Where(p => p.WorkOrderId == wo.Id).ToListAsync();
                        var piercingPlans = await _context.RoundBarPiercingPlans
                            .Where(p => p.WorkOrderId == wo.Id).ToListAsync();
                        var inProcessReworkPlans = await _context.InProcessReworkPlans
                            .Where(p => p.WorkOrderId == wo.Id).ToListAsync();
                        var fixedLengthRows = await _context.FixedLengthWorkOrders
                            .Where(p => p.WorkOrderId == wo.Id).ToListAsync();

                        if (semiPlans.Any()) _context.PurchaseSemiPlans.RemoveRange(semiPlans);
                        if (finishPlans.Any()) _context.PurchaseFinishedPlans.RemoveRange(finishPlans);
                        if (invPlans.Any()) _context.InventoryPlans.RemoveRange(invPlans);
                        if (piercingPlans.Any()) _context.RoundBarPiercingPlans.RemoveRange(piercingPlans);
                        if (inProcessReworkPlans.Any()) _context.InProcessReworkPlans.RemoveRange(inProcessReworkPlans);
                        if (fixedLengthRows.Any()) _context.FixedLengthWorkOrders.RemoveRange(fixedLengthRows);

                        // 清理读模型行（删除工单的执行状况不会在后续增量刷新中被清除）
                        var delListSummary = await _context.Set<WorkOrderListSummary>()
                            .FirstOrDefaultAsync(s => s.WorkOrderId == wo.Id);
                        if (delListSummary != null)
                            _context.Set<WorkOrderListSummary>().Remove(delListSummary);
                        var delExecSummary = await _context.Set<WorkOrderExecutionSummary>()
                            .FirstOrDefaultAsync(s => s.WorkOrderId == wo.Id);
                        if (delExecSummary != null)
                            _context.Set<WorkOrderExecutionSummary>().Remove(delExecSummary);

                        _context.WorkOrders.Remove(wo);
                        _logger.LogInformation("更新修改删除工单（无项次）: {WorkOrderNo}", wo.WorkOrderNo);
                    }
                }

                // 9. 保存新建的工单
                if (workOrdersToAdd.Any())
                {
                    await _context.WorkOrders.AddRangeAsync(workOrdersToAdd);
                }

                await _context.SaveChangesAsync();

                // 10. 为新建的工单填充ID
                for (int i = 0; i < workOrdersToAdd.Count; i++)
                {
                    result.Add(new GeneratedWorkOrderDto
                    {
                        Id = workOrdersToAdd[i].Id,
                        WorkOrderNo = workOrdersToAdd[i].WorkOrderNo,
                        SalesOrderNo = request.SalesOrderNo,
                        ProductionMainNo = workOrdersToAdd[i].ProductionMainNo,
                        ProductionSubNo = workOrdersToAdd[i].ProductionSubNo,
                        Status = WorkOrderStatus.Confirmed,
                        TotalQuantity = workOrdersToAdd[i].TotalQuantity,
                        TotalWeight = workOrdersToAdd[i].TotalWeight,
                        IsModified = true
                    });
                }

                // 10b. 重建定尺工单（更新 + 新建，先删旧行再插新行，长度从长到短）
                var fixedRebuildIds = rebuildFixedEntries.Select(e => e.WorkOrderId)
                    .Concat(workOrdersToAdd.Select(w => w.Id))
                    .Distinct()
                    .ToList();
                if (fixedRebuildIds.Count != 0)
                {
                    var oldRows = await _context.FixedLengthWorkOrders
                        .Where(f => fixedRebuildIds.Contains(f.WorkOrderId))
                        .ToListAsync();
                    if (oldRows.Any())
                    {
                        _context.FixedLengthWorkOrders.RemoveRange(oldRows);
                        await _context.SaveChangesAsync();
                    }

                    var newRows = new List<FixedLengthWorkOrder>();
                    foreach (var (wid, wno, so, pm, items) in rebuildFixedEntries)
                        newRows.AddRange(BuildFixedLengthWorkOrders(wid, wno, so, pm, items));
                    for (int i = 0; i < workOrdersToAdd.Count; i++)
                        newRows.AddRange(BuildFixedLengthWorkOrders(
                            workOrdersToAdd[i].Id, workOrdersToAdd[i].WorkOrderNo,
                            workOrdersToAdd[i].SalesOrderNo, workOrdersToAdd[i].ProductionMainNo, newGroupItemsList[i]));

                    if (newRows.Any())
                    {
                        _context.FixedLengthWorkOrders.AddRange(newRows);
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "更新修改工单失败: 订单号 {OrderNo}", request.SalesOrderNo);
                throw;
            }
        }

        // 刷新读模型（事务已提交，在 using 块之外执行）
        if (_listSummaryService != null) await _listSummaryService.RefreshBySalesOrderAsync(request.SalesOrderNo);
        var woNos = result.Select(r => r.WorkOrderNo).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
        if (woNos.Count != 0) await TryRefreshExecutionSummariesAsync(woNos);

        _logger.LogInformation("更新修改工单成功: 订单号 {OrderNo}, 更新 {UpdatedCount} 个, 新建 {NewCount} 个",
            request.SalesOrderNo, result.Count(r => !r.IsModified || existingByKey.ContainsKey((r.ProductionMainNo, r.ProductionSubNo))),
            workOrdersToAdd.Count);

        return result;
    }

    private (decimal? MinLength, decimal? MaxLength, int TotalQuantity, decimal TotalMeters,
             decimal TotalWeight, string? ItemDetails, RequirementType TechnicalRequirements)
        CalculateAggregates(List<OrderItem> items, LengthStatus lengthStatus)
    {
        decimal? minLength = null;
        decimal? maxLength = null;
        int totalQuantity = 0;
        decimal totalMeters = 0;
        decimal totalWeight = 0;
        var itemDetailsBuilder = new System.Text.StringBuilder();
        bool hasSpecialRequirement = false;

        foreach (var item in items)
        {
            if (item.MinLength.HasValue)
            {
                if (!minLength.HasValue || item.MinLength < minLength) minLength = item.MinLength;
            }
            if (item.MaxLength.HasValue)
            {
                if (!maxLength.HasValue || item.MaxLength > maxLength) maxLength = item.MaxLength;
            }

            if (item.ProductRequirement != null && item.ProductRequirement.RequirementType == RequirementType.Special)
                hasSpecialRequirement = true;

            if (lengthStatus == LengthStatus.Fixed)
            {
                totalQuantity += item.Quantity ?? 0;
                totalMeters += item.Meters ?? 0;
                totalWeight += item.TheoreticalWeight;

                if (item.Quantity.HasValue && item.Quantity > 0 && item.MaxLength.HasValue && item.MaxLength > 0)
                {
                    itemDetailsBuilder.Append($"{item.Sequence}项,{item.MaxLength:G29}mm,{item.Quantity}支;");
                }
            }
            else
            {
                totalWeight += item.ContractWeight;
            }
        }

        var technicalRequirements = hasSpecialRequirement ? RequirementType.Special : RequirementType.Normal;

        return (minLength, maxLength, totalQuantity, totalMeters, totalWeight,
                itemDetailsBuilder.Length > 0 ? itemDetailsBuilder.ToString() : null, technicalRequirements);
    }

    /// <summary>
    /// 按长度聚合构建定尺工单数据
    /// 仅取定尺项次（LengthStatus=Fixed 且 Quantity>0 且 MaxLength>0），同长度 Quantity 求和，长度从长到短排列
    /// </summary>
    private List<FixedLengthWorkOrder> BuildFixedLengthWorkOrders(
        int workOrderId, string workOrderNo, string salesOrderNo, string productionMainNo, List<OrderItem> groupItems)
    {
        return groupItems
            .Where(i => i.LengthStatus == LengthStatus.Fixed && i.Quantity.HasValue && i.Quantity > 0 && i.MaxLength.HasValue && i.MaxLength > 0)
            .GroupBy(i => i.MaxLength!.Value)
            .Select(g => new FixedLengthWorkOrder
            {
                WorkOrderId = workOrderId,
                WorkOrderNo = workOrderNo,
                SalesOrderNo = salesOrderNo,
                ProductionMainNo = productionMainNo,
                Length = g.Key,
                PlannedQuantity = g.Sum(i => i.Quantity!.Value)
            })
            .OrderByDescending(f => f.Length)
            .ToList();
    }

    #endregion

    #region 工单管理

    public async Task<List<WorkOrderListDto>> GetAllListAsync()
    {
        var items = await _context.Set<WorkOrderListSummary>()
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedTime)
            .Select(s => new WorkOrderListDto
            {
                Id = s.WorkOrderId,
                WorkOrderNo = s.WorkOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                ProductionMainNo = s.ProductionMainNo,
                ProductionSubNo = s.ProductionSubNo,
                SignDate = s.SignDate,
                Salesman = s.Salesman,
                EndCustomer = s.EndCustomer,
                DeliveryDate = s.DeliveryDate,
                DelayPenalty = s.DelayPenalty,
                SettlementMethod = Enum.Parse<SettlementMethod>(s.SettlementMethod),
                PlantGrade = s.PlantGrade,
                PipeManufacturingType = Enum.Parse<PipeManufacturingType>(s.MaterialName),
                Specification = s.Specification,
                LengthStatus = Enum.Parse<LengthStatus>(s.LengthStatus),
                MinLength = s.MinLength,
                MaxLength = s.MaxLength,
                TotalQuantity = s.TotalQuantity,
                TotalWeight = s.TotalWeight,
                DeliveryState = Enum.Parse<DeliveryState>(s.DeliveryState),
                TotalItemCount = s.TotalItemCount,
                Status = (WorkOrderStatus)s.Status,
                CreatedTime = s.CreatedTime,
                MaterialPlanStatus = (MaterialPlanStatus)s.MaterialPlanStatus,
                MaterialPlanRate = s.MaterialPlanRate,
                MainNoMaterialPlanStatus = (MaterialPlanStatus)s.MainNoMaterialPlanStatus,
                MainNoMaterialPlanRate = s.MainNoMaterialPlanRate,
                OrderMaterialPlanStatus = (MaterialPlanStatus)s.OrderMaterialPlanStatus,
                LatestPlanDate = s.LatestPlanDate,
                SemiPlanTotalWeight = s.SemiPlanTotalWeight,
                SemiPlanTotalPieces = s.SemiPlanTotalPieces,
                FinishedPlanTotalWeight = s.FinishedPlanTotalWeight,
                FinishedPlanTotalPieces = s.FinishedPlanTotalPieces,
                InventoryPlanTotalWeight = s.InventoryPlanTotalWeight,
                InventoryPlanTotalPieces = s.InventoryPlanTotalPieces,
                ReworkPlanTotalWeight = s.ReworkPlanTotalWeight,
                ReworkPlanTotalPieces = s.ReworkPlanTotalPieces,
                PiercingPlanTotalWeight = s.PiercingPlanTotalWeight,
                PiercingPlanTotalPieces = s.PiercingPlanTotalPieces,
                InProcessReworkPlanTotalWeight = s.InProcessReworkPlanTotalWeight,
                InProcessReworkPlanTotalPieces = s.InProcessReworkPlanTotalPieces,
                InMainWorkOrderPlanTotalWeight = s.InMainWorkOrderPlanTotalWeight,
                InMainWorkOrderPlanTotalPieces = s.InMainWorkOrderPlanTotalPieces,
                MaxStandardCycle = s.MaxStandardCycle,
                MainNoMaxStandardCycle = s.MainNoMaxStandardCycle,
                CapacityWorkDays = s.CapacityWorkDays,
                TheoreticalCutoffDate = s.TheoreticalCutoffDate,
                MaterialPlanCoveredCount = s.MaterialPlanCoveredCount,
                MaterialPlanProportion = s.MaterialPlanProportion,
                LatestRequiredDate = s.LatestRequiredDate
            })
            .ToListAsync();

        return items;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.WorkOrderFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            var items = await _context.Set<WorkOrderListSummary>()
                .AsNoTracking()
                .Select(s => new
                {
                    s.WorkOrderNo,
                    s.SalesOrderNo,
                    s.ProductionMainNo,
                    s.ProductionSubNo,
                    s.SignDate,
                    s.Salesman,
                    s.EndCustomer,
                    s.DeliveryDate,
                    s.PlantGrade,
                    s.Specification,
                    s.LatestPlanDate,
                    s.LatestRequiredDate,
                    s.TheoreticalCutoffDate
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["WorkOrderNo"] = items.Select(x => x.WorkOrderNo).Distinct().OrderBy(x => x).ToList(),
                ["SalesOrderNo"] = items.Select(x => x.SalesOrderNo).Distinct().OrderBy(x => x).ToList(),
                ["ProductionMainNo"] = items.Select(x => x.ProductionMainNo).Distinct().OrderBy(x => x).ToList(),
                ["ProductionSubNo"] = items.Select(x => x.ProductionSubNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SignDate"] = items.Select(x => x.SignDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["Salesman"] = items.Select(x => x.Salesman).Distinct().OrderBy(x => x).ToList(),
                ["EndCustomer"] = items.Select(x => x.EndCustomer).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["DeliveryDate"] = items.Select(x => x.DeliveryDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["PlantGrade"] = items.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
                ["Specification"] = items.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
                ["LatestPlanDate"] = items.Select(x => x.LatestPlanDate?.ToString("yyyy-MM-dd")).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["LatestRequiredDate"] = items.Select(x => x.LatestRequiredDate?.ToString("yyyy-MM-dd")).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["TheoreticalCutoffDate"] = items.Select(x => x.TheoreticalCutoffDate?.ToString("yyyy-MM-dd")).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["RawMaterialLockRemark"] = _context.Set<WorkOrderExecutionSummary>()
                    .Where(e => e.RawMaterialLockRemark != null)
                    .Select(e => e.RawMaterialLockRemark!)
                    .Distinct().OrderBy(x => x).ToList(),
                ["UrgencyLevel"] = _context.Set<WorkOrderExecutionSummary>()
                    .Where(e => e.UrgencyLevel != null)
                    .Select(e => e.UrgencyLevel!)
                    .Distinct().OrderBy(x => x).ToList()
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<PagedResult<WorkOrderListItemDto>> GetPagedAsync(WorkOrderQueryParams query)
    {
        return await GetPagedBasicAsync(query);
    }

    public async Task<PagedResult<WorkOrderListDto>> GetPagedWithPlansAsync(WorkOrderQueryParams query, MaterialPlanLinkFilterDto? linkFilter = null)
    {
        return await GetPagedEnrichedAsync(query, linkFilter);
    }


    #region 工单分页查询基础方法

    /// <summary>
    /// 基础分页查询：直接查 WorkOrder 表，返回精简 DTO（不含用料计划聚合数据）
    /// </summary>
    private async Task<PagedResult<WorkOrderListItemDto>> GetPagedBasicAsync(WorkOrderQueryParams query)
    {
        var workOrderQuery = _context.WorkOrders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(query.SalesOrderNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.SalesOrderNo.Contains(query.SalesOrderNo));
        if (!string.IsNullOrEmpty(query.ProductionMainNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.ProductionMainNo.Contains(query.ProductionMainNo));
        if (!string.IsNullOrEmpty(query.ProductionSubNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.ProductionSubNo != null && wo.ProductionSubNo.Contains(query.ProductionSubNo));
        if (query.Status.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.Status == (WorkOrderStatus)query.Status.Value);
        if (!string.IsNullOrEmpty(query.Specification))
            workOrderQuery = workOrderQuery.Where(wo => wo.Specification.Contains(query.Specification));
        if (!string.IsNullOrEmpty(query.PlantGrade))
            workOrderQuery = workOrderQuery.Where(wo => wo.PlantGrade.Contains(query.PlantGrade));
        if (!string.IsNullOrEmpty(query.Salesman))
            workOrderQuery = workOrderQuery.Where(wo => wo.Salesman.Contains(query.Salesman));
        if (!string.IsNullOrEmpty(query.EndCustomer))
            workOrderQuery = workOrderQuery.Where(wo => wo.EndCustomer != null && wo.EndCustomer.Contains(query.EndCustomer));
        if (query.DeliveryDateStart.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.DeliveryDate >= query.DeliveryDateStart.Value);
        if (query.DeliveryDateEnd.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.DeliveryDate <= query.DeliveryDateEnd.Value);
        if (query.SignDateFrom.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.SignDate >= query.SignDateFrom.Value);
        if (query.SignDateTo.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.SignDate < query.SignDateTo.Value.AddDays(1));

        // 关键字模糊搜索：工单号/订单号/业务员/牌号/规格/主号/次号/最终用户
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            workOrderQuery = workOrderQuery.Where(wo =>
                wo.WorkOrderNo.Contains(keyword) ||
                wo.SalesOrderNo.Contains(keyword) ||
                wo.Salesman.Contains(keyword) ||
                wo.PlantGrade.Contains(keyword) ||
                wo.Specification.Contains(keyword) ||
                wo.ProductionMainNo.Contains(keyword) ||
                (wo.ProductionSubNo != null && wo.ProductionSubNo.Contains(keyword)) ||
                (wo.EndCustomer != null && wo.EndCustomer.Contains(keyword))
            );
        }

        // ===== 应用 ExcelFilter 筛选条件（JSON 筛选器） =====
        workOrderQuery = workOrderQuery.ApplyFilters(query.Filters);

        var totalCount = await workOrderQuery.CountAsync();
        workOrderQuery = workOrderQuery.ApplySort(query.SortBy ?? "CreatedTime", query.IsDescending);

        var workOrders = await workOrderQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var items = workOrders.Select(wo => ToListItemDto(wo)).ToList();

        return new PagedResult<WorkOrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    #endregion

    #region 用料计划三级聚合

    /// <summary>
    /// 含用料计划聚合的分页查询（从 WorkOrderListSummary 读模型查询）
    /// </summary>
    private async Task<PagedResult<WorkOrderListDto>> GetPagedEnrichedAsync(WorkOrderQueryParams query, MaterialPlanLinkFilterDto? linkFilter = null)
    {
        var summaryQuery = _context.Set<WorkOrderListSummary>().AsNoTracking().AsQueryable();

        // 只查询 WorkOrders 表中仍存在的工单（避免已物理删除的工单脏数据残留）
        summaryQuery = summaryQuery.Where(s => _context.WorkOrders.Any(wo => wo.Id == s.WorkOrderId));

        if (!string.IsNullOrEmpty(query.SalesOrderNo))
            summaryQuery = summaryQuery.Where(s => s.SalesOrderNo.Contains(query.SalesOrderNo));
        if (!string.IsNullOrEmpty(query.ProductionMainNo))
            summaryQuery = summaryQuery.Where(s => s.ProductionMainNo.Contains(query.ProductionMainNo));
        if (!string.IsNullOrEmpty(query.ProductionSubNo))
            summaryQuery = summaryQuery.Where(s => s.ProductionSubNo != null && s.ProductionSubNo.Contains(query.ProductionSubNo));
        if (query.Status.HasValue)
            summaryQuery = summaryQuery.Where(s => s.Status == query.Status.Value);
        if (!string.IsNullOrEmpty(query.Specification))
            summaryQuery = summaryQuery.Where(s => s.Specification.Contains(query.Specification));
        if (!string.IsNullOrEmpty(query.PlantGrade))
            summaryQuery = summaryQuery.Where(s => s.PlantGrade.Contains(query.PlantGrade));
        if (!string.IsNullOrEmpty(query.Salesman))
            summaryQuery = summaryQuery.Where(s => s.Salesman.Contains(query.Salesman));
        if (!string.IsNullOrEmpty(query.EndCustomer))
            summaryQuery = summaryQuery.Where(s => s.EndCustomer != null && s.EndCustomer.Contains(query.EndCustomer));
        if (query.DeliveryDateStart.HasValue)
            summaryQuery = summaryQuery.Where(s => s.DeliveryDate >= query.DeliveryDateStart.Value);
        if (query.DeliveryDateEnd.HasValue)
            summaryQuery = summaryQuery.Where(s => s.DeliveryDate <= query.DeliveryDateEnd.Value);
        if (query.SignDateFrom.HasValue)
            summaryQuery = summaryQuery.Where(s => s.SignDate >= query.SignDateFrom.Value);
        if (query.SignDateTo.HasValue)
            summaryQuery = summaryQuery.Where(s => s.SignDate < query.SignDateTo.Value.AddDays(1));

        // 关键字模糊搜索：工单号/订单号/主号/次号/业务员/最终用户/牌号/规格
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            summaryQuery = summaryQuery.Where(s =>
                s.WorkOrderNo.Contains(keyword) ||
                s.SalesOrderNo.Contains(keyword) ||
                s.Salesman.Contains(keyword) ||
                s.PlantGrade.Contains(keyword) ||
                s.Specification.Contains(keyword) ||
                s.ProductionMainNo.Contains(keyword) ||
                (s.ProductionSubNo != null && s.ProductionSubNo.Contains(keyword)) ||
                (s.EndCustomer != null && s.EndCustomer.Contains(keyword)) ||
                (s.MaterialPlanProportion != null && s.MaterialPlanProportion.Contains(keyword))
            );
        }

        // ===== 计划类型筛选（前端复选框：未勾选的计划类型需排除） =====
        if (!string.IsNullOrEmpty(query.PlanTypeFilter))
        {
            var selectedTypes = query.PlanTypeFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .ToHashSet();

            // 未勾选的计划类型：对应重量/支数必须为 0（表示该工单不使用此计划类型）
            if (!selectedTypes.Contains("piercing"))
                summaryQuery = summaryQuery.Where(s => (s.PiercingPlanTotalWeight == null || s.PiercingPlanTotalWeight == 0) &&
                                                       (s.PiercingPlanTotalPieces == null || s.PiercingPlanTotalPieces == 0));
            if (!selectedTypes.Contains("semi"))
                summaryQuery = summaryQuery.Where(s => (s.SemiPlanTotalWeight == null || s.SemiPlanTotalWeight == 0) &&
                                                       (s.SemiPlanTotalPieces == null || s.SemiPlanTotalPieces == 0));
            if (!selectedTypes.Contains("finish"))
                summaryQuery = summaryQuery.Where(s => (s.FinishedPlanTotalWeight == null || s.FinishedPlanTotalWeight == 0) &&
                                                       (s.FinishedPlanTotalPieces == null || s.FinishedPlanTotalPieces == 0));
            if (!selectedTypes.Contains("inventory"))
                summaryQuery = summaryQuery.Where(s => (s.InventoryPlanTotalWeight == null || s.InventoryPlanTotalWeight == 0) &&
                                                       (s.InventoryPlanTotalPieces == null || s.InventoryPlanTotalPieces == 0));
            if (!selectedTypes.Contains("rework"))
                summaryQuery = summaryQuery.Where(s => (s.ReworkPlanTotalWeight == null || s.ReworkPlanTotalWeight == 0) &&
                                                       (s.ReworkPlanTotalPieces == null || s.ReworkPlanTotalPieces == 0));
            if (!selectedTypes.Contains("inprocess"))
                summaryQuery = summaryQuery.Where(s => (s.InProcessReworkPlanTotalWeight == null || s.InProcessReworkPlanTotalWeight == 0) &&
                                                       (s.InProcessReworkPlanTotalPieces == null || s.InProcessReworkPlanTotalPieces == 0));
            if (!selectedTypes.Contains("inmain"))
                summaryQuery = summaryQuery.Where(s => (s.InMainWorkOrderPlanTotalWeight == null || s.InMainWorkOrderPlanTotalWeight == 0) &&
                                                       (s.InMainWorkOrderPlanTotalPieces == null || s.InMainWorkOrderPlanTotalPieces == 0));
        }

        // ===== 应用 ExcelFilter 筛选条件 =====
        // 跨表字段（主号-关注/主号-原锁备注/主号-计划性/工单投料状态等）来自 WorkOrderExecutionSummary，需关联子查询筛选
        var execSummary = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var execFilters = query.Filters?
            .Where(f => f.Field is "ScheduleStage" or "RawMaterialLockRemark" or "UrgencyLevel" or "InputStatus")
            .ToList();
        var remainingFilters = query.Filters?
            .Where(f => f.Field is not ("ScheduleStage" or "RawMaterialLockRemark" or "UrgencyLevel" or "InputStatus"))
            .ToList();
        if (execFilters != null)
        {
            foreach (var f in execFilters)
            {
                if (f.Operator != "in" || f.Values == null || f.Values.Count == 0) continue;
                switch (f.Field)
                {
                    case "ScheduleStage":
                        var ssVals = f.Values.Where(v => int.TryParse(v, out _)).Select(int.Parse).ToList();
                        if (ssVals.Count > 0)
                            summaryQuery = summaryQuery.Where(s =>
                                execSummary.Where(e => e.WorkOrderId == s.WorkOrderId && ssVals.Contains(e.ScheduleStage)).Any());
                        break;
                    case "RawMaterialLockRemark":
                        summaryQuery = summaryQuery.Where(s =>
                            execSummary.Where(e => e.WorkOrderId == s.WorkOrderId && f.Values.Contains(e.RawMaterialLockRemark ?? "")).Any());
                        break;
                    case "UrgencyLevel":
                        summaryQuery = summaryQuery.Where(s =>
                            execSummary.Where(e => e.WorkOrderId == s.WorkOrderId && f.Values.Contains(e.UrgencyLevel ?? "")).Any());
                        break;
                    case "InputStatus":
                        var inputStatusVals = f.Values.Where(v => int.TryParse(v, out _)).Select(int.Parse).ToList();
                        if (inputStatusVals.Count > 0)
                            summaryQuery = summaryQuery.Where(s =>
                                execSummary.Where(e => e.WorkOrderId == s.WorkOrderId && inputStatusVals.Contains(e.InputStatus)).Any());
                        break;
                }
            }
        }
        // ===== 应用「待投料量汇总」卡片点击联动筛选（备注/计划性，严格限定 ScheduleStage=2 原料锁定） =====
        // 成购矩阵联动（PurchaseOnly=true）按「包含」口径限定：成品采购计划量 > 0（FinishPlanWeight > 0，含单一成品采购）
        // 待投料矩阵联动（ExcludeSingleFinishPurchase=true）排除「单一成品采购」工单（成品采购计划量 > 0 且其余 6 类计划量全部 ≤ 0），与卡片待投料口径同步
        if (linkFilter != null && (!string.IsNullOrEmpty(linkFilter.Remark) || !string.IsNullOrEmpty(linkFilter.Urgency)))
        {
            summaryQuery = summaryQuery.Where(s =>
                execSummary.Where(e => e.WorkOrderId == s.WorkOrderId
                    && e.ScheduleStage == 2
                    && (string.IsNullOrEmpty(linkFilter.Remark) || e.RawMaterialLockRemark == linkFilter.Remark)
                    && (string.IsNullOrEmpty(linkFilter.Urgency) || e.UrgencyLevel == linkFilter.Urgency)
                    && (!linkFilter.PurchaseOnly || e.FinishPlanWeight > 0)
                    && (!linkFilter.ExcludeSingleFinishPurchase || !(
                        e.FinishPlanWeight > 0
                        && e.PiercingPlanWeight <= 0
                        && e.SemiPlanWeight <= 0
                        && e.InventoryPlanWeight <= 0
                        && e.ReworkPlanWeight <= 0
                        && e.InProcessReworkPlanWeight <= 0
                        && e.InMainPlanWeight <= 0))).Any());
        }
        // ===== 应用「在产在检-错疑待料」卡片点击联动筛选（主号-关注档位 + 字段>0） =====
        // 点击「生产执行/成品检验/主号完成 + 理论原料未至」→ ScheduleStage=X AND TotalMissingWeight>0（3% 门槛口径）
        // 点击「生产执行/成品检验/主号完成 + 工单到料未投」→ ScheduleStage=X AND PendingInputWeight>0（Max(0, 现可投料总重−已投)）
        if (linkFilter != null && linkFilter.ScheduleStage.HasValue)
        {
            var stage = linkFilter.ScheduleStage.Value;
            // 理论原料未至 3% 门槛容差（与 WorkOrderExecutionSummaryDto.TotalMissingWeight 同口径；捕获局部变量供 SQL 参数化）
            var stageMissingTol = MaterialPlanToleranceProvider.InputConsistencyTolerance;
            summaryQuery = summaryQuery.Where(s =>
                execSummary.Where(e => e.WorkOrderId == s.WorkOrderId
                    && e.ScheduleStage == stage
                    && (!linkFilter.HasMissingWeight || (
                        e.PiercingPlanWeight + e.SemiPlanWeight + e.FinishPlanWeight
                            + e.InventoryPlanWeight + e.ReworkPlanWeight
                            + e.InProcessReworkPlanWeight + e.InMainPlanWeight
                            - (e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                + e.InProcessReworkInputWeight + e.InMainInputWeight)
                        > (e.PiercingPlanWeight + e.SemiPlanWeight + e.FinishPlanWeight
                            + e.InventoryPlanWeight + e.ReworkPlanWeight
                            + e.InProcessReworkPlanWeight + e.InMainPlanWeight) * stageMissingTol))
                    && (!linkFilter.HasPendingInputWeight || (
                        e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                            + e.InventoryOutWeight + e.ReworkPlanInputWeight
                            + e.InProcessReworkInputWeight + e.InMainInputWeight - e.InputWeight > 0m))).Any());
        }
        summaryQuery = summaryQuery.ApplyFilters(remainingFilters);

        var totalCount = await summaryQuery.CountAsync();

        // ===== 排序 =====
        // 跨表字段（WorkOrderExecutionSummary）排序需关联子查询（ApplySort 反射实体属性，无法处理跨表字段）
        var sortBy = query.SortBy ?? "CreatedTime";
        // 理论缺失总料重容差（缺口 > 计划投料总重×3% 才取值）：与 WorkOrderExecutionSummaryDto.TotalMissingWeight 同口径（SQL 可翻译，捕获局部变量参数化）
        var missingWeightTol = MaterialPlanToleranceProvider.InputConsistencyTolerance;
        if (sortBy is "ScheduleStage" or "RawMaterialLockRemark" or "UrgencyLevel" or "InputWeight" or "InputOutputRatio" or "InputStatus" or "PendingInputWeight" or "TotalMissingWeight")
        {
            switch (sortBy)
            {
                case "ScheduleStage":
                    summaryQuery = query.IsDescending
                        ? summaryQuery.OrderByDescending(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (int?)e.ScheduleStage).FirstOrDefault())
                        : summaryQuery.OrderBy(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (int?)e.ScheduleStage).FirstOrDefault());
                    break;
                case "RawMaterialLockRemark":
                    summaryQuery = query.IsDescending
                        ? summaryQuery.OrderByDescending(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => e.RawMaterialLockRemark).FirstOrDefault())
                        : summaryQuery.OrderBy(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => e.RawMaterialLockRemark).FirstOrDefault());
                    break;
                case "UrgencyLevel":
                    summaryQuery = query.IsDescending
                        ? summaryQuery.OrderByDescending(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => e.UrgencyLevel).FirstOrDefault())
                        : summaryQuery.OrderBy(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => e.UrgencyLevel).FirstOrDefault());
                    break;
                case "InputWeight":
                    summaryQuery = query.IsDescending
                        ? summaryQuery.OrderByDescending(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (decimal?)e.InputWeight).FirstOrDefault())
                        : summaryQuery.OrderBy(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (decimal?)e.InputWeight).FirstOrDefault());
                    break;
                case "InputOutputRatio":
                    summaryQuery = query.IsDescending
                        ? summaryQuery.OrderByDescending(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (decimal?)e.InputOutputRatio).FirstOrDefault())
                        : summaryQuery.OrderBy(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (decimal?)e.InputOutputRatio).FirstOrDefault());
                    break;
                case "InputStatus":
                    summaryQuery = query.IsDescending
                        ? summaryQuery.OrderByDescending(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (int?)e.InputStatus).FirstOrDefault())
                        : summaryQuery.OrderBy(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (int?)e.InputStatus).FirstOrDefault());
                    break;
                case "PendingInputWeight":
                    summaryQuery = query.IsDescending
                        ? summaryQuery.OrderByDescending(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId)
                            .Select(e => (decimal?)(
                                e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                + e.InProcessReworkInputWeight + e.InMainInputWeight - e.InputWeight > 0m
                                    ? e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                        + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                        + e.InProcessReworkInputWeight + e.InMainInputWeight - e.InputWeight
                                    : 0m)).FirstOrDefault())
                        : summaryQuery.OrderBy(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId)
                            .Select(e => (decimal?)(
                                e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                + e.InProcessReworkInputWeight + e.InMainInputWeight - e.InputWeight > 0m
                                    ? e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                        + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                        + e.InProcessReworkInputWeight + e.InMainInputWeight - e.InputWeight
                                    : 0m)).FirstOrDefault());
                    break;
                case "TotalMissingWeight":
                    // 理论缺失总料重：缺口 > 计划投料总重×3% 才取值，否则为 0，与 WorkOrderExecutionSummaryDto.TotalMissingWeight 同口径（SQL 可翻译内联三元）
                    summaryQuery = query.IsDescending
                        ? summaryQuery.OrderByDescending(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId)
                            .Select(e => (decimal?)(
                                e.PiercingPlanWeight + e.SemiPlanWeight + e.FinishPlanWeight
                                    + e.InventoryPlanWeight + e.ReworkPlanWeight
                                    + e.InProcessReworkPlanWeight + e.InMainPlanWeight
                                    - (e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                        + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                        + e.InProcessReworkInputWeight + e.InMainInputWeight)
                                    > (e.PiercingPlanWeight + e.SemiPlanWeight + e.FinishPlanWeight
                                        + e.InventoryPlanWeight + e.ReworkPlanWeight
                                        + e.InProcessReworkPlanWeight + e.InMainPlanWeight) * missingWeightTol
                                ? e.PiercingPlanWeight + e.SemiPlanWeight + e.FinishPlanWeight
                                    + e.InventoryPlanWeight + e.ReworkPlanWeight
                                    + e.InProcessReworkPlanWeight + e.InMainPlanWeight
                                    - (e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                        + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                        + e.InProcessReworkInputWeight + e.InMainInputWeight)
                                : 0m)).FirstOrDefault())
                        : summaryQuery.OrderBy(s => execSummary.Where(e => e.WorkOrderId == s.WorkOrderId)
                            .Select(e => (decimal?)(
                                e.PiercingPlanWeight + e.SemiPlanWeight + e.FinishPlanWeight
                                    + e.InventoryPlanWeight + e.ReworkPlanWeight
                                    + e.InProcessReworkPlanWeight + e.InMainPlanWeight
                                    - (e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                        + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                        + e.InProcessReworkInputWeight + e.InMainInputWeight)
                                    > (e.PiercingPlanWeight + e.SemiPlanWeight + e.FinishPlanWeight
                                        + e.InventoryPlanWeight + e.ReworkPlanWeight
                                        + e.InProcessReworkPlanWeight + e.InMainPlanWeight) * missingWeightTol
                                ? e.PiercingPlanWeight + e.SemiPlanWeight + e.FinishPlanWeight
                                    + e.InventoryPlanWeight + e.ReworkPlanWeight
                                    + e.InProcessReworkPlanWeight + e.InMainPlanWeight
                                    - (e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                        + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                        + e.InProcessReworkInputWeight + e.InMainInputWeight)
                                : 0m)).FirstOrDefault());
                    break;
            }
        }
        else
        {
            summaryQuery = summaryQuery.ApplySort(sortBy, query.IsDescending);
        }

        // ===== 分页 + 投影到 DTO =====
        var items = await summaryQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(s => new WorkOrderListDto
            {
                Id = s.WorkOrderId,
                WorkOrderNo = s.WorkOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                ProductionMainNo = s.ProductionMainNo,
                ProductionSubNo = s.ProductionSubNo,
                SignDate = s.SignDate,
                Salesman = s.Salesman,
                EndCustomer = s.EndCustomer,
                DeliveryDate = s.DeliveryDate,
                DelayPenalty = s.DelayPenalty,
                SettlementMethod = Enum.Parse<SettlementMethod>(s.SettlementMethod),
                PlantGrade = s.PlantGrade,
                PipeManufacturingType = Enum.Parse<PipeManufacturingType>(s.MaterialName),
                Specification = s.Specification,
                LengthStatus = Enum.Parse<LengthStatus>(s.LengthStatus),
                MinLength = s.MinLength,
                MaxLength = s.MaxLength,
                TotalQuantity = s.TotalQuantity,
                TotalWeight = s.TotalWeight,
                DeliveryState = Enum.Parse<DeliveryState>(s.DeliveryState),
                TotalItemCount = s.TotalItemCount,
                Status = (WorkOrderStatus)s.Status,
                CreatedTime = s.CreatedTime,
                MaterialPlanStatus = (MaterialPlanStatus)s.MaterialPlanStatus,
                MaterialPlanRate = s.MaterialPlanRate,
                MainNoMaterialPlanStatus = (MaterialPlanStatus)s.MainNoMaterialPlanStatus,
                MainNoMaterialPlanRate = s.MainNoMaterialPlanRate,
                OrderMaterialPlanStatus = (MaterialPlanStatus)s.OrderMaterialPlanStatus,
                LatestPlanDate = s.LatestPlanDate,
                SemiPlanTotalWeight = s.SemiPlanTotalWeight,
                SemiPlanTotalPieces = s.SemiPlanTotalPieces,
                FinishedPlanTotalWeight = s.FinishedPlanTotalWeight,
                FinishedPlanTotalPieces = s.FinishedPlanTotalPieces,
                InventoryPlanTotalWeight = s.InventoryPlanTotalWeight,
                InventoryPlanTotalPieces = s.InventoryPlanTotalPieces,
                ReworkPlanTotalWeight = s.ReworkPlanTotalWeight,
                ReworkPlanTotalPieces = s.ReworkPlanTotalPieces,
                PiercingPlanTotalWeight = s.PiercingPlanTotalWeight,
                PiercingPlanTotalPieces = s.PiercingPlanTotalPieces,
                InProcessReworkPlanTotalWeight = s.InProcessReworkPlanTotalWeight,
                InProcessReworkPlanTotalPieces = s.InProcessReworkPlanTotalPieces,
                InMainWorkOrderPlanTotalWeight = s.InMainWorkOrderPlanTotalWeight,
                InMainWorkOrderPlanTotalPieces = s.InMainWorkOrderPlanTotalPieces,
                MaxStandardCycle = s.MaxStandardCycle,
                MainNoMaxStandardCycle = s.MainNoMaxStandardCycle,
                CapacityWorkDays = s.CapacityWorkDays,
                TheoreticalCutoffDate = s.TheoreticalCutoffDate,
                MaterialPlanCoveredCount = s.MaterialPlanCoveredCount,
                MaterialPlanProportion = s.MaterialPlanProportion,
                LatestRequiredDate = s.LatestRequiredDate,
                ScheduleStage = execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (int?)e.ScheduleStage).FirstOrDefault(),
                RawMaterialLockRemark = execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => e.RawMaterialLockRemark).FirstOrDefault(),
                UrgencyLevel = execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => e.UrgencyLevel).FirstOrDefault(),
                InputWeight = execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (decimal?)e.InputWeight).FirstOrDefault(),
                InputOutputRatio = execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (decimal?)e.InputOutputRatio).FirstOrDefault(),
                InputStatus = execSummary.Where(e => e.WorkOrderId == s.WorkOrderId).Select(e => (int?)e.InputStatus).FirstOrDefault(),
                PendingInputWeight = execSummary.Where(e => e.WorkOrderId == s.WorkOrderId)
                    .Select(e => (decimal?)(
                        e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                        + e.InventoryOutWeight + e.ReworkPlanInputWeight
                        + e.InProcessReworkInputWeight + e.InMainInputWeight - e.InputWeight > 0m
                            ? e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                + e.InProcessReworkInputWeight + e.InMainInputWeight - e.InputWeight
                            : 0m)).FirstOrDefault(),
                TotalMissingWeight = execSummary.Where(e => e.WorkOrderId == s.WorkOrderId)
                    .Select(e => (decimal?)(
                        e.PiercingPlanWeight + e.SemiPlanWeight + e.FinishPlanWeight
                            + e.InventoryPlanWeight + e.ReworkPlanWeight
                            + e.InProcessReworkPlanWeight + e.InMainPlanWeight
                            - (e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                + e.InProcessReworkInputWeight + e.InMainInputWeight)
                            > (e.PiercingPlanWeight + e.SemiPlanWeight + e.FinishPlanWeight
                                + e.InventoryPlanWeight + e.ReworkPlanWeight
                                + e.InProcessReworkPlanWeight + e.InMainPlanWeight) * missingWeightTol
                        ? e.PiercingPlanWeight + e.SemiPlanWeight + e.FinishPlanWeight
                            + e.InventoryPlanWeight + e.ReworkPlanWeight
                            + e.InProcessReworkPlanWeight + e.InMainPlanWeight
                            - (e.PiercingSubInWeight + e.SemiInWeight + e.FinishInWeight
                                + e.InventoryOutWeight + e.ReworkPlanInputWeight
                                + e.InProcessReworkInputWeight + e.InMainInputWeight)
                        : 0m)).FirstOrDefault()
            })
            .ToListAsync();
        return new PagedResult<WorkOrderListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    #endregion

    public async Task<WorkOrderDetailDto> GetByIdAsync(int id)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == id);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var dto = ToDetailDto(workOrder);

        // CROSS-MODULE: reads Order.SalesOrders for snapshot field patch
        // 覆盖冗余快照字段：从 SalesOrder 快照字段读取
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.OrderNumber == workOrder.SalesOrderNo);
        if (salesOrder != null)
        {
            dto.Salesman = salesOrder.Salesman;
            dto.EndCustomer = salesOrder.EndCustomer;
        }

        return dto;
    }

    public async Task<WorkOrderDetailDto> GetByWorkOrderNoAsync(string workOrderNo)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.WorkOrderNo == workOrderNo);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var dto = ToDetailDto(workOrder);

        // CROSS-MODULE: reads Order.SalesOrders for snapshot field patch
        // 覆盖冗余快照字段：从 SalesOrder 快照字段读取
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.OrderNumber == workOrder.SalesOrderNo);
        if (salesOrder != null)
        {
            dto.Salesman = salesOrder.Salesman;
            dto.EndCustomer = salesOrder.EndCustomer;
        }

        return dto;
    }

    public async Task<List<WorkOrderListItemDto>> GetBySalesOrderNoAsync(string salesOrderNo)
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.SalesOrderNo == salesOrderNo)
            .OrderBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToListAsync();

        return workOrders.Select(wo => ToListItemDto(wo)).ToList();
    }

    public async Task<UpdateWorkOrderStatusResponseDto> UpdateStatusAsync(int id, UpdateWorkOrderStatusRequest request)
    {
        var workOrder = await _context.WorkOrders
            .FirstOrDefaultAsync(wo => wo.Id == id);
        if (workOrder == null)
            throw new BusinessException("工单不存在");
        if (!CanTransitionTo(workOrder.Status, request.Status))
            throw new BusinessException($"不允许从 {GetStatusText(workOrder.Status)} 变更为 {GetStatusText(request.Status)}");

        var oldStatusText = GetStatusText(workOrder.Status);
        workOrder.Status = request.Status;
        _context.Entry(workOrder).Property(x => x.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("工单已被其他用户修改，请刷新后重试");
        }

        // 增量更新读模型 Status（只改一个字段，无需全量刷新）
        var summaryRow = await _context.Set<WorkOrderListSummary>()
            .FirstOrDefaultAsync(s => s.WorkOrderId == id);
        if (summaryRow != null)
        {
            summaryRow.Status = (int)request.Status;
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("更新工单状态成功: 工单号 {WorkOrderNo}, 新状态 {Status}",
            workOrder.WorkOrderNo, request.Status);

        await _operationLogService.AddLogAsync("WorkOrder", id, "变更", $"工单号={workOrder.WorkOrderNo}, 状态: {oldStatusText} → {GetStatusText(request.Status)}");

        await TryRefreshExecutionSummaryAsync(workOrder.WorkOrderNo);

        return new UpdateWorkOrderStatusResponseDto { Id = workOrder.Id, Status = workOrder.Status };
    }

    public async Task DeleteAsync(int id)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder == null)
        {
            _logger.LogWarning("工单 {Id} 不存在（可能已被删除）", id);
            return;
        }

        // 级联删除关联的用料计划（无FK约束，需手动清理）
        var semiPlans = await _context.PurchaseSemiPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var finishPlans = await _context.PurchaseFinishedPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var invPlans = await _context.InventoryPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var piercingPlans = await _context.RoundBarPiercingPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var inProcessReworkPlans = await _context.InProcessReworkPlans.Where(p => p.WorkOrderId == id).ToListAsync();
        var fixedLengthRows = await _context.FixedLengthWorkOrders.Where(p => p.WorkOrderId == id).ToListAsync();
        if (semiPlans.Any()) _context.PurchaseSemiPlans.RemoveRange(semiPlans);
        if (finishPlans.Any()) _context.PurchaseFinishedPlans.RemoveRange(finishPlans);
        if (invPlans.Any()) _context.InventoryPlans.RemoveRange(invPlans);
        if (piercingPlans.Any()) _context.RoundBarPiercingPlans.RemoveRange(piercingPlans);
        if (inProcessReworkPlans.Any()) _context.InProcessReworkPlans.RemoveRange(inProcessReworkPlans);
        if (fixedLengthRows.Any()) _context.FixedLengthWorkOrders.RemoveRange(fixedLengthRows);

        // 直接清理该工单的读模型行（双重保险，即使后续完整刷新失败也不会留下脏数据）
        var summaryRow = await _context.Set<WorkOrderListSummary>()
            .FirstOrDefaultAsync(s => s.WorkOrderId == id);
        if (summaryRow != null)
            _context.Set<WorkOrderListSummary>().Remove(summaryRow);
        var execSummaryRow = await _context.Set<WorkOrderExecutionSummary>()
            .FirstOrDefaultAsync(s => s.WorkOrderId == id);
        if (execSummaryRow != null)
            _context.Set<WorkOrderExecutionSummary>().Remove(execSummaryRow);

        // 先标记剩余工单为待修正（在删除之前做，避免中间状态异常导致脏数据）
        var remainingWorkOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == workOrder.SalesOrderNo && wo.Status == WorkOrderStatus.Confirmed)
            .ToListAsync();
        foreach (var wo in remainingWorkOrders)
        {
            wo.Status = WorkOrderStatus.Pending;
        }

        // 删除工单
        _context.WorkOrders.Remove(workOrder);
        await _context.SaveChangesAsync();

        await _operationLogService.AddLogAsync("WorkOrder", id, "删除", $"工单号={workOrder.WorkOrderNo}");

        _logger.LogInformation("删除工单成功: 工单号 {WorkOrderNo}, 关联订单 {OrderNo} 剩余 {Count} 个工单已标记为待修正",
            workOrder.WorkOrderNo, workOrder.SalesOrderNo, remainingWorkOrders.Count);

        // 刷新读模型（放在 finally 中确保即使标记待修正失败也能执行，或放在 SaveChanges 后无条件执行）
        try
        {
            if (_listSummaryService != null) await _listSummaryService.RefreshBySalesOrderAsync(workOrder.SalesOrderNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读模型刷新失败: SalesOrderNo={SalesOrderNo}", workOrder.SalesOrderNo);
        }

        // 增量刷新工单执行状况（更新同订单内剩余工单的聚合值，删除当前工单的行由 ReadModelService 内处理）
        var remainingWoNos = remainingWorkOrders.Select(w => w.WorkOrderNo).ToList();
        foreach (var woNo in remainingWoNos) await TryRefreshExecutionSummaryAsync(woNo);
    }

    public async Task SoftDeleteAsync(int id)
    {
        // 工单使用物理删除，SoftDeleteAsync 直接调用 DeleteAsync
        await DeleteAsync(id);
    }

    #endregion

    #region 订单变更检测

    public async Task CheckAndUpdateWorkOrderStatusAsync(int salesOrderId)
    {
        var salesOrder = await _context.SalesOrders.AsNoTracking()
            .FirstOrDefaultAsync(so => so.Id == salesOrderId);
        await CheckAndUpdateWorkOrderStatusInternalAsync(salesOrderId);
        await _context.SaveChangesAsync();

        // 刷新读模型
        if (salesOrder != null)
        {
            if (_listSummaryService != null) await _listSummaryService.RefreshBySalesOrderAsync(salesOrder.OrderNumber);
            var woNos = await _context.WorkOrders
                .Where(wo => wo.SalesOrderNo == salesOrder.OrderNumber)
                .Select(wo => wo.WorkOrderNo)
                .ToListAsync();
            foreach (var woNo in woNos) await TryRefreshExecutionSummaryAsync(woNo);
        }
    }

    private async Task<bool> CheckAndUpdateWorkOrderStatusInternalAsync(int salesOrderId)
    {
        // CROSS-MODULE: reads Order.SalesOrders for status sync check
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == salesOrderId);
        if (salesOrder == null || salesOrder.Status != SalesOrderStatus.Confirmed)
            return false;

        var workOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == salesOrder.OrderNumber)
            .ToListAsync();

        if (!workOrders.Any())
            return false;

        bool anyUpdated = false;
        foreach (var workOrder in workOrders)
        {
            if (workOrder.Status != WorkOrderStatus.Confirmed)
                continue;

            // 使用 Max(CreatedTime, UpdatedTime) 判断：若工单被"更新修改"过，UpdatedTime 会更新到最新
            // 避免更新修改后的工单再次被标记为"待修正"
            var lastSyncTime = workOrder.UpdatedTime > workOrder.CreatedTime
                ? workOrder.UpdatedTime
                : workOrder.CreatedTime;
            if (salesOrder.LastItemChangeTime.HasValue && salesOrder.LastItemChangeTime > lastSyncTime)
            {
                workOrder.Status = WorkOrderStatus.Pending;
                anyUpdated = true;
                _logger.LogInformation("工单 {WorkOrderNo}({Id}) 检测到项次变更，状态已更新为待修正",
                    workOrder.WorkOrderNo, workOrder.Id);
            }
        }

        if (anyUpdated)
        {
            await _context.SaveChangesAsync();

            // 工单状态变更后刷新用料计划总览读模型
            if (_listSummaryService != null)
                await _listSummaryService.RefreshBySalesOrderAsync(salesOrder.OrderNumber);

            // 增量刷新工单执行状况
            foreach (var wo in workOrders)
                await TryRefreshExecutionSummaryAsync(wo.WorkOrderNo);

            return true;
        }
        return false;
    }

    #endregion

    #region 订单工单项次追溯

    public async Task<OrderWorkOrderRelationDto> GetOrderWorkOrderRelationAsync(string salesOrderNo)
    {
        // CROSS-MODULE: reads Order.SalesOrders + OrderItems for order-workorder traceability
        // 1. 获取订单信息
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.OrderNumber == salesOrderNo);

        if (salesOrder == null)
            throw new BusinessException($"订单 {salesOrderNo} 不存在");

        // 2. 获取该订单下的所有工单（状态不为已取消的工单）
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.SalesOrderNo == salesOrderNo)
            .OrderBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToListAsync();

        // 3. 收集所有工单包含的项次ID
        var allOrderItemIds = new List<int>();
        foreach (var wo in workOrders)
        {
            var ids = wo.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(id => int.TryParse(id, out var parsed) ? parsed : -1)
                         .Where(id => id > 0);
            allOrderItemIds.AddRange(ids);
        }

        // 4. 批量查询订单项次（一次性加载所有相关项次，按销售订单过滤避免不同订单Sequence冲突）
        var orderItems = await _context.OrderItems
            .Where(oi => oi.SalesOrderId == salesOrder.Id && allOrderItemIds.Contains(oi.Sequence))
            .ToDictionaryAsync(oi => oi.Sequence, oi => oi);

        // 5. 构建 DTO
        var result = new OrderWorkOrderRelationDto
        {
            SalesOrderId = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            Salesman = salesOrder.Salesman,
            CustomerName = salesOrder.CustomerName,
            EndCustomer = salesOrder.EndCustomer,
            WorkOrders = new List<WorkOrderRelationDto>()
        };

        foreach (var wo in workOrders)
        {
            var itemIds = wo.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(id => int.TryParse(id, out var parsed) ? parsed : -1)
                             .Where(id => id > 0)
                             .ToList();

            var workOrderItems = new List<OrderItemBriefDto>();
            foreach (var itemId in itemIds)
            {
                if (orderItems.TryGetValue(itemId, out var item))
                {
                    workOrderItems.Add(new OrderItemBriefDto
                    {
                        Sequence = item.Sequence,
                        StandardGrade = item.StandardGrade,
                        Specification = item.Specification,
                        LengthStatus = item.LengthStatus,
                        MinLength = item.MinLength,
                        MaxLength = item.MaxLength,
                        Quantity = item.Quantity,
                        Meters = item.Meters,
                        ContractWeight = item.ContractWeight,
                        TheoreticalWeight = item.TheoreticalWeight
                    });
                }
            }

            result.WorkOrders.Add(new WorkOrderRelationDto
            {
                WorkOrderId = wo.Id,
                WorkOrderNo = wo.WorkOrderNo,
                ProductionMainNo = wo.ProductionMainNo,
                ProductionSubNo = wo.ProductionSubNo,
                Status = wo.Status,
                StatusText = GetStatusText(wo.Status),
                MaterialName = wo.PipeManufacturingType.ToString(),
                StandardGrade = workOrderItems.FirstOrDefault()?.StandardGrade ?? "",
                PlantGrade = wo.PlantGrade,
                Specification = wo.Specification,
                OuterDiameterNegative = wo.OuterDiameterNegative,
                OuterDiameterPositive = wo.OuterDiameterPositive,
                WallThicknessNegative = wo.WallThicknessNegative,
                WallThicknessPositive = wo.WallThicknessPositive,
                DeliveryState = wo.DeliveryState,
                LengthStatus = wo.LengthStatus,
                DeliveryDate = wo.DeliveryDate,
                TotalQuantity = wo.TotalQuantity,
                TotalWeight = wo.TotalWeight,
                OrderItems = workOrderItems
            });
        }

        return result;
    }

    public async Task<byte[]> PrintWorkOrderAsync(int id)
    {
        var entity = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == id)
            ?? throw new BusinessException("工单不存在");

        return WorkOrderPrintHelper.GeneratePdf(entity);
    }

    public async Task<byte[]> PrintWorkOrdersByOrderAsync(string salesOrderNo)
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.SalesOrderNo == salesOrderNo
                     )
            .OrderBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToListAsync();

        if (workOrders.Count == 0)
            throw new BusinessException($"订单 {salesOrderNo} 下没有可打印的工单");

        return WorkOrderPrintHelper.GenerateBatchPdf(salesOrderNo, workOrders);
    }

    public async Task<byte[]> PrintWorkOrdersByOrderBatchAsync(string[] salesOrderNos)
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => salesOrderNos.Contains(wo.SalesOrderNo)
                     )
            .OrderBy(wo => wo.SalesOrderNo)
            .ThenBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToListAsync();

        if (workOrders.Count == 0)
            throw new BusinessException("所选订单下没有可打印的工单");

        return WorkOrderPrintHelper.GenerateMultiBatchPdf(workOrders);
    }

    /// <summary>打印选中列表（按当前可见列渲染列表 PDF，Mode A 前端已准备数据）</summary>
    public Task<byte[]> PrintWorkOrderListAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        // 打印选中列表：列表显示模式（内容自适应列宽 + 整页宽度铺满 + 数据居中 + 表头行数不限）
        var pdfBytes = TablePrintHelper.GeneratePdf(title, items, columns,
            autoWidth: true, alignCenter: true, headerMaxLines: 0);
        return Task.FromResult(pdfBytes);
    }

    #endregion

    #region 辅助方法

    private static bool CanTransitionTo(WorkOrderStatus currentStatus, WorkOrderStatus targetStatus)
    {
        if (currentStatus == targetStatus) return true;
        if (currentStatus == WorkOrderStatus.NotGenerated) return targetStatus == WorkOrderStatus.Confirmed;
        if (currentStatus == WorkOrderStatus.Confirmed) return targetStatus == WorkOrderStatus.Pending;
        if (currentStatus == WorkOrderStatus.Pending) return targetStatus == WorkOrderStatus.Confirmed;
        return false;
    }

    private static string GetStatusText(WorkOrderStatus status) => EnumHelper.GetDisplayName(status);

    private static string GetLengthStatusText(LengthStatus status) => EnumHelper.GetDisplayName(status);

    #endregion

    private static WorkOrderListItemDto ToListItemDto(WoEntity entity) => new()
    {
        Id = entity.Id,
        WorkOrderNo = entity.WorkOrderNo,
        SalesOrderNo = entity.SalesOrderNo,
        ProductionMainNo = entity.ProductionMainNo,
        ProductionSubNo = entity.ProductionSubNo,
        SignDate = entity.SignDate,
        Salesman = entity.Salesman,
        EndCustomer = entity.EndCustomer,
        DeliveryDate = entity.DeliveryDate,
        DelayPenalty = entity.DelayPenalty,
        SettlementMethod = entity.SettlementMethod,
        PlantGrade = entity.PlantGrade,
        PipeManufacturingType = entity.PipeManufacturingType,
        Specification = entity.Specification,
        LengthStatus = entity.LengthStatus,
        MinLength = entity.MinLength,
        MaxLength = entity.MaxLength,
        TotalQuantity = entity.TotalQuantity,
        TotalWeight = entity.TotalWeight,
        DeliveryState = entity.DeliveryState,
        TotalItemCount = entity.TotalItemCount,
        Status = entity.Status,
        CreatedBy = entity.CreatedBy,
        CreatedTime = entity.CreatedTime,
        UpdatedBy = entity.UpdatedBy,
        UpdatedTime = entity.UpdatedTime
    };

    private static WorkOrderDetailDto ToDetailDto(WoEntity entity) => new()
    {
        Id = entity.Id,
        WorkOrderNo = entity.WorkOrderNo,
        SalesOrderNo = entity.SalesOrderNo,
        ProductionMainNo = entity.ProductionMainNo,
        ProductionSubNo = entity.ProductionSubNo,
        OrderItemIds = entity.OrderItemIds,
        Status = entity.Status,
        SignDate = entity.SignDate,
        Salesman = entity.Salesman,
        EndCustomer = entity.EndCustomer,
        DeliveryDate = entity.DeliveryDate,
        DelayPenalty = entity.DelayPenalty,
        PipeManufacturingType = entity.PipeManufacturingType,
        SettlementMethod = entity.SettlementMethod,
        StandardCode = entity.StandardCode,
        DeliveryState = entity.DeliveryState,
        PlantGrade = entity.PlantGrade,
        Specification = entity.Specification,
        OuterDiameterNegative = entity.OuterDiameterNegative,
        OuterDiameterPositive = entity.OuterDiameterPositive,
        WallThicknessNegative = entity.WallThicknessNegative,
        WallThicknessPositive = entity.WallThicknessPositive,
        LengthStatus = entity.LengthStatus,
        MinLength = entity.MinLength,
        MaxLength = entity.MaxLength,
        TotalQuantity = entity.TotalQuantity,
        TotalMeters = entity.TotalMeters,
        TotalWeight = entity.TotalWeight,
        TotalItemCount = entity.TotalItemCount,
        ItemDetails = entity.ItemDetails,
        TechnicalRequirements = entity.TechnicalRequirements.ToString(),
        RowVersion = entity.RowVersion,
        CreatedTime = entity.CreatedTime,
        CreatedBy = entity.CreatedBy,
        UpdatedTime = entity.UpdatedTime,
        UpdatedBy = entity.UpdatedBy,
        MaterialPlanStatus = entity.MaterialPlanStatus,
        MaterialPlanRate = entity.MaterialPlanRate,
        UnitWeight = PipeWeightCalculator.CalculateUnitWeight(
            entity.Specification,
            entity.OuterDiameterNegative,
            entity.OuterDiameterPositive,
            entity.WallThicknessNegative,
            entity.WallThicknessPositive,
            entity.LengthStatus,
            entity.MaxLength)
    };
}
