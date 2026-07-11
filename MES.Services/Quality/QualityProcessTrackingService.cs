using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Constants;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.ProductionStandard;
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
using MES.Core.Interfaces.ProductionStandard;
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
using MES.Data.Entities.ProductionStandard;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Quality;
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
                (x.TagNo != null && x.TagNo.Contains(kw)) ||
                (x.SourceUnit != null && x.SourceUnit.Contains(kw)) ||
                (x.Salesman != null && x.Salesman.Contains(kw)) ||
                (x.DeliveryState != null && x.DeliveryState.Contains(kw))
            );
        }

        // 到料日期范围筛选
        if (query.ReceiveDateFrom.HasValue)
            q = q.Where(x => x.ReceiveDate >= query.ReceiveDateFrom.Value);
        if (query.ReceiveDateTo.HasValue)
            q = q.Where(x => x.ReceiveDate <= query.ReceiveDateTo.Value);

        // 列筛选（DB 级）
        q = q.ApplyFilters(query.Filters);

        // 排序（DB 级）
        q = ApplySorting(q, query.SortBy, query.IsDescending);

        // 计数
        var totalCount = await q.CountAsync();

        // 分页 + DTO 投影
        var items = await q
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(e => new QualityProcessTrackingDto
            {
                Id = e.Id,
                ProductionBatchId = e.ProductionBatchId,
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
                ReceiveDate = e.ReceiveDate,
                Shift = e.Shift,
                Checker = e.Checker,
                CreatedTime = e.CreatedTime,
                UpdatedTime = e.UpdatedTime,

                // G2
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

                // G3
                ProductionCutQuantity = e.ProductionCutQuantity,
                TotalQuantity = e.TotalQuantity,
                QualifiedQuantity = e.QualifiedQuantity,
                DefectReworkQuantity = e.DefectReworkQuantity,
                DefectWarehouseQuantity = e.DefectWarehouseQuantity,
                DefectScrapQuantity = e.DefectScrapQuantity,
                MaxInspectionDate = e.MaxInspectionDate,

                // G4
                InboundQuantity = e.InboundQuantity,
                InboundWeight = e.InboundWeight,
                InboundDate = e.InboundDate,

                // G5
                QualityStatus = e.QualityStatus
            })
            .ToListAsync();

        return new PagedResult<QualityProcessTrackingDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    private static IQueryable<QualityProcessTracking> ApplySorting(IQueryable<QualityProcessTracking> query, string? sortBy, bool isDescending)
    {
        var sortKey = sortBy?.ToLower();
        return sortKey switch
        {
            "batchno" => isDescending ? query.OrderByDescending(x => x.BatchNo ?? "") : query.OrderBy(x => x.BatchNo ?? ""),
            "manufacturingitem" => isDescending ? query.OrderByDescending(x => x.ManufacturingItem ?? "") : query.OrderBy(x => x.ManufacturingItem ?? ""),
            "plantgrade" => isDescending ? query.OrderByDescending(x => x.PlantGrade ?? "") : query.OrderBy(x => x.PlantGrade ?? ""),
            "specification" => isDescending ? query.OrderByDescending(x => x.Specification ?? "") : query.OrderBy(x => x.Specification ?? ""),
            "checker" => isDescending ? query.OrderByDescending(x => x.Checker ?? "") : query.OrderBy(x => x.Checker ?? ""),
            "shift" => isDescending ? query.OrderByDescending(x => x.Shift ?? "") : query.OrderBy(x => x.Shift ?? ""),
            "salesman" => isDescending ? query.OrderByDescending(x => x.Salesman ?? "") : query.OrderBy(x => x.Salesman ?? ""),
            "deliverystate" => isDescending ? query.OrderByDescending(x => x.DeliveryState ?? "") : query.OrderBy(x => x.DeliveryState ?? ""),
            _ => query.OrderByDescending(x => x.ReceiveDate)
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
                ["BatchNo"] = all.Select(x => x.BatchNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
                ["PlantGrade"] = all.Select(x => x.PlantGrade).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
                ["Specification"] = all.Select(x => x.Specification).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
                ["Shift"] = all.Select(x => x.Shift).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
                ["Checker"] = all.Select(x => x.Checker).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
                ["FurnaceNo"] = all.Select(x => x.FurnaceNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
                ["WorkOrderNo"] = all.Select(x => x.WorkOrderNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
                ["SalesOrderNo"] = all.Select(x => x.SalesOrderNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
                ["SourceUnit"] = all.Select(x => x.SourceUnit).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
                ["Salesman"] = all.Select(x => x.Salesman).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
                ["QualityStatus"] = all.Select(x => x.QualityStatus).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
                ["TagNo"] = all.Select(x => x.TagNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(x => x).ToList(),
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
    /// </summary>
    public async Task RefreshByMrCheckIdAsync(int mrCheckId)
    {
        // 1. 查 MRCheck
        var rc = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == mrCheckId);
        if (rc == null) return;

        // 2. 查 ProductionBatch
        var pb = await _context.ProductionBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == rc.ProductionBatchId);

        // 3. 查关联的 FinalInspections
        var inspections = await _context.FinalInspections
            .AsNoTracking()
            .Where(fi => fi.ProductionBatchId == rc.ProductionBatchId)
            .ToListAsync();

        // 4. 查关联的 InventoryBatches
        var pbBatchNo = pb?.BatchNo;
        var inventoryBatches = pbBatchNo != null
            ? await _context.InventoryBatches
                .AsNoTracking()
                .Where(ib => ib.ProductionBatchNo == pbBatchNo)
                .ToListAsync()
            : new List<InventoryBatch>();

        // 5. 查关联的 ProductionRecords（仅 Cut 工段）
        var cutRecords = await _context.ProductionRecords
            .AsNoTracking()
            .Where(pr => pr.ProductionBatchId == rc.ProductionBatchId
                      && pr.SectionName == SectionDefs.Cut
                      && pr.ProductStatus == "成品")
            .ToListAsync();

        // 6. 计算各字段值
        var qualityStatus = inspections.Count > 0
            ? (inventoryBatches.Count > 0 ? "完成检验" : "检验中")
            : "待检验";

        var pmiDate = GetInspectionDate(inspections, InspectionItem.PMIInspection);
        var visualDate = GetInspectionDate(inspections, InspectionItem.VisualInspection);
        var dimensionDate = GetInspectionDate(inspections, InspectionItem.Dimension);
        var endoscopyDate = GetInspectionDate(inspections, InspectionItem.Endoscopy);
        var hydroDate = GetInspectionDate(inspections, InspectionItem.HydrostaticPressure);
        var underwaterPneumaticDate = GetInspectionDate(inspections, InspectionItem.UnderwaterPneumatic);
        var eddyCurrentDate = GetInspectionDate(inspections, InspectionItem.EddyCurrent);
        var ultrasonicDate = GetInspectionDate(inspections, InspectionItem.Ultrasonic);
        var portColoringDate = GetInspectionDate(inspections, InspectionItem.PortColoring);

        var now = DateTime.Now;

        // 7. Upsert 物化行
        var existing = await _context.QualityProcessTrackings
            .FirstOrDefaultAsync(q => q.MaterialReceiveCheckId == mrCheckId);

        if (existing != null)
        {
            // 更新已有记录
            MapSourceToEntity(existing, rc, pb, inspections, inventoryBatches, cutRecords,
                qualityStatus, pmiDate, visualDate, dimensionDate, endoscopyDate,
                hydroDate, underwaterPneumaticDate, eddyCurrentDate, ultrasonicDate,
                portColoringDate, now);
            _context.Entry(existing).State = EntityState.Modified;
        }
        else
        {
            // 新增
            var entity = new QualityProcessTracking();
            MapSourceToEntity(entity, rc, pb, inspections, inventoryBatches, cutRecords,
                qualityStatus, pmiDate, visualDate, dimensionDate, endoscopyDate,
                hydroDate, underwaterPneumaticDate, eddyCurrentDate, ultrasonicDate,
                portColoringDate, now);
            _context.QualityProcessTrackings.Add(entity);
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 按批次ID刷新物化行（查找关联的 MRCheck 后调用 RefreshByMrCheckIdAsync）
    /// </summary>
    public async Task RefreshByProductionBatchIdAsync(int productionBatchId)
    {
        var mrCheckIds = await _context.MaterialReceiveChecks
            .Where(r => r.ProductionBatchId == productionBatchId)
            .Select(r => r.Id)
            .ToListAsync();

        foreach (var id in mrCheckIds)
        {
            await RefreshByMrCheckIdAsync(id);
        }
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

        foreach (var id in mrCheckIds)
        {
            await RefreshByMrCheckIdAsync(id);
        }
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
        DateTime refreshTime)
    {
        // 关联标识
        entity.MaterialReceiveCheckId = rc.Id;
        entity.ProductionBatchId = rc.ProductionBatchId;

        // G1
        entity.BatchNo = rc.BatchNo ?? pb?.BatchNo;
        entity.ManufacturingItem = rc.ManufacturingItem;
        entity.TagNo = rc.TagNo;
        entity.WorkOrderNo = rc.WorkOrderNo;
        entity.SalesOrderNo = rc.SalesOrderNo;
        entity.SourceUnit = rc.SourceUnit;
        entity.FurnaceNo = rc.FurnaceNo;
        entity.PlantGrade = rc.PlantGrade;
        entity.Specification = rc.Specification;
        entity.ProductionType = pb?.ProductionType;
        entity.LengthStatus = rc.LengthStatus;
        entity.ProductionWeight = rc.ProductionWeight;
        entity.IsForceCompleted = rc.IsForceCompleted;
        entity.Salesman = rc.Salesman ?? pb?.Salesman;
        entity.DeliveryState = rc.DeliveryState ?? pb?.DeliveryState;
        entity.ReceiveDate = rc.ReceiveDate;
        entity.Shift = rc.Shift;
        entity.Checker = rc.Checker;
        entity.PbBatchNo = pb?.BatchNo;

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

        // G3
        entity.ProductionCutQuantity = cutRecords.Sum(pr => pr.PostCutQuantity ?? 0);
        entity.TotalQuantity = inspections.Max(fi => (int?)(fi.Quantity ?? 0)) ?? 0;
        entity.QualifiedQuantity = inspections.Min(fi => (int?)(fi.QualifiedQuantity ?? 0)) ?? 0;
        entity.DefectReworkQuantity = inspections.Sum(fi => fi.DefectReworkQuantity ?? 0);
        entity.DefectWarehouseQuantity = inspections.Sum(fi => fi.DefectWarehouseQuantity ?? 0);
        entity.DefectScrapQuantity = inspections.Sum(fi => fi.DefectScrapQuantity ?? 0);
        entity.MaxInspectionDate = inspections.Max(fi => (DateTime?)fi.InspectionDate);

        // G4
        entity.InboundQuantity = inventoryBatches.Sum(ib => ib.InitialQuantity);
        entity.InboundWeight = inventoryBatches.Sum(ib => (decimal?)ib.InitialWeight);
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

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? receiveDateFrom = null, DateTime? receiveDateTo = null)
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
        var result = await GetPagedAsync(query);
        return QualityProcessTrackingPrintHelper.GenerateBatchPdf(result.Items, columns);
    }
}
