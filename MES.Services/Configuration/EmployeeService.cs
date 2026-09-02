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
using MES.Core.Helpers;
using MES.Core.Interfaces.Auth;
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
using MES.Shared.Constants;

namespace MES.Services.Configuration;

public class EmployeeService : IEmployeeService
{
    /// <summary>员工自动创建登录账号的默认密码（明文，登录后建议修改）</summary>
    public const string DefaultAccountPassword = "123456";

    private readonly AppDbContext _context;
    private readonly IUserManagementService _userManagementService;

    public EmployeeService(AppDbContext context, IUserManagementService userManagementService)
    {
        _context = context;
        _userManagementService = userManagementService;
    }

    public async Task<PagedResult<EmployeeDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.Employees
            .AsNoTracking()
            .AsQueryable();

        // 关键字模糊搜索（Position/Department 存英文 Key，中文名经 ToKey 归一后精确匹配）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                var positionKey = ResolvePositionKey(keyword);
                var categoryKey = ResolvePositionCategoryKey(keyword);
                queryable = queryable.Where(e =>
                    e.Code.Contains(keyword) ||
                    e.Name.Contains(keyword) ||
                    (e.Department != null && (e.Department.Contains(keyword) || (categoryKey != null && e.Department == categoryKey))) ||
                    (e.Position != null && (e.Position.Contains(keyword) || (positionKey != null && e.Position == positionKey))));
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
                AttendanceCoefficient = e.AttendanceCoefficient,
                HourlyWage = e.HourlyWage,
                DailyWage = e.DailyWage,
                MonthlyWage = e.MonthlyWage,
                SectionName = e.SectionName,
                GroupName = e.GroupName,
                InspectionItems = e.InspectionItems,
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

    // ========== 岗位/岗位类别参数表动态解析（配置表可加值，常量类仅兜底） ==========

    /// <summary>岗位中文 → Key：常量类优先，其次参数表 OverrideMap 动态反查（含配置新增岗位，如 cjws 车间卫生）</summary>
    private static string? ResolvePositionKey(string? keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return null;
        var key = PositionKeys.ToKey(keyword);
        if (key != null) return key;
        return ReverseLookup(DictValueDefaults.PositionKey, keyword);
    }

    /// <summary>岗位类别中文 → Key：常量类优先，其次参数表 OverrideMap 动态反查</summary>
    private static string? ResolvePositionCategoryKey(string? keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return null;
        var key = PositionCategoryKeys.ToKey(keyword);
        if (key != null) return key;
        return ReverseLookup(DictValueDefaults.PositionCategoryKey, keyword);
    }

    /// <summary>参数表中文显示名 → Key（OverrideMap 由 API 启动注入；未注入返回 null）</summary>
    private static string? ReverseLookup(string dictKey, string chinese)
    {
        if (DictValueDisplayHelper.OverrideMap != null
            && DictValueDisplayHelper.OverrideMap.TryGetValue(dictKey, out var inner))
        {
            foreach (var kvp in inner)
                if (string.Equals(kvp.Value, chinese, StringComparison.Ordinal)) return kvp.Key;
        }
        return null;
    }

    /// <summary>参数表字典全量 Key（OverrideMap 注入，含配置新增；未注入降级静态常量兜底）</summary>
    private static string[] GetDictBaseKeys(string dictKey, string[] fallback)
    {
        if (DictValueDisplayHelper.OverrideMap != null
            && DictValueDisplayHelper.OverrideMap.TryGetValue(dictKey, out var inner))
            return inner.Keys.ToArray();
        return fallback;
    }

    /// <summary>
    /// 逗号分隔列表字段：SectionName（生产工段）/ GroupName（工序组，可多工序）/ InspectionItems（成检项目资质）
    /// MaterialReceiveCheckItems 为布尔开关（是否属于成检到料确认人），走通用 bool 筛选
    /// </summary>
    private static readonly string[] CommaListFields =
    {
        nameof(Employee.SectionName),
        nameof(Employee.GroupName),
        nameof(Employee.InspectionItems)
    };

