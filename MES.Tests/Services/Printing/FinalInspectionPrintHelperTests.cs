using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data.Entities.Quality;
using MES.Services.Printing;
using QuestPDF.Infrastructure;
using Xunit;

namespace MES.Tests.Services.Printing;

/// <summary>
/// 成品检验记录打印版式回归（2026-09-12 拍板，紧凑排版档）：
/// 取字段最多的「涡流/超声波探伤」为最坏用例，验证满字段「字段页」恒为单页、
/// 检验照片统一另起第 2 页（单列每页 2 张 / 290pt，按上限 <see cref="QualityPhotoLimits.PerRecord"/> 张占 2 页）
/// 且无照片时不产生空白页。
/// </summary>
public class FinalInspectionPrintHelperTests
{
    static FinalInspectionPrintHelperTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void GeneratePagePdf_探伤满字段无照片_单页()
    {
        var pdf = FinalInspectionPrintHelper.GeneratePagePdf(
            new List<FinalInspection> { BuildEntity() }, NoImages);

        PrintTestAssets.CountPdfPages(pdf).Should().Be(1);
    }

    [Fact]
    public void GeneratePagePdf_探伤满字段满张照片_字段页加两页照片_共三页()
    {
        var pdf = FinalInspectionPrintHelper.GeneratePagePdf(
            new List<FinalInspection> { BuildEntity() }, BuildImagesByRecord(QualityPhotoLimits.PerRecord, 21));

        PrintTestAssets.CountPdfPages(pdf).Should().Be(3);
    }

    private static readonly IReadOnlyDictionary<int, IReadOnlyList<FinalInspectionPrintHelper.PrintImage>> NoImages =
        new Dictionary<int, IReadOnlyList<FinalInspectionPrintHelper.PrintImage>>();

    private static FinalInspection BuildEntity() => new()
    {
        Id = 21,
        InspectionItem = InspectionItem.Ultrasonic,
        InspectionDate = new DateTime(2026, 9, 12),
        BatchNo = "M26-09-001-01",
        InspectionType = "FormalInspection",
        EquipmentName = "超声探伤机-1",
        Shift = ShiftType.DayShift,
        Operator = "张三",
        FixedLength = "6000",
        CutLengthMatchType = "FullMatch",
        NonFixedLengthRange = "5800-6200",
        Quantity = 20,
        Weight = 4250,
        QualifiedQuantity = 18,
        QualifiedWeight = 3825,
        QualifiedConcessionQuantity = 0,
        ConcessionRemark = "",
        DefectReworkQuantity = 1,
        DefectInProcessWarehouseQuantity = 0,
        DefectWarehouseQuantity = 1,
        DefectScrapQuantity = 0,
        DefectReturnQuantity = 0,
        DefectDescription = "内壁缺陷回波超判废线",
        DefectReworkWeight = 210,
        DefectInProcessWarehouseWeight = 0,
        DefectWarehouseWeight = 210,
        DefectScrapWeight = 0,
        DefectReturnWeight = 0,
        QualificationLevel = "II 级",
        InspectionStandard = "GB/T 5777",
        InspectionGrade = "U2",
        InstrumentModel = "CTS-1002",
        NdtMethod = "接触法",
        StandardSampleSize = "Φ60×6.2",
        StandardSampleDefect = "纵向槽 N5",
        ProbeType = "双晶直探头",
        Couplant = "机油",
        CalibrationFrequency = "每班一次",
        DetectionFrequency = "5MHz",
        DetectionSensitivity = "Φ1.2 当量",
        DetectionPhase = "0°",
        DetectionSpeed = "1.5m/min",
        Remark = "复检合格后放行",
        DataSource = "SCAN"
    };

    private static Dictionary<int, IReadOnlyList<FinalInspectionPrintHelper.PrintImage>> BuildImagesByRecord(
        int countPerRecord, params int[] recordIds)
        => recordIds.ToDictionary(
            id => id,
            id => (IReadOnlyList<FinalInspectionPrintHelper.PrintImage>)Enumerable.Range(1, countPerRecord)
                .Select(i => new FinalInspectionPrintHelper.PrintImage
                {
                    FileName = $"final-inspection-{i}.png",
                    Data = PrintTestAssets.CreatePng()
                })
                .ToList());
}
