using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Constants;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.Helpers;
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
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Quality;
using System.Linq.Expressions;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Quality;

/// <summary>
/// 质量过程跟踪服务（成检到料 → 成品检验 → 成品入库 物化读模型）
/// 数据由业务 Service 写入后自动刷新 QualityProcessTracking 物化表
/// </summary>
public class QualityProcessTrackingService : IQualityProcessTrackingService
{
    private readonly AppDbContext _context;
    private readonly ILogger<QualityProcessTrackingService> _logger;
    private readonly IMemoryCache _cache;

    public QualityProcessTrackingService(AppDbContext context, ILogger<QualityProcessTrackingService> logger, IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    // 筛选上下文缓存由 IMemoryCache 管理（注入 _cache）

    public async Task<PagedResult<QualityProcessTrackingDto>> GetPagedAsync(QueryParams query)
    {
        var q = _context.Set<QualityProcessTracking>().AsNoTracking();

        // 关键词搜索
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            q = q.Where(x =>
                (x.BatchNo != null && x.BatchNo.Contains(kw)) ||
                (x.ManufacturingItem != null && x.ManufacturingItem.Contains(kw)) ||
                (x.PlantGrade != null && x.PlantGrade.Contains(kw)) ||
                (x.Specification != null && x.Specification.Contains(kw)) ||
                (x.Checker != null && x.Checker.Contains(kw)) ||
                (x.FurnaceNo != null && x.FurnaceNo.Contains(kw)) ||
                (x.WorkOrderNo != null && x.WorkOrderNo.Contains(kw)) ||
                (x.SalesOrderNo != null && x.SalesOrderNo.Contains(kw)) ||
                (x.ProductionMainNo != null && x.ProductionMainNo.Contains(kw)) ||
                (x.TagNo != null && x.TagNo.Contains(kw)) ||
                (x.SourceUnit != null && x.SourceUnit.Contains(kw)) ||
                (x.Salesman != null && x.Salesman.Contains(kw)) ||
                (x.DeliveryState != null && x.DeliveryState.Contains(kw)) ||
                (x.ManufacturingStatus != null && x.ManufacturingStatus.Contains(kw)) ||
                (x.EndCustomer != null && x.EndCustomer.Contains(kw))
            );
        }

        // 到料日期范围筛选
        if (query.ReceiveDateFrom.HasValue)
            q = q.Where(x => x.ReceiveDate >= query.ReceiveDateFrom.Value);
        if (query.ReceiveDateTo.HasValue)
            q = q.Where(x => x.ReceiveDate <= query.ReceiveDateTo.Value);

        // 列筛选（DB 级）
        // QualityStatus 筛选预处理："异常完成"是计算字段（IsForceCompleted=true 时优先显示）
        // 四种情况：
        //   仅选中"异常完成" → WHERE IsForceCompleted = true
        //   "异常完成" + 其他值 → WHERE (QualityStatus IN (...) OR IsForceCompleted = true)
        //   仅其他值（无"异常完成"）→ WHERE QualityStatus IN (...) AND IsForceCompleted = false
        //   无 QualityStatus 筛选 → 不处理
        var qsSelectedValues = new List<string>();
        var hasAbnormalComplete = false;
        if (query.Filters is { Count: > 0 })
        {
            var qsFilter = query.Filters.FirstOrDefault(f =>
                f.Field.Equals("QualityStatus", StringComparison.OrdinalIgnoreCase));
            if (qsFilter?.Values is { Count: > 0 })
            {
                query.Filters.Remove(qsFilter);
                hasAbnormalComplete = qsFilter.Values.Remove("异常完成");
                qsSelectedValues = qsFilter.Values.ToList();
            }
        }
        q = q.ApplyFilters(query.Filters);
        if (hasAbnormalComplete && qsSelectedValues.Count == 0)
        {
            // 仅选中"异常完成" → IsForceCompleted = true
            q = q.Where(x => x.IsForceCompleted);
        }
        else if (hasAbnormalComplete && qsSelectedValues.Count > 0)
        {
            // "异常完成" + 其他 QualityStatus 值 → (QualityStatus IN (...) OR IsForceCompleted = true)
            var param = Expression.Parameter(typeof(QualityProcessTracking), "e");
            var qsMember = Expression.Property(param, "QualityStatus");
            var qsList = Expression.Constant(qsSelectedValues);
            var containsMethod = typeof(List<string>).GetMethod("Contains", [typeof(string)])!;
            var qsIn = Expression.Call(qsList, containsMethod, qsMember);
            var fcCondition = Expression.Equal(Expression.Property(param, "IsForceCompleted"), Expression.Constant(true));
            var lambda = Expression.Lambda<Func<QualityProcessTracking, bool>>(Expression.OrElse(qsIn, fcCondition), param);
            q = q.Where(lambda);
        }
        else if (qsSelectedValues.Count > 0)
        {
            // 仅其他值（无"异常完成"）→ 排除强制完成的记录
            q = q.Where(x => qsSelectedValues.Contains(x.QualityStatus) && !x.IsForceCompleted);
        }

        // 排序（DB 级）
        q = q.ApplySort(query.SortBy, query.IsDescending);

        // 计数
        var totalCount = await q.CountAsync();

        // 分页 + 两步投影（枚举字段通过匿名类型中转）
        var raw = await q
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(e => new
            {
                e.Id, e.ProductionBatchId, e.BatchNo,
                e.InspectionType, e.IsDeliveryStatus,
                ManufacturingItemStr = e.ManufacturingItem,
                e.TagNo, e.WorkOrderNo, e.SalesOrderNo, e.ProductionMainNo, e.SourceUnit, e.FurnaceNo,
                e.PlantGrade, e.Specification,
                ProductionTypeStr = e.ProductionType,
                LengthStatusStr = e.LengthStatus,
                e.ProductionWeight, e.IsForceCompleted, e.Salesman,
                e.ManufacturingStatus,
                DeliveryStateStr = e.DeliveryState,
                e.EndCustomer,
                e.ReceiveDate, e.Shift, e.Checker, e.CreatedTime, e.UpdatedTime,
                e.PmiDate, e.VisualDate, e.DimensionDate, e.EndoscopyDate,
                e.HydroDate, e.UnderwaterPneumaticDate, e.EddyCurrentDate,
                e.UltrasonicDate, e.PortColoringDate, e.InspectionCount,
                e.ProductionCutQuantity, e.TotalQuantity, e.QualifiedQuantity,
                e.DefectReworkQuantity, e.DefectWarehouseQuantity, e.DefectScrapQuantity,
                e.MaxInspectionDate,
                e.InboundQuantity, e.InboundWeight, e.InboundDate,
                e.QualityStatus
            })
            .ToListAsync();

        var items = raw.Select(e => new QualityProcessTrackingDto
        {
            Id = e.Id,
            ProductionBatchId = e.ProductionBatchId,
            BatchNo = e.BatchNo,
            InspectionType = EnumHelper.TryParse<MES.Core.Enums.InspectionType>(e.InspectionType),
            IsDeliveryStatus = e.IsDeliveryStatus,
            ManufacturingItem = ParseMaterialType(e.ManufacturingItemStr),
            TagNo = e.TagNo,
            WorkOrderNo = e.WorkOrderNo,
            SalesOrderNo = e.SalesOrderNo,
            ProductionMainNo = e.ProductionMainNo,
            SourceUnit = e.SourceUnit,
            FurnaceNo = e.FurnaceNo,
            PlantGrade = e.PlantGrade,
            Specification = e.Specification,
            ProductionType = e.ProductionTypeStr != null ? Enum.Parse<ProductionType>(e.ProductionTypeStr) : null,
            LengthStatus = e.LengthStatusStr != null ? Enum.Parse<LengthStatus>(e.LengthStatusStr) : null,
            ProductionWeight = e.ProductionWeight,
            IsForceCompleted = e.IsForceCompleted,
            Salesman = e.Salesman,
            ManufacturingStatus = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(e.ManufacturingStatus),
            DeliveryState = e.DeliveryStateStr != null ? Enum.Parse<DeliveryState>(e.DeliveryStateStr) : null,
            EndCustomer = e.EndCustomer,
            ReceiveDate = e.ReceiveDate,
            Shift = e.Shift != null && Enum.TryParse<ShiftType>(e.Shift, out var s) ? s : (ShiftType?)null,
            Checker = e.Checker,
            CreatedTime = e.CreatedTime,
            UpdatedTime = e.UpdatedTime,
            PmiDate = e.PmiDate,
            VisualDate = e.VisualDate,
            DimensionDate = e.DimensionDate,
            EndoscopyDate = e.EndoscopyDate,
            HydroDate = e.HydroDate,
            UnderwaterPneumaticDate = e.UnderwaterPneumaticDate,
            EddyCurrentDate = e.EddyCurrentDate,
            UltrasonicDate = e.UltrasonicDate,
            PortColoringDate = e.PortColoringDate,
            InspectionCount = e.InspectionCount,
            ProductionCutQuantity = e.ProductionCutQuantity,
            TotalQuantity = e.TotalQuantity,
            QualifiedQuantity = e.QualifiedQuantity,
            DefectReworkQuantity = e.DefectReworkQuantity,
            DefectWarehouseQuantity = e.DefectWarehouseQuantity,
            DefectScrapQuantity = e.DefectScrapQuantity,
            MaxInspectionDate = e.MaxInspectionDate,
            InboundQuantity = e.InboundQuantity,
            InboundWeight = e.InboundWeight,
            InboundDate = e.InboundDate,
            QualityStatus = e.QualityStatus
        }).ToList();

        return new PagedResult<QualityProcessTrackingDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("QualityProcessTrackingService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            // 从物化表查询所有数据，在内存中构建 DISTINCT 字典
            var all = await _context.QualityProcessTrackings
                .AsNoTracking()
                .Select(e => new
                {
                    e.BatchNo,
                    e.PlantGrade,
                    e.Specification,
                    e.Shift,
                    e.Checker,
                    e.FurnaceNo,
                    e.WorkOrderNo,
                    e.SalesOrderNo,
                    e.ProductionMainNo,
                    e.SourceUnit,
                    e.Salesman,
                    e.TagNo,
                    e.ReceiveDate,
                    e.PmiDate,
                    e.VisualDate,
                    e.DimensionDate,
                    e.EndoscopyDate,
                    e.HydroDate,
                    e.UnderwaterPneumaticDate,
                    e.EddyCurrentDate,
                    e.UltrasonicDate,
                    e.PortColoringDate,
                    e.InboundDate,
                    e.QualityStatus
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                // 注：ManufacturingItem/DeliveryState 等有固定 EnumOptions 的枚举列
                // 由前端硬编码提供中文选项，不在 API 返回（按 04_开发规范 筛选陷阱#1）
                ["BatchNo"] = all.Select(x => x.BatchNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["PlantGrade"] = all.Select(x => x.PlantGrade).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["Specification"] = all.Select(x => x.Specification).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["Shift"] = all.Select(x => x.Shift).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["Checker"] = all.Select(x => x.Checker).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["FurnaceNo"] = all.Select(x => x.FurnaceNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["WorkOrderNo"] = all.Select(x => x.WorkOrderNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["SalesOrderNo"] = all.Select(x => x.SalesOrderNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["ProductionMainNo"] = all.Select(x => x.ProductionMainNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["SourceUnit"] = all.Select(x => x.SourceUnit).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["Salesman"] = all.Select(x => x.Salesman).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["QualityStatus"] = all.Select(x => x.QualityStatus).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["TagNo"] = all.Select(x => x.TagNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).Cast<string>().ToList(),
                ["ReceiveDate"] = all.Select(x => x.ReceiveDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["PmiDate"] = all.Where(x => x.PmiDate.HasValue).Select(x => x.PmiDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["VisualDate"] = all.Where(x => x.VisualDate.HasValue).Select(x => x.VisualDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["DimensionDate"] = all.Where(x => x.DimensionDate.HasValue).Select(x => x.DimensionDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["EndoscopyDate"] = all.Where(x => x.EndoscopyDate.HasValue).Select(x => x.EndoscopyDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["HydroDate"] = all.Where(x => x.HydroDate.HasValue).Select(x => x.HydroDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["UnderwaterPneumaticDate"] = all.Where(x => x.UnderwaterPneumaticDate.HasValue).Select(x => x.UnderwaterPneumaticDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["EddyCurrentDate"] = all.Where(x => x.EddyCurrentDate.HasValue).Select(x => x.EddyCurrentDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["UltrasonicDate"] = all.Where(x => x.UltrasonicDate.HasValue).Select(x => x.UltrasonicDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["PortColoringDate"] = all.Where(x => x.PortColoringDate.HasValue).Select(x => x.PortColoringDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["InboundDate"] = all.Where(x => x.InboundDate.HasValue).Select(x => x.InboundDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    /// <summary>
    /// 按成检到料ID刷新物化行（从源表重新计算并Upsert）
    /// 唯一键：批次 + 成检类型（IsDeliveryStatus 为信息列，随批次当前制造状态实时计算，不参与唯一性）
    /// </summary>
    public async Task RefreshByMrCheckIdAsync(int mrCheckId)
        => await RefreshByMrCheckIdsAsync(new[] { mrCheckId });

    /// <summary>
    /// 按批次ID刷新物化行（查找关联的 MRCheck 后批量刷新）
    /// </summary>
    public async Task RefreshByProductionBatchIdAsync(int productionBatchId)
    {
        var mrCheckIds = await _context.MaterialReceiveChecks
            .Where(r => r.ProductionBatchId == productionBatchId)
            .Select(r => r.Id)
            .ToListAsync();
        if (mrCheckIds.Count == 0) return;
        await RefreshByMrCheckIdsAsync(mrCheckIds);
    }

    /// <summary>
    /// 按批次号刷新物化行
    /// </summary>
    public async Task RefreshByBatchNoAsync(string batchNo)
    {
        var mrCheckIds = await _context.MaterialReceiveChecks
            .Where(r => r.BatchNo == batchNo)
            .Select(r => r.Id)
            .ToListAsync();
        if (mrCheckIds.Count == 0) return;
        await RefreshByMrCheckIdsAsync(mrCheckIds);
    }

    /// <summary>
    /// 全量刷新所有物化行（聚合口径/唯一键变更后的存量重算；分块避免 IN 参数超限）
    /// </summary>
    public async Task RefreshAllAsync()
    {
        var mrCheckIds = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Select(r => r.Id)
            .ToListAsync();
        foreach (var chunk in ChunkBatchIds(mrCheckIds, 1000))
            await RefreshByMrCheckIdsAsync(chunk);
    }

    /// <summary>
    /// 批量刷新多个 MRCheck 的物化行。
    /// 所有关联数据（批次/成检/入库/断切记录/QPT 行）一次批量加载，消除逐行 N+1 查询。
    /// 断切记录按批次分块 IN 查询，避免 SQL Server 2100 参数上限。
    /// 注意：QPT 行必须用跟踪查询加载，依赖 EF identity resolution 保证同一 DB 行同一实例，
    ///       避免 upsert（Modified/Remove）时因重复跟踪实例而冲突。
    /// </summary>
    private async Task RefreshByMrCheckIdsAsync(ICollection<int> mrCheckIds)
    {
        if (mrCheckIds.Count == 0) return;

        // 1. 一次查所有 MRCheck
        var rcs = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(r => mrCheckIds.Contains(r.Id))
            .ToListAsync();
        if (rcs.Count == 0) return;

        var involvedBatchIds = rcs.Select(r => r.ProductionBatchId).Distinct().ToList();
        var involvedBatchNos = rcs
            .Where(r => r.InspectionType != nameof(InspectionType.PreInspection))
            .Select(r => r.BatchNo)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .ToList();

        // 2. 一次查所有涉及的批次
        var pbDict = await _context.ProductionBatches
            .AsNoTracking()
            .Where(p => involvedBatchIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // 3. 一次查所有涉及的成检记录
        var inspectionsByBatch = (await _context.FinalInspections
            .AsNoTracking()
            .Where(fi => involvedBatchIds.Contains(fi.ProductionBatchId))
            .ToListAsync())
            .GroupBy(fi => fi.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 4. 一次查所有涉及的入库记录（仅正式成检关联；按批号分组，物料/制造状态在内存匹配）
        var inboundByBatchNo = new Dictionary<string, List<InventoryBatch>>(StringComparer.OrdinalIgnoreCase);
        if (involvedBatchNos.Count > 0)
        {
            var inboundRows = await _context.InventoryBatches
                .AsNoTracking()
                .Where(ib => ib.ProductionBatchNo != null && involvedBatchNos.Contains(ib.ProductionBatchNo))
                .ToListAsync();
            foreach (var ib in inboundRows)
            {
                if (ib.ProductionBatchNo == null) continue;
                if (!inboundByBatchNo.TryGetValue(ib.ProductionBatchNo, out var list))
                    inboundByBatchNo[ib.ProductionBatchNo] = list = new List<InventoryBatch>();
                list.Add(ib);
            }
        }

        // 5. 一次查所有涉及的断切成品记录（分块避免 IN 参数超限）
        var cutRecordsByBatch = new Dictionary<int, List<ProductionRecord>>();
        foreach (var chunk in ChunkBatchIds(involvedBatchIds, 1000))
        {
            var cutRows = await _context.ProductionRecords
                .AsNoTracking()
                .Where(pr => chunk.Contains(pr.ProductionBatchId)
                          && pr.SectionName == SectionKeys.Cut
                          && pr.ProductStatus == ProductStatuses.Finished
                          && pr.IsPreCut != true) // 预成切不计入成品切割支数
                .ToListAsync();
            foreach (var r in cutRows)
            {
                if (!cutRecordsByBatch.TryGetValue(r.ProductionBatchId, out var list))
                    cutRecordsByBatch[r.ProductionBatchId] = list = new List<ProductionRecord>();
                list.Add(r);
            }
        }

        // 6. 一次查所有涉及的 QPT 行（跟踪查询：历史归属 + 目标键行）
        var ownedRowsByMrCheckId = (await _context.QualityProcessTrackings
            .Where(q => mrCheckIds.Contains(q.MaterialReceiveCheckId))
            .ToListAsync())
            .GroupBy(q => q.MaterialReceiveCheckId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var qptRowsByBatch = (await _context.QualityProcessTrackings
            .Where(q => involvedBatchIds.Contains(q.ProductionBatchId))
            .ToListAsync())
            .GroupBy(q => q.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var now = DateTime.Now;

        // 7. 逐行计算并 Upsert
        foreach (var rc in rcs)
        {
            var pb = pbDict.GetValueOrDefault(rc.ProductionBatchId);

            // 关联的成检（按批次 + 成检类型匹配）
            var inspections = inspectionsByBatch.GetValueOrDefault(rc.ProductionBatchId)?
                .Where(fi => string.Equals(fi.InspectionType, rc.InspectionType, StringComparison.OrdinalIgnoreCase))
                .ToList() ?? new List<FinalInspection>();

            // 关联的入库（同生产批号 + 同物料类型 + 同制造状态 三条件匹配；预成检不关联）
            var inventoryBatches = pb != null && rc.InspectionType != nameof(InspectionType.PreInspection)
                ? (inboundByBatchNo.TryGetValue(pb.BatchNo, out var ibList)
                    ? ibList
                        .Where(ib => string.Equals(ib.MaterialType, pb.ManufacturingItem, StringComparison.OrdinalIgnoreCase)
                                  && string.Equals(ib.ManufacturingStatus, pb.ManufacturingStatus, StringComparison.OrdinalIgnoreCase))
                        .ToList()
                    : new List<InventoryBatch>())
                : new List<InventoryBatch>();

            // 关联的断切成品记录
            var cutRecords = cutRecordsByBatch.GetValueOrDefault(rc.ProductionBatchId) ?? new List<ProductionRecord>();

            // 计算各字段值
            var qualityStatus = inspections.Count > 0
                ? (inventoryBatches.Count > 0 ? "完成检验" : "检验中")
                : (inventoryBatches.Count > 0 ? "入库存疑" : "待检验");

            var pmiDate = GetInspectionDate(inspections, InspectionItem.PMIInspection);
            var visualDate = GetInspectionDate(inspections, InspectionItem.VisualInspection);
            var dimensionDate = GetInspectionDate(inspections, InspectionItem.Dimension);
            var endoscopyDate = GetInspectionDate(inspections, InspectionItem.Endoscopy);
            var hydroDate = GetInspectionDate(inspections, InspectionItem.HydrostaticPressure);
            var underwaterPneumaticDate = GetInspectionDate(inspections, InspectionItem.UnderwaterPneumatic);
            var eddyCurrentDate = GetInspectionDate(inspections, InspectionItem.EddyCurrent);
            var ultrasonicDate = GetInspectionDate(inspections, InspectionItem.Ultrasonic);
            var portColoringDate = GetInspectionDate(inspections, InspectionItem.PortColoring);

            // 交付态（信息列，随批次当前制造状态实时计算；不参与唯一性）
            var isDeliveryStatus = pb != null
                && !string.IsNullOrEmpty(pb.ManufacturingStatus)
                && !string.IsNullOrEmpty(pb.DeliveryState)
                && string.Equals(pb.ManufacturingStatus, pb.DeliveryState, StringComparison.OrdinalIgnoreCase)
                ? "是" : "否";

            // Upsert 物化行
            // 本 MRCheck 历史归属的行（唯一键调整后旧行需清理）
            var ownedRows = ownedRowsByMrCheckId.GetValueOrDefault(rc.Id) ?? new List<QualityProcessTracking>();

            // 目标键行（批次+成检类型）
            var targetByKey = qptRowsByBatch.GetValueOrDefault(rc.ProductionBatchId)?
                .FirstOrDefault(q => string.Equals(q.InspectionType, rc.InspectionType, StringComparison.OrdinalIgnoreCase));

            QualityProcessTracking entity;
            if (targetByKey != null)
            {
                entity = targetByKey;
                entity.MaterialReceiveCheckId = rc.Id;
                MapSourceToEntity(entity, rc, pb, inspections, inventoryBatches, cutRecords,
                    qualityStatus, pmiDate, visualDate, dimensionDate, endoscopyDate,
                    hydroDate, underwaterPneumaticDate, eddyCurrentDate, ultrasonicDate,
                    portColoringDate, isDeliveryStatus, now);
                _context.Entry(entity).State = EntityState.Modified;
            }
            else
            {
                entity = ownedRows.FirstOrDefault() ?? new QualityProcessTracking();
                MapSourceToEntity(entity, rc, pb, inspections, inventoryBatches, cutRecords,
                    qualityStatus, pmiDate, visualDate, dimensionDate, endoscopyDate,
                    hydroDate, underwaterPneumaticDate, eddyCurrentDate, ultrasonicDate,
                    portColoringDate, isDeliveryStatus, now);
                if (entity.Id == 0)
                    _context.QualityProcessTrackings.Add(entity);
                else
                    _context.Entry(entity).State = EntityState.Modified;
            }

            // 清理本 MRCheck 名下不再匹配目标键的旧行
            foreach (var stale in ownedRows.Where(q => q.Id != entity.Id))
            {
                _context.QualityProcessTrackings.Remove(stale);
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 分块工具：避免 SQL Server IN 子句 2100 参数上限
    /// </summary>
    private static IEnumerable<List<int>> ChunkBatchIds(List<int> ids, int size)
    {
        for (var i = 0; i < ids.Count; i += size)
            yield return ids.Skip(i).Take(size).ToList();
    }

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

    private static DateTime? GetInspectionDate(List<FinalInspection> inspections, InspectionItem item)
    {
        return inspections
            .Where(fi => fi.InspectionItem == item)
            .Max(fi => (DateTime?)fi.InspectionDate);
    }

    private static void MapSourceToEntity(
        QualityProcessTracking entity,
        MaterialReceiveCheck rc,
        ProductionBatch? pb,
        List<FinalInspection> inspections,
        List<InventoryBatch> inventoryBatches,
        List<ProductionRecord> cutRecords,
        string qualityStatus,
        DateTime? pmiDate, DateTime? visualDate, DateTime? dimensionDate,
        DateTime? endoscopyDate, DateTime? hydroDate, DateTime? underwaterPneumaticDate,
        DateTime? eddyCurrentDate, DateTime? ultrasonicDate, DateTime? portColoringDate,
        string isDeliveryStatus,
        DateTime refreshTime)
    {
        // 关联标识
        entity.MaterialReceiveCheckId = rc.Id;
        entity.ProductionBatchId = rc.ProductionBatchId;
        // 两字段唯一键（批次+成检类型）；IsDeliveryStatus 为信息列
        entity.InspectionType = rc.InspectionType;
        entity.IsDeliveryStatus = isDeliveryStatus;

        // G1（批次冗余字段从 ProductionBatch 获取）
        entity.BatchNo = rc.BatchNo;
        entity.ManufacturingItem = pb?.ManufacturingItem;
        entity.TagNo = pb?.TagNo;
        entity.WorkOrderNo = pb?.WorkOrderNo;
        entity.SalesOrderNo = pb?.SalesOrderNo;
        entity.ProductionMainNo = pb?.ProductionMainNo;
        entity.SourceUnit = pb?.SourceName;
        entity.FurnaceNo = pb?.SourceHeatNo;
        entity.PlantGrade = pb?.PlantGrade;
        entity.Specification = pb?.Specification;
        entity.ProductionType = pb?.ProductionType;
        entity.LengthStatus = pb?.LengthStatus;
        entity.IsForceCompleted = rc.IsForceCompleted;
        entity.Salesman = pb?.Salesman;
        entity.ManufacturingStatus = pb?.ManufacturingStatus;
        entity.DeliveryState = pb?.DeliveryState;
        entity.EndCustomer = pb?.EndCustomer;
        entity.ReceiveDate = rc.ReceiveDate;
        entity.Shift = rc.Shift?.ToString();
        entity.Checker = rc.Checker;
        // G2
        entity.PmiDate = pmiDate;
        entity.VisualDate = visualDate;
        entity.DimensionDate = dimensionDate;
        entity.EndoscopyDate = endoscopyDate;
        entity.HydroDate = hydroDate;
        entity.UnderwaterPneumaticDate = underwaterPneumaticDate;
        entity.EddyCurrentDate = eddyCurrentDate;
        entity.UltrasonicDate = ultrasonicDate;
        entity.PortColoringDate = portColoringDate;
        entity.InspectionCount = inspections.Select(fi => fi.InspectionItem).Distinct().Count();

        // G3 生产支数（三态口径，参考成切跟踪/定尺工单「免切理论支」逻辑）：
        //   1) 无需成品切割（CutRequirement=false）→ 批次理论成品支数
        //   2) 需成品切割 + 长度状态=定尺 → 断切成品记录切后支数(PostCutQuantity)汇总
        //   3) 需成品切割 + 长度状态<>定尺 → 断切成品记录加工支数(Quantity)汇总
        if (pb?.CutRequirement != true)
        {
            entity.ProductionCutQuantity = pb?.TheoreticalOutputQty ?? 0;
        }
        else if (string.Equals(pb.LengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase))
        {
            entity.ProductionCutQuantity = cutRecords.Sum(pr => pr.PostCutQuantity ?? 0);
        }
        else
        {
            entity.ProductionCutQuantity = cutRecords.Sum(pr => pr.Quantity ?? 0);
        }
        // 生产重量：非定尺=批次理论成品重量（算法不变）；定尺=产品单支重 × 生产支数（产品单支重缺失时回退理论单支重）
        if (pb == null || !string.Equals(pb.LengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase))
        {
            entity.ProductionWeight = pb?.TheoreticalOutputWeight;
        }
        else
        {
            var unitWeight = pb.ProductUnitWeight ?? pb.TheoreticalUnitWeight;
            entity.ProductionWeight = unitWeight.HasValue
                ? unitWeight.Value * entity.ProductionCutQuantity
                : null;
        }
        // 三个次品：按唯一性（批次+成检类型）汇总全部检验记录
        entity.DefectReworkQuantity = inspections.Sum(fi => fi.DefectReworkQuantity ?? 0);
        entity.DefectWarehouseQuantity = inspections.Sum(fi => fi.DefectWarehouseQuantity ?? 0);
        entity.DefectScrapQuantity = inspections.Sum(fi => fi.DefectScrapQuantity ?? 0);
        // 检验支数：按（唯一性+检验项目）分组汇总 Quantity，跨检验项目取最大
        // （同一项目多条记录各代表一批受检管子，需求和；不同项目覆盖管子数可能不同，取最大为受检总数）
        entity.TotalQuantity = inspections
            .GroupBy(fi => fi.InspectionItem)
            .Max(g => (int?)g.Sum(fi => fi.Quantity ?? 0)) ?? 0;
        // 理论合格支：检验支数 - 三个次品汇总（负值归零，防御跨项目重复计数）
        entity.QualifiedQuantity = Math.Max(0,
            entity.TotalQuantity - entity.DefectReworkQuantity - entity.DefectWarehouseQuantity - entity.DefectScrapQuantity);
        entity.MaxInspectionDate = inspections.Max(fi => (DateTime?)fi.InspectionDate);

        // G4
        entity.InboundQuantity = inventoryBatches.Sum(ib => ib.InitialQuantity);
        entity.InboundWeight = inventoryBatches.Sum(ib => ib.InitialWeight);
        entity.InboundDate = inventoryBatches.Max(ib => (DateTime?)ib.InboundDate);

        // G5
        entity.QualityStatus = qualityStatus;

        // 刷新追踪
        entity.LastRefreshTime = refreshTime;
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            SortBy = "Receivedate",
            IsDescending = true
        };
        var result = await GetPagedAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return QualityProcessTrackingPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? receiveDateFrom = null, DateTime? receiveDateTo = null, string? filters = null)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "Receivedate" : sortBy,
            IsDescending = isDescending,
            ReceiveDateFrom = receiveDateFrom,
            ReceiveDateTo = receiveDateTo
        };
        if (!string.IsNullOrEmpty(filters))
        {
            var f = System.Text.Json.JsonSerializer.Deserialize<List<FilterDescriptor>>(filters);
            if (f != null && f.Count > 0)
                query.Filters = f;
        }
        var result = await GetPagedAsync(query);
        return QualityProcessTrackingPrintHelper.GenerateBatchPdf(result.Items, columns);
    }
}