    /// <summary>
    /// 员工通用筛选：逗号分隔列表字段（SectionName 多工段、GroupName 工序组、InspectionItems 多检验项目资质）
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
                AttendanceCoefficient = e.AttendanceCoefficient,
                HourlyWage = e.HourlyWage,
                DailyWage = e.DailyWage,
                MonthlyWage = e.MonthlyWage,
                SectionName = e.SectionName,
                GroupName = e.GroupName,
                InspectionItems = e.InspectionItems,
                MaterialReceiveCheckItems = e.MaterialReceiveCheckItems,
                IsActive = e.IsActive
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> SaveAsync(EmployeeDto dto)
    {
        var isNew = dto.Id <= 0;
        var code = (dto.Code ?? string.Empty).Trim();

        // 与账号创建同一 DbContext 事务，保证「建同步建」原子性（账号创建失败回滚员工新增）
        await using var tx = await _context.Database.BeginTransactionAsync();

        if (!isNew)
        {
            // 更新
            var entity = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == dto.Id);

            if (entity == null)
                throw new BusinessException("员工不存在");

            entity.Code = code;
            entity.Name = dto.Name;
            entity.Department = dto.Department;
            entity.Position = dto.Position;
            entity.PositionRemark = dto.PositionRemark;
            entity.SalaryMode = dto.SalaryMode;
            entity.SalaryRemark = dto.SalaryRemark;
            entity.AttendanceCoefficient = dto.AttendanceCoefficient;
            entity.HourlyWage = dto.HourlyWage;
            entity.DailyWage = dto.DailyWage;
            entity.MonthlyWage = dto.MonthlyWage;
            entity.SectionName = dto.SectionName;
            entity.GroupName = dto.GroupName;
            entity.InspectionItems = dto.InspectionItems;
            entity.MaterialReceiveCheckItems = dto.MaterialReceiveCheckItems;
            entity.IsActive = dto.IsActive;
        }
        else
        {
            // 新增
            var entity = new Employee
            {
                Code = code,
                Name = dto.Name,
                Department = dto.Department,
                Position = dto.Position,
                PositionRemark = dto.PositionRemark,
                SalaryMode = dto.SalaryMode,
                SalaryRemark = dto.SalaryRemark,
                AttendanceCoefficient = dto.AttendanceCoefficient,
                HourlyWage = dto.HourlyWage,
                DailyWage = dto.DailyWage,
                MonthlyWage = dto.MonthlyWage,
                SectionName = dto.SectionName,
                GroupName = dto.GroupName,
                InspectionItems = dto.InspectionItems,
                MaterialReceiveCheckItems = dto.MaterialReceiveCheckItems,
                IsActive = dto.IsActive
            };
            _context.Employees.Add(entity);
        }

        await _context.SaveChangesAsync();

        // 建同步建：新增员工自动创建最小扫码账号（用户名=工号、密码=123456、仅 ScanViewer）；工号已存在账号则跳过
        if (isNew && !string.IsNullOrWhiteSpace(code))
            await EnsureAccountAsync(code, dto.Name);

