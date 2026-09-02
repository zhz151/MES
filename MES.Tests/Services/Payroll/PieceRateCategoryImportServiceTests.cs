using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OfficeOpenXml;
using MES.Core.Constants;
using MES.Core.Interfaces.Configuration;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Payroll;
using MES.Services.Payroll;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 生产计件类别专用导入/导出测试（2026-09-02）：
/// 定位 = 工段 × 三约束归一组（空=全选）；冲突 = 覆盖更新。
/// 类别模板不动档行、维档模板整组替换、定位缺失报错、组内重叠/重复整组拒存、预览统计、导出双 sheet。
/// </summary>
public class PieceRateCategoryImportServiceTests : TestBase
{
    // ---------- 中文域值（经常量往返，保证与导入解析同口径） ----------

    private static readonly string SectionCn = SectionKeys.ToChinese(SectionKeys.Pickle)!;           // 酸洗
    private static readonly string ProductCn = ProductStatuses.ToChinese(ProductStatuses.RoughTube)!; // 荒管
    private static readonly string StageCn = PieceRateStageKeys.ToChinese(PieceRateStageKeys.InTank)!; // 入缸
    private static readonly string OdCn = PieceRateDimensionKeys.ToChinese(PieceRateDimensionKeys.OuterDiameter)!; // 外径

    private static PieceRateCategoryImportService CreateImportService(AppDbContext ctx)
    {
        var sectionMock = new Mock<ISectionNameDisplayService>();
        sectionMock.Setup(x => x.GetSectionNameMapAsync()).ReturnsAsync(SectionKeys.KeyToChinese);
        return new PieceRateCategoryImportService(ctx, sectionMock.Object, CreateProcessDefinitionServiceMock());
    }

    /// <summary>种一个类别：酸洗 / 工序全选 / 荒管 / 入缸（无进程约束 = 全选）</summary>
    private static async Task<PieceRateProductionCategory> SeedCategoryAsync(AppDbContext ctx,
        decimal basePrice = 35, List<PieceRateProductionCategoryTier>? tiers = null)
    {
        var cat = new PieceRateProductionCategory
        {
            SectionKey = SectionKeys.Pickle,
            BasePrice = basePrice,
            Unit = "PerTon",
            IsActive = true,
            Remark = "seed"
        };
        if (tiers != null) cat.Tiers.AddRange(tiers);
        cat.ConstraintKeys.Add(new PieceRateProductionCategoryKey
        {
            ConstraintType = PieceRateConstraintTypes.ProductStatus,
            Key = ProductStatuses.RoughTube
        });
        cat.ConstraintKeys.Add(new PieceRateProductionCategoryKey
        {
            ConstraintType = PieceRateConstraintTypes.Stage,
            Key = PieceRateStageKeys.InTank
        });
        ctx.PieceRateProductionCategories.Add(cat);
        await ctx.SaveChangesAsync();
        return cat;
    }

    private static PieceRateProductionCategoryTier OdTier(string rangeText, decimal ratio, decimal? min = null, decimal? max = null)
        => new()
        {
            DimensionKey = PieceRateDimensionKeys.OuterDiameter,
            RangeText = rangeText,
            MinValue = min,
            MaxValue = max,
            Ratio = ratio,
            IsActive = true
        };

    // ---------- Excel 构造 ----------

    private static byte[] BuildCategoryFile(params string?[][] rows)
        => BuildSheet("类别",
            ["工段", "工序", "产类", "阶段", "基准价", "结算单位", "启用", "备注"], rows);

    private static byte[] BuildTierFile(params string?[][] rows)
        => BuildSheet("维档",
            ["工段", "工序", "产类", "阶段", "维度", "档值", "系数", "启用"], rows);

