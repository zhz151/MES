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
/// 来料炉号登记服务实现
/// </summary>
public class FurnaceRegistrationService : IFurnaceRegistrationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FurnaceRegistrationService> _logger;

    public FurnaceRegistrationService(
        AppDbContext context,
        ILogger<FurnaceRegistrationService> logger)
    {
        _context = context;
        _logger = logger;
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
                (r.RelatedPlantGrade != null && r.RelatedPlantGrade.Contains(kw)));
        }

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
        entity.RelatedPlantGrade = request.RelatedPlantGrade;
        entity.FurnaceNumber = request.FurnaceNumber;
        entity.Specification = request.Specification;
        entity.Quantity = request.Quantity;
        entity.Weight = request.Weight;
        entity.Carbon = request.Carbon;
        entity.Silicon = request.Silicon;
        entity.Manganese = request.Manganese;
        entity.Phosphorus = request.Phosphorus;
        entity.Sulfur = request.Sulfur;
        entity.Nickel = request.Nickel;
        entity.Chromium = request.Chromium;
        entity.Molybdenum = request.Molybdenum;
        entity.Copper = request.Copper;
        entity.Nitrogen = request.Nitrogen;
        entity.Niobium = request.Niobium;
        entity.Titanium = request.Titanium;
        entity.Iron = request.Iron;
        entity.Aluminum = request.Aluminum;
        entity.Tungsten = request.Tungsten;
        // 使用验证阶段自动计算的 PREN 值（Cr + 3.3*Mo + 16*N）
        entity.PREN = singleRequest.PREN;
        entity.Remark = request.Remark;

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

        var rule = await _context.ChemicalValidationRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.PlantGrade == request.RelatedPlantGrade);

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

    private static IQueryable<FurnaceRegistration> ApplySorting(IQueryable<FurnaceRegistration> queryable, string sortBy, bool isDescending)
    {
        return (sortBy.ToLower(), isDescending) switch
        {
            ("furnacenumber", false) => queryable.OrderBy(r => r.FurnaceNumber),
            ("furnacenumber", true) => queryable.OrderByDescending(r => r.FurnaceNumber),
            ("incomingdate", false) => queryable.OrderBy(r => r.IncomingDate),
            ("incomingdate", true) => queryable.OrderByDescending(r => r.IncomingDate),
            ("rawmaterialunit", false) => queryable.OrderBy(r => r.RawMaterialUnit),
            ("rawmaterialunit", true) => queryable.OrderByDescending(r => r.RawMaterialUnit),
            ("registeredgrade", false) => queryable.OrderBy(r => r.RegisteredGrade),
            ("registeredgrade", true) => queryable.OrderByDescending(r => r.RegisteredGrade),
            ("rawmaterialtype", false) => queryable.OrderBy(r => r.RawMaterialType),
            ("rawmaterialtype", true) => queryable.OrderByDescending(r => r.RawMaterialType),
            ("relatedplantgrade", false) => queryable.OrderBy(r => r.RelatedPlantGrade ?? ""),
            ("relatedplantgrade", true) => queryable.OrderByDescending(r => r.RelatedPlantGrade ?? ""),
            ("specification", false) => queryable.OrderBy(r => r.Specification ?? ""),
            ("specification", true) => queryable.OrderByDescending(r => r.Specification ?? ""),
            ("quantity", false) => queryable.OrderBy(r => r.Quantity ?? 0),
            ("quantity", true) => queryable.OrderByDescending(r => r.Quantity ?? 0),
            ("weight", false) => queryable.OrderBy(r => r.Weight ?? 0),
            ("weight", true) => queryable.OrderByDescending(r => r.Weight ?? 0),
            ("carbon", false) => queryable.OrderBy(r => r.Carbon ?? 0),
            ("carbon", true) => queryable.OrderByDescending(r => r.Carbon ?? 0),
            ("silicon", false) => queryable.OrderBy(r => r.Silicon ?? 0),
            ("silicon", true) => queryable.OrderByDescending(r => r.Silicon ?? 0),
            ("manganese", false) => queryable.OrderBy(r => r.Manganese ?? 0),
            ("manganese", true) => queryable.OrderByDescending(r => r.Manganese ?? 0),
            ("phosphorus", false) => queryable.OrderBy(r => r.Phosphorus ?? 0),
            ("phosphorus", true) => queryable.OrderByDescending(r => r.Phosphorus ?? 0),
            ("sulfur", false) => queryable.OrderBy(r => r.Sulfur ?? 0),
            ("sulfur", true) => queryable.OrderByDescending(r => r.Sulfur ?? 0),
            ("nickel", false) => queryable.OrderBy(r => r.Nickel ?? 0),
            ("nickel", true) => queryable.OrderByDescending(r => r.Nickel ?? 0),
            ("chromium", false) => queryable.OrderBy(r => r.Chromium ?? 0),
            ("chromium", true) => queryable.OrderByDescending(r => r.Chromium ?? 0),
            ("molybdenum", false) => queryable.OrderBy(r => r.Molybdenum ?? 0),
            ("molybdenum", true) => queryable.OrderByDescending(r => r.Molybdenum ?? 0),
            ("copper", false) => queryable.OrderBy(r => r.Copper ?? 0),
            ("copper", true) => queryable.OrderByDescending(r => r.Copper ?? 0),
            ("nitrogen", false) => queryable.OrderBy(r => r.Nitrogen ?? 0),
            ("nitrogen", true) => queryable.OrderByDescending(r => r.Nitrogen ?? 0),
            ("niobium", false) => queryable.OrderBy(r => r.Niobium ?? 0),
            ("niobium", true) => queryable.OrderByDescending(r => r.Niobium ?? 0),
            ("titanium", false) => queryable.OrderBy(r => r.Titanium ?? 0),
            ("titanium", true) => queryable.OrderByDescending(r => r.Titanium ?? 0),
            ("iron", false) => queryable.OrderBy(r => r.Iron ?? 0),
            ("iron", true) => queryable.OrderByDescending(r => r.Iron ?? 0),
            ("aluminum", false) => queryable.OrderBy(r => r.Aluminum ?? 0),
            ("aluminum", true) => queryable.OrderByDescending(r => r.Aluminum ?? 0),
            ("tungsten", false) => queryable.OrderBy(r => r.Tungsten ?? 0),
            ("tungsten", true) => queryable.OrderByDescending(r => r.Tungsten ?? 0),
            ("pren", false) => queryable.OrderBy(r => r.PREN ?? 0),
            ("pren", true) => queryable.OrderByDescending(r => r.PREN ?? 0),
            ("remark", false) => queryable.OrderBy(r => r.Remark ?? ""),
            ("remark", true) => queryable.OrderByDescending(r => r.Remark ?? ""),
            ("createdtime", false) => queryable.OrderBy(r => r.CreatedTime),
            ("createdtime", true) => queryable.OrderByDescending(r => r.CreatedTime),
            ("updatedtime", false) => queryable.OrderBy(r => r.UpdatedTime),
            ("updatedtime", true) => queryable.OrderByDescending(r => r.UpdatedTime),
            _ => isDescending
                ? queryable.OrderByDescending(r => r.FurnaceNumber)
                : queryable.OrderBy(r => r.FurnaceNumber)
        };
    }
}
