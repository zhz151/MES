using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Constants;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Services.Helpers;

namespace MES.Services;

/// <summary>
/// 质量过程跟踪服务（成检到料 → 成品检验 → 成品入库 联通表）
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
        // 1. 基础查询：MaterialReceiveCheck JOIN ProductionBatch
        var baseQuery = from rc in _context.MaterialReceiveChecks
                        join pb in _context.ProductionBatches on rc.ProductionBatchId equals pb.Id
                        select new { rc, pb };

        // 2. 关键词搜索（Select 前过滤）
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            baseQuery = baseQuery.Where(x =>
                (x.rc.BatchNo != null && x.rc.BatchNo.Contains(kw)) ||
                (x.rc.ManufacturingItem != null && x.rc.ManufacturingItem.Contains(kw)) ||
                (x.rc.PlantGrade != null && x.rc.PlantGrade.Contains(kw)) ||
                (x.rc.Specification != null && x.rc.Specification.Contains(kw)) ||
                (x.rc.Checker != null && x.rc.Checker.Contains(kw)) ||
                (x.rc.FurnaceNo != null && x.rc.FurnaceNo.Contains(kw)) ||
                (x.rc.WorkOrderNo != null && x.rc.WorkOrderNo.Contains(kw)) ||
                (x.rc.SalesOrderNo != null && x.rc.SalesOrderNo.Contains(kw)) ||
                (x.rc.TagNo != null && x.rc.TagNo.Contains(kw)) ||
                (x.rc.SourceUnit != null && x.rc.SourceUnit.Contains(kw))
            );
        }

        // 3. 计数（Select 前轻量计数）
        var totalCount = await baseQuery.CountAsync();

        // 4. 排序在 Select 投影后通过 switch expression 处理
        //    先投影到 DTO，再排序和分页

        // 5. 投影到 DTO（相关子查询）
        var dtoQuery = baseQuery.Select(x => new QualityProcessTrackingDto
        {
            // G1: 批次信息
            Id = x.rc.Id,
            ProductionBatchId = x.rc.ProductionBatchId,
            BatchNo = x.rc.BatchNo ?? x.pb.BatchNo,
            ManufacturingItem = x.rc.ManufacturingItem,
            TagNo = x.rc.TagNo,
            WorkOrderNo = x.rc.WorkOrderNo,
            SalesOrderNo = x.rc.SalesOrderNo,
            SourceUnit = x.rc.SourceUnit,
            FurnaceNo = x.rc.FurnaceNo,
            PlantGrade = x.rc.PlantGrade,
            Specification = x.rc.Specification,
            ProductionType = x.pb.ProductionType,
            ReceiveDate = x.rc.ReceiveDate,
            Shift = x.rc.Shift,
            Checker = x.rc.Checker,
            CreatedTime = x.rc.CreatedTime,
            UpdatedTime = x.rc.UpdatedTime,

            // G2: 检验日期（按 InspectionItem 拆分）
            PmiDate = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id && fi.InspectionItem == InspectionItem.PMIInspection)
                .Max(fi => (DateTime?)fi.InspectionDate),
            VisualDate = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id && fi.InspectionItem == InspectionItem.VisualInspection)
                .Max(fi => (DateTime?)fi.InspectionDate),
            DimensionDate = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id && fi.InspectionItem == InspectionItem.Dimension)
                .Max(fi => (DateTime?)fi.InspectionDate),
            EndoscopyDate = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id && fi.InspectionItem == InspectionItem.Endoscopy)
                .Max(fi => (DateTime?)fi.InspectionDate),
            HydroDate = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id && fi.InspectionItem == InspectionItem.HydrostaticPressure)
                .Max(fi => (DateTime?)fi.InspectionDate),
            UnderwaterPneumaticDate = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id && fi.InspectionItem == InspectionItem.UnderwaterPneumatic)
                .Max(fi => (DateTime?)fi.InspectionDate),
            EddyCurrentDate = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id && fi.InspectionItem == InspectionItem.EddyCurrent)
                .Max(fi => (DateTime?)fi.InspectionDate),
            UltrasonicDate = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id && fi.InspectionItem == InspectionItem.Ultrasonic)
                .Max(fi => (DateTime?)fi.InspectionDate),
            PortColoringDate = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id && fi.InspectionItem == InspectionItem.PortColoring)
                .Max(fi => (DateTime?)fi.InspectionDate),

            // G3: 检验汇总
            ProductionCutQuantity = _context.ProductionRecords
                .Where(pr => pr.ProductionBatchId == x.pb.Id && pr.SectionName == SectionDefs.Cut && pr.IsFinished)
                .Sum(pr => (int?)(pr.PostCutQuantity ?? 0)) ?? 0,
            InspectionCount = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id)
                .Select(fi => fi.InspectionItem)
                .Distinct()
                .Count(),
            TotalQuantity = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id)
                .Max(fi => (int?)(fi.Quantity ?? 0)) ?? 0,
            QualifiedQuantity = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id)
                .Min(fi => (int?)(fi.QualifiedQuantity ?? 0)) ?? 0,
            DefectReworkQuantity = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id)
                .Sum(fi => (int?)(fi.DefectReworkQuantity ?? 0)) ?? 0,
            DefectWarehouseQuantity = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id)
                .Sum(fi => (int?)(fi.DefectWarehouseQuantity ?? 0)) ?? 0,
            DefectScrapQuantity = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id)
                .Sum(fi => (int?)(fi.DefectScrapQuantity ?? 0)) ?? 0,
            MaxInspectionDate = _context.FinalInspections
                .Where(fi => fi.ProductionBatchId == x.pb.Id)
                .Max(fi => (DateTime?)fi.InspectionDate),

            // G4: 成品入库
            InboundQuantity = _context.InventoryBatches
                .Where(ib => ib.ProductionBatchNo == x.pb.BatchNo)
                .Sum(ib => (int?)ib.InitialQuantity) ?? 0,
            InboundWeight = _context.InventoryBatches
                .Where(ib => ib.ProductionBatchNo == x.pb.BatchNo)
                .Sum(ib => (decimal?)ib.InitialWeight),
            InboundDate = _context.InventoryBatches
                .Where(ib => ib.ProductionBatchNo == x.pb.BatchNo)
                .Max(ib => (DateTime?)ib.InboundDate),

            // G5: 执行状态（CASE WHEN）
            QualityStatus = _context.FinalInspections.Any(fi => fi.ProductionBatchId == x.pb.Id)
                ? (_context.InventoryBatches.Any(ib => ib.ProductionBatchNo == x.pb.BatchNo)
                    ? "完成检验"
                    : "检验中")
                : "待检验"
        });

        // 6. 排序
        var sortBy = query.SortBy?.ToLower();
        dtoQuery = (sortBy, query.IsDescending) switch
        {
            ("batchno", false) => dtoQuery.OrderBy(q => q.BatchNo ?? ""),
            ("batchno", true) => dtoQuery.OrderByDescending(q => q.BatchNo ?? ""),
            ("manufacturingitem", false) => dtoQuery.OrderBy(q => q.ManufacturingItem ?? ""),
            ("manufacturingitem", true) => dtoQuery.OrderByDescending(q => q.ManufacturingItem ?? ""),
            ("plantgrade", false) => dtoQuery.OrderBy(q => q.PlantGrade ?? ""),
            ("plantgrade", true) => dtoQuery.OrderByDescending(q => q.PlantGrade ?? ""),
            ("specification", false) => dtoQuery.OrderBy(q => q.Specification ?? ""),
            ("specification", true) => dtoQuery.OrderByDescending(q => q.Specification ?? ""),
            ("receivedate", false) => dtoQuery.OrderBy(q => q.ReceiveDate),
            ("receivedate", true) => dtoQuery.OrderByDescending(q => q.ReceiveDate),
            ("checker", false) => dtoQuery.OrderBy(q => q.Checker ?? ""),
            ("checker", true) => dtoQuery.OrderByDescending(q => q.Checker ?? ""),
            ("qualitystatus", false) => dtoQuery.OrderBy(q => q.QualityStatus),
            ("qualitystatus", true) => dtoQuery.OrderByDescending(q => q.QualityStatus),
            ("inbounddate", false) => dtoQuery.OrderBy(q => q.InboundDate),
            ("inbounddate", true) => dtoQuery.OrderByDescending(q => q.InboundDate),
            ("createdtime", false) => dtoQuery.OrderBy(q => q.CreatedTime),
            ("createdtime", true) => dtoQuery.OrderByDescending(q => q.CreatedTime),
            _ => dtoQuery.OrderByDescending(q => q.ReceiveDate)
        };

        // 7. 分页
        dtoQuery = dtoQuery
            .Skip(query.Skip)
            .Take(query.PageSize);

        // 8. 应用列筛选（After Select, before materialization）
        if (query.Filters is { Count: > 0 })
        {
            dtoQuery = dtoQuery.ApplyFilters(query.Filters);
        }

        var items = await dtoQuery.ToListAsync();

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
        var query = from rc in _context.MaterialReceiveChecks
                    join pb in _context.ProductionBatches on rc.ProductionBatchId equals pb.Id
                    select new
                    {
                        rc.BatchNo,
                        rc.ManufacturingItem,
                        rc.PlantGrade,
                        rc.Specification,
                        rc.Shift,
                        rc.Checker,
                        rc.FurnaceNo,
                        rc.WorkOrderNo,
                        rc.SalesOrderNo,
                        rc.SourceUnit
                    };

        var results = await query.AsNoTracking().ToListAsync();

        var dict = new Dictionary<string, List<string>>
        {
            ["BatchNo"] = results.Select(x => x.BatchNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["ManufacturingItem"] = results.Select(x => x.ManufacturingItem).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["PlantGrade"] = results.Select(x => x.PlantGrade).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Specification"] = results.Select(x => x.Specification).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Shift"] = results.Select(x => x.Shift).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Checker"] = results.Select(x => x.Checker).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["FurnaceNo"] = results.Select(x => x.FurnaceNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["SourceUnit"] = results.Select(x => x.SourceUnit).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["QualityStatus"] = new List<string> { "待检验", "检验中", "完成检验" }
        };

        return dict;
    }
}
