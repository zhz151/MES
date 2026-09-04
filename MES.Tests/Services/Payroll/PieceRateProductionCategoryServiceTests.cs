using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Payroll;
using MES.Data.Entities.Quality;
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

    // ==================== 模拟测算（按产量记录点选计价，2026-09-04） ====================

    /// <summary>种一个生产批次（浏览/计价用例导航主键：InMemory 必填导航 .Include 内联接会剔除孤儿，须真建主）</summary>
    private static async Task<ProductionBatch> SeedProductionBatchAsync(AppDbContext ctx, string spec = "60*3")
    {
        var batch = new ProductionBatch
        {
            BatchNo = "BATCH-PICK-" + Guid.NewGuid().ToString("N")[..8],
            MaterialName = "不锈钢管",
            PlantGrade = "304",
            Specification = spec,
            Status = BatchStatus.InProgress,
            ProductionType = "Internal",
            ManufacturingItem = "OrderFinished",
            WorkOrderNo = "WO-1",
            SalesOrderNo = "SO-1",
            ProductionMainNo = "M-1",
            OrderItemIds = "1",
            Salesman = "张三",
            SettlementMethod = "Weighing",
            StandardCode = "GB/T 14976",
            DeliveryState = "Hard",
            LengthStatus = nameof(LengthStatus.Fixed),
            TechnicalRequirements = "无",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            TotalQuantity = 100,
            TotalMeters = 1000m,
            TotalWeight = 5000m,
            TotalItemCount = 1,
            ItemDetails = null
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    /// <summary>种一条入缸记录（酸洗·入缸，供记录计价/浏览用例）</summary>
    private static async Task<PicklingInRecord> SeedPicklingInAsync(AppDbContext ctx, int idSeed,
        decimal? weight = null, string productStatus = ProductStatuses.RoughTube,
        string manufacturingSpec = "60*3", string? operatorName = null, string? remark = null,
        ProductionBatch? batch = null)
    {
        var rec = new PicklingInRecord
        {
            SectionName = SectionKeys.Pickle,
            ProcessName = "Degrease",
            ProductStatus = productStatus,
            ManufacturingSpec = manufacturingSpec,
            PlantGrade = "304",
            EquipmentName = "酸洗槽1",
            Operator = operatorName,
            Remark = remark,
            InDate = new DateTime(2026, 9, 1).AddDays(idSeed),
            Weight = weight
        };
        if (batch != null)
        {
            rec.ProductionBatchId = batch.Id;
            rec.ProductionBatch = batch;
        }
        ctx.PicklingInRecords.Add(rec);
        await ctx.SaveChangesAsync();
        return rec;
    }

    /// <summary>记录计价与手工 MatchPrice 一致（共享 Mapper/口径）且折算整行计件额</summary>
    [Fact]
    public async Task MatchProductionRecordAsync_入缸记录命中_单金额折算_与手工口径一致()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 酸洗·荒管·入缸·PerTon，OD>54 ×1.2
        var req = PickleRoughInTank(35);
        req.Tiers =
        [
            new PieceRateProductionCategoryTierSaveRequest { DimensionKey = PieceRateDimensionKeys.OuterDiameter, RangeText = ">54", Ratio = 1.2m }
        ];
        await svc.SaveAsync(null, req);

        // 入缸记录须挂真实批次（Service Include(ProductionBatch) 在 InMemory 剔除孤儿导航）
        var batch = await SeedProductionBatchAsync(ctx, spec: "60*3");
        var rec = await SeedPicklingInAsync(ctx, 0, weight: 1000, batch: batch);   // spec 60*3 → OD60 命中 1.2

        var hit = await svc.MatchProductionRecordAsync(PieceRateProductionTrialSource.PicklingIn, rec.Id);
        hit.Should().NotBeNull();
        hit!.BasePrice.Should().Be(35);
        hit.TotalRatio.Should().Be(1.2m);
        hit.UnitPrice.Should().Be(42m);                             // 35 × 1.2
        hit.Unit.Should().Be(PieceRateUnitKeys.PerTon);
        hit.SimulatedAmount.Should().Be(42m);                       // 1000kg/1000 × 42 = 42 元

        // 一致性：手工 MatchPrice 同请求（共享 Mapper 单源）→ 单价一致、SimulatedAmount 恒 null（无计量字段）
        var manual = await svc.MatchPriceAsync(new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.RoughTube,
            Stage = PieceRateStageKeys.InTank,
            OuterDiameter = 60,
            WallThickness = 3
        });
        manual.Should().NotBeNull();
        manual!.UnitPrice.Should().Be(hit.UnitPrice);
        manual.SimulatedAmount.Should().BeNull();
    }

    [Fact]
    public async Task MatchProductionRecordAsync_未定价null_记录不存在抛错()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var req = PickleRoughInTank(35);
        await svc.SaveAsync(null, req);

        // 产类 Finished 不匹配类别约束 RoughTube → 未定价 null（入缸记录须挂真实批次，防 InMemory Include 剔除孤儿）
        var batch = await SeedProductionBatchAsync(ctx);
        var rec = await SeedPicklingInAsync(ctx, 0, productStatus: ProductStatuses.Finished, batch: batch);
        var miss = await svc.MatchProductionRecordAsync(PieceRateProductionTrialSource.PicklingIn, rec.Id);
        miss.Should().BeNull();

        // 记录不存在 → BusinessException
        var act = async () => await svc.MatchProductionRecordAsync(PieceRateProductionTrialSource.PicklingIn, 999999);
        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Message.Should().Contain("入缸记录不存在");

        // 非法产量源参数由 Controller 拦截；此处确认枚举外其它 case 走记录不存在（防御兜底不做断言）
    }

    [Fact]
    public async Task GetTrialRecordsAsync_按源浏览_关键字命中_分页与中文补齐()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 生产记录 1 条 + 入缸 3 条（B 操作人命中关键字）；均须真挂生产批次（必填导航 .Include 在 InMemory 剔除孤儿）
        var batch = await SeedProductionBatchAsync(ctx);
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            SectionName = SectionKeys.ColdRollDraw,
            ProcessName = "ColdDraw",
            ProductStatus = ProductStatuses.InProgress,
            ManufacturingSpec = "60*3",
            Operator = "王五",
            ExecDate = new DateTime(2026, 9, 2),
            ProductionBatchId = batch.Id,
            ProductionBatch = batch
        });
        var a = await SeedPicklingInAsync(ctx, 0, operatorName: "张三", remark: "一批酸洗", batch: batch);
        var b = await SeedPicklingInAsync(ctx, 1, operatorName: "李四", batch: batch);
        var c = await SeedPicklingInAsync(ctx, 2, operatorName: "张三", batch: batch);
        await ctx.SaveChangesAsync();

        // 源过滤：仅入缸 3 条（默认记录日期降序 → 最新 InDate 在前）
        var onlyIn = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.PicklingIn),
            PageSize = 50
        });
        onlyIn.TotalCount.Should().Be(3);
        onlyIn.Items.Should().OnlyContain(i => i.SourceKey == nameof(PieceRateProductionTrialSource.PicklingIn));
        onlyIn.Items.First().RecordDate.Should().Be(c.InDate);      // 日期降序
        onlyIn.Items.Select(i => i.Id).Should().Contain(a.Id);

        // 关键字命中操作人「张三」（本地列下推）
        var byOperator = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.PicklingIn),
            Keyword = "张三",
            PageSize = 50
        });
        byOperator.TotalCount.Should().Be(2);
        byOperator.Items.Should().OnlyContain(i => i.Operator == "张三");

        // 关键字命中备注（本地列下推）
        var byRemark = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.PicklingIn),
            Keyword = "一批",
            PageSize = 50
        });
        byRemark.TotalCount.Should().Be(1);
        byRemark.Items.Single().Id.Should().Be(a.Id);

        // 关键字命中批次号（入缸无自冗余 BatchNo，经所属批次导航检索）
        var byBatchNo = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.PicklingIn),
            Keyword = batch.BatchNo,
            PageSize = 50
        });
        byBatchNo.TotalCount.Should().Be(3);
        byBatchNo.Items.Should().OnlyContain(i => i.BatchNo == batch.BatchNo);

        // 关键字命中工段/工序本地 Key（工段=Pickle、工序=Degrease 种子）
        var byProcess = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.PicklingIn),
            Keyword = "Degrease",
            PageSize = 50
        });
        byProcess.TotalCount.Should().Be(3);
        byProcess.Items.Should().OnlyContain(i => i.ProcessName == "Degrease");

        var bySection = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.PicklingIn),
            Keyword = SectionKeys.Pickle,
            PageSize = 50
        });
        bySection.TotalCount.Should().Be(3);

        // 源过滤仅返回生产记录 + 中文补齐
        var prod = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.ProductionRecord),
            PageSize = 50
        });
        prod.TotalCount.Should().Be(1);
        var row = prod.Items.Single();
        row.SourceKey.Should().Be(nameof(PieceRateProductionTrialSource.ProductionRecord));
        row.SourceChinese.Should().Be("生产记录");
        row.SectionKey.Should().Be(SectionKeys.ColdRollDraw);
        row.SectionKeyChinese.Should().Be(SectionKeys.ToChinese(SectionKeys.ColdRollDraw)); // 中文补齐
        row.StageKey.Should().BeNull();                             // 生产记录无作业阶段
        row.StageChinese.Should().BeNull();
        row.Operator.Should().Be("王五");
        row.RecordDate.Should().Be(new DateTime(2026, 9, 2));

        // 生产记录源（默认源）批次号经导航检索——页面搜索体验主修复
        var byProdBatch = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.ProductionRecord),
            Keyword = batch.BatchNo,
            PageSize = 50
        });
        byProdBatch.TotalCount.Should().Be(1);
        byProdBatch.Items.Single().Id.Should().Be(row.Id);

        // 入缸行阶段接线 + 中文
        var inRow = byOperator.Items.First();
        inRow.StageKey.Should().Be(PieceRateStageKeys.InTank);
        inRow.StageChinese.Should().Be(PieceRateStageKeys.ToChinese(PieceRateStageKeys.InTank));

        // 分页：日期降序首页 = [c, b]，第 2 页余 [a]
        var page = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.PicklingIn),
            PageIndex = 1,
            PageSize = 2
        });
        page.TotalCount.Should().Be(3);
        page.Items.Should().HaveCount(2);
        page.Items.First().Id.Should().Be(c.Id);                    // 最新 InDate 首行
        page.Items.Select(i => i.Id).Should().NotContain(a.Id);     // a 已落第 2 页

        // 缺/非法 Source → BusinessException
        var noSource = async () => await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery());
        (await noSource.Should().ThrowAsync<BusinessException>()).Which.Message.Should().Contain("选择产量源");

        var badSource = async () => await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery { Source = "Nope" });
        (await badSource.Should().ThrowAsync<BusinessException>()).Which.Message.Should().Contain("无效的产量源");
    }

    // ==================== 2026-09-04 补：中文关键字反查 + 断切率/元头接线 ====================

    /// <summary>种一条生产记录（Cut·ColdRoll50 / 冷拔 等，须真挂批次）</summary>
    private static async Task<ProductionRecord> SeedProductionRecordAsync(AppDbContext ctx,
        string sectionName, string processName, string productStatus, DateTime execDate, ProductionBatch batch,
        int? quantity = null, decimal? weight = null, int? faceCutCount = null, decimal? cuttingMultiple = null)
    {
        var rec = new ProductionRecord
        {
            SectionName = sectionName,
            ProcessName = processName,
            ProductStatus = productStatus,
            ManufacturingSpec = "60*3",
            Operator = "王五",
            ExecDate = execDate,
            Quantity = quantity,
            Weight = weight,
            FaceCutCount = faceCutCount,
            CuttingMultiple = cuttingMultiple,
            ProductionBatchId = batch.Id,
            ProductionBatch = batch
        };
        ctx.ProductionRecords.Add(rec);
        await ctx.SaveChangesAsync();
        return rec;
    }

    [Fact]
    public async Task GetTrialRecordsAsync_中文关键字_工段工序子串反查()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var batch = await SeedProductionBatchAsync(ctx);

        // 工序英文 Key（页面显示中文）：冷拔 ColdDraw / 50冷轧 ColdRoll50
        var coldDraw = await SeedProductionRecordAsync(ctx, SectionKeys.ColdRollDraw, ProcessKeys.ColdDraw,
            ProductStatuses.InProgress, new DateTime(2026, 9, 2), batch);
        var coldRoll50 = await SeedProductionRecordAsync(ctx, SectionKeys.Cut, ProcessKeys.ColdRoll50,
            ProductStatuses.Finished, new DateTime(2026, 9, 1), batch);

        // 工序中文「冷拔」→ 反查 ColdDraw 英文 Key → 仅命中冷拔行
        var byProcessCn = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.ProductionRecord),
            Keyword = "冷拔",
            PageSize = 50
        });
        byProcessCn.TotalCount.Should().Be(1);
        byProcessCn.Items.Single().ProcessName.Should().Be(ProcessKeys.ColdDraw);

        // 工序中文「50冷轧」子串 → 反查 ColdRoll50 → 命中 50 冷轧行（工段「冷轧拔」不误伤）
        var byColdRollCn = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.ProductionRecord),
            Keyword = "50冷轧",
            PageSize = 50
        });
        byColdRollCn.TotalCount.Should().Be(1);
        byColdRollCn.Items.Single().Id.Should().Be(coldRoll50.Id);

        // 工段中文「断切」→ 反查 Cut 英文 Key → 命中 Cut 工段行（50冷轧行属 Cut）
        var bySectionCn = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.ProductionRecord),
            Keyword = "断切",
            PageSize = 50
        });
        bySectionCn.TotalCount.Should().Be(1);
        bySectionCn.Items.Single().Id.Should().Be(coldRoll50.Id);

        // 无中文命中的关键字 → 空（哨兵防 SQL 空 IN）
        var none = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.ProductionRecord),
            Keyword = "无此工段名",
            PageSize = 50
        });
        none.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetTrialRecordsAsync_配置改名显示名_反查仍命中()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedProductionBatchAsync(ctx);
        var rec = await SeedProductionRecordAsync(ctx, SectionKeys.Cut, ProcessKeys.ColdRoll50,
            ProductStatuses.Finished, new DateTime(2026, 9, 1), batch);

        // 配置表改名显示名（OverrideMap 生效）→ 反查仍用页面同款显示名 Map 命中英文 Key
        var procMock = new Mock<IProcessDefinitionService>();
        var renamed = new Dictionary<string, string>(ProcessKeys.KeyToChinese, StringComparer.OrdinalIgnoreCase)
        {
            [ProcessKeys.ColdRoll50] = "50冷轧-新车间"
        };
        procMock.Setup(x => x.GetProcessNameMapAsync()).ReturnsAsync(renamed);
        var svc = new PieceRateProductionCategoryService(ctx, SectionDisplayMock(), procMock.Object);

        var hit = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.ProductionRecord),
            Keyword = "新车间",
            PageSize = 50
        });
        hit.TotalCount.Should().Be(1);
        hit.Items.Single().Id.Should().Be(rec.Id);
    }

    [Fact]
    public async Task MatchProductionRecordAsync_荒管断切元头_支数乘平头数折金额()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // Cut × 荒管 × 元/头 PerHead 0.4（真库 cat33 同款：ProductStatus 约束 RoughTube，无工序/阶段约束）
        await svc.SaveAsync(null, new PieceRateProductionCategorySaveRequest
        {
            SectionKey = SectionKeys.Cut,
            ProcessKeys = [],
            ProductStatusKeys = [ProductStatuses.RoughTube],
            StageKeys = [],
            BasePrice = 0.4m,
            Unit = PieceRateUnitKeys.PerHead,
            IsActive = true
        });

        var batch = await SeedProductionBatchAsync(ctx);
        var rec = await SeedProductionRecordAsync(ctx, SectionKeys.Cut, ProcessKeys.RoughTubeProcessing,
            ProductStatuses.RoughTube, new DateTime(2026, 9, 1), batch,
            quantity: 10, weight: 1000, faceCutCount: 2);

        var hit = await svc.MatchProductionRecordAsync(PieceRateProductionTrialSource.ProductionRecord, rec.Id);
        hit.Should().NotBeNull();
        hit!.Unit.Should().Be(PieceRateUnitKeys.PerHead);
        hit.UnitPrice.Should().Be(0.4m);                        // 无维档 → 单价=基准价
        hit.SimulatedAmount.Should().Be(8m);                    // 10 支 × 2 平头 = 20 头 × 0.4

        // 平头数空 → 默认 1：5 支 × 1 × 0.4 = 2 元
        var recNoFace = await SeedProductionRecordAsync(ctx, SectionKeys.Cut, ProcessKeys.RoughTubeProcessing,
            ProductStatuses.RoughTube, new DateTime(2026, 9, 2), batch, quantity: 5, weight: 1000);
        var hitNoFace = await svc.MatchProductionRecordAsync(PieceRateProductionTrialSource.ProductionRecord, recNoFace.Id);
        hitNoFace.Should().NotBeNull();
        hitNoFace!.SimulatedAmount.Should().Be(2m);

        // 缺支数 → null（无法折头数）
        var recNoQty = await SeedProductionRecordAsync(ctx, SectionKeys.Cut, ProcessKeys.RoughTubeProcessing,
            ProductStatuses.RoughTube, new DateTime(2026, 9, 3), batch, weight: 1000);
        var hitNoQty = await svc.MatchProductionRecordAsync(PieceRateProductionTrialSource.ProductionRecord, recNoQty.Id);
        hitNoQty!.SimulatedAmount.Should().BeNull();
    }

    [Fact]
    public async Task GetTrialRecords_生产记录行_平头数接线展示()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var batch = await SeedProductionBatchAsync(ctx);
        var rec = await SeedProductionRecordAsync(ctx, SectionKeys.Cut, ProcessKeys.RoughTubeProcessing,
            ProductStatuses.RoughTube, new DateTime(2026, 9, 1), batch, quantity: 10, faceCutCount: 2);

        var rows = await svc.GetTrialRecordsAsync(new PieceRateProductionTrialRecordQuery
        {
            Source = nameof(PieceRateProductionTrialSource.ProductionRecord),
            PageSize = 50
        });
        var row = rows.Items.Single(i => i.Id == rec.Id);
        row.FaceCutCount.Should().Be(2);                        // 生产记录行暴露平头数（前端展示折算依据）
        row.Quantity.Should().Be(10);
    }
}
