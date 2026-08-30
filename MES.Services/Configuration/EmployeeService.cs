using System.Linq.Expressions;
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
using MES.Core.Constants;
using MES.Core.Enums;
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

        // 通用筛选（SectionName 为逗号分隔的多工段串，equals 按列表任一元素匹配）
        queryable = ApplyEmployeeFilters(queryable, query.Filters);

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
                SectionName = e.SectionName,
                GroupName = e.GroupName,
                InspectionItems = e.InspectionItems,
                ProcessInspectionItems = e.ProcessInspectionItems,
                MaterialReceiveCheckItems = e.MaterialReceiveCheckItems,
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

    /// <summary>
    /// 逗号分隔列表字段：SectionName（生产工段）/ GroupName（组类，可多组）/ InspectionItems（成检项目资质）
    /// ProcessInspectionItems / MaterialReceiveCheckItems 为布尔开关（是否属于对应环节操作人），走通用 bool 筛选
    /// </summary>
    private static readonly string[] CommaListFields =
    {
        nameof(Employee.SectionName),
        nameof(Employee.GroupName),
        nameof(Employee.InspectionItems)
    };

    /// <summary>
    /// 员工通用筛选：逗号分隔列表字段（SectionName 多工段、GroupName 组类、InspectionItems 多检验项目资质）
    /// 的 equals/in 均按「任一元素精确匹配」，避免子串误匹配（如 Welding ⊂ WeldingHead）。
    /// </summary>
    private static IQueryable<Employee> ApplyEmployeeFilters(IQueryable<Employee> queryable, List<FilterDescriptor>? filters)
    {
        if (filters == null || filters.Count == 0)
            return queryable;

        var remaining = filters.ToList();
        foreach (var field in CommaListFields)
        {
            var listFilter = remaining.FirstOrDefault(f =>
                string.Equals(f.Field, field, StringComparison.OrdinalIgnoreCase)
                && string.Equals(f.Operator, "equals", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(f.Value));
            if (listFilter != null)
            {
                queryable = queryable.Where(BuildCommaListContains(field, listFilter.Value!.Trim()));
                remaining.Remove(listFilter);
                continue;
            }

            // ExcelFilter 列头多选（in）：任一选项命中逗号列表任一元素即可
            var inFilter = remaining.FirstOrDefault(f =>
                string.Equals(f.Field, field, StringComparison.OrdinalIgnoreCase)
                && string.Equals(f.Operator, "in", StringComparison.OrdinalIgnoreCase)
                && f.Values is { Count: > 0 });
            if (inFilter != null)
            {
                queryable = queryable.Where(BuildCommaListIn(field, inFilter.Values!));
                remaining.Remove(inFilter);
            }
        }

        if (remaining.Count > 0)
            queryable = queryable.ApplyFilters(remaining);

        return queryable;
    }

    /// <summary>构造逗号列表字段「任一元素精确匹配」表达式（== 或 StartsWith/Contains/EndsWith 逗号边界）</summary>
    private static Expression<Func<Employee, bool>> BuildCommaListContains(string field, string value)
    {
        var e = Expression.Parameter(typeof(Employee), "e");
        var body = BuildCommaListContainsBody(Expression.Property(e, field), value);
        return Expression.Lambda<Func<Employee, bool>>(body, e);
    }

    /// <summary>构造逗号列表字段「任一选项命中任一元素」表达式（多个单值匹配取 OrElse）</summary>
    private static Expression<Func<Employee, bool>> BuildCommaListIn(string field, List<string> values)
    {
        var e = Expression.Parameter(typeof(Employee), "e");
        var member = Expression.Property(e, field);
        var body = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => BuildCommaListContainsBody(member, v.Trim()))
            .Aggregate((acc, next) => Expression.OrElse(acc, next));
        return Expression.Lambda<Func<Employee, bool>>(body, e);
    }

    private static Expression BuildCommaListContainsBody(Expression member, string value)
    {
        var notNull = Expression.NotEqual(member, Expression.Constant(null, typeof(string)));
        var eq = Expression.Equal(member, Expression.Constant(value));
        Expression startsWith = Expression.AndAlso(notNull,
            Expression.Call(member, typeof(string).GetMethod("StartsWith", [typeof(string)])!, Expression.Constant(value + ",")));
        Expression contains = Expression.AndAlso(notNull,
            Expression.Call(member, typeof(string).GetMethod("Contains", [typeof(string)])!, Expression.Constant("," + value + ",")));
        Expression endsWith = Expression.AndAlso(notNull,
            Expression.Call(member, typeof(string).GetMethod("EndsWith", [typeof(string)])!, Expression.Constant("," + value)));
        return Expression.OrElse(eq, Expression.OrElse(startsWith, Expression.OrElse(contains, endsWith)));
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
                SectionName = e.SectionName,
                GroupName = e.GroupName,
                InspectionItems = e.InspectionItems,
                ProcessInspectionItems = e.ProcessInspectionItems,
                MaterialReceiveCheckItems = e.MaterialReceiveCheckItems,
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
            entity.SectionName = dto.SectionName;
            entity.GroupName = dto.GroupName;
            entity.InspectionItems = dto.InspectionItems;
            entity.ProcessInspectionItems = dto.ProcessInspectionItems;
            entity.MaterialReceiveCheckItems = dto.MaterialReceiveCheckItems;
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
                SectionName = dto.SectionName,
                GroupName = dto.GroupName,
                InspectionItems = dto.InspectionItems,
                ProcessInspectionItems = dto.ProcessInspectionItems,
                MaterialReceiveCheckItems = dto.MaterialReceiveCheckItems,
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

    /// <summary>
    /// 列头筛选上下文：自由文本列取存量去重值；工段/成检项目取标准选项（前端转中文显示）；
    /// 过程检验/成检到料/启用列固定 是/否。筛选值=存储值（英文 Key/枚举名/bool 串），与 GetPagedAsync 筛选匹配
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var rows = await _context.Employees
            .AsNoTracking()
            .Select(e => new { e.Code, e.Name, e.Department, e.Position, e.PositionRemark, e.SalaryMode, e.SalaryRemark, e.SectionName, e.GroupName })
            .ToListAsync();

        // 工段选项 = 26 标准工段 + 存量工段片段中非标准值（员工工段为逗号串多工段）
        var sectionValues = rows.Select(r => r.SectionName)
            .Where(v => !string.IsNullOrEmpty(v))
            .SelectMany(v => v!.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var sectionOptions = SectionKeys.All
            .Concat(sectionValues.Where(v => !SectionKeys.All.Contains(v, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        return new Dictionary<string, List<string>>
        {
            ["Department"] = Distinct(rows.Select(r => r.Department)),
            ["Position"] = Distinct(rows.Select(r => r.Position)),
            ["Code"] = Distinct(rows.Select(r => r.Code)),
            ["Name"] = Distinct(rows.Select(r => r.Name)),
            ["SectionName"] = sectionOptions,
            ["GroupName"] = Distinct(rows.Select(r => r.GroupName)),
            ["ProcessInspectionItems"] = new List<string> { "True", "False" },
            ["MaterialReceiveCheckItems"] = new List<string> { "True", "False" },
            ["InspectionItems"] = Enum.GetValues<InspectionItem>().Select(e => e.ToString()).ToList(),
            ["IsActive"] = new List<string> { "True", "False" },
            ["PositionRemark"] = Distinct(rows.Select(r => r.PositionRemark)),
            ["SalaryMode"] = Distinct(rows.Select(r => r.SalaryMode)),
            ["SalaryRemark"] = Distinct(rows.Select(r => r.SalaryRemark))
        };
    }

    private static List<string> Distinct(IEnumerable<string?> values)
        => values.Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v)
            .ToList();

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var all = await GetPagedAsync(query);
        var selected = all.Items.Where(i => ids.Contains(i.Id)).ToList();
        return EmployeePrintHelper.GenerateBatchPdf(selected, columns);
    }
}