        await tx.CommitAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id);

        if (entity == null)
            throw new BusinessException("员工不存在");

        var code = entity.Code;

        await using var tx = await _context.Database.BeginTransactionAsync();

        _context.Employees.Remove(entity);
        await _context.SaveChangesAsync();

        // 删同步删：删除员工同时删除其自动创建的登录账号（用户名=工号）；账号不存在则跳过，删除失败不阻塞员工删除
        if (!string.IsNullOrWhiteSpace(code))
        {
            var userId = await _userManagementService.FindIdByUserNameAsync(code);
            if (userId != null)
                await _userManagementService.DeleteAsync(userId);
        }

        await tx.CommitAsync();
        return true;
    }

    /// <summary>
    /// 一键补齐存量启用员工的登录账号（用户名=工号、密码=123456、仅 ScanViewer 最小扫码权限）。
    /// 已存在账号跳过；返回本次新建账号数。
    /// </summary>
    public async Task<int> SyncAccountsAsync()
    {
        var employees = await _context.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new { e.Code, e.Name })
            .ToListAsync();

        var created = 0;
        foreach (var emp in employees)
        {
            if (string.IsNullOrWhiteSpace(emp.Code)) continue;
            if (await _userManagementService.FindIdByUserNameAsync(emp.Code) != null) continue;
            var result = await _userManagementService.CreateAsync(CreateAccountRequest(emp.Code, emp.Name));
            if (result.Success) created++;
        }
        return created;
    }

    /// <summary>为单个员工创建最小扫码登录账号（用户名=工号）；已存在则跳过，创建失败抛业务异常（回滚员工新增）</summary>
    private async Task EnsureAccountAsync(string code, string? name)
    {
        if (await _userManagementService.FindIdByUserNameAsync(code) != null) return;
        var result = await _userManagementService.CreateAsync(CreateAccountRequest(code, name));
        if (!result.Success)
            throw new BusinessException($"创建登录账号失败（用户名={code}）: {result.Message}");
    }

    private static CreateUserRequest CreateAccountRequest(string code, string? name) => new()
    {
        UserName = code,
        Password = DefaultAccountPassword,
        FullName = name,
        Remark = "扫码员工自动创建",
        Roles = new List<string> { Roles.Menus.ScanViewer }
    };

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

        // 工序组选项 = 标准工序 + 存量工序片段中非标准值（员工工序组为逗号串多工序 Key）
        // 2026-08-31 修复：原直接取整串去重，导致列头筛选下拉显示英文逗号串（如 ColdRoll30,ColdRoll20）且无法按单工序筛选
        var processValues = rows.Select(r => r.GroupName)
            .Where(v => !string.IsNullOrEmpty(v))
            .SelectMany(v => v!.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var processOptions = ProcessKeys.All
            .Concat(processValues.Where(v => !ProcessKeys.All.Contains(v, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        // 岗位选项 = 参数表 PositionKey 全量 Key（OverrideMap，含配置新增）+ 存量非标准值；OverrideMap 未注入降级常量类
        var positionBase = GetDictBaseKeys(DictValueDefaults.PositionKey, PositionKeys.All);
        var positionOptions = positionBase
            .Concat(rows.Select(r => r.Position)
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!.Trim())
                .Where(v => v.Length > 0)
                .Where(v => !positionBase.Contains(v, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        // 岗位类别选项 = 参数表 PositionCategoryKey 全量 Key（含配置新增）+ 存量非标准值
        var categoryBase = GetDictBaseKeys(DictValueDefaults.PositionCategoryKey, PositionCategoryKeys.All);
        var categoryOptions = categoryBase
            .Concat(rows.Select(r => r.Department)
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!.Trim())
                .Where(v => v.Length > 0)
                .Where(v => !categoryBase.Contains(v, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        return new Dictionary<string, List<string>>
        {
            ["Department"] = categoryOptions,
            ["Position"] = positionOptions,
            ["Code"] = Distinct(rows.Select(r => r.Code)),
            ["Name"] = Distinct(rows.Select(r => r.Name)),
            ["SectionName"] = sectionOptions,
            ["GroupName"] = processOptions,
            ["MaterialReceiveCheckItems"] = new List<string> { "True", "False" },
            ["InspectionItems"] = Enum.GetValues<InspectionItem>().Select(e => e.ToString()).ToList(),
            ["IsActive"] = new List<string> { "True", "False" },
            ["PositionRemark"] = Distinct(rows.Select(r => r.PositionRemark)),
            ["SalaryMode"] = Enum.GetNames<SalaryMode>().ToList(),
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
