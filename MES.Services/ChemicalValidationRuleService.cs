using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// 牌号验证服务实现
/// </summary>
public class ChemicalValidationRuleService : IChemicalValidationRuleService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ChemicalValidationRuleService> _logger;

    public ChemicalValidationRuleService(
        AppDbContext context,
        ILogger<ChemicalValidationRuleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<ChemicalValidationRuleDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.ChemicalValidationRules
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            queryable = queryable.Where(r => r.PlantGrade.Contains(query.Keyword));
        }

        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.SortBy ?? "plantgrade", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => ToDto(r))
            .ToListAsync();

        return new PagedResult<ChemicalValidationRuleDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<ChemicalValidationRuleDto>> BatchCreateAsync(List<CreateChemicalValidationRuleRequest> requests)
    {
        if (requests.Count == 0)
            return new List<ChemicalValidationRuleDto>();

        // 检查牌号重复
        var plantGrades = requests.Select(r => r.PlantGrade).Distinct().ToList();
        var existing = await _context.ChemicalValidationRules
            .Where(c => plantGrades.Contains(c.PlantGrade))
            .Select(c => c.PlantGrade)
            .ToListAsync();

        if (existing.Any())
            throw new BusinessException($"以下工厂牌号已存在: {string.Join(", ", existing)}");

        var entities = requests.Select(r => new ChemicalValidationRule
        {
            PlantGrade = r.PlantGrade,
            CMin = r.CMin, CMax = r.CMax,
            SiMin = r.SiMin, SiMax = r.SiMax,
            MnMin = r.MnMin, MnMax = r.MnMax,
            PMin = r.PMin, PMax = r.PMax,
            SMin = r.SMin, SMax = r.SMax,
            NiMin = r.NiMin, NiMax = r.NiMax,
            CrMin = r.CrMin, CrMax = r.CrMax,
            MoMin = r.MoMin, MoMax = r.MoMax,
            CuMin = r.CuMin, CuMax = r.CuMax,
            NMin = r.NMin, NMax = r.NMax,
            NbMin = r.NbMin, NbMax = r.NbMax,
            TiMin = r.TiMin, TiMax = r.TiMax,
            FeMin = r.FeMin, FeMax = r.FeMax,
            AlMin = r.AlMin, AlMax = r.AlMax,
            WMin = r.WMin, WMax = r.WMax,
            PRENMin = r.PRENMin,
        }).ToList();

        _context.ChemicalValidationRules.AddRange(entities);
        await _context.SaveChangesAsync();

        return entities.Select(e => ToDto(e)).ToList();
    }

    public async Task<ChemicalValidationRuleDto> UpdateAsync(int id, UpdateChemicalValidationRuleRequest request)
    {
        var entity = await _context.ChemicalValidationRules.FindAsync(id)
            ?? throw new BusinessException($"牌号验证记录不存在(Id={id})");

        entity.PlantGrade = request.PlantGrade;
        entity.CMin = request.CMin; entity.CMax = request.CMax;
        entity.SiMin = request.SiMin; entity.SiMax = request.SiMax;
        entity.MnMin = request.MnMin; entity.MnMax = request.MnMax;
        entity.PMin = request.PMin; entity.PMax = request.PMax;
        entity.SMin = request.SMin; entity.SMax = request.SMax;
        entity.NiMin = request.NiMin; entity.NiMax = request.NiMax;
        entity.CrMin = request.CrMin; entity.CrMax = request.CrMax;
        entity.MoMin = request.MoMin; entity.MoMax = request.MoMax;
        entity.CuMin = request.CuMin; entity.CuMax = request.CuMax;
        entity.NMin = request.NMin; entity.NMax = request.NMax;
        entity.NbMin = request.NbMin; entity.NbMax = request.NbMax;
        entity.TiMin = request.TiMin; entity.TiMax = request.TiMax;
        entity.FeMin = request.FeMin; entity.FeMax = request.FeMax;
        entity.AlMin = request.AlMin; entity.AlMax = request.AlMax;
        entity.WMin = request.WMin; entity.WMax = request.WMax;
        entity.PRENMin = request.PRENMin;

        await _context.SaveChangesAsync();

        return ToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.ChemicalValidationRules.FindAsync(id)
            ?? throw new BusinessException($"牌号验证记录不存在(Id={id})");

        _context.ChemicalValidationRules.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<ChemicalValidationRuleDto?> GetByPlantGradeAsync(string plantGrade)
    {
        return await _context.ChemicalValidationRules
            .AsNoTracking()
            .Where(r => r.PlantGrade == plantGrade)
            .Select(r => ToDto(r))
            .FirstOrDefaultAsync();
    }

    private static ChemicalValidationRuleDto ToDto(ChemicalValidationRule r) => new()
    {
        Id = r.Id,
        PlantGrade = r.PlantGrade,
        CMin = r.CMin, CMax = r.CMax,
        SiMin = r.SiMin, SiMax = r.SiMax,
        MnMin = r.MnMin, MnMax = r.MnMax,
        PMin = r.PMin, PMax = r.PMax,
        SMin = r.SMin, SMax = r.SMax,
        NiMin = r.NiMin, NiMax = r.NiMax,
        CrMin = r.CrMin, CrMax = r.CrMax,
        MoMin = r.MoMin, MoMax = r.MoMax,
        CuMin = r.CuMin, CuMax = r.CuMax,
        NMin = r.NMin, NMax = r.NMax,
        NbMin = r.NbMin, NbMax = r.NbMax,
        TiMin = r.TiMin, TiMax = r.TiMax,
        FeMin = r.FeMin, FeMax = r.FeMax,
        AlMin = r.AlMin, AlMax = r.AlMax,
        WMin = r.WMin, WMax = r.WMax,
        PRENMin = r.PRENMin,
        CreatedTime = r.CreatedTime,
        UpdatedTime = r.UpdatedTime
    };

    private static IQueryable<ChemicalValidationRule> ApplySorting(IQueryable<ChemicalValidationRule> queryable, string sortBy, bool isDescending)
    {
        return (sortBy.ToLower(), isDescending) switch
        {
            ("plantgrade", false) => queryable.OrderBy(r => r.PlantGrade),
            ("plantgrade", true) => queryable.OrderByDescending(r => r.PlantGrade),
            ("cmin", false) => queryable.OrderBy(r => r.CMin ?? ""),
            ("cmin", true) => queryable.OrderByDescending(r => r.CMin ?? ""),
            ("cmax", false) => queryable.OrderBy(r => r.CMax ?? ""),
            ("cmax", true) => queryable.OrderByDescending(r => r.CMax ?? ""),
            ("simin", false) => queryable.OrderBy(r => r.SiMin ?? ""),
            ("simin", true) => queryable.OrderByDescending(r => r.SiMin ?? ""),
            ("simax", false) => queryable.OrderBy(r => r.SiMax ?? ""),
            ("simax", true) => queryable.OrderByDescending(r => r.SiMax ?? ""),
            ("mnmin", false) => queryable.OrderBy(r => r.MnMin ?? ""),
            ("mnmin", true) => queryable.OrderByDescending(r => r.MnMin ?? ""),
            ("mnmax", false) => queryable.OrderBy(r => r.MnMax ?? ""),
            ("mnmax", true) => queryable.OrderByDescending(r => r.MnMax ?? ""),
            ("pmin", false) => queryable.OrderBy(r => r.PMin ?? ""),
            ("pmin", true) => queryable.OrderByDescending(r => r.PMin ?? ""),
            ("pmax", false) => queryable.OrderBy(r => r.PMax ?? ""),
            ("pmax", true) => queryable.OrderByDescending(r => r.PMax ?? ""),
            ("smin", false) => queryable.OrderBy(r => r.SMin ?? ""),
            ("smin", true) => queryable.OrderByDescending(r => r.SMin ?? ""),
            ("smax", false) => queryable.OrderBy(r => r.SMax ?? ""),
            ("smax", true) => queryable.OrderByDescending(r => r.SMax ?? ""),
            ("nimin", false) => queryable.OrderBy(r => r.NiMin ?? ""),
            ("nimin", true) => queryable.OrderByDescending(r => r.NiMin ?? ""),
            ("nimax", false) => queryable.OrderBy(r => r.NiMax ?? ""),
            ("nimax", true) => queryable.OrderByDescending(r => r.NiMax ?? ""),
            ("crmin", false) => queryable.OrderBy(r => r.CrMin ?? ""),
            ("crmin", true) => queryable.OrderByDescending(r => r.CrMin ?? ""),
            ("crmax", false) => queryable.OrderBy(r => r.CrMax ?? ""),
            ("crmax", true) => queryable.OrderByDescending(r => r.CrMax ?? ""),
            ("momin", false) => queryable.OrderBy(r => r.MoMin ?? ""),
            ("momin", true) => queryable.OrderByDescending(r => r.MoMin ?? ""),
            ("momax", false) => queryable.OrderBy(r => r.MoMax ?? ""),
            ("momax", true) => queryable.OrderByDescending(r => r.MoMax ?? ""),
            ("cumin", false) => queryable.OrderBy(r => r.CuMin ?? ""),
            ("cumin", true) => queryable.OrderByDescending(r => r.CuMin ?? ""),
            ("cumax", false) => queryable.OrderBy(r => r.CuMax ?? ""),
            ("cumax", true) => queryable.OrderByDescending(r => r.CuMax ?? ""),
            ("nmin", false) => queryable.OrderBy(r => r.NMin ?? ""),
            ("nmin", true) => queryable.OrderByDescending(r => r.NMin ?? ""),
            ("nmax", false) => queryable.OrderBy(r => r.NMax ?? ""),
            ("nmax", true) => queryable.OrderByDescending(r => r.NMax ?? ""),
            ("nbmin", false) => queryable.OrderBy(r => r.NbMin ?? ""),
            ("nbmin", true) => queryable.OrderByDescending(r => r.NbMin ?? ""),
            ("nbmax", false) => queryable.OrderBy(r => r.NbMax ?? ""),
            ("nbmax", true) => queryable.OrderByDescending(r => r.NbMax ?? ""),
            ("timin", false) => queryable.OrderBy(r => r.TiMin ?? ""),
            ("timin", true) => queryable.OrderByDescending(r => r.TiMin ?? ""),
            ("timax", false) => queryable.OrderBy(r => r.TiMax ?? ""),
            ("timax", true) => queryable.OrderByDescending(r => r.TiMax ?? ""),
            ("femin", false) => queryable.OrderBy(r => r.FeMin ?? ""),
            ("femin", true) => queryable.OrderByDescending(r => r.FeMin ?? ""),
            ("femax", false) => queryable.OrderBy(r => r.FeMax ?? ""),
            ("femax", true) => queryable.OrderByDescending(r => r.FeMax ?? ""),
            ("almin", false) => queryable.OrderBy(r => r.AlMin ?? ""),
            ("almin", true) => queryable.OrderByDescending(r => r.AlMin ?? ""),
            ("almax", false) => queryable.OrderBy(r => r.AlMax ?? ""),
            ("almax", true) => queryable.OrderByDescending(r => r.AlMax ?? ""),
            ("wmin", false) => queryable.OrderBy(r => r.WMin ?? ""),
            ("wmin", true) => queryable.OrderByDescending(r => r.WMin ?? ""),
            ("wmax", false) => queryable.OrderBy(r => r.WMax ?? ""),
            ("wmax", true) => queryable.OrderByDescending(r => r.WMax ?? ""),
            ("prenmin", false) => queryable.OrderBy(r => r.PRENMin ?? ""),
            ("prenmin", true) => queryable.OrderByDescending(r => r.PRENMin ?? ""),
            ("createdtime", false) => queryable.OrderBy(r => r.CreatedTime),
            ("createdtime", true) => queryable.OrderByDescending(r => r.CreatedTime),
            ("updatedtime", false) => queryable.OrderBy(r => r.UpdatedTime),
            ("updatedtime", true) => queryable.OrderByDescending(r => r.UpdatedTime),
            _ => isDescending
                ? queryable.OrderByDescending(r => r.PlantGrade)
                : queryable.OrderBy(r => r.PlantGrade)
        };
    }
}
