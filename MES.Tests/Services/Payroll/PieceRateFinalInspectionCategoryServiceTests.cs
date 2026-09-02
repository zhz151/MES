using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Data;
using MES.Data.Entities.Payroll;
using MES.Services.Payroll;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 成检计件类别服务测试（2026-09-03）：
/// CRUD 整组保存、同成检项目启用唯一、中文归一、区间重叠（检验支数整数共享边界）/等值去重、
/// 试算匹配（连乘/未命中/数据违例）、列表模糊搜索与筛选、详情排序。
/// </summary>
public class PieceRateFinalInspectionCategoryServiceTests : TestBase
{
    private static PieceRateFinalInspectionCategoryService CreateService(AppDbContext ctx)
        => new(ctx);

    private static PieceRateFinalInspectionCategorySaveRequest BuildRequest(
        InspectionItem item = InspectionItem.Ultrasonic, decimal basePrice = 35,
        string unit = "PerTon", bool isActive = true)
        => new()
        {
            ItemKey = item.ToString(),
            BasePrice = basePrice,
            Unit = unit,
            IsActive = isActive
        };

    private static string ItemCn(InspectionItem item)
        => EnumHelper.GetDisplayName<InspectionItem>(item.ToString())!;

    // ==================== CRUD + 整组替换 ====================

    [Fact]
    public async Task SaveAsync_创建类别_中文项目归一_无档行()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = BuildRequest(InspectionItem.Ultrasonic);
        req.ItemKey = "超声波"; // 中文归一为枚举名
        var dto = await svc.SaveAsync(null, req);

        dto.Id.Should().BeGreaterThan(0);
        dto.ItemKey.Should().Be(nameof(InspectionItem.Ultrasonic));
        dto.ItemKeyChinese.Should().Be("超声波");
        dto.UnitChinese.Should().Be("元/吨");
        dto.TierCount.Should().Be(0);

