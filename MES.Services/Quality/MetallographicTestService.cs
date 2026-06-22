using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;

namespace MES.Services;

public class MetallographicTestService : IMetallographicTestService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MetallographicTestService> _logger;

    public MetallographicTestService(AppDbContext context, ILogger<MetallographicTestService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MetallographicTestDto?> GetByIdAsync(int id)
    {
        return await _context.MetallographicTests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync();
    }

    public async Task<PagedResult<MetallographicTestDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.MetallographicTests
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
                (r.EtchingMethod != null && r.EtchingMethod.Contains(kw)) ||
                (r.Magnification != null && r.Magnification.Contains(kw)) ||
                (r.Judgment != null && r.Judgment.Contains(kw)));
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

        return new PagedResult<MetallographicTestDto>
        {
            Items = items, TotalCount = totalCount,
            PageIndex = query.PageIndex, PageSize = query.PageSize
        };
    }

    public async Task<MetallographicTestDto> CreateAsync(CreateMetallographicTestRequest request)
    {
        var entity = MapToEntity(request);
        _context.MetallographicTests.Add(entity);
        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<MetallographicTestDto> UpdateAsync(int id, UpdateMetallographicTestRequest request)
    {
        var entity = await _context.MetallographicTests.FindAsync(id)
            ?? throw new BusinessException("金相检验记录不存在");

        entity.InspectionDate = request.InspectionDate;
        entity.Inspector = request.Inspector ?? entity.Inspector;
        entity.FurnaceNo = request.FurnaceNo ?? entity.FurnaceNo;
        entity.Grade = request.Grade ?? entity.Grade;
        entity.Specification = request.Specification ?? entity.Specification;
        entity.SampleNo = request.SampleNo ?? entity.SampleNo;
        entity.SampleSize = request.SampleSize ?? entity.SampleSize;
        entity.InspectionStandard = request.InspectionStandard ?? entity.InspectionStandard;
        entity.EtchingMethod = request.EtchingMethod ?? entity.EtchingMethod;
        entity.ElectrolyticVoltage = request.ElectrolyticVoltage ?? entity.ElectrolyticVoltage;
        entity.ElectrolyticTime = request.ElectrolyticTime ?? entity.ElectrolyticTime;
        entity.Magnification = request.Magnification ?? entity.Magnification;
        entity.FerriteContent = request.FerriteContent ?? entity.FerriteContent;
        entity.Judgment = request.Judgment ?? entity.Judgment;

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.MetallographicTests.FindAsync(id)
            ?? throw new BusinessException("金相检验记录不存在");
        _context.MetallographicTests.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<MetallographicTestDto>> BatchCreateAsync(List<CreateMetallographicTestRequest> requests)
    {
        if (requests.Count == 0) return new List<MetallographicTestDto>();
        var entities = requests.Select(MapToEntity).ToList();
        _context.MetallographicTests.AddRange(entities);
        await _context.SaveChangesAsync();
        return entities.Select(e => MapToDto(e)).ToList();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var all = await _context.MetallographicTests
            .AsNoTracking()
            .Select(r => new { r.Inspector, r.FurnaceNo, r.Grade, r.Specification, r.InspectionStandard, r.EtchingMethod, r.Magnification, r.Judgment, r.InspectionDate })
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["Inspector"] = all.Select(x => x.Inspector ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["FurnaceNo"] = all.Select(x => x.FurnaceNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Grade"] = all.Select(x => x.Grade ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Specification"] = all.Select(x => x.Specification ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["InspectionStandard"] = all.Select(x => x.InspectionStandard ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["EtchingMethod"] = all.Select(x => x.EtchingMethod ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Magnification"] = all.Select(x => x.Magnification ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Judgment"] = all.Select(x => x.Judgment ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["InspectionDate"] = all.Select(x => x.InspectionDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList()
        };
    }

    private static IQueryable<MetallographicTest> ApplySorting(IQueryable<MetallographicTest> queryable, string sortBy, bool isDescending)
        => queryable.ApplySort(sortBy, isDescending);

    private static MetallographicTestDto MapToDto(MetallographicTest e) => new()
    {
        Id = e.Id, InspectionDate = e.InspectionDate, Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo, Grade = e.Grade, Specification = e.Specification,
        SampleNo = e.SampleNo, SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard,
        EtchingMethod = e.EtchingMethod, ElectrolyticVoltage = e.ElectrolyticVoltage,
        ElectrolyticTime = e.ElectrolyticTime, Magnification = e.Magnification,
        FerriteContent = e.FerriteContent, Judgment = e.Judgment,
        CreatedTime = e.CreatedTime, UpdatedTime = e.UpdatedTime
    };

    private static System.Linq.Expressions.Expression<Func<MetallographicTest, MetallographicTestDto>> MapToDto() => e => new MetallographicTestDto
    {
        Id = e.Id, InspectionDate = e.InspectionDate, Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo, Grade = e.Grade, Specification = e.Specification,
        SampleNo = e.SampleNo, SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard,
        EtchingMethod = e.EtchingMethod, ElectrolyticVoltage = e.ElectrolyticVoltage,
        ElectrolyticTime = e.ElectrolyticTime, Magnification = e.Magnification,
        FerriteContent = e.FerriteContent, Judgment = e.Judgment,
        CreatedTime = e.CreatedTime, UpdatedTime = e.UpdatedTime
    };

    private static MetallographicTest MapToEntity(CreateMetallographicTestRequest r) => new()
    {
        InspectionDate = r.InspectionDate, Inspector = r.Inspector,
        FurnaceNo = r.FurnaceNo, Grade = r.Grade, Specification = r.Specification,
        SampleNo = r.SampleNo, SampleSize = r.SampleSize,
        InspectionStandard = r.InspectionStandard,
        EtchingMethod = r.EtchingMethod, ElectrolyticVoltage = r.ElectrolyticVoltage,
        ElectrolyticTime = r.ElectrolyticTime, Magnification = r.Magnification,
        FerriteContent = r.FerriteContent, Judgment = r.Judgment
    };
}
