using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Extensions;
using MES.Services.Helpers;

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

        // 处理 BatchNo 导航属性筛选（ProcessInspection 实体无 BatchNo 属性）
        if (query.Filters != null)
        {
            var batchNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("BatchNo", StringComparison.OrdinalIgnoreCase));
            if (batchNoFilter != null && batchNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.ProductionBatch != null
                    && batchNoFilter.Values.Contains(r.ProductionBatch.BatchNo));
                query.Filters.Remove(batchNoFilter);
            }
        }

        queryable = queryable.ApplyFilters(query.Filters);
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
                QualifiedConcessionQuantity = r.QualifiedConcessionQuantity,
                ConcessionRemark = r.ConcessionRemark,
                DefectReworkQuantity = r.DefectReworkQuantity,
                DefectWarehouseQuantity = r.DefectWarehouseQuantity,
                DefectScrapQuantity = r.DefectScrapQuantity,
                DefectDescription = r.DefectDescription,
                SourceUnit = r.SourceUnit,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                Remark = r.Remark,
                BatchNo = r.ProductionBatch.BatchNo,
                DataSource = r.DataSource,
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

    public async Task<List<ProcessInspectionDto>> GetAllListAsync()
    {
        var query = from pi in _context.ProcessInspections
                    join b in _context.ProductionBatches on pi.ProductionBatchId equals b.Id
                    orderby pi.Id descending
                    select new ProcessInspectionDto
                    {
                        Id = pi.Id,
                        InspectionDate = pi.InspectionDate,
                        ProductionBatchId = pi.ProductionBatchId,
                        BatchNo = b.BatchNo,
                        ProcessName = pi.ProcessName,
                        ManufacturingSpec = pi.ManufacturingSpec,
                        SectionName = pi.SectionName,
                        SequenceNumber = pi.SequenceNumber,
                        EquipmentName = pi.EquipmentName,
                        Inspector = pi.Inspector,
                        Shift = pi.Shift,
                        Quantity = pi.Quantity,
                        Weight = pi.Weight,
                        InspectionItem = pi.InspectionItem,
                        QualifiedQuantity = pi.QualifiedQuantity,
                        QualifiedWeight = pi.QualifiedWeight,
                        QualifiedConcessionQuantity = pi.QualifiedConcessionQuantity,
                        ConcessionRemark = pi.ConcessionRemark,
                        DefectReworkQuantity = pi.DefectReworkQuantity,
                        DefectWarehouseQuantity = pi.DefectWarehouseQuantity,
                        DefectScrapQuantity = pi.DefectScrapQuantity,
                        DefectDescription = pi.DefectDescription,
                        SourceUnit = pi.SourceUnit,
                        TagNo = pi.TagNo,
                        PlantGrade = pi.PlantGrade,
                        Remark = pi.Remark,
                        CreatedTime = pi.CreatedTime,
                        UpdatedTime = pi.UpdatedTime
                    };
        return await query.ToListAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var all = await _context.ProcessInspections
            .AsNoTracking()
            .Include(r => r.ProductionBatch)
            .Select(r => new
            {
                r.ProductionBatch.BatchNo,
                r.ProcessName,
                r.ManufacturingSpec,
                r.SectionName,
                r.EquipmentName,
                r.Inspector,
                r.Shift,
                r.InspectionItem,
                r.ConcessionRemark,
                r.DefectDescription,
                r.SourceUnit,
                r.TagNo,
                r.PlantGrade,
                r.InspectionDate,
                r.Remark
            })
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["BatchNo"] = all.Select(x => x.BatchNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
            ["ProcessName"] = all.Select(x => x.ProcessName).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
            ["ManufacturingSpec"] = all.Select(x => x.ManufacturingSpec ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["SectionName"] = all.Select(x => x.SectionName).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
            ["EquipmentName"] = all.Select(x => x.EquipmentName ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Inspector"] = all.Select(x => x.Inspector ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Shift"] = all.Select(x => x.Shift ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["InspectionItem"] = all.Select(x => x.InspectionItem ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["ConcessionRemark"] = all.Select(x => x.ConcessionRemark ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["DefectDescription"] = all.Select(x => x.DefectDescription ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["SourceUnit"] = all.Select(x => x.SourceUnit ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["TagNo"] = all.Select(x => x.TagNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["PlantGrade"] = all.Select(x => x.PlantGrade ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["InspectionDate"] = all.Select(x => x.InspectionDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList(),
            ["Remark"] = all.Select(x => x.Remark ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList()
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

        // 预查询：各批次所有已有的过程检验记录（用于执行序号跳跃验证）
        var allExistingRecords = await _context.ProcessInspections
            .Where(r => allBatchIds.Contains(r.ProductionBatchId))
            .ToListAsync();
        var recordsByBatch = allExistingRecords
            .GroupBy(r => r.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

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
                    seqNum = pg.GetSectionSequence(request.SectionName) ?? 0;
                }
            }

            // 执行序号跳跃限制：以每条记录的 InspectionDate 为准，对比该批次在此日期前已执行的最大序号，不能 > +7
            if (seqNum > 0)
            {
                var batchRecords = recordsByBatch.GetValueOrDefault(batchId, new List<ProcessInspection>());
                var prevMax = batchRecords
                    .Where(r => r.InspectionDate.Date < request.InspectionDate.Date)
                    .Select(r => (int?)r.SequenceNumber)
                    .Max() ?? 0;
                var maxAllowed = prevMax + 7;
                if (seqNum > maxAllowed)
                    errors.Add($"第{i + 1}行：执行序号({seqNum})超过该日期前已执行最大值({prevMax})+7={maxAllowed}");
            }

            // 重复校验：同批次+同工序组+同工段 → 重复
            if (pgId > 0)
            {
                var batchRecords = recordsByBatch.GetValueOrDefault(batchId, new List<ProcessInspection>());
                var dup = batchRecords.Any(r =>
                    r.ProcessGroupId == pgId.Value && r.SectionName == request.SectionName);
                if (dup)
                    errors.Add($"第{i + 1}行：工段「{request.SectionName}」在该批次该工序组中已存在过程检验记录，不能重复创建");
            }

            // 4) 检验支数 = 合格支数 + 返整支数 + 入库支数 + 报废支数
            if (request.Quantity.HasValue)
            {
                var sum = (request.QualifiedQuantity ?? 0)
                    + (request.DefectReworkQuantity ?? 0)
                    + (request.DefectWarehouseQuantity ?? 0)
                    + (request.DefectScrapQuantity ?? 0);
                if (request.Quantity.Value != sum)
                    errors.Add($"第{i + 1}行：检验支数({request.Quantity}) ≠ 合格支数({request.QualifiedQuantity ?? 0}) + 返整({request.DefectReworkQuantity ?? 0}) + 入库({request.DefectWarehouseQuantity ?? 0}) + 报废({request.DefectScrapQuantity ?? 0}) = {sum}");
            }

            // 5) 让步放行支数 ≤ 合格支数
            if (request.QualifiedConcessionQuantity.HasValue && request.QualifiedQuantity.HasValue
                && request.QualifiedConcessionQuantity.Value > request.QualifiedQuantity.Value)
            {
                errors.Add($"第{i + 1}行：让步放行支数({request.QualifiedConcessionQuantity})不能大于合格支数({request.QualifiedQuantity})");
            }

            // 6) 检验重量不能大于批次现有效原料重量
            if (request.Weight.HasValue && request.Weight > 0)
            {
                var maxWeight = batch.CurrentValidWeight ?? batch.InputWeight;
                if (request.Weight.Value > maxWeight)
                    errors.Add($"第{i + 1}行：检验重量({request.Weight})不能大于现有效原料重量({maxWeight})");
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
                QualifiedConcessionQuantity = request.QualifiedConcessionQuantity,
                ConcessionRemark = request.ConcessionRemark,
                DefectReworkQuantity = request.DefectReworkQuantity,
                DefectWarehouseQuantity = request.DefectWarehouseQuantity,
                DefectScrapQuantity = request.DefectScrapQuantity,
                DefectDescription = request.DefectDescription,
                SourceUnit = request.SourceUnit,
                TagNo = request.TagNo,
                PlantGrade = request.PlantGrade,
                Remark = request.Remark,
                DataSource = request.DataSource ?? "MANUAL"
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
            QualifiedConcessionQuantity = e.QualifiedConcessionQuantity,
            ConcessionRemark = e.ConcessionRemark,
            DefectReworkQuantity = e.DefectReworkQuantity,
            DefectWarehouseQuantity = e.DefectWarehouseQuantity,
            DefectScrapQuantity = e.DefectScrapQuantity,
            DefectDescription = e.DefectDescription,
            SourceUnit = e.SourceUnit,
            TagNo = e.TagNo,
            PlantGrade = e.PlantGrade,
            Remark = e.Remark,
            BatchNo = batchLookup.Values.FirstOrDefault(b => b.Id == e.ProductionBatchId)?.BatchNo,
            DataSource = e.DataSource,
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
        entity.QualifiedConcessionQuantity = request.QualifiedConcessionQuantity ?? entity.QualifiedConcessionQuantity;
        entity.ConcessionRemark = request.ConcessionRemark ?? entity.ConcessionRemark;
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
            QualifiedConcessionQuantity = entity.QualifiedConcessionQuantity,
            ConcessionRemark = entity.ConcessionRemark,
            DefectReworkQuantity = entity.DefectReworkQuantity,
            DefectWarehouseQuantity = entity.DefectWarehouseQuantity,
            DefectScrapQuantity = entity.DefectScrapQuantity,
            DefectDescription = entity.DefectDescription,
            SourceUnit = entity.SourceUnit,
            TagNo = entity.TagNo,
            PlantGrade = entity.PlantGrade,
            Remark = entity.Remark,
            BatchNo = entity.ProductionBatch.BatchNo,
            DataSource = entity.DataSource,
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
        return queryable.ApplySort(sortBy, isDescending);
    }
}
