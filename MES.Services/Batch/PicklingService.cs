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
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Exceptions;
using MES.Core.Constants;
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
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;
using MES.Services.Extensions;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Batch;

public class PicklingService : IPicklingService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PicklingService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IProductionRecordService _productionRecordService;

    public PicklingService(AppDbContext context, ILogger<PicklingService> logger, IMemoryCache cache,
        IProductionRecordService productionRecordService)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
        _productionRecordService = productionRecordService;
    }

    // ========== 入缸记录 ==========

    public async Task<PagedResult<PicklingInRecordDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.PicklingInRecords
            .AsNoTracking()
            .Include(s => s.ProductionBatch)
            .Include(s => s.PicklingOutRecords)
            .AsQueryable();

        // 关键字搜索
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(s =>
                s.ProcessName.Contains(kw) ||
                s.SectionName.Contains(kw) ||
                s.ProductionBatch.BatchNo.Contains(kw) ||
                (s.TagNo != null && s.TagNo.Contains(kw)) ||
                (s.ManufacturingSpec != null && s.ManufacturingSpec.Contains(kw)) ||
                (s.PlantGrade != null && s.PlantGrade.Contains(kw)) ||
                (s.Remark != null && s.Remark.Contains(kw)) ||
                (s.EquipmentName != null && s.EquipmentName.Contains(kw)) ||
                (s.Operator != null && s.Operator.Contains(kw)) ||
                (s.Shift != null && s.Shift.Contains(kw)) ||
                (s.DataSource != null && s.DataSource.Contains(kw)));
        }

        // 入缸日期范围筛选
        if (query.InDateFrom.HasValue)
        {
            var from = query.InDateFrom.Value.Date;
            queryable = queryable.Where(s => s.InDate >= from);
        }
        if (query.InDateTo.HasValue)
        {
            var to = query.InDateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(s => s.InDate < to);
        }

        // 完工日期范围筛选
        if (query.CompleteDateFrom.HasValue)
        {
            var from = query.CompleteDateFrom.Value.Date;
            queryable = queryable.Where(s => s.PicklingOutRecords.Any(r => r.CompleteDate >= from));
        }
        if (query.CompleteDateTo.HasValue)
        {
            var to = query.CompleteDateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(s => s.PicklingOutRecords.Any(r => r.CompleteDate < to));
        }

        // 处理 BatchNo 导航属性筛选
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
            ("indate", false) => queryable.OrderBy(s => s.InDate),
            ("indate", true) => queryable.OrderByDescending(s => s.InDate),
            ("status", false) => queryable.OrderBy(s => s.Status),
            ("status", true) => queryable.OrderByDescending(s => s.Status),
            ("manufacturingspec", false) => queryable.OrderBy(s => s.ManufacturingSpec ?? ""),
            ("manufacturingspec", true) => queryable.OrderByDescending(s => s.ManufacturingSpec ?? ""),
            ("sequencenumber", false) => queryable.OrderBy(s => s.SequenceNumber),
            ("sequencenumber", true) => queryable.OrderByDescending(s => s.SequenceNumber),
            ("datasource", false) => queryable.OrderBy(s => s.DataSource ?? ""),
            ("datasource", true) => queryable.OrderByDescending(s => s.DataSource ?? ""),
            ("tagno", false) => queryable.OrderBy(s => s.TagNo ?? ""),
            ("tagno", true) => queryable.OrderByDescending(s => s.TagNo ?? ""),
            ("plantgrade", false) => queryable.OrderBy(s => s.PlantGrade ?? ""),
            ("plantgrade", true) => queryable.OrderByDescending(s => s.PlantGrade ?? ""),
            ("remark", false) => queryable.OrderBy(s => s.Remark ?? ""),
            ("remark", true) => queryable.OrderByDescending(s => s.Remark ?? ""),
            ("updatedtime", false) => queryable.OrderBy(s => s.UpdatedTime),
            ("updatedtime", true) => queryable.OrderByDescending(s => s.UpdatedTime),
            ("equipmentname", false) => queryable.OrderBy(s => s.EquipmentName ?? ""),
            ("equipmentname", true) => queryable.OrderByDescending(s => s.EquipmentName ?? ""),
            ("operator", false) => queryable.OrderBy(s => s.Operator ?? ""),
            ("operator", true) => queryable.OrderByDescending(s => s.Operator ?? ""),
            ("shift", false) => queryable.OrderBy(s => s.Shift ?? ""),
            ("shift", true) => queryable.OrderByDescending(s => s.Shift ?? ""),
            ("quantity", false) => queryable.OrderBy(s => s.Quantity ?? 0),
            ("quantity", true) => queryable.OrderByDescending(s => s.Quantity ?? 0),
            ("weight", false) => queryable.OrderBy(s => s.Weight ?? 0),
            ("weight", true) => queryable.OrderByDescending(s => s.Weight ?? 0),
            ("productstatus", false) => queryable.OrderBy(s => s.ProductStatus ?? ""),
            ("productstatus", true) => queryable.OrderByDescending(s => s.ProductStatus ?? ""),
            ("completedate", false) => queryable.OrderBy(s => s.PicklingOutRecords.Select(r => (DateTime?)r.CompleteDate).FirstOrDefault()),
            ("completedate", true) => queryable.OrderByDescending(s => s.PicklingOutRecords.Select(r => (DateTime?)r.CompleteDate).FirstOrDefault()),
            _ => query.IsDescending
                ? queryable.OrderByDescending(s => s.CreatedTime)
                : queryable.OrderBy(s => s.CreatedTime)
        };

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(s => new PicklingInRecordDto
            {
                Id = s.Id,
                ProductionBatchId = s.ProductionBatchId,
                ProcessGroupId = s.ProcessGroupId,
                BatchNo = s.ProductionBatch.BatchNo,
                ProcessName = s.ProcessName,
                ManufacturingSpec = s.ManufacturingSpec,
                SectionName = s.SectionName,
                SequenceNumber = s.SequenceNumber,
                InDate = s.InDate,
                Status = s.Status,
                EquipmentName = s.EquipmentName,
                Operator = s.Operator,
                Shift = EnumHelper.TryParse<ShiftType>(s.Shift),
                Quantity = s.Quantity,
                Weight = s.Weight,
                ProductStatus = s.ProductStatus,
                TagNo = s.TagNo,
                PlantGrade = s.PlantGrade,
                Remark = s.Remark,
                DataSource = s.DataSource,
                CreatedTime = s.CreatedTime,
                UpdatedTime = s.UpdatedTime,
                PicklingOutRecordId = s.PicklingOutRecords.Select(r => (int?)r.Id).FirstOrDefault(),
                CompleteDate = s.PicklingOutRecords.Select(r => (DateTime?)r.CompleteDate).FirstOrDefault(),
                CompleteShift = s.PicklingOutRecords.Select(r => EnumHelper.TryParse<ShiftType>(r.Shift)).FirstOrDefault(),
                CompleteOperator = s.PicklingOutRecords.Select(r => r.Operator).FirstOrDefault()
            })
            .ToListAsync();

        return new PagedResult<PicklingInRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<PicklingInRecordDto> CreateAsync(CreatePicklingInRecordRequest request)
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
                sequenceNumber = pg.GetSectionSequence(request.SectionName) ?? 0;
        }
        else if (sequenceNumber == 0)
        {
            var pg = await _context.ProcessGroups.FindAsync(processGroupId.Value);
            if (pg != null)
                sequenceNumber = pg.GetSectionSequence(request.SectionName) ?? 0;
        }

        if (sequenceNumber == 0)
            throw new BusinessException($"工段「{request.SectionName}」不存在于工序组「{request.ProcessName}」中，无法提交");

        // 校验：加工重量不能大于批次现有效原料重量
        if (request.Weight.HasValue && request.Weight > 0 && request.Weight > (batch.CurrentValidWeight ?? batch.InputWeight))
            throw new BusinessException($"加工重量({request.Weight})不能大于有效原料重量({batch.CurrentValidWeight ?? batch.InputWeight})");

        // 规则⑦：重复记录校验 — 同批次+同工序组+同工段禁止重复
        var dupExists = await _context.PicklingInRecords
            .AnyAsync(pr => pr.ProductionBatchId == batch.Id
                && pr.ProcessGroupId == processGroupId
                && pr.SectionName == request.SectionName);
        if (dupExists)
            throw new BusinessException($"工段「{request.SectionName}」在该批次该工序组中已存在入缸记录，不能重复创建");

        // 规则④：冷轧/冷拔工序必须先有「冷轧拔」工段记录
        if (ProcessNames.IsColdRollOrDraw(request.ProcessName))
        {
            var hasColdRollDraw = await _context.ProductionRecords
                .AnyAsync(r => r.ProductionBatchId == batch.Id
                    && r.ProcessGroupId == processGroupId
                    && r.SectionName == SectionDefs.ColdRollDraw)
                || await _context.SectionOutsources
                    .AnyAsync(o => o.ProductionBatchId == batch.Id
                        && o.ProcessGroupId == processGroupId
                        && o.SectionName == SectionDefs.ColdRollDraw);
            if (!hasColdRollDraw)
                throw new BusinessException($"工序「{request.ProcessName}」必须首先记录「冷轧拔」工段，才能进行去油/酸洗");
        }

        // 规则③：执行序号跳跃限制 — 对比该批次在此日期前已执行的最大序号（涵盖生产记录/委外/过程检验/入缸4类），不能 > +7
        if (sequenceNumber > 0)
        {
            var prevMax = 0;
            // 生产记录
            var prodMax = await _context.ProductionRecords
                .Where(r => r.ProductionBatchId == batch.Id && r.ExecDate.Date < request.InDate.Date)
                .MaxAsync(r => (int?)r.SequenceNumber) ?? 0;
            if (prodMax > prevMax) prevMax = prodMax;
            // 工段委外
            var outsourceMax = await _context.SectionOutsources
                .Where(o => o.ProductionBatchId == batch.Id && o.SendOutDate.Date < request.InDate.Date)
                .MaxAsync(o => (int?)o.SequenceNumber) ?? 0;
            if (outsourceMax > prevMax) prevMax = outsourceMax;
            // 过程检验
            var inspectMax = await _context.ProcessInspections
                .Where(pi => pi.ProductionBatchId == batch.Id && pi.InspectionDate.Date < request.InDate.Date)
                .MaxAsync(pi => (int?)pi.SequenceNumber) ?? 0;
            if (inspectMax > prevMax) prevMax = inspectMax;
            // 入缸记录
            var picklingMax = await _context.PicklingInRecords
                .Where(pr => pr.ProductionBatchId == batch.Id && pr.InDate.Date < request.InDate.Date)
                .MaxAsync(pr => (int?)pr.SequenceNumber) ?? 0;
            if (picklingMax > prevMax) prevMax = picklingMax;

            var maxAllowed = prevMax + 7;
            if (sequenceNumber > maxAllowed)
                throw new BusinessException($"执行序号({sequenceNumber})超过该日期前已执行最大值({prevMax})+7={maxAllowed}");
        }

        // 加载工序组列表用于计算制造状态
        var pgList = await _context.ProcessGroups
            .Where(pg => pg.ProductionBatchId == batch.Id)
            .ToListAsync();

        var entity = new PicklingInRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = processGroupId ?? 0,
            ProcessName = request.ProcessName,
            ManufacturingSpec = request.ManufacturingSpec,
            SectionName = request.SectionName,
            SequenceNumber = sequenceNumber,
            InDate = request.InDate,
            Status = PicklingStatus.Soaking,
            EquipmentName = request.EquipmentName,
            Operator = request.Operator,
            Shift = request.Shift?.ToString(),
            Quantity = request.Quantity,
            Weight = request.Weight,
            TagNo = request.TagNo ?? batch.TagNo,
            PlantGrade = request.PlantGrade ?? batch.PlantGrade,
            Remark = request.Remark,
            DataSource = request.DataSource ?? "MANUAL",
            ProductStatus = ProductStatusHelper.Calculate(
                request.ProcessName, request.ManufacturingSpec, batch.ManufacturingItem, pgList, batch.Specification)
        };

        _context.PicklingInRecords.Add(entity);
        await _context.SaveChangesAsync();
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(batch.Id);

        return new PicklingInRecordDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            ProcessGroupId = entity.ProcessGroupId,
            BatchNo = batch.BatchNo,
            ProcessName = entity.ProcessName,
            ManufacturingSpec = entity.ManufacturingSpec,
            SectionName = entity.SectionName,
            SequenceNumber = entity.SequenceNumber,
            InDate = entity.InDate,
            Status = entity.Status,
            EquipmentName = entity.EquipmentName,
            Operator = entity.Operator,
            Shift = EnumHelper.TryParse<ShiftType>(entity.Shift),
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            ProductStatus = entity.ProductStatus,
            TagNo = entity.TagNo,
            PlantGrade = entity.PlantGrade,
            Remark = entity.Remark,
            DataSource = entity.DataSource,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task<List<PicklingInRecordDto>> BatchCreateAsync(List<CreatePicklingInRecordRequest> requests)
    {
        if (requests.Count == 0)
            return new List<PicklingInRecordDto>();

        var sequenceMaxJump = 7;

        // 预加载所有涉及批次
        var batchNos = requests.Select(r => r.BatchNo).Distinct().ToList();
        var batches = await _context.ProductionBatches
            .Where(b => batchNos.Contains(b.BatchNo))
            .ToDictionaryAsync(b => b.BatchNo);
        foreach (var bn in batchNos)
        {
            if (!batches.ContainsKey(bn))
                throw new BusinessException($"批次不存在: {bn}");
        }

        // 预加载所有涉及批次的工序组
        var allBatchIds = batches.Values.Select(b => b.Id).ToList();
        var allProcessGroups = await _context.ProcessGroups
            .Where(pg => allBatchIds.Contains(pg.ProductionBatchId))
            .ToListAsync();
        var pgByBatch = allProcessGroups
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 预查询已有的入缸记录（用于重复校验和跳跃验证）
        var existingRecords = await _context.PicklingInRecords
            .Where(pr => allBatchIds.Contains(pr.ProductionBatchId))
            .ToListAsync();
        var recordsByBatch = existingRecords
            .GroupBy(r => r.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 预查询所有已有执行记录序列号（生产记录/委外/过程检验/入缸）
        var allSequenceData = new List<(int BatchId, int Seq, DateTime Date)>();
        var prodSeqData = await _context.ProductionRecords
            .Where(r => allBatchIds.Contains(r.ProductionBatchId))
            .Select(r => new { r.ProductionBatchId, r.SequenceNumber, Date = r.ExecDate })
            .ToListAsync();
        allSequenceData.AddRange(prodSeqData.Select(r => (r.ProductionBatchId, r.SequenceNumber, r.Date)));
        var outsourceSeqData = await _context.SectionOutsources
            .Where(o => allBatchIds.Contains(o.ProductionBatchId))
            .Select(o => new { o.ProductionBatchId, o.SequenceNumber, Date = o.SendOutDate })
            .ToListAsync();
        allSequenceData.AddRange(outsourceSeqData.Select(o => (o.ProductionBatchId, o.SequenceNumber, o.Date)));
        var inspectionSeqData = await _context.ProcessInspections
            .Where(pi => allBatchIds.Contains(pi.ProductionBatchId))
            .Select(pi => new { pi.ProductionBatchId, pi.SequenceNumber, Date = pi.InspectionDate })
            .ToListAsync();
        allSequenceData.AddRange(inspectionSeqData.Select(pi => (pi.ProductionBatchId, pi.SequenceNumber, pi.Date)));
        var picklingSeqData = existingRecords
            .Select(r => (r.ProductionBatchId, r.SequenceNumber, Date: r.InDate))
            .ToList();
        allSequenceData.AddRange(picklingSeqData);
        var seqDataByBatch = allSequenceData
            .GroupBy(s => s.BatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 预查询已存在的冷轧拔记录（含生产记录、委外和入缸）
        var existingColdRollDraw = new HashSet<(int batchId, int pgId)>();
        var prodColdRollDraw = await _context.ProductionRecords
            .Where(r => allBatchIds.Contains(r.ProductionBatchId) && r.SectionName == SectionDefs.ColdRollDraw)
            .Select(r => new { r.ProductionBatchId, r.ProcessGroupId })
            .ToListAsync();
        foreach (var item in prodColdRollDraw)
            existingColdRollDraw.Add((item.ProductionBatchId, item.ProcessGroupId));
        var outsourcedColdRollDraw = await _context.SectionOutsources
            .Where(o => allBatchIds.Contains(o.ProductionBatchId) && o.SectionName == SectionDefs.ColdRollDraw)
            .Select(o => new { o.ProductionBatchId, o.ProcessGroupId })
            .ToListAsync();
        foreach (var item in outsourcedColdRollDraw)
            existingColdRollDraw.Add((item.ProductionBatchId, item.ProcessGroupId));
        var picklingColdRollDraw = existingRecords
            .Where(r => r.SectionName == SectionDefs.ColdRollDraw)
            .Select(r => (r.ProductionBatchId, r.ProcessGroupId));
        foreach (var item in picklingColdRollDraw)
            existingColdRollDraw.Add(item);

        var errors = new List<string>();
        var entities = new List<PicklingInRecord>();
        var pendingKeys = new HashSet<(int batchId, int pgId, string section)>();

        // 收集本次提交中的冷轧拔记录
        var pendingColdRollDraw = new HashSet<(int batchId, int pgId)>();
        foreach (var request in requests)
        {
            if (request.SectionName != SectionDefs.ColdRollDraw) continue;
            if (!batches.TryGetValue(request.BatchNo, out var batch)) continue;
            var pgId = request.ProcessGroupId;
            if (pgId == null || pgId == 0)
            {
                var matchedPg = pgByBatch.GetValueOrDefault(batch.Id)?
                    .FirstOrDefault(pg => pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec);
                pgId = matchedPg?.Id;
            }
            if (pgId > 0)
                pendingColdRollDraw.Add((batch.Id, pgId.Value));
        }

        // 逐行验证
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];

            if (!batches.TryGetValue(request.BatchNo, out var batch))
            {
                errors.Add($"第{i + 1}行：批次不存在: {request.BatchNo}");
                continue;
            }
            var batchId = batch.Id;

            // 解析 ProcessGroupId
            var processGroupId = request.ProcessGroupId;
            var sequenceNumber = request.SequenceNumber;
            if (processGroupId == null || processGroupId == 0)
            {
                var matchedPg = pgByBatch.GetValueOrDefault(batchId)?
                    .FirstOrDefault(pg => pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec);
                processGroupId = matchedPg?.Id;
                if (matchedPg != null && sequenceNumber == 0)
                    sequenceNumber = matchedPg.GetSectionSequence(request.SectionName) ?? 0;
            }
            else if (sequenceNumber == 0)
            {
                var pg = allProcessGroups.FirstOrDefault(p => p.Id == processGroupId.Value);
                if (pg != null)
                    sequenceNumber = pg.GetSectionSequence(request.SectionName) ?? 0;
            }

            if (sequenceNumber == 0)
            {
                errors.Add($"第{i + 1}行：工段「{request.SectionName}」不存在于工序组「{request.ProcessName}」中，无法提交");
                continue;
            }
            if (processGroupId == null || processGroupId == 0)
            {
                errors.Add($"第{i + 1}行：未找到匹配的工序组，无法提交");
                continue;
            }

            // 规则：加工重量不能大于批次现有效原料重量
            if (request.Weight.HasValue && request.Weight > 0 && request.Weight > (batch.CurrentValidWeight ?? batch.InputWeight))
            {
                errors.Add($"第{i + 1}行：加工重量({request.Weight})不能大于有效原料重量({batch.CurrentValidWeight ?? batch.InputWeight})");
                continue;
            }

            // 重复校验：同批次+同工序组+同工段 → 重复（pendingKeys 模式）
            if (processGroupId > 0)
            {
                var key = (batchId, processGroupId.Value, request.SectionName);
                var dupInDb = recordsByBatch.GetValueOrDefault(batchId, new List<PicklingInRecord>())
                    .Any(r => r.ProcessGroupId == processGroupId.Value && r.SectionName == request.SectionName);
                var dupInPending = pendingKeys.Contains(key);
                if (dupInDb || dupInPending)
                {
                    errors.Add($"第{i + 1}行：工段「{request.SectionName}」在该批次该工序组中已存在入缸记录，不能重复创建");
                    continue;
                }
                pendingKeys.Add(key);
            }

            // 冷轧/冷拔工序必须先有「冷轧拔」工段记录
            if (ProcessNames.IsColdRollOrDraw(request.ProcessName))
            {
                var hasColdRollDraw = existingColdRollDraw.Contains((batchId, processGroupId.Value))
                    || pendingColdRollDraw.Contains((batchId, processGroupId.Value));
                if (!hasColdRollDraw)
                {
                    errors.Add($"第{i + 1}行：工序「{request.ProcessName}」必须首先记录「冷轧拔」工段，才能进行去油/酸洗");
                    continue;
                }
            }

            // 执行序号跳跃限制
            if (sequenceNumber > 0)
            {
                var batchSeqData = seqDataByBatch.GetValueOrDefault(batchId, new List<(int BatchId, int Seq, DateTime Date)>());
                var prevMax = batchSeqData
                    .Where(s => s.Date.Date < request.InDate.Date)
                    .Select(s => (int?)s.Seq)
                    .Max() ?? 0;
                var maxAllowed = prevMax + sequenceMaxJump;
                if (sequenceNumber > maxAllowed)
                {
                    errors.Add($"第{i + 1}行：执行序号({sequenceNumber})超过该日期前已执行最大值({prevMax})+7={maxAllowed}");
                    continue;
                }
            }

            // 计算产品状态
            var pgList = pgByBatch.GetValueOrDefault(batchId, new List<ProcessGroup>());

            entities.Add(new PicklingInRecord
            {
                ProductionBatchId = batchId,
                ProcessGroupId = processGroupId.Value,
                ProcessName = request.ProcessName,
                ManufacturingSpec = request.ManufacturingSpec,
                SectionName = request.SectionName,
                SequenceNumber = sequenceNumber,
                InDate = request.InDate,
                Status = PicklingStatus.Soaking,
                EquipmentName = request.EquipmentName,
                Operator = request.Operator,
                Shift = request.Shift?.ToString(),
                Quantity = request.Quantity,
                Weight = request.Weight,
                TagNo = request.TagNo ?? batch.TagNo,
                PlantGrade = request.PlantGrade ?? batch.PlantGrade,
                Remark = request.Remark,
                DataSource = request.DataSource ?? "MANUAL",
                ProductStatus = ProductStatusHelper.Calculate(
                    request.ProcessName, request.ManufacturingSpec, batch.ManufacturingItem, pgList, batch.Specification)
            });
        }

        if (errors.Any())
            throw new BusinessException(string.Join("；", errors));

        _context.PicklingInRecords.AddRange(entities);
        await _context.SaveChangesAsync();

        // 批量刷新批次追踪字段
        var distinctBatchIds = entities.Select(e => e.ProductionBatchId).Distinct().ToList();
        foreach (var bid in distinctBatchIds)
            await _productionRecordService.RefreshBatchTrackingFieldsAsync(bid);

        // 构建 DTO
        return entities.Select(e =>
        {
            var batch = batches.Values.FirstOrDefault(b => b.Id == e.ProductionBatchId);
            return new PicklingInRecordDto
            {
                Id = e.Id,
                ProductionBatchId = e.ProductionBatchId,
                ProcessGroupId = e.ProcessGroupId,
                BatchNo = batch?.BatchNo ?? "",
                ProcessName = e.ProcessName,
                ManufacturingSpec = e.ManufacturingSpec,
                SectionName = e.SectionName,
                SequenceNumber = e.SequenceNumber,
                InDate = e.InDate,
                Status = e.Status,
                EquipmentName = e.EquipmentName,
                Operator = e.Operator,
                Shift = EnumHelper.TryParse<ShiftType>(e.Shift),
                Quantity = e.Quantity,
                Weight = e.Weight,
                ProductStatus = e.ProductStatus,
                TagNo = e.TagNo,
                PlantGrade = e.PlantGrade,
                Remark = e.Remark,
                DataSource = e.DataSource,
                CreatedTime = e.CreatedTime,
                UpdatedTime = e.UpdatedTime
            };
        }).ToList();
    }

    public async Task<PicklingInRecordDto> UpdateAsync(int id, UpdatePicklingInRecordRequest request)
    {
        var entity = await _context.PicklingInRecords
            .Include(s => s.ProductionBatch)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new BusinessException($"入缸记录不存在: {id}");

        if (request.InDate.HasValue)
            entity.InDate = request.InDate.Value;
        if (request.EquipmentName != null)
            entity.EquipmentName = request.EquipmentName;
        if (request.Operator != null)
            entity.Operator = request.Operator;
        if (request.Shift != null)
            entity.Shift = request.Shift.ToString();
        if (request.Quantity.HasValue)
            entity.Quantity = request.Quantity.Value;
        if (request.Weight.HasValue)
        {
            // 校验：编辑重量不能超过批次现有效原料重量
            if (request.Weight > 0 && entity.ProductionBatch != null
                && request.Weight > (entity.ProductionBatch.CurrentValidWeight ?? entity.ProductionBatch.InputWeight))
                throw new BusinessException($"加工重量({request.Weight})不能大于有效原料重量({entity.ProductionBatch.CurrentValidWeight ?? entity.ProductionBatch.InputWeight})");
            entity.Weight = request.Weight.Value;
        }
        if (request.Remark != null)
            entity.Remark = request.Remark;

        await _context.SaveChangesAsync();
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(entity.ProductionBatchId);

        return new PicklingInRecordDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            ProcessGroupId = entity.ProcessGroupId,
            BatchNo = entity.ProductionBatch!.BatchNo,
            ProcessName = entity.ProcessName,
            ManufacturingSpec = entity.ManufacturingSpec,
            SectionName = entity.SectionName,
            SequenceNumber = entity.SequenceNumber,
            InDate = entity.InDate,
            Status = entity.Status,
            EquipmentName = entity.EquipmentName,
            Operator = entity.Operator,
            Shift = EnumHelper.TryParse<ShiftType>(entity.Shift),
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            ProductStatus = entity.ProductStatus,
            TagNo = entity.TagNo,
            PlantGrade = entity.PlantGrade,
            Remark = entity.Remark,
            DataSource = entity.DataSource,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.PicklingInRecords
            .Include(s => s.PicklingOutRecords)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new BusinessException($"入缸记录不存在: {id}");

        if (entity.PicklingOutRecords.Count > 0)
            throw new BusinessException("该入缸记录已有完工记录，无法删除。请先删除完工记录");

        _context.PicklingInRecords.Remove(entity);
        await _context.SaveChangesAsync();
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(entity.ProductionBatchId);
    }

    // ========== 完工记录 ==========

    public async Task<PicklingOutRecordDto?> GetOutRecordByInIdAsync(int picklingInRecordId)
    {
        return await _context.PicklingOutRecords
            .AsNoTracking()
            .Where(r => r.PicklingInRecordId == picklingInRecordId)
            .Select(r => new PicklingOutRecordDto
            {
                Id = r.Id,
                PicklingInRecordId = r.PicklingInRecordId,
                CompleteDate = r.CompleteDate,
                Remark = r.Remark,
                DataSource = r.DataSource,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime,
                ProductionBatchId = r.ProductionBatchId,
                BatchNo = r.BatchNo,
                ProcessName = r.ProcessName,
                ManufacturingSpec = r.ManufacturingSpec,
                SectionName = r.SectionName,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                EquipmentName = r.EquipmentName,
                Operator = r.Operator,
                Shift = EnumHelper.TryParse<ShiftType>(r.Shift),
                Quantity = r.Quantity,
                Weight = r.Weight,
                ProductStatus = r.ProductStatus
            })
            .FirstOrDefaultAsync();
    }

    public async Task<PagedResult<PicklingOutRecordDto>> GetOutRecordsPagedAsync(QueryParams query)
    {
        var queryable = _context.PicklingOutRecords
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                (r.BatchNo != null && r.BatchNo.Contains(kw)) ||
                (r.ProcessName != null && r.ProcessName.Contains(kw)) ||
                r.SectionName.Contains(kw) ||
                (r.Remark != null && r.Remark.Contains(kw)) ||
                (r.DataSource != null && r.DataSource.Contains(kw)) ||
                (r.EquipmentName != null && r.EquipmentName.Contains(kw)) ||
                (r.Operator != null && r.Operator.Contains(kw)) ||
                (r.Shift != null && r.Shift.Contains(kw)) ||
                (r.ManufacturingSpec != null && r.ManufacturingSpec.Contains(kw)) ||
                (r.TagNo != null && r.TagNo.Contains(kw)) ||
                (r.PlantGrade != null && r.PlantGrade.Contains(kw)));
        }

        // 完工日期范围筛选
        if (query.CompleteDateFrom.HasValue)
        {
            var from = query.CompleteDateFrom.Value.Date;
            queryable = queryable.Where(r => r.CompleteDate >= from);
        }
        if (query.CompleteDateTo.HasValue)
        {
            var to = query.CompleteDateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(r => r.CompleteDate < to);
        }

        var totalCount = await queryable.CountAsync();

        queryable = (query.SortBy?.ToLower(), query.IsDescending) switch
        {
            ("batchno", false) => queryable.OrderBy(r => r.BatchNo ?? ""),
            ("batchno", true) => queryable.OrderByDescending(r => r.BatchNo ?? ""),
            ("processname", false) => queryable.OrderBy(r => r.ProcessName ?? ""),
            ("processname", true) => queryable.OrderByDescending(r => r.ProcessName ?? ""),
            ("tagno", false) => queryable.OrderBy(r => r.TagNo ?? ""),
            ("tagno", true) => queryable.OrderByDescending(r => r.TagNo ?? ""),
            ("completedate", false) => queryable.OrderBy(r => r.CompleteDate),
            ("completedate", true) => queryable.OrderByDescending(r => r.CompleteDate),
            ("datasource", false) => queryable.OrderBy(r => r.DataSource ?? ""),
            ("datasource", true) => queryable.OrderByDescending(r => r.DataSource ?? ""),
            ("equipmentname", false) => queryable.OrderBy(r => r.EquipmentName ?? ""),
            ("equipmentname", true) => queryable.OrderByDescending(r => r.EquipmentName ?? ""),
            ("operator", false) => queryable.OrderBy(r => r.Operator ?? ""),
            ("operator", true) => queryable.OrderByDescending(r => r.Operator ?? ""),
            ("shift", false) => queryable.OrderBy(r => r.Shift ?? ""),
            ("shift", true) => queryable.OrderByDescending(r => r.Shift ?? ""),
            ("quantity", false) => queryable.OrderBy(r => r.Quantity ?? 0),
            ("quantity", true) => queryable.OrderByDescending(r => r.Quantity ?? 0),
            ("weight", false) => queryable.OrderBy(r => r.Weight ?? 0),
            ("weight", true) => queryable.OrderByDescending(r => r.Weight ?? 0),
            ("productstatus", false) => queryable.OrderBy(r => r.ProductStatus ?? ""),
            ("productstatus", true) => queryable.OrderByDescending(r => r.ProductStatus ?? ""),
            _ => query.IsDescending
                ? queryable.OrderByDescending(r => r.CreatedTime)
                : queryable.OrderBy(r => r.CreatedTime)
        };

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new PicklingOutRecordDto
            {
                Id = r.Id,
                PicklingInRecordId = r.PicklingInRecordId,
                CompleteDate = r.CompleteDate,
                Remark = r.Remark,
                DataSource = r.DataSource,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime,
                ProductionBatchId = r.ProductionBatchId,
                BatchNo = r.BatchNo,
                ProcessName = r.ProcessName,
                ManufacturingSpec = r.ManufacturingSpec,
                SectionName = r.SectionName,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                EquipmentName = r.EquipmentName,
                Operator = r.Operator,
                Shift = EnumHelper.TryParse<ShiftType>(r.Shift),
                Quantity = r.Quantity,
                Weight = r.Weight,
                ProductStatus = r.ProductStatus
            })
            .ToListAsync();

        return new PagedResult<PicklingOutRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<PicklingOutRecordDto> CreateOutRecordAsync(CreatePicklingOutRecordRequest request)
    {
        var inRecord = await _context.PicklingInRecords
            .Include(p => p.ProductionBatch)
            .FirstOrDefaultAsync(p => p.Id == request.PicklingInRecordId)
            ?? throw new BusinessException($"入缸记录不存在: {request.PicklingInRecordId}");

        if (inRecord.Status == PicklingStatus.Completed)
            throw new BusinessException("该入缸记录已完工，不能重复完工");

        // 从入缸记录复制冗余字段（计件工资结算/数据冻结）
        var entity = new PicklingOutRecord
        {
            PicklingInRecordId = request.PicklingInRecordId,
            CompleteDate = request.CompleteDate,
            Remark = request.Remark,
            DataSource = request.DataSource ?? "MANUAL",
            ProductionBatchId = inRecord.ProductionBatchId,
            ManufacturingSpec = inRecord.ManufacturingSpec,
            SectionName = inRecord.SectionName,
            EquipmentName = inRecord.EquipmentName,
            Operator = request.Operator ?? inRecord.Operator,
            Shift = request.Shift?.ToString() ?? inRecord.Shift,
            Quantity = inRecord.Quantity,
            Weight = inRecord.Weight,
            ProductStatus = inRecord.ProductStatus,
            PlantGrade = inRecord.PlantGrade,
            BatchNo = inRecord.ProductionBatch.BatchNo,
            ProcessName = inRecord.ProcessName,
            TagNo = inRecord.TagNo
        };

        // 自动更新入缸状态为 Completed
        inRecord.Status = PicklingStatus.Completed;

        _context.PicklingOutRecords.Add(entity);
        await _context.SaveChangesAsync();
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(inRecord.ProductionBatchId);

        return new PicklingOutRecordDto
        {
            Id = entity.Id,
            PicklingInRecordId = entity.PicklingInRecordId,
            CompleteDate = entity.CompleteDate,
            Remark = entity.Remark,
            DataSource = entity.DataSource,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime,
            ProductionBatchId = entity.ProductionBatchId,
            ManufacturingSpec = entity.ManufacturingSpec,
            SectionName = entity.SectionName,
            PlantGrade = entity.PlantGrade,
            BatchNo = entity.BatchNo,
            ProcessName = entity.ProcessName,
            TagNo = entity.TagNo,
            EquipmentName = entity.EquipmentName,
            Operator = entity.Operator,
            Shift = EnumHelper.TryParse<ShiftType>(entity.Shift),
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            ProductStatus = entity.ProductStatus
        };
    }

    public async Task<PicklingOutRecordDto> UpdateOutRecordAsync(int id, UpdatePicklingOutRecordRequest request)
    {
        var entity = await _context.PicklingOutRecords
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new BusinessException($"完工记录不存在: {id}");

        if (request.CompleteDate.HasValue)
            entity.CompleteDate = request.CompleteDate.Value;
        if (request.Remark != null)
            entity.Remark = request.Remark;
        if (request.Operator != null)
            entity.Operator = request.Operator;
        if (request.Shift != null)
            entity.Shift = request.Shift.ToString();

        await _context.SaveChangesAsync();
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(entity.ProductionBatchId);

        return new PicklingOutRecordDto
        {
            Id = entity.Id,
            PicklingInRecordId = entity.PicklingInRecordId,
            CompleteDate = entity.CompleteDate,
            Remark = entity.Remark,
            DataSource = entity.DataSource,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime,
            ProductionBatchId = entity.ProductionBatchId,
            ManufacturingSpec = entity.ManufacturingSpec,
            SectionName = entity.SectionName,
            PlantGrade = entity.PlantGrade,
            BatchNo = entity.BatchNo,
            ProcessName = entity.ProcessName,
            TagNo = entity.TagNo,
            EquipmentName = entity.EquipmentName,
            Operator = entity.Operator,
            Shift = EnumHelper.TryParse<ShiftType>(entity.Shift),
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            ProductStatus = entity.ProductStatus
        };
    }

    public async Task DeleteOutRecordAsync(int id)
    {
        var entity = await _context.PicklingOutRecords
            .Include(r => r.PicklingInRecord)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new BusinessException($"完工记录不存在: {id}");

        var batchId = entity.PicklingInRecord.ProductionBatchId;

        // 恢复入缸状态为 Soaking
        entity.PicklingInRecord.Status = PicklingStatus.Soaking;

        _context.PicklingOutRecords.Remove(entity);
        await _context.SaveChangesAsync();
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(batchId);
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var items = await _context.PicklingInRecords
            .AsNoTracking()
            .Include(s => s.ProductionBatch)
            .Include(s => s.PicklingOutRecords)
            .Where(s => ids.Contains(s.Id))
            .ToListAsync();

        if (items.Count == 0)
            throw new BusinessException("未找到选中的数据");

        var data = items.Select(s => new Dictionary<string, object>
        {
            ["BatchNo"] = s.ProductionBatch.BatchNo,
            ["ProcessName"] = s.ProcessName,
            ["ManufacturingSpec"] = s.ManufacturingSpec ?? "",
            ["SequenceNumber"] = s.SequenceNumber,
            ["InDate"] = s.InDate.ToString("yyyy-MM-dd"),
            ["SectionName"] = s.SectionName,
            ["EquipmentName"] = s.EquipmentName ?? "",
            ["Operator"] = s.Operator ?? "",
            ["Shift"] = s.Shift?.ToString() ?? "",
            ["Quantity"] = s.Quantity ?? 0,
            ["Weight"] = s.Weight ?? 0,
            ["ProductStatus"] = s.ProductStatus ?? "在制",
            ["TagNo"] = s.TagNo ?? "",
            ["PlantGrade"] = s.PlantGrade ?? "",
            ["Status"] = s.Status == PicklingStatus.Completed ? "已完工" : "浸泡中",
            ["CompleteDate"] = s.PicklingOutRecords.Select(r => (DateTime?)r.CompleteDate).FirstOrDefault()?.ToString("yyyy-MM-dd") ?? "",
            ["Remark"] = s.Remark ?? "",
            ["DataSource"] = s.DataSource switch
            {
                "SCAN" => "扫码",
                "MANUAL" => "手动",
                _ => ""
            },
            ["UpdatedTime"] = s.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
        }).ToList();

        return TablePrintHelper.GeneratePdf("去油/酸洗入缸记录", data, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending,
        DateTime? inDateFrom, DateTime? inDateTo,
        DateTime? completeDateFrom, DateTime? completeDateTo,
        List<PrintColumnDef> columns)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "createdtime",
            IsDescending = isDescending,
            InDateFrom = inDateFrom,
            InDateTo = inDateTo,
            CompleteDateFrom = completeDateFrom,
            CompleteDateTo = completeDateTo
        };
        var paged = await GetPagedAsync(query);

        var data = paged.Items.Select(s => new Dictionary<string, object>
        {
            ["BatchNo"] = s.BatchNo,
            ["ProcessName"] = s.ProcessName,
            ["ManufacturingSpec"] = s.ManufacturingSpec ?? "",
            ["SequenceNumber"] = s.SequenceNumber,
            ["InDate"] = s.InDate.ToString("yyyy-MM-dd"),
            ["SectionName"] = s.SectionName,
            ["EquipmentName"] = s.EquipmentName ?? "",
            ["Operator"] = s.Operator ?? "",
            ["Shift"] = s.Shift?.ToString() ?? "",
            ["Quantity"] = s.Quantity ?? 0,
            ["Weight"] = s.Weight ?? 0,
            ["ProductStatus"] = s.ProductStatus ?? "在制",
            ["TagNo"] = s.TagNo ?? "",
            ["PlantGrade"] = s.PlantGrade ?? "",
            ["Status"] = s.Status == PicklingStatus.Completed ? "已完工" : "浸泡中",
            ["CompleteDate"] = s.CompleteDate?.ToString("yyyy-MM-dd") ?? "",
            ["Remark"] = s.Remark ?? "",
            ["DataSource"] = s.DataSource switch
            {
                "SCAN" => "扫码",
                "MANUAL" => "手动",
                _ => ""
            },
            ["UpdatedTime"] = s.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
        }).ToList();

        return TablePrintHelper.GeneratePdf("去油/酸洗入缸记录", data, columns);
    }

    // ========== 完工记录打印 ==========

    public async Task<byte[]> PrintOutBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var items = await _context.PicklingOutRecords
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .ToListAsync();

        if (items.Count == 0)
            throw new BusinessException("未找到选中的数据");

        var data = items.Select(s => new Dictionary<string, object>
        {
            ["BatchNo"] = s.BatchNo ?? "",
            ["ProcessName"] = s.ProcessName ?? "",
            ["SectionName"] = s.SectionName,
            ["ManufacturingSpec"] = s.ManufacturingSpec ?? "",
            ["CompleteDate"] = s.CompleteDate.ToString("yyyy-MM-dd"),
            ["EquipmentName"] = s.EquipmentName ?? "",
            ["Operator"] = s.Operator ?? "",
            ["Shift"] = s.Shift switch
            {
                "DayShift" => "白班",
                "MiddleShift" => "中班",
                "NightShift" => "夜班",
                _ => ""
            },
            ["Quantity"] = s.Quantity ?? 0,
            ["Weight"] = s.Weight ?? 0,
            ["ProductStatus"] = s.ProductStatus ?? "在制",
            ["TagNo"] = s.TagNo ?? "",
            ["PlantGrade"] = s.PlantGrade ?? "",
            ["Remark"] = s.Remark ?? "",
            ["DataSource"] = s.DataSource switch
            {
                "SCAN" => "扫码",
                "MANUAL" => "手动",
                _ => ""
            },
            ["UpdatedTime"] = s.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
        }).ToList();

        return TablePrintHelper.GeneratePdf("去油/酸洗完工记录", data, columns);
    }

    public async Task<byte[]> PrintOutAllAsync(string? keyword, string? sortBy, bool isDescending,
        DateTime? completeDateFrom, DateTime? completeDateTo,
        List<PrintColumnDef> columns)
    {
        var queryable = _context.PicklingOutRecords
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword;
            queryable = queryable.Where(s =>
                (s.BatchNo != null && s.BatchNo.Contains(kw)) ||
                (s.ProcessName != null && s.ProcessName.Contains(kw)) ||
                s.SectionName.Contains(kw) ||
                (s.Remark != null && s.Remark.Contains(kw)));
        }
        if (completeDateFrom.HasValue)
        {
            var from = completeDateFrom.Value.Date;
            queryable = queryable.Where(s => s.CompleteDate >= from);
        }
        if (completeDateTo.HasValue)
        {
            var to = completeDateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(s => s.CompleteDate < to);
        }

        queryable = (sortBy?.ToLower(), isDescending) switch
        {
            ("completedate", false) => queryable.OrderBy(s => s.CompleteDate),
            ("completedate", true) => queryable.OrderByDescending(s => s.CompleteDate),
            _ => isDescending
                ? queryable.OrderByDescending(s => s.CreatedTime)
                : queryable.OrderBy(s => s.CreatedTime)
        };

        var items = await queryable.ToListAsync();

        if (items.Count == 0)
            throw new BusinessException("未找到数据");

        var data = items.Select(s => new Dictionary<string, object>
        {
            ["BatchNo"] = s.BatchNo ?? "",
            ["ProcessName"] = s.ProcessName ?? "",
            ["SectionName"] = s.SectionName,
            ["ManufacturingSpec"] = s.ManufacturingSpec ?? "",
            ["CompleteDate"] = s.CompleteDate.ToString("yyyy-MM-dd"),
            ["EquipmentName"] = s.EquipmentName ?? "",
            ["Operator"] = s.Operator ?? "",
            ["Shift"] = s.Shift switch
            {
                "DayShift" => "白班",
                "MiddleShift" => "中班",
                "NightShift" => "夜班",
                _ => ""
            },
            ["Quantity"] = s.Quantity ?? 0,
            ["Weight"] = s.Weight ?? 0,
            ["ProductStatus"] = s.ProductStatus ?? "在制",
            ["TagNo"] = s.TagNo ?? "",
            ["PlantGrade"] = s.PlantGrade ?? "",
            ["Remark"] = s.Remark ?? "",
            ["DataSource"] = s.DataSource switch
            {
                "SCAN" => "扫码",
                "MANUAL" => "手动",
                _ => ""
            },
            ["UpdatedTime"] = s.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
        }).ToList();

        return TablePrintHelper.GeneratePdf("去油/酸洗完工记录", data, columns);
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("PicklingService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var dict = new Dictionary<string, List<string>>();

            // 从入缸记录获取各列去重值
            var processNames = await _context.PicklingInRecords
                .AsNoTracking()
                .Where(s => s.ProcessName != null)
                .Select(s => s.ProcessName)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (processNames.Count > 0) dict["ProcessName"] = processNames;

            var sectionNames = await _context.PicklingInRecords
                .AsNoTracking()
                .Where(s => s.SectionName != null)
                .Select(s => s.SectionName)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (sectionNames.Count > 0) dict["SectionName"] = sectionNames;

            var specs = await _context.PicklingInRecords
                .AsNoTracking()
                .Where(s => s.ManufacturingSpec != null)
                .Select(s => s.ManufacturingSpec!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (specs.Count > 0) dict["ManufacturingSpec"] = specs;

            var tagNos = await _context.PicklingInRecords
                .AsNoTracking()
                .Where(s => s.TagNo != null)
                .Select(s => s.TagNo!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (tagNos.Count > 0) dict["TagNo"] = tagNos;

            var plantGrades = await _context.PicklingInRecords
                .AsNoTracking()
                .Where(s => s.PlantGrade != null)
                .Select(s => s.PlantGrade!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (plantGrades.Count > 0) dict["PlantGrade"] = plantGrades;

            var equipmentNames = await _context.PicklingInRecords
                .AsNoTracking()
                .Where(s => s.EquipmentName != null)
                .Select(s => s.EquipmentName!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (equipmentNames.Count > 0) dict["EquipmentName"] = equipmentNames;

            var operators = await _context.PicklingInRecords
                .AsNoTracking()
                .Where(s => s.Operator != null)
                .Select(s => s.Operator!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (operators.Count > 0) dict["Operator"] = operators;

            var shifts = await _context.PicklingInRecords
                .AsNoTracking()
                .Where(s => s.Shift != null)
                .Select(s => s.Shift!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (shifts.Count > 0) dict["Shift"] = shifts;

            var sequenceNumbers = await _context.PicklingInRecords
                .AsNoTracking()
                .Select(s => s.SequenceNumber.ToString())
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (sequenceNumbers.Count > 0) dict["SequenceNumber"] = sequenceNumbers;

            // BatchNo 来自导航属性
            var batchNos = await _context.PicklingInRecords
                .AsNoTracking()
                .Include(s => s.ProductionBatch)
                .Where(s => s.ProductionBatch.BatchNo != null)
                .Select(s => s.ProductionBatch.BatchNo)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (batchNos.Count > 0) dict["BatchNo"] = batchNos;

            var remarks = await _context.PicklingInRecords
                .AsNoTracking()
                .Where(r => r.Remark != null)
                .Select(r => r.Remark!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (remarks.Count > 0) dict["Remark"] = remarks;

            var productStatusValues = await _context.PicklingInRecords
                .AsNoTracking()
                .Where(r => r.ProductStatus != null)
                .Select(r => r.ProductStatus!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (productStatusValues.Count > 0) dict["ProductStatus"] = productStatusValues;

            return dict;
        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<List<PicklingInRecordDto>> GetByBatchAsync(string batchNo)
    {
        return await _context.PicklingInRecords
            .AsNoTracking()
            .Include(s => s.ProductionBatch)
            .Where(s => s.ProductionBatch.BatchNo == batchNo)
            .OrderByDescending(s => s.InDate)
            .Select(s => new PicklingInRecordDto
            {
                Id = s.Id,
                ProductionBatchId = s.ProductionBatchId,
                ProcessGroupId = s.ProcessGroupId,
                BatchNo = s.ProductionBatch.BatchNo,
                ProcessName = s.ProcessName,
                ManufacturingSpec = s.ManufacturingSpec,
                SectionName = s.SectionName,
                SequenceNumber = s.SequenceNumber,
                InDate = s.InDate,
                Status = s.Status,
                EquipmentName = s.EquipmentName,
                Operator = s.Operator,
                Shift = EnumHelper.TryParse<ShiftType>(s.Shift),
                Quantity = s.Quantity,
                Weight = s.Weight,
                ProductStatus = s.ProductStatus,
                TagNo = s.TagNo,
                PlantGrade = s.PlantGrade,
                Remark = s.Remark,
                DataSource = s.DataSource,
                CreatedTime = s.CreatedTime,
                UpdatedTime = s.UpdatedTime
            })
            .ToListAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetOutRecordFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("PicklingService:OutRecordFilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var dict = new Dictionary<string, List<string>>();

            var processNames = await _context.PicklingOutRecords
                .AsNoTracking()
                .Where(r => r.ProcessName != null)
                .Select(r => r.ProcessName!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (processNames.Count > 0) dict["ProcessName"] = processNames;

            // SectionName 现为出缸记录实体字段
            var sectionNames = await _context.PicklingOutRecords
                .AsNoTracking()
                .Where(r => r.SectionName != null)
                .Select(r => r.SectionName)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (sectionNames.Count > 0) dict["SectionName"] = sectionNames;

            var batchNos = await _context.PicklingOutRecords
                .AsNoTracking()
                .Where(r => r.BatchNo != null)
                .Select(r => r.BatchNo!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (batchNos.Count > 0) dict["BatchNo"] = batchNos;

            var equipmentNames = await _context.PicklingOutRecords
                .AsNoTracking()
                .Where(r => r.EquipmentName != null)
                .Select(r => r.EquipmentName!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (equipmentNames.Count > 0) dict["EquipmentName"] = equipmentNames;

            var operators = await _context.PicklingOutRecords
                .AsNoTracking()
                .Where(r => r.Operator != null)
                .Select(r => r.Operator!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (operators.Count > 0) dict["Operator"] = operators;

            var shifts = await _context.PicklingOutRecords
                .AsNoTracking()
                .Where(r => r.Shift != null)
                .Select(r => r.Shift!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (shifts.Count > 0) dict["Shift"] = shifts;

            var isFinishedValues = await _context.PicklingOutRecords
                .AsNoTracking()
                .Where(r => r.ProductStatus != null).Select(r => r.ProductStatus!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (isFinishedValues.Count > 0) dict["ProductStatus"] = isFinishedValues;

            // 补充滤网：Remark / PlantGrade / TagNo / ManufacturingSpec
            var remarks = await _context.PicklingOutRecords
                .AsNoTracking()
                .Where(r => r.Remark != null)
                .Select(r => r.Remark!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (remarks.Count > 0) dict["Remark"] = remarks;

            var plantGrades = await _context.PicklingOutRecords
                .AsNoTracking()
                .Where(r => r.PlantGrade != null)
                .Select(r => r.PlantGrade!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (plantGrades.Count > 0) dict["PlantGrade"] = plantGrades;

            var tagNos = await _context.PicklingOutRecords
                .AsNoTracking()
                .Where(r => r.TagNo != null)
                .Select(r => r.TagNo!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (tagNos.Count > 0) dict["TagNo"] = tagNos;

            var mfSpecs = await _context.PicklingOutRecords
                .AsNoTracking()
                .Where(r => r.ManufacturingSpec != null)
                .Select(r => r.ManufacturingSpec!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            if (mfSpecs.Count > 0) dict["ManufacturingSpec"] = mfSpecs;

            return dict;
        }) ?? new Dictionary<string, List<string>>();
    }
}
