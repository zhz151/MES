// 文件路径: MES.Services/GradeMappingService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Mapping;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services;

/// <summary>
/// Grade mapping service implementation
/// </summary>
public class GradeMappingService : IGradeMappingService
{
    private readonly AppDbContext _context;

    public GradeMappingService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 分页查询牌号对照（支持关键字搜索）
    /// </summary>
    public async Task<PagedResult<StandardGradeMappingDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.StandardGradeMappings
            .AsNoTracking()
            .AsQueryable();

        // 关键字模糊搜索（多关键词AND + 状态中文映射）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                bool? parsedSpecial = keyword switch
                {
                    "是" => true,
                    "特殊" => true,
                    "否" => false,
                    "常规" => false,
                    _ => null
                };
                queryable = queryable.Where(g =>
                    g.StandardGrade.Contains(keyword) ||
                    (g.StandardGradeCategory != null && g.StandardGradeCategory.Contains(keyword)) ||
                    g.PlantGrade.Contains(keyword) ||
                    (g.HeatTreatment != null && g.HeatTreatment.Contains(keyword)) ||
                    (parsedSpecial.HasValue && g.SpecialMaterial == parsedSpecial.Value) ||
                    (g.SpecialNote != null && g.SpecialNote.Contains(keyword)) ||
                    g.SteelProperty.Contains(keyword) ||
                    (g.Remark != null && g.Remark.Contains(keyword)));
            }
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        // 排序（默认按 StandardGrade 排序）
        var sortBy = string.IsNullOrEmpty(query.SortBy) || query.SortBy.Equals("CreatedTime", StringComparison.OrdinalIgnoreCase)
            ? "StandardGrade"
            : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(g => new StandardGradeMappingDto
            {
                Id = g.Id,
                StandardGrade = g.StandardGrade,
                StandardGradeCategory = g.StandardGradeCategory,
                PlantGrade = g.PlantGrade,
                Density = g.Density,
                HeatTreatment = g.HeatTreatment,
                SpecialMaterial = g.SpecialMaterial,
                SpecialNote = g.SpecialNote,
                SteelProperty = g.SteelProperty,
                Remark = g.Remark
            })
            .ToListAsync();

        return new PagedResult<StandardGradeMappingDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    /// <summary>
    /// Get all grade mappings (for dropdown)
    /// </summary>
    public async Task<List<StandardGradeMappingDto>> GetAllAsync()
    {
        var items = await _context.StandardGradeMappings
            .AsNoTracking()
            .OrderBy(g => g.StandardGrade)
            .Select(g => new StandardGradeMappingDto
            {
                Id = g.Id,
                StandardGrade = g.StandardGrade,
                StandardGradeCategory = g.StandardGradeCategory,
                PlantGrade = g.PlantGrade,
                Density = g.Density,
                HeatTreatment = g.HeatTreatment,
                SpecialMaterial = g.SpecialMaterial,
                SpecialNote = g.SpecialNote,
                SteelProperty = g.SteelProperty,
                Remark = g.Remark
            })
            .ToListAsync();

        return items;
    }

    /// <summary>
    /// Get grade mapping details by ID
    /// </summary>
    public async Task<StandardGradeMappingDto> GetByIdAsync(int id)
    {
        var entity = await _context.StandardGradeMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id);

        if (entity == null)
        {
            throw new BusinessException("Grade mapping does not exist");
        }

        return entity.ToDto();
    }

    /// <summary>
    /// Create grade mapping
    /// </summary>
    public async Task<StandardGradeMappingDto> CreateAsync(CreateGradeMappingRequest request)
    {
        // Check standard grade uniqueness (composite: StandardGrade + StandardGradeCategory)
        var exists = await _context.StandardGradeMappings
            .AnyAsync(g => g.StandardGrade == request.StandardGrade && g.StandardGradeCategory == request.StandardGradeCategory);

        if (exists)
        {
            throw new BusinessException($"标准牌号 '{request.StandardGrade}' 已存在");
        }

        var entity = new StandardGradeMapping
        {
            StandardGrade = request.StandardGrade,
            StandardGradeCategory = request.StandardGradeCategory,
            PlantGrade = request.PlantGrade,
            Density = request.Density,
            HeatTreatment = request.HeatTreatment,
            SpecialMaterial = request.SpecialMaterial,
            SpecialNote = request.SpecialNote,
            SteelProperty = ComputeSteelProperty(request.PlantGrade),
            Remark = request.Remark
        };

        _context.StandardGradeMappings.Add(entity);
        await _context.SaveChangesAsync();

        return entity.ToDto();
    }

    /// <summary>
    /// Update grade mapping
    /// </summary>
    public async Task<StandardGradeMappingDto> UpdateAsync(int id, UpdateGradeMappingRequest request)
    {
        var entity = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(g => g.Id == id);

        if (entity == null)
        {
            throw new BusinessException("Grade mapping does not exist");
        }

        // Check standard grade uniqueness (composite, exclude self)
        var gradeChanged = !string.IsNullOrEmpty(request.StandardGrade) &&
            (request.StandardGrade != entity.StandardGrade ||
             request.StandardGradeCategory != entity.StandardGradeCategory);
        if (gradeChanged)
        {
            var exists = await _context.StandardGradeMappings
                .AnyAsync(g => g.StandardGrade == request.StandardGrade && g.StandardGradeCategory == request.StandardGradeCategory && g.Id != id);

            if (exists)
            {
                throw new BusinessException($"标准牌号 '{request.StandardGrade}' 已存在");
            }
            entity.StandardGrade = request.StandardGrade;
            entity.StandardGradeCategory = request.StandardGradeCategory;
        }

        if (!string.IsNullOrEmpty(request.PlantGrade))
        {
            entity.PlantGrade = request.PlantGrade;
            entity.SteelProperty = ComputeSteelProperty(request.PlantGrade);
        }

        if (request.Density.HasValue)
        {
            entity.Density = request.Density.Value;
        }

        if (request.HeatTreatment != null)
        {
            entity.HeatTreatment = request.HeatTreatment;
        }

        if (request.SpecialMaterial.HasValue)
        {
            entity.SpecialMaterial = request.SpecialMaterial.Value;
        }

        if (request.SpecialNote != null)
        {
            entity.SpecialNote = request.SpecialNote;
        }

        if (request.Remark != null)
        {
            entity.Remark = request.Remark;
        }

        await _context.SaveChangesAsync();

        return entity.ToDto();
    }

    /// <summary>
    /// Delete grade mapping (soft delete)
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(g => g.Id == id);

        if (entity == null)
        {
            throw new BusinessException("Grade mapping does not exist");
        }

        _context.StandardGradeMappings.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintGradeMappingAsync(int id)
    {
        var dto = await GetByIdAsync(id);
        return GradeMappingPrintHelper.GeneratePdf(dto);
    }

    public async Task<byte[]> PrintGradeMappingBatchAsync(int[] ids)
    {
        var result = new List<StandardGradeMappingDto>();
        foreach (var id in ids)
        {
            try
            {
                result.Add(await GetByIdAsync(id));
            }
            catch (BusinessException) { /* 跳过不存在的牌号映射 */ }
        }
        return GradeMappingPrintHelper.GenerateBatchPdf(result);
    }

    public async Task<byte[]> PrintGradeMappingAllAsync(string? keyword, string? sortBy = null, bool isDescending = false)
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
        return GradeMappingPrintHelper.GenerateBatchPdf(paged.Items);
    }

    // ========== 筛选上下文 ==========

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.StandardGradeMappings.AsNoTracking();
        return new Dictionary<string, List<string>>
        {
            ["StandardGrade"] = await query.Select(x => x.StandardGrade).Distinct().OrderBy(x => x).ToListAsync(),
            ["StandardGradeCategory"] = await query.Where(x => x.StandardGradeCategory != null).Select(x => x.StandardGradeCategory!).Distinct().OrderBy(x => x).ToListAsync(),
            ["PlantGrade"] = await query.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToListAsync(),
            ["HeatTreatment"] = await query.Where(x => x.HeatTreatment != null).Select(x => x.HeatTreatment!).Distinct().OrderBy(x => x).ToListAsync(),
            ["SteelProperty"] = await query.Select(x => x.SteelProperty).Distinct().OrderBy(x => x).ToListAsync(),
            ["SpecialNote"] = await query.Where(x => x.SpecialNote != null).Select(x => x.SpecialNote!).Distinct().OrderBy(x => x).ToListAsync(),
            ["Remark"] = await query.Where(x => x.Remark != null).Select(x => x.Remark!).Distinct().OrderBy(x => x).ToListAsync(),
        };
    }

    // ========== 钢性计算 ==========

    /// <summary>
    /// 根据工厂牌号首字计算钢性
    /// 首字为"3"或"9"→奥氏体，"2"→双相钢，其他→镍基合金
    /// </summary>
    internal static string ComputeSteelProperty(string plantGrade)
    {
        if (string.IsNullOrEmpty(plantGrade)) return "镍基合金";
        var firstChar = plantGrade.Trim()[0];
        return firstChar switch
        {
            '3' or '9' => "奥氏体",
            '2' => "双相钢",
            _ => "镍基合金"
        };
    }
}