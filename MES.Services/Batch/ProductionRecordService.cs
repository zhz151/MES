using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
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
    private readonly IFixedLengthWorkOrderService _fixedLengthWorkOrderService;
    private readonly ISectionNameDisplayService _sectionNameDisplay;
    private readonly IProcessDefinitionService _processDefService;
    private readonly IWorkOrderListSummaryRefreshService? _listSummaryService;
    private readonly IMemoryCache _cache;
    private readonly IOperatorNameValidator _operatorNameValidator;

    private sealed record SectionOutsourceInfo(
        int Id,
        int ProductionBatchId,
        int ProcessGroupId,
        string SectionName,
        int SequenceNumber,
        string ProcessName,
        string? OutsourceVendor,
        DateTime SendOutDate,
        int RecoveryCount,
        decimal RecoveryWeight,
        bool IsInternal,
        DateTime? MaxRecoveryDate);

    public ProductionRecordService(
        AppDbContext context,
        ILogger<ProductionRecordService> logger,
        IStandardWorkDayService standardWorkDayService,
        IStandardWorkDayDeliveryStateService deliveryStateService,
        IConfigParameterService configService,
        IQualityProcessTrackingService qualityProcessTracking,
        IWorkOrderExecutionService workOrderExecutionService,
        IFixedLengthWorkOrderService fixedLengthWorkOrderService,
        ISectionNameDisplayService sectionNameDisplay,
        IProcessDefinitionService processDefService,
        IMemoryCache cache,
        IOperatorNameValidator operatorNameValidator,
        IWorkOrderListSummaryRefreshService? listSummaryService = null)
    {
        _context = context;
        _logger = logger;
        _standardWorkDayService = standardWorkDayService;
        _deliveryStateService = deliveryStateService;
        _configService = configService;
        _qualityProcessTracking = qualityProcessTracking;
        _workOrderExecutionService = workOrderExecutionService;
        _fixedLengthWorkOrderService = fixedLengthWorkOrderService;
        _sectionNameDisplay = sectionNameDisplay;
        _processDefService = processDefService;
        _cache = cache;
        _operatorNameValidator = operatorNameValidator;
        _listSummaryService = listSummaryService;
    }

    /// <summary>
    /// 校验成品切割长度：当「订单号+主号」存在定尺工单时——
    /// 正式成品切割：切割长度必须属于该主号下的定尺长度集合；
    /// 预成切：切割长度必须不属于该主号下的定尺长度集合（预成切不是正式成品切割，应区别于正式成品长度）。
    /// 返回 null 表示通过，否则返回错误信息（不含行号前缀，由调用方补充）。
    /// </summary>
    private async Task<string?> ValidateFinishedCutLengthAsync(ProductionBatch? batch, decimal? finishedCutLength, bool isPreCut)
    {
        if (finishedCutLength == null || finishedCutLength <= 0 || batch == null) return null;
        var validLengths = await _fixedLengthWorkOrderService
            .GetLengthsByMainNoAsync(batch.SalesOrderNo, batch.ProductionMainNo);
        return ValidateFinishedCutLength(batch.SalesOrderNo, batch.ProductionMainNo, finishedCutLength, isPreCut, validLengths);
    }

    /// <summary>
    /// 定尺长度校验纯函数（预取集合版，供批量创建复用避免循环内 N+1 查询）。
    /// </summary>
    private static string? ValidateFinishedCutLength(
        string salesOrderNo, string productionMainNo, decimal? finishedCutLength, bool isPreCut, HashSet<decimal> validLengths)
    {
        if (finishedCutLength == null || finishedCutLength <= 0) return null;
        if (validLengths.Count == 0) return null; // 该订单号+主号非定尺，跳过校验
        var inSet = validLengths.Contains(finishedCutLength.Value);
        if (isPreCut)
        {
            if (inSet) return $"预成切长度({finishedCutLength.Value.ToString("G29")}mm)不能属于该订单号+主号({salesOrderNo}/{productionMainNo})下的正式定尺长度（预成切应区别于正式成品长度）";
            return null;
        }
        if (inSet) return null;
        return $"成品切割长度({finishedCutLength.Value.ToString("G29")}mm)不属于该订单号+主号({salesOrderNo}/{productionMainNo})下的定尺长度";
    }

    /// <summary>
    /// 预成切一致性校验：预成切本质是"成品切割"行为，必须是断切工段，且必须填写成品长度。
    /// 返回 null 表示通过，否则返回错误信息（不含行号前缀，由调用方补充）。
    /// </summary>
    private static string? ValidatePreCut(bool isPreCut, string? sectionName, decimal? finishedCutLength)
    {
        if (!isPreCut) return null;
        if (sectionName != SectionKeys.Cut)
            return "预成切必须是断切工段";
        if (finishedCutLength == null || finishedCutLength <= 0)
            return "预成切必须填写成品长度";
        return null;
    }

    /// <summary>
    /// 定尺切割长度匹配标识计算（存储枚举名字符串）：
    /// 仅「成品切割 + 定尺 + 非预成切 + 有成品长度」时计算——
    /// 完全匹配 = 长度属本工单号（订单+主号+次号）定尺集合；主号匹配 = 仅属订单+主号定尺集合；否则 null（不适用）。
    /// 现有成品切割长度校验已保证可提交长度必属订单+主号集合，故实际只会命中两态之一。
    /// </summary>
    private static string? ComputeCutLengthMatch(
        string? productStatus, string? recordLengthStatus, bool isPreCut, decimal? finishedCutLength,
        HashSet<decimal> workOrderLengths, HashSet<decimal> mainNoLengths)
    {
        if (productStatus != ProductStatuses.Finished) return null;
        if (!string.Equals(recordLengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase)) return null;
        if (isPreCut) return null;
        return CutLengthMatchHelper.Match(workOrderLengths, mainNoLengths, finishedCutLength)?.ToString();
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
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;
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

        var items = (await queryable
            .OrderBy(r => r.SequenceNumber)
            .ThenBy(r => r.ExecDate)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new
            {
                r.Id,
                r.ProductionBatchId,
                r.ProcessGroupId,
                r.ProcessName,
                r.ManufacturingSpec,
                r.SectionName,
                r.SequenceNumber,
                r.ExecDate,
                r.EquipmentName,
                r.Operator,
                r.Shift,
                r.Quantity,
                r.Weight,
                r.SolutionTemperature,
                r.SoakTime,
                r.ProductStatus,
                r.IsPreCut,
                r.LengthStatus,
                r.CuttingMultiple,
                r.FinishedCutLength,
                r.CutLengthMatchType,
                r.PostCutQuantity,
                r.FaceCutCount,
                r.TagNo,
                r.PlantGrade,
                r.Remark
            })
            .ToListAsync())
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
                IsPreCut = r.IsPreCut,
                LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(r.LengthStatus),
                CuttingMultiple = r.CuttingMultiple,
                FinishedCutLength = r.FinishedCutLength,
                CutLengthMatchType = EnumHelper.TryParse<MES.Core.Enums.CutLengthMatchType>(r.CutLengthMatchType),
                PostCutQuantity = r.PostCutQuantity,
                FaceCutCount = r.FaceCutCount,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                Remark = r.Remark
            })
            .ToList();

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

        // 自动解析 SequenceNumber（语义A：序号=工段步骤号，一旦定位工序组即对齐；工段不在工序组时保留前端值兜底）
        var sequenceNumber = request.SequenceNumber;
        if (processGroupId > 0)
        {
            var pg = await _context.ProcessGroups.FindAsync(processGroupId.Value);
            if (pg != null)
            {
                var sections = GetSectionsFromProcessGroup(pg);
                var match = sections.FirstOrDefault(s => SectionKeys.ToKey(s.SectionName) == request.SectionName);
                if (match.Sequence > 0)
                    sequenceNumber = match.Sequence;
            }
        }

        // 加载该批次所有工序组，用于自动计算产类
        var batchProcessGroups = await _context.ProcessGroups
            .Where(pg => pg.ProductionBatchId == batchId)
            .ToListAsync();

        var productStatus = CalculateProductStatus(request.ProcessName, request.ManufacturingSpec, batch.ManufacturingItem, batchProcessGroups, batch.Specification);
        var recordLengthStatus = CalculateLengthStatus(request.SectionName, productStatus, batch.LengthStatus);

        // 操作人强制实名：非空才校验，未命中启用员工表即拒绝
        await _operatorNameValidator.EnsureValidOrThrowAsync(request.Operator);

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
            Quantity = request.Quantity ?? 0,
            Weight = request.Weight ?? 0,
            SolutionTemperature = request.SolutionTemperature,
            SoakTime = request.SoakTime,
            ProductStatus = productStatus,
            IsPreCut = request.IsPreCut,
            LengthStatus = recordLengthStatus,
            CuttingMultiple = request.CuttingMultiple,
            FinishedCutLength = request.FinishedCutLength,
            PostCutQuantity = request.PostCutQuantity,
            FaceCutCount = request.FaceCutCount,
            TagNo = request.TagNo ?? batch.TagNo,
            PlantGrade = request.PlantGrade ?? batch.PlantGrade,
            Remark = request.Remark,
            DataSource = request.DataSource ?? "MANUAL"
        };

        // 预成切一致性校验（必须是断切工段 + 必须填写成品长度）
        var preCutError = ValidatePreCut(request.IsPreCut == true, request.SectionName, request.FinishedCutLength);
        if (preCutError != null)
            throw new BusinessException(preCutError);

        // 成品切割长度校验（按「订单号+主号」维度；预成切时反转：长度必须不在正式定尺集合内）
        var cutLengthError = await ValidateFinishedCutLengthAsync(batch, request.FinishedCutLength, request.IsPreCut == true);
        if (cutLengthError != null)
            throw new BusinessException(cutLengthError);

        // 定尺切割长度匹配标识（成品切割+定尺+非预成切+有成品长度时计算，否则空白）
        var woLengths = await _fixedLengthWorkOrderService.GetLengthsByWorkOrderNoAsync(batch.WorkOrderNo);
        var mainLengths = await _fixedLengthWorkOrderService.GetLengthsByMainNoAsync(batch.SalesOrderNo, batch.ProductionMainNo);
        entity.CutLengthMatchType = ComputeCutLengthMatch(productStatus, recordLengthStatus, request.IsPreCut == true, request.FinishedCutLength, woLengths, mainLengths);

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
            IsPreCut = entity.IsPreCut,
            LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(entity.LengthStatus),
            CuttingMultiple = entity.CuttingMultiple,
            FinishedCutLength = entity.FinishedCutLength,
            CutLengthMatchType = EnumHelper.TryParse<MES.Core.Enums.CutLengthMatchType>(entity.CutLengthMatchType),
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

        var crKeys = await _processDefService.GetColdRollOrDrawKeysAsync();
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

        // 操作人强制实名：预加载启用员工快照，逐行校验
        var activeEmployees = await _operatorNameValidator.LoadActiveAsync();

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
            .Where(r => allBatchIds.Contains(r.ProductionBatchId) && r.SectionName == SectionKeys.ColdRollDraw)
            .Select(r => new { r.ProductionBatchId, r.ProcessGroupId })
            .ToListAsync();
        var outsourcedColdRollDraw = await _context.SectionOutsources
            .Where(o => allBatchIds.Contains(o.ProductionBatchId) && o.SectionName == SectionKeys.ColdRollDraw)
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
            if (request.SectionName == SectionKeys.ColdRollDraw)
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
            if (request.SectionName == SectionKeys.ColdRollDraw)
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
            if (pg == null || !crKeys.Contains(ProcessKeys.ToKey(pg.ProcessName) ?? pg.ProcessName))
                continue;

            // 该工序组中是否有冷轧拔记录（已有 + 本次提交）
            var hasColdRollDraw = coldRollDrawExists.Contains((batchId, pgId.Value))
                || pendingColdRollDraw.Contains((batchId, pgId.Value));

            if (!hasColdRollDraw)
            {
                requestErrors.Add($"第{i + 1}行：工序「{ProcessKeys.ToChinese(pg.ProcessName)}」必须首先记录「冷轧拔」工段，才能记录「{SectionKeys.ToChinese(request.SectionName)}」");
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
                if (request.SectionName == SectionKeys.Inspection)
                {
                    requestErrors.Add($"第{i + 1}行：工段「检验」已由过程检验模块管理，不允许在生产记录中使用");
                    continue;
                }
                var sections = GetSectionsFromProcessGroup(pg);
                if (!sections.Any(s => SectionKeys.ToKey(s.SectionName) == request.SectionName))
                    requestErrors.Add($"第{i + 1}行：工段「{SectionKeys.ToChinese(request.SectionName)}」不存在于工序组「{ProcessKeys.ToChinese(pg.ProcessName)}」中，无法提交");
            }
        }

        // 预查询：各批次各工序组的冷轧拔总重量（用于冷轧拔总加工重量验证，含自产 + 委外发出）
        var coldRollDrawWeightByKey = allExistingRecords
            .Where(r => r.SectionName == SectionKeys.ColdRollDraw && r.Weight.HasValue)
            .GroupBy(r => new { r.ProductionBatchId, r.ProcessGroupId })
            .ToDictionary(g => (g.Key.ProductionBatchId, g.Key.ProcessGroupId), g => g.Sum(r => r.Weight!.Value));
        // 委外发出重量预取：扁平投影后内存聚合（避免 GroupBy 无聚合的不可翻译形态）
        var outsourcedCrRecords = await _context.SectionOutsources
            .Where(o => allBatchIds.Contains(o.ProductionBatchId) && o.SectionName == SectionKeys.ColdRollDraw && o.SendWeight.HasValue)
            .Select(o => new { o.ProductionBatchId, o.ProcessGroupId, o.SendWeight })
            .ToListAsync();
        foreach (var o in outsourcedCrRecords)
        {
            var key = (o.ProductionBatchId, o.ProcessGroupId);
            var w = o.SendWeight!.Value;
            if (coldRollDrawWeightByKey.TryGetValue(key, out var cur))
                coldRollDrawWeightByKey[key] = cur + w;
            else
                coldRollDrawWeightByKey[key] = w;
        }

        // 预查询：各批次各工序组的断切总重量（用于断切总加工重量验证，同批次+同工序组聚合，含预成切）
        var cutWeightByKey = allExistingRecords
            .Where(r => r.SectionName == SectionKeys.Cut && r.Weight.HasValue)
            .GroupBy(r => new { r.ProductionBatchId, r.ProcessGroupId })
            .ToDictionary(g => (g.Key.ProductionBatchId, g.Key.ProcessGroupId), g => g.Sum(r => r.Weight!.Value));

        var simpleDuplicateSections = new HashSet<string>
        {
            SectionKeys.OilPipeCut, SectionKeys.Degrease, SectionKeys.EmulsionWash,
            SectionKeys.UltrasonicWash, SectionKeys.ClothPolish, SectionKeys.BrightAnnealing,
            SectionKeys.Solution, SectionKeys.Straighten, SectionKeys.ThicknessMeasure,
            SectionKeys.Pickle, SectionKeys.OuterPolish, SectionKeys.InnerPolish,
            SectionKeys.InnerGrinding, SectionKeys.OuterSpotGrinding, SectionKeys.SandBlasting,
            SectionKeys.ShotBlasting, SectionKeys.WeldingHead, SectionKeys.Welding,
            SectionKeys.Lubrication, SectionKeys.Packing, SectionKeys.Extra1, SectionKeys.Extra2
        };

        // 预取：各批次所属「订单号+主号」的定尺长度集合（成品切割长度校验用，避免循环内 N+1 查询）
        var fixedLengthSets = new Dictionary<string, HashSet<decimal>>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in batchLookup.Values)
        {
            if (string.IsNullOrWhiteSpace(b.SalesOrderNo) || string.IsNullOrWhiteSpace(b.ProductionMainNo)) continue;
            var key = $"{b.SalesOrderNo.Trim()}|{b.ProductionMainNo.Trim()}";
            if (fixedLengthSets.ContainsKey(key)) continue;
            fixedLengthSets[key] = await _fixedLengthWorkOrderService
                .GetLengthsByMainNoAsync(b.SalesOrderNo, b.ProductionMainNo);
        }

        // 5) 重复记录校验（pendingKeys 模式：同时防范 DB 重复和行间重复）
        var pendingSimpleKeys = new HashSet<(int batchId, int pgId, string section)>();
        var pendingColdRollDrawKeys = new HashSet<(int batchId, int pgId, DateTime date, string equipment, string op)>();
        var pendingCutKeys = new HashSet<(int batchId, int pgId, decimal? cutLength)>();
        // 断切本次提交累计重量（同批次+同工序组聚合，用于断切总加工重量验证，防行间多条累加超限）
        var pendingCutWeightByKey = new Dictionary<(int batchId, int pgId), decimal>();
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var batch = batchLookup[request.BatchNo];
            var batchId = batch.Id;

            // 操作人强制实名：非空才校验，未命中启用员工表收集进 requestErrors
            var opUnmatched = OperatorNameHelper.FindUnmatched(activeEmployees, request.Operator);
            if (opUnmatched.Count > 0)
                requestErrors.Add($"第{i + 1}行：操作人「{string.Join("、", opUnmatched)}」不在启用员工表中，请选择有效操作人");

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
                    requestErrors.Add($"第{i + 1}行：工段「{SectionKeys.ToChinese(request.SectionName)}」在该批次该工序组中已存在记录，不能重复创建");
                else
                    pendingSimpleKeys.Add(key);
            }
            else if (request.SectionName == SectionKeys.ColdRollDraw)
            {
                // 规则(2)：同批次+同工序组+同工段+同执行日期+同设备名称+同操作人 → 重复
                var key = (batchId, pgId.Value, request.ExecDate.Date, request.EquipmentName ?? "", request.Operator ?? "");
                var dup = batchRecords.Any(r =>
                    r.ProcessGroupId == pgId.Value &&
                    r.SectionName == SectionKeys.ColdRollDraw &&
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
            else if (request.SectionName == SectionKeys.Cut)
            {
                // 规则(3)：同批次+同工序组+同工段+同成品长度 → 重复
                var key = (batchId, pgId.Value, request.FinishedCutLength);
                var dup = batchRecords.Any(r =>
                    r.ProcessGroupId == pgId.Value &&
                    r.SectionName == SectionKeys.Cut &&
                    r.FinishedCutLength == request.FinishedCutLength)
                    || pendingCutKeys.Contains(key);
                if (dup)
                    requestErrors.Add($"第{i + 1}行：断切在该批次该工序组中已存在相同成品长度的记录，不能重复创建");
                else
                    pendingCutKeys.Add(key);

                // 附加：断切总加工重量（DB已有 + 本次提交，同批次+同工序组聚合，含预成切）不能大于现有效原料重量
                var existingCutWeight = cutWeightByKey.GetValueOrDefault((batchId, pgId.Value), 0m);
                var pendingCutWeight = pendingCutWeightByKey.GetValueOrDefault((batchId, pgId.Value), 0m);
                var totalCutWeight = existingCutWeight + pendingCutWeight + (request.Weight ?? 0m);
                if (totalCutWeight > (batch.CurrentValidWeight ?? batch.InputWeight))
                    requestErrors.Add($"第{i + 1}行：断切总加工重量({totalCutWeight})不能大于有效原料重量({batch.CurrentValidWeight ?? batch.InputWeight})");
                pendingCutWeightByKey[(batchId, pgId.Value)] = pendingCutWeight + (request.Weight ?? 0m);
            }

            // 预成切一致性校验（必须是断切工段 + 必须填写成品长度）
            var preCutErr = ValidatePreCut(request.IsPreCut == true, request.SectionName, request.FinishedCutLength);
            if (preCutErr != null)
                requestErrors.Add($"第{i + 1}行：{preCutErr}");

            // 成品切割长度校验（按「订单号+主号」维度，无论工段只要有值；预成切时反转：长度必须不在正式定尺集合内）
            var cutLengthErr = ValidateFinishedCutLength(
                batch.SalesOrderNo, batch.ProductionMainNo,
                request.FinishedCutLength,
                request.IsPreCut == true,
                fixedLengthSets.GetValueOrDefault($"{batch.SalesOrderNo.Trim()}|{batch.ProductionMainNo.Trim()}", new HashSet<decimal>()));
            if (cutLengthErr != null)
                requestErrors.Add($"第{i + 1}行：{cutLengthErr}");
        }

        if (requestErrors.Any())
            throw new BusinessException(string.Join("；", requestErrors));

        // 定尺工单长度映射一次预取（按工单号 / 按订单号+主号），定尺切割长度匹配标识计算用
        var lengthMaps = await _fixedLengthWorkOrderService.GetLengthMapsAsync();

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

            // 自动解析 SequenceNumber（语义A：序号=工段步骤号，始终对齐工序组；工段必须存在已在校验阶段保证）
            var sequenceNumber = request.SequenceNumber;
            if (processGroupId > 0)
            {
                var pg = processGroups.FirstOrDefault(pg => pg.Id == processGroupId.Value);
                if (pg != null)
                {
                    var sections = GetSectionsFromProcessGroup(pg);
                    var match = sections.FirstOrDefault(s => SectionKeys.ToKey(s.SectionName) == request.SectionName);
                    if (match.Sequence > 0)
                        sequenceNumber = match.Sequence;
                }
            }

            var productStatus = CalculateProductStatus(request.ProcessName, request.ManufacturingSpec, batch.ManufacturingItem, pgByBatch.GetValueOrDefault(batchId) ?? new(), batch.Specification);
            var recordLengthStatus = CalculateLengthStatus(request.SectionName, productStatus, batch.LengthStatus);

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
                Quantity = request.Quantity ?? 0,
                Weight = request.Weight ?? 0,
                SolutionTemperature = request.SolutionTemperature,
                SoakTime = request.SoakTime,
                ProductStatus = productStatus,
                IsPreCut = request.IsPreCut,
                LengthStatus = recordLengthStatus,
                CuttingMultiple = request.CuttingMultiple,
                FinishedCutLength = request.FinishedCutLength,
                CutLengthMatchType = ComputeCutLengthMatch(
                    productStatus, recordLengthStatus, request.IsPreCut == true, request.FinishedCutLength,
                    lengthMaps.ByWorkOrderNo.GetValueOrDefault(batch.WorkOrderNo, new HashSet<decimal>()),
                    lengthMaps.ByMainKey.GetValueOrDefault($"{batch.SalesOrderNo.Trim()}|{batch.ProductionMainNo.Trim()}", new HashSet<decimal>())),
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
            IsPreCut = e.IsPreCut,
            LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(e.LengthStatus),
            CuttingMultiple = e.CuttingMultiple,
            FinishedCutLength = e.FinishedCutLength,
            CutLengthMatchType = EnumHelper.TryParse<MES.Core.Enums.CutLengthMatchType>(e.CutLengthMatchType),
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

        // 加载批次及其工序组，用于重新计算产类
        var batch = await _context.ProductionBatches.FindAsync(entity.ProductionBatchId);
        var batchProcessGroups = await _context.ProcessGroups
            .Where(pg => pg.ProductionBatchId == entity.ProductionBatchId)
            .ToListAsync();

        // 语义A：更新时重对齐序号 = 工段步骤号（工序组编辑后纠正漂移；工段不在工序组时保留原值）
        var recPg = batchProcessGroups.FirstOrDefault(pg => pg.Id == entity.ProcessGroupId);
        if (recPg != null)
        {
            var recSeq = recPg.GetSectionSequence(entity.SectionName);
            if (recSeq.HasValue)
                entity.SequenceNumber = recSeq.Value;
        }

        // 操作人强制实名：非空才校验（只校验新传入值）
        await _operatorNameValidator.EnsureValidOrThrowAsync(request.Operator);

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
        if (batch != null)
        {
            var productStatus = CalculateProductStatus(entity.ProcessName, entity.ManufacturingSpec, batch.ManufacturingItem, batchProcessGroups, batch.Specification);
            entity.ProductStatus = productStatus;
            entity.LengthStatus = CalculateLengthStatus(entity.SectionName, productStatus, batch.LengthStatus);
        }
        entity.CuttingMultiple = request.CuttingMultiple ?? entity.CuttingMultiple;
        entity.FinishedCutLength = request.FinishedCutLength ?? entity.FinishedCutLength;
        entity.PostCutQuantity = request.PostCutQuantity ?? entity.PostCutQuantity;
        entity.FaceCutCount = request.FaceCutCount ?? entity.FaceCutCount;
        entity.IsPreCut = request.IsPreCut ?? entity.IsPreCut;
        entity.TagNo = request.TagNo ?? entity.TagNo;
        entity.PlantGrade = request.PlantGrade ?? entity.PlantGrade;
        entity.Remark = request.Remark ?? entity.Remark;

        // 预成切一致性校验（工段不可编辑，用 entity 生效工段；长度用生效值）
        var effectiveCutLength = request.FinishedCutLength ?? entity.FinishedCutLength;
        var effectiveIsPreCut = request.IsPreCut ?? entity.IsPreCut;
        var preCutError = ValidatePreCut(effectiveIsPreCut == true, entity.SectionName, effectiveCutLength);
        if (preCutError != null)
            throw new BusinessException(preCutError);

        // 成品切割长度校验（按「订单号+主号」维度，用生效值校验；预成切时反转：长度必须不在正式定尺集合内）
        var cutLengthError = await ValidateFinishedCutLengthAsync(batch, effectiveCutLength, effectiveIsPreCut == true);
        if (cutLengthError != null)
            throw new BusinessException(cutLengthError);

        // 定尺切割长度匹配标识（用生效值重算；批次为空或集合为空时自然返回 null=不适用）
        HashSet<decimal> woLengths, mainLengths;
        if (batch != null)
        {
            woLengths = await _fixedLengthWorkOrderService.GetLengthsByWorkOrderNoAsync(batch.WorkOrderNo);
            mainLengths = await _fixedLengthWorkOrderService.GetLengthsByMainNoAsync(batch.SalesOrderNo, batch.ProductionMainNo);
        }
        else
        {
            woLengths = new HashSet<decimal>();
            mainLengths = new HashSet<decimal>();
        }
        entity.CutLengthMatchType = ComputeCutLengthMatch(entity.ProductStatus, entity.LengthStatus, effectiveIsPreCut == true, effectiveCutLength, woLengths, mainLengths);

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
            IsPreCut = entity.IsPreCut,
            LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(entity.LengthStatus),
            CuttingMultiple = entity.CuttingMultiple,
            FinishedCutLength = entity.FinishedCutLength,
            CutLengthMatchType = EnumHelper.TryParse<MES.Core.Enums.CutLengthMatchType>(entity.CutLengthMatchType),
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
        await TryRefreshListSummaryAsync(batchId);
    }

    /// <summary>
    /// 刷新用料计划总览（WorkOrderListSummary）：生产记录删除经批次状态重算
    /// （Completed 判定）影响产能工量 completedOutput，须联动刷新
    /// </summary>
    private async Task TryRefreshListSummaryAsync(int batchId)
    {
        if (_listSummaryService == null) return;
        try
        {
            var salesOrderNo = await _context.ProductionBatches.AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.SalesOrderNo)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(salesOrderNo))
                await _listSummaryService.RefreshBySalesOrderAsync(salesOrderNo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "用料计划总览刷新失败（不影响主流程）: BatchId={BatchId}", batchId);
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
        if (batchIds.Count == 0) return;

        // 分片处理：防止单条 SQL 的 IN 参数超过 SQL Server 2100 参数上限（一键修复等全量场景）
        // 强制完成分支内部已分片，此处对活跃批次主路径统一兜底；正常单批次/小批量走单片路径行为不变
        foreach (var chunk in batchIds.Chunk(1000))
        {
            await BatchUpdateTrackingFromRecordsAsync(chunk);
            await TryRefreshExecutionSummaryByBatchIdsAsync(chunk);
        }
    }

    /// <summary>
    /// 重算某批次全部生产记录的定尺切割长度匹配标识（CutLengthMatchType）
    /// 供批次编辑（LengthStatus/工单号等上游字段变更）后级联调用
    /// </summary>
    public async Task<int> RecomputeCutLengthMatchByBatchAsync(int batchId)
    {
        var records = await _context.ProductionRecords
            .Where(r => r.ProductionBatchId == batchId)
            .ToListAsync();
        if (records.Count == 0) return 0;

        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.Id == batchId)
            .FirstOrDefaultAsync();
        if (batch == null) return 0;

        var maps = await _fixedLengthWorkOrderService.GetLengthMapsAsync();
        var updated = 0;
        foreach (var r in records)
        {
            var newValue = ComputeCutLengthMatch(
                r.ProductStatus, r.LengthStatus, r.IsPreCut == true, r.FinishedCutLength,
                maps.ByWorkOrderNo.GetValueOrDefault(batch.WorkOrderNo, new HashSet<decimal>()),
                maps.ByMainKey.GetValueOrDefault($"{batch.SalesOrderNo.Trim()}|{batch.ProductionMainNo.Trim()}", new HashSet<decimal>()));
            if (r.CutLengthMatchType != newValue)
            {
                r.CutLengthMatchType = newValue;
                updated++;
            }
        }
        if (updated > 0)
            await _context.SaveChangesAsync();
        return updated;
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

        // 3e. 加载仓库入库记录（按批次号匹配，物料类型在内存中动态判定）
        var inventoryBatches = await _context.InventoryBatches
            .Include(ib => ib.Warehouse)
            .Where(ib => ib.ProductionBatchNo == batch.BatchNo)
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
                // 记录/委外/检验/入缸的 SectionName 存英文 Key；显示名（中文）先归一为 Key 再匹配
                var key = (pg.Id, SectionKeys.ToKey(sectionName)!);
                var hasRecord = recordByKey.TryGetValue(key, out var record);
                var hasOutsource = outsourceByKey.TryGetValue(key, out var outsource);
                var hasPickling = picklingByKey.TryGetValue(key, out var pickling);
                var hasInspection = inspectionByKey.TryGetValue(key, out var insp);

                // 检验到料匹配：工序组有成检到料记录 → 该组"检验"工段即为完成
                var hasMaterialCheck = sectionName == SectionDefs.Inspection
                    && materialCheckPgIds.Contains(pg.Id);

                // 仓库入库匹配：该工段为"入库"且有匹配的库存批次记录
                // 有效投料重量>0时需物料类型一致（排除次品入库），=0时全匹配（全次品场景）
                var hasWarehouse = sectionName == SectionDefs.Warehouse && inventoryBatches.Count > 0
                    && (batch.CurrentValidWeight <= 0 || inventoryBatches.Any(ib => ib.MaterialType == batch.ManufacturingItem));

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
            .Count(pg => pg.ProcessName != ProcessKeys.InProcessRepair
                && pg.ProcessName != ProcessKeys.AdditionalFinalInspection
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
        var groupDiscountRate = await GetConfigAsync("ProcessingDiscount", "GroupDiscountRate", 0.025m);
        var processInspectionNeedAdjustRatio = await GetConfigAsync("ProductionThreshold", "ProcessInspectionNeedAdjustRatio", 0.03m);
        var cutDoubtRatio = await GetConfigAsync("ProductionThreshold", "CutDoubtRatio", 0.05m);

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

            // 3e. 加载仓库入库记录（按批次号匹配，物料类型在内存中动态判定）
            var inventoryBatches = await _context.InventoryBatches
                .Include(ib => ib.Warehouse)
                .Where(ib => ib.ProductionBatchNo == batch.BatchNo)
                .OrderByDescending(ib => ib.InboundDate)
                .ToListAsync();

            // 仓库入库动态匹配：有效投料重量>0时需物料类型一致（排除次品入库），=0时全匹配（全次品场景）
            bool hasWarehouse = batch.CurrentValidWeight > 0
                ? inventoryBatches.Any(ib => ib.MaterialType == batch.ManufacturingItem)
                : inventoryBatches.Count > 0;
            if (hasMaterialCheck)
            {
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

            // 收集该批次的所有工段委外（含待回收和已回收）及各自的回收记录数与纯合格回收重量
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
                    s.IsInternal,
                    RecoveryCount = s.OutsourceRecoveries.Count,
                    RecoveryWeight = s.OutsourceRecoveries.Sum(r => r.RecoveryWeight ?? 0),
                    MaxRecoveryDate = s.OutsourceRecoveries.Select(r => (DateTime?)r.RecoveryDate).Max()
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
                .Include(p => p.PicklingOutRecords)
                .Where(p => p.ProductionBatchId == batchId)
                .OrderBy(p => p.SequenceNumber)
                .ThenBy(p => p.InDate)
                .ToListAsync();

            var hasRecords = productionRecords.Count > 0 || sectionOutsources.Count > 0 || processInspections.Count > 0 || picklingInRecords.Count > 0;

            // ====== 1. 状态（临时，供 ComputeBatchTrackingCore 计算剩余工量使用） ======
            // 挂起/强制完成状态不自动覆盖；检验到料已完成的批次保持 Completed
            // 无检验到料时先按"在产/未产"计，ComputeBatchTrackingCore 之后按"到达成检门"再定稿
            if (batch.Status != BatchStatus.Suspended && !hasMaterialCheck)
            {
                batch.Status = hasRecords ? BatchStatus.InProgress : BatchStatus.None;
            }

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
                s.ProcessName, s.OutsourceVendor, s.SendOutDate, s.RecoveryCount,
                s.RecoveryWeight, s.IsInternal, s.MaxRecoveryDate
            )).ToList();

            // 公共跟踪计算（除投料变更外）
            var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(batch.PlantGrade);
            var dsExtraDaysMap = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
            ComputeBatchTrackingCore(batch, pgSpecLookup, productionRecords, outsourceInfos,
                processInspections, picklingInRecords, hasMaterialCheck,
                materialChecks.Count > 0 ? materialChecks.Max(m => (DateTime?)m.ReceiveDate) : null,
                materialCheckSeq, materialCheckPg, coldRollCompleteRatio, dayMap, dsExtraDaysMap);

            // ====== 1b. 状态定稿：无检验到料/无仓库入库时，下工段为成品检验且前段已完工 → 成检 ======
            if (batch.Status != BatchStatus.Suspended && !hasMaterialCheck && !hasWarehouse
                && ReachedFinalInspectionGate(batch, hasRecords))
            {
                batch.Status = BatchStatus.InFinalInspection;
                batch.RemainingWorkDays = 0; // 成检无剩余工量（对齐 CalculateRemainingWorkDays）
            }

            // ====== 仓库入库覆盖：入库后当前工段为"入库"，无下一工段 ======
            if (hasWarehouse)
            {
                var latestInbound = batch.CurrentValidWeight > 0
                    ? inventoryBatches.First(ib => ib.MaterialType == batch.ManufacturingItem)
                    : inventoryBatches[0];
                batch.CurrentSectionName = SectionKeys.Warehouse; // "入库"
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

            // ====== 8. 投料变更：比较有效投料支数与领料支数是否一致 ======
            batch.HasInputChange = batch.InputQuantity.HasValue && batch.CurrentValidQty.HasValue
                && batch.InputQuantity.Value != batch.CurrentValidQty.Value;

            // ====== 9. 理论成品量计算 ======
            ComputeTheoreticalOutput(batch, groupDiscountRate);

            // ====== 10. 成检附加（仅"成检"状态有效）+ 成切跟踪计算（依赖理论成品支）+ 过程检字段 ======
            // 成检附加描述"成检"阶段的性质（预检/终检），仅 Status=成检 时计算，其余状态置空
            batch.InspectionStage = batch.Status == BatchStatus.InFinalInspection
                ? ComputeInspectionStage(materialChecks)
                : null;
            ComputeCutTracking(batch, productionRecords, cutDoubtRatio);
            ComputeProcessInspectionFields(batch, processInspections, processInspectionNeedAdjustRatio);
            ComputeProductUnitWeight(batch);

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
    /// 判定批次是否"到达成检门"：下工段为"检验"且属成品规格组（ManufacturingSpec == 批次规格），
    /// 且上工段为空（尚未开工）或上工段已完工。
    /// 供无检验到料/无仓库入库的批次做状态归属：到达成检门 → 成检，否则未产/在产。
    /// 依赖 ComputeBatchTrackingCore 输出的 NextSectionName / CorrespondingSpec / CurrentSectionCompleted。
    /// </summary>
    private static bool ReachedFinalInspectionGate(ProductionBatch batch, bool hasRecords)
    {
        // 仅成品类制造物品（订单成品/备料成品/临界成品/非交付态）才存在"成品检验"环节。
        // 非成品类（余库料/半成品/荒管等在制流转料）即使检验工段 ManufacturingSpec==Specification，
        // 也属"过程检验"，永不进成检（否则"在产"批次会被误归"成检"）。
        if (!ProductStatusHelper.IsFinishedManufacturingItem(batch.ManufacturingItem)) return false;
        // 下工段为"检验"（英文 Key），且该检验属成品规格组（成品检验，区别于过程检验）
        if (batch.NextSectionName != SectionKeys.Inspection) return false;
        if (!string.Equals(batch.CorrespondingSpec, batch.Specification, StringComparison.OrdinalIgnoreCase)) return false;
        // 上工段为空（无记录）或上工段已完工
        return !hasRecords || batch.CurrentSectionCompleted == true;
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
                .Count(pg => pg.ProcessName != ProcessKeys.InProcessRepair
                    && pg.ProcessName != ProcessKeys.AdditionalFinalInspection
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
    /// 成检附加计算：依据成检到料记录的 InspectionType（PreInspection=预检，FormalInspection=终检）
    /// 无到料 → null；含正式检验（或 InspectionType 为空/空白 → 按终检保守）→ FormalInspection（终检）；仅预成检 → PreInspection（预检）
    /// </summary>
    private static string? ComputeInspectionStage(IEnumerable<MaterialReceiveCheck>? materialChecks)
    {
        if (materialChecks == null) return null;
        var list = materialChecks.ToList();
        if (list.Count == 0) return null;

        var hasFormal = list.Any(m =>
            string.IsNullOrWhiteSpace(m.InspectionType)
            || string.Equals(m.InspectionType, nameof(InspectionType.FormalInspection), StringComparison.OrdinalIgnoreCase));
        return hasFormal ? nameof(InspectionType.FormalInspection) : nameof(InspectionType.PreInspection);
    }

    /// <summary>
    /// 成切跟踪计算（单批次/批量共用）
    /// 依赖顺序：必须先执行 ComputeTheoreticalOutput 取得理论成品支，再调用本方法。
    /// 判定标准：
    ///   成品关联的工序 = ManufacturingSpec == 成品规格（批次 Specification）的工序组（可能不止一个，均属成品工序）
    ///   成切需求 = 任一成品工序组内有「断切」工段
    ///   成切执行 = 需求=否→null；成品工序组内已有断切生产记录→是；否则→否
    ///   成切支数 = 「产类=成品」的断切生产记录汇总（无→null），口径与成检追踪「生产支数」第2/3种一致：
    ///   批次长度状态=定尺→PostCutQuantity（切后支数）；非定尺→Quantity（加工支数）
    ///   成切存疑 = 需求=否或执行=否→null；|成切支数−理论成品支|/理论成品支&gt;5%→疑问；否则→正常；理论成品支不可得→null
    ///   疑问-缺少（缺正式成切记录）：无正式断切记录且批次已到成检/完成且非强制完成。
    ///     状态=成检 且 成检附加=预检（仅预成检流程）→ 正常（正式切割留待正式成检，非缺失）
    ///     强制完成 → 略（人控短路）
    /// </summary>
    private static void ComputeCutTracking(ProductionBatch batch, List<ProductionRecord> productionRecords, decimal cutDoubtRatio)
    {
        // 成品关联的工序：ManufacturingSpec == 成品规格(batch.Specification) 的工序组（可能多个）
        var finishedPgIds = batch.ProcessGroups?
            .Where(pg => string.Equals(pg.ManufacturingSpec, batch.Specification, StringComparison.OrdinalIgnoreCase))
            .Select(pg => pg.Id)
            .ToHashSet() ?? new HashSet<int>();

        // 成切需求：任一成品工序组内有「断切」工段
        var hasCutSection = finishedPgIds.Count > 0
            && batch.ProcessGroups!.Any(pg => finishedPgIds.Contains(pg.Id)
                && GetSectionsFromProcessGroup(pg).Any(s => s.SectionName == SectionDefs.Cut));

        // 成品工序组内的「断切」生产记录（预成切不是正式成品切割，不算"已成切"，排除）
        var cutRecords = finishedPgIds.Count > 0
            ? productionRecords
                .Where(r => finishedPgIds.Contains(r.ProcessGroupId)
                    && r.SectionName == SectionKeys.Cut
                    && r.IsPreCut != true)
                .ToList()
            : new List<ProductionRecord>();

        batch.CutRequirement = hasCutSection;
        batch.CutExecution = hasCutSection ? (cutRecords.Count > 0 ? true : false) : null;

        // 汇总字段：批次长度状态=定尺→切后支数(PostCutQuantity)；非定尺→加工支数(Quantity)
        // 仅统计「产类=成品」的断切记录（不限工序组），口径与成检追踪「生产支数」第2/3种完全一致
        // 预成切(IsPreCut=true)不是正式成品切割，不计入成切支数
        var isFixedLength = string.Equals(batch.LengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase);
        var finishedCutRecords = productionRecords
            .Where(r => r.SectionName == SectionKeys.Cut && r.ProductStatus == ProductStatuses.Finished && r.IsPreCut != true)
            .ToList();
        batch.CutQuantity = finishedCutRecords.Count > 0
            ? finishedCutRecords
                .Where(r => isFixedLength ? r.PostCutQuantity.HasValue : r.Quantity.HasValue)
                .Sum(r => isFixedLength ? r.PostCutQuantity!.Value : r.Quantity!.Value)
            : null;

        // 成切存疑
        if (!hasCutSection)
            batch.CutDoubt = null; // 无需求 → 略
        else if (cutRecords.Count == 0)
        {
            // 成切执行=否：需求有但无正式断切记录
            var reachedFinishedStage = batch.Status is BatchStatus.InFinalInspection or BatchStatus.Completed;
            if (batch.IsForceCompleted)
            {
                // 强制完成：人控短路，不判缺失
                batch.CutDoubt = null;
            }
            else if (batch.Status == BatchStatus.InFinalInspection
                && string.Equals(batch.InspectionStage, nameof(InspectionType.PreInspection), StringComparison.OrdinalIgnoreCase))
            {
                // 成检+成检附加=预检：仅预成切流程，正式成切留待正式成检，属正常，非"缺失"
                batch.CutDoubt = CutDoubtType.Normal;
            }
            else
            {
                // 批次已到成检/完成 且 非强制完成 → 疑问-缺少（缺失正式成品切割记录）
                batch.CutDoubt = reachedFinishedStage ? CutDoubtType.MissingRecords : null;
            }
        }
        else if (batch.TheoreticalOutputQty.HasValue && batch.TheoreticalOutputQty.Value > 0 && batch.CutQuantity.HasValue)
        {
            var diff = Math.Abs(batch.CutQuantity.Value - batch.TheoreticalOutputQty.Value);
            var ratio = (decimal)diff / batch.TheoreticalOutputQty.Value;
            batch.CutDoubt = ratio > cutDoubtRatio ? CutDoubtType.QuantityMismatch : CutDoubtType.Normal;
        }
        else
        {
            // 理论成品支不可得 → 无判定依据
            batch.CutDoubt = null;
        }
    }

    /// <summary>
    /// 过程检字段计算（单批次/批量共用，持久化到批次表）
    /// 依赖顺序：必须先执行 ComputeTheoreticalOutput 取得理论成品支/单支重，再调用本方法。
    /// 口径（与批次首页「有效投料变更」组的投料变更决策辅助一致）：
    ///   缺陷-返整量 = 全部过程检验 理论返整重 求和（返整会另开批次，不延续本批）
    ///   缺陷-纯次品量 = 全部过程检验 理论报废重+理论入库重 求和（彻底退出正常流）
    ///   过程检合格支/合格量 = 当前执行工序组（CurrentGroupName 匹配）全部检验 合格支/合格量 求和；无 → null
    ///   过程检理论成品支 = Round(合格量 ÷ 合格支 ÷ 成品的理论单支重, AwayFromZero) × 合格支（重量口径折算）
    ///   需调整 = 批次状态为 成检/完成 时固定 null；其余 过程检理论成品支 与 当前理论成品支 偏差 &gt; 配置阈值 ProcessInspectionNeedAdjustRatio（默认 3%）→ true；否则/无数据 → null
    /// </summary>
    private static void ComputeProcessInspectionFields(ProductionBatch batch, List<ProcessInspection> processInspections, decimal processInspectionNeedAdjustRatio)
    {
        // 缺陷量：全量累计（不限工序组）
        batch.ProcessInspectionReworkWeight = processInspections.Sum(p => p.TheoreticalReworkWeight ?? 0);
        batch.ProcessInspectionScrapWeight = processInspections.Sum(p => (p.TheoreticalWarehouseWeight ?? 0) + (p.TheoreticalScrapWeight ?? 0));

        // 合格支/合格量：当前执行工序组聚合
        int qty = 0;
        decimal weight = 0m;
        if (!string.IsNullOrEmpty(batch.CurrentGroupName))
        {
            var currentGroupRows = processInspections
                .Where(p => string.Equals(p.ProcessName, batch.CurrentGroupName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (currentGroupRows.Count > 0)
            {
                qty = currentGroupRows.Sum(p => p.QualifiedQuantity ?? 0);
                weight = currentGroupRows.Sum(p => p.QualifiedWeight ?? 0);
            }
        }
        batch.ProcessInspectionQualifiedQty = qty > 0 ? qty : (int?)null;
        batch.ProcessInspectionQualifiedWeight = weight > 0 ? weight : (decimal?)null;

        // 过程检理论成品支：重量口径折算
        int? theoQty = null;
        if (qty > 0 && weight > 0 && batch.TheoreticalUnitWeight is { } uw && uw > 0)
            theoQty = (int)Math.Round(weight / qty / uw, 0, MidpointRounding.AwayFromZero) * qty;
        batch.ProcessInspectionTheoreticalQty = theoQty;

        // 需调整：成检/完成固定 null；其余与当前理论成品支偏差 > 3% → true
        batch.ProcessInspectionNeedAdjust =
            batch.Status != BatchStatus.InFinalInspection && batch.Status != BatchStatus.Completed
            && theoQty is { } tq && batch.TheoreticalOutputQty is { } toq && toq > 0
                ? Math.Abs(tq - toq) / (decimal)toq > processInspectionNeedAdjustRatio
                : (bool?)null;
    }

    /// <summary>
    /// 产品单支量计算（单批次/批量共用，持久化到批次表）
    /// 口径：仅"定尺"批次有效 = 总重量/总支数，保留1位小数；非定尺 → null
    /// </summary>
    private static void ComputeProductUnitWeight(ProductionBatch batch)
    {
        if (string.Equals(batch.LengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase)
            && batch.TotalWeight > 0 && batch.TotalQuantity > 0)
            batch.ProductUnitWeight = Math.Round(batch.TotalWeight / batch.TotalQuantity, 1, MidpointRounding.AwayFromZero);
        else
            batch.ProductUnitWeight = null;
    }

    /// <summary>
    /// 批量刷新多个批次的跟踪字段
    /// 一次查询所有数据，内存分组计算，一次SaveChanges
    /// </summary>
    private async Task BatchUpdateTrackingFromRecordsAsync(ICollection<int> batchIds)
    {
        if (batchIds.Count == 0) return;

        var coldRollCompleteRatio = await GetConfigAsync("ProductionThreshold", "ColdRollCompleteRatio", 0.95m);
        var groupDiscountRate = await GetConfigAsync("ProcessingDiscount", "GroupDiscountRate", 0.025m);
        var processInspectionNeedAdjustRatio = await GetConfigAsync("ProductionThreshold", "ProcessInspectionNeedAdjustRatio", 0.03m);
        var cutDoubtRatio = await GetConfigAsync("ProductionThreshold", "CutDoubtRatio", 0.05m);

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

        // 拆分批次：活跃批次（非强制完成）走全量跟踪；强制完成批次仅重算全工量/理论成品/成切跟踪
        var activeBatchIds = batchDict.Keys
            .Where(id => !batchDict[id].IsForceCompleted)
            .ToList();
        var forceCompletedBatchIds = batchDict.Keys
            .Where(id => batchDict[id].IsForceCompleted)
            .ToList();

        // 交货状态附加天数映射（全量跟踪与强制完成重算共用）
        var dsExtraDaysMap = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();

        // 强制完成批次：仅计算全工量 + 理论成品 + 成切跟踪，不改变活跃跟踪字段
        // 无论是否与活跃批次混合都会执行，避免混合场景下被主分支跳过
        if (forceCompletedBatchIds.Count > 0)
        {
            // 加载这些强制完成批次的生产记录（成切跟踪用，分片避免 SQL Server 2100 参数上限）
            var fcRecords = new List<ProductionRecord>();
            for (var i = 0; i < forceCompletedBatchIds.Count; i += 1000)
            {
                var ids = forceCompletedBatchIds.Skip(i).Take(1000).ToList();
                fcRecords.AddRange(await _context.ProductionRecords
                    .Where(r => ids.Contains(r.ProductionBatchId))
                    .ToListAsync());
            }
            var fcRecordsByBatch = fcRecords.GroupBy(r => r.ProductionBatchId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 加载这些强制完成批次的过程检验（过程检字段持久化用，分片避免 SQL Server 2100 参数上限）
            var fcInspections = new List<ProcessInspection>();
            for (var i = 0; i < forceCompletedBatchIds.Count; i += 1000)
            {
                var ids = forceCompletedBatchIds.Skip(i).Take(1000).ToList();
                fcInspections.AddRange(await _context.ProcessInspections
                    .Where(p => ids.Contains(p.ProductionBatchId))
                    .ToListAsync());
            }
            var fcInspectionsByBatch = fcInspections.GroupBy(p => p.ProductionBatchId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var id in forceCompletedBatchIds)
            {
                var b = batchDict[id];
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
                    dsExtraDaysMap,
                    b.DeliveryState);
                ComputeTheoreticalOutput(b, groupDiscountRate);
                b.InspectionStage = b.Status == BatchStatus.InFinalInspection
                    ? ComputeInspectionStage(materialCheckLookup.GetValueOrDefault(b.Id) ?? new())
                    : null;
                // 强制完成批次不判疑问-缺少（IsForceCompleted 短路），无需成检类型
                ComputeCutTracking(b, fcRecordsByBatch.GetValueOrDefault(b.Id) ?? new(), cutDoubtRatio);
                // 过程检字段（强制完成批次不重算活跃跟踪，但过程检缺陷/合格聚合独立于工段跟踪）
                ComputeProcessInspectionFields(b, fcInspectionsByBatch.GetValueOrDefault(b.Id) ?? new(), processInspectionNeedAdjustRatio);
                // 产品单支量（依赖批次自身 TotalWeight/TotalQuantity/LengthStatus）
                ComputeProductUnitWeight(b);
            }
        }

        if (activeBatchIds.Count == 0)
        {
            // 全部为强制完成批次：保存后直接返回
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
                s.IsInternal,
                RecoveryCount = s.OutsourceRecoveries.Count,
                RecoveryWeight = s.OutsourceRecoveries.Sum(r => r.RecoveryWeight ?? 0),
                MaxRecoveryDate = s.OutsourceRecoveries.Select(r => (DateTime?)r.RecoveryDate).Max()
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
            .Include(p => p.PicklingOutRecords)
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
        // 复合键集合：有效投料>0时需精确匹配"批次号|物料类型"
        var warehouseKeySet = new HashSet<string>(
            warehouseBatchEntries.Where(x => x.ProductionBatchNo != null)
                .Select(x => $"{x.ProductionBatchNo}|{x.MaterialType}"),
            StringComparer.OrdinalIgnoreCase);
        // 纯批次号集合：有效投料=0（全次品）时只需匹配批次号
        var warehouseBatchOnlySet = new HashSet<string>(
            warehouseBatchEntries.Where(x => x.ProductionBatchNo != null)
                .Select(x => x.ProductionBatchNo!),
            StringComparer.OrdinalIgnoreCase);

        // 7. 逐批次计算跟踪字段
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
            // 仓库入库动态匹配：有效投料重量>0时需物料类型一致（排除次品入库），=0时全匹配（全次品场景）
            var hasWarehouse = batch.BatchNo != null && (batch.CurrentValidWeight > 0
                ? warehouseKeySet.Contains($"{batch.BatchNo}|{batch.ManufacturingItem}")
                : warehouseBatchOnlySet.Contains(batch.BatchNo));
            if (hasCheck && hasWarehouse)
            {
                // 同时有成检到料和仓库入库记录 → 完成
                if (batch.Status != BatchStatus.Completed)
                    batch.Status = BatchStatus.Completed;
            }
            else if (hasCheck)
            {
                // 只有成检到料，无仓库入库 → 成检
                if (batch.Status != BatchStatus.InFinalInspection)
                    batch.Status = BatchStatus.InFinalInspection;
            }
            else if (batch.Status != BatchStatus.Suspended)
            {
                // 无检验到料：人工暂停的批次不覆盖状态（对齐单批次刷新路径）
                // 先按"在产/未产"计，ComputeBatchTrackingCore 之后按"到达成检门"再定稿
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
                s.ProcessName, s.OutsourceVendor, s.SendOutDate, s.RecoveryCount,
                s.RecoveryWeight, s.IsInternal, s.MaxRecoveryDate
            )).ToList();

            // 公共跟踪计算（除投料变更外）
            var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(batch.PlantGrade);
            ComputeBatchTrackingCore(batch, pgSpecLookup, productionRecords, outsourceInfos,
                processInspections, picklingInRecords, hasCheck,
                hasCheck && batchMaterialChecks != null ? batchMaterialChecks.Max(m => (DateTime?)m.ReceiveDate) : null,
                materialCheckSeq, materialCheckPg,
                coldRollCompleteRatio, dayMap, dsExtraDaysMap);

            // 状态定稿：无检验到料/无仓库入库时，下工段为成品检验且前段已完工 → 成检（对齐单批次刷新路径）
            if (batch.Status != BatchStatus.Suspended && !hasCheck && !hasWarehouse
                && ReachedFinalInspectionGate(batch, hasRecords))
            {
                batch.Status = BatchStatus.InFinalInspection;
                batch.RemainingWorkDays = 0;
            }

            // 仓库入库覆盖：入库后当前工段为"入库"，无下一工段（批量模式）
            if (hasWarehouse)
            {
                batch.CurrentSectionName = SectionKeys.Warehouse;
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

            // 投料变更：比较有效投料支数与领料支数是否一致
            batch.HasInputChange = batch.InputQuantity.HasValue && batch.CurrentValidQty.HasValue
                && batch.InputQuantity.Value != batch.CurrentValidQty.Value;
            ComputeTheoreticalOutput(batch, groupDiscountRate);

            // 成切跟踪计算（依赖理论成品支）
            batch.InspectionStage = batch.Status == BatchStatus.InFinalInspection
                ? ComputeInspectionStage(batchMaterialChecks)
                : null;
            ComputeCutTracking(batch, productionRecords, cutDoubtRatio);
            ComputeProcessInspectionFields(batch, processInspections, processInspectionNeedAdjustRatio);
            ComputeProductUnitWeight(batch);
        }

        _context.ProductionBatches.UpdateRange(batchDict.Values);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 公共跟踪计算核心（两个批次模式下共享）
    /// 计算：当前工段/工序/设备/委外/规格、截止执行日、完工状态、下一工段、剩余工量、全工量
    /// 不包含：投料变更（由调用方按各自逻辑计算）
    /// </summary>
    private static void ComputeBatchTrackingCore(
        ProductionBatch batch,
        Dictionary<int, string> pgSpecLookup,
        List<ProductionRecord> productionRecords,
        List<SectionOutsourceInfo> sectionOutsources,
        List<ProcessInspection> processInspections,
        List<PicklingInRecord> picklingInRecords,
        bool hasMaterialCheck,
        DateTime? materialCheckDate,
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

        // ====== 当前工段是否完工（前移：完工后当前设备/委外均清空） ======
        if (overallMaxSeq < 0)
        {
            batch.CurrentSectionCompleted = null;
        }
        else if (overallMaxSeq == maxRecordSeq && maxSeqRecord?.SectionName == SectionKeys.ColdRollDraw)
        {
            // 冷轧拔：生产记录重量 + Σ纯合格回收重量(RecoveryWeight，不含未加工退回) ≥ 有效原料重量 × 95% 才算完工
            var pgId = maxSeqRecord.ProcessGroupId;
            var producedWeight = productionRecords
                .Where(r => r.ProcessGroupId == pgId && r.SectionName == SectionKeys.ColdRollDraw && r.Weight.HasValue)
                .Sum(r => r.Weight!.Value);
            var recoveredWeight = sectionOutsources
                .Where(o => o.ProcessGroupId == pgId && o.SectionName == SectionKeys.ColdRollDraw)
                .Sum(o => o.RecoveryWeight);
            var totalWeight = producedWeight + recoveredWeight;
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
            // 其它工段（含过程检验/检验到料）：有记录即完工
            batch.CurrentSectionCompleted = true;
        }

        // ====== 截止执行日：生产/委外/过程检验/到料/入缸 五路日期取最大 ======
        // 委外/酸洗日期口径（A）：已完工取回收日/完工日，未完工取发出日/入缸日
        batch.CurrentExecDate = new[]
        {
            maxSeqRecord?.ExecDate,
            (maxSeqOutsource?.RecoveryCount > 0 && maxSeqOutsource?.MaxRecoveryDate is { } outRecDate) ? outRecDate : maxSeqOutsource?.SendOutDate,
            maxSeqInspection?.InspectionDate,
            hasMaterialCheck ? materialCheckDate : null,
            (maxSeqPickling?.Status == PicklingStatus.Completed && maxSeqPickling.PicklingOutRecords.Count > 0)
                ? maxSeqPickling.PicklingOutRecords.Max(o => (DateTime?)o.CompleteDate)
                : maxSeqPickling?.InDate
        }.Max();

        // ====== 当前工段/工序/规格（按 overallMaxSeq 唯一归因，与下一工段/剩余工量同口径） ======
        if (overallMaxSeq == maxRecordSeq)
        {
            batch.CurrentGroupName = maxSeqRecord?.ProcessName;
            batch.CurrentSectionName = maxSeqRecord?.SectionName;
            batch.CurrentSpec = maxSeqRecord != null
                ? pgSpecLookup.GetValueOrDefault(maxSeqRecord.ProcessGroupId)
                : null;
        }
        else if (overallMaxSeq == maxOutsourceSeq)
        {
            batch.CurrentGroupName = maxSeqOutsource!.ProcessName;
            batch.CurrentSectionName = maxSeqOutsource.SectionName;
            batch.CurrentSpec = pgSpecLookup.GetValueOrDefault(maxSeqOutsource.ProcessGroupId);
        }
        else if (overallMaxSeq == maxPicklingSeq)
        {
            batch.CurrentGroupName = maxSeqPickling?.ProcessName;
            batch.CurrentSectionName = maxSeqPickling?.SectionName;
            batch.CurrentSpec = maxSeqPickling != null
                ? pgSpecLookup.GetValueOrDefault(maxSeqPickling.ProcessGroupId)
                : null;
        }
        else if (overallMaxSeq == materialCheckSeq && hasMaterialCheck)
        {
            batch.CurrentGroupName = materialCheckPg?.ProcessName;
            batch.CurrentSectionName = SectionKeys.Inspection;
            batch.CurrentSpec = materialCheckPg != null
                ? pgSpecLookup.GetValueOrDefault(materialCheckPg.Id)
                : null;
        }
        else
        {
            batch.CurrentGroupName = maxSeqInspection?.ProcessName;
            batch.CurrentSectionName = maxSeqInspection?.SectionName;
            batch.CurrentSpec = maxSeqInspection != null
                ? pgSpecLookup.GetValueOrDefault(maxSeqInspection.ProcessGroupId)
                : null;
        }

        // ====== 当前设备 / 当前委外（独立维度互不覆盖；工段完工后均清空） ======
        if (batch.CurrentSectionCompleted == true)
        {
            batch.CurrentEquipmentName = null;
            batch.CurrentOutsource = null;
        }
        else
        {
            // 当前设备：设备来源（生产记录/入缸/过程检验）中序号最大者的设备名
            string? deviceName = null;
            var bestDeviceSeq = -1;
            foreach (var (seq, name) in new (int Seq, string? Name)[]
            {
                (maxRecordSeq, maxSeqRecord?.EquipmentName),
                (maxPicklingSeq, maxSeqPickling?.EquipmentName),
                (maxInspectionSeq, maxSeqInspection?.EquipmentName)
            })
            {
                if (seq > bestDeviceSeq && !string.IsNullOrWhiteSpace(name))
                {
                    bestDeviceSeq = seq;
                    deviceName = name;
                }
            }
            batch.CurrentEquipmentName = deviceName;

            // 当前委外：序号最大的委外记录，未回收则显示单位（与完工判定同源）
            batch.CurrentOutsource = maxSeqOutsource?.RecoveryCount == 0
                ? maxSeqOutsource.OutsourceVendor
                : null;
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
                var firstSections = firstPg.GetNonEmptySectionKeys();
                var firstSection = firstSections.OrderBy(s => s.SequenceNumber).FirstOrDefault();
                batch.NextSectionName = firstSection.SectionKey;
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
            batch.NextSectionName = nextSection != null ? SectionKeys.ToKey(nextSection.SectionName) : null;
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
                || (r.ProductionBatch.WorkOrderNo != null && r.ProductionBatch.WorkOrderNo.Contains(query.Keyword))
                || (r.ProductionBatch.SalesOrderNo != null && r.ProductionBatch.SalesOrderNo.Contains(query.Keyword))
                || (r.ProductionBatch.ProductionMainNo != null && r.ProductionBatch.ProductionMainNo.Contains(query.Keyword))
                || r.ProcessName.Contains(query.Keyword)
                || r.SectionName.Contains(query.Keyword)
                || (r.ManufacturingSpec != null && r.ManufacturingSpec.Contains(query.Keyword))
                || (r.EquipmentName != null && r.EquipmentName.Contains(query.Keyword))
                || (r.Operator != null && r.Operator.Contains(query.Keyword))
                || (r.Shift != null && r.Shift.Contains(query.Keyword))
                || (r.TagNo != null && r.TagNo.Contains(query.Keyword))
                || (r.PlantGrade != null && r.PlantGrade.Contains(query.Keyword))
                || (r.LengthStatus != null && r.LengthStatus.Contains(query.Keyword))
                || (r.Remark != null && r.Remark.Contains(query.Keyword))
                || (r.DataSource != null && r.DataSource.Contains(query.Keyword)));
        }

        if (query.ExecDateFrom.HasValue)
            queryable = queryable.Where(r => r.ExecDate >= query.ExecDateFrom.Value);

        if (query.ExecDateTo.HasValue)
            queryable = queryable.Where(r => r.ExecDate <= query.ExecDateTo.Value);

        // 处理批次导航属性筛选（ProductionRecord 实体无 BatchNo/WorkOrderNo/SalesOrderNo/ProductionMainNo 属性，ApplyFilters 反射不到）
        if (query.Filters != null)
        {
            var batchNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("BatchNo", StringComparison.OrdinalIgnoreCase));
            if (batchNoFilter != null && batchNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.ProductionBatch != null
                    && batchNoFilter.Values.Contains(r.ProductionBatch.BatchNo));
                query.Filters.Remove(batchNoFilter);
            }

            var workOrderNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("WorkOrderNo", StringComparison.OrdinalIgnoreCase));
            if (workOrderNoFilter != null && workOrderNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.ProductionBatch != null
                    && r.ProductionBatch.WorkOrderNo != null
                    && workOrderNoFilter.Values.Contains(r.ProductionBatch.WorkOrderNo));
                query.Filters.Remove(workOrderNoFilter);
            }

            var salesOrderNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("SalesOrderNo", StringComparison.OrdinalIgnoreCase));
            if (salesOrderNoFilter != null && salesOrderNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.ProductionBatch != null
                    && r.ProductionBatch.SalesOrderNo != null
                    && salesOrderNoFilter.Values.Contains(r.ProductionBatch.SalesOrderNo));
                query.Filters.Remove(salesOrderNoFilter);
            }

            var productionMainNoFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("ProductionMainNo", StringComparison.OrdinalIgnoreCase));
            if (productionMainNoFilter != null && productionMainNoFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(r => r.ProductionBatch != null
                    && r.ProductionBatch.ProductionMainNo != null
                    && productionMainNoFilter.Values.Contains(r.ProductionBatch.ProductionMainNo));
                query.Filters.Remove(productionMainNoFilter);
            }
        }

        queryable = queryable.ApplyFilters(query.Filters);
        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.SortBy ?? "createdtime", query.IsDescending);

        var items = (await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new
            {
                r.Id,
                r.ProductionBatchId,
                r.ProcessGroupId,
                r.ProcessName,
                r.ManufacturingSpec,
                r.SectionName,
                r.SequenceNumber,
                r.ExecDate,
                r.EquipmentName,
                r.Operator,
                r.Shift,
                r.Quantity,
                r.Weight,
                r.SolutionTemperature,
                r.SoakTime,
                r.ProductStatus,
                r.IsPreCut,
                r.LengthStatus,
                r.CuttingMultiple,
                r.FinishedCutLength,
                r.CutLengthMatchType,
                r.PostCutQuantity,
                r.FaceCutCount,
                r.TagNo,
                r.PlantGrade,
                r.Remark,
                r.DataSource,
                BatchNo = r.ProductionBatch.BatchNo,
                WorkOrderNo = r.ProductionBatch.WorkOrderNo,
                SalesOrderNo = r.ProductionBatch.SalesOrderNo,
                ProductionMainNo = r.ProductionBatch.ProductionMainNo,
                r.CreatedTime,
                r.UpdatedTime
            })
            .ToListAsync())
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
                IsPreCut = r.IsPreCut,
                LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(r.LengthStatus),
                CuttingMultiple = r.CuttingMultiple,
                FinishedCutLength = r.FinishedCutLength,
                CutLengthMatchType = EnumHelper.TryParse<MES.Core.Enums.CutLengthMatchType>(r.CutLengthMatchType),
                PostCutQuantity = r.PostCutQuantity,
                FaceCutCount = r.FaceCutCount,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                Remark = r.Remark,
                DataSource = r.DataSource,
                BatchNo = r.BatchNo,
                WorkOrderNo = r.WorkOrderNo,
                SalesOrderNo = r.SalesOrderNo,
                ProductionMainNo = r.ProductionMainNo,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            })
            .ToList();

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
        var raw = await _context.ProductionRecords
            .AsNoTracking()
            .Include(r => r.ProductionBatch)
            .OrderByDescending(r => r.CreatedTime)
            .Select(r => new
            {
                r.Id,
                r.ProductionBatchId,
                r.ProcessGroupId,
                r.ProcessName,
                r.ManufacturingSpec,
                r.SectionName,
                r.SequenceNumber,
                r.ExecDate,
                r.EquipmentName,
                r.Operator,
                r.Shift,
                r.Quantity,
                r.Weight,
                r.SolutionTemperature,
                r.SoakTime,
                r.ProductStatus,
                r.IsPreCut,
                r.LengthStatus,
                r.CuttingMultiple,
                r.FinishedCutLength,
                r.CutLengthMatchType,
                r.PostCutQuantity,
                r.FaceCutCount,
                r.TagNo,
                r.PlantGrade,
                r.Remark,
                r.DataSource,
                BatchNo = r.ProductionBatch.BatchNo,
                WorkOrderNo = r.ProductionBatch.WorkOrderNo,
                SalesOrderNo = r.ProductionBatch.SalesOrderNo,
                ProductionMainNo = r.ProductionBatch.ProductionMainNo,
                r.CreatedTime,
                r.UpdatedTime
            })
            .ToListAsync();

        return raw.Select(r => new ProductionRecordDto
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
            IsPreCut = r.IsPreCut,
            LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(r.LengthStatus),
            CuttingMultiple = r.CuttingMultiple,
            FinishedCutLength = r.FinishedCutLength,
            PostCutQuantity = r.PostCutQuantity,
            FaceCutCount = r.FaceCutCount,
            TagNo = r.TagNo,
            PlantGrade = r.PlantGrade,
            Remark = r.Remark,
            DataSource = r.DataSource,
            BatchNo = r.BatchNo,
            WorkOrderNo = r.WorkOrderNo,
            SalesOrderNo = r.SalesOrderNo,
            ProductionMainNo = r.ProductionMainNo,
            CreatedTime = r.CreatedTime,
            UpdatedTime = r.UpdatedTime
        }).ToList();
    }

    private static IQueryable<ProductionRecord> ApplySorting(IQueryable<ProductionRecord> queryable, string sortBy, bool isDescending)
    {
        return (sortBy.ToLowerInvariant(), isDescending) switch
        {
            ("execdate", false) => queryable.OrderBy(r => r.ExecDate),
            ("execdate", true) => queryable.OrderByDescending(r => r.ExecDate),
            ("batchno", false) => queryable.OrderBy(r => r.ProductionBatch.BatchNo),
            ("batchno", true) => queryable.OrderByDescending(r => r.ProductionBatch.BatchNo),
            ("workorderno", false) => queryable.OrderBy(r => r.ProductionBatch.WorkOrderNo ?? ""),
            ("workorderno", true) => queryable.OrderByDescending(r => r.ProductionBatch.WorkOrderNo ?? ""),
            ("salesorderno", false) => queryable.OrderBy(r => r.ProductionBatch.SalesOrderNo ?? ""),
            ("salesorderno", true) => queryable.OrderByDescending(r => r.ProductionBatch.SalesOrderNo ?? ""),
            ("productionmainno", false) => queryable.OrderBy(r => r.ProductionBatch.ProductionMainNo ?? ""),
            ("productionmainno", true) => queryable.OrderByDescending(r => r.ProductionBatch.ProductionMainNo ?? ""),
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
            ("cutlengthmatchtype", false) => queryable.OrderBy(r =>
                r.CutLengthMatchType == nameof(MES.Core.Enums.CutLengthMatchType.FullMatch) ? 0
                : r.CutLengthMatchType == nameof(MES.Core.Enums.CutLengthMatchType.MainNoMatch) ? 1 : 2),
            ("cutlengthmatchtype", true) => queryable.OrderByDescending(r =>
                r.CutLengthMatchType == nameof(MES.Core.Enums.CutLengthMatchType.FullMatch) ? 0
                : r.CutLengthMatchType == nameof(MES.Core.Enums.CutLengthMatchType.MainNoMatch) ? 1 : 2),
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
        var items = (await _context.ProductionRecords
            .AsNoTracking()
            .Include(r => r.ProductionBatch)
            .Where(r => ids.Contains(r.Id))
            .Select(r => new
            {
                r.Id,
                r.ProductionBatchId,
                r.ProcessGroupId,
                r.ProcessName,
                r.ManufacturingSpec,
                r.SectionName,
                r.SequenceNumber,
                r.ExecDate,
                r.EquipmentName,
                r.Operator,
                r.Shift,
                r.Quantity,
                r.Weight,
                r.SolutionTemperature,
                r.SoakTime,
                r.ProductStatus,
                r.IsPreCut,
                r.LengthStatus,
                r.CuttingMultiple,
                r.FinishedCutLength,
                r.CutLengthMatchType,
                r.PostCutQuantity,
                r.FaceCutCount,
                r.TagNo,
                r.PlantGrade,
                r.Remark,
                r.DataSource,
                BatchNo = r.ProductionBatch.BatchNo,
                WorkOrderNo = r.ProductionBatch.WorkOrderNo,
                SalesOrderNo = r.ProductionBatch.SalesOrderNo,
                ProductionMainNo = r.ProductionBatch.ProductionMainNo,
                r.CreatedTime,
                r.UpdatedTime
            })
            .ToListAsync())
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
                IsPreCut = r.IsPreCut,
                LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(r.LengthStatus),
                CuttingMultiple = r.CuttingMultiple,
                FinishedCutLength = r.FinishedCutLength,
                CutLengthMatchType = EnumHelper.TryParse<MES.Core.Enums.CutLengthMatchType>(r.CutLengthMatchType),
                PostCutQuantity = r.PostCutQuantity,
                FaceCutCount = r.FaceCutCount,
                TagNo = r.TagNo,
                PlantGrade = r.PlantGrade,
                Remark = r.Remark,
                DataSource = r.DataSource,
                BatchNo = r.BatchNo,
                WorkOrderNo = r.WorkOrderNo,
                SalesOrderNo = r.SalesOrderNo,
                ProductionMainNo = r.ProductionMainNo,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            })
            .ToList();

        return ProductionRecordPrintHelper.GenerateBatchPdf(items, columns, await _sectionNameDisplay.GetSectionNameMapAsync(), await _processDefService.GetProcessNameMapAsync());
    }

    /// <summary>
    /// 获取生产记录筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.ProductionRecordFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;
            var query = from r in _context.ProductionRecords
                        join pb in _context.ProductionBatches on r.ProductionBatchId equals pb.Id
                        select new
                        {
                            pb.BatchNo,
                            pb.WorkOrderNo,
                            pb.SalesOrderNo,
                            pb.ProductionMainNo,
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
                            r.ProductStatus,
                            r.LengthStatus
                        };

            var results = await query.AsNoTracking().ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["BatchNo"] = results.Select(x => x.BatchNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["WorkOrderNo"] = results.Select(x => x.WorkOrderNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SalesOrderNo"] = results.Select(x => x.SalesOrderNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["ProductionMainNo"] = results.Select(x => x.ProductionMainNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
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
                ["ProductStatus"] = results.Select(x => x.ProductStatus).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["LengthStatus"] = results.Select(x => x.LengthStatus).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!
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
            var sectionKey = SectionKeys.ToKey(section.SectionName);
            totalDays += sectionKey != null ? dayMap.GetValueOrDefault(sectionKey, 0) : 0;
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
            var sectionKey = SectionKeys.ToKey(section.SectionName);
            totalDays += sectionKey != null ? dayMap.GetValueOrDefault(sectionKey, 0) : 0;
        }

        // 交货状态调整：从配置表读取附加天数
        if (deliveryStateExtraDays.TryGetValue(deliveryState ?? "", out var dsExtra))
            totalDays += dsExtra;
        else if (deliveryStateExtraDays.TryGetValue("", out var defaultExtra))
            totalDays += defaultExtra;

        return (int)Math.Round(totalDays, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 自动计算产类（荒管/在制/成品）
    /// </summary>
    private static string CalculateProductStatus(
        string processName,
        string? manufacturingSpec,
        string? batchManufacturingItem,
        List<ProcessGroup> processGroups,
        string? finishedSpec = null)
    {
        return ProductStatusHelper.Calculate(processName, manufacturingSpec, batchManufacturingItem, processGroups, finishedSpec);
    }

    /// <summary>
    /// 自动计算长度状态：工段为"断切"且产类为"成品"时，从批次冗余其长度状态；否则为空
    /// </summary>
    private static string? CalculateLengthStatus(string? sectionName, string? productStatus, string? batchLengthStatus)
        => sectionName == SectionKeys.Cut && productStatus == ProductStatuses.Finished ? batchLengthStatus : null;

}
