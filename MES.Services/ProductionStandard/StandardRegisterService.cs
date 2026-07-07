using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services.ProductionStandard;

public class StandardRegisterService : IStandardRegisterService
{
    private readonly AppDbContext _context;

    public StandardRegisterService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<StandardRegisterDto>> GetPagedAsync(QueryParams query)
    {
        var q = _context.StandardRegisters.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = q.Where(e =>
                e.StandardNo.Contains(kw) ||
                e.StandardName.Contains(kw) ||
                (e.RefSpecification != null && e.RefSpecification.Contains(kw)) ||
                (e.Remark != null && e.Remark.Contains(kw)));
        }

        var sortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "StandardNo" : query.SortBy;
        var sortField = sortBy.ToLower() switch
        {
            "standardno" => "StandardNo",
            "standardname" => "StandardName",
            "refspecification" => "RefSpecification",
            "standardlevel" => "StandardLevel",
            "manufacturemethod" => "ManufactureMethod",
            "steeltype" => "SteelType",
            "createdtime" => "CreatedTime",
            _ => "StandardNo"
        };

        q = ApplySorting(q, sortField, query.IsDescending);

        var totalCount = await q.CountAsync();
        var items = await q
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new StandardRegisterDto
            {
                Id = e.Id,
                StandardNo = e.StandardNo,
                StandardName = e.StandardName,
                RefSpecification = e.RefSpecification,
                StandardLevel = e.StandardLevel,
                ManufactureMethod = e.ManufactureMethod,
                SteelType = e.SteelType,
                Remark = e.Remark
            })
            .ToListAsync();

        return new PagedResult<StandardRegisterDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<StandardRegisterDto?> GetByIdAsync(int id)
    {
        return await _context.StandardRegisters.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new StandardRegisterDto
            {
                Id = e.Id,
                StandardNo = e.StandardNo,
                StandardName = e.StandardName,
                RefSpecification = e.RefSpecification,
                StandardLevel = e.StandardLevel,
                ManufactureMethod = e.ManufactureMethod,
                SteelType = e.SteelType,
                Remark = e.Remark
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> SaveAsync(StandardRegisterDto dto)
    {
        if (dto.Id > 0)
        {
            var entity = await _context.StandardRegisters.FindAsync(dto.Id);
            if (entity == null) return false;

            entity.StandardNo = dto.StandardNo;
            entity.StandardName = dto.StandardName;
            entity.RefSpecification = dto.RefSpecification;
            entity.StandardLevel = dto.StandardLevel;
            entity.ManufactureMethod = dto.ManufactureMethod;
            entity.SteelType = dto.SteelType;
            entity.Remark = dto.Remark;
        }
        else
        {
            var entity = new StandardRegister
            {
                StandardNo = dto.StandardNo,
                StandardName = dto.StandardName,
                RefSpecification = dto.RefSpecification,
                StandardLevel = dto.StandardLevel,
                ManufactureMethod = dto.ManufactureMethod,
                SteelType = dto.SteelType,
                Remark = dto.Remark
            };
            _context.StandardRegisters.Add(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.StandardRegisters.FindAsync(id);
        if (entity == null) return false;

        _context.StandardRegisters.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<StandardRegisterDto>> GetAllAsync()
    {
        return await _context.StandardRegisters.AsNoTracking()
            .OrderBy(e => e.StandardNo)
            .Select(e => new StandardRegisterDto
            {
                Id = e.Id,
                StandardNo = e.StandardNo,
                StandardName = e.StandardName,
                RefSpecification = e.RefSpecification,
                StandardLevel = e.StandardLevel,
                ManufactureMethod = e.ManufactureMethod,
                SteelType = e.SteelType,
                Remark = e.Remark
            })
            .ToListAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var dict = new Dictionary<string, List<string>>();

        var levels = await _context.StandardRegisters.AsNoTracking()
            .Where(e => e.StandardLevel != null)
            .Select(e => e.StandardLevel!)
            .Distinct().ToListAsync();
        if (levels.Any()) dict["StandardLevel"] = levels;

        var methods = await _context.StandardRegisters.AsNoTracking()
            .Where(e => e.ManufactureMethod != null)
            .Select(e => e.ManufactureMethod!)
            .Distinct().ToListAsync();
        if (methods.Any()) dict["ManufactureMethod"] = methods;

        var steelTypes = await _context.StandardRegisters.AsNoTracking()
            .Where(e => e.SteelType != null)
            .Select(e => e.SteelType!)
            .Distinct().ToListAsync();
        if (steelTypes.Any()) dict["SteelType"] = steelTypes;

        return dict;
    }

    private static IQueryable<StandardRegister> ApplySorting(IQueryable<StandardRegister> query, string sortField, bool isDescending)
    {
        return (sortField, isDescending) switch
        {
            ("StandardName", false) => query.OrderBy(e => e.StandardName),
            ("StandardName", true) => query.OrderByDescending(e => e.StandardName),
            ("RefSpecification", false) => query.OrderBy(e => e.RefSpecification),
            ("RefSpecification", true) => query.OrderByDescending(e => e.RefSpecification),
            ("StandardLevel", false) => query.OrderBy(e => e.StandardLevel),
            ("StandardLevel", true) => query.OrderByDescending(e => e.StandardLevel),
            ("ManufactureMethod", false) => query.OrderBy(e => e.ManufactureMethod),
            ("ManufactureMethod", true) => query.OrderByDescending(e => e.ManufactureMethod),
            ("SteelType", false) => query.OrderBy(e => e.SteelType),
            ("SteelType", true) => query.OrderByDescending(e => e.SteelType),
            ("CreatedTime", false) => query.OrderBy(e => e.CreatedTime),
            ("CreatedTime", true) => query.OrderByDescending(e => e.CreatedTime),
            _ => query.OrderBy(e => e.StandardNo)
        };
    }

    // ========== 子项目 ==========

    public async Task<List<StandardRegisterItemDto>> GetItemsAsync(int standardRegisterId)
    {
        return await _context.StandardRegisterItems.AsNoTracking()
            .Where(e => e.StandardRegisterId == standardRegisterId)
            .OrderBy(e => e.SeqNo)
            .Select(e => new StandardRegisterItemDto
            {
                Id = e.Id,
                StandardRegisterId = e.StandardRegisterId,
                SeqNo = e.SeqNo,
                InspectionCategory = e.InspectionCategory,
                InspectionItem = e.InspectionItem,
                IsMandatory = e.IsMandatory,
                SamplingRequirement = e.SamplingRequirement,
                ApplicableRange = e.ApplicableRange,
                RefStandard = e.RefStandard,
                DetailRequirement = e.DetailRequirement
            })
            .ToListAsync();
    }

    public async Task<bool> SaveItemAsync(StandardRegisterItemDto dto)
    {
        if (dto.Id > 0)
        {
            var entity = await _context.StandardRegisterItems.FindAsync(dto.Id);
            if (entity == null) return false;

            entity.SeqNo = dto.SeqNo;
            entity.InspectionCategory = dto.InspectionCategory;
            entity.InspectionItem = dto.InspectionItem;
            entity.IsMandatory = dto.IsMandatory;
            entity.SamplingRequirement = dto.SamplingRequirement;
            entity.ApplicableRange = dto.ApplicableRange;
            entity.RefStandard = dto.RefStandard;
            entity.DetailRequirement = dto.DetailRequirement;
        }
        else
        {
            var entity = new StandardRegisterItem
            {
                StandardRegisterId = dto.StandardRegisterId,
                SeqNo = dto.SeqNo,
                InspectionCategory = dto.InspectionCategory,
                InspectionItem = dto.InspectionItem,
                IsMandatory = dto.IsMandatory,
                SamplingRequirement = dto.SamplingRequirement,
                ApplicableRange = dto.ApplicableRange,
                RefStandard = dto.RefStandard,
                DetailRequirement = dto.DetailRequirement
            };
            _context.StandardRegisterItems.Add(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteItemAsync(int id)
    {
        var entity = await _context.StandardRegisterItems.FindAsync(id);
        if (entity == null) return false;

        _context.StandardRegisterItems.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}
