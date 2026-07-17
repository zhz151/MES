using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using MES.Core.DTOs.Warehouse;
using MES.Core.Helpers;
using MES.Core.DTOs.Batch;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Interfaces.Warehouse;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;
using WoEntity = MES.Data.Entities.WorkOrder.WorkOrder;

namespace MES.Services.Warehouse;

public class InventoryBatchWriteService : IInventoryBatchWriteService
{
    private readonly AppDbContext _context;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly IQualityProcessTrackingService _qualityProcessTracking;
    private readonly ILogger<InventoryBatchWriteService> _logger;
    private readonly IInventorySyncService _syncService;
    private static readonly SemaphoreSlim _batchNoLock = new(1, 1);

    private static InventoryBatchDto BatchToDto(InventoryBatch b) => new()
    {
        Id = b.Id,
        BatchNo = b.BatchNo,
        WarehouseId = b.WarehouseId,
        MaterialType = !string.IsNullOrEmpty(b.MaterialType) ? EnumHelper.TryParse<MaterialType>(b.MaterialType) ?? default : default,
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
        SurfaceCondition = b.SurfaceCondition,
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
        SourceOrderNo = b.SourceOrderNo,
        SourceOrderSequence = b.SourceOrderSequence
    };

    public InventoryBatchWriteService(
        AppDbContext context,
        IWorkOrderExecutionService workOrderExecutionService,
        IQualityProcessTrackingService qualityProcessTracking,
        IInventorySyncService syncService,
        ILogger<InventoryBatchWriteService> logger)
    {
        _context = context;
        _workOrderExecutionService = workOrderExecutionService;
        _qualityProcessTracking = qualityProcessTracking;
        _syncService = syncService;
        _logger = logger;
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

    private async Task TryRefreshQualityProcessTrackingAsync(string? batchNo)
    {
        if (string.IsNullOrWhiteSpace(batchNo)) return;
        try
        {
            await _qualityProcessTracking.RefreshByBatchNoAsync(batchNo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "质量过程跟踪刷新失败（不影响主流程）: BatchNo={BatchNo}", batchNo);
        }
    }

    private async Task TrySyncSourceOrderAsync(string? sourceOrderNo)
    {
        if (string.IsNullOrWhiteSpace(sourceOrderNo)) return;
        try
        {
            await _syncService.SyncSourceOrdersAsync(new List<string> { sourceOrderNo });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "来源单同步失败（不影响主流程）: SourceOrderNo={SourceOrderNo}", sourceOrderNo);
        }
    }

    /// <summary>
    /// 根据工单号自动填充订单号和项次ID列表
    /// </summary>
    private static void AutoFillWorkOrderInfo(InventoryBatch entity, Dictionary<string, WoEntity> workOrders)
    {
        if (string.IsNullOrEmpty(entity.WorkOrderNo))
            return;

        if (workOrders.TryGetValue(entity.WorkOrderNo, out var workOrder))
        {
            entity.SalesOrderNo = workOrder.SalesOrderNo;
            entity.OrderItemIds = workOrder.OrderItemIds;
        }
    }

    public async Task<InventoryBatchDto> GetByIdAsync(int id)
    {
        var entity = await _context.InventoryBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException("批次不存在");

        return BatchToDto(entity);
    }

    public async Task<InventoryBatchDto> InboundAsync(CreateInboundRequest request)
    {
        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId);

        if (warehouse == null)
            throw new BusinessException("仓库不存在");

        var batchNo = await GenerateBatchNoAsync();

        var entity = new InventoryBatch
        {
            BatchNo = batchNo,
            WarehouseId = request.WarehouseId,
            MaterialType = request.MaterialType.ToString(),
            PlantGrade = request.PlantGrade,
            Specification = request.Specification,
            InboundSource = request.InboundSource.ToString(),
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
            RemainingMeters = request.Meters,
            RemainingQuantity = request.InitialQuantity,
            RemainingWeight = request.InitialWeight,
            ActualSpecification = request.ActualSpecification,
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
            SourceOrderNo = request.SourceOrderNo,
            SourceOrderSequence = request.SourceOrderSequence
        };

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

        await TryRefreshQualityProcessTrackingAsync(entity.ProductionBatchNo);
        await TrySyncSourceOrderAsync(entity.SourceOrderNo);

