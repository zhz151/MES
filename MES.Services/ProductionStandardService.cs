// 文件路径: MES.Services/ProductionStandardService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Mapping;
using MES.Services.Printing;

namespace MES.Services;

/// <summary>
/// Production standard service implementation
/// </summary>
public class ProductionStandardService : IProductionStandardService
{
    private readonly AppDbContext _context;

    public ProductionStandardService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 分页查询产品标准（支持关键字搜索）
    /// </summary>
    public async Task<PagedResult<ProductionStandardDto>> GetPagedAsync(QueryParams query, bool? isActive = null)
    {
        var queryable = _context.ProductionStandards
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        // 关键字模糊搜索（多关键词AND + 状态中文映射）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                bool? parsedActive = keyword switch
                {
                    "启用" => true,
                    "停用" => false,
                    _ => null
                };
                queryable = queryable.Where(p =>
                    p.StandardCode.Contains(keyword) ||
                    p.StandardName.Contains(keyword) ||
                    (parsedActive.HasValue && p.IsActive == parsedActive.Value));
            }
        }

        // 状态筛选（在服务端执行，确保分页总数正确）
        if (isActive.HasValue)
        {
            queryable = queryable.Where(p => p.IsActive == isActive.Value);
        }

        // 排序
        queryable = query.SortBy?.ToLower() switch
        {
            "standardcode" => query.IsDescending
                ? queryable.OrderByDescending(p => p.StandardCode)
                : queryable.OrderBy(p => p.StandardCode),
            "standardname" => query.IsDescending
                ? queryable.OrderByDescending(p => p.StandardName)
                : queryable.OrderBy(p => p.StandardName),
            "sortorder" => query.IsDescending
                ? queryable.OrderByDescending(p => p.SortOrder)
                : queryable.OrderBy(p => p.SortOrder),
            "isactive" => query.IsDescending
                ? queryable.OrderByDescending(p => p.IsActive)
                : queryable.OrderBy(p => p.IsActive),
            _ => query.IsDescending
                ? queryable.OrderByDescending(p => p.SortOrder)
                : queryable.OrderBy(p => p.SortOrder)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(p => new ProductionStandardDto
            {
                Id = p.Id,
                StandardCode = p.StandardCode,
                StandardName = p.StandardName,
                Remark = p.Remark,
                SortOrder = p.SortOrder,
                IsActive = p.IsActive
            })
            .ToListAsync();

        return new PagedResult<ProductionStandardDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    /// <summary>
    /// Get all production standards (for dropdown)
    /// </summary>
    /// <param name="onlyActive">Whether to return only active standards, default true</param>
    public async Task<List<ProductionStandardDto>> GetAllAsync(bool onlyActive = true)
    {
        var query = _context.ProductionStandards
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (onlyActive)
        {
            query = query.Where(p => p.IsActive);
        }

        var items = await query
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.StandardCode)
            .Select(p => new ProductionStandardDto
            {
                Id = p.Id,
                StandardCode = p.StandardCode,
                StandardName = p.StandardName,
                Remark = p.Remark,
                SortOrder = p.SortOrder,
                IsActive = p.IsActive
            })
            .ToListAsync();

        return items;
    }

    /// <summary>
    /// Get production standard details by ID
    /// </summary>
    public async Task<ProductionStandardDto> GetByIdAsync(int id)
    {
        var entity = await _context.ProductionStandards
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Production standard does not exist");
        }

        return entity.ToDto();
    }

    /// <summary>
    /// Create production standard
    /// </summary>
    public async Task<ProductionStandardDto> CreateAsync(CreateProductionStandardRequest request)
    {
        // Check standard code uniqueness
        var exists = await _context.ProductionStandards
            .AnyAsync(p => p.StandardCode == request.StandardCode && !p.IsDeleted);

        if (exists)
        {
            throw new BusinessException($"Standard code '{request.StandardCode}' already exists");
        }

        var entity = new ProductionStandard
        {
            StandardCode = request.StandardCode,
            StandardName = request.StandardName,
            Remark = request.Remark,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };

        _context.ProductionStandards.Add(entity);
        await _context.SaveChangesAsync();

        return entity.ToDto();
    }

    /// <summary>
    /// Update production standard
    /// </summary>
    public async Task<ProductionStandardDto> UpdateAsync(int id, UpdateProductionStandardRequest request)
    {
        var entity = await _context.ProductionStandards
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Production standard does not exist");
        }

        // Check standard code uniqueness (exclude self)
        if (!string.IsNullOrEmpty(request.StandardCode) && request.StandardCode != entity.StandardCode)
        {
            var exists = await _context.ProductionStandards
                .AnyAsync(p => p.StandardCode == request.StandardCode && p.Id != id && !p.IsDeleted);

            if (exists)
            {
                throw new BusinessException($"Standard code '{request.StandardCode}' already exists");
            }
            entity.StandardCode = request.StandardCode;
        }

        if (!string.IsNullOrEmpty(request.StandardName))
        {
            entity.StandardName = request.StandardName;
        }

        if (request.Remark != null)
        {
            entity.Remark = request.Remark;
        }

        if (request.SortOrder.HasValue)
        {
            entity.SortOrder = request.SortOrder.Value;
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        await _context.SaveChangesAsync();

        return entity.ToDto();
    }

    /// <summary>
    /// Delete production standard (soft delete)
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.ProductionStandards
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Production standard does not exist");
        }

        entity.IsDeleted = true;
        await _context.SaveChangesAsync();
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintStandardAsync(int id)
    {
        var dto = await GetByIdAsync(id);
        return StandardPrintHelper.GeneratePdf(dto);
    }

    public async Task<byte[]> PrintStandardBatchAsync(int[] ids)
    {
        var result = new List<ProductionStandardDto>();
        foreach (var id in ids)
        {
            try
            {
                result.Add(await GetByIdAsync(id));
            }
            catch (BusinessException) { }
        }
        return StandardPrintHelper.GenerateBatchPdf(result);
    }

    public async Task<byte[]> PrintStandardAllAsync(string? keyword, bool? isActive, string? sortBy = null, bool isDescending = false)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy,
            IsDescending = isDescending
        };
        var paged = await GetPagedAsync(query, isActive);
        return StandardPrintHelper.GenerateBatchPdf(paged.Items);
    }
}