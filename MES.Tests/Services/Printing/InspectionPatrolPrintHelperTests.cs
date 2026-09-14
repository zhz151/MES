using FluentAssertions;
using MES.Core.Constants;
using MES.Data.Entities.Quality;
using MES.Services.Printing;
using QuestPDF.Infrastructure;
using Xunit;

namespace MES.Tests.Services.Printing;

/// <summary>
/// 巡检单打印版式回归（2026-09-12 拍板，二次版式「单列每页 2 张 / 290pt」）：
/// 字段页（含 G3 明细）恒为单页；巡检照片与整改验证照片合并为同一张连续表格并统一另起第 2 页，
/// 单列每页 2 张 → 满载 <see cref="QualityPhotoLimits.PerType"/> × 2 张占 4 页照片。
/// </summary>
public class InspectionPatrolPrintHelperTests
{
    static InspectionPatrolPrintHelperTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void GeneratePdf_明细六项_涉及整改_无照片_单页()
    {
        var pdf = InspectionPatrolPrintHelper.GeneratePdf(
            BuildEntity(true), BuildItems(6), NoImages, NoImages);

        PrintTestAssets.CountPdfPages(pdf).Should().Be(1);
    }

    [Fact]
    public void GeneratePdf_明细六项_涉及整改_两类各满张照片_字段页加四页照片()
    {
        var per = QualityPhotoLimits.PerType;
        var pdf = InspectionPatrolPrintHelper.GeneratePdf(
            BuildEntity(true), BuildItems(6), BuildImages(per), BuildImages(per));

        PrintTestAssets.CountPdfPages(pdf).Should().Be(5);
    }

    [Fact]
    public void GeneratePdf_明细六项_仅巡检照片满张_字段页加两页照片()
    {
        var pdf = InspectionPatrolPrintHelper.GeneratePdf(
            BuildEntity(false), BuildItems(6), BuildImages(QualityPhotoLimits.PerType), NoImages);

        PrintTestAssets.CountPdfPages(pdf).Should().Be(3);
    }

    [Fact]
    public void GeneratePdf_明细六项_仅整改验证照片满张_字段页加两页照片()
    {
        var pdf = InspectionPatrolPrintHelper.GeneratePdf(
            BuildEntity(true), BuildItems(6), NoImages, BuildImages(QualityPhotoLimits.PerType));

        PrintTestAssets.CountPdfPages(pdf).Should().Be(3);
    }

    private static readonly IReadOnlyList<InspectionPatrolPrintHelper.PrintImage> NoImages =
        Array.Empty<InspectionPatrolPrintHelper.PrintImage>();

    private static InspectionPatrol BuildEntity(bool needRectification) => new()
    {
        Id = 5,
        PatrolDate = new DateTime(2026, 9, 12),
        Inspector = "张三",
        DataSource = "SCAN",
        BatchNo = "M26-09-001-01",
        WorkOrderNo = "WO20260901-001-01",
        ProcessName = "ColdRollDraw",
        ManufacturingSpec = "60*6.2*8000",
        SectionName = "ColdRoll",
        SequenceNumber = 3,
        ProductStatus = "WorkInProgress",
        PlantGrade = "304",
        ProductionUnit = "冷轧车间",
        EquipmentName = "冷轧机-1",
        ProductionOperator = "李四",
        NeedRectification = needRectification,
        RectificationDescription = needRectification ? "轧辊表面油污，需清理后复检" : null,
        VerificationResult = needRectification ? "已清理，复检合格" : null,
        RectificationOperator = needRectification ? "王五" : null,
        IsClosed = needRectification
    };

    private static List<InspectionPatrolItem> BuildItems(int count)
        => Enumerable.Range(1, count)
            .Select(i => new InspectionPatrolItem
            {
                Id = i,
                ItemName = $"巡检项 {i}",
                Result = "符合",
                Remark = i % 2 == 0 ? "无" : "",
                SortOrder = i
            })
            .ToList();

    private static IReadOnlyList<InspectionPatrolPrintHelper.PrintImage> BuildImages(int count)
        => Enumerable.Range(1, count)
            .Select(i => new InspectionPatrolPrintHelper.PrintImage
            {
                FileName = $"patrol-{i}.png",
                Data = PrintTestAssets.CreatePng()
            })
            .ToList();
}
