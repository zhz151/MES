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
using MES.Services.Printing;

namespace MES.Services;

public class SectionOutsourceService : ISectionOutsourceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SectionOutsourceService> _logger;
    private readonly IProductionRecordService _productionRecordService;

    public SectionOutsourceService(AppDbContext context, ILogger<SectionOutsourceService> logger,
        IProductionRecordService productionRecordService)
    {
        _context = context;
        _logger = logger;
        _productionRecordService = productionRecordService;
    }

    // ========== 工段委外 ==========

    public async Task<List<SectionOutsourceDto>> GetByIdsAsync(string ids)
    {
        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
        if (idList.Length == 0) return new List<SectionOutsourceDto>();
        return await _context.SectionOutsources
            .AsNoTracking()
            .Include(s => s.ProductionBatch)
            .Include(s => s.OutsourceRecoveries)
            .Where(s => idList.Contains(s.Id))
            .Select(s => new SectionOutsourceDto
            {
                Id = s.Id,
                ProductionBatchId = s.ProductionBatchId,
                ProcessGroupId = s.ProcessGroupId,
                BatchNo = s.ProductionBatch.BatchNo,
                ProcessName = s.ProcessName,
                ManufacturingSpec = s.ManufacturingSpec,
                SectionName = s.SectionName,
                SequenceNumber = s.SequenceNumber,
                OutsourceVendor = s.OutsourceVendor,
                SendOutDate = s.SendOutDate,
                SendQuantity = s.SendQuantity,
                SendWeight = s.SendWeight,
                Status = s.Status.ToString(),
                TagNo = s.TagNo,
                PlantGrade = s.PlantGrade,
                OutsourceSpec = s.OutsourceSpec,
                ExpectedReturnDate = s.ExpectedReturnDate,
                IsUrgent = s.IsUrgent,
                Remark = s.Remark,
                CreatedTime = s.CreatedTime,
                UpdatedTime = s.UpdatedTime,
                TotalRecoveredQuantity = s.OutsourceRecoveries.Sum(r => r.RecoveryQuantity),
                TotalRecoveredWeight = s.OutsourceRecoveries.Sum(r => r.RecoveryWeight),
                TotalUnprocessedQuantity = s.OutsourceRecoveries.Sum(r => r.UnprocessedQuantity),
                TotalUnprocessedWeight = s.OutsourceRecoveries.Sum(r => r.UnprocessedWeight),
                ActualRecoveryDate = s.OutsourceRecoveries.Max(r => (DateTime?)r.RecoveryDate)
            })
            .ToListAsync();
    }

    public async Task<PagedResult<SectionOutsourceDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.SectionOutsources
            .AsNoTracking()
            .Include(s => s.ProductionBatch)
            .Include(s => s.OutsourceRecoveries)
            .AsQueryable();

        // 关键字搜索
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(s =>
                s.OutsourceVendor.Contains(kw) ||
                s.ProcessName.Contains(kw) ||
                s.SectionName.Contains(kw) ||
                s.ProductionBatch.BatchNo.Contains(kw) ||
                (s.TagNo != null && s.TagNo.Contains(kw)) ||
                (s.ManufacturingSpec != null && s.ManufacturingSpec.Contains(kw)) ||
                (s.PlantGrade != null && s.PlantGrade.Contains(kw)) ||
                (s.OutsourceSpec != null && s.OutsourceSpec.Contains(kw)) ||
                (s.Remark != null && s.Remark.Contains(kw)));
        }

        // 发出日期范围筛选
        if (query.SendOutDateFrom.HasValue)
        {
            var from = query.SendOutDateFrom.Value.Date;
            queryable = queryable.Where(s => s.SendOutDate >= from);
        }
        if (query.SendOutDateTo.HasValue)
        {
            var to = query.SendOutDateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(s => s.SendOutDate < to);
        }

        // 实际回收日期范围筛选（需关联回收记录）
        if (query.ActualRecoveryDateFrom.HasValue)
        {
            var from = query.ActualRecoveryDateFrom.Value.Date;
            queryable = queryable.Where(s => s.OutsourceRecoveries.Any(r => r.RecoveryDate >= from));
        }
        if (query.ActualRecoveryDateTo.HasValue)
        {
            var to = query.ActualRecoveryDateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(s => s.OutsourceRecoveries.Any(r => r.RecoveryDate < to));
        }

        // 处理 BatchNo 导航属性筛选（SectionOutsource 实体无 BatchNo 属性，ApplyFilters 反射不到）
        if (query.Filters != null)
        {
            var batchNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("BatchNo", StringComparison.OrdinalIgnoreCase));
            if (batchNoFilter != null && batchNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(s => s.ProductionBatch != null
                    && batchNoFilter.Values.Contains(s.ProductionBatch.BatchNo));
                query.Filters.Remove(batchNoFilter);
            }
        }

        // 处理 ActualRecoveryDate 计算字段筛选（从 OutsourceRecoveries 聚合，非实体属性）
        if (query.Filters != null)
        {
            var actualRecoveryFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("ActualRecoveryDate", StringComparison.OrdinalIgnoreCase));
            if (actualRecoveryFilter != null && actualRecoveryFilter.Values?.Count > 0)
            {
                var parsedDates = actualRecoveryFilter.Values
                    .Select(v => DateTime.TryParse(v, out var d) ? (DateTime?)d.Date : null)
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .ToHashSet();
                if (parsedDates.Count > 0)
                {
                    queryable = queryable.Where(s => s.OutsourceRecoveries.Any(r => parsedDates.Contains(r.RecoveryDate.Date)));
                }
                query.Filters.Remove(actualRecoveryFilter);
            }
        }

        queryable = queryable.ApplyFilters(query.Filters);

        var totalCount = await queryable.CountAsync();

        // 排序
        queryable = (query.SortBy?.ToLower(), query.IsDescending) switch
        {
            ("batchno", false) => queryable.OrderBy(s => s.ProductionBatch.BatchNo),
            ("batchno", true) => queryable.OrderByDescending(s => s.ProductionBatch.BatchNo),
            ("processname", false) => queryable.OrderBy(s => s.ProcessName),
            ("processname", true) => queryable.OrderByDescending(s => s.ProcessName),
            ("sectionname", false) => queryable.OrderBy(s => s.SectionName),
            ("sectionname", true) => queryable.OrderByDescending(s => s.SectionName),
            ("outsourcevendor", false) => queryable.OrderBy(s => s.OutsourceVendor),
            ("outsourcevendor", true) => queryable.OrderByDescending(s => s.OutsourceVendor),
            ("sendoutdate", false) => queryable.OrderBy(s => s.SendOutDate),
            ("sendoutdate", true) => queryable.OrderByDescending(s => s.SendOutDate),
            ("sendquantity", false) => queryable.OrderBy(s => s.SendQuantity ?? 0),
            ("sendquantity", true) => queryable.OrderByDescending(s => s.SendQuantity ?? 0),
            ("sendweight", false) => queryable.OrderBy(s => s.SendWeight ?? 0),
            ("sendweight", true) => queryable.OrderByDescending(s => s.SendWeight ?? 0),
            ("status", false) => queryable.OrderBy(s => s.Status),
            ("status", true) => queryable.OrderByDescending(s => s.Status),
            ("expectedreturndate", false) => queryable.OrderBy(s => s.ExpectedReturnDate ?? DateTime.MaxValue),
            ("expectedreturndate", true) => queryable.OrderByDescending(s => s.ExpectedReturnDate),
            ("manufacturingspec", false) => queryable.OrderBy(s => s.ManufacturingSpec ?? ""),
            ("manufacturingspec", true) => queryable.OrderByDescending(s => s.ManufacturingSpec ?? ""),
            ("sequencenumber", false) => queryable.OrderBy(s => s.SequenceNumber),
            ("sequencenumber", true) => queryable.OrderByDescending(s => s.SequenceNumber),
            ("tagno", false) => queryable.OrderBy(s => s.TagNo ?? ""),
            ("tagno", true) => queryable.OrderByDescending(s => s.TagNo ?? ""),
            ("plantgrade", false) => queryable.OrderBy(s => s.PlantGrade ?? ""),
            ("plantgrade", true) => queryable.OrderByDescending(s => s.PlantGrade ?? ""),
            ("outsourcespec", false) => queryable.OrderBy(s => s.OutsourceSpec ?? ""),
            ("outsourcespec", true) => queryable.OrderByDescending(s => s.OutsourceSpec ?? ""),
            ("isurgent", false) => queryable.OrderBy(s => s.IsUrgent),
            ("isurgent", true) => queryable.OrderByDescending(s => s.IsUrgent),
            ("remark", false) => queryable.OrderBy(s => s.Remark ?? ""),
            ("remark", true) => queryable.OrderByDescending(s => s.Remark ?? ""),
            ("updatedtime", false) => queryable.OrderBy(s => s.UpdatedTime),
            ("updatedtime", true) => queryable.OrderByDescending(s => s.UpdatedTime),
            _ => query.IsDescending
                ? queryable.OrderByDescending(s => s.CreatedTime)
                : queryable.OrderBy(s => s.CreatedTime)
        };

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(s => new SectionOutsourceDto
            {
                Id = s.Id,
                ProductionBatchId = s.ProductionBatchId,
                ProcessGroupId = s.ProcessGroupId,
                BatchNo = s.ProductionBatch.BatchNo,
                ProcessName = s.ProcessName,
                ManufacturingSpec = s.ManufacturingSpec,
                SectionName = s.SectionName,
                SequenceNumber = s.SequenceNumber,
                OutsourceVendor = s.OutsourceVendor,
                SendOutDate = s.SendOutDate,
                SendQuantity = s.SendQuantity,
                SendWeight = s.SendWeight,
                Status = s.Status.ToString(),
                TagNo = s.TagNo,
                PlantGrade = s.PlantGrade,
                OutsourceSpec = s.OutsourceSpec,
                ExpectedReturnDate = s.ExpectedReturnDate,
                IsUrgent = s.IsUrgent,
                Remark = s.Remark,
                CreatedTime = s.CreatedTime,
                UpdatedTime = s.UpdatedTime,
                TotalRecoveredQuantity = s.OutsourceRecoveries.Sum(r => r.RecoveryQuantity),
                TotalRecoveredWeight = s.OutsourceRecoveries.Sum(r => r.RecoveryWeight),
                TotalUnprocessedQuantity = s.OutsourceRecoveries.Sum(r => r.UnprocessedQuantity),
                TotalUnprocessedWeight = s.OutsourceRecoveries.Sum(r => r.UnprocessedWeight),
                ActualRecoveryDate = s.OutsourceRecoveries.Max(r => (DateTime?)r.RecoveryDate)
            })
            .ToListAsync();

        return new PagedResult<SectionOutsourceDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<SectionOutsourceDto> CreateAsync(CreateSectionOutsourceRequest request)
    {
        var batch = await _context.ProductionBatches
            .FirstOrDefaultAsync(b => b.BatchNo == request.BatchNo)
            ?? throw new BusinessException($"批次不存在: {request.BatchNo}");

        // 自动解析 ProcessGroupId 和 SequenceNumber
        var processGroupId = request.ProcessGroupId;
        var sequenceNumber = request.SequenceNumber;
        if (processGroupId == null || processGroupId == 0)
        {
            var pg = await _context.ProcessGroups
                .Where(pg => pg.ProductionBatchId == batch.Id
                    && pg.ProcessName == request.ProcessName
                    && pg.ManufacturingSpec == request.ManufacturingSpec)
                .FirstOrDefaultAsync();
            processGroupId = pg?.Id ?? 0;
            if (pg != null && sequenceNumber == 0)
                sequenceNumber = ResolveSequenceNumber(pg, request.SectionName);
        }
        else if (sequenceNumber == 0)
        {
            var pg = await _context.ProcessGroups.FindAsync(processGroupId.Value);
            if (pg != null)
                sequenceNumber = ResolveSequenceNumber(pg, request.SectionName);
        }

        if (sequenceNumber == 0)
            throw new BusinessException($"工段「{request.SectionName}」不存在于工序组「{request.ProcessName}」中，无法提交");

        var entity = new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = processGroupId ?? 0,
            ProcessName = request.ProcessName,
            ManufacturingSpec = request.ManufacturingSpec,
            SectionName = request.SectionName,
            SequenceNumber = sequenceNumber,
            OutsourceVendor = request.OutsourceVendor,
            SendOutDate = request.SendOutDate,
            SendQuantity = request.SendQuantity,
            SendWeight = request.SendWeight,
            Status = SectionOutsourceStatus.PendingRecovery,
            TagNo = request.TagNo ?? batch.TagNo,
            PlantGrade = request.PlantGrade ?? batch.PlantGrade,
            OutsourceSpec = request.OutsourceSpec,
            ExpectedReturnDate = request.ExpectedReturnDate,
            IsUrgent = request.IsUrgent,
            Remark = request.Remark
        };

        _context.SectionOutsources.Add(entity);
        await _context.SaveChangesAsync();

        await _productionRecordService.RefreshBatchTrackingFieldsAsync(batch.Id);

        _logger.LogInformation("创建工段委外 {SectionName}/{ProcessName} → 批次 {BatchNo}",
            request.SectionName, request.ProcessName, request.BatchNo);

        return ToDto(entity, batch.BatchNo);
    }

    public async Task<List<SectionOutsourceDto>> BatchCreateAsync(List<CreateSectionOutsourceRequest> requests)
    {
        if (requests.Count == 0)
            return new List<SectionOutsourceDto>();

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

        // 预加载所有涉及批次的工序组
        var allBatchIds = batchLookup.Values.Select(b => b.Id).ToList();
        var processGroups = await _context.ProcessGroups
            .Where(pg => allBatchIds.Contains(pg.ProductionBatchId))
            .ToListAsync();
        var pgByBatch = processGroups.GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ========== 业务规则验证 ==========
        var requestErrors = new List<string>();
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var batch = batchLookup[request.BatchNo];

            // 1) 制造规格不能为空
            if (string.IsNullOrWhiteSpace(request.ManufacturingSpec))
                requestErrors.Add($"第{i + 1}行：制造规格不能为空");

            // 2) 发出重量不能大于批次领料重量
            if (request.SendWeight.HasValue && request.SendWeight > 0 && request.SendWeight > batch.InputWeight)
                requestErrors.Add($"第{i + 1}行：发出重量({request.SendWeight})不能大于批次领料重量({batch.InputWeight})");

            // 3) 验证工段存在于工序组中（非0值）
            var pgId = request.ProcessGroupId;
            if (pgId == null || pgId == 0)
            {
                var matchedPg = pgByBatch.GetValueOrDefault(batch.Id)?
                    .FirstOrDefault(pg => pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec);
                pgId = matchedPg?.Id;
            }
            if (pgId > 0)
            {
                var pg = processGroups.FirstOrDefault(pg => pg.Id == pgId.Value);
                if (pg != null && ResolveSequenceNumber(pg, request.SectionName) == 0)
                    requestErrors.Add($"第{i + 1}行：工段「{request.SectionName}」不存在于工序组「{pg.ProcessName}」中，无法提交");
            }
        }
        if (requestErrors.Any())
            throw new BusinessException(string.Join("；", requestErrors));

        var entities = new List<SectionOutsource>();
        foreach (var request in requests)
        {
            var batch = batchLookup[request.BatchNo];

            // 自动解析 ProcessGroupId
            var processGroupId = request.ProcessGroupId;
            if (processGroupId == null || processGroupId == 0)
            {
                var matchedPg = pgByBatch.GetValueOrDefault(batch.Id)?
                    .FirstOrDefault(pg => pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec);
                processGroupId = matchedPg?.Id;
            }

            // 自动解析 SequenceNumber
            var sequenceNumber = request.SequenceNumber;
            if (sequenceNumber == 0 && processGroupId > 0)
            {
                var pg = processGroups.FirstOrDefault(pg => pg.Id == processGroupId.Value);
                if (pg != null)
                    sequenceNumber = ResolveSequenceNumber(pg, request.SectionName);
            }

            entities.Add(new SectionOutsource
            {
                ProductionBatchId = batch.Id,
                ProcessGroupId = processGroupId ?? 0,
                ProcessName = request.ProcessName,
                ManufacturingSpec = request.ManufacturingSpec,
                SectionName = request.SectionName,
                SequenceNumber = sequenceNumber,
                OutsourceVendor = request.OutsourceVendor,
                SendOutDate = request.SendOutDate,
                SendQuantity = request.SendQuantity,
                SendWeight = request.SendWeight,
                Status = SectionOutsourceStatus.PendingRecovery,
                TagNo = request.TagNo ?? batch.TagNo,
                PlantGrade = request.PlantGrade ?? batch.PlantGrade,
                OutsourceSpec = request.OutsourceSpec,
                ExpectedReturnDate = request.ExpectedReturnDate,
                IsUrgent = request.IsUrgent,
                Remark = request.Remark
            });
        }

        _context.SectionOutsources.AddRange(entities);
        await _context.SaveChangesAsync();

        // 批量刷新跟踪字段
        var distinctBatchIds = entities.Select(e => e.ProductionBatchId).Distinct().ToList();
        await _productionRecordService.BatchUpdateBatchTrackingAsync(distinctBatchIds);

        _logger.LogInformation("批量创建工段委外 {Count} 条，涉及 {BatchCount} 个批次",
            entities.Count, distinctBatchIds.Count);

        return entities.Select(e => ToDto(e, batchLookup.Values.First(b => b.Id == e.ProductionBatchId).BatchNo)).ToList();
    }

    public async Task<SectionOutsourceDto> UpdateAsync(int id, UpdateSectionOutsourceRequest request)
    {
        var entity = await _context.SectionOutsources
            .Include(s => s.ProductionBatch)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new BusinessException($"工段委外记录不存在 (Id={id})");

        entity.SendQuantity = request.SendQuantity ?? entity.SendQuantity;
        entity.SendWeight = request.SendWeight ?? entity.SendWeight;
        if (request.OutsourceVendor != null) entity.OutsourceVendor = request.OutsourceVendor;
        if (request.OutsourceSpec != null) entity.OutsourceSpec = request.OutsourceSpec;
        entity.ExpectedReturnDate = request.ExpectedReturnDate ?? entity.ExpectedReturnDate;
        if (request.IsUrgent.HasValue) entity.IsUrgent = request.IsUrgent.Value;
        if (request.Remark != null) entity.Remark = request.Remark;

        await _context.SaveChangesAsync();

        await _productionRecordService.RefreshBatchTrackingFieldsAsync(entity.ProductionBatchId);

        _logger.LogInformation("更新工段委外 (Id={Id})", id);

        return ToDto(entity, entity.ProductionBatch.BatchNo);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.SectionOutsources
            .Include(s => s.OutsourceRecoveries)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new BusinessException("工段委外记录不存在");

        if (entity.OutsourceRecoveries.Count > 0)
            throw new BusinessException("该委外已有回收记录，无法删除");

        var batchId = entity.ProductionBatchId;
        _context.SectionOutsources.Remove(entity);
        await _context.SaveChangesAsync();

        await _productionRecordService.RefreshBatchTrackingFieldsAsync(batchId);

        _logger.LogInformation("删除工段委外 (Id={Id})", id);
    }

    // ========== 委外回收 ==========

    public async Task<List<OutsourceRecoveryDto>> GetRecoveriesAsync(int outsourceId)
    {
        return await _context.OutsourceRecoveries
            .AsNoTracking()
            .Where(r => r.SectionOutsourceId == outsourceId)
            .OrderBy(r => r.RecoveryDate)
            .Select(r => new OutsourceRecoveryDto
            {
                Id = r.Id,
                SectionOutsourceId = r.SectionOutsourceId,
                RecoveryDate = r.RecoveryDate,
                RecoveryQuantity = r.RecoveryQuantity,
                RecoveryWeight = r.RecoveryWeight,
                UnprocessedQuantity = r.UnprocessedQuantity,
                UnprocessedWeight = r.UnprocessedWeight,
                Remark = r.Remark,
                CreatedTime = r.CreatedTime
            })
            .ToListAsync();
    }

    public async Task<PagedResult<OutsourceRecoveryDto>> GetRecoveriesPagedAsync(QueryParams query)
    {
        var queryable = _context.OutsourceRecoveries
            .AsNoTracking()
            .Include(r => r.SectionOutsource)
                .ThenInclude(s => s.ProductionBatch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                r.SectionOutsource.OutsourceVendor.Contains(kw) ||
                r.SectionOutsource.ProcessName.Contains(kw) ||
                r.SectionOutsource.ProductionBatch.BatchNo.Contains(kw) ||
                (r.Remark != null && r.Remark.Contains(kw)) ||
                (r.SectionOutsource.SectionName != null && r.SectionOutsource.SectionName.Contains(kw)) ||
                (r.SectionOutsource.OutsourceSpec != null && r.SectionOutsource.OutsourceSpec.Contains(kw)) ||
                (r.SectionOutsource.ManufacturingSpec != null && r.SectionOutsource.ManufacturingSpec.Contains(kw)) ||
                (r.SectionOutsource.TagNo != null && r.SectionOutsource.TagNo.Contains(kw)) ||
                (r.SectionOutsource.PlantGrade != null && r.SectionOutsource.PlantGrade.Contains(kw)));
        }

        // 回收日期范围筛选
        if (query.RecoveryDateFrom.HasValue)
        {
            var from = query.RecoveryDateFrom.Value.Date;
            queryable = queryable.Where(r => r.RecoveryDate >= from);
        }
        if (query.RecoveryDateTo.HasValue)
        {
            var to = query.RecoveryDateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(r => r.RecoveryDate < to);
        }

        // 处理导航属性筛选（OutsourceRecovery 实体无这些属性，ApplyFilters 反射不到）
        if (query.Filters != null)
        {
            var batchNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("BatchNo", StringComparison.OrdinalIgnoreCase));
            if (batchNoFilter != null && batchNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.SectionOutsource.ProductionBatch.BatchNo != null
                    && batchNoFilter.Values.Contains(r.SectionOutsource.ProductionBatch.BatchNo));
                query.Filters.Remove(batchNoFilter);
            }

            var vendorFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("OutsourceVendor", StringComparison.OrdinalIgnoreCase));
            if (vendorFilter != null && vendorFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.SectionOutsource.OutsourceVendor != null
                    && vendorFilter.Values.Contains(r.SectionOutsource.OutsourceVendor));
                query.Filters.Remove(vendorFilter);
            }

            var processFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("ProcessName", StringComparison.OrdinalIgnoreCase));
            if (processFilter != null && processFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => processFilter.Values.Contains(r.SectionOutsource.ProcessName));
                query.Filters.Remove(processFilter);
            }

            var sectionFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("SectionName", StringComparison.OrdinalIgnoreCase));
            if (sectionFilter != null && sectionFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.SectionOutsource.SectionName != null
                    && sectionFilter.Values.Contains(r.SectionOutsource.SectionName));
                query.Filters.Remove(sectionFilter);
            }

            var mfgSpecFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("ManufacturingSpec", StringComparison.OrdinalIgnoreCase));
            if (mfgSpecFilter != null && mfgSpecFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.SectionOutsource.ManufacturingSpec != null
                    && mfgSpecFilter.Values.Contains(r.SectionOutsource.ManufacturingSpec));
                query.Filters.Remove(mfgSpecFilter);
            }

            var outsourceSpecFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("OutsourceSpec", StringComparison.OrdinalIgnoreCase));
            if (outsourceSpecFilter != null && outsourceSpecFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.SectionOutsource.OutsourceSpec != null
                    && outsourceSpecFilter.Values.Contains(r.SectionOutsource.OutsourceSpec));
                query.Filters.Remove(outsourceSpecFilter);
            }

            var tagNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("TagNo", StringComparison.OrdinalIgnoreCase));
            if (tagNoFilter != null && tagNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.SectionOutsource.TagNo != null
                    && tagNoFilter.Values.Contains(r.SectionOutsource.TagNo));
                query.Filters.Remove(tagNoFilter);
            }

            var plantGradeFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("PlantGrade", StringComparison.OrdinalIgnoreCase));
            if (plantGradeFilter != null && plantGradeFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.SectionOutsource.PlantGrade != null
                    && plantGradeFilter.Values.Contains(r.SectionOutsource.PlantGrade));
                query.Filters.Remove(plantGradeFilter);
            }
        }

        queryable = queryable.ApplyFilters(query.Filters);

        var totalCount = await queryable.CountAsync();

        queryable = (query.SortBy?.ToLower(), query.IsDescending) switch
        {
            ("recoverydate", false) => queryable.OrderBy(r => r.RecoveryDate),
            ("recoverydate", true) => queryable.OrderByDescending(r => r.RecoveryDate),
            ("recoveryquantity", false) => queryable.OrderBy(r => r.RecoveryQuantity ?? 0),
            ("recoveryquantity", true) => queryable.OrderByDescending(r => r.RecoveryQuantity ?? 0),
            ("recoveryweight", false) => queryable.OrderBy(r => r.RecoveryWeight ?? 0),
            ("recoveryweight", true) => queryable.OrderByDescending(r => r.RecoveryWeight ?? 0),
            ("unprocessedquantity", false) => queryable.OrderBy(r => r.UnprocessedQuantity ?? 0),
            ("unprocessedquantity", true) => queryable.OrderByDescending(r => r.UnprocessedQuantity ?? 0),
            ("unprocessedweight", false) => queryable.OrderBy(r => r.UnprocessedWeight ?? 0),
            ("unprocessedweight", true) => queryable.OrderByDescending(r => r.UnprocessedWeight ?? 0),
            ("remark", false) => queryable.OrderBy(r => r.Remark ?? ""),
            ("remark", true) => queryable.OrderByDescending(r => r.Remark ?? ""),
            ("batchno", false) => queryable.OrderBy(r => r.SectionOutsource.ProductionBatch.BatchNo),
            ("batchno", true) => queryable.OrderByDescending(r => r.SectionOutsource.ProductionBatch.BatchNo),
            ("outsourcevendor", false) => queryable.OrderBy(r => r.SectionOutsource.OutsourceVendor),
            ("outsourcevendor", true) => queryable.OrderByDescending(r => r.SectionOutsource.OutsourceVendor),
            ("processname", false) => queryable.OrderBy(r => r.SectionOutsource.ProcessName),
            ("processname", true) => queryable.OrderByDescending(r => r.SectionOutsource.ProcessName),
            ("sectionname", false) => queryable.OrderBy(r => r.SectionOutsource.SectionName),
            ("sectionname", true) => queryable.OrderByDescending(r => r.SectionOutsource.SectionName),
            ("manufacturingspec", false) => queryable.OrderBy(r => r.SectionOutsource.ManufacturingSpec ?? ""),
            ("manufacturingspec", true) => queryable.OrderByDescending(r => r.SectionOutsource.ManufacturingSpec ?? ""),
            ("outsourcespec", false) => queryable.OrderBy(r => r.SectionOutsource.OutsourceSpec ?? ""),
            ("outsourcespec", true) => queryable.OrderByDescending(r => r.SectionOutsource.OutsourceSpec ?? ""),
            ("tagno", false) => queryable.OrderBy(r => r.SectionOutsource.TagNo ?? ""),
            ("tagno", true) => queryable.OrderByDescending(r => r.SectionOutsource.TagNo ?? ""),
            ("plantgrade", false) => queryable.OrderBy(r => r.SectionOutsource.PlantGrade ?? ""),
            ("plantgrade", true) => queryable.OrderByDescending(r => r.SectionOutsource.PlantGrade ?? ""),
            ("sendquantity", false) => queryable.OrderBy(r => r.SectionOutsource.SendQuantity ?? 0),
            ("sendquantity", true) => queryable.OrderByDescending(r => r.SectionOutsource.SendQuantity ?? 0),
            ("sendweight", false) => queryable.OrderBy(r => r.SectionOutsource.SendWeight ?? 0),
            ("sendweight", true) => queryable.OrderByDescending(r => r.SectionOutsource.SendWeight ?? 0),
            _ => query.IsDescending
                ? queryable.OrderByDescending(r => r.CreatedTime)
                : queryable.OrderBy(r => r.CreatedTime)
        };

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new OutsourceRecoveryDto
            {
                Id = r.Id,
                SectionOutsourceId = r.SectionOutsourceId,
                RecoveryDate = r.RecoveryDate,
                RecoveryQuantity = r.RecoveryQuantity,
                RecoveryWeight = r.RecoveryWeight,
                UnprocessedQuantity = r.UnprocessedQuantity,
                UnprocessedWeight = r.UnprocessedWeight,
                Remark = r.Remark,
                CreatedTime = r.CreatedTime,
                BatchNo = r.SectionOutsource.ProductionBatch.BatchNo,
                OutsourceVendor = r.SectionOutsource.OutsourceVendor,
                ProcessName = r.SectionOutsource.ProcessName,
                SectionName = r.SectionOutsource.SectionName,
                ManufacturingSpec = r.SectionOutsource.ManufacturingSpec,
                OutsourceSpec = r.SectionOutsource.OutsourceSpec,
                SendQuantity = r.SectionOutsource.SendQuantity,
                SendWeight = r.SectionOutsource.SendWeight,
                TagNo = r.SectionOutsource.TagNo,
                PlantGrade = r.SectionOutsource.PlantGrade
            })
            .ToListAsync();

        return new PagedResult<OutsourceRecoveryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<OutsourceRecoveryDto> CreateRecoveryAsync(CreateOutsourceRecoveryRequest request)
    {
        var outsource = await _context.SectionOutsources.FindAsync(request.SectionOutsourceId)
            ?? throw new BusinessException("工段委外记录不存在");

        var entity = new OutsourceRecovery
        {
            SectionOutsourceId = request.SectionOutsourceId,
            RecoveryDate = request.RecoveryDate,
            RecoveryQuantity = request.RecoveryQuantity,
            RecoveryWeight = request.RecoveryWeight,
            UnprocessedQuantity = request.UnprocessedQuantity,
            UnprocessedWeight = request.UnprocessedWeight,
            Remark = request.Remark
        };

        _context.OutsourceRecoveries.Add(entity);
        await _context.SaveChangesAsync();

        // 按重量 99% 阈值更新委外状态
        await UpdateOutsourceStatusByWeight(outsource);

        await _productionRecordService.RefreshBatchTrackingFieldsAsync(outsource.ProductionBatchId);

        _logger.LogInformation("创建委外回收 (SectionOutsourceId={Id})", request.SectionOutsourceId);

        return new OutsourceRecoveryDto
        {
            Id = entity.Id,
            SectionOutsourceId = entity.SectionOutsourceId,
            RecoveryDate = entity.RecoveryDate,
            RecoveryQuantity = entity.RecoveryQuantity,
            RecoveryWeight = entity.RecoveryWeight,
            UnprocessedQuantity = entity.UnprocessedQuantity,
            UnprocessedWeight = entity.UnprocessedWeight,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime
        };
    }

    public async Task<List<OutsourceRecoveryDto>> BatchCreateRecoveriesAsync(List<CreateOutsourceRecoveryRequest> requests)
    {
        if (requests.Count == 0)
            return new List<OutsourceRecoveryDto>();

        // 预加载所有涉及的委外记录
        var outsourceIds = requests.Select(r => r.SectionOutsourceId).Distinct().ToList();
        var outsourceLookup = await _context.SectionOutsources
            .Where(s => outsourceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        foreach (var id in outsourceIds)
        {
            if (!outsourceLookup.ContainsKey(id))
                throw new BusinessException($"工段委外记录不存在: {id}");
        }

        var entities = new List<OutsourceRecovery>();
        foreach (var request in requests)
        {
            entities.Add(new OutsourceRecovery
            {
                SectionOutsourceId = request.SectionOutsourceId,
                RecoveryDate = request.RecoveryDate,
                RecoveryQuantity = request.RecoveryQuantity,
                RecoveryWeight = request.RecoveryWeight,
                UnprocessedQuantity = request.UnprocessedQuantity,
                UnprocessedWeight = request.UnprocessedWeight,
                Remark = request.Remark
            });
        }

        _context.OutsourceRecoveries.AddRange(entities);
        await _context.SaveChangesAsync();

        // 批量更新委外状态（单次查询 + 单次 SaveChangesAsync）
        var modifiedOutsourceIds = requests.Select(r => r.SectionOutsourceId).Distinct().ToList();
        var affectedOutsources = modifiedOutsourceIds.Select(oid => outsourceLookup[oid]).ToList();
        var batchIds = new HashSet<int>(affectedOutsources.Select(o => o.ProductionBatchId));

        await BatchUpdateOutsourceStatusByWeightAsync(affectedOutsources);

        // 批量刷新跟踪字段
        await _productionRecordService.BatchUpdateBatchTrackingAsync(batchIds);

        _logger.LogInformation("批量创建委外回收 {Count} 条，涉及 {BatchCount} 个批次",
            entities.Count, batchIds.Count);

        return entities.Select(e => new OutsourceRecoveryDto
        {
            Id = e.Id,
            SectionOutsourceId = e.SectionOutsourceId,
            RecoveryDate = e.RecoveryDate,
            RecoveryQuantity = e.RecoveryQuantity,
            RecoveryWeight = e.RecoveryWeight,
            UnprocessedQuantity = e.UnprocessedQuantity,
            UnprocessedWeight = e.UnprocessedWeight,
            Remark = e.Remark,
            CreatedTime = e.CreatedTime
        }).ToList();
    }

    public async Task<OutsourceRecoveryDto> UpdateRecoveryAsync(int id, UpdateOutsourceRecoveryRequest request)
    {
        var entity = await _context.OutsourceRecoveries.FindAsync(id)
            ?? throw new BusinessException($"委外回收记录不存在 (Id={id})");

        if (request.RecoveryDate.HasValue) entity.RecoveryDate = request.RecoveryDate.Value;
        entity.RecoveryQuantity = request.RecoveryQuantity ?? entity.RecoveryQuantity;
        entity.RecoveryWeight = request.RecoveryWeight ?? entity.RecoveryWeight;
        entity.UnprocessedQuantity = request.UnprocessedQuantity ?? entity.UnprocessedQuantity;
        entity.UnprocessedWeight = request.UnprocessedWeight ?? entity.UnprocessedWeight;
        if (request.Remark != null) entity.Remark = request.Remark;

        await _context.SaveChangesAsync();

        // 重新计算委外状态
        var outsource = await _context.SectionOutsources.FindAsync(entity.SectionOutsourceId);
        if (outsource != null)
        {
            await UpdateOutsourceStatusByWeight(outsource);
            await _productionRecordService.RefreshBatchTrackingFieldsAsync(outsource.ProductionBatchId);
        }

        return new OutsourceRecoveryDto
        {
            Id = entity.Id,
            SectionOutsourceId = entity.SectionOutsourceId,
            RecoveryDate = entity.RecoveryDate,
            RecoveryQuantity = entity.RecoveryQuantity,
            RecoveryWeight = entity.RecoveryWeight,
            UnprocessedQuantity = entity.UnprocessedQuantity,
            UnprocessedWeight = entity.UnprocessedWeight,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime
        };
    }

    public async Task DeleteRecoveryAsync(int id)
    {
        var entity = await _context.OutsourceRecoveries.FindAsync(id)
            ?? throw new BusinessException("委外回收记录不存在");

        var outsourceId = entity.SectionOutsourceId;
        _context.OutsourceRecoveries.Remove(entity);
        await _context.SaveChangesAsync();

        // 重新计算委外状态
        var outsource = await _context.SectionOutsources.FindAsync(outsourceId);
        if (outsource != null)
        {
            await UpdateOutsourceStatusByWeight(outsource);
            await _productionRecordService.RefreshBatchTrackingFieldsAsync(outsource.ProductionBatchId);
        }

        _logger.LogInformation("删除委外回收 (Id={Id})", id);
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var items = await _context.SectionOutsources
            .AsNoTracking()
            .Include(s => s.ProductionBatch)
            .Include(s => s.OutsourceRecoveries)
            .Where(s => ids.Contains(s.Id))
            .ToListAsync();

        if (items.Count == 0)
            throw new BusinessException("未找到选中的委外数据");

        var data = items.Select(s => new Dictionary<string, object>
        {
            ["BatchNo"] = s.ProductionBatch.BatchNo,
            ["ProcessName"] = s.ProcessName,
            ["ManufacturingSpec"] = s.ManufacturingSpec ?? "",
            ["SectionName"] = s.SectionName,
            ["SequenceNumber"] = s.SequenceNumber,
            ["OutsourceVendor"] = s.OutsourceVendor,
            ["SendOutDate"] = s.SendOutDate.ToString("yyyy-MM-dd"),
            ["SendQuantity"] = s.SendQuantity,
            ["SendWeight"] = s.SendWeight,
            ["Status"] = s.Status,
            ["TagNo"] = s.TagNo ?? "",
            ["PlantGrade"] = s.PlantGrade ?? "",
            ["OutsourceSpec"] = s.OutsourceSpec ?? "",
            ["ExpectedReturnDate"] = s.ExpectedReturnDate?.ToString("yyyy-MM-dd") ?? "",
            ["IsUrgent"] = s.IsUrgent ? "是" : "否",
            ["TotalRecoveredQuantity"] = s.OutsourceRecoveries.Sum(r => r.RecoveryQuantity) ?? 0,
            ["TotalRecoveredWeight"] = s.OutsourceRecoveries.Sum(r => r.RecoveryWeight) ?? 0,
            ["TotalUnprocessedQuantity"] = s.OutsourceRecoveries.Sum(r => r.UnprocessedQuantity) ?? 0,
            ["TotalUnprocessedWeight"] = s.OutsourceRecoveries.Sum(r => r.UnprocessedWeight) ?? 0,
            ["ActualRecoveryDate"] = s.OutsourceRecoveries.Max(r => (DateTime?)r.RecoveryDate)?.ToString("yyyy-MM-dd") ?? ""
        }).ToList();

        return TablePrintHelper.GeneratePdf("工段委外列表", data, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending,
        DateTime? sendOutDateFrom, DateTime? sendOutDateTo,
        DateTime? actualRecoveryDateFrom, DateTime? actualRecoveryDateTo,
        List<PrintColumnDef> columns)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "createdtime",
            IsDescending = isDescending,
            SendOutDateFrom = sendOutDateFrom,
            SendOutDateTo = sendOutDateTo,
            ActualRecoveryDateFrom = actualRecoveryDateFrom,
            ActualRecoveryDateTo = actualRecoveryDateTo
        };
        var paged = await GetPagedAsync(query);

        var data = paged.Items.Select(s => new Dictionary<string, object>
        {
            ["BatchNo"] = s.BatchNo,
            ["ProcessName"] = s.ProcessName,
            ["ManufacturingSpec"] = s.ManufacturingSpec ?? "",
            ["SectionName"] = s.SectionName,
            ["SequenceNumber"] = s.SequenceNumber,
            ["OutsourceVendor"] = s.OutsourceVendor,
            ["SendOutDate"] = s.SendOutDate.ToString("yyyy-MM-dd"),
            ["SendQuantity"] = s.SendQuantity,
            ["SendWeight"] = s.SendWeight,
            ["Status"] = s.Status,
            ["TagNo"] = s.TagNo ?? "",
            ["PlantGrade"] = s.PlantGrade ?? "",
            ["OutsourceSpec"] = s.OutsourceSpec ?? "",
            ["ExpectedReturnDate"] = s.ExpectedReturnDate?.ToString("yyyy-MM-dd") ?? "",
            ["IsUrgent"] = s.IsUrgent ? "是" : "否",
            ["TotalRecoveredQuantity"] = s.TotalRecoveredQuantity ?? 0,
            ["TotalRecoveredWeight"] = s.TotalRecoveredWeight ?? 0,
            ["TotalUnprocessedQuantity"] = s.TotalUnprocessedQuantity ?? 0,
            ["TotalUnprocessedWeight"] = s.TotalUnprocessedWeight ?? 0,
            ["ActualRecoveryDate"] = s.ActualRecoveryDate?.ToString("yyyy-MM-dd") ?? ""
        }).ToList();

        return TablePrintHelper.GeneratePdf("工段委外列表", data, columns);
    }

    public async Task<byte[]> PrintRecoveryBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var items = await _context.OutsourceRecoveries
            .AsNoTracking()
            .Include(r => r.SectionOutsource)
                .ThenInclude(s => s.ProductionBatch)
            .Where(r => ids.Contains(r.Id))
            .ToListAsync();

        if (items.Count == 0)
            throw new BusinessException("未找到选中的回收数据");

        var data = items.Select(r => new Dictionary<string, object>
        {
            ["RecoveryDate"] = r.RecoveryDate.ToString("yyyy-MM-dd"),
            ["BatchNo"] = r.SectionOutsource.ProductionBatch.BatchNo,
            ["OutsourceVendor"] = r.SectionOutsource.OutsourceVendor,
            ["ProcessName"] = r.SectionOutsource.ProcessName,
            ["SectionName"] = r.SectionOutsource.SectionName,
            ["ManufacturingSpec"] = r.SectionOutsource.ManufacturingSpec ?? "",
            ["OutsourceSpec"] = r.SectionOutsource.OutsourceSpec ?? "",
            ["SendQuantity"] = r.SectionOutsource.SendQuantity ?? 0,
            ["SendWeight"] = r.SectionOutsource.SendWeight ?? 0,
            ["TagNo"] = r.SectionOutsource.TagNo ?? "",
            ["PlantGrade"] = r.SectionOutsource.PlantGrade ?? "",
            ["RecoveryQuantity"] = r.RecoveryQuantity ?? 0,
            ["RecoveryWeight"] = r.RecoveryWeight ?? 0,
            ["UnprocessedQuantity"] = r.UnprocessedQuantity ?? 0,
            ["UnprocessedWeight"] = r.UnprocessedWeight ?? 0,
            ["Remark"] = r.Remark ?? ""
        }).ToList();

        return TablePrintHelper.GeneratePdf("委外回收列表", data, columns);
    }

    public async Task<byte[]> PrintRecoveryAllAsync(string? keyword, string? sortBy, bool isDescending,
        DateTime? recoveryDateFrom, DateTime? recoveryDateTo,
        List<PrintColumnDef> columns)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "recoverydate",
            IsDescending = isDescending,
            RecoveryDateFrom = recoveryDateFrom,
            RecoveryDateTo = recoveryDateTo
        };
        var paged = await GetRecoveriesPagedAsync(query);

        var data = paged.Items.Select(r => new Dictionary<string, object>
        {
            ["RecoveryDate"] = r.RecoveryDate.ToString("yyyy-MM-dd"),
            ["BatchNo"] = r.BatchNo ?? "",
            ["OutsourceVendor"] = r.OutsourceVendor ?? "",
            ["ProcessName"] = r.ProcessName ?? "",
            ["SectionName"] = r.SectionName ?? "",
            ["ManufacturingSpec"] = r.ManufacturingSpec ?? "",
            ["OutsourceSpec"] = r.OutsourceSpec ?? "",
            ["SendQuantity"] = r.SendQuantity ?? 0,
            ["SendWeight"] = r.SendWeight ?? 0,
            ["TagNo"] = r.TagNo ?? "",
            ["PlantGrade"] = r.PlantGrade ?? "",
            ["RecoveryQuantity"] = r.RecoveryQuantity ?? 0,
            ["RecoveryWeight"] = r.RecoveryWeight ?? 0,
            ["UnprocessedQuantity"] = r.UnprocessedQuantity ?? 0,
            ["UnprocessedWeight"] = r.UnprocessedWeight ?? 0,
            ["Remark"] = r.Remark ?? ""
        }).ToList();

        return TablePrintHelper.GeneratePdf("委外回收列表", data, columns);
    }

    // ========== 筛选上下文 ==========

    public async Task<Dictionary<string, List<string>>> GetOutsourceRecoveryFilterContextsAsync()
    {
        var recoveries = _context.OutsourceRecoveries
            .AsNoTracking()
            .Include(r => r.SectionOutsource)
                .ThenInclude(s => s.ProductionBatch);

        return new Dictionary<string, List<string>>
        {
            ["BatchNo"] = await recoveries
                .Select(r => r.SectionOutsource.ProductionBatch.BatchNo)
                .Distinct().OrderBy(x => x).ToListAsync(),
            ["OutsourceVendor"] = await recoveries
                .Select(r => r.SectionOutsource.OutsourceVendor)
                .Distinct().OrderBy(x => x).ToListAsync(),
            ["ProcessName"] = await recoveries
                .Select(r => r.SectionOutsource.ProcessName)
                .Distinct().OrderBy(x => x).ToListAsync(),
            ["SectionName"] = await recoveries
                .Select(r => r.SectionOutsource.SectionName)
                .Distinct().OrderBy(x => x).ToListAsync(),
            ["ManufacturingSpec"] = await recoveries
                .Where(r => r.SectionOutsource.ManufacturingSpec != null)
                .Select(r => r.SectionOutsource.ManufacturingSpec!)
                .Distinct().OrderBy(x => x).ToListAsync(),
            ["OutsourceSpec"] = await recoveries
                .Where(r => r.SectionOutsource.OutsourceSpec != null)
                .Select(r => r.SectionOutsource.OutsourceSpec!)
                .Distinct().OrderBy(x => x).ToListAsync(),
            ["TagNo"] = await recoveries
                .Where(r => r.SectionOutsource.TagNo != null)
                .Select(r => r.SectionOutsource.TagNo!)
                .Distinct().OrderBy(x => x).ToListAsync(),
            ["PlantGrade"] = await recoveries
                .Where(r => r.SectionOutsource.PlantGrade != null)
                .Select(r => r.SectionOutsource.PlantGrade!)
                .Distinct().OrderBy(x => x).ToListAsync(),
            ["Remark"] = await recoveries
                .Where(r => r.Remark != null)
                .Select(r => r.Remark!)
                .Distinct().OrderBy(x => x).ToListAsync(),
            ["RecoveryDate"] = await recoveries
                .Select(r => r.RecoveryDate.ToString("yyyy-MM-dd"))
                .Distinct().OrderBy(x => x).ToListAsync(),
            ["CreatedTime"] = await recoveries
                .Select(r => r.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"))
                .Distinct().OrderBy(x => x).ToListAsync(),
        };
    }

    /// <summary>
    /// 获取工段委外发出筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.SectionOutsources
            .AsNoTracking()
            .Include(s => s.ProductionBatch)
            .Include(s => s.OutsourceRecoveries);

        // 注意：枚举列（Status）不在此处返回，
        // 由前端 EnumOptions fallback 直接提供带中文 Display 的选项，避免映射丢失。
        var results = await query.Select(s => new
        {
            s.ProductionBatch.BatchNo,
            s.ProcessName,
            s.ManufacturingSpec,
            s.SectionName,
            s.OutsourceVendor,
            s.TagNo,
            s.PlantGrade,
            s.OutsourceSpec,
            s.Remark,
            s.SendOutDate,
            s.ExpectedReturnDate,
            ActualRecoveryDate = s.OutsourceRecoveries.Max(r => (DateTime?)r.RecoveryDate)
        }).ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["BatchNo"] = results.Select(x => x.BatchNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["ProcessName"] = results.Select(x => x.ProcessName).Distinct().OrderBy(x => x).ToList(),
            ["ManufacturingSpec"] = results.Select(x => x.ManufacturingSpec).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["SectionName"] = results.Select(x => x.SectionName).Distinct().OrderBy(x => x).ToList(),
            ["OutsourceVendor"] = results.Select(x => x.OutsourceVendor).Distinct().OrderBy(x => x).ToList(),
            ["TagNo"] = results.Select(x => x.TagNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["PlantGrade"] = results.Select(x => x.PlantGrade).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["OutsourceSpec"] = results.Select(x => x.OutsourceSpec).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Remark"] = results.Select(x => x.Remark).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["SendOutDate"] = results.Select(x => x.SendOutDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["ExpectedReturnDate"] = results.Where(x => x.ExpectedReturnDate.HasValue)
                .Select(x => x.ExpectedReturnDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["ActualRecoveryDate"] = results.Where(x => x.ActualRecoveryDate.HasValue)
                .Select(x => x.ActualRecoveryDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
        };
    }

    // ========== 辅助方法 ==========

    private static SectionOutsourceDto ToDto(SectionOutsource entity, string batchNo)
    {
        return new SectionOutsourceDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            ProcessGroupId = entity.ProcessGroupId,
            BatchNo = batchNo,
            ProcessName = entity.ProcessName,
            ManufacturingSpec = entity.ManufacturingSpec,
            SectionName = entity.SectionName,
            SequenceNumber = entity.SequenceNumber,
            OutsourceVendor = entity.OutsourceVendor,
            SendOutDate = entity.SendOutDate,
            SendQuantity = entity.SendQuantity,
            SendWeight = entity.SendWeight,
            Status = entity.Status.ToString(),
            TagNo = entity.TagNo,
            PlantGrade = entity.PlantGrade,
            OutsourceSpec = entity.OutsourceSpec,
            ExpectedReturnDate = entity.ExpectedReturnDate,
            IsUrgent = entity.IsUrgent,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    /// <summary>
    /// 批量按重量判定状态：单次查询全部合计 + 单次 SaveChangesAsync
    /// </summary>
    private async Task BatchUpdateOutsourceStatusByWeightAsync(List<SectionOutsource> outsources)
    {
        if (outsources.Count == 0) return;

        var ids = outsources.Select(o => o.Id).ToList();
        var totalsDict = await _context.OutsourceRecoveries
            .Where(r => ids.Contains(r.SectionOutsourceId))
            .GroupBy(r => r.SectionOutsourceId)
            .Select(g => new
            {
                SectionOutsourceId = g.Key,
                TotalWeight = g.Sum(r => (r.RecoveryWeight ?? 0) + (r.UnprocessedWeight ?? 0))
            })
            .ToDictionaryAsync(g => g.SectionOutsourceId, g => g.TotalWeight);

        var anyChange = false;
        foreach (var outsource in outsources)
        {
            var totalRecoveredWeight = totalsDict.GetValueOrDefault(outsource.Id, 0m);
            var threshold = outsource.SendWeight.HasValue && outsource.SendWeight.Value > 0
                ? outsource.SendWeight.Value * 0.99m
                : 0m;

            var isCompleted = outsource.SendWeight.HasValue && totalRecoveredWeight >= threshold;

            if (isCompleted && outsource.Status != SectionOutsourceStatus.Recovered)
            {
                outsource.Status = SectionOutsourceStatus.Recovered;
                anyChange = true;
                _logger.LogInformation("工段委外 (Id={Id}) 批量回收完成，状态→已回收", outsource.Id);
            }
            else if (!isCompleted && outsource.Status != SectionOutsourceStatus.PendingRecovery)
            {
                outsource.Status = SectionOutsourceStatus.PendingRecovery;
                anyChange = true;
            }
        }

        if (anyChange)
            await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 按重量判定：正常回收重量 + 非正常回收重量 >= 发出重量 × 0.99 时标记为"已回收"
    /// </summary>
    private async Task UpdateOutsourceStatusByWeight(SectionOutsource outsource)
    {
        var totals = await _context.OutsourceRecoveries
            .Where(r => r.SectionOutsourceId == outsource.Id)
            .GroupBy(r => r.SectionOutsourceId)
            .Select(g => new
            {
                TotalWeight = g.Sum(r => (r.RecoveryWeight ?? 0) + (r.UnprocessedWeight ?? 0))
            })
            .FirstOrDefaultAsync();

        var totalRecoveredWeight = totals?.TotalWeight ?? 0m;
        var threshold = outsource.SendWeight.HasValue && outsource.SendWeight.Value > 0
            ? outsource.SendWeight.Value * 0.99m
            : 0m;

        var isCompleted = outsource.SendWeight.HasValue && totalRecoveredWeight >= threshold;

        if (isCompleted && outsource.Status != SectionOutsourceStatus.Recovered)
        {
            outsource.Status = SectionOutsourceStatus.Recovered;
            _context.SectionOutsources.Update(outsource);
            await _context.SaveChangesAsync();
            _logger.LogInformation("工段委外 (Id={Id}) 回收完成，状态→已回收", outsource.Id);
        }
        else if (!isCompleted && outsource.Status != SectionOutsourceStatus.PendingRecovery)
        {
            outsource.Status = SectionOutsourceStatus.PendingRecovery;
            _context.SectionOutsources.Update(outsource);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 根据工段名称从工序组中解析对应的执行序号
    /// </summary>
    private static int ResolveSequenceNumber(ProcessGroup pg, string sectionName)
    {
        return sectionName switch
        {
            "冷轧拔" => pg.ColdRollDraw ?? 0,
            "油管断" => pg.OilPipeCut ?? 0,
            "去油" => pg.Degrease ?? 0,
            "固溶" => pg.Solution ?? 0,
            "矫直" => pg.Straighten ?? 0,
            "断切" => pg.Cut ?? 0,
            "测壁厚" => pg.ThicknessMeasure ?? 0,
            "酸洗" => pg.Pickle ?? 0,
            "外抛光" => pg.OuterPolish ?? 0,
            "内修磨" => pg.InnerGrinding ?? 0,
            "外点磨" => pg.OuterSpotGrinding ?? 0,
            "检验" => pg.Inspection ?? 0,
            "打焊头" => pg.WeldingHead ?? 0,
            "润滑" => pg.Lubrication ?? 0,
            "入库" => pg.Warehouse ?? 0,
            _ => 0
        };
    }
}
