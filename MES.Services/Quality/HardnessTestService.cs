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

public class HardnessTestService : IHardnessTestService
{
    private readonly AppDbContext _context;
    private readonly ILogger<HardnessTestService> _logger;
    private readonly IMemoryCache _cache;

    public HardnessTestService(AppDbContext context, ILogger<HardnessTestService> logger, IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    public async Task<HardnessTestDto?> GetByIdAsync(int id)
    {
        return await _context.HardnessTests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync();
    }

    public async Task<PagedResult<HardnessTestDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.HardnessTests
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
                (r.HardnessMode != null && r.HardnessMode.Contains(kw)) ||
                (r.HardnessValue != null && r.HardnessValue.Contains(kw)));
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

        return new PagedResult<HardnessTestDto>
        {
            Items = items, TotalCount = totalCount,
            PageIndex = query.PageIndex, PageSize = query.PageSize
        };
    }

    public async Task<HardnessTestDto> CreateAsync(CreateHardnessTestRequest request)
    {
        var entity = MapToEntity(request);
        _context.HardnessTests.Add(entity);
        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<HardnessTestDto> UpdateAsync(int id, UpdateHardnessTestRequest request)
    {
        var entity = await _context.HardnessTests.FindAsync(id)
            ?? throw new BusinessException("硬度检验记录不存在");

        entity.InspectionDate = request.InspectionDate;
        entity.Inspector = request.Inspector ?? entity.Inspector;
        entity.FurnaceNo = request.FurnaceNo ?? entity.FurnaceNo;
        entity.Grade = request.Grade ?? entity.Grade;
        entity.Specification = request.Specification ?? entity.Specification;
        entity.SampleNo = request.SampleNo ?? entity.SampleNo;
        entity.SampleSize = request.SampleSize ?? entity.SampleSize;
        entity.InspectionStandard = request.InspectionStandard ?? entity.InspectionStandard;
        entity.HardnessMode = request.HardnessMode ?? entity.HardnessMode;
        entity.HardnessValue = request.HardnessValue ?? entity.HardnessValue;
        entity.Judgment = request.Judgment ?? entity.Judgment;

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.HardnessTests.FindAsync(id)
            ?? throw new BusinessException("硬度检验记录不存在");
        _context.HardnessTests.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<HardnessTestDto>> BatchCreateAsync(List<CreateHardnessTestRequest> requests)
    {
        if (requests.Count == 0) return new List<HardnessTestDto>();
        var entities = requests.Select(MapToEntity).ToList();
        _context.HardnessTests.AddRange(entities);
        await _context.SaveChangesAsync();
        return entities.Select(e => MapToDto(e)).ToList();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("HardnessTestService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var all = await _context.HardnessTests
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

    private static IQueryable<HardnessTest> ApplySorting(IQueryable<HardnessTest> queryable, string sortBy, bool isDescending)
        => queryable.ApplySort(sortBy, isDescending);

    private static HardnessTestDto MapToDto(HardnessTest e) => new()
    {
        Id = e.Id, InspectionDate = e.InspectionDate, Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo, Grade = e.Grade, Specification = e.Specification,
        SampleNo = e.SampleNo, SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard, HardnessMode = e.HardnessMode,
        HardnessValue = e.HardnessValue, Judgment = e.Judgment,
        CreatedTime = e.CreatedTime, UpdatedTime = e.UpdatedTime
    };

    private static System.Linq.Expressions.Expression<Func<HardnessTest, HardnessTestDto>> MapToDto() => e => new HardnessTestDto
    {
        Id = e.Id, InspectionDate = e.InspectionDate, Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo, Grade = e.Grade, Specification = e.Specification,
        SampleNo = e.SampleNo, SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard, HardnessMode = e.HardnessMode,
        HardnessValue = e.HardnessValue, Judgment = e.Judgment,
        CreatedTime = e.CreatedTime, UpdatedTime = e.UpdatedTime
    };

    private static HardnessTest MapToEntity(CreateHardnessTestRequest r) => new()
    {
        InspectionDate = r.InspectionDate, Inspector = r.Inspector,
        FurnaceNo = r.FurnaceNo, Grade = r.Grade, Specification = r.Specification,
        SampleNo = r.SampleNo, SampleSize = r.SampleSize,
        InspectionStandard = r.InspectionStandard, HardnessMode = r.HardnessMode,
        HardnessValue = r.HardnessValue, Judgment = r.Judgment
    };
}
