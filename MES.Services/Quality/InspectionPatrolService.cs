using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MES.Core.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Exceptions;
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
/// 巡检单服务 — 质量检验的一种类型（过程巡检记录）。
/// 记录模式对齐不合格反馈：从批次冗余工单号/牌号，从工序组对齐执行序号，产类自动计算；
/// 巡检明细为一对多子表，整改块以「涉及整改」为开关（未涉及则清空整改三字段）。
/// </summary>
public class InspectionPatrolService : IInspectionPatrolService
{
    private readonly AppDbContext _context;
    private readonly ILogger<InspectionPatrolService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IAttachmentStorage _storage;
    private readonly IProcessDefinitionService _processDefinitionService;
    private readonly ISectionNameDisplayService _sectionNameDisplayService;

    public InspectionPatrolService(
        AppDbContext context,
        ILogger<InspectionPatrolService> logger,
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

    public int MaxAttachmentPerType => InspectionPatrolPhotoTypes.MaxPerType;

    public long MaxAttachmentSizeBytes => _storage.MaxFileSizeBytes;

    // ========== 查询 ==========

    public async Task<PagedResult<InspectionPatrolDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.InspectionPatrols
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                r.BatchNo.Contains(kw) ||
                (r.WorkOrderNo != null && r.WorkOrderNo.Contains(kw)) ||
                r.Inspector.Contains(kw) ||
                r.ProcessName.Contains(kw) ||
                r.SectionName.Contains(kw) ||
                (r.ManufacturingSpec != null && r.ManufacturingSpec.Contains(kw)) ||
                (r.PlantGrade != null && r.PlantGrade.Contains(kw)) ||
                (r.ProductStatus != null && r.ProductStatus.Contains(kw)) ||
                (r.ProductionUnit != null && r.ProductionUnit.Contains(kw)) ||
                (r.EquipmentName != null && r.EquipmentName.Contains(kw)) ||
                (r.ProductionOperator != null && r.ProductionOperator.Contains(kw)) ||
                (r.RectificationDescription != null && r.RectificationDescription.Contains(kw)) ||
                (r.VerificationResult != null && r.VerificationResult.Contains(kw)) ||
                (r.DataSource != null && r.DataSource.Contains(kw)) ||
                r.Items.Any(i => i.ItemName.Contains(kw) || (i.Result != null && i.Result.Contains(kw))));
        }

        if (query.ReportDateFrom.HasValue)
            queryable = queryable.Where(r => r.PatrolDate >= query.ReportDateFrom.Value);
        if (query.ReportDateTo.HasValue)
            queryable = queryable.Where(r => r.PatrolDate < query.ReportDateTo.Value.AddDays(1));

        queryable = queryable.ApplyFilters(query.Filters);
        var totalCount = await queryable.CountAsync();
        queryable = queryable.ApplySort(query.SortBy ?? "patroldate", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip).Take(query.PageSize)
            .Select(MapToSummaryDto())
            .ToListAsync();

        return new PagedResult<InspectionPatrolDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<InspectionPatrolDto?> GetByIdAsync(int id)
    {
        var dto = await _context.InspectionPatrols
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(MapToSummaryDto())
            .FirstOrDefaultAsync();
        if (dto == null) return null;

        dto.Items = await _context.InspectionPatrolItems
            .AsNoTracking()
            .Where(i => i.PatrolId == id)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new InspectionPatrolItemDto
            {
                Id = i.Id,
                ItemName = i.ItemName,
                Result = i.Result,
                Remark = i.Remark,
                SortOrder = i.SortOrder
            })
            .ToListAsync();
        dto.ItemCount = dto.Items.Count;

        dto.Attachments = await GetAttachmentsAsync(id);
        dto.AttachmentCount = dto.Attachments.Count;
        return dto;
    }

    // ========== 增删改 ==========

    public async Task<InspectionPatrolDto> CreateAsync(CreateInspectionPatrolRequest request)
    {
        // 巡检人非空校验放这里而非 DTO 的 [Required]：扫码链由 ScanQualityService 先覆写成登录人再调用本方法，
        // 留在 DTO 上会在覆写之前被 [ApiController] 模型校验拦下（2026-09-14 事故：扫码提交报「巡检人不能为空」）。
        if (string.IsNullOrWhiteSpace(request.Inspector))
            throw new BusinessException("巡检人不能为空");

        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BatchNo == request.BatchNo)
            ?? throw new BusinessException($"生产编号不存在：{request.BatchNo}");

        var processGroups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == batch.Id)
            .ToListAsync();

        var pgId = ResolveProcessGroupId(request.ProcessGroupId, request.ProcessName, request.ManufacturingSpec, processGroups);
        var seqNum = ResolveSequenceNumber(request.SequenceNumber, request.SectionName, pgId, processGroups);

        var entity = new InspectionPatrol
        {
            PatrolDate = request.PatrolDate,
            Inspector = request.Inspector,
            DataSource = string.IsNullOrWhiteSpace(request.DataSource) ? "MANUAL" : request.DataSource,
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            WorkOrderNo = batch.WorkOrderNo,
            ProcessGroupId = pgId,
            ProcessName = request.ProcessName,
            ManufacturingSpec = request.ManufacturingSpec,
            SectionName = request.SectionName,
            SequenceNumber = seqNum,
            ProductStatus = ProductStatusHelper.Calculate(
                request.ProcessName, request.ManufacturingSpec, batch.ManufacturingItem, processGroups, batch.Specification),
            PlantGrade = batch.PlantGrade,
            ProductionUnit = request.ProductionUnit,
            EquipmentName = request.EquipmentName,
            ProductionOperator = request.ProductionOperator,
            NeedRectification = request.NeedRectification,
            RectificationDescription = request.NeedRectification ? request.RectificationDescription : null,
            VerificationResult = request.NeedRectification ? request.VerificationResult : null,
            IsClosed = request.NeedRectification && request.IsClosed
        };

        entity.Items = BuildItems(request.Items);
        if (entity.Items.Count == 0)
            throw new BusinessException("请至少填写一条巡检明细");

        _context.InspectionPatrols.Add(entity);
        await _context.SaveChangesAsync();
        InvalidateFilterContexts();

        return await GetByIdAsync(entity.Id) ?? throw new BusinessException("巡检单创建后读取失败");
    }

    public async Task<InspectionPatrolDto> UpdateAsync(int id, UpdateInspectionPatrolRequest request)
    {
        var entity = await _context.InspectionPatrols
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new BusinessException("巡检单不存在");

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
        var sectionName = request.SectionName ?? entity.SectionName;

        var pgId = ResolveProcessGroupId(request.ProcessGroupId, processName, manufacturingSpec, processGroups);
        var seqNum = ResolveSequenceNumber(request.SequenceNumber ?? 0, sectionName, pgId, processGroups);

        entity.PatrolDate = request.PatrolDate;
        entity.Inspector = string.IsNullOrWhiteSpace(request.Inspector) ? entity.Inspector : request.Inspector;
        entity.ProductionBatchId = batch.Id;
        entity.BatchNo = batch.BatchNo;
        entity.WorkOrderNo = batch.WorkOrderNo;
        entity.ProcessGroupId = pgId;
        entity.ProcessName = processName;
        entity.ManufacturingSpec = manufacturingSpec;
        entity.SectionName = sectionName;
        entity.SequenceNumber = seqNum;
        entity.ProductStatus = ProductStatusHelper.Calculate(
            processName, manufacturingSpec, batch.ManufacturingItem, processGroups, batch.Specification);
        entity.PlantGrade = batch.PlantGrade;
        entity.ProductionUnit = request.ProductionUnit;
        entity.EquipmentName = request.EquipmentName;
        entity.ProductionOperator = request.ProductionOperator;
        entity.NeedRectification = request.NeedRectification;
        entity.RectificationDescription = request.NeedRectification ? request.RectificationDescription : null;
        entity.VerificationResult = request.NeedRectification ? request.VerificationResult : null;
        entity.IsClosed = request.NeedRectification && request.IsClosed;

        // 巡检明细整组替换
        var newItems = BuildItems(request.Items);
        if (newItems.Count == 0)
            throw new BusinessException("请至少填写一条巡检明细");
        _context.InspectionPatrolItems.RemoveRange(entity.Items);
        entity.Items = newItems;

        await _context.SaveChangesAsync();
        InvalidateFilterContexts();

        return await GetByIdAsync(entity.Id) ?? throw new BusinessException("巡检单更新后读取失败");
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.InspectionPatrols
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new BusinessException("巡检单不存在");

        // 先删磁盘文件（失败仅告警不阻断），再删主记录（明细/附件行级联删除）
        foreach (var att in entity.Attachments)
            await _storage.DeleteAsync(att.StoredName);

        _context.InspectionPatrols.Remove(entity);
        await _context.SaveChangesAsync();
        InvalidateFilterContexts();
    }

    public async Task SetClosedAsync(int id, bool isClosed)
    {
        var entity = await _context.InspectionPatrols.FindAsync(id)
            ?? throw new BusinessException("巡检单不存在");
        entity.IsClosed = isClosed;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 按「批次 + 工序组 + 工段」查询（扫码第二次定位用）：未闭环保在前（等着补整改的就是它）。
    /// 同状态内按 巡检日期/Id 倒序取新。
    /// </summary>
    public async Task<List<InspectionPatrolDto>> GetByKeyAsync(string batchNo, int processGroupId, string sectionName)
    {
        if (string.IsNullOrWhiteSpace(batchNo)) return new List<InspectionPatrolDto>();

        return await _context.InspectionPatrols
            .AsNoTracking()
            .Where(r => r.BatchNo == batchNo
                        && r.ProcessGroupId == processGroupId
                        && r.SectionName == sectionName)
            // 待整改（涉及整改且未闭环）优先 —— 不涉及整改的单本就无需闭环，不占「待整改」位
            .OrderBy(r => r.NeedRectification && !r.IsClosed ? 0 : 1)
            .ThenByDescending(r => r.PatrolDate)
            .ThenByDescending(r => r.Id)
            .Select(MapToSummaryDto())
            .ToListAsync();
    }

    /// <summary>
    /// 窄口径整改回填：只写 验证结果 / 是否闭环 / 整改人。
    /// ⚠️ 刻意不加载 Items、不调用 UpdateAsync —— 后者是明细整组替换，会把第一次巡检的明细清空。
    /// </summary>
    public async Task<InspectionPatrolDto> RectifyAsync(int id, InspectionPatrolRectifyRequest request)
    {
        var entity = await _context.InspectionPatrols
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new BusinessException("巡检单不存在");

        if (!entity.NeedRectification)
            throw new BusinessException("该巡检单未涉及整改，无需整改回填");

        entity.VerificationResult = request.VerificationResult;
        entity.RectificationOperator = request.RectificationOperator;
        entity.IsClosed = request.IsClosed;

        await _context.SaveChangesAsync();
        InvalidateFilterContexts();

        return await GetByIdAsync(id) ?? throw new BusinessException("整改回填后读取失败");
    }

    // ========== 筛选/带出 ==========

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.InspectionPatrolFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            var all = await _context.InspectionPatrols
                .AsNoTracking()
                .Select(r => new
                {
                    r.Inspector,
                    r.BatchNo,
                    r.WorkOrderNo,
                    r.ProcessName,
                    r.SectionName,
                    r.ManufacturingSpec,
                    r.PlantGrade,
                    r.ProductStatus,
                    r.ProductionUnit,
                    r.EquipmentName,
                    r.ProductionOperator,
                    r.DataSource,
                    r.PatrolDate
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["Inspector"] = Distinct(all.Select(x => x.Inspector)),
                ["BatchNo"] = Distinct(all.Select(x => x.BatchNo)),
                ["WorkOrderNo"] = Distinct(all.Select(x => x.WorkOrderNo)),
                ["ProcessName"] = Distinct(all.Select(x => x.ProcessName)),
                ["SectionName"] = Distinct(all.Select(x => x.SectionName)),
                ["ManufacturingSpec"] = Distinct(all.Select(x => x.ManufacturingSpec)),
                ["PlantGrade"] = Distinct(all.Select(x => x.PlantGrade)),
                ["ProductStatus"] = Distinct(all.Select(x => x.ProductStatus)),
                ["ProductionUnit"] = Distinct(all.Select(x => x.ProductionUnit)),
                ["EquipmentName"] = Distinct(all.Select(x => x.EquipmentName)),
                ["ProductionOperator"] = Distinct(all.Select(x => x.ProductionOperator)),
                ["DataSource"] = Distinct(all.Select(x => x.DataSource)),
                ["PatrolDate"] = all.Select(x => x.PatrolDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList()
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<InspectionPatrolLookupResultDto?> LookupBatchAsync(string batchNo)
    {
        if (string.IsNullOrWhiteSpace(batchNo)) return null;

        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.BatchNo == batchNo)
            .Select(b => new { b.Id, b.BatchNo, b.WorkOrderNo, b.PlantGrade, b.CurrentOutsource, b.CurrentEquipmentName })
            .FirstOrDefaultAsync();
        if (batch == null) return null;

        var processGroups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == batch.Id)
            .OrderBy(pg => pg.SequenceNumber)
            .ToListAsync();

        return new InspectionPatrolLookupResultDto
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            WorkOrderNo = batch.WorkOrderNo,
            PlantGrade = batch.PlantGrade,
            CurrentOutsource = batch.CurrentOutsource,
            CurrentEquipmentName = batch.CurrentEquipmentName,
            ProcessGroups = processGroups.Select(pg => new InspectionPatrolProcessGroupOption
            {
                ProcessGroupId = pg.Id,
                ProcessName = pg.ProcessName,
                ManufacturingSpec = pg.ManufacturingSpec,
                Sections = pg.GetNonEmptySectionKeys().Select(s => s.SectionKey).ToList()
            }).ToList()
        };
    }

    public async Task<InspectionPatrolPositionOptionsDto> GetPositionOptionsAsync()
    {
        var productionUnits = await _context.OutsourceVendorProfiles
            .AsNoTracking()
            .Where(v => v.IsActive)
            .Select(v => v.VendorName)
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync();

        return new InspectionPatrolPositionOptionsDto
        {
            ProductionUnits = productionUnits
        };
    }

    // ========== 附件 ==========

    public async Task<List<InspectionPatrolAttachmentDto>> GetAttachmentsAsync(int patrolId)
    {
        return await _context.InspectionPatrolAttachments
            .AsNoTracking()
            .Where(a => a.PatrolId == patrolId)
            .OrderBy(a => a.PhotoType).ThenBy(a => a.SortOrder).ThenBy(a => a.Id)
            .Select(a => new InspectionPatrolAttachmentDto
            {
                Id = a.Id,
                PhotoType = a.PhotoType,
                FileName = a.FileName,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes,
                SortOrder = a.SortOrder,
                CreatedTime = a.CreatedTime
            })
            .ToListAsync();
    }

    public async Task<InspectionPatrolAttachmentDto> AddAttachmentAsync(
        int patrolId, string photoType, Stream content, string fileName, string contentType)
    {
        if (!InspectionPatrolPhotoTypes.IsValid(photoType))
            throw new BusinessException($"照片类型无效：{photoType}");

        var exists = await _context.InspectionPatrols.AnyAsync(r => r.Id == patrolId);
        if (!exists) throw new BusinessException("巡检单不存在");

        var count = await _context.InspectionPatrolAttachments
            .CountAsync(a => a.PatrolId == patrolId && a.PhotoType == photoType);
        if (count >= InspectionPatrolPhotoTypes.MaxPerType)
            throw new BusinessException($"{InspectionPatrolPhotoTypes.ToChinese(photoType)}数量已达上限（{InspectionPatrolPhotoTypes.MaxPerType} 张）");

        // 文件大小在上传流可寻址时先取（IFormFile 流可寻址），否则交给存储层写入后由返回名兜底为 0
        var sizeBytes = content.CanSeek ? content.Length : 0L;
        var storedName = await _storage.SaveAsync(content, fileName);

        var entity = new InspectionPatrolAttachment
        {
            PatrolId = patrolId,
            PhotoType = photoType,
            FileName = Path.GetFileName(fileName),
            StoredName = storedName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = sizeBytes,
            SortOrder = count
        };

        try
        {
            _context.InspectionPatrolAttachments.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // 文件已落盘但落库失败 → 回收磁盘文件并脱离变更跟踪，避免孤儿文件与下次 SaveChanges 重试
            _context.Entry(entity).State = EntityState.Detached;
            await _storage.DeleteAsync(storedName);
            throw;
        }

        return new InspectionPatrolAttachmentDto
        {
            Id = entity.Id,
            PhotoType = entity.PhotoType,
            FileName = entity.FileName,
            ContentType = entity.ContentType,
            SizeBytes = entity.SizeBytes,
            SortOrder = entity.SortOrder,
            CreatedTime = entity.CreatedTime
        };
    }

    public async Task<AttachmentContent?> GetAttachmentContentAsync(int patrolId, int attachmentId)
    {
        var att = await _context.InspectionPatrolAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.PatrolId == patrolId);
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

    public async Task DeleteAttachmentAsync(int patrolId, int attachmentId)
    {
        var att = await _context.InspectionPatrolAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.PatrolId == patrolId)
            ?? throw new BusinessException("附件不存在");

        _context.InspectionPatrolAttachments.Remove(att);
        await _context.SaveChangesAsync();
        await _storage.DeleteAsync(att.StoredName);
    }

    // ========== 打印 ==========

    public async Task<byte[]> GetPrintPdfAsync(int patrolId)
    {
        var entity = await _context.InspectionPatrols
            .AsNoTracking()
            .Include(r => r.Items)
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == patrolId)
            ?? throw new BusinessException("巡检单不存在");

        var patrolImages = new List<InspectionPatrolPrintHelper.PrintImage>();
        var rectImages = new List<InspectionPatrolPrintHelper.PrintImage>();

        foreach (var att in entity.Attachments.OrderBy(a => a.SortOrder).ThenBy(a => a.Id))
        {
            var bytes = await _storage.ReadAsync(att.StoredName);
            if (bytes is not { Length: > 0 }) continue;

            var target = att.PhotoType == InspectionPatrolPhotoTypes.Rectification ? rectImages : patrolImages;
            target.Add(new InspectionPatrolPrintHelper.PrintImage
            {
                FileName = att.FileName,
                Data = bytes
            });
        }

        // 工序/工段存英文 Key，打印层须转中文（配置表优先，兜底常量规范中文）
        var processNameMap = await _processDefinitionService.GetProcessNameMapAsync();
        var sectionNameMap = await _sectionNameDisplayService.GetSectionNameMapAsync();

        return InspectionPatrolPrintHelper.GeneratePdf(
            entity, entity.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList(),
            patrolImages, rectImages, processNameMap, sectionNameMap);
    }

    // ========== 私有辅助 ==========

    private void InvalidateFilterContexts() => _cache.Remove(CacheKeys.InspectionPatrolFilterContexts);

    private static List<string> Distinct(IEnumerable<string?> values)
        => values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).Distinct().OrderBy(v => v).ToList();

    /// <summary>请求明细 → 实体明细（跳过巡检项为空的行，按提交顺序编号）</summary>
    private static List<InspectionPatrolItem> BuildItems(List<InspectionPatrolItemRequest>? items)
    {
        if (items == null) return new List<InspectionPatrolItem>();
        return items
            .Where(i => !string.IsNullOrWhiteSpace(i.ItemName))
            .Select((i, index) => new InspectionPatrolItem
            {
                ItemName = i.ItemName.Trim(),
                Result = i.Result,
                Remark = i.Remark,
                SortOrder = index
            })
            .ToList();
    }

    /// <summary>解析工序组ID：优先用前端传入，否则按 工序名+制造规格 匹配</summary>
    private static int ResolveProcessGroupId(
        int? requested, string processName, string? manufacturingSpec, List<ProcessGroup> processGroups)
    {
        if (requested.HasValue && requested.Value > 0) return requested.Value;
        var matched = processGroups.FirstOrDefault(pg =>
            pg.ProcessName == processName && pg.ManufacturingSpec == manufacturingSpec);
        return matched?.Id ?? 0;
    }

    /// <summary>解析执行序号（工段步骤号）：定位工序组后对齐，解析不到保留前端值兜底</summary>
    private static int ResolveSequenceNumber(
        int requested, string sectionName, int processGroupId, List<ProcessGroup> processGroups)
    {
        if (processGroupId <= 0) return requested;
        var pg = processGroups.FirstOrDefault(p => p.Id == processGroupId);
        var aligned = pg?.GetSectionSequence(sectionName);
        return aligned ?? requested;
    }

    /// <summary>列表/详情共用投影（不含明细与附件集合，仅计数，避免笛卡尔积）</summary>
    private static System.Linq.Expressions.Expression<Func<InspectionPatrol, InspectionPatrolDto>> MapToSummaryDto() => e => new InspectionPatrolDto
    {
        Id = e.Id,
        PatrolDate = e.PatrolDate,
        Inspector = e.Inspector,
        DataSource = e.DataSource,
        ProductionBatchId = e.ProductionBatchId,
        BatchNo = e.BatchNo,
        WorkOrderNo = e.WorkOrderNo,
        ProcessGroupId = e.ProcessGroupId,
        ProcessName = e.ProcessName,
        ManufacturingSpec = e.ManufacturingSpec,
        SectionName = e.SectionName,
        SequenceNumber = e.SequenceNumber,
        ProductStatus = e.ProductStatus,
        PlantGrade = e.PlantGrade,
        ProductionUnit = e.ProductionUnit,
        EquipmentName = e.EquipmentName,
        ProductionOperator = e.ProductionOperator,
        ItemCount = e.Items.Count,
        NeedRectification = e.NeedRectification,
        RectificationDescription = e.RectificationDescription,
        VerificationResult = e.VerificationResult,
        RectificationOperator = e.RectificationOperator,
        IsClosed = e.IsClosed,
        AttachmentCount = e.Attachments.Count,
        CreatedTime = e.CreatedTime,
        UpdatedTime = e.UpdatedTime
    };
}
