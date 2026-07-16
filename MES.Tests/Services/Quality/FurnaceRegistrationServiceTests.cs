using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
using MES.Core.Enums;
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
using MES.Services.Quality;
using MES.Tests.Tests;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 来料炉号登记服务测试：CRUD、关键字搜索、牌号映射查询、化学成分验证
/// </summary>
public class FurnaceRegistrationServiceTests : TestBase
{
    private FurnaceRegistrationService CreateService(AppDbContext ctx)
    {
        var ruleServiceMock = new Moq.Mock<IChemicalValidationRuleService>();
        return new(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<FurnaceRegistrationService>.Instance, ruleServiceMock.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    private async Task SeedFurnaceAsync(AppDbContext ctx, string furnaceNo = "FUR001",
        string unit = "钢厂A", string grade = "Q345B")
    {
        ctx.FurnaceRegistrations.Add(new FurnaceRegistration
        {
            IncomingDate = DateTime.Today,
            RawMaterialUnit = unit,
            RawMaterialType = "RoughTube",
            RegisteredGrade = grade,
            RelatedPlantGrade = grade,
            FurnaceNumber = furnaceNo,
            Specification = "219*8",
            Quantity = 10,
            Weight = 1000m
        });
        await ctx.SaveChangesAsync();
    }

    // ========== GetAllAsync ==========

    [Fact]
    public async Task GetAllAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_按炉号搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR001");
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR002");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "FUR001" });

        result.Items.Should().HaveCount(1);
        result.Items[0].FurnaceNumber.Should().Be("FUR001");
    }

    [Fact]
    public async Task GetAllAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx, furnaceNo: "B-FUR");
        await SeedFurnaceAsync(ctx, furnaceNo: "A-FUR");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "furnacenumber", IsDescending = false });

        result.Items[0].FurnaceNumber.Should().Be("A-FUR");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx);
        var id = await ctx.FurnaceRegistrations.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result!.FurnaceNumber.Should().Be("FUR001");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(999);

        result.Should().BeNull();
    }

    // ========== BatchCreateAsync ==========

    [Fact]
    public async Task BatchCreateAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateFurnaceRegistrationRequest>
        {
            new()
            {
                IncomingDate = DateTime.Today, RawMaterialUnit = "钢厂A", RawMaterialType = RawMaterialType.RoughTube,
                RegisteredGrade = "Q345B", FurnaceNumber = "FUR001", Quantity = 10, Weight = 1000m
            },
            new()
            {
                IncomingDate = DateTime.Today, RawMaterialUnit = "钢厂B", RawMaterialType = RawMaterialType.RoughTube,
                RegisteredGrade = "20#", FurnaceNumber = "FUR002", Quantity = 20, Weight = 2000m
            }
        });

        result.Should().HaveCount(2);
        result[0].FurnaceNumber.Should().Be("FUR001");
    }

    [Fact]
    public async Task BatchCreateAsync_空列表_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateFurnaceRegistrationRequest>());

        result.Should().BeEmpty();
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx);
        var id = await ctx.FurnaceRegistrations.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateFurnaceRegistrationRequest
        {
            IncomingDate = DateTime.Today,
            RawMaterialUnit = "新钢厂",
            RawMaterialType = RawMaterialType.RoughTube,
            RegisteredGrade = "304",
            FurnaceNumber = "FUR001-NEW",
            Quantity = 15
        });

        result.RawMaterialUnit.Should().Be("新钢厂");
        result.Quantity.Should().Be(15);
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateFurnaceRegistrationRequest
        {
            IncomingDate = DateTime.Today,
            RawMaterialUnit = "钢厂",
            RawMaterialType = RawMaterialType.RoughTube,
            RegisteredGrade = "Q345B",
            FurnaceNumber = "FUR999"
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx);
        var id = await ctx.FurnaceRegistrations.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.FurnaceRegistrations.FindAsync(id);
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

    // ========== LookupPlantGradeAsync ==========

    [Fact]
    public async Task LookupPlantGradeAsync_存在映射_返回工厂牌号()
    {
        var ctx = CreateDbContext();
        ctx.StandardGradeMappings.Add(new StandardGradeMapping
        {
            StandardGrade = "Q345B",
            PlantGrade = "Q345B-Plant",
            Density = 7.85m
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.LookupPlantGradeAsync("Q345B");

        result.Should().Be("Q345B-Plant");
    }

    [Fact]
    public async Task LookupPlantGradeAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.LookupPlantGradeAsync("NONEXISTENT");

        result.Should().BeNull();
    }

    [Fact]
    public async Task LookupPlantGradeAsync_空参数_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.LookupPlantGradeAsync("");

        result.Should().BeNull();
    }

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetAllAsync_关键词搜索规格_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.FurnaceRegistrations.Add(new FurnaceRegistration
        {
            IncomingDate = DateTime.Today,
            RawMaterialUnit = "钢厂A",
            RawMaterialType = "RoughTube",
            RegisteredGrade = "Q345B",
            RelatedPlantGrade = "Q345B",
            FurnaceNumber = "FUR-SPEC",
            Specification = "325*12",
            Quantity = 10,
            Weight = 1000m
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "325*12" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Specification.Should().Be("325*12");
    }

    [Fact]
    public async Task GetAllAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.FurnaceRegistrations.Add(new FurnaceRegistration
        {
            IncomingDate = DateTime.Today,
            RawMaterialUnit = "钢厂A",
            RawMaterialType = "RoughTube",
            RegisteredGrade = "Q345B",
            RelatedPlantGrade = "Q345B",
            FurnaceNumber = "FUR-REMARK",
            Specification = "219*8",
            Quantity = 10,
            Weight = 1000m,
            Remark = "炉号备注信息"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "炉号备注" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Remark.Should().Be("炉号备注信息");
    }

    // ========== B10 专项测试 ==========

    [Fact]
    public async Task GetAllAsync_按炉号排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR-B", grade: "Q345B");
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR-A", grade: "Q235B");
        var svc = CreateService(ctx);

        var resultAsc = await svc.GetAllAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "furnacenumber", IsDescending = false });

        resultAsc.Items[0].FurnaceNumber.Should().Be("FUR-A");
        resultAsc.Items[1].FurnaceNumber.Should().Be("FUR-B");
    }

    [Fact]
    public async Task GetAllAsync_按相关牌号排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR-001", grade: "B-Grade");
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR-002", grade: "A-Grade");
        var svc = CreateService(ctx);

        var resultAsc = await svc.GetAllAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "relatedplantgrade", IsDescending = false });

        resultAsc.Items[0].RelatedPlantGrade.Should().Be("A-Grade");
        resultAsc.Items[1].RelatedPlantGrade.Should().Be("B-Grade");
    }

    // ========== 筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetAllAsync_Filters_RawMaterialUnitContains_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR001", unit: "钢厂A");
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR002", unit: "钢厂B");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "RawMaterialUnit", Operator = "contains", Value = "钢厂A" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].RawMaterialUnit.Should().Be("钢厂A");
    }

    [Fact]
    public async Task GetAllAsync_Filters_FurnaceNumberIn_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR001");
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR002");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "FurnaceNumber", Operator = "in", Values = new List<string> { "FUR001" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].FurnaceNumber.Should().Be("FUR001");
    }

    [Fact]
    public async Task GetAllAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "FurnaceNumber", Operator = "contains", Value = "NONEXISTENT" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR001", unit: "钢厂A", grade: "Q345B");
        await SeedFurnaceAsync(ctx, furnaceNo: "FUR002", unit: "钢厂B", grade: "20#");
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("RawMaterialUnit");
        contexts["RawMaterialUnit"].Should().BeEquivalentTo(new[] { "钢厂A", "钢厂B" }, opts => opts.WithStrictOrdering());
        contexts.Should().ContainKey("FurnaceNumber");
        contexts["FurnaceNumber"].Should().BeEquivalentTo(new[] { "FUR001", "FUR002" }, opts => opts.WithStrictOrdering());
        contexts["RegisteredGrade"].Should().BeEquivalentTo(new[] { "20#", "Q345B" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["RawMaterialUnit"].Should().BeEmpty();
        contexts["FurnaceNumber"].Should().BeEmpty();
        contexts["RegisteredGrade"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        ctx.FurnaceRegistrations.Add(new FurnaceRegistration
        {
            IncomingDate = DateTime.Today,
            RawMaterialUnit = "钢厂A",
            RawMaterialType = "RoughTube",
            RegisteredGrade = "Q345B",
            FurnaceNumber = "FUR001",
            Specification = null,
            RelatedPlantGrade = null,
            Remark = null,
            Quantity = 10,
            Weight = 1000m
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["FurnaceNumber"].Should().HaveCount(1);
        contexts["Specification"].Should().BeEmpty();
        contexts["Remark"].Should().BeEmpty();
    }
}
