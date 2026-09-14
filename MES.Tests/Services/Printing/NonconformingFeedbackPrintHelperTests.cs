using FluentAssertions;
using MES.Core.Constants;
using MES.Data.Entities.Quality;
using MES.Services.Printing;
using QuestPDF.Infrastructure;
using Xunit;

namespace MES.Tests.Services.Printing;

/// <summary>
/// 不合格反馈单打印版式回归（2026-09-12 拍板）：字段页恒为单页，问题照片统一另起第 2 页；
/// 照片单列每页 2 张（290pt），按上限 <see cref="QualityPhotoLimits.PerRecord"/> 张占 2 页；无照片时不产生空白页。
/// </summary>
public class NonconformingFeedbackPrintHelperTests
{
    static NonconformingFeedbackPrintHelperTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void GeneratePdf_无照片_仅字段页一页()
    {
        var pdf = NonconformingFeedbackPrintHelper.GeneratePdf(BuildEntity(), NoImages);

        PrintTestAssets.CountPdfPages(pdf).Should().Be(1);
    }

    [Fact]
    public void GeneratePdf_长问题描述_无照片_不溢出为两页()
    {
        var entity = BuildEntity();
        entity.ProblemDescription = string.Concat(Enumerable.Repeat("外表面连续纵向划伤并伴随局部点蚀，需返整处理；", 15));

        var pdf = NonconformingFeedbackPrintHelper.GeneratePdf(entity, NoImages);

        PrintTestAssets.CountPdfPages(pdf).Should().Be(1);
    }

    [Fact]
    public void GeneratePdf_满张问题照片_字段页加两页照片_共三页()
    {
        var pdf = NonconformingFeedbackPrintHelper.GeneratePdf(
            BuildEntity(), BuildImages(QualityPhotoLimits.PerRecord));

        PrintTestAssets.CountPdfPages(pdf).Should().Be(3);
    }

    private static readonly IReadOnlyList<NonconformingFeedbackPrintHelper.PrintImage> NoImages =
        Array.Empty<NonconformingFeedbackPrintHelper.PrintImage>();

    private static NonconformingFeedback BuildEntity() => new()
    {
        Id = 12,
        ReportDate = new DateTime(2026, 9, 12),
        Reporter = "张三",
        DataSource = "MANUAL",
        BatchNo = "M26-09-001-01",
        WorkOrderNo = "WO20260901-001-01",
        ProcessName = "ColdRollDraw",
        SectionName = "ColdRoll",
        PlantGrade = "304",
        ManufacturingSpec = "60*6.2*8000",
        ProductStatus = "WorkInProgress",
        IncomingQuantity = 20,
        IncomingWeight = 4250.5m,
        DefectQuantity = 3,
        DefectWeight = 640,
        ProblemDescription = "外表面划伤"
    };

    private static IReadOnlyList<NonconformingFeedbackPrintHelper.PrintImage> BuildImages(int count)
        => Enumerable.Range(1, count)
            .Select(i => new NonconformingFeedbackPrintHelper.PrintImage
            {
                FileName = $"photo-{i}.png",
                Data = PrintTestAssets.CreatePng()
            })
            .ToList();
}
