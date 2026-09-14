using FluentAssertions;
using MES.Core.Constants;
using MES.Data.Entities.Quality;
using MES.Services.Printing;
using QuestPDF.Infrastructure;
using Xunit;

namespace MES.Tests.Services.Printing;

/// <summary>
/// 过程检验记录打印版式回归（2026-09-12 拍板，紧凑排版档）：
/// 满字段「字段页」恒为单页，检验照片统一另起第 2 页；照片单列铺排、每页 2 张（290pt），
/// 按上限 <see cref="QualityPhotoLimits.PerRecord"/> 张占 2 页；多条记录时逐条「字段页 + 照片页」，不产生空白页。
/// </summary>
public class ProcessInspectionPrintHelperTests
{
    static ProcessInspectionPrintHelperTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void GeneratePagePdf_满字段无照片_单页()
    {
        var pdf = ProcessInspectionPrintHelper.GeneratePagePdf(new List<ProcessInspection> { BuildEntity() }, NoImages);

        PrintTestAssets.CountPdfPages(pdf).Should().Be(1);
    }

    [Fact]
    public void GeneratePagePdf_满字段满张照片_字段页加两页照片_共三页()
    {
        var pdf = ProcessInspectionPrintHelper.GeneratePagePdf(
            new List<ProcessInspection> { BuildEntity() }, BuildImagesByRecord(QualityPhotoLimits.PerRecord, 10));

        PrintTestAssets.CountPdfPages(pdf).Should().Be(3);
    }

    [Fact]
    public void GeneratePagePdf_两条记录各满张照片_共六页_无空白页()
    {
        var first = BuildEntity();
        first.Id = 10;
        var second = BuildEntity();
        second.Id = 11;

        var pdf = ProcessInspectionPrintHelper.GeneratePagePdf(
            new List<ProcessInspection> { first, second },
            BuildImagesByRecord(QualityPhotoLimits.PerRecord, 10, 11));

        PrintTestAssets.CountPdfPages(pdf).Should().Be(6);
    }

    private static readonly IReadOnlyDictionary<int, IReadOnlyList<ProcessInspectionPrintHelper.PrintImage>> NoImages =
        new Dictionary<int, IReadOnlyList<ProcessInspectionPrintHelper.PrintImage>>();

    private static ProcessInspection BuildEntity() => new()
    {
        Id = 10,
        BatchNo = "M26-09-001-01",
        PlantGrade = "304",
        ProcessName = "ColdRollDraw",
        ManufacturingSpec = "60*6.2*8000",
        SectionName = "ColdRoll",
        SequenceNumber = 3,
        ProductStatus = "WorkInProgress",
        TagNo = "TP-0001",
        InspectionDate = new DateTime(2026, 9, 12),
        Shift = "DayShift",
        Inspector = "张三",
        EquipmentName = "冷轧机-1",
        InspectionItem = "Dimension",
        SourceUnit = "冷轧车间",
        DataSource = "SCAN",
        Quantity = 20,
        Weight = 4250.5m,
        QualifiedQuantity = 16,
        QualifiedWeight = 3400.4m,
        QualifiedConcessionQuantity = 1,
        ConcessionRemark = "轻微壁厚偏差，客户让步接收",
        DefectReworkQuantity = 1,
        TheoreticalReworkWeight = 213,
        DefectWarehouseQuantity = 1,
        TheoreticalWarehouseWeight = 213,
        DefectScrapQuantity = 1,
        TheoreticalScrapWeight = 213,
        DefectReturnQuantity = 0,
        TheoreticalReturnWeight = 0,
        DefectDescription = "外表面划伤",
        Remark = "已通知当班调整"
    };

    private static Dictionary<int, IReadOnlyList<ProcessInspectionPrintHelper.PrintImage>> BuildImagesByRecord(
        int countPerRecord, params int[] recordIds)
        => recordIds.ToDictionary(
            id => id,
            id => (IReadOnlyList<ProcessInspectionPrintHelper.PrintImage>)Enumerable.Range(1, countPerRecord)
                .Select(i => new ProcessInspectionPrintHelper.PrintImage
                {
                    FileName = $"inspection-{i}.png",
                    Data = PrintTestAssets.CreatePng()
                })
                .ToList());
}
