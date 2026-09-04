using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;
using MES.Services.Payroll;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 计件工资共享采集器 PieceRateCollector 定向测试（2026-09-04 定尺接线 + 未匹配提醒拆分）：
/// ①切行接线：Length=FinishedCutLength、FixedLengthCount=批次 ItemDetails 去重定尺种数，两维档命中连乘金额；
/// ②提醒拆分：仅「已记录到量但命中不到启用类别」计 unpriced；无产出量 / 命中类别但数量缺失折算 0 → 静默不计。
/// 直接 new PieceRateCollector(ctx).CollectAsync 驱动，不经工资结算服务。
/// </summary>
public class PieceRateCollectorTests : TestBase
{
    // ==================== 种子 Helper ====================

    private static async Task<Employee> SeedEmployeeAsync(AppDbContext ctx, string code, string name)
    {
        var e = new Employee
        {
            Code = code,
            Name = name,
            SalaryMode = SalaryMode.PieceIndividual,
            IsActive = true
        };
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx,
        string lengthStatus, string? itemDetails, string spec = "219*8")
    {
        var batch = new ProductionBatch
        {
            BatchNo = "BATCH-CUT-" + Guid.NewGuid().ToString("N")[..8],
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
            LengthStatus = lengthStatus,
            TechnicalRequirements = "无",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            TotalQuantity = 100,
            TotalMeters = 1000m,
            TotalWeight = 5000m,
            TotalItemCount = 1,
            ItemDetails = itemDetails
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    /// <summary>切工段计件类别种子（SectionKey=Cut；constraints 空=工序/产类/阶段全选，dimensionKeys 档行后补）</summary>
    private static async Task<PieceRateProductionCategory> SeedCutCategoryAsync(AppDbContext ctx,
        decimal basePrice, string unit, IReadOnlyList<(string DimKey, decimal? Min, decimal? Max, int? MinInt, int? MaxInt, decimal Ratio)> tiers)
    {
        var cat = new PieceRateProductionCategory
        {
            SectionKey = SectionKeys.Cut,
            BasePrice = basePrice,
            Unit = unit,
            IsActive = true,
            ConstraintKeys = new List<PieceRateProductionCategoryKey>(),
            Tiers = new List<PieceRateProductionCategoryTier>()
        };
        ctx.PieceRateProductionCategories.Add(cat);
        await ctx.SaveChangesAsync();
        foreach (var (dim, min, max, minInt, maxInt, ratio) in tiers)
        {
            var t = new PieceRateProductionCategoryTier
            {
                CategoryId = cat.Id,
                DimensionKey = dim,
                MinValue = min,
                MaxValue = max,
                MinInt = minInt,
                MaxInt = maxInt,
                Ratio = ratio,
                IsActive = true
            };
            cat.Tiers.Add(t);
            ctx.PieceRateProductionCategoryTiers.Add(t);
        }
        await ctx.SaveChangesAsync();
        return cat;
    }

    /// <summary>添加一条当月切工段生产记录（写名人 = code/name；Weight kg）</summary>
    private static async Task SeedCutRecordAsync(AppDbContext ctx, ProductionBatch batch,
        string operatorText, decimal? weightKg, int? quantity, decimal? finishedCutLength)
    {
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            ProcessGroupId = 1,
            ProcessName = "Cut",
            SectionName = SectionKeys.Cut,
            ExecDate = new DateTime(2024, 3, 12),
            Operator = operatorText,
            ProductStatus = ProductStatuses.InProgress,
            Weight = weightKg,
            Quantity = quantity,
            FinishedCutLength = finishedCutLength
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task<CollectResult> CollectMonthAsync(AppDbContext ctx, Employee emp)
        => await new PieceRateCollector(ctx).CollectAsync(new DateTime(2024, 3, 1), new DateTime(2024, 4, 1), new[] { emp });

    // ==================== A. 定尺接线（计划口径） ====================

    [Fact]
    public async Task CollectAsync_切定尺行_长度与定尺种数档命中_金额连乘()
    {
        using var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "YGC1", "切一");
        var batch = await SeedBatchAsync(ctx, "Fixed",
            "5,14154mm,30支;6,14241mm,24支;7,14328mm,14支;8,14415mm,12支;");
        // Category 37 同款：Base=40 元/吨；Length >14000→0.55；FixedLengthCount 3-5→0.85
        await SeedCutCategoryAsync(ctx, 40m, PieceRateUnitKeys.PerTon,
        [
            (PieceRateDimensionKeys.Length, 14000m, null, null, null, 0.55m),
            (PieceRateDimensionKeys.FixedLengthCount, null, null, 3, 5, 0.85m)
        ]);
        // 1000kg=1吨；切 4 种定尺 → 种数 4 落 3-5 档 ×0.85；长度 14415mm 落 >14000 档 ×0.55
        await SeedCutRecordAsync(ctx, batch, OperatorNameHelper.Format("切一", "YGC1"), 1000m, null, 14415m);

        var res = await CollectMonthAsync(ctx, emp);

        res.Rows.Should().HaveCount(1);
        res.Rows[0].TotalHeadcount.Should().Be(1);
        res.Rows[0].Amount.Should().Be(40m * 0.55m * 0.85m); // 1 吨 × 40 × Length0.55 × FixedCount0.85
        res.UnpricedCount.Should().Be(0);
    }

    [Fact]
    public async Task CollectAsync_切行批次明细缺失_定尺维不乘仅长度档命中()
    {
        using var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "YGC2", "切二");
        var batch = await SeedBatchAsync(ctx, "Fixed", null); // 定尺批但 ItemDetails 缺失 → 种数未知
        await SeedCutCategoryAsync(ctx, 40m, PieceRateUnitKeys.PerTon,
        [
            (PieceRateDimensionKeys.Length, 14000m, null, null, null, 0.55m),
            (PieceRateDimensionKeys.FixedLengthCount, null, null, 3, 5, 0.85m)
        ]);
        await SeedCutRecordAsync(ctx, batch, OperatorNameHelper.Format("切二", "YGC2"), 1000m, null, 14415m);

        var res = await CollectMonthAsync(ctx, emp);

        // FixedLengthCount 缺值 → 引擎跳过该维（系数 1）；仅 Length >14000 ×0.55
        res.Rows.Should().ContainSingle();
        res.Rows[0].Amount.Should().Be(40m * 0.55m);
        res.UnpricedCount.Should().Be(0);
    }

    // ==================== B. 未匹配提醒拆分 ====================

    [Fact]
    public async Task CollectAsync_未定价但有量_计入提醒()
    {
        using var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "YGB1", "量一");
        var batch = await SeedBatchAsync(ctx, "Fixed", null);
        // 不注册任何启用类别 → 命中 null；行有量(Weight=500>0) → 真缺口，进 unpriced
        await SeedCutRecordAsync(ctx, batch, OperatorNameHelper.Format("量一", "YGB1"), 500m, null, 6000m);

        var res = await CollectMonthAsync(ctx, emp);

        res.Rows.Should().BeEmpty();
        res.UnpricedCount.Should().Be(1);
    }

