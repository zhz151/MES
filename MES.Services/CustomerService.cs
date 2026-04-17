// 文件路径: MES.Services/CustomerService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// Customer profile service implementation
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;

    public CustomerService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get paged customer list
    /// </summary>
    public async Task<PagedResult<CustomerProfileDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.CustomerProfiles
            .Where(c => !c.IsDeleted)
            .AsQueryable();

        // Keyword search
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            queryable = queryable.Where(c =>
                c.CustomerCode.Contains(query.Keyword) ||
                c.CustomerUnit.Contains(query.Keyword) ||
                c.Salesman.Contains(query.Keyword));
        }

        // Sorting
        queryable = query.SortBy?.ToLower() switch
        {
            "customercode" => query.IsDescending
                ? queryable.OrderByDescending(c => c.CustomerCode)
                : queryable.OrderBy(c => c.CustomerCode),
            "customerunit" => query.IsDescending
                ? queryable.OrderByDescending(c => c.CustomerUnit)
                : queryable.OrderBy(c => c.CustomerUnit),
            "salesman" => query.IsDescending
                ? queryable.OrderByDescending(c => c.Salesman)
                : queryable.OrderBy(c => c.Salesman),
            _ => query.IsDescending
                ? queryable.OrderByDescending(c => c.CreatedTime)
                : queryable.OrderBy(c => c.CreatedTime)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(c => new CustomerProfileDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                Salesman = c.Salesman,
                CustomerUnit = c.CustomerUnit,
                EndCustomer = c.EndCustomer,
                ContactPerson = c.ContactPerson,
                ContactPhone = c.ContactPhone,
                Address = c.Address,
                Status = c.Status.ToString(),
                Remark = c.Remark
            })
            .ToListAsync();

        return new PagedResult<CustomerProfileDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    /// <summary>
    /// Get customer details by ID
    /// </summary>
    public async Task<CustomerProfileDto> GetByIdAsync(int id)
    {
        var entity = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Customer does not exist");
        }

        return new CustomerProfileDto
        {
            Id = entity.Id,
            CustomerCode = entity.CustomerCode,
            Salesman = entity.Salesman,
            CustomerUnit = entity.CustomerUnit,
            EndCustomer = entity.EndCustomer,
            ContactPerson = entity.ContactPerson,
            ContactPhone = entity.ContactPhone,
            Address = entity.Address,
            Status = entity.Status.ToString(),
            Remark = entity.Remark
        };
    }

    /// <summary>
    /// Create customer
    /// </summary>
    public async Task<CustomerProfileDto> CreateAsync(CreateCustomerRequest request)
    {
        // Check customer code uniqueness
        var exists = await _context.CustomerProfiles
            .AnyAsync(c => c.CustomerCode == request.CustomerCode && !c.IsDeleted);

        if (exists)
        {
            throw new BusinessException($"Customer code '{request.CustomerCode}' already exists");
        }

        // Parse status enum
        var status = Enum.TryParse<CustomerStatus>(request.Status, true, out var parsedStatus)
            ? parsedStatus
            : CustomerStatus.Active;

        var entity = new CustomerProfile
        {
            CustomerCode = request.CustomerCode,
            Salesman = request.Salesman,
            CustomerUnit = request.CustomerUnit,
            EndCustomer = request.EndCustomer,
            ContactPerson = request.ContactPerson,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            Status = status,
            Remark = request.Remark
        };

        _context.CustomerProfiles.Add(entity);
        await _context.SaveChangesAsync();

        return new CustomerProfileDto
        {
            Id = entity.Id,
            CustomerCode = entity.CustomerCode,
            Salesman = entity.Salesman,
            CustomerUnit = entity.CustomerUnit,
            EndCustomer = entity.EndCustomer,
            ContactPerson = entity.ContactPerson,
            ContactPhone = entity.ContactPhone,
            Address = entity.Address,
            Status = entity.Status.ToString(),
            Remark = entity.Remark
        };
    }

    /// <summary>
    /// Update customer
    /// </summary>
    public async Task<CustomerProfileDto> UpdateAsync(int id, UpdateCustomerRequest request)
    {
        var entity = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Customer does not exist");
        }

        // Check customer code uniqueness (exclude self)
        if (!string.IsNullOrEmpty(request.CustomerCode) && request.CustomerCode != entity.CustomerCode)
        {
            var exists = await _context.CustomerProfiles
                .AnyAsync(c => c.CustomerCode == request.CustomerCode && c.Id != id && !c.IsDeleted);

            if (exists)
            {
                throw new BusinessException($"Customer code '{request.CustomerCode}' already exists");
            }
            entity.CustomerCode = request.CustomerCode;
        }

        if (!string.IsNullOrEmpty(request.Salesman))
        {
            entity.Salesman = request.Salesman;
        }

        if (!string.IsNullOrEmpty(request.CustomerUnit))
        {
            entity.CustomerUnit = request.CustomerUnit;
        }

        if (request.EndCustomer != null)
        {
            entity.EndCustomer = request.EndCustomer;
        }

        if (request.ContactPerson != null)
        {
            entity.ContactPerson = request.ContactPerson;
        }

        if (request.ContactPhone != null)
        {
            entity.ContactPhone = request.ContactPhone;
        }

        if (request.Address != null)
        {
            entity.Address = request.Address;
        }

        if (request.Status.HasValue)
        {
            entity.Status = request.Status.Value;
        }

        if (request.Remark != null)
        {
            entity.Remark = request.Remark;
        }

        await _context.SaveChangesAsync();

        return new CustomerProfileDto
        {
            Id = entity.Id,
            CustomerCode = entity.CustomerCode,
            Salesman = entity.Salesman,
            CustomerUnit = entity.CustomerUnit,
            EndCustomer = entity.EndCustomer,
            ContactPerson = entity.ContactPerson,
            ContactPhone = entity.ContactPhone,
            Address = entity.Address,
            Status = entity.Status.ToString(),
            Remark = entity.Remark
        };
    }

    /// <summary>
    /// Delete customer (soft delete)
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Customer does not exist");
        }

        entity.IsDeleted = true;
        await _context.SaveChangesAsync();
    }
}