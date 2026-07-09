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

    public NcrService(AppDbContext context, ILogger<NcrService> logger,
        IConfigParameterService configService,
        IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
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

    public async Task<NcrDto?> GetByIdAsync(int id)
    {
        return await _context.Ncrs
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(ToDto())
            .FirstOrDefaultAsync();
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

        queryable = queryable.ApplyFilters(query.Filters);
        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.SortBy ?? "reportdate", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(ToDto())
            .ToListAsync();

        return new PagedResult<NcrDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<NcrDto>> GetAllListAsync()
    {
        return await _context.Ncrs
            .AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Select(ToDto())
            .ToListAsync();
    }

    public async Task<NcrDto> CreateAsync(CreateNcrRequest request)
    {
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
            ProblemDescription = request.ProblemDescription,
            SourceInspectionItem = request.SourceInspectionItem,

            // G2
            DisposalMethod = request.DisposalMethod,
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

            // 状态（登记即处理中）
            Status = NcrStatus.Processing
        };

        // 自动关闭：三个条件全部满足直接设为已关闭
        if (entity.DisposalIsCompleted
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
        entity.ProblemDescription = request.ProblemDescription ?? entity.ProblemDescription;
        entity.SourceInspectionItem = request.SourceInspectionItem ?? entity.SourceInspectionItem;

        // G2
        entity.DisposalMethod = request.DisposalMethod ?? entity.DisposalMethod;
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
        return await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.BatchNo == batchNo)
            .Select(b => new NcrLookupResultDto
            {
                WorkOrderNo = b.WorkOrderNo,
                SalesOrderNo = b.SalesOrderNo,
                TagNo = b.TagNo,
                PlantGrade = b.PlantGrade,
                Specification = b.Specification
            })
            .FirstOrDefaultAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("NcrService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var queryable = _context.Ncrs.AsNoTracking();

            // 注意：枚举列（PipeCategory/DisposalMethod/Severity/ResponsibilityCategory/VerifyResult/Status 等）
            // 不在此处返回，由前端 EnumOptions fallback 直接提供带中文 Display 的选项，避免映射丢失。
            var results = await queryable
                .Select(r => new
                {
                    r.ReportDepartment, r.Reporter,
                    r.BatchNo, r.WorkOrderNo, r.PlantGrade, r.Specification,
                    r.ProblemDescription, r.DisposalRemark,
                    r.RootCauseAnalysis, r.AnalysisConfirmer,
                    r.ResponsibleDept, r.ResponsiblePerson, r.PersonDisposition,
                    r.CorrectiveAction, r.ActionPlanner, r.ActionVerifier, r.ActionResult,
                    r.ReportDate, r.DisposalCompleteDate, r.AnalysisConfirmDate
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

        var ncrReworkCount = await GetConfigAsync("NcrThreshold", "ReworkCount", 5m);
        var ncrReworkPercent = await GetConfigAsync("NcrThreshold", "ReworkPercent", 0.05m);
        var ncrWarehouseCount = await GetConfigAsync("NcrThreshold", "WarehouseCount", 5m);
        var ncrWarehousePercent = await GetConfigAsync("NcrThreshold", "WarehousePercent", 0.05m);
        var ncrScrapCount = await GetConfigAsync("NcrThreshold", "ScrapCount", 3m);
        var ncrScrapPercent = await GetConfigAsync("NcrThreshold", "ScrapPercent", 0.05m);

        // ======== 1. 过程检验分析 ========
        var processAggs = await _context.ProcessInspections
            .AsNoTracking()
            .Where(pi => pi.Quantity > 0)
            .GroupBy(pi => pi.ProductionBatchId)
            .Select(g => new
            {
                ProductionBatchId = g.Key,
                TotalRework = g.Sum(pi => (int?)pi.DefectReworkQuantity) ?? 0,
                TotalWarehouse = g.Sum(pi => (int?)pi.DefectWarehouseQuantity) ?? 0,
                TotalScrap = g.Sum(pi => (int?)pi.DefectScrapQuantity) ?? 0,
                TotalQuantity = g.Sum(pi => (int?)pi.Quantity) ?? 0,
                InspectionItem = g.Select(pi => pi.InspectionItem).FirstOrDefault(),
                Inspector = g.Select(pi => pi.Inspector).FirstOrDefault(),
                ProcessName = g.Select(pi => pi.ProcessName).FirstOrDefault(),
                ManufacturingSpec = g.Select(pi => pi.ManufacturingSpec).FirstOrDefault(),
                BatchNo = g.Select(pi => pi.BatchNo).FirstOrDefault(),
                InspectionDate = g.Select(pi => pi.InspectionDate).FirstOrDefault(),
                DefectDescription = g.Select(pi => pi.DefectDescription).FirstOrDefault(),
            })
            .ToListAsync();

        var procBatchIds = processAggs.Where(a => a.TotalQuantity > 0).Select(a => a.ProductionBatchId).Distinct().ToList();
        var procBatchLookup = await GetBatchLookupAsync(procBatchIds);

        foreach (var a in processAggs)
        {
            if (a.TotalQuantity <= 0) continue;

            var batch = procBatchLookup.GetValueOrDefault(a.ProductionBatchId);
            var totalQty = a.TotalQuantity;

            if (a.TotalRework >= (int)ncrReworkCount && (decimal)a.TotalRework / totalQty >= ncrReworkPercent)
            {
                results.Add(new NcrPendingCheckDto
                {
                    BatchNo = a.BatchNo ?? batch?.BatchNo ?? "",
                    WorkOrderNo = batch?.WorkOrderNo,
                    PlantGrade = batch?.PlantGrade,
                    Specification = a.ManufacturingSpec,
                    ReportDate = a.InspectionDate,
                    SourceType = "ProcessInspection",
                    InspectionItem = a.InspectionItem,
                    ProcessName = a.ProcessName,
                    Inspector = a.Inspector,
                    DefectDescription = a.DefectDescription,
                    DisposalMethod = DisposalMethod.Rework,
                    DefectQuantity = a.TotalRework,
                    TotalQuantity = totalQty,
                    Percentage = Math.Round((decimal)a.TotalRework / totalQty * 100, 1)
                });
            }

            if (a.TotalWarehouse >= (int)ncrWarehouseCount && (decimal)a.TotalWarehouse / totalQty >= ncrWarehousePercent)
            {
                results.Add(new NcrPendingCheckDto
                {
                    BatchNo = a.BatchNo ?? batch?.BatchNo ?? "",
                    WorkOrderNo = batch?.WorkOrderNo,
                    PlantGrade = batch?.PlantGrade,
                    Specification = a.ManufacturingSpec,
                    ReportDate = a.InspectionDate,
                    SourceType = "ProcessInspection",
                    InspectionItem = a.InspectionItem,
                    ProcessName = a.ProcessName,
                    Inspector = a.Inspector,
                    DefectDescription = a.DefectDescription,
                    DisposalMethod = DisposalMethod.WarehouseEntry,
                    DefectQuantity = a.TotalWarehouse,
                    TotalQuantity = totalQty,
                    Percentage = Math.Round((decimal)a.TotalWarehouse / totalQty * 100, 1)
                });
            }

            if (a.TotalScrap >= (int)ncrScrapCount && (decimal)a.TotalScrap / totalQty >= ncrScrapPercent)
            {
                results.Add(new NcrPendingCheckDto
                {
                    BatchNo = a.BatchNo ?? batch?.BatchNo ?? "",
                    WorkOrderNo = batch?.WorkOrderNo,
                    PlantGrade = batch?.PlantGrade,
                    Specification = a.ManufacturingSpec,
                    ReportDate = a.InspectionDate,
                    SourceType = "ProcessInspection",
                    InspectionItem = a.InspectionItem,
                    ProcessName = a.ProcessName,
                    Inspector = a.Inspector,
                    DefectDescription = a.DefectDescription,
                    DisposalMethod = DisposalMethod.Scrap,
                    DefectQuantity = a.TotalScrap,
                    TotalQuantity = totalQty,
                    Percentage = Math.Round((decimal)a.TotalScrap / totalQty * 100, 1)
                });
            }
        }

        // ======== 2. 成品检验分析 ========
        var finalAggs = await _context.FinalInspections
            .AsNoTracking()
            .Where(fi => fi.Quantity > 0)
            .GroupBy(fi => new { fi.ProductionBatchId, fi.InspectionItem })
            .Select(g => new
            {
                g.Key.ProductionBatchId,
                InspectionItem = g.Key.InspectionItem,
                TotalRework = g.Sum(fi => (int?)fi.DefectReworkQuantity) ?? 0,
                TotalWarehouse = g.Sum(fi => (int?)fi.DefectWarehouseQuantity) ?? 0,
                TotalScrap = g.Sum(fi => (int?)fi.DefectScrapQuantity) ?? 0,
                TotalQuantity = g.Sum(fi => (int?)fi.Quantity) ?? 0,
                Inspector = g.Select(fi => fi.Operator).FirstOrDefault(),
                MaterialName = g.Select(fi => fi.MaterialName).FirstOrDefault(),
                Specification = g.Select(fi => fi.Specification).FirstOrDefault(),
                BatchNo = g.Select(fi => fi.BatchNo).FirstOrDefault(),
                WorkOrderNo = g.Select(fi => fi.WorkOrderNo).FirstOrDefault(),
                PlantGrade = g.Select(fi => fi.PlantGrade).FirstOrDefault(),
                InspectionDate = g.Select(fi => fi.InspectionDate).FirstOrDefault(),
                DefectDescription = g.Select(fi => fi.DefectDescription).FirstOrDefault(),
            })
            .ToListAsync();

        foreach (var a in finalAggs)
        {
            if (a.TotalQuantity <= 0) continue;

            var totalQty = a.TotalQuantity;
            var inspectionItem = a.InspectionItem.ToString();

            if (a.TotalRework >= (int)ncrReworkCount && (decimal)a.TotalRework / totalQty >= ncrReworkPercent)
            {
                results.Add(new NcrPendingCheckDto
                {
                    BatchNo = a.BatchNo ?? "",
                    WorkOrderNo = a.WorkOrderNo,
                    PlantGrade = a.PlantGrade,
                    Specification = a.Specification,
                    ReportDate = a.InspectionDate,
                    SourceType = "FinalInspection",
                    InspectionItem = inspectionItem,
                    MaterialName = a.MaterialName,
                    Inspector = a.Inspector,
                    DefectDescription = a.DefectDescription,
                    DisposalMethod = DisposalMethod.Rework,
                    DefectQuantity = a.TotalRework,
                    TotalQuantity = totalQty,
                    Percentage = Math.Round((decimal)a.TotalRework / totalQty * 100, 1)
                });
            }

            if (a.TotalWarehouse >= (int)ncrWarehouseCount && (decimal)a.TotalWarehouse / totalQty >= ncrWarehousePercent)
            {
                results.Add(new NcrPendingCheckDto
                {
                    BatchNo = a.BatchNo ?? "",
                    WorkOrderNo = a.WorkOrderNo,
                    PlantGrade = a.PlantGrade,
                    Specification = a.Specification,
                    ReportDate = a.InspectionDate,
                    SourceType = "FinalInspection",
                    InspectionItem = inspectionItem,
                    MaterialName = a.MaterialName,
                    Inspector = a.Inspector,
                    DefectDescription = a.DefectDescription,
                    DisposalMethod = DisposalMethod.WarehouseEntry,
                    DefectQuantity = a.TotalWarehouse,
                    TotalQuantity = totalQty,
                    Percentage = Math.Round((decimal)a.TotalWarehouse / totalQty * 100, 1)
                });
            }

            if (a.TotalScrap >= (int)ncrScrapCount && (decimal)a.TotalScrap / totalQty >= ncrScrapPercent)
            {
                results.Add(new NcrPendingCheckDto
                {
                    BatchNo = a.BatchNo ?? "",
                    WorkOrderNo = a.WorkOrderNo,
                    PlantGrade = a.PlantGrade,
                    Specification = a.Specification,
                    ReportDate = a.InspectionDate,
                    SourceType = "FinalInspection",
                    InspectionItem = inspectionItem,
                    MaterialName = a.MaterialName,
                    Inspector = a.Inspector,
                    DefectDescription = a.DefectDescription,
                    DisposalMethod = DisposalMethod.Scrap,
                    DefectQuantity = a.TotalScrap,
                    TotalQuantity = totalQty,
                    Percentage = Math.Round((decimal)a.TotalScrap / totalQty * 100, 1)
                });
            }
        }

        // 排除已有 NCR 记录的 (BatchNo, DisposalMethod, InspectionItem) 三字段组合
        var existingCombos = await _context.Ncrs
            .AsNoTracking()
            .Where(n => n.BatchNo != null)
            .Select(n => new { n.BatchNo, n.DisposalMethod, n.SourceInspectionItem })
            .ToListAsync();
        var existingComboKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in existingCombos)
        {
            var dm = c.DisposalMethod?.ToString() ?? "";
            var item = c.SourceInspectionItem ?? "";
            existingComboKeys.Add($"{c.BatchNo}|{dm}|{item}");
        }
        results.RemoveAll(r =>
        {
            var key = $"{r.BatchNo}|{r.DisposalMethod}|{r.InspectionItem ?? ""}";
            return existingComboKeys.Contains(key);
        });

        return results;
    }

    // ========== 打印（HTML 打印样式） ==========

    public async Task<string> PrintSelectedAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var entities = await _context.Ncrs
            .AsNoTracking()
            .Where(n => ids.Contains(n.Id))
            .OrderBy(n => n.CreatedTime)
            .ToListAsync();

        if (entities.Count == 0)
            throw new BusinessException("未找到选中的 NCR 报告数据");

        return BuildNcrPrintHtml(entities);
    }

    public async Task<string> PrintAllAsync(NcrPrintAllRequest request)
    {
        var queryable = _context.Ncrs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword;
            queryable = queryable.Where(r =>
                r.BatchNo.Contains(kw) ||
                (r.WorkOrderNo != null && r.WorkOrderNo.Contains(kw)) ||
                (r.PlantGrade != null && r.PlantGrade.Contains(kw)) ||
                (r.Specification != null && r.Specification.Contains(kw)) ||
                (r.ReportDepartment != null && r.ReportDepartment.Contains(kw)) ||
                (r.Reporter != null && r.Reporter.Contains(kw)));
        }

        var entities = await queryable
            .OrderByDescending(n => n.CreatedTime)
            .ToListAsync();

        return BuildNcrPrintHtml(entities);
    }

    private static string BuildNcrPrintHtml(List<Ncr> entities)
    {
        var html = new System.Text.StringBuilder();
        html.AppendLine("<!DOCTYPE html><html><head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<title>不合格品报告</title>");
        html.AppendLine("<style>");
        html.AppendLine(GetPrintStyles());
        html.AppendLine("</style></head><body>");

        for (int i = 0; i < entities.Count; i++)
        {
            var n = entities[i];
            html.AppendLine("<div class=\"ncr-print-report\">");
            AppendNcrReport(html, n);
            html.AppendLine("</div>");
            if (i < entities.Count - 1)
                html.AppendLine("<div class=\"page-break\"></div>");
        }

        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private static void AppendNcrReport(System.Text.StringBuilder html, Ncr n)
    {
        // 报告头
        html.AppendLine("<div class=\"ncr-print-header\">");
        html.AppendLine("<h1>不合格报告</h1>");
        html.AppendLine($"<div class=\"ncr-print-id\">编号: NCR-{n.Id:D4}</div>");
        html.AppendLine($"<div class=\"ncr-print-status\"><span class=\"ncr-status-badge ncr-status-{GetStatusCss(n.Status)}\">{GetStatusText(n.Status)}</span></div>");
        html.AppendLine("</div>");

        // G1 问题反馈
        html.AppendLine("<div class=\"ncr-print-section\">");
        html.AppendLine("<h2 class=\"ncr-section-title ncr-section-g1\">G1 问题反馈</h2>");
        html.AppendLine("<table class=\"ncr-print-table\">");
        AppendFieldRow(html, "反馈日期", n.ReportDate.ToString("yyyy-MM-dd"), "反馈部门", n.ReportDepartment ?? "");
        AppendFieldRow(html, "反馈人", n.Reporter ?? "", "钢管类别", GetPipeCategoryText(n.PipeCategory));
        AppendFieldRow(html, "生产编号", n.BatchNo, "工单号", n.WorkOrderNo ?? "");
        AppendFieldRow(html, "牌号", n.PlantGrade ?? "", "规格", n.Specification ?? "");
        AppendFieldRow(html, "不合格支数", n.DefectiveQuantity?.ToString("G29") ?? "0", "", "");
        AppendFieldSpan(html, "问题描述", n.ProblemDescription ?? "");
        html.AppendLine("</table></div>");

        // G2 不合格品处置
        html.AppendLine("<div class=\"ncr-print-section\">");
        html.AppendLine("<h2 class=\"ncr-section-title ncr-section-g2\">G2 不合格品处置</h2>");
        html.AppendLine("<table class=\"ncr-print-table\">");
        AppendFieldRow(html, "处置方式", GetDisposalMethodText(n.DisposalMethod), "处置完结", n.DisposalIsCompleted ? "是" : "否");
        AppendFieldRow(html, "处置完结日期", FormatDate(n.DisposalCompleteDate), "", "");
        AppendFieldSpan(html, "处置备注", n.DisposalRemark ?? "");
        html.AppendLine("</table></div>");

        // G3 原因分析
        html.AppendLine("<div class=\"ncr-print-section\">");
        html.AppendLine("<h2 class=\"ncr-section-title ncr-section-g3\">G3 原因分析</h2>");
        html.AppendLine("<table class=\"ncr-print-table\">");
        AppendFieldRow(html, "严重程度", GetSeverityText(n.Severity), "分析确认人", n.AnalysisConfirmer ?? "");
        AppendFieldRow(html, "确认日期", FormatDate(n.AnalysisConfirmDate), "", "");
        AppendFieldSpan(html, "原因分析", n.RootCauseAnalysis ?? "");
        html.AppendLine("</table></div>");

        // G4 责任人及处理
        html.AppendLine("<div class=\"ncr-print-section\">");
        html.AppendLine("<h2 class=\"ncr-section-title ncr-section-g4\">G4 责任人及处理</h2>");
        html.AppendLine("<table class=\"ncr-print-table\">");
        AppendFieldRow(html, "责任类别", GetResponsibilityCategoryText(n.ResponsibilityCategory), "责任部门", n.ResponsibleDept ?? "");
        AppendFieldRow(html, "责任人", n.ResponsiblePerson ?? "", "操作日期", FormatDate(n.OperationDate));
        AppendFieldRow(html, "处理完结", n.PersonIsCompleted ? "是" : "否", "完结日期", FormatDate(n.PersonCompleteDate));
        AppendFieldSpan(html, "对责任人的处理", n.PersonDisposition ?? "");
        html.AppendLine("</table></div>");

        // G5 纠正预防措施及结果验证
        html.AppendLine("<div class=\"ncr-print-section\">");
        html.AppendLine("<h2 class=\"ncr-section-title ncr-section-g5\">G5 纠正预防措施及结果验证</h2>");
        html.AppendLine("<table class=\"ncr-print-table\">");
        AppendFieldRow(html, "计划人", n.ActionPlanner ?? "", "计划日期", FormatDate(n.ActionPlanDate));
        AppendFieldRow(html, "验证人", n.ActionVerifier ?? "", "验证日期", FormatDate(n.ActionVerifyDate));
        AppendFieldRow(html, "验证结论", GetVerifyResultText(n.VerifyResult), "结果判定", n.ActionResult ?? "");
        AppendFieldSpan(html, "纠正预防措施", n.CorrectiveAction ?? "");
        html.AppendLine("</table></div>");

        // 页脚
        html.AppendLine("<div class=\"ncr-print-footer\">");
        html.AppendLine($"<div class=\"ncr-print-audit\">创建时间: {n.CreatedTime:yyyy-MM-dd HH:mm} | 更新时间: {n.UpdatedTime:yyyy-MM-dd HH:mm}</div>");
        html.AppendLine("</div>");
    }

    private static void AppendFieldRow(System.Text.StringBuilder html, string label1, string value1, string label2, string value2)
    {
        html.Append("<tr>");
        html.Append($"<td class=\"ncr-label\">{EscapeHtml(label1)}</td>");
        html.Append($"<td style=\"width:25%\">{EscapeHtml(value1)}</td>");
        html.Append($"<td class=\"ncr-label\">{EscapeHtml(label2)}</td>");
        html.Append($"<td style=\"width:25%\">{EscapeHtml(value2)}</td>");
        html.AppendLine("</tr>");
    }

    private static void AppendFieldSpan(System.Text.StringBuilder html, string label, string value)
    {
        html.Append("<tr>");
        html.Append($"<td class=\"ncr-label\">{EscapeHtml(label)}</td>");
        html.Append($"<td colspan=\"3\">{EscapeHtml(value)}</td>");
        html.AppendLine("</tr>");
    }

    private static string EscapeHtml(string text) => System.Net.WebUtility.HtmlEncode(text ?? "");

    private static string GetPrintStyles()
    {
        return @"
    body { font-family: ""Helvetica Neue"", Helvetica, Arial, ""Microsoft YaHei"", sans-serif; font-size: 12px; color: #222; margin: 0; padding: 0; }
    .ncr-print-report { max-width: 210mm; margin: 0 auto; padding: 20px 10px; }
    .ncr-print-header { text-align: center; border-bottom: 2px solid #d32f2f; margin-bottom: 16px; padding-bottom: 10px; }
    .ncr-print-header h1 { font-size: 22px; font-weight: bold; margin: 0 0 6px; color: #d32f2f; }
    .ncr-print-id { font-size: 13px; color: #555; margin-bottom: 4px; }
    .ncr-print-status { font-size: 12px; }
    .ncr-status-badge { display: inline-block; padding: 2px 12px; border-radius: 3px; font-weight: 600; }
    .ncr-status-processing { background-color: #fff3e0; color: #e65100; }
    .ncr-status-closed { background-color: #e8f5e9; color: #2e7d32; }
    .ncr-section-title { font-size: 13px; font-weight: bold; padding: 5px 10px; margin: 14px 0 8px; border-left: 4px solid #999; }
    .ncr-section-g1 { border-left-color: #d32f2f; background: #fff5f5; }
    .ncr-section-g2 { border-left-color: #f57c00; background: #fff8e1; }
    .ncr-section-g3 { border-left-color: #1976d2; background: #e3f2fd; }
    .ncr-section-g4 { border-left-color: #5c6bc0; background: #e8eaf6; }
    .ncr-section-g5 { border-left-color: #388e3c; background: #e8f5e9; }
    .ncr-print-table { width: 100%; border-collapse: collapse; margin-bottom: 4px; }
    .ncr-print-table td { border: 1px solid #ccc; padding: 5px 8px; vertical-align: top; }
    .ncr-label { background-color: #f5f5f5; font-weight: 600; white-space: nowrap; width: 110px; }
    .ncr-print-footer { margin-top: 24px; padding-top: 8px; border-top: 1px solid #ccc; }
    .ncr-print-audit { font-size: 10px; color: #888; text-align: center; }
    .page-break { page-break-after: always; }
    @@media print {
        @@page { size: portrait; margin: 15mm 12mm; }
        body { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        .ncr-print-section { page-break-inside: avoid; }
    }";
    }

    private static string GetStatusText(NcrStatus status) => status switch
    {
        NcrStatus.Processing => "处理中",
        NcrStatus.Closed => "已关闭",
        _ => status.ToString()
    };

    private static string GetStatusCss(NcrStatus status) => status switch
    {
        NcrStatus.Processing => "processing",
        NcrStatus.Closed => "closed",
        _ => ""
    };

    private static string GetPipeCategoryText(PipeCategory category) => category switch
    {
        PipeCategory.TubeBlank => "荒管",
        PipeCategory.Intermediate => "中间品",
        PipeCategory.SurplusInventory => "余库料",
        PipeCategory.CriticalFinished => "临界成品",
        PipeCategory.OrderFinished => "订单成品",
        PipeCategory.SpecialDelivery => "特定交态成品",
        _ => category.ToString()
    };

    private static string GetDisposalMethodText(DisposalMethod? method) => method switch
    {
        DisposalMethod.Rework => "返整",
        DisposalMethod.WarehouseEntry => "入库",
        DisposalMethod.Scrap => "报废",
        _ => ""
    };

    private static string GetSeverityText(SeverityLevel? severity) => severity switch
    {
        SeverityLevel.Critical => "严重",
        SeverityLevel.General => "一般",
        _ => ""
    };

    private static string GetResponsibilityCategoryText(ResponsibilityCategory? category) => category switch
    {
        ResponsibilityCategory.ProductionInternal => "生产-厂内",
        ResponsibilityCategory.ProductionOutsource => "生产-外协",
        ResponsibilityCategory.MaterialTubeBlank => "原料-荒管",
        ResponsibilityCategory.MaterialPurchased => "原料-外购成品",
        ResponsibilityCategory.MaterialSurplus => "原料-余库料",
        _ => ""
    };

    private static string GetVerifyResultText(VerifyResult? result) => result switch
    {
        VerifyResult.Passed => "通过",
        VerifyResult.NeedsRectification => "需整改",
        VerifyResult.NotApplicable => "不适用",
        _ => ""
    };

    private static string FormatDate(DateTime? dt) => dt?.ToString("yyyy-MM-dd") ?? "";

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
            ProblemDescription = r.ProblemDescription,
            SourceInspectionItem = r.SourceInspectionItem,
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
            ProblemDescription = entity.ProblemDescription,
            SourceInspectionItem = entity.SourceInspectionItem,
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
