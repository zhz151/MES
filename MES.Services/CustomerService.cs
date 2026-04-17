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
/// 客户档案服务实现
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;

    public CustomerService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 分页查询客户列表
    /// </summary>
    public async Task<PagedResult<CustomerProfileDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.CustomerProfiles
            .Where(c => !c.IsDeleted)
            .AsQueryable();

        // 关键词搜索
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            queryable = queryable.Where(c =>
                c.CustomerCode.Contains(query.Keyword) ||
                c.CustomerUnit.Contains(query.Keyword) ||
                c.Salesman.Contains(query.Keyword));
        }

        // 排序
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
    /// 根据ID获取客户详情
    /// </summary>
    public async Task<CustomerProfileDto> GetByIdAsync(int id)
    {
        var entity = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("客户不存在");
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
    /// 创建客户
    /// </summary>
    public async Task<CustomerProfileDto> CreateAsync(CreateCustomerRequest request)
    {
        // 检查客户编码唯一性
        var exists = await _context.CustomerProfiles
            .AnyAsync(c => c.CustomerCode == request.CustomerCode && !c.IsDeleted);

        if (exists)
        {
            throw new BusinessException($"客户编码 '{request.CustomerCode}' 已存在");
        }

        // 解析状态枚举
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
    /// 更新客户
    /// </summary>
    public async Task<CustomerProfileDto> UpdateAsync(int id, UpdateCustomerRequest request)
    {
        var entity = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("客户不存在");
        }

        // 检查客户编码唯一性（排除自身）
        if (!string.IsNullOrEmpty(request.CustomerCode) && request.CustomerCode != entity.CustomerCode)
        {
            var exists = await _context.CustomerProfiles
                .AnyAsync(c => c.CustomerCode == request.CustomerCode && c.Id != id && !c.IsDeleted);

            if (exists)
            {
                throw new BusinessException($"客户编码 '{request.CustomerCode}' 已存在");
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
    /// 删除客户（软删除）
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.CustomerProfiles
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("客户不存在");
        }

        entity.IsDeleted = true;
        await _context.SaveChangesAsync();
    }
}