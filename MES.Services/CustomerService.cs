// 文件路径: MES.Services/CustomerService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Mapping;
using MES.Services.Printing;

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
            .AsNoTracking()
            .AsQueryable();

        // Keyword search（多关键词AND + 状态中文映射）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                CustomerStatus? parsedStatus = keyword switch
                {
                    "启用" => CustomerStatus.Active,
                    "停用" => CustomerStatus.Inactive,
                    _ => null
                };
                queryable = queryable.Where(c =>
                    c.CustomerCode.Contains(keyword) ||
                    c.CustomerUnit.Contains(keyword) ||
                    c.Salesman.Contains(keyword) ||
                    (c.EndCustomer != null && c.EndCustomer.Contains(keyword)) ||
                    (parsedStatus.HasValue && c.Status == parsedStatus.Value));
            }
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
            "endcustomer" => query.IsDescending
                ? queryable.OrderByDescending(c => c.EndCustomer ?? "")
                : queryable.OrderBy(c => c.EndCustomer ?? ""),
            "status" => query.IsDescending
                ? queryable.OrderByDescending(c => c.Status)
                : queryable.OrderBy(c => c.Status),
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
                Status = c.Status,  // 直接赋值枚举，不再调用 ToString()
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
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
        {
            throw new BusinessException("客户不存在");
        }

        return entity.ToDto();
    }

    /// <summary>
    /// Create customer
    /// </summary>
    public async Task<CustomerProfileDto> CreateAsync(CreateCustomerRequest request)
    {
        // Check customer code uniqueness
        var exists = await _context.CustomerProfiles
            .AnyAsync(c => c.CustomerCode == request.CustomerCode);

        if (exists)
        {
            throw new BusinessException($"Customer code '{request.CustomerCode}' already exists");
        }

        var entity = new CustomerProfile
        {
            CustomerCode = request.CustomerCode,
            Salesman = request.Salesman,
            CustomerUnit = request.CustomerUnit,
            EndCustomer = request.EndCustomer,
            ContactPerson = request.ContactPerson,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            Status = request.Status,  // 直接使用枚举
            Remark = request.Remark
        };

        _context.CustomerProfiles.Add(entity);
        await _context.SaveChangesAsync();

        return entity.ToDto();
    }

    /// <summary>
    /// Update customer
    /// </summary>
    public async Task<CustomerProfileDto> UpdateAsync(int id, UpdateCustomerRequest request)
    {
        var entity = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
        {
            throw new BusinessException("客户不存在");
        }

        // Check customer code uniqueness (exclude self)
        if (!string.IsNullOrEmpty(request.CustomerCode) && request.CustomerCode != entity.CustomerCode)
        {
            var exists = await _context.CustomerProfiles
                .AnyAsync(c => c.CustomerCode == request.CustomerCode && c.Id != id);

            if (exists)
            {
                throw new BusinessException($"客户代码'{request.CustomerCode}'已存在");
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

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("客户信息已被其他用户修改，请刷新后重试");
        }

        return entity.ToDto();
    }

    /// <summary>
    /// Delete customer (soft delete)
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
        {
            throw new BusinessException("客户不存在");
        }

        _context.CustomerProfiles.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintCustomerAsync(int id)
    {
        var dto = await GetByIdAsync(id);
        return CustomerPrintHelper.GeneratePdf(dto);
    }

    public async Task<byte[]> PrintCustomerBatchAsync(int[] ids)
    {
        var result = new List<CustomerProfileDto>();
        foreach (var id in ids)
        {
            try
            {
                result.Add(await GetByIdAsync(id));
            }
            catch (BusinessException) { }
        }
        return CustomerPrintHelper.GenerateBatchPdf(result);
    }

    public async Task<byte[]> PrintCustomerAllAsync(string? keyword, string? sortBy = null, bool isDescending = false)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy,
            IsDescending = isDescending
        };
        var paged = await GetPagedAsync(query);
        return CustomerPrintHelper.GenerateBatchPdf(paged.Items);
    }
}