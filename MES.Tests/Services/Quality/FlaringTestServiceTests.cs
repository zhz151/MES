using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MES.Core.DTOs.Quality;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Quality;
using MES.Services.Quality;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 扩口检验服务测试：CRUD、关键字/日期范围过滤、null 补丁更新、缺失抛业务异常、批量创建、缓存筛选上下文去重。
/// </summary>
public class FlaringTestServiceTests : TestBase
{
    private static FlaringTestService CreateService(AppDbContext ctx)
        => new(ctx, NullLogger<FlaringTestService>.Instance, new MemoryCache(new MemoryCacheOptions()));

    private static async Task<FlaringTest> SeedAsync(AppDbContext ctx, DateTime date, string furnaceNo = "FUR-1",
        string inspector = "张三", string grade = "Q345B", string specification = "219*8",
        string? mandrelTaper = null, decimal? flaredDiameter = null)
    {
        var e = new FlaringTest
        {
            InspectionDate = date,
            Inspector = inspector,
            FurnaceNo = furnaceNo,
            Grade = grade,
            Specification = specification,
            MandrelTaper = mandrelTaper,
            FlaredDiameter = flaredDiameter
        };
        ctx.FlaringTests.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static CreateFlaringTestRequest NewCreate(string furnaceNo = "FUR-1") => new()
    {
        InspectionDate = new DateTime(2026, 3, 1),
        Inspector = "张三",
        FurnaceNo = furnaceNo,
        Grade = "Q345B",
        Specification = "219*8",
        MandrelTaper = "1:30",
        FlaredDiameter = 300m
    };

    // ========== CreateAsync / GetByIdAsync ==========

    [Fact]
    public async Task CreateAsync_新增_落库返回Dto()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = await svc.CreateAsync(NewCreate("FUR-100"));

        dto.Id.Should().BeGreaterThan(0);
        dto.FurnaceNo.Should().Be("FUR-100");
        dto.MandrelTaper.Should().Be("1:30");
        dto.FlaredDiameter.Should().Be(300m);
        var row = await ctx.FlaringTests.SingleAsync();
        row.FurnaceNo.Should().Be("FUR-100");
    }

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, new DateTime(2026, 3, 1), furnaceNo: "FUR-7");

        var dto = await CreateService(ctx).GetByIdAsync(e.Id);

        dto.Should().NotBeNull();
        dto!.FurnaceNo.Should().Be("FUR-7");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();

        var dto = await CreateService(ctx).GetByIdAsync(99999);

        dto.Should().BeNull();
    }

    // ========== GetAllAsync：空 / 关键字 / 日期范围 ==========

    [Fact]
    public async Task GetAllAsync_无数据_返回空()
    {
        var ctx = CreateDbContext();

        var page = await CreateService(ctx).GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_关键字命中检验员或炉号()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, new DateTime(2026, 3, 1), furnaceNo: "FUR-1", inspector: "张三");
        await SeedAsync(ctx, new DateTime(2026, 3, 2), furnaceNo: "FUR-2", inspector: "李四");
        var svc = CreateService(ctx);

        var byFurnace = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "FUR-2" });
        byFurnace.Items.Should().ContainSingle().Which.FurnaceNo.Should().Be("FUR-2");

        var byInspector = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "李四" });
        byInspector.Items.Should().ContainSingle().Which.Inspector.Should().Be("李四");
    }

    [Fact]
    public async Task GetAllAsync_关键字命中可空特有列()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, new DateTime(2026, 3, 1), furnaceNo: "FUR-1");
        await SeedAsync(ctx, new DateTime(2026, 3, 2), furnaceNo: "FUR-2");
        await SeedAsync(ctx, new DateTime(2026, 3, 3), furnaceNo: "FUR-3", mandrelTaper: "1:50");
        var svc = CreateService(ctx);

        var hit = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "1:50" });

        hit.Items.Should().ContainSingle().Which.FurnaceNo.Should().Be("FUR-3");
    }

    [Fact]
    public async Task GetAllAsync_日期范围过滤_闭区间()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, new DateTime(2026, 3, 1), furnaceNo: "FUR-1");
        await SeedAsync(ctx, new DateTime(2026, 3, 15), furnaceNo: "FUR-2");
        await SeedAsync(ctx, new DateTime(2026, 4, 1), furnaceNo: "FUR-3");
        var svc = CreateService(ctx);

        var page = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            InspectionDateFrom = new DateTime(2026, 3, 1),
            InspectionDateTo = new DateTime(2026, 3, 15)
        });

        page.TotalCount.Should().Be(2);
        page.Items.Select(i => i.FurnaceNo).Should().BeEquivalentTo("FUR-1", "FUR-2");
    }

    // ========== UpdateAsync：null 补丁 / 缺失 ==========

    [Fact]
    public async Task UpdateAsync_部分字段更新_未提供字段保持原值()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, new DateTime(2026, 3, 1), furnaceNo: "FUR-1", inspector: "张三",
            mandrelTaper: "1:30");
        var svc = CreateService(ctx);

        var dto = await svc.UpdateAsync(e.Id, new UpdateFlaringTestRequest
        {
            InspectionDate = new DateTime(2026, 3, 2),
            FurnaceNo = "FUR-1-NEW",
            FlaredDiameter = 320m
            // Inspector/Grade/Specification/MandrelTaper 传 null → 回退保留
        });

        dto.FurnaceNo.Should().Be("FUR-1-NEW");
        dto.InspectionDate.Should().Be(new DateTime(2026, 3, 2));
        dto.FlaredDiameter.Should().Be(320m);
        dto.Inspector.Should().Be("张三");
        dto.Grade.Should().Be("Q345B");
        dto.MandrelTaper.Should().Be("1:30");
    }

    [Fact]
    public async Task UpdateAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.UpdateAsync(99999, new UpdateFlaringTestRequest
        {
            InspectionDate = new DateTime(2026, 3, 1)
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*扩口检验记录不存在*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, new DateTime(2026, 3, 1));
        var svc = CreateService(ctx);

        await svc.DeleteAsync(e.Id);

        ctx.FlaringTests.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.DeleteAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*扩口检验记录不存在*");
    }

    // ========== BatchCreateAsync ==========

    [Fact]
    public async Task BatchCreateAsync_批量落库()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var list = await svc.BatchCreateAsync(new List<CreateFlaringTestRequest>
        {
            NewCreate("FUR-A1"), NewCreate("FUR-A2")
        });

        list.Should().HaveCount(2);
        list.Select(d => d.FurnaceNo).Should().Equal("FUR-A1", "FUR-A2");
        ctx.FlaringTests.Count().Should().Be(2);
    }

    [Fact]
    public async Task BatchCreateAsync_空列表_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var list = await svc.BatchCreateAsync(new List<CreateFlaringTestRequest>());

        list.Should().BeEmpty();
        ctx.FlaringTests.Should().BeEmpty();
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回去重有序上下文()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, new DateTime(2026, 3, 1), furnaceNo: "FUR-1", inspector: "张三");
        await SeedAsync(ctx, new DateTime(2026, 3, 2), furnaceNo: "FUR-2", inspector: "张三");
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["Inspector"].Should().Equal("张三");
        contexts["FurnaceNo"].Should().Equal("FUR-1", "FUR-2");
        contexts.Should().ContainKey("MandrelTaper");
        contexts.Should().ContainKey("Judgment");
    }
}
