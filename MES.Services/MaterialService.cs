using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

public class MaterialService : IMaterialService
{
    private readonly AppDbContext _context;

    public MaterialService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<MaterialDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.Materials
            .AsNoTracking()
            .Where(m => !m.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(m =>
                m.MaterialCategory.Contains(kw) ||
                m.PlantGrade.Contains(kw) ||
                m.Specification.Contains(kw));
        }

        queryable = query.SortBy?.ToLower() switch
        {
            "materialcategory" => query.IsDescending
                ? queryable.OrderByDescending(m => m.MaterialCategory)
                : queryable.OrderBy(m => m.MaterialCategory),
            "plangrade" => query.IsDescending
                ? queryable.OrderByDescending(m => m.PlantGrade)
                : queryable.OrderBy(m => m.PlantGrade),
            _ => query.IsDescending
                ? queryable.OrderByDescending(m => m.CreatedTime)
                : queryable.OrderBy(m => m.CreatedTime)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(m => new MaterialDto
            {
                Id = m.Id,
                MaterialCategory = m.MaterialCategory,
                PlantGrade = m.PlantGrade,
                Specification = m.Specification,
                IsActive = m.IsActive,
                Remark = m.Remark,
                CreatedTime = m.CreatedTime,
                CreatedBy = m.CreatedBy
            })
            .ToListAsync();

        return new PagedResult<MaterialDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<MaterialDto> GetByIdAsync(int id)
    {
        var entity = await _context.Materials
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        if (entity == null) throw new BusinessException("物料不存在");
        return ToDto(entity);
    }

    public async Task<List<MaterialDto>> GetActiveAsync()
    {
        var items = await _context.Materials
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.IsActive)
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
            .Where(m => !m.IsDeleted && m.IsActive)
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
                m.Specification == spec &&
                !m.IsDeleted);
        return entity != null ? ToDto(entity) : null;
    }

    public async Task<MaterialDto> CreateAsync(CreateMaterialRequest request)
    {
        var exists = await _context.Materials
            .AnyAsync(m =>
                m.MaterialCategory == request.MaterialCategory &&
                m.PlantGrade == request.PlantGrade &&
                m.Specification == request.Specification &&
                !m.IsDeleted);
        if (exists) throw new BusinessException("该物料组合已存在");

        var entity = new Material
        {
            MaterialCategory = request.MaterialCategory,
            PlantGrade = request.PlantGrade,
            Specification = request.Specification,
            IsActive = request.IsActive,
            Remark = request.Remark
        };

        _context.Materials.Add(entity);
        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<MaterialDto> UpdateAsync(int id, UpdateMaterialRequest request)
    {
        var entity = await _context.Materials
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        if (entity == null) throw new BusinessException("物料不存在");

        if (request.MaterialCategory != null) entity.MaterialCategory = request.MaterialCategory;
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
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        if (entity == null) throw new BusinessException("物料不存在");

        // 检查是否有关联的库存批次（匹配分类+钢种+规格）
        var hasBatches = await _context.InventoryBatches
            .AnyAsync(b => b.MaterialType == entity.MaterialCategory && b.PlantGrade == entity.PlantGrade && b.Specification == entity.Specification && !b.IsDeleted);
        if (hasBatches) throw new BusinessException("该物料存在关联的库存批次，无法删除");

        entity.IsDeleted = true;
        await _context.SaveChangesAsync();
    }

    private static MaterialDto ToDto(Material entity) => new()
    {
        Id = entity.Id,
        MaterialCategory = entity.MaterialCategory,
        PlantGrade = entity.PlantGrade,
        Specification = entity.Specification,
        IsActive = entity.IsActive,
        Remark = entity.Remark,
        CreatedTime = entity.CreatedTime,
        CreatedBy = entity.CreatedBy
    };
}
