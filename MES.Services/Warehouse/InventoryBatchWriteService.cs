using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Helpers;
using MES.Core.Constants;
using MES.Core.DTOs.Batch;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Interfaces.Warehouse;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;
using WoEntity = MES.Data.Entities.WorkOrder.WorkOrder;

namespace MES.Services.Warehouse;

public class InventoryBatchWriteService : IInventoryBatchWriteService
{
    private readonly AppDbContext _context;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly IQualityProcessTrackingService _qualityProcessTracking;
    private readonly IProductionRecordService _productionRecordService;
    private readonly ILogger<InventoryBatchWriteService> _logger;
    private readonly IInventorySyncService _syncService;
    private readonly INotificationService _notificationService;
    private readonly IFixedLengthWorkOrderService _fixedLengthWorkOrderService;
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
        OrderItemIds = b.OrderItemIds,
        SourceOrderNo = b.SourceOrderNo,
        SourceOrderSequence = b.SourceOrderSequence
    };

    // ========== 定尺切割长度匹配标识 ==========

    /// <summary>
    /// 成品入库物料类型集合（FG 成品，等价成品判定）
    /// </summary>
    private static readonly HashSet<string> _fgMaterialTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        InventoryMaterialTypes.Finished,
        InventoryMaterialTypes.OrderFinished,
        InventoryMaterialTypes.CriticalFinished,
        InventoryMaterialTypes.SpecialDeliveryStatus
    };

    /// <summary>
    /// 定尺切割长度匹配标识计算（纯判定，ToList 后内存比较）。
    /// 适用条件：成品库(FG) + FG 成品物料 + 定尺 + 关联可解析 + 长度映射存在；命中本工单号定尺集合 → 完全匹配；命中订单+主号集合 → 主号匹配；否则 null。
    /// 其他库房（次品库等）即使有生产批号关联的入库也不核查（库房维度为准，与前端列显隐一致）。
    /// </summary>
    private static string? ComputeCutLengthMatch(string? warehouseCode, string? materialType, string? lengthStatus, decimal? minLength,
        FixedLengthLengthMaps? maps, (string WorkOrderNo, string SalesOrderNo, string ProductionMainNo)? assoc)
    {
        if (!string.Equals(warehouseCode, "FG", StringComparison.OrdinalIgnoreCase)) return null;
        if (string.IsNullOrEmpty(materialType)) return null;
        if (!_fgMaterialTypes.Contains(materialType)) return null;
        if (!string.Equals(lengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase)) return null;
        if (assoc == null || maps == null) return null;

        var woLengths = maps.ByWorkOrderNo.GetValueOrDefault(assoc.Value.WorkOrderNo, new HashSet<decimal>());
        var mainNoLengths = maps.ByMainKey.GetValueOrDefault($"{assoc.Value.SalesOrderNo.Trim()}|{assoc.Value.ProductionMainNo.Trim()}", new HashSet<decimal>());
        return CutLengthMatchHelper.Match(woLengths, mainNoLengths, minLength)?.ToString();
    }

    /// <summary>
    /// 解析入库批次的工单关联（工单号/订单号/主号）：生产批号为主 → 生产批次；工单号兜底 → 工单。
    /// 支持传入预载字典避免 N+1；未传则逐条查询。返回 null 表示两者皆不可解析。
    /// </summary>
    private async Task<(string WorkOrderNo, string SalesOrderNo, string ProductionMainNo)?> ResolveAssociationAsync(InventoryBatch entity,
        Dictionary<string, ProductionBatch>? batchDict = null, Dictionary<string, WoEntity>? workOrderDict = null)
    {
        if (!string.IsNullOrEmpty(entity.ProductionBatchNo))
        {
            ProductionBatch? pb = null;
            if (batchDict != null)
                batchDict.TryGetValue(entity.ProductionBatchNo, out pb);
            else
                pb = await _context.ProductionBatches.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.BatchNo == entity.ProductionBatchNo);
            if (pb != null)
                return (pb.WorkOrderNo, pb.SalesOrderNo, pb.ProductionMainNo);
        }

        if (!string.IsNullOrEmpty(entity.WorkOrderNo))
        {
            WoEntity? wo = null;
            if (workOrderDict != null)
                workOrderDict.TryGetValue(entity.WorkOrderNo, out wo);
            else
                wo = await _context.WorkOrders.AsNoTracking()
                    .FirstOrDefaultAsync(w => w.WorkOrderNo == entity.WorkOrderNo);
            if (wo != null)
                return (wo.WorkOrderNo, wo.SalesOrderNo, wo.ProductionMainNo);
        }

        return null;
    }

    public InventoryBatchWriteService(
        AppDbContext context,
        IWorkOrderExecutionService workOrderExecutionService,
        IQualityProcessTrackingService qualityProcessTracking,
        IProductionRecordService productionRecordService,
        IInventorySyncService syncService,
        INotificationService notificationService,
        IFixedLengthWorkOrderService fixedLengthWorkOrderService,
        ILogger<InventoryBatchWriteService> logger)
    {
        _context = context;
        _workOrderExecutionService = workOrderExecutionService;
        _qualityProcessTracking = qualityProcessTracking;
        _productionRecordService = productionRecordService;
        _syncService = syncService;
        _notificationService = notificationService;
        _fixedLengthWorkOrderService = fixedLengthWorkOrderService;
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

    /// <summary>
    /// 入库一致性校验（仅提醒不阻止）
    /// 触发条件：同生产批号 + 同制造物品匹配，且入库制造状态与生产批次制造状态不一致
    /// 三态一致不发；制造物品与批次本身不一致不发（不同物料类别，状态比较无意义）
    /// 仅入库方显式填写制造状态且双方均非空时比较，避免空值误报
    /// </summary>
    private async Task CheckInboundConsistencyAndNotifyAsync(InventoryBatch entity)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entity.ProductionBatchNo))
                return;

            var batch = await _context.ProductionBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BatchNo == entity.ProductionBatchNo);
            if (batch == null) return;

            // 制造物品与批次本身不一致 → 无需通知
            if (string.IsNullOrEmpty(batch.ManufacturingItem)
                || !string.Equals(entity.MaterialType, batch.ManufacturingItem, StringComparison.OrdinalIgnoreCase))
                return;

            // 制造状态一致（三态一致）或任一方为空 → 无需通知
            if (string.IsNullOrEmpty(entity.ManufacturingStatus)
                || string.IsNullOrEmpty(batch.ManufacturingStatus)
                || string.Equals(entity.ManufacturingStatus, batch.ManufacturingStatus, StringComparison.OrdinalIgnoreCase))
                return;

            // 短时去重：同生产批号30分钟内未读通知不再重复发
            var cutoff = DateTimeOffset.Now.AddMinutes(-30);
            var recent = await _context.Notifications.AnyAsync(n =>
                n.NotificationType == nameof(NotificationType.InboundMismatchAlert)
                && n.Content != null
                && n.Content.Contains(entity.ProductionBatchNo)
                && !n.IsRead
                && n.CreatedTime >= cutoff);
            if (recent) return;

            await _notificationService.CreateAsync(
                nameof(NotificationType.InboundMismatchAlert),
                $"入库制造状态不一致：{entity.ProductionBatchNo}",
                $"入库批次 {entity.BatchNo}（生产批号 {entity.ProductionBatchNo}）的制造状态「{entity.ManufacturingStatus}」与生产批次制造状态「{batch.ManufacturingStatus}」不一致，请核对。",
                targetId: entity.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "入库一致性通知失败（不影响主流程）: BatchNo={BatchNo}", entity.BatchNo);
        }
    }

    /// <summary>
    /// 生产产出入库时，将批次推进到「完成」
    /// - FG 首笔入库：批次从 InFinalInspection → Completed（有成检到料的成品入库）
    /// - WIP 余库料入库：批次从 InProgress → Completed（无成检到料，入库即最后工序）
    /// </summary>
    private async Task TryCompleteProductionBatchAsync(string productionBatchNo)
    {
        try
        {
            var batch = await _context.ProductionBatches
                .FirstOrDefaultAsync(b => b.BatchNo == productionBatchNo);

            if (batch == null)
            {
                _logger.LogWarning("入库完成推进失败，批次不存在: {BatchNo}", productionBatchNo);
                return;
            }

            if (batch.Status == BatchStatus.InFinalInspection)
            {
                // 首笔入库：成检 → 完成
                // 先刷新跟踪字段（更新当前工段/截止执行日等），再设为完成
                await _productionRecordService.RefreshBatchTrackingFieldsAsync(batch.Id);
                await _context.Entry(batch).ReloadAsync();
                batch.Status = BatchStatus.Completed;
                await _context.SaveChangesAsync();
            }
            else if (batch.Status == BatchStatus.InProgress
                     && batch.ManufacturingItem == MaterialType.Surplus.ToString())
            {
                // 余库料入库：在产 → 完成（无成检到料阶段）
                await _productionRecordService.RefreshBatchTrackingFieldsAsync(batch.Id);
                await _context.Entry(batch).ReloadAsync();
                batch.Status = BatchStatus.Completed;
                await _context.SaveChangesAsync();
            }
            else
            {
                _logger.LogDebug("入库跳过完成推进，批次 {BatchNo} 状态={Status} 物品={Item}",
                    productionBatchNo, batch.Status, batch.ManufacturingItem);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "入库完成推进失败（不影响主流程）: BatchNo={BatchNo}", productionBatchNo);
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
        {
            entity.IsLinkedToWorkOrder = false;
            return;
        }

        entity.IsLinkedToWorkOrder = true;
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
            LengthStatus = request.LengthStatus?.ToString(),
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
            ManufacturingStatus = request.ManufacturingStatus?.ToString(),
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
            entity.IsLinkedToWorkOrder = true;
            var singleWo = await _context.WorkOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.WorkOrderNo == entity.WorkOrderNo);
            if (singleWo != null)
            {
                entity.SalesOrderNo = singleWo.SalesOrderNo;
                entity.OrderItemIds = singleWo.OrderItemIds;
            }
        }
        else
        {
            entity.IsLinkedToWorkOrder = false;
        }

        // 定尺切割长度匹配标识（生产批号为主 + 工单号兜底解析关联，长度映射一次取全表；仅成品库 FG 核查）
        var assoc = await ResolveAssociationAsync(entity);
        var lengthMaps = await _fixedLengthWorkOrderService.GetLengthMapsAsync();
        entity.CutLengthMatchType = ComputeCutLengthMatch(warehouse.Code, entity.MaterialType, entity.LengthStatus, entity.MinLength, lengthMaps, assoc);

        _context.InventoryBatches.Add(entity);
        await _context.SaveChangesAsync();

        // 入库一致性通知（仅提醒）
        await CheckInboundConsistencyAndNotifyAsync(entity);

        // 入库触发批次完成推进
        if (!string.IsNullOrEmpty(entity.ProductionBatchNo))
            await TryCompleteProductionBatchAsync(entity.ProductionBatchNo);

        await TryRefreshQualityProcessTrackingAsync(entity.ProductionBatchNo);
        await TrySyncSourceOrderAsync(entity.SourceOrderNo);
        await TryRefreshExecutionSummaryAsync(entity.WorkOrderNo);

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
        var createdEntities = new List<InventoryBatch>();
        var productionBatchNos = new List<string?>();
        var workOrderNos = new List<string?>();
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

                // 预载生产批次字典（生产批号为主关联解析）+ 定尺长度映射一次取全表
                var distinctBatchNos = request.Rows
                    .Select(r => r.ProductionBatchNo ?? request.ProductionBatchNo)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList();
                var productionBatches = distinctBatchNos.Count > 0
                    ? await _context.ProductionBatches.AsNoTracking()
                        .Where(b => distinctBatchNos.Contains(b.BatchNo))
                        .ToDictionaryAsync(b => b.BatchNo, b => b, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, ProductionBatch>();
                var lengthMaps = await _fixedLengthWorkOrderService.GetLengthMapsAsync();

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
                        LengthStatus = (row.LengthStatus ?? request.LengthStatus)?.ToString(),
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
                        ManufacturingStatus = (row.ManufacturingStatus ?? request.ManufacturingStatus)?.ToString(),
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

                    // 定尺切割长度匹配标识（生产批号为主 + 工单号兜底；仅成品库 FG 核查）
                    var rowAssoc = await ResolveAssociationAsync(entity, productionBatches, workOrders);
                    entity.CutLengthMatchType = ComputeCutLengthMatch(warehouse.Code, entity.MaterialType, entity.LengthStatus, entity.MinLength, lengthMaps, rowAssoc);

                    _context.InventoryBatches.Add(entity);
                    createdEntities.Add(entity);
                    results.Add(batchNo);
                    productionBatchNos.Add(row.ProductionBatchNo ?? request.ProductionBatchNo);
                    workOrderNos.Add(row.WorkOrderNo);
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

        // 入库一致性通知（仅提醒）
        foreach (var created in createdEntities)
            await CheckInboundConsistencyAndNotifyAsync(created);

        // 入库触发批次完成推进（去重）
        foreach (var pbn in productionBatchNos.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())
            await TryCompleteProductionBatchAsync(pbn!);

        // 去重同步所有关联的来源单号
        var sourceOrderNos = request.Rows
            .Select(r => r.SourceOrderNo ?? request.SourceOrderNo)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToList();
        foreach (var son in sourceOrderNos)
            await TrySyncSourceOrderAsync(son);

        // 去重刷新质量过程跟踪（按生产批号）
        foreach (var pbn in productionBatchNos.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())
            await TryRefreshQualityProcessTrackingAsync(pbn!);

        // 去重刷新工单执行状况（按工单号）
        foreach (var won in workOrderNos.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())
            await TryRefreshExecutionSummaryAsync(won!);

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
        entity.LengthStatus = request.LengthStatus?.ToString() ?? entity.LengthStatus;
        entity.MinLength = request.MinLength ?? entity.MinLength;
        entity.MaxLength = request.MaxLength ?? entity.MaxLength;
        entity.UnitWeight = request.UnitWeight ?? entity.UnitWeight;
        entity.Meters = request.Meters ?? entity.Meters;
        entity.ManufacturingStatus = request.ManufacturingStatus?.ToString() ?? entity.ManufacturingStatus;
        entity.LocationArea = request.LocationArea ?? entity.LocationArea;
        entity.LocationRack = request.LocationRack ?? entity.LocationRack;
        entity.Remark = request.Remark ?? entity.Remark;

        // 长度值逻辑验证：定尺 Min==Max>0，范围尺 Max>Min>0
        var ls = request.LengthStatus?.ToString() ?? entity.LengthStatus;
        if (!string.IsNullOrEmpty(ls))
        {
            var minLen = request.MinLength ?? entity.MinLength;
            var maxLen = request.MaxLength ?? entity.MaxLength;
            if (ls == "Fixed")
            {
                if (!minLen.HasValue || !maxLen.HasValue)
                    throw new BusinessException("长度状态为「定尺」时，最小长度和最大长度必填");
                if (minLen.Value <= 0 || maxLen.Value <= 0)
                    throw new BusinessException("长度状态为「定尺」时，最小长度和最大长度必须大于0");
                if (minLen.Value != maxLen.Value)
                    throw new BusinessException("长度状态为「定尺」时，最小长度和最大长度必须相等");
            }
            else if (ls == "Range")
            {
                if (!minLen.HasValue || !maxLen.HasValue)
                    throw new BusinessException("长度状态为「范围尺」时，最小长度和最大长度必填");
                if (minLen.Value <= 0)
                    throw new BusinessException("长度状态为「范围尺」时，最小长度必须大于0");
                if (maxLen.Value <= minLen.Value)
                    throw new BusinessException("长度状态为「范围尺」时，最大长度必须大于最小长度");
            }
        }

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

        // IsLinkedToWorkOrder 级联：是→否（前端发送 IsLinkedToWorkOrder=false 触发）
        if (request.IsLinkedToWorkOrder.HasValue && !request.IsLinkedToWorkOrder.Value)
        {
            entity.IsLinkedToWorkOrder = false;
            if (request.WorkOrderNo == "") entity.WorkOrderNo = null;
            if (request.SalesOrderNo == "") entity.SalesOrderNo = null;
            if (request.OrderItemIds == "") entity.OrderItemIds = null;
        }

        // 前端显式传入 MaterialType 时触发物料变更（FG 级联）
        if (request.MaterialType.HasValue)
        {
            entity.MaterialType = request.MaterialType.Value.ToString();
        }

        await _context.SaveChangesAsync();

        // 定尺切割长度匹配标识重算（长度状态/最小长度可能已变更；仅成品库 FG 核查）
        var updAssoc = await ResolveAssociationAsync(entity);
        var updMaps = await _fixedLengthWorkOrderService.GetLengthMapsAsync();
        var updWhCode = await _context.Warehouses.AsNoTracking()
            .Where(w => w.Id == entity.WarehouseId)
            .Select(w => w.Code)
            .FirstOrDefaultAsync();
        entity.CutLengthMatchType = ComputeCutLengthMatch(updWhCode, entity.MaterialType, entity.LengthStatus, entity.MinLength, updMaps, updAssoc);
        await _context.SaveChangesAsync();

        // 入库一致性通知（仅提醒）
        await CheckInboundConsistencyAndNotifyAsync(entity);

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

        // 若此为关联该批次的最后一条入库记录，回退批次状态
        if (!string.IsNullOrEmpty(productionBatchNo))
        {
            var remainingCount = await _context.InventoryBatches
                .CountAsync(ib => ib.ProductionBatchNo == productionBatchNo);
            if (remainingCount == 0)
            {
                var batch = await _context.ProductionBatches
                    .FirstOrDefaultAsync(b => b.BatchNo == productionBatchNo);
                if (batch != null && batch.Status == BatchStatus.Completed)
                {
                    await _productionRecordService.RefreshBatchTrackingFieldsAsync(batch.Id);
                }
            }
        }
    }

    public async Task<int> RefreshAllCutLengthMatchAsync()
    {
        // 跟踪加载全部入库批次（直接赋值 + SaveChanges 持久化）
        var batches = await _context.InventoryBatches.ToListAsync();

        // 预载生产批次字典 + 工单字典 + 定尺长度映射（Chunk 防 SQL Server 2100 参数上限）
        var batchNos = batches.Select(b => b.ProductionBatchNo)
            .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        var productionBatches = new Dictionary<string, ProductionBatch>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in batchNos.Chunk(1000))
        {
            foreach (var pb in await _context.ProductionBatches.AsNoTracking()
                .Where(b => chunk.Contains(b.BatchNo))
                .ToListAsync())
            {
                productionBatches[pb.BatchNo] = pb;
            }
        }

        var woNos = batches.Select(b => b.WorkOrderNo)
            .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        var workOrders = new Dictionary<string, WoEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in woNos.Chunk(1000))
        {
            foreach (var wo in await _context.WorkOrders.AsNoTracking()
                .Where(w => chunk.Contains(w.WorkOrderNo))
                .ToListAsync())
            {
                workOrders[wo.WorkOrderNo] = wo;
            }
        }

        // 预载库房代码字典（仅成品库 FG 核查）
        var warehouseCodes = await _context.Warehouses.AsNoTracking()
            .Where(w => batches.Select(b => b.WarehouseId).Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Code);

        var lengthMaps = await _fixedLengthWorkOrderService.GetLengthMapsAsync();

        var updated = 0;
        foreach (var batch in batches)
        {
            var assoc = await ResolveAssociationAsync(batch, productionBatches, workOrders);
            warehouseCodes.TryGetValue(batch.WarehouseId, out var whCode);
            var value = ComputeCutLengthMatch(whCode, batch.MaterialType, batch.LengthStatus, batch.MinLength, lengthMaps, assoc);
            if (batch.CutLengthMatchType != value)
            {
                batch.CutLengthMatchType = value;
                updated++;
            }
        }

        if (updated > 0)
            await _context.SaveChangesAsync();

        return updated;
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
