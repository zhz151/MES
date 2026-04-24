using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// 工单服务实现
/// </summary>
public class WorkOrderService : IWorkOrderService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkOrderService> _logger;
    private static readonly SemaphoreSlim _workOrderNoSemaphore = new SemaphoreSlim(1, 1);

    public WorkOrderService(AppDbContext context, ILogger<WorkOrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region 工单首页（订单状态监控）

    public async Task<PagedResult<OrderWorkOrderStatusDto>> GetOrderWorkOrderStatusPageAsync(WorkOrderQueryParams query)
    {
        // 获取所有已确认且未删除的订单（不包括已取消的订单）
        var orderQuery = _context.SalesOrders
            .Where(so => so.Status == SalesOrderStatus.Confirmed && !so.IsDeleted)
            .Join(
                _context.CustomerProfiles.Where(c => !c.IsDeleted),
                so => so.CustomerId,
                c => c.Id,
                (so, c) => new { SalesOrder = so, Customer = c }
            );

        // 应用筛选条件
        if (!string.IsNullOrEmpty(query.Salesman))
        {
            orderQuery = orderQuery.Where(x => x.Customer.Salesman.Contains(query.Salesman));
        }

        if (!string.IsNullOrEmpty(query.EndCustomer))
        {
            orderQuery = orderQuery.Where(x => x.Customer.EndCustomer != null && x.Customer.EndCustomer.Contains(query.EndCustomer));
        }

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            orderQuery = orderQuery.Where(x =>
                x.SalesOrder.OrderNumber.Contains(keyword) ||
                x.Customer.CustomerUnit.Contains(keyword) ||
                x.Customer.Salesman.Contains(keyword) ||
                (x.Customer.EndCustomer != null && x.Customer.EndCustomer.Contains(keyword))
            );
        }

        // 获取所有符合条件的订单（用于状态计算）
        var allOrders = await orderQuery.ToListAsync();
        var allOrderNumbers = allOrders.Select(x => x.SalesOrder.OrderNumber).ToList();

        // 获取所有相关工单（工单使用物理删除，不需要 IsDeleted 条件）
        var allWorkOrders = await _context.WorkOrders
            .Where(wo => allOrderNumbers.Contains(wo.SalesOrderNo) && wo.Status != 3)
            .ToListAsync();

        // 计算每个订单的工单状态
        var orderStatusMap = new Dictionary<string, (string Status, int? WorkOrderId)>();
        foreach (var item in allOrders)
        {
            var orderNumber = item.SalesOrder.OrderNumber;
            var orderWorkOrders = allWorkOrders.Where(wo => wo.SalesOrderNo == orderNumber).ToList();

            string workOrderStatus;
            int? workOrderId = null;

            if (!orderWorkOrders.Any())
            {
                workOrderStatus = "NotGenerated";
            }
            else
            {
                var pendingWorkOrder = orderWorkOrders.FirstOrDefault(wo => wo.Status == 2);
                if (pendingWorkOrder != null)
                {
                    workOrderStatus = "Pending";
                    workOrderId = pendingWorkOrder.Id;
                }
                else
                {
                    workOrderStatus = "Confirmed";
                    workOrderId = orderWorkOrders.FirstOrDefault()?.Id;
                }
            }

            orderStatusMap[orderNumber] = (workOrderStatus, workOrderId);
        }

        // 按状态筛选（在数据库层面，通过订单号过滤）
        if (!string.IsNullOrEmpty(query.WorkOrderStatus))
        {
            var filteredOrderNumbers = orderStatusMap
                .Where(x => x.Value.Status == query.WorkOrderStatus)
                .Select(x => x.Key)
                .ToList();

            orderQuery = orderQuery.Where(x => filteredOrderNumbers.Contains(x.SalesOrder.OrderNumber));
        }

        // 获取总数
        var totalCount = await orderQuery.CountAsync();

        // 排序和分页
        var orderedQuery = query.IsDescending
            ? orderQuery.OrderByDescending(x => x.SalesOrder.SignDate)
            : orderQuery.OrderBy(x => x.SalesOrder.SignDate);

        var orderList = await orderedQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        // 获取分页后订单的工单信息
        var pagedOrderNumbers = orderList.Select(x => x.SalesOrder.OrderNumber).ToList();
        var pagedWorkOrders = await _context.WorkOrders
            .Where(wo => pagedOrderNumbers.Contains(wo.SalesOrderNo) && wo.Status != 3)
            .ToListAsync();

        var items = new List<OrderWorkOrderStatusDto>();

        foreach (var item in orderList)
        {
            var order = item.SalesOrder;
            var customer = item.Customer;
            var orderWorkOrders = pagedWorkOrders.Where(wo => wo.SalesOrderNo == order.OrderNumber).ToList();

            string workOrderStatus;
            bool hasWorkOrder = orderWorkOrders.Any();
            int? workOrderId = null;

            if (!hasWorkOrder)
            {
                workOrderStatus = "NotGenerated";
            }
            else
            {
                var pendingWorkOrder = orderWorkOrders.FirstOrDefault(wo => wo.Status == 2);
                if (pendingWorkOrder != null)
                {
                    workOrderStatus = "Pending";
                    workOrderId = pendingWorkOrder.Id;
                }
                else
                {
                    workOrderStatus = "Confirmed";
                    workOrderId = orderWorkOrders.FirstOrDefault()?.Id;
                }
            }

            items.Add(new OrderWorkOrderStatusDto
            {
                SalesOrderId = order.Id,
                OrderNumber = order.OrderNumber,
                SignDate = order.SignDate,
                Salesman = customer.Salesman,
                CustomerName = customer.CustomerUnit,
                EndCustomer = customer.EndCustomer,
                WorkOrderStatus = workOrderStatus,
                HasWorkOrder = hasWorkOrder,
                WorkOrderId = workOrderId
            });
        }

        // 排序：待修正 → 未编制 → 已确定
        items = items
            .OrderBy(x => GetWorkOrderStatusOrder(x.WorkOrderStatus))
            .ThenByDescending(x => x.SignDate)
            .ToList();

        return new PagedResult<OrderWorkOrderStatusDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    private static int GetWorkOrderStatusOrder(string status)
    {
        return status switch
        {
            "Pending" => 1,
            "NotGenerated" => 2,
            "Confirmed" => 3,
            _ => 4
        };
    }

    public async Task<List<CancelledOrderDto>> GetCancelledOrdersAsync()
    {
        var query = from so in _context.SalesOrders
                    join c in _context.CustomerProfiles on so.CustomerId equals c.Id
                    join wo in _context.WorkOrders on so.OrderNumber equals wo.SalesOrderNo
                    where so.Status == SalesOrderStatus.Cancelled && !so.IsDeleted
                          && wo.Status != 3
                    select new CancelledOrderDto
                    {
                        SalesOrderId = so.Id,
                        OrderNumber = so.OrderNumber,
                        SignDate = so.SignDate,
                        Salesman = c.Salesman,
                        CustomerName = c.CustomerUnit,
                        WorkOrderId = wo.Id,
                        WorkOrderNo = wo.WorkOrderNo
                    };

        return await query.ToListAsync();
    }

    #endregion

    #region 工单生成

    public async Task<List<OrderItemForWorkOrderDto>> GetOrderItemsForWorkOrderAsync(string salesOrderNo)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.OrderNumber == salesOrderNo && !so.IsDeleted);
        if (salesOrder == null)
            throw new BusinessException($"订单 {salesOrderNo} 不存在");
        if (salesOrder.Status != SalesOrderStatus.Confirmed)
            throw new BusinessException($"订单 {salesOrderNo} 状态不是已确认，无法生成工单");

        // 获取该订单下所有状态不为已取消的工单（用于提取原主号/次号）
        var existingWorkOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == salesOrderNo && wo.Status != 3)
            .ToListAsync();

        // 构建 项次ID -> (原主号, 原次号) 映射
        var itemToOriginalNo = new Dictionary<int, (string MainNo, string? SubNo)>();
        foreach (var wo in existingWorkOrders)
        {
            var itemIds = wo.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(int.Parse);
            foreach (var itemId in itemIds)
            {
                if (!itemToOriginalNo.ContainsKey(itemId))
                {
                    itemToOriginalNo[itemId] = (wo.ProductionMainNo, wo.ProductionSubNo);
                }
            }
        }

        var orderItems = await _context.OrderItems
            .Include(oi => oi.ProductionStandard)
            .Include(oi => oi.ProductRequirement)
            .Where(oi => oi.SalesOrderId == salesOrder.Id && !oi.IsDeleted)
            .OrderBy(oi => oi.Sequence)
            .ToListAsync();

        if (!orderItems.Any())
            throw new BusinessException($"订单 {salesOrderNo} 没有有效的项次");

        var groups = GroupOrderItemsByMergeFields(orderItems);
        var result = new List<OrderItemForWorkOrderDto>();
        var mainNoCounter = 1;

        foreach (var group in groups)
        {
            var firstItem = group.First();
            var prefix = GetMainNoPrefix(firstItem.MaterialName, firstItem.LengthStatus);
            var suggestedMainNo = $"{prefix}{mainNoCounter++:D2}";

            foreach (var item in group)
            {
                var dto = new OrderItemForWorkOrderDto
                {
                    Id = item.Id,
                    OrderNumber = salesOrder.OrderNumber,
                    Sequence = item.Sequence,
                    MaterialName = item.MaterialName.ToString(),
                    DeliveryDate = item.DeliveryDate,
                    DelayPenalty = item.DelayPenalty,
                    SettlementMethod = item.SettlementMethod.ToString(),
                    StandardCode = item.ProductionStandard?.StandardCode ?? string.Empty,
                    DeliveryState = item.DeliveryState.ToString(),
                    PlantGrade = item.PlantGrade,
                    Specification = item.Specification,
                    OuterDiameterMinus = item.OuterDiameterNegative,
                    OuterDiameterPlus = item.OuterDiameterPositive,
                    WallThicknessMinus = item.WallThicknessNegative,
                    WallThicknessPlus = item.WallThicknessPositive,
                    LengthStatus = item.LengthStatus.ToString(),
                    MinLength = item.MinLength,
                    MaxLength = item.MaxLength,
                    Quantity = item.Quantity,
                    Meters = item.Meters,
                    ContractWeight = item.ContractWeight,
                    TheoreticalWeight = item.TheoreticalWeight,
                    RequirementType = item.ProductRequirement?.RequirementType.ToString() ?? "Normal",
                    SuggestedMainNo = suggestedMainNo
                };

                // 尝试获取原主号/次号（若存在则填充，供覆盖生成时预填使用）
                if (itemToOriginalNo.TryGetValue(item.Id, out var original))
                {
                    dto.OriginalMainNo = original.MainNo;
                    dto.OriginalSubNo = original.SubNo;
                }
                result.Add(dto);
            }
        }
        return result;
    }

    private string GetMergeKey(OrderItem item)
    {
        return $"{item.DeliveryDate:yyyy-MM-dd}|{item.DelayPenalty}|{item.MaterialName}|{item.SettlementMethod}|" +
               $"{item.ProductionStandardId}|{item.DeliveryState}|{item.PlantGrade}|{item.Specification}|" +
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
            errors.Add($"延期违约金 ({item1.DelayPenalty} ≠ {item2.DelayPenalty})");
        if (item1.SettlementMethod != item2.SettlementMethod)
            errors.Add($"结算方式 ({item1.SettlementMethod} ≠ {item2.SettlementMethod})");
        if (item1.MaterialName != item2.MaterialName)
            errors.Add($"物料名称 ({item1.MaterialName} ≠ {item2.MaterialName})");
        if (item1.ProductionStandardId != item2.ProductionStandardId)
            errors.Add($"产品标准 ({item1.ProductionStandardId} ≠ {item2.ProductionStandardId})");
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

    private static string GetMainNoPrefix(MaterialName materialName, LengthStatus lengthStatus)
    {
        if (materialName == MaterialName.WeldedPipe)
            return "H";
        else
            return lengthStatus switch
            {
                LengthStatus.Fixed => "D",
                LengthStatus.Range => "F",
                LengthStatus.NonFixed => "L",
                _ => "D"
            };
    }

    private static void ValidateSubNo(LengthStatus lengthStatus, string? productionSubNo)
    {
        if (lengthStatus == LengthStatus.Fixed)
        {
            if (string.IsNullOrEmpty(productionSubNo))
                throw new BusinessException("定尺模式下次号不能为空");
            if (!System.Text.RegularExpressions.Regex.IsMatch(productionSubNo, @"^C\d{2}$"))
                throw new BusinessException($"次号格式必须为C+两位数字，当前值：{productionSubNo}");
        }
        else
        {
            if (!string.IsNullOrEmpty(productionSubNo))
                throw new BusinessException($"{GetLengthStatusText(lengthStatus)}模式下不允许填写次号");
        }
    }

    public async Task<List<GeneratedWorkOrderDto>> GenerateWorkOrdersAsync(CreateWorkOrderRequest request)
    {
        // 使用信号量确保同一时间只有一个工单生成操作
        await _workOrderNoSemaphore.WaitAsync();
        try
        {
            return await GenerateWorkOrdersCoreAsync(request);
        }
        finally
        {
            _workOrderNoSemaphore.Release();
        }
    }

    private async Task<List<GeneratedWorkOrderDto>> GenerateWorkOrdersCoreAsync(CreateWorkOrderRequest request)
    {
        // 1. 获取订单信息
        var salesOrder = await _context.SalesOrders
            .Include(so => so.Customer)
            .FirstOrDefaultAsync(so => so.OrderNumber == request.SalesOrderNo && !so.IsDeleted);
        
        if (salesOrder == null)
            throw new BusinessException($"订单 {request.SalesOrderNo} 不存在");
        
        if (salesOrder.Status != SalesOrderStatus.Confirmed)
            throw new BusinessException($"订单 {request.SalesOrderNo} 状态不是已确认，无法生成工单");

        // 2. 获取订单项次
        var allOrderItems = await _context.OrderItems
            .Include(oi => oi.ProductionStandard)
            .Include(oi => oi.ProductRequirement)
            .Where(oi => oi.SalesOrderId == salesOrder.Id && !oi.IsDeleted)
            .ToDictionaryAsync(oi => oi.Id, oi => oi);

        // 3. 验证项次
        foreach (var workOrderGroup in request.WorkOrders)
        {
            foreach (var itemId in workOrderGroup.OrderItemIds)
            {
                if (!allOrderItems.ContainsKey(itemId))
                    throw new BusinessException($"项次 ID {itemId} 不存在或已被删除");
            }
        }

        // 4. 验证合并规则
        var mergeFieldErrors = new List<string>();
        foreach (var workOrderGroup in request.WorkOrders)
        {
            var groupItems = workOrderGroup.OrderItemIds
                .Select(id => allOrderItems.GetValueOrDefault(id))
                .Where(item => item != null)
                .ToList();
            if (!groupItems.Any())
                throw new BusinessException($"工单分组 {workOrderGroup.ProductionMainNo} 没有有效的项次");
            if (groupItems.Count <= 1) continue;

            var firstItem = groupItems.First()!;
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

        // 5. 使用事务
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 6. 物理删除现有工单
            var existingWorkOrders = await _context.WorkOrders
                .Where(wo => wo.SalesOrderNo == request.SalesOrderNo)
                .ToListAsync();
            
            _logger.LogInformation("订单 {OrderNo} 原有工单数量: {Count}，执行物理删除", 
                request.SalesOrderNo, existingWorkOrders.Count);
            
            _context.WorkOrders.RemoveRange(existingWorkOrders);
            await _context.SaveChangesAsync();
            
            // 清除缓存，确保查询最新数据
            _context.ChangeTracker.Clear();
            
            // 7. 获取下一个可用序号
            var workOrderDate = DateTime.Now;
            var dateStr = workOrderDate.ToString("yyyyMMdd");
            var prefix = $"WO{dateStr}";
            
            // 获取当天所有工单号
            var existingNos = await _context.WorkOrders
                .AsNoTracking()
                .Where(wo => wo.WorkOrderNo.StartsWith(prefix))
                .Select(wo => wo.WorkOrderNo)
                .ToListAsync();
            
            int maxSeq = 0;
            foreach (var no in existingNos)
            {
                if (no.Length >= 4 && int.TryParse(no.Substring(no.Length - 4), out var seq))
                {
                    if (seq > maxSeq) maxSeq = seq;
                }
            }
            
            int currentSeq = maxSeq + 1;
            
            if (currentSeq > 9999)
                throw new BusinessException($"当天工单号已达到上限9999，无法生成新工单");
            
            _logger.LogWarning("=== 工单序号分配 ===");
            _logger.LogWarning($"日期前缀: {prefix}");
            _logger.LogWarning($"当前最大序号: {maxSeq}");
            _logger.LogWarning($"起始序号: {currentSeq}");
            
            var workOrdersToAdd = new List<WorkOrder>();
            var generatedWorkOrders = new List<GeneratedWorkOrderDto>();
            
            foreach (var workOrderGroup in request.WorkOrders)
            {
                var groupItems = workOrderGroup.OrderItemIds
                    .Select(id => allOrderItems.GetValueOrDefault(id))
                    .Where(item => item != null)
                    .ToList();
                if (!groupItems.Any()) continue;

                var firstItem = groupItems.First()!;
                
                ValidateSubNo(firstItem.LengthStatus, workOrderGroup.ProductionSubNo);

                var (minLength, maxLength, totalQuantity, totalMeters, totalWeight, itemDetails, technicalRequirements) =
                    CalculateAggregates(groupItems!, firstItem.LengthStatus);

                var workOrderNo = $"{prefix}{currentSeq:D4}";
                currentSeq++;

                _logger.LogWarning($"生成工单号: {workOrderNo}");

                decimal? finalMaxLength = maxLength;
                if (firstItem.LengthStatus == LengthStatus.Fixed && minLength.HasValue)
                {
                    finalMaxLength = minLength;
                }

                var workOrder = new WorkOrder
                {
                    WorkOrderNo = workOrderNo,
                    SalesOrderNo = request.SalesOrderNo,
                    ProductionMainNo = workOrderGroup.ProductionMainNo,
                    ProductionSubNo = workOrderGroup.ProductionSubNo,
                    OrderItemIds = string.Join(",", workOrderGroup.OrderItemIds),
                    Status = 1,
                    SignDate = salesOrder.SignDate,
                    Salesman = salesOrder.Customer?.Salesman ?? string.Empty,
                    EndCustomer = salesOrder.Customer?.EndCustomer,
                    DeliveryDate = firstItem.DeliveryDate,
                    DelayPenalty = firstItem.DelayPenalty,
                    MaterialName = firstItem.MaterialName.ToString(),
                    SettlementMethod = firstItem.SettlementMethod.ToString(),
                    StandardCode = firstItem.ProductionStandard?.StandardCode ?? string.Empty,
                    DeliveryState = firstItem.DeliveryState.ToString(),
                    PlantGrade = firstItem.PlantGrade,
                    Specification = firstItem.Specification,
                    OuterDiameterMinus = firstItem.OuterDiameterNegative,
                    OuterDiameterPlus = firstItem.OuterDiameterPositive,
                    WallThicknessMinus = firstItem.WallThicknessNegative,
                    WallThicknessPlus = firstItem.WallThicknessPositive,
                    LengthStatus = firstItem.LengthStatus.ToString(),
                    MinLength = minLength,
                    MaxLength = finalMaxLength,
                    TotalQuantity = totalQuantity,
                    TotalMeters = totalMeters,
                    TotalWeight = totalWeight,
                    TotalItemCount = groupItems.Count,
                    ItemDetails = itemDetails,
                    TechnicalRequirements = technicalRequirements
                };

                workOrdersToAdd.Add(workOrder);

                generatedWorkOrders.Add(new GeneratedWorkOrderDto
                {
                    Id = 0,
                    WorkOrderNo = workOrderNo,
                    SalesOrderNo = request.SalesOrderNo,
                    ProductionMainNo = workOrderGroup.ProductionMainNo,
                    ProductionSubNo = workOrderGroup.ProductionSubNo,
                    Status = 1,
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

            await transaction.CommitAsync();
            
            _logger.LogInformation("生成工单成功: 订单号 {OrderNo}, 生成 {Count} 个工单",
                request.SalesOrderNo, generatedWorkOrders.Count);

            return generatedWorkOrders;
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

    private (decimal? MinLength, decimal? MaxLength, int TotalQuantity, decimal TotalMeters,
             decimal TotalWeight, string? ItemDetails, string TechnicalRequirements)
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
                    itemDetailsBuilder.Append($"{item.Sequence}项,{item.MaxLength}mm,{item.Quantity}支;");
                }
            }
            else
            {
                totalWeight += item.ContractWeight;
            }
        }

        var technicalRequirements = hasSpecialRequirement ? "Special" : "Normal";

        return (minLength, maxLength, totalQuantity, totalMeters, totalWeight,
                itemDetailsBuilder.Length > 0 ? itemDetailsBuilder.ToString() : null, technicalRequirements);
    }

    #endregion

    #region 工单管理

    public async Task<PagedResult<WorkOrderListDto>> GetPagedAsync(WorkOrderQueryParams query)
    {
        // 工单使用物理删除，不需要 IsDeleted 过滤
        var workOrderQuery = _context.WorkOrders.AsQueryable();

        if (!string.IsNullOrEmpty(query.SalesOrderNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.SalesOrderNo.Contains(query.SalesOrderNo));
        if (!string.IsNullOrEmpty(query.ProductionMainNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.ProductionMainNo.Contains(query.ProductionMainNo));
        if (!string.IsNullOrEmpty(query.ProductionSubNo))
            workOrderQuery = workOrderQuery.Where(wo => wo.ProductionSubNo != null && wo.ProductionSubNo.Contains(query.ProductionSubNo));
        if (query.Status.HasValue)
            workOrderQuery = workOrderQuery.Where(wo => wo.Status == query.Status.Value);
        if (!string.IsNullOrEmpty(query.MaterialName))
            workOrderQuery = workOrderQuery.Where(wo => wo.MaterialName.Contains(query.MaterialName));
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
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword;
            workOrderQuery = workOrderQuery.Where(wo =>
                wo.WorkOrderNo.Contains(keyword) ||
                wo.SalesOrderNo.Contains(keyword) ||
                wo.ProductionMainNo.Contains(keyword) ||
                (wo.ProductionSubNo != null && wo.ProductionSubNo.Contains(keyword)) ||
                wo.MaterialName.Contains(keyword) ||
                wo.Specification.Contains(keyword));
        }

        var totalCount = await workOrderQuery.CountAsync();

        if (!string.IsNullOrEmpty(query.SortBy))
        {
            switch (query.SortBy.ToLower())
            {
                case "workorderno":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.WorkOrderNo) : workOrderQuery.OrderBy(wo => wo.WorkOrderNo);
                    break;
                case "salesorderno":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.SalesOrderNo) : workOrderQuery.OrderBy(wo => wo.SalesOrderNo);
                    break;
                case "deliverydate":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.DeliveryDate) : workOrderQuery.OrderBy(wo => wo.DeliveryDate);
                    break;
                case "status":
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.Status) : workOrderQuery.OrderBy(wo => wo.Status);
                    break;
                default:
                    workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.CreatedTime) : workOrderQuery.OrderBy(wo => wo.CreatedTime);
                    break;
            }
        }
        else
        {
            workOrderQuery = query.IsDescending ? workOrderQuery.OrderByDescending(wo => wo.CreatedTime) : workOrderQuery.OrderBy(wo => wo.CreatedTime);
        }

        var workOrders = await workOrderQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var items = workOrders.Select(wo => new WorkOrderListDto
        {
            Id = wo.Id,
            WorkOrderNo = wo.WorkOrderNo,
            SalesOrderNo = wo.SalesOrderNo,
            ProductionMainNo = wo.ProductionMainNo,
            ProductionSubNo = wo.ProductionSubNo,
            MaterialName = wo.MaterialName,
            Specification = wo.Specification,
            DeliveryDate = wo.DeliveryDate,
            TotalQuantity = wo.TotalQuantity,
            TotalWeight = wo.TotalWeight,
            Status = wo.Status,
            CreatedTime = wo.CreatedTime
        }).ToList();

        return new PagedResult<WorkOrderListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<WorkOrderDetailDto> GetByIdAsync(int id)
    {
        var workOrder = await _context.WorkOrders
            .FirstOrDefaultAsync(wo => wo.Id == id);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        return new WorkOrderDetailDto
        {
            Id = workOrder.Id,
            WorkOrderNo = workOrder.WorkOrderNo,
            SalesOrderNo = workOrder.SalesOrderNo,
            ProductionMainNo = workOrder.ProductionMainNo,
            ProductionSubNo = workOrder.ProductionSubNo,
            OrderItemIds = workOrder.OrderItemIds,
            Status = workOrder.Status,
            SignDate = workOrder.SignDate,
            Salesman = workOrder.Salesman,
            EndCustomer = workOrder.EndCustomer,
            DeliveryDate = workOrder.DeliveryDate,
            DelayPenalty = workOrder.DelayPenalty,
            MaterialName = workOrder.MaterialName,
            SettlementMethod = workOrder.SettlementMethod,
            StandardCode = workOrder.StandardCode,
            DeliveryState = workOrder.DeliveryState,
            PlantGrade = workOrder.PlantGrade,
            Specification = workOrder.Specification,
            OuterDiameterMinus = workOrder.OuterDiameterMinus,
            OuterDiameterPlus = workOrder.OuterDiameterPlus,
            WallThicknessMinus = workOrder.WallThicknessMinus,
            WallThicknessPlus = workOrder.WallThicknessPlus,
            LengthStatus = workOrder.LengthStatus,
            MinLength = workOrder.MinLength,
            MaxLength = workOrder.MaxLength,
            TotalQuantity = workOrder.TotalQuantity,
            TotalMeters = workOrder.TotalMeters,
            TotalWeight = workOrder.TotalWeight,
            TotalItemCount = workOrder.TotalItemCount,
            ItemDetails = workOrder.ItemDetails,
            TechnicalRequirements = workOrder.TechnicalRequirements,
            RowVersion = workOrder.RowVersion,
            CreatedTime = workOrder.CreatedTime,
            CreatedBy = workOrder.CreatedBy,
            UpdatedTime = workOrder.UpdatedTime,
            UpdatedBy = workOrder.UpdatedBy
        };
    }

    public async Task<List<WorkOrderListDto>> GetBySalesOrderNoAsync(string salesOrderNo)
    {
        var workOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == salesOrderNo)
            .OrderBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToListAsync();

        return workOrders.Select(wo => new WorkOrderListDto
        {
            Id = wo.Id,
            WorkOrderNo = wo.WorkOrderNo,
            SalesOrderNo = wo.SalesOrderNo,
            ProductionMainNo = wo.ProductionMainNo,
            ProductionSubNo = wo.ProductionSubNo,
            MaterialName = wo.MaterialName,
            Specification = wo.Specification,
            DeliveryDate = wo.DeliveryDate,
            TotalQuantity = wo.TotalQuantity,
            TotalWeight = wo.TotalWeight,
            Status = wo.Status,
            CreatedTime = wo.CreatedTime
        }).ToList();
    }

    public async Task<List<OrderItemForWorkOrderDto>> GetWorkOrderItemsAsync(int workOrderId)
    {
        var workOrder = await _context.WorkOrders
            .FirstOrDefaultAsync(wo => wo.Id == workOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var itemIds = workOrder.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse)
                        .ToList();

        var orderItems = await _context.OrderItems
            .Where(oi => itemIds.Contains(oi.Id) && !oi.IsDeleted)
            .OrderBy(oi => oi.Sequence)
            .Select(oi => new OrderItemForWorkOrderDto
            {
                Id = oi.Id,
                Sequence = oi.Sequence,
                MaterialName = oi.MaterialName.ToString(),
                Specification = oi.Specification,
                LengthStatus = oi.LengthStatus.ToString(),
                MinLength = oi.MinLength,
                MaxLength = oi.MaxLength,
                Quantity = oi.Quantity,
                Meters = oi.Meters,
                ContractWeight = oi.ContractWeight,
                TheoreticalWeight = oi.TheoreticalWeight            })
            .ToListAsync();

        return orderItems;
    }

    public async Task<UpdateWorkOrderStatusResponseDto> UpdateStatusAsync(int id, UpdateWorkOrderStatusRequest request)
    {
        var workOrder = await _context.WorkOrders
            .FirstOrDefaultAsync(wo => wo.Id == id);
        if (workOrder == null)
            throw new BusinessException("工单不存在");
        if (!CanTransitionTo(workOrder.Status, request.Status))
            throw new BusinessException($"不允许从 {GetStatusText(workOrder.Status)} 变更为 {GetStatusText(request.Status)}");

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

        _logger.LogInformation("更新工单状态成功: 工单号 {WorkOrderNo}, 新状态 {Status}",
            workOrder.WorkOrderNo, request.Status);
        return new UpdateWorkOrderStatusResponseDto { Id = workOrder.Id, Status = workOrder.Status };
    }

    public async Task DeleteAsync(int id)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder == null)
            throw new BusinessException("工单不存在");
        _context.WorkOrders.Remove(workOrder);
        await _context.SaveChangesAsync();
        _logger.LogInformation("删除工单成功: 工单号 {WorkOrderNo}", workOrder.WorkOrderNo);
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
        await CheckAndUpdateWorkOrderStatusInternalAsync(salesOrderId);
        await _context.SaveChangesAsync();
    }

    public async Task CheckAllOrdersChangeAsync()
    {
        _logger.LogInformation("开始执行订单变更检测定时任务");
        var confirmedOrders = await _context.SalesOrders
            .Where(so => so.Status == SalesOrderStatus.Confirmed && !so.IsDeleted)
            .Select(so => new { so.Id, so.OrderNumber, so.LastItemChangeTime })
            .ToListAsync();

        int updatedCount = 0;
        foreach (var order in confirmedOrders)
        {
            if (await CheckAndUpdateWorkOrderStatusInternalAsync(order.Id))
                updatedCount++;
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("订单变更检测完成，共更新 {Count} 个订单的工单状态", updatedCount);
    }

    private async Task<bool> CheckAndUpdateWorkOrderStatusInternalAsync(int salesOrderId)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == salesOrderId && !so.IsDeleted);
        if (salesOrder == null || salesOrder.Status != SalesOrderStatus.Confirmed)
            return false;

        var workOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == salesOrder.OrderNumber && wo.Status != 3)
            .ToListAsync();

        if (!workOrders.Any())
            return false;

        bool hasChange = false;
        foreach (var workOrder in workOrders)
        {
            if (salesOrder.LastItemChangeTime.HasValue && salesOrder.LastItemChangeTime > workOrder.CreatedTime)
            {
                hasChange = true;
                break;
            }
        }

        if (hasChange && workOrders.All(wo => wo.Status != 2))
        {
            foreach (var workOrder in workOrders)
            {
                if (workOrder.Status == 1)
                    workOrder.Status = 2;
            }
            _logger.LogInformation("订单 {OrderNumber} 发生项次变更，关联工单状态已更新为待修正", salesOrder.OrderNumber);
            return true;
        }
        return false;
    }

    #endregion

    #region 订单工单项次追溯

    public async Task<OrderWorkOrderRelationDto> GetOrderWorkOrderRelationAsync(string salesOrderNo)
    {
        // 1. 获取订单信息
        var salesOrderQuery = await _context.SalesOrders
            .Where(so => so.OrderNumber == salesOrderNo && !so.IsDeleted)
            .Join(_context.CustomerProfiles.Where(c => !c.IsDeleted),
                so => so.CustomerId,
                c => c.Id,
                (so, c) => new { SalesOrder = so, Customer = c })
            .FirstOrDefaultAsync();

        if (salesOrderQuery == null)
            throw new BusinessException($"订单 {salesOrderNo} 不存在");

        var salesOrder = salesOrderQuery.SalesOrder;
        var customer = salesOrderQuery.Customer;

        // 2. 获取该订单下的所有工单（状态不为已取消的工单）
        var workOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == salesOrderNo && wo.Status != 3)
            .OrderBy(wo => wo.ProductionMainNo)
            .ThenBy(wo => wo.ProductionSubNo)
            .ToListAsync();

        // 3. 收集所有工单包含的项次ID
        var allOrderItemIds = new List<int>();
        foreach (var wo in workOrders)
        {
            var ids = wo.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(int.Parse);
            allOrderItemIds.AddRange(ids);
        }

        // 4. 批量查询订单项次（一次性加载所有相关项次）
        var orderItems = await _context.OrderItems
            .Where(oi => allOrderItemIds.Contains(oi.Id) && !oi.IsDeleted)
            .ToDictionaryAsync(oi => oi.Id, oi => oi);

        // 5. 构建 DTO
        var result = new OrderWorkOrderRelationDto
        {
            SalesOrderId = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            Salesman = customer.Salesman,
            CustomerName = customer.CustomerUnit,
            EndCustomer = customer.EndCustomer,
            WorkOrders = new List<WorkOrderRelationDto>()
        };

        foreach (var wo in workOrders)
        {
            var itemIds = wo.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(int.Parse)
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
    LengthStatus = item.LengthStatus.ToString(),
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
    MaterialName = wo.MaterialName,
    PlantGrade = wo.PlantGrade,           
    Specification = wo.Specification,
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

    #endregion

    #region 辅助方法

    private static bool CanTransitionTo(int currentStatus, int targetStatus)
    {
        if (currentStatus == targetStatus) return true;
        if (currentStatus == 3) return false;
        if (currentStatus == 0) return targetStatus == 1;
        if (currentStatus == 1) return targetStatus == 2 || targetStatus == 3;
        if (currentStatus == 2) return targetStatus == 1 || targetStatus == 3;
        return false;
    }

    private static string GetStatusText(int status)
    {
        return status switch
        {
            0 => "未编制",
            1 => "已确定",
            2 => "待修正",
            3 => "已取消",
            _ => "未知"
        };
    }

    private static string GetLengthStatusText(LengthStatus status)
    {
        return status switch
        {
            LengthStatus.Fixed => "定尺",
            LengthStatus.Range => "范围尺",
            LengthStatus.NonFixed => "非定尺",
            _ => status.ToString()
        };
    }

    #endregion
}