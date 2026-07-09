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

public class FlaringTestService : IFlaringTestService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FlaringTestService> _logger;
    private readonly IMemoryCache _cache;

    public FlaringTestService(AppDbContext context, ILogger<FlaringTestService> logger, IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    public async Task<FlaringTestDto?> GetByIdAsync(int id)
    {
        return await _context.FlaringTests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync();
    }

    public async Task<PagedResult<FlaringTestDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.FlaringTests
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
                (r.MandrelTaper != null && r.MandrelTaper.Contains(kw)) ||
                (r.Observation != null && r.Observation.Contains(kw)) ||
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

        return new PagedResult<FlaringTestDto>
        {
            Items = items, TotalCount = totalCount,
            PageIndex = query.PageIndex, PageSize = query.PageSize
        };
    }

    public async Task<FlaringTestDto> CreateAsync(CreateFlaringTestRequest request)
    {
        var entity = MapToEntity(request);
        _context.FlaringTests.Add(entity);
        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<FlaringTestDto> UpdateAsync(int id, UpdateFlaringTestRequest request)
    {
        var entity = await _context.FlaringTests.FindAsync(id)
            ?? throw new BusinessException("扩口检验记录不存在");

        entity.InspectionDate = request.InspectionDate;
        entity.Inspector = request.Inspector ?? entity.Inspector;
        entity.FurnaceNo = request.FurnaceNo ?? entity.FurnaceNo;
        entity.Grade = request.Grade ?? entity.Grade;
        entity.Specification = request.Specification ?? entity.Specification;
        entity.SampleNo = request.SampleNo ?? entity.SampleNo;
        entity.SampleSize = request.SampleSize ?? entity.SampleSize;
        entity.InspectionStandard = request.InspectionStandard ?? entity.InspectionStandard;
        entity.MandrelTaper = request.MandrelTaper ?? entity.MandrelTaper;
        entity.FlaredDiameter = request.FlaredDiameter ?? entity.FlaredDiameter;
        entity.FlaringRate = request.FlaringRate ?? entity.FlaringRate;
        entity.Observation = request.Observation ?? entity.Observation;
        entity.Judgment = request.Judgment ?? entity.Judgment;

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.FlaringTests.FindAsync(id)
            ?? throw new BusinessException("扩口检验记录不存在");
        _context.FlaringTests.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<FlaringTestDto>> BatchCreateAsync(List<CreateFlaringTestRequest> requests)
    {
        if (requests.Count == 0) return new List<FlaringTestDto>();
        var entities = requests.Select(MapToEntity).ToList();
        _context.FlaringTests.AddRange(entities);
        await _context.SaveChangesAsync();
        return entities.Select(e => MapToDto(e)).ToList();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("FlaringTestService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var all = await _context.FlaringTests
                .AsNoTracking()
                .Select(r => new { r.Inspector, r.FurnaceNo, r.Grade, r.Specification, r.InspectionStandard, r.MandrelTaper, r.Judgment, r.InspectionDate })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["Inspector"] = all.Select(x => x.Inspector ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["FurnaceNo"] = all.Select(x => x.FurnaceNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Grade"] = all.Select(x => x.Grade ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Specification"] = all.Select(x => x.Specification ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["InspectionStandard"] = all.Select(x => x.InspectionStandard ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["MandrelTaper"] = all.Select(x => x.MandrelTaper ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Judgment"] = all.Select(x => x.Judgment ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["InspectionDate"] = all.Select(x => x.InspectionDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList()
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    private static IQueryable<FlaringTest> ApplySorting(IQueryable<FlaringTest> queryable, string sortBy, bool isDescending)
        => queryable.ApplySort(sortBy, isDescending);

    private static FlaringTestDto MapToDto(FlaringTest e) => new()
    {
        Id = e.Id, InspectionDate = e.InspectionDate, Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo, Grade = e.Grade, Specification = e.Specification,
        SampleNo = e.SampleNo, SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard, MandrelTaper = e.MandrelTaper,
        FlaredDiameter = e.FlaredDiameter, FlaringRate = e.FlaringRate,
        Observation = e.Observation, Judgment = e.Judgment,
        CreatedTime = e.CreatedTime, UpdatedTime = e.UpdatedTime
    };

    private static System.Linq.Expressions.Expression<Func<FlaringTest, FlaringTestDto>> MapToDto() => e => new FlaringTestDto
    {
        Id = e.Id, InspectionDate = e.InspectionDate, Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo, Grade = e.Grade, Specification = e.Specification,
        SampleNo = e.SampleNo, SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard, MandrelTaper = e.MandrelTaper,
        FlaredDiameter = e.FlaredDiameter, FlaringRate = e.FlaringRate,
        Observation = e.Observation, Judgment = e.Judgment,
        CreatedTime = e.CreatedTime, UpdatedTime = e.UpdatedTime
    };

    private static FlaringTest MapToEntity(CreateFlaringTestRequest r) => new()
    {
        InspectionDate = r.InspectionDate, Inspector = r.Inspector,
        FurnaceNo = r.FurnaceNo, Grade = r.Grade, Specification = r.Specification,
        SampleNo = r.SampleNo, SampleSize = r.SampleSize,
        InspectionStandard = r.InspectionStandard, MandrelTaper = r.MandrelTaper,
        FlaredDiameter = r.FlaredDiameter, FlaringRate = r.FlaringRate,
        Observation = r.Observation, Judgment = r.Judgment
    };
}
