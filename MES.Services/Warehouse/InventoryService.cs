using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Warehouse;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Warehouse;
using MES.Services.Helpers;
using MES.Services.Printing;
using MES.Core.Helpers;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;

namespace MES.Services.Warehouse;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;
    private readonly IInventoryBatchWriteService _batchWriteService;
    private readonly IOutboundWriteService _outboundWriteService;
    private readonly IInventorySyncService _syncService;
    private readonly ILogger<InventoryService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IPendingDeliveryQueryService _pendingDeliveryQueryService;

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
        LengthStatus = EnumHelper.TryParse<LengthStatus>(b.LengthStatus),
        MinLength = b.MinLength,
        MaxLength = b.MaxLength,
        CutLengthMatchType = EnumHelper.TryParse<CutLengthMatchType>(b.CutLengthMatchType),
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
        ProductionMainNo = b.ProductionMainNo,
        OrderItemIds = b.OrderItemIds,
        SourceOrderNo = b.SourceOrderNo,
        SourceOrderSequence = b.SourceOrderSequence,
        CreatedBy = b.CreatedBy,
        CreatedTime = b.CreatedTime,
        UpdatedBy = b.UpdatedBy,
        UpdatedTime = b.UpdatedTime
    };

    private static readonly Expression<Func<OutboundRecord, OutboundRecordDto>> OutboundToDtoExpr = r => new OutboundRecordDto
    {
        Id = r.Id,
        InventoryBatchId = r.InventoryBatchId,
        BatchNo = r.BatchNo,
        OutboundType = r.OutboundType,
        WorkOrderNo = r.WorkOrderNo,
        ReturnSourceBatchNo = r.ReturnSourceBatchNo,
        SourceOrderNo = r.SourceOrderNo,
        TargetCompany = r.TargetCompany,
        OutboundQuantity = r.OutboundQuantity,
        OutboundWeight = r.OutboundWeight,
        OutboundMeters = r.OutboundMeters,
        OutboundDate = r.OutboundDate,
        Remark = r.Remark,
        CreatedBy = r.CreatedBy,
        CreatedTime = r.CreatedTime,
        UpdatedBy = r.UpdatedBy,
        UpdatedTime = r.UpdatedTime
    };

    public InventoryService(
        AppDbContext context,
        IInventoryBatchWriteService batchWriteService,
        IOutboundWriteService outboundWriteService,
        IInventorySyncService syncService,
        ILogger<InventoryService> logger,
        IMemoryCache cache,
        IPendingDeliveryQueryService pendingDeliveryQueryService)
    {
        _context = context;
        _batchWriteService = batchWriteService;
        _outboundWriteService = outboundWriteService;
        _syncService = syncService;
        _logger = logger;
        _cache = cache;
        _pendingDeliveryQueryService = pendingDeliveryQueryService;
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
                    (b.ProductionMainNo != null && b.ProductionMainNo.Contains(keyword)) ||
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
            "cutlengthmatchtype" => query.IsDescending
                ? queryable.OrderByDescending(b => b.CutLengthMatchType == nameof(CutLengthMatchType.FullMatch) ? 0
                    : b.CutLengthMatchType == nameof(CutLengthMatchType.MainNoMatch) ? 1 : 2)
                : queryable.OrderBy(b => b.CutLengthMatchType == nameof(CutLengthMatchType.FullMatch) ? 0
                    : b.CutLengthMatchType == nameof(CutLengthMatchType.MainNoMatch) ? 1 : 2),
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
            "productionmainno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.ProductionMainNo ?? "")
                : queryable.OrderBy(b => b.ProductionMainNo ?? ""),
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

    /// <summary>
    /// 库存/出库数据写操作后失效列筛选上下文缓存（Outbound/InventoryFilterContexts），
    /// 避免下拉选项残留已删值或缺失新增值
    /// </summary>
    private void InvalidateFilterContexts()
    {
        _cache.Remove(CacheKeys.InventoryOutboundFilterContexts);
        _cache.Remove(CacheKeys.InventoryFilterContexts);
    }

    public async Task<InventoryBatchDto> InboundAsync(CreateInboundRequest request)
    {
        var result = await _batchWriteService.InboundAsync(request);
        await _pendingDeliveryQueryService.InvalidateCachesAsync();
        InvalidateFilterContexts();
        return result;
    }

    public async Task<BatchInboundResult> BatchInboundAsync(BatchInboundRequest request)
    {
        var result = await _batchWriteService.BatchInboundAsync(request);
        await _pendingDeliveryQueryService.InvalidateCachesAsync();
        InvalidateFilterContexts();
        return result;
    }

    public async Task<InventoryBatchDto> UpdateInventoryBatchAsync(int id, UpdateInventoryBatchRequest request)
    {
        var result = await _batchWriteService.UpdateInventoryBatchAsync(id, request);
        await _pendingDeliveryQueryService.InvalidateCachesAsync();
        InvalidateFilterContexts();
        return result;
    }

    public async Task HardDeleteInventoryBatchAsync(int id)
    {
        await _batchWriteService.HardDeleteInventoryBatchAsync(id);
        await _pendingDeliveryQueryService.InvalidateCachesAsync();
        InvalidateFilterContexts();
    }

    // ========== 出库操作 ==========

    public async Task<OutboundRecordDto> OutboundAsync(CreateOutboundRequest request)
    {
        var result = await _outboundWriteService.OutboundAsync(request);
        await _pendingDeliveryQueryService.InvalidateCachesAsync();
        InvalidateFilterContexts();
        return result;
    }

    public async Task<BatchOutboundResult> BatchOutboundAsync(BatchOutboundRequest request)
    {
        var result = await _outboundWriteService.BatchOutboundAsync(request);
        await _pendingDeliveryQueryService.InvalidateCachesAsync();
        InvalidateFilterContexts();
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
                        (r.WorkOrderNo != null && r.WorkOrderNo.Contains(keyword)) ||
                        (r.ReturnSourceBatchNo != null && r.ReturnSourceBatchNo.Contains(keyword)) ||
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
                        (r.WorkOrderNo != null && r.WorkOrderNo.Contains(keyword)) ||
                        (r.ReturnSourceBatchNo != null && r.ReturnSourceBatchNo.Contains(keyword)) ||
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
            "workorderno" => query.IsDescending
                ? queryable.OrderByDescending(r => r.WorkOrderNo ?? "")
                : queryable.OrderBy(r => r.WorkOrderNo ?? ""),
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
            "returnsourcebatchno" => query.IsDescending
                ? queryable.OrderByDescending(r => r.ReturnSourceBatchNo ?? "")
                : queryable.OrderBy(r => r.ReturnSourceBatchNo ?? ""),
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
        await _pendingDeliveryQueryService.InvalidateCachesAsync();
        InvalidateFilterContexts();
        return result;
    }

    public async Task HardDeleteOutboundRecordAsync(long id)
    {
        await _outboundWriteService.HardDeleteOutboundRecordAsync(id);
        await _pendingDeliveryQueryService.InvalidateCachesAsync();
        InvalidateFilterContexts();
    }

    // ========== 来源单验证与同步 ==========

    public async Task<SourceOrderValidationResult> ValidateSourceOrderAsync(string sourceOrderNo, string inboundSource, int? sourceOrderSequence = null)
        => await _syncService.ValidateSourceOrderAsync(sourceOrderNo, inboundSource, sourceOrderSequence);

    public async Task<SourceOrderValidationResult> ValidateProductionBatchAsync(string productionBatchNo)
        => await _syncService.ValidateProductionBatchAsync(productionBatchNo);

    public async Task<SourceOrderValidationResult> ResolveLinkedWorkOrderAsync(int inventoryBatchId)
        => await _syncService.ResolveLinkedWorkOrderAsync(inventoryBatchId);

    public async Task<List<BatchWorkOrderMismatchDto>> GetMismatchedWorkOrderBatchesAsync(int? warehouseId = null)
        => await _syncService.GetMismatchedWorkOrderBatchesAsync(warehouseId);

    public async Task<List<SourceOrderChangedBatchDto>> GetSourceOrderChangedBatchesAsync(int? warehouseId = null)
        => await _syncService.GetSourceOrderChangedBatchesAsync(warehouseId);

    public async Task<List<string>> GetDistinctWorkOrderNosByWarehouseAsync(int warehouseId)
        => await _syncService.GetDistinctWorkOrderNosByWarehouseAsync(warehouseId);

    // ========== 打印 ==========

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
        return await _cache.GetOrCreateAsync(CacheKeys.InventoryOutboundFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            var records = _context.OutboundRecords.AsNoTracking();

            return new Dictionary<string, List<string>>
            {
                ["BatchNo"] = await records.Where(r => r.BatchNo != null).Select(r => r.BatchNo!).Distinct().OrderBy(x => x).ToListAsync(),
                ["OutboundType"] = (await records.Select(r => r.OutboundType).Distinct().ToListAsync()).Select(e => e.ToString()).OrderBy(x => x).ToList(),
                ["WorkOrderNo"] = await records.Where(r => r.WorkOrderNo != null).Select(r => r.WorkOrderNo!).Distinct().OrderBy(x => x).ToListAsync(),
                ["ReturnSourceBatchNo"] = await records.Where(r => r.ReturnSourceBatchNo != null).Select(r => r.ReturnSourceBatchNo!).Distinct().OrderBy(x => x).ToListAsync(),
                ["SourceOrderNo"] = await records.Where(r => r.SourceOrderNo != null).Select(r => r.SourceOrderNo!).Distinct().OrderBy(x => x).ToListAsync(),
                ["TargetCompany"] = await records.Where(r => r.TargetCompany != null).Select(r => r.TargetCompany!).Distinct().OrderBy(x => x).ToListAsync(),
                ["Remark"] = await records.Where(r => r.Remark != null).Select(r => r.Remark!).Distinct().OrderBy(x => x).ToListAsync(),
                ["CreatedBy"] = await records.Select(r => r.CreatedBy).Distinct().OrderBy(x => x).ToListAsync(),
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<Dictionary<string, List<string>>> GetInventoryFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.InventoryFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

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
                    OrderItemIds = b.OrderItemIds,
                    ProductionMainNo = b.ProductionMainNo
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
                ["ProductionMainNo"] = results.Where(x => x.ProductionMainNo != null).Select(x => x.ProductionMainNo!).Distinct().OrderBy(x => x).ToList(),
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    // ========== 物料进出存报表行排序：库房固定顺序 + 物料类型固定顺序 + 来源/类型固定顺序 ==========

    private static readonly Dictionary<string, int> WarehouseDisplayOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["原料库"] = 1,
        ["成品库"] = 2,
        ["在制品库"] = 3,
        ["次品库"] = 4
    };

    private static readonly Dictionary<string, int> MaterialTypeDisplayOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        [MaterialType.RoundBar.ToString()] = 1,             // 圆棒
        [MaterialType.RoughTube.ToString()] = 2,            // 荒管
        [MaterialType.CriticalFinished.ToString()] = 3,     // 临界成品
        [MaterialType.OrderFinished.ToString()] = 4,        // 订单成品
        [MaterialType.SpecialDeliveryStatus.ToString()] = 5, // 订成-非交付态
        [MaterialType.Finished.ToString()] = 6,             // 备料成品
        [MaterialType.Surplus.ToString()] = 7,              // 余库料
        [MaterialType.SemiFinished.ToString()] = 8,         // 半成品
        [MaterialType.WorkInProgress.ToString()] = 9,       // 在制品
        [MaterialType.DefectRoundBar.ToString()] = 10,      // 次品圆棒
        [MaterialType.DefectRoughTube.ToString()] = 11,     // 次品荒管
        [MaterialType.DefectFinished.ToString()] = 12,      // 次品成品
        [MaterialType.DefectSemi.ToString()] = 13,          // 次品半成品
        [MaterialType.DefectWIP.ToString()] = 14,           // 次品在制
        [MaterialType.Scrap.ToString()] = 15                // 报废品
    };

    private static readonly Dictionary<string, int> InboundSourceDisplayOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        [InboundSource.Purchase.ToString()] = 1,             // 外购
        [InboundSource.Subcontract.ToString()] = 2,          // 委外
        [InboundSource.ProductionInbound.ToString()] = 3,    // 生产入库
        [InboundSource.InspectionInbound.ToString()] = 4,    // 检验入库
        [InboundSource.Other.ToString()] = 5                 // 其它
    };

    private static readonly Dictionary<string, int> OutboundTypeDisplayOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        [OutboundType.ProductionPick.ToString()] = 1,        // 生产领用
        [OutboundType.SalesOut.ToString()] = 2,              // 销售出库
        [OutboundType.ReturnOut.ToString()] = 3,             // 退货出库
        [OutboundType.SubcontractOut.ToString()] = 4,        // 委外出库
        [OutboundType.OtherOut.ToString()] = 5               // 其它出库
    };

    /// <summary>
    /// 物料进出存报表（月度库存变化）汇总：行=库房×物料类型（库房合并单元格），
    /// 列=期初（上年末全口径结存）+ 12月×（入/出/结）+ 全年合计。
    /// 同一数据集支撑 4 报表切换（入库/出库/库存/物料进出存，仅展示列不同）。
    /// 入库/出库报表额外返回来源/类型粒度行（InboundSourceRows/OutboundTypeRows）。
    /// 结存为真实库存余额（全口径），无入/出筛选维度、无合计行。
    /// </summary>
    public async Task<MonthlyStockSummaryResultDto> GetMonthlyStockSummaryAsync()
    {
        var year = DateTime.Today.Year;
        var start = new DateTime(year, 1, 1);

        // ===== 1. 加载库房名 + 全部入库批次（期初需截至去年底的累计）/ 出库记录 =====
        var warehouseNames = await _context.Warehouses.AsNoTracking()
            .ToDictionaryAsync(w => w.Id, w => w.Name ?? string.Empty);

        var inboundRows = await _context.InventoryBatches
            .AsNoTracking()
            .Select(b => new { b.WarehouseId, b.MaterialType, b.InboundSource, b.InboundDate, b.InitialWeight })
            .ToListAsync();

        var outboundRows = await _context.OutboundRecords
            .AsNoTracking()
            .Join(_context.InventoryBatches.AsNoTracking(),
                r => r.InventoryBatchId,
                b => b.Id,
                (r, b) => new { b.WarehouseId, b.MaterialType, r.OutboundType, r.OutboundDate, r.OutboundWeight })
            .ToListAsync();

        // ===== 2. 按 (库房, 物料类型) 归行聚合 =====
        var rows = new Dictionary<string, MonthlyStockRowDto>(StringComparer.OrdinalIgnoreCase);

        MonthlyStockRowDto GetOrAdd(int warehouseId, string materialType)
        {
            var key = warehouseId + "|" + materialType;
            if (!rows.TryGetValue(key, out var row))
            {
                row = new MonthlyStockRowDto
                {
                    WarehouseName = warehouseNames.TryGetValue(warehouseId, out var name) ? name : string.Empty,
                    MaterialType = materialType,
                    Months = Enumerable.Range(0, 12).Select(_ => new MonthlyStockMonthValueDto()).ToList()
                };
                rows[key] = row;
            }
            return row;
        }

        // 期初 = 截至上年末的入−出
        foreach (var r in inboundRows.Where(r => r.InboundDate < start))
            GetOrAdd(r.WarehouseId, Norm(r.MaterialType)).OpeningWeight += r.InitialWeight;
        foreach (var r in outboundRows.Where(r => r.OutboundDate < start))
            GetOrAdd(r.WarehouseId, Norm(r.MaterialType)).OpeningWeight -= r.OutboundWeight;

        // 本年各月入/出
        foreach (var r in inboundRows.Where(r => r.InboundDate >= start))
            GetOrAdd(r.WarehouseId, Norm(r.MaterialType)).Months[r.InboundDate.Month - 1].In += r.InitialWeight;
        foreach (var r in outboundRows.Where(r => r.OutboundDate >= start))
            GetOrAdd(r.WarehouseId, Norm(r.MaterialType)).Months[r.OutboundDate.Month - 1].Out += r.OutboundWeight;

        // ===== 3. 逐月递推真实结存 + 全年合计 + 整行全 0 隐藏 =====
        var result = new List<MonthlyStockRowDto>();
        foreach (var row in rows.Values)
        {
            decimal closing = row.OpeningWeight;
            for (var i = 0; i < 12; i++)
            {
                var mv = row.Months[i];
                closing += mv.In - mv.Out;
                mv.Closing = closing;
                row.TotalIn += mv.In;
                row.TotalOut += mv.Out;
            }
            row.ClosingWeight = closing;
            if (row.OpeningWeight == 0m && row.TotalIn == 0m && row.TotalOut == 0m) continue;
            result.Add(row);
        }

        // ===== 4. 排序：库房固定顺序（原料库→成品库→在制品库→次品库）+ 物料类型固定顺序 =====
        result = result
            .OrderBy(r => GetWarehouseOrder(r.WarehouseName))
            .ThenBy(r => r.WarehouseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => GetMaterialOrder(r.MaterialType))
            .ThenBy(r => r.MaterialType, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ===== 5. 入库报表粒度行：库房×物料类型×入库来源（仅本年入，来源固定顺序，全 0 隐藏）=====
        var sourceRows = new Dictionary<string, MonthlyStockRowDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in inboundRows.Where(r => r.InboundDate >= start))
        {
            var key = r.WarehouseId + "|" + Norm(r.MaterialType) + "|" + Norm(r.InboundSource);
            if (!sourceRows.TryGetValue(key, out var row))
            {
                row = new MonthlyStockRowDto
                {
                    WarehouseName = warehouseNames.TryGetValue(r.WarehouseId, out var name) ? name : string.Empty,
                    MaterialType = Norm(r.MaterialType),
                    InboundSource = Norm(r.InboundSource),
                    Months = Enumerable.Range(0, 12).Select(_ => new MonthlyStockMonthValueDto()).ToList()
                };
                sourceRows[key] = row;
            }
            row.Months[r.InboundDate.Month - 1].In += r.InitialWeight;
        }
        var inboundSourceResult = new List<MonthlyStockRowDto>();
        foreach (var row in sourceRows.Values)
        {
            for (var i = 0; i < 12; i++) row.TotalIn += row.Months[i].In;
            if (row.TotalIn == 0m) continue;
            inboundSourceResult.Add(row);
        }
        inboundSourceResult = inboundSourceResult
            .OrderBy(r => GetWarehouseOrder(r.WarehouseName))
            .ThenBy(r => r.WarehouseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => GetMaterialOrder(r.MaterialType))
            .ThenBy(r => r.MaterialType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => GetInboundSourceOrder(r.InboundSource))
            .ThenBy(r => r.InboundSource, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ===== 6. 出库报表粒度行：库房×物料类型×出库类型（仅本年出，类型固定顺序，全 0 隐藏）=====
        var typeRows = new Dictionary<string, MonthlyStockRowDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in outboundRows.Where(r => r.OutboundDate >= start))
        {
            var key = r.WarehouseId + "|" + Norm(r.MaterialType) + "|" + r.OutboundType.ToString();
            if (!typeRows.TryGetValue(key, out var row))
            {
                row = new MonthlyStockRowDto
                {
                    WarehouseName = warehouseNames.TryGetValue(r.WarehouseId, out var name) ? name : string.Empty,
                    MaterialType = Norm(r.MaterialType),
                    OutboundType = r.OutboundType.ToString(),
                    Months = Enumerable.Range(0, 12).Select(_ => new MonthlyStockMonthValueDto()).ToList()
                };
                typeRows[key] = row;
            }
            row.Months[r.OutboundDate.Month - 1].Out += r.OutboundWeight;
        }
        var outboundTypeResult = new List<MonthlyStockRowDto>();
        foreach (var row in typeRows.Values)
        {
            for (var i = 0; i < 12; i++) row.TotalOut += row.Months[i].Out;
            if (row.TotalOut == 0m) continue;
            outboundTypeResult.Add(row);
        }
        outboundTypeResult = outboundTypeResult
            .OrderBy(r => GetWarehouseOrder(r.WarehouseName))
            .ThenBy(r => r.WarehouseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => GetMaterialOrder(r.MaterialType))
            .ThenBy(r => r.MaterialType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => GetOutboundTypeOrder(r.OutboundType))
            .ThenBy(r => r.OutboundType, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MonthlyStockSummaryResultDto
        {
            Year = year,
            MonthLabels = Enumerable.Range(1, 12).Select(m => new DateTime(year, m, 1).ToString("yyyy-MM")).ToList(),
            Rows = result,
            InboundSourceRows = inboundSourceResult,
            OutboundTypeRows = outboundTypeResult
        };
    }

    private static int GetWarehouseOrder(string name)
        => WarehouseDisplayOrder.TryGetValue(name, out var o) ? o : int.MaxValue;

    private static int GetMaterialOrder(string type)
    {
        if (MaterialTypeDisplayOrder.TryGetValue(type, out var o)) return o;
        if (EnumHelper.TryParse<MaterialType>(type) is { } parsed
            && MaterialTypeDisplayOrder.TryGetValue(parsed.ToString(), out var o2)) return o2;
        return int.MaxValue;
    }

    private static int GetInboundSourceOrder(string? source)
        => InboundSourceDisplayOrder.TryGetValue(Norm(source), out var o) ? o : int.MaxValue;

    private static int GetOutboundTypeOrder(string? type)
        => OutboundTypeDisplayOrder.TryGetValue(Norm(type), out var o) ? o : int.MaxValue;

    private static string Norm(string? s) => string.IsNullOrEmpty(s) ? string.Empty : s;
}