        return BatchToDto(entity);
    }

    public async Task<BatchInboundResult> BatchInboundAsync(BatchInboundRequest request)
    {
        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId);

        if (warehouse == null)
            throw new BusinessException("仓库不存在");

        var results = new List<string>();
        var productionBatchNos = new List<string?>();
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                var batchNos = await GenerateBatchNoSequenceAsync(request.Rows.Count);

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
                    : new Dictionary<string, WoEntity>();

                for (int i = 0; i < request.Rows.Count; i++)
                {
                    var row = request.Rows[i];
                    var batchNo = batchNos[i];

                    var entity = new InventoryBatch
                    {
                        BatchNo = batchNo,
                        WarehouseId = request.WarehouseId,
                        MaterialType = (row.MaterialType ?? request.MaterialType)?.ToString() ?? string.Empty,
                        PlantGrade = row.PlantGrade ?? request.PlantGrade ?? string.Empty,
                        Specification = row.Specification ?? request.Specification ?? string.Empty,
                        InboundSource = (row.InboundSource ?? request.InboundSource).ToString() ?? string.Empty,
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
                        RemainingMeters = row.Meters ?? request.Meters,
                        RemainingQuantity = row.InitialQuantity,
                        RemainingWeight = row.InitialWeight,
                        ActualSpecification = row.ActualSpecification ?? request.ActualSpecification,
                        SurfaceCondition = row.SurfaceCondition ?? request.SurfaceCondition,
                        LocationArea = row.LocationArea ?? request.LocationArea,
                        LocationRack = row.LocationRack ?? request.LocationRack,
                        Remark = row.Remark,
                        DefectReason = row.DefectReason ?? request.DefectReason,
                        LiabilityType = row.LiabilityType ?? request.LiabilityType,
                        OriginalSupplier = row.OriginalSupplier ?? request.OriginalSupplier,
                        TagNo = row.TagNo ?? request.TagNo,
                        DefectRemark = row.DefectRemark ?? request.DefectRemark,
                        IsLinkedToWorkOrder = row.IsLinkedToWorkOrder ?? false,
                        WorkOrderNo = row.WorkOrderNo,
                        SalesOrderNo = row.SalesOrderNo,
                        OrderItemIds = row.OrderItemIds,
                        SourceOrderNo = row.SourceOrderNo ?? request.SourceOrderNo,
                        SourceOrderSequence = row.SourceOrderSequence ?? request.SourceOrderSequence
                    };

                    AutoFillWorkOrderInfo(entity, workOrders);

                    _context.InventoryBatches.Add(entity);
                    results.Add(batchNo);
                    productionBatchNos.Add(row.ProductionBatchNo ?? request.ProductionBatchNo);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // 去重同步所有关联的来源单号
        var sourceOrderNos = request.Rows
            .Select(r => r.SourceOrderNo ?? request.SourceOrderNo)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToList();
        foreach (var son in sourceOrderNos)
            await TrySyncSourceOrderAsync(son);

        return new BatchInboundResult
        {
            SuccessCount = results.Count,
            BatchNos = results
        };
    }

    public async Task<InventoryBatchDto> UpdateInventoryBatchAsync(int id, UpdateInventoryBatchRequest request)
    {
        var entity = await _context.InventoryBatches
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException("入库批次不存在");

        var oldQuantity = entity.InitialQuantity;
        var oldWeight = entity.InitialWeight;
        var oldMeters = entity.Meters;
        var oldRemainingMeters = entity.RemainingMeters;

        // 仅允许变更 Group 3（手动输入）字段，Group 1（来源信息）和 Group 2（自动填充）只读
        entity.BatchNo = request.BatchNo ?? entity.BatchNo;
        if (request.InboundDate.HasValue) entity.InboundDate = request.InboundDate.Value;
        entity.HeatNo = request.HeatNo ?? entity.HeatNo;
        entity.LengthStatus = request.LengthStatus ?? entity.LengthStatus;
        entity.MinLength = request.MinLength ?? entity.MinLength;
        entity.MaxLength = request.MaxLength ?? entity.MaxLength;
        entity.UnitWeight = request.UnitWeight ?? entity.UnitWeight;
        entity.Meters = request.Meters ?? entity.Meters;
        entity.SurfaceCondition = request.SurfaceCondition ?? entity.SurfaceCondition;
        entity.LocationArea = request.LocationArea ?? entity.LocationArea;
        entity.LocationRack = request.LocationRack ?? entity.LocationRack;
        entity.Remark = request.Remark ?? entity.Remark;

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

        if (request.Meters.HasValue)
        {
            var outboundTotalM = (oldMeters ?? 0m) - (oldRemainingMeters ?? 0m);
            var newRemainingM = request.Meters.Value - outboundTotalM;
            if (newRemainingM < 0)
                throw new BusinessException($"批次{entity.BatchNo}已出库{outboundTotalM:G29}m，新米数{request.Meters.Value:G29}m不足覆盖（差额{Math.Abs(newRemainingM):G29}m），请先更正出库后再更改入库");
            entity.RemainingMeters = newRemainingM;
        }

        await _context.SaveChangesAsync();

        await TryRefreshExecutionSummaryAsync(entity.WorkOrderNo);
        await TryRefreshQualityProcessTrackingAsync(entity.ProductionBatchNo);
        await TrySyncSourceOrderAsync(entity.SourceOrderNo);

        var dto = BatchToDto(entity);
        return dto;
    }

    public async Task HardDeleteInventoryBatchAsync(int id)
    {
        var entity = await _context.InventoryBatches
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException("入库批次不存在");

        var hasOutbounds = await _context.OutboundRecords
            .AnyAsync(r => r.InventoryBatchId == id);
        if (hasOutbounds)
            throw new BusinessException($"批次{entity.BatchNo}存在出库记录，无法直接删除。请先在出库历史中删除关联的出库记录后重试");

        var sourceOrderNo = entity.SourceOrderNo;
        var workOrderNo = entity.WorkOrderNo;
        var productionBatchNo = entity.ProductionBatchNo;
        _context.InventoryBatches.Remove(entity);
        await _context.SaveChangesAsync();

        await TryRefreshExecutionSummaryAsync(workOrderNo);
        await TryRefreshQualityProcessTrackingAsync(productionBatchNo);
        await TrySyncSourceOrderAsync(sourceOrderNo);
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
}
