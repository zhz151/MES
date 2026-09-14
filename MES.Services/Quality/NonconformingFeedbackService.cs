using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MES.Core.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Quality;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;
using MES.Services.Extensions;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Quality;

/// <summary>
/// 不合格反馈单服务 — 只登记问题（含问题照片），处置由质量负责人在不合格报告中决定。
/// 记录模式对齐生产记录：从批次冗余工单号/牌号，从工序组对齐执行序号，产类自动计算。
/// </summary>
public class NonconformingFeedbackService : INonconformingFeedbackService
{
    private readonly AppDbContext _context;
    private readonly ILogger<NonconformingFeedbackService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IAttachmentStorage _storage;
    private readonly IProcessDefinitionService _processDefinitionService;
    private readonly ISectionNameDisplayService _sectionNameDisplayService;

    /// <summary>附件张数上限（单一出口见 QualityPhotoLimits）</summary>
    private const int MaxAttachments = QualityPhotoLimits.PerRecord;

    public NonconformingFeedbackService(
        AppDbContext context,
        ILogger<NonconformingFeedbackService> logger,
        IMemoryCache cache,
        IAttachmentStorage storage,
        IProcessDefinitionService processDefinitionService,
        ISectionNameDisplayService sectionNameDisplayService)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
        _storage = storage;
        _processDefinitionService = processDefinitionService;
        _sectionNameDisplayService = sectionNameDisplayService;
    }

    public int MaxAttachmentCount => MaxAttachments;

    public long MaxAttachmentSizeBytes => _storage.MaxFileSizeBytes;

    // ========== 查询 ==========

    public async Task<PagedResult<NonconformingFeedbackDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.NonconformingFeedbacks
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                r.BatchNo.Contains(kw) ||
                (r.WorkOrderNo != null && r.WorkOrderNo.Contains(kw)) ||
                r.Reporter.Contains(kw) ||
                r.ProcessName.Contains(kw) ||
                r.SourceType.Contains(kw) ||
                (r.SectionName != null && r.SectionName.Contains(kw)) ||
                (r.ManufacturingSpec != null && r.ManufacturingSpec.Contains(kw)) ||
                (r.PlantGrade != null && r.PlantGrade.Contains(kw)) ||
                (r.ProductStatus != null && r.ProductStatus.Contains(kw)) ||
                (r.ProblemDescription != null && r.ProblemDescription.Contains(kw)) ||
                (r.DataSource != null && r.DataSource.Contains(kw)));
        }

        if (query.ReportDateFrom.HasValue)
            queryable = queryable.Where(r => r.ReportDate >= query.ReportDateFrom.Value);
        if (query.ReportDateTo.HasValue)
            queryable = queryable.Where(r => r.ReportDate < query.ReportDateTo.Value.AddDays(1));

        queryable = queryable.ApplyFilters(query.Filters);
        var totalCount = await queryable.CountAsync();
        queryable = ApplySorting(queryable, query.SortBy ?? "reportdate", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip).Take(query.PageSize)
            .Select(MapToDto()).ToListAsync();

        return new PagedResult<NonconformingFeedbackDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<NonconformingFeedbackDto?> GetByIdAsync(int id)
    {
        var dto = await _context.NonconformingFeedbacks
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync();
        if (dto == null) return null;

        dto.Attachments = await GetAttachmentsAsync(id);
        dto.AttachmentCount = dto.Attachments.Count;
        return dto;
    }

    // ========== 增删改 ==========

    public async Task<NonconformingFeedbackDto> CreateAsync(CreateNonconformingFeedbackRequest request)
    {
        // 反馈人非空校验放这里而非 DTO 的 [Required]：扫码链由 ScanQualityService 先覆写成登录人再调用本方法，
        // 留在 DTO 上会在覆写之前被 [ApiController] 模型校验拦下（2026-09-14 事故：扫码提交报「反馈人不能为空」）。
        if (string.IsNullOrWhiteSpace(request.Reporter))
            throw new BusinessException("反馈人不能为空");

        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BatchNo == request.BatchNo)
            ?? throw new BusinessException($"生产编号不存在：{request.BatchNo}");

        var processGroups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == batch.Id)
            .ToListAsync();

        var sourceType = ParseSourceType(request.SourceType);
        var (sectionName, inspectionItem) = NormalizeLocation(sourceType, request.SectionName, request.InspectionItem);

        var pgId = ResolveProcessGroupId(request.ProcessGroupId, request.ProcessName, request.ManufacturingSpec, processGroups);
        var seqNum = ResolveSequenceNumber(request.SequenceNumber, sectionName, pgId, processGroups);

        var entity = new NonconformingFeedback
        {
            ReportDate = request.ReportDate,
            Reporter = request.Reporter,
            DataSource = string.IsNullOrWhiteSpace(request.DataSource) ? "MANUAL" : request.DataSource,
            SourceType = sourceType.ToString(),
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            WorkOrderNo = batch.WorkOrderNo,
            ProcessGroupId = pgId,
            ProcessName = request.ProcessName,
            ManufacturingSpec = request.ManufacturingSpec,
            SectionName = sectionName,
            SequenceNumber = seqNum,
            InspectionItem = inspectionItem,
            ProductStatus = ProductStatusHelper.Calculate(
                request.ProcessName, request.ManufacturingSpec, batch.ManufacturingItem, processGroups, batch.Specification),
            PlantGrade = batch.PlantGrade,
            IncomingQuantity = request.IncomingQuantity,
            IncomingWeight = request.IncomingWeight,
            DefectQuantity = request.DefectQuantity,
            DefectWeight = ComputeDefectWeight(request.IncomingWeight, request.IncomingQuantity, request.DefectQuantity, request.DefectWeight),
            ProblemDescription = request.ProblemDescription
        };

        _context.NonconformingFeedbacks.Add(entity);
        await _context.SaveChangesAsync();
        InvalidateFilterContexts();

        return await GetByIdAsync(entity.Id) ?? throw new BusinessException("不合格反馈单创建后读取失败");
    }

    public async Task<NonconformingFeedbackDto> UpdateAsync(int id, UpdateNonconformingFeedbackRequest request)
    {
        var entity = await _context.NonconformingFeedbacks.FindAsync(id)
            ?? throw new BusinessException("不合格反馈单不存在");

        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BatchNo == request.BatchNo)
            ?? throw new BusinessException($"生产编号不存在：{request.BatchNo}");

        var processGroups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == batch.Id)
            .ToListAsync();

        var processName = request.ProcessName ?? entity.ProcessName;
        var manufacturingSpec = request.ManufacturingSpec ?? entity.ManufacturingSpec;

        // 来源随全量提交覆盖：位置信息按来源分档归一化（成品检验档清空工段、必填检验项目）
        var sourceType = ParseSourceType(request.SourceType);
        var (sectionName, inspectionItem) = NormalizeLocation(sourceType, request.SectionName, request.InspectionItem);

        var pgId = ResolveProcessGroupId(request.ProcessGroupId, processName, manufacturingSpec, processGroups);
        var seqNum = ResolveSequenceNumber(request.SequenceNumber, sectionName, pgId, processGroups);

        entity.ReportDate = request.ReportDate;
        entity.Reporter = string.IsNullOrWhiteSpace(request.Reporter) ? entity.Reporter : request.Reporter;
        entity.SourceType = sourceType.ToString();
        entity.ProductionBatchId = batch.Id;
        entity.BatchNo = batch.BatchNo;
        entity.WorkOrderNo = batch.WorkOrderNo;
        entity.ProcessGroupId = pgId;
        entity.ProcessName = processName;
        entity.ManufacturingSpec = manufacturingSpec;
        entity.SectionName = sectionName;
        entity.SequenceNumber = seqNum;
        entity.InspectionItem = inspectionItem;
        entity.ProductStatus = ProductStatusHelper.Calculate(
            processName, manufacturingSpec, batch.ManufacturingItem, processGroups, batch.Specification);
        entity.PlantGrade = batch.PlantGrade;
        entity.IncomingQuantity = request.IncomingQuantity;
        entity.IncomingWeight = request.IncomingWeight;
        entity.DefectQuantity = request.DefectQuantity;
        entity.DefectWeight = ComputeDefectWeight(request.IncomingWeight, request.IncomingQuantity, request.DefectQuantity, request.DefectWeight);
        entity.ProblemDescription = request.ProblemDescription;

        await _context.SaveChangesAsync();
        InvalidateFilterContexts();

        return await GetByIdAsync(entity.Id) ?? throw new BusinessException("不合格反馈单更新后读取失败");
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.NonconformingFeedbacks
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new BusinessException("不合格反馈单不存在");

        // 先删磁盘文件（失败仅告警不阻断），再删主记录（附件行级联删除）
        foreach (var att in entity.Attachments)
            await _storage.DeleteAsync(att.StoredName);

        _context.NonconformingFeedbacks.Remove(entity);
        await _context.SaveChangesAsync();
        InvalidateFilterContexts();
    }

    // ========== 筛选/带出 ==========

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.NonconformingFeedbackFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            var all = await _context.NonconformingFeedbacks
                .AsNoTracking()
                .Select(r => new
                {
                    r.Reporter,
                    r.BatchNo,
                    r.WorkOrderNo,
                    r.ProcessName,
                    r.SourceType,
                    r.SectionName,
                    r.InspectionItem,
                    r.ManufacturingSpec,
                    r.PlantGrade,
                    r.ProductStatus,
                    r.DataSource,
                    r.ReportDate
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["Reporter"] = Distinct(all.Select(x => x.Reporter)),
                ["BatchNo"] = Distinct(all.Select(x => x.BatchNo)),
                ["WorkOrderNo"] = Distinct(all.Select(x => x.WorkOrderNo)),
                ["ProcessName"] = Distinct(all.Select(x => x.ProcessName)),
                ["SourceType"] = Distinct(all.Select(x => x.SourceType)),
                ["SectionName"] = Distinct(all.Select(x => x.SectionName)),
                ["InspectionItem"] = Distinct(all.Select(x => x.InspectionItem?.ToString())),
                ["ManufacturingSpec"] = Distinct(all.Select(x => x.ManufacturingSpec)),
                ["PlantGrade"] = Distinct(all.Select(x => x.PlantGrade)),
                ["ProductStatus"] = Distinct(all.Select(x => x.ProductStatus)),
                ["DataSource"] = Distinct(all.Select(x => x.DataSource)),
                ["ReportDate"] = all.Select(x => x.ReportDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList()
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<NonconformingFeedbackLookupResultDto?> LookupBatchAsync(string batchNo)
    {
        if (string.IsNullOrWhiteSpace(batchNo)) return null;

        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.BatchNo == batchNo)
            .Select(b => new { b.Id, b.BatchNo, b.WorkOrderNo, b.PlantGrade })
            .FirstOrDefaultAsync();
        if (batch == null) return null;

        var processGroups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == batch.Id)
            .OrderBy(pg => pg.SequenceNumber)
            .ToListAsync();

        return new NonconformingFeedbackLookupResultDto
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            WorkOrderNo = batch.WorkOrderNo,
            PlantGrade = batch.PlantGrade,
            // 该批次是否存在「附加成检」工序 —— 成品检验档据此推导「预检/终检」
            // （有附加成检：附加成检工序=终检，其余工序=预检；无附加成检：全部=终检）
            HasAdditionalFinalInspection = processGroups.Any(pg => pg.ProcessName == ProcessKeys.AdditionalFinalInspection),
            ProcessGroups = processGroups.Select(pg => new NonconformingFeedbackProcessGroupOption
            {
                ProcessGroupId = pg.Id,
                ProcessName = pg.ProcessName,
                ManufacturingSpec = pg.ManufacturingSpec,
                Sections = pg.GetNonEmptySectionKeys().Select(s => s.SectionKey).ToList()
            }).ToList()
        };
    }

    // ========== 附件 ==========

    public async Task<List<NonconformingFeedbackAttachmentDto>> GetAttachmentsAsync(int feedbackId)
    {
        return await _context.NonconformingFeedbackAttachments
            .AsNoTracking()
            .Where(a => a.FeedbackId == feedbackId)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Id)
            .Select(a => new NonconformingFeedbackAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes,
                SortOrder = a.SortOrder,
                CreatedTime = a.CreatedTime
            })
            .ToListAsync();
    }

    public async Task<NonconformingFeedbackAttachmentDto> AddAttachmentAsync(
        int feedbackId, Stream content, string fileName, string contentType)
    {
        var exists = await _context.NonconformingFeedbacks.AnyAsync(r => r.Id == feedbackId);
        if (!exists) throw new BusinessException("不合格反馈单不存在");

        var count = await _context.NonconformingFeedbackAttachments.CountAsync(a => a.FeedbackId == feedbackId);
        if (count >= MaxAttachments)
            throw new BusinessException($"附件数量已达上限（{MaxAttachments} 张）");

        // 文件大小在上传流可寻址时先取（IFormFile 流可寻址），否则交给存储层写入后由返回名兜底为 0
        var sizeBytes = content.CanSeek ? content.Length : 0L;
        var storedName = await _storage.SaveAsync(content, fileName);

        var entity = new NonconformingFeedbackAttachment
        {
            FeedbackId = feedbackId,
            FileName = Path.GetFileName(fileName),
            StoredName = storedName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = sizeBytes,
            SortOrder = count
        };

        try
        {
            _context.NonconformingFeedbackAttachments.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // 文件已落盘但落库失败 → 回收磁盘文件并脱离变更跟踪，避免孤儿文件与下次 SaveChanges 重试
            _context.Entry(entity).State = EntityState.Detached;
            await _storage.DeleteAsync(storedName);
            throw;
        }

        return new NonconformingFeedbackAttachmentDto
        {
            Id = entity.Id,
            FileName = entity.FileName,
            ContentType = entity.ContentType,
            SizeBytes = entity.SizeBytes,
            SortOrder = entity.SortOrder,
            CreatedTime = entity.CreatedTime
        };
    }

    public async Task<AttachmentContent?> GetAttachmentContentAsync(int feedbackId, int attachmentId)
    {
        var att = await _context.NonconformingFeedbackAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.FeedbackId == feedbackId);
        if (att == null) return null;

        var bytes = await _storage.ReadAsync(att.StoredName);
        if (bytes == null) return null;

        return new AttachmentContent
        {
            Content = bytes,
            ContentType = att.ContentType,
            FileName = att.FileName
        };
    }

    public async Task DeleteAttachmentAsync(int feedbackId, int attachmentId)
    {
        var att = await _context.NonconformingFeedbackAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.FeedbackId == feedbackId)
            ?? throw new BusinessException("附件不存在");

        _context.NonconformingFeedbackAttachments.Remove(att);
        await _context.SaveChangesAsync();
        await _storage.DeleteAsync(att.StoredName);
    }

    // ========== 打印 ==========

    public async Task<byte[]> GetPrintPdfAsync(int feedbackId)
    {
        var entity = await _context.NonconformingFeedbacks
            .AsNoTracking()
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == feedbackId)
            ?? throw new BusinessException("不合格反馈单不存在");

        var images = new List<NonconformingFeedbackPrintHelper.PrintImage>();
        foreach (var att in entity.Attachments.OrderBy(a => a.SortOrder).ThenBy(a => a.Id))
        {
            var bytes = await _storage.ReadAsync(att.StoredName);
            if (bytes is { Length: > 0 })
                images.Add(new NonconformingFeedbackPrintHelper.PrintImage
                {
                    FileName = att.FileName,
                    Data = bytes
                });
        }

        // 工序/工段存英文 Key，打印层须转中文（配置表优先，兜底常量规范中文）
        var processNameMap = await _processDefinitionService.GetProcessNameMapAsync();
        var sectionNameMap = await _sectionNameDisplayService.GetSectionNameMapAsync();

        return NonconformingFeedbackPrintHelper.GeneratePdf(entity, images, processNameMap, sectionNameMap);
    }

    // ========== 私有辅助 ==========

    private void InvalidateFilterContexts() => _cache.Remove(CacheKeys.NonconformingFeedbackFilterContexts);

    private static List<string> Distinct(IEnumerable<string?> values)
        => values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).Distinct().OrderBy(v => v).ToList();

    /// <summary>解析工序组ID：优先用前端传入，否则按 工序名+制造规格 匹配</summary>
    private static int ResolveProcessGroupId(
        int? requested, string processName, string? manufacturingSpec, List<ProcessGroup> processGroups)
    {
        if (requested.HasValue && requested.Value > 0) return requested.Value;
        var matched = processGroups.FirstOrDefault(pg =>
            pg.ProcessName == processName && pg.ManufacturingSpec == manufacturingSpec);
        return matched?.Id ?? 0;
    }

    /// <summary>
    /// 解析来源类型（枚举名，容忍中文）。DTO 层 [Required] 已拦截空值，此处缺失/非法一律抛业务异常。
    /// </summary>
    private static NonconformingFeedbackSourceType ParseSourceType(string? sourceType)
    {
        var parsed = EnumHelper.TryParse<NonconformingFeedbackSourceType>(sourceType);
        if (parsed == null)
            throw new BusinessException($"来源类型无效：{sourceType}");
        return parsed.Value;
    }

    /// <summary>
    /// 按来源分档归一化位置信息：
    /// 成品检验档无工段概念（清空工段/执行序号）、必须选择检验项目；
    /// 生产工段与过程检验档必须选择工段、检验项目置空。
    /// </summary>
    private static (string? SectionName, InspectionItem? InspectionItem) NormalizeLocation(
        NonconformingFeedbackSourceType sourceType, string? sectionName, InspectionItem? inspectionItem)
    {
        if (sourceType == NonconformingFeedbackSourceType.FinalInspection)
        {
            if (inspectionItem == null)
                throw new BusinessException("成品检验来源须选择检验项目");
            return (null, inspectionItem);
        }

        if (string.IsNullOrWhiteSpace(sectionName))
            throw new BusinessException($"{EnumHelper.GetDisplayName(sourceType)}来源须选择工段");
        return (sectionName.Trim(), null);
    }

    /// <summary>
    /// 解析执行序号（工段步骤号）：定位工序组后对齐，解析不到保留前端值兜底。
    /// 成品检验档无工段 → 无执行序号，返回 null。
    /// </summary>
    private static int? ResolveSequenceNumber(
        int? requested, string? sectionName, int processGroupId, List<ProcessGroup> processGroups)
    {
        if (string.IsNullOrWhiteSpace(sectionName)) return null;
        if (processGroupId <= 0) return requested;
        var pg = processGroups.FirstOrDefault(p => p.Id == processGroupId);
        var aligned = pg?.GetSectionSequence(sectionName);
        return aligned ?? requested;
    }

    /// <summary>
    /// 不合格重量：留空则按 来料重量 ÷ 来料支数 × 不合格支数 四舍五入取整；手填值优先。
    /// </summary>
    private static int? ComputeDefectWeight(decimal? incomingWeight, int? incomingQuantity, int? defectQuantity, int? manual)
    {
        if (manual.HasValue) return manual;
        if (!incomingWeight.HasValue || !incomingQuantity.HasValue || incomingQuantity.Value <= 0
            || !defectQuantity.HasValue || defectQuantity.Value <= 0)
            return null;
        return (int)Math.Round(incomingWeight.Value / incomingQuantity.Value * defectQuantity.Value, MidpointRounding.AwayFromZero);
    }

    private static IQueryable<NonconformingFeedback> ApplySorting(
        IQueryable<NonconformingFeedback> queryable, string sortBy, bool isDescending)
        => queryable.ApplySort(sortBy, isDescending);

    private static System.Linq.Expressions.Expression<Func<NonconformingFeedback, NonconformingFeedbackDto>> MapToDto() => e => new NonconformingFeedbackDto
    {
        Id = e.Id,
        ReportDate = e.ReportDate,
        Reporter = e.Reporter,
        DataSource = e.DataSource,
        SourceType = e.SourceType,
        ProductionBatchId = e.ProductionBatchId,
        BatchNo = e.BatchNo,
        WorkOrderNo = e.WorkOrderNo,
        ProcessGroupId = e.ProcessGroupId,
        ProcessName = e.ProcessName,
        ManufacturingSpec = e.ManufacturingSpec,
        SectionName = e.SectionName,
        SequenceNumber = e.SequenceNumber,
        InspectionItem = e.InspectionItem,
        ProductStatus = e.ProductStatus,
        PlantGrade = e.PlantGrade,
        IncomingQuantity = e.IncomingQuantity,
        IncomingWeight = e.IncomingWeight,
        DefectQuantity = e.DefectQuantity,
        DefectWeight = e.DefectWeight,
        ProblemDescription = e.ProblemDescription,
        AttachmentCount = e.Attachments.Count,
        CreatedTime = e.CreatedTime,
        UpdatedTime = e.UpdatedTime
    };
}
