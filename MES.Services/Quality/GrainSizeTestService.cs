using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;

namespace MES.Services.Quality;

public class GrainSizeTestService : IGrainSizeTestService
{
    private readonly AppDbContext _context;
    private readonly ILogger<GrainSizeTestService> _logger;

    public GrainSizeTestService(AppDbContext context, ILogger<GrainSizeTestService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<GrainSizeTestDto?> GetByIdAsync(int id)
    {
        return await _context.GrainSizeTests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync();
    }

    public async Task<PagedResult<GrainSizeTestDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.GrainSizeTests
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
                (r.GrainSizeGrade != null && r.GrainSizeGrade.Contains(kw)) ||
                (r.GrainSizeMethod != null && r.GrainSizeMethod.Contains(kw)) ||
                (r.Magnification != null && r.Magnification.Contains(kw)));
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

        return new PagedResult<GrainSizeTestDto>
        {
            Items = items, TotalCount = totalCount,
            PageIndex = query.PageIndex, PageSize = query.PageSize
        };
    }

    public async Task<GrainSizeTestDto> CreateAsync(CreateGrainSizeTestRequest request)
    {
        var entity = MapToEntity(request);
        _context.GrainSizeTests.Add(entity);
        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<GrainSizeTestDto> UpdateAsync(int id, UpdateGrainSizeTestRequest request)
    {
        var entity = await _context.GrainSizeTests.FindAsync(id)
            ?? throw new BusinessException("晶粒度检验记录不存在");

        entity.InspectionDate = request.InspectionDate;
        entity.Inspector = request.Inspector ?? entity.Inspector;
        entity.FurnaceNo = request.FurnaceNo ?? entity.FurnaceNo;
        entity.Grade = request.Grade ?? entity.Grade;
        entity.Specification = request.Specification ?? entity.Specification;
        entity.SampleNo = request.SampleNo ?? entity.SampleNo;
        entity.SampleSize = request.SampleSize ?? entity.SampleSize;
        entity.InspectionStandard = request.InspectionStandard ?? entity.InspectionStandard;
        entity.GrainSizeGrade = request.GrainSizeGrade ?? entity.GrainSizeGrade;
        entity.GrainSizeMethod = request.GrainSizeMethod ?? entity.GrainSizeMethod;
        entity.Magnification = request.Magnification ?? entity.Magnification;
        entity.Judgment = request.Judgment ?? entity.Judgment;

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.GrainSizeTests.FindAsync(id)
            ?? throw new BusinessException("晶粒度检验记录不存在");
        _context.GrainSizeTests.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<GrainSizeTestDto>> BatchCreateAsync(List<CreateGrainSizeTestRequest> requests)
    {
        if (requests.Count == 0) return new List<GrainSizeTestDto>();
        var entities = requests.Select(MapToEntity).ToList();
        _context.GrainSizeTests.AddRange(entities);
        await _context.SaveChangesAsync();
        return entities.Select(e => MapToDto(e)).ToList();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var all = await _context.GrainSizeTests
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
    }

    private static IQueryable<GrainSizeTest> ApplySorting(IQueryable<GrainSizeTest> queryable, string sortBy, bool isDescending)
        => queryable.ApplySort(sortBy, isDescending);

    private static GrainSizeTestDto MapToDto(GrainSizeTest e) => new()
    {
        Id = e.Id, InspectionDate = e.InspectionDate, Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo, Grade = e.Grade, Specification = e.Specification,
        SampleNo = e.SampleNo, SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard, GrainSizeGrade = e.GrainSizeGrade,
        GrainSizeMethod = e.GrainSizeMethod, Magnification = e.Magnification, Judgment = e.Judgment,
        CreatedTime = e.CreatedTime, UpdatedTime = e.UpdatedTime
    };

    private static System.Linq.Expressions.Expression<Func<GrainSizeTest, GrainSizeTestDto>> MapToDto() => e => new GrainSizeTestDto
    {
        Id = e.Id, InspectionDate = e.InspectionDate, Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo, Grade = e.Grade, Specification = e.Specification,
        SampleNo = e.SampleNo, SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard, GrainSizeGrade = e.GrainSizeGrade,
        GrainSizeMethod = e.GrainSizeMethod, Magnification = e.Magnification, Judgment = e.Judgment,
        CreatedTime = e.CreatedTime, UpdatedTime = e.UpdatedTime
    };

    private static GrainSizeTest MapToEntity(CreateGrainSizeTestRequest r) => new()
    {
        InspectionDate = r.InspectionDate, Inspector = r.Inspector,
        FurnaceNo = r.FurnaceNo, Grade = r.Grade, Specification = r.Specification,
        SampleNo = r.SampleNo, SampleSize = r.SampleSize,
        InspectionStandard = r.InspectionStandard, GrainSizeGrade = r.GrainSizeGrade,
        GrainSizeMethod = r.GrainSizeMethod, Magnification = r.Magnification, Judgment = r.Judgment
    };
}