    [Fact]
    public async Task CollectAsync_未定价但全零量_静默()
    {
        using var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "YGB2", "空一");
        var batch = await SeedBatchAsync(ctx, "Fixed", null);
        // 虚拟补录数量空（Weight/Quantity 均 0）→ 无产出量，静默不计 unpriced
        await SeedCutRecordAsync(ctx, batch, OperatorNameHelper.Format("空一", "YGB2"), 0m, 0, null);

        var res = await CollectMonthAsync(ctx, emp);

        res.Rows.Should().BeEmpty();
        res.UnpricedCount.Should().Be(0);
    }

    [Fact]
    public async Task CollectAsync_命中类别但重量缺失_数量问题静默()
    {
        using var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "YGB3", "缺量一");
        var batch = await SeedBatchAsync(ctx, "Fixed", null);
        // 命中 PerTon 类别但无重量（Quantity=0）→ 金额折算不出 → 数量问题静默（不发工资不进提醒）
        await SeedCutCategoryAsync(ctx, 40m, PieceRateUnitKeys.PerTon, []);
        await SeedCutRecordAsync(ctx, batch, OperatorNameHelper.Format("缺量一", "YGB3"), null, 0, null);

        var res = await CollectMonthAsync(ctx, emp);

        res.Rows.Should().BeEmpty();
        res.UnpricedCount.Should().Be(0);
    }
}
