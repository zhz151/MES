using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.ProductionStandard;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Services.Materials;
using MES.Tests.Tests;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Materials;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 供应商服务测试：供应商CRUD、关键字搜索、激活查询
/// </summary>
public class SupplierServiceTests : TestBase
{
    private SupplierService CreateService(AppDbContext ctx) => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private async Task SeedSupplierAsync(AppDbContext ctx, string name = "测试供应商", string? code = null, string contact = "张三", string phone = "13800138000")
    {
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = code ?? $"S{Guid.NewGuid():N}"[..10],
            SupplierName = name,
            ContactPerson = contact,
            ContactPhone = phone,
            IsActive = true
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
    public async Task GetPagedAsync_按供应商名称搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "大明钢铁");
        await SeedSupplierAsync(ctx, name: "大明不锈钢");
        await SeedSupplierAsync(ctx, name: "宝钢");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "大明" });

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedAsync_按联系人搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "A公司", contact: "李四");
        await SeedSupplierAsync(ctx, name: "B公司", contact: "王五");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "李四" });

        result.Items.Should().HaveCount(1);
        result.Items[0].ContactPerson.Should().Be("李四");
    }

    [Fact]
    public async Task GetPagedAsync_按电话搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "A公司", phone: "13900001111");
        await SeedSupplierAsync(ctx, name: "B公司", phone: "13800002222");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "13900001111" });

        result.Items.Should().HaveCount(1);
        result.Items[0].ContactPhone.Should().Be("13900001111");
    }

    [Fact]
    public async Task GetPagedAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_按名称排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "B供应商");
        await SeedSupplierAsync(ctx, name: "A供应商");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "SupplierName", IsDescending = false });

        result.Items[0].SupplierName.Should().Be("A供应商");
        result.Items[1].SupplierName.Should().Be("B供应商");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx);
        var id = await ctx.SupplierProfiles.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result.SupplierName.Should().Be("测试供应商");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("供应商不存在");
    }

    // ========== GetActiveAsync ==========

    [Fact]
    public async Task GetActiveAsync_仅返回激活供应商()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "激活供应商");
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = $"S{Guid.NewGuid():N}"[..10],
            SupplierName = "停用供应商",
            IsActive = false
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetActiveAsync();

        result.Should().HaveCount(1);
        result[0].SupplierName.Should().Be("激活供应商");
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建供应商()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateSupplierRequest
        {
            SupplierName = "新供应商",
            ContactPerson = "王经理",
            ContactPhone = "13900009999",
            Address = "上海市",
            IsActive = true
        });

        result.Should().NotBeNull();
        result.SupplierName.Should().Be("新供应商");
        result.ContactPerson.Should().Be("王经理");

        var saved = await ctx.SupplierProfiles.FirstAsync(s => s.SupplierName == "新供应商");
        saved.SupplierName.Should().Be("新供应商");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新供应商()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx);
        var id = await ctx.SupplierProfiles.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateSupplierRequest
        {
            ContactPerson = "李经理",
            Remark = "优质供应商"
        });

        result.ContactPerson.Should().Be("李经理");

        var saved = await ctx.SupplierProfiles.FirstAsync(s => s.Id == id);
        saved.ContactPerson.Should().Be("李经理");
        saved.Remark.Should().Be("优质供应商");
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateSupplierRequest { SupplierName = "新名称" });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("供应商不存在");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx);
        var id = await ctx.SupplierProfiles.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.SupplierProfiles.FindAsync(id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("供应商不存在");
    }

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_关键词搜索地址_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = $"S{Guid.NewGuid():N}"[..10],
            SupplierName = "地址测试供应商",
            Address = "上海市浦东新区",
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "浦东" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Address.Should().Be("上海市浦东新区");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = $"S{Guid.NewGuid():N}"[..10],
            SupplierName = "备注测试供应商",
            Remark = "优质供应商备注",
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "优质供应商" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Remark.Should().Be("优质供应商备注");
    }

    // ========== 筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetPagedAsync_Filters_SupplierNameContains_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "大明钢铁");
        await SeedSupplierAsync(ctx, name: "宝钢");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SupplierName", Operator = "contains", Value = "大明" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].SupplierName.Should().Be("大明钢铁");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_SupplierCodeIn_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "供应商A", code: "SU0001");
        await SeedSupplierAsync(ctx, name: "供应商B", code: "SU0002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SupplierCode", Operator = "in", Values = new List<string> { "SU0002" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].SupplierName.Should().Be("供应商B");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SupplierName", Operator = "contains", Value = "NONEXISTENT" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_Filters_IsActiveIn_返回激活()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "激活供应商");
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = "SU9999",
            SupplierName = "停用供应商",
            IsActive = false
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "IsActive", Operator = "in", Values = new List<string> { "True" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].IsActive.Should().BeTrue();
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "供应商A", contact: "张三");
        await SeedSupplierAsync(ctx, name: "供应商B", contact: "李四");
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("SupplierName");
        contexts["SupplierName"].Should().BeEquivalentTo(new[] { "供应商A", "供应商B" }, opts => opts.WithStrictOrdering());
        contexts.Should().ContainKey("ContactPerson");
        contexts["ContactPerson"].Should().Contain("张三");
        contexts["ContactPerson"].Should().Contain("李四");
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["SupplierName"].Should().BeEmpty();
        contexts["SupplierCode"].Should().BeEmpty();
        contexts["ContactPerson"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = "SU0001",
            SupplierName = "测试供应商",
            IsActive = true,
            ContactPerson = null,
            Remark = null
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["SupplierName"].Should().HaveCount(1);
        contexts["ContactPerson"].Should().BeEmpty();
        contexts["Remark"].Should().BeEmpty();
    }
}
