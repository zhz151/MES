using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Warehouse;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Warehouse;
using MES.Services.Helpers;
using MES.Services.Printing;
using MES.Core.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Warehouse;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;
    private readonly IInventoryBatchWriteService _batchWriteService;
    private readonly IOutboundWriteService _outboundWriteService;
    private readonly IInventorySyncService _syncService;
    private readonly ILogger<InventoryService> _logger;
    private readonly IMemoryCache _cache;

    // ========== DTO 映射辅助 ==========

    private static InventoryBatchDto ToDto(InventoryBatch b) => new()
    {
        Id = b.Id,
        BatchNo = b.BatchNo,
        WarehouseId = b.WarehouseId,
        MaterialType = EnumHelper.TryParse<MaterialType>(b.MaterialType) ?? default,
        PlantGrade = b.PlantGrade,
        Specification = b.Specification,
        InboundSource = !string.IsNullOrEmpty(b.InboundSource) ? EnumHelper.TryParse<InboundSource>(b.InboundSource) ?? default : default,
        SourceName = b.SourceName,
        InboundDate = b.InboundDate,
        HeatNo = b.HeatNo,
        ProductionBatchNo = b.ProductionBatchNo,
        LengthStatus = b.LengthStatus,
        MinLength = b.MinLength,
        MaxLength = b.MaxLength,
        InitialQuantity = b.InitialQuantity,
        InitialWeight = b.InitialWeight,
        UnitWeight = b.UnitWeight,
        Meters = b.Meters,
        RemainingMeters = b.RemainingMeters,
        RemainingQuantity = b.RemainingQuantity,
        RemainingWeight = b.RemainingWeight,
        ActualSpecification = b.ActualSpecification,
        ManufacturingStatus = EnumHelper.TryParse<DeliveryState>(b.ManufacturingStatus),
        LocationArea = b.LocationArea,
        LocationRack = b.LocationRack,
        Remark = b.Remark,
        DefectReason = b.DefectReason,
        LiabilityType = b.LiabilityType,
        OriginalSupplier = b.OriginalSupplier,
        TagNo = b.TagNo,
        DefectRemark = b.DefectRemark,
        IsLinkedToWorkOrder = b.IsLinkedToWorkOrder,
        WorkOrderNo = b.WorkOrderNo,
        SalesOrderNo = b.SalesOrderNo,
        OrderItemIds = b.OrderItemIds,
        SourceOrderNo = b.SourceOrderNo
    };

    private static readonly Expression<Func<OutboundRecord, OutboundRecordDto>> OutboundToDtoExpr = r => new OutboundRecordDto
    {
        Id = r.Id,
        InventoryBatchId = r.InventoryBatchId,
        BatchNo = r.BatchNo,
        OutboundType = r.OutboundType,
        SourceOrderNo = r.SourceOrderNo,
        TargetCompany = r.TargetCompany,
        OutboundQuantity = r.OutboundQuantity,
        OutboundWeight = r.OutboundWeight,
        OutboundMeters = r.OutboundMeters,
        OutboundDate = r.OutboundDate,
        Remark = r.Remark,
        CreatedBy = r.CreatedBy,
        CreatedTime = r.CreatedTime
    };
    private static readonly Func<OutboundRecord, OutboundRecordDto> OutboundToDto = OutboundToDtoExpr.Compile();

    public InventoryService(
        AppDbContext context,
        IInventoryBatchWriteService batchWriteService,
        IOutboundWriteService outboundWriteService,
        IInventorySyncService syncService,
        ILogger<InventoryService> logger,
        IMemoryCache cache)
    {
        _context = context;
        _batchWriteService = batchWriteService;
        _outboundWriteService = outboundWriteService;
        _syncService = syncService;
        _logger = logger;
        _cache = cache;
    }

    // ========== 入库批次查询 ==========

    public async Task<PagedResult<InventoryBatchDto>> GetPagedAsync(InventoryQueryParams query)
    {
        var queryable = BuildInventoryQuery(query);

        var totalCount = await queryable.CountAsync();
        var entities = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var items = entities.Select(ToDto).ToList();

        return new PagedResult<InventoryBatchDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<InventoryBatchDto>> GetAllListAsync(InventoryQueryParams query)
    {
        var queryable = BuildInventoryQuery(query);

        var entities = await queryable.ToListAsync();
        return entities.Select(ToDto).ToList();
    }

    private IQueryable<InventoryBatch> BuildInventoryQuery(InventoryQueryParams query)
    {
        var queryable = _context.InventoryBatches
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            // 预计算枚举显示名匹配 — 支持用户输入中文搜索
            var currentKeywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in currentKeywords)
            {
                var keyword = kw;
                var matchedMaterialTypes = Enum.GetValues<MaterialType>()
                    .Where(e => EnumHelper.GetDisplayName(e).Contains(keyword))
                    .Select(e => e.ToString())
                    .ToList();
                var matchedInboundSources = Enum.GetValues<InboundSource>()
                    .Where(e => EnumHelper.GetDisplayName(e).Contains(keyword))
                    .Select(e => e.ToString())
                    .ToList();

                queryable = queryable.Where(b =>
                    b.BatchNo.Contains(keyword) ||
                    (b.SourceOrderNo != null && b.SourceOrderNo.Contains(keyword)) ||
                    (b.SourceName != null && b.SourceName.Contains(keyword)) ||
                    b.MaterialType.Contains(keyword) ||
                    (matchedMaterialTypes.Count > 0 && matchedMaterialTypes.Contains(b.MaterialType)) ||
                    b.PlantGrade.Contains(keyword) ||
                    b.Specification.Contains(keyword) ||
                    (b.HeatNo != null && b.HeatNo.Contains(keyword)) ||
                    (b.ManufacturingStatus != null && b.ManufacturingStatus.Contains(keyword)) ||
                    (b.WorkOrderNo != null && b.WorkOrderNo.Contains(keyword)) ||
                    (b.ProductionBatchNo != null && b.ProductionBatchNo.Contains(keyword)) ||
                    b.InboundSource.Contains(keyword) ||
                    (matchedInboundSources.Count > 0 && matchedInboundSources.Contains(b.InboundSource)) ||
                    (b.TagNo != null && b.TagNo.Contains(keyword)) ||
                    (b.DefectReason != null && b.DefectReason.Contains(keyword)) ||
                    (b.OriginalSupplier != null && b.OriginalSupplier.Contains(keyword)) ||
                    (b.ActualSpecification != null && b.ActualSpecification.Contains(keyword)) ||
                    (b.SalesOrderNo != null && b.SalesOrderNo.Contains(keyword)) ||
                    (b.LocationArea != null && b.LocationArea.Contains(keyword)) ||
                    (b.LocationRack != null && b.LocationRack.Contains(keyword)) ||
                    (b.Remark != null && b.Remark.Contains(keyword)) ||
                    (b.LiabilityType != null && b.LiabilityType.Contains(keyword)) ||
                    (b.DefectRemark != null && b.DefectRemark.Contains(keyword)) ||
                    (b.OrderItemIds != null && b.OrderItemIds.Contains(keyword)) ||
                    (b.LengthStatus != null && b.LengthStatus.Contains(keyword)));
            }
        }

        if (query.WarehouseId.HasValue)
            queryable = queryable.Where(b => b.WarehouseId == query.WarehouseId.Value);

        if (!string.IsNullOrEmpty(query.MaterialType))
            queryable = queryable.Where(b => b.MaterialType == query.MaterialType);

        if (!string.IsNullOrEmpty(query.PlantGrade))
            queryable = queryable.Where(b => b.PlantGrade == query.PlantGrade);

        if (query.OnlyWithStock)
            queryable = queryable.Where(b => b.RemainingWeight > 0);

        if (!string.IsNullOrEmpty(query.WorkOrderNo))
            queryable = queryable.Where(b => b.WorkOrderNo == query.WorkOrderNo);

        if (!string.IsNullOrEmpty(query.BatchNo))
            queryable = queryable.Where(b => b.BatchNo.Contains(query.BatchNo));

        if (query.InboundSource.HasValue)
            queryable = queryable.Where(b => b.InboundSource == query.InboundSource.Value.ToString());

        if (!string.IsNullOrEmpty(query.SourceName))
            queryable = queryable.Where(b => b.SourceName != null && b.SourceName.Contains(query.SourceName));

        if (query.InboundDateFrom.HasValue)
            queryable = queryable.Where(b => b.InboundDate >= query.InboundDateFrom.Value);

        if (query.InboundDateTo.HasValue)
            queryable = queryable.Where(b => b.InboundDate <= query.InboundDateTo.Value);

        if (!string.IsNullOrEmpty(query.HeatNo))
            queryable = queryable.Where(b => b.HeatNo != null && b.HeatNo.Contains(query.HeatNo));

        if (!string.IsNullOrEmpty(query.Specification))
            queryable = queryable.Where(b => b.Specification.Contains(query.Specification));

        if (!string.IsNullOrEmpty(query.LengthStatus))
            queryable = queryable.Where(b => b.LengthStatus == query.LengthStatus);

        if (!string.IsNullOrEmpty(query.ManufacturingStatus))
            queryable = queryable.Where(b => b.ManufacturingStatus == query.ManufacturingStatus);

        if (!string.IsNullOrEmpty(query.DefectReason))
            queryable = queryable.Where(b => b.DefectReason != null && b.DefectReason.Contains(query.DefectReason));

        if (!string.IsNullOrEmpty(query.LiabilityType))
            queryable = queryable.Where(b => b.LiabilityType == query.LiabilityType);

        if (!string.IsNullOrEmpty(query.ProductionBatchNo))
            queryable = queryable.Where(b => b.ProductionBatchNo != null && b.ProductionBatchNo.Contains(query.ProductionBatchNo));

        if (!string.IsNullOrEmpty(query.ActualSpecification))
            queryable = queryable.Where(b => b.ActualSpecification != null && b.ActualSpecification.Contains(query.ActualSpecification));

        if (!string.IsNullOrEmpty(query.OriginalSupplier))
            queryable = queryable.Where(b => b.OriginalSupplier != null && b.OriginalSupplier.Contains(query.OriginalSupplier));

        queryable = queryable.ApplyFilters(query.Filters);

        queryable = query.SortBy?.ToLower() switch
        {
            "batchno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.BatchNo)
                : queryable.OrderBy(b => b.BatchNo),
            "materialtype" => query.IsDescending
                ? queryable.OrderByDescending(b => b.MaterialType)
                : queryable.OrderBy(b => b.MaterialType),
            "inbounddate" => query.IsDescending
                ? queryable.OrderByDescending(b => b.InboundDate)
                : queryable.OrderBy(b => b.InboundDate),
            "remainingweight" => query.IsDescending
                ? queryable.OrderByDescending(b => b.RemainingWeight)
                : queryable.OrderBy(b => b.RemainingWeight),
            "plantgrade" => query.IsDescending
                ? queryable.OrderByDescending(b => b.PlantGrade)
                : queryable.OrderBy(b => b.PlantGrade),
            "specification" => query.IsDescending
                ? queryable.OrderByDescending(b => b.Specification)
                : queryable.OrderBy(b => b.Specification),
            "heatno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.HeatNo ?? "")
                : queryable.OrderBy(b => b.HeatNo ?? ""),
            "remainingquantity" => query.IsDescending
                ? queryable.OrderByDescending(b => b.RemainingQuantity)
                : queryable.OrderBy(b => b.RemainingQuantity),
            "initialquantity" => query.IsDescending
                ? queryable.OrderByDescending(b => b.InitialQuantity)
                : queryable.OrderBy(b => b.InitialQuantity),
            "initialweight" => query.IsDescending
                ? queryable.OrderByDescending(b => b.InitialWeight)
                : queryable.OrderBy(b => b.InitialWeight),
            "unitweight" => query.IsDescending
                ? queryable.OrderByDescending(b => b.UnitWeight ?? 0)
                : queryable.OrderBy(b => b.UnitWeight ?? 0),
            "inboundsource" => query.IsDescending
                ? queryable.OrderByDescending(b => b.InboundSource)
                : queryable.OrderBy(b => b.InboundSource),
            "productionbatchno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.ProductionBatchNo ?? "")
                : queryable.OrderBy(b => b.ProductionBatchNo ?? ""),
            "salesorderno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.SalesOrderNo ?? "")
                : queryable.OrderBy(b => b.SalesOrderNo ?? ""),
            "manufacturingstatus" => query.IsDescending
                ? queryable.OrderByDescending(b => b.ManufacturingStatus ?? "")
                : queryable.OrderBy(b => b.ManufacturingStatus ?? ""),
            "lengthstatus" => query.IsDescending
                ? queryable.OrderByDescending(b => b.LengthStatus ?? "")
                : queryable.OrderBy(b => b.LengthStatus ?? ""),
            "workorderno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.WorkOrderNo ?? "")
                : queryable.OrderBy(b => b.WorkOrderNo ?? ""),
            "sourcename" => query.IsDescending
                ? queryable.OrderByDescending(b => b.SourceName)
                : queryable.OrderBy(b => b.SourceName),
            "sourceorderno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.SourceOrderNo ?? "")
                : queryable.OrderBy(b => b.SourceOrderNo ?? ""),
            "minlength" => query.IsDescending
                ? queryable.OrderByDescending(b => b.MinLength ?? 0)
                : queryable.OrderBy(b => b.MinLength ?? 0),
            "maxlength" => query.IsDescending
                ? queryable.OrderByDescending(b => b.MaxLength ?? 0)
                : queryable.OrderBy(b => b.MaxLength ?? 0),
            "meters" => query.IsDescending
                ? queryable.OrderByDescending(b => b.Meters ?? 0)
                : queryable.OrderBy(b => b.Meters ?? 0),
            "actualspecification" => query.IsDescending
                ? queryable.OrderByDescending(b => b.ActualSpecification ?? "")
                : queryable.OrderBy(b => b.ActualSpecification ?? ""),
            "locationarea" => query.IsDescending
                ? queryable.OrderByDescending(b => b.LocationArea ?? "")
                : queryable.OrderBy(b => b.LocationArea ?? ""),
            "locationrack" => query.IsDescending
                ? queryable.OrderByDescending(b => b.LocationRack ?? "")
                : queryable.OrderBy(b => b.LocationRack ?? ""),
            "tagno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.TagNo ?? "")
                : queryable.OrderBy(b => b.TagNo ?? ""),
            "defectreason" => query.IsDescending
                ? queryable.OrderByDescending(b => b.DefectReason ?? "")
                : queryable.OrderBy(b => b.DefectReason ?? ""),
            "liabilitytype" => query.IsDescending
                ? queryable.OrderByDescending(b => b.LiabilityType ?? "")
                : queryable.OrderBy(b => b.LiabilityType ?? ""),
            "originalsupplier" => query.IsDescending
                ? queryable.OrderByDescending(b => b.OriginalSupplier ?? "")
                : queryable.OrderBy(b => b.OriginalSupplier ?? ""),
            "defectremark" => query.IsDescending
                ? queryable.OrderByDescending(b => b.DefectRemark ?? "")
                : queryable.OrderBy(b => b.DefectRemark ?? ""),
            "orderitemids" => query.IsDescending
                ? queryable.OrderByDescending(b => b.OrderItemIds ?? "")
                : queryable.OrderBy(b => b.OrderItemIds ?? ""),
            "remark" => query.IsDescending
                ? queryable.OrderByDescending(b => b.Remark ?? "")
                : queryable.OrderBy(b => b.Remark ?? ""),
            "islinkedtoworkorder" => query.IsDescending
                ? queryable.OrderByDescending(b => b.IsLinkedToWorkOrder)
                : queryable.OrderBy(b => b.IsLinkedToWorkOrder),
            _ => query.IsDescending
                ? queryable.OrderByDescending(b => b.CreatedTime)
                : queryable.OrderBy(b => b.CreatedTime)
        };

        return queryable;
    }

    public async Task<InventoryBatchDto> GetByIdAsync(int id)
        => await _batchWriteService.GetByIdAsync(id);

    public async Task<InventoryBatchDto> InboundAsync(CreateInboundRequest request)
    {
        var result = await _batchWriteService.InboundAsync(request);
        _cache.Remove(PendingDeliveryQueryService.CacheKey);
        return result;
    }

    public async Task<BatchInboundResult> BatchInboundAsync(BatchInboundRequest request)
    {
        var result = await _batchWriteService.BatchInboundAsync(request);
        _cache.Remove(PendingDeliveryQueryService.CacheKey);
        return result;
    }

    public async Task<InventoryBatchDto> UpdateInventoryBatchAsync(int id, UpdateInventoryBatchRequest request)
    {
        var result = await _batchWriteService.UpdateInventoryBatchAsync(id, request);
        _cache.Remove(PendingDeliveryQueryService.CacheKey);
        return result;
    }

    public async Task HardDeleteInventoryBatchAsync(int id)
    {
        await _batchWriteService.HardDeleteInventoryBatchAsync(id);
        _cache.Remove(PendingDeliveryQueryService.CacheKey);
    }

    // ========== 出库操作 ==========

    public async Task<OutboundRecordDto> OutboundAsync(CreateOutboundRequest request)
    {
        var result = await _outboundWriteService.OutboundAsync(request);
        _cache.Remove(PendingDeliveryQueryService.CacheKey);
        return result;
    }

    public async Task<BatchOutboundResult> BatchOutboundAsync(BatchOutboundRequest request)
    {
        var result = await _outboundWriteService.BatchOutboundAsync(request);
        _cache.Remove(PendingDeliveryQueryService.CacheKey);
        return result;
    }

    public async Task<PagedResult<OutboundRecordDto>> GetOutboundRecordsAsync(OutboundQueryParams query)
    {
        var queryable = _context.OutboundRecords
            .AsNoTracking()
            .AsQueryable();

        if (query.InventoryBatchId.HasValue)
            queryable = queryable.Where(r => r.InventoryBatchId == query.InventoryBatchId.Value);

        if (query.WarehouseId.HasValue)
            queryable = queryable.Where(r => _context.InventoryBatches
                .Where(b => b.WarehouseId == query.WarehouseId.Value)
                .Select(b => b.Id)
                .Contains(r.InventoryBatchId));

        if (!string.IsNullOrEmpty(query.OutboundType) && Enum.TryParse<OutboundType>(query.OutboundType, out var parsedOutType))
            queryable = queryable.Where(r => r.OutboundType == parsedOutType);

        if (query.StartDate.HasValue)
            queryable = queryable.Where(r => r.OutboundDate >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            queryable = queryable.Where(r => r.OutboundDate <= query.EndDate.Value);

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                var matchedOutboundType = Enum.TryParse<OutboundType>(keyword, ignoreCase: true, out var parsedType);
                if (matchedOutboundType)
                {
                    var outboundType = parsedType;
                    queryable = queryable.Where(r =>
                        (r.TargetCompany != null && r.TargetCompany.Contains(keyword)) ||
                        (r.CreatedBy != null && r.CreatedBy.Contains(keyword)) ||
                        (r.SourceOrderNo != null && r.SourceOrderNo.Contains(keyword)) ||
                        (r.Remark != null && r.Remark.Contains(keyword)) ||
                        (r.BatchNo != null && r.BatchNo.Contains(keyword)) ||
                        r.OutboundType == outboundType);
                }
                else
                {
                    queryable = queryable.Where(r =>
                        (r.TargetCompany != null && r.TargetCompany.Contains(keyword)) ||
                        (r.CreatedBy != null && r.CreatedBy.Contains(keyword)) ||
                        (r.SourceOrderNo != null && r.SourceOrderNo.Contains(keyword)) ||
                        (r.Remark != null && r.Remark.Contains(keyword)) ||
                        (r.BatchNo != null && r.BatchNo.Contains(keyword)));
                }
            }
        }

        queryable = queryable.ApplyFilters(query.Filters);

        queryable = query.SortBy?.ToLower() switch
        {
            "batchno" => query.IsDescending
                ? queryable.OrderByDescending(r => r.BatchNo ?? "")
                : queryable.OrderBy(r => r.BatchNo ?? ""),
            "outbounddate" => query.IsDescending
                ? queryable.OrderByDescending(r => r.OutboundDate)
                : queryable.OrderBy(r => r.OutboundDate),
            "outboundtype" => query.IsDescending
                ? queryable.OrderByDescending(r => r.OutboundType)
                : queryable.OrderBy(r => r.OutboundType),
            "outboundquantity" => query.IsDescending
                ? queryable.OrderByDescending(r => r.OutboundQuantity)
                : queryable.OrderBy(r => r.OutboundQuantity),
            "outboundweight" => query.IsDescending
                ? queryable.OrderByDescending(r => r.OutboundWeight)
                : queryable.OrderBy(r => r.OutboundWeight),
            "targetcompany" => query.IsDescending
                ? queryable.OrderByDescending(r => r.TargetCompany ?? "")
                : queryable.OrderBy(r => r.TargetCompany ?? ""),
            "createdby" => query.IsDescending
                ? queryable.OrderByDescending(r => r.CreatedBy ?? "")
                : queryable.OrderBy(r => r.CreatedBy ?? ""),
            "sourceorderno" => query.IsDescending
                ? queryable.OrderByDescending(r => r.SourceOrderNo ?? "")
                : queryable.OrderBy(r => r.SourceOrderNo ?? ""),
            "remark" => query.IsDescending
                ? queryable.OrderByDescending(r => r.Remark ?? "")
                : queryable.OrderBy(r => r.Remark ?? ""),
            "outboundmeters" => query.IsDescending
                ? queryable.OrderByDescending(r => r.OutboundMeters)
                : queryable.OrderBy(r => r.OutboundMeters),
            _ => query.IsDescending
                ? queryable.OrderByDescending(r => r.OutboundDate)
                : queryable.OrderBy(r => r.OutboundDate)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(OutboundToDtoExpr)
            .ToListAsync();

        return new PagedResult<OutboundRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<OutboundRecordDto> UpdateOutboundRecordAsync(long id, UpdateOutboundRecordRequest request)
    {
        var result = await _outboundWriteService.UpdateOutboundRecordAsync(id, request);
        _cache.Remove(PendingDeliveryQueryService.CacheKey);
        return result;
    }

    public async Task HardDeleteOutboundRecordAsync(long id)
    {
        await _outboundWriteService.HardDeleteOutboundRecordAsync(id);
        _cache.Remove(PendingDeliveryQueryService.CacheKey);
    }

    // ========== 来源单验证与同步 ==========

    public async Task<SourceOrderValidationResult> ValidateSourceOrderAsync(string sourceOrderNo, string inboundSource, int? sourceOrderSequence = null)
        => await _syncService.ValidateSourceOrderAsync(sourceOrderNo, inboundSource, sourceOrderSequence);

    public async Task<SourceOrderValidationResult> ValidateProductionBatchAsync(string productionBatchNo)
        => await _syncService.ValidateProductionBatchAsync(productionBatchNo);

    public async Task<List<string>> ValidateWarehouseWorkOrderNosAsync(int warehouseId)
        => await _syncService.ValidateWarehouseWorkOrderNosAsync(warehouseId);

    public async Task<List<BatchWorkOrderMismatchDto>> GetMismatchedWorkOrderBatchesAsync(int? warehouseId = null)
        => await _syncService.GetMismatchedWorkOrderBatchesAsync(warehouseId);

    public async Task<List<string>> GetDistinctWorkOrderNosByWarehouseAsync(int warehouseId)
        => await _syncService.GetDistinctWorkOrderNosByWarehouseAsync(warehouseId);

    // ========== 打印 ==========

    public async Task<byte[]> PrintInventoryAllAsync(InventoryPrintAllRequest request)
    {
        var query = new InventoryQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = request.Keyword,
            SortBy = request.SortBy ?? "inbounddate",
            IsDescending = request.IsDescending,
            WarehouseId = request.WarehouseId,
            OnlyWithStock = request.OnlyWithStock
        };
        var paged = await GetPagedAsync(query);
        var title = request.OnlyWithStock ? "仓 库 库 存 列 表" : "入 库 历 史 列 表";
        return TablePrintHelper.GeneratePdf(title, paged.Items, request.Columns);
    }

    public async Task<byte[]> PrintInventorySelectedAsync(InventoryPrintSelectedRequest request)
    {
        var entities = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => request.Ids.Contains(b.Id))
            .ToListAsync();

        var items = entities.Select(ToDto).ToList();
        return TablePrintHelper.GeneratePdf("入 库 批 次 打 印", items, request.Columns);
    }

    public async Task<byte[]> PrintStockAllAsync(InventoryPrintAllRequest request)
    {
        var query = new InventoryQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = request.Keyword,
            SortBy = request.SortBy ?? "inbounddate",
            IsDescending = request.IsDescending,
            WarehouseId = request.WarehouseId,
            OnlyWithStock = true
        };
        var paged = await GetPagedAsync(query);
        var resolvers = new Dictionary<string, Func<object?, string>>
        {
            ["LengthStatus"] = v => EnumHelper.GetDisplayName<LengthStatus>(v?.ToString())
        };
        return TablePrintHelper.GeneratePdf("仓 库 库 存 列 表", paged.Items, request.Columns, resolvers);
    }

    public async Task<byte[]> PrintStockSelectedAsync(InventoryPrintSelectedRequest request)
    {
        var entities = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => request.Ids.Contains(b.Id))
            .ToListAsync();

        var items = entities.Select(ToDto).ToList();
        var resolvers = new Dictionary<string, Func<object?, string>>
        {
            ["LengthStatus"] = v => EnumHelper.GetDisplayName<LengthStatus>(v?.ToString())
        };
        return TablePrintHelper.GeneratePdf("库 存 批 次 打 印", items, request.Columns, resolvers);
    }

    public async Task<byte[]> PrintInboundAllAsync(InventoryPrintAllRequest request)
    {
        var query = new InventoryQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = request.Keyword,
            SortBy = request.SortBy ?? "inbounddate",
            IsDescending = request.IsDescending,
            WarehouseId = request.WarehouseId,
            OnlyWithStock = false
        };
        var paged = await GetPagedAsync(query);
        var resolvers = new Dictionary<string, Func<object?, string>>
        {
            ["LengthStatus"] = v => EnumHelper.GetDisplayName<LengthStatus>(v?.ToString())
        };
        return TablePrintHelper.GeneratePdf("入 库 历 史 列 表", paged.Items, request.Columns, resolvers);
    }

    public async Task<byte[]> PrintInboundSelectedAsync(InventoryPrintSelectedRequest request)
    {
        var entities = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => request.Ids.Contains(b.Id))
            .ToListAsync();

        var items = entities.Select(ToDto).ToList();
        var resolvers = new Dictionary<string, Func<object?, string>>
        {
            ["LengthStatus"] = v => EnumHelper.GetDisplayName<LengthStatus>(v?.ToString())
        };
        return TablePrintHelper.GeneratePdf("入 库 批 次 打 印", items, request.Columns, resolvers);
    }

    public async Task<byte[]> PrintOutboundAllAsync(OutboundPrintAllRequest request)
    {
        var query = new OutboundQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = request.Keyword,
            SortBy = request.SortBy ?? "outbounddate",
            IsDescending = request.IsDescending,
            WarehouseId = request.WarehouseId
        };
        var paged = await GetOutboundRecordsAsync(query);
        var resolvers = GetOutboundPrintResolvers();
        return TablePrintHelper.GeneratePdf("出 库 历 史 列 表", paged.Items, request.Columns, resolvers);
    }

    public async Task<byte[]> PrintOutboundSelectedAsync(OutboundPrintSelectedRequest request)
    {
        var items = await _context.OutboundRecords
            .AsNoTracking()
            .Where(r => request.Ids.Contains(r.Id))
            .Select(OutboundToDtoExpr)
            .ToListAsync();

        var resolvers = GetOutboundPrintResolvers();
        return TablePrintHelper.GeneratePdf("出 库 记 录 打 印", items, request.Columns, resolvers);
    }

    private static Dictionary<string, Func<object?, string>> GetOutboundPrintResolvers()
    {
        return new Dictionary<string, Func<object?, string>>
        {
            ["OutboundType"] = raw =>
            {
                if (raw == null) return "";
                if (raw is OutboundType ot) return EnumHelper.GetDisplayName(ot);
                return Enum.TryParse<OutboundType>(raw.ToString(), out var parsed)
                    ? EnumHelper.GetDisplayName(parsed)
                    : raw.ToString() ?? "";
            }
        };
    }

    // ========== 筛选上下文 ==========

    public async Task<Dictionary<string, List<string>>> GetOutboundFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("InventoryService:OutboundFilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var records = _context.OutboundRecords.AsNoTracking();

            return new Dictionary<string, List<string>>
            {
                ["BatchNo"] = await records.Where(r => r.BatchNo != null).Select(r => r.BatchNo!).Distinct().OrderBy(x => x).ToListAsync(),
                ["OutboundType"] = (await records.Select(r => r.OutboundType).Distinct().ToListAsync()).Select(e => e.ToString()).OrderBy(x => x).ToList(),
                ["SourceOrderNo"] = await records.Where(r => r.SourceOrderNo != null).Select(r => r.SourceOrderNo!).Distinct().OrderBy(x => x).ToListAsync(),
                ["TargetCompany"] = await records.Where(r => r.TargetCompany != null).Select(r => r.TargetCompany!).Distinct().OrderBy(x => x).ToListAsync(),
                ["Remark"] = await records.Where(r => r.Remark != null).Select(r => r.Remark!).Distinct().OrderBy(x => x).ToListAsync(),
                ["CreatedBy"] = await records.Select(r => r.CreatedBy).Distinct().OrderBy(x => x).ToListAsync(),
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<Dictionary<string, List<string>>> GetInventoryFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("InventoryService:InventoryFilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var results = await _context.InventoryBatches.AsNoTracking()
                .Select(b => new
                {
                    b.BatchNo,
                    b.InboundDate,
                    b.SourceOrderNo,
                    b.MaterialType,
                    b.SourceName,
                    b.ManufacturingStatus,
                    b.LocationArea,
                    b.LocationRack,
                    b.HeatNo,
                    b.PlantGrade,
                    b.Specification,
                    b.Remark,
                    b.IsLinkedToWorkOrder,
                    b.WorkOrderNo,
                    b.SalesOrderNo,
                    b.ProductionBatchNo,
                    b.ActualSpecification,
                    b.DefectReason,
                    b.LiabilityType,
                    b.OriginalSupplier,
                    b.TagNo,
                    b.DefectRemark,
                    b.InboundSource,
                    b.LengthStatus,
                    OrderItemIds = b.OrderItemIds
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["BatchNo"] = results.Select(x => x.BatchNo).Distinct().OrderBy(x => x).ToList(),
                ["InboundDate"] = results.Select(x => x.InboundDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["SourceOrderNo"] = results.Where(x => x.SourceOrderNo != null).Select(x => x.SourceOrderNo!).Distinct().OrderBy(x => x).ToList(),
                ["MaterialType"] = results.Select(x => x.MaterialType).Distinct().OrderBy(x => x).ToList(),
                ["SourceName"] = results.Select(x => x.SourceName).Distinct().OrderBy(x => x).ToList(),
                ["ManufacturingStatus"] = results.Where(x => x.ManufacturingStatus != null).Select(x => x.ManufacturingStatus!).Distinct().OrderBy(x => x).ToList(),
                ["LocationArea"] = results.Where(x => x.LocationArea != null).Select(x => x.LocationArea!).Distinct().OrderBy(x => x).ToList(),
                ["LocationRack"] = results.Where(x => x.LocationRack != null).Select(x => x.LocationRack!).Distinct().OrderBy(x => x).ToList(),
                ["HeatNo"] = results.Where(x => x.HeatNo != null).Select(x => x.HeatNo!).Distinct().OrderBy(x => x).ToList(),
                ["PlantGrade"] = results.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
                ["Specification"] = results.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
                ["Remark"] = results.Where(x => x.Remark != null).Select(x => x.Remark!).Distinct().OrderBy(x => x).ToList(),
                ["IsLinkedToWorkOrder"] = results.Select(x => x.IsLinkedToWorkOrder.ToString()).Distinct().OrderBy(x => x).ToList(),
                ["WorkOrderNo"] = results.Where(x => x.WorkOrderNo != null).Select(x => x.WorkOrderNo!).Distinct().OrderBy(x => x).ToList(),
                ["SalesOrderNo"] = results.Where(x => x.SalesOrderNo != null).Select(x => x.SalesOrderNo!).Distinct().OrderBy(x => x).ToList(),
                ["ProductionBatchNo"] = results.Where(x => x.ProductionBatchNo != null).Select(x => x.ProductionBatchNo!).Distinct().OrderBy(x => x).ToList(),
                ["ActualSpecification"] = results.Where(x => x.ActualSpecification != null).Select(x => x.ActualSpecification!).Distinct().OrderBy(x => x).ToList(),
                ["DefectReason"] = results.Where(x => x.DefectReason != null).Select(x => x.DefectReason!).Distinct().OrderBy(x => x).ToList(),
                ["LiabilityType"] = results.Where(x => x.LiabilityType != null).Select(x => x.LiabilityType!).Distinct().OrderBy(x => x).ToList(),
                ["OriginalSupplier"] = results.Where(x => x.OriginalSupplier != null).Select(x => x.OriginalSupplier!).Distinct().OrderBy(x => x).ToList(),
                ["TagNo"] = results.Where(x => x.TagNo != null).Select(x => x.TagNo!).Distinct().OrderBy(x => x).ToList(),
                ["DefectRemark"] = results.Where(x => x.DefectRemark != null).Select(x => x.DefectRemark!).Distinct().OrderBy(x => x).ToList(),
                ["InboundSource"] = results.Select(x => x.InboundSource.ToString()).Distinct().OrderBy(x => x).ToList(),
                ["LengthStatus"] = results.Where(x => x.LengthStatus != null).Select(x => x.LengthStatus!).Distinct().OrderBy(x => x).ToList(),
                ["OrderItemIds"] = results.Where(x => x.OrderItemIds != null).Select(x => x.OrderItemIds!).Distinct().OrderBy(x => x).ToList(),
            };
        }) ?? new Dictionary<string, List<string>>();
    }
}
