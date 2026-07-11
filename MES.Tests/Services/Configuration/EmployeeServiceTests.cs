using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;
using MES.Core.DTOs.Configuration;

namespace MES.Tests.Services;

/// <summary>
/// 员工管理服务测试：分页查询、按工号查询、新增/更新、删除
/// </summary>
public class EmployeeServiceTests : TestBase
{
    private EmployeeService CreateService(AppDbContext ctx) => new(ctx);

    private async Task<Employee> SeedEmployeeAsync(AppDbContext ctx,
        string code = "EMP001", string name = "张三", bool isActive = true)
    {
        var emp = new Employee
        {
            Code = code,
            Name = name,
            Department = "生产部",
            Position = "操作工",
            IsActive = isActive
        };
        ctx.Employees.Add(emp);
        await ctx.SaveChangesAsync();
        return emp;
    }

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_返回分页数据()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "EMP001", "张三");
        await SeedEmployeeAsync(ctx, "EMP002", "李四");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_按关键字搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "EMP001", "张三");
        await SeedEmployeeAsync(ctx, "EMP002", "李四");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "张三" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("张三");
    }

    [Fact]
    public async Task GetPagedAsync_默认排序为Code()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "EMP002", "李四");
        await SeedEmployeeAsync(ctx, "EMP001", "张三");
        var svc = CreateService(ctx);

        // IsDescending 默认为 true，Code 降序
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items[0].Code.Should().Be("EMP002");
        result.Items[1].Code.Should().Be("EMP001");
    }

    // ========== GetByCodeAsync ==========

    [Fact]
    public async Task GetByCodeAsync_存在_返回员工()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("EMP001");

        result.Should().NotBeNull();
        result!.Name.Should().Be("张三");
    }

    [Fact]
    public async Task GetByCodeAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("NONEXISTENT");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByCodeAsync_未启用_返回Null()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, isActive: false);
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("EMP001");

        result.Should().BeNull();
    }

    // ========== SaveAsync ==========

    [Fact]
    public async Task SaveAsync_新增_成功创建()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new EmployeeDto
        {
            Code = "EMP001",
            Name = "张三",
            Department = "生产部"
        });

        result.Should().BeTrue();

        var saved = await ctx.Employees.FirstAsync();
        saved.Name.Should().Be("张三");
    }

    [Fact]
    public async Task SaveAsync_更新_成功修改()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new EmployeeDto
        {
            Id = emp.Id,
            Code = "EMP001",
            Name = "张三(改)",
            Department = "质检部"
        });

        result.Should().BeTrue();

        var updated = await ctx.Employees.FindAsync(emp.Id);
        updated!.Name.Should().Be("张三(改)");
        updated.Department.Should().Be("质检部");
    }

    [Fact]
    public async Task SaveAsync_更新不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.SaveAsync(new EmployeeDto
        {
            Id = 999,
            Code = "EMP001",
            Name = "不存在"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.DeleteAsync(emp.Id);

        result.Should().BeTrue();
        var deleted = await ctx.Employees.FindAsync(emp.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }
}
