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

public class GradePhysicalPropertyService : IGradePhysicalPropertyService
{
    private readonly AppDbContext _context;

    public GradePhysicalPropertyService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<GradePhysicalPropertyDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.GradePhysicalProperties
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
            .Select(g => new GradePhysicalPropertyDto
            {
                Id = g.Id,
                StandardGrade = g.StandardGrade,
                StandardGradeCategory = g.StandardGradeCategory,
                Density = g.Density,
                HeatTreatmentTemp = g.HeatTreatmentTemp,
                HardnessRockwell = g.HardnessRockwell,
                HardnessVickers = g.HardnessVickers,
                HardnessBrinell = g.HardnessBrinell,
                TensileStrength = g.TensileStrength,
                YieldStrength02 = g.YieldStrength02,
                YieldStrength10 = g.YieldStrength10,
                Elongation = g.Elongation,
                GrainSize = g.GrainSize
            })
            .ToListAsync();

        return new PagedResult<GradePhysicalPropertyDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<GradePhysicalPropertyDto>> GetAllAsync()
    {
        return await _context.GradePhysicalProperties
            .AsNoTracking()
            .OrderBy(g => g.StandardGrade)
            .Select(g => new GradePhysicalPropertyDto
            {
                Id = g.Id,
                StandardGrade = g.StandardGrade,
                StandardGradeCategory = g.StandardGradeCategory,
                Density = g.Density,
                HeatTreatmentTemp = g.HeatTreatmentTemp,
                HardnessRockwell = g.HardnessRockwell,
                HardnessVickers = g.HardnessVickers,
                HardnessBrinell = g.HardnessBrinell,
                TensileStrength = g.TensileStrength,
                YieldStrength02 = g.YieldStrength02,
                YieldStrength10 = g.YieldStrength10,
                Elongation = g.Elongation,
                GrainSize = g.GrainSize
            })
            .ToListAsync();
    }

    public async Task<GradePhysicalPropertyDto> GetByIdAsync(int id)
    {
        var entity = await _context.GradePhysicalProperties
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id);
        if (entity == null)
            throw new BusinessException("牌号物理性能不存在");
        return entity.ToPhysicalPropertyDto();
    }

    public async Task<GradePhysicalPropertyDto> CreateAsync(CreateGradePhysicalPropertyRequest request)
    {
        var exists = await _context.GradePhysicalProperties
            .AnyAsync(g => g.StandardGrade == request.StandardGrade && g.StandardGradeCategory == request.StandardGradeCategory);
        if (exists)
            throw new BusinessException($"标准牌号 '{request.StandardGrade}' 已存在");

        var entity = new GradePhysicalProperty
        {
            StandardGrade = request.StandardGrade,
            StandardGradeCategory = request.StandardGradeCategory,
            Density = request.Density,
            HeatTreatmentTemp = request.HeatTreatmentTemp,
            HardnessRockwell = request.HardnessRockwell,
            HardnessVickers = request.HardnessVickers,
            HardnessBrinell = request.HardnessBrinell,
            TensileStrength = request.TensileStrength,
            YieldStrength02 = request.YieldStrength02,
            YieldStrength10 = request.YieldStrength10,
            Elongation = request.Elongation,
            GrainSize = request.GrainSize
        };

        _context.GradePhysicalProperties.Add(entity);
        await _context.SaveChangesAsync();
        return entity.ToPhysicalPropertyDto();
    }

    public async Task<GradePhysicalPropertyDto> UpdateAsync(int id, UpdateGradePhysicalPropertyRequest request)
    {
        var entity = await _context.GradePhysicalProperties
            .FirstOrDefaultAsync(g => g.Id == id);
        if (entity == null)
            throw new BusinessException("牌号物理性能不存在");

        var gradeChanged = request.StandardGrade != entity.StandardGrade ||
            request.StandardGradeCategory != entity.StandardGradeCategory;
        if (gradeChanged)
        {
            var exists = await _context.GradePhysicalProperties
                .AnyAsync(g => g.StandardGrade == request.StandardGrade && g.StandardGradeCategory == request.StandardGradeCategory && g.Id != id);
            if (exists)
                throw new BusinessException($"标准牌号 '{request.StandardGrade}' 已存在");
            entity.StandardGrade = request.StandardGrade;
            entity.StandardGradeCategory = request.StandardGradeCategory;
        }

        if (request.Density.HasValue) entity.Density = request.Density.Value;
        if (request.HeatTreatmentTemp != null) entity.HeatTreatmentTemp = request.HeatTreatmentTemp;
        if (request.HardnessRockwell != null) entity.HardnessRockwell = request.HardnessRockwell;
        if (request.HardnessVickers != null) entity.HardnessVickers = request.HardnessVickers;
        if (request.HardnessBrinell != null) entity.HardnessBrinell = request.HardnessBrinell;
        if (request.TensileStrength != null) entity.TensileStrength = request.TensileStrength;
        if (request.YieldStrength02 != null) entity.YieldStrength02 = request.YieldStrength02;
        if (request.YieldStrength10 != null) entity.YieldStrength10 = request.YieldStrength10;
        if (request.Elongation != null) entity.Elongation = request.Elongation;
        if (request.GrainSize != null) entity.GrainSize = request.GrainSize;

        await _context.SaveChangesAsync();
        return entity.ToPhysicalPropertyDto();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.GradePhysicalProperties
            .FirstOrDefaultAsync(g => g.Id == id);
        if (entity == null)
            throw new BusinessException("牌号物理性能不存在");
        _context.GradePhysicalProperties.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.GradePhysicalProperties.AsNoTracking();
        return new Dictionary<string, List<string>>
        {
            ["StandardGrade"] = await query.Select(x => x.StandardGrade).Distinct().OrderBy(x => x).ToListAsync(),
            ["StandardGradeCategory"] = await query.Where(x => x.StandardGradeCategory != null).Select(x => x.StandardGradeCategory!).Distinct().OrderBy(x => x).ToListAsync(),
        };
    }
}
