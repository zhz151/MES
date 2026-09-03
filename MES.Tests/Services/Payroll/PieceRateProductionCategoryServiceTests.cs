using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.Payroll;
using MES.Services.Payroll;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 生产计件类别服务测试（2026-09-02 两表模型）：
/// CRUD 整组保存、约束集合全选归一、禁交集拒存、区间重叠/等值去重校验、
/// 相切邻接合法、试算匹配（连乘/未命中/数据违例）、列表模糊搜索。
/// </summary>
public class PieceRateProductionCategoryServiceTests : TestBase
{
    private static ISectionNameDisplayService SectionDisplayMock()
    {
        var mock = new Mock<ISectionNameDisplayService>();
        mock.Setup(x => x.GetSectionNameMapAsync()).ReturnsAsync(SectionKeys.KeyToChinese);
        return mock.Object;
    }

    private PieceRateProductionCategoryService CreateService(AppDbContext ctx)
        => new(ctx, SectionDisplayMock(), CreateProcessDefinitionServiceMock());

    private static PieceRateProductionCategorySaveRequest PickleRoughInTank(decimal basePrice = 35, string unit = "PerTon")
        => new()
        {
            SectionKey = SectionKeys.Pickle,
            ProcessKeys = [],
            ProductStatusKeys = [ProductStatuses.RoughTube],
            StageKeys = [PieceRateStageKeys.InTank],
            BasePrice = basePrice,
            Unit = unit,
            IsActive = true
        };

    // ==================== CRUD + 整组替换 ====================

    [Fact]
    public async Task SaveAsync_创建无档行类别_四键中文归一化()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = await svc.SaveAsync(null, PickleRoughInTank());

        dto.Id.Should().BeGreaterThan(0);
        dto.SectionKey.Should().Be(SectionKeys.Pickle);
        dto.SectionKeyChinese.Should().Be("酸洗");
        dto.ProductStatusKeys.Should().Contain(ProductStatuses.RoughTube);
        dto.DisplayName.Should().Be("酸洗｜荒管｜全部工序｜入缸");
        dto.TierCount.Should().Be(0);

