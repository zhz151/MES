using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;

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
                QualifiedConcessionQuantity = r.QualifiedConcessionQuantity,
                ConcessionRemark = r.ConcessionRemark,
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

        queryable = queryable.ApplyFilters(query.Filters);
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
                QualifiedConcessionQuantity = r.QualifiedConcessionQuantity,
                ConcessionRemark = r.ConcessionRemark,
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
                DataSource = r.DataSource,
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

    public async Task<List<FinalInspectionDto>> GetAllListAsync()
    {
        return await _context.FinalInspections
            .AsNoTracking()
            .OrderByDescending(r => r.Id)
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
                QualifiedConcessionQuantity = r.QualifiedConcessionQuantity,
                ConcessionRemark = r.ConcessionRemark,
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
            QualifiedConcessionQuantity = request.QualifiedConcessionQuantity,
            ConcessionRemark = request.ConcessionRemark,
            DefectReworkQuantity = request.DefectReworkQuantity,
            DefectWarehouseQuantity = request.DefectWarehouseQuantity,
            DefectScrapQuantity = request.DefectScrapQuantity,
            DefectDescription = request.DefectDescription,
            OuterDiameterRange = request.OuterDiameterRange,
            WallThicknessRange = request.WallThicknessRange,
            LengthAllowanceRange = request.LengthAllowanceRange,
            Pressure = request.Pressure,
            HoldTime = request.HoldTime,
            Remark = request.Remark,
            DataSource = request.DataSource ?? "MANUAL"
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
            QualifiedConcessionQuantity = entity.QualifiedConcessionQuantity,
            ConcessionRemark = entity.ConcessionRemark,
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
            DataSource = entity.DataSource,
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
        entity.QualifiedConcessionQuantity = request.QualifiedConcessionQuantity ?? entity.QualifiedConcessionQuantity;
        entity.ConcessionRemark = request.ConcessionRemark ?? entity.ConcessionRemark;
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
            QualifiedConcessionQuantity = entity.QualifiedConcessionQuantity,
            ConcessionRemark = entity.ConcessionRemark,
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
            DataSource = entity.DataSource,
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

        // 预查询：各批次已有的成品检验记录（用于重复校验）
        var allBatchIds = batchLookup.Values.Select(b => b.Id).ToList();
        var existingRecords = await _context.FinalInspections
            .Where(f => allBatchIds.Contains(f.ProductionBatchId))
            .ToListAsync();
        var existingByBatch = existingRecords
            .GroupBy(f => f.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 重复校验：同批次 + 同物料名称 + 同检验项目 + 同定尺长度 + 同操作人 → 重复
        var errors = new List<string>();
        foreach (var (request, i) in requests.Select((r, idx) => (r, idx)))
        {
            var batch = batchLookup[request.BatchNo];
            var batchId = batch.Id;

            var materialName = request.MaterialName ?? batch.MaterialName;
            var fixedLength = request.FixedLength ?? (batch.LengthStatus == "Fixed" && batch.MinLength.HasValue ? $"{batch.MinLength.Value:G29}mm" : null);
            var operatorName = request.Operator;

            var existing = existingByBatch.GetValueOrDefault(batchId, new List<FinalInspection>());
            var dup = existing.Any(f =>
                f.MaterialName == materialName &&
                f.InspectionItem == request.InspectionItem &&
                f.FixedLength == fixedLength &&
                f.Operator == operatorName);
            if (dup)
                errors.Add($"第{i + 1}行：该批次已存在相同物料/检验项目/定尺/操作人的成品检验记录，不能重复创建");

            // 2) 检验支数 = 合格支数 + 返整支数 + 入库支数 + 报废支数
            if (request.Quantity.HasValue)
            {
                var sum = (request.QualifiedQuantity ?? 0)
                    + (request.DefectReworkQuantity ?? 0)
                    + (request.DefectWarehouseQuantity ?? 0)
                    + (request.DefectScrapQuantity ?? 0);
                if (request.Quantity.Value != sum)
                    errors.Add($"第{i + 1}行：检验支数({request.Quantity}) ≠ 合格支数({request.QualifiedQuantity ?? 0}) + 返整({request.DefectReworkQuantity ?? 0}) + 入库({request.DefectWarehouseQuantity ?? 0}) + 报废({request.DefectScrapQuantity ?? 0}) = {sum}");
            }

            // 3) 让步放行支数 ≤ 合格支数
            if (request.QualifiedConcessionQuantity.HasValue && request.QualifiedQuantity.HasValue
                && request.QualifiedConcessionQuantity.Value > request.QualifiedQuantity.Value)
            {
                errors.Add($"第{i + 1}行：让步放行支数({request.QualifiedConcessionQuantity})不能大于合格支数({request.QualifiedQuantity})");
            }

            // 4) 检验重量不能大于批次现有效原料重量
            if (request.Weight.HasValue && request.Weight > 0)
            {
                var maxWeight = batch.CurrentValidWeight ?? batch.InputWeight;
                if (request.Weight.Value > maxWeight)
                    errors.Add($"第{i + 1}行：检验重量({request.Weight})不能大于现有效原料重量({maxWeight})");
            }
        }
        if (errors.Any())
            throw new BusinessException(string.Join("；", errors));

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
                QualifiedConcessionQuantity = r.QualifiedConcessionQuantity,
                ConcessionRemark = r.ConcessionRemark,
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
                DataSource = r.DataSource ?? "MANUAL"
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
            QualifiedConcessionQuantity = e.QualifiedConcessionQuantity,
            ConcessionRemark = e.ConcessionRemark,
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
            DataSource = e.DataSource,
            CreatedTime = e.CreatedTime,
            UpdatedTime = e.UpdatedTime
        }).ToList();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var all = await _context.FinalInspections
            .AsNoTracking()
            .Select(r => new
            {
                r.BatchNo,
                r.MaterialName,
                r.TagNo,
                r.WorkOrderNo,
                r.SalesOrderNo,
                r.SourceUnit,
                r.FurnaceNo,
                r.PlantGrade,
                r.Specification,
                r.FixedLength,
                r.EquipmentName,
                r.Shift,
                r.Operator,
                r.ConcessionRemark,
                r.DefectDescription,
                r.OuterDiameterRange,
                r.WallThicknessRange,
                r.LengthAllowanceRange,
                r.InspectionDate,
                r.Remark
            })
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["BatchNo"] = all.Select(x => x.BatchNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
            ["MaterialName"] = all.Select(x => x.MaterialName ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["TagNo"] = all.Select(x => x.TagNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["WorkOrderNo"] = all.Select(x => x.WorkOrderNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["SalesOrderNo"] = all.Select(x => x.SalesOrderNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["SourceUnit"] = all.Select(x => x.SourceUnit ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["FurnaceNo"] = all.Select(x => x.FurnaceNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["PlantGrade"] = all.Select(x => x.PlantGrade ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Specification"] = all.Select(x => x.Specification ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["FixedLength"] = all.Select(x => x.FixedLength ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["EquipmentName"] = all.Select(x => x.EquipmentName ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Shift"] = all.Select(x => x.Shift ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Operator"] = all.Select(x => x.Operator ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["ConcessionRemark"] = all.Select(x => x.ConcessionRemark ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["DefectDescription"] = all.Select(x => x.DefectDescription ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["OuterDiameterRange"] = all.Select(x => x.OuterDiameterRange ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["WallThicknessRange"] = all.Select(x => x.WallThicknessRange ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["LengthAllowanceRange"] = all.Select(x => x.LengthAllowanceRange ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["InspectionDate"] = all.Select(x => x.InspectionDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList(),
            ["Remark"] = all.Select(x => x.Remark ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList()
        };
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
        return queryable.ApplySort(sortBy, isDescending);
    }
}
