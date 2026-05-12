using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services;

public class SupplierService : ISupplierService
{
    private readonly AppDbContext _context;

    public SupplierService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<SupplierProfileDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.SupplierProfiles
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(s =>
                s.SupplierCode.Contains(kw) ||
                s.SupplierName.Contains(kw) ||
                (s.MaterialCategory != null && s.MaterialCategory.Contains(kw)) ||
                (s.ContactPerson != null && s.ContactPerson.Contains(kw)) ||
                (s.ContactPhone != null && s.ContactPhone.Contains(kw)));
        }

        queryable = query.SortBy?.ToLower() switch
        {
            "suppliercode" => query.IsDescending
                ? queryable.OrderByDescending(s => s.SupplierCode)
                : queryable.OrderBy(s => s.SupplierCode),
            "suppliername" => query.IsDescending
                ? queryable.OrderByDescending(s => s.SupplierName)
                : queryable.OrderBy(s => s.SupplierName),
            "materialcategory" => query.IsDescending
                ? queryable.OrderByDescending(s => s.MaterialCategory ?? "")
                : queryable.OrderBy(s => s.MaterialCategory ?? ""),
            "contactperson" => query.IsDescending
                ? queryable.OrderByDescending(s => s.ContactPerson ?? "")
                : queryable.OrderBy(s => s.ContactPerson ?? ""),
            "contactphone" => query.IsDescending
                ? queryable.OrderByDescending(s => s.ContactPhone ?? "")
                : queryable.OrderBy(s => s.ContactPhone ?? ""),
            "isactive" => query.IsDescending
                ? queryable.OrderByDescending(s => s.IsActive)
                : queryable.OrderBy(s => s.IsActive),
            _ => query.IsDescending
                ? queryable.OrderByDescending(s => s.CreatedTime)
                : queryable.OrderBy(s => s.CreatedTime)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(s => new SupplierProfileDto
            {
                Id = s.Id,
                SupplierCode = s.SupplierCode,
                SupplierName = s.SupplierName,
                MaterialCategory = s.MaterialCategory,
                ContactPerson = s.ContactPerson,
                ContactPhone = s.ContactPhone,
                Address = s.Address,
                IsActive = s.IsActive,
                Remark = s.Remark,
                CreatedTime = s.CreatedTime
            })
            .ToListAsync();

        return new PagedResult<SupplierProfileDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<SupplierProfileDto> GetByIdAsync(int id)
    {
        var entity = await _context.SupplierProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("供应商不存在");
        return ToDto(entity);
    }

    public async Task<List<SupplierProfileDto>> GetActiveAsync()
    {
        return await _context.SupplierProfiles
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SupplierName)
            .Select(s => ToDto(s))
            .ToListAsync();
    }

    public async Task<SupplierProfileDto> CreateAsync(CreateSupplierRequest request)
    {
        var supplierCode = await CodeGenerator.GenerateNextAsync(
            _context.SupplierProfiles.Select(s => s.SupplierCode), "SU");

        var entity = new SupplierProfile
        {
            SupplierCode = supplierCode,
            SupplierName = request.SupplierName,
            MaterialCategory = request.MaterialCategory,
            ContactPerson = request.ContactPerson,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            IsActive = request.IsActive,
            Remark = request.Remark
        };

        _context.SupplierProfiles.Add(entity);
        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<List<SupplierProfileDto>> CreateBatchAsync(List<CreateSupplierRequest> requests)
    {
        if (requests.Count == 0) return new List<SupplierProfileDto>();

        // 预生成编码
        var maxCode = await _context.SupplierProfiles
            .Where(s => s.SupplierCode.StartsWith("SU") && s.SupplierCode.Length == 6)
            .OrderByDescending(s => s.SupplierCode)
            .Select(s => s.SupplierCode)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (maxCode != null && int.TryParse(maxCode[2..], out var lastSeq))
            sequence = lastSeq + 1;

        var entities = new List<SupplierProfile>(requests.Count);
        for (int i = 0; i < requests.Count; i++)
        {
            var r = requests[i];
            var code = $"SU{sequence + i:D4}";
            entities.Add(new SupplierProfile
            {
                SupplierCode = code,
                SupplierName = r.SupplierName,
                MaterialCategory = r.MaterialCategory,
                ContactPerson = r.ContactPerson,
                ContactPhone = r.ContactPhone,
                Address = r.Address,
                IsActive = r.IsActive,
                Remark = r.Remark
            });
        }

        _context.SupplierProfiles.AddRange(entities);
        await _context.SaveChangesAsync();
        return entities.Select(ToDto).ToList();
    }

    public async Task<SupplierProfileDto> UpdateAsync(int id, UpdateSupplierRequest request)
    {
        var entity = await _context.SupplierProfiles
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("供应商不存在");

        if (request.SupplierName != null) entity.SupplierName = request.SupplierName;
        if (request.MaterialCategory != null) entity.MaterialCategory = request.MaterialCategory;
        if (request.ContactPerson != null) entity.ContactPerson = request.ContactPerson;
        if (request.ContactPhone != null) entity.ContactPhone = request.ContactPhone;
        if (request.Address != null) entity.Address = request.Address;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        if (request.Remark != null) entity.Remark = request.Remark;

        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.SupplierProfiles
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("供应商不存在");

        _context.SupplierProfiles.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintSupplierAsync(int id)
    {
        var dto = await GetByIdAsync(id);
        return SupplierPrintHelper.GeneratePdf(dto);
    }

    public async Task<byte[]> PrintSupplierBatchAsync(int[] ids)
    {
        var result = new List<SupplierProfileDto>();
        foreach (var id in ids)
        {
            try
            {
                result.Add(await GetByIdAsync(id));
            }
            catch (BusinessException) { /* 跳过不存在的供应商 */ }
        }
        return SupplierPrintHelper.GenerateBatchPdf(result);
    }

    public async Task<byte[]> PrintSupplierAllAsync(string? keyword, string? sortBy = null, bool isDescending = false)
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
        return SupplierPrintHelper.GenerateBatchPdf(paged.Items);
    }

    private static SupplierProfileDto ToDto(SupplierProfile entity) => new()
    {
        Id = entity.Id,
        SupplierCode = entity.SupplierCode,
        SupplierName = entity.SupplierName,
        MaterialCategory = entity.MaterialCategory,
        ContactPerson = entity.ContactPerson,
        ContactPhone = entity.ContactPhone,
        Address = entity.Address,
        IsActive = entity.IsActive,
        Remark = entity.Remark,
        CreatedTime = entity.CreatedTime
    };
}
