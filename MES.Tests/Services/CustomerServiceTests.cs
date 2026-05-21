using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 客户档案服务测试：CRUD、关键字搜索、排序
/// </summary>
public class CustomerServiceTests : TestBase
{
    private CustomerService CreateService(AppDbContext ctx) => new(ctx, null!);

    private async Task SeedCustomerAsync(AppDbContext ctx, string code = "C001", string unit = "测试客户",
        string salesman = "张三", CustomerStatus status = CustomerStatus.Active)
    {
        ctx.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerCode = code,
            CustomerUnit = unit,
            Salesman = salesman,
            Status = status
        });
        await ctx.SaveChangesAsync();
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
    public async Task GetPagedAsync_按客户编码搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, code: "C001");
        await SeedCustomerAsync(ctx, code: "C002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "C001" });

        result.Items.Should().HaveCount(1);
        result.Items[0].CustomerCode.Should().Be("C001");
    }

    [Fact]
    public async Task GetPagedAsync_按客户单位搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, unit: "大明钢铁");
        await SeedCustomerAsync(ctx, unit: "宝钢");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "大明" });

        result.Items.Should().HaveCount(1);
        result.Items[0].CustomerUnit.Should().Be("大明钢铁");
    }

    [Fact]
    public async Task GetPagedAsync_按业务员搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, salesman: "张三");
        await SeedCustomerAsync(ctx, salesman: "李四");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "张三" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Salesman.Should().Be("张三");
    }

    [Fact]
    public async Task GetPagedAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_删除后不显示()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx);
        var id = await ctx.CustomerProfiles.Select(c => c.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_按客户编码排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, code: "B001");
        await SeedCustomerAsync(ctx, code: "A001");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "CustomerCode", IsDescending = false });

        result.Items[0].CustomerCode.Should().Be("A001");
        result.Items[1].CustomerCode.Should().Be("B001");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx);
        var id = await ctx.CustomerProfiles.Select(c => c.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result.CustomerCode.Should().Be("C001");
        result.CustomerUnit.Should().Be("测试客户");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("客户不存在");
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建客户()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateCustomerRequest
        {
            CustomerCode = "C100",
            CustomerUnit = "新客户",
            Salesman = "王五",
            Status = CustomerStatus.Active
        });

        result.Should().NotBeNull();
        result.CustomerCode.Should().Be("C100");
        result.CustomerUnit.Should().Be("新客户");
        result.Salesman.Should().Be("王五");
        result.Status.Should().Be(CustomerStatus.Active);

        var saved = await ctx.CustomerProfiles.FirstAsync();
        saved.CustomerCode.Should().Be("C100");
    }

    [Fact]
    public async Task CreateAsync_重复编码_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, code: "C001");
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateCustomerRequest
        {
            CustomerCode = "C001",
            CustomerUnit = "重复客户",
            Salesman = "张三"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*already exists*");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新客户()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx);
        var id = await ctx.CustomerProfiles.Select(c => c.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateCustomerRequest
        {
            CustomerUnit = "更新单位",
            ContactPerson = "李经理"
        });

        result.CustomerUnit.Should().Be("更新单位");

        var saved = await ctx.CustomerProfiles.FirstAsync(c => c.Id == id);
        saved.CustomerUnit.Should().Be("更新单位");
        saved.ContactPerson.Should().Be("李经理");
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateCustomerRequest { CustomerUnit = "新名称" });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("客户不存在");
    }

    [Fact]
    public async Task UpdateAsync_重复编码_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, code: "C001");
        await SeedCustomerAsync(ctx, code: "C002");
        var id = await ctx.CustomerProfiles
            .Where(c => c.CustomerCode == "C001")
            .Select(c => c.Id)
            .FirstAsync();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(id, new UpdateCustomerRequest { CustomerCode = "C002" });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已存在*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx);
        var id = await ctx.CustomerProfiles.Select(c => c.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.CustomerProfiles.FindAsync(id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("客户不存在");
    }

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_关键词搜索联系人_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerCode = "C-CONTACT",
            CustomerUnit = "联系人测试客户",
            ContactPerson = "李经理",
            Salesman = "测试业务员",
            Status = CustomerStatus.Active
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "李经理" });

        result.Items.Should().HaveCount(1);
        result.Items[0].ContactPerson.Should().Be("李经理");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索地址_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerCode = "C-ADDR",
            CustomerUnit = "地址测试客户",
            Address = "北京市海淀区",
            Salesman = "测试业务员",
            Status = CustomerStatus.Active
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "海淀" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Address.Should().Be("北京市海淀区");
    }
}
