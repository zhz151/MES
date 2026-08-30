// 文件路径: MES.Services/CustomerService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
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
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Services.Printing;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Order;
using MES.Services.Helpers;
using MES.Services.Order;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Order;

/// <summary>
/// Customer profile service implementation
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public CustomerService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
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
                    (parsedStatus.HasValue && c.Status == parsedStatus.Value) ||
                    (c.ContactPerson != null && c.ContactPerson.Contains(keyword)) ||
                    (c.ContactPhone != null && c.ContactPhone.Contains(keyword)) ||
                    (c.Address != null && c.Address.Contains(keyword)) ||
                    (c.Remark != null && c.Remark.Contains(keyword)));
            }
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        // Sorting
        queryable = queryable.ApplySort(query.SortBy, query.IsDescending);

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

        return ToDto(entity);
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
            throw new BusinessException($"客户代码'{request.CustomerCode}'已存在");
        }

        var entity = new CustomerProfile
        {
            CustomerCode = request.CustomerCode,
            Salesman = request.Salesman,
            CustomerUnit = request.CustomerUnit,
            EndCustomer = string.IsNullOrEmpty(request.EndCustomer) ? request.CustomerUnit : request.EndCustomer,
            ContactPerson = request.ContactPerson,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            Status = request.Status,  // 直接使用枚举
            Remark = request.Remark
        };

        _context.CustomerProfiles.Add(entity);
        await _context.SaveChangesAsync();

        return ToDto(entity);
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

        // 客户信息变更不再刷新订单读模型——订单快照字段独立维护
        return ToDto(entity);
    }

    /// <summary>
    /// Delete customer (物理删除)
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
        {
            throw new BusinessException("客户不存在");
        }

        // CustomerId FK 已移除，订单快照字段独立维护，可直接删除客户
        _context.CustomerProfiles.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<CustomerSelectDto>> GetSelectListAsync()
    {
        return await _context.CustomerProfiles
            .AsNoTracking()
            .OrderBy(c => c.CustomerUnit)
            .Select(c => new CustomerSelectDto
            {
                Id = c.Id,
                CustomerUnit = c.CustomerUnit,
                Salesman = c.Salesman ?? string.Empty,
                EndCustomer = c.EndCustomer
            })
            .ToListAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.CustomerFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            // 注意：枚举列（Status）不在此处返回，
            // 由前端 EnumOptions fallback 直接提供带中文 Display 的选项，避免映射丢失。
            var all = await _context.CustomerProfiles
                .AsNoTracking()
                .Select(c => new
                {
                    c.CustomerCode,
                    c.Salesman,
                    c.CustomerUnit,
                    c.EndCustomer,
                    c.ContactPerson,
                    c.ContactPhone,
                    c.Address,
                    c.Remark
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["CustomerCode"] = all.Select(x => x.CustomerCode).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
                ["Salesman"] = all.Select(x => x.Salesman).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
                ["CustomerUnit"] = all.Select(x => x.CustomerUnit).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
                ["EndCustomer"] = all.Select(x => x.EndCustomer ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["ContactPerson"] = all.Select(x => x.ContactPerson ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["ContactPhone"] = all.Select(x => x.ContactPhone ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Address"] = all.Select(x => x.Address ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Remark"] = all.Select(x => x.Remark ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList()
            };

        }) ?? new Dictionary<string, List<string>>();
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintCustomerBatchAsync(int[] ids, List<PrintColumnDef>? columns = null)
    {
        var result = new List<CustomerProfileDto>();
        foreach (var id in ids)
        {
            try
            {
                result.Add(await GetByIdAsync(id));
            }
            catch (BusinessException) { /* 跳过不存在的客户 */ }
        }
        return TablePrintHelper.GeneratePdf("客户档案列表", result, columns ?? []);
    }

    private static CustomerProfileDto ToDto(CustomerProfile entity) => new()
    {
        Id = entity.Id,
        CustomerCode = entity.CustomerCode,
        Salesman = entity.Salesman,
        CustomerUnit = entity.CustomerUnit,
        EndCustomer = entity.EndCustomer,
        ContactPerson = entity.ContactPerson,
        ContactPhone = entity.ContactPhone,
        Address = entity.Address,
        Status = entity.Status,
        Remark = entity.Remark
    };
}
