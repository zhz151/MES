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
using MES.Core.Exceptions;
using MES.Core.Helpers;
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
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;
using MES.Core.Constants;
using MES.Services.Extensions;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Batch;

/// <summary>
/// 生产记录服务实现
/// </summary>
public class ProductionRecordService : IProductionRecordService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductionRecordService> _logger;
    private readonly IStandardWorkDayService _standardWorkDayService;
    private readonly IStandardWorkDayDeliveryStateService _deliveryStateService;
    private readonly IConfigParameterService _configService;
    private readonly IQualityProcessTrackingService _qualityProcessTracking;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly IMemoryCache _cache;

    private sealed record SectionOutsourceInfo(
        int Id,
        int ProductionBatchId,
        int ProcessGroupId,
        string SectionName,
        int SequenceNumber,
        string ProcessName,
        string? OutsourceVendor,
        DateTime SendOutDate,
        int RecoveryCount);

    public ProductionRecordService(
        AppDbContext context,
        ILogger<ProductionRecordService> logger,
        IStandardWorkDayService standardWorkDayService,
        IStandardWorkDayDeliveryStateService deliveryStateService,
        IConfigParameterService configService,
        IQualityProcessTrackingService qualityProcessTracking,
        IWorkOrderExecutionService workOrderExecutionService,
        IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _standardWorkDayService = standardWorkDayService;
        _deliveryStateService = deliveryStateService;
        _configService = configService;
        _qualityProcessTracking = qualityProcessTracking;
        _workOrderExecutionService = workOrderExecutionService;
        _cache = cache;
    }

    private async Task TryRefreshQualityProcessTrackingAsync(int productionBatchId)
    {
        try
        {
            await _qualityProcessTracking.RefreshByProductionBatchIdAsync(productionBatchId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "质量过程跟踪刷新失败（不影响主流程）: ProductionBatchId={ProductionBatchId}", productionBatchId);
        }
    }

    private async Task TryRefreshExecutionSummaryAsync(int batchId)
    {
        try
        {
            var workOrderNo = await _context.ProductionBatches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.WorkOrderNo)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(workOrderNo))
                await _workOrderExecutionService.RefreshByWorkOrderNosAsync(new List<string> { workOrderNo });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工单执行摘要刷新失败（不影响主流程）: BatchId={BatchId}", batchId);
        }
    }

    private async Task TryRefreshExecutionSummaryByBatchIdsAsync(IEnumerable<int> batchIds)
    {
        try
        {
            var workOrderNos = await _context.ProductionBatches
                .AsNoTracking()
                .Where(b => batchIds.Contains(b.Id))
                .Select(b => b.WorkOrderNo)
                .Distinct()
                .Where(wo => wo != null)
                .ToListAsync();
            if (workOrderNos.Count > 0)
                await _workOrderExecutionService.RefreshByWorkOrderNosAsync(workOrderNos!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工单执行摘要批量刷新失败（不影响主流程）");
        }
    }

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        var cacheKey = $"ProductionRecordService:ConfigMap:{category}";
        var map = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _configService.GetConfigMapAsync(category);
        });
        return map?.GetValueOrDefault(key, defaultValue) ?? defaultValue;
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
                Shift = EnumHelper.TryParse<ShiftType>(r.Shift),
                Quantity = r.Quantity,
                Weight = r.Weight,
                SolutionTemperature = r.SolutionTemperature,
                SoakTime = r.SoakTime,
                ProductStatus = r.ProductStatus,
                CuttingMultiple = r.CuttingMultiple,
                FinishedCutLength = r.FinishedCutLength,
                PostCutQuantity = r.PostCutQuantity,
                FaceCutCount = r.FaceCutCount,
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

        // 加载该批次所有工序组，用于自动计算产品状态
        var batchProcessGroups = await _context.ProcessGroups
            .Where(pg => pg.ProductionBatchId == batchId)
            .ToListAsync();

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
            Shift = request.Shift?.ToString(),
            Quantity = request.Quantity,
            Weight = request.Weight,
            SolutionTemperature = request.SolutionTemperature,
            SoakTime = request.SoakTime,
            ProductStatus = CalculateProductStatus(request.ProcessName, request.ManufacturingSpec, batch.ManufacturingItem, batchProcessGroups),
            CuttingMultiple = request.CuttingMultiple,
            FinishedCutLength = request.FinishedCutLength,
            PostCutQuantity = request.PostCutQuantity,
            FaceCutCount = request.FaceCutCount,
            TagNo = request.TagNo ?? batch.TagNo,
            PlantGrade = request.PlantGrade ?? batch.PlantGrade,
            Remark = request.Remark,
            DataSource = request.DataSource ?? "MANUAL"
        };

        _context.ProductionRecords.Add(entity);
        await _context.SaveChangesAsync();

        await TryRefreshQualityProcessTrackingAsync(entity.ProductionBatchId);
        await UpdateBatchTrackingFromRecordsAsync(batchId);
        await TryRefreshExecutionSummaryAsync(batchId);

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
            Shift = EnumHelper.TryParse<ShiftType>(entity.Shift),
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            SolutionTemperature = entity.SolutionTemperature,
            SoakTime = entity.SoakTime,
            ProductStatus = entity.ProductStatus,
            CuttingMultiple = entity.CuttingMultiple,
            FinishedCutLength = entity.FinishedCutLength,
            PostCutQuantity = entity.PostCutQuantity,
            FaceCutCount = entity.FaceCutCount,
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

        var sequenceMaxJump = await GetConfigAsync("SequenceJump", "MaxJump", 7);

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

        // 预查询：各批次所有已有的生产记录（用于执行序号跳跃验证）
        var allExistingRecords = await _context.ProductionRecords
            .Where(r => allBatchIds.Contains(r.ProductionBatchId))
            .ToListAsync();
        var recordsByBatch = allExistingRecords
            .GroupBy(r => r.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 预查询：各批次所有已有的执行记录（用于执行序号跳跃验证，涵盖生产记录/委外/过程检验/去油酸洗4类）
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
        var picklingSeqData = await _context.PicklingInRecords
            .Where(pr => allBatchIds.Contains(pr.ProductionBatchId))
            .Select(pr => new { pr.ProductionBatchId, pr.SequenceNumber, Date = pr.InDate })
            .ToListAsync();
        allSequenceData.AddRange(picklingSeqData.Select(pr => (pr.ProductionBatchId, pr.SequenceNumber, pr.Date)));
        var seqDataByBatch = allSequenceData
            .GroupBy(s => s.BatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 预查询：各批次已存在的冷轧拔记录（包含生产记录 + 委外记录）
        var existingColdRollDraw = await _context.ProductionRecords
            .Where(r => allBatchIds.Contains(r.ProductionBatchId) && r.SectionName == SectionDefs.ColdRollDraw)
            .Select(r => new { r.ProductionBatchId, r.ProcessGroupId })
            .ToListAsync();
        var outsourcedColdRollDraw = await _context.SectionOutsources
            .Where(o => allBatchIds.Contains(o.ProductionBatchId) && o.SectionName == SectionDefs.ColdRollDraw)
            .Select(o => new { o.ProductionBatchId, o.ProcessGroupId })
            .ToListAsync();
        var coldRollDrawExists = new HashSet<(int BatchId, int PgId)>(
            existingColdRollDraw.Concat(outsourcedColdRollDraw).Select(r => (r.ProductionBatchId, r.ProcessGroupId)));

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

            // 3) 执行序号跳跃限制：以每条记录的 ExecDate 为准，对比该批次在此日期前已执行的最大序号（涵盖生产记录/委外/过程检验/去油酸洗4类），不能 > +7
            if (request.SequenceNumber > 0)
            {
                var batchSeqData = seqDataByBatch.GetValueOrDefault(batchId, new List<(int BatchId, int Seq, DateTime Date)>());
                var prevMax = batchSeqData
                    .Where(s => s.Date.Date < request.ExecDate.Date)
                    .Select(s => (int?)s.Seq)
                    .Max() ?? 0;
                var maxAllowed = prevMax + sequenceMaxJump;
                if (request.SequenceNumber > maxAllowed)
                    requestErrors.Add($"第{i + 1}行：执行序号({request.SequenceNumber})超过该日期前已执行最大值({prevMax})+7={maxAllowed}");
            }
        }

        // 收集本次提交中的冷轧拔记录（用于冷轧/冷拔验证）
        var pendingColdRollDraw = new HashSet<(int BatchId, int PgId)>();
        foreach (var request in requests)
        {
            if (request.SectionName == SectionDefs.ColdRollDraw)
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
            if (request.SectionName == SectionDefs.ColdRollDraw)
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
            if (pg == null || !ProcessNames.IsColdRollOrDraw(pg.ProcessName))
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
                if (request.SectionName == SectionDefs.Inspection)
                {
                    requestErrors.Add($"第{i + 1}行：工段「检验」已由过程检验模块管理，不允许在生产记录中使用");
                    continue;
                }
                var sections = GetSectionsFromProcessGroup(pg);
                if (!sections.Any(s => s.SectionName == request.SectionName))
                    requestErrors.Add($"第{i + 1}行：工段「{request.SectionName}」不存在于工序组「{pg.ProcessName}」中，无法提交");
            }
        }

        // 预查询：各批次各工序组的冷轧拔总重量（用于冷轧拔总加工重量验证，含自产 + 委外发出）
        var coldRollDrawWeightByKey = allExistingRecords
            .Where(r => r.SectionName == SectionDefs.ColdRollDraw && r.Weight.HasValue)
            .GroupBy(r => new { r.ProductionBatchId, r.ProcessGroupId })
            .ToDictionary(g => (g.Key.ProductionBatchId, g.Key.ProcessGroupId), g => g.Sum(r => r.Weight!.Value));
        var outsourcedCrWeights = await _context.SectionOutsources
            .Where(o => allBatchIds.Contains(o.ProductionBatchId) && o.SectionName == SectionDefs.ColdRollDraw && o.SendWeight.HasValue)
            .GroupBy(o => new { o.ProductionBatchId, o.ProcessGroupId })
            .ToListAsync();
        foreach (var grp in outsourcedCrWeights)
        {
            var key = (grp.Key.ProductionBatchId, grp.Key.ProcessGroupId);
            var w = grp.Sum(o => o.SendWeight!.Value);
            if (coldRollDrawWeightByKey.ContainsKey(key))
                coldRollDrawWeightByKey[key] += w;
            else
                coldRollDrawWeightByKey[key] = w;
        }

        var simpleDuplicateSections = new HashSet<string>
        {
            SectionDefs.OilPipeCut, SectionDefs.Degrease, SectionDefs.Solution, SectionDefs.Straighten,
            SectionDefs.ThicknessMeasure, SectionDefs.Pickle, SectionDefs.OuterPolish,
            SectionDefs.InnerGrinding, SectionDefs.OuterSpotGrinding, SectionDefs.WeldingHead, SectionDefs.Lubrication
        };

        // 5) 重复记录校验（pendingKeys 模式：同时防范 DB 重复和行间重复）
        var pendingSimpleKeys = new HashSet<(int batchId, int pgId, string section)>();
        var pendingColdRollDrawKeys = new HashSet<(int batchId, int pgId, DateTime date, string equipment, string op)>();
        var pendingCutKeys = new HashSet<(int batchId, int pgId, decimal? cutLength)>();
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var batch = batchLookup[request.BatchNo];
            var batchId = batch.Id;

            // 解析 ProcessGroupId
            var pgId = request.ProcessGroupId;
            if (pgId == null || pgId == 0)
            {
                var matchedPg = pgByBatch.GetValueOrDefault(batchId)?
                    .FirstOrDefault(pg => pg.ProcessName == request.ProcessName && pg.ManufacturingSpec == request.ManufacturingSpec);
                pgId = matchedPg?.Id;
            }
            if (pgId == null || pgId == 0)
                continue;

            var batchRecords = recordsByBatch.GetValueOrDefault(batchId, new List<ProductionRecord>());

            if (simpleDuplicateSections.Contains(request.SectionName))
            {
                // 规则(1)：同批次+同工序组+同工段 → 重复
                var key = (batchId, pgId.Value, request.SectionName);
                var dup = batchRecords.Any(r =>
                    r.ProcessGroupId == pgId.Value && r.SectionName == request.SectionName)
                    || pendingSimpleKeys.Contains(key);
                if (dup)
                    requestErrors.Add($"第{i + 1}行：工段「{request.SectionName}」在该批次该工序组中已存在记录，不能重复创建");
                else
                    pendingSimpleKeys.Add(key);
            }
            else if (request.SectionName == SectionDefs.ColdRollDraw)
            {
                // 规则(2)：同批次+同工序组+同工段+同执行日期+同设备名称+同操作人 → 重复
                var key = (batchId, pgId.Value, request.ExecDate.Date, request.EquipmentName ?? "", request.Operator ?? "");
                var dup = batchRecords.Any(r =>
                    r.ProcessGroupId == pgId.Value &&
                    r.SectionName == SectionDefs.ColdRollDraw &&
                    r.ExecDate.Date == request.ExecDate.Date &&
                    r.EquipmentName == request.EquipmentName &&
                    r.Operator == request.Operator)
                    || pendingColdRollDrawKeys.Contains(key);
                if (dup)
                    requestErrors.Add($"第{i + 1}行：冷轧拔在该日期/设备/操作人下已存在记录，不能重复创建");
                else
                    pendingColdRollDrawKeys.Add(key);

                // 附加：冷轧拔总加工重量不能大于现有效原料重量
                var existingWeight = coldRollDrawWeightByKey.GetValueOrDefault((batchId, pgId.Value), 0m);
                var totalWeight = existingWeight + (request.Weight ?? 0m);
                if (totalWeight > (batch.CurrentValidWeight ?? batch.InputWeight))
                    requestErrors.Add($"第{i + 1}行：冷轧拔总加工重量({totalWeight})不能大于有效原料重量({batch.CurrentValidWeight ?? batch.InputWeight})");
            }
            else if (request.SectionName == SectionDefs.Cut)
            {
                // 规则(3)：同批次+同工序组+同工段+同成品长度 → 重复
                var key = (batchId, pgId.Value, request.FinishedCutLength);
                var dup = batchRecords.Any(r =>
                    r.ProcessGroupId == pgId.Value &&
                    r.SectionName == SectionDefs.Cut &&
                    r.FinishedCutLength == request.FinishedCutLength)
                    || pendingCutKeys.Contains(key);
                if (dup)
                    requestErrors.Add($"第{i + 1}行：断切在该批次该工序组中已存在相同成品长度的记录，不能重复创建");
                else
                    pendingCutKeys.Add(key);
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
                Shift = request.Shift?.ToString(),
                Quantity = request.Quantity,
                Weight = request.Weight,
                SolutionTemperature = request.SolutionTemperature,
                SoakTime = request.SoakTime,
                ProductStatus = CalculateProductStatus(request.ProcessName, request.ManufacturingSpec, batch.ManufacturingItem, pgByBatch.GetValueOrDefault(batchId) ?? new()),
                CuttingMultiple = request.CuttingMultiple,
                FinishedCutLength = request.FinishedCutLength,
                PostCutQuantity = request.PostCutQuantity,
                FaceCutCount = request.FaceCutCount,
                TagNo = request.TagNo ?? batch.TagNo,
                PlantGrade = request.PlantGrade ?? batch.PlantGrade,
                Remark = request.Remark,
                DataSource = "MANUAL"
            });
        }

        _context.ProductionRecords.AddRange(entities);
        await _context.SaveChangesAsync();

        // 批量刷新所有涉及批次的跟踪字段
        var distinctBatchIds = entities.Select(e => e.ProductionBatchId).Distinct().ToList();
        foreach (var id in distinctBatchIds)
            await TryRefreshQualityProcessTrackingAsync(id);
        await BatchUpdateTrackingFromRecordsAsync(distinctBatchIds);
        await TryRefreshExecutionSummaryByBatchIdsAsync(distinctBatchIds);

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
            Shift = EnumHelper.TryParse<ShiftType>(e.Shift),
            Quantity = e.Quantity,
            Weight = e.Weight,
            SolutionTemperature = e.SolutionTemperature,
            SoakTime = e.SoakTime,
            ProductStatus = e.ProductStatus,
            CuttingMultiple = e.CuttingMultiple,
            FinishedCutLength = e.FinishedCutLength,
            PostCutQuantity = e.PostCutQuantity,
            FaceCutCount = e.FaceCutCount,
            TagNo = e.TagNo,
            PlantGrade = e.PlantGrade,
            Remark = e.Remark
        }).ToList();
    }

    public async Task<ProductionRecordDto> UpdateProductionRecordAsync(int id, UpdateProductionRecordRequest request)
    {
        var entity = await _context.ProductionRecords.FindAsync(id)
            ?? throw new BusinessException("生产记录不存在");

        // 加载批次及其工序组，用于重新计算产品状态
        var batch = await _context.ProductionBatches.FindAsync(entity.ProductionBatchId);
        var batchProcessGroups = await _context.ProcessGroups
            .Where(pg => pg.ProductionBatchId == entity.ProductionBatchId)
            .ToListAsync();

        entity.ExecDate = request.ExecDate;
        entity.EquipmentName = request.EquipmentName ?? entity.EquipmentName;
        entity.Operator = request.Operator ?? entity.Operator;
        entity.Shift = request.Shift?.ToString() ?? entity.Shift;
        entity.Quantity = request.Quantity ?? entity.Quantity;

        // 编辑重量时校验：不能超过批次现有效原料重量
        if (request.Weight.HasValue && request.Weight > 0 && batch != null && request.Weight > (batch.CurrentValidWeight ?? batch.InputWeight))
            throw new BusinessException($"加工重量({request.Weight})不能大于有效原料重量({batch.CurrentValidWeight ?? batch.InputWeight})");
        entity.Weight = request.Weight ?? entity.Weight;
        entity.SolutionTemperature = request.SolutionTemperature ?? entity.SolutionTemperature;
        entity.SoakTime = request.SoakTime ?? entity.SoakTime;
        entity.ProductStatus = batch != null
            ? CalculateProductStatus(entity.ProcessName, entity.ManufacturingSpec, batch.ManufacturingItem, batchProcessGroups)
            : entity.ProductStatus;
        entity.CuttingMultiple = request.CuttingMultiple ?? entity.CuttingMultiple;
        entity.FinishedCutLength = request.FinishedCutLength ?? entity.FinishedCutLength;
        entity.PostCutQuantity = request.PostCutQuantity ?? entity.PostCutQuantity;
        entity.FaceCutCount = request.FaceCutCount ?? entity.FaceCutCount;
        entity.TagNo = request.TagNo ?? entity.TagNo;
        entity.PlantGrade = request.PlantGrade ?? entity.PlantGrade;
        entity.Remark = request.Remark ?? entity.Remark;

        _context.ProductionRecords.Update(entity);
        await _context.SaveChangesAsync();

        await TryRefreshQualityProcessTrackingAsync(entity.ProductionBatchId);
        await UpdateBatchTrackingFromRecordsAsync(entity.ProductionBatchId);
        await TryRefreshExecutionSummaryAsync(entity.ProductionBatchId);

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
            Shift = EnumHelper.TryParse<ShiftType>(entity.Shift),
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            SolutionTemperature = entity.SolutionTemperature,
            SoakTime = entity.SoakTime,
            ProductStatus = entity.ProductStatus,
            CuttingMultiple = entity.CuttingMultiple,
            FinishedCutLength = entity.FinishedCutLength,
            PostCutQuantity = entity.PostCutQuantity,
            FaceCutCount = entity.FaceCutCount,
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

        await TryRefreshQualityProcessTrackingAsync(batchId);
        await UpdateBatchTrackingFromRecordsAsync(batchId);
        await TryRefreshExecutionSummaryAsync(batchId);
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
                Status = s.Status,
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

    private async Task UpdateOutsourceStatusAsync(SectionOutsource outsource)
    {
        var outsourceRecoveryRatio = await GetConfigAsync("WarehouseThreshold", "OutsourceRecoveryRatio", 0.99m);

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
            ? outsource.SendWeight.Value * outsourceRecoveryRatio
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

    // ========== 批次跟踪字段刷新 ==========

    public async Task RefreshBatchTrackingFieldsAsync(int batchId)
    {
        await UpdateBatchTrackingFromRecordsAsync(batchId);
        await TryRefreshExecutionSummaryAsync(batchId);
    }

    public async Task BatchUpdateBatchTrackingAsync(ICollection<int> batchIds)
    {
        await BatchUpdateTrackingFromRecordsAsync(batchIds);
        await TryRefreshExecutionSummaryByBatchIdsAsync(batchIds);
    }

    /// <summary>
    /// 删除生产记录中所有"去油"和"酸洗"的旧数据（已被 PicklingInRecord 替代）
    /// </summary>
    public async Task<int> CleanupDegreasePickleRecordsAsync()
    {
        var records = await _context.ProductionRecords
            .Where(r => r.SectionName == SectionDefs.Degrease || r.SectionName == SectionDefs.Pickle)
            .ToListAsync();
        var count = records.Count;
        if (count > 0)
        {
            _context.ProductionRecords.RemoveRange(records);
            await _context.SaveChangesAsync();
            _logger.LogInformation("已删除 {Count} 条去油/酸洗生产记录", count);
        }
        return count;
    }

    public async Task<int> RefreshAllBatchTrackingAsync()
    {
        var batchIds = await _context.ProductionBatches
            .Where(b => !b.IsForceCompleted)
            .Select(b => b.Id)
            .ToListAsync();
        await BatchUpdateTrackingFromRecordsAsync(batchIds);
        await TryRefreshExecutionSummaryByBatchIdsAsync(batchIds);
        return batchIds.Count;
    }

    /// <summary>
    /// 回填所有批次的理论成品量（含强制完成批次）
    /// 仅计算 TheoreticalOutputQty/Weight/UnitWeight，不修改其他跟踪字段
    /// </summary>
    public async Task<int> BackfillTheoreticalOutputAsync()
    {
        var batchIds = await _context.ProductionBatches
            .Select(b => b.Id)
            .ToListAsync();
        if (batchIds.Count == 0) return 0;

        await BatchUpdateTrackingFromRecordsAsync(batchIds);
        return batchIds.Count;
    }

    // ========== 批次跟踪可视化 ==========

    public async Task<BatchTrackingVisualDto> GetTrackingVisualAsync(int batchId)
    {
        var groupDiscountRate = await GetConfigAsync("ProcessingDiscount", "GroupDiscountRate", 0.025m);

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

        // 3c. 加载所有检验到料
        var materialChecks = await _context.MaterialReceiveChecks
            .Include(m => m.ProductionBatch)
            .Where(m => m.ProductionBatchId == batchId)
            .ToListAsync();
        var materialReceiveCheck = materialChecks.OrderByDescending(m => m.ReceiveDate).FirstOrDefault();
        // 建有到料的工序组 ID → 到料日期 映射（批次+工序组+检验 三字段匹配用）
        var materialCheckPgIds = materialChecks
            .Where(m => m.ProcessGroupId > 0)
            .Select(m => m.ProcessGroupId)
            .ToHashSet();
        var materialCheckDateByPgId = materialChecks
            .Where(m => m.ProcessGroupId > 0)
            .GroupBy(m => m.ProcessGroupId)
            .ToDictionary(g => g.Key, g => g.Max(m => (DateTime?)m.ReceiveDate));

        // 3d. 加载所有去油/酸洗入缸记录
        var allPicklingInRecords = await _context.PicklingInRecords
            .Where(p => p.ProductionBatchId == batchId)
            .OrderBy(p => p.SequenceNumber)
            .ThenBy(p => p.InDate)
            .ToListAsync();

        // 3e. 加载仓库入库记录（按批次号+物料类型匹配，排除次品/报废品入库）
        var inventoryBatches = await _context.InventoryBatches
            .Include(ib => ib.Warehouse)
            .Where(ib => ib.ProductionBatchNo == batch.BatchNo && ib.MaterialType == batch.ManufacturingItem)
            .OrderByDescending(ib => ib.InboundDate)
            .ToListAsync();

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

        // 4c. 去油/酸洗入缸记录查询字典
        var picklingByKey = allPicklingInRecords
            .GroupBy(p => (p.ProcessGroupId, p.SectionName))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.InDate).First());

        // 5. 构建所有工段的完成状态
        var maxRecordSeq = allRecords.Count > 0 ? allRecords.Max(r => r.SequenceNumber) : -1;
        var maxOutsourceSeq = allOutsources.Count > 0 ? allOutsources.Max(s => s.SequenceNumber) : -1;
        var maxInspectionSeq = allInspections.Count > 0 ? allInspections.Max(p => p.SequenceNumber) : -1;
        var maxPicklingSeq = allPicklingInRecords.Count > 0 ? allPicklingInRecords.Max(p => p.SequenceNumber) : -1;

        // 检验到料：直接按 ProcessGroupId 集合匹配（批次+工序组+检验 三字段匹配）
        int materialCheckSeq = materialChecks.Count > 0
            ? materialChecks.Max(m => m.SequenceNumber)
            : -1;

        var currentMaxSeq = Math.Max(Math.Max(Math.Max(Math.Max(maxRecordSeq, maxOutsourceSeq), maxInspectionSeq), materialCheckSeq), maxPicklingSeq);

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
                var hasPickling = picklingByKey.TryGetValue(key, out var pickling);
                var hasInspection = inspectionByKey.TryGetValue(key, out var insp);

                // 检验到料匹配：工序组有成检到料记录 → 该组"检验"工段即为完成
                var hasMaterialCheck = sectionName == SectionDefs.Inspection
                    && materialCheckPgIds.Contains(pg.Id);

                // 仓库入库匹配：该工段为"入库"且有库存批次记录
                var hasWarehouse = sectionName == SectionDefs.Warehouse && inventoryBatches.Count > 0;

                // 确定状态
                SectionStatus sectionStatus;
                if (hasRecord)
                    sectionStatus = SectionStatus.Completed;
                else if (hasPickling)
                    sectionStatus = SectionStatus.Completed;
                else if (hasInspection)
                    sectionStatus = SectionStatus.Completed;
                else if (hasMaterialCheck)
                    sectionStatus = SectionStatus.Completed;
                else if (hasWarehouse)
                    sectionStatus = SectionStatus.Completed;
                else if (hasOutsource && outsource!.Status == SectionOutsourceStatus.Recovered)
                    sectionStatus = SectionStatus.Completed;
                else if (hasOutsource)
                    sectionStatus = SectionStatus.Outsource;
                else if (seq == currentMaxSeq + 1)
                    sectionStatus = SectionStatus.Next;
                else if (seq <= currentMaxSeq)
                    sectionStatus = SectionStatus.Completed; // 跨工序组时，之前组的未记录工段视作已完成
                else
                    sectionStatus = SectionStatus.Pending;

                // 修正：如果有记录则为 Completed
                if (hasRecord)
                    sectionStatus = SectionStatus.Completed;

                if (sectionStatus == SectionStatus.Completed && sectionName != SectionDefs.Warehouse) groupCompleted++;

                // 委外进度
                decimal? outsourceProgress = null;
                if (hasOutsource && outsource!.SendWeight > 0)
                {
                    outsourceProgress = (decimal)outsource.TotalRecoveredWeight / outsource.SendWeight.Value * 100;
                }

                // 预计算仓库入库的汇总数量和重量
                int? warehouseQty = hasWarehouse ? inventoryBatches.Sum(ib => ib.InitialQuantity) : (int?)null;
                decimal? warehouseWt = hasWarehouse ? inventoryBatches.Sum(ib => ib.InitialWeight) : (decimal?)null;

                // 按优先级计算 ExecDate/设备/数量/重量
                var finalQty = record?.Quantity ?? pickling?.Quantity;
                if (finalQty == null && warehouseQty.HasValue) finalQty = warehouseQty.Value;
                var finalWt = record?.Weight ?? pickling?.Weight;
                if (finalWt == null && warehouseWt.HasValue) finalWt = warehouseWt.Value;

                var sectionDto = new SectionVisualDto
                {
                    SectionName = sectionName,
                    SequenceNumber = seq,
                    ProcessGroupId = pg.Id,
                    Status = sectionStatus,
                    ExecDate = record?.ExecDate
                        ?? (hasInspection ? insp!.InspectionDate : (DateTime?)null)
                        ?? (hasPickling ? pickling!.InDate : (DateTime?)null)
                        ?? (hasOutsource ? outsource!.SendOutDate : (DateTime?)null)
                        ?? (sectionName == SectionDefs.Inspection && materialCheckDateByPgId.TryGetValue(pg.Id, out var mcDate)
                            ? mcDate : (DateTime?)null)
                        ?? (hasWarehouse ? inventoryBatches[0].InboundDate : (DateTime?)null),
                    EquipmentName = record?.EquipmentName ?? pickling?.EquipmentName ?? (hasInspection ? insp!.EquipmentName : null)
                        ?? (hasWarehouse ? string.Join("、", inventoryBatches.Where(ib => ib.Warehouse != null).Select(ib => ib.Warehouse!.Name).Distinct()) : null),
                    Quantity = finalQty,
                    Weight = finalWt,
                    Operator = record?.Operator ?? pickling?.Operator ?? (hasInspection ? insp!.Inspector : null),
                    OutsourceVendor = hasOutsource && outsource!.Status != SectionOutsourceStatus.Recovered ? outsource.OutsourceVendor : null,
                    OutsourceProgress = hasOutsource
                        ? (outsource!.SendWeight > 0
                            ? (decimal)outsource.TotalRecoveredWeight / outsource.SendWeight.Value * 100
                            : null)
                        : null,
                    WarehouseDetails = hasWarehouse
                        ? inventoryBatches
                            .Where(ib => ib.Warehouse != null)
                            .Select(ib => new WarehouseDetailDto
                            {
                                WarehouseName = ib.Warehouse!.Name,
                                Quantity = ib.InitialQuantity,
                                Weight = ib.InitialWeight,
                                InboundDate = ib.InboundDate
                            })
                            .ToList()
                        : null
                };

                groupSectionDtos.Add(sectionDto);
                allSectionDtos.Add(sectionDto);
            }

            var warehouseInGroup = sections.Count(s => s.SectionName == SectionDefs.Warehouse);
            processGroupDtos.Add(new ProcessGroupVisualDto
            {
                Id = pg.Id,
                SequenceNumber = pg.SequenceNumber,
                ProcessName = pg.ProcessName,
                ManufacturingSpec = pg.ManufacturingSpec,
                TotalSections = sections.Count - warehouseInGroup,
                CompletedSections = groupCompleted,
                Sections = groupSectionDtos
            });

            totalSectionCount += sections.Count - warehouseInGroup;
            completedSectionCount += groupCompleted;
        }

        // 5b. 检验到料日期兜底：若无其他日期，则取 MaterialReceiveCheck.ReceiveDate 作为最后工段日期
        // 注意：入库工段必须跳过，避免无真实仓库入库时错误显示检验日期
        if (materialReceiveCheck != null && allSectionDtos.Count > 0)
        {
            var lastSection = allSectionDtos.MaxBy(s => s.SequenceNumber);
            if (lastSection != null && !lastSection.ExecDate.HasValue
                && lastSection.SectionName != SectionDefs.Warehouse)
                lastSection.ExecDate = materialReceiveCheck.ReceiveDate;
        }

        // 6. 计算投料量与目标量（使用现有效原料数据）
        int? inputQty = batch.CurrentValidQty;
        int? inputWt = batch.CurrentValidWeight.HasValue ? (int?)batch.CurrentValidWeight.Value : null;

        // 目标支数 = 投料支数 × 制成倍数
        int? targetQty = batch.ProductionRatio > 0 && inputQty.HasValue
            ? inputQty.Value * batch.ProductionRatio
            : null;

        // 目标重量 = 投料重量 × (1 - 有效工序组数 × 0.025)
        // "在制修检"和"附加成检"不计入有效工序组
        var effectiveGroupCount = batch.ProcessGroups
            .Count(pg => pg.ProcessName != ProcessNames.InProcessRepair
                && pg.ProcessName != ProcessNames.AdditionalFinalInspection
                && GetSectionsFromProcessGroup(pg).Count > 0);
        var discount = 1.0m - effectiveGroupCount * groupDiscountRate;
        if (discount < 0) discount = 0;
        int? targetWt = inputWt.HasValue
            ? (int?)(inputWt.Value * discount)
            : null;

        // 7. 组装返回
        var maxBySeq = allSectionDtos
            .Where(s => s.Status == SectionStatus.InProgress || s.Status == SectionStatus.Outsource || s.Status == SectionStatus.Completed)
            .OrderByDescending(s => s.SequenceNumber)
            .FirstOrDefault();

        var nextBySeq = allSectionDtos
            .Where(s => s.Status == SectionStatus.Next)
            .OrderBy(s => s.SequenceNumber)
            .FirstOrDefault();

        // 当前工序：取最后执行的工序名称（用作回退）
        var currentPgName = maxBySeq != null
            ? batch.ProcessGroups
                .Where(pg => pg.Id == maxBySeq.ProcessGroupId)
                .Select(pg => pg.ProcessName)
                .FirstOrDefault()
            : null;
        // 下一工序：基于下一工段所在工序组的工序名称（与工段关联）
        var nextProcessName = nextBySeq != null
            ? batch.ProcessGroups
                .Where(pg => pg.Id == nextBySeq.ProcessGroupId)
                .Select(pg => pg.ProcessName)
                .FirstOrDefault()
            // 无下一工段时回退为当前工序的下一个工序
            : currentPgName != null
                ? batch.ProcessGroups
                    .OrderBy(pg => pg.SequenceNumber)
                    .SkipWhile(pg => pg.ProcessName != currentPgName)
                    .Skip(1)
                    .Select(pg => pg.ProcessName)
                    .FirstOrDefault()
                // 未开始生产 → 首个工序
                : batch.ProcessGroups
                    .OrderBy(pg => pg.SequenceNumber)
                    .Select(pg => pg.ProcessName)
                    .FirstOrDefault();

        return new BatchTrackingVisualDto
        {
            BatchId = batch.Id,
            BatchNo = batch.BatchNo,
            TotalSectionCount = totalSectionCount,
            CompletedSectionCount = completedSectionCount,

            CurrentGroupName = currentPgName,
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
            NextProcess = nextProcessName,
            CurrentWarehouseDetails = maxBySeq?.WarehouseDetails,

            InputQuantity = inputQty,
            InputWeight = inputWt,
            TargetQuantity = targetQty,
            TargetWeight = targetWt,

            ProcessGroups = processGroupDtos
        };
    }

    private async Task UpdateBatchTrackingFromRecordsAsync(int batchId)
    {
        var coldRollCompleteRatio = await GetConfigAsync("ProductionThreshold", "ColdRollCompleteRatio", 0.95m);
        var validInputUpper = await GetConfigAsync("ProductionThreshold", "ValidInputUpper", 1.03m);
        var validInputLower = await GetConfigAsync("ProductionThreshold", "ValidInputLower", 0.97m);
        var groupDiscountRate = await GetConfigAsync("ProcessingDiscount", "GroupDiscountRate", 0.025m);

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

            // 检验到料 + 仓库入库：截止执行日 = 到料日期，状态置为完成
            // 批量查出所有到料记录，取 Inspection 值最高的工序组用于跟踪
            var materialChecks = await _context.MaterialReceiveChecks
                .Include(m => m.ProductionBatch)
                .Where(m => m.ProductionBatchId == batchId)
                .ToListAsync();
            bool hasMaterialCheck = materialChecks.Count > 0;

            // 3e. 加载仓库入库记录（按批次号+物料类型匹配，排除次品/报废品入库）
            var inventoryBatches = await _context.InventoryBatches
                .Include(ib => ib.Warehouse)
                .Where(ib => ib.ProductionBatchNo == batch.BatchNo && ib.MaterialType == batch.ManufacturingItem)
                .OrderByDescending(ib => ib.InboundDate)
                .ToListAsync();

            bool hasWarehouse = inventoryBatches.Count > 0;
            if (hasMaterialCheck)
            {
                batch.CurrentExecDate = materialChecks.Max(m => (DateTime?)m.ReceiveDate);
                if (hasWarehouse)
                {
                    // 同时有成检到料和仓库入库 → 完成
                    if (batch.Status != BatchStatus.Completed)
                        batch.Status = BatchStatus.Completed;
                }
                else
                {
                    // 只有成检到料 → 成检
                    if (batch.Status != BatchStatus.InFinalInspection)
                        batch.Status = BatchStatus.InFinalInspection;
                }
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

            // 收集该批次的所有去油/酸洗入缸记录
            var picklingInRecords = await _context.PicklingInRecords
                .Where(p => p.ProductionBatchId == batchId)
                .OrderBy(p => p.SequenceNumber)
                .ThenBy(p => p.InDate)
                .ToListAsync();

            var hasRecords = productionRecords.Count > 0 || sectionOutsources.Count > 0 || processInspections.Count > 0 || picklingInRecords.Count > 0;

            // ====== 1. 状态 ======
            // 挂起/强制完成状态不自动覆盖；检验到料已完成的批次保持 Completed
            if (batch.Status != BatchStatus.Suspended && !hasMaterialCheck)
                batch.Status = hasRecords ? BatchStatus.InProgress : BatchStatus.None;

            // ====== 3-5. 当前工段/工序/设备/委外/规格 + 截止执行日 ======
            // 构建 ProcessGroup 查表（Id -> ManufacturingSpec）
            var pgSpecLookup = batch.ProcessGroups
                .ToDictionary(pg => pg.Id, pg => pg.ManufacturingSpec!);

            // 检验到料：建工序组ID集合 + 取"检验"工段的最大工段序号（用于 overallMaxSeq 比较）
            var materialCheckPgIds = materialChecks
                .Where(m => m.ProcessGroupId > 0)
                .Select(m => m.ProcessGroupId)
                .ToHashSet();
            int materialCheckSeq = -1;
            ProcessGroup? materialCheckPg = null;
            if (materialCheckPgIds.Count > 0)
            {
                foreach (var pg in batch.ProcessGroups)
                {
                    if (!materialCheckPgIds.Contains(pg.Id)) continue;
                    var sections = GetSectionsFromProcessGroup(pg);
                    var inspSection = sections.FirstOrDefault(s => s.SectionName == SectionDefs.Inspection);
                    if (inspSection.Sequence > materialCheckSeq)
                    {
                        materialCheckSeq = inspSection.Sequence;
                        materialCheckPg = pg;
                    }
                }
            }

            // 转换委外列表为命名类型
            var outsourceInfos = sectionOutsources.Select(s => new SectionOutsourceInfo(
                s.Id, 0, s.ProcessGroupId, s.SectionName, s.SequenceNumber,
                s.ProcessName, s.OutsourceVendor, s.SendOutDate, s.RecoveryCount
            )).ToList();

            // 公共跟踪计算（除有效投料疑问外）
            var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(batch.PlantGrade);
            var dsExtraDaysMap = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
            ComputeBatchTrackingCore(batch, pgSpecLookup, productionRecords, outsourceInfos,
                processInspections, picklingInRecords, hasMaterialCheck,
                materialChecks.OrderByDescending(m => m.SequenceNumber).FirstOrDefault(), materialCheckSeq, materialCheckPg, coldRollCompleteRatio, dayMap, dsExtraDaysMap);

            // ====== 仓库入库覆盖：入库后当前工段为"入库"，无下一工段 ======
            if (hasWarehouse)
            {
                var latestInbound = inventoryBatches[0]; // 已按 InboundDate 降序
                batch.CurrentSectionName = SectionDefs.Warehouse; // "入库"
                batch.CurrentExecDate = latestInbound.InboundDate;
                batch.NextSectionName = "-";
                batch.NextProcess = null;
                batch.CorrespondingSpec = null;
                batch.CurrentGroupName = null;
                batch.CurrentEquipmentName = null;
                batch.CurrentSpec = null;
                batch.CurrentOutsource = null;
                batch.CurrentSectionCompleted = null;
                batch.RemainingWorkDays = 0;
            }

            // ====== 8. 有效投料疑问（与批量模式一致，基于投料对比）=====
            // 对照现有效原料支数与投料支数，相差超过阈值 → 疑问
            batch.ValidInputQuestion = false;
            if (batch.InputQuantity.HasValue && batch.InputQuantity > 0 && batch.CurrentValidQty.HasValue)
            {
                var ratio = (decimal)batch.CurrentValidQty.Value / batch.InputQuantity.Value;
                batch.ValidInputQuestion = ratio < validInputLower || ratio > validInputUpper;
            }

            // ====== 9. 理论成品量计算 ======
            ComputeTheoreticalOutput(batch, groupDiscountRate);

            _context.ProductionBatches.Update(batch);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新批次跟踪字段失败 (BatchId={BatchId})", batchId);
            throw;
        }
    }

    private static void ComputeTheoreticalOutput(ProductionBatch batch, decimal groupDiscountRate)
    {
        // 理论成品支 = CurrentValidQty × ProductionRatio
        if (batch.ProductionRatio > 0 && batch.CurrentValidQty.HasValue)
            batch.TheoreticalOutputQty = batch.CurrentValidQty.Value * batch.ProductionRatio;
        else
            batch.TheoreticalOutputQty = null;

        // 理论成品重 = CurrentValidWeight × (1 - 有效工序组数 × 折扣率)
        int? targetWt = null;
        if (batch.CurrentValidWeight.HasValue)
        {
            var effectiveGroupCount = batch.ProcessGroups?
                .Count(pg => pg.ProcessName != ProcessNames.InProcessRepair
                    && pg.ProcessName != ProcessNames.AdditionalFinalInspection
                    && pg.GetNonEmptySections().Count > 0) ?? 0;
            var discount = 1.0m - effectiveGroupCount * groupDiscountRate;
            if (discount < 0) discount = 0;
            targetWt = (int?)(batch.CurrentValidWeight.Value * discount);
        }
        batch.TheoreticalOutputWeight = targetWt;

        // 理论单支重 = 理论成品重 / 理论成品支（1位小数）
        if (batch.TheoreticalOutputQty.HasValue && batch.TheoreticalOutputQty.Value > 0
            && batch.TheoreticalOutputWeight.HasValue)
            batch.TheoreticalUnitWeight = Math.Round(
                (decimal)batch.TheoreticalOutputWeight.Value / batch.TheoreticalOutputQty.Value, 1);
        else
            batch.TheoreticalUnitWeight = null;
    }

    /// <summary>
    /// 批量刷新多个批次的跟踪字段
    /// 一次查询所有数据，内存分组计算，一次SaveChanges
    /// </summary>
    private async Task BatchUpdateTrackingFromRecordsAsync(ICollection<int> batchIds)
    {
        if (batchIds.Count == 0) return;

        var coldRollCompleteRatio = await GetConfigAsync("ProductionThreshold", "ColdRollCompleteRatio", 0.95m);
        var validInputUpper = await GetConfigAsync("ProductionThreshold", "ValidInputUpper", 1.03m);
        var validInputLower = await GetConfigAsync("ProductionThreshold", "ValidInputLower", 0.97m);
        var groupDiscountRate = await GetConfigAsync("ProcessingDiscount", "GroupDiscountRate", 0.025m);

        // 1. 加载所有批次 + ProcessGroups
        var batchDict = await _context.ProductionBatches
            .Include(b => b.ProcessGroups)
            .Where(b => batchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id);

        if (batchDict.Count == 0) return;

        // 2. 找出已有检验到料的批次及完整实体数据（含 Specification 用于匹配工序组）
        var materialCheckData = await _context.MaterialReceiveChecks
            .Include(m => m.ProductionBatch)
            .Where(m => batchIds.Contains(m.ProductionBatchId))
            .ToListAsync();
        var materialCheckLookup = materialCheckData
            .GroupBy(m => m.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 3. 活跃批次（非强制完成即可，含检验到料批次）
        var activeBatchIds = batchDict.Keys
            .Where(id => !batchDict[id].IsForceCompleted)
            .ToList();

        if (activeBatchIds.Count == 0)
        {
            // 只有强制完成批次，仅计算全工量
            var dsExtraMap2 = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
            foreach (var b in batchDict.Values)
            {
                var allSections = b.ProcessGroups
                    .SelectMany(pg => GetSectionsFromProcessGroup(pg)
                        .Select(s => (s.SectionName, s.Sequence)))
                    .Where(s => s.SectionName != SectionDefs.Warehouse)
                    .ToList();
                var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(b.PlantGrade);
                b.TotalWorkDays = CalculateTotalWorkDays(
                    b.Status,
                    allSections,
                    dayMap,
                    dsExtraMap2,
                    b.DeliveryState);
                ComputeTheoreticalOutput(b, groupDiscountRate);
            }
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

        // 5c. 一次查出所有活跃批次的去油/酸洗入缸记录
        var allPicklingInRecords = await _context.PicklingInRecords
            .Where(p => activeBatchIds.Contains(p.ProductionBatchId))
            .OrderBy(p => p.SequenceNumber)
            .ThenBy(p => p.InDate)
            .ToListAsync();
        var picklingByBatch = allPicklingInRecords.GroupBy(p => p.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 6. 一次查出所有活跃批次的回收日期
        var recoveryDateLookup = (await _context.OutsourceRecoveries
            .Where(r => activeBatchIds.Contains(r.SectionOutsource.ProductionBatchId))
            .GroupBy(r => r.SectionOutsource.ProductionBatchId)
            .Select(g => new { BatchId = g.Key, MaxDate = g.Max(r => (DateTime?)r.RecoveryDate) })
            .ToListAsync())
            .ToDictionary(r => r.BatchId, r => r.MaxDate);

        // 6b. 一次查出所有活跃批次的仓库入库记录（按 ProductionBatchNo 匹配批次号）
        var allBatchNos = activeBatchIds
            .Select(id => batchDict[id].BatchNo)
            .Where(n => n != null)
            .Distinct()
            .ToList();
        var warehouseBatchEntries = await _context.InventoryBatches
            .Where(ib => ib.ProductionBatchNo != null && allBatchNos.Contains(ib.ProductionBatchNo))
            .Select(ib => new { ib.ProductionBatchNo, ib.MaterialType })
            .Distinct()
            .ToListAsync();
        var warehouseBatchKeySet = new HashSet<string>(
            warehouseBatchEntries.Where(x => x.ProductionBatchNo != null)
                .Select(x => $"{x.ProductionBatchNo}|{x.MaterialType}"),
            StringComparer.OrdinalIgnoreCase);

        // 7. 逐批次计算跟踪字段
        var dsExtraDaysMap = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
        foreach (var batchId in activeBatchIds)
        {
            var batch = batchDict[batchId];
            var pgSpecLookup = batch.ProcessGroups.ToDictionary(pg => pg.Id, pg => pg.ManufacturingSpec!);

            var productionRecords = recordsByBatch.GetValueOrDefault(batchId) ?? new();
            var sectionOutsources = outsourcesByBatch.GetValueOrDefault(batchId) ?? new();
            var processInspections = inspectionsByBatch.GetValueOrDefault(batchId) ?? new();
            var picklingInRecords = picklingByBatch.GetValueOrDefault(batchId) ?? new();

            var hasRecords = productionRecords.Count > 0 || sectionOutsources.Count > 0 || processInspections.Count > 0 || picklingInRecords.Count > 0;

            // 检验到料：状态判定优先级：完成（成检+入库）> 成检 > 在产/未产
            var hasCheck = materialCheckLookup.TryGetValue(batchId, out var batchMaterialChecks);
            var hasWarehouse = batch.BatchNo != null && warehouseBatchKeySet.Contains($"{batch.BatchNo}|{batch.ManufacturingItem}");
            if (hasCheck && hasWarehouse)
            {
                // 同时有成检到料和仓库入库记录 → 完成
                if (batch.Status != BatchStatus.Completed)
                    batch.Status = BatchStatus.Completed;
                batch.CurrentExecDate = batchMaterialChecks!.Max(m => (DateTime?)m.ReceiveDate);
            }
            else if (hasCheck)
            {
                // 只有成检到料，无仓库入库 → 成检
                if (batch.Status != BatchStatus.InFinalInspection)
                    batch.Status = BatchStatus.InFinalInspection;
                batch.CurrentExecDate = batchMaterialChecks!.Max(m => (DateTime?)m.ReceiveDate);
            }
            else
            {
                batch.Status = hasRecords ? BatchStatus.InProgress : BatchStatus.None;
            }

            // 检验到料：建工序组ID集合 + 取"检验"工段的最大工段序号
            int materialCheckSeq = -1;
            ProcessGroup? materialCheckPg = null;
            if (hasCheck && batchMaterialChecks?.Count > 0)
            {
                var mcPgIds = batchMaterialChecks
                    .Where(m => m.ProcessGroupId > 0)
                    .Select(m => m.ProcessGroupId)
                    .ToHashSet();
                foreach (var pg in batch.ProcessGroups)
                {
                    if (!mcPgIds.Contains(pg.Id)) continue;
                    var sections = GetSectionsFromProcessGroup(pg);
                    var inspSection = sections.FirstOrDefault(s => s.SectionName == SectionDefs.Inspection);
                    if (inspSection.Sequence > materialCheckSeq)
                    {
                        materialCheckSeq = inspSection.Sequence;
                        materialCheckPg = pg;
                    }
                }
            }

            // 转换委外列表为命名类型
            var outsourceInfos = sectionOutsources.Select(s => new SectionOutsourceInfo(
                s.Id, 0, s.ProcessGroupId, s.SectionName, s.SequenceNumber,
                s.ProcessName, s.OutsourceVendor, s.SendOutDate, s.RecoveryCount
            )).ToList();

            // 公共跟踪计算（除有效投料疑问外）
            var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(batch.PlantGrade);
            ComputeBatchTrackingCore(batch, pgSpecLookup, productionRecords, outsourceInfos,
                processInspections, picklingInRecords, hasCheck,
                batchMaterialChecks?.FirstOrDefault(), materialCheckSeq, materialCheckPg,
                coldRollCompleteRatio, dayMap, dsExtraDaysMap);

            // 仓库入库覆盖：入库后当前工段为"入库"，无下一工段（批量模式）
            if (hasWarehouse)
            {
                batch.CurrentSectionName = SectionDefs.Warehouse;
                batch.NextSectionName = "-";
                batch.NextProcess = null;
                batch.CorrespondingSpec = null;
                batch.CurrentGroupName = null;
                batch.CurrentEquipmentName = null;
                batch.CurrentSpec = null;
                batch.CurrentOutsource = null;
                batch.CurrentSectionCompleted = null;
                batch.RemainingWorkDays = 0;
            }

            // 有效投料疑问
            // 对照现有效原料支数与投料支数，相差超过 5% → 疑问
            batch.ValidInputQuestion = false;
            if (batch.InputQuantity.HasValue && batch.InputQuantity > 0 && batch.CurrentValidQty.HasValue)
            {
                var ratio = (decimal)batch.CurrentValidQty.Value / batch.InputQuantity.Value;
                batch.ValidInputQuestion = ratio < validInputLower || ratio > validInputUpper;
            }
            ComputeTheoreticalOutput(batch, groupDiscountRate);
        }

        _context.ProductionBatches.UpdateRange(batchDict.Values);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 公共跟踪计算核心（两个批次模式下共享）
    /// 计算：当前工段/工序/设备/委外/规格、截止执行日、完工状态、下一工段、剩余工量、全工量
    /// 不包含：有效投料疑问（由调用方按各自逻辑计算）
    /// </summary>
    private static void ComputeBatchTrackingCore(
        ProductionBatch batch,
        Dictionary<int, string> pgSpecLookup,
        List<ProductionRecord> productionRecords,
        List<SectionOutsourceInfo> sectionOutsources,
        List<ProcessInspection> processInspections,
        List<PicklingInRecord> picklingInRecords,
        bool hasMaterialCheck,
        MaterialReceiveCheck? materialCheck,
        int materialCheckSeq,
        ProcessGroup? materialCheckPg,
        decimal coldRollCompleteRatio,
        Dictionary<string, double> dayMap,
        Dictionary<string, double> dsExtraDaysMap)
    {
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

        // 在去油/酸洗入缸记录中找最大 SequenceNumber 的记录
        PicklingInRecord? maxSeqPickling = picklingInRecords
            .OrderByDescending(p => p.SequenceNumber)
            .ThenByDescending(p => p.InDate)
            .FirstOrDefault();

        int maxRecordSeq = maxSeqRecord?.SequenceNumber ?? -1;
        int maxOutsourceSeq = maxSeqOutsource?.SequenceNumber ?? -1;
        int maxInspectionSeq = maxSeqInspection?.SequenceNumber ?? -1;
        int maxPicklingSeq = maxSeqPickling?.SequenceNumber ?? -1;

        // 五取最大（含检验到料的"检验"工段序号 + 去油/酸洗入缸记录）
        int overallMaxSeq = Math.Max(Math.Max(Math.Max(Math.Max(maxRecordSeq, maxOutsourceSeq), maxInspectionSeq), materialCheckSeq), maxPicklingSeq);

        // ====== 截止执行日 ======
        if (overallMaxSeq == maxRecordSeq && maxSeqRecord != null)
            batch.CurrentExecDate = maxSeqRecord.ExecDate;
        else if (overallMaxSeq == maxOutsourceSeq && maxSeqOutsource != null)
            batch.CurrentExecDate = maxSeqOutsource.SendOutDate;
        else if (overallMaxSeq == maxInspectionSeq && maxSeqInspection != null)
            batch.CurrentExecDate = maxSeqInspection.InspectionDate;
        else if (overallMaxSeq == materialCheckSeq && hasMaterialCheck)
            batch.CurrentExecDate = materialCheck?.ReceiveDate;
        else if (overallMaxSeq == maxPicklingSeq && maxSeqPickling != null)
            batch.CurrentExecDate = maxSeqPickling.InDate;
        else
            batch.CurrentExecDate = null;

        // ====== 当前工段/工序/设备/委外/规格 ======
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
            batch.CurrentGroupName = maxSeqOutsource!.ProcessName;
            batch.CurrentSectionName = maxSeqOutsource.SectionName;
            batch.CurrentEquipmentName = null;
            batch.CurrentSpec = pgSpecLookup.GetValueOrDefault(maxSeqOutsource.ProcessGroupId);
            batch.CurrentOutsource = maxSeqOutsource.RecoveryCount == 0
                ? maxSeqOutsource.OutsourceVendor
                : null;
        }
        else if (overallMaxSeq == maxPicklingSeq)
        {
            batch.CurrentGroupName = maxSeqPickling?.ProcessName;
            batch.CurrentSectionName = maxSeqPickling?.SectionName;
            batch.CurrentEquipmentName = maxSeqPickling?.EquipmentName;
            batch.CurrentSpec = maxSeqPickling != null
                ? pgSpecLookup.GetValueOrDefault(maxSeqPickling.ProcessGroupId)
                : null;
            batch.CurrentOutsource = null;
        }
        else if (overallMaxSeq == materialCheckSeq && hasMaterialCheck)
        {
            batch.CurrentGroupName = materialCheckPg?.ProcessName;
            batch.CurrentSectionName = SectionDefs.Inspection;
            batch.CurrentEquipmentName = null;
            batch.CurrentSpec = materialCheckPg != null
                ? pgSpecLookup.GetValueOrDefault(materialCheckPg.Id)
                : null;
            batch.CurrentOutsource = null;
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

        // ====== 当前工段是否完工 ======
        if (overallMaxSeq < 0)
        {
            batch.CurrentSectionCompleted = null;
        }
        else if (overallMaxSeq == maxRecordSeq && maxSeqRecord?.SectionName == SectionDefs.ColdRollDraw)
        {
            // 冷轧拔：总加工重量 ≥ 有效原料重量 × 95% 才算完工
            var pgId = maxSeqRecord.ProcessGroupId;
            var totalWeight = productionRecords
                .Where(r => r.ProcessGroupId == pgId && r.SectionName == SectionDefs.ColdRollDraw && r.Weight.HasValue)
                .Sum(r => r.Weight!.Value);
            var threshold = (batch.CurrentValidWeight ?? batch.InputWeight ?? 0) * coldRollCompleteRatio;
            batch.CurrentSectionCompleted = totalWeight >= threshold;
        }
        else if (overallMaxSeq == maxOutsourceSeq)
        {
            // 工段委外：有回收记录才算完工
            batch.CurrentSectionCompleted = maxSeqOutsource?.RecoveryCount > 0;
        }
        else if (overallMaxSeq == maxPicklingSeq)
        {
            // 去油/酸洗：按入缸状态判断 — Soaking=在产中(false)，Completed=已完工(true)
            batch.CurrentSectionCompleted = maxSeqPickling?.Status == PicklingStatus.Completed;
        }
        else
        {
            // 其它工段（含过程检验）：有记录即完工
            batch.CurrentSectionCompleted = true;
        }

        // ====== 下一工段 / 对应规格 ======
        var allSections = batch.ProcessGroups
            .SelectMany(pg => GetSectionsFromProcessGroup(pg)
                .Select(s => new { pgId = pg.Id, s.SectionName, s.Sequence }))
            .ToList();

        if (overallMaxSeq < 0)
        {
            // 未开始生产：取第一工序组（SequenceNumber=1）的最小工段
            var firstPg = batch.ProcessGroups
                .OrderBy(pg => pg.SequenceNumber)
                .FirstOrDefault();
            if (firstPg != null)
            {
                var firstSections = GetSectionsFromProcessGroup(firstPg);
                var firstSection = firstSections.OrderBy(s => s.Sequence).FirstOrDefault();
                batch.NextSectionName = firstSection.SectionName;
                batch.CorrespondingSpec = firstPg.ManufacturingSpec;
            }
            else
            {
                batch.NextSectionName = null;
                batch.CorrespondingSpec = null;
            }
            batch.NextProcess = firstPg?.ProcessName;
        }
        else
        {
            int nextSeq = overallMaxSeq + 1;
            var nextSection = allSections.FirstOrDefault(s => s.Sequence == nextSeq);
            batch.NextSectionName = nextSection?.SectionName;
            batch.CorrespondingSpec = nextSection != null
                ? pgSpecLookup.GetValueOrDefault(nextSection.pgId)
                : null;
            batch.NextProcess = nextSection != null
                ? batch.ProcessGroups
                    .Where(pg => pg.Id == nextSection.pgId)
                    .Select(pg => pg.ProcessName)
                    .FirstOrDefault()
                : null;
        }

        // ====== 剩余工量计算（排除"入库"工段） ======
        var sectionTuples = allSections
            .Where(s => s.SectionName != SectionDefs.Warehouse)
            .Select(s => (s.SectionName, s.Sequence))
            .ToList();
        batch.RemainingWorkDays = CalculateRemainingWorkDays(
            batch.Status,
            batch.CurrentSectionCompleted,
            overallMaxSeq,
            sectionTuples,
            dayMap,
            dsExtraDaysMap,
            batch.DeliveryState);

        // ====== 全工量计算 ======
        batch.TotalWorkDays = CalculateTotalWorkDays(
            batch.Status,
            sectionTuples,
            dayMap,
            dsExtraDaysMap,
            batch.DeliveryState);
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

        // 处理 BatchNo 导航属性筛选（ProductionRecord 实体无 BatchNo 属性，ApplyFilters 反射不到）
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
                Shift = EnumHelper.TryParse<ShiftType>(r.Shift),
                Quantity = r.Quantity,
                Weight = r.Weight,
                SolutionTemperature = r.SolutionTemperature,
                SoakTime = r.SoakTime,
                ProductStatus = r.ProductStatus,
                CuttingMultiple = r.CuttingMultiple,
                FinishedCutLength = r.FinishedCutLength,
                PostCutQuantity = r.PostCutQuantity,
                FaceCutCount = r.FaceCutCount,
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
                Shift = EnumHelper.TryParse<ShiftType>(r.Shift),
                Quantity = r.Quantity,
                Weight = r.Weight,
                SolutionTemperature = r.SolutionTemperature,
                SoakTime = r.SoakTime,
                ProductStatus = r.ProductStatus,
                CuttingMultiple = r.CuttingMultiple,
                FinishedCutLength = r.FinishedCutLength,
                PostCutQuantity = r.PostCutQuantity,
                FaceCutCount = r.FaceCutCount,
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

    public async Task<List<DailySectionOutputDto>> GetDailySectionOutputAsync(DateTime date)
    {
        return await _context.ProductionRecords
            .AsNoTracking()
            .Where(r => r.ExecDate.Date == date.Date)
            .GroupBy(r => r.SectionName)
            .Select(g => new DailySectionOutputDto
            {
                SectionName = g.Key,
                TotalWeight = g.Sum(r => r.Weight ?? 0m),
                RecordCount = g.Count()
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
            ("solutiontemperature", false) => queryable.OrderBy(r => r.SolutionTemperature ?? 0),
            ("solutiontemperature", true) => queryable.OrderByDescending(r => r.SolutionTemperature ?? 0),
            ("soaktime", false) => queryable.OrderBy(r => r.SoakTime ?? 0),
            ("soaktime", true) => queryable.OrderByDescending(r => r.SoakTime ?? 0),
            ("productstatus", false) => queryable.OrderBy(r => r.ProductStatus ?? ""),
            ("productstatus", true) => queryable.OrderByDescending(r => r.ProductStatus ?? ""),
            ("cuttingmultiple", false) => queryable.OrderBy(r => r.CuttingMultiple ?? 0),
            ("cuttingmultiple", true) => queryable.OrderByDescending(r => r.CuttingMultiple ?? 0),
            ("finishedcutlength", false) => queryable.OrderBy(r => r.FinishedCutLength ?? 0),
            ("finishedcutlength", true) => queryable.OrderByDescending(r => r.FinishedCutLength ?? 0),
            ("postcutquantity", false) => queryable.OrderBy(r => r.PostCutQuantity ?? 0),
            ("postcutquantity", true) => queryable.OrderByDescending(r => r.PostCutQuantity ?? 0),
            ("facecutcount", false) => queryable.OrderBy(r => r.FaceCutCount ?? 0),
            ("facecutcount", true) => queryable.OrderByDescending(r => r.FaceCutCount ?? 0),
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
            ("datasource", false) => queryable.OrderBy(r => r.DataSource ?? ""),
            ("datasource", true) => queryable.OrderByDescending(r => r.DataSource ?? ""),
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
                Status = s.Status,
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
                        Status = s.Status,
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
                Shift = EnumHelper.TryParse<ShiftType>(r.Shift),
                Quantity = r.Quantity,
                Weight = r.Weight,
                SolutionTemperature = r.SolutionTemperature,
                SoakTime = r.SoakTime,
                ProductStatus = r.ProductStatus,
                CuttingMultiple = r.CuttingMultiple,
                FinishedCutLength = r.FinishedCutLength,
                PostCutQuantity = r.PostCutQuantity,
                FaceCutCount = r.FaceCutCount,
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

    /// <summary>
    /// 获取生产记录筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("ProductionRecordService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var query = from r in _context.ProductionRecords
                        join pb in _context.ProductionBatches on r.ProductionBatchId equals pb.Id
                        select new
                        {
                            pb.BatchNo,
                            r.ProcessName,
                            r.ManufacturingSpec,
                            r.SectionName,
                            r.EquipmentName,
                            r.Operator,
                            r.Shift,
                            r.TagNo,
                            r.PlantGrade,
                            r.Remark,
                            r.ExecDate,
                            r.DataSource,
                            r.ProductStatus
                        };

            var results = await query.AsNoTracking().ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["BatchNo"] = results.Select(x => x.BatchNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["ProcessName"] = results.Select(x => x.ProcessName).Distinct().OrderBy(x => x).ToList(),
                ["ManufacturingSpec"] = results.Select(x => x.ManufacturingSpec).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SectionName"] = results.Select(x => x.SectionName).Distinct().OrderBy(x => x).ToList(),
                ["EquipmentName"] = results.Select(x => x.EquipmentName).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["Operator"] = results.Select(x => x.Operator).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["Shift"] = results.Select(x => x.Shift).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["TagNo"] = results.Select(x => x.TagNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["PlantGrade"] = results.Select(x => x.PlantGrade).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["Remark"] = results.Select(x => x.Remark).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["ExecDate"] = results.Select(x => x.ExecDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["ProductStatus"] = results.Select(x => x.ProductStatus).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    /// <summary>
    /// 从工序组中提取所有非空工段及其顺序值
    /// </summary>
    private static List<(string SectionName, int Sequence)> GetSectionsFromProcessGroup(ProcessGroup pg)
        => pg.GetNonEmptySections();

    // ========== 剩余工量计算 ==========

    /// <summary>
    /// 计算批次剩余工量（天）
    /// </summary>
    private static int CalculateRemainingWorkDays(
        BatchStatus status,
        bool? currentSectionCompleted,
        int overallMaxSeq,
        List<(string SectionName, int Sequence)> allSections,
        Dictionary<string, double> dayMap,
        Dictionary<string, double> deliveryStateExtraDays,
        string? deliveryState)
    {
        // 完成/成检 → 0
        if (status == BatchStatus.Completed || status == BatchStatus.InFinalInspection)
            return 0;

        if (allSections.Count == 0) return 0;

        // 确定起始工段序号
        int startSeq;
        if (overallMaxSeq < 0)
        {
            // 没有任何记录，从第一个工段开始
            startSeq = allSections.Min(s => s.Sequence);
        }
        else if (currentSectionCompleted == false)
        {
            // 当前工段"生产中" → 包含当前工段
            startSeq = overallMaxSeq;
        }
        else
        {
            // 当前工段 null（无记录）或 true（完工）→ 从下一工段开始
            startSeq = overallMaxSeq + 1;
        }

        // 累加从 startSeq 开始的所有工段天数
        double totalDays = 0;
        foreach (var section in allSections.Where(s => s.Sequence >= startSeq))
        {
            totalDays += dayMap.GetValueOrDefault(section.SectionName, 0);
        }

        // 交货状态调整：从配置表读取附加天数
        if (deliveryStateExtraDays.TryGetValue(deliveryState ?? "", out var dsExtra))
            totalDays += dsExtra;
        else if (deliveryStateExtraDays.TryGetValue("", out var defaultExtra))
            totalDays += defaultExtra;

        return (int)Math.Round(totalDays, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 计算批次全工量（天）：从组内序号1开始，累加所有工段的标准天数
    /// </summary>
    private static int CalculateTotalWorkDays(
        BatchStatus status,
        List<(string SectionName, int Sequence)> allSections,
        Dictionary<string, double> dayMap,
        Dictionary<string, double> deliveryStateExtraDays,
        string? deliveryState)
    {
        // 全工量始终计算，不受批次状态影响

        if (allSections.Count == 0) return 0;

        // 始终从最小序号（即组内序号1）开始累加
        int startSeq = allSections.Min(s => s.Sequence);

        double totalDays = 0;
        foreach (var section in allSections.Where(s => s.Sequence >= startSeq))
        {
            totalDays += dayMap.GetValueOrDefault(section.SectionName, 0);
        }

        // 交货状态调整：从配置表读取附加天数
        if (deliveryStateExtraDays.TryGetValue(deliveryState ?? "", out var dsExtra))
            totalDays += dsExtra;
        else if (deliveryStateExtraDays.TryGetValue("", out var defaultExtra))
            totalDays += defaultExtra;

        return (int)Math.Round(totalDays, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 自动计算产品状态（荒管/在制/成品）
    /// </summary>
    private static string CalculateProductStatus(
        string processName,
        string? manufacturingSpec,
        string? batchManufacturingItem,
        List<ProcessGroup> processGroups)
    {
        return ProductStatusHelper.Calculate(processName, manufacturingSpec, batchManufacturingItem, processGroups);
    }

    /// <summary>
    /// 判断制造物品是否属于"成品"类别（OrderFinishedProduct/PreparedMaterial/SpecialDeliveryStatus）
    /// </summary>
    private static bool IsFinishedManufacturingItem(string? manufacturingItem) =>
        ProductStatusHelper.IsFinishedManufacturingItem(manufacturingItem);
}
