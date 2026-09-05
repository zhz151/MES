using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Services.WorkOrder;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 用料计划工序组服务测试：按计划类型读取工序组（1/3/6/4 四类映射）、非法类型抛业务异常。
/// 注：SaveAsync 依赖事务 + 原生 SQL（ExecuteSqlRawAsync），EF InMemory 不支持，故仅覆盖查询路径。
/// </summary>
public class MaterialPlanProcessGroupServiceTests : TestBase
{
    private static MaterialPlanProcessGroupService CreateService(AppDbContext ctx) => new(
        ctx,
        new Mock<IStandardWorkDayService>().Object,
        new Mock<IStandardWorkDayDeliveryStateService>().Object,
        new Mock<IConfigParameterService>().Object);

    private static async Task SeedGroupAsync(AppDbContext ctx, int planType, int planId, int seq,
        string processName = "冷拔", int? cut = 1)
    {
        switch (planType)
        {
            case 1:
                ctx.SemiPlanProcessGroups.Add(new SemiPlanProcessGroup
                {
                    PurchaseSemiPlanId = planId, SequenceNumber = seq, ProcessName = processName, Cut = cut
                });
                break;
            case 3:
                ctx.InventoryPlanProcessGroups.Add(new InventoryPlanProcessGroup
                {
                    InventoryPlanId = planId, SequenceNumber = seq, ProcessName = processName, Cut = cut
                });
                break;
            case 4:
                ctx.PiercingPlanProcessGroups.Add(new PiercingPlanProcessGroup
                {
                    RoundBarPiercingPlanId = planId, SequenceNumber = seq, ProcessName = processName, Cut = cut
                });
                break;
            case 6:
                ctx.InProcessReworkPlanProcessGroups.Add(new InProcessReworkPlanProcessGroup
                {
                    InProcessReworkPlanId = planId, SequenceNumber = seq, ProcessName = processName, Cut = cut
                });
                break;
        }
        await ctx.SaveChangesAsync();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public async Task GetByPlanAsync_支持类型_按序号返回工序组(int planType)
    {
        var ctx = CreateDbContext();
        await SeedGroupAsync(ctx, planType, 7, 1, "断切", 1);
        await SeedGroupAsync(ctx, planType, 7, 2, "冷拔", 3);
        await SeedGroupAsync(ctx, planType, 99, 1, "无关计划"); // 其它计划 → 排除
        var svc = CreateService(ctx);

        var rows = await svc.GetByPlanAsync(planType, 7);

        rows.Should().HaveCount(2);
        rows.Select(r => r.SequenceNumber).Should().Equal(1, 2); // 按序号升序
        rows[0].ProcessName.Should().Be("断切");
        rows[1].ProcessName.Should().Be("冷拔");
        rows[1].Cut.Should().Be(3);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public async Task GetByPlanAsync_支持类型_无记录返回空(int planType)
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var rows = await svc.GetByPlanAsync(planType, 12345);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByPlanAsync_非法计划类型_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.GetByPlanAsync(5, 1);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*无效的用料计划类型: 5*");
    }
}
