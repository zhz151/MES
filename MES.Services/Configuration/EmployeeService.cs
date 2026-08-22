using Microsoft.EntityFrameworkCore;
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
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Configuration;

public class EmployeeService : IEmployeeService
{
    private readonly AppDbContext _context;

    public EmployeeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<EmployeeDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.Employees
            .AsNoTracking()
            .AsQueryable();

        // 关键字模糊搜索
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                queryable = queryable.Where(e =>
                    e.Code.Contains(keyword) ||
                    e.Name.Contains(keyword) ||
                    (e.Department != null && e.Department.Contains(keyword)) ||
                    (e.Position != null && e.Position.Contains(keyword)) ||
                    (e.SalaryMode != null && e.SalaryMode.Contains(keyword)));
            }
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        // 排序
        var sortBy = string.IsNullOrEmpty(query.SortBy) || query.SortBy.Equals("CreatedTime", StringComparison.OrdinalIgnoreCase)
            ? "Code"
            : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(e => new EmployeeDto
            {
                Id = e.Id,
                Code = e.Code,
                Name = e.Name,
                Department = e.Department,
                Position = e.Position,
                PositionRemark = e.PositionRemark,
                SalaryMode = e.SalaryMode,
                SalaryRemark = e.SalaryRemark,
                IsActive = e.IsActive
            })
            .ToListAsync();

        return new PagedResult<EmployeeDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<EmployeeDto?> GetByCodeAsync(string code)
    {
        return await _context.Employees
            .Where(e => e.Code == code && e.IsActive)
            .Select(e => new EmployeeDto
            {
                Id = e.Id,
                Code = e.Code,
                Name = e.Name,
                Department = e.Department,
                Position = e.Position,
                PositionRemark = e.PositionRemark,
                SalaryMode = e.SalaryMode,
                SalaryRemark = e.SalaryRemark,
                IsActive = e.IsActive
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> SaveAsync(EmployeeDto dto)
    {
        if (dto.Id > 0)
        {
            // 更新
            var entity = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == dto.Id);

            if (entity == null)
                throw new BusinessException("员工不存在");

            entity.Code = dto.Code;
            entity.Name = dto.Name;
            entity.Department = dto.Department;
            entity.Position = dto.Position;
            entity.PositionRemark = dto.PositionRemark;
            entity.SalaryMode = dto.SalaryMode;
            entity.SalaryRemark = dto.SalaryRemark;
            entity.IsActive = dto.IsActive;
        }
        else
        {
            // 新增
            var entity = new Employee
            {
                Code = dto.Code,
                Name = dto.Name,
                Department = dto.Department,
                Position = dto.Position,
                PositionRemark = dto.PositionRemark,
                SalaryMode = dto.SalaryMode,
                SalaryRemark = dto.SalaryRemark,
                IsActive = dto.IsActive
            };
            _context.Employees.Add(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id);

        if (entity == null)
            throw new BusinessException("员工不存在");

        _context.Employees.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var all = await GetPagedAsync(query);
        var selected = all.Items.Where(i => ids.Contains(i.Id)).ToList();
        return EmployeePrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? null! : sortBy,
            IsDescending = isDescending
        };
        var result = await GetPagedAsync(query);
        return EmployeePrintHelper.GenerateBatchPdf(result.Items, columns);
    }
}
