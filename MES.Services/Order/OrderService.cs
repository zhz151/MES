// 文件路径: MES.Services/Order/OrderService.cs
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
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Order;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Order;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OrderService> _logger;
    private readonly INotificationService _notificationService;
    private readonly IConfigParameterService _configService;
    private readonly IWorkOrderService? _workOrderService;
    private readonly IWorkOrderListSummaryRefreshService? _listSummaryService;
    private readonly IOperationLogService _operationLogService;
    private readonly IMemoryCache _cache;

    public OrderService(AppDbContext context, ILogger<OrderService> logger, INotificationService notificationService, IConfigParameterService configService, IOperationLogService operationLogService, IMemoryCache cache, IWorkOrderService? workOrderService = null, IWorkOrderListSummaryRefreshService? listSummaryService = null)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
        _configService = configService;
        _workOrderService = workOrderService;
        _listSummaryService = listSummaryService;
        _operationLogService = operationLogService;
        _cache = cache;
    }

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        var cacheKey = $"OrderService:ConfigMap:{category}";
        var map = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _configService.GetConfigMapAsync(category);
        });
        return map?.GetValueOrDefault(key, defaultValue) ?? defaultValue;
    }

    #region 订单管理

    public async Task<PagedResult<SalesOrderListDto>> GetPagedAsync(QueryParams query, string? technicalStatus = null, string? orderStatus = null, DateTime? signDateFrom = null, DateTime? signDateTo = null, DateTime? deliveryDateFrom = null, DateTime? deliveryDateTo = null)
    {
        bool? hasTechnicalRequirement = technicalStatus?.ToLower() switch
        {
            "edited" => true,
            "notedited" => false,
            _ => null
        };

        List<SalesOrderStatus>? statuses = null;
        if (!string.IsNullOrEmpty(orderStatus))
        {
            var statusStrings = orderStatus.Split(',', StringSplitOptions.RemoveEmptyEntries);
            statuses = new List<SalesOrderStatus>();
            foreach (var s in statusStrings)
            {
                if (Enum.TryParse<SalesOrderStatus>(s, true, out var status))
                    statuses.Add(status);
            }
        }

        var queryable = _context.Set<OrderListSummary>().AsQueryable();

        // 订单状态筛选
        if (statuses == null || !statuses.Any())
        {
            statuses = new List<SalesOrderStatus> { SalesOrderStatus.Pending, SalesOrderStatus.Confirmed };
        }
        queryable = queryable.Where(s => statuses.Contains(s.Status));

        // 签订日期范围筛选
        if (signDateFrom.HasValue)
            queryable = queryable.Where(s => s.SignDate >= signDateFrom.Value);
        if (signDateTo.HasValue)
            queryable = queryable.Where(s => s.SignDate <= signDateTo.Value);

        // 交货日期范围筛选
        if (deliveryDateFrom.HasValue)
            queryable = queryable.Where(s => s.DeliveryStart >= deliveryDateFrom.Value);
        if (deliveryDateTo.HasValue)
            queryable = queryable.Where(s => s.DeliveryStart <= deliveryDateTo.Value);

        // 关键字模糊搜索（多关键词AND + 状态中文映射）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                SalesOrderStatus? parsedStatus = keyword switch
                {
                    "待处理" => SalesOrderStatus.Pending,
                    "已确认" => SalesOrderStatus.Confirmed,
                    _ => null
                };
                queryable = queryable.Where(s =>
                    s.OrderNumber.Contains(keyword) ||
                    s.CustomerName.Contains(keyword) ||
                    s.Salesman.Contains(keyword) ||
                    (s.EndCustomer != null && s.EndCustomer.Contains(keyword)) ||
                    (parsedStatus.HasValue && s.Status == parsedStatus.Value) ||
                    (keyword == "是" && s.HasDelayPenalty) ||
                    (keyword == "否" && !s.HasDelayPenalty));
            }
        }

        // 技术要求状态筛选
        if (hasTechnicalRequirement.HasValue)
        {
            if (hasTechnicalRequirement.Value)
            {
                // 已编辑：有项次且所有项次都有技术要求（HasTechReqCount == ItemCount）
                queryable = queryable.Where(s => s.ItemCount > 0 && s.HasTechReqCount == s.ItemCount);
            }
            else
            {
                // 未编辑：至少有一个项次没有技术要求
                queryable = queryable.Where(s => s.HasTechReqCount < s.ItemCount);
            }
        }

        // schedulestage 含空值（未排产）逻辑，由 ApplyComputedFieldFilters 统一处理
        var noStageFilters = query.Filters?
            .Where(f => !string.Equals(f.Field, "schedulestage", StringComparison.OrdinalIgnoreCase))
            .ToList();
        queryable = queryable.ApplyFilters(noStageFilters);
        queryable = ApplyComputedFieldFilters(queryable, query.Filters);

        var totalCount = await queryable.CountAsync();

        // 排序
        if (!string.IsNullOrEmpty(query.SortBy))
        {
            switch (query.SortBy.ToLower())
            {
                case "ordernumber":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.OrderNumber) : queryable.OrderBy(s => s.OrderNumber);
                    break;
                case "signdate":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.SignDate) : queryable.OrderBy(s => s.SignDate);
                    break;
                case "status":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.Status) : queryable.OrderBy(s => s.Status);
                    break;
                case "salesman":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.Salesman) : queryable.OrderBy(s => s.Salesman);
                    break;
                case "customername":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.CustomerName) : queryable.OrderBy(s => s.CustomerName);
                    break;
                case "endcustomer":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.EndCustomer ?? "") : queryable.OrderBy(s => s.EndCustomer ?? "");
                    break;
                case "deliverystart":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.DeliveryStart) : queryable.OrderBy(s => s.DeliveryStart);
                    break;
                case "deliveryend":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.DeliveryEnd) : queryable.OrderBy(s => s.DeliveryEnd);
                    break;
                case "hasdelaypenalty":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.HasDelayPenalty) : queryable.OrderBy(s => s.HasDelayPenalty);
                    break;
                case "totalcontractweight":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.TotalContractWeight) : queryable.OrderBy(s => s.TotalContractWeight);
                    break;
                case "itemcount":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.ItemCount) : queryable.OrderBy(s => s.ItemCount);
                    break;
                case "lastchangedate":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.LastChangeDate) : queryable.OrderBy(s => s.LastChangeDate);
                    break;
                case "schedulestage":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.ScheduleStage) : queryable.OrderBy(s => s.ScheduleStage);
                    break;
                case "urgencylevel":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.UrgencyLevel ?? "") : queryable.OrderBy(s => s.UrgencyLevel ?? "");
                    break;
                case "estimatedcompletiondate":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.EstimatedCompletionDate) : queryable.OrderBy(s => s.EstimatedCompletionDate);
                    break;
                case "finishedinboundweight":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.FinishedInboundWeight) : queryable.OrderBy(s => s.FinishedInboundWeight);
                    break;
                case "finishedoutboundweight":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.FinishedOutboundWeight) : queryable.OrderBy(s => s.FinishedOutboundWeight);
                    break;
                case "finishedstockweight":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.FinishedStockWeight) : queryable.OrderBy(s => s.FinishedStockWeight);
                    break;
                case "businesscompleted":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.BusinessCompleted) : queryable.OrderBy(s => s.BusinessCompleted);
                    break;
                case "hastechnicalrequirement":
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.ItemCount > 0 && s.HasTechReqCount == s.ItemCount) : queryable.OrderBy(s => s.ItemCount > 0 && s.HasTechReqCount == s.ItemCount);
                    break;
                default:
                    queryable = query.IsDescending ? queryable.OrderByDescending(s => s.SignDate) : queryable.OrderBy(s => s.SignDate);
                    break;
            }
        }
        else
        {
            queryable = queryable.OrderByDescending(s => s.SignDate);
        }

        var summaries = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        // 批量加载 SalesOrder 的 RowVersion（OrderListSummary 的 RowVersion 是读模型自身的 rowversion，与业务表不同）
        var orderIds = summaries.Select(s => s.OrderId).ToList();
        var salesOrderRowVersions = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => orderIds.Contains(so.Id))
            .ToDictionaryAsync(so => so.Id, so => so.RowVersion);

        var items = summaries.Select(s => new SalesOrderListDto
        {
            Id = s.OrderId,
            OrderNumber = s.OrderNumber,
            SignDate = s.SignDate,
            CustomerName = s.CustomerName,
            Salesman = s.Salesman,
            EndCustomer = s.EndCustomer,
            DeliveryStart = s.DeliveryStart,
            DeliveryEnd = s.DeliveryEnd,
            HasDelayPenalty = s.HasDelayPenalty,
            TotalContractWeight = s.TotalContractWeight,
            ItemCount = s.ItemCount,
            Status = s.Status,
            RowVersion = salesOrderRowVersions.GetValueOrDefault(s.OrderId) ?? Array.Empty<byte>(),
            HasTechnicalRequirement = s.ItemCount > 0 && s.HasTechReqCount == s.ItemCount,
            FirstOrderItemId = s.FirstOrderItemId,
            LastChangeDate = s.LastChangeDate,
            ScheduleStage = s.ScheduleStage,
            UrgencyLevel = s.UrgencyLevel,
            EstimatedCompletionDate = s.EstimatedCompletionDate,
            FinishedInboundWeight = s.FinishedInboundWeight,
            FinishedOutboundWeight = s.FinishedOutboundWeight,
            FinishedStockWeight = s.FinishedStockWeight,
            BusinessCompleted = s.BusinessCompleted
        }).ToList();

        return new PagedResult<SalesOrderListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    /// <summary>
    /// 获取订单接单·出库及现负荷汇总（本年按月：接单量/出库量；当前存量：成品库存(完工/未完工)/订单负荷量(实时)）
    /// 口径：
    /// 1. 接单量 = 本年签订、排除已取消订单的合同重量，按签订月份分布 12 列
    /// 2. 出库量 = 本年成品销售出库（OutboundType=SalesOut 且批次 MaterialType=OrderFinished）重量，按出库月份分布 12 列
    /// 3. 成品库存(完工/未完工) = 所有年份、排除已取消订单的成品库存量（FinishedStockWeight），按执行关注 ScheduleStage==1（主号完成）分档，为当前存量，非月度累计
    /// 4. 订单负荷量(实时) = 执行关注&lt;&gt;主号完成 的订单合同重量 − 成品库存(未完工)，为当前存量，非月度累计
    /// </summary>
    public async Task<OrderInOutSummaryDto> GetOrderInOutSummaryAsync(int year)
    {
        var yearStart = new DateTime(year, 1, 1);
        var nextYearStart = new DateTime(year + 1, 1, 1);

        // 订单数据：所有年份、排除已取消（接单量仅取本年签订部分）
        var orders = await _context.Set<OrderListSummary>()
            .Where(s => s.Status != SalesOrderStatus.Cancelled)
            .Select(s => new { s.SignDate, s.TotalContractWeight, s.ScheduleStage, s.FinishedStockWeight })
            .ToListAsync();

        var orderWeightByMonth = new decimal[12];
        var finishedStockCompleted = 0m;
        var finishedStockUncompleted = 0m;
        var uncompletedOrderWeight = 0m;
        foreach (var o in orders)
        {
            if (o.SignDate >= yearStart && o.SignDate < nextYearStart)
                orderWeightByMonth[o.SignDate.Month - 1] += o.TotalContractWeight;

            if (o.ScheduleStage == 1)
                finishedStockCompleted += o.FinishedStockWeight;
            else
            {
                finishedStockUncompleted += o.FinishedStockWeight;
                uncompletedOrderWeight += o.TotalContractWeight;
            }
        }

        // 出库量：本年成品销售出库（SalesOut + OrderFinished 批次）
        var outbound = await (
            from r in _context.OutboundRecords
            join ib in _context.InventoryBatches on r.InventoryBatchId equals ib.Id
            where r.OutboundType == OutboundType.SalesOut
                && ib.MaterialType == InventoryMaterialTypes.OrderFinished
                && r.OutboundDate >= yearStart && r.OutboundDate < nextYearStart
            select new { Month = r.OutboundDate.Month, r.OutboundWeight })
            .ToListAsync();

        var outboundWeightByMonth = new decimal[12];
        foreach (var o in outbound)
            outboundWeightByMonth[o.Month - 1] += o.OutboundWeight;

        return new OrderInOutSummaryDto
        {
            Year = year,
            MonthLabels = Enumerable.Range(1, 12).Select(m => $"{year}年{m}月").ToArray(),
            OrderWeightByMonth = orderWeightByMonth,
            OutboundWeightByMonth = outboundWeightByMonth,
            FinishedStockCompleted = finishedStockCompleted,
            FinishedStockUncompleted = finishedStockUncompleted,
            TurnoverTotal = uncompletedOrderWeight - finishedStockUncompleted
        };
    }

    /// <summary>
    /// 订单交期预估（业务总况两小表，订单级口径，2026-08-23 用户决策）
    /// 数据源：OrderListSummary（一行一订单）。单数按订单号、重量按订单总重量（合同重量 kg）。
    /// 参与范围：已排产订单（ScheduleStage≥2 且预计完成非空）；延期判定：预计完成日 > 交期截止（DeliveryEnd）。
    /// 表1「订单(整单)完成预估」＝延期订单按预计完成日归桶 + 非延期订单按交期截止归桶（对应现负荷「订单延期量[预计完结]+订单非延期」）；
    /// 表2「风险-已延期订单(整单)」＝延期订单按交期截止归桶（对应现负荷「订单延期量」）。
    /// 7 桶边界：≤今日 / +1~+7 / +8~+15 / +16~+30 / +31~+45 / +46~+60 / >+60，桶标签为绝对日期区间（yy/M/d）。
    /// 桶边界走配置表 DateBucket（Bucket1~Bucket5，与现负荷总量表同源，2026-08-23 用户决策复用）。
    /// </summary>
    public async Task<OrderDeliveryEstimateDto> GetDeliveryEstimateAsync()
    {
        var now = DateTime.Today;

        // 桶边界走配置表 DateBucket（与现负荷总量表同源）：默认 7/15/30/45/60
        var bucket1 = (int)await GetConfigAsync("DateBucket", "Bucket1", 7m);
        var bucket2 = (int)await GetConfigAsync("DateBucket", "Bucket2", 15m);
        var bucket3 = (int)await GetConfigAsync("DateBucket", "Bucket3", 30m);
        var bucket4 = (int)await GetConfigAsync("DateBucket", "Bucket4", 45m);
        var bucket5 = (int)await GetConfigAsync("DateBucket", "Bucket5", 60m);

        var orders = await _context.Set<OrderListSummary>()
            .AsNoTracking()
            .Where(s => s.Status != SalesOrderStatus.Cancelled
                        && s.ScheduleStage >= 2
                        && s.EstimatedCompletionDate != null
                        && s.DeliveryEnd != null)
            .Select(s => new
            {
                s.OrderNumber,
                DeliveryEnd = s.DeliveryEnd!.Value,
                Estimated = s.EstimatedCompletionDate!.Value,
                s.TotalContractWeight
            })
            .ToListAsync();

        var delayOrders = orders.Where(o => o.Estimated > o.DeliveryEnd).ToList();
        var onTimeOrders = orders.Where(o => o.Estimated <= o.DeliveryEnd).ToList();

        // 表2：延期交货订单预估（延期订单按交期截止归桶）
        var delayBuckets = new List<OrderDeliveryBucketDto>();
        for (var i = 0; i < 7; i++)
        {
            var subset = delayOrders.Where(o => GetDeliveryBucket(o.DeliveryEnd, now, bucket1, bucket2, bucket3, bucket4, bucket5) == i).ToList();
            delayBuckets.Add(new OrderDeliveryBucketDto
            {
                Count = subset.Count,
                Weight = subset.Sum(o => o.TotalContractWeight) / 1000m
            });
        }

        // 表1：订单完成预估（延期订单按预计完成日 + 非延期订单按交期截止归桶）
        var completeBuckets = new List<OrderDeliveryBucketDto>();
        for (var i = 0; i < 7; i++)
        {
            var d = delayOrders.Where(o => GetDeliveryBucket(o.Estimated, now, bucket1, bucket2, bucket3, bucket4, bucket5) == i).ToList();
            var t = onTimeOrders.Where(o => GetDeliveryBucket(o.DeliveryEnd, now, bucket1, bucket2, bucket3, bucket4, bucket5) == i).ToList();
            completeBuckets.Add(new OrderDeliveryBucketDto
            {
                Count = d.Count + t.Count,
                Weight = (d.Sum(o => o.TotalContractWeight) + t.Sum(o => o.TotalContractWeight)) / 1000m
            });
        }

        // 绝对日期桶标签（2026-08-23 用户决策，替代相对「今日+N」，边界按配置表）：≤今日 / +1~+7 / +8~+15 / +16~+30 / +31~+45 / +46~+60 / >+60
        var bucketLabels = new List<string> { $"≤{now:yy/M/d}" };
        var bucketStart = now.AddDays(1);
        foreach (var endOffset in new[] { bucket1, bucket2, bucket3, bucket4, bucket5 })
        {
            var bucketEnd = now.AddDays(endOffset);
            bucketLabels.Add($"{bucketStart:yy/M/d}-{bucketEnd:yy/M/d}");
            bucketStart = bucketEnd.AddDays(1);
        }
        bucketLabels.Add($"≥{bucketStart:yy/M/d}");

        return new OrderDeliveryEstimateDto
        {
            Tables = new List<OrderDeliveryEstimateTableDto>
            {
                new()
                {
                    Name = "订单(整单)完成预估",
                    BucketLabels = bucketLabels,
                    Buckets = completeBuckets
                },
                new()
                {
                    Name = "风险-已延期订单(整单)",
                    BucketLabels = bucketLabels,
                    Buckets = delayBuckets
                }
            },
            GeneratedTime = now
        };
    }

    /// <summary>日期 → 桶索引（边界由配置表 DateBucket 驱动，默认 ≤今日=0 / 1-7 / 8-15 / 16-30 / 31-45 / 46-60 / &gt;60=6）</summary>
    private static int GetDeliveryBucket(DateTime date, DateTime today, int bucket1, int bucket2, int bucket3, int bucket4, int bucket5)
    {
        var days = (date.Date - today).Days;
        if (days <= 0) return 0;
        if (days <= bucket1) return 1;
        if (days <= bucket2) return 2;
        if (days <= bucket3) return 3;
        if (days <= bucket4) return 4;
        if (days <= bucket5) return 5;
        return 6;
    }

    public async Task<SalesOrderDetailDto> GetByIdAsync(int id)
    {
        // 1. 查订单头（无 Include，避免 LEFT JOIN 数据重复）
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.Id == id);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        // 2. 查项次（独立查询，不与订单头 JOIN）
        var orderItems = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.SalesOrderId == id)
            .ToListAsync();

        // 4. 加载标准号字典
        var standardNos = orderItems
            .Where(oi => !string.IsNullOrEmpty(oi.StandardNo))
            .Select(oi => oi.StandardNo)
            .Distinct()
            .ToList();
        var srDict = standardNos.Any()
            ? await _context.StandardRegisters
                .AsNoTracking()
                .Where(sr => standardNos.Contains(sr.StandardNo))
                .ToDictionaryAsync(sr => sr.StandardNo, sr => sr, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, MES.Data.Entities.StandardRegister.StandardRegister>(StringComparer.OrdinalIgnoreCase);

        // 5. 加载牌号映射
        var gradeDict = await LoadGradeMappingsDictAsync(orderItems);

        // 6. 映射 DTO
        return new SalesOrderDetailDto
        {
            Id = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            CustomerName = salesOrder.CustomerName,
            Salesman = salesOrder.Salesman,
            EndCustomer = salesOrder.EndCustomer,
            Status = salesOrder.Status,
            RowVersion = salesOrder.RowVersion,
            Items = orderItems.Select(oi =>
            {
                return new OrderItemDto
                {
                    Id = oi.Id,
                    Sequence = oi.Sequence,
                    DeliveryDate = oi.DeliveryDate,
                    DelayPenalty = oi.DelayPenalty,
                    SettlementMethod = oi.SettlementMethod,
                    PipeManufacturingType = oi.PipeManufacturingType,
                    StandardNo = oi.StandardNo ?? string.Empty,
                    DeliveryState = oi.DeliveryState,
                    StandardGrade = oi.StandardGrade,
                    PlantGrade = gradeDict.TryGetValue(oi.StandardGrade, out var gm) ? gm.PlantGrade : oi.PlantGrade,
                    Density = gradeDict.TryGetValue(oi.StandardGrade, out var gm2) ? gm2.Density : oi.Density,
                    OuterDiameter = oi.OuterDiameter,
                    WallThickness = oi.WallThickness,
                    Specification = oi.Specification,
                    OuterDiameterNegative = oi.OuterDiameterNegative,
                    OuterDiameterPositive = oi.OuterDiameterPositive,
                    WallThicknessNegative = oi.WallThicknessNegative,
                    WallThicknessPositive = oi.WallThicknessPositive,
                    LengthStatus = oi.LengthStatus,
                    MinLength = oi.MinLength,
                    MaxLength = oi.MaxLength,
                    Quantity = oi.Quantity,
                    Meters = oi.Meters,
                    ContractWeight = oi.ContractWeight,
                    TheoreticalWeight = oi.TheoreticalWeight,
                    Remark = oi.Remark,
                    CreatedTime = oi.CreatedTime,
                    UpdatedTime = oi.UpdatedTime
                };
            }).ToList()
        };
    }

    public async Task<int?> GetIdByOrderNumberAsync(string orderNo)
    {
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.OrderNumber == orderNo);
        return salesOrder?.Id;
    }

    public async Task<SalesOrderListDto> CreateAsync(CreateSalesOrderRequest request)
    {
        if (await _context.SalesOrders.AnyAsync(so => so.OrderNumber == request.OrderNumber))
            throw new BusinessException("订单号已存在");

        var customer = await _context.CustomerProfiles.FirstOrDefaultAsync(c => c.Id == request.CustomerId);
        if (customer == null)
            throw new BusinessException("客户不存在");

        var salesOrder = new SalesOrder
        {
            OrderNumber = request.OrderNumber,
            SignDate = request.SignDate,
            Status = SalesOrderStatus.Pending,
            CustomerName = customer.CustomerUnit,
            Salesman = customer.Salesman,
            EndCustomer = customer.EndCustomer
        };

        var sequence = 1;
        foreach (var itemRequest in request.Items)
        {
            var orderItem = await CreateOrderItemFromCreateRequestAsync(itemRequest, salesOrder.Id, sequence);
            salesOrder.OrderItems.Add(orderItem);
            sequence++;
        }

        _context.SalesOrders.Add(salesOrder);
        await _context.SaveChangesAsync();

        await RefreshByOrderIdAsync(salesOrder.Id);

        _logger.LogInformation("创建订单成功: {OrderNumber}", salesOrder.OrderNumber);

        await _operationLogService.AddLogAsync("Order", salesOrder.Id, "创建", $"订单号={salesOrder.OrderNumber}, 客户={salesOrder.CustomerName}");

        return new SalesOrderListDto
        {
            Id = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            CustomerName = salesOrder.CustomerName,
            Salesman = salesOrder.Salesman,
            EndCustomer = salesOrder.EndCustomer,
            Status = salesOrder.Status,
            RowVersion = salesOrder.RowVersion
        };
    }

    public async Task<SalesOrderListDto> UpdateAsync(int id, UpdateSalesOrderRequest request)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == id);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        if (!string.IsNullOrEmpty(request.OrderNumber) && request.OrderNumber != salesOrder.OrderNumber)
        {
            if (await _context.SalesOrders.AnyAsync(so => so.OrderNumber == request.OrderNumber && so.Id != id))
                throw new BusinessException("订单号已存在");
            salesOrder.OrderNumber = request.OrderNumber;
        }

        if (request.SignDate.HasValue)
            salesOrder.SignDate = request.SignDate.Value;

        if (request.CustomerName != null)
            salesOrder.CustomerName = request.CustomerName;

        if (request.Salesman != null)
            salesOrder.Salesman = request.Salesman;

        if (request.EndCustomer != null)
            salesOrder.EndCustomer = request.EndCustomer;

        if (request.Status.HasValue)
        {
            var newStatus = request.Status.Value;

            if (!CanTransitionTo(salesOrder.Status, newStatus))
                throw new BusinessException($"不允许从 {GetStatusText(salesOrder.Status)} 变更为 {GetStatusText(newStatus)}");

            salesOrder.Status = newStatus;
        }

        _context.Entry(salesOrder).Property(x => x.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("订单已被其他用户修改，请刷新后重试");
        }

        await RefreshByOrderIdAsync(salesOrder.Id);

        // 记录变更日志
        var updateChanges = new List<string>();
        if (!string.IsNullOrEmpty(request.OrderNumber) && request.OrderNumber != salesOrder.OrderNumber)
            updateChanges.Add($"订单号变更");
        if (request.Status.HasValue)
            updateChanges.Add($"状态: {GetStatusText(request.Status.Value)}");
        if (updateChanges.Count > 0)
            await _operationLogService.AddLogAsync("Order", id, "变更", string.Join("; ", updateChanges));

        return new SalesOrderListDto
        {
            Id = salesOrder.Id,
            OrderNumber = salesOrder.OrderNumber,
            SignDate = salesOrder.SignDate,
            CustomerName = salesOrder.CustomerName,
            Salesman = salesOrder.Salesman,
            EndCustomer = salesOrder.EndCustomer,
            Status = salesOrder.Status,
            RowVersion = salesOrder.RowVersion
        };
    }

    public async Task DeleteAsync(int id)
    {
        var salesOrder = await _context.SalesOrders
            .Include(so => so.OrderItems)
                .ThenInclude(oi => oi.ProductRequirement)
            .FirstOrDefaultAsync(so => so.Id == id);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        // 1. 使用事务确保数据一致性（包含查询和写入）
        int workOrderCount = 0;
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                // 2. 物理删除订单（级联删除订单项次和产品要求）
                _context.SalesOrders.Remove(salesOrder);

                // 4. 物理删除关联工单（在事务内查询和删除，避免并发窗口）
                var workOrders = await _context.WorkOrders
                    .Where(wo => wo.SalesOrderNo == salesOrder.OrderNumber)
                    .ToListAsync();

                workOrderCount = workOrders.Count;

                if (workOrderCount > 0)
                {
                    // 先级联删除工单关联的用料计划（无FK约束，需手动清理）
                    var woIds = workOrders.Select(w => w.Id).ToList();
                    var workOrderNos = workOrders.Select(w => w.WorkOrderNo).ToHashSet();
                    var semiPlans = await _context.PurchaseSemiPlans.Where(p => woIds.Contains(p.WorkOrderId)).ToListAsync();
                    var finishPlans = await _context.PurchaseFinishedPlans.Where(p => woIds.Contains(p.WorkOrderId)).ToListAsync();
                    var invPlans = await _context.InventoryPlans.Where(p => woIds.Contains(p.WorkOrderId)).ToListAsync();
                    var piercingPlans = await _context.RoundBarPiercingPlans.Where(p => woIds.Contains(p.WorkOrderId)).ToListAsync();
                    var inProcessReworkPlans = await _context.InProcessReworkPlans.Where(p => woIds.Contains(p.WorkOrderId)).ToListAsync();
                    if (semiPlans.Any()) _context.PurchaseSemiPlans.RemoveRange(semiPlans);
                    if (finishPlans.Any()) _context.PurchaseFinishedPlans.RemoveRange(finishPlans);
                    if (invPlans.Any()) _context.InventoryPlans.RemoveRange(invPlans);
                    if (piercingPlans.Any()) _context.RoundBarPiercingPlans.RemoveRange(piercingPlans);
                    if (inProcessReworkPlans.Any()) _context.InProcessReworkPlans.RemoveRange(inProcessReworkPlans);

                    // 清理读模型行（事务内执行，避免残留脏数据）
                    var delListRows = await _context.Set<WorkOrderListSummary>()
                        .Where(s => woIds.Contains(s.WorkOrderId)).ToListAsync();
                    if (delListRows.Count != 0)
                        _context.Set<WorkOrderListSummary>().RemoveRange(delListRows);
                    var delExecRows = await _context.Set<WorkOrderExecutionSummary>()
                        .Where(s => woIds.Contains(s.WorkOrderId)).ToListAsync();
                    if (delExecRows.Count != 0)
                        _context.Set<WorkOrderExecutionSummary>().RemoveRange(delExecRows);

                    _context.WorkOrders.RemoveRange(workOrders);
                }

                await _context.SaveChangesAsync();

                // 5. 生成统一通知（告知已自动清理工单）
                if (workOrderCount > 0)
                {
                    await _notificationService.CreateAsync(
                        "OrderDeleted",
                        string.Empty,
                        $"⚠️ 订单 {salesOrder.OrderNumber} 已删除，已自动清理 {workOrderCount} 个关联工单。"
                    );
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // 6. 刷新读模型（事务已提交，在 using 块之外执行）
        await RefreshByOrderIdAsync(salesOrder.Id);

        await _operationLogService.AddLogAsync("Order", id, "删除", $"订单号={salesOrder.OrderNumber}");

        _logger.LogInformation("订单 {OrderNumber} 已被删除，同时自动清理了 {Count} 个关联工单",
            salesOrder.OrderNumber, workOrderCount);
    }

    #endregion

    #region 项次管理

    public async Task<OrderItemDto> AddItemAsync(int orderId, AddOrderItemRequest request)
    {
        var salesOrder = await _context.SalesOrders
            .Include(so => so.OrderItems)
            .FirstOrDefaultAsync(so => so.Id == orderId);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        var allSequences = await _context.OrderItems
            .Where(oi => oi.SalesOrderId == orderId)
            .Select(oi => oi.Sequence)
            .ToListAsync();

        int sequence;
        if (request.Sequence.HasValue && request.Sequence.Value > 0)
        {
            sequence = request.Sequence.Value;
            if (allSequences.Contains(sequence))
                throw new BusinessException($"项次号 {sequence} 已存在");
        }
        else
        {
            sequence = 1;
            while (allSequences.Contains(sequence))
                sequence++;
        }

        var orderItem = await CreateOrderItemFromAddRequestAsync(request, salesOrder.Id, sequence);
        _context.OrderItems.Add(orderItem);

        // 更新订单的最后项次变更时间
        salesOrder.LastItemChangeTime = DateTimeOffset.Now;
        _context.Entry(salesOrder).Property(x => x.LastItemChangeTime).IsModified = true;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();

            await CreateItemChangedNotificationIfNeededAsync(orderId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        await _operationLogService.AddLogAsync("Order", orderId, "变更",
            $"新增项次{orderItem.Sequence}: 交货日期={orderItem.DeliveryDate:yyyy-MM-dd}, 交货状态={EnumHelper.GetDisplayName(orderItem.DeliveryState)}, 标准牌号={orderItem.StandardGrade}, 外径={orderItem.OuterDiameter:G29}, 壁厚={orderItem.WallThickness:G29}, 支数={orderItem.Quantity}, 合同重量={orderItem.ContractWeight:G29}");

        await RefreshByOrderIdAsync(orderId);

        return await MapToOrderItemDto(orderItem);
    }

    public async Task<OrderItemDto> UpdateItemAsync(int orderId, int itemId, UpdateOrderItemRequest request)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == orderId);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        var orderItem = await _context.OrderItems
            .FirstOrDefaultAsync(oi => oi.Id == itemId && oi.SalesOrderId == orderId);

        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        if (request.Sequence != orderItem.Sequence)
        {
            var exists = await _context.OrderItems
                .AnyAsync(oi => oi.SalesOrderId == orderId && oi.Sequence == request.Sequence && oi.Id != itemId);
            if (exists)
                throw new BusinessException($"项次号 {request.Sequence} 已存在");
            orderItem.Sequence = request.Sequence;
        }

        var gradeMapping = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(sgm => sgm.StandardGrade == request.StandardGrade);

        var plantGrade = gradeMapping?.PlantGrade ?? orderItem.PlantGrade;
        var density = gradeMapping?.Density ?? orderItem.Density;

        ValidateLengthStatus(request.LengthStatus, request.MinLength, request.MaxLength);

        var normalizedOuterDiameter = NormalizeDecimalValue(request.OuterDiameter);
        var normalizedWallThickness = NormalizeDecimalValue(request.WallThickness);
        var normalizedOuterDiameterNegative = NormalizeDecimalValue(request.OuterDiameterNegative);
        var normalizedOuterDiameterPositive = NormalizeDecimalValue(request.OuterDiameterPositive);
        var normalizedWallThicknessNegative = NormalizeDecimalValue(request.WallThicknessNegative);
        var normalizedWallThicknessPositive = NormalizeDecimalValue(request.WallThicknessPositive);
        var normalizedContractWeight = NormalizeDecimalValue(request.ContractWeight);

        var meters = CalculateMeters(request.LengthStatus, request.MinLength, request.MaxLength, request.Quantity, request.Meters);
        var metersValue = meters ?? 0m;
        var theoreticalWeight = CalculateTheoreticalWeight(
            density,
            normalizedOuterDiameter,
            normalizedWallThickness,
            normalizedOuterDiameterNegative, normalizedOuterDiameterPositive,
            normalizedWallThicknessNegative, normalizedWallThicknessPositive,
            metersValue);

        // 捕获旧值，用于变更检测
        var oldDeliveryDate = orderItem.DeliveryDate;
        var oldDeliveryState = orderItem.DeliveryState;
        var oldStandardGrade = orderItem.StandardGrade;
        var oldOuterDiameter = orderItem.OuterDiameter;
        var oldWallThickness = orderItem.WallThickness;
        var oldOuterDiameterNegative = orderItem.OuterDiameterNegative;
        var oldOuterDiameterPositive = orderItem.OuterDiameterPositive;
        var oldWallThicknessNegative = orderItem.WallThicknessNegative;
        var oldWallThicknessPositive = orderItem.WallThicknessPositive;
        var oldLengthStatus = orderItem.LengthStatus;
        var oldMinLength = orderItem.MinLength;
        var oldMaxLength = orderItem.MaxLength;
        var oldQuantity = orderItem.Quantity;
        var oldContractWeight = orderItem.ContractWeight;

        SetOrderItemFields(orderItem,
            deliveryDate: request.DeliveryDate,
            delayPenalty: request.DelayPenalty,
            settlementMethod: request.SettlementMethod,
            pipeManufacturingType: request.PipeManufacturingType,
            standardNo: request.StandardNo,
            deliveryState: request.DeliveryState,
            standardGrade: request.StandardGrade,
            plantGrade: plantGrade,
            density: density,
            outerDiameter: normalizedOuterDiameter,
            wallThickness: normalizedWallThickness,
            specification: $"{normalizedOuterDiameter}*{normalizedWallThickness}",
            outerDiameterNegative: normalizedOuterDiameterNegative,
            outerDiameterPositive: normalizedOuterDiameterPositive,
            wallThicknessNegative: normalizedWallThicknessNegative,
            wallThicknessPositive: normalizedWallThicknessPositive,
            lengthStatus: request.LengthStatus,
            minLength: request.MinLength,
            maxLength: CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength),
            quantity: request.Quantity,
            meters: meters,
            contractWeight: normalizedContractWeight,
            theoreticalWeight: theoreticalWeight,
            remark: request.Remark);

        // 对比变更，生成详细日志
        var fieldChanges = new List<string>();
        if (orderItem.DeliveryDate != oldDeliveryDate)
            fieldChanges.Add($"交货日期={oldDeliveryDate:yyyy-MM-dd}→{orderItem.DeliveryDate:yyyy-MM-dd}");
        if (orderItem.DeliveryState != oldDeliveryState)
            fieldChanges.Add($"交货状态={EnumHelper.GetDisplayName(oldDeliveryState)}→{EnumHelper.GetDisplayName(orderItem.DeliveryState)}");
        if (orderItem.StandardGrade != oldStandardGrade)
            fieldChanges.Add($"标准牌号={oldStandardGrade}→{orderItem.StandardGrade}");
        if (orderItem.OuterDiameter != oldOuterDiameter)
            fieldChanges.Add($"外径={oldOuterDiameter:G29}→{orderItem.OuterDiameter:G29}");
        if (orderItem.WallThickness != oldWallThickness)
            fieldChanges.Add($"壁厚={oldWallThickness:G29}→{orderItem.WallThickness:G29}");
        if (orderItem.OuterDiameterNegative != oldOuterDiameterNegative)
            fieldChanges.Add($"外径下差={oldOuterDiameterNegative:G29}→{orderItem.OuterDiameterNegative:G29}");
        if (orderItem.OuterDiameterPositive != oldOuterDiameterPositive)
            fieldChanges.Add($"外径上差={oldOuterDiameterPositive:G29}→{orderItem.OuterDiameterPositive:G29}");
        if (orderItem.WallThicknessNegative != oldWallThicknessNegative)
            fieldChanges.Add($"壁厚下差={oldWallThicknessNegative:G29}→{orderItem.WallThicknessNegative:G29}");
        if (orderItem.WallThicknessPositive != oldWallThicknessPositive)
            fieldChanges.Add($"壁厚上差={oldWallThicknessPositive:G29}→{orderItem.WallThicknessPositive:G29}");
        if (orderItem.LengthStatus != oldLengthStatus)
            fieldChanges.Add($"长度状态={EnumHelper.GetDisplayName(oldLengthStatus)}→{EnumHelper.GetDisplayName(orderItem.LengthStatus)}");
        if (orderItem.MinLength != oldMinLength)
            fieldChanges.Add($"最小长度={oldMinLength?.ToString("G29")}→{orderItem.MinLength?.ToString("G29")}");
        if (orderItem.MaxLength != oldMaxLength)
            fieldChanges.Add($"最大长度={oldMaxLength?.ToString("G29")}→{orderItem.MaxLength?.ToString("G29")}");
        if (orderItem.Quantity != oldQuantity)
            fieldChanges.Add($"支数={oldQuantity}→{orderItem.Quantity}");
        if (orderItem.ContractWeight != oldContractWeight)
            fieldChanges.Add($"合同重量={oldContractWeight:G29}→{orderItem.ContractWeight:G29}");

        var itemChangeLog = $"项次{orderItem.Sequence}:" + (fieldChanges.Count > 0
            ? string.Join(", ", fieldChanges)
            : "无字段变更");

        // 更新订单的最后项次变更时间
        salesOrder.LastItemChangeTime = DateTimeOffset.Now;
        _context.Entry(salesOrder).Property(x => x.LastItemChangeTime).IsModified = true;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();
            await CreateItemChangedNotificationIfNeededAsync(orderId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        await _operationLogService.AddLogAsync("Order", orderId, "变更", itemChangeLog);

        // 读模型刷新已移除（原 RefreshByOrderAsync 调用）
        // 项次变更后先标记工单为"待修正"，再刷新读模型
        await MarkWorkOrdersPendingAsync(salesOrder.OrderNumber);

        await RefreshByOrderIdAsync(orderId);

        // 刷新用料计划总览读模型（工单状态变更后同步）
        if (_listSummaryService != null)
            await _listSummaryService.RefreshBySalesOrderAsync(salesOrder.OrderNumber);

        return await MapToOrderItemDto(orderItem);
    }

    public async Task DeleteItemAsync(int orderId, int itemId)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == orderId);

        if (salesOrder == null)
            throw new BusinessException("订单不存在");

        var orderItem = await _context.OrderItems
            .Include(oi => oi.ProductRequirement)
            .FirstOrDefaultAsync(oi => oi.Id == itemId && oi.SalesOrderId == orderId);

        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        _context.OrderItems.Remove(orderItem);

        // 更新订单的最后项次变更时间
        salesOrder.LastItemChangeTime = DateTimeOffset.Now;
        _context.Entry(salesOrder).Property(x => x.LastItemChangeTime).IsModified = true;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();
            await CreateItemChangedNotificationIfNeededAsync(orderId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        await _operationLogService.AddLogAsync("Order", orderId, "变更", $"删除项次: {orderItem.Sequence}");

        // 项次变更后先标记工单为"待修正"，再刷新读模型
        await MarkWorkOrdersPendingAsync(salesOrder.OrderNumber);

        // 读模型刷新已移除（原 RefreshByOrderAsync 调用）
        await RefreshByOrderIdAsync(orderId);

        // 刷新用料计划总览读模型（工单状态变更后同步）
        if (_listSummaryService != null)
            await _listSummaryService.RefreshBySalesOrderAsync(salesOrder.OrderNumber);
    }

    public async Task<SaveAllOrderResponse> SaveAllAsync(int id, SaveAllOrderRequest request)
    {
        // 1. 加载订单（含全部现有项次）
        var salesOrder = await _context.SalesOrders
            .Include(so => so.OrderItems)
            .FirstOrDefaultAsync(so => so.Id == id);
        if (salesOrder == null)
            throw new BusinessException("订单不存在");
        // 2. RowVersion 乐观并发检查
        _context.Entry(salesOrder).Property(x => x.RowVersion).OriginalValue = request.RowVersion;

        // 3. 批量加载引用数据（消除 N+1）
        var allStandardNos = request.NewItems.Concat(request.UpdatedItems)
            .Select(i => i.StandardNo).Distinct().ToList();
        var allGradeNames = request.NewItems.Concat(request.UpdatedItems)
            .Select(i => i.StandardGrade).Distinct().ToList();

        var srDict = allStandardNos.Any()
            ? await _context.StandardRegisters.Where(sr => allStandardNos.Contains(sr.StandardNo))
                .ToDictionaryAsync(sr => sr.StandardNo, sr => sr, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, MES.Data.Entities.StandardRegister.StandardRegister>(StringComparer.OrdinalIgnoreCase);
        var gradeDict = allGradeNames.Any()
            ? (await _context.StandardGradeMappings.Where(sgm => allGradeNames.Contains(sgm.StandardGrade))
                .ToListAsync())
                .GroupBy(sgm => sgm.StandardGrade, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, StandardGradeMapping>();

        var existingItems = salesOrder.OrderItems.ToDictionary(oi => oi.Id);

        // 4. 验证
        foreach (var deleteId in request.DeletedItemIds)
            if (!existingItems.ContainsKey(deleteId))
                throw new BusinessException($"要删除的项次 ID={deleteId} 不存在");

        var remainingCount = existingItems.Count - request.DeletedItemIds.Count + request.NewItems.Count;
        if (remainingCount < 1)
            throw new BusinessException("订单至少需要包含一个项次");

        foreach (var itemReq in request.NewItems.Concat(request.UpdatedItems))
        {
            if (!srDict.ContainsKey(itemReq.StandardNo))
                throw new BusinessException($"标准号 '{itemReq.StandardNo}' 不存在");
            if (!gradeDict.ContainsKey(itemReq.StandardGrade))
                throw new BusinessException($"标准牌号 '{itemReq.StandardGrade}' 不存在");
            ValidateLengthStatus(itemReq.LengthStatus, itemReq.MinLength, itemReq.MaxLength);
        }

        // 5. 单事务处理
        var newItemIdMap = new Dictionary<int, int>();
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                // 5a. 删除项次
                foreach (var deleteId in request.DeletedItemIds)
                    _context.OrderItems.Remove(existingItems[deleteId]);

                // 5b. 更新现有项次
                var keptItemIds = existingItems.Keys.Where(id => !request.DeletedItemIds.Contains(id)).ToHashSet();
                var existingSequences = existingItems.Values
                    .Where(oi => keptItemIds.Contains(oi.Id))
                    .Select(oi => oi.Sequence)
                    .ToHashSet();

                foreach (var updateReq in request.UpdatedItems)
                {
                    if (!existingItems.TryGetValue(updateReq.Id, out var orderItem))
                        throw new BusinessException($"要更新的项次 ID={updateReq.Id} 不存在");

                    var gradeMapping = gradeDict[updateReq.StandardGrade];

                    // Sequence 冲突检查（排除自身）
                    if (updateReq.Sequence != orderItem.Sequence)
                    {
                        var otherOccupied = existingItems.Values
                            .Any(oi => oi.Id != updateReq.Id && keptItemIds.Contains(oi.Id) && oi.Sequence == updateReq.Sequence);
                        if (otherOccupied)
                            throw new BusinessException($"项次号 {updateReq.Sequence} 已被其他项次占用");
                        orderItem.Sequence = updateReq.Sequence;
                    }

                    // 归一化 + 计算
                    var normalizedOd = NormalizeDecimalValue(updateReq.OuterDiameter);
                    var normalizedWt = NormalizeDecimalValue(updateReq.WallThickness);
                    var normalizedOdNeg = NormalizeDecimalValue(updateReq.OuterDiameterNegative);
                    var normalizedOdPos = NormalizeDecimalValue(updateReq.OuterDiameterPositive);
                    var normalizedWtNeg = NormalizeDecimalValue(updateReq.WallThicknessNegative);
                    var normalizedWtPos = NormalizeDecimalValue(updateReq.WallThicknessPositive);
                    var normalizedCw = NormalizeDecimalValue(updateReq.ContractWeight);

                    var meters = CalculateMeters(updateReq.LengthStatus, updateReq.MinLength, updateReq.MaxLength, updateReq.Quantity, updateReq.Meters);
                    var metersValue = meters ?? 0m;
                    var theoreticalWeight = CalculateTheoreticalWeight(
                        gradeMapping.Density, normalizedOd, normalizedWt,
                        normalizedOdNeg, normalizedOdPos, normalizedWtNeg, normalizedWtPos, metersValue);

                    if (updateReq.LengthStatus == LengthStatus.Fixed && theoreticalWeight > 0)
                    {
                        var lowerBound = await GetConfigAsync("ContractWeight", "LowerBound", 0.94m);
                        var upperBound = await GetConfigAsync("ContractWeight", "UpperBound", 1.06m);
                        ValidateContractWeightAgainstTheoreticalWeight(normalizedCw, theoreticalWeight, lowerBound, upperBound);
                    }

                    SetOrderItemFields(orderItem,
                        deliveryDate: updateReq.DeliveryDate, delayPenalty: updateReq.DelayPenalty,
                        settlementMethod: updateReq.SettlementMethod, pipeManufacturingType: updateReq.PipeManufacturingType,
                        standardNo: updateReq.StandardNo, deliveryState: updateReq.DeliveryState,
                        standardGrade: updateReq.StandardGrade, plantGrade: gradeMapping.PlantGrade,
                        density: gradeMapping.Density, outerDiameter: normalizedOd, wallThickness: normalizedWt,
                        specification: $"{normalizedOd}*{normalizedWt}",
                        outerDiameterNegative: normalizedOdNeg, outerDiameterPositive: normalizedOdPos,
                        wallThicknessNegative: normalizedWtNeg, wallThicknessPositive: normalizedWtPos,
                        lengthStatus: updateReq.LengthStatus, minLength: updateReq.MinLength,
                        maxLength: CalculateMaxLength(updateReq.LengthStatus, updateReq.MinLength, updateReq.MaxLength),
                        quantity: updateReq.Quantity, meters: meters, contractWeight: normalizedCw,
                        theoreticalWeight: theoreticalWeight, remark: updateReq.Remark);
                }

                // 5c. 新增项次
                var allNewItems = new List<(int Index, OrderItem Entity)>();
                var nextSequence = existingSequences.Any() ? existingSequences.Max() + 1 : 1;
                // 考虑更新后的 Sequence 可能占用更大的值
                foreach (var u in request.UpdatedItems)
                    if (u.Sequence >= nextSequence) nextSequence = u.Sequence + 1;

                for (int i = 0; i < request.NewItems.Count; i++)
                {
                    var newReq = request.NewItems[i];
                    var gradeMapping = gradeDict[newReq.StandardGrade];
                    var sequence = nextSequence + i;

                    var orderItem = new OrderItem { SalesOrderId = salesOrder.Id, Sequence = sequence };
                    var normalizedOd = NormalizeDecimalValue(newReq.OuterDiameter);
                    var normalizedWt = NormalizeDecimalValue(newReq.WallThickness);
                    var normalizedOdNeg = NormalizeDecimalValue(newReq.OuterDiameterNegative);
                    var normalizedOdPos = NormalizeDecimalValue(newReq.OuterDiameterPositive);
                    var normalizedWtNeg = NormalizeDecimalValue(newReq.WallThicknessNegative);
                    var normalizedWtPos = NormalizeDecimalValue(newReq.WallThicknessPositive);
                    var normalizedCw = NormalizeDecimalValue(newReq.ContractWeight);

                    var meters = CalculateMeters(newReq.LengthStatus, newReq.MinLength, newReq.MaxLength, newReq.Quantity, newReq.Meters);
                    var metersValue = meters ?? 0m;
                    var theoreticalWeight = CalculateTheoreticalWeight(
                        gradeMapping.Density, normalizedOd, normalizedWt,
                        normalizedOdNeg, normalizedOdPos, normalizedWtNeg, normalizedWtPos, metersValue);

                    if (newReq.LengthStatus == LengthStatus.Fixed && theoreticalWeight > 0)
                    {
                        var lowerBound = await GetConfigAsync("ContractWeight", "LowerBound", 0.94m);
                        var upperBound = await GetConfigAsync("ContractWeight", "UpperBound", 1.06m);
                        ValidateContractWeightAgainstTheoreticalWeight(normalizedCw, theoreticalWeight, lowerBound, upperBound);
                    }

                    SetOrderItemFields(orderItem,
                        deliveryDate: newReq.DeliveryDate, delayPenalty: newReq.DelayPenalty,
                        settlementMethod: newReq.SettlementMethod, pipeManufacturingType: newReq.PipeManufacturingType,
                        standardNo: newReq.StandardNo, deliveryState: newReq.DeliveryState,
                        standardGrade: newReq.StandardGrade, plantGrade: gradeMapping.PlantGrade,
                        density: gradeMapping.Density, outerDiameter: normalizedOd, wallThickness: normalizedWt,
                        specification: $"{normalizedOd}*{normalizedWt}",
                        outerDiameterNegative: normalizedOdNeg, outerDiameterPositive: normalizedOdPos,
                        wallThicknessNegative: normalizedWtNeg, wallThicknessPositive: normalizedWtPos,
                        lengthStatus: newReq.LengthStatus, minLength: newReq.MinLength,
                        maxLength: CalculateMaxLength(newReq.LengthStatus, newReq.MinLength, newReq.MaxLength),
                        quantity: newReq.Quantity, meters: meters, contractWeight: normalizedCw,
                        theoreticalWeight: theoreticalWeight, remark: newReq.Remark);

                    _context.OrderItems.Add(orderItem);
                    allNewItems.Add((i, orderItem));
                }

                // 5d. 更新订单头
                if (!string.IsNullOrEmpty(request.OrderNumber) && request.OrderNumber != salesOrder.OrderNumber)
                {
                    if (await _context.SalesOrders.AnyAsync(so => so.OrderNumber == request.OrderNumber && so.Id != id))
                        throw new BusinessException("订单号已存在");
                    salesOrder.OrderNumber = request.OrderNumber;
                }
                if (request.SignDate.HasValue)
                    salesOrder.SignDate = request.SignDate.Value;
                if (request.CustomerName != null)
                    salesOrder.CustomerName = request.CustomerName;

                if (request.Salesman != null)
                    salesOrder.Salesman = request.Salesman;

                if (request.EndCustomer != null)
                    salesOrder.EndCustomer = request.EndCustomer;

                salesOrder.LastItemChangeTime = DateTimeOffset.Now;
                _context.Entry(salesOrder).Property(x => x.LastItemChangeTime).IsModified = true;

                // 5e. SaveChanges（触发 RowVersion 乐观并发检查）
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw new BusinessException("订单已被其他用户修改，请刷新后重试");
                }

                // 5f. 构建新项次 ID 映射（SaveChanges 后 EF 自动填入 Id）
                foreach (var (index, entity) in allNewItems)
                    newItemIdMap[index] = entity.Id;

                // 5g. 统一创建通知（仅在订单状态为 Confirmed 时，同一事务内）
                await CreateItemChangedNotificationIfNeededAsync(salesOrder.Id);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // 6. 构建响应（在事务 using 块外执行，避免已提交事务不可用）
        _logger.LogInformation("批量保存订单成功: {OrderNumber}, 新增={NewCount}, 更新={UpdateCount}, 删除={DeleteCount}",
            salesOrder.OrderNumber, request.NewItems.Count, request.UpdatedItems.Count, request.DeletedItemIds.Count);

        await _operationLogService.AddLogAsync("Order", id, "变更",
            $"批量保存: 新增{request.NewItems.Count}项, 更新{request.UpdatedItems.Count}项, 删除{request.DeletedItemIds.Count}项");

        var resultItems = salesOrder.OrderItems
            .Where(oi => !request.DeletedItemIds.Contains(oi.Id))
            .Select(oi => new OrderItemSaveResult
            {
                Id = oi.Id,
                Sequence = oi.Sequence,
                Meters = oi.Meters ?? 0m,
                TheoreticalWeight = oi.TheoreticalWeight
            })
            .OrderBy(r => r.Sequence)
            .ToList();

        // 刷新读模型
        await RefreshByOrderIdAsync(salesOrder.Id);

        // 自动触发工单状态检测
        if (_workOrderService != null)
            await _workOrderService.CheckAndUpdateWorkOrderStatusAsync(salesOrder.Id);

        return new SaveAllOrderResponse
        {
            RowVersion = salesOrder.RowVersion,
            NewItemIdMap = newItemIdMap,
            Items = resultItems
        };

    }

    #endregion

    /// <summary>
    /// 获取所有订单列表数据（无分页，供客户端筛选排序）
    /// </summary>
    public async Task<List<SalesOrderListDto>> GetAllListAsync()
    {
        var summaries = await _context.Set<OrderListSummary>()
            .AsNoTracking()
            .Where(s => s.Status == SalesOrderStatus.Pending || s.Status == SalesOrderStatus.Confirmed)
            .ToListAsync();

        var orderIds = summaries.Select(s => s.OrderId).ToList();
        var salesOrderRowVersions = await _context.SalesOrders
            .AsNoTracking()
            .Where(so => orderIds.Contains(so.Id))
            .ToDictionaryAsync(so => so.Id, so => so.RowVersion);

        return summaries.Select(s => new SalesOrderListDto
        {
            Id = s.OrderId,
            OrderNumber = s.OrderNumber,
            SignDate = s.SignDate,
            CustomerName = s.CustomerName,
            Salesman = s.Salesman,
            EndCustomer = s.EndCustomer,
            DeliveryStart = s.DeliveryStart,
            DeliveryEnd = s.DeliveryEnd,
            HasDelayPenalty = s.HasDelayPenalty,
            TotalContractWeight = s.TotalContractWeight,
            ItemCount = s.ItemCount,
            Status = s.Status,
            RowVersion = salesOrderRowVersions.GetValueOrDefault(s.OrderId) ?? Array.Empty<byte>(),
            HasTechnicalRequirement = s.ItemCount > 0 && s.HasTechReqCount == s.ItemCount,
            FirstOrderItemId = s.FirstOrderItemId,
            LastChangeDate = s.LastChangeDate,
            ScheduleStage = s.ScheduleStage,
            UrgencyLevel = s.UrgencyLevel,
            EstimatedCompletionDate = s.EstimatedCompletionDate,
            FinishedInboundWeight = s.FinishedInboundWeight,
            FinishedOutboundWeight = s.FinishedOutboundWeight,
            FinishedStockWeight = s.FinishedStockWeight,
            BusinessCompleted = s.BusinessCompleted
        }).ToList();
    }

    // ========== 读模型刷新 ==========

    public async Task RefreshAllAsync()
    {
        // 全量删除后重新插入
        _context.Set<OrderListSummary>().RemoveRange(await _context.Set<OrderListSummary>().ToListAsync());

        var orderIds = await _context.SalesOrders.Select(so => so.Id).ToListAsync();
        foreach (var orderId in orderIds)
        {
            await RefreshByOrderIdAsync(orderId);
        }
    }

    /// <summary>
    /// 标记指定订单号下所有已确定工单为"待修正"
    /// 在订单项次变更后调用，替代定时任务的检测逻辑
    /// </summary>
    private async Task MarkWorkOrdersPendingAsync(string orderNumber)
    {
        var pendingWorkOrders = await _context.WorkOrders
            .Where(wo => wo.SalesOrderNo == orderNumber
                      && wo.Status == WorkOrderStatus.Confirmed)
            .ToListAsync();
        if (pendingWorkOrders.Count > 0)
        {
            foreach (var wo in pendingWorkOrders)
                wo.Status = WorkOrderStatus.Pending;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RefreshByOrderIdAsync(int orderId)
    {
        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .Include(so => so.OrderItems)
                .ThenInclude(oi => oi.ProductRequirement)
            .FirstOrDefaultAsync(so => so.Id == orderId);

        if (salesOrder == null)
        {
            var existing = await _context.Set<OrderListSummary>()
                .FirstOrDefaultAsync(s => s.OrderId == orderId);
            if (existing != null)
            {
                _context.Set<OrderListSummary>().Remove(existing);
                await _context.SaveChangesAsync();
            }
            return;
        }

        var items = salesOrder.OrderItems.ToList();
        var deliveryStart = items.MinBy(oi => oi.DeliveryDate)?.DeliveryDate;
        var deliveryEnd = items.MaxBy(oi => oi.DeliveryDate)?.DeliveryDate;

        // 从 WorkOrderExecutionSummary 聚合工单执行数据
        var executionSummaries = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Where(e => e.SalesOrderNo == salesOrder.OrderNumber)
            .ToListAsync();

        int? scheduleStage = null;
        string? urgencyLevel = null;
        DateTime? estimatedCompletionDate = null;

        if (executionSummaries.Count > 0)
        {
            // ScheduleStage: 主号暂停(0) > 原料锁定(2) > 生产执行(3) > 成品检验(4) > 主号完成(1)
            if (executionSummaries.Any(e => e.ScheduleStage == 0))
                scheduleStage = 0;
            else if (executionSummaries.Any(e => e.ScheduleStage == 2))
                scheduleStage = 2;
            else if (executionSummaries.Any(e => e.ScheduleStage == 3))
                scheduleStage = 3;
            else if (executionSummaries.Any(e => e.ScheduleStage == 4))
                scheduleStage = 4;
            else
                scheduleStage = 1;

            // UrgencyLevel: 取最紧急（按 UrgencyLevelKeys.All 顺序 A+ > A > B > C > D > E，先归一为英文 Key 再比等级）
            var nonEmpty = executionSummaries
                .Select(e => UrgencyLevelKeys.ToKey(e.UrgencyLevel))
                .Where(k => k != null)
                .OrderBy(k => Array.IndexOf(UrgencyLevelKeys.All, k!))
                .Select(k => k!)
                .FirstOrDefault();
            urgencyLevel = nonEmpty;

            // EstimatedCompletionDate: 主号完成取成品入库截止日最大值（实际入库完成时点），其余取预计生产完成日最大值
            if (scheduleStage == 1)
            {
                estimatedCompletionDate = executionSummaries
                    .Where(e => e.WarehousingEndDate.HasValue)
                    .Select(e => e.WarehousingEndDate!.Value)
                    .DefaultIfEmpty()
                    .Max();
            }
            else
            {
                estimatedCompletionDate = executionSummaries
                    .Where(e => e.EstimatedProcessCompletionDate.HasValue)
                    .Select(e => e.EstimatedProcessCompletionDate!.Value)
                    .DefaultIfEmpty()
                    .Max();
            }
        }

        // ========== 成品数据聚合（入库/出库/库存） ==========
        // 成品批次 = 订单下工单关联的 InventoryBatch，仅 MaterialType=OrderFinished（"订单成品"）：
        // BatchService 强制约束「制造物品≠订成-非交付态时，制造状态必须==交货状态」，
        // 故 OrderFinished 天然是符合交货状态的可交付成品；SpecialDeliveryStatus（订成-非交付态，
        // 制造状态≠交货状态）不满足交货要求，不属于可交付成品，不计入
        var finishedBatches = await (
            from ib in _context.InventoryBatches
            join w in _context.WorkOrders on ib.WorkOrderNo equals w.WorkOrderNo
            where w.SalesOrderNo == salesOrder.OrderNumber
                && ib.MaterialType == InventoryMaterialTypes.OrderFinished
            select new { ib.Id, ib.InitialWeight, ib.RemainingWeight })
            .ToListAsync();

        var finishedInboundWeight = finishedBatches.Sum(x => x.InitialWeight);
        var finishedStockWeight = finishedBatches.Sum(x => x.RemainingWeight);

        // 成品出库量：仅销售出库（SalesOut）
        var finishedOutboundWeight = 0m;
        if (finishedBatches.Count > 0)
        {
            var finishedBatchIds = finishedBatches.Select(x => x.Id).ToList();
            finishedOutboundWeight = await _context.OutboundRecords
                .Where(r => r.OutboundType == OutboundType.SalesOut
                    && finishedBatchIds.Contains(r.InventoryBatchId))
                .SumAsync(r => r.OutboundWeight);
        }

        // 业务完结：主号完成(1) 且 有成品入库 且 库存清零
        var businessCompleted = scheduleStage == 1 && finishedInboundWeight > 0m && finishedStockWeight == 0m;

        var existingSummary = await _context.Set<OrderListSummary>()
            .FirstOrDefaultAsync(s => s.OrderId == orderId);

        if (existingSummary != null)
        {
            existingSummary.OrderNumber = salesOrder.OrderNumber;
            existingSummary.SignDate = salesOrder.SignDate;
            existingSummary.CustomerName = salesOrder.CustomerName;
            existingSummary.Salesman = salesOrder.Salesman;
            existingSummary.EndCustomer = salesOrder.EndCustomer;
            existingSummary.DeliveryStart = deliveryStart;
            existingSummary.DeliveryEnd = deliveryEnd;
            existingSummary.HasDelayPenalty = items.Any(oi => oi.DelayPenalty);
            existingSummary.TotalContractWeight = (int)items.Sum(oi => oi.ContractWeight);
            existingSummary.ItemCount = items.Count;
            existingSummary.HasTechReqCount = items.Count(oi => oi.ProductRequirement != null);
            existingSummary.Status = salesOrder.Status;
            existingSummary.LastChangeDate = salesOrder.LastItemChangeTime?.DateTime;
            existingSummary.FirstOrderItemId = items.MinBy(oi => oi.Id)?.Id;
            existingSummary.ScheduleStage = scheduleStage;
            existingSummary.UrgencyLevel = urgencyLevel;
            existingSummary.EstimatedCompletionDate = estimatedCompletionDate;
            existingSummary.FinishedInboundWeight = finishedInboundWeight;
            existingSummary.FinishedOutboundWeight = finishedOutboundWeight;
            existingSummary.FinishedStockWeight = finishedStockWeight;
            existingSummary.BusinessCompleted = businessCompleted;
        }
        else
        {
            _context.Set<OrderListSummary>().Add(new OrderListSummary
            {
                OrderId = salesOrder.Id,
                OrderNumber = salesOrder.OrderNumber,
                SignDate = salesOrder.SignDate,
                CustomerName = salesOrder.CustomerName,
                Salesman = salesOrder.Salesman,
                EndCustomer = salesOrder.EndCustomer,
                DeliveryStart = deliveryStart,
                DeliveryEnd = deliveryEnd,
                HasDelayPenalty = items.Any(oi => oi.DelayPenalty),
                TotalContractWeight = (int)items.Sum(oi => oi.ContractWeight),
                ItemCount = items.Count,
                HasTechReqCount = items.Count(oi => oi.ProductRequirement != null),
                Status = salesOrder.Status,
                LastChangeDate = salesOrder.LastItemChangeTime?.DateTime,
                FirstOrderItemId = items.MinBy(oi => oi.Id)?.Id,
                ScheduleStage = scheduleStage,
                UrgencyLevel = urgencyLevel,
                EstimatedCompletionDate = estimatedCompletionDate,
                FinishedInboundWeight = finishedInboundWeight,
                FinishedOutboundWeight = finishedOutboundWeight,
                FinishedStockWeight = finishedStockWeight,
                BusinessCompleted = businessCompleted
            });
        }

        await _context.SaveChangesAsync();
    }

    #region Private Methods

    private async Task<OrderItem> CreateOrderItemFromCreateRequestAsync(CreateOrderItemRequest request, int salesOrderId, int sequence)
    {
        var lowerBound = await GetConfigAsync("ContractWeight", "LowerBound", 0.94m);
        var upperBound = await GetConfigAsync("ContractWeight", "UpperBound", 1.06m);

        var gradeMapping = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(sgm => sgm.StandardGrade == request.StandardGrade);
        if (gradeMapping == null)
            throw new BusinessException($"标准牌号 '{request.StandardGrade}' 不存在");

        ValidateLengthStatus(request.LengthStatus, request.MinLength, request.MaxLength);

        var normalizedOuterDiameter = NormalizeDecimalValue(request.OuterDiameter);
        var normalizedWallThickness = NormalizeDecimalValue(request.WallThickness);
        var normalizedOuterDiameterNegative = NormalizeDecimalValue(request.OuterDiameterNegative);
        var normalizedOuterDiameterPositive = NormalizeDecimalValue(request.OuterDiameterPositive);
        var normalizedWallThicknessNegative = NormalizeDecimalValue(request.WallThicknessNegative);
        var normalizedWallThicknessPositive = NormalizeDecimalValue(request.WallThicknessPositive);
        var normalizedContractWeight = NormalizeDecimalValue(request.ContractWeight);

        var meters = CalculateMeters(request.LengthStatus, request.MinLength, request.MaxLength, request.Quantity, request.Meters);
        var metersValue = meters ?? 0m;
        var theoreticalWeight = CalculateTheoreticalWeight(
            gradeMapping.Density,
            normalizedOuterDiameter,
            normalizedWallThickness,
            normalizedOuterDiameterNegative, normalizedOuterDiameterPositive,
            normalizedWallThicknessNegative, normalizedWallThicknessPositive,
            metersValue);

        // 验证合同重量与理算重量的关系
        if (request.LengthStatus == LengthStatus.Fixed && theoreticalWeight > 0)
        {
            ValidateContractWeightAgainstTheoreticalWeight(normalizedContractWeight, theoreticalWeight, lowerBound, upperBound);
        }

        var item = new OrderItem { SalesOrderId = salesOrderId, Sequence = sequence };
        SetOrderItemFields(item,
            deliveryDate: request.DeliveryDate,
            delayPenalty: request.DelayPenalty,
            settlementMethod: request.SettlementMethod,
            pipeManufacturingType: request.PipeManufacturingType,
            standardNo: request.StandardNo,
            deliveryState: request.DeliveryState,
            standardGrade: request.StandardGrade,
            plantGrade: gradeMapping.PlantGrade,
            density: gradeMapping.Density,
            outerDiameter: normalizedOuterDiameter,
            wallThickness: normalizedWallThickness,
            specification: $"{normalizedOuterDiameter}*{normalizedWallThickness}",
            outerDiameterNegative: normalizedOuterDiameterNegative,
            outerDiameterPositive: normalizedOuterDiameterPositive,
            wallThicknessNegative: normalizedWallThicknessNegative,
            wallThicknessPositive: normalizedWallThicknessPositive,
            lengthStatus: request.LengthStatus,
            minLength: request.MinLength,
            maxLength: CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength),
            quantity: request.Quantity,
            meters: meters,
            contractWeight: normalizedContractWeight,
            theoreticalWeight: theoreticalWeight,
            remark: request.Remark);
        return item;
    }

    private async Task<OrderItem> CreateOrderItemFromAddRequestAsync(AddOrderItemRequest request, int salesOrderId, int sequence)
    {
        var lowerBound = await GetConfigAsync("ContractWeight", "LowerBound", 0.94m);
        var upperBound = await GetConfigAsync("ContractWeight", "UpperBound", 1.06m);

        var gradeMapping = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(sgm => sgm.StandardGrade == request.StandardGrade);
        if (gradeMapping == null)
            throw new BusinessException($"标准牌号 '{request.StandardGrade}' 不存在");

        ValidateLengthStatus(request.LengthStatus, request.MinLength, request.MaxLength);

        var normalizedOuterDiameter = NormalizeDecimalValue(request.OuterDiameter);
        var normalizedWallThickness = NormalizeDecimalValue(request.WallThickness);
        var normalizedOuterDiameterNegative = NormalizeDecimalValue(request.OuterDiameterNegative);
        var normalizedOuterDiameterPositive = NormalizeDecimalValue(request.OuterDiameterPositive);
        var normalizedWallThicknessNegative = NormalizeDecimalValue(request.WallThicknessNegative);
        var normalizedWallThicknessPositive = NormalizeDecimalValue(request.WallThicknessPositive);
        var normalizedContractWeight = NormalizeDecimalValue(request.ContractWeight);

        var meters = CalculateMeters(request.LengthStatus, request.MinLength, request.MaxLength, request.Quantity, request.Meters);
        var metersValue = meters ?? 0m;
        var theoreticalWeight = CalculateTheoreticalWeight(
            gradeMapping.Density,
            normalizedOuterDiameter,
            normalizedWallThickness,
            normalizedOuterDiameterNegative, normalizedOuterDiameterPositive,
            normalizedWallThicknessNegative, normalizedWallThicknessPositive,
            metersValue);

        // 验证合同重量与理算重量的关系
        if (request.LengthStatus == LengthStatus.Fixed && theoreticalWeight > 0)
        {
            ValidateContractWeightAgainstTheoreticalWeight(normalizedContractWeight, theoreticalWeight, lowerBound, upperBound);
        }

        var item = new OrderItem { SalesOrderId = salesOrderId, Sequence = sequence };
        SetOrderItemFields(item,
            deliveryDate: request.DeliveryDate,
            delayPenalty: request.DelayPenalty,
            settlementMethod: request.SettlementMethod,
            pipeManufacturingType: request.PipeManufacturingType,
            standardNo: request.StandardNo,
            deliveryState: request.DeliveryState,
            standardGrade: request.StandardGrade,
            plantGrade: gradeMapping.PlantGrade,
            density: gradeMapping.Density,
            outerDiameter: normalizedOuterDiameter,
            wallThickness: normalizedWallThickness,
            specification: $"{normalizedOuterDiameter}*{normalizedWallThickness}",
            outerDiameterNegative: normalizedOuterDiameterNegative,
            outerDiameterPositive: normalizedOuterDiameterPositive,
            wallThicknessNegative: normalizedWallThicknessNegative,
            wallThicknessPositive: normalizedWallThicknessPositive,
            lengthStatus: request.LengthStatus,
            minLength: request.MinLength,
            maxLength: CalculateMaxLength(request.LengthStatus, request.MinLength, request.MaxLength),
            quantity: request.Quantity,
            meters: meters,
            contractWeight: normalizedContractWeight,
            theoreticalWeight: theoreticalWeight,
            remark: request.Remark);
        return item;
    }

    private static decimal NormalizeDecimalValue(decimal value)
    {
        return decimal.Parse(value.ToString("G29"));
    }

    /// <summary>
    /// 验证长度状态
    /// </summary>
    private static void ValidateLengthStatus(LengthStatus lengthStatus, decimal? minLength, decimal? maxLength)
    {
        switch (lengthStatus)
        {
            case LengthStatus.Fixed:
                if (!minLength.HasValue || minLength <= 0)
                    throw new BusinessException("定尺时必须填写长度");

                // 新增：定尺模式下最小长度必须等于最大长度
                if (!maxLength.HasValue || maxLength.Value != minLength.Value)
                    throw new BusinessException("定尺模式下最小长度必须等于最大长度");
                break;

            case LengthStatus.Range:
                if (!minLength.HasValue || minLength <= 0 || !maxLength.HasValue || maxLength <= 0 || maxLength <= minLength)
                    throw new BusinessException("范围尺时必须填写最小长度和最大长度，且最大长度必须大于最小长度");
                break;
        }
    }

    /// <summary>
    /// 验证合同重量与理算重量的关系
    /// </summary>
    private static void ValidateContractWeightAgainstTheoreticalWeight(decimal contractWeight, decimal theoreticalWeight, decimal lowerBound, decimal upperBound)
    {
        if (theoreticalWeight <= 0) return;

        var ratio = contractWeight / theoreticalWeight;

        if (ratio < lowerBound)
        {
            throw new BusinessException($"合同重量 {contractWeight:G29} kg 低于理算重量 {theoreticalWeight:G29} kg 的{lowerBound * 100:F0}%，可能亏损");
        }

        if (ratio > upperBound)
        {
            throw new BusinessException($"合同重量 {contractWeight:G29} kg 高于理算重量 {theoreticalWeight:G29} kg 的{upperBound * 100:F0}%");
        }
    }

    private static decimal? CalculateMeters(LengthStatus lengthStatus, decimal? minLength, decimal? maxLength, int? quantity, decimal? meters)
    {
        switch (lengthStatus)
        {
            case LengthStatus.Fixed:
                if (quantity.HasValue && quantity > 0 && maxLength.HasValue && maxLength > 0)
                    return maxLength.Value * quantity.Value / 1000;
                return null;
            case LengthStatus.Range:
            case LengthStatus.NonFixed:
                return meters.HasValue ? meters.Value : 0;
            default:
                return 0;
        }
    }

    private static decimal CalculateMaxLength(LengthStatus lengthStatus, decimal? minLength, decimal? maxLength)
    {
        switch (lengthStatus)
        {
            case LengthStatus.Fixed:
                return minLength ?? 0;
            case LengthStatus.Range:
                return maxLength ?? 0;
            default:
                return 0;
        }
    }

    private static decimal CalculateTheoreticalWeight(
        decimal density,
        decimal outerDiameter,
        decimal wallThickness,
        decimal outerDiameterNegative,
        decimal outerDiameterPositive,
        decimal wallThicknessNegative,
        decimal wallThicknessPositive,
        decimal meters)
    {
        const decimal pi = 3.1416m;
        var effectiveWallThickness = wallThickness - 0.5m * wallThicknessNegative + 0.5m * wallThicknessPositive;
        var effectiveOuterDiameter = outerDiameter - 0.5m * outerDiameterNegative + 0.5m * outerDiameterPositive;

        if (effectiveWallThickness < 0) effectiveWallThickness = 0;
        if (effectiveOuterDiameter <= effectiveWallThickness)
            effectiveOuterDiameter = effectiveWallThickness + 0.001m;

        var weight = density * pi * effectiveWallThickness * (effectiveOuterDiameter - effectiveWallThickness) * meters / 1000;
        if (weight < 0) weight = 0;
        return Math.Round(weight, 1, MidpointRounding.AwayFromZero);
    }

    private static void SetOrderItemFields(OrderItem item,
        DateTime deliveryDate, bool delayPenalty, SettlementMethod settlementMethod, PipeManufacturingType pipeManufacturingType,
        string standardNo, DeliveryState deliveryState, string standardGrade, string plantGrade,
        decimal density, decimal outerDiameter, decimal wallThickness, string specification,
        decimal outerDiameterNegative, decimal outerDiameterPositive, decimal wallThicknessNegative,
        decimal wallThicknessPositive, LengthStatus lengthStatus, decimal? minLength, decimal? maxLength,
        int? quantity, decimal? meters, decimal contractWeight, decimal theoreticalWeight, string? remark)
    {
        item.DeliveryDate = deliveryDate;
        item.DelayPenalty = delayPenalty;
        item.SettlementMethod = settlementMethod;
        item.PipeManufacturingType = pipeManufacturingType;
        item.StandardNo = standardNo;
        item.DeliveryState = deliveryState;
        item.StandardGrade = standardGrade;
        item.PlantGrade = plantGrade;
        item.Density = density;
        item.OuterDiameter = outerDiameter;
        item.WallThickness = wallThickness;
        item.Specification = specification;
        item.OuterDiameterNegative = outerDiameterNegative;
        item.OuterDiameterPositive = outerDiameterPositive;
        item.WallThicknessNegative = wallThicknessNegative;
        item.WallThicknessPositive = wallThicknessPositive;
        item.LengthStatus = lengthStatus;
        item.MinLength = minLength ?? item.MinLength;
        item.MaxLength = maxLength ?? item.MaxLength;
        item.Quantity = quantity ?? item.Quantity;
        item.Meters = meters ?? item.Meters;
        item.ContractWeight = contractWeight;
        item.TheoreticalWeight = theoreticalWeight;
        item.Remark = remark ?? item.Remark;
    }

    private Task<OrderItemDto> MapToOrderItemDto(OrderItem orderItem)
    {
        return Task.FromResult(new OrderItemDto
        {
            Id = orderItem.Id,
            Sequence = orderItem.Sequence,
            DeliveryDate = orderItem.DeliveryDate,
            DelayPenalty = orderItem.DelayPenalty,
            SettlementMethod = orderItem.SettlementMethod,
            PipeManufacturingType = orderItem.PipeManufacturingType,
            StandardNo = orderItem.StandardNo ?? string.Empty,
            DeliveryState = orderItem.DeliveryState,
            StandardGrade = orderItem.StandardGrade,
            PlantGrade = orderItem.PlantGrade,
            Density = orderItem.Density,
            OuterDiameter = orderItem.OuterDiameter,
            WallThickness = orderItem.WallThickness,
            Specification = orderItem.Specification,
            OuterDiameterNegative = orderItem.OuterDiameterNegative,
            OuterDiameterPositive = orderItem.OuterDiameterPositive,
            WallThicknessNegative = orderItem.WallThicknessNegative,
            WallThicknessPositive = orderItem.WallThicknessPositive,
            LengthStatus = orderItem.LengthStatus,
            MinLength = orderItem.MinLength,
            MaxLength = orderItem.MaxLength,
            Quantity = orderItem.Quantity,
            Meters = orderItem.Meters,
            ContractWeight = orderItem.ContractWeight,
            TheoreticalWeight = orderItem.TheoreticalWeight,
            Remark = orderItem.Remark,
            CreatedTime = orderItem.CreatedTime,
            UpdatedTime = orderItem.UpdatedTime
        });
    }

    /// <summary>
    /// 加载牌号映射表，用于从 StandardGradeMapping 取最新 PlantGrade/Density 覆盖 OrderItem 的冗余快照
    /// </summary>
    private async Task<Dictionary<string, StandardGradeMapping>> LoadGradeMappingsDictAsync(IEnumerable<OrderItem> orderItems)
    {
        var gradeNames = orderItems.Select(oi => oi.StandardGrade).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        if (gradeNames.Count == 0)
            return new Dictionary<string, StandardGradeMapping>();

        return (await _context.StandardGradeMappings
            .Where(sgm => gradeNames.Contains(sgm.StandardGrade))
            .ToListAsync())
            .GroupBy(sgm => sgm.StandardGrade, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool CanTransitionTo(SalesOrderStatus current, SalesOrderStatus target)
    {
        if (current == target) return true;
        if (current == SalesOrderStatus.Pending)
            return target == SalesOrderStatus.Confirmed;
        if (current == SalesOrderStatus.Confirmed)
            return false;
        return false;
    }

    private static string GetStatusText(SalesOrderStatus status) => EnumHelper.GetDisplayName(status);

    private async Task CreateItemChangedNotificationIfNeededAsync(int salesOrderId)
    {
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == salesOrderId);
        if (salesOrder == null || salesOrder.Status != SalesOrderStatus.Confirmed)
            return;

        var hasRecent = await _notificationService.HasRecentItemChangedNotificationAsync(salesOrder.OrderNumber, 5);
        if (hasRecent) return;

        await _notificationService.CreateAsync(
            "OrderChanged",
            string.Empty,
            $"⚠️ 订单 {salesOrder.OrderNumber} 已更新，关联工单需要同步更新。"
        );
    }

    #endregion

    #region 打印

    public async Task<SalesOrderDetailDto> GetByIdForPrintAsync(int id)
    {
        return await GetByIdAsync(id);
    }

    public async Task<List<SalesOrderDetailDto>> GetByIdsForPrintAsync(int[] ids)
    {
        return await GetByIdsAsync(ids);
    }

    public async Task<List<SalesOrderDetailDto>> GetByIdsAsync(int[] ids)
    {
        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .Include(so => so.OrderItems)
            .Where(so => ids.Contains(so.Id))
            .ToListAsync();


        var allStandardNos = salesOrders.SelectMany(so => so.OrderItems)
            .Where(oi => !string.IsNullOrEmpty(oi.StandardNo))
            .Select(oi => oi.StandardNo)
            .Distinct()
            .ToList();
        var srDict = allStandardNos.Any()
            ? await _context.StandardRegisters
                .Where(sr => allStandardNos.Contains(sr.StandardNo))
                .ToDictionaryAsync(sr => sr.StandardNo, sr => sr, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, MES.Data.Entities.StandardRegister.StandardRegister>(StringComparer.OrdinalIgnoreCase);

        // 加载牌号映射（从 StandardGradeMapping 取最新 PlantGrade/Density）
        var allOrderItems = salesOrders.SelectMany(so => so.OrderItems).ToList();
        var gradeDict = await LoadGradeMappingsDictAsync(allOrderItems);

        return salesOrders.Select(so =>
        {
            return new SalesOrderDetailDto
            {
                Id = so.Id,
                OrderNumber = so.OrderNumber,
                SignDate = so.SignDate,
                CustomerName = so.CustomerName,
                Salesman = so.Salesman,
                EndCustomer = so.EndCustomer,
                Status = so.Status,
                RowVersion = so.RowVersion,
                Items = so.OrderItems.Select(oi =>
                {
                    return new OrderItemDto
                    {
                        Id = oi.Id,
                        Sequence = oi.Sequence,
                        DeliveryDate = oi.DeliveryDate,
                        DelayPenalty = oi.DelayPenalty,
                        SettlementMethod = oi.SettlementMethod,
                        PipeManufacturingType = oi.PipeManufacturingType,
                        StandardNo = oi.StandardNo ?? string.Empty,
                        DeliveryState = oi.DeliveryState,
                        StandardGrade = oi.StandardGrade,
                        PlantGrade = gradeDict.TryGetValue(oi.StandardGrade, out var gm) ? gm.PlantGrade : oi.PlantGrade,
                        Density = gradeDict.TryGetValue(oi.StandardGrade, out var gm2) ? gm2.Density : oi.Density,
                        OuterDiameter = oi.OuterDiameter,
                        WallThickness = oi.WallThickness,
                        Specification = oi.Specification,
                        OuterDiameterNegative = oi.OuterDiameterNegative,
                        OuterDiameterPositive = oi.OuterDiameterPositive,
                        WallThicknessNegative = oi.WallThicknessNegative,
                        WallThicknessPositive = oi.WallThicknessPositive,
                        LengthStatus = oi.LengthStatus,
                        MinLength = oi.MinLength,
                        MaxLength = oi.MaxLength,
                        Quantity = oi.Quantity,
                        Meters = oi.Meters,
                        ContractWeight = oi.ContractWeight,
                        TheoreticalWeight = oi.TheoreticalWeight,
                        Remark = oi.Remark,
                        CreatedTime = oi.CreatedTime,
                        UpdatedTime = oi.UpdatedTime
                    };
                }).ToList()
            };
        }).ToList();
    }

    public async Task<byte[]> PrintOrderAsync(int id)
    {
        var order = await GetByIdForPrintAsync(id);
        return SalesOrderPrintHelper.GenerateOrderPdf(order);
    }

    public async Task<byte[]> PrintOrderBatchAsync(int[] ids)
    {
        var orders = await GetByIdsForPrintAsync(ids);
        return SalesOrderPrintHelper.GenerateBatchOrderPdf(orders);
    }

    public async Task<byte[]> PrintOrderAllAsync(string? keyword, string? sortBy, bool isDescending, DateTime? signDateFrom = null, DateTime? signDateTo = null, DateTime? deliveryDateFrom = null, DateTime? deliveryDateTo = null)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "signdate",
            IsDescending = isDescending
        };
        var pagedResult = await GetPagedAsync(query, null, null, signDateFrom, signDateTo, deliveryDateFrom, deliveryDateTo);
        var allIds = pagedResult.Items.Select(s => s.Id).ToArray();
        if (allIds.Length == 0) return Array.Empty<byte>();
        var orders = await GetByIdsForPrintAsync(allIds);
        return SalesOrderPrintHelper.GenerateBatchOrderPdf(orders);
    }

    public async Task<byte[]> PrintOrderRequirementsAsync(int orderId)
    {
        var order = await GetByIdForPrintAsync(orderId);

        // 加载技术要求
        var reqResult = await _context.ProductRequirements
            .Where(pr => pr.OrderItem != null && pr.OrderItem.SalesOrderId == orderId)
            .Include(pr => pr.OrderItem)
            .ToListAsync();

        var requirements = reqResult.Select(pr => new ProductRequirementDto
        {
            Id = pr.Id,
            OrderItemId = pr.OrderItemId,
            RequirementType = pr.RequirementType,
            ChemicalComposition = pr.ChemicalComposition,
            PmiInspection = pr.PmiInspection,
            SurfaceInspection = pr.SurfaceInspection,
            Dimension = pr.Dimension,
            Endoscopy = pr.Endoscopy,
            HydrostaticTest = pr.HydrostaticTest,
            UnderwaterPressure = pr.UnderwaterPressure,
            EddyCurrent = pr.EddyCurrent,
            UltrasonicTest = pr.UltrasonicTest,
            PortColoring = pr.PortColoring,
            RadiographicTest = pr.RadiographicTest,
            HardnessRockwell = pr.HardnessRockwell,
            HardnessBrinell = pr.HardnessBrinell,
            HardnessVickers = pr.HardnessVickers,
            TensileRoomTemp = pr.TensileRoomTemp,
            TensileHighTemp = pr.TensileHighTemp,
            WeldJointTensile = pr.WeldJointTensile,
            ImpactTest = pr.ImpactTest,
            WeldJointImpact = pr.WeldJointImpact,
            FlatteningTest = pr.FlatteningTest,
            FlaringTest = pr.FlaringTest,
            ExpandingTest = pr.ExpandingTest,
            BendTest = pr.BendTest,
            WeldJointBend = pr.WeldJointBend,
            GrainSize = pr.GrainSize,
            IntergranularCorrosion = pr.IntergranularCorrosion,
            PittingCorrosion = pr.PittingCorrosion,
            FerriteContent = pr.FerriteContent,
            Macrostructure = pr.Macrostructure,
            OtherRequirement = pr.OtherRequirement,
            Sequence = pr.OrderItem?.Sequence ?? 0,
            CreatedTime = pr.CreatedTime,
            UpdatedTime = pr.UpdatedTime
        }).OrderBy(r => r.Sequence).ToList();

        return SalesOrderPrintHelper.GenerateRequirementsPdf(order, requirements);
    }

    private static IQueryable<OrderListSummary> ApplyComputedFieldFilters(IQueryable<OrderListSummary> queryable, List<FilterDescriptor>? filters)
    {
        if (filters == null || filters.Count == 0)
            return queryable;

        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field))
                continue;

            switch (filter.Field.ToLower())
            {
                case "deliverystart":
                    if (DateTime.TryParse(filter.From?.ToString(), out var dsFrom))
                        queryable = queryable.Where(s => s.DeliveryStart >= dsFrom);
                    if (DateTime.TryParse(filter.To?.ToString(), out var dsTo))
                        queryable = queryable.Where(s => s.DeliveryStart <= dsTo);
                    break;

                case "deliveryend":
                    if (DateTime.TryParse(filter.From?.ToString(), out var deFrom))
                        queryable = queryable.Where(s => s.DeliveryEnd >= deFrom);
                    if (DateTime.TryParse(filter.To?.ToString(), out var deTo))
                        queryable = queryable.Where(s => s.DeliveryEnd <= deTo);
                    break;

                case "hasdelaypenalty":
                    if (bool.TryParse(filter.Value, out var dpVal))
                        queryable = queryable.Where(s => s.HasDelayPenalty == dpVal);
                    else if (filter.Value == "是")
                        queryable = queryable.Where(s => s.HasDelayPenalty);
                    else if (filter.Value == "否")
                        queryable = queryable.Where(s => !s.HasDelayPenalty);
                    break;

                case "totalcontractweight":
                    if (int.TryParse(filter.From?.ToString(), out var tcwMin))
                        queryable = queryable.Where(s => s.TotalContractWeight >= tcwMin);
                    if (int.TryParse(filter.To?.ToString(), out var tcwMax))
                        queryable = queryable.Where(s => s.TotalContractWeight <= tcwMax);
                    break;

                case "itemcount":
                    if (int.TryParse(filter.From?.ToString(), out var icMin))
                        queryable = queryable.Where(s => s.ItemCount >= icMin);
                    if (int.TryParse(filter.To?.ToString(), out var icMax))
                        queryable = queryable.Where(s => s.ItemCount <= icMax);
                    break;

                case "notech":
                    if (bool.TryParse(filter.Value, out var techVal))
                    {
                        if (techVal)
                            queryable = queryable.Where(s => s.ItemCount > 0 && s.HasTechReqCount == s.ItemCount);
                        else
                            queryable = queryable.Where(s => s.HasTechReqCount < s.ItemCount);
                    }
                    else if (filter.Value == "已编辑")
                        queryable = queryable.Where(s => s.ItemCount > 0 && s.HasTechReqCount == s.ItemCount);
                    else if (filter.Value == "未编辑")
                        queryable = queryable.Where(s => s.HasTechReqCount < s.ItemCount);
                    break;

                case "schedulestage":
                    if (filter.Values != null && filter.Values.Count > 0)
                    {
                        var hasNull = filter.Values.Any(v => string.IsNullOrEmpty(v));
                        var parsedStages = filter.Values
                            .Select(v => int.TryParse(v, out var n) ? (int?)n : null)
                            .Where(v => v.HasValue)
                            .Select(v => v!.Value)
                            .ToHashSet();

                        if (hasNull && parsedStages.Count > 0)
                            queryable = queryable.Where(s => s.ScheduleStage == null || parsedStages.Contains(s.ScheduleStage.Value));
                        else if (hasNull)
                            queryable = queryable.Where(s => s.ScheduleStage == null);
                        else if (parsedStages.Count > 0)
                            queryable = queryable.Where(s => s.ScheduleStage != null && parsedStages.Contains(s.ScheduleStage.Value));
                    }
                    break;

                case "businesscompleted":
                    if (bool.TryParse(filter.Value, out var bcVal))
                        queryable = queryable.Where(s => s.BusinessCompleted == bcVal);
                    else if (filter.Value == "完结")
                        queryable = queryable.Where(s => s.BusinessCompleted);
                    else if (filter.Value == "否")
                        queryable = queryable.Where(s => !s.BusinessCompleted);
                    break;
            }
        }
        return queryable;
    }

    #endregion

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("OrderService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var query = _context.Set<OrderListSummary>().AsNoTracking();

            var all = await query
                .Select(x => new
                {
                    x.OrderNumber,
                    x.SignDate,
                    x.Salesman,
                    x.CustomerName,
                    x.EndCustomer,
                    x.DeliveryStart,
                    x.DeliveryEnd,
                    x.LastChangeDate,
                    x.UrgencyLevel,
                    x.EstimatedCompletionDate
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["OrderNumber"] = all.Select(x => x.OrderNumber).Distinct().OrderBy(x => x).ToList(),
                ["SignDate"] = all.Select(x => x.SignDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["Salesman"] = all.Select(x => x.Salesman).Distinct().OrderBy(x => x).ToList(),
                ["CustomerName"] = all.Select(x => x.CustomerName).Distinct().OrderBy(x => x).ToList(),
                ["EndCustomer"] = all.Where(x => x.EndCustomer != null).Select(x => x.EndCustomer!).Distinct().OrderBy(x => x).ToList(),
                ["DeliveryStart"] = all.Where(x => x.DeliveryStart != null).Select(x => x.DeliveryStart!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["DeliveryEnd"] = all.Where(x => x.DeliveryEnd != null).Select(x => x.DeliveryEnd!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["LastChangeDate"] = all.Where(x => x.LastChangeDate != null).Select(x => x.LastChangeDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["UrgencyLevel"] = all.Where(x => x.UrgencyLevel != null).Select(x => x.UrgencyLevel!).Distinct().OrderBy(x => x).ToList(),
                ["EstimatedCompletionDate"] = all.Where(x => x.EstimatedCompletionDate != null).Select(x => x.EstimatedCompletionDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            };
        }) ?? new Dictionary<string, List<string>>();
    }
}