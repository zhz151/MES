using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Payroll;
using MES.Services.Payroll;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 成检计件类别专用导入/导出测试（2026-09-03）：
/// 定位 = 成检项目（InspectionItem 单键）；冲突 = 覆盖更新。
/// 类别模板不动档行、维档模板整组替换、定位缺失报错、组内重叠/重复整组拒存、
/// 检验支数整数带落库、预览统计、导出双 sheet（含长度状态中文回显）。
/// </summary>
public class PieceRateFinalInspectionCategoryImportServiceTests : TestBase
{
    // ---------- 中文域值（经常量/枚举往返，保证与导入解析同口径） ----------

    private static string ItemCn(InspectionItem item)
        => EnumHelper.GetDisplayName<InspectionItem>(item.ToString())!;

    private static readonly string UltrasonicCn = ItemCn(InspectionItem.Ultrasonic);       // 超声波
    private static readonly string OdCn = PieceRateInspectionDimensionKeys.ToChinese(PieceRateInspectionDimensionKeys.OuterDiameter)!;         // 外径
    private static readonly string CountCn = PieceRateInspectionDimensionKeys.ToChinese(PieceRateInspectionDimensionKeys.InspectionCount)!;      // 检验支数
    private static readonly string LengthStatusCn = PieceRateInspectionDimensionKeys.ToChinese(PieceRateInspectionDimensionKeys.LengthStatus)!;  // 长度状态
    private static readonly string FixedCn = EnumHelper.GetDisplayName<LengthStatus>(nameof(LengthStatus.Fixed))!;                              // 定尺

    private static PieceRateFinalInspectionCategoryImportService CreateImportService(AppDbContext ctx)
        => new(ctx);

    /// <summary>种一个「超声波」启用类别（可带既有档行）</summary>
    private static async Task<PieceRateFinalInspectionCategory> SeedUltrasonicAsync(AppDbContext ctx,
        decimal basePrice = 35, List<PieceRateFinalInspectionCategoryTier>? tiers = null)
    {
        var cat = new PieceRateFinalInspectionCategory
        {
            ItemKey = nameof(InspectionItem.Ultrasonic),
            BasePrice = basePrice,
            Unit = "PerTon",
            IsActive = true,
            Remark = "seed"
        };
        if (tiers != null) cat.Tiers.AddRange(tiers);
        ctx.PieceRateFinalInspectionCategories.Add(cat);
        await ctx.SaveChangesAsync();
        return cat;
    }

    private static PieceRateFinalInspectionCategoryTier OdTier(string rangeText, decimal ratio,
        decimal? min = null, decimal? max = null)
        => new()
        {
            DimensionKey = PieceRateInspectionDimensionKeys.OuterDiameter,
            RangeText = rangeText,
            MinValue = min,
            MaxValue = max,
            Ratio = ratio,
            IsActive = true
        };

    // ---------- Excel 构造 ----------

    private static byte[] BuildCategoryFile(params string?[][] rows)
        => BuildSheet("类别", ["成检项目", "基准价", "结算单位", "启用", "备注"], rows);

    private static byte[] BuildTierFile(params string?[][] rows)
        => BuildSheet("维档", ["成检项目", "维度", "档值", "系数", "启用"], rows);

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
        => [UltrasonicCn, basePrice, "元/吨", isActive, remarkOverride ?? remark];

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

        var stored = await ctx.PieceRateFinalInspectionCategories.Include(c => c.Tiers).SingleAsync();
        stored.ItemKey.Should().Be(nameof(InspectionItem.Ultrasonic)); // 中文项目归一为枚举名
        stored.BasePrice.Should().Be(40m);
        stored.Unit.Should().Be("PerTon");
        stored.IsActive.Should().BeTrue();
        stored.Remark.Should().Be("首批类别");
        stored.Tiers.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_Category_字段非法整行标错不落库()
    {
        using var ctx = CreateDbContext();
        var svc = CreateImportService(ctx);
        var file = BuildCategoryFile([UltrasonicCn, "40", "不存在的单位", "是", "坏行"]);

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Category, file);
        preview.ErrorCount.Should().Be(1);
        preview.RowResults.Single(r => !r.IsValid).Errors.Should().Contain(e => e.Contains("无效的结算单位"));

