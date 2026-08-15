using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Shared;
using MES.Core.Exceptions;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.StandardRegister;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.StandardRegister;

public class FactoryInspectionRequirementService : IFactoryInspectionRequirementService
{
    private readonly AppDbContext _context;

    public FactoryInspectionRequirementService(AppDbContext context) => _context = context;

    public async Task<PagedResult<FactoryInspectionRequirementDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.FactoryInspectionRequirements
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
            .Select(x => new FactoryInspectionRequirementDto
            {
                Id = x.Id,
                StandardNo = x.StandardNo,
                ChemicalComposition = x.ChemicalComposition,
                PmiInspection = x.PmiInspection,
                SurfaceInspection = x.SurfaceInspection,
                Dimension = x.Dimension,
                Endoscopy = x.Endoscopy,
                HydrostaticTest = x.HydrostaticTest,
                UnderwaterPressure = x.UnderwaterPressure,
                EddyCurrent = x.EddyCurrent,
                UltrasonicTest = x.UltrasonicTest,
                PortColoring = x.PortColoring,
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

        return new PagedResult<FactoryInspectionRequirementDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<FactoryInspectionRequirementDto?> GetByIdAsync(int id)
    {
        var entity = await _context.FactoryInspectionRequirements
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            throw new BusinessException("工厂检验项要求记录不存在");
        return ToDto(entity);
    }

    public async Task<FactoryInspectionRequirementDto> CreateAsync(CreateFactoryInspectionRequirementRequest request)
    {
        var exists = await _context.FactoryInspectionRequirements
            .AnyAsync(x => x.StandardNo == request.StandardNo);
        if (exists)
            throw new BusinessException($"标准号 '{request.StandardNo}' 已存在");

        var entity = new FactoryInspectionRequirement
        {
            StandardNo = request.StandardNo,
            ChemicalComposition = request.ChemicalComposition,
            PmiInspection = request.PmiInspection,
            SurfaceInspection = request.SurfaceInspection,
            Dimension = request.Dimension,
            Endoscopy = request.Endoscopy,
            HydrostaticTest = request.HydrostaticTest,
            UnderwaterPressure = request.UnderwaterPressure,
            EddyCurrent = request.EddyCurrent,
            UltrasonicTest = request.UltrasonicTest,
            PortColoring = request.PortColoring,
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

        _context.FactoryInspectionRequirements.Add(entity);
        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<FactoryInspectionRequirementDto> UpdateAsync(int id, UpdateFactoryInspectionRequirementRequest request)
    {
        var entity = await _context.FactoryInspectionRequirements
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            throw new BusinessException("工厂检验项要求记录不存在");

        if (request.StandardNo != entity.StandardNo)
        {
            var exists = await _context.FactoryInspectionRequirements
                .AnyAsync(x => x.StandardNo == request.StandardNo && x.Id != id);
            if (exists)
                throw new BusinessException($"标准号 '{request.StandardNo}' 已存在");
            entity.StandardNo = request.StandardNo;
        }

        if (request.ChemicalComposition != null) entity.ChemicalComposition = request.ChemicalComposition;
        if (request.PmiInspection != null) entity.PmiInspection = request.PmiInspection;
        if (request.SurfaceInspection != null) entity.SurfaceInspection = request.SurfaceInspection;
        if (request.Dimension != null) entity.Dimension = request.Dimension;
        if (request.Endoscopy != null) entity.Endoscopy = request.Endoscopy;
        if (request.HydrostaticTest != null) entity.HydrostaticTest = request.HydrostaticTest;
        if (request.UnderwaterPressure != null) entity.UnderwaterPressure = request.UnderwaterPressure;
        if (request.EddyCurrent != null) entity.EddyCurrent = request.EddyCurrent;
        if (request.UltrasonicTest != null) entity.UltrasonicTest = request.UltrasonicTest;
        if (request.PortColoring != null) entity.PortColoring = request.PortColoring;
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
        var entity = await _context.FactoryInspectionRequirements
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            throw new BusinessException("工厂检验项要求记录不存在");
        _context.FactoryInspectionRequirements.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var result = await GetPagedAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return FactoryInspectionRequirementPrintHelper.GenerateBatchPdf(selected, columns);
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
        return FactoryInspectionRequirementPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var all = await _context.FactoryInspectionRequirements
            .AsNoTracking()
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["StandardNo"] = all.Select(x => x.StandardNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["ChemicalComposition"] = all.Select(x => x.ChemicalComposition).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["PmiInspection"] = all.Select(x => x.PmiInspection).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["SurfaceInspection"] = all.Select(x => x.SurfaceInspection).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Dimension"] = all.Select(x => x.Dimension).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["Endoscopy"] = all.Select(x => x.Endoscopy).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["HydrostaticTest"] = all.Select(x => x.HydrostaticTest).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["UnderwaterPressure"] = all.Select(x => x.UnderwaterPressure).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["EddyCurrent"] = all.Select(x => x.EddyCurrent).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["UltrasonicTest"] = all.Select(x => x.UltrasonicTest).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["PortColoring"] = all.Select(x => x.PortColoring).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
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

    private static FactoryInspectionRequirementDto ToDto(FactoryInspectionRequirement e) => new()
    {
        Id = e.Id,
        StandardNo = e.StandardNo,
        ChemicalComposition = e.ChemicalComposition,
        PmiInspection = e.PmiInspection,
        SurfaceInspection = e.SurfaceInspection,
        Dimension = e.Dimension,
        Endoscopy = e.Endoscopy,
        HydrostaticTest = e.HydrostaticTest,
        UnderwaterPressure = e.UnderwaterPressure,
        EddyCurrent = e.EddyCurrent,
        UltrasonicTest = e.UltrasonicTest,
        PortColoring = e.PortColoring,
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
