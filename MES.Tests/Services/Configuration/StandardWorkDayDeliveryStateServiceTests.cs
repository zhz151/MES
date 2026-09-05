using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Configuration;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 交货状态附加天数服务测试：CRUD（枚举名/空串默认行存储）、缺失抛业务异常、
/// GetDeliveryStateExtraDaysMapAsync 仅通用行 + 默认空串键。
/// </summary>
public class StandardWorkDayDeliveryStateServiceTests : TestBase
{
    private static StandardWorkDayDeliveryStateService CreateService(AppDbContext ctx) => new(ctx);

    private static async Task<StandardWorkDayDeliveryState> SeedAsync(AppDbContext ctx, string? deliveryState,
        double extraDays, string? plantGradePrefix = null)
    {
        var e = new StandardWorkDayDeliveryState
        {
            DeliveryState = deliveryState ?? string.Empty,
            ExtraDays = extraDays,
            PlantGradePrefix = plantGradePrefix
        };
        ctx.StandardWorkDayDeliveryStates.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static StandardWorkDayDeliveryStateDto NewDto(DeliveryState? state = DeliveryState.Bright, double days = 2)
        => new()
        {
            DeliveryState = state, // null → 默认行（空串）
            ExtraDays = days
        };

    // ========== Save / GetById / Delete ==========

    [Fact]
    public async Task SaveAsync_新增枚举状态_落库存枚举名()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.SaveAsync(NewDto(DeliveryState.Bright, 2))).Should().BeTrue();

        var row = await ctx.StandardWorkDayDeliveryStates.SingleAsync();
        row.DeliveryState.Should().Be("Bright");
        row.ExtraDays.Should().Be(2);
    }

    [Fact]
    public async Task SaveAsync_新增默认行_存空串()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.SaveAsync(NewDto(null, 1.5))).Should().BeTrue();

        var row = await ctx.StandardWorkDayDeliveryStates.SingleAsync();
        row.DeliveryState.Should().Be("");
        row.ExtraDays.Should().Be(1.5);
    }

    [Fact]
    public async Task SaveAsync_更新缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.SaveAsync(new StandardWorkDayDeliveryStateDto { Id = 99999 });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*交货状态附加天数配置不存在*");
    }

    [Fact]
    public async Task GetByIdAsync_状态行_解析为枚举()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, "Bright", 2);
        var svc = CreateService(ctx);

        var dto = await svc.GetByIdAsync(e.Id);

        dto!.DeliveryState.Should().Be(DeliveryState.Bright);
        dto.ExtraDays.Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_默认行_返回空枚举()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, null, 1.5);
        var svc = CreateService(ctx);

        var dto = await svc.GetByIdAsync(e.Id);

        dto!.DeliveryState.Should().BeNull();
        dto.ExtraDays.Should().Be(1.5);
    }

    [Fact]
    public async Task GetByIdAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.GetByIdAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*交货状态附加天数配置不存在*");
    }

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, "Bright", 2);
        var svc = CreateService(ctx);

        (await svc.DeleteAsync(e.Id)).Should().BeTrue();

        ctx.StandardWorkDayDeliveryStates.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_缺失_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = async () => await svc.DeleteAsync(99999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*交货状态附加天数配置不存在*");
    }

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_关键字命中状态名或备注()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, "Bright", 2);
        await SeedAsync(ctx, "Hard", 0);
        var svc = CreateService(ctx);

        var hit = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "Bright" });
        hit.Items.Should().ContainSingle().Which.DeliveryState.Should().Be(DeliveryState.Bright);
    }

    // ========== GetDeliveryStateExtraDaysMapAsync ==========

    [Fact]
    public async Task GetDeliveryStateExtraDaysMapAsync_仅通用行_含默认空键()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, null, 1.5);               // 默认行
        await SeedAsync(ctx, "Bright", 2);             // 状态行
        await SeedAsync(ctx, "Hard", 3, plantGradePrefix: "3"); // 牌号前缀覆盖行 → 排除
        var svc = CreateService(ctx);

        var map = await svc.GetDeliveryStateExtraDaysMapAsync();

        map.Should().BeEquivalentTo(new Dictionary<string, double>
        {
            [""] = 1.5,
            ["Bright"] = 2
        });
        map.Should().NotContainKey("Hard");
    }

    [Fact]
    public async Task GetDeliveryStateExtraDaysMapAsync_无数据_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var map = await svc.GetDeliveryStateExtraDaysMapAsync();

        map.Should().BeEmpty();
    }
}
