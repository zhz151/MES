using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// 成品检验服务实现
/// </summary>
public class FinalInspectionService : IFinalInspectionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FinalInspectionService> _logger;

    public FinalInspectionService(AppDbContext context, ILogger<FinalInspectionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FinalInspectionDto?> GetByIdAsync(int id)
    {
        return await _context.FinalInspections
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new FinalInspectionDto
            {
                Id = r.Id,
                InspectionItem = r.InspectionItem,
                InspectionDate = r.InspectionDate,
                BatchNo = r.BatchNo,
                ProductionBatchId = r.ProductionBatchId,
                MaterialName = r.MaterialName,
                TagNo = r.TagNo,
                WorkOrderNo = r.WorkOrderNo,
                SalesOrderNo = r.SalesOrderNo,
                SourceUnit = r.SourceUnit,
                FurnaceNo = r.FurnaceNo,
                PlantGrade = r.PlantGrade,
                Specification = r.Specification,
                FixedLength = r.FixedLength,
                EquipmentName = r.EquipmentName,
                Shift = r.Shift,
                Operator = r.Operator,
                Quantity = r.Quantity,
                Weight = r.Weight,
                QualifiedQuantity = r.QualifiedQuantity,
                QualifiedWeight = r.QualifiedWeight,
                DefectReworkQuantity = r.DefectReworkQuantity,
                DefectWarehouseQuantity = r.DefectWarehouseQuantity,
                DefectScrapQuantity = r.DefectScrapQuantity,
                DefectDescription = r.DefectDescription,
                OuterDiameterRange = r.OuterDiameterRange,
                WallThicknessRange = r.WallThicknessRange,
                LengthAllowanceRange = r.LengthAllowanceRange,
                Pressure = r.Pressure,
                HoldTime = r.HoldTime,
                Remark = r.Remark,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            })
            .FirstOrDefaultAsync();
    }

    public async Task<PagedResult<FinalInspectionDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.FinalInspections
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                r.BatchNo.Contains(kw) ||
                (r.MaterialName != null && r.MaterialName.Contains(kw)) ||
                (r.PlantGrade != null && r.PlantGrade.Contains(kw)) ||
                (r.Specification != null && r.Specification.Contains(kw)) ||
                (r.TagNo != null && r.TagNo.Contains(kw)) ||
                (r.WorkOrderNo != null && r.WorkOrderNo.Contains(kw)) ||
                (r.SalesOrderNo != null && r.SalesOrderNo.Contains(kw)) ||
                (r.SourceUnit != null && r.SourceUnit.Contains(kw)) ||
                (r.FurnaceNo != null && r.FurnaceNo.Contains(kw)) ||
                (r.FixedLength != null && r.FixedLength.Contains(kw)) ||
                (r.EquipmentName != null && r.EquipmentName.Contains(kw)) ||
                (r.Shift != null && r.Shift.Contains(kw)) ||
                (r.Operator != null && r.Operator.Contains(kw)) ||
                (r.DefectDescription != null && r.DefectDescription.Contains(kw)) ||
                (r.OuterDiameterRange != null && r.OuterDiameterRange.Contains(kw)) ||
                (r.WallThicknessRange != null && r.WallThicknessRange.Contains(kw)) ||
                (r.LengthAllowanceRange != null && r.LengthAllowanceRange.Contains(kw)) ||
                (r.Remark != null && r.Remark.Contains(kw)));
        }

        if (query.InspectionDateFrom.HasValue)
            queryable = queryable.Where(r => r.InspectionDate >= query.InspectionDateFrom.Value);

        if (query.InspectionDateTo.HasValue)
            queryable = queryable.Where(r => r.InspectionDate <= query.InspectionDateTo.Value);

        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.SortBy ?? "inspectiondate", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new FinalInspectionDto
            {
                Id = r.Id,
                InspectionItem = r.InspectionItem,
                InspectionDate = r.InspectionDate,
                BatchNo = r.BatchNo,
                ProductionBatchId = r.ProductionBatchId,
                MaterialName = r.MaterialName,
                TagNo = r.TagNo,
                WorkOrderNo = r.WorkOrderNo,
                SalesOrderNo = r.SalesOrderNo,
                SourceUnit = r.SourceUnit,
                FurnaceNo = r.FurnaceNo,
                PlantGrade = r.PlantGrade,
                Specification = r.Specification,
                FixedLength = r.FixedLength,
                EquipmentName = r.EquipmentName,
                Shift = r.Shift,
                Operator = r.Operator,
                Quantity = r.Quantity,
                Weight = r.Weight,
                QualifiedQuantity = r.QualifiedQuantity,
                QualifiedWeight = r.QualifiedWeight,
                DefectReworkQuantity = r.DefectReworkQuantity,
                DefectWarehouseQuantity = r.DefectWarehouseQuantity,
                DefectScrapQuantity = r.DefectScrapQuantity,
                DefectDescription = r.DefectDescription,
                OuterDiameterRange = r.OuterDiameterRange,
                WallThicknessRange = r.WallThicknessRange,
                LengthAllowanceRange = r.LengthAllowanceRange,
                Pressure = r.Pressure,
                HoldTime = r.HoldTime,
                Remark = r.Remark,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            })
            .ToListAsync();

        return new PagedResult<FinalInspectionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<FinalInspectionDto> CreateAsync(CreateFinalInspectionRequest request)
    {
        // 如果ProductionBatchId为0，尝试根据BatchNo解析
        if (request.ProductionBatchId == 0 || request.ProductionBatchId == default)
        {
            var batch = await _context.ProductionBatches
                .AsNoTracking()
                .Where(b => b.BatchNo == request.BatchNo)
                .Select(b => new {
                    b.Id,
                    b.MaterialName,
                    b.TagNo,
                    b.WorkOrderNo,
                    b.SalesOrderNo,
                    b.SourceName,
                    b.SourceHeatNo,
                    b.PlantGrade,
                    b.Specification,
                    b.LengthStatus,
                    b.MinLength,
                    b.MaxLength
                })
                .FirstOrDefaultAsync();

            if (batch != null)
            {
                request.ProductionBatchId = batch.Id;
                // 填充未提供的批次冗余字段
                request.MaterialName ??= batch.MaterialName;
                request.TagNo ??= batch.TagNo;
                request.WorkOrderNo ??= batch.WorkOrderNo;
                request.SalesOrderNo ??= batch.SalesOrderNo;
                request.SourceUnit ??= batch.SourceName;
                request.FurnaceNo ??= batch.SourceHeatNo;
                request.PlantGrade ??= batch.PlantGrade;
                request.Specification ??= batch.Specification;
                if (string.IsNullOrEmpty(request.FixedLength))
                {
                    request.FixedLength = batch.LengthStatus == "Fixed" && batch.MinLength.HasValue
                        ? $"{batch.MinLength.Value:G29}mm"
                        : null;
                }
            }
        }

        var entity = new FinalInspection
        {
            InspectionItem = request.InspectionItem,
            InspectionDate = request.InspectionDate,
            BatchNo = request.BatchNo,
            ProductionBatchId = request.ProductionBatchId,
            MaterialName = request.MaterialName,
            TagNo = request.TagNo,
            WorkOrderNo = request.WorkOrderNo,
            SalesOrderNo = request.SalesOrderNo,
            SourceUnit = request.SourceUnit,
            FurnaceNo = request.FurnaceNo,
            PlantGrade = request.PlantGrade,
            Specification = request.Specification,
            FixedLength = request.FixedLength,
            EquipmentName = request.EquipmentName,
            Shift = request.Shift,
            Operator = request.Operator,
            Quantity = request.Quantity,
            Weight = request.Weight,
            QualifiedQuantity = request.QualifiedQuantity,
            QualifiedWeight = request.QualifiedWeight,
            DefectReworkQuantity = request.DefectReworkQuantity,
            DefectWarehouseQuantity = request.DefectWarehouseQuantity,
            DefectScrapQuantity = request.DefectScrapQuantity,
            DefectDescription = request.DefectDescription,
            OuterDiameterRange = request.OuterDiameterRange,
            WallThicknessRange = request.WallThicknessRange,
            LengthAllowanceRange = request.LengthAllowanceRange,
            Pressure = request.Pressure,
            HoldTime = request.HoldTime,
            Remark = request.Remark
        };

        _context.FinalInspections.Add(entity);
        await _context.SaveChangesAsync();

        return new FinalInspectionDto
        {
            Id = entity.Id,
            InspectionItem = entity.InspectionItem,
            InspectionDate = entity.InspectionDate,
            BatchNo = entity.BatchNo,
            ProductionBatchId = entity.ProductionBatchId,
            MaterialName = entity.MaterialName,
            TagNo = entity.TagNo,
            WorkOrderNo = entity.WorkOrderNo,
            SalesOrderNo = entity.SalesOrderNo,
            SourceUnit = entity.SourceUnit,
            FurnaceNo = entity.FurnaceNo,
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
            FixedLength = entity.FixedLength,
            EquipmentName = entity.EquipmentName,
            Shift = entity.Shift,
            Operator = entity.Operator,
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            QualifiedQuantity = entity.QualifiedQuantity,
            QualifiedWeight = entity.QualifiedWeight,
            DefectReworkQuantity = entity.DefectReworkQuantity,
            DefectWarehouseQuantity = entity.DefectWarehouseQuantity,
            DefectScrapQuantity = entity.DefectScrapQuantity,
            DefectDescription = entity.DefectDescription,
            OuterDiameterRange = entity.OuterDiameterRange,
            WallThicknessRange = entity.WallThicknessRange,
            LengthAllowanceRange = entity.LengthAllowanceRange,
            Pressure = entity.Pressure,
            HoldTime = entity.HoldTime,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task<FinalInspectionDto> UpdateAsync(int id, UpdateFinalInspectionRequest request)
    {
        var entity = await _context.FinalInspections.FindAsync(id)
            ?? throw new BusinessException("成品检验记录不存在");

        entity.InspectionDate = request.InspectionDate;
        entity.EquipmentName = request.EquipmentName ?? entity.EquipmentName;
        entity.Shift = request.Shift ?? entity.Shift;
        entity.Operator = request.Operator ?? entity.Operator;
        entity.Quantity = request.Quantity ?? entity.Quantity;
        entity.Weight = request.Weight ?? entity.Weight;
        entity.QualifiedQuantity = request.QualifiedQuantity ?? entity.QualifiedQuantity;
        entity.QualifiedWeight = request.QualifiedWeight ?? entity.QualifiedWeight;
        entity.DefectReworkQuantity = request.DefectReworkQuantity ?? entity.DefectReworkQuantity;
        entity.DefectWarehouseQuantity = request.DefectWarehouseQuantity ?? entity.DefectWarehouseQuantity;
        entity.DefectScrapQuantity = request.DefectScrapQuantity ?? entity.DefectScrapQuantity;
        entity.DefectDescription = request.DefectDescription ?? entity.DefectDescription;
        entity.OuterDiameterRange = request.OuterDiameterRange ?? entity.OuterDiameterRange;
        entity.WallThicknessRange = request.WallThicknessRange ?? entity.WallThicknessRange;
        entity.LengthAllowanceRange = request.LengthAllowanceRange ?? entity.LengthAllowanceRange;
        entity.Pressure = request.Pressure ?? entity.Pressure;
        entity.HoldTime = request.HoldTime ?? entity.HoldTime;
        entity.Remark = request.Remark ?? entity.Remark;

        await _context.SaveChangesAsync();

        return new FinalInspectionDto
        {
            Id = entity.Id,
            InspectionItem = entity.InspectionItem,
            InspectionDate = entity.InspectionDate,
            BatchNo = entity.BatchNo,
            ProductionBatchId = entity.ProductionBatchId,
            MaterialName = entity.MaterialName,
            TagNo = entity.TagNo,
            WorkOrderNo = entity.WorkOrderNo,
            SalesOrderNo = entity.SalesOrderNo,
            SourceUnit = entity.SourceUnit,
            FurnaceNo = entity.FurnaceNo,
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
            FixedLength = entity.FixedLength,
            EquipmentName = entity.EquipmentName,
            Shift = entity.Shift,
            Operator = entity.Operator,
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            QualifiedQuantity = entity.QualifiedQuantity,
            QualifiedWeight = entity.QualifiedWeight,
            DefectReworkQuantity = entity.DefectReworkQuantity,
            DefectWarehouseQuantity = entity.DefectWarehouseQuantity,
            DefectScrapQuantity = entity.DefectScrapQuantity,
            DefectDescription = entity.DefectDescription,
            OuterDiameterRange = entity.OuterDiameterRange,
            WallThicknessRange = entity.WallThicknessRange,
            LengthAllowanceRange = entity.LengthAllowanceRange,
            Pressure = entity.Pressure,
            HoldTime = entity.HoldTime,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.FinalInspections.FindAsync(id)
            ?? throw new BusinessException("成品检验记录不存在");

        _context.FinalInspections.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<FinalInspectionDto>> BatchCreateAsync(List<CreateFinalInspectionRequest> requests)
    {
        if (requests.Count == 0)
            return new List<FinalInspectionDto>();

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

        var entities = requests.Select(r =>
        {
            var batch = batchLookup[r.BatchNo];
            return new FinalInspection
            {
                InspectionItem = r.InspectionItem,
                InspectionDate = r.InspectionDate,
                BatchNo = r.BatchNo,
                ProductionBatchId = batch.Id,
                MaterialName = r.MaterialName ?? batch.MaterialName,
                TagNo = r.TagNo ?? batch.TagNo,
                WorkOrderNo = r.WorkOrderNo ?? batch.WorkOrderNo,
                SalesOrderNo = r.SalesOrderNo ?? batch.SalesOrderNo,
                SourceUnit = r.SourceUnit ?? batch.SourceName,
                FurnaceNo = r.FurnaceNo ?? batch.SourceHeatNo,
                PlantGrade = r.PlantGrade ?? batch.PlantGrade,
                Specification = r.Specification ?? batch.Specification,
                FixedLength = r.FixedLength ?? (batch.LengthStatus == "Fixed" && batch.MinLength.HasValue ? $"{batch.MinLength.Value:G29}mm" : null),
                EquipmentName = r.EquipmentName,
                Shift = r.Shift,
                Operator = r.Operator,
                Quantity = r.Quantity,
                Weight = r.Weight,
                QualifiedQuantity = r.QualifiedQuantity,
                QualifiedWeight = r.QualifiedWeight,
                DefectReworkQuantity = r.DefectReworkQuantity,
                DefectWarehouseQuantity = r.DefectWarehouseQuantity,
                DefectScrapQuantity = r.DefectScrapQuantity,
                DefectDescription = r.DefectDescription,
                OuterDiameterRange = r.OuterDiameterRange,
                WallThicknessRange = r.WallThicknessRange,
                LengthAllowanceRange = r.LengthAllowanceRange,
                Pressure = r.Pressure,
                HoldTime = r.HoldTime,
                Remark = r.Remark
            };
        }).ToList();

        _context.FinalInspections.AddRange(entities);
        await _context.SaveChangesAsync();

        return entities.Select(e => new FinalInspectionDto
        {
            Id = e.Id,
            InspectionItem = e.InspectionItem,
            InspectionDate = e.InspectionDate,
            BatchNo = e.BatchNo,
            ProductionBatchId = e.ProductionBatchId,
            MaterialName = e.MaterialName,
            TagNo = e.TagNo,
            WorkOrderNo = e.WorkOrderNo,
            SalesOrderNo = e.SalesOrderNo,
            SourceUnit = e.SourceUnit,
            FurnaceNo = e.FurnaceNo,
            PlantGrade = e.PlantGrade,
            Specification = e.Specification,
            FixedLength = e.FixedLength,
            EquipmentName = e.EquipmentName,
            Shift = e.Shift,
            Operator = e.Operator,
            Quantity = e.Quantity,
            Weight = e.Weight,
            QualifiedQuantity = e.QualifiedQuantity,
            QualifiedWeight = e.QualifiedWeight,
            DefectReworkQuantity = e.DefectReworkQuantity,
            DefectWarehouseQuantity = e.DefectWarehouseQuantity,
            DefectScrapQuantity = e.DefectScrapQuantity,
            DefectDescription = e.DefectDescription,
            OuterDiameterRange = e.OuterDiameterRange,
            WallThicknessRange = e.WallThicknessRange,
            LengthAllowanceRange = e.LengthAllowanceRange,
            Pressure = e.Pressure,
            HoldTime = e.HoldTime,
            Remark = e.Remark,
            CreatedTime = e.CreatedTime,
            UpdatedTime = e.UpdatedTime
        }).ToList();
    }

    public async Task<BatchLookupResultDto?> LookupBatchAsync(string batchNo)
    {
        if (string.IsNullOrWhiteSpace(batchNo))
            return null;

        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.BatchNo == batchNo)
            .Select(b => new BatchLookupResultDto
            {
                ProductionBatchId = b.Id,
                MaterialName = b.MaterialName,
                TagNo = b.TagNo,
                WorkOrderNo = b.WorkOrderNo,
                SalesOrderNo = b.SalesOrderNo,
                SourceUnit = b.SourceName,
                FurnaceNo = b.SourceHeatNo,
                PlantGrade = b.PlantGrade,
                Specification = b.Specification,
                FixedLength = b.LengthStatus == "Fixed" && b.MinLength.HasValue
                    ? b.MinLength.Value.ToString("G29") + "mm"
                    : null
            })
            .FirstOrDefaultAsync();

        return batch;
    }

    private static IQueryable<FinalInspection> ApplySorting(IQueryable<FinalInspection> queryable, string sortBy, bool isDescending)
    {
        var sorted = (sortBy.ToLowerInvariant(), isDescending) switch
        {
            ("inspectionitem", false) => queryable.OrderBy(r => r.InspectionItem),
            ("inspectionitem", true) => queryable.OrderByDescending(r => r.InspectionItem),
            ("inspectiondate", false) => queryable.OrderBy(r => r.InspectionDate),
            ("inspectiondate", true) => queryable.OrderByDescending(r => r.InspectionDate),
            ("batchno", false) => queryable.OrderBy(r => r.BatchNo),
            ("batchno", true) => queryable.OrderByDescending(r => r.BatchNo),
            ("materialname", false) => queryable.OrderBy(r => r.MaterialName ?? ""),
            ("materialname", true) => queryable.OrderByDescending(r => r.MaterialName ?? ""),
            ("tagno", false) => queryable.OrderBy(r => r.TagNo ?? ""),
            ("tagno", true) => queryable.OrderByDescending(r => r.TagNo ?? ""),
            ("workorderno", false) => queryable.OrderBy(r => r.WorkOrderNo ?? ""),
            ("workorderno", true) => queryable.OrderByDescending(r => r.WorkOrderNo ?? ""),
            ("salesorderno", false) => queryable.OrderBy(r => r.SalesOrderNo ?? ""),
            ("salesorderno", true) => queryable.OrderByDescending(r => r.SalesOrderNo ?? ""),
            ("sourceunit", false) => queryable.OrderBy(r => r.SourceUnit ?? ""),
            ("sourceunit", true) => queryable.OrderByDescending(r => r.SourceUnit ?? ""),
            ("furnaceno", false) => queryable.OrderBy(r => r.FurnaceNo ?? ""),
            ("furnaceno", true) => queryable.OrderByDescending(r => r.FurnaceNo ?? ""),
            ("plantgrade", false) => queryable.OrderBy(r => r.PlantGrade ?? ""),
            ("plantgrade", true) => queryable.OrderByDescending(r => r.PlantGrade ?? ""),
            ("specification", false) => queryable.OrderBy(r => r.Specification ?? ""),
            ("specification", true) => queryable.OrderByDescending(r => r.Specification ?? ""),
            ("fixedlength", false) => queryable.OrderBy(r => r.FixedLength ?? ""),
            ("fixedlength", true) => queryable.OrderByDescending(r => r.FixedLength ?? ""),
            ("equipmentname", false) => queryable.OrderBy(r => r.EquipmentName ?? ""),
            ("equipmentname", true) => queryable.OrderByDescending(r => r.EquipmentName ?? ""),
            ("shift", false) => queryable.OrderBy(r => r.Shift ?? ""),
            ("shift", true) => queryable.OrderByDescending(r => r.Shift ?? ""),
            ("operator", false) => queryable.OrderBy(r => r.Operator ?? ""),
            ("operator", true) => queryable.OrderByDescending(r => r.Operator ?? ""),
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
            ("defectdescription", false) => queryable.OrderBy(r => r.DefectDescription ?? ""),
            ("defectdescription", true) => queryable.OrderByDescending(r => r.DefectDescription ?? ""),
            ("outerdiameterrange", false) => queryable.OrderBy(r => r.OuterDiameterRange ?? ""),
            ("outerdiameterrange", true) => queryable.OrderByDescending(r => r.OuterDiameterRange ?? ""),
            ("wallthicknessrange", false) => queryable.OrderBy(r => r.WallThicknessRange ?? ""),
            ("wallthicknessrange", true) => queryable.OrderByDescending(r => r.WallThicknessRange ?? ""),
            ("lengthallowancerange", false) => queryable.OrderBy(r => r.LengthAllowanceRange ?? ""),
            ("lengthallowancerange", true) => queryable.OrderByDescending(r => r.LengthAllowanceRange ?? ""),
            ("pressure", false) => queryable.OrderBy(r => r.Pressure ?? 0),
            ("pressure", true) => queryable.OrderByDescending(r => r.Pressure ?? 0),
            ("holdtime", false) => queryable.OrderBy(r => r.HoldTime ?? 0),
            ("holdtime", true) => queryable.OrderByDescending(r => r.HoldTime ?? 0),
            ("remark", false) => queryable.OrderBy(r => r.Remark ?? ""),
            ("remark", true) => queryable.OrderByDescending(r => r.Remark ?? ""),
            ("createdtime", false) => queryable.OrderBy(r => r.CreatedTime),
            ("createdtime", true) => queryable.OrderByDescending(r => r.CreatedTime),
            ("updatedtime", false) => queryable.OrderBy(r => r.UpdatedTime),
            ("updatedtime", true) => queryable.OrderByDescending(r => r.UpdatedTime),
            _ => queryable.OrderByDescending(r => r.CreatedTime)
        };
        return sorted;
    }
}
