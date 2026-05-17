using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// 过程检验服务实现
/// </summary>
public class ProcessInspectionService : IProcessInspectionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProcessInspectionService> _logger;
    private readonly IProductionRecordService _productionRecordService;

    public ProcessInspectionService(
        AppDbContext context,
        ILogger<ProcessInspectionService> logger,
        IProductionRecordService productionRecordService)
    {
        _context = context;
        _logger = logger;
        _productionRecordService = productionRecordService;
    }

    public async Task<PagedResult<ProcessInspectionDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.ProcessInspections
            .AsNoTracking()
            .Include(r => r.ProductionBatch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            queryable = queryable.Where(r => r.ProductionBatch.BatchNo.Contains(query.Keyword)
                || r.ProcessName.Contains(query.Keyword)
                || r.SectionName.Contains(query.Keyword)
                || (r.ManufacturingSpec != null && r.ManufacturingSpec.Contains(query.Keyword))
                || (r.EquipmentName != null && r.EquipmentName.Contains(query.Keyword))
                || (r.Inspector != null && r.Inspector.Contains(query.Keyword))
                || (r.Shift != null && r.Shift.Contains(query.Keyword))
                || (r.InspectionItem != null && r.InspectionItem.Contains(query.Keyword))
                || (r.DefectDescription != null && r.DefectDescription.Contains(query.Keyword))
                || (r.SourceUnit != null && r.SourceUnit.Contains(query.Keyword))
                || (r.TagNo != null && r.TagNo.Contains(query.Keyword))
                || (r.PlantGrade != null && r.PlantGrade.Contains(query.Keyword))
                || (r.Remark != null && r.Remark.Contains(query.Keyword)));
        }

        if (query.InspectionDateFrom.HasValue)
            queryable = queryable.Where(r => r.InspectionDate >= query.InspectionDateFrom.Value);

        if (query.InspectionDateTo.HasValue)
            queryable = queryable.Where(r => r.InspectionDate <= query.InspectionDateTo.Value);

        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.SortBy ?? "createdtime", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new ProcessInspectionDto
            {
                Id = r.Id,
                ProductionBatchId = r.ProductionBatchId,
                ProcessGroupId = r.ProcessGroupId,
                ProcessName = r.ProcessName,
                ManufacturingSpec = r.ManufacturingSpec,
                SectionName = r.SectionName,
                SequenceNumber = r.SequenceNumber,
                InspectionDate = r.InspectionDate,
                EquipmentName = r.EquipmentName,
                Inspector = r.Inspector,
                Shift = r.Shift,
                Quantity = r.Quantity,
                Weight = r.Weight,
                InspectionItem = r.InspectionItem,
                QualifiedQuantity = r.QualifiedQuantity,
                QualifiedWeight = r.QualifiedWeight,
                DefectReworkQuantity = r.DefectReworkQuantity,
                DefectWarehouseQuantity = r.DefectWarehouseQuantity,
                DefectScrapQuantity = r.DefectScrapQuantity,
                DefectDescription = r.DefectDescription,
                SourceUnit = r.SourceUnit,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                Remark = r.Remark,
                BatchNo = r.ProductionBatch.BatchNo,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            })
            .ToListAsync();

        return new PagedResult<ProcessInspectionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<ProcessInspectionDto>> BatchCreateAsync(List<CreateProcessInspectionRequest> requests)
    {
        if (requests.Count == 0)
            return new List<ProcessInspectionDto>();

        // 预加载所有涉及的批次
        var batchNos = requests.Select(r => r.BatchNo).Distinct().ToList();
        var batchLookup = await _context.ProductionBatches
            .Where(b => batchNos.Contains(b.BatchNo))
            .ToDictionaryAsync(b => b.BatchNo);
        foreach (var bn in batchNos)
        {
            if (!batchLookup.ContainsKey(bn))
                throw new BusinessException($"批次不存在: {bn}");
        }

        // 预加载所有涉及批次的工序组（用于 ProcessGroupId 解析）
        var allBatchIds = batchLookup.Values.Select(b => b.Id).ToList();
        var processGroups = await _context.ProcessGroups
            .Where(pg => allBatchIds.Contains(pg.ProductionBatchId))
            .ToListAsync();
        var pgByBatch = processGroups.GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var entities = new List<ProcessInspection>();
        var errors = new List<string>();

        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var batch = batchLookup[request.BatchNo];
            var batchId = batch.Id;

            // 解析 ProcessGroupId
            int? pgId = request.ProcessGroupId;
            if (pgId == null || pgId == 0)
            {
                var matchedPg = pgByBatch.GetValueOrDefault(batchId)?
                    .FirstOrDefault(pg => pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec);
                pgId = matchedPg?.Id;
            }

            // 解析 SequenceNumber
            int seqNum = request.SequenceNumber;
            if (seqNum == 0 && pgId > 0)
            {
                var pg = pgByBatch.GetValueOrDefault(batchId)?.FirstOrDefault(p => p.Id == pgId);
                if (pg != null)
                {
                    seqNum = request.SectionName switch
                    {
                        "冷轧拔" => pg.ColdRollDraw ?? 0,
                        "切管" => pg.OilPipeCut ?? 0,
                        "脱脂" => pg.Degrease ?? 0,
                        "固溶" => pg.Solution ?? 0,
                        "矫直" => pg.Straighten ?? 0,
                        "断切" => pg.Cut ?? 0,
                        "测厚" => pg.ThicknessMeasure ?? 0,
                        "酸洗" => pg.Pickle ?? 0,
                        "外抛" => pg.OuterPolish ?? 0,
                        "内磨" => pg.InnerGrinding ?? 0,
                        "外点磨" => pg.OuterSpotGrinding ?? 0,
                        "探伤" => pg.Inspection ?? 0,
                        "检验" => pg.Inspection ?? 0,
                        "焊头" => pg.WeldingHead ?? 0,
                        "润滑" => pg.Lubrication ?? 0,
                        "入库" => pg.Warehouse ?? 0,
                        _ => 0
                    };
                }
            }

            var entity = new ProcessInspection
            {
                ProductionBatchId = batchId,
                ProcessGroupId = pgId ?? 0,
                ProcessName = request.ProcessName,
                ManufacturingSpec = request.ManufacturingSpec,
                SectionName = request.SectionName,
                SequenceNumber = seqNum,
                InspectionDate = request.InspectionDate,
                EquipmentName = request.EquipmentName,
                Inspector = request.Inspector,
                Shift = request.Shift,
                Quantity = request.Quantity,
                Weight = request.Weight,
                InspectionItem = request.InspectionItem,
                QualifiedQuantity = request.QualifiedQuantity,
                QualifiedWeight = request.QualifiedWeight,
                DefectReworkQuantity = request.DefectReworkQuantity,
                DefectWarehouseQuantity = request.DefectWarehouseQuantity,
                DefectScrapQuantity = request.DefectScrapQuantity,
                DefectDescription = request.DefectDescription,
                SourceUnit = request.SourceUnit,
                TagNo = request.TagNo,
                PlantGrade = request.PlantGrade,
                Remark = request.Remark
            };

            entities.Add(entity);
        }

        if (errors.Any())
            throw new BusinessException(string.Join("；", errors));

        _context.ProcessInspections.AddRange(entities);
        await _context.SaveChangesAsync();

        // 刷新涉及批次的跟踪字段
        var batchIds = entities.Select(e => e.ProductionBatchId).Distinct().ToList();
        foreach (var bid in batchIds)
        {
            await _productionRecordService.RefreshBatchTrackingFieldsAsync(bid);
        }

        return entities.Select(e => new ProcessInspectionDto
        {
            Id = e.Id,
            ProductionBatchId = e.ProductionBatchId,
            ProcessGroupId = e.ProcessGroupId,
            ProcessName = e.ProcessName,
            ManufacturingSpec = e.ManufacturingSpec,
            SectionName = e.SectionName,
            SequenceNumber = e.SequenceNumber,
            InspectionDate = e.InspectionDate,
            EquipmentName = e.EquipmentName,
            Inspector = e.Inspector,
            Shift = e.Shift,
            Quantity = e.Quantity,
            Weight = e.Weight,
            InspectionItem = e.InspectionItem,
            QualifiedQuantity = e.QualifiedQuantity,
            QualifiedWeight = e.QualifiedWeight,
            DefectReworkQuantity = e.DefectReworkQuantity,
            DefectWarehouseQuantity = e.DefectWarehouseQuantity,
            DefectScrapQuantity = e.DefectScrapQuantity,
            DefectDescription = e.DefectDescription,
            SourceUnit = e.SourceUnit,
            TagNo = e.TagNo,
            PlantGrade = e.PlantGrade,
            Remark = e.Remark,
            BatchNo = batchLookup.Values.FirstOrDefault(b => b.Id == e.ProductionBatchId)?.BatchNo,
            CreatedTime = e.CreatedTime,
            UpdatedTime = e.UpdatedTime
        }).ToList();
    }

    public async Task<ProcessInspectionDto> UpdateAsync(int id, UpdateProcessInspectionRequest request)
    {
        var entity = await _context.ProcessInspections.FindAsync(id)
            ?? throw new BusinessException($"过程检验记录不存在(Id={id})");

        entity.InspectionDate = request.InspectionDate;
        entity.EquipmentName = request.EquipmentName ?? entity.EquipmentName;
        entity.Inspector = request.Inspector ?? entity.Inspector;
        entity.Shift = request.Shift ?? entity.Shift;
        entity.Quantity = request.Quantity ?? entity.Quantity;
        entity.Weight = request.Weight ?? entity.Weight;
        entity.InspectionItem = request.InspectionItem ?? entity.InspectionItem;
        entity.QualifiedQuantity = request.QualifiedQuantity ?? entity.QualifiedQuantity;
        entity.QualifiedWeight = request.QualifiedWeight ?? entity.QualifiedWeight;
        entity.DefectReworkQuantity = request.DefectReworkQuantity ?? entity.DefectReworkQuantity;
        entity.DefectWarehouseQuantity = request.DefectWarehouseQuantity ?? entity.DefectWarehouseQuantity;
        entity.DefectScrapQuantity = request.DefectScrapQuantity ?? entity.DefectScrapQuantity;
        entity.DefectDescription = request.DefectDescription ?? entity.DefectDescription;
        entity.SourceUnit = request.SourceUnit ?? entity.SourceUnit;
        entity.TagNo = request.TagNo ?? entity.TagNo;
        entity.PlantGrade = request.PlantGrade ?? entity.PlantGrade;
        entity.Remark = request.Remark ?? entity.Remark;

        await _context.SaveChangesAsync();

        // 重新加载以获取批次号
        await _context.Entry(entity).Reference(e => e.ProductionBatch).LoadAsync();

        // 刷新批次跟踪字段
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(entity.ProductionBatchId);

        return new ProcessInspectionDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            ProcessGroupId = entity.ProcessGroupId,
            ProcessName = entity.ProcessName,
            ManufacturingSpec = entity.ManufacturingSpec,
            SectionName = entity.SectionName,
            SequenceNumber = entity.SequenceNumber,
            InspectionDate = entity.InspectionDate,
            EquipmentName = entity.EquipmentName,
            Inspector = entity.Inspector,
            Shift = entity.Shift,
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            InspectionItem = entity.InspectionItem,
            QualifiedQuantity = entity.QualifiedQuantity,
            QualifiedWeight = entity.QualifiedWeight,
            DefectReworkQuantity = entity.DefectReworkQuantity,
            DefectWarehouseQuantity = entity.DefectWarehouseQuantity,
            DefectScrapQuantity = entity.DefectScrapQuantity,
            DefectDescription = entity.DefectDescription,
            SourceUnit = entity.SourceUnit,
            TagNo = entity.TagNo,
            PlantGrade = entity.PlantGrade,
            Remark = entity.Remark,
            BatchNo = entity.ProductionBatch.BatchNo,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.ProcessInspections.FindAsync(id)
            ?? throw new BusinessException($"过程检验记录不存在(Id={id})");

        _context.ProcessInspections.Remove(entity);
        await _context.SaveChangesAsync();

        // 刷新批次跟踪字段
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(entity.ProductionBatchId);
    }

    private static IQueryable<ProcessInspection> ApplySorting(IQueryable<ProcessInspection> queryable, string sortBy, bool isDescending)
    {
        return (sortBy.ToLower(), isDescending) switch
        {
            ("batchno", false) => queryable.OrderBy(r => r.ProductionBatch.BatchNo),
            ("batchno", true) => queryable.OrderByDescending(r => r.ProductionBatch.BatchNo),
            ("processname", false) => queryable.OrderBy(r => r.ProcessName),
            ("processname", true) => queryable.OrderByDescending(r => r.ProcessName),
            ("sectionname", false) => queryable.OrderBy(r => r.SectionName),
            ("sectionname", true) => queryable.OrderByDescending(r => r.SectionName),
            ("inspectiondate", false) => queryable.OrderBy(r => r.InspectionDate),
            ("inspectiondate", true) => queryable.OrderByDescending(r => r.InspectionDate),
            ("sequencenumber", false) => queryable.OrderBy(r => r.SequenceNumber),
            ("sequencenumber", true) => queryable.OrderByDescending(r => r.SequenceNumber),
            ("equipmentname", false) => queryable.OrderBy(r => r.EquipmentName ?? ""),
            ("equipmentname", true) => queryable.OrderByDescending(r => r.EquipmentName ?? ""),
            ("inspector", false) => queryable.OrderBy(r => r.Inspector ?? ""),
            ("inspector", true) => queryable.OrderByDescending(r => r.Inspector ?? ""),
            ("quantity", false) => queryable.OrderBy(r => r.Quantity ?? 0),
            ("quantity", true) => queryable.OrderByDescending(r => r.Quantity ?? 0),
            ("weight", false) => queryable.OrderBy(r => r.Weight ?? 0),
            ("weight", true) => queryable.OrderByDescending(r => r.Weight ?? 0),
            ("qualifiedquantity", false) => queryable.OrderBy(r => r.QualifiedQuantity ?? 0),
            ("qualifiedquantity", true) => queryable.OrderByDescending(r => r.QualifiedQuantity ?? 0),
            ("qualifiedweight", false) => queryable.OrderBy(r => r.QualifiedWeight ?? 0),
            ("qualifiedweight", true) => queryable.OrderByDescending(r => r.QualifiedWeight ?? 0),
            ("defectreworkquantity", false) => queryable.OrderBy(r => r.DefectReworkQuantity ?? 0),
            ("defectreworkquantity", true) => queryable.OrderByDescending(r => r.DefectReworkQuantity ?? 0),
            ("defectwarehousequantity", false) => queryable.OrderBy(r => r.DefectWarehouseQuantity ?? 0),
            ("defectwarehousequantity", true) => queryable.OrderByDescending(r => r.DefectWarehouseQuantity ?? 0),
            ("defectscrapquantity", false) => queryable.OrderBy(r => r.DefectScrapQuantity ?? 0),
            ("defectscrapquantity", true) => queryable.OrderByDescending(r => r.DefectScrapQuantity ?? 0),
            ("manufacturingspec", false) => queryable.OrderBy(r => r.ManufacturingSpec ?? ""),
            ("manufacturingspec", true) => queryable.OrderByDescending(r => r.ManufacturingSpec ?? ""),
            ("shift", false) => queryable.OrderBy(r => r.Shift ?? ""),
            ("shift", true) => queryable.OrderByDescending(r => r.Shift ?? ""),
            ("inspectionitem", false) => queryable.OrderBy(r => r.InspectionItem ?? ""),
            ("inspectionitem", true) => queryable.OrderByDescending(r => r.InspectionItem ?? ""),
            ("defectdescription", false) => queryable.OrderBy(r => r.DefectDescription ?? ""),
            ("defectdescription", true) => queryable.OrderByDescending(r => r.DefectDescription ?? ""),
            ("sourceunit", false) => queryable.OrderBy(r => r.SourceUnit ?? ""),
            ("sourceunit", true) => queryable.OrderByDescending(r => r.SourceUnit ?? ""),
            ("tagno", false) => queryable.OrderBy(r => r.TagNo ?? ""),
            ("tagno", true) => queryable.OrderByDescending(r => r.TagNo ?? ""),
            ("plantgrade", false) => queryable.OrderBy(r => r.PlantGrade ?? ""),
            ("plantgrade", true) => queryable.OrderByDescending(r => r.PlantGrade ?? ""),
            ("remark", false) => queryable.OrderBy(r => r.Remark ?? ""),
            ("remark", true) => queryable.OrderByDescending(r => r.Remark ?? ""),
            ("createdtime", false) => queryable.OrderBy(r => r.CreatedTime),
            ("createdtime", true) => queryable.OrderByDescending(r => r.CreatedTime),
            ("updatedtime", false) => queryable.OrderBy(r => r.UpdatedTime),
            ("updatedtime", true) => queryable.OrderByDescending(r => r.UpdatedTime),
            _ => isDescending
                ? queryable.OrderByDescending(r => r.CreatedTime)
                : queryable.OrderBy(r => r.CreatedTime)
        };
    }
}
