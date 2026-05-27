using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Mapping;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly SemaphoreSlim _batchNoLock = new(1, 1);

    public InventoryService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    private string GetCurrentUser()
    {
        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userName))
            return userName;

        var emailClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email);
        if (emailClaim != null)
            return emailClaim.Value;

        return "system";
    }

    /// <summary>
    /// 根据工单号自动填充订单号和项次ID列表（字典查找，无DB查询）
    /// </summary>
    private static void AutoFillWorkOrderInfo(InventoryBatch entity, Dictionary<string, WorkOrder> workOrders)
    {
        if (string.IsNullOrEmpty(entity.WorkOrderNo))
            return;

        if (workOrders.TryGetValue(entity.WorkOrderNo, out var workOrder))
        {
            entity.SalesOrderNo = workOrder.SalesOrderNo;
            entity.OrderItemIds = workOrder.OrderItemIds;
        }
    }

    public async Task<PagedResult<InventoryBatchDto>> GetPagedAsync(InventoryQueryParams query)
    {
        var queryable = BuildInventoryQuery(query);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(b => b.ToDto())
            .ToListAsync();

        // 填充仓库名称
        await FillWarehouseNamesAsync(items);

        return new PagedResult<InventoryBatchDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    /// <summary>
    /// 全量加载库存批次（无分页，供前端 Items 模式使用）
    /// </summary>
    public async Task<List<InventoryBatchDto>> GetAllListAsync(InventoryQueryParams query)
    {
        var queryable = BuildInventoryQuery(query);

        var items = await queryable
            .Select(b => b.ToDto())
            .ToListAsync();

        // 填充仓库名称
        await FillWarehouseNamesAsync(items);

        return items;
    }

    /// <summary>
    /// 构建库存批次查询（含筛选 + 排序，不含分页）
    /// </summary>
    private IQueryable<InventoryBatch> BuildInventoryQuery(InventoryQueryParams query)
    {
        var queryable = _context.InventoryBatches
            .AsNoTracking()
            .AsQueryable();

        // 关键字搜索（按空格拆分多词 AND 匹配所有展示字段）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                queryable = queryable.Where(b =>
                    b.BatchNo.Contains(keyword) ||
                    (b.SourceOrderNo != null && b.SourceOrderNo.Contains(keyword)) ||
                    (b.SourceName != null && b.SourceName.Contains(keyword)) ||
                    b.MaterialType.Contains(keyword) ||
                    b.PlantGrade.Contains(keyword) ||
                    b.Specification.Contains(keyword) ||
                    (b.HeatNo != null && b.HeatNo.Contains(keyword)) ||
                    (b.SurfaceCondition != null && b.SurfaceCondition.Contains(keyword)) ||
                    (b.WorkOrderNo != null && b.WorkOrderNo.Contains(keyword)) ||
                    (b.ProductionBatchNo != null && b.ProductionBatchNo.Contains(keyword)) ||
                    b.InboundSource.Contains(keyword) ||
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

        // ========== 表头列筛选 ==========
        if (!string.IsNullOrEmpty(query.BatchNo))
            queryable = queryable.Where(b => b.BatchNo.Contains(query.BatchNo));

        if (!string.IsNullOrEmpty(query.InboundSource))
            queryable = queryable.Where(b => b.InboundSource == query.InboundSource);

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

        if (!string.IsNullOrEmpty(query.SurfaceCondition))
            queryable = queryable.Where(b => b.SurfaceCondition == query.SurfaceCondition);

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

        // 处理 WarehouseName 筛选（由 FillWarehouseNamesAsync 后处理填充，非 InventoryBatch 直接属性）
        if (query.Filters != null)
        {
            var whFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("WarehouseName", StringComparison.OrdinalIgnoreCase));
            if (whFilter != null)
            {
                var op = whFilter.Operator?.ToLowerInvariant() ?? "contains";
                if (op == "in" && whFilter.Values?.Count > 0)
                    queryable = queryable.Where(b => _context.Warehouses.Any(w => w.Id == b.WarehouseId && whFilter.Values.Contains(w.Name)));
                else if (!string.IsNullOrEmpty(whFilter.Value))
                    queryable = queryable.Where(b => _context.Warehouses.Any(w => w.Id == b.WarehouseId && w.Name.Contains(whFilter.Value)));
                query.Filters.Remove(whFilter);
            }
        }

        queryable = queryable.ApplyFilters(query.Filters);

        // 排序
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
            "surfacecondition" => query.IsDescending
                ? queryable.OrderByDescending(b => b.SurfaceCondition ?? "")
                : queryable.OrderBy(b => b.SurfaceCondition ?? ""),
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
            "actualouterdiameter" => query.IsDescending
                ? queryable.OrderByDescending(b => b.ActualOuterDiameter ?? 0)
                : queryable.OrderBy(b => b.ActualOuterDiameter ?? 0),
            "actualwallthickness" => query.IsDescending
                ? queryable.OrderByDescending(b => b.ActualWallThickness ?? 0)
                : queryable.OrderBy(b => b.ActualWallThickness ?? 0),
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
            "warehousename" => query.IsDescending
                ? queryable.Join(_context.Warehouses, b => b.WarehouseId, w => w.Id, (b, w) => new { b, w.Name }).OrderByDescending(x => x.Name).Select(x => x.b)
                : queryable.Join(_context.Warehouses, b => b.WarehouseId, w => w.Id, (b, w) => new { b, w.Name }).OrderBy(x => x.Name).Select(x => x.b),
            _ => query.IsDescending
                ? queryable.OrderByDescending(b => b.CreatedTime)
                : queryable.OrderBy(b => b.CreatedTime)
        };

        return queryable;
    }

    /// <summary>
    /// 填充批次列表的仓库名称
    /// </summary>
    private async Task FillWarehouseNamesAsync(List<InventoryBatchDto> items)
    {
        var warehouseIds = items.Select(i => i.WarehouseId).Distinct();
        var warehouses = await _context.Warehouses
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name);

        foreach (var item in items)
        {
            if (warehouses.TryGetValue(item.WarehouseId, out var name))
                item.WarehouseName = name;
        }
    }

    public async Task<BatchInboundResult> BatchInboundAsync(BatchInboundRequest request)
    {
        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId);

        if (warehouse == null)
            throw new BusinessException("仓库不存在");

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var results = new List<string>();

            // 预生成批次号序列（避免每行查询数据库导致重复）
            var batchNos = await GenerateBatchNoSequenceAsync(request.Rows.Count);

            // 预加载所有工单（避免循环中 N+1 查询）
            var distinctWoNos = request.Rows
                .Select(r => r.WorkOrderNo ?? request.WorkOrderNo)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList();
            var workOrders = distinctWoNos.Count > 0
                ? await _context.WorkOrders
                    .AsNoTracking()
                    .Where(w => distinctWoNos.Contains(w.WorkOrderNo))
                    .ToDictionaryAsync(w => w.WorkOrderNo, w => w)
                : new Dictionary<string, WorkOrder>();

            for (int i = 0; i < request.Rows.Count; i++)
            {
                var row = request.Rows[i];
                var batchNo = batchNos[i];

                // 公共字段 + 行级字段合并（行级优先）
                var entity = new InventoryBatch
                {
                    BatchNo = batchNo,
                    WarehouseId = request.WarehouseId,
                    MaterialType = row.MaterialType ?? request.MaterialType ?? string.Empty,
                    PlantGrade = row.PlantGrade ?? request.PlantGrade ?? string.Empty,
                    Specification = row.Specification ?? request.Specification ?? string.Empty,
                    InboundSource = row.InboundSource ?? request.InboundSource ?? string.Empty,
                    SourceName = row.SourceName ?? request.SourceName ?? string.Empty,
                    InboundDate = request.InboundDate ?? DateTime.Today,
                    HeatNo = row.HeatNo ?? request.HeatNo,
                    ProductionBatchNo = row.ProductionBatchNo ?? request.ProductionBatchNo,
                    LengthStatus = row.LengthStatus ?? request.LengthStatus,
                    MinLength = row.MinLength ?? request.MinLength,
                    MaxLength = row.MaxLength ?? request.MaxLength,
                    InitialQuantity = row.InitialQuantity,
                    InitialWeight = row.InitialWeight,
                    UnitWeight = row.UnitWeight ?? request.UnitWeight,
                    Meters = row.Meters ?? request.Meters,
                    RemainingQuantity = row.InitialQuantity,
                    RemainingWeight = row.InitialWeight,
                    ActualSpecification = row.ActualSpecification ?? request.ActualSpecification,
                    ActualOuterDiameter = row.ActualOuterDiameter ?? request.ActualOuterDiameter,
                    ActualWallThickness = row.ActualWallThickness ?? request.ActualWallThickness,
                    SurfaceCondition = row.SurfaceCondition ?? request.SurfaceCondition,
                    LocationArea = row.LocationArea ?? request.LocationArea,
                    LocationRack = row.LocationRack ?? request.LocationRack,
                    Remark = row.Remark,
                    DefectReason = row.DefectReason ?? request.DefectReason,
                    LiabilityType = row.LiabilityType ?? request.LiabilityType,
                    OriginalSupplier = row.OriginalSupplier ?? request.OriginalSupplier,
                    TagNo = row.TagNo ?? request.TagNo,
                    DefectRemark = row.DefectRemark ?? request.DefectRemark,
                    IsLinkedToWorkOrder = row.IsLinkedToWorkOrder ?? request.IsLinkedToWorkOrder ?? false,
                    WorkOrderNo = row.WorkOrderNo ?? request.WorkOrderNo,
                    SalesOrderNo = row.SalesOrderNo ?? request.SalesOrderNo,
                    OrderItemIds = row.OrderItemIds ?? request.OrderItemIds,
                    SourceOrderNo = row.SourceOrderNo ?? request.SourceOrderNo
                };

                // 如果有工单号，自动填充订单号和项次（字典查找，无DB查询）
                AutoFillWorkOrderInfo(entity, workOrders);

                _context.InventoryBatches.Add(entity);
                results.Add(batchNo);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // 自动同步采购单/委外单的收货状态
            var sourceOrderNos = request.Rows
                .Select(r => r.SourceOrderNo ?? request.SourceOrderNo)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList()!;
            await SyncSourceOrdersAsync(sourceOrderNos);

            return new BatchInboundResult
            {
                SuccessCount = results.Count,
                BatchNos = results
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<InventoryBatchDto> GetByIdAsync(int id)
    {
        var entity = await _context.InventoryBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException("批次不存在");

        var dto = entity.ToDto();

        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == entity.WarehouseId);
        if (warehouse != null)
            dto.WarehouseName = warehouse.Name;

        return dto;
    }

    public async Task<InventoryBatchDto> InboundAsync(CreateInboundRequest request)
    {
        // 验证仓库存在
        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId);

        if (warehouse == null)
            throw new BusinessException("仓库不存在");

        // 生成批次号
        var batchNo = await GenerateBatchNoAsync();

        var entity = new InventoryBatch
        {
            BatchNo = batchNo,
            WarehouseId = request.WarehouseId,
            MaterialType = request.MaterialType,
            PlantGrade = request.PlantGrade,
            Specification = request.Specification,
            InboundSource = request.InboundSource,
            SourceName = request.SourceName,
            InboundDate = request.InboundDate,
            HeatNo = request.HeatNo,
            ProductionBatchNo = request.ProductionBatchNo,
            LengthStatus = request.LengthStatus,
            MinLength = request.MinLength,
            MaxLength = request.MaxLength,
            InitialQuantity = request.InitialQuantity,
            InitialWeight = request.InitialWeight,
            UnitWeight = request.UnitWeight,
            Meters = request.Meters,
            RemainingQuantity = request.InitialQuantity,
            RemainingWeight = request.InitialWeight,
            ActualSpecification = request.ActualSpecification,
            ActualOuterDiameter = request.ActualOuterDiameter,
            ActualWallThickness = request.ActualWallThickness,
            SurfaceCondition = request.SurfaceCondition,
            LocationArea = request.LocationArea,
            LocationRack = request.LocationRack,
            Remark = request.Remark,
            DefectReason = request.DefectReason,
            LiabilityType = request.LiabilityType,
            OriginalSupplier = request.OriginalSupplier,
            TagNo = request.TagNo,
            DefectRemark = request.DefectRemark,
            IsLinkedToWorkOrder = request.IsLinkedToWorkOrder,
            WorkOrderNo = request.WorkOrderNo,
            SalesOrderNo = request.SalesOrderNo,
            OrderItemIds = request.OrderItemIds,
            SourceOrderNo = request.SourceOrderNo
        };

        // 如果有工单号，自动填充订单号和项次
        if (!string.IsNullOrEmpty(entity.WorkOrderNo))
        {
            var singleWo = await _context.WorkOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.WorkOrderNo == entity.WorkOrderNo);
            if (singleWo != null)
            {
                entity.SalesOrderNo = singleWo.SalesOrderNo;
                entity.OrderItemIds = singleWo.OrderItemIds;
            }
        }

        _context.InventoryBatches.Add(entity);
        await _context.SaveChangesAsync();

        var dto = entity.ToDto();
        dto.WarehouseName = warehouse.Name;
        return dto;
    }

    public async Task<OutboundRecordDto> OutboundAsync(CreateOutboundRequest request)
    {
        var batch = await _context.InventoryBatches
            .FirstOrDefaultAsync(b => b.Id == request.InventoryBatchId);

        if (batch == null)
            throw new BusinessException("批次不存在");

        if (batch.RemainingQuantity < request.OutboundQuantity)
            throw new BusinessException($"剩余支数不足（剩余{batch.RemainingQuantity}，出库{request.OutboundQuantity}）");

        if (batch.RemainingWeight < request.OutboundWeight)
            throw new BusinessException($"剩余重量不足（剩余{batch.RemainingWeight:G29}kg，出库{request.OutboundWeight:G29}kg）");

        // 更新剩余量
        batch.RemainingQuantity -= request.OutboundQuantity;
        batch.RemainingWeight -= request.OutboundWeight;

        var record = new OutboundRecord
        {
            InventoryBatchId = request.InventoryBatchId,
            OutboundType = Enum.Parse<OutboundType>(request.OutboundType),
            SourceOrderNo = request.SourceOrderNo,
            TargetCompany = request.TargetCompany,
            OutboundQuantity = request.OutboundQuantity,
            OutboundWeight = request.OutboundWeight,
            OutboundDate = request.OutboundDate,
            Remark = request.Remark,
        };

        _context.OutboundRecords.Add(record);
        await _context.SaveChangesAsync();

        var dto = record.ToDto();
        dto.BatchNo = batch.BatchNo;
        return dto;
    }

    public async Task<BatchOutboundResult> BatchOutboundAsync(BatchOutboundRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 一次性加载所有批次（N+1 → 1）
            var batchIds = request.Items.Select(i => i.InventoryBatchId).Distinct().ToList();
            var batches = await _context.InventoryBatches
                .Where(b => batchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            var results = new List<OutboundRecordDto>();

            foreach (var item in request.Items)
            {
                if (!batches.TryGetValue(item.InventoryBatchId, out var batch))
                    throw new BusinessException($"批次ID={item.InventoryBatchId}不存在");

                if (batch.RemainingQuantity < item.OutboundQuantity)
                    throw new BusinessException($"批次{batch.BatchNo}剩余支数不足（剩余{batch.RemainingQuantity}，出库{item.OutboundQuantity}）");

                if (batch.RemainingWeight < item.OutboundWeight)
                    throw new BusinessException($"批次{batch.BatchNo}剩余重量不足（剩余{batch.RemainingWeight:G29}kg，出库{item.OutboundWeight:G29}kg）");

                // 更新剩余量
                batch.RemainingQuantity -= item.OutboundQuantity;
                batch.RemainingWeight -= item.OutboundWeight;

                var record = new OutboundRecord
                {
                    InventoryBatchId = item.InventoryBatchId,
                    OutboundType = Enum.Parse<OutboundType>(item.OutboundType ?? request.OutboundType),
                    SourceOrderNo = item.SourceOrderNo ?? request.SourceOrderNo,
                    TargetCompany = item.TargetCompany ?? request.TargetCompany,
                    OutboundQuantity = item.OutboundQuantity,
                    OutboundWeight = item.OutboundWeight,
                    OutboundDate = request.OutboundDate,
                    Remark = item.Remark ?? request.Remark,
                };

                _context.OutboundRecords.Add(record);

                var dto = record.ToDto();
                dto.BatchNo = batch.BatchNo;
                results.Add(dto);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new BatchOutboundResult
            {
                SuccessCount = results.Count,
                Records = results
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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
                queryable = queryable.Where(r =>
                    (r.TargetCompany != null && r.TargetCompany.Contains(keyword)) ||
                    (r.CreatedBy != null && r.CreatedBy.Contains(keyword)) ||
                    (r.SourceOrderNo != null && r.SourceOrderNo.Contains(keyword)) ||
                    (r.Remark != null && r.Remark.Contains(keyword)) ||
                    (r.OutboundType.ToString().Contains(keyword)) ||
                    _context.InventoryBatches.Any(b => b.Id == r.InventoryBatchId && b.BatchNo.Contains(keyword)));
            }
        }

        // 跨表计算字段筛选（非 OutboundRecord 直接属性，ApplyFilters 无法处理）
        if (query.Filters != null)
        {
            var batchNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("BatchNo", StringComparison.OrdinalIgnoreCase));
            if (batchNoFilter != null)
            {
                var op = batchNoFilter.Operator?.ToLowerInvariant() ?? "contains";
                if (op == "in" && batchNoFilter.Values?.Count > 0)
                    queryable = queryable.Where(r => _context.InventoryBatches.Any(b => b.Id == r.InventoryBatchId && batchNoFilter.Values.Contains(b.BatchNo)));
                else if (!string.IsNullOrEmpty(batchNoFilter.Value))
                    queryable = queryable.Where(r => _context.InventoryBatches.Any(b => b.Id == r.InventoryBatchId && b.BatchNo.Contains(batchNoFilter.Value)));
                query.Filters.Remove(batchNoFilter);
            }
        }

        queryable = queryable.ApplyFilters(query.Filters);

        queryable = query.SortBy?.ToLower() switch
        {
            "batchno" => query.IsDescending
                ? queryable.OrderByDescending(r => _context.InventoryBatches
                    .Where(b => b.Id == r.InventoryBatchId)
                    .Select(b => b.BatchNo)
                    .FirstOrDefault() ?? "")
                : queryable.OrderBy(r => _context.InventoryBatches
                    .Where(b => b.Id == r.InventoryBatchId)
                    .Select(b => b.BatchNo)
                    .FirstOrDefault() ?? ""),
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
            _ => query.IsDescending
                ? queryable.OrderByDescending(r => r.OutboundDate)
                : queryable.OrderBy(r => r.OutboundDate)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => r.ToDto())
            .ToListAsync();

        // 填充批次号和仓库名称
        var batchIds = items.Select(i => i.InventoryBatchId).Distinct();
        var batchDict = await _context.InventoryBatches
            .Where(b => batchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BatchNo, b.WarehouseId })
            .ToDictionaryAsync(b => b.Id);

        var whIds = batchDict.Values.Select(b => b.WarehouseId).Distinct();
        var warehouseNames = await _context.Warehouses
            .Where(w => whIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name);

        foreach (var item in items)
        {
            if (batchDict.TryGetValue(item.InventoryBatchId, out var batchInfo))
            {
                item.BatchNo = batchInfo.BatchNo;
                // 填充仓库名称
                if (warehouseNames.TryGetValue(batchInfo.WarehouseId, out var whName))
                    item.WarehouseName = whName;
            }
        }

        return new PagedResult<OutboundRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<InventoryBatchDto> UpdateInventoryBatchAsync(int id, UpdateInventoryBatchRequest request)
    {
        var entity = await _context.InventoryBatches
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException("入库批次不存在");

        // 记录旧值用于判断是否需要同步
        var oldSourceOrderNo = entity.SourceOrderNo;
        var oldQuantity = entity.InitialQuantity;
        var oldWeight = entity.InitialWeight;

        // 所有可空 DTO 字段用 ?? entity.Field 防止空值覆盖
        entity.BatchNo = request.BatchNo ?? entity.BatchNo;
        entity.MaterialType = request.MaterialType ?? entity.MaterialType;
        entity.PlantGrade = request.PlantGrade ?? entity.PlantGrade;
        entity.Specification = request.Specification ?? entity.Specification;
        entity.InboundSource = request.InboundSource ?? entity.InboundSource;
        entity.SourceName = request.SourceName ?? entity.SourceName;
        if (request.InboundDate.HasValue) entity.InboundDate = request.InboundDate.Value;
        entity.HeatNo = request.HeatNo ?? entity.HeatNo;
        entity.ProductionBatchNo = request.ProductionBatchNo ?? entity.ProductionBatchNo;
        entity.LengthStatus = request.LengthStatus ?? entity.LengthStatus;
        entity.MinLength = request.MinLength ?? entity.MinLength;
        entity.MaxLength = request.MaxLength ?? entity.MaxLength;
        // InitialQuantity/InitialWeight 在下方的剩余量计算块中处理（需先计算已出库差量）
        entity.UnitWeight = request.UnitWeight ?? entity.UnitWeight;
        entity.Meters = request.Meters ?? entity.Meters;
        entity.ActualSpecification = request.ActualSpecification ?? entity.ActualSpecification;
        entity.ActualOuterDiameter = request.ActualOuterDiameter ?? entity.ActualOuterDiameter;
        entity.ActualWallThickness = request.ActualWallThickness ?? entity.ActualWallThickness;
        entity.SurfaceCondition = request.SurfaceCondition ?? entity.SurfaceCondition;
        entity.LocationArea = request.LocationArea ?? entity.LocationArea;
        entity.LocationRack = request.LocationRack ?? entity.LocationRack;
        entity.Remark = request.Remark ?? entity.Remark;
        entity.DefectReason = request.DefectReason ?? entity.DefectReason;
        entity.LiabilityType = request.LiabilityType ?? entity.LiabilityType;
        entity.OriginalSupplier = request.OriginalSupplier ?? entity.OriginalSupplier;
        entity.TagNo = request.TagNo ?? entity.TagNo;
        entity.DefectRemark = request.DefectRemark ?? entity.DefectRemark;
        if (request.IsLinkedToWorkOrder.HasValue) entity.IsLinkedToWorkOrder = request.IsLinkedToWorkOrder.Value;
        entity.WorkOrderNo = request.WorkOrderNo ?? entity.WorkOrderNo;
        if (request.WorkOrderNo != null)
        {
            var woEntity = await _context.WorkOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.WorkOrderNo == request.WorkOrderNo);
            if (woEntity != null)
            {
                entity.SalesOrderNo = woEntity.SalesOrderNo;
                entity.OrderItemIds = woEntity.OrderItemIds;
            }
        }
        entity.SalesOrderNo = request.SalesOrderNo ?? entity.SalesOrderNo;
        entity.OrderItemIds = request.OrderItemIds ?? entity.OrderItemIds;
        entity.SourceOrderNo = request.SourceOrderNo ?? entity.SourceOrderNo;

        // 如果修改了数量或重量，基于已出库差量计算剩余量
        if (request.InitialQuantity.HasValue)
        {
            var outboundTotalQty = entity.InitialQuantity - entity.RemainingQuantity;
            var newRemaining = request.InitialQuantity.Value - outboundTotalQty;
            if (newRemaining < 0)
                throw new BusinessException($"批次{entity.BatchNo}已出库{outboundTotalQty}支，新入库量{request.InitialQuantity.Value}支不足覆盖（差额{Math.Abs(newRemaining)}支），请先更正出库后再更改入库");
            entity.RemainingQuantity = newRemaining;
            entity.InitialQuantity = request.InitialQuantity.Value;
        }
        if (request.InitialWeight.HasValue)
        {
            var outboundTotalWt = entity.InitialWeight - entity.RemainingWeight;
            var newRemainingWt = request.InitialWeight.Value - outboundTotalWt;
            if (newRemainingWt < 0)
                throw new BusinessException($"批次{entity.BatchNo}已出库{outboundTotalWt:G29}kg，新入库量{request.InitialWeight.Value:G29}kg不足覆盖（差额{Math.Abs(newRemainingWt):G29}kg），请先更正出库后再更改入库");
            entity.RemainingWeight = newRemainingWt;
            entity.InitialWeight = request.InitialWeight.Value;
        }

        entity.UpdatedTime = DateTimeOffset.Now;
        entity.UpdatedBy = GetCurrentUser();

        await _context.SaveChangesAsync();

        var dto = entity.ToDto();

        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == entity.WarehouseId);
        if (warehouse != null)
            dto.WarehouseName = warehouse.Name;

        // 数量/重量或来源单号变更时，自动同步采购单/委外单的收货状态
        var sourceChanged = request.SourceOrderNo != oldSourceOrderNo;
        var qtyChanged = request.InitialQuantity.HasValue && request.InitialQuantity.Value != oldQuantity;
        var wtChanged = request.InitialWeight.HasValue && request.InitialWeight.Value != oldWeight;
        if (sourceChanged || qtyChanged || wtChanged)
        {
            var nos = new List<string>();
            if (!string.IsNullOrEmpty(oldSourceOrderNo)) nos.Add(oldSourceOrderNo);
            if (!string.IsNullOrEmpty(request.SourceOrderNo) && request.SourceOrderNo != oldSourceOrderNo)
                nos.Add(request.SourceOrderNo);
            if (nos.Count > 0)
                await SyncSourceOrdersAsync(nos);
        }

        return dto;
    }

    public async Task HardDeleteInventoryBatchAsync(int id)
    {
        var entity = await _context.InventoryBatches
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException("入库批次不存在");

        // 检查是否存在出库记录，有则阻止删除
        var hasOutbounds = await _context.OutboundRecords
            .AnyAsync(r => r.InventoryBatchId == id);
        if (hasOutbounds)
            throw new BusinessException($"批次{entity.BatchNo}存在出库记录，无法直接删除。请先在出库历史中删除关联的出库记录后重试");

        // 物理删除批次
        var sourceOrderNo = entity.SourceOrderNo;
        _context.InventoryBatches.Remove(entity);
        await _context.SaveChangesAsync();

        // 自动同步采购单/委外单的收货状态
        if (!string.IsNullOrEmpty(sourceOrderNo))
            await SyncSourceOrdersAsync(new List<string> { sourceOrderNo });
    }

    public async Task<OutboundRecordDto> UpdateOutboundRecordAsync(long id, UpdateOutboundRecordRequest request)
    {
        var entity = await _context.OutboundRecords
            .FirstOrDefaultAsync(r => r.Id == id);

        if (entity == null)
            throw new BusinessException("出库记录不存在");

        var oldQty = entity.OutboundQuantity;
        var oldWt = entity.OutboundWeight;

        if (request.OutboundType != null) entity.OutboundType = Enum.Parse<OutboundType>(request.OutboundType);
        entity.SourceOrderNo = request.SourceOrderNo ?? entity.SourceOrderNo;
        entity.TargetCompany = request.TargetCompany ?? entity.TargetCompany;
        if (request.OutboundQuantity.HasValue) entity.OutboundQuantity = request.OutboundQuantity.Value;
        if (request.OutboundWeight.HasValue) entity.OutboundWeight = request.OutboundWeight.Value;
        if (request.OutboundDate.HasValue) entity.OutboundDate = request.OutboundDate.Value;
        entity.Remark = request.Remark ?? entity.Remark;

        // 数量/重量变化时，调整库存批次的剩余量（delta = new - old，剩余量 -= delta）
        var deltaQty = entity.OutboundQuantity - oldQty;
        var deltaWt = entity.OutboundWeight - oldWt;
        if (deltaQty != 0 || deltaWt != 0)
        {
            var batch = await _context.InventoryBatches
                .FirstOrDefaultAsync(b => b.Id == entity.InventoryBatchId);
            if (batch == null)
                throw new BusinessException("关联的库存批次不存在");
            if (batch.RemainingQuantity < deltaQty)
                throw new BusinessException($"批次{batch.BatchNo}剩余支数不足（剩余{batch.RemainingQuantity}，调整差额{deltaQty}）");
            if (batch.RemainingWeight < deltaWt)
                throw new BusinessException($"批次{batch.BatchNo}剩余重量不足（剩余{batch.RemainingWeight:G29}kg，调整差额{deltaWt:G29}kg）");

            batch.RemainingQuantity -= deltaQty;
            batch.RemainingWeight -= deltaWt;
        }

        await _context.SaveChangesAsync();

        var dto = entity.ToDto();

        // 填充批次号
        var batchDto = await _context.InventoryBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == entity.InventoryBatchId);
        if (batchDto != null)
        {
            dto.BatchNo = batchDto.BatchNo;
            var wh = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == batchDto.WarehouseId);
            if (wh != null)
                dto.WarehouseName = wh.Name;
        }

        return dto;
    }

    public async Task HardDeleteOutboundRecordAsync(long id)
    {
        var entity = await _context.OutboundRecords
            .FirstOrDefaultAsync(r => r.Id == id);

        if (entity == null)
            throw new BusinessException("出库记录不存在");

        // 恢复库存批次的剩余量
        var batch = await _context.InventoryBatches
            .FirstOrDefaultAsync(b => b.Id == entity.InventoryBatchId);
        if (batch != null)
        {
            batch.RemainingQuantity += entity.OutboundQuantity;
            batch.RemainingWeight += entity.OutboundWeight;
        }

        _context.OutboundRecords.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<string> GenerateBatchNoAsync()
    {
        await _batchNoLock.WaitAsync();
        try
        {
            var today = DateTime.Now.ToString("yyMMdd");
            var prefix = $"CK{today}";

            var lastBatch = await _context.InventoryBatches
                .AsNoTracking()
                .Where(b => b.BatchNo.StartsWith(prefix))
                .OrderByDescending(b => b.BatchNo)
                .FirstOrDefaultAsync();

            int sequence = 1;
            if (lastBatch != null && int.TryParse(lastBatch.BatchNo[^3..], out var lastSeq))
            {
                sequence = lastSeq + 1;
            }

            return $"{prefix}{sequence:D3}";
        }
        finally
        {
            _batchNoLock.Release();
        }
    }

    /// <summary>
    /// 批量预生成批次号（内存递增，避免 DB 查询重复）
    /// </summary>
    private async Task<List<string>> GenerateBatchNoSequenceAsync(int count)
    {
        await _batchNoLock.WaitAsync();
        try
        {
            var today = DateTime.Now.ToString("yyMMdd");
            var prefix = $"CK{today}";

            var lastBatch = await _context.InventoryBatches
                .AsNoTracking()
                .Where(b => b.BatchNo.StartsWith(prefix))
                .OrderByDescending(b => b.BatchNo)
                .FirstOrDefaultAsync();

            int sequence = 1;
            if (lastBatch != null && int.TryParse(lastBatch.BatchNo[^3..], out var lastSeq))
            {
                sequence = lastSeq + 1;
            }

            var results = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                results.Add($"{prefix}{sequence + i:D3}");
            }
            return results;
        }
        finally
        {
            _batchNoLock.Release();
        }
    }

    public async Task<SourceOrderValidationResult> ValidateSourceOrderAsync(string sourceOrderNo, string inboundSource, int? sourceOrderSequence = null)
    {
        var result = new SourceOrderValidationResult { IsValid = true };

        if (string.IsNullOrEmpty(sourceOrderNo))
        {
            result.Warnings.Add("来源单号为空");
            result.IsValid = false;
            return result;
        }

        if (inboundSource == "Purchase")
        {
            var order = await _context.PurchaseOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.OrderNo == sourceOrderNo);

            if (order == null)
            {
                result.Warnings.Add($"来源单号「{sourceOrderNo}」在采购订单中不存在");
                result.IsValid = false;
            }
            else
            {
                if (!string.IsNullOrEmpty(order.SourceWorkOrderNo))
                    result.ExpectedWorkOrderNo = order.SourceWorkOrderNo;
                result.MaterialCategory = order.MaterialCategory;
                result.PlantGrade = order.PlantGrade;
                result.Specification = order.Specification;
                if (order.SupplierId > 0)
                {
                    var supplier = await _context.SupplierProfiles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == order.SupplierId);
                    result.SupplierName = supplier?.SupplierName;
                }
            }
        }
        else if (inboundSource == "Subcontract")
        {
            var order = await _context.SubcontractOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrderNo == sourceOrderNo);

            if (order == null)
            {
                result.Warnings.Add($"来源单号「{sourceOrderNo}」在委外订单中不存在");
                result.IsValid = false;
            }
            else if (sourceOrderSequence.HasValue)
            {
                // 按 OrderNo + Sequence 定位 SubcontractReturnItem
                var item = await _context.SubcontractReturnItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.SubcontractOrderId == order.Id && i.Sequence == sourceOrderSequence.Value);

                if (item == null)
                {
                    result.Warnings.Add($"委外单「{sourceOrderNo}」中未找到序号 {sourceOrderSequence.Value} 的明细");
                    result.IsValid = false;
                }
                else
                {
                    result.MaterialCategory = item.MaterialCategory;
                    result.PlantGrade = item.PlantGrade;
                    result.Specification = item.ProcessSpecification;
                    if (!string.IsNullOrEmpty(item.SourceWorkOrderNo))
                        result.ExpectedWorkOrderNo = item.SourceWorkOrderNo;
                    // 供应商取自委外主表
                    if (order.SupplierId > 0)
                    {
                        var supplier = await _context.SupplierProfiles
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.Id == order.SupplierId);
                        result.SupplierName = supplier?.SupplierName;
                    }
                }
            }
            else
            {
                // 未提供序号时，取委外主表的发出信息（兼容旧行为）
                result.MaterialCategory = order.OutMaterialCategory;
                result.PlantGrade = order.OutPlantGrade;
                result.Specification = order.OutSpecification;
                if (order.SupplierId > 0)
                {
                    var supplier = await _context.SupplierProfiles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == order.SupplierId);
                    result.SupplierName = supplier?.SupplierName;
                }
            }
        }
        // 其他入库来源不验证

        return result;
    }

    public async Task<List<string>> ValidateWarehouseWorkOrderNosAsync(int warehouseId)
    {
        // 查询本仓库中有工单号的入库批次
        var workOrderNos = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.WarehouseId == warehouseId
                     && b.WorkOrderNo != null
                     && b.WorkOrderNo != string.Empty)
            .Select(b => b.WorkOrderNo!)
            .Distinct()
            .ToListAsync();

        if (workOrderNos.Count == 0)
            return new List<string>();

        // 查询这些工单号在工单表中是否存在
        var existingWorkOrderNos = await _context.WorkOrders
            .AsNoTracking()
            .Where(w => workOrderNos.Contains(w.WorkOrderNo))
            .Select(w => w.WorkOrderNo)
            .ToListAsync();

        var existingSet = existingWorkOrderNos.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 返回不存在的工单号
        return workOrderNos.Where(woNo => !existingSet.Contains(woNo)).ToList();
    }

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
        var items = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => request.Ids.Contains(b.Id))
            .Select(b => b.ToDto())
            .ToListAsync();

        // 填充仓库名称
        var whIds = items.Select(i => i.WarehouseId).Distinct();
        var warehouseNames = await _context.Warehouses
            .Where(w => whIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name);
        foreach (var item in items)
        {
            if (warehouseNames.TryGetValue(item.WarehouseId, out var wn))
                item.WarehouseName = wn;
        }

        return TablePrintHelper.GeneratePdf("入 库 批 次 打 印", items, request.Columns);
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
        return TablePrintHelper.GeneratePdf("出 库 历 史 列 表", paged.Items, request.Columns);
    }

    public async Task<byte[]> PrintOutboundSelectedAsync(OutboundPrintSelectedRequest request)
    {
        var items = await _context.OutboundRecords
            .AsNoTracking()
            .Where(r => request.Ids.Contains(r.Id))
            .Select(r => r.ToDto())
            .ToListAsync();

        // 填充批次号和仓库名称
        var batchIds = items.Select(i => i.InventoryBatchId).Distinct();
        var batchDict = await _context.InventoryBatches
            .Where(b => batchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BatchNo, b.WarehouseId })
            .ToDictionaryAsync(b => b.Id);
        var whIds = batchDict.Values.Select(b => b.WarehouseId).Distinct();
        var warehouseNames = await _context.Warehouses
            .Where(w => whIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name);
        foreach (var item in items)
        {
            if (batchDict.TryGetValue(item.InventoryBatchId, out var batchInfo))
            {
                item.BatchNo = batchInfo.BatchNo;
                if (warehouseNames.TryGetValue(batchInfo.WarehouseId, out var wn))
                    item.WarehouseName = wn;
            }
        }

        return TablePrintHelper.GeneratePdf("出 库 记 录 打 印", items, request.Columns);
    }

    public async Task<Dictionary<string, List<string>>> GetOutboundFilterContextsAsync()
    {
        var records = _context.OutboundRecords.AsNoTracking();

        return new Dictionary<string, List<string>>
        {
            ["BatchNo"] = await _context.InventoryBatches.AsNoTracking()
                .Where(b => _context.OutboundRecords.Select(r => r.InventoryBatchId).Contains(b.Id))
                .Select(b => b.BatchNo).Distinct().OrderBy(x => x).ToListAsync(),
            ["OutboundType"] = await records.Select(r => r.OutboundType.ToString()).Distinct().OrderBy(x => x).ToListAsync(),
            ["SourceOrderNo"] = await records.Where(r => r.SourceOrderNo != null).Select(r => r.SourceOrderNo!).Distinct().OrderBy(x => x).ToListAsync(),
            ["TargetCompany"] = await records.Where(r => r.TargetCompany != null).Select(r => r.TargetCompany!).Distinct().OrderBy(x => x).ToListAsync(),
            ["Remark"] = await records.Where(r => r.Remark != null).Select(r => r.Remark!).Distinct().OrderBy(x => x).ToListAsync(),
            ["CreatedBy"] = await records.Select(r => r.CreatedBy).Distinct().OrderBy(x => x).ToListAsync(),
        };
    }

    public async Task<Dictionary<string, List<string>>> GetInventoryFilterContextsAsync()
    {
        var results = await _context.InventoryBatches.AsNoTracking()
            .Select(b => new
            {
                b.BatchNo,
                b.InboundDate,
                b.SourceOrderNo,
                b.MaterialType,
                b.SourceName,
                b.SurfaceCondition,
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
                b.LengthStatus
            })
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["BatchNo"] = results.Select(x => x.BatchNo).Distinct().OrderBy(x => x).ToList(),
            ["InboundDate"] = results.Select(x => x.InboundDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["SourceOrderNo"] = results.Where(x => x.SourceOrderNo != null).Select(x => x.SourceOrderNo!).Distinct().OrderBy(x => x).ToList(),
            ["MaterialType"] = results.Select(x => x.MaterialType).Distinct().OrderBy(x => x).ToList(),
            ["SourceName"] = results.Select(x => x.SourceName).Distinct().OrderBy(x => x).ToList(),
            ["SurfaceCondition"] = results.Where(x => x.SurfaceCondition != null).Select(x => x.SurfaceCondition!).Distinct().OrderBy(x => x).ToList(),
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
            ["InboundSource"] = results.Select(x => x.InboundSource).Distinct().OrderBy(x => x).ToList(),
            ["LengthStatus"] = results.Where(x => x.LengthStatus != null).Select(x => x.LengthStatus!).Distinct().OrderBy(x => x).ToList(),
        };
    }

    /// <summary>
    /// 入库批次变更后自动同步采购单/委外单的收货数量及状态
    /// </summary>
    private async Task SyncSourceOrdersAsync(List<string> sourceOrderNos)
    {
        if (sourceOrderNos.Count == 0) return;

        var changed = false;

        // 同步采购单
        var purchaseOrders = await _context.PurchaseOrders
            .Where(p => sourceOrderNos.Contains(p.OrderNo))
            .ToListAsync();
        if (purchaseOrders.Count > 0)
        {
            var allBatchData = await _context.InventoryBatches
                .AsNoTracking()
                .Where(b => b.SourceOrderNo != null && sourceOrderNos.Contains(b.SourceOrderNo))
                .GroupBy(b => b.SourceOrderNo)
                .Select(g => new
                {
                    OrderNo = g.Key!,
                    TotalQty = g.Sum(b => b.InitialQuantity),
                    TotalWt = g.Sum(b => b.InitialWeight),
                    MaxDate = g.Max(b => (DateTime?)b.InboundDate)
                })
                .ToListAsync();

            var batchDict = allBatchData.ToDictionary(x => x.OrderNo, x => x, StringComparer.OrdinalIgnoreCase);
            foreach (var order in purchaseOrders)
            {
                if (!batchDict.TryGetValue(order.OrderNo, out var data)) continue;
                order.ReceivedQuantity = data.TotalQty;
                order.ReceivedWeight = data.TotalWt;
                order.LastArrivalDate = data.MaxDate;

                if (!order.IsForceCompleted)
                {
                    if (order.ReceivedQuantity == 0)
                        order.Status = PurchaseOrderStatus.Open;
                    else if (order.Quantity.HasValue && order.ReceivedQuantity >= order.Quantity.Value)
                        order.Status = PurchaseOrderStatus.Completed;
                    else
                        order.Status = PurchaseOrderStatus.Partial;
                }
                changed = true;
            }
        }

        // 同步委外单
        var subcontractOrders = await _context.SubcontractOrders
            .Where(s => sourceOrderNos.Contains(s.OrderNo))
            .ToListAsync();
        if (subcontractOrders.Count > 0)
        {
            var allBatchData = await _context.InventoryBatches
                .AsNoTracking()
                .Where(b => b.SourceOrderNo != null && sourceOrderNos.Contains(b.SourceOrderNo))
                .GroupBy(b => b.SourceOrderNo)
                .Select(g => new
                {
                    OrderNo = g.Key!,
                    TotalQty = g.Sum(b => b.InitialQuantity),
                    TotalWt = g.Sum(b => b.InitialWeight)
                })
                .ToListAsync();

            var batchDict = allBatchData.ToDictionary(x => x.OrderNo, x => x, StringComparer.OrdinalIgnoreCase);
            foreach (var order in subcontractOrders)
            {
                if (!batchDict.TryGetValue(order.OrderNo, out var data)) continue;
                order.InQuantity = data.TotalQty;
                order.InWeight = data.TotalWt;

                if (!order.IsForceCompleted)
                {
                    if (order.InWeight == null || order.InWeight == 0)
                        order.Status = SubcontractOrderStatus.Sent;
                    else if (order.InWeight >= order.OutWeight * 0.95m)
                        order.Status = SubcontractOrderStatus.Completed;
                    else
                        order.Status = SubcontractOrderStatus.PartialReturned;
                }
                changed = true;
            }
        }

        if (changed)
            await _context.SaveChangesAsync();
    }
}
