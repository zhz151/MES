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

/// <summary>
/// 生产记录服务实现
/// </summary>
public class ProductionRecordService : IProductionRecordService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductionRecordService> _logger;

    public ProductionRecordService(AppDbContext context, ILogger<ProductionRecordService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ========== 内部生产记录 ==========

    public async Task<PagedResult<ProductionRecordDto>> GetProductionRecordsAsync(int batchId, QueryParams query)
    {
        var queryable = _context.ProductionRecords
            .AsNoTracking()
            .Where(r => r.ProductionBatchId == batchId);

        var totalCount = await queryable.CountAsync();

        var items = await queryable
            .OrderBy(r => r.SequenceNumber)
            .ThenBy(r => r.ExecDate)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new ProductionRecordDto
            {
                Id = r.Id,
                ProductionBatchId = r.ProductionBatchId,
                ProcessGroupId = r.ProcessGroupId,
                ProcessName = r.ProcessName,
                ManufacturingSpec = r.ManufacturingSpec,
                SectionName = r.SectionName,
                SequenceNumber = r.SequenceNumber,
                ExecDate = r.ExecDate,
                EquipmentName = r.EquipmentName,
                Operator = r.Operator,
                Shift = r.Shift,
                Quantity = r.Quantity,
                Weight = r.Weight,
                IsFinished = r.IsFinished,
                CuttingMultiple = r.CuttingMultiple,
                FinishedCutLength = r.FinishedCutLength,
                PostCutQuantity = r.PostCutQuantity,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                Remark = r.Remark
            })
            .ToListAsync();

        return new PagedResult<ProductionRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<ProductionRecordDto> CreateProductionRecordAsync(CreateProductionRecordRequest request)
    {
        var batchNo = request.BatchNo;
        var batch = await _context.ProductionBatches.FirstOrDefaultAsync(b => b.BatchNo == batchNo)
            ?? throw new BusinessException($"批次不存在: {batchNo}");

        var batchId = batch.Id;

        // 自动解析 ProcessGroupId
        var processGroupId = request.ProcessGroupId;
        if (processGroupId == null || processGroupId == 0)
        {
            var processGroup = await _context.ProcessGroups
                .Where(pg => pg.ProductionBatchId == batchId && pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec)
                .Select(pg => (int?)pg.Id)
                .FirstOrDefaultAsync();
            processGroupId = processGroup ?? 0;
        }

        // 自动解析 SequenceNumber（为0时从ProcessGroup查）
        var sequenceNumber = request.SequenceNumber;
        if (sequenceNumber == 0 && processGroupId > 0)
        {
            var pg = await _context.ProcessGroups.FindAsync(processGroupId.Value);
            if (pg != null)
            {
                var sections = GetSectionsFromProcessGroup(pg);
                var match = sections.FirstOrDefault(s => s.SectionName == request.SectionName);
                sequenceNumber = match.Sequence;
            }
        }

        var entity = new ProductionRecord
        {
            ProductionBatchId = batchId,
            ProcessGroupId = processGroupId.Value,
            ProcessName = request.ProcessName,
            ManufacturingSpec = request.ManufacturingSpec,
            SectionName = request.SectionName,
            SequenceNumber = sequenceNumber,
            ExecDate = request.ExecDate,
            EquipmentName = request.EquipmentName,
            Operator = request.Operator,
            Shift = request.Shift,
            Quantity = request.Quantity,
            Weight = request.Weight,
            IsFinished = request.IsFinished,
            CuttingMultiple = request.CuttingMultiple,
            FinishedCutLength = request.FinishedCutLength,
            PostCutQuantity = request.PostCutQuantity,
            TagNo = request.TagNo ?? batch.TagNo,
            PlantGrade = request.PlantGrade ?? batch.PlantGrade,
            Remark = request.Remark,
            DataSource = request.DataSource ?? "MANUAL"
        };

        _context.ProductionRecords.Add(entity);
        await _context.SaveChangesAsync();

        await UpdateBatchTrackingFromRecords(batchId);

        return new ProductionRecordDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            ProcessGroupId = entity.ProcessGroupId,
            ProcessName = entity.ProcessName,
            ManufacturingSpec = entity.ManufacturingSpec,
            SectionName = entity.SectionName,
            SequenceNumber = entity.SequenceNumber,
            ExecDate = entity.ExecDate,
            EquipmentName = entity.EquipmentName,
            Operator = entity.Operator,
            Shift = entity.Shift,
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            IsFinished = entity.IsFinished,
            CuttingMultiple = entity.CuttingMultiple,
            FinishedCutLength = entity.FinishedCutLength,
            PostCutQuantity = entity.PostCutQuantity,
            TagNo = entity.TagNo,
            PlantGrade = entity.PlantGrade,
            Remark = entity.Remark,
            DataSource = entity.DataSource
        };
    }

    public async Task<List<ProductionRecordDto>> BatchCreateProductionRecordsAsync(List<CreateProductionRecordRequest> requests)
    {
        if (requests.Count == 0)
            return new List<ProductionRecordDto>();

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

        // 预加载所有涉及批次的工序组（用于 ProcessGroupId + SequenceNumber 解析）
        var allBatchIds = batchLookup.Values.Select(b => b.Id).ToList();
        var processGroups = await _context.ProcessGroups
            .Where(pg => allBatchIds.Contains(pg.ProductionBatchId))
            .ToListAsync();
        var pgByBatch = processGroups.GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var entities = new List<ProductionRecord>();
        var requestErrors = new List<string>();
        var today = DateTime.Today;

        // 预查询：各批次上个最近执行日期的最大 SequenceNumber
        var prevRecords = await _context.ProductionRecords
            .Where(r => allBatchIds.Contains(r.ProductionBatchId) && r.ExecDate < today)
            .ToListAsync();
        var prevMaxSeqByBatch = new Dictionary<int, int>();
        var prevDateByBatch = prevRecords
            .GroupBy(r => r.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.Max(r => r.ExecDate));
        foreach (var kv in prevDateByBatch)
        {
            var maxSeq = prevRecords
                .Where(r => r.ProductionBatchId == kv.Key && r.ExecDate == kv.Value)
                .Max(r => (int?)r.SequenceNumber) ?? 0;
            prevMaxSeqByBatch[kv.Key] = maxSeq;
        }

        // 预查询：各批次已存在的冷轧拔记录
        var existingColdRollDraw = await _context.ProductionRecords
            .Where(r => allBatchIds.Contains(r.ProductionBatchId) && r.SectionName == "冷轧拔")
            .Select(r => new { r.ProductionBatchId, r.ProcessGroupId })
            .ToListAsync();
        var coldRollDrawExists = new HashSet<(int BatchId, int PgId)>(
            existingColdRollDraw.Select(r => (r.ProductionBatchId, r.ProcessGroupId)));

        // 第一遍：业务规则验证
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var batch = batchLookup[request.BatchNo];
            var batchId = batch.Id;

            // 1) 制造规格不能为空
            if (string.IsNullOrWhiteSpace(request.ManufacturingSpec))
                requestErrors.Add($"第{i + 1}行：制造规格不能为空");

            // 2) 加工重量不能大于批次现有效原料重量
            if (request.Weight.HasValue && request.Weight > 0 && request.Weight > (batch.CurrentValidWeight ?? batch.InputWeight))
                requestErrors.Add($"第{i + 1}行：加工重量({request.Weight})不能大于有效原料重量({batch.CurrentValidWeight ?? batch.InputWeight})");

            // 3) 执行序号不超上个最近日期最大值+7
            if (request.SequenceNumber > 0)
            {
                var prevMax = prevMaxSeqByBatch.GetValueOrDefault(batchId, 0);
                var maxAllowed = prevMax + 7;
                if (request.SequenceNumber > maxAllowed)
                    requestErrors.Add($"第{i + 1}行：执行序号({request.SequenceNumber})超过上个最近日期最大值({prevMax})+7={maxAllowed}");
            }
        }

        // 收集本次提交中的冷轧拔记录（用于冷轧/冷拔验证）
        var pendingColdRollDraw = new HashSet<(int BatchId, int PgId)>();
        foreach (var request in requests)
        {
            if (request.SectionName == "冷轧拔")
            {
                var batch = batchLookup[request.BatchNo];
                var batchId = batch.Id;
                var pgId = request.ProcessGroupId;
                if (pgId == null || pgId == 0)
                {
                    var matchedPg = pgByBatch.GetValueOrDefault(batchId)?
                        .FirstOrDefault(pg => pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec);
                    pgId = matchedPg?.Id;
                }
                if (pgId > 0)
                    pendingColdRollDraw.Add((batchId, pgId.Value));
            }
        }

        // 第二遍：冷轧/冷拔验证 — 工序组名称为冷轧/冷拔的，必须先记录「冷轧拔」工段
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            if (request.SectionName == "冷轧拔")
                continue;

            var batch = batchLookup[request.BatchNo];
            var batchId = batch.Id;

            // 解析该记录所属的 ProcessGroupId
            var pgId = request.ProcessGroupId;
            if (pgId == null || pgId == 0)
            {
                var matchedPg = pgByBatch.GetValueOrDefault(batchId)?
                    .FirstOrDefault(pg => pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec);
                pgId = matchedPg?.Id;
            }
            if (pgId == null || pgId == 0)
                continue;

            // 查工序组名称
            var pg = processGroups.FirstOrDefault(p => p.Id == pgId.Value);
            if (pg == null || (pg.ProcessName != "冷轧" && pg.ProcessName != "冷拔"))
                continue;

            // 该工序组中是否有冷轧拔记录（已有 + 本次提交）
            var hasColdRollDraw = coldRollDrawExists.Contains((batchId, pgId.Value))
                || pendingColdRollDraw.Contains((batchId, pgId.Value));

            if (!hasColdRollDraw)
            {
                requestErrors.Add($"第{i + 1}行：工序「{pg.ProcessName}」必须首先记录「冷轧拔」工段，才能记录「{request.SectionName}」");
            }
        }

        // 4) 验证工段存在于工序组中（非0值）
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var batch = batchLookup[request.BatchNo];
            var batchId = batch.Id;

            var pgId = request.ProcessGroupId;
            if (pgId == null || pgId == 0)
            {
                var matchedPg = pgByBatch.GetValueOrDefault(batchId)?
                    .FirstOrDefault(pg => pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec);
                pgId = matchedPg?.Id;
            }
            if (pgId == null || pgId == 0)
            {
                requestErrors.Add($"第{i + 1}行：未找到匹配的工序组，无法提交");
                continue;
            }

            var pg = processGroups.FirstOrDefault(pg => pg.Id == pgId.Value);
            if (pg != null)
            {
                // 过程检验已独立为单独模块，生产记录中不允许使用"检验"工段
                if (request.SectionName == "检验")
                {
                    requestErrors.Add($"第{i + 1}行：工段「检验」已由过程检验模块管理，不允许在生产记录中使用");
                    continue;
                }
                var sections = GetSectionsFromProcessGroup(pg);
                if (!sections.Any(s => s.SectionName == request.SectionName))
                    requestErrors.Add($"第{i + 1}行：工段「{request.SectionName}」不存在于工序组「{pg.ProcessName}」中，无法提交");
            }
        }

        if (requestErrors.Any())
            throw new BusinessException(string.Join("；", requestErrors));

        // ========== 构建实体 ==========
        foreach (var request in requests)
        {
            var batch = batchLookup[request.BatchNo];
            var batchId = batch.Id;

            // 自动解析 ProcessGroupId（从该批次的工序组中查找）
            var processGroupId = request.ProcessGroupId;
            if (processGroupId == null || processGroupId == 0)
            {
                var matchedPg = pgByBatch.GetValueOrDefault(batchId)?
                    .FirstOrDefault(pg => pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec);
                processGroupId = matchedPg?.Id;
            }

            // 自动解析 SequenceNumber
            var sequenceNumber = request.SequenceNumber;
            if (sequenceNumber == 0 && processGroupId > 0)
            {
                var pg = processGroups.FirstOrDefault(pg => pg.Id == processGroupId.Value);
                if (pg != null)
                {
                    var sections = GetSectionsFromProcessGroup(pg);
                    var match = sections.FirstOrDefault(s => s.SectionName == request.SectionName);
                    sequenceNumber = match.Sequence;
                }
            }

            entities.Add(new ProductionRecord
            {
                ProductionBatchId = batchId,
                ProcessGroupId = processGroupId ?? 0,
                ProcessName = request.ProcessName,
                ManufacturingSpec = request.ManufacturingSpec,
                SectionName = request.SectionName,
                SequenceNumber = sequenceNumber,
                ExecDate = request.ExecDate,
                EquipmentName = request.EquipmentName,
                Operator = request.Operator,
                Shift = request.Shift,
                Quantity = request.Quantity,
                Weight = request.Weight,
                IsFinished = request.IsFinished,
                CuttingMultiple = request.CuttingMultiple,
                FinishedCutLength = request.FinishedCutLength,
                PostCutQuantity = request.PostCutQuantity,
                TagNo = request.TagNo ?? batch.TagNo,
                PlantGrade = request.PlantGrade ?? batch.PlantGrade,
                Remark = request.Remark
            });
        }

        _context.ProductionRecords.AddRange(entities);
        await _context.SaveChangesAsync();

        // 批量刷新所有涉及批次的跟踪字段
        var distinctBatchIds = entities.Select(e => e.ProductionBatchId).Distinct().ToList();
        await BatchUpdateTrackingFromRecordsAsync(distinctBatchIds);

        return entities.Select(e => new ProductionRecordDto
        {
            Id = e.Id,
            ProductionBatchId = e.ProductionBatchId,
            ProcessGroupId = e.ProcessGroupId,
            ProcessName = e.ProcessName,
            ManufacturingSpec = e.ManufacturingSpec,
            SectionName = e.SectionName,
            SequenceNumber = e.SequenceNumber,
            ExecDate = e.ExecDate,
            EquipmentName = e.EquipmentName,
            Operator = e.Operator,
            Shift = e.Shift,
            Quantity = e.Quantity,
            Weight = e.Weight,
            IsFinished = e.IsFinished,
            CuttingMultiple = e.CuttingMultiple,
            FinishedCutLength = e.FinishedCutLength,
            PostCutQuantity = e.PostCutQuantity,
            TagNo = e.TagNo,
            PlantGrade = e.PlantGrade,
            Remark = e.Remark
        }).ToList();
    }

    public async Task<ProductionRecordDto> UpdateProductionRecordAsync(int id, UpdateProductionRecordRequest request)
    {
        var entity = await _context.ProductionRecords.FindAsync(id)
            ?? throw new BusinessException("生产记录不存在");

        entity.ExecDate = request.ExecDate;
        entity.EquipmentName = request.EquipmentName ?? entity.EquipmentName;
        entity.Operator = request.Operator ?? entity.Operator;
        entity.Shift = request.Shift ?? entity.Shift;
        entity.Quantity = request.Quantity ?? entity.Quantity;
        entity.Weight = request.Weight ?? entity.Weight;
        entity.IsFinished = request.IsFinished;
        entity.CuttingMultiple = request.CuttingMultiple ?? entity.CuttingMultiple;
        entity.FinishedCutLength = request.FinishedCutLength ?? entity.FinishedCutLength;
        entity.PostCutQuantity = request.PostCutQuantity ?? entity.PostCutQuantity;
        entity.TagNo = request.TagNo ?? entity.TagNo;
        entity.PlantGrade = request.PlantGrade ?? entity.PlantGrade;
        entity.Remark = request.Remark ?? entity.Remark;

        _context.ProductionRecords.Update(entity);
        await _context.SaveChangesAsync();

        await UpdateBatchTrackingFromRecords(entity.ProductionBatchId);

        return new ProductionRecordDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            ProcessGroupId = entity.ProcessGroupId,
            ProcessName = entity.ProcessName,
            ManufacturingSpec = entity.ManufacturingSpec,
            SectionName = entity.SectionName,
            SequenceNumber = entity.SequenceNumber,
            ExecDate = entity.ExecDate,
            EquipmentName = entity.EquipmentName,
            Operator = entity.Operator,
            Shift = entity.Shift,
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            IsFinished = entity.IsFinished,
            CuttingMultiple = entity.CuttingMultiple,
            FinishedCutLength = entity.FinishedCutLength,
            PostCutQuantity = entity.PostCutQuantity,
            TagNo = entity.TagNo,
            PlantGrade = entity.PlantGrade,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task DeleteProductionRecordAsync(int id)
    {
        var entity = await _context.ProductionRecords.FindAsync(id)
            ?? throw new BusinessException("生产记录不存在");

        var batchId = entity.ProductionBatchId;
        _context.ProductionRecords.Remove(entity);
        await _context.SaveChangesAsync();

        await UpdateBatchTrackingFromRecords(batchId);
    }

    // ========== 工段委外 ==========

    public async Task<PagedResult<SectionOutsourceDto>> GetSectionOutsourcesAsync(int batchId, QueryParams query)
    {
        var queryable = _context.SectionOutsources
            .AsNoTracking()
            .Where(s => s.ProductionBatchId == batchId);

        var totalCount = await queryable.CountAsync();

        var items = await queryable
            .OrderBy(s => s.SequenceNumber)
            .ThenBy(s => s.SendOutDate)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(s => new SectionOutsourceDto
            {
                Id = s.Id,
                ProductionBatchId = s.ProductionBatchId,
                ProcessGroupId = s.ProcessGroupId,
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
                TotalRecoveredQuantity = s.OutsourceRecoveries.Sum(r => r.RecoveryQuantity)
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

    public async Task<SectionOutsourceDto> CreateSectionOutsourceAsync(CreateSectionOutsourceRequest request)
    {
        var batch = await _context.ProductionBatches.FirstOrDefaultAsync(b => b.BatchNo == request.BatchNo)
            ?? throw new BusinessException($"批次不存在: {request.BatchNo}");

        var processGroupId = request.ProcessGroupId;
        if (processGroupId == null || processGroupId == 0)
        {
            var pg = await _context.ProcessGroups
                .Where(pg => pg.ProductionBatchId == batch.Id && pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec)
                .Select(pg => (int?)pg.Id)
                .FirstOrDefaultAsync();
            processGroupId = pg ?? 0;
        }

        var entity = new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = processGroupId ?? 0,
            ProcessName = request.ProcessName,
            ManufacturingSpec = request.ManufacturingSpec,
            SectionName = request.SectionName,
            SequenceNumber = request.SequenceNumber,
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

        await UpdateBatchTrackingFromRecords(batch.Id);

        return ToSectionOutsourceDto(entity, batch.BatchNo);
    }

    private static SectionOutsourceDto ToSectionOutsourceDto(SectionOutsource entity, string batchNo)
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

    public async Task DeleteSectionOutsourceAsync(int id)
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

        await UpdateBatchTrackingFromRecords(batchId);
    }

    // ========== 委外回收 ==========

    public async Task<List<OutsourceRecoveryDto>> GetOutsourceRecoveriesAsync(int outsourceId)
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
                Remark = r.Remark
            })
            .ToListAsync();
    }

    public async Task<OutsourceRecoveryDto> CreateOutsourceRecoveryAsync(CreateOutsourceRecoveryRequest request)
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

        // 更新委外状态
        await UpdateOutsourceStatus(outsource);

        await UpdateBatchTrackingFromRecords(outsource.ProductionBatchId);

        return new OutsourceRecoveryDto
        {
            Id = entity.Id,
            SectionOutsourceId = entity.SectionOutsourceId,
            RecoveryDate = entity.RecoveryDate,
            RecoveryQuantity = entity.RecoveryQuantity,
            RecoveryWeight = entity.RecoveryWeight,
            UnprocessedQuantity = entity.UnprocessedQuantity,
            UnprocessedWeight = entity.UnprocessedWeight,
            Remark = entity.Remark
        };
    }

    public async Task DeleteOutsourceRecoveryAsync(int id)
    {
        var entity = await _context.OutsourceRecoveries.FindAsync(id)
            ?? throw new BusinessException("委外回收记录不存在");

        var outsource = await _context.SectionOutsources.FindAsync(entity.SectionOutsourceId);
        var batchId = outsource?.ProductionBatchId;

        _context.OutsourceRecoveries.Remove(entity);
        await _context.SaveChangesAsync();

        if (outsource != null)
        {
            await UpdateOutsourceStatus(outsource);
            if (batchId.HasValue)
                await UpdateBatchTrackingFromRecords(batchId.Value);
        }
    }

    private async Task UpdateOutsourceStatus(SectionOutsource outsource)
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
            _logger.LogInformation("委外 (Id={Id}) 回收完成，状态→已回收（重量 {TotalWeight}/{Threshold}）", outsource.Id, totalRecoveredWeight, threshold);
        }
        else if (!isCompleted && outsource.Status != SectionOutsourceStatus.PendingRecovery)
        {
            outsource.Status = SectionOutsourceStatus.PendingRecovery;
            _context.SectionOutsources.Update(outsource);
            await _context.SaveChangesAsync();
        }
    }

    // ========== 检验到料 ==========

    public async Task<MaterialReceiveCheckDto?> GetMaterialReceiveCheckAsync(int batchId)
    {
        return await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(m => m.ProductionBatchId == batchId)
            .Select(m => new MaterialReceiveCheckDto
            {
                Id = m.Id,
                ProductionBatchId = m.ProductionBatchId,
                ReceiveDate = m.ReceiveDate,
                ReceivedQuantity = m.ReceivedQuantity,
                ReceivedWeight = m.ReceivedWeight,
                Shift = m.Shift,
                Checker = m.Checker,
                Remark = m.Remark,
                CreatedTime = m.CreatedTime,
                UpdatedTime = m.UpdatedTime
            })
            .FirstOrDefaultAsync();
    }

    public async Task<MaterialReceiveCheckDto> CreateMaterialReceiveCheckAsync(CreateMaterialReceiveCheckRequest request)
    {
        // 优先通过BatchNo解析，兼容直接传ProductionBatchId
        if (request.ProductionBatchId <= 0 && !string.IsNullOrWhiteSpace(request.BatchNo))
        {
            var batchByNo = await _context.ProductionBatches
                .FirstOrDefaultAsync(b => b.BatchNo == request.BatchNo)
                ?? throw new BusinessException($"批次号不存在: {request.BatchNo}");
            request.ProductionBatchId = batchByNo.Id;
        }

        var batch = await _context.ProductionBatches.FindAsync(request.ProductionBatchId)
            ?? throw new BusinessException($"批次不存在: {request.ProductionBatchId}");

        // 检查是否已存在检验到料记录
        var exists = await _context.MaterialReceiveChecks
            .AnyAsync(m => m.ProductionBatchId == request.ProductionBatchId);
        if (exists)
            throw new BusinessException("该批次已完成检验到料，不能重复创建");

        var entity = new MaterialReceiveCheck
        {
            ProductionBatchId = request.ProductionBatchId,
            ReceiveDate = request.ReceiveDate,
            ReceivedQuantity = request.ReceivedQuantity,
            ReceivedWeight = request.ReceivedWeight,
            Shift = request.Shift,
            Checker = request.Checker,
            Remark = request.Remark
        };

        _context.MaterialReceiveChecks.Add(entity);

        // 批次设为完成
        batch.Status = BatchStatus.Completed;
        _context.ProductionBatches.Update(batch);

        await _context.SaveChangesAsync();

        await RefreshBatchTrackingFieldsAsync(batch.Id);

        return new MaterialReceiveCheckDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            ReceiveDate = entity.ReceiveDate,
            ReceivedQuantity = entity.ReceivedQuantity,
            ReceivedWeight = entity.ReceivedWeight,
            Shift = entity.Shift,
            Checker = entity.Checker,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task<List<MaterialReceiveCheckDto>> BatchCreateMaterialReceiveChecksAsync(List<CreateMaterialReceiveCheckRequest> requests)
    {
        if (requests.Count == 0)
            return new List<MaterialReceiveCheckDto>();

        // 预加载所有涉及的批次
        var batchNos = requests.Where(r => r.ProductionBatchId <= 0 && !string.IsNullOrWhiteSpace(r.BatchNo))
            .Select(r => r.BatchNo).Distinct().ToList();
        var batchLookup = batchNos.Count > 0
            ? await _context.ProductionBatches.Where(b => batchNos.Contains(b.BatchNo)).ToDictionaryAsync(b => b.BatchNo)
            : new Dictionary<string, ProductionBatch>();

        // 检查所有批次是否存在
        var modifiedBatches = new List<ProductionBatch>();
        var existingCheckBatchIds = new HashSet<int>();

        foreach (var request in requests)
        {
            if (request.ProductionBatchId <= 0 && !string.IsNullOrWhiteSpace(request.BatchNo))
            {
                if (!batchLookup.TryGetValue(request.BatchNo, out var batchByNo))
                    throw new BusinessException($"批次号不存在: {request.BatchNo}");
                request.ProductionBatchId = batchByNo.Id;
                modifiedBatches.Add(batchByNo);
            }
            else
            {
                var batch = await _context.ProductionBatches.FindAsync(request.ProductionBatchId)
                    ?? throw new BusinessException($"批次不存在: {request.ProductionBatchId}");
                modifiedBatches.Add(batch);
            }

            // 延迟批量检查重复（先收集IDs）
            existingCheckBatchIds.Add(request.ProductionBatchId);
        }

        // 一次查出已存在检验到料的批次ID
        var existingBatchIds = await _context.MaterialReceiveChecks
            .Where(m => existingCheckBatchIds.Contains(m.ProductionBatchId))
            .Select(m => m.ProductionBatchId)
            .ToListAsync();

        if (existingBatchIds.Count > 0)
        {
            var dupBatchNos = modifiedBatches
                .Where(b => existingBatchIds.Contains(b.Id))
                .Select(b => b.BatchNo);
            throw new BusinessException($"批次 \"{string.Join(", ", dupBatchNos)}\" 已完成检验到料，不能重复创建");
        }

        var entities = new List<MaterialReceiveCheck>();
        foreach (var request in requests)
        {
            entities.Add(new MaterialReceiveCheck
            {
                ProductionBatchId = request.ProductionBatchId,
                ReceiveDate = request.ReceiveDate,
                ReceivedQuantity = request.ReceivedQuantity,
                ReceivedWeight = request.ReceivedWeight,
                Shift = request.Shift,
                Checker = request.Checker,
                Remark = request.Remark
            });
        }

        // 批次设为完成
        foreach (var batch in modifiedBatches)
            batch.Status = BatchStatus.Completed;

        _context.MaterialReceiveChecks.AddRange(entities);
        _context.ProductionBatches.UpdateRange(modifiedBatches);
        await _context.SaveChangesAsync();

        // 批量刷新跟踪字段
        var distinctBatchIds = modifiedBatches.Select(b => b.Id).Distinct().ToList();
        await BatchUpdateTrackingFromRecordsAsync(distinctBatchIds);

        return entities.Select(e => new MaterialReceiveCheckDto
        {
            Id = e.Id,
            ProductionBatchId = e.ProductionBatchId,
            ReceiveDate = e.ReceiveDate,
            ReceivedQuantity = e.ReceivedQuantity,
            ReceivedWeight = e.ReceivedWeight,
            Shift = e.Shift,
            Checker = e.Checker,
            Remark = e.Remark,
            CreatedTime = e.CreatedTime,
            UpdatedTime = e.UpdatedTime
        }).ToList();
    }

    public async Task<MaterialReceiveCheckDto> UpdateMaterialReceiveCheckAsync(int id, UpdateMaterialReceiveCheckRequest request)
    {
        var entity = await _context.MaterialReceiveChecks.FindAsync(id)
            ?? throw new BusinessException("检验到料记录不存在");

        entity.ReceiveDate = request.ReceiveDate;
        entity.ReceivedQuantity = request.ReceivedQuantity ?? entity.ReceivedQuantity;
        entity.ReceivedWeight = request.ReceivedWeight ?? entity.ReceivedWeight;
        entity.Shift = request.Shift ?? entity.Shift;
        entity.Checker = request.Checker ?? entity.Checker;
        entity.Remark = request.Remark ?? entity.Remark;

        _context.MaterialReceiveChecks.Update(entity);
        await _context.SaveChangesAsync();

        await RefreshBatchTrackingFieldsAsync(entity.ProductionBatchId);

        return new MaterialReceiveCheckDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            ReceiveDate = entity.ReceiveDate,
            ReceivedQuantity = entity.ReceivedQuantity,
            ReceivedWeight = entity.ReceivedWeight,
            Shift = entity.Shift,
            Checker = entity.Checker,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task DeleteMaterialReceiveCheckAsync(int id)
    {
        var entity = await _context.MaterialReceiveChecks.FindAsync(id)
            ?? throw new BusinessException("检验到料记录不存在");

        var batchId = entity.ProductionBatchId;
        _context.MaterialReceiveChecks.Remove(entity);

        // 重置批次状态为进行中
        var batch = await _context.ProductionBatches.FindAsync(batchId);
        if (batch != null)
        {
            batch.Status = BatchStatus.InProgress;
            _context.ProductionBatches.Update(batch);
        }

        await _context.SaveChangesAsync();

        // 删除检验到料后重新计算跟踪字段
        await RefreshBatchTrackingFieldsAsync(batchId);
    }

    // ========== 批次跟踪字段刷新 ==========

    public async Task RefreshBatchTrackingFieldsAsync(int batchId)
    {
        await UpdateBatchTrackingFromRecords(batchId);
    }

    public async Task BatchUpdateBatchTrackingAsync(ICollection<int> batchIds)
    {
        await BatchUpdateTrackingFromRecordsAsync(batchIds);
    }

    // ========== 批次跟踪可视化 ==========

    public async Task<BatchTrackingVisualDto> GetTrackingVisualAsync(int batchId)
    {
        // 1. 加载批次 + ProcessGroups
        var batch = await _context.ProductionBatches
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .FirstOrDefaultAsync(b => b.Id == batchId)
            ?? throw new BusinessException("批次不存在");

        // 2. 加载所有生产记录
        var allRecords = await _context.ProductionRecords
            .Where(r => r.ProductionBatchId == batchId)
            .OrderBy(r => r.SequenceNumber)
            .ThenBy(r => r.ExecDate)
            .ToListAsync();

        // 3. 加载所有工段委外（含回收统计）
        var allOutsources = await _context.SectionOutsources
            .Where(s => s.ProductionBatchId == batchId)
            .Select(s => new
            {
                s.Id,
                s.ProcessGroupId,
                s.SectionName,
                s.SequenceNumber,
                s.ProcessName,
                s.OutsourceVendor,
                s.SendOutDate,
                s.Status,
                s.SendWeight,
                TotalRecoveredWeight = s.OutsourceRecoveries.Sum(r =>
                    (r.RecoveryWeight ?? 0) + (r.UnprocessedWeight ?? 0))
            })
            .ToListAsync();

        // 3b. 加载所有过程检验
        var allInspections = await _context.ProcessInspections
            .Where(p => p.ProductionBatchId == batchId)
            .ToListAsync();

        // 3c. 加载检验到料（批次级，无工序组关联）
        var materialReceiveCheck = await _context.MaterialReceiveChecks
            .Where(m => m.ProductionBatchId == batchId)
            .OrderByDescending(m => m.ReceiveDate)
            .FirstOrDefaultAsync();

        // 4. 构建查询字典
        var recordByKey = allRecords
            .GroupBy(r => (r.ProcessGroupId, r.SectionName))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ExecDate).First());

        var outsourceByKey = allOutsources
            .GroupBy(s => (s.ProcessGroupId, s.SectionName))
            .ToDictionary(g => g.Key, g => g.First());

        // 4b. 过程检验查询字典
        var inspectionByKey = allInspections
            .GroupBy(p => (p.ProcessGroupId, p.SectionName))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.InspectionDate).First());

        // 4c. 找到最后一个工段的序号（用于检验到料日期兜底）
        var lastSectionSeq = -1;

        // 5. 构建所有工段的完成状态
        var maxRecordSeq = allRecords.Count > 0 ? allRecords.Max(r => r.SequenceNumber) : -1;
        var maxOutsourceSeq = allOutsources.Count > 0 ? allOutsources.Max(s => s.SequenceNumber) : -1;
        var currentMaxSeq = Math.Max(maxRecordSeq, maxOutsourceSeq);

        var totalSectionCount = 0;
        var completedSectionCount = 0;
        var allSectionDtos = new List<SectionVisualDto>();

        var processGroupDtos = new List<ProcessGroupVisualDto>();

        foreach (var pg in batch.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
        {
            var sections = GetSectionsFromProcessGroup(pg);
            if (sections.Count == 0) continue;

            var groupSectionDtos = new List<SectionVisualDto>();
            var groupCompleted = 0;

            foreach (var (sectionName, seq) in sections)
            {
                var key = (pg.Id, sectionName);
                var hasRecord = recordByKey.TryGetValue(key, out var record);
                var hasOutsource = outsourceByKey.TryGetValue(key, out var outsource);

                // 确定状态
                string status;
                if (hasRecord)
                    status = "Completed";
                else if (hasOutsource && outsource.Status == SectionOutsourceStatus.Recovered)
                    status = "Completed";
                else if (hasOutsource)
                    status = "Outsource";
                else if (seq == currentMaxSeq + 1)
                    status = "Next";
                else if (seq <= currentMaxSeq)
                    status = "Completed"; // 跨工序组时，之前组的未记录工段视作已完成
                else
                    status = "Pending";

                // 当前正在进行的工段
                if (seq == currentMaxSeq && status != "Completed" && status != "Outsource")
                    status = "InProgress";

                // 修正：如果有记录则为 Completed
                if (hasRecord)
                    status = "Completed";

                if (status == "Completed") groupCompleted++;

                // 委外进度
                decimal? outsourceProgress = null;
                if (hasOutsource && outsource.SendWeight > 0)
                {
                    outsourceProgress = Math.Round(
                        (decimal)outsource.TotalRecoveredWeight / outsource.SendWeight.Value * 100, 1);
                }

                var sectionDto = new SectionVisualDto
                {
                    SectionName = sectionName,
                    SequenceNumber = seq,
                    ProcessGroupId = pg.Id,
                    Status = status,
                    ExecDate = record?.ExecDate
                        ?? (inspectionByKey.TryGetValue(key, out var insp) ? insp.InspectionDate : (DateTime?)null)
                        ?? (hasOutsource ? outsource.SendOutDate : (DateTime?)null),
                    EquipmentName = record?.EquipmentName,
                    Quantity = record?.Quantity,
                    Weight = record?.Weight,
                    Operator = record?.Operator,
                    OutsourceVendor = hasOutsource ? outsource.OutsourceVendor : null,
                    OutsourceProgress = hasOutsource
                        ? (outsource.SendWeight > 0
                            ? Math.Round((decimal)outsource.TotalRecoveredWeight / outsource.SendWeight.Value * 100, 1)
                            : null)
                        : null
                };

                groupSectionDtos.Add(sectionDto);
                allSectionDtos.Add(sectionDto);
            }

            processGroupDtos.Add(new ProcessGroupVisualDto
            {
                Id = pg.Id,
                SequenceNumber = pg.SequenceNumber,
                ProcessName = pg.ProcessName,
                ManufacturingSpec = pg.ManufacturingSpec,
                TotalSections = sections.Count,
                CompletedSections = groupCompleted,
                Sections = groupSectionDtos
            });

            totalSectionCount += sections.Count;
            completedSectionCount += groupCompleted;
        }

        // 5b. 检验到料日期兜底：若无其他日期，则取 MaterialReceiveCheck.ReceiveDate 作为最后工段日期
        if (materialReceiveCheck != null && allSectionDtos.Count > 0)
        {
            var lastSection = allSectionDtos.MaxBy(s => s.SequenceNumber);
            if (lastSection != null && !lastSection.ExecDate.HasValue)
                lastSection.ExecDate = materialReceiveCheck.ReceiveDate;
        }

        // 6. 计算投料量与目标量（使用现有效原料数据）
        int? inputQty = batch.CurrentValidQty;
        int? inputWt = batch.CurrentValidWeight.HasValue ? (int?)Math.Round(batch.CurrentValidWeight.Value) : null;

        // 目标支数 = 投料支数 × 制几率
        int? targetQty = batch.ProductionRatio > 0 && inputQty.HasValue
            ? inputQty.Value * batch.ProductionRatio
            : null;

        // 目标重量 = 投料重量 × (1 - 有效工序组数 × 0.025)
        var effectiveGroupCount = batch.ProcessGroups
            .Count(pg => GetSectionsFromProcessGroup(pg).Count > 0);
        var discount = 1.0m - effectiveGroupCount * 0.025m;
        if (discount < 0) discount = 0;
        int? targetWt = inputWt.HasValue
            ? (int?)Math.Round(inputWt.Value * discount)
            : null;

        // 7. 组装返回
        var maxBySeq = allSectionDtos
            .Where(s => s.Status == "InProgress" || s.Status == "Outsource" || s.Status == "Completed")
            .OrderByDescending(s => s.SequenceNumber)
            .FirstOrDefault();

        var nextBySeq = allSectionDtos
            .Where(s => s.Status == "Next")
            .OrderBy(s => s.SequenceNumber)
            .FirstOrDefault();

        return new BatchTrackingVisualDto
        {
            BatchId = batch.Id,
            BatchNo = batch.BatchNo,
            TotalSectionCount = totalSectionCount,
            CompletedSectionCount = completedSectionCount,

            CurrentGroupName = maxBySeq != null
                ? batch.ProcessGroups
                    .Where(pg => pg.Id == maxBySeq.ProcessGroupId)
                    .Select(pg => pg.ProcessName)
                    .FirstOrDefault()
                : null,
            CurrentSectionName = maxBySeq?.SectionName,
            CurrentEquipmentName = maxBySeq?.EquipmentName,
            CurrentOutsource = maxBySeq?.OutsourceVendor,
            CurrentSpec = maxBySeq != null
                ? batch.ProcessGroups
                    .Where(pg => pg.Id == maxBySeq.ProcessGroupId)
                    .Select(pg => pg.ManufacturingSpec)
                    .FirstOrDefault()
                : null,
            NextSectionName = nextBySeq?.SectionName,

            InputQuantity = inputQty,
            InputWeight = inputWt,
            TargetQuantity = targetQty,
            TargetWeight = targetWt,

            ProcessGroups = processGroupDtos
        };
    }

    private async Task UpdateBatchTrackingFromRecords(int batchId)
    {
        var batch = await _context.ProductionBatches
            .Include(b => b.ProcessGroups)
            .FirstOrDefaultAsync(b => b.Id == batchId);

        if (batch == null) return;

        // 强制完成的批次不自动跟踪
        if (batch.IsForceCompleted) return;

        // ProcessGroups 为 null 时保护
        if (batch.ProcessGroups == null)
        {
            _logger.LogWarning("批次 {BatchId} 的 ProcessGroups 为 null", batchId);
            return;
        }

        try
        {

        // 检验到料存在：截止执行日 = 到料日期，不计算其他跟踪字段
        var materialCheckDate = await _context.MaterialReceiveChecks
            .Where(m => m.ProductionBatchId == batchId)
            .Select(m => (DateTime?)m.ReceiveDate)
            .FirstOrDefaultAsync();
        if (materialCheckDate.HasValue)
        {
            batch.CurrentExecDate = materialCheckDate.Value;
            if (batch.Status != BatchStatus.Completed)
                batch.Status = BatchStatus.Completed;
            _context.ProductionBatches.Update(batch);
            await _context.SaveChangesAsync();
            return;
        }

        // 收集该批次的所有生产记录
        var productionRecords = await _context.ProductionRecords
            .Where(r => r.ProductionBatchId == batchId)
            .OrderBy(r => r.SequenceNumber)
            .ThenBy(r => r.ExecDate)
            .ToListAsync();

        // 收集该批次的所有工段委外（含待回收和已回收）及各自的回收记录数
        var sectionOutsources = await _context.SectionOutsources
            .Where(s => s.ProductionBatchId == batchId)
            .Select(s => new
            {
                s.Id,
                s.ProcessGroupId,
                s.SectionName,
                s.SequenceNumber,
                s.ProcessName,
                s.OutsourceVendor,
                s.SendOutDate,
                RecoveryCount = s.OutsourceRecoveries.Count
            })
            .OrderBy(s => s.SequenceNumber)
            .ToListAsync();

        // 收集该批次的所有过程检验记录
        var processInspections = await _context.ProcessInspections
            .Where(p => p.ProductionBatchId == batchId)
            .OrderBy(p => p.SequenceNumber)
            .ThenBy(p => p.InspectionDate)
            .ToListAsync();

        var hasRecords = productionRecords.Count > 0 || sectionOutsources.Count > 0 || processInspections.Count > 0;

        // ====== 1. 状态 ======
        // 挂起/强制完成状态不自动覆盖
        if (batch.Status != BatchStatus.Suspended)
            batch.Status = hasRecords ? BatchStatus.InProgress : BatchStatus.None;

        // ====== 3-5. 当前工段/工序/设备/委外/规格 + 截止执行日 ======
        // 构建 ProcessGroup 查表（Id -> ManufacturingSpec）
        var pgSpecLookup = batch.ProcessGroups
            .ToDictionary(pg => pg.Id, pg => pg.ManufacturingSpec);

        // 在生产记录中找最大 SequenceNumber 的记录
        ProductionRecord? maxSeqRecord = productionRecords
            .OrderByDescending(r => r.SequenceNumber)
            .ThenByDescending(r => r.ExecDate)
            .FirstOrDefault();

        // 在工段委外中找最大 SequenceNumber 的记录
        var maxSeqOutsource = sectionOutsources
            .OrderByDescending(s => s.SequenceNumber)
            .FirstOrDefault();

        // 在过程检验中找最大 SequenceNumber 的记录
        ProcessInspection? maxSeqInspection = processInspections
            .OrderByDescending(p => p.SequenceNumber)
            .ThenByDescending(p => p.InspectionDate)
            .FirstOrDefault();

        int maxRecordSeq = maxSeqRecord?.SequenceNumber ?? -1;
        int maxOutsourceSeq = maxSeqOutsource?.SequenceNumber ?? -1;
        int maxInspectionSeq = maxSeqInspection?.SequenceNumber ?? -1;

        // 三取最大
        int overallMaxSeq = Math.Max(Math.Max(maxRecordSeq, maxOutsourceSeq), maxInspectionSeq);

        // 截止执行日 = 最大 SequenceNumber 记录的执行日期
        if (overallMaxSeq == maxRecordSeq && maxSeqRecord != null)
            batch.CurrentExecDate = maxSeqRecord.ExecDate;
        else if (overallMaxSeq == maxOutsourceSeq && maxSeqOutsource != null)
            batch.CurrentExecDate = maxSeqOutsource.SendOutDate;
        else if (overallMaxSeq == maxInspectionSeq && maxSeqInspection != null)
            batch.CurrentExecDate = maxSeqInspection.InspectionDate;
        else
            batch.CurrentExecDate = null;

        if (overallMaxSeq == maxRecordSeq)
        {
            // 最大值在生产记录上（含三者都无记录的情况）
            if (maxSeqRecord != null)
            {
                batch.CurrentGroupName = maxSeqRecord.ProcessName;
                batch.CurrentSectionName = maxSeqRecord.SectionName;
                batch.CurrentEquipmentName = maxSeqRecord.EquipmentName;
                batch.CurrentSpec = pgSpecLookup.GetValueOrDefault(maxSeqRecord.ProcessGroupId);
            }
            else
            {
                batch.CurrentGroupName = null;
                batch.CurrentSectionName = null;
                batch.CurrentEquipmentName = null;
                batch.CurrentSpec = null;
            }
            batch.CurrentOutsource = null;
        }
        else if (overallMaxSeq == maxOutsourceSeq)
        {
            // 最大值在工段委外上
            batch.CurrentGroupName = maxSeqOutsource.ProcessName;
            batch.CurrentSectionName = maxSeqOutsource.SectionName;
            batch.CurrentEquipmentName = null;
            batch.CurrentSpec = pgSpecLookup.GetValueOrDefault(maxSeqOutsource.ProcessGroupId);
            // 当前委外：无回收记录才显示委外单位名
            batch.CurrentOutsource = maxSeqOutsource.RecoveryCount == 0
                ? maxSeqOutsource.OutsourceVendor
                : null;
        }
        else
        {
            // 最大值在过程检验上
            batch.CurrentGroupName = maxSeqInspection.ProcessName;
            batch.CurrentSectionName = maxSeqInspection.SectionName;
            batch.CurrentEquipmentName = maxSeqInspection.EquipmentName;
            batch.CurrentSpec = pgSpecLookup.GetValueOrDefault(maxSeqInspection.ProcessGroupId);
            batch.CurrentOutsource = null;
        }

        // ====== 7. 下一工段 / 对应规格 ======
        // 取三者最大 SequenceNumber，+1 即为下一工段的全局执行序号
        var allSections = batch.ProcessGroups
            .SelectMany(pg => GetSectionsFromProcessGroup(pg)
                .Select(s => new { pgId = pg.Id, s.SectionName, s.Sequence }))
            .ToList();

        int nextSeq = overallMaxSeq + 1;

        var nextSection = allSections.FirstOrDefault(s => s.Sequence == nextSeq);

        batch.NextSectionName = nextSection?.SectionName;
        batch.CorrespondingSpec = nextSection != null
            ? pgSpecLookup.GetValueOrDefault(nextSection.pgId)
            : null;

        _context.ProductionBatches.Update(batch);
        await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新批次跟踪字段失败 (BatchId={BatchId})", batchId);
            throw;
        }
    }

    /// <summary>
    /// 批量刷新多个批次的跟踪字段
    /// 一次查询所有数据，内存分组计算，一次SaveChanges
    /// </summary>
    private async Task BatchUpdateTrackingFromRecordsAsync(ICollection<int> batchIds)
    {
        if (batchIds.Count == 0) return;

        // 1. 加载所有批次 + ProcessGroups
        var batchDict = await _context.ProductionBatches
            .Include(b => b.ProcessGroups)
            .Where(b => batchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id);

        if (batchDict.Count == 0) return;

        // 2. 找出已有检验到料的批次（设完成+截止执行日，跳过计算）
        var materialCheckData = await _context.MaterialReceiveChecks
            .Where(m => batchIds.Contains(m.ProductionBatchId))
            .Select(m => new { m.ProductionBatchId, m.ReceiveDate })
            .ToListAsync();
        var materialCheckBatchIds = materialCheckData
            .Select(m => m.ProductionBatchId)
            .Distinct()
            .ToList();
        var materialCheckDateLookup = materialCheckData
            .GroupBy(m => m.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.Max(m => m.ReceiveDate));

        // 3. 活跃批次（非强制完成、无检验到料）
        var activeBatchIds = batchDict.Keys
            .Where(id => !batchDict[id].IsForceCompleted && !materialCheckBatchIds.Contains(id))
            .ToList();

        // 有检验到料的批次 → 已完成 + 截止执行日 = 到料日期
        foreach (var id in materialCheckBatchIds)
        {
            if (batchDict.TryGetValue(id, out var b))
            {
                if (b.Status != BatchStatus.Completed)
                    b.Status = BatchStatus.Completed;
                if (materialCheckDateLookup.TryGetValue(id, out var receiveDate))
                    b.CurrentExecDate = receiveDate;
            }
        }

        if (activeBatchIds.Count == 0)
        {
            _context.ProductionBatches.UpdateRange(batchDict.Values);
            await _context.SaveChangesAsync();
            return;
        }

        // 4. 一次查出所有活跃批次的生产记录
        var allRecords = await _context.ProductionRecords
            .Where(r => activeBatchIds.Contains(r.ProductionBatchId))
            .OrderBy(r => r.SequenceNumber)
            .ThenBy(r => r.ExecDate)
            .ToListAsync();
        var recordsByBatch = allRecords.GroupBy(r => r.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 5. 一次查出所有活跃批次的工段委外
        var allOutsources = await _context.SectionOutsources
            .Where(s => activeBatchIds.Contains(s.ProductionBatchId))
            .Select(s => new
            {
                s.Id,
                s.ProductionBatchId,
                s.ProcessGroupId,
                s.SectionName,
                s.SequenceNumber,
                s.ProcessName,
                s.OutsourceVendor,
                s.SendOutDate,
                RecoveryCount = s.OutsourceRecoveries.Count
            })
            .OrderBy(s => s.SequenceNumber)
            .ToListAsync();
        var outsourcesByBatch = allOutsources.GroupBy(s => s.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 5b. 一次查出所有活跃批次的过程检验记录
        var allInspections = await _context.ProcessInspections
            .Where(p => activeBatchIds.Contains(p.ProductionBatchId))
            .OrderBy(p => p.SequenceNumber)
            .ThenBy(p => p.InspectionDate)
            .ToListAsync();
        var inspectionsByBatch = allInspections.GroupBy(p => p.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 6. 一次查出所有活跃批次的回收日期
        var recoveryDateLookup = (await _context.OutsourceRecoveries
            .Where(r => activeBatchIds.Contains(r.SectionOutsource.ProductionBatchId))
            .GroupBy(r => r.SectionOutsource.ProductionBatchId)
            .Select(g => new { BatchId = g.Key, MaxDate = g.Max(r => (DateTime?)r.RecoveryDate) })
            .ToListAsync())
            .ToDictionary(r => r.BatchId, r => r.MaxDate);

        // 7. 逐批次计算跟踪字段
        foreach (var batchId in activeBatchIds)
        {
            var batch = batchDict[batchId];
            var pgSpecLookup = batch.ProcessGroups.ToDictionary(pg => pg.Id, pg => pg.ManufacturingSpec);

            var productionRecords = recordsByBatch.GetValueOrDefault(batchId) ?? new();
            var sectionOutsources = outsourcesByBatch.GetValueOrDefault(batchId) ?? new();
            var processInspections = inspectionsByBatch.GetValueOrDefault(batchId) ?? new();

            var hasRecords = productionRecords.Count > 0 || sectionOutsources.Count > 0 || processInspections.Count > 0;

            // 状态
            batch.Status = hasRecords ? BatchStatus.InProgress : BatchStatus.None;

            // 截止执行日 = 最大 SequenceNumber 记录的执行日期
            ProductionRecord? maxSeqRecord = productionRecords
                .OrderByDescending(r => r.SequenceNumber)
                .ThenByDescending(r => r.ExecDate)
                .FirstOrDefault();

            var maxSeqOutsource = sectionOutsources
                .OrderByDescending(s => s.SequenceNumber)
                .FirstOrDefault();

            ProcessInspection? maxSeqInspection = processInspections
                .OrderByDescending(p => p.SequenceNumber)
                .ThenByDescending(p => p.InspectionDate)
                .FirstOrDefault();

            int maxRecordSeq = maxSeqRecord?.SequenceNumber ?? -1;
            int maxOutsourceSeq = maxSeqOutsource?.SequenceNumber ?? -1;
            int maxInspectionSeq = maxSeqInspection?.SequenceNumber ?? -1;

            int overallMaxSeq = Math.Max(Math.Max(maxRecordSeq, maxOutsourceSeq), maxInspectionSeq);

            // 截止执行日 = 最大 SequenceNumber 记录的执行日期
            if (overallMaxSeq == maxRecordSeq && maxSeqRecord != null)
                batch.CurrentExecDate = maxSeqRecord.ExecDate;
            else if (overallMaxSeq == maxOutsourceSeq && maxSeqOutsource != null)
                batch.CurrentExecDate = maxSeqOutsource.SendOutDate;
            else if (overallMaxSeq == maxInspectionSeq && maxSeqInspection != null)
                batch.CurrentExecDate = maxSeqInspection.InspectionDate;
            else
                batch.CurrentExecDate = null;

            if (overallMaxSeq == maxRecordSeq)
            {
                if (maxSeqRecord != null)
                {
                    batch.CurrentGroupName = maxSeqRecord.ProcessName;
                    batch.CurrentSectionName = maxSeqRecord.SectionName;
                    batch.CurrentEquipmentName = maxSeqRecord.EquipmentName;
                    batch.CurrentSpec = pgSpecLookup.GetValueOrDefault(maxSeqRecord.ProcessGroupId);
                }
                else
                {
                    batch.CurrentGroupName = null;
                    batch.CurrentSectionName = null;
                    batch.CurrentEquipmentName = null;
                    batch.CurrentSpec = null;
                }
                batch.CurrentOutsource = null;
            }
            else if (overallMaxSeq == maxOutsourceSeq)
            {
                batch.CurrentGroupName = maxSeqOutsource.ProcessName;
                batch.CurrentSectionName = maxSeqOutsource.SectionName;
                batch.CurrentEquipmentName = null;
                batch.CurrentSpec = pgSpecLookup.GetValueOrDefault(maxSeqOutsource.ProcessGroupId);
                batch.CurrentOutsource = maxSeqOutsource.RecoveryCount == 0
                    ? maxSeqOutsource.OutsourceVendor
                    : null;
            }
            else
            {
                batch.CurrentGroupName = maxSeqInspection?.ProcessName;
                batch.CurrentSectionName = maxSeqInspection?.SectionName;
                batch.CurrentEquipmentName = maxSeqInspection?.EquipmentName;
                batch.CurrentSpec = maxSeqInspection != null
                    ? pgSpecLookup.GetValueOrDefault(maxSeqInspection.ProcessGroupId)
                    : null;
                batch.CurrentOutsource = null;
            }

            // 下一工段 / 对应规格
            var allSections = batch.ProcessGroups
                .SelectMany(pg => GetSectionsFromProcessGroup(pg)
                    .Select(s => new { pgId = pg.Id, s.SectionName, s.Sequence }))
                .ToList();

            int nextSeq = overallMaxSeq + 1;

            var nextSection = allSections.FirstOrDefault(s => s.Sequence == nextSeq);

            batch.NextSectionName = nextSection?.SectionName;
            batch.CorrespondingSpec = nextSection != null
                ? pgSpecLookup.GetValueOrDefault(nextSection.pgId)
                : null;
        }

        _context.ProductionBatches.UpdateRange(batchDict.Values);
        await _context.SaveChangesAsync();
    }

    // ========== 跨批次查询 ==========

    public async Task<PagedResult<ProductionRecordDto>> GetAllProductionRecordsAsync(QueryParams query)
    {
        var queryable = _context.ProductionRecords
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
                || (r.Operator != null && r.Operator.Contains(query.Keyword))
                || (r.Shift != null && r.Shift.Contains(query.Keyword))
                || (r.TagNo != null && r.TagNo.Contains(query.Keyword))
                || (r.PlantGrade != null && r.PlantGrade.Contains(query.Keyword))
                || (r.Remark != null && r.Remark.Contains(query.Keyword))
                || (r.DataSource != null && r.DataSource.Contains(query.Keyword)));
        }

        if (query.ExecDateFrom.HasValue)
            queryable = queryable.Where(r => r.ExecDate >= query.ExecDateFrom.Value);

        if (query.ExecDateTo.HasValue)
            queryable = queryable.Where(r => r.ExecDate <= query.ExecDateTo.Value);

        queryable = queryable.ApplyFilters(query.Filters);
        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.SortBy ?? "createdtime", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new ProductionRecordDto
            {
                Id = r.Id,
                ProductionBatchId = r.ProductionBatchId,
                ProcessGroupId = r.ProcessGroupId,
                ProcessName = r.ProcessName,
                ManufacturingSpec = r.ManufacturingSpec,
                SectionName = r.SectionName,
                SequenceNumber = r.SequenceNumber,
                ExecDate = r.ExecDate,
                EquipmentName = r.EquipmentName,
                Operator = r.Operator,
                Shift = r.Shift,
                Quantity = r.Quantity,
                Weight = r.Weight,
                IsFinished = r.IsFinished,
                CuttingMultiple = r.CuttingMultiple,
                FinishedCutLength = r.FinishedCutLength,
                PostCutQuantity = r.PostCutQuantity,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                Remark = r.Remark,
                DataSource = r.DataSource,
                BatchNo = r.ProductionBatch.BatchNo,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            })
            .ToListAsync();

        return new PagedResult<ProductionRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<ProductionRecordDto>> GetAllProductionRecordListAsync()
    {
        return await _context.ProductionRecords
            .AsNoTracking()
            .Include(r => r.ProductionBatch)
            .OrderByDescending(r => r.CreatedTime)
            .Select(r => new ProductionRecordDto
            {
                Id = r.Id,
                ProductionBatchId = r.ProductionBatchId,
                ProcessGroupId = r.ProcessGroupId,
                ProcessName = r.ProcessName,
                ManufacturingSpec = r.ManufacturingSpec,
                SectionName = r.SectionName,
                SequenceNumber = r.SequenceNumber,
                ExecDate = r.ExecDate,
                EquipmentName = r.EquipmentName,
                Operator = r.Operator,
                Shift = r.Shift,
                Quantity = r.Quantity,
                Weight = r.Weight,
                IsFinished = r.IsFinished,
                CuttingMultiple = r.CuttingMultiple,
                FinishedCutLength = r.FinishedCutLength,
                PostCutQuantity = r.PostCutQuantity,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                Remark = r.Remark,
                DataSource = r.DataSource,
                BatchNo = r.ProductionBatch.BatchNo,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            })
            .ToListAsync();
    }

    private static IQueryable<ProductionRecord> ApplySorting(IQueryable<ProductionRecord> queryable, string sortBy, bool isDescending)
    {
        return (sortBy.ToLowerInvariant(), isDescending) switch
        {
            ("execdate", false) => queryable.OrderBy(r => r.ExecDate),
            ("execdate", true) => queryable.OrderByDescending(r => r.ExecDate),
            ("batchno", false) => queryable.OrderBy(r => r.ProductionBatch.BatchNo),
            ("batchno", true) => queryable.OrderByDescending(r => r.ProductionBatch.BatchNo),
            ("processname", false) => queryable.OrderBy(r => r.ProcessName),
            ("processname", true) => queryable.OrderByDescending(r => r.ProcessName),
            ("manufacturingspec", false) => queryable.OrderBy(r => r.ManufacturingSpec ?? ""),
            ("manufacturingspec", true) => queryable.OrderByDescending(r => r.ManufacturingSpec ?? ""),
            ("sectionname", false) => queryable.OrderBy(r => r.SectionName),
            ("sectionname", true) => queryable.OrderByDescending(r => r.SectionName),
            ("sequencenumber", false) => queryable.OrderBy(r => r.SequenceNumber),
            ("sequencenumber", true) => queryable.OrderByDescending(r => r.SequenceNumber),
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
            ("isfinished", false) => queryable.OrderBy(r => r.IsFinished),
            ("isfinished", true) => queryable.OrderByDescending(r => r.IsFinished),
            ("cuttingmultiple", false) => queryable.OrderBy(r => r.CuttingMultiple ?? 0),
            ("cuttingmultiple", true) => queryable.OrderByDescending(r => r.CuttingMultiple ?? 0),
            ("finishedcutlength", false) => queryable.OrderBy(r => r.FinishedCutLength ?? 0),
            ("finishedcutlength", true) => queryable.OrderByDescending(r => r.FinishedCutLength ?? 0),
            ("postcutquantity", false) => queryable.OrderBy(r => r.PostCutQuantity ?? 0),
            ("postcutquantity", true) => queryable.OrderByDescending(r => r.PostCutQuantity ?? 0),
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
            _ => queryable.OrderByDescending(r => r.CreatedTime)
        };
    }

    public async Task<PagedResult<SectionOutsourceDto>> GetAllSectionOutsourcesAsync(QueryParams query)
    {
        var queryable = _context.SectionOutsources
            .AsNoTracking()
            .Include(s => s.OutsourceRecoveries)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            queryable = queryable.Where(s => s.OutsourceVendor.Contains(query.Keyword)
                || s.ProcessName.Contains(query.Keyword)
                || s.SectionName.Contains(query.Keyword));
        }

        var totalCount = await queryable.CountAsync();

        queryable = (query.SortBy?.ToLower(), query.IsDescending) switch
        {
            ("processname", false) => queryable.OrderBy(s => s.ProcessName),
            ("processname", true) => queryable.OrderByDescending(s => s.ProcessName),
            ("sectionname", false) => queryable.OrderBy(s => s.SectionName),
            ("sectionname", true) => queryable.OrderByDescending(s => s.SectionName),
            ("outsourcevendor", false) => queryable.OrderBy(s => s.OutsourceVendor),
            ("outsourcevendor", true) => queryable.OrderByDescending(s => s.OutsourceVendor),
            ("sendoutdate", false) => queryable.OrderBy(s => s.SendOutDate),
            ("sendoutdate", true) => queryable.OrderByDescending(s => s.SendOutDate),
            ("manufacturingspec", false) => queryable.OrderBy(s => s.ManufacturingSpec ?? ""),
            ("manufacturingspec", true) => queryable.OrderByDescending(s => s.ManufacturingSpec ?? ""),
            ("sequencenumber", false) => queryable.OrderBy(s => s.SequenceNumber),
            ("sequencenumber", true) => queryable.OrderByDescending(s => s.SequenceNumber),
            ("sendquantity", false) => queryable.OrderBy(s => s.SendQuantity ?? 0),
            ("sendquantity", true) => queryable.OrderByDescending(s => s.SendQuantity ?? 0),
            ("sendweight", false) => queryable.OrderBy(s => s.SendWeight ?? 0),
            ("sendweight", true) => queryable.OrderByDescending(s => s.SendWeight ?? 0),
            ("status", false) => queryable.OrderBy(s => s.Status),
            ("status", true) => queryable.OrderByDescending(s => s.Status),
            ("tagno", false) => queryable.OrderBy(s => s.TagNo ?? ""),
            ("tagno", true) => queryable.OrderByDescending(s => s.TagNo ?? ""),
            ("plantgrade", false) => queryable.OrderBy(s => s.PlantGrade ?? ""),
            ("plantgrade", true) => queryable.OrderByDescending(s => s.PlantGrade ?? ""),
            ("outsourcespec", false) => queryable.OrderBy(s => s.OutsourceSpec ?? ""),
            ("outsourcespec", true) => queryable.OrderByDescending(s => s.OutsourceSpec ?? ""),
            ("expectedreturndate", false) => queryable.OrderBy(s => s.ExpectedReturnDate ?? DateTime.MaxValue),
            ("expectedreturndate", true) => queryable.OrderByDescending(s => s.ExpectedReturnDate),
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
                TotalRecoveredQuantity = s.OutsourceRecoveries.Sum(r => r.RecoveryQuantity)
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

    public async Task<PagedResult<OutsourceRecoveryDto>> GetAllOutsourceRecoveriesAsync(QueryParams query)
    {
        var queryable = _context.OutsourceRecoveries
            .AsNoTracking()
            .Include(r => r.SectionOutsource)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            queryable = queryable.Where(r => r.SectionOutsource.OutsourceVendor.Contains(query.Keyword)
                || r.SectionOutsource.ProcessName.Contains(query.Keyword)
                || (r.SectionOutsource.ProductionBatch != null && r.SectionOutsource.ProductionBatch.BatchNo.Contains(query.Keyword))
                || (r.SectionOutsource.SectionName != null && r.SectionOutsource.SectionName.Contains(query.Keyword))
                || (r.Remark != null && r.Remark.Contains(query.Keyword))
                || (r.SectionOutsource.OutsourceSpec != null && r.SectionOutsource.OutsourceSpec.Contains(query.Keyword))
                || (r.SectionOutsource.ManufacturingSpec != null && r.SectionOutsource.ManufacturingSpec.Contains(query.Keyword))
                || (r.SectionOutsource.TagNo != null && r.SectionOutsource.TagNo.Contains(query.Keyword))
                || (r.SectionOutsource.PlantGrade != null && r.SectionOutsource.PlantGrade.Contains(query.Keyword)));
        }

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
            ("createdtime", false) => queryable.OrderBy(r => r.CreatedTime),
            ("createdtime", true) => queryable.OrderByDescending(r => r.CreatedTime),
            ("updatedtime", false) => queryable.OrderBy(r => r.UpdatedTime),
            ("updatedtime", true) => queryable.OrderByDescending(r => r.UpdatedTime),
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
                BatchNo = r.SectionOutsource.ProductionBatch.BatchNo,
                OutsourceVendor = r.SectionOutsource.OutsourceVendor,
                ProcessName = r.SectionOutsource.ProcessName,
                SectionName = r.SectionOutsource.SectionName
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

    public async Task<PagedResult<MaterialReceiveCheckDto>> GetAllMaterialReceiveChecksAsync(QueryParams query)
    {
        var queryable = _context.MaterialReceiveChecks
            .AsNoTracking()
            .Include(m => m.ProductionBatch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            queryable = queryable.Where(m => m.ProductionBatch.BatchNo.Contains(query.Keyword)
                || (m.Checker != null && m.Checker.Contains(query.Keyword))
                || (m.Shift != null && m.Shift.Contains(query.Keyword))
                || (m.Remark != null && m.Remark.Contains(query.Keyword)));
        }

        if (query.ReceiveDateFrom.HasValue)
            queryable = queryable.Where(m => m.ReceiveDate >= query.ReceiveDateFrom.Value);

        if (query.ReceiveDateTo.HasValue)
            queryable = queryable.Where(m => m.ReceiveDate <= query.ReceiveDateTo.Value);

        var totalCount = await queryable.CountAsync();

        queryable = (query.SortBy?.ToLower(), query.IsDescending) switch
        {
            ("batchno", false) => queryable.OrderBy(m => m.ProductionBatch.BatchNo),
            ("batchno", true) => queryable.OrderByDescending(m => m.ProductionBatch.BatchNo),
            ("receivedate", false) => queryable.OrderBy(m => m.ReceiveDate),
            ("receivedate", true) => queryable.OrderByDescending(m => m.ReceiveDate),
            ("receivedquantity", false) => queryable.OrderBy(m => m.ReceivedQuantity ?? 0),
            ("receivedquantity", true) => queryable.OrderByDescending(m => m.ReceivedQuantity ?? 0),
            ("receivedweight", false) => queryable.OrderBy(m => m.ReceivedWeight ?? 0),
            ("receivedweight", true) => queryable.OrderByDescending(m => m.ReceivedWeight ?? 0),
            ("checker", false) => queryable.OrderBy(m => m.Checker ?? ""),
            ("checker", true) => queryable.OrderByDescending(m => m.Checker ?? ""),
            ("createdtime", false) => queryable.OrderBy(m => m.CreatedTime),
            ("createdtime", true) => queryable.OrderByDescending(m => m.CreatedTime),
            ("updatedtime", false) => queryable.OrderBy(m => m.UpdatedTime),
            ("updatedtime", true) => queryable.OrderByDescending(m => m.UpdatedTime),
            ("shift", false) => queryable.OrderBy(m => m.Shift ?? ""),
            ("shift", true) => queryable.OrderByDescending(m => m.Shift ?? ""),
            ("remark", false) => queryable.OrderBy(m => m.Remark ?? ""),
            ("remark", true) => queryable.OrderByDescending(m => m.Remark ?? ""),
            _ => query.IsDescending
                ? queryable.OrderByDescending(m => m.CreatedTime)
                : queryable.OrderBy(m => m.CreatedTime)
        };

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(m => new MaterialReceiveCheckDto
            {
                Id = m.Id,
                ProductionBatchId = m.ProductionBatchId,
                ReceiveDate = m.ReceiveDate,
                ReceivedQuantity = m.ReceivedQuantity,
                ReceivedWeight = m.ReceivedWeight,
                Shift = m.Shift,
                Checker = m.Checker,
                Remark = m.Remark,
                BatchNo = m.ProductionBatch.BatchNo,
                CreatedTime = m.CreatedTime,
                UpdatedTime = m.UpdatedTime
            })
            .ToListAsync();

        return new PagedResult<MaterialReceiveCheckDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<MaterialReceiveCheckDto>> GetAllMaterialReceiveCheckListAsync()
    {
        var query = from rc in _context.MaterialReceiveChecks
                    join b in _context.ProductionBatches on rc.ProductionBatchId equals b.Id
                    orderby rc.Id descending
                    select new MaterialReceiveCheckDto
                    {
                        Id = rc.Id,
                        ProductionBatchId = rc.ProductionBatchId,
                        BatchNo = b.BatchNo,
                        ReceiveDate = rc.ReceiveDate,
                        ReceivedQuantity = rc.ReceivedQuantity,
                        ReceivedWeight = rc.ReceivedWeight,
                        Shift = rc.Shift,
                        Checker = rc.Checker,
                        Remark = rc.Remark,
                        CreatedTime = rc.CreatedTime,
                        UpdatedTime = rc.UpdatedTime
                    };
        return await query.ToListAsync();
    }

    public async Task<List<SectionOutsourceDto>> GetAllSectionOutsourceListAsync()
    {
        var query = from s in _context.SectionOutsources
                    join b in _context.ProductionBatches on s.ProductionBatchId equals b.Id
                    orderby s.Id descending
                    select new SectionOutsourceDto
                    {
                        Id = s.Id,
                        ProductionBatchId = s.ProductionBatchId,
                        ProcessGroupId = s.ProcessGroupId,
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
                        BatchNo = b.BatchNo,
                        CreatedTime = s.CreatedTime,
                        UpdatedTime = s.UpdatedTime
                    };
        return await query.ToListAsync();
    }

    public async Task<List<OutsourceRecoveryDto>> GetAllOutsourceRecoveryListAsync()
    {
        var query = from r in _context.OutsourceRecoveries
                    join s in _context.SectionOutsources on r.SectionOutsourceId equals s.Id
                    join b in _context.ProductionBatches on s.ProductionBatchId equals b.Id
                    orderby r.Id descending
                    select new OutsourceRecoveryDto
                    {
                        Id = r.Id,
                        SectionOutsourceId = r.SectionOutsourceId,
                        RecoveryDate = r.RecoveryDate,
                        RecoveryQuantity = r.RecoveryQuantity,
                        RecoveryWeight = r.RecoveryWeight,
                        UnprocessedQuantity = r.UnprocessedQuantity,
                        UnprocessedWeight = r.UnprocessedWeight,
                        Remark = r.Remark,
                        BatchNo = b.BatchNo,
                        OutsourceVendor = s.OutsourceVendor,
                        ProcessName = s.ProcessName,
                        SectionName = s.SectionName,
                        ManufacturingSpec = s.ManufacturingSpec,
                        OutsourceSpec = s.OutsourceSpec,
                        TagNo = s.TagNo,
                        PlantGrade = s.PlantGrade,
                        SendQuantity = s.SendQuantity,
                        SendWeight = s.SendWeight,
                        CreatedTime = r.CreatedTime
                    };
        return await query.ToListAsync();
    }

    public async Task<List<SectionOutsourceDto>> BatchCreateSectionOutsourcesAsync(List<CreateSectionOutsourceRequest> requests)
    {
        if (requests.Count == 0)
            return new List<SectionOutsourceDto>();

        var results = new List<SectionOutsourceDto>();
        foreach (var request in requests)
        {
            var dto = await CreateSectionOutsourceAsync(request);
            results.Add(dto);
        }
        return results;
    }

    public async Task<List<OutsourceRecoveryDto>> BatchCreateOutsourceRecoveriesAsync(List<CreateOutsourceRecoveryRequest> requests)
    {
        if (requests.Count == 0)
            return new List<OutsourceRecoveryDto>();

        var results = new List<OutsourceRecoveryDto>();
        foreach (var request in requests)
        {
            var dto = await CreateOutsourceRecoveryAsync(request);
            results.Add(dto);
        }
        return results;
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintProductionRecordBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var items = await _context.ProductionRecords
            .AsNoTracking()
            .Include(r => r.ProductionBatch)
            .Where(r => ids.Contains(r.Id))
            .Select(r => new ProductionRecordDto
            {
                Id = r.Id,
                ProductionBatchId = r.ProductionBatchId,
                ProcessGroupId = r.ProcessGroupId,
                ProcessName = r.ProcessName,
                ManufacturingSpec = r.ManufacturingSpec,
                SectionName = r.SectionName,
                SequenceNumber = r.SequenceNumber,
                ExecDate = r.ExecDate,
                EquipmentName = r.EquipmentName,
                Operator = r.Operator,
                Shift = r.Shift,
                Quantity = r.Quantity,
                Weight = r.Weight,
                IsFinished = r.IsFinished,
                CuttingMultiple = r.CuttingMultiple,
                FinishedCutLength = r.FinishedCutLength,
                PostCutQuantity = r.PostCutQuantity,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                Remark = r.Remark,
                BatchNo = r.ProductionBatch.BatchNo,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            })
            .ToListAsync();

        return ProductionRecordPrintHelper.GenerateBatchPdf(items, columns);
    }

    public async Task<byte[]> PrintProductionRecordAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? execDateFrom, DateTime? execDateTo)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "createdtime",
            IsDescending = isDescending,
            ExecDateFrom = execDateFrom,
            ExecDateTo = execDateTo
        };
        var paged = await GetAllProductionRecordsAsync(query);
        return ProductionRecordPrintHelper.GenerateBatchPdf(paged.Items, columns);
    }

    public async Task<byte[]> PrintMaterialCheckBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var items = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Include(m => m.ProductionBatch)
            .Where(m => ids.Contains(m.Id))
            .Select(m => new MaterialReceiveCheckDto
            {
                Id = m.Id,
                ProductionBatchId = m.ProductionBatchId,
                ReceiveDate = m.ReceiveDate,
                ReceivedQuantity = m.ReceivedQuantity,
                ReceivedWeight = m.ReceivedWeight,
                Shift = m.Shift,
                Checker = m.Checker,
                Remark = m.Remark,
                BatchNo = m.ProductionBatch.BatchNo,
                CreatedTime = m.CreatedTime,
                UpdatedTime = m.UpdatedTime
            })
            .ToListAsync();

        return MaterialCheckPrintHelper.GenerateBatchPdf(items, columns);
    }

    public async Task<byte[]> PrintMaterialCheckAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? receiveDateFrom, DateTime? receiveDateTo)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "createdtime",
            IsDescending = isDescending,
            ReceiveDateFrom = receiveDateFrom,
            ReceiveDateTo = receiveDateTo
        };
        var paged = await GetAllMaterialReceiveChecksAsync(query);
        return MaterialCheckPrintHelper.GenerateBatchPdf(paged.Items, columns);
    }

    /// <summary>
    /// 从工序组中提取所有非空工段及其顺序值
    /// </summary>
    private static List<(string SectionName, int Sequence)> GetSectionsFromProcessGroup(ProcessGroup pg)
    {
        var sections = new List<(string SectionName, int Sequence)>();

        if (pg.ColdRollDraw.HasValue) sections.Add(("冷轧拔", pg.ColdRollDraw.Value));
        if (pg.OilPipeCut.HasValue) sections.Add(("油管断", pg.OilPipeCut.Value));
        if (pg.Degrease.HasValue) sections.Add(("去油", pg.Degrease.Value));
        if (pg.Solution.HasValue) sections.Add(("固溶", pg.Solution.Value));
        if (pg.Straighten.HasValue) sections.Add(("矫直", pg.Straighten.Value));
        if (pg.Cut.HasValue) sections.Add(("断切", pg.Cut.Value));
        if (pg.ThicknessMeasure.HasValue) sections.Add(("测壁厚", pg.ThicknessMeasure.Value));
        if (pg.Pickle.HasValue) sections.Add(("酸洗", pg.Pickle.Value));
        if (pg.OuterPolish.HasValue) sections.Add(("外抛光", pg.OuterPolish.Value));
        if (pg.InnerGrinding.HasValue) sections.Add(("内修磨", pg.InnerGrinding.Value));
        if (pg.OuterSpotGrinding.HasValue) sections.Add(("外点磨", pg.OuterSpotGrinding.Value));
        if (pg.Inspection.HasValue) sections.Add(("检验", pg.Inspection.Value));
        if (pg.WeldingHead.HasValue) sections.Add(("打焊头", pg.WeldingHead.Value));
        if (pg.Lubrication.HasValue) sections.Add(("润滑", pg.Lubrication.Value));
        if (pg.Warehouse.HasValue) sections.Add(("入库", pg.Warehouse.Value));

        return sections;
    }
}
