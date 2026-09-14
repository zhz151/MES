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
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces.Batch;
using MES.Services.Printing;
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
using MES.Services.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Quality;

/// <summary>
/// NCR 不合格品报告服务实现
/// </summary>
public class NcrService : INcrService
{
    private readonly AppDbContext _context;
    private readonly ILogger<NcrService> _logger;
    private readonly IConfigParameterService _configService;
    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();
    private readonly IMemoryCache _cache;
    private readonly IAttachmentStorage _storage;

    public NcrService(AppDbContext context, ILogger<NcrService> logger,
        IConfigParameterService configService,
        IMemoryCache cache,
        IAttachmentStorage storage)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
        _cache = cache;
        _storage = storage;
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

    public async Task<NcrDto?> GetByIdAsync(int id)
    {
        var dto = await _context.Ncrs
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(ToDto())
            .FirstOrDefaultAsync();

        if (dto != null)
            await FillProductionBatchIdsAsync(new[] { dto });

        return dto;
    }

    /// <summary>
    /// 按生产编号批量构造「批号 → 生产批次 Id」映射（大小写不敏感，未命中批号的键不在字典中）。
    /// NCR 表仅存 BatchNo 字符串（无外键），故查询后按批号一次性反查 ProductionBatch 表；
    /// 未命中（历史数据批次可能已清理）由调用方保持 0，前端据此降级为纯文本不渲染链接。
    /// </summary>
    private async Task<Dictionary<string, int>> GetBatchIdMapAsync(IEnumerable<string?> batchNos)
    {
        var keys = batchNos
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (keys.Count == 0) return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var rows = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => keys.Contains(b.BatchNo))
            .Select(b => new { b.BatchNo, b.Id })
            .ToListAsync();

