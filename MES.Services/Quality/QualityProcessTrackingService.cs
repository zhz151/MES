using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Constants;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;

namespace MES.Services.Quality;

/// <summary>
/// 质量过程跟踪服务（成检到料 → 成品检验 → 成品入库 联通表）
/// 优化策略：拆分子查询为预查询 + 内存映射，避免 EF Core 生成 15+ 关联子查询的单一巨量 SQL
/// </summary>
public class QualityProcessTrackingService : IQualityProcessTrackingService
{
    private readonly AppDbContext _context;
    private readonly ILogger<QualityProcessTrackingService> _logger;

    public QualityProcessTrackingService(AppDbContext context, ILogger<QualityProcessTrackingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<QualityProcessTrackingDto>> GetPagedAsync(QueryParams query)
    {
        // 1. 基础查询：MaterialReceiveCheck JOIN ProductionBatch（不含子查询）
        var baseQuery = from rc in _context.MaterialReceiveChecks
                        join pb in _context.ProductionBatches on rc.ProductionBatchId equals pb.Id
                        select new BaseRow
                        {
                            Id = rc.Id,
                            ProductionBatchId = rc.ProductionBatchId,
                            BatchNo = rc.BatchNo ?? pb.BatchNo,
                            ManufacturingItem = rc.ManufacturingItem,
                            TagNo = rc.TagNo,
                            WorkOrderNo = rc.WorkOrderNo,
                            SalesOrderNo = rc.SalesOrderNo,
                            SourceUnit = rc.SourceUnit,
                            FurnaceNo = rc.FurnaceNo,
                            PlantGrade = rc.PlantGrade,
                            Specification = rc.Specification,
                            ProductionType = pb.ProductionType,
                            LengthStatus = rc.LengthStatus,
                            ProductionWeight = rc.ProductionWeight,
                            IsForceCompleted = rc.IsForceCompleted,
                            Salesman = rc.Salesman ?? pb.Salesman,
                            DeliveryState = rc.DeliveryState ?? pb.DeliveryState,
                            ReceiveDate = rc.ReceiveDate,
                            Shift = rc.Shift,
                            Checker = rc.Checker,
                            CreatedTime = rc.CreatedTime,
                            UpdatedTime = rc.UpdatedTime,
                            PbBatchNo = pb.BatchNo
                        };

        // 2. 关键词搜索
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            baseQuery = baseQuery.Where(x =>
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

        // 3. 计数
        var totalCount = await baseQuery.CountAsync();

        // 4. 排序
        var sortBy = query.SortBy?.ToLower();
        IOrderedQueryable<BaseRow> orderedQuery;
        switch (sortBy)
        {
            case "batchno":
                orderedQuery = query.IsDescending
                    ? baseQuery.OrderByDescending(q => q.BatchNo ?? "")
                    : baseQuery.OrderBy(q => q.BatchNo ?? "");
                break;
            case "manufacturingitem":
                orderedQuery = query.IsDescending
                    ? baseQuery.OrderByDescending(q => q.ManufacturingItem ?? "")
                    : baseQuery.OrderBy(q => q.ManufacturingItem ?? "");
                break;
            case "plantgrade":
                orderedQuery = query.IsDescending
                    ? baseQuery.OrderByDescending(q => q.PlantGrade ?? "")
                    : baseQuery.OrderBy(q => q.PlantGrade ?? "");
                break;
            case "specification":
                orderedQuery = query.IsDescending
                    ? baseQuery.OrderByDescending(q => q.Specification ?? "")
                    : baseQuery.OrderBy(q => q.Specification ?? "");
                break;
            case "checker":
                orderedQuery = query.IsDescending
                    ? baseQuery.OrderByDescending(q => q.Checker ?? "")
                    : baseQuery.OrderBy(q => q.Checker ?? "");
                break;
            case "shift":
                orderedQuery = query.IsDescending
                    ? baseQuery.OrderByDescending(q => q.Shift ?? "")
                    : baseQuery.OrderBy(q => q.Shift ?? "");
                break;
            case "salesman":
                orderedQuery = query.IsDescending
                    ? baseQuery.OrderByDescending(q => q.Salesman ?? "")
                    : baseQuery.OrderBy(q => q.Salesman ?? "");
                break;
            case "deliverystate":
                orderedQuery = query.IsDescending
                    ? baseQuery.OrderByDescending(q => q.DeliveryState ?? "")
                    : baseQuery.OrderBy(q => q.DeliveryState ?? "");
                break;
            default:
                orderedQuery = baseQuery.OrderByDescending(q => q.ReceiveDate);
                break;
        }

        // 5. 分页（仅返回当前页的基础数据）
        var pageBase = await orderedQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        // 6. 预查询关联数据
        var batchIds = pageBase.Select(x => x.ProductionBatchId).Distinct().ToList();
        var pbBatchNos = pageBase.Select(x => x.PbBatchNo).Where(x => x != null).Distinct().ToList()!;

        // 6a. FinalInspections 按 ProductionBatchId 分组
        var inspections = batchIds.Count > 0
            ? await _context.FinalInspections
                .Where(fi => batchIds.Contains(fi.ProductionBatchId))
                .AsNoTracking()
                .ToListAsync()
            : new List<FinalInspection>();

        var inspectionLookup = inspections
            .GroupBy(fi => fi.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 6b. InventoryBatches 按 ProductionBatchNo 分组
        var inventoryBatches = pbBatchNos.Count > 0
            ? await _context.InventoryBatches
                .Where(ib => ib.ProductionBatchNo != null && pbBatchNos.Contains(ib.ProductionBatchNo))
                .AsNoTracking()
                .ToListAsync()
            : new List<InventoryBatch>();

        var inventoryLookup = inventoryBatches
            .Where(ib => ib.ProductionBatchNo != null)
            .GroupBy(ib => ib.ProductionBatchNo!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 6c. ProductionRecords（仅 Cut 工段）
        var productionRecords = batchIds.Count > 0
            ? await _context.ProductionRecords
                .Where(pr => batchIds.Contains(pr.ProductionBatchId) && pr.SectionName == SectionDefs.Cut && pr.IsFinished)
                .AsNoTracking()
                .ToListAsync()
            : new List<ProductionRecord>();

        var prLookup = productionRecords
            .GroupBy(pr => pr.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 7. 内存映射到 DTO（含聚合计算）
        var items = pageBase.Select(baseRow =>
        {
            var inspList = inspectionLookup.GetValueOrDefault(baseRow.ProductionBatchId, new());
            var invList = inventoryLookup.GetValueOrDefault(baseRow.PbBatchNo ?? "", new());
            var prList = prLookup.GetValueOrDefault(baseRow.ProductionBatchId, new());

            return MapToDto(baseRow, inspList, invList, prList);
        }).ToList();

        // 8. 应用列筛选（内存中）
        if (query.Filters is { Count: > 0 })
        {
            // 由于 DTO 是内存对象，需要显式过滤
            var filtered = items.AsEnumerable();
            foreach (var filter in query.Filters)
            {
                filtered = filtered.Where(item =>
                {
                    var prop = typeof(QualityProcessTrackingDto).GetProperty(filter.Field);
                    if (prop == null) return true;
                    var val = prop.GetValue(item)?.ToString();
                    if (filter.Values == null || filter.Values.Count == 0) return true;
                    if (val == null) return filter.Values.Contains("__EXCEL_FILTER_NULL__");
                    return filter.Values.Contains(val);
                });
            }
            items = filtered.ToList();
        }

        return new PagedResult<QualityProcessTrackingDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    private static QualityProcessTrackingDto MapToDto(
        BaseRow row,
        List<FinalInspection> inspections,
        List<InventoryBatch> inventoryBatches,
        List<ProductionRecord> productionRecords)
    {
        // G5: 执行状态
        string qualityStatus;
        if (inspections.Count > 0)
            qualityStatus = inventoryBatches.Count > 0 ? "完成检验" : "检验中";
        else
            qualityStatus = "待检验";

        return new QualityProcessTrackingDto
        {
            // G1
            Id = row.Id,
            ProductionBatchId = row.ProductionBatchId,
            BatchNo = row.BatchNo,
            ManufacturingItem = row.ManufacturingItem,
            TagNo = row.TagNo,
            WorkOrderNo = row.WorkOrderNo,
            SalesOrderNo = row.SalesOrderNo,
            SourceUnit = row.SourceUnit,
            FurnaceNo = row.FurnaceNo,
            PlantGrade = row.PlantGrade,
            Specification = row.Specification,
            ProductionType = row.ProductionType,
            LengthStatus = row.LengthStatus,
            ProductionWeight = row.ProductionWeight,
            IsForceCompleted = row.IsForceCompleted,
            Salesman = row.Salesman,
            DeliveryState = row.DeliveryState,
            ReceiveDate = row.ReceiveDate,
            Shift = row.Shift,
            Checker = row.Checker,
            CreatedTime = row.CreatedTime,
            UpdatedTime = row.UpdatedTime,

            // G2: 检验日期（按 InspectionItem 拆分）
            PmiDate = GetInspectionDate(inspections, InspectionItem.PMIInspection),
            VisualDate = GetInspectionDate(inspections, InspectionItem.VisualInspection),
            DimensionDate = GetInspectionDate(inspections, InspectionItem.Dimension),
            EndoscopyDate = GetInspectionDate(inspections, InspectionItem.Endoscopy),
            HydroDate = GetInspectionDate(inspections, InspectionItem.HydrostaticPressure),
            UnderwaterPneumaticDate = GetInspectionDate(inspections, InspectionItem.UnderwaterPneumatic),
            EddyCurrentDate = GetInspectionDate(inspections, InspectionItem.EddyCurrent),
            UltrasonicDate = GetInspectionDate(inspections, InspectionItem.Ultrasonic),
            PortColoringDate = GetInspectionDate(inspections, InspectionItem.PortColoring),

            // G3: 检验汇总
            ProductionCutQuantity = productionRecords.Sum(pr => pr.PostCutQuantity ?? 0),
            InspectionCount = inspections.Select(fi => fi.InspectionItem).Distinct().Count(),
            TotalQuantity = inspections.Max(fi => (int?)(fi.Quantity ?? 0)) ?? 0,
            QualifiedQuantity = inspections.Min(fi => (int?)(fi.QualifiedQuantity ?? 0)) ?? 0,
            DefectReworkQuantity = inspections.Sum(fi => fi.DefectReworkQuantity ?? 0),
            DefectWarehouseQuantity = inspections.Sum(fi => fi.DefectWarehouseQuantity ?? 0),
            DefectScrapQuantity = inspections.Sum(fi => fi.DefectScrapQuantity ?? 0),
            MaxInspectionDate = inspections.Max(fi => (DateTime?)fi.InspectionDate),

            // G4: 成品入库
            InboundQuantity = inventoryBatches.Sum(ib => ib.InitialQuantity),
            InboundWeight = inventoryBatches.Sum(ib => (decimal?)ib.InitialWeight),
            InboundDate = inventoryBatches.Max(ib => (DateTime?)ib.InboundDate),

            // G5
            QualityStatus = qualityStatus
        };
    }

    private static DateTime? GetInspectionDate(List<FinalInspection> inspections, InspectionItem item)
    {
        return inspections
            .Where(fi => fi.InspectionItem == item)
            .Max(fi => (DateTime?)fi.InspectionDate);
    }

    /// <summary>
    /// 基础行投影（不含子查询的轻量 JOIN）
    /// </summary>
    private class BaseRow
    {
        public int Id { get; set; }
        public int ProductionBatchId { get; set; }
        public string? BatchNo { get; set; }
        public string? ManufacturingItem { get; set; }
        public string? TagNo { get; set; }
        public string? WorkOrderNo { get; set; }
        public string? SalesOrderNo { get; set; }
        public string? SourceUnit { get; set; }
        public string? FurnaceNo { get; set; }
        public string? PlantGrade { get; set; }
        public string? Specification { get; set; }
        public string? ProductionType { get; set; }
        public string? LengthStatus { get; set; }
        public decimal? ProductionWeight { get; set; }
        public bool IsForceCompleted { get; set; }
        public string? Salesman { get; set; }
        public string? DeliveryState { get; set; }
        public DateTime ReceiveDate { get; set; }
        public string? Shift { get; set; }
        public string? Checker { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public DateTimeOffset UpdatedTime { get; set; }
        public string? PbBatchNo { get; set; }
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var dict = new Dictionary<string, List<string>>();

        // 逐列数据库级 DISTINCT（各列均直接来自 MaterialReceiveCheck，无需 JOIN ProductionBatch）
        dict["BatchNo"] = await _context.MaterialReceiveChecks
            .Where(m => m.BatchNo != null).Select(m => m.BatchNo!).Distinct().OrderBy(x => x).ToListAsync();
        dict["ManufacturingItem"] = await _context.MaterialReceiveChecks
            .Where(m => m.ManufacturingItem != null).Select(m => m.ManufacturingItem!).Distinct().OrderBy(x => x).ToListAsync();
        dict["PlantGrade"] = await _context.MaterialReceiveChecks
            .Where(m => m.PlantGrade != null).Select(m => m.PlantGrade!).Distinct().OrderBy(x => x).ToListAsync();
        dict["Specification"] = await _context.MaterialReceiveChecks
            .Where(m => m.Specification != null).Select(m => m.Specification!).Distinct().OrderBy(x => x).ToListAsync();
        dict["Shift"] = await _context.MaterialReceiveChecks
            .Where(m => m.Shift != null).Select(m => m.Shift!).Distinct().OrderBy(x => x).ToListAsync();
        dict["Checker"] = await _context.MaterialReceiveChecks
            .Where(m => m.Checker != null).Select(m => m.Checker!).Distinct().OrderBy(x => x).ToListAsync();
        dict["FurnaceNo"] = await _context.MaterialReceiveChecks
            .Where(m => m.FurnaceNo != null).Select(m => m.FurnaceNo!).Distinct().OrderBy(x => x).ToListAsync();
        dict["WorkOrderNo"] = await _context.MaterialReceiveChecks
            .Where(m => m.WorkOrderNo != null).Select(m => m.WorkOrderNo!).Distinct().OrderBy(x => x).ToListAsync();
        dict["SalesOrderNo"] = await _context.MaterialReceiveChecks
            .Where(m => m.SalesOrderNo != null).Select(m => m.SalesOrderNo!).Distinct().OrderBy(x => x).ToListAsync();
        dict["SourceUnit"] = await _context.MaterialReceiveChecks
            .Where(m => m.SourceUnit != null).Select(m => m.SourceUnit!).Distinct().OrderBy(x => x).ToListAsync();
        dict["Salesman"] = await _context.MaterialReceiveChecks
            .Where(m => m.Salesman != null).Select(m => m.Salesman!).Distinct().OrderBy(x => x).ToListAsync();
        dict["DeliveryState"] = await _context.MaterialReceiveChecks
            .Where(m => m.DeliveryState != null).Select(m => m.DeliveryState!).Distinct().OrderBy(x => x).ToListAsync();
        dict["QualityStatus"] = new List<string> { "待检验", "检验中", "完成检验" };

        return dict;
    }
}
