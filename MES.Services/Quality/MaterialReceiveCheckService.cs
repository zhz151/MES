using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Services.Extensions;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Quality;

/// <summary>
/// 检验到料（成检到料）服务实现
/// </summary>
public class MaterialReceiveCheckService : IMaterialReceiveCheckService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MaterialReceiveCheckService> _logger;
    private readonly IConfigParameterService _configService;
    private readonly IQualityProcessTrackingService _qualityProcessTracking;
    private readonly IMemoryCache _cache;
    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();

    public MaterialReceiveCheckService(
        AppDbContext context,
        IConfigParameterService configService,
        IQualityProcessTrackingService qualityProcessTracking,
        ILogger<MaterialReceiveCheckService> logger,
        IMemoryCache cache)
    {
        _context = context;
        _configService = configService;
        _qualityProcessTracking = qualityProcessTracking;
        _logger = logger;
        _cache = cache;
    }

    private void TryRefreshQualityProcessTrackingAsync(int mrCheckId)
    {
        try
        {
            _ = _qualityProcessTracking.RefreshByMrCheckIdAsync(mrCheckId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "质量过程跟踪刷新失败（不影响主流程）: MrCheckId={MrCheckId}", mrCheckId);
        }
    }

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        if (!_configMaps.TryGetValue(category, out var map))
        {
            map = await _configService.GetConfigMapAsync(category);
            _configMaps[category] = map;
        }
        return map.GetValueOrDefault(key, defaultValue);
    }

    /// <summary>
    /// 从工序组中提取所有非空工段及其顺序值
    /// </summary>
    private static List<(string SectionName, int Sequence)> GetSectionsFromProcessGroup(ProcessGroup pg)
        => pg.GetNonEmptySections();

    private void ComputeMaterialCheckQuantities(ProductionBatch batch, MaterialReceiveCheck entity, decimal groupDiscountRate)
    {
        // 库存/外购/返整/委外加工 → 现有效原料支数/重量
        // 荒管生产/在制生产/对外加工 → 切管产记录汇总 / 目标重量
        var isStockType = batch.ProductionType == "Inventory"
            || batch.ProductionType == "OutsourcedPurchased"
            || batch.ProductionType == "Rework"
            || batch.ProductionType == "Subcontract";

        if (isStockType)
        {
            entity.ProductionCutQuantity = batch.CurrentValidQty ?? 0;
            entity.ProductionWeight = batch.CurrentValidWeight;
        }
        else
        {
            // 生产支数：切管工序已完工产记录汇总
            entity.ProductionCutQuantity = _context.ProductionRecords
                .Where(pr => pr.ProductionBatchId == batch.Id && pr.SectionName == SectionDefs.Cut && pr.IsFinished)
                .Sum(pr => (int?)(pr.PostCutQuantity ?? 0)) ?? 0;

            // 目标重量 = 投料重量 × (1 - 有效工序组数 × 0.025)
            if (batch.CurrentValidWeight == null)
            {
                entity.ProductionWeight = null;
            }
            else
            {
                var effectiveGroupCount = batch.ProcessGroups
                    .Count(pg => GetSectionsFromProcessGroup(pg).Count > 0);
                var discount = 1.0m - effectiveGroupCount * groupDiscountRate;
                if (discount < 0) discount = 0;
                entity.ProductionWeight = (int?)(batch.CurrentValidWeight.Value * discount);
            }
        }
    }

    public async Task<MaterialReceiveCheckDto?> GetMaterialReceiveCheckAsync(int batchId)
    {
        return await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(m => m.ProductionBatchId == batchId)
            .Select(m => new MaterialReceiveCheckDto
            {
                Id = m.Id,
                ProductionBatchId = m.ProductionBatchId,
                ReceiveDate = m.ReceiveDate,
                Shift = m.Shift,
                Checker = m.Checker,
                Remark = m.Remark,
                BatchNo = m.BatchNo!,
                ManufacturingItem = m.ManufacturingItem,
                TagNo = m.TagNo,
                WorkOrderNo = m.WorkOrderNo,
                SalesOrderNo = m.SalesOrderNo,
                SourceUnit = m.SourceUnit,
                FurnaceNo = m.FurnaceNo,
                PlantGrade = m.PlantGrade,
                Specification = m.Specification,
                ProductionType = m.ProductionType,
                IsForceCompleted = m.IsForceCompleted,
                DataSource = m.DataSource,
                Salesman = m.Salesman,
                DeliveryState = m.DeliveryState,
                CreatedTime = m.CreatedTime,
                UpdatedTime = m.UpdatedTime
            })
            .FirstOrDefaultAsync();
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
            .Include(b => b.ProcessGroups)
            .FirstOrDefaultAsync(b => b.Id == request.ProductionBatchId)
            ?? throw new BusinessException($"批次不存在: {request.ProductionBatchId}");

        // 检查是否已存在检验到料记录
        var exists = await _context.MaterialReceiveChecks
            .AnyAsync(m => m.ProductionBatchId == request.ProductionBatchId);
        if (exists)
            throw new BusinessException("该批次已完成成检到料，不能重复创建");

        var entity = new MaterialReceiveCheck
        {
            ProductionBatchId = request.ProductionBatchId,
            ReceiveDate = request.ReceiveDate,
            Shift = request.Shift,
            Checker = request.Checker,
            Remark = request.Remark,
            DataSource = request.DataSource ?? "MANUAL",
            // 从 ProductionBatch 复制冗余字段
            BatchNo = batch.BatchNo,
            ManufacturingItem = batch.ManufacturingItem,
            TagNo = batch.TagNo,
            WorkOrderNo = batch.WorkOrderNo,
            SalesOrderNo = batch.SalesOrderNo,
            SourceUnit = batch.SourceName,
            FurnaceNo = batch.SourceHeatNo,
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            ProductionType = batch.ProductionType,
            LengthStatus = batch.LengthStatus,
            IsForceCompleted = false,
            Salesman = batch.Salesman,
            DeliveryState = batch.DeliveryState
        };

        // 计算生产支数/生产重量（创建时快照）
        var groupDiscountRate = await GetConfigAsync("ProcessingDiscount", "GroupDiscountRate", 0.025m);
        ComputeMaterialCheckQuantities(batch, entity, groupDiscountRate);

        _context.MaterialReceiveChecks.Add(entity);

        // 批次设为完成
        batch.Status = BatchStatus.Completed;
        _context.ProductionBatches.Update(batch);

        await _context.SaveChangesAsync();

        TryRefreshQualityProcessTrackingAsync(entity.Id);

        return new MaterialReceiveCheckDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            ReceiveDate = entity.ReceiveDate,
            Shift = entity.Shift,
            Checker = entity.Checker,
            Remark = entity.Remark,
            BatchNo = entity.BatchNo,
            ManufacturingItem = entity.ManufacturingItem,
            TagNo = entity.TagNo,
            WorkOrderNo = entity.WorkOrderNo,
            SalesOrderNo = entity.SalesOrderNo,
            SourceUnit = entity.SourceUnit,
            FurnaceNo = entity.FurnaceNo,
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
            ProductionType = entity.ProductionType,
            LengthStatus = entity.LengthStatus,
            ProductionWeight = entity.ProductionWeight,
            IsForceCompleted = entity.IsForceCompleted,
            DataSource = entity.DataSource,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task<List<MaterialReceiveCheckDto>> BatchCreateMaterialReceiveChecksAsync(List<CreateMaterialReceiveCheckRequest> requests)
    {
        if (requests.Count == 0)
            return new List<MaterialReceiveCheckDto>();

        // 预加载所有涉及的批次
        var batchNos = requests.Where(r => r.ProductionBatchId <= 0 && !string.IsNullOrWhiteSpace(r.BatchNo))
            .Select(r => r.BatchNo).Distinct().ToList();
        var batchLookup = batchNos.Count > 0
            ? await _context.ProductionBatches.Include(b => b.ProcessGroups).Where(b => batchNos.Contains(b.BatchNo)).ToDictionaryAsync(b => b.BatchNo)
            : new Dictionary<string, ProductionBatch>();

        // 检查所有批次是否存在
        var modifiedBatches = new List<ProductionBatch>();
        var existingCheckBatchIds = new HashSet<int>();

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
                    .Include(b => b.ProcessGroups)
                    .FirstOrDefaultAsync(b => b.Id == request.ProductionBatchId)
                    ?? throw new BusinessException($"批次不存在: {request.ProductionBatchId}");
                modifiedBatches.Add(batch);
            }

            // 延迟批量检查重复（先收集IDs）
            existingCheckBatchIds.Add(request.ProductionBatchId);
        }

        // 一次查出已存在检验到料的批次ID
        var existingBatchIds = await _context.MaterialReceiveChecks
            .Where(m => existingCheckBatchIds.Contains(m.ProductionBatchId))
            .Select(m => m.ProductionBatchId)
            .ToListAsync();

        if (existingBatchIds.Count > 0)
        {
            var dupBatchNos = modifiedBatches
                .Where(b => existingBatchIds.Contains(b.Id))
                .Select(b => b.BatchNo);
            throw new BusinessException($"批次 \"{string.Join(", ", dupBatchNos)}\" 已完成成检到料，不能重复创建");
        }

        var entities = new List<MaterialReceiveCheck>();
        foreach (var request in requests)
        {
            var batch = modifiedBatches[entities.Count];
            entities.Add(new MaterialReceiveCheck
            {
                ProductionBatchId = request.ProductionBatchId,
                ReceiveDate = request.ReceiveDate,
                Shift = request.Shift,
                Checker = request.Checker,
                Remark = request.Remark,
                DataSource = "MANUAL",
                // 从 ProductionBatch 复制冗余字段
                BatchNo = batch.BatchNo,
                ManufacturingItem = batch.ManufacturingItem,
                TagNo = batch.TagNo,
                WorkOrderNo = batch.WorkOrderNo,
                SalesOrderNo = batch.SalesOrderNo,
                SourceUnit = batch.SourceName,
                FurnaceNo = batch.SourceHeatNo,
                PlantGrade = batch.PlantGrade,
                Specification = batch.Specification,
                ProductionType = batch.ProductionType,
                LengthStatus = batch.LengthStatus,
                IsForceCompleted = false
            });
            // 计算生产支数/生产重量（创建时快照）
            var grpDiscount = await GetConfigAsync("ProcessingDiscount", "GroupDiscountRate", 0.025m);
            ComputeMaterialCheckQuantities(batch, entities[^1], grpDiscount);
        }

        foreach (var batch in modifiedBatches)
            batch.Status = BatchStatus.Completed;

        _context.MaterialReceiveChecks.AddRange(entities);
        _context.ProductionBatches.UpdateRange(modifiedBatches);
        await _context.SaveChangesAsync();

        return entities.Select(e => new MaterialReceiveCheckDto
        {
            Id = e.Id,
            ProductionBatchId = e.ProductionBatchId,
            ReceiveDate = e.ReceiveDate,
            Shift = e.Shift,
            Checker = e.Checker,
            Remark = e.Remark,
            BatchNo = e.BatchNo,
            ManufacturingItem = e.ManufacturingItem,
            TagNo = e.TagNo,
            WorkOrderNo = e.WorkOrderNo,
            SalesOrderNo = e.SalesOrderNo,
            SourceUnit = e.SourceUnit,
            FurnaceNo = e.FurnaceNo,
            PlantGrade = e.PlantGrade,
            Specification = e.Specification,
            ProductionType = e.ProductionType,
            LengthStatus = e.LengthStatus,
            ProductionWeight = e.ProductionWeight,
            IsForceCompleted = e.IsForceCompleted,
            Salesman = e.Salesman,
            DeliveryState = e.DeliveryState,
            DataSource = e.DataSource,
            CreatedTime = e.CreatedTime,
            UpdatedTime = e.UpdatedTime
        }).ToList();
    }

    public async Task<MaterialReceiveCheckDto> UpdateMaterialReceiveCheckAsync(int id, UpdateMaterialReceiveCheckRequest request)
    {
        var entity = await _context.MaterialReceiveChecks.FindAsync(id)
            ?? throw new BusinessException("成检到料记录不存在");

        if (request.ReceiveDate != default)
            entity.ReceiveDate = request.ReceiveDate;
        entity.Shift = request.Shift ?? entity.Shift;
        entity.Checker = request.Checker ?? entity.Checker;
        entity.Remark = request.Remark ?? entity.Remark;
        if (request.IsForceCompleted.HasValue)
            entity.IsForceCompleted = request.IsForceCompleted.Value;

        _context.MaterialReceiveChecks.Update(entity);
        await _context.SaveChangesAsync();

        TryRefreshQualityProcessTrackingAsync(entity.Id);

        return new MaterialReceiveCheckDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            ReceiveDate = entity.ReceiveDate,
            Shift = entity.Shift,
            Checker = entity.Checker,
            Remark = entity.Remark,
            BatchNo = entity.BatchNo,
            ManufacturingItem = entity.ManufacturingItem,
            TagNo = entity.TagNo,
            WorkOrderNo = entity.WorkOrderNo,
            SalesOrderNo = entity.SalesOrderNo,
            SourceUnit = entity.SourceUnit,
            FurnaceNo = entity.FurnaceNo,
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
            ProductionType = entity.ProductionType,
            IsForceCompleted = entity.IsForceCompleted,
            DataSource = entity.DataSource,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task DeleteMaterialReceiveCheckAsync(int id)
    {
        var entity = await _context.MaterialReceiveChecks.FindAsync(id)
            ?? throw new BusinessException("成检到料记录不存在");

        var batchId = entity.ProductionBatchId;
        _context.MaterialReceiveChecks.Remove(entity);

        // 重置批次状态为进行中
        var batch = await _context.ProductionBatches.FindAsync(batchId);
        if (batch != null)
        {
            batch.Status = BatchStatus.InProgress;
            _context.ProductionBatches.Update(batch);
        }

        await _context.SaveChangesAsync();

        // 删除物化行
        var existingQpt = await _context.QualityProcessTrackings
            .FirstOrDefaultAsync(q => q.MaterialReceiveCheckId == id);
        if (existingQpt != null)
        {
            _context.QualityProcessTrackings.Remove(existingQpt);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<PagedResult<MaterialReceiveCheckDto>> GetAllMaterialReceiveChecksAsync(QueryParams query)
    {
        var queryable = _context.MaterialReceiveChecks
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(m =>
                (m.BatchNo != null && m.BatchNo.Contains(kw)) ||
                (m.ManufacturingItem != null && m.ManufacturingItem.Contains(kw)) ||
                (m.PlantGrade != null && m.PlantGrade.Contains(kw)) ||
                (m.Specification != null && m.Specification.Contains(kw)) ||
                (m.Checker != null && m.Checker.Contains(kw)) ||
                (m.Shift != null && m.Shift.Contains(kw)) ||
                (m.WorkOrderNo != null && m.WorkOrderNo.Contains(kw)) ||
                (m.SalesOrderNo != null && m.SalesOrderNo.Contains(kw)) ||
                (m.FurnaceNo != null && m.FurnaceNo.Contains(kw)) ||
                (m.TagNo != null && m.TagNo.Contains(kw)) ||
                (m.SourceUnit != null && m.SourceUnit.Contains(kw)) ||
                (m.Remark != null && m.Remark.Contains(kw)) ||
                (m.Salesman != null && m.Salesman.Contains(kw)) ||
                (m.DeliveryState != null && m.DeliveryState.Contains(kw)));
        }

        if (query.ReceiveDateFrom.HasValue)
            queryable = queryable.Where(m => m.ReceiveDate >= query.ReceiveDateFrom.Value);

        if (query.ReceiveDateTo.HasValue)
            queryable = queryable.Where(m => m.ReceiveDate <= query.ReceiveDateTo.Value);

        queryable = queryable.ApplyFilters(query.Filters);

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
            ("shift", false) => queryable.OrderBy(m => m.Shift ?? ""),
            ("shift", true) => queryable.OrderByDescending(m => m.Shift ?? ""),
            ("remark", false) => queryable.OrderBy(m => m.Remark ?? ""),
            ("remark", true) => queryable.OrderByDescending(m => m.Remark ?? ""),
            ("manufacturingitem", false) => queryable.OrderBy(m => m.ManufacturingItem ?? ""),
            ("manufacturingitem", true) => queryable.OrderByDescending(m => m.ManufacturingItem ?? ""),
            ("plantgrade", false) => queryable.OrderBy(m => m.PlantGrade ?? ""),
            ("plantgrade", true) => queryable.OrderByDescending(m => m.PlantGrade ?? ""),
            ("specification", false) => queryable.OrderBy(m => m.Specification ?? ""),
            ("specification", true) => queryable.OrderByDescending(m => m.Specification ?? ""),
            ("tagno", false) => queryable.OrderBy(m => m.TagNo ?? ""),
            ("tagno", true) => queryable.OrderByDescending(m => m.TagNo ?? ""),
            ("workorderno", false) => queryable.OrderBy(m => m.WorkOrderNo ?? ""),
            ("workorderno", true) => queryable.OrderByDescending(m => m.WorkOrderNo ?? ""),
            ("salesorderno", false) => queryable.OrderBy(m => m.SalesOrderNo ?? ""),
            ("salesorderno", true) => queryable.OrderByDescending(m => m.SalesOrderNo ?? ""),
            ("furnaceno", false) => queryable.OrderBy(m => m.FurnaceNo ?? ""),
            ("furnaceno", true) => queryable.OrderByDescending(m => m.FurnaceNo ?? ""),
            ("sourceunit", false) => queryable.OrderBy(m => m.SourceUnit ?? ""),
            ("sourceunit", true) => queryable.OrderByDescending(m => m.SourceUnit ?? ""),
            ("productiontype", false) => queryable.OrderBy(m => m.ProductionType ?? ""),
            ("productiontype", true) => queryable.OrderByDescending(m => m.ProductionType ?? ""),
            ("datasource", false) => queryable.OrderBy(m => m.DataSource ?? ""),
            ("datasource", true) => queryable.OrderByDescending(m => m.DataSource ?? ""),
            ("productioncutquantity", false) => queryable.OrderBy(m => m.ProductionCutQuantity),
            ("productioncutquantity", true) => queryable.OrderByDescending(m => m.ProductionCutQuantity),
            ("productionweight", false) => queryable.OrderBy(m => m.ProductionWeight ?? 0),
            ("productionweight", true) => queryable.OrderByDescending(m => m.ProductionWeight ?? 0),
            ("lengthstatus", false) => queryable.OrderBy(m => m.LengthStatus ?? ""),
            ("lengthstatus", true) => queryable.OrderByDescending(m => m.LengthStatus ?? ""),
            ("isforcecompleted", false) => queryable.OrderBy(m => m.IsForceCompleted),
            ("isforcecompleted", true) => queryable.OrderByDescending(m => m.IsForceCompleted),
            ("salesman", false) => queryable.OrderBy(m => m.Salesman ?? ""),
            ("salesman", true) => queryable.OrderByDescending(m => m.Salesman ?? ""),
            ("deliverystate", false) => queryable.OrderBy(m => m.DeliveryState ?? ""),
            ("deliverystate", true) => queryable.OrderByDescending(m => m.DeliveryState ?? ""),
            _ => query.IsDescending
                ? queryable.OrderByDescending(m => m.CreatedTime)
                : queryable.OrderBy(m => m.CreatedTime)
        };

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(m => new MaterialReceiveCheckDto
            {
                Id = m.Id,
                ProductionBatchId = m.ProductionBatchId,
                ReceiveDate = m.ReceiveDate,
                Shift = m.Shift,
                Checker = m.Checker,
                Remark = m.Remark,
                DataSource = m.DataSource,
                BatchNo = m.BatchNo!,
                ManufacturingItem = m.ManufacturingItem!,
                TagNo = m.TagNo,
                WorkOrderNo = m.WorkOrderNo,
                SalesOrderNo = m.SalesOrderNo,
                SourceUnit = m.SourceUnit,
                FurnaceNo = m.FurnaceNo,
                PlantGrade = m.PlantGrade!,
                Specification = m.Specification!,
                ProductionType = m.ProductionType!,
                ProductionCutQuantity = m.ProductionCutQuantity,
                ProductionWeight = m.ProductionWeight,
                LengthStatus = m.LengthStatus!,
                IsForceCompleted = m.IsForceCompleted,
                Salesman = m.Salesman,
                DeliveryState = m.DeliveryState,
                CreatedTime = m.CreatedTime,
                UpdatedTime = m.UpdatedTime
            })
            .ToListAsync();

        return new PagedResult<MaterialReceiveCheckDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<MaterialReceiveCheckDto>> GetAllMaterialReceiveCheckListAsync()
    {
        return await _context.MaterialReceiveChecks
            .AsNoTracking()
            .OrderByDescending(rc => rc.Id)
            .Select(rc => new MaterialReceiveCheckDto
            {
                Id = rc.Id,
                ProductionBatchId = rc.ProductionBatchId,
                BatchNo = rc.BatchNo!,
                ManufacturingItem = rc.ManufacturingItem!,
                TagNo = rc.TagNo,
                WorkOrderNo = rc.WorkOrderNo,
                SalesOrderNo = rc.SalesOrderNo,
                SourceUnit = rc.SourceUnit,
                FurnaceNo = rc.FurnaceNo,
                PlantGrade = rc.PlantGrade!,
                Specification = rc.Specification!,
                ProductionType = rc.ProductionType!,
                DataSource = rc.DataSource,
                ProductionCutQuantity = rc.ProductionCutQuantity,
                ProductionWeight = rc.ProductionWeight,
                LengthStatus = rc.LengthStatus!,
                IsForceCompleted = rc.IsForceCompleted,
                Salesman = rc.Salesman,
                DeliveryState = rc.DeliveryState,
                ReceiveDate = rc.ReceiveDate,
                Shift = rc.Shift,
                Checker = rc.Checker,
                Remark = rc.Remark,
                CreatedTime = rc.CreatedTime,
                UpdatedTime = rc.UpdatedTime
            })
            .ToListAsync();
    }

    public async Task<List<PendingMaterialCheckDto>> GetPendingMaterialChecksAsync()
    {
        // ====== 两段式查询：先取批次，再取工序组，内存匹配 ======
        // 避免相关子查询（4 × N 次重复执行）
        // 说明：成品检验阶段 = CurrentSectionName="检验" AND SequenceNumber = 批次最大Seq

        // Step 1: 获取已有成检到料的批次 ID
        var existingIds = await _context.MaterialReceiveChecks
            .Select(m => m.ProductionBatchId)
            .ToListAsync();
        var existingSet = new HashSet<int>(existingIds);

        // Step 2: 获取所有活跃批次（在产中、进入或即将进入成品检验）
        var batches = await _context.ProductionBatches.AsNoTracking()
            .Where(b => (b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress)
                && (b.CurrentSectionName == "检验" || b.NextSectionName == "检验"))
            .Select(b => new
            {
                b.Id, b.BatchNo, b.WorkOrderNo, b.Salesman, b.TagNo,
                b.PlantGrade, b.Specification, b.CurrentValidWeight, b.CurrentExecDate,
                b.CurrentSectionName, b.CurrentSectionCompleted, b.CurrentGroupName,
                b.NextSectionName, b.NextProcess
            })
            .ToListAsync();

        // Step 3: 获取这些批次的 ProcessGroup 数据
        var batchIds = batches.Select(b => b.Id).ToList();
        var processGroups = await _context.Set<ProcessGroup>().AsNoTracking()
            .Where(pg => batchIds.Contains(pg.ProductionBatchId))
            .Select(pg => new { pg.ProductionBatchId, pg.SequenceNumber, pg.ProcessName })
            .ToListAsync();

        // Step 4: 构建 O(1) 查找
        var maxSeqLookup = processGroups
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.Max(pg => pg.SequenceNumber));

        var processSeqLookup = processGroups
            .GroupBy(pg => (pg.ProductionBatchId, pg.ProcessName ?? ""))
            .ToDictionary(g => g.Key, g => g.First().SequenceNumber);

        // Step 5: 内存匹配
        var pending = batches
            .Where(b => !existingSet.Contains(b.Id))
            .Where(b =>
            {
                if (!maxSeqLookup.TryGetValue(b.Id, out var maxSeq)) return false;

                if (b.CurrentSectionCompleted == false && b.CurrentSectionName == "检验")
                {
                    var seq = processSeqLookup.GetValueOrDefault((b.Id, b.CurrentGroupName ?? ""));
                    return seq == maxSeq;
                }

                if (b.CurrentSectionCompleted != false && b.NextSectionName == "检验" && b.NextProcess != null)
                {
                    var seq = processSeqLookup.GetValueOrDefault((b.Id, b.NextProcess));
                    return seq == maxSeq;
                }

                return false;
            })
            .OrderByDescending(b => b.CurrentValidWeight ?? 0)
            .Select(b => new PendingMaterialCheckDto
            {
                BatchId = b.Id,
                BatchNo = b.BatchNo,
                WorkOrderNo = b.WorkOrderNo,
                Salesman = b.Salesman,
                TagNo = b.TagNo,
                PlantGrade = b.PlantGrade,
                Specification = b.Specification,
                CurrentValidWeight = b.CurrentValidWeight ?? 0,
                CurrentExecDate = b.CurrentExecDate,
                CurrentSectionName = b.CurrentSectionName
            })
            .ToList();

        return pending;
    }


    // 需要从数据库 DISTINCT 查询的列（枚举/布尔由前端 EnumOptions 处理）
    private static readonly string[] _stringFilterColumns = new[]
    {
        "BatchNo", "PlantGrade", "Specification", "Shift", "Checker",
        "TagNo", "WorkOrderNo", "SalesOrderNo", "FurnaceNo", "SourceUnit",
        "Remark", "Salesman"
    };

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("MaterialReceiveCheckService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

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
            "PlantGrade" => queryable.Where(m => m.PlantGrade != null).Select(m => m.PlantGrade!).Distinct().OrderBy(x => x),
            "Specification" => queryable.Where(m => m.Specification != null).Select(m => m.Specification!).Distinct().OrderBy(x => x),
            "Shift" => queryable.Where(m => m.Shift != null).Select(m => m.Shift!).Distinct().OrderBy(x => x),
            "Checker" => queryable.Where(m => m.Checker != null).Select(m => m.Checker!).Distinct().OrderBy(x => x),
            "TagNo" => queryable.Where(m => m.TagNo != null).Select(m => m.TagNo!).Distinct().OrderBy(x => x),
            "WorkOrderNo" => queryable.Where(m => m.WorkOrderNo != null).Select(m => m.WorkOrderNo!).Distinct().OrderBy(x => x),
            "SalesOrderNo" => queryable.Where(m => m.SalesOrderNo != null).Select(m => m.SalesOrderNo!).Distinct().OrderBy(x => x),
            "FurnaceNo" => queryable.Where(m => m.FurnaceNo != null).Select(m => m.FurnaceNo!).Distinct().OrderBy(x => x),
            "SourceUnit" => queryable.Where(m => m.SourceUnit != null).Select(m => m.SourceUnit!).Distinct().OrderBy(x => x),
            "Remark" => queryable.Where(m => m.Remark != null).Select(m => m.Remark!).Distinct().OrderBy(x => x),
            "Salesman" => queryable.Where(m => m.Salesman != null).Select(m => m.Salesman!).Distinct().OrderBy(x => x),
            _ => null
        };
    }

    public async Task<byte[]> PrintMaterialCheckBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var items = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .Select(m => new MaterialReceiveCheckDto
            {
                Id = m.Id,
                ProductionBatchId = m.ProductionBatchId,
                ReceiveDate = m.ReceiveDate,
                Shift = m.Shift,
                Checker = m.Checker,
                Remark = m.Remark,
                BatchNo = m.BatchNo!,
                ManufacturingItem = m.ManufacturingItem!,
                TagNo = m.TagNo,
                WorkOrderNo = m.WorkOrderNo,
                SalesOrderNo = m.SalesOrderNo,
                SourceUnit = m.SourceUnit,
                FurnaceNo = m.FurnaceNo,
                PlantGrade = m.PlantGrade!,
                Specification = m.Specification!,
                ProductionType = m.ProductionType!,
                DataSource = m.DataSource,
                ProductionCutQuantity = m.ProductionCutQuantity,
                ProductionWeight = m.ProductionWeight,
                LengthStatus = m.LengthStatus!,
                IsForceCompleted = m.IsForceCompleted,
                CreatedTime = m.CreatedTime,
                UpdatedTime = m.UpdatedTime
            })
            .ToListAsync();

        return MaterialCheckPrintHelper.GenerateBatchPdf(items, columns);
    }

    public async Task<byte[]> PrintMaterialCheckAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? receiveDateFrom, DateTime? receiveDateTo)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "createdtime",
            IsDescending = isDescending,
            ReceiveDateFrom = receiveDateFrom,
            ReceiveDateTo = receiveDateTo
        };
        var paged = await GetAllMaterialReceiveChecksAsync(query);
        return MaterialCheckPrintHelper.GenerateBatchPdf(paged.Items, columns);
    }
}
