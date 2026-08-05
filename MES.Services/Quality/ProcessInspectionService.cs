using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Auth;
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
using MES.Core.Exceptions;
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
using MES.Services.Extensions;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.Helpers;

namespace MES.Services.Quality;

/// <summary>
/// 过程检验服务实现
/// </summary>
public class ProcessInspectionService : IProcessInspectionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProcessInspectionService> _logger;
    private readonly IProductionRecordService _productionRecordService;
    private readonly IConfigParameterService _configService;
    private readonly IMemoryCache _cache;
    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();

    public ProcessInspectionService(
        AppDbContext context,
        ILogger<ProcessInspectionService> logger,
        IProductionRecordService productionRecordService,
        IConfigParameterService configService,
        IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _productionRecordService = productionRecordService;
        _configService = configService;
        _cache = cache;
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

    public async Task<PagedResult<ProcessInspectionDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.ProcessInspections
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            queryable = queryable.Where(r => r.BatchNo!.Contains(query.Keyword)
                || (r.ProductionBatch.WorkOrderNo != null && r.ProductionBatch.WorkOrderNo.Contains(query.Keyword))
                || (r.ProductionBatch.SalesOrderNo != null && r.ProductionBatch.SalesOrderNo.Contains(query.Keyword))
                || (r.ProductionBatch.ProductionMainNo != null && r.ProductionBatch.ProductionMainNo.Contains(query.Keyword))
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

        // 处理 BatchNo 筛选（实体已有 BatchNo 冗余字段）
        if (query.Filters != null)
        {
            var batchNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("BatchNo", StringComparison.OrdinalIgnoreCase));
            if (batchNoFilter != null && batchNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.BatchNo != null
                    && batchNoFilter.Values.Contains(r.BatchNo));
                query.Filters.Remove(batchNoFilter);
            }
        }

        // 处理批次导航属性筛选（实体无冗余字段，需手动处理）
        if (query.Filters != null)
        {
            var woNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("WorkOrderNo", StringComparison.OrdinalIgnoreCase));
            if (woNoFilter != null && woNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.ProductionBatch.WorkOrderNo != null
                    && woNoFilter.Values.Contains(r.ProductionBatch.WorkOrderNo));
                query.Filters.Remove(woNoFilter);
            }

            var salesOrderNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("SalesOrderNo", StringComparison.OrdinalIgnoreCase));
            if (salesOrderNoFilter != null && salesOrderNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.ProductionBatch.SalesOrderNo != null
                    && salesOrderNoFilter.Values.Contains(r.ProductionBatch.SalesOrderNo));
                query.Filters.Remove(salesOrderNoFilter);
            }

            var productionMainNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("ProductionMainNo", StringComparison.OrdinalIgnoreCase));
            if (productionMainNoFilter != null && productionMainNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.ProductionBatch.ProductionMainNo != null
                    && productionMainNoFilter.Values.Contains(r.ProductionBatch.ProductionMainNo));
                query.Filters.Remove(productionMainNoFilter);
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
                Shift = EnumHelper.TryParse<ShiftType>(r.Shift),
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
                TheoreticalReworkWeight = r.TheoreticalReworkWeight,
                TheoreticalWarehouseWeight = r.TheoreticalWarehouseWeight,
                TheoreticalScrapWeight = r.TheoreticalScrapWeight,
                DefectDescription = r.DefectDescription,
                SourceUnit = r.SourceUnit,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                Remark = r.Remark,
                BatchNo = r.BatchNo!,
                WorkOrderNo = r.ProductionBatch.WorkOrderNo,
                SalesOrderNo = r.ProductionBatch.SalesOrderNo,
                ProductionMainNo = r.ProductionBatch.ProductionMainNo,
                DataSource = r.DataSource,
                ProductStatus = r.ProductStatus,
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
        return await _context.ProcessInspections
            .AsNoTracking()
            .OrderByDescending(pi => pi.Id)
            .Select(pi => new ProcessInspectionDto
            {
                Id = pi.Id,
                InspectionDate = pi.InspectionDate,
                ProductionBatchId = pi.ProductionBatchId,
                BatchNo = pi.BatchNo!,
                WorkOrderNo = pi.ProductionBatch.WorkOrderNo,
                SalesOrderNo = pi.ProductionBatch.SalesOrderNo,
                ProductionMainNo = pi.ProductionBatch.ProductionMainNo,
                ProcessName = pi.ProcessName,
                ManufacturingSpec = pi.ManufacturingSpec,
                SectionName = pi.SectionName,
                SequenceNumber = pi.SequenceNumber,
                EquipmentName = pi.EquipmentName,
                Inspector = pi.Inspector,
                Shift = EnumHelper.TryParse<ShiftType>(pi.Shift),
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
                TheoreticalReworkWeight = pi.TheoreticalReworkWeight,
                TheoreticalWarehouseWeight = pi.TheoreticalWarehouseWeight,
                TheoreticalScrapWeight = pi.TheoreticalScrapWeight,
                DefectDescription = pi.DefectDescription,
                SourceUnit = pi.SourceUnit,
                TagNo = pi.TagNo,
                PlantGrade = pi.PlantGrade,
                Remark = pi.Remark,
                CreatedTime = pi.CreatedTime,
                UpdatedTime = pi.UpdatedTime,
                ProductStatus = pi.ProductStatus
            })
            .ToListAsync();
    }


    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("ProcessInspectionService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            // 顺序执行各列 DISTINCT 查询（DbContext 非线程安全，禁止并行）
            var batchNos = await _context.ProcessInspections.Where(r => r.BatchNo != null).Select(r => r.BatchNo!).Distinct().OrderBy(v => v).ToListAsync();
            var workOrderNos = await _context.ProcessInspections.Where(r => r.ProductionBatch.WorkOrderNo != null).Select(r => r.ProductionBatch.WorkOrderNo).Distinct().OrderBy(v => v).ToListAsync();
            var salesOrderNos = await _context.ProcessInspections.Where(r => r.ProductionBatch.SalesOrderNo != null).Select(r => r.ProductionBatch.SalesOrderNo).Distinct().OrderBy(v => v).ToListAsync();
            var productionMainNos = await _context.ProcessInspections.Where(r => r.ProductionBatch.ProductionMainNo != null).Select(r => r.ProductionBatch.ProductionMainNo).Distinct().OrderBy(v => v).ToListAsync();
            var processNames = await _context.ProcessInspections.Where(r => r.ProcessName != null).Select(r => r.ProcessName!).Distinct().OrderBy(v => v).ToListAsync();
            var manufacturingSpecs = await _context.ProcessInspections.Where(r => r.ManufacturingSpec != null && r.ManufacturingSpec != "").Select(r => r.ManufacturingSpec!).Distinct().OrderBy(v => v).ToListAsync();
            var sectionNames = await _context.ProcessInspections.Where(r => r.SectionName != null).Select(r => r.SectionName!).Distinct().OrderBy(v => v).ToListAsync();
            var equipmentNames = await _context.ProcessInspections.Where(r => r.EquipmentName != null && r.EquipmentName != "").Select(r => r.EquipmentName!).Distinct().OrderBy(v => v).ToListAsync();
            var inspectors = await _context.ProcessInspections.Where(r => r.Inspector != null && r.Inspector != "").Select(r => r.Inspector!).Distinct().OrderBy(v => v).ToListAsync();
            var shifts = await _context.ProcessInspections.Where(r => r.Shift != null && r.Shift != "").Select(r => r.Shift!).Distinct().OrderBy(v => v).ToListAsync();
            var inspectionItems = await _context.ProcessInspections.Where(r => r.InspectionItem != null && r.InspectionItem != "").Select(r => r.InspectionItem!).Distinct().OrderBy(v => v).ToListAsync();
            var concessionRemarks = await _context.ProcessInspections.Where(r => r.ConcessionRemark != null && r.ConcessionRemark != "").Select(r => r.ConcessionRemark!).Distinct().OrderBy(v => v).ToListAsync();
            var defectDescriptions = await _context.ProcessInspections.Where(r => r.DefectDescription != null && r.DefectDescription != "").Select(r => r.DefectDescription!).Distinct().OrderBy(v => v).ToListAsync();
            var sourceUnits = await _context.ProcessInspections.Where(r => r.SourceUnit != null && r.SourceUnit != "").Select(r => r.SourceUnit!).Distinct().OrderBy(v => v).ToListAsync();
            var tagNos = await _context.ProcessInspections.Where(r => r.TagNo != null && r.TagNo != "").Select(r => r.TagNo!).Distinct().OrderBy(v => v).ToListAsync();
            var plantGrades = await _context.ProcessInspections.Where(r => r.PlantGrade != null && r.PlantGrade != "").Select(r => r.PlantGrade!).Distinct().OrderBy(v => v).ToListAsync();
            var inspectionDates = (await _context.ProcessInspections.Select(r => r.InspectionDate).Distinct().ToListAsync()).Select(d => d.ToString("yyyy-MM-dd")).OrderBy(x => x).ToList();
            var remarks = await _context.ProcessInspections.Where(r => r.Remark != null && r.Remark != "").Select(r => r.Remark!).Distinct().OrderBy(v => v).ToListAsync();
            var dataSources = await _context.ProcessInspections.Where(r => r.DataSource != null && r.DataSource != "").Select(r => r.DataSource!).Distinct().OrderBy(v => v).ToListAsync();
            var productStatuses = await _context.ProcessInspections.Where(r => r.ProductStatus != null && r.ProductStatus != "").Select(r => r.ProductStatus!).Distinct().OrderBy(v => v).ToListAsync();
            return new Dictionary<string, List<string>>
            {
                ["BatchNo"] = batchNos,
                ["WorkOrderNo"] = workOrderNos,
                ["SalesOrderNo"] = salesOrderNos,
                ["ProductionMainNo"] = productionMainNos,
                ["ProcessName"] = processNames,
                ["ManufacturingSpec"] = manufacturingSpecs,
                ["SectionName"] = sectionNames,
                ["EquipmentName"] = equipmentNames,
                ["Inspector"] = inspectors,
                ["Shift"] = shifts,
                ["InspectionItem"] = inspectionItems,
                ["ConcessionRemark"] = concessionRemarks,
                ["DefectDescription"] = defectDescriptions,
                ["DataSource"] = dataSources,
                ["SourceUnit"] = sourceUnits,
                ["TagNo"] = tagNos,
                ["PlantGrade"] = plantGrades,
                ["InspectionDate"] = inspectionDates,
                ["Remark"] = remarks,
                ["ProductStatus"] = productStatuses
            };
        }) ?? new Dictionary<string, List<string>>();
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

        var sequenceMaxJump = (int)await GetConfigAsync("SequenceJump", "MaxJump", 7m);

        // 预查询：各批次已存在的冷轧拔生产记录（用于冷轧/冷拔前置校验）
        var existingColdRollDraw = await _context.ProductionRecords
            .Where(r => allBatchIds.Contains(r.ProductionBatchId) && r.SectionName == SectionKeys.ColdRollDraw)
            .Select(r => new { r.ProductionBatchId, r.ProcessGroupId })
            .ToListAsync();
        var coldRollDrawExists = new HashSet<(int BatchId, int PgId)>(
            existingColdRollDraw.Select(r => (r.ProductionBatchId, r.ProcessGroupId)));

        // 收集本次提交中的冷轧拔过程检验记录
        var pendingColdRollDraw = new HashSet<(int BatchId, int PgId)>();
        foreach (var req in requests)
        {
            if (req.SectionName == SectionKeys.ColdRollDraw)
            {
                var b = batchLookup[req.BatchNo];
                var bId = b.Id;
                var pId = req.ProcessGroupId;
                if (pId == null || pId == 0)
                {
                    var matchedPg = pgByBatch.GetValueOrDefault(bId)?
                        .FirstOrDefault(pg => pg.ProcessName == req.ProcessName && pg.ManufacturingSpec == req.ManufacturingSpec);
                    pId = matchedPg?.Id;
                }
                if (pId > 0)
                    pendingColdRollDraw.Add((bId, pId.Value));
            }
        }

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

            // 制造规格不能为空
            if (string.IsNullOrWhiteSpace(request.ManufacturingSpec))
                errors.Add($"第{i + 1}行：制造规格不能为空");

            // 工段必须是"检验"
            if (request.SectionName != SectionKeys.Inspection)
                errors.Add($"第{i + 1}行：工段必须为「检验」，不允许填写其他工段");

            // 执行序号跳跃限制：以每条记录的 InspectionDate 为准，对比该批次在此日期前已执行的最大序号，不能 > +7
            if (seqNum > 0)
            {
                var batchRecords = recordsByBatch.GetValueOrDefault(batchId, new List<ProcessInspection>());
                var prevMax = batchRecords
                    .Where(r => r.InspectionDate.Date < request.InspectionDate.Date)
                    .Select(r => (int?)r.SequenceNumber)
                    .Max() ?? 0;
                var maxAllowed = prevMax + sequenceMaxJump;
                if (seqNum > maxAllowed)
                    errors.Add($"第{i + 1}行：执行序号({seqNum})超过该日期前已执行最大值({prevMax})+{sequenceMaxJump}={maxAllowed}");
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

            // 冷轧/冷拔前置校验：工序组为冷轧/冷拔的，必须先有冷轧拔记录
            if (pgId > 0)
            {
                var pg = processGroups.FirstOrDefault(p => p.Id == pgId.Value);
                if (pg != null && ProcessNames.IsColdRollOrDraw(pg.ProcessName))
                {
                    var hasColdRollDraw = coldRollDrawExists.Contains((batchId, pgId.Value))
                        || pendingColdRollDraw.Contains((batchId, pgId.Value));
                    if (!hasColdRollDraw)
                        errors.Add($"第{i + 1}行：工序「{pg.ProcessName}」必须首先存在「冷轧拔」生产记录，才能进行过程检验");
                }
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
                BatchNo = batch.BatchNo,
                ProcessGroupId = pgId ?? 0,
                ProcessName = request.ProcessName,
                ManufacturingSpec = request.ManufacturingSpec,
                SectionName = request.SectionName,
                SequenceNumber = seqNum,
                InspectionDate = request.InspectionDate,
                EquipmentName = request.EquipmentName,
                Inspector = request.Inspector,
                Shift = request.Shift?.ToString(),
                Quantity = request.Quantity,
                Weight = request.Weight,
                InspectionItem = request.InspectionItem?.ToString(),
                QualifiedQuantity = request.QualifiedQuantity,
                QualifiedWeight = request.QualifiedWeight,
                QualifiedConcessionQuantity = request.QualifiedConcessionQuantity,
                ConcessionRemark = request.ConcessionRemark,
                DefectReworkQuantity = request.DefectReworkQuantity,
                DefectWarehouseQuantity = request.DefectWarehouseQuantity,
                DefectScrapQuantity = request.DefectScrapQuantity,
                TheoreticalReworkWeight = ComputeTheoreticalWeight(request.Weight, request.Quantity, request.DefectReworkQuantity),
                TheoreticalWarehouseWeight = ComputeTheoreticalWeight(request.Weight, request.Quantity, request.DefectWarehouseQuantity),
                TheoreticalScrapWeight = ComputeTheoreticalWeight(request.Weight, request.Quantity, request.DefectScrapQuantity),
                DefectDescription = request.DefectDescription,
                SourceUnit = request.SourceUnit,
                TagNo = request.TagNo,
                PlantGrade = request.PlantGrade,
                Remark = request.Remark,
                DataSource = request.DataSource ?? "MANUAL",
                ProductStatus = ProductStatusHelper.Calculate(request.ProcessName, request.ManufacturingSpec, batch.ManufacturingItem,
                    pgByBatch.GetValueOrDefault(batchId) ?? new(), batch.Specification)
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
            Shift = EnumHelper.TryParse<ShiftType>(e.Shift),
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
            TheoreticalReworkWeight = e.TheoreticalReworkWeight,
            TheoreticalWarehouseWeight = e.TheoreticalWarehouseWeight,
            TheoreticalScrapWeight = e.TheoreticalScrapWeight,
            DefectDescription = e.DefectDescription,
            SourceUnit = e.SourceUnit,
            TagNo = e.TagNo,
            PlantGrade = e.PlantGrade,
            Remark = e.Remark,
            BatchNo = e.BatchNo,
            WorkOrderNo = batchLookup.TryGetValue(e.BatchNo, out var createdBatch) ? createdBatch.WorkOrderNo : null,
            SalesOrderNo = batchLookup.TryGetValue(e.BatchNo, out var cb2) ? cb2.SalesOrderNo : null,
            ProductionMainNo = batchLookup.TryGetValue(e.BatchNo, out var cb3) ? cb3.ProductionMainNo : null,
            DataSource = e.DataSource,
            ProductStatus = e.ProductStatus,
            CreatedTime = e.CreatedTime,
            UpdatedTime = e.UpdatedTime
        }).ToList();
    }

    public async Task<ProcessInspectionDto> UpdateAsync(int id, UpdateProcessInspectionRequest request)
    {
        var entity = await _context.ProcessInspections.FindAsync(id)
            ?? throw new BusinessException($"过程检验记录不存在(Id={id})");

        // 加载批次（用于重量校验）
        var batch = await _context.ProductionBatches.FindAsync(entity.ProductionBatchId);

        // 支数平衡校验：检验支数 = 合格支数 + 返整支数 + 入库支数 + 报废支数
        if (request.Quantity.HasValue)
        {
            var sum = (request.QualifiedQuantity ?? 0)
                + (request.DefectReworkQuantity ?? 0)
                + (request.DefectWarehouseQuantity ?? 0)
                + (request.DefectScrapQuantity ?? 0);
            if (request.Quantity.Value != sum)
                throw new BusinessException($"检验支数({request.Quantity}) ≠ 合格支数({request.QualifiedQuantity ?? 0}) + 返整({request.DefectReworkQuantity ?? 0}) + 入库({request.DefectWarehouseQuantity ?? 0}) + 报废({request.DefectScrapQuantity ?? 0}) = {sum}");
        }

        // 让步放行支数 ≤ 合格支数
        if (request.QualifiedConcessionQuantity.HasValue && request.QualifiedQuantity.HasValue
            && request.QualifiedConcessionQuantity.Value > request.QualifiedQuantity.Value)
            throw new BusinessException($"让步放行支数({request.QualifiedConcessionQuantity})不能大于合格支数({request.QualifiedQuantity})");

        // 检验重量不能大于批次现有效原料重量
        if (request.Weight.HasValue && request.Weight > 0 && batch != null
            && request.Weight.Value > (batch.CurrentValidWeight ?? batch.InputWeight))
            throw new BusinessException($"检验重量({request.Weight})不能大于现有效原料重量({batch.CurrentValidWeight ?? batch.InputWeight})");

        entity.InspectionDate = request.InspectionDate;
        entity.EquipmentName = request.EquipmentName ?? entity.EquipmentName;
        entity.Inspector = request.Inspector ?? entity.Inspector;
        entity.Shift = request.Shift?.ToString() ?? entity.Shift;
        entity.Quantity = request.Quantity ?? entity.Quantity;
        entity.Weight = request.Weight ?? entity.Weight;
        entity.InspectionItem = request.InspectionItem?.ToString() ?? entity.InspectionItem;
        entity.QualifiedQuantity = request.QualifiedQuantity ?? entity.QualifiedQuantity;
        entity.QualifiedWeight = request.QualifiedWeight ?? entity.QualifiedWeight;
        entity.QualifiedConcessionQuantity = request.QualifiedConcessionQuantity ?? entity.QualifiedConcessionQuantity;
        entity.ConcessionRemark = request.ConcessionRemark ?? entity.ConcessionRemark;
        entity.DefectReworkQuantity = request.DefectReworkQuantity ?? 0;
        entity.DefectWarehouseQuantity = request.DefectWarehouseQuantity ?? 0;
        entity.DefectScrapQuantity = request.DefectScrapQuantity ?? 0;

        // 自动计算理论重量
        var effectiveQty = request.Quantity ?? entity.Quantity;
        var effectiveWeight = request.Weight ?? entity.Weight;
        entity.TheoreticalReworkWeight = ComputeTheoreticalWeight(effectiveWeight, effectiveQty, request.DefectReworkQuantity ?? entity.DefectReworkQuantity);
        entity.TheoreticalWarehouseWeight = ComputeTheoreticalWeight(effectiveWeight, effectiveQty, request.DefectWarehouseQuantity ?? entity.DefectWarehouseQuantity);
        entity.TheoreticalScrapWeight = ComputeTheoreticalWeight(effectiveWeight, effectiveQty, request.DefectScrapQuantity ?? entity.DefectScrapQuantity);

        entity.DefectDescription = request.DefectDescription ?? entity.DefectDescription;
        entity.SourceUnit = request.SourceUnit ?? entity.SourceUnit;
        entity.TagNo = request.TagNo ?? entity.TagNo;
        entity.PlantGrade = request.PlantGrade ?? entity.PlantGrade;
        entity.Remark = request.Remark ?? entity.Remark;

        // 重算产品状态（产类）：与生产记录行为一致，更新时基于批次最新信息刷新
        if (batch != null)
        {
            var batchProcessGroups = await _context.ProcessGroups
                .Where(pg => pg.ProductionBatchId == entity.ProductionBatchId)
                .ToListAsync();
            entity.ProductStatus = ProductStatusHelper.Calculate(
                entity.ProcessName, entity.ManufacturingSpec, batch.ManufacturingItem, batchProcessGroups, batch.Specification);
        }

        await _context.SaveChangesAsync();

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
            Shift = EnumHelper.TryParse<ShiftType>(entity.Shift),
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
            TheoreticalReworkWeight = entity.TheoreticalReworkWeight,
            TheoreticalWarehouseWeight = entity.TheoreticalWarehouseWeight,
            TheoreticalScrapWeight = entity.TheoreticalScrapWeight,
            DefectDescription = entity.DefectDescription,
            SourceUnit = entity.SourceUnit,
            TagNo = entity.TagNo,
            PlantGrade = entity.PlantGrade,
            Remark = entity.Remark,
            BatchNo = entity.BatchNo,
            WorkOrderNo = batch?.WorkOrderNo,
            SalesOrderNo = batch?.SalesOrderNo,
            ProductionMainNo = batch?.ProductionMainNo,
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

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var result = await GetAllAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return ProcessInspectionPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? null! : sortBy,
            IsDescending = isDescending,
            InspectionDateFrom = inspectionDateFrom,
            InspectionDateTo = inspectionDateTo
        };
        var result = await GetAllAsync(query);
        return ProcessInspectionPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    private static int? ComputeTheoreticalWeight(decimal? weight, int? quantity, int? defectQuantity)
    {
        if (!weight.HasValue || !quantity.HasValue || quantity.Value <= 0
            || !defectQuantity.HasValue || defectQuantity.Value <= 0)
            return null;
        return (int?)(weight.Value / quantity.Value * defectQuantity.Value);
    }

    private static IQueryable<ProcessInspection> ApplySorting(IQueryable<ProcessInspection> queryable, string sortBy, bool isDescending)
    {
        // 导航字段 WorkOrderNo/SalesOrderNo/ProductionMainNo 需特判（通用 ApplySort 只反射实体属性，不支持导航属性）
        if (sortBy.Equals("workorderno", StringComparison.OrdinalIgnoreCase))
            return isDescending
                ? queryable.OrderByDescending(r => r.ProductionBatch.WorkOrderNo ?? "")
                : queryable.OrderBy(r => r.ProductionBatch.WorkOrderNo ?? "");
        if (sortBy.Equals("salesorderno", StringComparison.OrdinalIgnoreCase))
            return isDescending
                ? queryable.OrderByDescending(r => r.ProductionBatch.SalesOrderNo ?? "")
                : queryable.OrderBy(r => r.ProductionBatch.SalesOrderNo ?? "");
        if (sortBy.Equals("productionmainno", StringComparison.OrdinalIgnoreCase))
            return isDescending
                ? queryable.OrderByDescending(r => r.ProductionBatch.ProductionMainNo ?? "")
                : queryable.OrderBy(r => r.ProductionBatch.ProductionMainNo ?? "");

        return queryable.ApplySort(sortBy, isDescending);
    }
}