    private static byte[] BuildSheet(string sheetName, string[] headers, params string?[][] rows)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add(sheetName);
        for (var c = 0; c < headers.Length; c++) ws.Cells[1, c + 1].Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < headers.Length && c < rows[r].Length; c++)
                if (!string.IsNullOrEmpty(rows[r][c])) ws.Cells[r + 2, c + 1].Value = rows[r][c];
        return package.GetAsByteArray();
    }

    private static string?[] CategoryRow(string? remark = "file", string basePrice = "40",
        string isActive = "是", string? remarkOverride = null)
        => [SectionCn, null, ProductCn, StageCn, basePrice, "元/吨", isActive, remarkOverride ?? remark];

    // ==================== 类别定义导入 ====================

    [Fact]
    public async Task PreviewAndImport_Category_未命中新建类别无档行()
    {
        using var ctx = CreateDbContext();
        var svc = CreateImportService(ctx);
        var file = BuildCategoryFile(CategoryRow("首批类别"));

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Category, file);
        preview.TotalRows.Should().Be(1);
        preview.ErrorCount.Should().Be(0);
        preview.AddCount.Should().Be(1);
        preview.OverwriteCount.Should().Be(0);
        preview.RowResults[0].RowAction.Should().Be("新增");

        var result = await svc.ImportAsync(PieceRateImportKinds.Category, file);
        result.HasRolledBack.Should().BeFalse();
        result.SuccessCount.Should().Be(1);

        var stored = await ctx.PieceRateProductionCategories
            .Include(c => c.Tiers).Include(c => c.ConstraintKeys).SingleAsync();
        stored.SectionKey.Should().Be(SectionKeys.Pickle);
        stored.BasePrice.Should().Be(40m);
        stored.Unit.Should().Be("PerTon");
        stored.IsActive.Should().BeTrue();
        stored.Remark.Should().Be("首批类别");
        stored.Tiers.Should().BeEmpty();
        stored.ConstraintKeys.Where(k => k.ConstraintType == PieceRateConstraintTypes.Process).Should().BeEmpty();
        stored.ConstraintKeys.Where(k => k.ConstraintType == PieceRateConstraintTypes.ProductStatus)
            .Select(k => k.Key).Should().Contain(ProductStatuses.RoughTube);
        stored.ConstraintKeys.Where(k => k.ConstraintType == PieceRateConstraintTypes.Stage)
            .Select(k => k.Key).Should().Contain(PieceRateStageKeys.InTank);
    }

    [Fact]
    public async Task Import_Category_字段非法整行标错不落库()
    {
        using var ctx = CreateDbContext();
        var svc = CreateImportService(ctx);
        var file = BuildCategoryFile([SectionCn, null, ProductCn, StageCn, "40", "不存在的单位", "是", "坏行"]);

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Category, file);
        preview.ErrorCount.Should().Be(1);
        preview.RowResults.Single(r => !r.IsValid).Errors.Should().Contain(e => e.Contains("无效的结算单位"));

        var result = await svc.ImportAsync(PieceRateImportKinds.Category, file);
        result.HasRolledBack.Should().BeTrue();
        ctx.PieceRateProductionCategories.Count().Should().Be(0);
    }

    [Fact]
    public async Task Import_Category_覆盖定义不动既有档行()
    {
        using var ctx = CreateDbContext();
        var seeded = await SeedCategoryAsync(ctx, basePrice: 35,
            tiers: [OdTier(">54", 1.1m, min: 54), OdTier("30-54", 1.05m, min: 30, max: 54)]);

        var svc = CreateImportService(ctx);
        var file = BuildCategoryFile(CategoryRow("改价", basePrice: "60"));

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Category, file);
        preview.OverwriteCount.Should().Be(1);
        preview.AddCount.Should().Be(0);

        var result = await svc.ImportAsync(PieceRateImportKinds.Category, file);
        result.HasRolledBack.Should().BeFalse();

        var stored = await ctx.PieceRateProductionCategories
            .Include(c => c.Tiers).Include(c => c.ConstraintKeys).SingleAsync();
        stored.Id.Should().Be(seeded.Id);
        stored.BasePrice.Should().Be(60m);
        stored.Remark.Should().Be("改价");
        // 类别模板绝不清档：既有两档仍完整保留
        stored.Tiers.Select(t => t.RangeText).Should().BeEquivalentTo([">54", "30-54"]);
        stored.Tiers.Single(t => t.RangeText == ">54").Ratio.Should().Be(1.1m);
    }

    [Fact]
    public async Task Import_Category_文件内重复定位整体拒绝()
    {
        using var ctx = CreateDbContext();
        var svc = CreateImportService(ctx);
        var file = BuildCategoryFile(CategoryRow("a"), CategoryRow("b"));

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Category, file);
        preview.ErrorCount.Should().Be(1);
        preview.RowResults.Single(r => !r.IsValid).Errors.Should().Contain(e => e.Contains("重复定位"));

        var result = await svc.ImportAsync(PieceRateImportKinds.Category, file);
        result.HasRolledBack.Should().BeTrue();
        ctx.PieceRateProductionCategories.Count().Should().Be(0);
    }

    [Fact]
    public async Task Import_Category_与既有启用类别覆盖冲突整体拒绝()
    {
        using var ctx = CreateDbContext();
        // 既有：酸洗/荒管/入缸（active）；导入一个覆盖更窄但被包含的类别 → 覆盖相交
        await SeedCategoryAsync(ctx);
        var svc = CreateImportService(ctx);
        var file = BuildCategoryFile(new string?[]
        {
            SectionCn, ProcessKeys.ToChinese(ProcessKeys.ColdRoll60)!, ProductCn, StageCn, "50", "元/吨", "是", "窄覆盖"
        });

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Category, file);
        preview.ErrorCount.Should().Be(1);
        preview.RowResults.Single(r => !r.IsValid).Errors.Should().Contain(e => e.Contains("禁止交集"));

        var result = await svc.ImportAsync(PieceRateImportKinds.Category, file);
        result.HasRolledBack.Should().BeTrue();
    }

    // ==================== 维档导入 ====================

    [Fact]
    public async Task PreviewAndImport_Tier_定位类别整组替换档行()
    {
        using var ctx = CreateDbContext();
        await SeedCategoryAsync(ctx, tiers: [OdTier(">54", 1.1m, min: 54)]); // 旧档 1 行

        var svc = CreateImportService(ctx);
        var file = BuildTierFile(
            [SectionCn, null, ProductCn, StageCn, OdCn, ">76", "1.2", "是"],
            [SectionCn, null, ProductCn, StageCn, OdCn, "30-54", "1.05", "是"]);

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Tier, file);
        preview.TotalRows.Should().Be(2);
        preview.ErrorCount.Should().Be(0);
        preview.OverwriteCount.Should().Be(2);

        var result = await svc.ImportAsync(PieceRateImportKinds.Tier, file);
        result.HasRolledBack.Should().BeFalse();

        var stored = await ctx.PieceRateProductionCategories
            .Include(c => c.Tiers).Include(c => c.ConstraintKeys).SingleAsync();
        stored.Tiers.Should().HaveCount(2);                 // 旧档已整组替换
        stored.Tiers.Select(t => t.RangeText).Should().BeEquivalentTo([">76", "30-54"]);
        stored.Tiers.Single(t => t.RangeText == ">76").Ratio.Should().Be(1.2m);
        stored.Tiers.Single(t => t.RangeText == "30-54").Ratio.Should().Be(1.05m);
        stored.ConstraintKeys.Where(k => k.ConstraintType == PieceRateConstraintTypes.ProductStatus)
            .Select(k => k.Key).Should().Contain(ProductStatuses.RoughTube);
    }

    [Fact]
    public async Task Import_Tier_定位类别不存在整行报错()
    {
        using var ctx = CreateDbContext(); // 无类别
        var svc = CreateImportService(ctx);
        var file = BuildTierFile([SectionCn, null, ProductCn, StageCn, OdCn, ">54", "1.1", "是"]);

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Tier, file);
        preview.ErrorCount.Should().Be(1);
        preview.RowResults.Single(r => !r.IsValid).Errors.Should().Contain(e => e.Contains("请先导入类别定义"));

        var result = await svc.ImportAsync(PieceRateImportKinds.Tier, file);
        result.HasRolledBack.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("请先导入类别定义"));
    }

    [Fact]
    public async Task Import_Tier_组内区间重叠整组拒绝不动原档()
    {
        using var ctx = CreateDbContext();
        await SeedCategoryAsync(ctx, tiers: [OdTier("30-54", 1.05m, min: 30, max: 54)]);

        var svc = CreateImportService(ctx);
        var file = BuildTierFile(
            [SectionCn, null, ProductCn, StageCn, OdCn, ">54", "1.1", "是"],      // 54 起
            [SectionCn, null, ProductCn, StageCn, OdCn, "30-76", "1.2", "是"]);   // 与上行重叠

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Tier, file);
        preview.ErrorCount.Should().Be(2); // 同组违例 → 整组每行都标错

        var result = await svc.ImportAsync(PieceRateImportKinds.Tier, file);
        result.HasRolledBack.Should().BeTrue();

        var stored = await ctx.PieceRateProductionCategories.Include(c => c.Tiers).SingleAsync();
        stored.Tiers.Should().HaveCount(1); // 原档不动
        stored.Tiers.Single().RangeText.Should().Be("30-54");
    }

    [Fact]
    public async Task Import_Tier_等值维重复整组拒绝()
    {
        using var ctx = CreateDbContext();
        await SeedCategoryAsync(ctx);

        var svc = CreateImportService(ctx);
        var bright = PieceRateStateKeys.ToChinese(PieceRateStateKeys.Bright)!;
        var stateCn = PieceRateDimensionKeys.ToChinese(PieceRateDimensionKeys.SpecialState)!;
        var file = BuildTierFile(
            [SectionCn, null, ProductCn, StageCn, stateCn, bright, "1.35", "是"],
            [SectionCn, null, ProductCn, StageCn, stateCn, bright, "1.4", "是"]); // 取值重复

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Tier, file);
        preview.ErrorCount.Should().Be(2);
        preview.RowResults.First(r => !r.IsValid).Errors.Should().Contain(e => e.Contains("取值重复"));

        var result = await svc.ImportAsync(PieceRateImportKinds.Tier, file);
        result.HasRolledBack.Should().BeTrue();
    }

    // ==================== 导出 / 模板 ====================

    [Fact]
    public async Task Export_双Sheet往返包含类别与档行()
    {
        using var ctx = CreateDbContext();
        await SeedCategoryAsync(ctx, tiers: [OdTier(">54", 1.1m, min: 54)]);

        var svc = CreateImportService(ctx);
        var bytes = await svc.ExportAsync();

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new MemoryStream(bytes));
        package.Workbook.Worksheets.Count.Should().Be(2);
        var catSheet = package.Workbook.Worksheets["类别"];
        var tierSheet = package.Workbook.Worksheets["维档"];
        catSheet.Should().NotBeNull();
        tierSheet.Should().NotBeNull();
        (catSheet!.Dimension?.Rows ?? 0).Should().Be(2);   // 表头 + 1 类别
        (tierSheet!.Dimension?.Rows ?? 0).Should().Be(2);  // 表头 + 1 档行
        catSheet.Cells[2, 1].Text.Should().Be(SectionCn);
        catSheet.Cells[2, 5].Value.ToString().Should().Be("35");
        tierSheet.Cells[2, 5].Text.Should().Be(OdCn);
        tierSheet.Cells[2, 6].Text.Should().Be(">54");
    }

    [Fact]
    public async Task GenerateTemplate_两类单Sheet含示例行()
    {
        using var ctx = CreateDbContext();
        var svc = CreateImportService(ctx);

        foreach (var kind in PieceRateImportKinds.All)
        {
            var bytes = await svc.GenerateTemplateAsync(kind);
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(new MemoryStream(bytes));
            var sheet = package.Workbook.Worksheets[0];
            sheet.Dimension!.Rows.Should().Be(2); // 表头 + 1 示例行
            sheet.Cells[2, 1].Text.Should().NotBeNullOrWhiteSpace();
        }
    }
}
