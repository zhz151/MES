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
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Quality;

/// <summary>
/// 来料炉号登记服务实现
/// </summary>
public class FurnaceRegistrationService : IFurnaceRegistrationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FurnaceRegistrationService> _logger;
    private readonly IChemicalValidationRuleService _chemicalValidationRuleService;
    private readonly IMemoryCache _cache;

    public FurnaceRegistrationService(
        AppDbContext context,
        ILogger<FurnaceRegistrationService> logger,
        IChemicalValidationRuleService chemicalValidationRuleService,
        IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _chemicalValidationRuleService = chemicalValidationRuleService;
        _cache = cache;
    }

    public async Task<FurnaceRegistrationDto?> GetByIdAsync(int id)
    {
        return await _context.FurnaceRegistrations
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new FurnaceRegistrationDto
            {
                Id = r.Id,
                IncomingDate = r.IncomingDate,
                RawMaterialUnit = r.RawMaterialUnit,
                RawMaterialType = r.RawMaterialType,
                RegisteredGrade = r.RegisteredGrade,
                RelatedPlantGrade = r.RelatedPlantGrade,
                FurnaceNumber = r.FurnaceNumber,
                Specification = r.Specification,
                Quantity = r.Quantity,
                Weight = r.Weight,
                Carbon = r.Carbon,
                Silicon = r.Silicon,
                Manganese = r.Manganese,
                Phosphorus = r.Phosphorus,
                Sulfur = r.Sulfur,
                Nickel = r.Nickel,
                Chromium = r.Chromium,
                Molybdenum = r.Molybdenum,
                Copper = r.Copper,
                Nitrogen = r.Nitrogen,
                Niobium = r.Niobium,
                Titanium = r.Titanium,
                Iron = r.Iron,
                Aluminum = r.Aluminum,
                Tungsten = r.Tungsten,
                PREN = r.PREN,
                Remark = r.Remark,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<FurnaceRegistrationDto>> GetAllListAsync()
    {
        return await _context.FurnaceRegistrations
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Select(x => new FurnaceRegistrationDto
            {
                Id = x.Id,
                IncomingDate = x.IncomingDate,
                RawMaterialUnit = x.RawMaterialUnit,
                RawMaterialType = x.RawMaterialType,
                RegisteredGrade = x.RegisteredGrade,
                RelatedPlantGrade = x.RelatedPlantGrade,
                FurnaceNumber = x.FurnaceNumber,
                Specification = x.Specification,
                Quantity = x.Quantity,
                Weight = x.Weight,
                Carbon = x.Carbon, Silicon = x.Silicon, Manganese = x.Manganese, Phosphorus = x.Phosphorus, Sulfur = x.Sulfur,
                Nickel = x.Nickel, Chromium = x.Chromium, Molybdenum = x.Molybdenum, Copper = x.Copper,
                Nitrogen = x.Nitrogen, Niobium = x.Niobium, Titanium = x.Titanium, Iron = x.Iron,
                Aluminum = x.Aluminum, Tungsten = x.Tungsten, PREN = x.PREN,
                Remark = x.Remark,
                CreatedTime = x.CreatedTime,
                UpdatedTime = x.UpdatedTime
            })
            .ToListAsync();
    }

    public async Task<PagedResult<FurnaceRegistrationDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.FurnaceRegistrations
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                r.FurnaceNumber.Contains(kw) ||
                r.RawMaterialUnit.Contains(kw) ||
                r.RegisteredGrade.Contains(kw) ||
                (r.RelatedPlantGrade != null && r.RelatedPlantGrade.Contains(kw)) ||
                r.RawMaterialType.Contains(kw) ||
                (r.Specification != null && r.Specification.Contains(kw)) ||
                (r.Remark != null && r.Remark.Contains(kw)));
        }

        queryable = queryable.ApplyFilters(query.Filters);
        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.SortBy ?? "furnacenumber", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new FurnaceRegistrationDto
            {
                Id = r.Id,
                IncomingDate = r.IncomingDate,
                RawMaterialUnit = r.RawMaterialUnit,
                RawMaterialType = r.RawMaterialType,
                RegisteredGrade = r.RegisteredGrade,
                RelatedPlantGrade = r.RelatedPlantGrade,
                FurnaceNumber = r.FurnaceNumber,
                Specification = r.Specification,
                Quantity = r.Quantity,
                Weight = r.Weight,
                Carbon = r.Carbon,
                Silicon = r.Silicon,
                Manganese = r.Manganese,
                Phosphorus = r.Phosphorus,
                Sulfur = r.Sulfur,
                Nickel = r.Nickel,
                Chromium = r.Chromium,
                Molybdenum = r.Molybdenum,
                Copper = r.Copper,
                Nitrogen = r.Nitrogen,
                Niobium = r.Niobium,
                Titanium = r.Titanium,
                Iron = r.Iron,
                Aluminum = r.Aluminum,
                Tungsten = r.Tungsten,
                PREN = r.PREN,
                Remark = r.Remark,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            })
            .ToListAsync();

        return new PagedResult<FurnaceRegistrationDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<FurnaceRegistrationDto>> BatchCreateAsync(List<CreateFurnaceRegistrationRequest> requests)
    {
        if (requests.Count == 0)
            return new List<FurnaceRegistrationDto>();

        // 化学成分验证
        var errors = new List<string>();
        for (int i = 0; i < requests.Count; i++)
        {
            var rowErrors = await ValidateChemicalCompositionAsync(requests[i], i + 1);
            errors.AddRange(rowErrors);
        }
        if (errors.Any())
            throw new BusinessException(string.Join("；", errors));

        var entities = requests.Select(r => new FurnaceRegistration
        {
            IncomingDate = r.IncomingDate,
            RawMaterialUnit = r.RawMaterialUnit,
            RawMaterialType = r.RawMaterialType,
            RegisteredGrade = r.RegisteredGrade,
            RelatedPlantGrade = r.RelatedPlantGrade,
            FurnaceNumber = r.FurnaceNumber,
            Specification = r.Specification,
            Quantity = r.Quantity,
            Weight = r.Weight,
            Carbon = r.Carbon,
            Silicon = r.Silicon,
            Manganese = r.Manganese,
            Phosphorus = r.Phosphorus,
            Sulfur = r.Sulfur,
            Nickel = r.Nickel,
            Chromium = r.Chromium,
            Molybdenum = r.Molybdenum,
            Copper = r.Copper,
            Nitrogen = r.Nitrogen,
            Niobium = r.Niobium,
            Titanium = r.Titanium,
            Iron = r.Iron,
            Aluminum = r.Aluminum,
            Tungsten = r.Tungsten,
            PREN = r.PREN,
            Remark = r.Remark
        }).ToList();

        _context.FurnaceRegistrations.AddRange(entities);
        await _context.SaveChangesAsync();

        return entities.Select(e => new FurnaceRegistrationDto
        {
            Id = e.Id,
            IncomingDate = e.IncomingDate,
            RawMaterialUnit = e.RawMaterialUnit,
            RawMaterialType = e.RawMaterialType,
            RegisteredGrade = e.RegisteredGrade,
            RelatedPlantGrade = e.RelatedPlantGrade,
            FurnaceNumber = e.FurnaceNumber,
            Specification = e.Specification,
            Quantity = e.Quantity,
            Weight = e.Weight,
            Carbon = e.Carbon,
            Silicon = e.Silicon,
            Manganese = e.Manganese,
            Phosphorus = e.Phosphorus,
            Sulfur = e.Sulfur,
            Nickel = e.Nickel,
            Chromium = e.Chromium,
            Molybdenum = e.Molybdenum,
            Copper = e.Copper,
            Nitrogen = e.Nitrogen,
            Niobium = e.Niobium,
            Titanium = e.Titanium,
            Iron = e.Iron,
            Aluminum = e.Aluminum,
            Tungsten = e.Tungsten,
            PREN = e.PREN,
            Remark = e.Remark,
            CreatedTime = e.CreatedTime,
            UpdatedTime = e.UpdatedTime
        }).ToList();
    }

    public async Task<FurnaceRegistrationDto> UpdateAsync(int id, UpdateFurnaceRegistrationRequest request)
    {
        var entity = await _context.FurnaceRegistrations.FindAsync(id)
            ?? throw new BusinessException($"来料炉号登记记录不存在(Id={id})");

        // 化学成分验证
        var singleRequest = new CreateFurnaceRegistrationRequest
        {
            IncomingDate = request.IncomingDate,
            RawMaterialUnit = request.RawMaterialUnit,
            RawMaterialType = request.RawMaterialType,
            RegisteredGrade = request.RegisteredGrade,
            RelatedPlantGrade = request.RelatedPlantGrade,
            FurnaceNumber = request.FurnaceNumber,
            Specification = request.Specification,
            Quantity = request.Quantity,
            Weight = request.Weight,
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
            Tungsten = request.Tungsten,
            PREN = request.PREN,
            Remark = request.Remark,
        };
        var errors = await ValidateChemicalCompositionAsync(singleRequest, 1);
        if (errors.Any())
            throw new BusinessException(string.Join("；", errors));

        entity.IncomingDate = request.IncomingDate;
        entity.RawMaterialUnit = request.RawMaterialUnit;
        entity.RawMaterialType = request.RawMaterialType;
        entity.RegisteredGrade = request.RegisteredGrade;
        entity.RelatedPlantGrade = request.RelatedPlantGrade ?? entity.RelatedPlantGrade;
        entity.FurnaceNumber = request.FurnaceNumber;
        entity.Specification = request.Specification ?? entity.Specification;
        entity.Quantity = request.Quantity ?? entity.Quantity;
        entity.Weight = request.Weight ?? entity.Weight;
        entity.Carbon = request.Carbon ?? entity.Carbon;
        entity.Silicon = request.Silicon ?? entity.Silicon;
        entity.Manganese = request.Manganese ?? entity.Manganese;
        entity.Phosphorus = request.Phosphorus ?? entity.Phosphorus;
        entity.Sulfur = request.Sulfur ?? entity.Sulfur;
        entity.Nickel = request.Nickel ?? entity.Nickel;
        entity.Chromium = request.Chromium ?? entity.Chromium;
        entity.Molybdenum = request.Molybdenum ?? entity.Molybdenum;
        entity.Copper = request.Copper ?? entity.Copper;
        entity.Nitrogen = request.Nitrogen ?? entity.Nitrogen;
        entity.Niobium = request.Niobium ?? entity.Niobium;
        entity.Titanium = request.Titanium ?? entity.Titanium;
        entity.Iron = request.Iron ?? entity.Iron;
        entity.Aluminum = request.Aluminum ?? entity.Aluminum;
        entity.Tungsten = request.Tungsten ?? entity.Tungsten;
        // 使用验证阶段自动计算的 PREN 值（Cr + 3.3*Mo + 16*N）
        entity.PREN = singleRequest.PREN ?? entity.PREN;
        entity.Remark = request.Remark ?? entity.Remark;

        await _context.SaveChangesAsync();

        return new FurnaceRegistrationDto
        {
            Id = entity.Id,
            IncomingDate = entity.IncomingDate,
            RawMaterialUnit = entity.RawMaterialUnit,
            RawMaterialType = entity.RawMaterialType,
            RegisteredGrade = entity.RegisteredGrade,
            RelatedPlantGrade = entity.RelatedPlantGrade,
            FurnaceNumber = entity.FurnaceNumber,
            Specification = entity.Specification,
            Quantity = entity.Quantity,
            Weight = entity.Weight,
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
            Tungsten = entity.Tungsten,
            PREN = entity.PREN,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.FurnaceRegistrations.FindAsync(id)
            ?? throw new BusinessException($"来料炉号登记记录不存在(Id={id})");

        _context.FurnaceRegistrations.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("FurnaceRegistrationService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

        var all = await _context.FurnaceRegistrations
            .AsNoTracking()
            .Select(r => new
            {
                r.RawMaterialUnit,
                r.RawMaterialType,
                r.RegisteredGrade,
                r.RelatedPlantGrade,
                r.FurnaceNumber,
                r.Specification,
                r.IncomingDate,
                r.Remark
            })
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["RawMaterialUnit"] = all.Select(x => x.RawMaterialUnit).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
            ["RawMaterialType"] = all.Select(x => x.RawMaterialType).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
            ["RegisteredGrade"] = all.Select(x => x.RegisteredGrade).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
            ["RelatedPlantGrade"] = all.Select(x => x.RelatedPlantGrade ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["FurnaceNumber"] = all.Select(x => x.FurnaceNumber).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
            ["Specification"] = all.Select(x => x.Specification ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["IncomingDate"] = all.Select(x => x.IncomingDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList(),
            ["Remark"] = all.Select(x => x.Remark ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList()
        };

        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<string?> LookupPlantGradeAsync(string registeredGrade)
    {
        if (string.IsNullOrWhiteSpace(registeredGrade))
            return null;

        var mapping = await _context.StandardGradeMappings
            .AsNoTracking()
            .Where(m => m.StandardGrade == registeredGrade)
            .Select(m => m.PlantGrade)
            .FirstOrDefaultAsync();

        return mapping;
    }

    /// <summary>
    /// 验证单行化学成分是否满足牌号验证规则
    /// </summary>
    private async Task<List<string>> ValidateChemicalCompositionAsync(CreateFurnaceRegistrationRequest request, int rowIndex)
    {
        var errors = new List<string>();
        var prefix = $"第{rowIndex}行";

        if (string.IsNullOrWhiteSpace(request.RelatedPlantGrade))
            return errors;

        var rule = await _chemicalValidationRuleService.GetByPlantGradeAsync(request.RelatedPlantGrade);

        if (rule == null)
            return errors;

        var carbon = request.Carbon;

        ValidateElement(errors, prefix, "C",  request.Carbon,    rule.CMin,    rule.CMax,    carbon);
        ValidateElement(errors, prefix, "Si", request.Silicon,   rule.SiMin,   rule.SiMax,   carbon);
        ValidateElement(errors, prefix, "Mn", request.Manganese, rule.MnMin,   rule.MnMax,   carbon);
        ValidateElement(errors, prefix, "P",  request.Phosphorus,rule.PMin,    rule.PMax,    carbon);
        ValidateElement(errors, prefix, "S",  request.Sulfur,    rule.SMin,    rule.SMax,    carbon);
        ValidateElement(errors, prefix, "Ni", request.Nickel,    rule.NiMin,   rule.NiMax,   carbon);
        ValidateElement(errors, prefix, "Cr", request.Chromium,  rule.CrMin,   rule.CrMax,   carbon);
        ValidateElement(errors, prefix, "Mo", request.Molybdenum,rule.MoMin,   rule.MoMax,   carbon);
        ValidateElement(errors, prefix, "Cu", request.Copper,    rule.CuMin,   rule.CuMax,   carbon);
        ValidateElement(errors, prefix, "N",  request.Nitrogen,  rule.NMin,    rule.NMax,    carbon);
        ValidateElement(errors, prefix, "Nb", request.Niobium,   rule.NbMin,   rule.NbMax,   carbon);
        ValidateElement(errors, prefix, "Ti", request.Titanium,  rule.TiMin,   rule.TiMax,   carbon);
        ValidateElement(errors, prefix, "Fe", request.Iron,      rule.FeMin,   rule.FeMax,   carbon);
        ValidateElement(errors, prefix, "Al", request.Aluminum,  rule.AlMin,   rule.AlMax,   carbon);
        ValidateElement(errors, prefix, "W",  request.Tungsten,  rule.WMin,    rule.WMax,    carbon);

        // PREN 自动计算与验证：PREN = Cr + 3.3*Mo + 16*N
        var calculatedPren = CalculatePREN(request.Chromium, request.Molybdenum, request.Nitrogen);
        if (calculatedPren.HasValue)
        {
            if (!string.IsNullOrEmpty(rule.PRENMin))
            {
                var prenMin = ParseRuleValue(rule.PRENMin, carbon);
                if (prenMin.HasValue && calculatedPren.Value < prenMin.Value)
                    errors.Add($"{prefix}：PREN腐蚀当量({calculatedPren:G29}) 小于最小允许值({prenMin:G29})，规则要求≥{rule.PRENMin}");
            }
            // 回填计算值到请求对象
            request.PREN = calculatedPren;
        }

        return errors;
    }

    private static void ValidateElement(List<string> errors, string prefix, string name,
        decimal? value, string? ruleMin, string? ruleMax, decimal? carbon)
    {
        if (!value.HasValue) return;

        if (!string.IsNullOrEmpty(ruleMin))
        {
            var minVal = ParseRuleValue(ruleMin, carbon);
            if (minVal.HasValue && value.Value < minVal.Value)
                errors.Add($"{prefix}：{name}({value:G29}) 小于最小允许值({minVal:G29})，规则要求≥{ruleMin}");
        }

        if (!string.IsNullOrEmpty(ruleMax))
        {
            var maxVal = ParseRuleValue(ruleMax, carbon);
            if (maxVal.HasValue && value.Value > maxVal.Value)
                errors.Add($"{prefix}：{name}({value:G29}) 大于最大允许值({maxVal:G29})，规则要求≤{ruleMax}");
        }
    }

    private static decimal? ParseRuleValue(string? raw, decimal? carbon)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(raw, @"^(\d+(?:\.\d+)?)\s*[×x]\s*C%?\s*$");
        if (match.Success && carbon.HasValue)
        {
            if (decimal.TryParse(match.Groups[1].Value, out var coefficient))
                return coefficient * carbon.Value;
            return null;
        }

        if (decimal.TryParse(raw, out var numValue))
            return numValue;

        return null;
    }

    /// <summary>
    /// 计算 PREN 腐蚀当量：PREN = Cr + 3.3*Mo + 16*N
    /// </summary>
    private static decimal? CalculatePREN(decimal? chromium, decimal? molybdenum, decimal? nitrogen)
    {
        if (!chromium.HasValue)
            return null;
        return chromium.Value + 3.3m * (molybdenum ?? 0m) + 16m * (nitrogen ?? 0m);
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var result = await GetAllAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return FurnaceRegistrationPrintHelper.GenerateBatchPdf(selected, columns);
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
        return FurnaceRegistrationPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    private static IQueryable<FurnaceRegistration> ApplySorting(IQueryable<FurnaceRegistration> queryable, string sortBy, bool isDescending)
    {
        return queryable.ApplySort(sortBy, isDescending);
    }
}
