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

        if (query.ReportDateFrom.HasValue)
            queryable = queryable.Where(r => r.ReportDate >= query.ReportDateFrom.Value);
        if (query.ReportDateTo.HasValue)
            queryable = queryable.Where(r => r.ReportDate <= query.ReportDateTo.Value);

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

        return NcrPrintHelper.GeneratePdf(entities);
    }

    public async Task<byte[]> PrintAllAsync(NcrPrintAllRequest request)
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

        return NcrPrintHelper.GeneratePdf(entities);
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
