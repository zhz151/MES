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
using MES.Core.Helpers;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Materials;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Materials;

public class MaterialService : IMaterialService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public MaterialService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<MaterialDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.Materials
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(m =>
                m.MaterialCode.Contains(kw) ||
                m.MaterialCategory.Contains(kw) ||
                m.PlantGrade.Contains(kw) ||
                m.Specification.Contains(kw) ||
                (m.Remark != null && m.Remark.Contains(kw)) ||
                (m.CreatedBy != null && m.CreatedBy.Contains(kw)));
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        queryable = queryable.ApplySort(query.SortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(m => new
            {
                m.Id, m.MaterialCode, m.MaterialCategory, m.PlantGrade,
                m.Specification, m.IsActive, m.Remark, m.CreatedTime, m.CreatedBy
            })
            .ToListAsync();

        var dtos = items.Select(m => new MaterialDto
        {
            Id = m.Id,
            MaterialCode = m.MaterialCode,
            MaterialCategory = !string.IsNullOrEmpty(m.MaterialCategory) && Enum.TryParse<MaterialType>(m.MaterialCategory, out var mc) ? mc : default,
            PlantGrade = m.PlantGrade,
            Specification = m.Specification,
            IsActive = m.IsActive,
            Remark = m.Remark,
            CreatedTime = m.CreatedTime,
            CreatedBy = m.CreatedBy
        }).ToList();

        return new PagedResult<MaterialDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<MaterialDto>> GetAllListAsync()
    {
        var materials = await _context.Materials
            .AsNoTracking()
            .OrderBy(m => m.MaterialCode)
            .Select(m => new
            {
                m.Id, m.MaterialCode, m.MaterialCategory, m.PlantGrade,
                m.Specification, m.IsActive, m.Remark, m.CreatedTime, m.CreatedBy
            })
            .ToListAsync();

        return materials.Select(m => new MaterialDto
        {
            Id = m.Id,
            MaterialCode = m.MaterialCode,
            MaterialCategory = !string.IsNullOrEmpty(m.MaterialCategory) && Enum.TryParse<MaterialType>(m.MaterialCategory, out var mc) ? mc : default,
            PlantGrade = m.PlantGrade,
            Specification = m.Specification,
            IsActive = m.IsActive,
            Remark = m.Remark,
            CreatedTime = m.CreatedTime,
            CreatedBy = m.CreatedBy
        }).ToList();
    }

    public async Task<MaterialDto> GetByIdAsync(int id)
    {
        var entity = await _context.Materials
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
        if (entity == null) throw new BusinessException("物料不存在");
        return ToDto(entity);
    }

    public async Task<List<MaterialDto>> GetActiveAsync()
    {
        var items = await _context.Materials
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.MaterialCategory)
            .ThenBy(m => m.PlantGrade)
            .Select(m => ToDto(m))
            .ToListAsync();
        return items;
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        return await _context.Materials
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => m.MaterialCategory)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    public async Task<MaterialDto?> MatchAsync(string category, string grade, string spec)
    {
        var entity = await _context.Materials
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.MaterialCategory == category &&
                m.PlantGrade == grade &&
                m.Specification == spec);
        return entity != null ? ToDto(entity) : null;
    }

    public async Task<List<BatchMaterialMatchItem>> BatchMatchAsync(List<BatchMaterialMatchItem> items)
    {
        if (items == null || items.Count == 0)
            return new List<BatchMaterialMatchItem>();

        // 一次查询所有物料（组合索引 UK_Material_Combo 覆盖，(MaterialCategory, PlantGrade, Specification)）
        var existingCategories = items.Select(i => i.Category).Distinct().ToList();
        var existingGrades = items.Select(i => i.Grade).Distinct().ToList();
        var existingSpecs = items.Select(i => i.Spec).Distinct().ToList();

        var existingMaterials = await _context.Materials
            .AsNoTracking()
            .Where(m => existingCategories.Contains(m.MaterialCategory) &&
                        existingGrades.Contains(m.PlantGrade) &&
                        existingSpecs.Contains(m.Specification))
            .Select(m => new { m.MaterialCategory, m.PlantGrade, m.Specification })
            .ToListAsync();

        var existingSet = existingMaterials
            .Select(m => $"{m.MaterialCategory}|{m.PlantGrade}|{m.Specification}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return items
            .Where(i => !existingSet.Contains($"{i.Category}|{i.Grade}|{i.Spec}"))
            .ToList();
    }

    public async Task<MaterialDto> CreateAsync(CreateMaterialRequest request)
    {
        var exists = await _context.Materials
            .AnyAsync(m =>
                m.MaterialCategory == request.MaterialCategory.ToString() &&
                m.PlantGrade == request.PlantGrade &&
                m.Specification == request.Specification);
        if (exists) throw new BusinessException("该物料组合已存在");

        var materialCode = await CodeGenerator.GenerateNextAsync(
            _context.Materials.Select(m => m.MaterialCode), "MA");

        var entity = new Material
        {
            MaterialCode = materialCode,
            MaterialCategory = request.MaterialCategory.ToString(),
            PlantGrade = request.PlantGrade,
            Specification = request.Specification,
            IsActive = request.IsActive,
            Remark = request.Remark
        };

        _context.Materials.Add(entity);
        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<List<MaterialDto>> CreateBatchAsync(List<CreateMaterialRequest> requests)
    {
        if (requests.Count == 0) return new List<MaterialDto>();

        // 检查批次内是否有重复组合
        var duplicates = requests
            .GroupBy(r => new { r.MaterialCategory, r.PlantGrade, r.Specification })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Any())
        {
            var dupMsg = string.Join("；", duplicates.Select(d => $"{d.MaterialCategory}/{d.PlantGrade}/{d.Specification}"));
            throw new BusinessException($"批次内存在重复物料组合：{dupMsg}");
        }

        // 检查数据库中是否已存在
        var existing = await _context.Materials
            .Select(m => new { m.MaterialCategory, m.PlantGrade, m.Specification })
            .ToListAsync();
        var conflict = requests.FirstOrDefault(r =>
            existing.Any(e =>
                e.MaterialCategory == r.MaterialCategory.ToString() &&
                e.PlantGrade == r.PlantGrade &&
                e.Specification == r.Specification));
        if (conflict != null)
            throw new BusinessException($"物料组合已存在：{conflict.MaterialCategory}/{conflict.PlantGrade}/{conflict.Specification}");

        // 预生成编码
        var maxCode = await _context.Materials
            .Where(m => m.MaterialCode.StartsWith("MA") && m.MaterialCode.Length == 6)
            .OrderByDescending(m => m.MaterialCode)
            .Select(m => m.MaterialCode)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (maxCode != null && int.TryParse(maxCode[2..], out var lastSeq))
            sequence = lastSeq + 1;

        var entities = new List<Material>(requests.Count);
        for (int i = 0; i < requests.Count; i++)
        {
            var r = requests[i];
            var code = $"MA{sequence + i:D4}";
            entities.Add(new Material
            {
                MaterialCode = code,
                MaterialCategory = r.MaterialCategory.ToString(),
                PlantGrade = r.PlantGrade,
                Specification = r.Specification,
                IsActive = r.IsActive,
                Remark = r.Remark
            });
        }

        _context.Materials.AddRange(entities);
        await _context.SaveChangesAsync();
        return entities.Select(ToDto).ToList();
    }

    public async Task<MaterialDto> UpdateAsync(int id, UpdateMaterialRequest request)
    {
        var entity = await _context.Materials
            .FirstOrDefaultAsync(m => m.Id == id);
        if (entity == null) throw new BusinessException("物料不存在");

        if (request.MaterialCategory != null) entity.MaterialCategory = request.MaterialCategory.ToString()!;
        if (request.PlantGrade != null) entity.PlantGrade = request.PlantGrade;
        if (request.Specification != null) entity.Specification = request.Specification;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        if (request.Remark != null) entity.Remark = request.Remark;

        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Materials
            .FirstOrDefaultAsync(m => m.Id == id);
        if (entity == null) throw new BusinessException("物料不存在");

        _context.Materials.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // ========== 打印 ==========

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("MaterialService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var query = _context.Materials.AsNoTracking();
            return new Dictionary<string, List<string>>
            {
                ["MaterialCode"] = await query.Where(m => m.MaterialCode != null).Select(m => m.MaterialCode).Distinct().OrderBy(x => x).ToListAsync(),
                ["MaterialCategory"] = await query.Where(m => m.MaterialCategory != null).Select(m => m.MaterialCategory).Distinct().OrderBy(x => x).ToListAsync(),
                ["PlantGrade"] = await query.Where(m => m.PlantGrade != null).Select(m => m.PlantGrade).Distinct().OrderBy(x => x).ToListAsync(),
                ["Specification"] = await query.Where(m => m.Specification != null).Select(m => m.Specification).Distinct().OrderBy(x => x).ToListAsync(),
                ["Remark"] = await query.Where(m => m.Remark != null).Select(m => m.Remark!).Distinct().OrderBy(x => x).ToListAsync(),
                ["IsActive"] = await query.Select(m => m.IsActive.ToString()).Distinct().OrderBy(x => x).ToListAsync(),
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<byte[]> PrintMaterialAsync(int id, List<PrintColumnDef>? columns = null)
    {
        var dto = await GetByIdAsync(id);
        return TablePrintHelper.GeneratePdf("物料档案列表", new List<Dictionary<string, object>> { ToPrintDict(dto) }, columns ?? []);
    }

    public async Task<byte[]> PrintMaterialBatchAsync(int[] ids, List<PrintColumnDef>? columns = null)
    {
        var result = new List<MaterialDto>();
        foreach (var id in ids)
        {
            try
            {
                result.Add(await GetByIdAsync(id));
            }
            catch (BusinessException) { /* 跳过不存在的物料 */ }
        }
        return TablePrintHelper.GeneratePdf("物料档案列表", result.Select(ToPrintDict).ToList(), columns ?? []);
    }

    public async Task<byte[]> PrintMaterialAllAsync(string? keyword, string? sortBy = null, bool isDescending = false, List<PrintColumnDef>? columns = null)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "CreatedTime",
            IsDescending = isDescending
        };
        var paged = await GetPagedAsync(query);
        return TablePrintHelper.GeneratePdf("物料档案列表", paged.Items.Select(ToPrintDict).ToList(), columns ?? []);
    }

    private static Dictionary<string, object> ToPrintDict(MaterialDto dto) => new()
    {
        ["MaterialCode"] = dto.MaterialCode,
        ["MaterialCategory"] = EnumHelper.GetDisplayName(dto.MaterialCategory),
        ["PlantGrade"] = dto.PlantGrade,
        ["Specification"] = dto.Specification,
        ["IsActive"] = dto.IsActive ? "启用" : "停用",
        ["Remark"] = (object?)dto.Remark ?? "",
    };

    private static MaterialDto ToDto(Material entity) => new()
    {
        Id = entity.Id,
        MaterialCode = entity.MaterialCode,
        MaterialCategory = !string.IsNullOrEmpty(entity.MaterialCategory) && Enum.TryParse<MaterialType>(entity.MaterialCategory, out var mc) ? mc : default,
        PlantGrade = entity.PlantGrade,
        Specification = entity.Specification,
        IsActive = entity.IsActive,
        Remark = entity.Remark,
        CreatedTime = entity.CreatedTime,
        CreatedBy = entity.CreatedBy
    };
}
