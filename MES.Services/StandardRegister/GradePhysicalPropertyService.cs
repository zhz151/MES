using Microsoft.EntityFrameworkCore;
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
        return ToPhysicalPropertyDto(entity);
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
        return ToPhysicalPropertyDto(entity);
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
        return ToPhysicalPropertyDto(entity);
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

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var all = await GetAllAsync();
        var selected = all.Where(i => ids.Contains(i.Id)).ToList();
        return GradePhysicalPropertyPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? null! : sortBy,
            IsDescending = isDescending
        };
        var result = await GetPagedAsync(query);
        return GradePhysicalPropertyPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var all = await _context.GradePhysicalProperties
            .AsNoTracking()
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["StandardGrade"] = all.Select(x => x.StandardGrade).Distinct().OrderBy(x => x).ToList(),
            ["StandardGradeCategory"] = all.Select(x => x.StandardGradeCategory).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Density"] = all.Select(x => x.Density.ToString()).Distinct().OrderBy(x => x).ToList(),
            ["HeatTreatmentTemp"] = all.Select(x => x.HeatTreatmentTemp).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["HardnessRockwell"] = all.Select(x => x.HardnessRockwell).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["HardnessVickers"] = all.Select(x => x.HardnessVickers).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["HardnessBrinell"] = all.Select(x => x.HardnessBrinell).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["TensileStrength"] = all.Select(x => x.TensileStrength).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["YieldStrength02"] = all.Select(x => x.YieldStrength02).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["YieldStrength10"] = all.Select(x => x.YieldStrength10).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Elongation"] = all.Select(x => x.Elongation).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["GrainSize"] = all.Select(x => x.GrainSize).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
        };
    }

    private static GradePhysicalPropertyDto ToPhysicalPropertyDto(GradePhysicalProperty entity) => new()
    {
        Id = entity.Id,
        StandardGrade = entity.StandardGrade,
        StandardGradeCategory = entity.StandardGradeCategory,
        Density = entity.Density,
        HeatTreatmentTemp = entity.HeatTreatmentTemp,
        HardnessRockwell = entity.HardnessRockwell,
        HardnessVickers = entity.HardnessVickers,
        HardnessBrinell = entity.HardnessBrinell,
        TensileStrength = entity.TensileStrength,
        YieldStrength02 = entity.YieldStrength02,
        YieldStrength10 = entity.YieldStrength10,
        Elongation = entity.Elongation,
        GrainSize = entity.GrainSize
    };
}
