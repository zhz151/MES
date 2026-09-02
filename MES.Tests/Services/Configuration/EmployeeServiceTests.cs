using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Auth;
using MES.Core.Enums;
using MES.Core.Interfaces.Auth;
using MES.Shared.Constants;

namespace MES.Tests.Services;

/// <summary>
/// 员工管理服务测试：分页查询、按工号查询、新增/更新、删除、登录账号联动（建同步建/删同步删/一键补齐）
/// </summary>
public class EmployeeServiceTests : TestBase
{
    /// <summary>默认 mock：FindId 返回 null、Create 返回成功、Delete 返回成功</summary>
    private static Mock<IUserManagementService> CreateUserMock()
    {
        var mock = new Mock<IUserManagementService>();
        mock.Setup(x => x.FindIdByUserNameAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        mock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>()))
            .ReturnsAsync((CreateUserRequest r) => ApiResponse<UserDto>.Ok(new UserDto { UserName = r.UserName ?? "" }, "ok"));
        mock.Setup(x => x.DeleteAsync(It.IsAny<string>()))
            .ReturnsAsync(ApiResponse<object>.Ok(new object(), "ok"));
        return mock;
    }

    private EmployeeService CreateService(AppDbContext ctx) => new(ctx, CreateUserMock().Object);

    private EmployeeService CreateService(AppDbContext ctx, Mock<IUserManagementService> userMock) => new(ctx, userMock.Object);

    private async Task<Employee> SeedEmployeeAsync(AppDbContext ctx,
        string code = "EMP001", string name = "张三", bool isActive = true, string? sectionName = null, string? inspectionItems = null,
        bool? materialReceiveCheckItems = null, string? groupName = null, string? position = null, SalaryMode? salaryMode = null,
        string? department = null, decimal? attendanceCoefficient = null, decimal? hourlyWage = null, decimal? dailyWage = null, decimal? monthlyWage = null)
    {
        var emp = new Employee
        {
            Code = code,
            Name = name,
            Department = department ?? PositionCategoryKeys.Workshop,
            Position = position ?? PositionKeys.Cutting,
            SectionName = sectionName,
            InspectionItems = inspectionItems,
            MaterialReceiveCheckItems = materialReceiveCheckItems,
            GroupName = groupName,
            SalaryMode = salaryMode,
            AttendanceCoefficient = attendanceCoefficient,
            HourlyWage = hourlyWage,
            DailyWage = dailyWage,
            MonthlyWage = monthlyWage,
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

    // ========== SectionName（扫码操作人按工段过滤） ==========

    [Fact]
    public async Task GetPagedAsync_返回工段字段()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: SectionKeys.ColdRollDraw);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        result.Items[0].SectionName.Should().Be(SectionKeys.ColdRollDraw);
    }

    [Fact]
    public async Task GetByCodeAsync_返回工段字段()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: SectionKeys.Pickle);
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("EMP001");

        result.Should().NotBeNull();
        result!.SectionName.Should().Be(SectionKeys.Pickle);
    }

    [Fact]
    public async Task SaveAsync_新增_保存工段()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new EmployeeDto
        {
            Code = "EMP001",
            Name = "张三",
            Department = "生产部",
            SectionName = SectionKeys.Degrease
        });

        result.Should().BeTrue();

        var saved = await ctx.Employees.FirstAsync();
        saved.SectionName.Should().Be(SectionKeys.Degrease);
    }

    [Fact]
    public async Task SaveAsync_更新_修改工段()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, sectionName: SectionKeys.ColdRollDraw);
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new EmployeeDto
        {
            Id = emp.Id,
            Code = "EMP001",
            Name = "张三",
            Department = "生产部",
            SectionName = SectionKeys.Straighten
        });

        result.Should().BeTrue();

        var updated = await ctx.Employees.FindAsync(emp.Id);
        updated!.SectionName.Should().Be(SectionKeys.Straighten);
    }

    // ========== 多工段（逗号分隔）筛选 ==========

    [Fact]
    public async Task GetPagedAsync_多工段_按末尾工段命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "ColdRollDraw,Straighten");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SectionName", Operator = "equals", Value = "Straighten" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].SectionName.Should().Be("ColdRollDraw,Straighten");
    }

    [Fact]
    public async Task GetPagedAsync_多工段_按中间工段命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "Pickle,WeldingHead,Inspection");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SectionName", Operator = "equals", Value = "WeldingHead" }
            }
        });

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_多工段_子串不误匹配()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "WeldingHead");
        var svc = CreateService(ctx);

        // Welding ⊂ WeldingHead，但作为独立工段不应命中
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SectionName", Operator = "equals", Value = "Welding" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_单值工段_仍精确命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "Pickle");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SectionName", Operator = "equals", Value = "Pickle" }
            }
        });

        result.Items.Should().HaveCount(1);
    }

    // ========== 检验项目资质（InspectionItems 逗号分隔，成品检验扫码过滤） ==========

    [Fact]
    public async Task GetPagedAsync_返回检验项目字段()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: null, inspectionItems: "Ultrasonic,EddyCurrent");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        result.Items[0].InspectionItems.Should().Be("Ultrasonic,EddyCurrent");
    }

    [Fact]
    public async Task SaveAsync_保存检验项目()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new EmployeeDto
        {
            Code = "EMP001",
            Name = "张三",
            Department = "质检部",
            SectionName = "Inspection",
            InspectionItems = "Ultrasonic,EddyCurrent"
        });

        result.Should().BeTrue();

        var saved = await ctx.Employees.FirstAsync();
        saved.InspectionItems.Should().Be("Ultrasonic,EddyCurrent");
    }

    [Fact]
    public async Task GetPagedAsync_按检验项目_末尾项目命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, inspectionItems: "Ultrasonic,EddyCurrent");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "InspectionItems", Operator = "equals", Value = "EddyCurrent" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].InspectionItems.Should().Be("Ultrasonic,EddyCurrent");
    }

    [Fact]
    public async Task GetPagedAsync_按检验项目_未配置不命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, inspectionItems: "Ultrasonic");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "InspectionItems", Operator = "equals", Value = "EddyCurrent" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    // ========== 过程检验/成检到料（布尔开关：勾选=true 属于该环节操作人） ==========

    [Fact]
    public async Task GetPagedAsync_返回成检到料开关()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, materialReceiveCheckItems: false);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        result.Items[0].MaterialReceiveCheckItems.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_保存成检到料开关()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new EmployeeDto
        {
            Code = "EMP001",
            Name = "张三",
            Department = "质检部",
            SectionName = "Inspection",
            MaterialReceiveCheckItems = false
        });

        result.Should().BeTrue();

        var saved = await ctx.Employees.FirstAsync();
        saved.MaterialReceiveCheckItems.Should().BeFalse();
    }

    [Fact]
    public async Task GetPagedAsync_按成检到料为是_命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, materialReceiveCheckItems: true);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "MaterialReceiveCheckItems", Operator = "equals", Value = "True" }
            }
        });

        result.Items.Should().HaveCount(1);
    }

    // ========== 组类（GroupName 逗号分隔可多组，扫码先选组再选人） ==========

    [Fact]
    public async Task GetPagedAsync_返回组类字段()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "ColdRollDraw", groupName: "甲班");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        result.Items[0].GroupName.Should().Be("甲班");
    }

    [Fact]
    public async Task GetByCodeAsync_返回组类字段()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, groupName: "乙班");
        var svc = CreateService(ctx);

        var result = await svc.GetByCodeAsync("EMP001");

        result!.GroupName.Should().Be("乙班");
    }

    [Fact]
    public async Task SaveAsync_新增_保存组类()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new EmployeeDto
        {
            Code = "EMP001",
            Name = "张三",
            Department = "生产部",
            SectionName = SectionKeys.ColdRollDraw,
            GroupName = "甲班,乙班"
        });

        result.Should().BeTrue();

        var saved = await ctx.Employees.FirstAsync();
        saved.GroupName.Should().Be("甲班,乙班");
    }

    [Fact]
    public async Task SaveAsync_更新_修改组类()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, groupName: "甲班");
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new EmployeeDto
        {
            Id = 1,
            Code = "EMP001",
            Name = "张三",
            Department = "生产部",
            SectionName = SectionKeys.ColdRollDraw,
            GroupName = "乙班"
        });

        result.Should().BeTrue();

        var updated = await ctx.Employees.FirstAsync();
        updated.GroupName.Should().Be("乙班");
    }

    [Fact]
    public async Task GetPagedAsync_组类多组_按末尾组命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "ColdRollDraw", groupName: "甲班,乙班");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "GroupName", Operator = "equals", Value = "乙班" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].GroupName.Should().Be("甲班,乙班");
    }

    [Fact]
    public async Task GetPagedAsync_组类多组_按中间组命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "ColdRollDraw", groupName: "甲班,乙班,丙班");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "GroupName", Operator = "equals", Value = "乙班" }
            }
        });

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_组类_子串不误匹配()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "ColdRollDraw", groupName: "甲一班");
        var svc = CreateService(ctx);

        // 甲班 ⊂ 甲一班，但作为独立组类不应命中
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "GroupName", Operator = "equals", Value = "甲班" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_组类_工位未配置组类时按工段候选()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "ColdRollDraw"); // 未设组类
        var svc = CreateService(ctx);

        // 组类筛选传空 = 不过滤组类（「全部组」语义），仅按工段
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SectionName", Operator = "equals", Value = "ColdRollDraw" },
                new() { Field = "GroupName", Operator = "equals", Value = (string?)null }
            }
        });

        result.Items.Should().HaveCount(1);
    }

    // ========== 列头多选 in 筛选（逗号列表任一元素命中） ==========

    [Fact]
    public async Task GetPagedAsync_多工段_列头多选in_命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "ColdRollDraw,Straighten");
        await SeedEmployeeAsync(ctx, "EMP002", "李四", sectionName: "Pickle");
        var svc = CreateService(ctx);

        // ExcelFilter 走 Operator="in"，选项为单个工段，命中逗号列表任一元素
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SectionName", Operator = "in", Values = new List<string> { "Straighten" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].SectionName.Should().Be("ColdRollDraw,Straighten");
    }

    [Fact]
    public async Task GetPagedAsync_多工段_列头多选in_多选项任一命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "Straighten");
        await SeedEmployeeAsync(ctx, "EMP002", "李四", sectionName: "Pickle");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SectionName", Operator = "in", Values = new List<string> { "Straighten", "ColdRollDraw" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].SectionName.Should().Be("Straighten");
    }

    [Fact]
    public async Task GetPagedAsync_多工段_列头多选in_子串不误匹配()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "WeldingHead");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SectionName", Operator = "in", Values = new List<string> { "Welding" } }
            }
        });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_检验项目_列头多选in_命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, inspectionItems: "Ultrasonic,EddyCurrent");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "InspectionItems", Operator = "in", Values = new List<string> { "EddyCurrent" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].InspectionItems.Should().Be("Ultrasonic,EddyCurrent");
    }

    [Fact]
    public async Task GetPagedAsync_组类_列头多选in_命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "ColdRollDraw", groupName: "甲班,乙班");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "GroupName", Operator = "in", Values = new List<string> { "乙班" } }
            }
        });

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_组类_列头多选in_整串选项仍命中()
    {
        // 组类筛选上下文为存量整串选项，整串选项通过 in 也应精确命中
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "ColdRollDraw", groupName: "甲班,乙班");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "GroupName", Operator = "in", Values = new List<string> { "甲班,乙班" } }
            }
        });

        result.Items.Should().HaveCount(1);
    }

    // ========== GetFilterContextsAsync（列头筛选上下文） ==========

    [Fact]
    public async Task GetFilterContextsAsync_枚举与布尔列_返回完整选项()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        // 成检项目 = 枚举全部
        result["InspectionItems"].Should().HaveCount(Enum.GetValues<InspectionItem>().Length);
        result["InspectionItems"].Should().Contain(InspectionItem.Ultrasonic.ToString());
        // 布尔开关 = 是/否
        result["MaterialReceiveCheckItems"].Should().Equal("True", "False");
        result["IsActive"].Should().Equal("True", "False");
    }

    [Fact]
    public async Task GetFilterContextsAsync_自由文本列_取存量去重值()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "EMP001");
        await SeedEmployeeAsync(ctx, "EMP002");
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result["Department"].Should().Contain(PositionCategoryKeys.Workshop); // SeedEmployeeAsync 固定岗位类别（英文 Key）
        result["Code"].Should().Contain("EMP001");
        result["Code"].Should().Contain("EMP002");
        result["Code"].Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFilterContextsAsync_工段列_拆分逗号串取片段_补充标准工段()
    {
        // 员工工段为逗号串多工段，筛选上下文按片段提供选项（含存量非标准片段），供列头多选筛选
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "ColdRollDraw,Straighten");
        await SeedEmployeeAsync(ctx, "EMP002", "李四", sectionName: "自定段");
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result["SectionName"].Should().Contain(SectionKeys.ColdRollDraw); // 标准工段补充
        result["SectionName"].Should().Contain("Straighten"); // 存量片段拆分
        result["SectionName"].Should().Contain("自定段"); // 存量非标准片段补充
        result["SectionName"].Should().HaveCountGreaterThan(SectionKeys.All.Length);
    }

    [Fact]
    public async Task GetFilterContextsAsync_组类列_拆分逗号串取片段_补充标准工序()
    {
        // 员工工序组为逗号串多工序（工序英文 Key），筛选上下文按片段提供选项（含存量非标准片段），供列头多选筛选
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, sectionName: "ColdRollDraw", groupName: "ColdRoll60,ColdRoll20");
        await SeedEmployeeAsync(ctx, "EMP002", "李四", sectionName: "Pickle", groupName: "ColdRoll60,ColdRoll20");
        await SeedEmployeeAsync(ctx, "EMP003", "王五", sectionName: "Pickle", groupName: "自定工序");
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result["GroupName"].Should().Contain(ProcessKeys.ColdRoll60); // 标准工序补充
        result["GroupName"].Should().Contain(ProcessKeys.ColdRoll20); // 存量片段拆分
        result["GroupName"].Should().Contain("自定工序"); // 存量非标准片段补充
        result["GroupName"].Should().HaveCountGreaterThan(ProcessKeys.All.Length);
    }

    // ========== 工资结算模式（SalaryMode 枚举化）/ 岗位（Position 字典化） ==========

    [Fact]
    public async Task GetPagedAsync_按工资模式in筛选_命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, salaryMode: SalaryMode.PieceCollective);
        await SeedEmployeeAsync(ctx, "EMP002", "李四", salaryMode: SalaryMode.Hourly);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SalaryMode", Operator = "in", Values = new List<string> { "PieceCollective" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].SalaryMode.Should().Be(SalaryMode.PieceCollective);
    }

    [Fact]
    public async Task GetPagedAsync_按工资模式in筛选_未命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, salaryMode: SalaryMode.PieceIndividual);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SalaryMode", Operator = "in", Values = new List<string> { "Fixed" } }
            }
        });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_返回工资模式与岗位字段()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, position: PositionKeys.Cutting, salaryMode: SalaryMode.PieceCollective);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        result.Items[0].SalaryMode.Should().Be(SalaryMode.PieceCollective);
        result.Items[0].Position.Should().Be(PositionKeys.Cutting);
    }

    [Fact]
    public async Task SaveAsync_新增_保存工资模式与岗位()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new EmployeeDto
        {
            Code = "EMP001",
            Name = "张三",
            Department = "生产部",
            Position = PositionKeys.FinishedInspection,
            SalaryMode = SalaryMode.PieceCollective
        });

        result.Should().BeTrue();

        var saved = await ctx.Employees.FirstAsync();
        saved.Position.Should().Be(PositionKeys.FinishedInspection);
        saved.SalaryMode.Should().Be(SalaryMode.PieceCollective);
    }

    [Fact]
    public async Task GetPagedAsync_按中文岗位搜索_命中对应Key员工()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, position: PositionKeys.Cutting);
        await SeedEmployeeAsync(ctx, "EMP002", "李四", position: PositionKeys.Grinding);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "切割" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Code.Should().Be("EMP001");
    }

    [Fact]
    public async Task GetFilterContextsAsync_工资模式列_返回6枚举名()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result["SalaryMode"].Should().HaveCount(Enum.GetNames<SalaryMode>().Length);
        result["SalaryMode"].Should().Contain(SalaryMode.PieceCollective.ToString());
        result["SalaryMode"].Should().Contain(SalaryMode.Fixed.ToString());
    }

    [Fact]
    public async Task GetFilterContextsAsync_岗位列_返回标准14岗位()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result["Position"].Should().HaveCount(PositionKeys.All.Length);
        result["Position"].Should().Contain(PositionKeys.Cutting);
        result["Position"].Should().Contain(PositionKeys.FinishedInspection);
    }

    // ========== 岗位类别（Department 字典化，PositionCategoryKey）/ 工资字段（靠工系数/小时/日/月工资） ==========

    [Fact]
    public async Task GetPagedAsync_按岗位类别in筛选_命中()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, department: PositionCategoryKeys.Workshop);
        await SeedEmployeeAsync(ctx, "EMP002", "李四", department: PositionCategoryKeys.QualityInspection);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "Department", Operator = "in", Values = new List<string> { PositionCategoryKeys.Workshop } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].Code.Should().Be("EMP001");
    }

    [Fact]
    public async Task GetPagedAsync_按中文岗位类别搜索_命中对应Key员工()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, department: PositionCategoryKeys.Workshop);
        await SeedEmployeeAsync(ctx, "EMP002", "李四", department: PositionCategoryKeys.QualityInspection);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "车间生产" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Code.Should().Be("EMP001");
    }

    [Fact]
    public async Task GetFilterContextsAsync_岗位类别列_返回标准4类()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result["Department"].Should().HaveCount(PositionCategoryKeys.All.Length);
        result["Department"].Should().Contain(PositionCategoryKeys.Workshop);
        result["Department"].Should().Contain(PositionCategoryKeys.QualityInspection);
    }

    [Fact]
    public async Task GetPagedAsync_返回工资字段()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, salaryMode: SalaryMode.PieceAttendance,
            attendanceCoefficient: 1.2m, hourlyWage: 25m, dailyWage: 200m, monthlyWage: 5000m);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        result.Items[0].AttendanceCoefficient.Should().Be(1.2m);
        result.Items[0].HourlyWage.Should().Be(25m);
        result.Items[0].DailyWage.Should().Be(200m);
        result.Items[0].MonthlyWage.Should().Be(5000m);
    }

    [Fact]
    public async Task SaveAsync_新增_保存工资字段()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new EmployeeDto
        {
            Code = "EMP001",
            Name = "张三",
            Department = PositionCategoryKeys.Workshop,
            SalaryMode = SalaryMode.PieceAttendance,
            AttendanceCoefficient = 1.5m,
            HourlyWage = 30m,
            DailyWage = 240m,
            MonthlyWage = 6000m
        });

        result.Should().BeTrue();

        var saved = await ctx.Employees.FirstAsync();
        saved.Department.Should().Be(PositionCategoryKeys.Workshop);
        saved.AttendanceCoefficient.Should().Be(1.5m);
        saved.HourlyWage.Should().Be(30m);
        saved.DailyWage.Should().Be(240m);
        saved.MonthlyWage.Should().Be(6000m);
    }

    [Fact]
    public async Task SaveAsync_新增_靠工系数默认1点0()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.SaveAsync(new EmployeeDto
        {
            Code = "EMP001",
            Name = "张三",
            Department = PositionCategoryKeys.Workshop
        });

        result.Should().BeTrue();

        var saved = await ctx.Employees.FirstAsync();
        saved.AttendanceCoefficient.Should().Be(1.0m);
    }

    // ========== 登录账号联动（建同步建 / 删同步删 / 一键补齐） ==========

    [Fact]
    public async Task SaveAsync_新增_自动创建最小扫码账号()
    {
        var ctx = CreateDbContext();
        var userMock = CreateUserMock();
        var svc = CreateService(ctx, userMock);

        await svc.SaveAsync(new EmployeeDto { Code = "EMP001", Name = "张三", Department = "生产部" });

        userMock.Verify(x => x.CreateAsync(It.Is<CreateUserRequest>(r =>
            r.UserName == "EMP001"
            && r.Password == EmployeeService.DefaultAccountPassword
            && r.FullName == "张三"
            && r.Roles.SequenceEqual(new[] { Roles.Menus.ScanViewer }))), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_新增_账号已存在_跳过创建()
    {
        var ctx = CreateDbContext();
        var userMock = CreateUserMock();
        userMock.Setup(x => x.FindIdByUserNameAsync("EMP001")).ReturnsAsync("existing-user");
        var svc = CreateService(ctx, userMock);

        await svc.SaveAsync(new EmployeeDto { Code = "EMP001", Name = "张三" });

        userMock.Verify(x => x.CreateAsync(It.IsAny<CreateUserRequest>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_新增_账号创建失败_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var userMock = CreateUserMock();
        userMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>()))
            .ReturnsAsync(ApiResponse<UserDto>.Fail("创建失败"));
        var svc = CreateService(ctx, userMock);

        var act = () => svc.SaveAsync(new EmployeeDto { Code = "EMP001", Name = "张三" });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*创建登录账号失败*");
    }

    [Fact]
    public async Task SaveAsync_更新_不创建账号()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx);
        var userMock = CreateUserMock();
        var svc = CreateService(ctx, userMock);

        await svc.SaveAsync(new EmployeeDto { Id = emp.Id, Code = "EMP001", Name = "张三(改)" });

        userMock.Verify(x => x.CreateAsync(It.IsAny<CreateUserRequest>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_删除员工_同时删除账号()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "EMP001");
        var userMock = CreateUserMock();
        userMock.Setup(x => x.FindIdByUserNameAsync("EMP001")).ReturnsAsync("user-abc");
        var svc = CreateService(ctx, userMock);

        await svc.DeleteAsync(emp.Id);

        userMock.Verify(x => x.DeleteAsync("user-abc"), Times.Once);
        (await ctx.Employees.FindAsync(emp.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_删除员工_账号不存在_不删账号()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "EMP001");
        var userMock = CreateUserMock();
        var svc = CreateService(ctx, userMock);

        await svc.DeleteAsync(emp.Id);

        userMock.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SyncAccountsAsync_补齐缺失账号_返回新建数()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "EMP001", "张三");
        await SeedEmployeeAsync(ctx, "EMP002", "李四");
        await SeedEmployeeAsync(ctx, "EMP003", "王五", isActive: false); // 停用员工不补齐
        var userMock = CreateUserMock();
        // EMP001 已有账号；EMP002 缺失（需新建）；EMP003 停用（不处理）
        userMock.Setup(x => x.FindIdByUserNameAsync("EMP001")).ReturnsAsync("existing-user");
        var svc = CreateService(ctx, userMock);

        var created = await svc.SyncAccountsAsync();

        created.Should().Be(1);
        userMock.Verify(x => x.CreateAsync(It.Is<CreateUserRequest>(r => r.UserName == "EMP002")), Times.Once);
        userMock.Verify(x => x.CreateAsync(It.Is<CreateUserRequest>(r => r.UserName == "EMP001")), Times.Never);
        userMock.Verify(x => x.CreateAsync(It.Is<CreateUserRequest>(r => r.UserName == "EMP003")), Times.Never);
    }
}
