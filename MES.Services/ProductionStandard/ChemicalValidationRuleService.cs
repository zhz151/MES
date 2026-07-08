using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.ProductionStandard;

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
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                r.PlantGrade.Contains(kw) ||
                (r.CMin != null && r.CMin.Contains(kw)) ||
                (r.CMax != null && r.CMax.Contains(kw)) ||
                (r.SiMin != null && r.SiMin.Contains(kw)) ||
                (r.SiMax != null && r.SiMax.Contains(kw)) ||
                (r.MnMin != null && r.MnMin.Contains(kw)) ||
                (r.MnMax != null && r.MnMax.Contains(kw)) ||
                (r.PMin != null && r.PMin.Contains(kw)) ||
                (r.PMax != null && r.PMax.Contains(kw)) ||
                (r.SMin != null && r.SMin.Contains(kw)) ||
                (r.SMax != null && r.SMax.Contains(kw)) ||
                (r.NiMin != null && r.NiMin.Contains(kw)) ||
                (r.NiMax != null && r.NiMax.Contains(kw)) ||
                (r.CrMin != null && r.CrMin.Contains(kw)) ||
                (r.CrMax != null && r.CrMax.Contains(kw)) ||
                (r.MoMin != null && r.MoMin.Contains(kw)) ||
                (r.MoMax != null && r.MoMax.Contains(kw)) ||
                (r.CuMin != null && r.CuMin.Contains(kw)) ||
                (r.CuMax != null && r.CuMax.Contains(kw)) ||
                (r.NMin != null && r.NMin.Contains(kw)) ||
                (r.NMax != null && r.NMax.Contains(kw)) ||
                (r.NbMin != null && r.NbMin.Contains(kw)) ||
                (r.NbMax != null && r.NbMax.Contains(kw)) ||
                (r.TiMin != null && r.TiMin.Contains(kw)) ||
                (r.TiMax != null && r.TiMax.Contains(kw)) ||
                (r.FeMin != null && r.FeMin.Contains(kw)) ||
                (r.FeMax != null && r.FeMax.Contains(kw)) ||
                (r.AlMin != null && r.AlMin.Contains(kw)) ||
                (r.AlMax != null && r.AlMax.Contains(kw)) ||
                (r.WMin != null && r.WMin.Contains(kw)) ||
                (r.WMax != null && r.WMax.Contains(kw)) ||
                (r.PRENMin != null && r.PRENMin.Contains(kw)));
        }

        queryable = queryable.ApplyFilters(query.Filters);
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
        entity.CMin = request.CMin ?? entity.CMin; entity.CMax = request.CMax ?? entity.CMax;
        entity.SiMin = request.SiMin ?? entity.SiMin; entity.SiMax = request.SiMax ?? entity.SiMax;
        entity.MnMin = request.MnMin ?? entity.MnMin; entity.MnMax = request.MnMax ?? entity.MnMax;
        entity.PMin = request.PMin ?? entity.PMin; entity.PMax = request.PMax ?? entity.PMax;
        entity.SMin = request.SMin ?? entity.SMin; entity.SMax = request.SMax ?? entity.SMax;
        entity.NiMin = request.NiMin ?? entity.NiMin; entity.NiMax = request.NiMax ?? entity.NiMax;
        entity.CrMin = request.CrMin ?? entity.CrMin; entity.CrMax = request.CrMax ?? entity.CrMax;
        entity.MoMin = request.MoMin ?? entity.MoMin; entity.MoMax = request.MoMax ?? entity.MoMax;
        entity.CuMin = request.CuMin ?? entity.CuMin; entity.CuMax = request.CuMax ?? entity.CuMax;
        entity.NMin = request.NMin ?? entity.NMin; entity.NMax = request.NMax ?? entity.NMax;
        entity.NbMin = request.NbMin ?? entity.NbMin; entity.NbMax = request.NbMax ?? entity.NbMax;
        entity.TiMin = request.TiMin ?? entity.TiMin; entity.TiMax = request.TiMax ?? entity.TiMax;
        entity.FeMin = request.FeMin ?? entity.FeMin; entity.FeMax = request.FeMax ?? entity.FeMax;
        entity.AlMin = request.AlMin ?? entity.AlMin; entity.AlMax = request.AlMax ?? entity.AlMax;
        entity.WMin = request.WMin ?? entity.WMin; entity.WMax = request.WMax ?? entity.WMax;
        entity.PRENMin = request.PRENMin ?? entity.PRENMin;

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

    public async Task<ChemicalValidationRuleDto?> GetByIdAsync(int id)
    {
        return await _context.ChemicalValidationRules
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => ToDto(r))
            .FirstOrDefaultAsync();
    }

    public async Task<List<ChemicalValidationRuleDto>> GetAllListAsync()
    {
        return await _context.ChemicalValidationRules
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Select(x => new ChemicalValidationRuleDto
            {
                Id = x.Id,
                PlantGrade = x.PlantGrade,
                CMin = x.CMin, CMax = x.CMax,
                SiMin = x.SiMin, SiMax = x.SiMax,
                MnMin = x.MnMin, MnMax = x.MnMax,
                PMin = x.PMin, PMax = x.PMax,
                SMin = x.SMin, SMax = x.SMax,
                NiMin = x.NiMin, NiMax = x.NiMax,
                CrMin = x.CrMin, CrMax = x.CrMax,
                MoMin = x.MoMin, MoMax = x.MoMax,
                CuMin = x.CuMin, CuMax = x.CuMax,
                NMin = x.NMin, NMax = x.NMax,
                NbMin = x.NbMin, NbMax = x.NbMax,
                TiMin = x.TiMin, TiMax = x.TiMax,
                FeMin = x.FeMin, FeMax = x.FeMax,
                AlMin = x.AlMin, AlMax = x.AlMax,
                WMin = x.WMin, WMax = x.WMax,
                PRENMin = x.PRENMin,
                CreatedTime = x.CreatedTime,
                UpdatedTime = x.UpdatedTime
            })
            .ToListAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var all = await _context.ChemicalValidationRules
            .AsNoTracking()
            .Select(r => new
            {
                r.PlantGrade,
                r.CMin, r.CMax,
                r.SiMin, r.SiMax,
                r.MnMin, r.MnMax,
                r.PMin, r.PMax,
                r.SMin, r.SMax,
                r.NiMin, r.NiMax,
                r.CrMin, r.CrMax,
                r.MoMin, r.MoMax,
                r.CuMin, r.CuMax,
                r.NMin, r.NMax,
                r.NbMin, r.NbMax,
                r.TiMin, r.TiMax,
                r.FeMin, r.FeMax,
                r.AlMin, r.AlMax,
                r.WMin, r.WMax,
                r.PRENMin
            })
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["PlantGrade"] = all.Select(x => x.PlantGrade).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
            ["CMin"] = all.Select(x => x.CMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["CMax"] = all.Select(x => x.CMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["SiMin"] = all.Select(x => x.SiMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["SiMax"] = all.Select(x => x.SiMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["MnMin"] = all.Select(x => x.MnMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["MnMax"] = all.Select(x => x.MnMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["PMin"] = all.Select(x => x.PMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["PMax"] = all.Select(x => x.PMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["SMin"] = all.Select(x => x.SMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["SMax"] = all.Select(x => x.SMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["NiMin"] = all.Select(x => x.NiMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["NiMax"] = all.Select(x => x.NiMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["CrMin"] = all.Select(x => x.CrMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["CrMax"] = all.Select(x => x.CrMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["MoMin"] = all.Select(x => x.MoMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["MoMax"] = all.Select(x => x.MoMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["CuMin"] = all.Select(x => x.CuMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["CuMax"] = all.Select(x => x.CuMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["NMin"] = all.Select(x => x.NMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["NMax"] = all.Select(x => x.NMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["NbMin"] = all.Select(x => x.NbMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["NbMax"] = all.Select(x => x.NbMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["TiMin"] = all.Select(x => x.TiMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["TiMax"] = all.Select(x => x.TiMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["FeMin"] = all.Select(x => x.FeMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["FeMax"] = all.Select(x => x.FeMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["AlMin"] = all.Select(x => x.AlMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["AlMax"] = all.Select(x => x.AlMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["WMin"] = all.Select(x => x.WMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["WMax"] = all.Select(x => x.WMax ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["PRENMin"] = all.Select(x => x.PRENMin ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList()
        };
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

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var result = await GetAllAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return ChemicalValidationRulePrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? null : sortBy,
            IsDescending = isDescending
        };
        var result = await GetAllAsync(query);
        return ChemicalValidationRulePrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    private static IQueryable<ChemicalValidationRule> ApplySorting(IQueryable<ChemicalValidationRule> queryable, string sortBy, bool isDescending)
    {
        return queryable.ApplySort(sortBy, isDescending);
    }
}