        var stored = await ctx.PieceRateProductionCategories.SingleAsync();
        var storedKeys = await ctx.PieceRateProductionCategoryKeys
            .Where(k => k.CategoryId == stored.Id)
            .ToListAsync();
        storedKeys.Where(k => k.ConstraintType == PieceRateConstraintTypes.Process)
            .Should().BeEmpty();                  // 空工序 = 全选 → 无成员行
        storedKeys.Where(k => k.ConstraintType == PieceRateConstraintTypes.ProductStatus)
            .Select(k => k.Key)
            .Should().Contain(ProductStatuses.RoughTube);
        storedKeys.Where(k => k.ConstraintType == PieceRateConstraintTypes.Stage)
            .Select(k => k.Key)
            .Should().Contain(PieceRateStageKeys.InTank);
    }

    [Fact]
    public async Task SaveAsync_更新整组替换档行()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var created = await svc.SaveAsync(null, new PieceRateProductionCategorySaveRequest
        {
            SectionKey = SectionKeys.Pickle,
            ProductStatusKeys = [ProductStatuses.RoughTube],
            StageKeys = [PieceRateStageKeys.InTank],
            BasePrice = 35,
            Unit = "PerTon",
            Tiers =
            [
                new PieceRateProductionCategoryTierSaveRequest
                {
                    DimensionKey = PieceRateDimensionKeys.OuterDiameter,
                    RangeText = ">54",
                    Ratio = 1.2m
                }
            ]
        });
        created.Tiers.Should().HaveCount(1);

        // 整组替换为 2 档 + 改基准
        var updated = await svc.SaveAsync(created.Id, new PieceRateProductionCategorySaveRequest
        {
            SectionKey = SectionKeys.Pickle,
            ProductStatusKeys = [ProductStatuses.RoughTube],
            StageKeys = [PieceRateStageKeys.InTank],
            BasePrice = 40,
            Unit = "PerTon",
            Tiers =
            [
                new PieceRateProductionCategoryTierSaveRequest
                {
                    DimensionKey = PieceRateDimensionKeys.OuterDiameter,
                    RangeText = ">54",
                    Ratio = 1.1m
                },
                new PieceRateProductionCategoryTierSaveRequest
                {
                    DimensionKey = PieceRateDimensionKeys.WallThickness,
                    RangeText = ">4.3",
                    Ratio = 1.15m
                }
            ]
        });

        updated.BasePrice.Should().Be(40);
        updated.Tiers.Should().HaveCount(2);
        var od = updated.Tiers.Single(t => t.DimensionKey == PieceRateDimensionKeys.OuterDiameter);
        od.Ratio.Should().Be(1.1m);
        od.MinValue.Should().Be(54);
        od.MaxValue.Should().BeNull();
        var wt = updated.Tiers.Single(t => t.DimensionKey == PieceRateDimensionKeys.WallThickness);
        wt.MinValue.Should().Be(4.3m);

        var stored = await ctx.PieceRateProductionCategoryTiers
            .Where(t => t.CategoryId == created.Id)
            .ToListAsync();
        stored.Should().HaveCount(2); // 旧档行已整组清除
    }

    [Fact]
    public async Task SaveAsync_显式全产类列表归一为null()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var request = PickleRoughInTank();
        request.ProductStatusKeys = [.. ProductStatuses.All]; // 显式全列表 = 全选
        var dto = await svc.SaveAsync(null, request);

        dto.ProductStatusKeys.Should().BeEmpty();            // 归一为空（前端展示空=全选）
        dto.DisplayName.Should().Contain("全部产类");
        var stored = await ctx.PieceRateProductionCategories.SingleAsync();
        var storedProds = await ctx.PieceRateProductionCategoryKeys
            .Where(k => k.CategoryId == stored.Id && k.ConstraintType == PieceRateConstraintTypes.ProductStatus)
            .ToListAsync();
        storedProds.Should().BeEmpty();                       // 显式全列表归一 → 无成员行 = 全选
    }

    [Fact]
    public async Task DeleteAsync_级联删除档行()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var request = PickleRoughInTank();
        request.Tiers =
        [
            new PieceRateProductionCategoryTierSaveRequest
            {
                DimensionKey = PieceRateDimensionKeys.OuterDiameter,
                RangeText = ">54",
                Ratio = 1.2m
            }
        ];
        var created = await svc.SaveAsync(null, request);

        await svc.DeleteAsync(created.Id);

        (await ctx.PieceRateProductionCategories.CountAsync()).Should().Be(0);
        (await ctx.PieceRateProductionCategoryTiers.CountAsync()).Should().Be(0);
    }

    // ==================== 禁止交集 ====================

    [Fact]
    public async Task SaveAsync_同工段产类互斥类别不冲突()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var a = PickleRoughInTank();                                   // 酸洗·荒管·入缸
        var b = PickleRoughInTank();
        b.ProductStatusKeys = [ProductStatuses.InProgress, ProductStatuses.Finished]; // 酸洗·在制/成品·入缸
        await svc.SaveAsync(null, a);
        var dtoB = await svc.SaveAsync(null, b);
        dtoB.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_阶段互斥类别不冲突()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var a = PickleRoughInTank();
        var b = PickleRoughInTank();
        b.StageKeys = [PieceRateStageKeys.OutTank];
        await svc.SaveAsync(null, a);
        var dtoB = await svc.SaveAsync(null, b);
        dtoB.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_全阶段类别与阶段具体类别冲突拒存()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var a = PickleRoughInTank();                                    // 阶段={入缸}
        await svc.SaveAsync(null, a);

        var b = PickleRoughInTank();
        b.StageKeys = [];                                              // 阶段=全选（含入缸）
        var act = async () => await svc.SaveAsync(null, b);
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("禁止交集");
    }

    [Fact]
    public async Task SaveAsync_同覆盖重复拒存_停用后可重建()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var a = PickleRoughInTank();
        var createdA = await svc.SaveAsync(null, a);

        var dup = PickleRoughInTank();
        var act = async () => await svc.SaveAsync(null, dup);
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("禁止交集");

        // 拒绝时 dup 已 Add 但未保存，清除跟踪器防止后续 SaveChanges 连带提交
        ctx.ChangeTracker.Clear();

        // 先停用旧类别（调价版本模式：停用旧 + 新建新）
        a.IsActive = false;
        await svc.SaveAsync(createdA.Id, a);
        var dto = await svc.SaveAsync(null, PickleRoughInTank());
        dto.Id.Should().BeGreaterThan(0);
        dto.Id.Should().NotBe(createdA.Id);
    }

    // ==================== 档行校验 ====================

    [Fact]
    public async Task SaveAsync_同维区间重叠拒存_相切邻接合法()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var baseTiers = new List<PieceRateProductionCategoryTierSaveRequest>
        {
            new() { DimensionKey = PieceRateDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 1.2m }
        };

        // 相切邻接 (54-… 与 …-54) 合法
        var adjacent = PickleRoughInTank();
        adjacent.Tiers =
        [
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 1.2m },
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.OuterDiameter, RangeText = "41-54", Ratio = 1m }
        ];
        var okDto = await svc.SaveAsync(null, adjacent);
        okDto.Tiers.Should().HaveCount(2);

        // 跨段重叠 (54-… 与 50-…) 拒存
        var overlap = PickleRoughInTank();
        overlap.Tiers =
        [
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 1.2m },
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.OuterDiameter, RangeText = "50-60", Ratio = 0.9m }
        ];
        var actOverlap = async () => await svc.SaveAsync(null, overlap);
        var ex = await actOverlap.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("区间重叠");
    }

    [Fact]
    public async Task SaveAsync_定尺整数共享边界拒存()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = PickleRoughInTank();
        req.Tiers =
        [
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.FixedLengthCount, RangeText = "3-5", Ratio = 1m },
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.FixedLengthCount, RangeText = "5-8", Ratio = 1.2m }
        ];
        var act = async () => await svc.SaveAsync(null, req);
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("区间重叠");
    }

    [Fact]
    public async Task SaveAsync_等值维取值重复拒存()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = PickleRoughInTank();
        req.Tiers =
        [
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.SpecialState, MatchValue = "Bright", Ratio = 1.35m },
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.SpecialState, MatchValue = "bright", Ratio = 1.1m }
        ];
        var act = async () => await svc.SaveAsync(null, req);
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("取值重复");
    }

    [Fact]
    public async Task SaveAsync_系数不大于0拒存()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = PickleRoughInTank();
        req.Tiers =
        [
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 0m }
        ];
        var act = async () => await svc.SaveAsync(null, req);
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("大于0");
    }

    // ==================== 试算匹配 ====================

    [Fact]
    public async Task MatchPriceAsync_连乘计算_命中维档()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = PickleRoughInTank(35);
        req.Tiers =
        [
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 1.2m },
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.WallThickness, RangeText = ">4.3", Ratio = 1.15m }
        ];
        await svc.SaveAsync(null, req);

        var hit = await svc.MatchPriceAsync(new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.Pickle,
            ProcessName = ProcessKeys.ColdDraw,
            ProductStatus = ProductStatuses.RoughTube,
            Stage = PieceRateStageKeys.InTank,
            OuterDiameter = 60,
            WallThickness = 5
        });

        hit.Should().NotBeNull();
        hit!.BasePrice.Should().Be(35);
        hit.TotalRatio.Should().Be(1.38m);          // 1.2 × 1.15
        hit.UnitPrice.Should().Be(48.3m);           // 35 × 1.38
        hit.UnitChinese.Should().Be("元/吨");
        hit.Hits.Should().HaveCount(2);
    }

    [Fact]
    public async Task MatchPriceAsync_未命中档维系数为1_未定价返回null()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.SaveAsync(null, PickleRoughInTank(35));

        // 产类不匹配 → 未定价
        var miss = await svc.MatchPriceAsync(new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.Finished,
            Stage = PieceRateStageKeys.InTank
        });
        miss.Should().BeNull();

        // 未配置维档维（外径档不存在）→ 该维系数 1，仍出基准价
        var baseHit = await svc.MatchPriceAsync(new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.RoughTube,
            Stage = PieceRateStageKeys.InTank,
            OuterDiameter = 60
        });
        baseHit.Should().NotBeNull();
        baseHit!.UnitPrice.Should().Be(35);
    }

    [Fact]
    public async Task MatchPriceAsync_中文输入归一化命中()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = PickleRoughInTank(35);
        req.StageKeys = [];
        await svc.SaveAsync(null, req);

        // 中文工段/产类归一为英文 Key（兼容存量输入）
        var hit = await svc.MatchPriceAsync(new PieceRateProductionMatchRequest
        {
            SectionName = "酸洗",
            ProductStatus = "荒管"
        });
        hit.Should().NotBeNull();
        hit!.UnitPrice.Should().Be(35);
    }

    [Fact]
    public async Task MatchPriceAsync_等值补充档命中()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = PickleRoughInTank(20);
        req.Tiers =
        [
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.SpecialState, MatchValue = PieceRateStateKeys.Bright, Ratio = 1.35m }
        ];
        await svc.SaveAsync(null, req);

        var plain = await svc.MatchPriceAsync(new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.RoughTube,
            Stage = PieceRateStageKeys.InTank
        });
        plain!.UnitPrice.Should().Be(20);           // 无特殊状态 → 1

        var bright = await svc.MatchPriceAsync(new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.RoughTube,
            Stage = PieceRateStageKeys.InTank,
            SpecialState = PieceRateStateKeys.Bright
        });
        bright!.UnitPrice.Should().Be(27m);         // 20 × 1.35
    }

    [Fact]
    public async Task MatchPriceAsync_ColdDrawType_备注关键词命中()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = PickleRoughInTank(20);
        req.Tiers =
        [
            new PieceRateProductionCategoryTierSaveRequest
            {
                DimensionKey = PieceRateDimensionKeys.ColdDrawType,
                MatchValue = "减壁",
                Ratio = 2m
            }
        ];
        await svc.SaveAsync(null, req);

        // 备注不含关键词 → 该维系数 1
        var plain = await svc.MatchPriceAsync(new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.RoughTube,
            Stage = PieceRateStageKeys.InTank,
            Remark = "普通拉拔"
        });
        plain!.UnitPrice.Should().Be(20);

        // 备注含关键词（前后缀均命中）→ ×2
        var hit = await svc.MatchPriceAsync(new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.RoughTube,
            Stage = PieceRateStageKeys.InTank,
            Remark = "减壁处理"
        });
        hit!.UnitPrice.Should().Be(40m);            // 20 × 2
    }

    [Fact]
    public async Task MatchPriceAsync_数据违例命中多类别抛错()
    {
        using var ctx = CreateDbContext();
        // 直接绕过禁交集校验插入两条重叠启用类别（模拟历史数据违例）
        ctx.PieceRateProductionCategories.AddRange(
            new PieceRateProductionCategory { SectionKey = SectionKeys.Pickle, IsActive = true, BasePrice = 10, Unit = "PerTon" },
            new PieceRateProductionCategory { SectionKey = SectionKeys.Pickle, IsActive = true, BasePrice = 20, Unit = "PerTon" });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var act = async () => await svc.MatchPriceAsync(new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.RoughTube
        });
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("命中多个启用类别");
    }

    // ==================== 列表 ====================

    [Fact]
    public async Task GetPagedAsync_模糊搜索中文组合名()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.SaveAsync(null, PickleRoughInTank());
        var degrease = PickleRoughInTank();
        degrease.SectionKey = SectionKeys.Degrease;
        degrease.ProductStatusKeys = [ProductStatuses.InProgress, ProductStatuses.Finished];
        await svc.SaveAsync(null, degrease);

        var result = await svc.GetPagedAsync(new PieceRateProductionCategoryQueryParams
        {
            Keyword = "酸洗",
            PageSize = 50
        });
        result.TotalCount.Should().Be(1);
        result.Items.Single().DisplayName.Should().Contain("酸洗");

        var byStatus = await svc.GetPagedAsync(new PieceRateProductionCategoryQueryParams
        {
            Keyword = "在制",
            PageSize = 50
        });
        byStatus.Items.Should().OnlyContain(i => i.DisplayName.Contains("在制"));
    }

    [Fact]
    public async Task GetPagedAsync_工段与启停筛选()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await svc.SaveAsync(null, PickleRoughInTank());
        var disabled = PickleRoughInTank();
        disabled.SectionKey = SectionKeys.Degrease;
        disabled.IsActive = false;
        await svc.SaveAsync(null, disabled);

        var onlyPickle = await svc.GetPagedAsync(new PieceRateProductionCategoryQueryParams
        {
            SectionKey = SectionKeys.Pickle,
            PageSize = 50
        });
        onlyPickle.TotalCount.Should().Be(1);

        var onlyActive = await svc.GetPagedAsync(new PieceRateProductionCategoryQueryParams
        {
            IsActive = true,
            PageSize = 50
        });
        onlyActive.TotalCount.Should().Be(1);

        var inactive = await svc.GetPagedAsync(new PieceRateProductionCategoryQueryParams
        {
            IsActive = false,
            PageSize = 50
        });
        inactive.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDetailAsync_排序稳定_含停用档行()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = PickleRoughInTank();
        req.Tiers =
        [
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.WallThickness, RangeText = ">4.3", Ratio = 1.15m },
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 1.2m, IsActive = false }
        ];
        var created = await svc.SaveAsync(null, req);

        var detail = await svc.GetDetailAsync(created.Id);
        detail.Should().NotBeNull();
        detail!.Tiers.Should().HaveCount(2);                       // 含停用行（编辑页展示）
        detail.Tiers.First().DimensionKey.Should().Be(PieceRateDimensionKeys.OuterDiameter); // 按维度声明序
        detail.TierCount.Should().Be(1);                           // 仅启用档行计数
    }
}
