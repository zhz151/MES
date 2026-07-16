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

using StdReg = MES.Data.Entities.StandardRegister.StandardRegister;

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

        q = q.ApplyFilters(query.Filters);

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

    public async Task<int> SaveAsync(StandardRegisterDto dto)
    {
        if (dto.Id > 0)
        {
            var entity = await _context.StandardRegisters.FindAsync(dto.Id);
            if (entity == null) return 0;

            entity.StandardNo = dto.StandardNo;
            entity.StandardName = dto.StandardName;
            entity.RefSpecification = dto.RefSpecification;
            entity.StandardLevel = dto.StandardLevel;
            entity.ManufactureMethod = dto.ManufactureMethod;
            entity.SteelType = dto.SteelType;
            entity.Remark = dto.Remark;

            await _context.SaveChangesAsync();
            return entity.Id;
        }
        else
        {
            var entity = new StdReg
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
            await _context.SaveChangesAsync();
            return entity.Id;
        }
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

    /// <summary>
    /// 根据标准号解析标准名称（含容错匹配），用于质保书新建页面前端自动填充
    /// </summary>
    public async Task<string?> ResolveNameAsync(string standardNo)
    {
        if (string.IsNullOrWhiteSpace(standardNo)) return null;

        var std = await _context.StandardRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StandardNo == standardNo);
        if (std != null) return std.StandardName;

        // 容错1：去掉年份后缀
        var withoutYear = System.Text.RegularExpressions.Regex.Replace(standardNo, @"-\d{4}$", "");
        if (withoutYear != standardNo)
        {
            std = await _context.StandardRegisters
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StandardNo == withoutYear);
            if (std != null) return std.StandardName;
        }

        // 容错2：去空白（原始输入去空白 — 键值与输入仅空格不同时）
        var origNoSpace = standardNo.Replace(" ", "").Replace("\t", "");
        std = await _context.StandardRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StandardNo!.Replace(" ", "").Replace("\t", "") == origNoSpace);
        if (std != null) return std.StandardName;

        // 容错3：去年份再去空白（键无年份但输入有年份且含空格时）
        var noSpace = withoutYear.Replace(" ", "").Replace("\t", "");
        if (noSpace != withoutYear && noSpace != origNoSpace)
        {
            std = await _context.StandardRegisters
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StandardNo!.Replace(" ", "").Replace("\t", "") == noSpace);
            if (std != null) return std.StandardName;
        }
        return null;
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var all = await GetAllAsync();
        var selected = all.Where(i => ids.Contains(i.Id)).ToList();
        return StandardRegisterPrintHelper.GenerateBatchPdf(selected, columns);
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
        return StandardRegisterPrintHelper.GenerateBatchPdf(result.Items, columns);
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

    private static IQueryable<StdReg> ApplySorting(IQueryable<StdReg> query, string sortField, bool isDescending)
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

    public async Task<int> SaveItemAsync(StandardRegisterItemDto dto)
    {
        if (dto.Id > 0)
        {
            var entity = await _context.StandardRegisterItems.FindAsync(dto.Id);
            if (entity == null) return 0;

            entity.SeqNo = dto.SeqNo;
            entity.InspectionCategory = dto.InspectionCategory;
            entity.InspectionItem = dto.InspectionItem;
            entity.IsMandatory = dto.IsMandatory;
            entity.SamplingRequirement = dto.SamplingRequirement;
            entity.ApplicableRange = dto.ApplicableRange;
            entity.RefStandard = dto.RefStandard;
            entity.DetailRequirement = dto.DetailRequirement;

            await _context.SaveChangesAsync();
            return entity.Id;
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
            await _context.SaveChangesAsync();
            return entity.Id;
        }
    }

    public async Task<bool> DeleteItemAsync(int id)
    {
        var entity = await _context.StandardRegisterItems.FindAsync(id);
        if (entity == null) return false;

        _context.StandardRegisterItems.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 清理孤儿子项（StandardRegisterId=0 或无对应主表记录）以及序号重复的冗余项
    /// </summary>
    public async Task<int> CleanupOrphanedItemsAsync()
    {
        var removedCount = 0;

        // 1. 删除 StandardRegisterId=0 的孤儿子项
        var orphanedByIdZero = await _context.StandardRegisterItems
            .Where(i => i.StandardRegisterId == 0)
            .ToListAsync();
        removedCount += orphanedByIdZero.Count;
        _context.StandardRegisterItems.RemoveRange(orphanedByIdZero);

        // 2. 删除 StandardRegisterId 在标准号主表中不存在的孤儿子项
        var validIds = await _context.StandardRegisters.Select(r => r.Id).ToListAsync();
        var orphanedByMissingRef = await _context.StandardRegisterItems
            .Where(i => i.StandardRegisterId != 0 && !validIds.Contains(i.StandardRegisterId))
            .ToListAsync();
        removedCount += orphanedByMissingRef.Count;
        _context.StandardRegisterItems.RemoveRange(orphanedByMissingRef);

        // 3. 删除同 StandardRegisterId + 同 SeqNo 的重复项（保留 Id 最小的）
        var allItems = await _context.StandardRegisterItems
            .OrderBy(i => i.Id)
            .ToListAsync();
        var seen = new HashSet<string>();
        var dups = new List<StandardRegisterItem>();
        foreach (var item in allItems)
        {
            var key = $"{item.StandardRegisterId}_{item.SeqNo}";
            if (!seen.Add(key))
                dups.Add(item);
        }
        removedCount += dups.Count;
        _context.StandardRegisterItems.RemoveRange(dups);

        await _context.SaveChangesAsync();
        return removedCount;
    }
}
