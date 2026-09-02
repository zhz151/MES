using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Auth;
using MES.Core.Interfaces.Batch;
using MES.Core.DTOs.Batch;
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
using MES.Core.Exceptions;
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
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Quality;
using MES.Core.Enums;
using MES.Services.Extensions;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.Helpers;

namespace MES.Services.Quality;

/// <summary>
/// 检验到料（成检到料）服务实现
/// </summary>
public class MaterialReceiveCheckService : IMaterialReceiveCheckService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MaterialReceiveCheckService> _logger;
    private readonly IQualityProcessTrackingService _qualityProcessTracking;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly IProductionRecordService _productionRecordService;
    private readonly IWorkOrderListSummaryRefreshService? _listSummaryService;
    private readonly IMemoryCache _cache;
    private readonly IOperatorNameValidator _operatorNameValidator;

    public MaterialReceiveCheckService(
        AppDbContext context,
        IQualityProcessTrackingService qualityProcessTracking,
        IWorkOrderExecutionService workOrderExecutionService,
        IProductionRecordService productionRecordService,
        ILogger<MaterialReceiveCheckService> logger,
        IMemoryCache cache,
        IOperatorNameValidator operatorNameValidator,
        IWorkOrderListSummaryRefreshService? listSummaryService = null)
    {
        _context = context;
        _qualityProcessTracking = qualityProcessTracking;
        _workOrderExecutionService = workOrderExecutionService;
        _productionRecordService = productionRecordService;
        _logger = logger;
        _cache = cache;
        _operatorNameValidator = operatorNameValidator;
        _listSummaryService = listSummaryService;
    }

    /// <summary>
    /// 解析 MaterialType，兼容旧版枚举名（OrderFinishedProduct → OrderFinished 等）
    /// </summary>
    private static MaterialType? ParseMaterialType(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value switch
        {
            "OrderFinishedProduct" => MaterialType.OrderFinished,
            "PreparedMaterial" or "PreparedFinished" or "StockFinished" => MaterialType.Finished,
            "SurplusStock" => MaterialType.Surplus,
            "IntermediateProduct" => MaterialType.SemiFinished,
            _ => Enum.TryParse<MaterialType>(value, true, out var r) ? r : null
        };
    }

    private async Task TryRefreshQualityProcessTrackingAsync(int mrCheckId)
    {
        try
        {
            await _qualityProcessTracking.RefreshByMrCheckIdAsync(mrCheckId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "质量过程跟踪刷新失败（不影响主流程）: MrCheckId={MrCheckId}", mrCheckId);
        }
    }

    private async Task TryRefreshExecutionSummaryAsync(string? workOrderNo)
    {
        if (string.IsNullOrWhiteSpace(workOrderNo) || workOrderNo == WorkOrderNoSentinel.NotWorkOrder) return;
        try
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(new List<string> { workOrderNo });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工单执行状况刷新失败（不影响主流程）: WorkOrderNo={WorkOrderNo}", workOrderNo);
        }
    }

    /// <summary>
    /// 刷新用料计划总览（WorkOrderListSummary）：成检到料增删改会经 RefreshBatchTrackingFieldsAsync
    /// 改变批次 Status（到料+入库=Completed → 删除后回退），进而影响产能工量 completedOutput，须联动刷新
    /// </summary>
    private async Task TryRefreshListSummaryAsync(string? salesOrderNo)
    {
        if (_listSummaryService == null || string.IsNullOrWhiteSpace(salesOrderNo)) return;
        try
        {
            await _listSummaryService.RefreshBySalesOrderAsync(salesOrderNo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "用料计划总览刷新失败（不影响主流程）: SalesOrderNo={SalesOrderNo}", salesOrderNo);
        }
    }

    /// <summary>
    /// 判断指定工序组是否是该批次中 Inspection 值最高的（用于 DeliveryState 有效性判定）
    /// </summary>
    private async Task<bool> IsLastProcessGroupAsync(int productionBatchId, int processGroupId)
    {
        var pgs = await _context.Set<ProcessGroup>()
            .Where(pg => pg.ProductionBatchId == productionBatchId && pg.Inspection.HasValue)
            .Select(pg => new { pg.Id, pg.Inspection })
            .ToListAsync();
        if (pgs.Count == 0) return true;
        var maxInsp = pgs.Max(pg => pg.Inspection!.Value);
        var insp = pgs.FirstOrDefault(pg => pg.Id == processGroupId)?.Inspection;
        return insp.HasValue && insp.Value == maxInsp;
    }

    /// <summary>
    /// 从 ProductionBatch 解析 DTO 的批次冗余字段（通过导航属性）
    /// </summary>
    private static MaterialReceiveCheckDto MapToDto(MaterialReceiveCheck m, ProductionBatch? batch = null, bool isLastProcessGroup = true)
    {
        batch ??= m.ProductionBatch;
        return new MaterialReceiveCheckDto
        {
            Id = m.Id,
            ProductionBatchId = m.ProductionBatchId,
            ReceiveDate = m.ReceiveDate,
            Shift = m.Shift,
            Checker = m.Checker,
            Remark = m.Remark,
            BatchNo = m.BatchNo!,
            DataSource = m.DataSource,
            IsForceCompleted = m.IsForceCompleted,
            ProcessGroupId = m.ProcessGroupId,
            ProcessName = m.ProcessName,
            SequenceNumber = m.SequenceNumber,
            ManufacturingItem = ParseMaterialType(batch?.ManufacturingItem),
            TagNo = batch?.TagNo,
            WorkOrderNo = batch?.WorkOrderNo,
            SalesOrderNo = batch?.SalesOrderNo,
            SourceUnit = batch?.SourceName,
            FurnaceNo = batch?.SourceHeatNo,
            PlantGrade = batch?.PlantGrade,
            Specification = batch?.Specification,
            ProductionType = EnumHelper.TryParse<ProductionType>(batch?.ProductionType),
            LengthStatus = EnumHelper.TryParse<LengthStatus>(batch?.LengthStatus),
            Salesman = batch?.Salesman,
            // 交货状态仅最后工序组的成检到料有效
            DeliveryState = isLastProcessGroup
                ? EnumHelper.TryParse<DeliveryState>(batch?.DeliveryState)
                : null,
            ManufacturingStatus = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(batch?.ManufacturingStatus),
            RawDeliveryState = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(batch?.DeliveryState),
            InspectionType = EnumHelper.TryParse<MES.Core.Enums.InspectionType>(m.InspectionType),
            CreatedTime = m.CreatedTime,
            UpdatedTime = m.UpdatedTime
        };
    }

    public async Task<MaterialReceiveCheckDto?> GetMaterialReceiveCheckAsync(int batchId)
    {
        var m = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Include(m => m.ProductionBatch)
            .Where(x => x.ProductionBatchId == batchId)
            .FirstOrDefaultAsync();

        if (m == null) return null;

        var isLast = await IsLastProcessGroupAsync(m.ProductionBatchId, m.ProcessGroupId);
        return MapToDto(m, isLastProcessGroup: isLast);
    }

    public async Task<MaterialReceiveCheckDto> CreateMaterialReceiveCheckAsync(CreateMaterialReceiveCheckRequest request)
    {
        // 优先通过BatchNo解析，兼容直接传ProductionBatchId
        if (request.ProductionBatchId <= 0 && !string.IsNullOrWhiteSpace(request.BatchNo))
        {
            var batchByNo = await _context.ProductionBatches
                .FirstOrDefaultAsync(b => b.BatchNo == request.BatchNo)
                ?? throw new BusinessException($"批次号不存在: {request.BatchNo}");
            request.ProductionBatchId = batchByNo.Id;
        }

        var batch = await _context.ProductionBatches
            .FirstOrDefaultAsync(b => b.Id == request.ProductionBatchId)
            ?? throw new BusinessException($"批次不存在: {request.ProductionBatchId}");

        // 仅成品类制造物品可建成检到料；非成品类（余库料等）的检验属"过程检验"，不走成检
        if (!ProductStatusHelper.IsFinishedManufacturingItem(batch.ManufacturingItem))
            throw new BusinessException($"批次 {batch.BatchNo} 为非成品类制造物品（{batch.ManufacturingItem}），检验应走过程检验，不能创建成检到料");

        // 操作人实名校验（非空才校验）
        await _operatorNameValidator.EnsureValidOrThrowAsync(request.Checker);

        var entity = new MaterialReceiveCheck
        {
            ProductionBatchId = request.ProductionBatchId,
            ReceiveDate = request.ReceiveDate,
            Shift = request.Shift ?? ShiftHelper.GetShiftByTime(),
            Checker = request.Checker,
            Remark = request.Remark,
            DataSource = request.DataSource ?? "MANUAL",
            BatchNo = batch.BatchNo,
            IsForceCompleted = false
        };

        // 工序关联：优先取请求参数，否则按规格匹配自动查找
        if (request.ProcessGroupId > 0)
        {
            // 精确查找工序组，取正确的 Inspection 值
            var pg = await _context.Set<ProcessGroup>()
                .FirstOrDefaultAsync(pg => pg.Id == request.ProcessGroupId
                    && pg.ProductionBatchId == request.ProductionBatchId
                    && pg.Inspection.HasValue);
            if (pg == null)
                throw new BusinessException("指定的工序组不存在或非检验工序组");

            entity.ProcessGroupId = pg.Id;
            entity.ProcessName = request.ProcessName ?? pg.ProcessName;
            entity.SequenceNumber = pg.Inspection!.Value;
        }
        else
        {
            // 按 ManufacturingSpec == batch.Specification 匹配工序组
            // 优先取非"附加成检"的工序组
            var pg = await _context.Set<ProcessGroup>()
                .Where(pg => pg.ProductionBatchId == request.ProductionBatchId
                          && pg.ManufacturingSpec == batch.Specification
                          && pg.Inspection.HasValue)
                .OrderBy(pg => pg.ProcessName == ProcessKeys.AdditionalFinalInspection ? 1 : 0)
                .ThenBy(pg => pg.SequenceNumber)
                .FirstOrDefaultAsync()
                ?? throw new BusinessException("批次未配置匹配成品规格的工序组，无法创建成检到料");

            entity.ProcessGroupId = pg.Id;
            entity.ProcessName = pg.ProcessName;
            entity.SequenceNumber = pg.Inspection!.Value;
        }

        // 按工序组查重（允许同一批次多个检验工序组分别到料）
        var exists = await _context.MaterialReceiveChecks
            .AnyAsync(m => m.ProcessGroupId == entity.ProcessGroupId);
        if (exists)
            throw new BusinessException($"该批次工序组「{ProcessKeys.ToChinese(entity.ProcessName)}」已完成成检到料，不能重复创建");

        // 自动判定成检类型（最后检验工序组=正式成检，否则=预成检）
        var isLast = await IsLastProcessGroupAsync(batch.Id, entity.ProcessGroupId);
        entity.InspectionType = isLast ? nameof(InspectionType.FormalInspection) : nameof(InspectionType.PreInspection);

        _context.MaterialReceiveChecks.Add(entity);

        // 批次设为成检
        batch.Status = BatchStatus.InFinalInspection;
        _context.ProductionBatches.Update(batch);

        await _context.SaveChangesAsync();

        await _productionRecordService.RefreshBatchTrackingFieldsAsync(batch.Id);
        await TryRefreshQualityProcessTrackingAsync(entity.Id);
        await TryRefreshExecutionSummaryAsync(batch.WorkOrderNo);
        await TryRefreshListSummaryAsync(batch.SalesOrderNo);

        return MapToDto(entity, batch, isLastProcessGroup: isLast);
    }

    public async Task<List<MaterialReceiveCheckDto>> BatchCreateMaterialReceiveChecksAsync(List<CreateMaterialReceiveCheckRequest> requests)
    {
        if (requests.Count == 0)
            return new List<MaterialReceiveCheckDto>();

        // 预加载所有涉及的批次
        var batchNos = requests.Where(r => r.ProductionBatchId <= 0 && !string.IsNullOrWhiteSpace(r.BatchNo))
            .Select(r => r.BatchNo).Distinct().ToList();
        var batchLookup = batchNos.Count > 0
            ? await _context.ProductionBatches.Where(b => batchNos.Contains(b.BatchNo)).ToDictionaryAsync(b => b.BatchNo)
            : new Dictionary<string, ProductionBatch>();

        // 检查所有批次是否存在
        var modifiedBatches = new List<ProductionBatch>();

        foreach (var request in requests)
        {
            if (request.ProductionBatchId <= 0 && !string.IsNullOrWhiteSpace(request.BatchNo))
            {
                if (!batchLookup.TryGetValue(request.BatchNo, out var batchByNo))
                    throw new BusinessException($"批次号不存在: {request.BatchNo}");
                request.ProductionBatchId = batchByNo.Id;
                modifiedBatches.Add(batchByNo);
            }
            else
            {
                var batch = await _context.ProductionBatches
                    .FirstOrDefaultAsync(b => b.Id == request.ProductionBatchId)
                    ?? throw new BusinessException($"批次不存在: {request.ProductionBatchId}");
                modifiedBatches.Add(batch);
            }
        }

        var entities = new List<MaterialReceiveCheck>();

        // 操作人实名校验：预加载启用员工快照一次，逐行行内校验
        var activeEmployees = await _operatorNameValidator.LoadActiveAsync();

        // 预加载所有相关批次的工序组（按 ManufacturingSpec 匹配）
        var allBatchIds = modifiedBatches.Select(b => b.Id).ToList();
        var batchSpecLookup = modifiedBatches.ToDictionary(b => b.Id, b => b.Specification);
        var allGroups = await _context.Set<ProcessGroup>()
            .Where(pg => allBatchIds.Contains(pg.ProductionBatchId)
                      && pg.ManufacturingSpec != null
                      && pg.Inspection.HasValue)
            .ToListAsync();

        // 预先查出所有已有成检到料的工序组 ID（按工序组粒度查重）
        var existingPgIds = await _context.MaterialReceiveChecks
            .Select(m => m.ProcessGroupId)
            .ToListAsync();
        var existingPgSet = new HashSet<int>(existingPgIds);

        foreach (var request in requests)
        {
            var batch = modifiedBatches[entities.Count];
            // 操作人实名校验（非空才校验，行内 throw）
            var unmatched = OperatorNameHelper.FindUnmatched(activeEmployees, request.Checker);
            if (unmatched.Count > 0)
                throw new BusinessException($"第{entities.Count + 1}行：操作人「{string.Join("、", unmatched)}」不在启用员工表中，请选择有效操作人");

            // 仅成品类制造物品可建成检到料；非成品类（余库料等）的检验属"过程检验"，不走成检
            if (!ProductStatusHelper.IsFinishedManufacturingItem(batch.ManufacturingItem))
                throw new BusinessException($"批次「{batch.BatchNo}」为非成品类制造物品（{batch.ManufacturingItem}），检验应走过程检验，不能创建成检到料");

            var batchSpec = batchSpecLookup.GetValueOrDefault(batch.Id, "");

            // 按 ManufacturingSpec == batch.Specification 匹配，优先非"附加成检"
            var matchedPg = allGroups
                .Where(pg => pg.ProductionBatchId == batch.Id
                          && pg.ManufacturingSpec == batchSpec)
                .OrderBy(pg => pg.ProcessName == ProcessKeys.AdditionalFinalInspection ? 1 : 0)
                .ThenBy(pg => pg.SequenceNumber)
                .FirstOrDefault()
                ?? throw new BusinessException($"批次「{batch.BatchNo}」未配置匹配成品规格的工序组，无法创建成检到料");

            var finalPgId = request.ProcessGroupId > 0 ? request.ProcessGroupId : matchedPg.Id;

            // 按工序组查重（允许同一批次不同检验工序组分别到料）
            if (existingPgSet.Contains(finalPgId))
                throw new BusinessException($"批次「{batch.BatchNo}」工序组「{ProcessKeys.ToChinese(matchedPg.ProcessName)}」已完成成检到料，不能重复创建");

            existingPgSet.Add(finalPgId); // 防止同一请求中重复

            // 用 finalPgId 精确查找对应的工序组（避免 ProcessName / SequenceNumber 取错）
            var finalPg = allGroups.FirstOrDefault(pg => pg.Id == finalPgId) ?? matchedPg;

            // 自动判定成检类型
            var maxInspForBatch = allGroups
                .Where(pg => pg.ProductionBatchId == batch.Id)
                .Max(pg => pg.Inspection!.Value);
            var isLastBatch = finalPg.Inspection!.Value >= maxInspForBatch;

            entities.Add(new MaterialReceiveCheck
            {
                ProductionBatchId = request.ProductionBatchId,
                ReceiveDate = request.ReceiveDate,
                Shift = request.Shift,
                Checker = request.Checker,
                Remark = request.Remark,
                DataSource = "MANUAL",
                BatchNo = batch.BatchNo,
                IsForceCompleted = false,
                ProcessGroupId = finalPgId,
                ProcessName = request.ProcessName ?? finalPg.ProcessName,
                SequenceNumber = finalPg.Inspection!.Value,
                InspectionType = isLastBatch ? nameof(InspectionType.FormalInspection) : nameof(InspectionType.PreInspection)
            });
        }

        foreach (var batch in modifiedBatches)
            batch.Status = BatchStatus.InFinalInspection;

        _context.MaterialReceiveChecks.AddRange(entities);
        _context.ProductionBatches.UpdateRange(modifiedBatches);
        await _context.SaveChangesAsync();

        // 批量创建后逐个刷新批次跟踪字段
        foreach (var modifiedBatch in modifiedBatches)
            await _productionRecordService.RefreshBatchTrackingFieldsAsync(modifiedBatch.Id);

        // 批量创建后逐个刷新质量过程跟踪
        foreach (var entity in entities)
            await TryRefreshQualityProcessTrackingAsync(entity.Id);

        // 去重刷新工单执行状况
        foreach (var woNo in modifiedBatches.Where(b => !string.IsNullOrWhiteSpace(b.WorkOrderNo) && b.WorkOrderNo != WorkOrderNoSentinel.NotWorkOrder)
                                 .Select(b => b.WorkOrderNo)
                                 .Distinct(StringComparer.OrdinalIgnoreCase))
            await TryRefreshExecutionSummaryAsync(woNo);

        // 去重刷新用料计划总览（成检到料创建会经批次状态变化影响产能工量）
        foreach (var soNo in modifiedBatches.Where(b => !string.IsNullOrWhiteSpace(b.SalesOrderNo))
                                 .Select(b => b.SalesOrderNo)
                                 .Distinct(StringComparer.OrdinalIgnoreCase))
            await TryRefreshListSummaryAsync(soNo);

        // 预查所有检验工序组，判定各记录是否为 Inspection 值最高的
        var lastPgBatchIds = modifiedBatches.Select(b => b.Id).Distinct().ToList();
        var allPgs = await _context.Set<ProcessGroup>()
            .Where(pg => lastPgBatchIds.Contains(pg.ProductionBatchId) && pg.Inspection.HasValue)
            .Select(pg => new { pg.Id, pg.ProductionBatchId, pg.Inspection })
            .ToListAsync();
        var maxInspByBatch = allPgs
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.Max(pg => pg.Inspection!.Value));
        var pgInspLookup = allPgs.ToDictionary(pg => pg.Id, pg => pg.Inspection!.Value);

        return entities.Select((e, i) =>
        {
            var isLast = pgInspLookup.TryGetValue(e.ProcessGroupId, out var insp)
                && maxInspByBatch.TryGetValue(e.ProductionBatchId, out var maxInsp)
                && insp == maxInsp;
            return MapToDto(e, modifiedBatches[i], isLastProcessGroup: isLast);
        }).ToList();
    }

    public async Task<MaterialReceiveCheckDto> UpdateMaterialReceiveCheckAsync(int id, UpdateMaterialReceiveCheckRequest request)
    {
        var entity = await _context.MaterialReceiveChecks
            .Include(e => e.ProductionBatch)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new BusinessException("成检到料记录不存在");

        if (request.ReceiveDate != default)
            entity.ReceiveDate = request.ReceiveDate;
        entity.Shift = request.Shift ?? entity.Shift;
        // 操作人实名校验（仅校验新传入值，非空才校验）
        await _operatorNameValidator.EnsureValidOrThrowAsync(request.Checker);
        entity.Checker = request.Checker ?? entity.Checker;
        entity.Remark = request.Remark ?? entity.Remark;
        if (request.IsForceCompleted.HasValue)
            entity.IsForceCompleted = request.IsForceCompleted.Value;

        // 重选工序组：校验属于该批次且为检验工序组，且未被其它到料占用
        if (request.ProcessGroupId is > 0)
        {
            if (request.ProcessGroupId.Value != entity.ProcessGroupId)
            {
                var newPg = await _context.ProcessGroups.AsNoTracking()
                    .FirstOrDefaultAsync(pg => pg.Id == request.ProcessGroupId.Value
                        && pg.ProductionBatchId == entity.ProductionBatchId
                        && pg.Inspection.HasValue)
                    ?? throw new BusinessException("指定的工序组不存在、不属于该批次或非检验工序组");

                var occupied = await _context.MaterialReceiveChecks
                    .AnyAsync(m => m.ProcessGroupId == newPg.Id && m.Id != id);
                if (occupied)
                    throw new BusinessException($"工序组「{ProcessKeys.ToChinese(newPg.ProcessName)}」已完成成检到料，不能重复关联");

                entity.ProcessGroupId = newPg.Id;
            }
        }

        // 保存时无条件重新同步推导值（工艺卡/工序组变更后纠正过期快照）：
        // 1) 工序冗余字段（工序名称/执行序）从当前工序组刷新
        // 2) 成检类型按当前工艺卡重新判定（该工序组是否仍为批次最深检验节点）
        var pg = await _context.ProcessGroups.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == entity.ProcessGroupId);
        if (pg != null)
        {
            entity.ProcessName = pg.ProcessName;
            // 「执行序」语义 = 检验工段值（Inspection），与创建逻辑一致（Create 用 pg.Inspection!.Value）；
            // 工序组被降级为非检验时保底保持旧值，避免空引用
            entity.SequenceNumber = pg.Inspection ?? entity.SequenceNumber;
        }
        var isLast = await IsLastProcessGroupAsync(entity.ProductionBatchId, entity.ProcessGroupId);
        entity.InspectionType = isLast ? nameof(InspectionType.FormalInspection) : nameof(InspectionType.PreInspection);

        _context.MaterialReceiveChecks.Update(entity);
        await _context.SaveChangesAsync();

        await TryRefreshQualityProcessTrackingAsync(entity.Id);

        // 更新可变更 ReceiveDate/IsForceCompleted/ProcessGroupId（→ InspectionType），
        // 均为批次跟踪字段（Status/CurrentExecDate/CurrentSectionName/InspectionStage）的判定输入，
        // 需与 Delete 对齐重算批次跟踪字段 + 工单执行状况读模型
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(entity.ProductionBatchId);
        await TryRefreshExecutionSummaryAsync(entity.ProductionBatch?.WorkOrderNo);
        await TryRefreshListSummaryAsync(entity.ProductionBatch?.SalesOrderNo);

        return MapToDto(entity, isLastProcessGroup: isLast);
    }

    public async Task DeleteMaterialReceiveCheckAsync(int id)
    {
        var entity = await _context.MaterialReceiveChecks.FindAsync(id)
            ?? throw new BusinessException("成检到料记录不存在");

        var batchId = entity.ProductionBatchId;
        _context.MaterialReceiveChecks.Remove(entity);
        await _context.SaveChangesAsync();

        // 重新计算批次跟踪字段（NextSectionName/Status/CurrentExecDate 等回退到删除前的状态）
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(batchId);

        // 删除本到料对应的物化行（RefreshByProductionBatchIdAsync 只重推导剩余到料，孤儿行须先显式删）
        var existingQpt = await _context.QualityProcessTrackings
            .FirstOrDefaultAsync(q => q.MaterialReceiveCheckId == id);
        if (existingQpt != null)
        {
            _context.QualityProcessTrackings.Remove(existingQpt);
            await _context.SaveChangesAsync();
        }

        // 删除后重推导批次剩余到料对应的物化行
        // （剩余到料的成检类型/检验状态可能因「最深检验节点」变化而调整，须整批重算）
        await TryRefreshQualityProcessTrackingByBatchAsync(batchId);

        var batch = await _context.ProductionBatches.FindAsync(batchId);
        await TryRefreshExecutionSummaryAsync(batch?.WorkOrderNo);
        await TryRefreshListSummaryAsync(batch?.SalesOrderNo);
    }

    private async Task TryRefreshQualityProcessTrackingByBatchAsync(int productionBatchId)
    {
        try
        {
            await _qualityProcessTracking.RefreshByProductionBatchIdAsync(productionBatchId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "质量过程跟踪批次重推导失败（不影响主流程）: BatchId={BatchId}", productionBatchId);
        }
    }

    public async Task<PagedResult<MaterialReceiveCheckDto>> GetAllMaterialReceiveChecksAsync(QueryParams query)
    {
        var queryable = ApplyListQueryFilters(_context.MaterialReceiveChecks.AsNoTracking().AsQueryable(), query);

        var totalCount = await queryable.CountAsync();

        queryable = (query.SortBy?.ToLower(), query.IsDescending) switch
        {
            ("batchno", false) => queryable.OrderBy(m => m.BatchNo ?? ""),
            ("batchno", true) => queryable.OrderByDescending(m => m.BatchNo ?? ""),
            ("receivedate", false) => queryable.OrderBy(m => m.ReceiveDate),
            ("receivedate", true) => queryable.OrderByDescending(m => m.ReceiveDate),
            ("checker", false) => queryable.OrderBy(m => m.Checker ?? ""),
            ("checker", true) => queryable.OrderByDescending(m => m.Checker ?? ""),
            ("createdtime", false) => queryable.OrderBy(m => m.CreatedTime),
            ("createdtime", true) => queryable.OrderByDescending(m => m.CreatedTime),
            ("updatedtime", false) => queryable.OrderBy(m => m.UpdatedTime),
            ("updatedtime", true) => queryable.OrderByDescending(m => m.UpdatedTime),
            ("shift", false) => queryable.OrderBy(m => m.Shift),
            ("shift", true) => queryable.OrderByDescending(m => m.Shift),
            ("remark", false) => queryable.OrderBy(m => m.Remark ?? ""),
            ("remark", true) => queryable.OrderByDescending(m => m.Remark ?? ""),
            ("manufacturingitem", false) => queryable.OrderBy(m => m.ProductionBatch.ManufacturingItem ?? ""),
            ("manufacturingitem", true) => queryable.OrderByDescending(m => m.ProductionBatch.ManufacturingItem ?? ""),
            ("plantgrade", false) => queryable.OrderBy(m => m.ProductionBatch.PlantGrade ?? ""),
            ("plantgrade", true) => queryable.OrderByDescending(m => m.ProductionBatch.PlantGrade ?? ""),
            ("specification", false) => queryable.OrderBy(m => m.ProductionBatch.Specification ?? ""),
            ("specification", true) => queryable.OrderByDescending(m => m.ProductionBatch.Specification ?? ""),
            ("productiontype", false) => queryable.OrderBy(m => m.ProductionBatch.ProductionType ?? ""),
            ("productiontype", true) => queryable.OrderByDescending(m => m.ProductionBatch.ProductionType ?? ""),
            ("tagno", false) => queryable.OrderBy(m => m.ProductionBatch.TagNo ?? ""),
            ("tagno", true) => queryable.OrderByDescending(m => m.ProductionBatch.TagNo ?? ""),
            ("workorderno", false) => queryable.OrderBy(m => m.ProductionBatch.WorkOrderNo ?? ""),
            ("workorderno", true) => queryable.OrderByDescending(m => m.ProductionBatch.WorkOrderNo ?? ""),
            ("salesorderno", false) => queryable.OrderBy(m => m.ProductionBatch.SalesOrderNo ?? ""),
            ("salesorderno", true) => queryable.OrderByDescending(m => m.ProductionBatch.SalesOrderNo ?? ""),
            ("productionmainno", false) => queryable.OrderBy(m => m.ProductionBatch.ProductionMainNo ?? ""),
            ("productionmainno", true) => queryable.OrderByDescending(m => m.ProductionBatch.ProductionMainNo ?? ""),
            ("furnaceno", false) => queryable.OrderBy(m => m.ProductionBatch.SourceHeatNo ?? ""),
            ("furnaceno", true) => queryable.OrderByDescending(m => m.ProductionBatch.SourceHeatNo ?? ""),
            ("sourceunit", false) => queryable.OrderBy(m => m.ProductionBatch.SourceName ?? ""),
            ("sourceunit", true) => queryable.OrderByDescending(m => m.ProductionBatch.SourceName ?? ""),
            ("datasource", false) => queryable.OrderBy(m => m.DataSource ?? ""),
            ("datasource", true) => queryable.OrderByDescending(m => m.DataSource ?? ""),
            ("isforcecompleted", false) => queryable.OrderBy(m => m.IsForceCompleted),
            ("isforcecompleted", true) => queryable.OrderByDescending(m => m.IsForceCompleted),
            ("lengthstatus", false) => queryable.OrderBy(m => m.ProductionBatch.LengthStatus ?? ""),
            ("lengthstatus", true) => queryable.OrderByDescending(m => m.ProductionBatch.LengthStatus ?? ""),
            ("salesman", false) => queryable.OrderBy(m => m.ProductionBatch.Salesman ?? ""),
            ("salesman", true) => queryable.OrderByDescending(m => m.ProductionBatch.Salesman ?? ""),
            ("deliverystate", false) => queryable.OrderBy(m => m.ProductionBatch.DeliveryState ?? ""),
            ("deliverystate", true) => queryable.OrderByDescending(m => m.ProductionBatch.DeliveryState ?? ""),
            ("manufacturingsstatus", false) => queryable.OrderBy(m => m.ProductionBatch.ManufacturingStatus ?? ""),
            ("manufacturingsstatus", true) => queryable.OrderByDescending(m => m.ProductionBatch.ManufacturingStatus ?? ""),
            ("inspectiontype", false) => queryable.OrderBy(m => m.InspectionType ?? ""),
            ("inspectiontype", true) => queryable.OrderByDescending(m => m.InspectionType ?? ""),
            ("isdeliverystatus", false) => queryable.OrderBy(m => m.ProductionBatch.ManufacturingStatus != null && m.ProductionBatch.ManufacturingStatus == m.ProductionBatch.DeliveryState),
            ("isdeliverystatus", true) => queryable.OrderByDescending(m => m.ProductionBatch.ManufacturingStatus != null && m.ProductionBatch.ManufacturingStatus == m.ProductionBatch.DeliveryState),
            ("processname", false) => queryable.OrderBy(m => m.ProcessName ?? ""),
            ("processname", true) => queryable.OrderByDescending(m => m.ProcessName ?? ""),
            ("sequencenumber", false) => queryable.OrderBy(m => m.SequenceNumber),
            ("sequencenumber", true) => queryable.OrderByDescending(m => m.SequenceNumber),
            _ => query.IsDescending
                ? queryable.OrderByDescending(m => m.CreatedTime)
                : queryable.OrderBy(m => m.CreatedTime)
        };

        var rawItems = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(m => new
            {
                m.Id, m.ProductionBatchId, m.ReceiveDate, m.Shift, m.Checker,
                m.Remark, m.DataSource,
                m.BatchNo, m.IsForceCompleted,
                m.ProcessGroupId, m.ProcessName, m.SequenceNumber, m.InspectionType,
                m.CreatedTime, m.UpdatedTime,
                // 通过 ProductionBatch 导航属性获取批次冗余字段
                ManufacturingItem = m.ProductionBatch.ManufacturingItem,
                TagNo = m.ProductionBatch.TagNo,
                WorkOrderNo = m.ProductionBatch.WorkOrderNo,
                SalesOrderNo = m.ProductionBatch.SalesOrderNo,
                ProductionMainNo = m.ProductionBatch.ProductionMainNo,
                SourceUnit = m.ProductionBatch.SourceName,
                FurnaceNo = m.ProductionBatch.SourceHeatNo,
                PlantGrade = m.ProductionBatch.PlantGrade,
                Specification = m.ProductionBatch.Specification,
                ProductionType = m.ProductionBatch.ProductionType,
                LengthStatus = m.ProductionBatch.LengthStatus,
                Salesman = m.ProductionBatch.Salesman,
                DeliveryState = m.ProductionBatch.DeliveryState,
                ManufacturingStatus = m.ProductionBatch.ManufacturingStatus
            })
            .ToListAsync();

        var items = rawItems.Select(m => new MaterialReceiveCheckDto
        {
            Id = m.Id,
            ProductionBatchId = m.ProductionBatchId,
            ReceiveDate = m.ReceiveDate,
            Shift = m.Shift,
            Checker = m.Checker,
            Remark = m.Remark,
            DataSource = m.DataSource,
            BatchNo = m.BatchNo!,
            IsForceCompleted = m.IsForceCompleted,
            ProcessGroupId = m.ProcessGroupId,
            ProcessName = m.ProcessName,
            SequenceNumber = m.SequenceNumber,
            InspectionType = EnumHelper.TryParse<MES.Core.Enums.InspectionType>(m.InspectionType),
            ManufacturingItem = ParseMaterialType(m.ManufacturingItem),
            TagNo = m.TagNo,
            WorkOrderNo = m.WorkOrderNo,
            SalesOrderNo = m.SalesOrderNo,
            ProductionMainNo = m.ProductionMainNo,
            SourceUnit = m.SourceUnit,
            FurnaceNo = m.FurnaceNo,
            PlantGrade = m.PlantGrade!,
            Specification = m.Specification!,
            ProductionType = EnumHelper.TryParse<ProductionType>(m.ProductionType),
            LengthStatus = EnumHelper.TryParse<LengthStatus>(m.LengthStatus),
            Salesman = m.Salesman,
            DeliveryState = EnumHelper.TryParse<DeliveryState>(m.DeliveryState),
            RawDeliveryState = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(m.DeliveryState),
            ManufacturingStatus = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(m.ManufacturingStatus),
            CreatedTime = m.CreatedTime,
            UpdatedTime = m.UpdatedTime
        }).ToList();

        // 判断每个记录是否该批次中 Inspection 值最高的检验工序组（交货状态仅最后检验有效）
        var batchIds4P = rawItems.Select(m => m.ProductionBatchId).Distinct().ToList();
        var pgs4P = await _context.Set<ProcessGroup>()
            .Where(pg => batchIds4P.Contains(pg.ProductionBatchId) && pg.Inspection.HasValue)
            .Select(pg => new { pg.Id, pg.ProductionBatchId, pg.Inspection })
            .ToListAsync();
        var pgInspLookup = pgs4P.ToDictionary(pg => pg.Id, pg => pg.Inspection!.Value);
        var maxInspByBatch = pgs4P
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.Max(pg => pg.Inspection!.Value));
        foreach (var item in items)
        {
            item.IsLastProcessGroup = pgInspLookup.TryGetValue(item.ProcessGroupId, out var inspVal)
                && maxInspByBatch.TryGetValue(item.ProductionBatchId, out var maxInsp)
                && inspVal == maxInsp;
            // 非最后工序组的成检到料，交货状态无效
            if (!item.IsLastProcessGroup)
                item.DeliveryState = null;

            // 实时校验（按当前工艺卡比对，工艺卡变更后可及时提示）：
            // 情况2：关联工序组不存在或已非检验工序组（Inspection 被清空）
            if (!pgInspLookup.ContainsKey(item.ProcessGroupId))
            {
                item.HealthIssue = "工序组非检验";
            }
            // 情况1：实时判定的成检类型与存储值不一致
            else
            {
                var realType = item.IsLastProcessGroup
                    ? nameof(InspectionType.FormalInspection)
                    : nameof(InspectionType.PreInspection);
                if (!string.Equals(item.InspectionType?.ToString(), realType, StringComparison.OrdinalIgnoreCase))
                    item.HealthIssue = "成检类型过期";
            }
        }

        return new PagedResult<MaterialReceiveCheckDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    /// <summary>
    /// 列表通用过滤（Keyword/日期/自定义筛选），分页查询与健康统计共用
    /// </summary>
    private IQueryable<MaterialReceiveCheck> ApplyListQueryFilters(IQueryable<MaterialReceiveCheck> queryable, QueryParams query)
    {
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(m =>
                (m.BatchNo != null && m.BatchNo.Contains(kw)) ||
                (m.Checker != null && m.Checker.Contains(kw)) ||
                (m.Remark != null && m.Remark.Contains(kw)) ||
                (m.ProductionBatch.PlantGrade != null && m.ProductionBatch.PlantGrade.Contains(kw)) ||
                (m.ProductionBatch.Specification != null && m.ProductionBatch.Specification.Contains(kw)) ||
                (m.ProductionBatch.WorkOrderNo != null && m.ProductionBatch.WorkOrderNo.Contains(kw)) ||
                (m.ProductionBatch.SalesOrderNo != null && m.ProductionBatch.SalesOrderNo.Contains(kw)) ||
                (m.ProductionBatch.ProductionMainNo != null && m.ProductionBatch.ProductionMainNo.Contains(kw)) ||
                (m.ProductionBatch.SourceHeatNo != null && m.ProductionBatch.SourceHeatNo.Contains(kw)) ||
                (m.ProductionBatch.TagNo != null && m.ProductionBatch.TagNo.Contains(kw)) ||
                (m.ProductionBatch.SourceName != null && m.ProductionBatch.SourceName.Contains(kw)) ||
                (m.ProductionBatch.Salesman != null && m.ProductionBatch.Salesman.Contains(kw)) ||
                (m.ProcessName != null && m.ProcessName.Contains(kw)));
        }

        if (query.ReceiveDateFrom.HasValue)
            queryable = queryable.Where(m => m.ReceiveDate >= query.ReceiveDateFrom.Value);

        if (query.ReceiveDateTo.HasValue)
            queryable = queryable.Where(m => m.ReceiveDate <= query.ReceiveDateTo.Value);

        // 自定义筛选：批量派生字段不在实体上，需通过 ProductionBatch 导航属性处理
        if (query.Filters != null && query.Filters.Count > 0)
        {
            var remainingFilters = new List<FilterDescriptor>();
            foreach (var filter in query.Filters)
            {
                if (filter.Operator != "in" || filter.Values == null || filter.Values.Count == 0)
                {
                    remainingFilters.Add(filter);
                    continue;
                }
                switch (filter.Field)
                {
                    case "ManufacturingItem":
                        queryable = queryable.Where(m => ExpandManufacturingItemFilter(filter.Values).Contains(m.ProductionBatch.ManufacturingItem));
                        break;
                    case "PlantGrade":
                        queryable = queryable.Where(m => filter.Values.Contains(m.ProductionBatch.PlantGrade));
                        break;
                    case "Specification":
                        queryable = queryable.Where(m => filter.Values.Contains(m.ProductionBatch.Specification));
                        break;
                    case "TagNo":
                        queryable = queryable.Where(m => m.ProductionBatch.TagNo != null && filter.Values.Contains(m.ProductionBatch.TagNo));
                        break;
                    case "WorkOrderNo":
                        queryable = queryable.Where(m => filter.Values.Contains(m.ProductionBatch.WorkOrderNo));
                        break;
                    case "SalesOrderNo":
                        queryable = queryable.Where(m => filter.Values.Contains(m.ProductionBatch.SalesOrderNo));
                        break;
                    case "ProductionMainNo":
                        queryable = queryable.Where(m => m.ProductionBatch.ProductionMainNo != null && filter.Values.Contains(m.ProductionBatch.ProductionMainNo));
                        break;
                    case "FurnaceNo":
                        queryable = queryable.Where(m => m.ProductionBatch.SourceHeatNo != null && filter.Values.Contains(m.ProductionBatch.SourceHeatNo));
                        break;
                    case "SourceUnit":
                        queryable = queryable.Where(m => m.ProductionBatch.SourceName != null && filter.Values.Contains(m.ProductionBatch.SourceName));
                        break;
                    case "ProductionType":
                        queryable = queryable.Where(m => filter.Values.Contains(m.ProductionBatch!.ProductionType!));
                        break;
                    case "LengthStatus":
                        queryable = queryable.Where(m => filter.Values.Contains(m.ProductionBatch.LengthStatus));
                        break;
                    case "Salesman":
                        queryable = queryable.Where(m => m.ProductionBatch.Salesman != null && filter.Values.Contains(m.ProductionBatch.Salesman));
                        break;
                    case "DeliveryState":
                        queryable = queryable.Where(m => filter.Values.Contains(m.ProductionBatch.DeliveryState));
                        break;
                    case "ManufacturingStatus":
                        queryable = queryable.Where(m => m.ProductionBatch.ManufacturingStatus != null && filter.Values.Contains(m.ProductionBatch.ManufacturingStatus));
                        break;
                    case "InspectionType":
                        queryable = queryable.Where(m => m.InspectionType != null && filter.Values.Contains(m.InspectionType));
                        break;
                    case "IsDeliveryStatus":
                    {
                        // 交付态为派生字段（批次制造状态==交货状态），DB 层用原始字段比较
                        var wantYes = filter.Values.Contains("是");
                        var wantNo = filter.Values.Contains("否");
                        if (wantYes && wantNo)
                        {
                            // 是+否 = 全选，不过滤
                        }
                        else if (wantYes)
                        {
                            queryable = queryable.Where(m => m.ProductionBatch.ManufacturingStatus != null
                                && m.ProductionBatch.ManufacturingStatus == m.ProductionBatch.DeliveryState);
                        }
                        else if (wantNo)
                        {
                            queryable = queryable.Where(m => m.ProductionBatch.ManufacturingStatus == null
                                || m.ProductionBatch.ManufacturingStatus != m.ProductionBatch.DeliveryState);
                        }
                        break;
                    }
                    default:
                        remainingFilters.Add(filter);
                        break;
                }
            }
            query.Filters = remainingFilters;
        }

        return queryable.ApplyFilters(query.Filters);
    }

    /// <summary>
    /// 实时健康汇总（按当前筛选条件全量统计成检类型过期/工序组非检验数量）
    /// </summary>
    public async Task<MaterialCheckHealthSummaryDto> GetMaterialCheckHealthSummaryAsync(QueryParams query)
    {
        var queryable = ApplyListQueryFilters(_context.MaterialReceiveChecks.AsNoTracking().AsQueryable(), query);

        var raw = await queryable
            .Select(m => new { m.ProductionBatchId, m.ProcessGroupId, m.InspectionType, m.BatchNo })
            .ToListAsync();
        if (raw.Count == 0)
            return new MaterialCheckHealthSummaryDto { TotalCount = 0 };

        var batchIds = raw.Select(r => r.ProductionBatchId).Distinct().ToList();
        var pgs = await _context.Set<ProcessGroup>()
            .Where(pg => batchIds.Contains(pg.ProductionBatchId) && pg.Inspection.HasValue)
            .Select(pg => new { pg.Id, pg.ProductionBatchId, pg.Inspection })
            .ToListAsync();
        var pgInspLookup = pgs.ToDictionary(pg => pg.Id, pg => pg.Inspection!.Value);
        var maxInspByBatch = pgs
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.Max(pg => pg.Inspection!.Value));

        var expiredBatchNos = new List<string>();
        var nonInspBatchNos = new List<string>();
        foreach (var r in raw)
        {
            if (!pgInspLookup.TryGetValue(r.ProcessGroupId, out var insp))
            {
                nonInspBatchNos.Add(r.BatchNo ?? "");
                continue;
            }
            var isLast = insp == maxInspByBatch.GetValueOrDefault(r.ProductionBatchId);
            var realType = isLast ? nameof(InspectionType.FormalInspection) : nameof(InspectionType.PreInspection);
            if (!string.Equals(r.InspectionType, realType, StringComparison.OrdinalIgnoreCase))
                expiredBatchNos.Add(r.BatchNo ?? "");
        }

        return new MaterialCheckHealthSummaryDto
        {
            TotalCount = raw.Count,
            InspectionTypeExpiredBatchNos = expiredBatchNos,
            ProcessGroupNotInspectionBatchNos = nonInspBatchNos
        };
    }

    public async Task<List<MaterialReceiveCheckDto>> GetAllMaterialReceiveCheckListAsync()
    {
        var raw = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .OrderByDescending(rc => rc.Id)
            .Select(rc => new
            {
                rc.Id, rc.ProductionBatchId, rc.BatchNo, rc.DataSource,
                rc.IsForceCompleted, rc.ReceiveDate, rc.Shift, rc.Checker, rc.Remark,
                rc.ProcessGroupId, rc.ProcessName, rc.SequenceNumber, rc.InspectionType,
                rc.CreatedTime, rc.UpdatedTime,
                // 通过 ProductionBatch 导航属性
                ManufacturingItem = rc.ProductionBatch.ManufacturingItem,
                TagNo = rc.ProductionBatch.TagNo,
                WorkOrderNo = rc.ProductionBatch.WorkOrderNo,
                SalesOrderNo = rc.ProductionBatch.SalesOrderNo,
                SourceUnit = rc.ProductionBatch.SourceName,
                FurnaceNo = rc.ProductionBatch.SourceHeatNo,
                PlantGrade = rc.ProductionBatch.PlantGrade,
                Specification = rc.ProductionBatch.Specification,
                ProductionType = rc.ProductionBatch.ProductionType,
                LengthStatus = rc.ProductionBatch.LengthStatus,
                Salesman = rc.ProductionBatch.Salesman,
                DeliveryState = rc.ProductionBatch.DeliveryState,
                ManufacturingStatus = rc.ProductionBatch.ManufacturingStatus
            })
            .ToListAsync();

        var resultList = raw.Select(rc => new MaterialReceiveCheckDto
        {
            Id = rc.Id,
            ProductionBatchId = rc.ProductionBatchId,
            BatchNo = rc.BatchNo!,
            DataSource = rc.DataSource,
            IsForceCompleted = rc.IsForceCompleted,
            ProcessGroupId = rc.ProcessGroupId,
            ProcessName = rc.ProcessName,
            SequenceNumber = rc.SequenceNumber,
            InspectionType = EnumHelper.TryParse<MES.Core.Enums.InspectionType>(rc.InspectionType),
            ManufacturingItem = ParseMaterialType(rc.ManufacturingItem),
            TagNo = rc.TagNo,
            WorkOrderNo = rc.WorkOrderNo,
            SalesOrderNo = rc.SalesOrderNo,
            SourceUnit = rc.SourceUnit,
            FurnaceNo = rc.FurnaceNo,
            PlantGrade = rc.PlantGrade!,
            Specification = rc.Specification!,
            ProductionType = EnumHelper.TryParse<ProductionType>(rc.ProductionType),
            LengthStatus = EnumHelper.TryParse<LengthStatus>(rc.LengthStatus),
            Salesman = rc.Salesman,
            DeliveryState = EnumHelper.TryParse<DeliveryState>(rc.DeliveryState),
            RawDeliveryState = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(rc.DeliveryState),
            ManufacturingStatus = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(rc.ManufacturingStatus),
            ReceiveDate = rc.ReceiveDate,
            Shift = rc.Shift,
            Checker = rc.Checker,
            Remark = rc.Remark,
            CreatedTime = rc.CreatedTime,
            UpdatedTime = rc.UpdatedTime
        }).ToList();

        // 判断每个记录是否该批次中 Inspection 值最高的检验工序组
        var batchIds4All = raw.Select(m => m.ProductionBatchId).Distinct().ToList();
        var pgs4All = await _context.Set<ProcessGroup>()
            .Where(pg => batchIds4All.Contains(pg.ProductionBatchId) && pg.Inspection.HasValue)
            .Select(pg => new { pg.Id, pg.ProductionBatchId, pg.Inspection })
            .ToListAsync();
        var pgInspLookupAll = pgs4All.ToDictionary(pg => pg.Id, pg => pg.Inspection!.Value);
        var maxInspByBatchAll = pgs4All
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.Max(pg => pg.Inspection!.Value));
        foreach (var item in resultList)
        {
            item.IsLastProcessGroup = pgInspLookupAll.TryGetValue(item.ProcessGroupId, out var inspVal)
                && maxInspByBatchAll.TryGetValue(item.ProductionBatchId, out var maxInsp)
                && inspVal == maxInsp;
            // 非最后工序组的成检到料，交货状态无效
            if (!item.IsLastProcessGroup)
                item.DeliveryState = null;
        }

        return resultList;
    }

    public async Task<List<PendingMaterialCheckDto>> GetPendingMaterialChecksAsync()
    {
        // ====== 两段式查询：先取批次，再取工序组，内存匹配 ======
        // 匹配规则：
        //   ① batch.NextSectionName == "检验" 或为 null（覆盖成检到料删除后回退场景）
        //   ② ProcessGroup.ManufacturingSpec == ProductionBatch.Specification

        // Step 1: 获取已有成检到料的工序组 ID（按工序组排除，允许同一批次多个工序组分别到料）
        var existingPgIds = await _context.MaterialReceiveChecks
            .Select(m => m.ProcessGroupId)
            .ToListAsync();
        var existingPgSet = new HashSet<int>(existingPgIds);

        // Step 2: 获取下一工段为"检验" 或 尚未开始（已重置）的活跃批次
        // 放宽 NextSectionName 条件，覆盖成检到料删除后 NextSectionName 被回退为 null 的情况
        // 仅成品类制造物品才有"成品检验"环节，非成品类（余库料等）的检验属"过程检验"，不入本面板
        var finishedItems = new[] { nameof(MaterialType.OrderFinished), nameof(MaterialType.Finished), nameof(MaterialType.CriticalFinished), nameof(MaterialType.SpecialDeliveryStatus) };
        var batches = await _context.ProductionBatches.AsNoTracking()
            .Where(b => (b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress
                      || b.Status == BatchStatus.InFinalInspection)
                     && finishedItems.Contains(b.ManufacturingItem)
                     && (b.NextSectionName == SectionKeys.Inspection || b.NextSectionName == null))
            .Select(b => new
            {
                b.Id, b.BatchNo, b.WorkOrderNo, b.Salesman, b.TagNo,
                b.PlantGrade, b.Specification, b.CurrentValidWeight,
                b.CurrentExecDate, b.CurrentSectionName, b.NextProcess
            })
            .ToListAsync();

        if (batches.Count == 0) return new();

        // Step 3: 获取这些批次的 ProcessGroup 数据
        var batchSpecLookup = batches.ToDictionary(b => b.Id, b => b.Specification ?? "");
        var batchNextProcessLookup = batches.ToDictionary(b => b.Id, b => b.NextProcess ?? "");
        var batchIds = batches.Select(b => b.Id).ToList();
        var processGroups = await _context.Set<ProcessGroup>().AsNoTracking()
            .Where(pg => batchIds.Contains(pg.ProductionBatchId)
                      && pg.ManufacturingSpec != null
                      && pg.Inspection.HasValue)
            .Select(pg => new
            {
                pg.ProductionBatchId, pg.Id, pg.ProcessName,
                pg.ManufacturingSpec, pg.SequenceNumber
            })
            .ToListAsync();

        // Step 4: 匹配工艺规格 + 下一工序组名，定位尚未到料的检验工序组
        //   pg.ProcessName == batch.NextProcess 精准定位下一工序组（NextProcess 为 null 时只按规格匹配）
        //   pg.ManufacturingSpec == batch.Specification 确保规格匹配
        //   已存在成检到料的工序组排除（允许同一批次多个检验工序组分别到料）
        var pendingMap = processGroups
            .Where(pg =>
            {
                if (existingPgSet.Contains(pg.Id)) return false;
                var spec = batchSpecLookup.GetValueOrDefault(pg.ProductionBatchId, "");
                var nextProcess = batchNextProcessLookup.GetValueOrDefault(pg.ProductionBatchId, "");
                if (!string.Equals(pg.ManufacturingSpec, spec, StringComparison.OrdinalIgnoreCase))
                    return false;
                return string.IsNullOrEmpty(nextProcess)
                    || string.Equals(pg.ProcessName, nextProcess, StringComparison.OrdinalIgnoreCase);
            })
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.First());

        // Step 5: 构建结果（取有匹配且未到料工序组的批次）
        var result = batches
            .Where(b => pendingMap.ContainsKey(b.Id))
            .OrderByDescending(b => b.CurrentValidWeight ?? 0)
            .Select(b =>
            {
                var pg = pendingMap[b.Id];
                return new PendingMaterialCheckDto
                {
                    BatchId = b.Id, BatchNo = b.BatchNo,
                    WorkOrderNo = b.WorkOrderNo, Salesman = b.Salesman,
                    TagNo = b.TagNo, PlantGrade = b.PlantGrade,
                    Specification = b.Specification,
                    CurrentValidWeight = b.CurrentValidWeight ?? 0,
                    CurrentExecDate = b.CurrentExecDate,
                    CurrentSectionName = b.CurrentSectionName,
                    ProcessGroupId = pg.Id,
                    ProcessGroupName = pg.ProcessName
                };
            })
            .ToList();

        return result;
    }


    // 需要从数据库 DISTINCT 查询的列（枚举/布尔由前端 EnumOptions 处理）
    // 字段通过 m.ProductionBatch 导航属性获取 ProductionBatch 冗余值
    private static readonly string[] _stringFilterColumns = new[]
    {
        "BatchNo", "PlantGrade", "Specification", "Checker",
        "TagNo", "WorkOrderNo", "SalesOrderNo", "ProductionMainNo", "FurnaceNo", "SourceUnit",
        "Remark", "Salesman", "ProcessName"
    };

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.MaterialReceiveCheckFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            var dict = new Dictionary<string, List<string>>();

            // 逐个列 SELECT DISTINCT（各列简单查询，SQL Server 可为每列优化访问路径）
            foreach (var col in _stringFilterColumns)
            {
                var query = ApplyFilterColumnDistinct(col);
                if (query != null)
                    dict[col] = await query.ToListAsync();
            }

            // ReceiveDate 格式化为字符串
            var dates = await _context.MaterialReceiveChecks
                .Select(m => m.ReceiveDate).Distinct().ToListAsync();
            dict["ReceiveDate"] = dates.Select(d => d.ToString("yyyy-MM-dd"))
                .OrderBy(x => x).ToList();

            return dict;
        }) ?? new Dictionary<string, List<string>>();
    }

    private IQueryable<string>? ApplyFilterColumnDistinct(string column)
    {
        var queryable = _context.MaterialReceiveChecks.AsNoTracking();
        return column switch
        {
            "BatchNo" => queryable.Where(m => m.BatchNo != null).Select(m => m.BatchNo!).Distinct().OrderBy(x => x),
            "PlantGrade" => queryable.Where(m => m.ProductionBatch.PlantGrade != null).Select(m => m.ProductionBatch.PlantGrade!).Distinct().OrderBy(x => x),
            "Specification" => queryable.Where(m => m.ProductionBatch.Specification != null).Select(m => m.ProductionBatch.Specification!).Distinct().OrderBy(x => x),
            "Checker" => queryable.Where(m => m.Checker != null).Select(m => m.Checker!).Distinct().OrderBy(x => x),
            "TagNo" => queryable.Where(m => m.ProductionBatch.TagNo != null).Select(m => m.ProductionBatch.TagNo!).Distinct().OrderBy(x => x),
            "WorkOrderNo" => queryable.Where(m => m.ProductionBatch.WorkOrderNo != null).Select(m => m.ProductionBatch.WorkOrderNo!).Distinct().OrderBy(x => x),
            "SalesOrderNo" => queryable.Where(m => m.ProductionBatch.SalesOrderNo != null).Select(m => m.ProductionBatch.SalesOrderNo!).Distinct().OrderBy(x => x),
            "ProductionMainNo" => queryable.Where(m => m.ProductionBatch.ProductionMainNo != null).Select(m => m.ProductionBatch.ProductionMainNo!).Distinct().OrderBy(x => x),
            "FurnaceNo" => queryable.Where(m => m.ProductionBatch.SourceHeatNo != null).Select(m => m.ProductionBatch.SourceHeatNo!).Distinct().OrderBy(x => x),
            "SourceUnit" => queryable.Where(m => m.ProductionBatch.SourceName != null).Select(m => m.ProductionBatch.SourceName!).Distinct().OrderBy(x => x),
            "Remark" => queryable.Where(m => m.Remark != null).Select(m => m.Remark!).Distinct().OrderBy(x => x),
            "Salesman" => queryable.Where(m => m.ProductionBatch.Salesman != null).Select(m => m.ProductionBatch.Salesman!).Distinct().OrderBy(x => x),
            "ProcessName" => queryable.Where(m => m.ProcessName != null).Select(m => m.ProcessName!).Distinct().OrderBy(x => x),
            _ => null
        };
    }

    public async Task<byte[]> PrintMaterialCheckBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var raw = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .Select(m => new
            {
                m.Id, m.ProductionBatchId, m.ReceiveDate, m.Shift, m.Checker,
                m.Remark, m.DataSource,
                m.BatchNo, m.IsForceCompleted,
                m.ProcessGroupId, m.ProcessName, m.SequenceNumber, m.InspectionType,
                m.CreatedTime, m.UpdatedTime,
                // 通过 ProductionBatch 导航属性
                ManufacturingItem = m.ProductionBatch.ManufacturingItem,
                TagNo = m.ProductionBatch.TagNo,
                WorkOrderNo = m.ProductionBatch.WorkOrderNo,
                SalesOrderNo = m.ProductionBatch.SalesOrderNo,
                ProductionMainNo = m.ProductionBatch.ProductionMainNo,
                SourceUnit = m.ProductionBatch.SourceName,
                FurnaceNo = m.ProductionBatch.SourceHeatNo,
                PlantGrade = m.ProductionBatch.PlantGrade,
                Specification = m.ProductionBatch.Specification,
                ProductionType = m.ProductionBatch.ProductionType,
                LengthStatus = m.ProductionBatch.LengthStatus,
                Salesman = m.ProductionBatch.Salesman,
                DeliveryState = m.ProductionBatch.DeliveryState,
                ManufacturingStatus = m.ProductionBatch.ManufacturingStatus
            })
            .ToListAsync();

        var items = raw.Select(m => new MaterialReceiveCheckDto
        {
            Id = m.Id,
            ProductionBatchId = m.ProductionBatchId,
            ReceiveDate = m.ReceiveDate,
            Shift = m.Shift,
            Checker = m.Checker,
            Remark = m.Remark,
            BatchNo = m.BatchNo!,
            IsForceCompleted = m.IsForceCompleted,
            DataSource = m.DataSource,
            ProcessGroupId = m.ProcessGroupId,
            ProcessName = m.ProcessName,
            SequenceNumber = m.SequenceNumber,
            InspectionType = EnumHelper.TryParse<MES.Core.Enums.InspectionType>(m.InspectionType),
            ManufacturingItem = ParseMaterialType(m.ManufacturingItem),
            TagNo = m.TagNo,
            WorkOrderNo = m.WorkOrderNo,
            SalesOrderNo = m.SalesOrderNo,
            ProductionMainNo = m.ProductionMainNo,
            SourceUnit = m.SourceUnit,
            FurnaceNo = m.FurnaceNo,
            PlantGrade = m.PlantGrade!,
            Specification = m.Specification!,
            ProductionType = EnumHelper.TryParse<ProductionType>(m.ProductionType),
            LengthStatus = EnumHelper.TryParse<LengthStatus>(m.LengthStatus),
            Salesman = m.Salesman,
            DeliveryState = EnumHelper.TryParse<DeliveryState>(m.DeliveryState),
            RawDeliveryState = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(m.DeliveryState),
            ManufacturingStatus = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(m.ManufacturingStatus),
            CreatedTime = m.CreatedTime,
            UpdatedTime = m.UpdatedTime
        }).ToList();

        // 判定各记录是否该批次中 Inspection 值最高的检验工序组（交货状态仅最后检验有效）
        var batchIds4Print = raw.Select(m => m.ProductionBatchId).Distinct().ToList();
        var pgs4Print = await _context.Set<ProcessGroup>()
            .Where(pg => batchIds4Print.Contains(pg.ProductionBatchId) && pg.Inspection.HasValue)
            .Select(pg => new { pg.Id, pg.ProductionBatchId, pg.Inspection })
            .ToListAsync();
        var pgInspLookup4Print = pgs4Print.ToDictionary(pg => pg.Id, pg => pg.Inspection!.Value);
        var maxInspByBatch4Print = pgs4Print
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.Max(pg => pg.Inspection!.Value));
        foreach (var item in items)
        {
            item.IsLastProcessGroup = pgInspLookup4Print.TryGetValue(item.ProcessGroupId, out var insp)
                && maxInspByBatch4Print.TryGetValue(item.ProductionBatchId, out var maxInsp)
                && insp == maxInsp;
            if (!item.IsLastProcessGroup)
                item.DeliveryState = null;
        }

        return MaterialCheckPrintHelper.GenerateBatchPdf(items, columns);
    }

    /// <summary>
    /// 扩展制造物品筛选值，兼容历史数据中的非标准值
    /// </summary>
    private static HashSet<string> ExpandManufacturingItemFilter(List<string> values)
    {
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in values)
        {
            expanded.Add(v);
            switch (v)
            {
                case InventoryMaterialTypes.OrderFinished:
                    expanded.Add("OrderFinishedProduct");
                    break;
                case InventoryMaterialTypes.Finished:
                    expanded.Add("PreparedMaterial");
                    expanded.Add("PreparedFinished");
                    expanded.Add("StockFinished");
                    break;
                case InventoryMaterialTypes.Surplus:
                    expanded.Add("SurplusStock");
                    break;
                case InventoryMaterialTypes.SemiFinished:
                    expanded.Add("IntermediateProduct");
                    break;
            }
        }
        return expanded;
    }
}