        // ⚠️ SQL Server 大小写不敏感、C# 内存默认 Ordinal 区分大小写 → 必须显式 OrdinalIgnoreCase
        return rows
            .GroupBy(r => r.BatchNo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>回填 NCR 列表的生产批次 Id（未命中保持 0）</summary>
    private async Task FillProductionBatchIdsAsync(IEnumerable<NcrDto> items)
    {
        var list = items.ToList();
        var map = await GetBatchIdMapAsync(list.Select(i => i.BatchNo));
        foreach (var item in list)
        {
            if (!string.IsNullOrWhiteSpace(item.BatchNo) && map.TryGetValue(item.BatchNo, out var batchId))
                item.ProductionBatchId = batchId;
        }
    }

    /// <summary>回填 NCR 待处理批次卡片的生产批次 Id（未命中保持 0）</summary>
    private async Task FillProductionBatchIdsAsync(List<NcrPendingCheckDto> items)
    {
        var map = await GetBatchIdMapAsync(items.Select(i => i.BatchNo));
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.BatchNo) && map.TryGetValue(item.BatchNo, out var batchId))
                item.ProductionBatchId = batchId;
        }
    }

    public async Task<PagedResult<NcrDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.Ncrs
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                r.BatchNo.Contains(kw) ||
                (r.WorkOrderNo != null && r.WorkOrderNo.Contains(kw)) ||
                (r.PlantGrade != null && r.PlantGrade.Contains(kw)) ||
                (r.Specification != null && r.Specification.Contains(kw)) ||
                (r.ReportDepartment != null && r.ReportDepartment.Contains(kw)) ||
                (r.Reporter != null && r.Reporter.Contains(kw)) ||
                (r.ProblemDescription != null && r.ProblemDescription.Contains(kw)) ||
                (r.ConcessionRemark != null && r.ConcessionRemark.Contains(kw)) ||
                (r.DisposalRemark != null && r.DisposalRemark.Contains(kw)) ||
                (r.RootCauseAnalysis != null && r.RootCauseAnalysis.Contains(kw)) ||
                (r.AnalysisConfirmer != null && r.AnalysisConfirmer.Contains(kw)) ||
                (r.ResponsibleDept != null && r.ResponsibleDept.Contains(kw)) ||
                (r.ResponsiblePerson != null && r.ResponsiblePerson.Contains(kw)) ||
                (r.PersonDisposition != null && r.PersonDisposition.Contains(kw)) ||
                (r.CorrectiveAction != null && r.CorrectiveAction.Contains(kw)) ||
                (r.ActionPlanner != null && r.ActionPlanner.Contains(kw)) ||
                (r.ActionVerifier != null && r.ActionVerifier.Contains(kw)) ||
                (r.ActionResult != null && r.ActionResult.Contains(kw)));
        }

        if (query.ReportDateFrom.HasValue)
            queryable = queryable.Where(r => r.ReportDate >= query.ReportDateFrom.Value);
        if (query.ReportDateTo.HasValue)
            queryable = queryable.Where(r => r.ReportDate < query.ReportDateTo.Value.AddDays(1));

        queryable = queryable.ApplyFilters(query.Filters);
        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.SortBy ?? "reportdate", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(ToDto())
            .ToListAsync();

        await FillProductionBatchIdsAsync(items);

        return new PagedResult<NcrDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<NcrDto> CreateAsync(CreateNcrRequest request)
    {
        if (request.Status.HasValue && request.Status != NcrStatus.Ignored)
            throw new BusinessException("登记状态仅支持「忽略」");

        // 尝试根据 BatchNo 填充冗余字段
        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.BatchNo == request.BatchNo)
            .Select(b => new
            {
                b.WorkOrderNo,
                b.PlantGrade,
                b.Specification
            })
            .FirstOrDefaultAsync();

        var entity = new Ncr
        {
            // G1
            ReportDate = request.ReportDate,
            ReportDepartment = request.ReportDepartment,
            Reporter = request.Reporter,
            PipeCategory = request.PipeCategory,
            BatchNo = request.BatchNo,
            WorkOrderNo = request.WorkOrderNo ?? batch?.WorkOrderNo,
            PlantGrade = request.PlantGrade ?? batch?.PlantGrade,
            Specification = request.Specification ?? batch?.Specification,
            DefectiveQuantity = request.DefectiveQuantity,
            DefectiveWeight = request.DefectiveWeight,
            ProblemDescription = request.ProblemDescription,
            ConcessionQuantity = request.ConcessionQuantity,
            ConcessionWeight = request.ConcessionWeight,
            ConcessionRemark = request.ConcessionRemark,
            SourceInspectionItem = request.SourceInspectionItem,
            SourceGroupKey = request.SourceGroupKey,
            FlowDirection = request.FlowDirection,
            NonconformingFeedbackId = request.NonconformingFeedbackId,

            // G2
            DisposalMethod = NcrDisposalKeys.ToKey(request.DisposalMethod),
            DisposalRemark = request.DisposalRemark,
            DisposalIsCompleted = request.DisposalIsCompleted,
            DisposalCompleteDate = request.DisposalCompleteDate,

            // G3
            RootCauseAnalysis = request.RootCauseAnalysis,
            Severity = request.Severity,
            AnalysisConfirmer = request.AnalysisConfirmer,
            AnalysisConfirmDate = request.AnalysisConfirmDate,

            // G4
            ResponsibilityCategory = request.ResponsibilityCategory,
            ResponsibleDept = request.ResponsibleDept,
            OperationDate = request.OperationDate,
            ResponsiblePerson = request.ResponsiblePerson,
            PersonDisposition = request.PersonDisposition,
            PersonIsCompleted = request.PersonIsCompleted,
            PersonCompleteDate = request.PersonCompleteDate,

            // G5
            CorrectiveAction = request.CorrectiveAction,
            ActionPlanner = request.ActionPlanner,
            ActionPlanDate = request.ActionPlanDate,
            ActionVerifier = request.ActionVerifier,
            ActionVerifyDate = request.ActionVerifyDate,
            ActionResult = request.ActionResult,
            VerifyResult = request.VerifyResult,

            // 状态（登记即处理中；「忽略」动作登记即忽略）
            Status = request.Status == NcrStatus.Ignored ? NcrStatus.Ignored : NcrStatus.Processing
        };

        // 自动关闭：三个条件全部满足直接设为已关闭（忽略单不参与）
        if (entity.Status != NcrStatus.Ignored
            && entity.DisposalIsCompleted
            && entity.PersonIsCompleted
            && (entity.VerifyResult == VerifyResult.Passed || entity.VerifyResult == VerifyResult.NotApplicable))
        {
            entity.Status = NcrStatus.Closed;
        }

        _context.Ncrs.Add(entity);
        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<NcrDto> UpdateAsync(int id, UpdateNcrRequest request)
    {
        var entity = await _context.Ncrs.FindAsync(id)
            ?? throw new BusinessException("不合格品报告不存在");

        // G1
        entity.ReportDate = request.ReportDate;
        entity.ReportDepartment = request.ReportDepartment ?? entity.ReportDepartment;
        entity.Reporter = request.Reporter ?? entity.Reporter;
        entity.PipeCategory = request.PipeCategory;
        entity.WorkOrderNo = request.WorkOrderNo ?? entity.WorkOrderNo;
        entity.PlantGrade = request.PlantGrade ?? entity.PlantGrade;
        entity.Specification = request.Specification ?? entity.Specification;
        entity.DefectiveQuantity = request.DefectiveQuantity ?? entity.DefectiveQuantity;
        entity.DefectiveWeight = request.DefectiveWeight ?? entity.DefectiveWeight;
        entity.ProblemDescription = request.ProblemDescription ?? entity.ProblemDescription;
        entity.ConcessionQuantity = request.ConcessionQuantity ?? entity.ConcessionQuantity;
        entity.ConcessionWeight = request.ConcessionWeight ?? entity.ConcessionWeight;
        entity.ConcessionRemark = request.ConcessionRemark ?? entity.ConcessionRemark;
        entity.SourceInspectionItem = request.SourceInspectionItem ?? entity.SourceInspectionItem;
        // 流向为只读字段（由检验记录带出），编辑时不可改

        // G2
        entity.DisposalMethod = NcrDisposalKeys.ToKey(request.DisposalMethod) ?? entity.DisposalMethod;
        entity.DisposalRemark = request.DisposalRemark ?? entity.DisposalRemark;
        entity.DisposalIsCompleted = request.DisposalIsCompleted;
        entity.DisposalCompleteDate = request.DisposalCompleteDate ?? entity.DisposalCompleteDate;

        // G3
        entity.RootCauseAnalysis = request.RootCauseAnalysis ?? entity.RootCauseAnalysis;
        entity.Severity = request.Severity ?? entity.Severity;
        entity.AnalysisConfirmer = request.AnalysisConfirmer ?? entity.AnalysisConfirmer;
        entity.AnalysisConfirmDate = request.AnalysisConfirmDate ?? entity.AnalysisConfirmDate;

        // G4
        entity.ResponsibilityCategory = request.ResponsibilityCategory ?? entity.ResponsibilityCategory;
        entity.ResponsibleDept = request.ResponsibleDept ?? entity.ResponsibleDept;
        entity.OperationDate = request.OperationDate ?? entity.OperationDate;
        entity.ResponsiblePerson = request.ResponsiblePerson ?? entity.ResponsiblePerson;
        entity.PersonDisposition = request.PersonDisposition ?? entity.PersonDisposition;
        entity.PersonIsCompleted = request.PersonIsCompleted;
        entity.PersonCompleteDate = request.PersonCompleteDate ?? entity.PersonCompleteDate;

        // G5
        entity.CorrectiveAction = request.CorrectiveAction ?? entity.CorrectiveAction;
        entity.ActionPlanner = request.ActionPlanner ?? entity.ActionPlanner;
        entity.ActionPlanDate = request.ActionPlanDate ?? entity.ActionPlanDate;
        entity.ActionVerifier = request.ActionVerifier ?? entity.ActionVerifier;
        entity.ActionVerifyDate = request.ActionVerifyDate ?? entity.ActionVerifyDate;
        entity.ActionResult = request.ActionResult ?? entity.ActionResult;
        entity.VerifyResult = request.VerifyResult ?? entity.VerifyResult;

        // 自动状态流转：关闭条件全部满足时自动设为已关闭
        if (entity.Status != NcrStatus.Closed
            && entity.DisposalIsCompleted
            && entity.PersonIsCompleted
            && (entity.VerifyResult == VerifyResult.Passed || entity.VerifyResult == VerifyResult.NotApplicable))
        {
            entity.Status = NcrStatus.Closed;
        }

        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Ncrs.FindAsync(id)
            ?? throw new BusinessException("不合格品报告不存在");

        _context.Ncrs.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<NcrDto> UpdateStatusAsync(int id, UpdateNcrStatusRequest request)
    {
        var entity = await _context.Ncrs.FindAsync(id)
            ?? throw new BusinessException("不合格品报告不存在");

        var newStatus = request.Status;

        // 关闭时检查必要条件
        if (newStatus == NcrStatus.Closed)
        {
            if (!entity.DisposalIsCompleted)
                throw new BusinessException("处置未完结，不能关闭");
            if (!entity.PersonIsCompleted)
                throw new BusinessException("责任人处理未完结，不能关闭");
            if (entity.VerifyResult != Core.Enums.VerifyResult.Passed && entity.VerifyResult != Core.Enums.VerifyResult.NotApplicable)
                throw new BusinessException("纠正措施验证未通过，不能关闭");
        }

        entity.Status = newStatus;
        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<NcrLookupResultDto?> LookupBatchAsync(string batchNo)
    {
        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.BatchNo == batchNo)
            .Select(b => new { b.Id, b.WorkOrderNo, b.SalesOrderNo, b.TagNo, b.PlantGrade, b.Specification })
            .FirstOrDefaultAsync();
        if (batch == null) return null;

        // 手动输入生产编号时自动填入：该批次检验记录的次品支数/重量合计
        //（过程检验取理论重量、成品检验取实际重量；各处置类型合计）
        var processDefects = await _context.ProcessInspections
            .AsNoTracking()
            .Where(pi => pi.ProductionBatchId == batch.Id)
            .Select(pi => new
            {
                Qty = (pi.DefectReworkQuantity ?? 0) + (pi.DefectWarehouseQuantity ?? 0) + (pi.DefectScrapQuantity ?? 0) + (pi.DefectReturnQuantity ?? 0),
                Weight = (pi.TheoreticalReworkWeight ?? 0) + (pi.TheoreticalWarehouseWeight ?? 0) + (pi.TheoreticalScrapWeight ?? 0) + (pi.TheoreticalReturnWeight ?? 0)
            })
            .ToListAsync();
        var finalDefects = await _context.FinalInspections
            .AsNoTracking()
            .Where(fi => fi.ProductionBatchId == batch.Id)
            .Select(fi => new
            {
                Qty = (fi.DefectReworkQuantity ?? 0) + (fi.DefectInProcessWarehouseQuantity ?? 0) + (fi.DefectWarehouseQuantity ?? 0) + (fi.DefectScrapQuantity ?? 0) + (fi.DefectReturnQuantity ?? 0),
                Weight = (fi.DefectReworkWeight ?? 0) + (fi.DefectInProcessWarehouseWeight ?? 0) + (fi.DefectWarehouseWeight ?? 0) + (fi.DefectScrapWeight ?? 0) + (fi.DefectReturnWeight ?? 0)
            })
            .ToListAsync();

        return new NcrLookupResultDto
        {
            ProductionBatchId = batch.Id,
            WorkOrderNo = batch.WorkOrderNo,
            SalesOrderNo = batch.SalesOrderNo,
            TagNo = batch.TagNo,
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            DefectiveQuantity = processDefects.Sum(x => x.Qty) + finalDefects.Sum(x => x.Qty),
            DefectiveWeight = processDefects.Sum(x => x.Weight) + finalDefects.Sum(x => x.Weight)
        };
    }

    /// <summary>
    /// 取不合格报告「来源」关联的照片（建单/编辑页生产编号旁的照片入口，只读）。
    /// <para>
    /// 主动来源（不合格反馈）传 <paramref name="feedbackId"/>；被动来源传 <paramref name="groupKey"/>
    /// （格式 <c>{来源类型}|{批次Id}|{工序}|{成检类型}|{检验项目}|{检验记录Id}</c>，与 <c>Ncr.SourceGroupKey</c> 同源）。
    /// 被动来源按<b>检验记录 Id</b> 精确定位该条记录的照片；<b>该记录无照片时返回空列表</b>（前端据此不渲染入口）。
    /// </para>
    /// </summary>
    public async Task<List<NcrSourcePhotoGroupDto>> GetSourcePhotosAsync(string? groupKey, int? feedbackId)
    {
        var groups = new List<NcrSourcePhotoGroupDto>();

        // 主动来源：不合格反馈单（唯一记录）
        if (feedbackId is > 0)
        {
            var feedback = await _context.NonconformingFeedbacks.AsNoTracking()
                .Where(f => f.Id == feedbackId)
                .Select(f => new { f.Id, f.ReportDate })
                .FirstOrDefaultAsync();
            if (feedback == null) return groups;

            var feedbackPhotos = await _context.NonconformingFeedbackAttachments.AsNoTracking()
                .Where(a => a.FeedbackId == feedback.Id)
                .OrderBy(a => a.SortOrder).ThenBy(a => a.Id)
                .Select(a => new NcrSourcePhotoDto { AttachmentId = a.Id, FileName = a.FileName, ContentType = a.ContentType })
                .ToListAsync();

            if (feedbackPhotos.Count > 0)
            {
                groups.Add(new NcrSourcePhotoGroupDto
                {
                    Kind = nameof(NcrPendingSourceType.NonconformingFeedback),
                    RecordId = feedback.Id,
                    RecordDate = feedback.ReportDate,
                    Photos = feedbackPhotos
                });
            }
            return groups;
        }

        if (string.IsNullOrWhiteSpace(groupKey)) return groups;

        // 记录级定位键：{来源类型}|{批次Id}|{工序}|{成检类型}|{检验项目}|{检验记录Id}
        var parts = groupKey.Split('|');
        if (parts.Length < 6 || !int.TryParse(parts[5], out var recordId) || recordId <= 0) return groups;

        if (parts[0] == nameof(NcrPendingSourceType.ProcessInspection))
        {
            var record = await _context.ProcessInspections.AsNoTracking()
                .Where(pi => pi.Id == recordId)
                .Select(pi => new { pi.InspectionDate, pi.Inspector })
                .FirstOrDefaultAsync();
            if (record == null) return groups;

            var photos = await _context.ProcessInspectionAttachments.AsNoTracking()
                .Where(a => a.ProcessInspectionId == recordId)
                .OrderBy(a => a.SortOrder).ThenBy(a => a.Id)
                .Select(a => new NcrSourcePhotoDto { AttachmentId = a.Id, FileName = a.FileName, ContentType = a.ContentType })
                .ToListAsync();

            if (photos.Count > 0)
            {
                groups.Add(new NcrSourcePhotoGroupDto
                {
                    Kind = nameof(NcrPendingSourceType.ProcessInspection),
                    RecordId = recordId,
                    RecordDate = record.InspectionDate,
                    Inspector = record.Inspector,
                    Photos = photos
                });
            }
        }
        else if (parts[0] == nameof(NcrPendingSourceType.FinalInspection))
        {
            var record = await _context.FinalInspections.AsNoTracking()
                .Where(fi => fi.Id == recordId)
                .Select(fi => new { fi.InspectionDate, Inspector = fi.Operator })
                .FirstOrDefaultAsync();
            if (record == null) return groups;

            var photos = await _context.FinalInspectionAttachments.AsNoTracking()
                .Where(a => a.FinalInspectionId == recordId)
                .OrderBy(a => a.SortOrder).ThenBy(a => a.Id)
                .Select(a => new NcrSourcePhotoDto { AttachmentId = a.Id, FileName = a.FileName, ContentType = a.ContentType })
                .ToListAsync();

            if (photos.Count > 0)
            {
                groups.Add(new NcrSourcePhotoGroupDto
                {
                    Kind = nameof(NcrPendingSourceType.FinalInspection),
                    RecordId = recordId,
                    RecordDate = record.InspectionDate,
                    Inspector = record.Inspector,
                    Photos = photos
                });
            }
        }

        return groups;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.NcrFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            var queryable = _context.Ncrs.AsNoTracking();

            // 注意：枚举列（PipeCategory/FlowDirection 流向/Severity/VerifyResult/Status 等）与字典列（ResponsibilityCategory 责任类别走 NcrResponsibilityKey、
            // DisposalMethod 处置方式走 NcrDisposalKey）
            // 不在此处返回，由前端 EnumOptions/字典 options fallback 直接提供带中文 Display 的选项，避免映射丢失。
            var results = await queryable
                .Select(r => new
                {
                    r.ReportDepartment,
                    r.Reporter,
                    r.BatchNo,
                    r.WorkOrderNo,
                    r.PlantGrade,
                    r.Specification,
                    r.ProblemDescription,
                    r.DisposalRemark,
                    r.RootCauseAnalysis,
                    r.AnalysisConfirmer,
                    r.ResponsibleDept,
                    r.ResponsiblePerson,
                    r.PersonDisposition,
                    r.CorrectiveAction,
                    r.ActionPlanner,
                    r.ActionVerifier,
                    r.ActionResult,
                    r.ReportDate,
                    r.DisposalCompleteDate,
                    r.AnalysisConfirmDate
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["ReportDepartment"] = results.Select(x => x.ReportDepartment).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["Reporter"] = results.Select(x => x.Reporter).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["BatchNo"] = results.Select(x => x.BatchNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["WorkOrderNo"] = results.Select(x => x.WorkOrderNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["PlantGrade"] = results.Select(x => x.PlantGrade).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["Specification"] = results.Select(x => x.Specification).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["ProblemDescription"] = results.Select(x => x.ProblemDescription).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["DisposalRemark"] = results.Select(x => x.DisposalRemark).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["RootCauseAnalysis"] = results.Select(x => x.RootCauseAnalysis).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["AnalysisConfirmer"] = results.Select(x => x.AnalysisConfirmer).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["ResponsibleDept"] = results.Select(x => x.ResponsibleDept).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["ResponsiblePerson"] = results.Select(x => x.ResponsiblePerson).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["PersonDisposition"] = results.Select(x => x.PersonDisposition).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["CorrectiveAction"] = results.Select(x => x.CorrectiveAction).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["ActionPlanner"] = results.Select(x => x.ActionPlanner).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["ActionVerifier"] = results.Select(x => x.ActionVerifier).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["ActionResult"] = results.Select(x => x.ActionResult).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["ReportDate"] = results.Select(x => x.ReportDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["DisposalCompleteDate"] = results.Where(x => x.DisposalCompleteDate.HasValue).Select(x => x.DisposalCompleteDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["AnalysisConfirmDate"] = results.Where(x => x.AnalysisConfirmDate.HasValue).Select(x => x.AnalysisConfirmDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    // ========== 私有方法 ==========

    public async Task<List<NcrPendingCheckDto>> GetPendingChecksAsync()
    {
        var results = new List<NcrPendingCheckDto>();

        // 阈值（2026-09-12 统一口径，不再按流向分设）：单条检验记录的不合格合计（让步放行 + 各流向）
        // 需同时【严格大于】绝对支数阈值与占比阈值才列为待处理
        var ncrThresholdCount = await GetConfigAsync("NcrThreshold", "Count", 5m);
        var ncrThresholdPercent = await GetConfigAsync("NcrThreshold", "Percent", 0.10m);

        // ======== 1. 过程检验分析（2026-09-13 起逐条记录判定，不再按「批次+工序」组合计）========
        var processRows = await _context.ProcessInspections
            .AsNoTracking()
            .Where(pi => pi.Quantity > 0)
            .Select(pi => new
            {
                pi.Id,
                pi.ProductionBatchId,
                pi.ProcessName,
                pi.InspectionItem,
                pi.SectionName,
                pi.ManufacturingSpec,
                pi.BatchNo,
                pi.InspectionDate,
                pi.Inspector,
                pi.DefectDescription,
                pi.ConcessionRemark,
                Rework = pi.DefectReworkQuantity ?? 0,
                Warehouse = pi.DefectWarehouseQuantity ?? 0,
                Scrap = pi.DefectScrapQuantity ?? 0,
                Return = pi.DefectReturnQuantity ?? 0,
                Concession = pi.QualifiedConcessionQuantity ?? 0,
                ReworkWeight = pi.TheoreticalReworkWeight ?? 0,
                WarehouseWeight = pi.TheoreticalWarehouseWeight ?? 0,
                ScrapWeight = pi.TheoreticalScrapWeight ?? 0,
                ReturnWeight = pi.TheoreticalReturnWeight ?? 0,
                Quantity = pi.Quantity ?? 0,
            })
            .ToListAsync();

        var procBatchIds = processRows.Select(a => a.ProductionBatchId).Distinct().ToList();
        var procBatchLookup = await GetBatchLookupAsync(procBatchIds);

        foreach (var a in processRows)
        {
            if (a.Quantity <= 0) continue;

            // 该条记录的不合格合计 = 让步放行支 + 各档流向支（让步放行计入分子但不单独成行）
            var defectQty = a.Rework + a.Warehouse + a.Scrap + a.Return;
            var overageQty = defectQty + a.Concession;
            if (overageQty <= (int)ncrThresholdCount) continue;
            if ((decimal)overageQty / a.Quantity <= ncrThresholdPercent) continue;

            var batch = procBatchLookup.GetValueOrDefault(a.ProductionBatchId);
            results.Add(new NcrPendingCheckDto
            {
                BatchNo = a.BatchNo ?? batch?.BatchNo ?? "",
                ProductionBatchId = a.ProductionBatchId,
                WorkOrderNo = batch?.WorkOrderNo,
                PlantGrade = batch?.PlantGrade,
                Specification = a.ManufacturingSpec,
                ReportDate = a.InspectionDate,
                SourceType = nameof(NcrPendingSourceType.ProcessInspection),
                Bucket = NcrPendingBucket.OverageMissing,
                InspectionRecordId = a.Id,
                InspectionItem = a.InspectionItem,
                ProcessName = a.ProcessName,
                SectionName = a.SectionName,
                Inspector = a.Inspector,
                DefectDescription = a.DefectDescription,
                ConcessionRemark = a.ConcessionRemark,
                ReworkQuantity = a.Rework,
                InProcessWarehouseQuantity = a.Warehouse,
                ScrapQuantity = a.Scrap,
                ReturnQuantity = a.Return,
                ConcessionQuantity = a.Concession,
                FlowDirection = PickMajorFlowDirection(a.Rework, a.Warehouse, 0, a.Scrap, a.Return),
                DefectQuantity = defectQty,
                DefectiveWeight = a.ReworkWeight + a.WarehouseWeight + a.ScrapWeight + a.ReturnWeight,
                TotalQuantity = a.Quantity,
                Percentage = Math.Round((decimal)overageQty / a.Quantity * 100, 1)
            });
            results[^1].GroupKey = BuildGroupKey(results[^1]);
        }

        // ======== 2. 成品检验分析（2026-09-13 起逐条记录判定，不再按「批次+成检类型+检验项目」组合计）========
        var finalRows = await _context.FinalInspections
            .AsNoTracking()
            .Where(fi => fi.Quantity > 0)
            .Select(fi => new
            {
                fi.Id,
                fi.ProductionBatchId,
                fi.InspectionType,
                fi.InspectionItem,
                fi.BatchNo,
                fi.InspectionDate,
                fi.DefectDescription,
                fi.ConcessionRemark,
                Inspector = fi.Operator,
                ManufacturingItem = fi.ProductionBatch.ManufacturingItem,
                Specification = fi.ProductionBatch.Specification,
                WorkOrderNo = fi.ProductionBatch.WorkOrderNo,
                PlantGrade = fi.ProductionBatch.PlantGrade,
                Rework = fi.DefectReworkQuantity ?? 0,
                InProcessWarehouse = fi.DefectInProcessWarehouseQuantity ?? 0,
                Warehouse = fi.DefectWarehouseQuantity ?? 0,
                Scrap = fi.DefectScrapQuantity ?? 0,
                Return = fi.DefectReturnQuantity ?? 0,
                Concession = fi.QualifiedConcessionQuantity ?? 0,
                ReworkWeight = fi.DefectReworkWeight ?? 0,
                InProcessWarehouseWeight = fi.DefectInProcessWarehouseWeight ?? 0,
                WarehouseWeight = fi.DefectWarehouseWeight ?? 0,
                ScrapWeight = fi.DefectScrapWeight ?? 0,
                ReturnWeight = fi.DefectReturnWeight ?? 0,
                Quantity = fi.Quantity ?? 0,
            })
            .ToListAsync();

        foreach (var a in finalRows)
        {
            if (a.Quantity <= 0) continue;

            // 该条记录的不合格合计 = 让步放行支 + 各档流向支（让步放行计入分子但不单独成行）
            var defectQty = a.Rework + a.InProcessWarehouse + a.Warehouse + a.Scrap + a.Return;
            var overageQty = defectQty + a.Concession;
            if (overageQty <= (int)ncrThresholdCount) continue;
            if ((decimal)overageQty / a.Quantity <= ncrThresholdPercent) continue;

            results.Add(new NcrPendingCheckDto
            {
                BatchNo = a.BatchNo ?? "",
                ProductionBatchId = a.ProductionBatchId,
                WorkOrderNo = a.WorkOrderNo,
                PlantGrade = a.PlantGrade,
                Specification = a.Specification,
                ReportDate = a.InspectionDate,
                SourceType = nameof(NcrPendingSourceType.FinalInspection),
                Bucket = NcrPendingBucket.OverageMissing,
                InspectionRecordId = a.Id,
                InspectionItem = a.InspectionItem.ToString(),
                InspectionType = a.InspectionType,
                MaterialName = a.ManufacturingItem,
                Inspector = a.Inspector,
                DefectDescription = a.DefectDescription,
                ConcessionRemark = a.ConcessionRemark,
                ReworkQuantity = a.Rework,
                InProcessWarehouseQuantity = a.InProcessWarehouse,
                FinishedWarehouseQuantity = a.Warehouse,
                ScrapQuantity = a.Scrap,
                ReturnQuantity = a.Return,
                ConcessionQuantity = a.Concession,
                FlowDirection = PickMajorFlowDirection(a.Rework, a.InProcessWarehouse, a.Warehouse, a.Scrap, a.Return),
                DefectQuantity = defectQty,
                DefectiveWeight = a.ReworkWeight + a.InProcessWarehouseWeight + a.WarehouseWeight + a.ScrapWeight + a.ReturnWeight,
                TotalQuantity = a.Quantity,
                Percentage = Math.Round((decimal)overageQty / a.Quantity * 100, 1)
            });
            results[^1].GroupKey = BuildGroupKey(results[^1]);
        }

        // ======== 2.1 被动条去重：该记录已有 NCR 或该维度已有主动反馈的不再重复列出 ========
        // NCR 侧按 Ncr.SourceGroupKey 比对（记录级 6 段键；存量 5 段旧键视为覆盖该维度全部记录）；
        // 主动侧按「批次+工序」（过程检验口径）与「批次+检验项目」（成品检验口径）比对。
        var existingNcrGroupKeys = new HashSet<string>(
            await _context.Ncrs.AsNoTracking()
                .Where(n => n.SourceGroupKey != null && n.SourceGroupKey != "")
                .Select(n => n.SourceGroupKey!)
                .ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        var feedbackKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in await _context.NonconformingFeedbacks.AsNoTracking()
                     .Select(f => new { f.ProductionBatchId, f.ProcessName, f.InspectionItem })
                     .ToListAsync())
        {
            if (!string.IsNullOrEmpty(f.ProcessName))
                feedbackKeys.Add($"{f.ProductionBatchId}|{f.ProcessName}");
            if (f.InspectionItem.HasValue)
                feedbackKeys.Add($"{f.ProductionBatchId}|{f.InspectionItem.Value}");
        }

        results.RemoveAll(r =>
        {
            if (r.Bucket != NcrPendingBucket.OverageMissing) return false;
            if (existingNcrGroupKeys.Contains(BuildGroupKey(r))) return true;
            // 存量兼容：记录级改造前登记的 NCR 只有 5 段旧键，视为该维度全部记录已处理
            if (existingNcrGroupKeys.Contains(BuildLegacyGroupKey(r))) return true;
            var dedupKey = r.SourceType == nameof(NcrPendingSourceType.ProcessInspection)
                ? $"{r.ProductionBatchId}|{r.ProcessName}"
                : $"{r.ProductionBatchId}|{r.InspectionItem}";
            return feedbackKeys.Contains(dedupKey);
        });

        // ======== 3. 不合格反馈（人工上报）========
        // 不受 NcrThreshold 阈值约束：凡尚未生成 NCR 的反馈单一律列为待处理；
        // 是否「已处理」以 Ncr.NonconformingFeedbackId 是否引用该反馈单为准。
        var feedbacks = await _context.NonconformingFeedbacks
            .AsNoTracking()
            .Where(f => !_context.Ncrs.Any(n => n.NonconformingFeedbackId == f.Id))
            .Select(f => new
            {
                f.Id,
                f.ReportDate,
                f.BatchNo,
                f.WorkOrderNo,
                f.PlantGrade,
                f.ManufacturingSpec,
                f.ProcessName,
                f.SectionName,
                f.ProductionBatchId,
                f.Reporter,
                f.IncomingQuantity,
                f.DefectQuantity,
                f.DefectWeight,
                f.ProblemDescription
            })
            .ToListAsync();

        foreach (var f in feedbacks)
        {
            var totalQty = f.IncomingQuantity ?? 0;
            var defectQty = f.DefectQuantity ?? 0;
            results.Add(new NcrPendingCheckDto
            {
                BatchNo = f.BatchNo,
                ProductionBatchId = f.ProductionBatchId,
                WorkOrderNo = f.WorkOrderNo,
                PlantGrade = f.PlantGrade,
                Specification = f.ManufacturingSpec,
                ReportDate = f.ReportDate,
                SourceType = nameof(NcrPendingSourceType.NonconformingFeedback),
                Bucket = NcrPendingBucket.NormalSubmitted,
                NonconformingFeedbackId = f.Id,
                ProcessName = f.ProcessName,
                SectionName = f.SectionName,
                Inspector = f.Reporter,
                DefectDescription = f.ProblemDescription,
                // 流向留空：人工上报不预设流向（无检验记录可带出），处置方式由质量负责人在 NCR 中判定
                FlowDirection = null,
                DefectQuantity = defectQty,
                DefectiveWeight = f.DefectWeight,
                TotalQuantity = totalQty,
                Percentage = totalQty > 0 ? Math.Round((decimal)defectQty / totalQty * 100, 1) : 0
            });
        }

        await FillProductionBatchIdsAsync(results);

        return results;
    }

    /// <summary>
    /// 被动「待处理记录」定位键：<c>{来源类型}|{生产批次Id}|{工序}|{成检类型}|{检验项目}|{检验记录Id}</c>，不适用段为空。
    /// 与 <c>Ncr.SourceGroupKey</c> 一致（该条检验记录已有 NCR 时不再列出，含「忽略」状态）。
    /// </summary>
    private static string BuildGroupKey(NcrPendingCheckDto r)
        => $"{BuildLegacyGroupKey(r)}|{r.InspectionRecordId}";

    /// <summary>
    /// 记录级改造前（2026-09-13 之前）的 5 段旧键：<c>{来源类型}|{生产批次Id}|{工序}|{成检类型}|{检验项目}</c>。
    /// 仅用于存量 NCR 的排重兼容——旧键按「该维度全部检验记录已处理」处理。
    /// </summary>
    private static string BuildLegacyGroupKey(NcrPendingCheckDto r)
        => $"{r.SourceType}|{r.ProductionBatchId}|{r.ProcessName ?? ""}|{r.InspectionType ?? ""}|{r.InspectionItem ?? ""}";

    /// <summary>
    /// 取组内支数最多的流向作次品主流向（并列按 返整 &gt; 入在制库 &gt; 可入备库 &gt; 入次品库 &gt; 退货 定序）；
    /// 各档全为 0 时返回 null。
    /// </summary>
    private static FlowDirection? PickMajorFlowDirection(int rework, int inProcessWarehouse, int finishedWarehouse, int scrap, int ret)
    {
        var candidates = new (int Qty, FlowDirection Direction)[]
        {
            (rework, FlowDirection.Rework),
            (inProcessWarehouse, FlowDirection.InProcessWarehouse),
            (finishedWarehouse, FlowDirection.FinishedWarehouse),
            (scrap, FlowDirection.Scrap),
            (ret, FlowDirection.Return)
        };

        var best = candidates[0];
        foreach (var c in candidates)
        {
            if (c.Qty > best.Qty) best = c;
        }
        return best.Qty > 0 ? best.Direction : null;
    }

    /// <summary>
    /// 获取不合格品月度汇总：按（责任类别→责任部门→处置方式）三级分组，12 个月次品支数/重量矩阵。
    /// 分月基准 = 反馈日期（ReportDate）；责任类别/责任部门/处置方式为空归「未填写」分组，全量守恒。
    /// <b>状态为「忽略」的报告不纳入统计</b>（忽略 = 该组无需完整不合格报告，仅登记留痕）。
    /// 返回行已按 责任类别→责任部门→处置方式 排序、同组相邻，便于前端合并单元格。
    /// </summary>
    public async Task<NcrMonthlySummaryDto> GetMonthlySummaryAsync()
    {
        var year = DateTime.Today.Year;

        var ncrRows = await _context.Ncrs
            .AsNoTracking()
            .Where(n => n.ReportDate.Year == year && n.Status != NcrStatus.Ignored)
            .Select(n => new
            {
                n.ReportDate,
                Category = n.ResponsibilityCategory,
                Dept = n.ResponsibleDept,
                n.DisposalMethod,
                n.FlowDirection,
                Qty = n.DefectiveQuantity,
                Weight = n.DefectiveWeight
            })
            .ToListAsync();

        var rows = ncrRows
            .GroupBy(r => new
            {
                Category = string.IsNullOrWhiteSpace(r.Category) ? "" : r.Category.Trim(),
                Dept = string.IsNullOrWhiteSpace(r.Dept) ? "" : r.Dept.Trim(),
                // 行维度 = 处置方式（8 档字典）；已判定处置的用处置方式，
                // 未判定但有流向（检验带出）的按流向→处置默认映射归集，两者皆空归「未填写」
                Method = !string.IsNullOrWhiteSpace(r.DisposalMethod)
                    ? r.DisposalMethod!.Trim()
                    : MapFlowDirectionToDisposal(r.FlowDirection)
            })
            .Select(g =>
            {
                var months = new List<NcrMonthValueDto>(12);
                for (var m = 1; m <= 12; m++)
                {
                    months.Add(new NcrMonthValueDto
                    {
                        Quantity = g.Where(x => x.ReportDate.Month == m).Sum(x => x.Qty ?? 0),
                        Weight = g.Where(x => x.ReportDate.Month == m).Sum(x => x.Weight ?? 0)
                    });
                }
                return new NcrMonthlyRowDto
                {
                    ResponsibilityCategory = g.Key.Category,
                    CategoryDisplay = string.IsNullOrEmpty(g.Key.Category)
                        ? "未填写"
                        : (DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, g.Key.Category) ?? g.Key.Category),
                    ResponsibleDept = string.IsNullOrEmpty(g.Key.Dept) ? "未填写" : g.Key.Dept,
                    DisposalMethod = g.Key.Method,
                    DisposalMethodDisplay = string.IsNullOrEmpty(g.Key.Method)
                        ? "未填写"
                        : (DictValueDisplayHelper.GetText(DictValueDefaults.NcrDisposalKey, g.Key.Method) ?? g.Key.Method),
                    Months = months,
                    TotalQuantity = g.Sum(x => x.Qty ?? 0),
                    TotalWeight = g.Sum(x => x.Weight ?? 0)
                };
            })
            .OrderBy(r => string.IsNullOrEmpty(r.ResponsibilityCategory) ? 1 : 0)
            .ThenBy(r => r.CategoryDisplay, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ResponsibleDept, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.DisposalMethodDisplay, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new NcrMonthlySummaryDto
        {
            MonthLabels = Enumerable.Range(1, 12).Select(m => $"{year}-{m:D2}").ToList(),
            CurrentMonthIndex = DateTime.Today.Month - 1,
            Rows = rows
        };
    }

    /// <summary>
    /// 流向 → 处置方式默认映射（月度汇总兜底：NCR 尚未判定处置方式但有流向时按对应档位归集）。
    /// 流向 5 档与处置方式对应档位 Key 同名（Rework/InProcessWarehouse/FinishedWarehouse/Scrap/Return），
    /// 区别仅在显示名（流向「返整」↔ 处置「返整(新卡流转)」等）。
    /// 「让步放行」「返工」是操作前预先判定、不产生流向，故映射结果必为这 5 档之一。
    /// </summary>
    private static string? MapFlowDirectionToDisposal(FlowDirection? flowDirection) => flowDirection switch
    {
        FlowDirection.Rework => NcrDisposalKeys.Rework,
        FlowDirection.InProcessWarehouse => NcrDisposalKeys.InProcessWarehouse,
        FlowDirection.FinishedWarehouse => NcrDisposalKeys.FinishedWarehouse,
        FlowDirection.Scrap => NcrDisposalKeys.Scrap,
        FlowDirection.Return => NcrDisposalKeys.Return,
        _ => null
    };

    // ========== 打印（PDF - QuestPDF） ==========

    public async Task<byte[]> PrintSelectedAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var entities = await _context.Ncrs
            .AsNoTracking()
            .Where(n => ids.Contains(n.Id))
            .OrderBy(n => n.CreatedTime)
            .ToListAsync();

        if (entities.Count == 0)
            throw new BusinessException("未找到选中的 NCR 报告数据");

        var imagesByNcr = await LoadFeedbackImagesAsync(entities);
        return NcrPrintHelper.GeneratePdf(entities, imagesByNcr);
    }

    /// <summary>
    /// 按 NCR 记录 Id 装载关联不合格反馈单的照片（用于单据式打印嵌入）。
    /// 无关联反馈单或反馈单无附件时不含该键；单张读取失败跳过该张，不阻断打印。
    /// </summary>
    private async Task<Dictionary<int, IReadOnlyList<NcrPrintHelper.PrintImage>>> LoadFeedbackImagesAsync(List<Ncr> entities)
    {
        var result = new Dictionary<int, IReadOnlyList<NcrPrintHelper.PrintImage>>();
        var feedbackIds = entities
            .Where(n => n.NonconformingFeedbackId.HasValue)
            .Select(n => n.NonconformingFeedbackId!.Value)
            .Distinct()
            .ToList();
        if (feedbackIds.Count == 0) return result;

        var attachments = await _context.NonconformingFeedbackAttachments
            .AsNoTracking()
            .Where(a => feedbackIds.Contains(a.FeedbackId))
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Id)
            .ToListAsync();
        if (attachments.Count == 0) return result;

        var byFeedback = attachments.GroupBy(a => a.FeedbackId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var n in entities)
        {
            if (!n.NonconformingFeedbackId.HasValue) continue;
            if (!byFeedback.TryGetValue(n.NonconformingFeedbackId.Value, out var atts)) continue;

            var images = new List<NcrPrintHelper.PrintImage>();
            foreach (var att in atts)
            {
                try
                {
                    var data = await _storage.ReadAsync(att.StoredName);
                    if (data is { Length: > 0 })
                        images.Add(new NcrPrintHelper.PrintImage { FileName = att.FileName, Data = data });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "NCR 打印读取照片失败：NcrId={NcrId} StoredName={StoredName}", n.Id, att.StoredName);
                }
            }
            if (images.Count > 0)
                result[n.Id] = images;
        }

        return result;
    }

    /// <summary>打印选中列表（按当前可见列渲染列表 PDF，Mode A 前端已准备数据）</summary>
    public Task<byte[]> PrintNcrListAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        // 打印选中列表：列表显示模式（内容自适应列宽 + 整页宽度铺满 + 数据居中 + 表头行数不限）
        var pdfBytes = TablePrintHelper.GeneratePdf(title, items, columns,
            autoWidth: true, alignCenter: true, headerMaxLines: 0);
        return Task.FromResult(pdfBytes);
    }

    /// <summary>
    /// 批量查询 ProductionBatch 信息
    /// </summary>
    private async Task<Dictionary<int, BatchInfo>> GetBatchLookupAsync(List<int> batchIds)
    {
        if (batchIds.Count == 0) return new();

        return await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => batchIds.Contains(b.Id))
            .Select(b => new BatchInfo
            {
                Id = b.Id,
                BatchNo = b.BatchNo,
                WorkOrderNo = b.WorkOrderNo,
                SalesOrderNo = b.SalesOrderNo,
                TagNo = b.TagNo,
                PlantGrade = b.PlantGrade
            })
            .ToDictionaryAsync(b => b.Id, b => b);
    }

    private class BatchInfo
    {
        public int Id { get; set; }
        public string BatchNo { get; set; } = null!;
        public string? WorkOrderNo { get; set; }
        public string? SalesOrderNo { get; set; }
        public string? TagNo { get; set; }
        public string? PlantGrade { get; set; }
    }

    private static System.Linq.Expressions.Expression<Func<Ncr, NcrDto>> ToDto()
    {
        return r => new NcrDto
        {
            Id = r.Id,
            ReportDate = r.ReportDate,
            ReportDepartment = r.ReportDepartment,
            Reporter = r.Reporter,
            PipeCategory = r.PipeCategory,
            BatchNo = r.BatchNo,
            WorkOrderNo = r.WorkOrderNo,
            PlantGrade = r.PlantGrade,
            Specification = r.Specification,
            DefectiveQuantity = r.DefectiveQuantity,
            DefectiveWeight = r.DefectiveWeight,
            ProblemDescription = r.ProblemDescription,
            ConcessionQuantity = r.ConcessionQuantity,
            ConcessionWeight = r.ConcessionWeight,
            ConcessionRemark = r.ConcessionRemark,
            SourceInspectionItem = r.SourceInspectionItem,
            SourceGroupKey = r.SourceGroupKey,
            FlowDirection = r.FlowDirection,
            NonconformingFeedbackId = r.NonconformingFeedbackId,
            DisposalMethod = r.DisposalMethod,
            DisposalRemark = r.DisposalRemark,
            DisposalIsCompleted = r.DisposalIsCompleted,
            DisposalCompleteDate = r.DisposalCompleteDate,
            RootCauseAnalysis = r.RootCauseAnalysis,
            Severity = r.Severity,
            AnalysisConfirmer = r.AnalysisConfirmer,
            AnalysisConfirmDate = r.AnalysisConfirmDate,
            ResponsibilityCategory = r.ResponsibilityCategory,
            ResponsibleDept = r.ResponsibleDept,
            OperationDate = r.OperationDate,
            ResponsiblePerson = r.ResponsiblePerson,
            PersonDisposition = r.PersonDisposition,
            PersonIsCompleted = r.PersonIsCompleted,
            PersonCompleteDate = r.PersonCompleteDate,
            CorrectiveAction = r.CorrectiveAction,
            ActionPlanner = r.ActionPlanner,
            ActionPlanDate = r.ActionPlanDate,
            ActionVerifier = r.ActionVerifier,
            ActionVerifyDate = r.ActionVerifyDate,
            ActionResult = r.ActionResult,
            VerifyResult = r.VerifyResult,
            Status = r.Status,
            CreatedTime = r.CreatedTime,
            UpdatedTime = r.UpdatedTime
        };
    }

    private static NcrDto MapToDto(Ncr entity)
    {
        return new NcrDto
        {
            Id = entity.Id,
            ReportDate = entity.ReportDate,
            ReportDepartment = entity.ReportDepartment,
            Reporter = entity.Reporter,
            PipeCategory = entity.PipeCategory,
            BatchNo = entity.BatchNo,
            WorkOrderNo = entity.WorkOrderNo,
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
            DefectiveQuantity = entity.DefectiveQuantity,
            DefectiveWeight = entity.DefectiveWeight,
            ProblemDescription = entity.ProblemDescription,
            ConcessionQuantity = entity.ConcessionQuantity,
            ConcessionWeight = entity.ConcessionWeight,
            ConcessionRemark = entity.ConcessionRemark,
            SourceInspectionItem = entity.SourceInspectionItem,
            SourceGroupKey = entity.SourceGroupKey,
            FlowDirection = entity.FlowDirection,
            NonconformingFeedbackId = entity.NonconformingFeedbackId,
            DisposalMethod = entity.DisposalMethod,
            DisposalRemark = entity.DisposalRemark,
            DisposalIsCompleted = entity.DisposalIsCompleted,
            DisposalCompleteDate = entity.DisposalCompleteDate,
            RootCauseAnalysis = entity.RootCauseAnalysis,
            Severity = entity.Severity,
            AnalysisConfirmer = entity.AnalysisConfirmer,
            AnalysisConfirmDate = entity.AnalysisConfirmDate,
            ResponsibilityCategory = entity.ResponsibilityCategory,
            ResponsibleDept = entity.ResponsibleDept,
            OperationDate = entity.OperationDate,
            ResponsiblePerson = entity.ResponsiblePerson,
            PersonDisposition = entity.PersonDisposition,
            PersonIsCompleted = entity.PersonIsCompleted,
            PersonCompleteDate = entity.PersonCompleteDate,
            CorrectiveAction = entity.CorrectiveAction,
            ActionPlanner = entity.ActionPlanner,
            ActionPlanDate = entity.ActionPlanDate,
            ActionVerifier = entity.ActionVerifier,
            ActionVerifyDate = entity.ActionVerifyDate,
            ActionResult = entity.ActionResult,
            VerifyResult = entity.VerifyResult,
            Status = entity.Status,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    private static IQueryable<Ncr> ApplySorting(IQueryable<Ncr> queryable, string sortBy, bool isDescending)
    {
        return queryable.ApplySort(sortBy, isDescending);
    }
}
