using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;
using MES.Services.Mapping;

namespace MES.Services.ProductionStandard;

public class GradeChemicalCompositionService : IGradeChemicalCompositionService
{
    private readonly AppDbContext _context;

    public GradeChemicalCompositionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<GradeChemicalCompositionDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.GradeChemicalCompositions
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                queryable = queryable.Where(g =>
                    g.StandardGrade.Contains(keyword) ||
                    (g.StandardGradeCategory != null && g.StandardGradeCategory.Contains(keyword)));
            }
        }

        queryable = queryable.ApplyFilters(query.Filters);
        var sortBy = string.IsNullOrEmpty(query.SortBy) || query.SortBy.Equals("CreatedTime", StringComparison.OrdinalIgnoreCase)
            ? "StandardGrade"
            : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(g => new GradeChemicalCompositionDto
            {
                Id = g.Id,
                StandardGrade = g.StandardGrade,
                StandardGradeCategory = g.StandardGradeCategory,
                Carbon = g.Carbon,
                Silicon = g.Silicon,
                Manganese = g.Manganese,
                Phosphorus = g.Phosphorus,
                Sulfur = g.Sulfur,
                Nickel = g.Nickel,
                Chromium = g.Chromium,
                Molybdenum = g.Molybdenum,
                Copper = g.Copper,
                Nitrogen = g.Nitrogen,
                Niobium = g.Niobium,
                Titanium = g.Titanium,
                Iron = g.Iron,
                Aluminum = g.Aluminum,
                Tungsten = g.Tungsten
            })
            .ToListAsync();

        return new PagedResult<GradeChemicalCompositionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<GradeChemicalCompositionDto>> GetAllAsync()
    {
        return await _context.GradeChemicalCompositions
            .AsNoTracking()
            .OrderBy(g => g.StandardGrade)
            .Select(g => new GradeChemicalCompositionDto
            {
                Id = g.Id,
                StandardGrade = g.StandardGrade,
                StandardGradeCategory = g.StandardGradeCategory,
                Carbon = g.Carbon,
                Silicon = g.Silicon,
                Manganese = g.Manganese,
                Phosphorus = g.Phosphorus,
                Sulfur = g.Sulfur,
                Nickel = g.Nickel,
                Chromium = g.Chromium,
                Molybdenum = g.Molybdenum,
                Copper = g.Copper,
                Nitrogen = g.Nitrogen,
                Niobium = g.Niobium,
                Titanium = g.Titanium,
                Iron = g.Iron,
                Aluminum = g.Aluminum,
                Tungsten = g.Tungsten
            })
            .ToListAsync();
    }

    public async Task<GradeChemicalCompositionDto> GetByIdAsync(int id)
    {
        var entity = await _context.GradeChemicalCompositions
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id);
        if (entity == null)
            throw new BusinessException("牌号化学成分不存在");
        return entity.ToChemicalCompositionDto();
    }

    public async Task<GradeChemicalCompositionDto> CreateAsync(CreateGradeChemicalCompositionRequest request)
    {
        var exists = await _context.GradeChemicalCompositions
            .AnyAsync(g => g.StandardGrade == request.StandardGrade && g.StandardGradeCategory == request.StandardGradeCategory);
        if (exists)
            throw new BusinessException($"标准牌号 '{request.StandardGrade}' 已存在");

        var entity = new GradeChemicalComposition
        {
            StandardGrade = request.StandardGrade,
            StandardGradeCategory = request.StandardGradeCategory,
            Carbon = request.Carbon,
            Silicon = request.Silicon,
            Manganese = request.Manganese,
            Phosphorus = request.Phosphorus,
            Sulfur = request.Sulfur,
            Nickel = request.Nickel,
            Chromium = request.Chromium,
            Molybdenum = request.Molybdenum,
            Copper = request.Copper,
            Nitrogen = request.Nitrogen,
            Niobium = request.Niobium,
            Titanium = request.Titanium,
            Iron = request.Iron,
            Aluminum = request.Aluminum,
            Tungsten = request.Tungsten
        };

        _context.GradeChemicalCompositions.Add(entity);
        await _context.SaveChangesAsync();
        return entity.ToChemicalCompositionDto();
    }

    public async Task<GradeChemicalCompositionDto> UpdateAsync(int id, UpdateGradeChemicalCompositionRequest request)
    {
        var entity = await _context.GradeChemicalCompositions
            .FirstOrDefaultAsync(g => g.Id == id);
        if (entity == null)
            throw new BusinessException("牌号化学成分不存在");

        var gradeChanged = request.StandardGrade != entity.StandardGrade ||
            request.StandardGradeCategory != entity.StandardGradeCategory;
        if (gradeChanged)
        {
            var exists = await _context.GradeChemicalCompositions
                .AnyAsync(g => g.StandardGrade == request.StandardGrade && g.StandardGradeCategory == request.StandardGradeCategory && g.Id != id);
            if (exists)
                throw new BusinessException($"标准牌号 '{request.StandardGrade}' 已存在");
            entity.StandardGrade = request.StandardGrade;
            entity.StandardGradeCategory = request.StandardGradeCategory;
        }

        if (request.Carbon != null) entity.Carbon = request.Carbon;
        if (request.Silicon != null) entity.Silicon = request.Silicon;
        if (request.Manganese != null) entity.Manganese = request.Manganese;
        if (request.Phosphorus != null) entity.Phosphorus = request.Phosphorus;
        if (request.Sulfur != null) entity.Sulfur = request.Sulfur;
        if (request.Nickel != null) entity.Nickel = request.Nickel;
        if (request.Chromium != null) entity.Chromium = request.Chromium;
        if (request.Molybdenum != null) entity.Molybdenum = request.Molybdenum;
        if (request.Copper != null) entity.Copper = request.Copper;
        if (request.Nitrogen != null) entity.Nitrogen = request.Nitrogen;
        if (request.Niobium != null) entity.Niobium = request.Niobium;
        if (request.Titanium != null) entity.Titanium = request.Titanium;
        if (request.Iron != null) entity.Iron = request.Iron;
        if (request.Aluminum != null) entity.Aluminum = request.Aluminum;
        if (request.Tungsten != null) entity.Tungsten = request.Tungsten;

        await _context.SaveChangesAsync();
        return entity.ToChemicalCompositionDto();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.GradeChemicalCompositions
            .FirstOrDefaultAsync(g => g.Id == id);
        if (entity == null)
            throw new BusinessException("牌号化学成分不存在");
        _context.GradeChemicalCompositions.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.GradeChemicalCompositions.AsNoTracking();
        return new Dictionary<string, List<string>>
        {
            ["StandardGrade"] = await query.Select(x => x.StandardGrade).Distinct().OrderBy(x => x).ToListAsync(),
            ["StandardGradeCategory"] = await query.Where(x => x.StandardGradeCategory != null).Select(x => x.StandardGradeCategory!).Distinct().OrderBy(x => x).ToListAsync(),
        };
    }
}