        var result = await svc.ImportAsync(PieceRateImportKinds.Category, file);
        result.HasRolledBack.Should().BeTrue();
        ctx.PieceRateFinalInspectionCategories.Count().Should().Be(0);
    }

    [Fact]
    public async Task Import_Category_覆盖定义不动既有档行()
    {
        using var ctx = CreateDbContext();
        await SeedUltrasonicAsync(ctx, basePrice: 35,
            tiers: [OdTier(">54", 1.1m, min: 54)]);

        var svc = CreateImportService(ctx);
        var file = BuildCategoryFile(CategoryRow("改价", basePrice: "60"));

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Category, file);
        preview.OverwriteCount.Should().Be(1);
        preview.AddCount.Should().Be(0);
        preview.RowResults[0].RowAction.Should().Be("覆盖");

        var result = await svc.ImportAsync(PieceRateImportKinds.Category, file);
        result.HasRolledBack.Should().BeFalse();

        var stored = await ctx.PieceRateFinalInspectionCategories.Include(c => c.Tiers).SingleAsync();
        stored.BasePrice.Should().Be(60m);
        stored.Remark.Should().Be("改价");
        // 类别模板绝不清档：既有档行仍完整保留
        stored.Tiers.Should().ContainSingle();
        stored.Tiers.Single().RangeText.Should().Be(">54");
        stored.Tiers.Single().Ratio.Should().Be(1.1m);
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
        ctx.PieceRateFinalInspectionCategories.Count().Should().Be(0);
    }

    [Fact]
    public async Task Import_Category_既有违例启用同项目冲突整体拒绝()
    {
        using var ctx = CreateDbContext();
        // 绕过唯一校验直插两条启用同类（模拟历史违例；InMemory 无过滤唯一索引兜底）→ 导入启用行触「同项目启用唯一」
        ctx.PieceRateFinalInspectionCategories.AddRange(
            new PieceRateFinalInspectionCategory { ItemKey = nameof(InspectionItem.Ultrasonic), IsActive = true, BasePrice = 10, Unit = "PerTon" },
            new PieceRateFinalInspectionCategory { ItemKey = nameof(InspectionItem.Ultrasonic), IsActive = true, BasePrice = 20, Unit = "PerTon" });
        await ctx.SaveChangesAsync();

        var svc = CreateImportService(ctx);
        var file = BuildCategoryFile(CategoryRow("新价", basePrice: "50"));

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Category, file);
        preview.ErrorCount.Should().Be(1);
        preview.RowResults.Single(r => !r.IsValid).Errors.Should().Contain(e => e.Contains("同项目启用唯一"));

        var result = await svc.ImportAsync(PieceRateImportKinds.Category, file);
        result.HasRolledBack.Should().BeTrue();
    }

    // ==================== 维档导入 ====================

    [Fact]
    public async Task PreviewAndImport_Tier_定位类别整组替换档行()
    {
        using var ctx = CreateDbContext();
        await SeedUltrasonicAsync(ctx, tiers: [OdTier(">54", 1.1m, min: 54)]); // 旧档 1 行

        var svc = CreateImportService(ctx);
        var file = BuildTierFile(
            [UltrasonicCn, OdCn, ">76", "1.2", "是"],
            [UltrasonicCn, OdCn, "30-54", "1.05", "是"],
            [UltrasonicCn, LengthStatusCn, FixedCn, "1.1", "是"]); // 长度状态 定尺 等值行

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Tier, file);
        preview.TotalRows.Should().Be(3);
        preview.ErrorCount.Should().Be(0);
        preview.OverwriteCount.Should().Be(3);

        var result = await svc.ImportAsync(PieceRateImportKinds.Tier, file);
        result.HasRolledBack.Should().BeFalse();

        var stored = await ctx.PieceRateFinalInspectionCategories.Include(c => c.Tiers).SingleAsync();
        stored.Tiers.Should().HaveCount(3); // 旧档已整组替换
        stored.Tiers.Select(t => t.RangeText).Should().BeEquivalentTo([">76", "30-54", "Fixed"]);
        stored.Tiers.Single(t => t.RangeText == ">76").Ratio.Should().Be(1.2m);
        stored.Tiers.Single(t => t.RangeText == "30-54").Ratio.Should().Be(1.05m);
        // 等值维中文 → 英文 Key 归一落库
        stored.Tiers.Single(t => t.DimensionKey == PieceRateInspectionDimensionKeys.LengthStatus)
            .MatchValue.Should().Be(nameof(LengthStatus.Fixed));
    }

    [Fact]
    public async Task Import_Tier_检验支数整数闭区间邻接合法并落库()
    {
        using var ctx = CreateDbContext();
        await SeedUltrasonicAsync(ctx); // 无档

        var svc = CreateImportService(ctx);
        var file = BuildTierFile(
            [UltrasonicCn, CountCn, "1-10", "2", "是"],
            [UltrasonicCn, CountCn, "11-9999", "1", "是"]); // 整数档不可共享边界，高档从 11 起

        var result = await svc.ImportAsync(PieceRateImportKinds.Tier, file);
        result.HasRolledBack.Should().BeFalse();

        var stored = await ctx.PieceRateFinalInspectionCategories.Include(c => c.Tiers).SingleAsync();
        stored.Tiers.Should().HaveCount(2);
        var low = stored.Tiers.Single(t => t.RangeText == "1-10");
        low.MinInt.Should().Be(1);
        low.MaxInt.Should().Be(10);
        low.Ratio.Should().Be(2m);
        var high = stored.Tiers.Single(t => t.RangeText == "11-9999");
        high.MinInt.Should().Be(11);
        high.MaxInt.Should().Be(9999);
        high.Ratio.Should().Be(1m);
    }

    [Fact]
    public async Task Import_Tier_检验支数小数边界整行报错()
    {
        using var ctx = CreateDbContext();
        await SeedUltrasonicAsync(ctx);

        var svc = CreateImportService(ctx);
        var file = BuildTierFile([UltrasonicCn, CountCn, "1.5-10", "2", "是"]);

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Tier, file);
        preview.ErrorCount.Should().Be(1);
        preview.RowResults.Single(r => !r.IsValid).Errors.Should().Contain(e => e.Contains("必须为整数区间"));

        var result = await svc.ImportAsync(PieceRateImportKinds.Tier, file);
        result.HasRolledBack.Should().BeTrue();
    }

    [Fact]
    public async Task Import_Tier_定位类别不存在整行报错()
    {
        using var ctx = CreateDbContext(); // 无类别
        var svc = CreateImportService(ctx);
        var file = BuildTierFile([UltrasonicCn, OdCn, ">54", "1.1", "是"]);

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
        await SeedUltrasonicAsync(ctx, tiers: [OdTier("30-54", 1.05m, min: 30, max: 54)]);

        var svc = CreateImportService(ctx);
        var file = BuildTierFile(
            [UltrasonicCn, OdCn, ">54", "1.1", "是"],      // 54 起
            [UltrasonicCn, OdCn, "30-76", "1.2", "是"]);   // 与上行重叠

        var preview = await svc.PreviewImportAsync(PieceRateImportKinds.Tier, file);
        preview.ErrorCount.Should().Be(2); // 同组违例 → 整组每行都标错
        preview.RowResults.First(r => !r.IsValid).Errors.Should().Contain(e => e.Contains("区间重叠"));

        var result = await svc.ImportAsync(PieceRateImportKinds.Tier, file);
        result.HasRolledBack.Should().BeTrue();

        var stored = await ctx.PieceRateFinalInspectionCategories.Include(c => c.Tiers).SingleAsync();
        stored.Tiers.Should().ContainSingle(); // 原档不动
        stored.Tiers.Single().RangeText.Should().Be("30-54");
    }

    [Fact]
    public async Task Import_Tier_等值维重复整组拒绝()
    {
        using var ctx = CreateDbContext();
        await SeedUltrasonicAsync(ctx);

        var svc = CreateImportService(ctx);
        var file = BuildTierFile(
            [UltrasonicCn, LengthStatusCn, FixedCn, "1.1", "是"],
            [UltrasonicCn, LengthStatusCn, FixedCn, "1.2", "是"]); // 取值重复（中文归一后同为 Fixed）

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
        await SeedUltrasonicAsync(ctx, basePrice: 35,
            tiers:
            [
                OdTier(">54", 1.1m, min: 54),
                new PieceRateFinalInspectionCategoryTier
                {
                    DimensionKey = PieceRateInspectionDimensionKeys.LengthStatus,
                    RangeText = nameof(LengthStatus.Fixed),
                    MatchValue = nameof(LengthStatus.Fixed),
                    Ratio = 1.1m,
                    IsActive = true
                }
            ]);

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
        (tierSheet!.Dimension?.Rows ?? 0).Should().Be(3);  // 表头 + 2 档行
        catSheet.Cells[2, 1].Text.Should().Be(UltrasonicCn);
        Convert.ToDecimal(catSheet.Cells[2, 2].Value).Should().Be(35m);
        catSheet.Cells[2, 3].Text.Should().Be("元/吨");
        catSheet.Cells[2, 4].Text.Should().Be("是");
        tierSheet.Cells[2, 2].Text.Should().Be(OdCn);
        tierSheet.Cells[2, 3].Text.Should().Be(">54");
        tierSheet.Cells[3, 2].Text.Should().Be(LengthStatusCn);
        tierSheet.Cells[3, 3].Text.Should().Be(FixedCn);  // 长度状态 Key → 中文回显
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
