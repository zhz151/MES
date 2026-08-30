using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Constants;
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
using MES.Core.Exceptions;
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
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Quality;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Quality;

/// <summary>
/// 化学分析服务实现
/// </summary>
public class ChemicalAnalysisService : IChemicalAnalysisService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ChemicalAnalysisService> _logger;
    private readonly IMemoryCache _cache;

    public ChemicalAnalysisService(AppDbContext context, ILogger<ChemicalAnalysisService> logger, IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    public async Task<ChemicalAnalysisDto?> GetByIdAsync(int id)
    {
        return await _context.ChemicalAnalyses
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync();
    }

    public async Task<PagedResult<ChemicalAnalysisDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.ChemicalAnalyses
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                r.Analyst.Contains(kw) ||
                r.FurnaceNo.Contains(kw) ||
                r.Grade.Contains(kw) ||
                (r.AnalysisStandard != null && r.AnalysisStandard.Contains(kw)));
        }

        if (query.InspectionDateFrom.HasValue)
            queryable = queryable.Where(r => r.AnalysisDate >= query.InspectionDateFrom.Value);

        if (query.InspectionDateTo.HasValue)
            queryable = queryable.Where(r => r.AnalysisDate <= query.InspectionDateTo.Value);

        queryable = queryable.ApplyFilters(query.Filters);
        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.SortBy ?? "analysisdate", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(MapToDto())
            .ToListAsync();

        return new PagedResult<ChemicalAnalysisDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<ChemicalAnalysisDto> CreateAsync(CreateChemicalAnalysisRequest request)
    {
        var entity = new ChemicalAnalysis
        {
            AnalysisDate = request.AnalysisDate,
            Analyst = request.Analyst,
            FurnaceNo = request.FurnaceNo,
            Grade = request.Grade,
            AnalysisCount = request.AnalysisCount,
            AnalysisStandard = request.AnalysisStandard,
            C = request.C,
            Si = request.Si,
            Mn = request.Mn,
            P = request.P,
            S = request.S,
            Ni = request.Ni,
            Cr = request.Cr,
            Mo = request.Mo,
            Cu = request.Cu,
            N = request.N,
            Nb = request.Nb,
            Ti = request.Ti,
            Fe = request.Fe,
            Al = request.Al,
            W = request.W
        };

        _context.ChemicalAnalyses.Add(entity);
        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<ChemicalAnalysisDto> UpdateAsync(int id, UpdateChemicalAnalysisRequest request)
    {
        var entity = await _context.ChemicalAnalyses.FindAsync(id)
            ?? throw new BusinessException("化学分析记录不存在");

        entity.AnalysisDate = request.AnalysisDate;
        entity.Analyst = request.Analyst ?? entity.Analyst;
        entity.FurnaceNo = request.FurnaceNo ?? entity.FurnaceNo;
        entity.Grade = request.Grade ?? entity.Grade;
        entity.AnalysisCount = request.AnalysisCount ?? entity.AnalysisCount;
        entity.AnalysisStandard = request.AnalysisStandard ?? entity.AnalysisStandard;
        entity.C = request.C ?? entity.C;
        entity.Si = request.Si ?? entity.Si;
        entity.Mn = request.Mn ?? entity.Mn;
        entity.P = request.P ?? entity.P;
        entity.S = request.S ?? entity.S;
        entity.Ni = request.Ni ?? entity.Ni;
        entity.Cr = request.Cr ?? entity.Cr;
        entity.Mo = request.Mo ?? entity.Mo;
        entity.Cu = request.Cu ?? entity.Cu;
        entity.N = request.N ?? entity.N;
        entity.Nb = request.Nb ?? entity.Nb;
        entity.Ti = request.Ti ?? entity.Ti;
        entity.Fe = request.Fe ?? entity.Fe;
        entity.Al = request.Al ?? entity.Al;
        entity.W = request.W ?? entity.W;

        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.ChemicalAnalyses.FindAsync(id)
            ?? throw new BusinessException("化学分析记录不存在");

        _context.ChemicalAnalyses.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ChemicalAnalysisDto>> BatchCreateAsync(List<CreateChemicalAnalysisRequest> requests)
    {
        if (requests.Count == 0)
            return new List<ChemicalAnalysisDto>();

        var entities = requests.Select(r => new ChemicalAnalysis
        {
            AnalysisDate = r.AnalysisDate,
            Analyst = r.Analyst,
            FurnaceNo = r.FurnaceNo,
            Grade = r.Grade,
            AnalysisCount = r.AnalysisCount,
            AnalysisStandard = r.AnalysisStandard,
            C = r.C,
            Si = r.Si,
            Mn = r.Mn,
            P = r.P,
            S = r.S,
            Ni = r.Ni,
            Cr = r.Cr,
            Mo = r.Mo,
            Cu = r.Cu,
            N = r.N,
            Nb = r.Nb,
            Ti = r.Ti,
            Fe = r.Fe,
            Al = r.Al,
            W = r.W
        }).ToList();

        _context.ChemicalAnalyses.AddRange(entities);
        await _context.SaveChangesAsync();

        return entities.Select(e => MapToDto(e)).ToList();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.ChemicalAnalysisFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            var all = await _context.ChemicalAnalyses
                .AsNoTracking()
                .Select(r => new
                {
                    r.Analyst,
                    r.FurnaceNo,
                    r.Grade,
                    r.AnalysisStandard,
                    r.AnalysisDate
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["Analyst"] = all.Select(x => x.Analyst ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["FurnaceNo"] = all.Select(x => x.FurnaceNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Grade"] = all.Select(x => x.Grade ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["AnalysisStandard"] = all.Select(x => x.AnalysisStandard ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["AnalysisDate"] = all.Select(x => x.AnalysisDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList()
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    private static IQueryable<ChemicalAnalysis> ApplySorting(IQueryable<ChemicalAnalysis> queryable, string sortBy, bool isDescending)
    {
        return queryable.ApplySort(sortBy, isDescending);
    }

    private static ChemicalAnalysisDto MapToDto(ChemicalAnalysis entity)
    {
        return new ChemicalAnalysisDto
        {
            Id = entity.Id,
            AnalysisDate = entity.AnalysisDate,
            Analyst = entity.Analyst,
            FurnaceNo = entity.FurnaceNo,
            Grade = entity.Grade,
            AnalysisCount = entity.AnalysisCount,
            AnalysisStandard = entity.AnalysisStandard,
            C = entity.C,
            Si = entity.Si,
            Mn = entity.Mn,
            P = entity.P,
            S = entity.S,
            Ni = entity.Ni,
            Cr = entity.Cr,
            Mo = entity.Mo,
            Cu = entity.Cu,
            N = entity.N,
            Nb = entity.Nb,
            Ti = entity.Ti,
            Fe = entity.Fe,
            Al = entity.Al,
            W = entity.W,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var result = await GetAllAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return ChemicalAnalysisPrintHelper.GenerateBatchPdf(selected, columns);
    }

    private static System.Linq.Expressions.Expression<Func<ChemicalAnalysis, ChemicalAnalysisDto>> MapToDto()
    {
        return r => new ChemicalAnalysisDto
        {
            Id = r.Id,
            AnalysisDate = r.AnalysisDate,
            Analyst = r.Analyst,
            FurnaceNo = r.FurnaceNo,
            Grade = r.Grade,
            AnalysisCount = r.AnalysisCount,
            AnalysisStandard = r.AnalysisStandard,
            C = r.C,
            Si = r.Si,
            Mn = r.Mn,
            P = r.P,
            S = r.S,
            Ni = r.Ni,
            Cr = r.Cr,
            Mo = r.Mo,
            Cu = r.Cu,
            N = r.N,
            Nb = r.Nb,
            Ti = r.Ti,
            Fe = r.Fe,
            Al = r.Al,
            W = r.W,
            CreatedTime = r.CreatedTime,
            UpdatedTime = r.UpdatedTime
        };
    }
}
