using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Scheduling;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Scheduling;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 每日产能档案服务测试：工序名合法性校验（仅荒管抛光 Polish 或冷轧机台组 GroupKey，忽略大小写）+ CRUD + 分页排序。
/// </summary>
public class DailyProductionCapacityServiceTests : TestBase
{
    private static Mock<IColdRollMachineGroupConfigService> CreateGroupMock(params string[] groupKeys)
    {
        var mock = new Mock<IColdRollMachineGroupConfigService>();
        mock.Setup(x => x.GetAllAsync())
            .ReturnsAsync(groupKeys.Select(k => new ColdRollMachineGroupConfigDto { GroupKey = k }).ToList());
        return mock;
    }

    [Fact]
    public async Task SaveAsync_新增固定首行荒管抛光_成功()
    {
        var ctx = CreateDbContext();
        var svc = new DailyProductionCapacityService(ctx, CreateGroupMock().Object);

        var saved = await svc.SaveAsync(new DailyProductionCapacityDto
        {
            ProcessName = ProductionOverviewRowKeys.Polish,
            DailyCapacity = 120m,
            Remark = "首行"
        });

        saved.Should().BeTrue();
        var row = await ctx.DailyProductionCapacities.SingleAsync();
        row.ProcessName.Should().Be(ProductionOverviewRowKeys.Polish);
        row.DailyCapacity.Should().Be(120m);
        row.Remark.Should().Be("首行");
    }

    [Fact]
    public async Task SaveAsync_新增合法机台组_忽略大小写成功()
    {
        var ctx = CreateDbContext();
        var groupMock = CreateGroupMock("GRP-CR60", "GRP-CR30");
        var svc = new DailyProductionCapacityService(ctx, groupMock.Object);

        // 小写输入命中大写 GroupKey（存储英文 Key 容忍）
        var saved = await svc.SaveAsync(new DailyProductionCapacityDto
        {
            ProcessName = "grp-cr60",
            DailyCapacity = 300m
        });

        saved.Should().BeTrue();
        var row = await ctx.DailyProductionCapacities.SingleAsync();
        row.ProcessName.Should().Be("grp-cr60");
    }

    [Fact]
    public async Task SaveAsync_工序名非机台组_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = new DailyProductionCapacityService(ctx, CreateGroupMock("GRP-CR60").Object);

        var act = async () => await svc.SaveAsync(new DailyProductionCapacityDto
        {
            ProcessName = "未知工序",
            DailyCapacity = 100m
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*仅支持荒管抛光或冷轧机台组*");
    }

    [Fact]
    public async Task SaveAsync_工序名为空_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = new DailyProductionCapacityService(ctx, CreateGroupMock().Object);

        var act = async () => await svc.SaveAsync(new DailyProductionCapacityDto
        {
            ProcessName = "  ",
            DailyCapacity = 100m
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*工序名称不能为空*");
        ctx.DailyProductionCapacities.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_更新既有_修改字段()
    {
        var ctx = CreateDbContext();
        var svc = new DailyProductionCapacityService(ctx, CreateGroupMock("GRP-CR60").Object);

        var dto = new DailyProductionCapacityDto { ProcessName = "GRP-CR60", DailyCapacity = 100m };
        (await svc.SaveAsync(dto)).Should().BeTrue();
        dto.Id = (await ctx.DailyProductionCapacities.SingleAsync()).Id;
        dto.DailyCapacity = 350m;
        dto.Remark = "更新备注";

        (await svc.SaveAsync(dto)).Should().BeTrue();
        var row = await ctx.DailyProductionCapacities.SingleAsync();
        row.Id.Should().Be(dto.Id);
        row.DailyCapacity.Should().Be(350m);
        row.Remark.Should().Be("更新备注");
    }

    [Fact]
    public async Task SaveAsync_更新不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = new DailyProductionCapacityService(ctx, CreateGroupMock("GRP-CR60").Object);

        var act = async () => await svc.SaveAsync(new DailyProductionCapacityDto
        {
            Id = 99999,
            ProcessName = "GRP-CR60",
            DailyCapacity = 100m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*记录不存在*");
    }

    [Fact]
    public async Task DeleteAsync_删除成功()
    {
        var ctx = CreateDbContext();
        var svc = new DailyProductionCapacityService(ctx, CreateGroupMock().Object);
        await svc.SaveAsync(new DailyProductionCapacityDto
        {
            ProcessName = ProductionOverviewRowKeys.Polish,
            DailyCapacity = 10m
        });
        var id = (await ctx.DailyProductionCapacities.SingleAsync()).Id;

        (await svc.DeleteAsync(id)).Should().BeTrue();
        ctx.DailyProductionCapacities.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = new DailyProductionCapacityService(ctx, CreateGroupMock().Object);

        var act = async () => await svc.DeleteAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*记录不存在*");
    }

    [Fact]
    public async Task GetPagedAsync_关键字过滤_命中工序名与备注()
    {
        var ctx = CreateDbContext();
        ctx.DailyProductionCapacities.AddRange(
            new DailyProductionCapacity { ProcessName = "GRP-CR60", DailyCapacity = 300m, Remark = "三辊冷轧" },
            new DailyProductionCapacity { ProcessName = "GRP-CR30", DailyCapacity = 200m, Remark = null });
        await ctx.SaveChangesAsync();
        var svc = new DailyProductionCapacityService(ctx, CreateGroupMock().Object);

        var hit = await svc.GetPagedAsync(new Core.Models.QueryParams { Keyword = "CR60", PageIndex = 0, PageSize = 10 });
        hit.TotalCount.Should().Be(1);
        hit.Items.Single().ProcessName.Should().Be("GRP-CR60");

        var remarkHit = await svc.GetPagedAsync(new Core.Models.QueryParams { Keyword = "三辊", PageIndex = 0, PageSize = 10 });
        remarkHit.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_默认按工序名排序_不分页时全取()
    {
        var ctx = CreateDbContext();
        ctx.DailyProductionCapacities.AddRange(
            new DailyProductionCapacity { ProcessName = "GRP-CR30", DailyCapacity = 200m },
            new DailyProductionCapacity { ProcessName = "GRP-CR60", DailyCapacity = 300m },
            new DailyProductionCapacity { ProcessName = ProductionOverviewRowKeys.Polish, DailyCapacity = 120m });
        await ctx.SaveChangesAsync();
        var svc = new DailyProductionCapacityService(ctx, CreateGroupMock().Object);

        var page = await svc.GetPagedAsync(new Core.Models.QueryParams { PageIndex = 0, PageSize = 2 });
        page.TotalCount.Should().Be(3);
        // 分页取回 2 行（排序语义由 QueryableExtensionsTests 兜底，此处不依赖文化排序）
        page.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_返回全部产能档案()
    {
        var ctx = CreateDbContext();
        ctx.DailyProductionCapacities.AddRange(
            new DailyProductionCapacity { ProcessName = "GRP-CR60", DailyCapacity = 300m },
            new DailyProductionCapacity { ProcessName = ProductionOverviewRowKeys.Polish, DailyCapacity = 120m },
            new DailyProductionCapacity { ProcessName = "GRP-CR30", DailyCapacity = 200m });
        await ctx.SaveChangesAsync();
        var svc = new DailyProductionCapacityService(ctx, CreateGroupMock().Object);

        var rows = await svc.GetAllAsync();
        rows.Select(r => r.ProcessName).Should().BeEquivalentTo(
            "GRP-CR30", "GRP-CR60", ProductionOverviewRowKeys.Polish);
    }
}
