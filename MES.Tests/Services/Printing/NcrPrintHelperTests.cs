using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data.Entities.Quality;
using MES.Services.Printing;
using QuestPDF.Infrastructure;
using Xunit;

namespace MES.Tests.Services.Printing;

/// <summary>
/// 不合格报告（NCR）打印版式回归（2026-09-12 拍板）：字段页恒为单页，问题照片统一另起第 2 页；
/// 照片单列每页 2 张（290pt），按上限 <see cref="QualityPhotoLimits.PerRecord"/> 张占 2 页；无照片时不产生空白页。
/// </summary>
public class NcrPrintHelperTests
{
    static NcrPrintHelperTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void GeneratePdf_无照片_仅字段页一页()
    {
        var pdf = NcrPrintHelper.GeneratePdf(new List<Ncr> { BuildEntity() }, NoImages);

        PrintTestAssets.CountPdfPages(pdf).Should().Be(1);
    }

    [Fact]
    public void GeneratePdf_满张问题照片_字段页加两页照片_共三页()
    {
        var pdf = NcrPrintHelper.GeneratePdf(
            new List<Ncr> { BuildEntity() }, BuildImagesByNcr(QualityPhotoLimits.PerRecord));

        PrintTestAssets.CountPdfPages(pdf).Should().Be(3);
    }

    private static readonly IReadOnlyDictionary<int, IReadOnlyList<NcrPrintHelper.PrintImage>> NoImages =
        new Dictionary<int, IReadOnlyList<NcrPrintHelper.PrintImage>>();

    private static Ncr BuildEntity() => new()
    {
        Id = 8,
        Status = NcrStatus.Processing,
        ReportDate = new DateTime(2026, 9, 12),
        ReportDepartment = "质量部",
        Reporter = "李四",
        PipeCategory = MaterialType.DefectFinished,
        BatchNo = "M26-09-001-01",
        WorkOrderNo = "WO20260901-001-01",
        PlantGrade = "304",
        Specification = "60*6.2*8000",
        DefectiveQuantity = 3,
        DefectiveWeight = 640,
        ProblemDescription = "外表面划伤",
        FlowDirection = FlowDirection.Rework,
        DisposalMethod = NcrDisposalKeys.Rework,
        DisposalRemark = "返整后复检",
        DisposalIsCompleted = true,
        DisposalCompleteDate = new DateTime(2026, 9, 13),
        RootCauseAnalysis = "轧制润滑不足",
        Severity = SeverityLevel.General,
        AnalysisConfirmer = "王五",
        AnalysisConfirmDate = new DateTime(2026, 9, 13),
        ResponsibilityCategory = "Production",
        ResponsibleDept = "冷轧车间",
        ResponsiblePerson = "赵六",
        OperationDate = new DateTime(2026, 9, 11),
        PersonIsCompleted = true,
        PersonCompleteDate = new DateTime(2026, 9, 14),
        PersonDisposition = "绩效扣减",
        ActionPlanner = "王五",
        ActionPlanDate = new DateTime(2026, 9, 14),
        ActionVerifier = "李四",
        ActionVerifyDate = new DateTime(2026, 9, 20),
        VerifyResult = VerifyResult.Passed,
        ActionResult = "已复检合格",
        CorrectiveAction = "调整润滑配比并加严巡检频次"
    };

    private static IReadOnlyDictionary<int, IReadOnlyList<NcrPrintHelper.PrintImage>> BuildImagesByNcr(int count)
        => new Dictionary<int, IReadOnlyList<NcrPrintHelper.PrintImage>>
        {
            [8] = Enumerable.Range(1, count)
                .Select(i => new NcrPrintHelper.PrintImage
                {
                    FileName = $"ncr-photo-{i}.png",
                    Data = PrintTestAssets.CreatePng()
                })
                .ToList()
        };
}
