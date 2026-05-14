using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Constants;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Printing;

namespace MES.Services;

public class BatchService : IBatchService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BatchService> _logger;
    private readonly IProductionRecordService _productionRecordService;

    public BatchService(AppDbContext context, ILogger<BatchService> logger, IProductionRecordService productionRecordService)
    {
        _context = context;
        _logger = logger;
        _productionRecordService = productionRecordService;
    }

    public async Task<PagedResult<ProductionBatchListDto>> GetPagedAsync(BatchQueryParams query)
    {
        var queryable = _context.ProductionBatches
            .AsNoTracking()
            .AsQueryable();

        // 关键字搜索（匹配生产编号、工单号、挂牌号、订单号、主号、次号）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(b =>
                b.BatchNo.Contains(kw) ||
                b.WorkOrderNo.Contains(kw) ||
                b.SalesOrderNo.Contains(kw) ||
                b.ProductionMainNo.Contains(kw) ||
                (b.ProductionSubNo != null && b.ProductionSubNo.Contains(kw)) ||
                (b.TagNo != null && b.TagNo.Contains(kw)));
        }

        // 筛选条件
        if (!string.IsNullOrEmpty(query.WorkOrderNo))
            queryable = queryable.Where(b => b.WorkOrderNo.Contains(query.WorkOrderNo));

        if (!string.IsNullOrEmpty(query.Status) && Enum.TryParse<BatchStatus>(query.Status, out var batchStatus))
            queryable = queryable.Where(b => b.Status == batchStatus);

        if (!string.IsNullOrEmpty(query.TagNo))
            queryable = queryable.Where(b => b.TagNo != null && b.TagNo.Contains(query.TagNo));

        if (!string.IsNullOrEmpty(query.BatchNo))
            queryable = queryable.Where(b => b.BatchNo.Contains(query.BatchNo));
        if (!string.IsNullOrEmpty(query.SalesOrderNo))
            queryable = queryable.Where(b => b.SalesOrderNo.Contains(query.SalesOrderNo));
        if (!string.IsNullOrEmpty(query.ProductionMainNo))
            queryable = queryable.Where(b => b.ProductionMainNo.Contains(query.ProductionMainNo));
        if (!string.IsNullOrEmpty(query.ProductionSubNo))
            queryable = queryable.Where(b => b.ProductionSubNo != null && b.ProductionSubNo.Contains(query.ProductionSubNo));

        // 排序
        queryable = (query.SortBy?.ToLower()) switch
        {
            "batchno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.BatchNo)
                : queryable.OrderBy(b => b.BatchNo),
            "tagno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.TagNo ?? "")
                : queryable.OrderBy(b => b.TagNo ?? ""),
            "createdtime" => query.IsDescending
                ? queryable.OrderByDescending(b => b.CreatedTime)
                : queryable.OrderBy(b => b.CreatedTime),
            "updatedtime" => query.IsDescending
                ? queryable.OrderByDescending(b => b.UpdatedTime)
                : queryable.OrderBy(b => b.UpdatedTime),
            "workorderno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.WorkOrderNo)
                : queryable.OrderBy(b => b.WorkOrderNo),
            "salesorderno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.SalesOrderNo)
                : queryable.OrderBy(b => b.SalesOrderNo),
            "productionmainno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.ProductionMainNo)
                : queryable.OrderBy(b => b.ProductionMainNo),
            "productionsubno" => query.IsDescending
                ? queryable.OrderByDescending(b => b.ProductionSubNo ?? "")
                : queryable.OrderBy(b => b.ProductionSubNo ?? ""),
            "productiontype" => query.IsDescending
                ? queryable.OrderByDescending(b => b.ProductionType ?? "")
                : queryable.OrderBy(b => b.ProductionType ?? ""),
            "status" => query.IsDescending
                ? queryable.OrderByDescending(b => b.Status)
                : queryable.OrderBy(b => b.Status),
            "currentexecdate" => query.IsDescending
                ? queryable.OrderByDescending(b => b.CurrentExecDate)
                : queryable.OrderBy(b => b.CurrentExecDate),
            "currentgroupname" => query.IsDescending
                ? queryable.OrderByDescending(b => b.CurrentGroupName ?? "")
                : queryable.OrderBy(b => b.CurrentGroupName ?? ""),
            "currentsectionname" => query.IsDescending
                ? queryable.OrderByDescending(b => b.CurrentSectionName ?? "")
                : queryable.OrderBy(b => b.CurrentSectionName ?? ""),
            "currentequipmentname" => query.IsDescending
                ? queryable.OrderByDescending(b => b.CurrentEquipmentName ?? "")
                : queryable.OrderBy(b => b.CurrentEquipmentName ?? ""),
            "currentoutsource" => query.IsDescending
                ? queryable.OrderByDescending(b => b.CurrentOutsource ?? "")
                : queryable.OrderBy(b => b.CurrentOutsource ?? ""),
            "nextsectionname" => query.IsDescending
                ? queryable.OrderByDescending(b => b.NextSectionName ?? "")
                : queryable.OrderBy(b => b.NextSectionName ?? ""),
            _ => query.IsDescending
                ? queryable.OrderByDescending(b => b.CreatedTime)
                : queryable.OrderBy(b => b.CreatedTime)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(b => new ProductionBatchListDto
            {
                Id = b.Id,
                BatchNo = b.BatchNo,
                TagNo = b.TagNo,
                CreatedTime = b.CreatedTime,
                UpdatedTime = b.UpdatedTime,
                WorkOrderNo = b.WorkOrderNo,
                SalesOrderNo = b.SalesOrderNo,
                ProductionMainNo = b.ProductionMainNo,
                ProductionSubNo = b.ProductionSubNo,
                ProductionType = b.ProductionType,
                Status = b.Status.ToString(),
                CurrentExecDate = b.CurrentExecDate,
                CurrentGroupName = b.CurrentGroupName,
                CurrentSectionName = b.CurrentSectionName,
                CurrentEquipmentName = b.CurrentEquipmentName,
                CurrentOutsource = b.CurrentOutsource,
                CurrentSpec = b.CurrentSpec,
                NextSectionName = b.NextSectionName,
                CorrespondingSpec = b.CorrespondingSpec,
                CurrentValidQty = b.CurrentValidQty,
                CurrentValidWeight = b.CurrentValidWeight,
                CreatedBy = b.CreatedBy
            })
            .ToListAsync();

        return new PagedResult<ProductionBatchListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<ProductionBatchDetailDto> GetByIdAsync(int id)
    {
        var entity = await _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        var dto = ToDetailDto(entity);

        // 填充仓库名称
        if (dto.WarehouseId.HasValue)
        {
            var warehouse = await _context.Warehouses.FindAsync(dto.WarehouseId.Value);
            if (warehouse != null)
                dto.WarehouseName = warehouse.Name;
        }

        return dto;
    }

    public async Task<ProductionBatchDetailDto> GetByBatchNoAsync(string batchNo)
    {
        var entity = await _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .FirstOrDefaultAsync(b => b.BatchNo == batchNo);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (BatchNo={batchNo})");

        return ToDetailDto(entity);
    }

    public async Task<AdjacentBatchDto> GetAdjacentBatchAsync(int currentId)
    {
        // 获取所有批次ID（按创建时间排序），找到当前批次的相邻记录
        var allIds = await _context.ProductionBatches
            .AsNoTracking()
            .OrderBy(b => b.CreatedTime)
            .ThenBy(b => b.Id)
            .Select(b => new { b.Id, b.BatchNo })
            .ToListAsync();

        var currentIndex = allIds.FindIndex(x => x.Id == currentId);
        if (currentIndex < 0)
            return new AdjacentBatchDto();

        return new AdjacentBatchDto
        {
            PrevId = currentIndex > 0 ? allIds[currentIndex - 1].Id : null,
            PrevBatchNo = currentIndex > 0 ? allIds[currentIndex - 1].BatchNo : null,
            NextId = currentIndex < allIds.Count - 1 ? allIds[currentIndex + 1].Id : null,
            NextBatchNo = currentIndex < allIds.Count - 1 ? allIds[currentIndex + 1].BatchNo : null
        };
    }

    public async Task<ProductionBatchListDto> CreateAsync(CreateProductionBatchRequest request)
    {
        // 生成生产编号
        var batchNo = await GenerateBatchNoAsync();

        // 如果提供了工单号，验证工单是否存在
        WorkOrder? workOrder = null;
        if (!string.IsNullOrWhiteSpace(request.WorkOrderNo))
        {
            workOrder = await _context.WorkOrders
                .FirstOrDefaultAsync(w => w.WorkOrderNo == request.WorkOrderNo);

            if (workOrder == null)
                throw new BusinessException($"工单不存在 (WorkOrderNo={request.WorkOrderNo})");
        }

        // 生产类型必填
        if (string.IsNullOrWhiteSpace(request.ProductionType))
            throw new BusinessException("生产类型不能为空");

        // 工厂牌号验证（高代低）
        if (!GradeSubstitutes.IsSubstitutable(request.PlantGrade, request.SourcePlantGrade))
            throw new BusinessException("仓库工厂牌号与工单工厂牌号不一致，且不可替代（仅允许高代低）");

        var entity = new ProductionBatch
        {
            BatchNo = batchNo,
            Status = BatchStatus.None,
            TagNo = request.TagNo,
            ProductionType = request.ProductionType,
            ProductionRatio = CalculateProductionRatio(request, workOrder),
            IsForceCompleted = false,
            QualityRemark = request.QualityRemark,
            SolutionParams = request.SolutionParams,
            Remark = request.Remark,

            // 仓库来源
            SourceBatchNo = request.SourceBatchNo,
            WarehouseId = request.WarehouseId,
            SourceMaterialType = request.SourceMaterialType,
            InboundSource = request.InboundSource,
            SourceName = request.SourceName,
            InboundDate = request.InboundDate,
            SourceHeatNo = request.SourceHeatNo,
            SourcePlantGrade = request.SourcePlantGrade,
            SourceSpecification = request.SourceSpecification,
            SourceLengthStatus = request.SourceLengthStatus,
            SourceUnitWeight = request.SourceUnitWeight,
            InputQuantity = request.InputQuantity,
            InputWeight = request.InputWeight,
            CurrentValidQty = request.CurrentValidQty,
            CurrentValidWeight = request.CurrentValidWeight,

            // 从工单复制冗余字段（29个，优先使用前端传入值）
            WorkOrderNo = request.WorkOrderNo ?? workOrder?.WorkOrderNo ?? "",
            SalesOrderNo = request.SalesOrderNo ?? workOrder?.SalesOrderNo ?? "",
            ProductionMainNo = request.ProductionMainNo ?? workOrder?.ProductionMainNo ?? "",
            ProductionSubNo = request.ProductionSubNo ?? workOrder?.ProductionSubNo,
            OrderItemIds = request.OrderItemIds ?? workOrder?.OrderItemIds ?? "",
            SignDate = request.SignDate ?? workOrder?.SignDate ?? default,
            Salesman = request.Salesman ?? workOrder?.Salesman ?? "",
            EndCustomer = request.EndCustomer ?? workOrder?.EndCustomer,
            DeliveryDate = request.DeliveryDate ?? workOrder?.DeliveryDate ?? default,
            DelayPenalty = request.DelayPenalty ?? workOrder?.DelayPenalty ?? default,
            MaterialName = request.MaterialName ?? workOrder?.MaterialName.ToString() ?? "",
            SettlementMethod = request.SettlementMethod ?? workOrder?.SettlementMethod.ToString() ?? "",
            StandardCode = request.StandardCode ?? workOrder?.StandardCode ?? "",
            DeliveryState = request.DeliveryState ?? workOrder?.DeliveryState.ToString() ?? "",
            PlantGrade = request.PlantGrade ?? workOrder?.PlantGrade ?? "",
            Specification = request.Specification ?? workOrder?.Specification ?? "",
            OuterDiameterNegative = request.OuterDiameterNegative ?? workOrder?.OuterDiameterNegative ?? default,
            OuterDiameterPositive = request.OuterDiameterPositive ?? workOrder?.OuterDiameterPositive ?? default,
            WallThicknessNegative = request.WallThicknessNegative ?? workOrder?.WallThicknessNegative ?? default,
            WallThicknessPositive = request.WallThicknessPositive ?? workOrder?.WallThicknessPositive ?? default,
            LengthStatus = request.LengthStatus ?? workOrder?.LengthStatus.ToString() ?? "",
            MinLength = request.MinLength ?? workOrder?.MinLength,
            MaxLength = request.MaxLength ?? workOrder?.MaxLength,
            TotalQuantity = request.TotalQuantity ?? workOrder?.TotalQuantity ?? default,
            TotalMeters = request.TotalMeters ?? workOrder?.TotalMeters ?? default,
            TotalWeight = request.TotalWeight ?? workOrder?.TotalWeight ?? default,
            TotalItemCount = request.TotalItemCount ?? workOrder?.TotalItemCount ?? default,
            ItemDetails = request.ItemDetails ?? workOrder?.ItemDetails,
            TechnicalRequirements = request.TechnicalRequirements ?? workOrder?.TechnicalRequirements.ToString() ?? ""
        };

        // 工序组数值验证
        if (request.ProcessGroups != null && request.ProcessGroups.Count > 0)
            ValidateProcessGroupValues(request.ProcessGroups);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.ProductionBatches.Add(entity);
            await _context.SaveChangesAsync();

            // 保存工序组
            if (request.ProcessGroups != null && request.ProcessGroups.Count > 0)
            {
                for (int i = 0; i < request.ProcessGroups.Count; i++)
                {
                    var pg = request.ProcessGroups[i];
                    var pgEntity = new ProcessGroup
                    {
                        ProductionBatchId = entity.Id,
                        SequenceNumber = i + 1,
                        ProcessName = pg.ProcessName,
                        ManufacturingSpec = pg.ManufacturingSpec,
                        OuterDiameterTolerance = pg.OuterDiameterTolerance,
                        WallThicknessTolerance = pg.WallThicknessTolerance,
                        ManufacturingLength = pg.ManufacturingLength,
                        CuttingTreatment = pg.CuttingTreatment,
                        Remark = pg.Remark,
                        ColdRollDraw = pg.ColdRollDraw,
                        OilPipeCut = pg.OilPipeCut,
                        Degrease = pg.Degrease,
                        Solution = pg.Solution,
                        Straighten = pg.Straighten,
                        Cut = pg.Cut,
                        ThicknessMeasure = pg.ThicknessMeasure,
                        Pickle = pg.Pickle,
                        OuterPolish = pg.OuterPolish,
                        InnerGrinding = pg.InnerGrinding,
                        OuterSpotGrinding = pg.OuterSpotGrinding,
                        Inspection = pg.Inspection,
                        WeldingHead = pg.WeldingHead,
                        Lubrication = pg.Lubrication,
                        Warehouse = pg.Warehouse
                    };
                    _context.ProcessGroups.Add(pgEntity);
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        _logger.LogInformation("创建生产批次 {BatchNo} (工单: {WorkOrderNo})", batchNo, entity.WorkOrderNo);

        return new ProductionBatchListDto
        {
            Id = entity.Id,
            BatchNo = entity.BatchNo,
            Status = entity.Status.ToString(),
            TagNo = entity.TagNo,
            WorkOrderNo = entity.WorkOrderNo,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy
        };
    }

    public async Task<ProductionBatchDetailDto> UpdateAsync(int id, UpdateProductionBatchRequest request)
    {
        var entity = await _context.ProductionBatches
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        // 乐观锁检查
        if (!request.RowVersion.SequenceEqual(entity.RowVersion))
            throw new BusinessException("数据已被其他用户修改，请刷新后重试");

        // 工厂牌号验证（高代低）- 仅当任一牌号被更新时校验
        var effectivePlantGrade = request.PlantGrade ?? entity.PlantGrade;
        var effectiveSourcePlantGrade = request.SourcePlantGrade ?? entity.SourcePlantGrade;
        if (!GradeSubstitutes.IsSubstitutable(effectivePlantGrade, effectiveSourcePlantGrade))
            throw new BusinessException("仓库工厂牌号与工单工厂牌号不一致，且不可替代（仅允许高代低）");

        // 更新可修改字段（nullable 字段直接赋值支持清空，non-nullable 保留守卫）
        entity.TagNo = request.TagNo;
        entity.QualityRemark = request.QualityRemark;
        entity.SolutionParams = request.SolutionParams;
        entity.Remark = request.Remark;
        entity.SourceBatchNo = request.SourceBatchNo;
        entity.WarehouseId = request.WarehouseId;
        entity.SourceMaterialType = request.SourceMaterialType;
        entity.SourceName = request.SourceName;
        entity.SourceHeatNo = request.SourceHeatNo;
        entity.SourcePlantGrade = request.SourcePlantGrade;
        entity.SourceSpecification = request.SourceSpecification;
        entity.SourceLengthStatus = request.SourceLengthStatus;
        entity.SourceUnitWeight = request.SourceUnitWeight;
        entity.InputQuantity = request.InputQuantity;
        entity.InputWeight = request.InputWeight;
        var oldValidQty = entity.CurrentValidQty;
        var oldValidWeight = entity.CurrentValidWeight;
        entity.CurrentValidQty = request.CurrentValidQty;
        entity.CurrentValidWeight = request.CurrentValidWeight;
        if (request.IsForceCompleted.HasValue) entity.IsForceCompleted = request.IsForceCompleted.Value;

        // 工单冗余字段（non-nullable 保留守卫防崩溃）
        if (request.WorkOrderNo != null) entity.WorkOrderNo = request.WorkOrderNo;
        if (request.SalesOrderNo != null) entity.SalesOrderNo = request.SalesOrderNo;
        if (request.ProductionMainNo != null) entity.ProductionMainNo = request.ProductionMainNo;
        entity.ProductionSubNo = request.ProductionSubNo;
        if (request.OrderItemIds != null) entity.OrderItemIds = request.OrderItemIds;
        if (request.SignDate.HasValue) entity.SignDate = request.SignDate.Value;
        if (request.Salesman != null) entity.Salesman = request.Salesman;
        entity.EndCustomer = request.EndCustomer;
        if (request.DeliveryDate.HasValue) entity.DeliveryDate = request.DeliveryDate.Value;
        if (request.DelayPenalty.HasValue) entity.DelayPenalty = request.DelayPenalty.Value;
        if (request.MaterialName != null) entity.MaterialName = request.MaterialName;
        if (request.SettlementMethod != null) entity.SettlementMethod = request.SettlementMethod;
        if (request.StandardCode != null) entity.StandardCode = request.StandardCode;
        if (request.DeliveryState != null) entity.DeliveryState = request.DeliveryState;
        if (request.PlantGrade != null) entity.PlantGrade = request.PlantGrade;
        if (request.Specification != null) entity.Specification = request.Specification;
        if (request.OuterDiameterNegative.HasValue) entity.OuterDiameterNegative = request.OuterDiameterNegative.Value;
        if (request.OuterDiameterPositive.HasValue) entity.OuterDiameterPositive = request.OuterDiameterPositive.Value;
        if (request.WallThicknessNegative.HasValue) entity.WallThicknessNegative = request.WallThicknessNegative.Value;
        if (request.WallThicknessPositive.HasValue) entity.WallThicknessPositive = request.WallThicknessPositive.Value;
        if (request.LengthStatus != null) entity.LengthStatus = request.LengthStatus;
        entity.MinLength = request.MinLength;
        entity.MaxLength = request.MaxLength;
        if (request.TotalQuantity.HasValue) entity.TotalQuantity = request.TotalQuantity.Value;
        if (request.TotalMeters.HasValue) entity.TotalMeters = request.TotalMeters.Value;
        if (request.TotalWeight.HasValue) entity.TotalWeight = request.TotalWeight.Value;
        if (request.TotalItemCount.HasValue) entity.TotalItemCount = request.TotalItemCount.Value;
        entity.ItemDetails = request.ItemDetails;
        if (request.TechnicalRequirements != null) entity.TechnicalRequirements = request.TechnicalRequirements;

        await _context.SaveChangesAsync();

        // 记录有效数量变更日志
        if (oldValidQty != request.CurrentValidQty || oldValidWeight != request.CurrentValidWeight)
        {
            var detail = $"有效数量变更: 有效支数={oldValidQty}→{request.CurrentValidQty}" +
                         $", 有效重量={oldValidWeight?.ToString("G29")}→{request.CurrentValidWeight?.ToString("G29")}kg";
            await AddOperationLogAsync(id, "有效数量变更", detail);
        }

        _logger.LogInformation("更新生产批次 {BatchNo} (Id={Id})", entity.BatchNo, id);

        var dto = ToDetailDto(entity);

        // 填充仓库名称
        if (dto.WarehouseId.HasValue)
        {
            var warehouse = await _context.Warehouses.FindAsync(dto.WarehouseId.Value);
            if (warehouse != null)
                dto.WarehouseName = warehouse.Name;
        }

        return dto;
    }

    public async Task UpdateStatusAsync(int id, UpdateBatchStatusRequest request)
    {
        var entity = await _context.ProductionBatches
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        if (!request.RowVersion.SequenceEqual(entity.RowVersion))
            throw new BusinessException("数据已被其他用户修改，请刷新后重试");

        if (!Enum.TryParse<BatchStatus>(request.Status, out var newStatus))
            throw new BusinessException($"无效的批次状态: {request.Status}");

        var oldStatus = entity.Status;
        entity.Status = newStatus;

        // 强制完成逻辑
        if (newStatus == BatchStatus.Completed)
            entity.IsForceCompleted = true;

        // 从强制完成恢复时清除标记
        if (oldStatus == BatchStatus.Completed && entity.IsForceCompleted && newStatus != BatchStatus.Completed)
            entity.IsForceCompleted = false;

        // 使用事务包裹状态变更和日志写入
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();
            var logDetail = $"状态变更: {oldStatus} → {newStatus}";
            await AddOperationLogAsync(id, "状态变更", logDetail);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        _logger.LogInformation("更新批次状态 {BatchNo} → {Status}", entity.BatchNo, newStatus);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.ProductionBatches
            .Include(b => b.ProcessGroups)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        // 物理删除（批次+工序组通过Cascade自动删除）
        _context.ProductionBatches.Remove(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("删除生产批次 {BatchNo} (Id={Id})", entity.BatchNo, id);
    }

    public async Task<SaveBatchResponse> SaveAllAsync(int id, SaveBatchRequest request)
    {
        var entity = await _context.ProductionBatches
            .Include(b => b.ProcessGroups)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        // 乐观锁检查
        if (!request.RowVersion.SequenceEqual(entity.RowVersion))
            throw new BusinessException("数据已被其他用户修改，请刷新后重试");

        // 工序组数值验证（全量替换前校验，失败则阻止后续修改）
        if (request.ProcessGroups != null && request.ProcessGroups.Count > 0)
            ValidateProcessGroupValues(request.ProcessGroups);

        // ===== 1. 更新批次头字段（nullable 直接赋值，non-nullable 保留守卫） =====
        entity.TagNo = request.TagNo;
        entity.QualityRemark = request.QualityRemark;
        entity.SolutionParams = request.SolutionParams;
        entity.Remark = request.Remark;
        entity.SourceBatchNo = request.SourceBatchNo;
        entity.WarehouseId = request.WarehouseId;
        entity.SourceMaterialType = request.SourceMaterialType;
        entity.SourceName = request.SourceName;
        entity.SourceHeatNo = request.SourceHeatNo;
        entity.SourcePlantGrade = request.SourcePlantGrade;
        entity.SourceSpecification = request.SourceSpecification;
        entity.SourceLengthStatus = request.SourceLengthStatus;
        entity.SourceUnitWeight = request.SourceUnitWeight;
        entity.InputQuantity = request.InputQuantity;
        entity.InputWeight = request.InputWeight;
        var oldValidQty = entity.CurrentValidQty;
        var oldValidWeight = entity.CurrentValidWeight;
        entity.CurrentValidQty = request.CurrentValidQty;
        entity.CurrentValidWeight = request.CurrentValidWeight;
        if (request.IsForceCompleted.HasValue) entity.IsForceCompleted = request.IsForceCompleted.Value;

        // 工单冗余字段（non-nullable 保留守卫防崩溃）
        if (request.WorkOrderNo != null) entity.WorkOrderNo = request.WorkOrderNo;
        if (request.SalesOrderNo != null) entity.SalesOrderNo = request.SalesOrderNo;
        if (request.ProductionMainNo != null) entity.ProductionMainNo = request.ProductionMainNo;
        entity.ProductionSubNo = request.ProductionSubNo;
        if (request.OrderItemIds != null) entity.OrderItemIds = request.OrderItemIds;
        if (request.SignDate.HasValue) entity.SignDate = request.SignDate.Value;
        if (request.Salesman != null) entity.Salesman = request.Salesman;
        entity.EndCustomer = request.EndCustomer;
        if (request.DeliveryDate.HasValue) entity.DeliveryDate = request.DeliveryDate.Value;
        if (request.DelayPenalty.HasValue) entity.DelayPenalty = request.DelayPenalty.Value;
        if (request.MaterialName != null) entity.MaterialName = request.MaterialName;
        if (request.SettlementMethod != null) entity.SettlementMethod = request.SettlementMethod;
        if (request.StandardCode != null) entity.StandardCode = request.StandardCode;
        if (request.DeliveryState != null) entity.DeliveryState = request.DeliveryState;
        if (request.PlantGrade != null) entity.PlantGrade = request.PlantGrade;
        if (request.Specification != null) entity.Specification = request.Specification;
        if (request.OuterDiameterNegative.HasValue) entity.OuterDiameterNegative = request.OuterDiameterNegative.Value;
        if (request.OuterDiameterPositive.HasValue) entity.OuterDiameterPositive = request.OuterDiameterPositive.Value;
        if (request.WallThicknessNegative.HasValue) entity.WallThicknessNegative = request.WallThicknessNegative.Value;
        if (request.WallThicknessPositive.HasValue) entity.WallThicknessPositive = request.WallThicknessPositive.Value;
        if (request.LengthStatus != null) entity.LengthStatus = request.LengthStatus;
        entity.MinLength = request.MinLength;
        entity.MaxLength = request.MaxLength;
        if (request.TotalQuantity.HasValue) entity.TotalQuantity = request.TotalQuantity.Value;
        if (request.TotalMeters.HasValue) entity.TotalMeters = request.TotalMeters.Value;
        if (request.TotalWeight.HasValue) entity.TotalWeight = request.TotalWeight.Value;
        if (request.TotalItemCount.HasValue) entity.TotalItemCount = request.TotalItemCount.Value;
        entity.ItemDetails = request.ItemDetails;
        if (request.TechnicalRequirements != null) entity.TechnicalRequirements = request.TechnicalRequirements;

        // ===== 2. 更新状态（如有） =====
        if (!string.IsNullOrEmpty(request.Status) && request.Status != entity.Status.ToString())
        {
            if (!Enum.TryParse<BatchStatus>(request.Status, out var newStatus))
                throw new BusinessException($"无效的批次状态: {request.Status}");

            entity.Status = newStatus;

            if (newStatus == BatchStatus.Completed)
                entity.IsForceCompleted = true;
        }

        // ===== 3. 全量替换工序组 =====
        // 3a. 删除旧工序组（跳过有生产记录或委外记录引用的）
        HashSet<int> referencedIds = new();
        if (entity.ProcessGroups.Any())
        {
            var oldIds = entity.ProcessGroups.Select(pg => pg.Id).ToList();

            var referencedByRecord = await _context.ProductionRecords
                .Where(r => oldIds.Contains(r.ProcessGroupId))
                .Select(r => r.ProcessGroupId)
                .Distinct()
                .ToListAsync();

            var referencedByOutsource = await _context.SectionOutsources
                .Where(s => oldIds.Contains(s.ProcessGroupId))
                .Select(s => s.ProcessGroupId)
                .Distinct()
                .ToListAsync();

            referencedIds = new HashSet<int>(referencedByRecord.Concat(referencedByOutsource));
            var toRemove = entity.ProcessGroups.Where(pg => !referencedIds.Contains(pg.Id)).ToList();

            if (toRemove.Count > 0)
            {
                _context.ProcessGroups.RemoveRange(toRemove);
            }

            if (referencedIds.Count > 0)
            {
                var refNames = entity.ProcessGroups
                    .Where(pg => referencedIds.Contains(pg.Id))
                    .Select(pg => $"{pg.ProcessName}(Id={pg.Id})");
                _logger.LogWarning("批次 {BatchNo} 的 {Count} 个工序组因存在生产记录或委外记录引用而跳过删除: {Names}",
                    entity.BatchNo, referencedIds.Count, string.Join(", ", refNames));
            }
        }

        // 3b. 先提交删除，避免后续 INSERT 与旧记录主键冲突
        await _context.SaveChangesAsync();

        // 3c. 创建新工序组
        //  - 若请求项与保留的被引用工序组序列号相同 → 原地更新（避免唯一键冲突）
        //  - 否则 → 新增插入（旧记录已在 3b 删除，同序列号可安全插入）
        for (int i = 0; i < request.ProcessGroups.Count; i++)
        {
            var pgReq = request.ProcessGroups[i];
            var seq = i + 1;

            // 若有被引用保留的同序列号工序组，原地更新
            var existingReferenced = entity.ProcessGroups
                .FirstOrDefault(pg => referencedIds.Contains(pg.Id) && pg.SequenceNumber == seq);
            if (existingReferenced != null)
            {
                existingReferenced.ProcessName = pgReq.ProcessName;
                existingReferenced.ManufacturingSpec = pgReq.ManufacturingSpec;
                existingReferenced.OuterDiameterTolerance = pgReq.OuterDiameterTolerance;
                existingReferenced.WallThicknessTolerance = pgReq.WallThicknessTolerance;
                existingReferenced.ManufacturingLength = pgReq.ManufacturingLength;
                existingReferenced.CuttingTreatment = pgReq.CuttingTreatment;
                existingReferenced.Remark = pgReq.Remark;
                existingReferenced.ColdRollDraw = pgReq.ColdRollDraw;
                existingReferenced.OilPipeCut = pgReq.OilPipeCut;
                existingReferenced.Degrease = pgReq.Degrease;
                existingReferenced.Solution = pgReq.Solution;
                existingReferenced.Straighten = pgReq.Straighten;
                existingReferenced.Cut = pgReq.Cut;
                existingReferenced.ThicknessMeasure = pgReq.ThicknessMeasure;
                existingReferenced.Pickle = pgReq.Pickle;
                existingReferenced.OuterPolish = pgReq.OuterPolish;
                existingReferenced.InnerGrinding = pgReq.InnerGrinding;
                existingReferenced.OuterSpotGrinding = pgReq.OuterSpotGrinding;
                existingReferenced.Inspection = pgReq.Inspection;
                existingReferenced.WeldingHead = pgReq.WeldingHead;
                existingReferenced.Lubrication = pgReq.Lubrication;
                existingReferenced.Warehouse = pgReq.Warehouse;
                continue;
            }

            var pg = new ProcessGroup
            {
                ProductionBatchId = id,
                SequenceNumber = seq,
                ProcessName = pgReq.ProcessName,
                ManufacturingSpec = pgReq.ManufacturingSpec,
                OuterDiameterTolerance = pgReq.OuterDiameterTolerance,
                WallThicknessTolerance = pgReq.WallThicknessTolerance,
                ManufacturingLength = pgReq.ManufacturingLength,
                CuttingTreatment = pgReq.CuttingTreatment,
                Remark = pgReq.Remark,
                ColdRollDraw = pgReq.ColdRollDraw,
                OilPipeCut = pgReq.OilPipeCut,
                Degrease = pgReq.Degrease,
                Solution = pgReq.Solution,
                Straighten = pgReq.Straighten,
                Cut = pgReq.Cut,
                ThicknessMeasure = pgReq.ThicknessMeasure,
                Pickle = pgReq.Pickle,
                OuterPolish = pgReq.OuterPolish,
                InnerGrinding = pgReq.InnerGrinding,
                OuterSpotGrinding = pgReq.OuterSpotGrinding,
                Inspection = pgReq.Inspection,
                WeldingHead = pgReq.WeldingHead,
                Lubrication = pgReq.Lubrication,
                Warehouse = pgReq.Warehouse
            };
            _context.ProcessGroups.Add(pg);
        }

        // ===== 4. 提交新增工序组（此时仅有 INSERT，无冲突） =====
        await _context.SaveChangesAsync();

        // 记录有效数量变更日志
        if (oldValidQty != request.CurrentValidQty || oldValidWeight != request.CurrentValidWeight)
        {
            var detail = $"有效数量变更: 有效支数={oldValidQty}→{request.CurrentValidQty}" +
                         $", 有效重量={oldValidWeight?.ToString("G29")}→{request.CurrentValidWeight?.ToString("G29")}kg";
            await AddOperationLogAsync(id, "有效数量变更", detail);
        }

        // ===== 5. 工序组已变更，刷新批次跟踪字段 =====
        await _productionRecordService.BatchUpdateBatchTrackingAsync(new[] { id });

        _logger.LogInformation("批量保存生产批次 {BatchNo} (Id={Id}), 工序组={GroupCount}",
            entity.BatchNo, id, request.ProcessGroups.Count);

        return new SaveBatchResponse
        {
            RowVersion = entity.RowVersion,
            Status = entity.Status.ToString()
        };
    }

    // ========== 工序组 ==========

    public async Task<List<ProcessGroupDto>> GetProcessGroupsAsync(int batchId)
    {
        var groups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == batchId)
            .OrderBy(pg => pg.SequenceNumber)
            .Select(pg => ToGroupDto(pg))
            .ToListAsync();

        return groups;
    }

    public async Task<ProcessGroupDto> AddProcessGroupAsync(int batchId, CreateProcessGroupRequest request)
    {
        var batch = await _context.ProductionBatches.FindAsync(batchId);
        if (batch == null)
            throw new BusinessException($"生产批次不存在 (Id={batchId})");

        // 获取下一个序号
        var maxSeq = await _context.ProcessGroups
            .Where(pg => pg.ProductionBatchId == batchId)
            .MaxAsync(pg => (int?)pg.SequenceNumber) ?? 0;

        var entity = new ProcessGroup
        {
            ProductionBatchId = batchId,
            SequenceNumber = maxSeq + 1,
            ProcessName = request.ProcessName,
            ManufacturingSpec = request.ManufacturingSpec,
            OuterDiameterTolerance = request.OuterDiameterTolerance,
            WallThicknessTolerance = request.WallThicknessTolerance,
            ManufacturingLength = request.ManufacturingLength,
            CuttingTreatment = request.CuttingTreatment,
            Remark = request.Remark,
            ColdRollDraw = request.ColdRollDraw,
            OilPipeCut = request.OilPipeCut,
            Degrease = request.Degrease,
            Solution = request.Solution,
            Straighten = request.Straighten,
            Cut = request.Cut,
            ThicknessMeasure = request.ThicknessMeasure,
            Pickle = request.Pickle,
            OuterPolish = request.OuterPolish,
            InnerGrinding = request.InnerGrinding,
            OuterSpotGrinding = request.OuterSpotGrinding,
            Inspection = request.Inspection,
            WeldingHead = request.WeldingHead,
            Lubrication = request.Lubrication,
            Warehouse = request.Warehouse
        };

        _context.ProcessGroups.Add(entity);
        await _context.SaveChangesAsync();

        // 工序组变更影响工段解析，刷新批次跟踪字段
        await _productionRecordService.BatchUpdateBatchTrackingAsync(new[] { batchId });

        _logger.LogInformation("添加工序组 {ProcessName} → 批次 {BatchId}", request.ProcessName, batchId);

        return ToGroupDto(entity);
    }

    public async Task DeleteProcessGroupAsync(int groupId)
    {
        var entity = await _context.ProcessGroups.FindAsync(groupId);
        if (entity == null)
            throw new BusinessException($"工序组不存在 (Id={groupId})");

        var batchId = entity.ProductionBatchId;

        // 检查是否有生产记录或委外记录引用
        var hasRecord = await _context.ProductionRecords.AnyAsync(r => r.ProcessGroupId == groupId);
        if (hasRecord)
            throw new BusinessException($"工序组 (Id={groupId}) 已被生产记录引用，无法删除。请先删除相关生产记录后再试。");

        var hasOutsource = await _context.SectionOutsources.AnyAsync(s => s.ProcessGroupId == groupId);
        if (hasOutsource)
            throw new BusinessException($"工序组 (Id={groupId}) 已被委外发出记录引用，无法删除。请先删除相关委外记录后再试。");

        _context.ProcessGroups.Remove(entity);
        await _context.SaveChangesAsync();

        // 工序组变更影响工段解析，刷新批次跟踪字段
        await _productionRecordService.BatchUpdateBatchTrackingAsync(new[] { batchId });

        _logger.LogInformation("删除工序组 (Id={GroupId})", groupId);
    }

    // ========== 查询 ==========

    public async Task<List<AvailableBatchDto>> GetAvailableBatchesAsync()
    {
        var usedBatchNos = await _context.ProductionBatches
            .Where(b => b.SourceBatchNo != null)
            .Select(b => b.SourceBatchNo)
            .ToListAsync();

        var available = await _context.OutboundRecords
            .Where(o => o.OutboundType == OutboundType.ProductionPick)
            .Join(_context.InventoryBatches,
                o => o.InventoryBatchId,
                ib => ib.Id,
                (o, ib) => new { o, ib })
            .Where(x => !usedBatchNos.Contains(x.ib.BatchNo))
            .Join(_context.Warehouses,
                x => x.ib.WarehouseId,
                w => w.Id,
                (x, w) => new { x.o, x.ib, w })
            .OrderBy(x => x.ib.BatchNo)
            .Select(x => new AvailableBatchDto
            {
                BatchNo = x.ib.BatchNo,
                WarehouseId = x.ib.WarehouseId,
                WarehouseName = x.w.Name,
                MaterialType = x.ib.MaterialType,
                InboundSource = x.ib.InboundSource,
                SourceName = x.ib.SourceName,
                InboundDate = x.ib.InboundDate,
                HeatNo = x.ib.HeatNo,
                OutboundQuantity = x.o.OutboundQuantity,
                OutboundWeight = x.o.OutboundWeight,
                WorkOrderNo = x.ib.WorkOrderNo,
                PlantGrade = x.ib.PlantGrade,
                Specification = x.ib.Specification,
                LengthStatus = x.ib.LengthStatus,
                UnitWeight = x.ib.UnitWeight
            })
            .ToListAsync();

        return available;
    }

    // ========== 编号生成 ==========

    public async Task<string> GetNextBatchNoAsync()
    {
        return await GenerateBatchNoAsync();
    }

    // ========== 获取上批次工序组 ==========

    public async Task<List<CreateProcessGroupRequest>> GetLastBatchProcessGroupsAsync()
    {
        var lastBatch = await _context.ProductionBatches
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedTime)
            .FirstOrDefaultAsync();

        if (lastBatch == null)
            return new List<CreateProcessGroupRequest>();

        var groups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == lastBatch.Id)
            .OrderBy(pg => pg.SequenceNumber)
            .Select(pg => new CreateProcessGroupRequest
            {
                ProcessName = pg.ProcessName,
                ManufacturingSpec = pg.ManufacturingSpec,
                OuterDiameterTolerance = pg.OuterDiameterTolerance,
                WallThicknessTolerance = pg.WallThicknessTolerance,
                ManufacturingLength = pg.ManufacturingLength,
                CuttingTreatment = pg.CuttingTreatment,
                Remark = pg.Remark,
                ColdRollDraw = pg.ColdRollDraw,
                OilPipeCut = pg.OilPipeCut,
                Degrease = pg.Degrease,
                Solution = pg.Solution,
                Straighten = pg.Straighten,
                Cut = pg.Cut,
                ThicknessMeasure = pg.ThicknessMeasure,
                Pickle = pg.Pickle,
                OuterPolish = pg.OuterPolish,
                InnerGrinding = pg.InnerGrinding,
                OuterSpotGrinding = pg.OuterSpotGrinding,
                Inspection = pg.Inspection,
                WeldingHead = pg.WeldingHead,
                Lubrication = pg.Lubrication,
                Warehouse = pg.Warehouse
            })
            .ToListAsync();

        return groups;
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintBatchAsync(int id)
    {
        var entity = await _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        var groups = entity.ProcessGroups.Select(ToGroupDto).ToList();

        var columns = new List<PrintColumnDef>
        {
            new() { Key = "BatchNo", Label = "生产编号" },
            new() { Key = "TagNo", Label = "挂牌号" },
            new() { Key = "CreatedTime", Label = "创建时间" },
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "SalesOrderNo", Label = "订单号" },
            new() { Key = "ProductionMainNo", Label = "主号" },
            new() { Key = "ProductionSubNo", Label = "次号" },
            new() { Key = "ProductionType", Label = "生产类型" },
            new() { Key = "Status", Label = "状态" },
            new() { Key = "CurrentExecDate", Label = "截止执行日" },
            new() { Key = "CurrentGroupName", Label = "当前工序" },
            new() { Key = "CurrentSectionName", Label = "当前工段" },
            new() { Key = "CurrentEquipmentName", Label = "当前设备" },
            new() { Key = "CurrentOutsource", Label = "当前委外" },
            new() { Key = "CurrentSpec", Label = "当前规格" },
            new() { Key = "NextSectionName", Label = "下一工段" },
            new() { Key = "CorrespondingSpec", Label = "对应规格" }
        };

        var items = new List<Dictionary<string, object>>
        {
            new()
            {
                ["BatchNo"] = entity.BatchNo,
                ["TagNo"] = entity.TagNo ?? "",
                ["CreatedTime"] = entity.CreatedTime,
                ["WorkOrderNo"] = entity.WorkOrderNo,
                ["SalesOrderNo"] = entity.SalesOrderNo,
                ["ProductionMainNo"] = entity.ProductionMainNo,
                ["ProductionSubNo"] = entity.ProductionSubNo ?? "",
                ["ProductionType"] = entity.ProductionType ?? "",
                ["Status"] = entity.Status.ToString(),
                ["CurrentExecDate"] = entity.CurrentExecDate?.ToString("yyyy-MM-dd") ?? "",
                ["CurrentGroupName"] = entity.CurrentGroupName ?? "",
                ["CurrentSectionName"] = entity.CurrentSectionName ?? "",
                ["CurrentEquipmentName"] = entity.CurrentEquipmentName ?? "",
                ["CurrentOutsource"] = entity.CurrentOutsource ?? "",
                ["CurrentSpec"] = entity.CurrentSpec ?? "",
                ["NextSectionName"] = entity.NextSectionName ?? "",
                ["CorrespondingSpec"] = entity.CorrespondingSpec ?? ""
            }
        };

        return TablePrintHelper.GeneratePdf($"生产批次 - {entity.BatchNo}", items, columns);
    }

    public async Task<byte[]> PrintBatchAllAsync(BatchPrintAllRequest request)
    {
        var queryable = _context.ProductionBatches.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            var kw = request.Keyword;
            queryable = queryable.Where(b =>
                b.BatchNo.Contains(kw) ||
                b.WorkOrderNo.Contains(kw) ||
                b.SalesOrderNo.Contains(kw) ||
                b.ProductionMainNo.Contains(kw) ||
                (b.ProductionSubNo != null && b.ProductionSubNo.Contains(kw)) ||
                (b.TagNo != null && b.TagNo.Contains(kw)));
        }
        if (!string.IsNullOrEmpty(request.WorkOrderNo))
            queryable = queryable.Where(b => b.WorkOrderNo.Contains(request.WorkOrderNo));
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<BatchStatus>(request.Status, out var batchStatus))
            queryable = queryable.Where(b => b.Status == batchStatus);
        if (!string.IsNullOrEmpty(request.TagNo))
            queryable = queryable.Where(b => b.TagNo != null && b.TagNo.Contains(request.TagNo));
        if (!string.IsNullOrEmpty(request.BatchNo))
            queryable = queryable.Where(b => b.BatchNo.Contains(request.BatchNo));
        if (!string.IsNullOrEmpty(request.SalesOrderNo))
            queryable = queryable.Where(b => b.SalesOrderNo.Contains(request.SalesOrderNo));
        if (!string.IsNullOrEmpty(request.ProductionMainNo))
            queryable = queryable.Where(b => b.ProductionMainNo.Contains(request.ProductionMainNo));
        if (!string.IsNullOrEmpty(request.ProductionSubNo))
            queryable = queryable.Where(b => b.ProductionSubNo != null && b.ProductionSubNo.Contains(request.ProductionSubNo));

        var entities = await queryable
            .OrderByDescending(b => b.CreatedTime)
            .ToListAsync();

        var items = entities.Select(b => new Dictionary<string, object>
        {
            ["BatchNo"] = b.BatchNo,
            ["TagNo"] = b.TagNo ?? "",
            ["CreatedTime"] = b.CreatedTime,
            ["WorkOrderNo"] = b.WorkOrderNo,
            ["SalesOrderNo"] = b.SalesOrderNo,
            ["ProductionMainNo"] = b.ProductionMainNo,
            ["ProductionSubNo"] = b.ProductionSubNo ?? "",
            ["ProductionType"] = b.ProductionType ?? "",
            ["Status"] = b.Status.ToString(),
            ["CurrentExecDate"] = b.CurrentExecDate,
            ["CurrentGroupName"] = b.CurrentGroupName ?? "",
            ["CurrentSectionName"] = b.CurrentSectionName ?? "",
            ["CurrentEquipmentName"] = b.CurrentEquipmentName ?? "",
            ["CurrentOutsource"] = b.CurrentOutsource ?? "",
            ["CurrentSpec"] = b.CurrentSpec ?? "",
            ["NextSectionName"] = b.NextSectionName ?? "",
            ["CorrespondingSpec"] = b.CorrespondingSpec ?? ""
        }).ToList();

        var columns = new List<PrintColumnDef>
        {
            new() { Key = "BatchNo", Label = "生产编号" },
            new() { Key = "TagNo", Label = "挂牌号" },
            new() { Key = "CreatedTime", Label = "创建时间" },
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "SalesOrderNo", Label = "订单号" },
            new() { Key = "ProductionMainNo", Label = "主号" },
            new() { Key = "ProductionSubNo", Label = "次号" },
            new() { Key = "ProductionType", Label = "生产类型" },
            new() { Key = "Status", Label = "状态" },
            new() { Key = "CurrentExecDate", Label = "截止执行日" },
            new() { Key = "CurrentGroupName", Label = "当前工序" },
            new() { Key = "CurrentSectionName", Label = "当前工段" },
            new() { Key = "CurrentEquipmentName", Label = "当前设备" },
            new() { Key = "CurrentOutsource", Label = "当前委外" },
            new() { Key = "CurrentSpec", Label = "当前规格" },
            new() { Key = "NextSectionName", Label = "下一工段" },
            new() { Key = "CorrespondingSpec", Label = "对应规格" }
        };

        return TablePrintHelper.GeneratePdf("生产批次列表", items, columns);
    }

    public async Task<byte[]> PrintBatchSelectedAsync(int[] ids)
    {
        var entities = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .OrderBy(b => b.CreatedTime)
            .ToListAsync();

        if (entities.Count == 0)
            throw new BusinessException("未找到选中的批次数据");

        var items = entities.Select(b => new Dictionary<string, object>
        {
            ["BatchNo"] = b.BatchNo,
            ["TagNo"] = b.TagNo ?? "",
            ["CreatedTime"] = b.CreatedTime,
            ["WorkOrderNo"] = b.WorkOrderNo,
            ["SalesOrderNo"] = b.SalesOrderNo,
            ["ProductionMainNo"] = b.ProductionMainNo,
            ["ProductionSubNo"] = b.ProductionSubNo ?? "",
            ["ProductionType"] = b.ProductionType ?? "",
            ["Status"] = b.Status.ToString(),
            ["CurrentExecDate"] = b.CurrentExecDate,
            ["CurrentGroupName"] = b.CurrentGroupName ?? "",
            ["CurrentSectionName"] = b.CurrentSectionName ?? "",
            ["CurrentEquipmentName"] = b.CurrentEquipmentName ?? "",
            ["CurrentOutsource"] = b.CurrentOutsource ?? "",
            ["CurrentSpec"] = b.CurrentSpec ?? "",
            ["NextSectionName"] = b.NextSectionName ?? "",
            ["CorrespondingSpec"] = b.CorrespondingSpec ?? ""
        }).ToList();

        var columns = new List<PrintColumnDef>
        {
            new() { Key = "BatchNo", Label = "生产编号" },
            new() { Key = "TagNo", Label = "挂牌号" },
            new() { Key = "CreatedTime", Label = "创建时间" },
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "SalesOrderNo", Label = "订单号" },
            new() { Key = "ProductionMainNo", Label = "主号" },
            new() { Key = "ProductionSubNo", Label = "次号" },
            new() { Key = "ProductionType", Label = "生产类型" },
            new() { Key = "Status", Label = "状态" },
            new() { Key = "CurrentExecDate", Label = "截止执行日" },
            new() { Key = "CurrentGroupName", Label = "当前工序" },
            new() { Key = "CurrentSectionName", Label = "当前工段" },
            new() { Key = "CurrentEquipmentName", Label = "当前设备" },
            new() { Key = "CurrentOutsource", Label = "当前委外" },
            new() { Key = "CurrentSpec", Label = "当前规格" },
            new() { Key = "NextSectionName", Label = "下一工段" },
            new() { Key = "CorrespondingSpec", Label = "对应规格" }
        };

        return TablePrintHelper.GeneratePdf("生产批次列表", items, columns);
    }

    public async Task<byte[]> PrintProcessCardAsync(ProcessCardPrintRequest request)
    {
        List<ProductionBatch> entities;

        if (request.Ids.Length > 0)
        {
            entities = await _context.ProductionBatches
                .AsNoTracking()
                .Include(b => b.ProcessGroups)
                .Where(b => request.Ids.Contains(b.Id))
                .OrderBy(b => b.CreatedTime)
                .ToListAsync();
        }
        else
        {
            entities = await _context.ProductionBatches
                .AsNoTracking()
                .Include(b => b.ProcessGroups)
                .OrderBy(b => b.CreatedTime)
                .ToListAsync();
        }

        if (entities.Count == 0)
            throw new BusinessException("未找到批次数据");

        var columns = request.Columns;
        return ProcessCardPrintHelper.GeneratePdf("工 艺 流 转 卡", entities, columns);
    }

    // ========== 辅助方法 ==========

    private static int? CalculateProductionRatio(CreateProductionBatchRequest request, WorkOrder? workOrder)
    {
        // 仅在定尺(Fixed)状态下计算
        var lengthStatus = request.LengthStatus ?? workOrder?.LengthStatus.ToString() ?? "";
        if (lengthStatus != "Fixed")
            return request.ProductionRatio;

        // 投料单重
        if (request.InputWeight == null || request.InputQuantity == null || request.InputQuantity <= 0)
            return request.ProductionRatio;
        var unitInputWeight = request.InputWeight.Value / (decimal)request.InputQuantity.Value;

        // 工单单重
        var totalWeight = request.TotalWeight ?? workOrder?.TotalWeight;
        var totalQty = request.TotalQuantity ?? workOrder?.TotalQuantity;
        if (totalWeight == null || totalQty == null || totalQty <= 0)
            return request.ProductionRatio;
        var unitWorkWeight = totalWeight.Value / (decimal)totalQty.Value;

        if (unitWorkWeight <= 0)
            return request.ProductionRatio;

        // rounddown
        return (int)Math.Floor(unitInputWeight / unitWorkWeight);
    }

    private async Task<string> GenerateBatchNoAsync()
    {
        var now = DateTime.Now;
        var prefix = now.ToString("yyMM");

        // 查找当月最大序号
        var maxNo = await _context.ProductionBatches
            .Where(b => b.BatchNo.StartsWith(prefix))
            .OrderByDescending(b => b.BatchNo)
            .Select(b => b.BatchNo)
            .FirstOrDefaultAsync();

        int nextSeq = 1;
        if (!string.IsNullOrEmpty(maxNo) && maxNo.Length >= 9)
        {
            var seqStr = maxNo[5..9]; // 取后4位
            if (int.TryParse(seqStr, out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        if (nextSeq > 9999)
            throw new BusinessException("当月生产编号已用尽");

        return $"{prefix}-{nextSeq:D4}";
    }

    public async Task<List<BatchWorkOrderMismatchDto>> VerifyWorkOrderNosAsync()
    {
        // 获取所有生产批次中非空的工单号
        var batchWorkOrderNos = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => !string.IsNullOrEmpty(b.WorkOrderNo))
            .Select(b => new { b.Id, b.BatchNo, b.WorkOrderNo })
            .ToListAsync();

        if (batchWorkOrderNos.Count == 0)
            return new List<BatchWorkOrderMismatchDto>();

        // 获取所有存在的工单号
        var existingWorkOrderNos = await _context.WorkOrders
            .AsNoTracking()
            .Select(wo => wo.WorkOrderNo)
            .Distinct()
            .ToListAsync();

        var existingSet = new HashSet<string>(existingWorkOrderNos, StringComparer.OrdinalIgnoreCase);

        // 找出批次引用了但工单表中不存在的工单号
        var mismatches = batchWorkOrderNos
            .Where(b => !existingSet.Contains(b.WorkOrderNo))
            .Select(b => new BatchWorkOrderMismatchDto
            {
                BatchId = b.Id,
                BatchNo = b.BatchNo,
                WorkOrderNo = b.WorkOrderNo
            })
            .ToList();

        _logger.LogInformation("工单号验证完成: 共检查 {Total} 个批次, 发现 {Mismatch} 个不匹配",
            batchWorkOrderNos.Count, mismatches.Count);

        return mismatches;
    }

    private static ProductionBatchDetailDto ToDetailDto(ProductionBatch entity)
    {
        return new ProductionBatchDetailDto
        {
            Id = entity.Id,
            BatchNo = entity.BatchNo,
            Status = entity.Status.ToString(),
            TagNo = entity.TagNo,
            ProductionType = entity.ProductionType,
            ProductionRatio = entity.ProductionRatio,
            IsForceCompleted = entity.IsForceCompleted,
            QualityRemark = entity.QualityRemark,
            SolutionParams = entity.SolutionParams,
            CurrentExecDate = entity.CurrentExecDate,
            CurrentGroupName = entity.CurrentGroupName,
            CurrentSectionName = entity.CurrentSectionName,
            CurrentEquipmentName = entity.CurrentEquipmentName,
            CurrentOutsource = entity.CurrentOutsource,
            CurrentSpec = entity.CurrentSpec,
            NextSectionName = entity.NextSectionName,
            CorrespondingSpec = entity.CorrespondingSpec,
            Remark = entity.Remark,

            // 工单冗余
            WorkOrderNo = entity.WorkOrderNo,
            SalesOrderNo = entity.SalesOrderNo,
            ProductionMainNo = entity.ProductionMainNo,
            ProductionSubNo = entity.ProductionSubNo,
            OrderItemIds = entity.OrderItemIds,
            SignDate = entity.SignDate,
            Salesman = entity.Salesman,
            EndCustomer = entity.EndCustomer,
            DeliveryDate = entity.DeliveryDate,
            DelayPenalty = entity.DelayPenalty,
            MaterialName = entity.MaterialName,
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
            TechnicalRequirements = entity.TechnicalRequirements,

            // 仓库冗余
            SourceBatchNo = entity.SourceBatchNo,
            WarehouseId = entity.WarehouseId,
            SourceMaterialType = entity.SourceMaterialType,
            InboundSource = entity.InboundSource,
            SourceName = entity.SourceName,
            InboundDate = entity.InboundDate,
            SourceHeatNo = entity.SourceHeatNo,
            SourcePlantGrade = entity.SourcePlantGrade,
            SourceSpecification = entity.SourceSpecification,
            SourceLengthStatus = entity.SourceLengthStatus,
            SourceUnitWeight = entity.SourceUnitWeight,
            InputQuantity = entity.InputQuantity,
            InputWeight = entity.InputWeight,
            CurrentValidQty = entity.CurrentValidQty,
            CurrentValidWeight = entity.CurrentValidWeight,

            // 审计
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy,
            UpdatedTime = entity.UpdatedTime,
            UpdatedBy = entity.UpdatedBy,

            RowVersion = entity.RowVersion,

            ProcessGroups = entity.ProcessGroups?.Select(ToGroupDto).ToList() ?? new()
        };
    }

    private static ProcessGroupDto ToGroupDto(ProcessGroup entity)
    {
        return new ProcessGroupDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            SequenceNumber = entity.SequenceNumber,
            ProcessName = entity.ProcessName,
            ManufacturingSpec = entity.ManufacturingSpec,
            OuterDiameterTolerance = entity.OuterDiameterTolerance,
            WallThicknessTolerance = entity.WallThicknessTolerance,
            ManufacturingLength = entity.ManufacturingLength,
            CuttingTreatment = entity.CuttingTreatment,
            Remark = entity.Remark,
            ColdRollDraw = entity.ColdRollDraw,
            OilPipeCut = entity.OilPipeCut,
            Degrease = entity.Degrease,
            Solution = entity.Solution,
            Straighten = entity.Straighten,
            Cut = entity.Cut,
            ThicknessMeasure = entity.ThicknessMeasure,
            Pickle = entity.Pickle,
            OuterPolish = entity.OuterPolish,
            InnerGrinding = entity.InnerGrinding,
            OuterSpotGrinding = entity.OuterSpotGrinding,
            Inspection = entity.Inspection,
            WeldingHead = entity.WeldingHead,
            Lubrication = entity.Lubrication,
            Warehouse = entity.Warehouse,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy
        };
    }

    /// <summary>
    /// 工序组数值验证：至少1个工段有值、同组不重复、全局从1开始连续且不重复
    /// </summary>
    private static void ValidateProcessGroupValues(List<CreateProcessGroupRequest> processGroups)
    {
        var allValues = new List<int>();
        var nameSpecCombos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < processGroups.Count; i++)
        {
            var pg = processGroups[i];
            var values = new List<int?>
            {
                pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.Solution,
                pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                pg.OuterPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                pg.Inspection, pg.WeldingHead, pg.Lubrication, pg.Warehouse
            };
            var nonNullValues = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();

            if (nonNullValues.Count == 0)
                throw new BusinessException($"工序组第{i + 1}行：至少需要1个工段有值");

            if (nonNullValues.Distinct().Count() != nonNullValues.Count)
                throw new BusinessException($"工序组第{i + 1}行：工段数值不能重复");

            // 工序名称不为空时制造规格必填
            if (!string.IsNullOrWhiteSpace(pg.ProcessName) && string.IsNullOrWhiteSpace(pg.ManufacturingSpec))
                throw new BusinessException($"工序组第{i + 1}行：工序名称「{pg.ProcessName}」已填写，制造规格不能为空");

            // 工序名称+制造规格在批次内唯一
            if (!string.IsNullOrWhiteSpace(pg.ProcessName))
            {
                var comboKey = $"{pg.ProcessName}|{pg.ManufacturingSpec ?? ""}";
                if (!nameSpecCombos.Add(comboKey))
                    throw new BusinessException($"工序组第{i + 1}行：工序名称「{pg.ProcessName}」+制造规格「{pg.ManufacturingSpec ?? ""}」组合已在其他工序组中存在");
            }

            // 检查跨工序组重复
            foreach (var v in nonNullValues)
            {
                if (allValues.Contains(v))
                    throw new BusinessException($"工段数值{v}在多个工序组中重复，同一批次中每个工段值必须唯一");
                allValues.Add(v);
            }
        }

        // 全局连续性检查：从1开始连续
        if (allValues.Count > 0)
        {
            allValues.Sort();
            if (allValues[0] != 1)
                throw new BusinessException($"工段数值必须从1开始（当前最小值为{allValues[0]}）");
            for (int i = 1; i < allValues.Count; i++)
            {
                if (allValues[i] != allValues[i - 1] + 1)
                    throw new BusinessException($"工段数值必须连续（1,2,3...），缺失值: {allValues[i - 1] + 1}");
            }
        }
    }

    // ========== 批次操作日志 ==========

    public async Task AddOperationLogAsync(int batchId, string operationType, string? detail = null)
    {
        var log = new BatchOperationLog
        {
            ProductionBatchId = batchId,
            OperationType = operationType,
            Detail = detail,
            CreatedBy = "system",
            CreatedTime = DateTimeOffset.UtcNow
        };
        _context.BatchOperationLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<BatchOperationLogDto>> GetOperationLogsAsync(int batchId)
    {
        return await _context.BatchOperationLogs
            .Where(l => l.ProductionBatchId == batchId)
            .OrderByDescending(l => l.CreatedTime)
            .Select(l => new BatchOperationLogDto
            {
                Id = l.Id,
                OperationType = l.OperationType,
                Detail = l.Detail,
                CreatedBy = l.CreatedBy,
                CreatedTime = l.CreatedTime
            })
            .ToListAsync();
    }
}
