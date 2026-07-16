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
            .Select(x => new SubStandardQuickViewDto
            {
                Id = x.Id,
                StandardNo = x.StandardNo,
                ChemicalComposition = x.ChemicalComposition,
                HydrostaticTest = x.HydrostaticTest,
                EddyCurrent = x.EddyCurrent,
                UltrasonicTest = x.UltrasonicTest,
                RadiographicTest = x.RadiographicTest,
                HardnessRockwell = x.HardnessRockwell,
                HardnessBrinell = x.HardnessBrinell,
                HardnessVickers = x.HardnessVickers,
                TensileRoomTemp = x.TensileRoomTemp,
                TensileHighTemp = x.TensileHighTemp,
                WeldJointTensile = x.WeldJointTensile,
                ImpactTest = x.ImpactTest,
                WeldJointImpact = x.WeldJointImpact,
                FlatteningTest = x.FlatteningTest,
                FlaringTest = x.FlaringTest,
                ExpandingTest = x.ExpandingTest,
                BendTest = x.BendTest,
                WeldJointBend = x.WeldJointBend,
                GrainSize = x.GrainSize,
                IntergranularCorrosion = x.IntergranularCorrosion,
                PittingCorrosion = x.PittingCorrosion,
                FerriteContent = x.FerriteContent,
                Macrostructure = x.Macrostructure
            })
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

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var result = await GetPagedAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return SubStandardQuickViewPrintHelper.GenerateBatchPdf(selected, columns);
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
        return SubStandardQuickViewPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var all = await _context.SubStandardQuickViews
            .AsNoTracking()
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["StandardNo"] = all.Select(x => x.StandardNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["ChemicalComposition"] = all.Select(x => x.ChemicalComposition).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["HydrostaticTest"] = all.Select(x => x.HydrostaticTest).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["EddyCurrent"] = all.Select(x => x.EddyCurrent).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["UltrasonicTest"] = all.Select(x => x.UltrasonicTest).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["RadiographicTest"] = all.Select(x => x.RadiographicTest).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["HardnessRockwell"] = all.Select(x => x.HardnessRockwell).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["HardnessBrinell"] = all.Select(x => x.HardnessBrinell).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["HardnessVickers"] = all.Select(x => x.HardnessVickers).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["TensileRoomTemp"] = all.Select(x => x.TensileRoomTemp).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["TensileHighTemp"] = all.Select(x => x.TensileHighTemp).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["WeldJointTensile"] = all.Select(x => x.WeldJointTensile).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["ImpactTest"] = all.Select(x => x.ImpactTest).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["WeldJointImpact"] = all.Select(x => x.WeldJointImpact).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["FlatteningTest"] = all.Select(x => x.FlatteningTest).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["FlaringTest"] = all.Select(x => x.FlaringTest).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["ExpandingTest"] = all.Select(x => x.ExpandingTest).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["BendTest"] = all.Select(x => x.BendTest).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["WeldJointBend"] = all.Select(x => x.WeldJointBend).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["GrainSize"] = all.Select(x => x.GrainSize).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["IntergranularCorrosion"] = all.Select(x => x.IntergranularCorrosion).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["PittingCorrosion"] = all.Select(x => x.PittingCorrosion).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["FerriteContent"] = all.Select(x => x.FerriteContent).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Macrostructure"] = all.Select(x => x.Macrostructure).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
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
