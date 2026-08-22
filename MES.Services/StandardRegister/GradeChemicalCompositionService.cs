using Microsoft.EntityFrameworkCore;
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
using MES.Data.Entities.Quality;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.StandardRegister;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.StandardRegister;

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
        return ToChemicalCompositionDto(entity);
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
        return ToChemicalCompositionDto(entity);
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
        return ToChemicalCompositionDto(entity);
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
        var all = await _context.GradeChemicalCompositions
            .AsNoTracking()
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["StandardGrade"] = all.Select(x => x.StandardGrade).Distinct().OrderBy(x => x).ToList(),
            ["StandardGradeCategory"] = all.Select(x => x.StandardGradeCategory).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Carbon"] = all.Select(x => x.Carbon).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Silicon"] = all.Select(x => x.Silicon).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Manganese"] = all.Select(x => x.Manganese).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Phosphorus"] = all.Select(x => x.Phosphorus).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Sulfur"] = all.Select(x => x.Sulfur).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Nickel"] = all.Select(x => x.Nickel).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Chromium"] = all.Select(x => x.Chromium).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Molybdenum"] = all.Select(x => x.Molybdenum).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Copper"] = all.Select(x => x.Copper).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Nitrogen"] = all.Select(x => x.Nitrogen).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Niobium"] = all.Select(x => x.Niobium).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Titanium"] = all.Select(x => x.Titanium).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Iron"] = all.Select(x => x.Iron).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Aluminum"] = all.Select(x => x.Aluminum).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Tungsten"] = all.Select(x => x.Tungsten).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
        };
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var all = await GetAllAsync();
        var selected = all.Where(i => ids.Contains(i.Id)).ToList();
        return GradeChemicalCompositionPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "StandardGrade",
            IsDescending = isDescending
        };
        var paged = await GetPagedAsync(query);
        return GradeChemicalCompositionPrintHelper.GenerateBatchPdf(paged.Items, columns);
    }

    private static GradeChemicalCompositionDto ToChemicalCompositionDto(GradeChemicalComposition entity) => new()
    {
        Id = entity.Id,
        StandardGrade = entity.StandardGrade,
        StandardGradeCategory = entity.StandardGradeCategory,
        Carbon = entity.Carbon,
        Silicon = entity.Silicon,
        Manganese = entity.Manganese,
        Phosphorus = entity.Phosphorus,
        Sulfur = entity.Sulfur,
        Nickel = entity.Nickel,
        Chromium = entity.Chromium,
        Molybdenum = entity.Molybdenum,
        Copper = entity.Copper,
        Nitrogen = entity.Nitrogen,
        Niobium = entity.Niobium,
        Titanium = entity.Titanium,
        Iron = entity.Iron,
        Aluminum = entity.Aluminum,
        Tungsten = entity.Tungsten
    };
}
