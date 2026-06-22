using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;

namespace MES.Services.ProductionStandard;

public class SubStandardQuickViewService : ISubStandardQuickViewService
{
    private readonly AppDbContext _context;

    public SubStandardQuickViewService(AppDbContext context) => _context = context;

    public async Task<PagedResult<SubStandardQuickViewDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.SubStandardQuickViews
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                queryable = queryable.Where(x =>
                    x.StandardNo.Contains(keyword));
            }
        }

        queryable = queryable.ApplyFilters(query.Filters);
        var sortBy = string.IsNullOrEmpty(query.SortBy) || query.SortBy.Equals("CreatedTime", StringComparison.OrdinalIgnoreCase)
            ? "StandardNo"
            : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(x => ToDto(x))
            .ToListAsync();

        return new PagedResult<SubStandardQuickViewDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<SubStandardQuickViewDto> GetByIdAsync(int id)
    {
        var entity = await _context.SubStandardQuickViews
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            throw new BusinessException("子标准速览记录不存在");
        return ToDto(entity);
    }

    public async Task<SubStandardQuickViewDto> CreateAsync(CreateSubStandardQuickViewRequest request)
    {
        var exists = await _context.SubStandardQuickViews
            .AnyAsync(x => x.StandardNo == request.StandardNo);
        if (exists)
            throw new BusinessException($"标准号 '{request.StandardNo}' 已存在");

        var entity = new SubStandardQuickView
        {
            StandardNo = request.StandardNo,
            ChemicalComposition = request.ChemicalComposition,
            HydrostaticTest = request.HydrostaticTest,
            EddyCurrent = request.EddyCurrent,
            UltrasonicTest = request.UltrasonicTest,
            RadiographicTest = request.RadiographicTest,
            HardnessRockwell = request.HardnessRockwell,
            HardnessBrinell = request.HardnessBrinell,
            HardnessVickers = request.HardnessVickers,
            TensileRoomTemp = request.TensileRoomTemp,
            TensileHighTemp = request.TensileHighTemp,
            WeldJointTensile = request.WeldJointTensile,
            ImpactTest = request.ImpactTest,
            WeldJointImpact = request.WeldJointImpact,
            FlatteningTest = request.FlatteningTest,
            FlaringTest = request.FlaringTest,
            ExpandingTest = request.ExpandingTest,
            BendTest = request.BendTest,
            WeldJointBend = request.WeldJointBend,
            GrainSize = request.GrainSize,
            IntergranularCorrosion = request.IntergranularCorrosion,
            PittingCorrosion = request.PittingCorrosion,
            FerriteContent = request.FerriteContent,
            Macrostructure = request.Macrostructure
        };

        _context.SubStandardQuickViews.Add(entity);
        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<SubStandardQuickViewDto> UpdateAsync(int id, UpdateSubStandardQuickViewRequest request)
    {
        var entity = await _context.SubStandardQuickViews
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            throw new BusinessException("子标准速览记录不存在");

        if (request.StandardNo != entity.StandardNo)
        {
            var exists = await _context.SubStandardQuickViews
                .AnyAsync(x => x.StandardNo == request.StandardNo && x.Id != id);
            if (exists)
                throw new BusinessException($"标准号 '{request.StandardNo}' 已存在");
            entity.StandardNo = request.StandardNo;
        }

        if (request.ChemicalComposition != null) entity.ChemicalComposition = request.ChemicalComposition;
        if (request.HydrostaticTest != null) entity.HydrostaticTest = request.HydrostaticTest;
        if (request.EddyCurrent != null) entity.EddyCurrent = request.EddyCurrent;
        if (request.UltrasonicTest != null) entity.UltrasonicTest = request.UltrasonicTest;
        if (request.RadiographicTest != null) entity.RadiographicTest = request.RadiographicTest;
        if (request.HardnessRockwell != null) entity.HardnessRockwell = request.HardnessRockwell;
        if (request.HardnessBrinell != null) entity.HardnessBrinell = request.HardnessBrinell;
        if (request.HardnessVickers != null) entity.HardnessVickers = request.HardnessVickers;
        if (request.TensileRoomTemp != null) entity.TensileRoomTemp = request.TensileRoomTemp;
        if (request.TensileHighTemp != null) entity.TensileHighTemp = request.TensileHighTemp;
        if (request.WeldJointTensile != null) entity.WeldJointTensile = request.WeldJointTensile;
        if (request.ImpactTest != null) entity.ImpactTest = request.ImpactTest;
        if (request.WeldJointImpact != null) entity.WeldJointImpact = request.WeldJointImpact;
        if (request.FlatteningTest != null) entity.FlatteningTest = request.FlatteningTest;
        if (request.FlaringTest != null) entity.FlaringTest = request.FlaringTest;
        if (request.ExpandingTest != null) entity.ExpandingTest = request.ExpandingTest;
        if (request.BendTest != null) entity.BendTest = request.BendTest;
        if (request.WeldJointBend != null) entity.WeldJointBend = request.WeldJointBend;
        if (request.GrainSize != null) entity.GrainSize = request.GrainSize;
        if (request.IntergranularCorrosion != null) entity.IntergranularCorrosion = request.IntergranularCorrosion;
        if (request.PittingCorrosion != null) entity.PittingCorrosion = request.PittingCorrosion;
        if (request.FerriteContent != null) entity.FerriteContent = request.FerriteContent;
        if (request.Macrostructure != null) entity.Macrostructure = request.Macrostructure;

        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.SubStandardQuickViews
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            throw new BusinessException("子标准速览记录不存在");
        _context.SubStandardQuickViews.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.SubStandardQuickViews.AsNoTracking();
        return new Dictionary<string, List<string>>
        {
            ["StandardNo"] = await query.Select(x => x.StandardNo).Distinct().OrderBy(x => x).ToListAsync(),
        };
    }

    private static SubStandardQuickViewDto ToDto(SubStandardQuickView e) => new()
    {
        Id = e.Id,
        StandardNo = e.StandardNo,
        ChemicalComposition = e.ChemicalComposition,
        HydrostaticTest = e.HydrostaticTest,
        EddyCurrent = e.EddyCurrent,
        UltrasonicTest = e.UltrasonicTest,
        RadiographicTest = e.RadiographicTest,
        HardnessRockwell = e.HardnessRockwell,
        HardnessBrinell = e.HardnessBrinell,
        HardnessVickers = e.HardnessVickers,
        TensileRoomTemp = e.TensileRoomTemp,
        TensileHighTemp = e.TensileHighTemp,
        WeldJointTensile = e.WeldJointTensile,
        ImpactTest = e.ImpactTest,
        WeldJointImpact = e.WeldJointImpact,
        FlatteningTest = e.FlatteningTest,
        FlaringTest = e.FlaringTest,
        ExpandingTest = e.ExpandingTest,
        BendTest = e.BendTest,
        WeldJointBend = e.WeldJointBend,
        GrainSize = e.GrainSize,
        IntergranularCorrosion = e.IntergranularCorrosion,
        PittingCorrosion = e.PittingCorrosion,
        FerriteContent = e.FerriteContent,
        Macrostructure = e.Macrostructure
    };
}