        var stored = await ctx.PieceRateFinalInspectionCategories.SingleAsync();
        stored.ItemKey.Should().Be(nameof(InspectionItem.Ultrasonic));
    }

    [Fact]
    public async Task SaveAsync_更新整组替换档行()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = BuildRequest(basePrice: 35);
        req.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest
            {
                DimensionKey = PieceRateInspectionDimensionKeys.OuterDiameter,
                RangeText = ">54",
                Ratio = 1.2m
            }
        ];
        var withTier = await svc.SaveAsync(null, req);
        withTier.Tiers.Should().HaveCount(1);

        var upd = BuildRequest(basePrice: 40);
        upd.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest
            {
                DimensionKey = PieceRateInspectionDimensionKeys.OuterDiameter,
                RangeText = ">54",
                Ratio = 1.1m
            },
            new PieceRateFinalInspectionCategoryTierSaveRequest
            {
                DimensionKey = PieceRateInspectionDimensionKeys.WallThickness,
                RangeText = ">4.3",
                Ratio = 1.15m
            }
        ];
        var updated = await svc.SaveAsync(withTier.Id, upd);

        updated.BasePrice.Should().Be(40);
        updated.Tiers.Should().HaveCount(2);
        var od = updated.Tiers.Single(t => t.DimensionKey == PieceRateInspectionDimensionKeys.OuterDiameter);
        od.Ratio.Should().Be(1.1m);
        od.MinValue.Should().Be(54);
        od.MaxValue.Should().BeNull();
        var wt = updated.Tiers.Single(t => t.DimensionKey == PieceRateInspectionDimensionKeys.WallThickness);
        wt.MinValue.Should().Be(4.3m);

        (await ctx.PieceRateFinalInspectionCategoryTiers.CountAsync()).Should().Be(2); // 旧档行已整组清除
    }

    [Fact]
    public async Task SaveAsync_同项目启用唯一_停用后可重建()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.SaveAsync(null, BuildRequest(InspectionItem.Ultrasonic));

        var dup = BuildRequest(InspectionItem.Ultrasonic);
        var act = async () => await svc.SaveAsync(null, dup);
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("已存在启用类别");

        ctx.ChangeTracker.Clear();

        // 停用旧 → 允许新建启用
        var disable = BuildRequest(InspectionItem.Ultrasonic, isActive: false);
        await svc.SaveAsync(created.Id, disable);
        var dto = await svc.SaveAsync(null, BuildRequest(InspectionItem.Ultrasonic));
        dto.Id.Should().NotBe(created.Id);
    }

    [Fact]
    public async Task SaveAsync_异项目共存_同项目停用不冲突()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var a = await svc.SaveAsync(null, BuildRequest(InspectionItem.Ultrasonic));
        var b = await svc.SaveAsync(null, BuildRequest(InspectionItem.HydrostaticPressure));
        b.Id.Should().BeGreaterThan(0);
        var inactive = await svc.SaveAsync(null,
            BuildRequest(InspectionItem.Ultrasonic, isActive: false)); // 停用可多条并存
        inactive.Id.Should().NotBe(a.Id);
    }

    [Fact]
    public async Task DeleteAsync_级联删除档行()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = BuildRequest();
        req.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest
            {
                DimensionKey = PieceRateInspectionDimensionKeys.OuterDiameter,
                RangeText = ">54",
                Ratio = 1.2m
            }
        ];
        var created = await svc.SaveAsync(null, req);

        await svc.DeleteAsync(created.Id);

        (await ctx.PieceRateFinalInspectionCategories.CountAsync()).Should().Be(0);
        (await ctx.PieceRateFinalInspectionCategoryTiers.CountAsync()).Should().Be(0);
    }

    // ==================== 档行校验 ====================

    [Fact]
    public async Task SaveAsync_同维区间重叠拒存_相切邻接合法()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var adjacent = BuildRequest();
        adjacent.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 1.2m },
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.OuterDiameter, RangeText = "41-54", Ratio = 1m }
        ];
        var okDto = await svc.SaveAsync(null, adjacent);
        okDto.Tiers.Should().HaveCount(2);

        var overlap = BuildRequest();
        overlap.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 1.2m },
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.OuterDiameter, RangeText = "50-60", Ratio = 0.9m }
        ];
        var act = async () => await svc.SaveAsync(null, overlap);
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("区间重叠");
    }

    [Fact]
    public async Task SaveAsync_检验支数整数共享边界拒存()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = BuildRequest();
        req.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.InspectionCount, RangeText = "1-10", Ratio = 2m },
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.InspectionCount, RangeText = "10-100", Ratio = 1.5m }
        ];
        var act = async () => await svc.SaveAsync(null, req);
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("区间重叠");

        ctx.ChangeTracker.Clear();

        // 闭区间邻接合法：整数档不可共享边界，低档 1-10 末值 10，高档须从 11 起（11-100）
        var ok = BuildRequest();
        ok.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.InspectionCount, RangeText = "1-10", Ratio = 2m },
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.InspectionCount, RangeText = "11-100", Ratio = 1m }
        ];
        var dto = await svc.SaveAsync(null, ok);
        dto.Tiers.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveAsync_检验支数小数边界拒存()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = BuildRequest();
        req.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.InspectionCount, RangeText = "1.5-10", Ratio = 2m }
        ];
        var act = async () => await svc.SaveAsync(null, req);
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("整数");
    }

    [Fact]
    public async Task SaveAsync_等值维取值重复拒存()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = BuildRequest();
        req.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.LengthStatus, MatchValue = "Fixed", Ratio = 1.1m },
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.LengthStatus, MatchValue = "fixed", Ratio = 1.2m }
        ];
        var act = async () => await svc.SaveAsync(null, req);
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("取值重复");
    }

    [Fact]
    public async Task SaveAsync_系数不大于0拒存_无效维度拒存()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var zero = BuildRequest();
        zero.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 0m }
        ];
        var actZero = async () => await svc.SaveAsync(null, zero);
        (await actZero.Should().ThrowAsync<BusinessException>()).Which.Message.Should().Contain("大于0");

        ctx.ChangeTracker.Clear();

        var badDim = BuildRequest();
        badDim.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = "Nope", RangeText = ">54", Ratio = 1m }
        ];
        var actDim = async () => await svc.SaveAsync(null, badDim);
        (await actDim.Should().ThrowAsync<BusinessException>()).Which.Message.Should().Contain("无效的维度");
    }

    // ==================== 试算匹配 ====================

    [Fact]
    public async Task MatchPriceAsync_连乘计算_命中维档()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = BuildRequest(basePrice: 35);
        req.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 1.2m },
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.WallThickness, RangeText = ">4.3", Ratio = 1.15m }
        ];
        await svc.SaveAsync(null, req);

        var hit = await svc.MatchPriceAsync(new PieceRateFinalInspectionMatchRequest
        {
            ItemKey = nameof(InspectionItem.Ultrasonic),
            OuterDiameter = 60,
            WallThickness = 5
        });

        hit.Should().NotBeNull();
        hit!.BasePrice.Should().Be(35);
        hit.TotalRatio.Should().Be(1.38m);   // 1.2 × 1.15
        hit.UnitPrice.Should().Be(48.3m);    // 35 × 1.38
        hit.ItemKeyChinese.Should().Be("超声波");
        hit.UnitChinese.Should().Be("元/吨");
        hit.Hits.Should().HaveCount(2);
    }

    [Fact]
    public async Task MatchPriceAsync_中文项目与长度状态归一命中()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = BuildRequest(basePrice: 35);
        req.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.LengthStatus, MatchValue = "Fixed", Ratio = 1.1m },
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.Length, RangeText = ">10", Ratio = 1.2m }
        ];
        await svc.SaveAsync(null, req);

        var hit = await svc.MatchPriceAsync(new PieceRateFinalInspectionMatchRequest
        {
            ItemKey = "超声波",            // 中文
            LengthStatus = "定尺",          // 中文 → Fixed
            Length = 15
        });

        hit.Should().NotBeNull();
        hit!.TotalRatio.Should().Be(1.32m);  // 1.1 × 1.2
        hit.UnitPrice.Should().Be(46.2m);
    }

    [Fact]
    public async Task MatchPriceAsync_未定价返回null_未配置维档系数1()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 无任何类别 → 未定价
        var miss = await svc.MatchPriceAsync(new PieceRateFinalInspectionMatchRequest
        {
            ItemKey = nameof(InspectionItem.Ultrasonic)
        });
        miss.Should().BeNull();

        await svc.SaveAsync(null, BuildRequest(basePrice: 35));

        // 有类别但仅要求无档维 → 基准价
        var baseHit = await svc.MatchPriceAsync(new PieceRateFinalInspectionMatchRequest
        {
            ItemKey = nameof(InspectionItem.Ultrasonic),
            InspectionCount = 50 // 未配检验支数档 → 系数 1
        });
        baseHit.Should().NotBeNull();
        baseHit!.UnitPrice.Should().Be(35);
    }

    [Fact]
    public async Task MatchPriceAsync_检验支数分段命中()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = BuildRequest(basePrice: 35);
        req.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.InspectionCount, RangeText = "1-10", Ratio = 2m },
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.InspectionCount, RangeText = "11-9999", Ratio = 1m }
        ];
        await svc.SaveAsync(null, req);

        var few = await svc.MatchPriceAsync(new PieceRateFinalInspectionMatchRequest
        {
            ItemKey = nameof(InspectionItem.Ultrasonic),
            InspectionCount = 5
        });
        few!.UnitPrice.Should().Be(70m);   // 35 × 2

        var many = await svc.MatchPriceAsync(new PieceRateFinalInspectionMatchRequest
        {
            ItemKey = nameof(InspectionItem.Ultrasonic),
            InspectionCount = 1000
        });
        many!.UnitPrice.Should().Be(35m);  // 35 × 1
    }

    [Fact]
    public async Task MatchPriceAsync_数据违例命中多类别抛错()
    {
        using var ctx = CreateDbContext();
        // 绕过唯一校验直插两条启用同类（模拟历史违例；InMemory 无过滤唯一索引兜底）
        ctx.PieceRateFinalInspectionCategories.AddRange(
            new PieceRateFinalInspectionCategory { ItemKey = nameof(InspectionItem.Ultrasonic), IsActive = true, BasePrice = 10, Unit = "PerTon" },
            new PieceRateFinalInspectionCategory { ItemKey = nameof(InspectionItem.Ultrasonic), IsActive = true, BasePrice = 20, Unit = "PerTon" });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var act = async () => await svc.MatchPriceAsync(new PieceRateFinalInspectionMatchRequest
        {
            ItemKey = nameof(InspectionItem.Ultrasonic)
        });
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("命中多个启用类别");
    }

    // ==================== 列表 / 详情 ====================

    [Fact]
    public async Task GetPagedAsync_项目与启停筛选_模糊搜索中文()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.SaveAsync(null, BuildRequest(InspectionItem.Ultrasonic));
        var disabled = BuildRequest(InspectionItem.HydrostaticPressure, isActive: false);
        disabled.Remark = "停用备用";
        await svc.SaveAsync(null, disabled);

        var onlyUs = await svc.GetPagedAsync(new PieceRateFinalInspectionCategoryQueryParams
        {
            ItemKey = nameof(InspectionItem.Ultrasonic),
            PageSize = 50
        });
        onlyUs.TotalCount.Should().Be(1);

        var onlyActive = await svc.GetPagedAsync(new PieceRateFinalInspectionCategoryQueryParams
        {
            IsActive = true,
            PageSize = 50
        });
        onlyActive.TotalCount.Should().Be(1);

        var byKw = await svc.GetPagedAsync(new PieceRateFinalInspectionCategoryQueryParams
        {
            Keyword = "停用备用",
            PageSize = 50
        });
        byKw.TotalCount.Should().Be(1);

        var byItemCn = await svc.GetPagedAsync(new PieceRateFinalInspectionCategoryQueryParams
        {
            Keyword = "水压",
            PageSize = 50
        });
        byItemCn.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDetailAsync_维度序稳定_含停用档行()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = BuildRequest();
        req.Tiers =
        [
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.WallThickness, RangeText = ">4.3", Ratio = 1.15m },
            new PieceRateFinalInspectionCategoryTierSaveRequest { DimensionKey = PieceRateInspectionDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 1.2m, IsActive = false }
        ];
        var created = await svc.SaveAsync(null, req);

        var detail = await svc.GetDetailAsync(created.Id);
        detail.Should().NotBeNull();
        detail!.Tiers.Should().HaveCount(2);                    // 含停用行（编辑页展示）
        detail.Tiers.First().DimensionKey.Should().Be(PieceRateInspectionDimensionKeys.OuterDiameter); // 维度声明序
        detail.TierCount.Should().Be(1);                        // 仅启用档行计数
    }

    [Fact]
    public async Task GetOptionsAsync_含成检项目与长度状态中文选项()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var options = await svc.GetOptionsAsync();
        options.Items.Should().NotBeEmpty();
        options.Items.Select(o => o.Key).Should().Contain(nameof(InspectionItem.Ultrasonic));
        options.Items.First(o => o.Key == nameof(InspectionItem.Ultrasonic)).Name.Should().Be("超声波");
        options.LengthStatuses.Select(o => o.Key).Should().Contain("Fixed");
        options.LengthStatuses.First(o => o.Key == "Fixed").Name.Should().Be("定尺");
        options.Units.Should().NotBeEmpty();
        options.States.Should().NotBeEmpty();
    }
}
