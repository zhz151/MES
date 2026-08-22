using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Quality;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Quality;

public class GrainSizeTestService : IGrainSizeTestService
{
    private readonly AppDbContext _context;
    private readonly ILogger<GrainSizeTestService> _logger;
    private readonly IMemoryCache _cache;

    public GrainSizeTestService(AppDbContext context, ILogger<GrainSizeTestService> logger, IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
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
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
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
        return await _cache.GetOrCreateAsync("GrainSizeTestService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

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
        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var result = await GetAllAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return GrainSizeTestPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? null! : sortBy,
            IsDescending = isDescending,
            InspectionDateFrom = inspectionDateFrom,
            InspectionDateTo = inspectionDateTo
        };
        var result = await GetAllAsync(query);
        return GrainSizeTestPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    private static IQueryable<GrainSizeTest> ApplySorting(IQueryable<GrainSizeTest> queryable, string sortBy, bool isDescending)
        => queryable.ApplySort(sortBy, isDescending);

    private static GrainSizeTestDto MapToDto(GrainSizeTest e) => new()
    {
        Id = e.Id,
        InspectionDate = e.InspectionDate,
        Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo,
        Grade = e.Grade,
        Specification = e.Specification,
        SampleNo = e.SampleNo,
        SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard,
        GrainSizeGrade = e.GrainSizeGrade,
        GrainSizeMethod = e.GrainSizeMethod,
        Magnification = e.Magnification,
        Judgment = e.Judgment,
        CreatedTime = e.CreatedTime,
        UpdatedTime = e.UpdatedTime
    };

    private static System.Linq.Expressions.Expression<Func<GrainSizeTest, GrainSizeTestDto>> MapToDto() => e => new GrainSizeTestDto
    {
        Id = e.Id,
        InspectionDate = e.InspectionDate,
        Inspector = e.Inspector,
        FurnaceNo = e.FurnaceNo,
        Grade = e.Grade,
        Specification = e.Specification,
        SampleNo = e.SampleNo,
        SampleSize = e.SampleSize,
        InspectionStandard = e.InspectionStandard,
        GrainSizeGrade = e.GrainSizeGrade,
        GrainSizeMethod = e.GrainSizeMethod,
        Magnification = e.Magnification,
        Judgment = e.Judgment,
        CreatedTime = e.CreatedTime,
        UpdatedTime = e.UpdatedTime
    };

    private static GrainSizeTest MapToEntity(CreateGrainSizeTestRequest r) => new()
    {
        InspectionDate = r.InspectionDate,
        Inspector = r.Inspector,
        FurnaceNo = r.FurnaceNo,
        Grade = r.Grade,
        Specification = r.Specification,
        SampleNo = r.SampleNo,
        SampleSize = r.SampleSize,
        InspectionStandard = r.InspectionStandard,
        GrainSizeGrade = r.GrainSizeGrade,
        GrainSizeMethod = r.GrainSizeMethod,
        Magnification = r.Magnification,
        Judgment = r.Judgment
    };
}
