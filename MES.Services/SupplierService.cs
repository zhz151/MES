using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

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
            .Where(s => !s.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(s =>
                s.SupplierName.Contains(kw) ||
                (s.ContactPerson != null && s.ContactPerson.Contains(kw)) ||
                (s.ContactPhone != null && s.ContactPhone.Contains(kw)));
        }

        queryable = query.SortBy?.ToLower() switch
        {
            "suppliername" => query.IsDescending
                ? queryable.OrderByDescending(s => s.SupplierName)
                : queryable.OrderBy(s => s.SupplierName),
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
                SupplierName = s.SupplierName,
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
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        if (entity == null) throw new BusinessException("供应商不存在");
        return ToDto(entity);
    }

    public async Task<List<SupplierProfileDto>> GetActiveAsync()
    {
        return await _context.SupplierProfiles
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsActive)
            .OrderBy(s => s.SupplierName)
            .Select(s => ToDto(s))
            .ToListAsync();
    }

    public async Task<SupplierProfileDto> CreateAsync(CreateSupplierRequest request)
    {
        var entity = new SupplierProfile
        {
            SupplierName = request.SupplierName,
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

    public async Task<SupplierProfileDto> UpdateAsync(int id, UpdateSupplierRequest request)
    {
        var entity = await _context.SupplierProfiles
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        if (entity == null) throw new BusinessException("供应商不存在");

        if (request.SupplierName != null) entity.SupplierName = request.SupplierName;
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
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        if (entity == null) throw new BusinessException("供应商不存在");

        entity.IsDeleted = true;
        await _context.SaveChangesAsync();
    }

    private static SupplierProfileDto ToDto(SupplierProfile entity) => new()
    {
        Id = entity.Id,
        SupplierName = entity.SupplierName,
        ContactPerson = entity.ContactPerson,
        ContactPhone = entity.ContactPhone,
        Address = entity.Address,
        IsActive = entity.IsActive,
        Remark = entity.Remark,
        CreatedTime = entity.CreatedTime
    };
}
