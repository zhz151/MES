using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Quality;

public class TensileTestService : ITensileTestService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TensileTestService> _logger;
    private readonly IMemoryCache _cache;

    public TensileTestService(AppDbContext context, ILogger<TensileTestService> logger, IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    // 筛选上下文缓存由 IMemoryCache 管理（注入 _cache）

    public async Task<TensileTestDto?> GetByIdAsync(int id)
    {
        return await _context.TensileTests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync();
    }

    public async Task<PagedResult<TensileTestDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.TensileTests
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                r.Inspector.Contains(kw) ||
                r.FurnaceNo.Contains(kw) ||
                r.Grade.Contains(kw) ||
                r.Specification.Contains(kw) ||
                (r.InspectionStandard != null && r.InspectionStandard.Contains(kw)) ||
                (r.Judgment != null && r.Judgment.Contains(kw)));
        }

        if (query.InspectionDateFrom.HasValue)
            queryable = queryable.Where(r => r.InspectionDate >= query.InspectionDateFrom.Value);
        if (query.InspectionDateTo.HasValue)
            queryable = queryable.Where(r => r.InspectionDate <= query.InspectionDateTo.Value);

        queryable = queryable.ApplyFilters(query.Filters);
        var totalCount = await queryable.CountAsync();
        queryable = ApplySorting(queryable, query.SortBy ?? "inspectiondate", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip).Take(query.PageSize)
            .Select(MapToDto()).ToListAsync();

        return new PagedResult<TensileTestDto>
        {
            Items = items, TotalCount = totalCount,
            PageIndex = query.PageIndex, PageSize = query.PageSize
        };
    }

    public async Task<TensileTestDto> CreateAsync(CreateTensileTestRequest request)
    {
        var entity = MapToEntity(request);
        _context.TensileTests.Add(entity);
        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<TensileTestDto> UpdateAsync(int id, UpdateTensileTestRequest request)
    {
        var entity = await _context.TensileTests.FindAsync(id)
            ?? throw new BusinessException("室温拉伸检验记录不存在");

        entity.InspectionDate = request.InspectionDate;
        entity.Inspector = request.Inspector ?? entity.Inspector;
        entity.FurnaceNo = request.FurnaceNo ?? entity.FurnaceNo;
        entity.Grade = request.Grade ?? entity.Grade;
        entity.Specification = request.Specification ?? entity.Specification;
        entity.SampleNo = request.SampleNo ?? entity.SampleNo;
        entity.SampleSize = request.SampleSize ?? entity.SampleSize;
        entity.InspectionStandard = request.InspectionStandard ?? entity.InspectionStandard;
        entity.OriginalGaugeLength = request.OriginalGaugeLength ?? entity.OriginalGaugeLength;
        entity.FinalGaugeLength = request.FinalGaugeLength ?? entity.FinalGaugeLength;
        entity.TensileStrength = request.TensileStrength ?? entity.TensileStrength;
        entity.YieldStrengthRp02 = request.YieldStrengthRp02 ?? entity.YieldStrengthRp02;
        entity.YieldStrengthRp1 = request.YieldStrengthRp1 ?? entity.YieldStrengthRp1;
        entity.Elongation = request.Elongation ?? entity.Elongation;
        entity.Judgment = request.Judgment ?? entity.Judgment;

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.TensileTests.FindAsync(id)
            ?? throw new BusinessException("室温拉伸检验记录不存在");
        _context.TensileTests.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<TensileTestDto>> BatchCreateAsync(List<CreateTensileTestRequest> requests)
    {
        if (requests.Count == 0) return new List<TensileTestDto>();
        var entities = requests.Select(MapToEntity).ToList();
        _context.TensileTests.AddRange(entities);
        await _context.SaveChangesAsync();
        return entities.Select(e => MapToDto(e)).ToList();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("TensileTestService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

        var all = await _context.TensileTests
            .AsNoTracking()
            .Select(r => new { r.Inspector, r.FurnaceNo, r.Grade, r.Specification, r.InspectionStandard, r.Judgment, r.InspectionDate })
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["Inspector"] = all.Select(x => x.Inspector ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["FurnaceNo"] = all.Select(x => x.FurnaceNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Grade"] = all.Select(x => x.Grade ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Specification"] = all.Select(x => x.Specification ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["InspectionStandard"] = all.Select(x => x.InspectionStandard ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Judgment"] = all.Select(x => x.Judgment ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["InspectionDate"] = all.Select(x => x.InspectionDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList()
        };

        }) ?? new Dictionary<string, List<string>>();
    }

    private static IQueryable<TensileTest> ApplySorting(IQueryable<TensileTest> queryable, string sortBy, bool isDescending)
        => queryable.ApplySort(sortBy, isDescending);

    private static TensileTestDto MapToDto(TensileTest e) => new()
    {
        Id = e.Id, InspectionDate = e.InspectionDate, Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo, Grade = e.Grade, Specification = e.Specification,
        SampleNo = e.SampleNo, SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard,
        OriginalGaugeLength = e.OriginalGaugeLength, FinalGaugeLength = e.FinalGaugeLength,
        TensileStrength = e.TensileStrength, YieldStrengthRp02 = e.YieldStrengthRp02,
        YieldStrengthRp1 = e.YieldStrengthRp1, Elongation = e.Elongation,
        Judgment = e.Judgment,
        CreatedTime = e.CreatedTime, UpdatedTime = e.UpdatedTime
    };

    private static System.Linq.Expressions.Expression<Func<TensileTest, TensileTestDto>> MapToDto() => e => new TensileTestDto
    {
        Id = e.Id, InspectionDate = e.InspectionDate, Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo, Grade = e.Grade, Specification = e.Specification,
        SampleNo = e.SampleNo, SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard,
        OriginalGaugeLength = e.OriginalGaugeLength, FinalGaugeLength = e.FinalGaugeLength,
        TensileStrength = e.TensileStrength, YieldStrengthRp02 = e.YieldStrengthRp02,
        YieldStrengthRp1 = e.YieldStrengthRp1, Elongation = e.Elongation,
        Judgment = e.Judgment,
        CreatedTime = e.CreatedTime, UpdatedTime = e.UpdatedTime
    };

    private static TensileTest MapToEntity(CreateTensileTestRequest r) => new()
    {
        InspectionDate = r.InspectionDate, Inspector = r.Inspector,
        FurnaceNo = r.FurnaceNo, Grade = r.Grade, Specification = r.Specification,
        SampleNo = r.SampleNo, SampleSize = r.SampleSize,
        InspectionStandard = r.InspectionStandard,
        OriginalGaugeLength = r.OriginalGaugeLength, FinalGaugeLength = r.FinalGaugeLength,
        TensileStrength = r.TensileStrength, YieldStrengthRp02 = r.YieldStrengthRp02,
        YieldStrengthRp1 = r.YieldStrengthRp1, Elongation = r.Elongation,
        Judgment = r.Judgment
    };
}
