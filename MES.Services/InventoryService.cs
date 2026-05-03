using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Mapping;

namespace MES.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

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

    public async Task<PagedResult<InventoryBatchDto>> GetPagedAsync(InventoryQueryParams query)
    {
        var queryable = _context.InventoryBatches
            .AsNoTracking()
            .Where(b => !b.IsDeleted)
            .AsQueryable();

        // 关键字搜索（物料/钢种/规格/炉号/订单号等；按空格拆分多词 AND 匹配）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                queryable = queryable.Where(b =>
                    b.MaterialType.Contains(keyword) ||
                    b.PlantGrade.Contains(keyword) ||
                    b.Specification.Contains(keyword) ||
                    (b.HeatNo != null && b.HeatNo.Contains(keyword)) ||
                    (b.SalesOrderNo != null && b.SalesOrderNo.Contains(keyword)));
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
            _ => query.IsDescending
                ? queryable.OrderByDescending(b => b.CreatedTime)
                : queryable.OrderBy(b => b.CreatedTime)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(b => b.ToDto())
            .ToListAsync();

        // 填充仓库名称
        var warehouseIds = items.Select(i => i.WarehouseId).Distinct();
        var warehouses = await _context.Warehouses
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name);

        foreach (var item in items)
        {
            if (warehouses.TryGetValue(item.WarehouseId, out var name))
                item.WarehouseName = name;
        }

        return new PagedResult<InventoryBatchDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<BatchInboundResult> BatchInboundAsync(BatchInboundRequest request)
    {
        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && !w.IsDeleted);

        if (warehouse == null)
            throw new BusinessException("仓库不存在");

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var results = new List<string>();

            // 预生成批次号序列（避免每行查询数据库导致重复）
            var batchNos = await GenerateBatchNoSequenceAsync(request.Rows.Count);

            for (int i = 0; i < request.Rows.Count; i++)
            {
                var row = request.Rows[i];
                var batchNo = batchNos[i];

                // 公共字段 + 行级字段合并（行级优先）
                var entity = new InventoryBatch
                {
                    BatchNo = batchNo,
                    WarehouseId = request.WarehouseId,
                    MaterialType = request.MaterialType ?? string.Empty,
                    PlantGrade = request.PlantGrade ?? string.Empty,
                    Specification = request.Specification ?? string.Empty,
                    InboundSource = request.InboundSource ?? string.Empty,
                    SourceName = request.SourceName ?? string.Empty,
                    InboundDate = request.InboundDate ?? DateTime.Today,
                    HeatNo = request.HeatNo,
                    ProductionBatchNo = request.ProductionBatchNo,
                    LengthStatus = row.LengthStatus ?? request.LengthStatus,
                    MinLength = row.MinLength ?? request.MinLength,
                    MaxLength = row.MaxLength ?? request.MaxLength,
                    InitialQuantity = row.InitialQuantity,
                    InitialWeight = row.InitialWeight,
                    UnitWeight = row.UnitWeight ?? request.UnitWeight,
                    Meters = row.Meters ?? request.Meters,
                    RemainingQuantity = row.InitialQuantity,
                    RemainingWeight = row.InitialWeight,
                    ActualSpecification = request.ActualSpecification,
                    ActualOuterDiameter = request.ActualOuterDiameter,
                    ActualWallThickness = request.ActualWallThickness,
                    SurfaceCondition = row.SurfaceCondition ?? request.SurfaceCondition,
                    LocationArea = row.LocationArea ?? request.LocationArea,
                    LocationRack = row.LocationRack ?? request.LocationRack,
                    Remark = row.Remark,
                    DefectReason = row.DefectReason ?? request.DefectReason,
                    LiabilityType = row.LiabilityType ?? request.LiabilityType,
                    OriginalSupplier = row.OriginalSupplier ?? request.OriginalSupplier,
                    TagNo = row.TagNo ?? request.TagNo,
                    DefectRemark = row.DefectRemark ?? request.DefectRemark,
                    IsLinkedToWorkOrder = request.IsLinkedToWorkOrder ?? false,
                    WorkOrderNo = request.WorkOrderNo,
                    SalesOrderNo = request.SalesOrderNo,
                    OrderItemIds = request.OrderItemIds,
                    SourceOrderNo = request.SourceOrderNo
                };

                _context.InventoryBatches.Add(entity);
                results.Add(batchNo);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

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
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

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
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && !w.IsDeleted);

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

        _context.InventoryBatches.Add(entity);
        await _context.SaveChangesAsync();

        var dto = entity.ToDto();
        dto.WarehouseName = warehouse.Name;
        return dto;
    }

    public async Task<OutboundRecordDto> OutboundAsync(CreateOutboundRequest request)
    {
        var batch = await _context.InventoryBatches
            .FirstOrDefaultAsync(b => b.Id == request.InventoryBatchId && !b.IsDeleted);

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
            OutboundType = request.OutboundType,
            TargetCompany = request.TargetCompany,
            OutboundQuantity = request.OutboundQuantity,
            OutboundWeight = request.OutboundWeight,
            OutboundDate = request.OutboundDate,
            Operator = GetCurrentUser(),
            Remark = request.Remark,
            CreatedTime = DateTimeOffset.Now,
            CreatedBy = GetCurrentUser(),
            UpdatedTime = DateTimeOffset.Now,
            UpdatedBy = GetCurrentUser()
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
            var results = new List<OutboundRecordDto>();

            foreach (var item in request.Items)
            {
                var batch = await _context.InventoryBatches
                    .FirstOrDefaultAsync(b => b.Id == item.InventoryBatchId && !b.IsDeleted);

                if (batch == null)
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
                    OutboundType = request.OutboundType,
                    TargetCompany = request.TargetCompany,
                    OutboundQuantity = item.OutboundQuantity,
                    OutboundWeight = item.OutboundWeight,
                    OutboundDate = request.OutboundDate,
                    Operator = GetCurrentUser(),
                    Remark = request.Remark,
                    CreatedTime = DateTimeOffset.Now,
                    CreatedBy = GetCurrentUser(),
                    UpdatedTime = DateTimeOffset.Now,
                    UpdatedBy = GetCurrentUser()
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
                .Any(b => b.Id == r.InventoryBatchId && b.WarehouseId == query.WarehouseId.Value));

        if (!string.IsNullOrEmpty(query.OutboundType))
            queryable = queryable.Where(r => r.OutboundType == query.OutboundType);

        if (query.StartDate.HasValue)
            queryable = queryable.Where(r => r.OutboundDate >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            queryable = queryable.Where(r => r.OutboundDate <= query.EndDate.Value);

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            queryable = queryable.Where(r =>
                (r.TargetCompany != null && r.TargetCompany.Contains(query.Keyword)));
        }

        queryable = query.IsDescending
            ? queryable.OrderByDescending(r => r.OutboundDate)
            : queryable.OrderBy(r => r.OutboundDate);

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
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

        if (entity == null)
            throw new BusinessException("入库批次不存在");

        // 仅更新非null字段
        if (request.BatchNo != null) entity.BatchNo = request.BatchNo;
        if (request.MaterialType != null) entity.MaterialType = request.MaterialType;
        if (request.PlantGrade != null) entity.PlantGrade = request.PlantGrade;
        if (request.Specification != null) entity.Specification = request.Specification;
        if (request.InboundSource != null) entity.InboundSource = request.InboundSource;
        if (request.SourceName != null) entity.SourceName = request.SourceName;
        if (request.InboundDate.HasValue) entity.InboundDate = request.InboundDate.Value;
        if (request.HeatNo != null) entity.HeatNo = request.HeatNo;
        if (request.ProductionBatchNo != null) entity.ProductionBatchNo = request.ProductionBatchNo;
        if (request.LengthStatus != null) entity.LengthStatus = request.LengthStatus;
        if (request.MinLength.HasValue) entity.MinLength = request.MinLength;
        if (request.MaxLength.HasValue) entity.MaxLength = request.MaxLength;
        if (request.InitialQuantity.HasValue) entity.InitialQuantity = request.InitialQuantity.Value;
        if (request.InitialWeight.HasValue) entity.InitialWeight = request.InitialWeight.Value;
        if (request.UnitWeight.HasValue) entity.UnitWeight = request.UnitWeight;
        if (request.Meters.HasValue) entity.Meters = request.Meters;
        if (request.ActualSpecification != null) entity.ActualSpecification = request.ActualSpecification;
        if (request.ActualOuterDiameter.HasValue) entity.ActualOuterDiameter = request.ActualOuterDiameter;
        if (request.ActualWallThickness.HasValue) entity.ActualWallThickness = request.ActualWallThickness;
        if (request.SurfaceCondition != null) entity.SurfaceCondition = request.SurfaceCondition;
        if (request.LocationArea != null) entity.LocationArea = request.LocationArea;
        if (request.LocationRack != null) entity.LocationRack = request.LocationRack;
        if (request.Remark != null) entity.Remark = request.Remark;
        if (request.DefectReason != null) entity.DefectReason = request.DefectReason;
        if (request.LiabilityType != null) entity.LiabilityType = request.LiabilityType;
        if (request.OriginalSupplier != null) entity.OriginalSupplier = request.OriginalSupplier;
        if (request.TagNo != null) entity.TagNo = request.TagNo;
        if (request.DefectRemark != null) entity.DefectRemark = request.DefectRemark;
        if (request.IsLinkedToWorkOrder.HasValue) entity.IsLinkedToWorkOrder = request.IsLinkedToWorkOrder.Value;
        if (request.WorkOrderNo != null) entity.WorkOrderNo = request.WorkOrderNo;
        if (request.SalesOrderNo != null) entity.SalesOrderNo = request.SalesOrderNo;
        if (request.OrderItemIds != null) entity.OrderItemIds = request.OrderItemIds;

        if (request.SourceOrderNo != null) entity.SourceOrderNo = request.SourceOrderNo;

        // 如果修改了数量或重量，同步更新剩余量
        if (request.InitialQuantity.HasValue)
            entity.RemainingQuantity = request.InitialQuantity.Value;
        if (request.InitialWeight.HasValue)
            entity.RemainingWeight = request.InitialWeight.Value;

        entity.UpdatedTime = DateTimeOffset.Now;
        entity.UpdatedBy = GetCurrentUser();

        await _context.SaveChangesAsync();

        var dto = entity.ToDto();

        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == entity.WarehouseId);
        if (warehouse != null)
            dto.WarehouseName = warehouse.Name;

        return dto;
    }

    public async Task HardDeleteInventoryBatchAsync(int id)
    {
        var entity = await _context.InventoryBatches
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException("入库批次不存在");

        // 物理删除关联的出库记录
        var relatedOutbounds = await _context.OutboundRecords
            .Where(r => r.InventoryBatchId == id)
            .ToListAsync();
        _context.OutboundRecords.RemoveRange(relatedOutbounds);

        // 物理删除批次
        _context.InventoryBatches.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<OutboundRecordDto> UpdateOutboundRecordAsync(long id, UpdateOutboundRecordRequest request)
    {
        var entity = await _context.OutboundRecords
            .FirstOrDefaultAsync(r => r.Id == id);

        if (entity == null)
            throw new BusinessException("出库记录不存在");

        if (request.OutboundType != null) entity.OutboundType = request.OutboundType;
        if (request.TargetCompany != null) entity.TargetCompany = request.TargetCompany;
        if (request.OutboundQuantity.HasValue) entity.OutboundQuantity = request.OutboundQuantity.Value;
        if (request.OutboundWeight.HasValue) entity.OutboundWeight = request.OutboundWeight.Value;
        if (request.OutboundDate.HasValue) entity.OutboundDate = request.OutboundDate.Value;
        if (request.Remark != null) entity.Remark = request.Remark;

        entity.UpdatedTime = DateTimeOffset.Now;
        entity.UpdatedBy = GetCurrentUser();

        await _context.SaveChangesAsync();

        var dto = entity.ToDto();

        // 填充批次号
        var batch = await _context.InventoryBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == entity.InventoryBatchId);
        if (batch != null)
        {
            dto.BatchNo = batch.BatchNo;
            var wh = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == batch.WarehouseId);
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

        _context.OutboundRecords.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<string> GenerateBatchNoAsync()
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

    /// <summary>
    /// 批量预生成批次号（内存递增，避免 DB 查询重复）
    /// </summary>
    private async Task<List<string>> GenerateBatchNoSequenceAsync(int count)
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
}
