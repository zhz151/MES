using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Pages.Quality;
using MES.Blazor.Services;
using MES.Core.DTOs.Quality;

namespace MES.Tests.Components.Quality;

public class NcrPrintTests : TestBase
{
    private const string NcrBase = "/api/ncr";

    public NcrPrintTests()
    {
        RegisterServices(typeof(NcrService));
    }

    [Fact]
    public void Render_DisplaysReport_WhenDataLoaded()
    {
        ConfigureNcrResponse(1);
        var cut = Ctx.RenderComponent<NcrPrint>(p => p.Add(c => c.Id, 1));
        cut.WaitForState(() => cut.Markup.Contains("不合格报告"));

        // 报告标题和编号
        cut.Markup.Should().Contain("不合格报告");
        cut.Markup.Should().Contain("NCR-0001");

        // G1 问题反馈
        cut.Markup.Should().Contain("G1 问题反馈");
        cut.Markup.Should().Contain("2024-06-15");
        cut.Markup.Should().Contain("质检部");
        cut.Markup.Should().Contain("张三");
        cut.Markup.Should().Contain("BATCH001");
        cut.Markup.Should().Contain("WO-001");
        cut.Markup.Should().Contain("304");
        cut.Markup.Should().Contain("219*8");
        cut.Markup.Should().Contain("表面划伤");

        // G2 不合格品处置
        cut.Markup.Should().Contain("G2 不合格品处置");
        cut.Markup.Should().Contain("返整");

        // G3 原因分析
        cut.Markup.Should().Contain("G3 原因分析");
        cut.Markup.Should().Contain("严重");
        cut.Markup.Should().Contain("设备故障");

        // G4 责任人及处理
        cut.Markup.Should().Contain("G4 责任人及处理");
        cut.Markup.Should().Contain("生产-厂内");
        cut.Markup.Should().Contain("轧制车间");
        cut.Markup.Should().Contain("李四");

        // G5 纠正预防措施
        cut.Markup.Should().Contain("G5 纠正预防措施");
        cut.Markup.Should().Contain("通过");
        cut.Markup.Should().Contain("更换模具");

        // 状态
        cut.Markup.Should().Contain("处理中");

        // 打印按钮应可见
        cut.Markup.Should().Contain("打印");
    }

    [Fact]
    public void Render_ShowsError_WhenLoadFails()
    {
        ConfigureResponse($"{NcrBase}/999", new ApiResponse<NcrDto>
        {
            Success = false,
            Code = 404,
            Message = "未找到"
        });
        var cut = Ctx.RenderComponent<NcrPrint>(p => p.Add(c => c.Id, 999));
        cut.WaitForState(() => cut.Markup.Contains("加载失败"));
        cut.Markup.Should().Contain("加载失败");
        cut.Markup.Should().Contain("返回列表");
    }

    [Fact]
    public void Render_ShowsClosedStatus_WhenNcrClosed()
    {
        var dto = CreateFullNcrDto(2);
        dto.Status = NcrStatus.Closed;
        ConfigureResponse($"{NcrBase}/2", new ApiResponse<NcrDto> { Success = true, Code = 200, Data = dto });
        var cut = Ctx.RenderComponent<NcrPrint>(p => p.Add(c => c.Id, 2));
        cut.WaitForState(() => cut.Markup.Contains("不合格报告"));
        cut.Markup.Should().Contain("已关闭");
    }

    [Fact]
    public void Render_AllSectionsHaveCorrectColors()
    {
        ConfigureNcrResponse(3);
        var cut = Ctx.RenderComponent<NcrPrint>(p => p.Add(c => c.Id, 3));
        cut.WaitForState(() => cut.Markup.Contains("G1 问题反馈"));
        cut.Markup.Should().Contain("ncr-section-g1");
        cut.Markup.Should().Contain("ncr-section-g2");
        cut.Markup.Should().Contain("ncr-section-g3");
        cut.Markup.Should().Contain("ncr-section-g4");
        cut.Markup.Should().Contain("ncr-section-g5");
    }

    [Fact]
    public void Render_ShowsEmptyForNullOptionalFields()
    {
        var dto = CreateFullNcrDto(4);
        dto.ReportDepartment = null;
        dto.Reporter = null;
        dto.WorkOrderNo = null;
        dto.DisposalRemark = null;
        dto.RootCauseAnalysis = null;
        dto.PersonDisposition = null;
        dto.CorrectiveAction = null;
        ConfigureResponse($"{NcrBase}/4", new ApiResponse<NcrDto> { Success = true, Code = 200, Data = dto });
        var cut = Ctx.RenderComponent<NcrPrint>(p => p.Add(c => c.Id, 4));
        cut.WaitForState(() => cut.Markup.Contains("不合格报告"));
        // 页面不显示 "null" 文本
        cut.Markup.Should().NotContain("null");
    }

    [Fact]
    public void PrintButton_ClickTriggersPrint()
    {
        ConfigureNcrResponse(5);
        var cut = Ctx.RenderComponent<NcrPrint>(p => p.Add(c => c.Id, 5));
        cut.WaitForState(() => cut.Markup.Contains("打印"));
        var printBtn = cut.FindAll("button").First(b => b.TextContent.Contains("打印"));
        printBtn.Click();
        // 不应抛出异常（SilentJsRuntime 静默处理 InvokeVoidAsync）
    }

    [Fact]
    public void BackButton_NavigatesToList()
    {
        ConfigureNcrResponse(6);
        var cut = Ctx.RenderComponent<NcrPrint>(p => p.Add(c => c.Id, 6));
        cut.WaitForState(() => cut.Markup.Contains("返回列表"));
        var backBtn = cut.FindAll("button").First(b => b.TextContent.Contains("返回列表"));
        backBtn.Click();
        var nav = (FakeNavigationManager)Ctx.Services.GetService(typeof(NavigationManager))!;
        nav.Uri.Should().Contain("/quality/ncr");
    }

    // ========== 辅助方法 ==========

    private void ConfigureNcrResponse(int id)
    {
        ConfigureResponse($"{NcrBase}/{id}", new ApiResponse<NcrDto>
        {
            Success = true,
            Code = 200,
            Data = CreateFullNcrDto(id)
        });
    }

    private static NcrDto CreateFullNcrDto(int id) => new()
    {
        Id = id,
        Status = NcrStatus.Processing,
        ReportDate = new DateTime(2024, 6, 15),
        ReportDepartment = "质检部",
        Reporter = "张三",
        PipeCategory = MaterialType.OrderFinished,
        BatchNo = "BATCH001",
        WorkOrderNo = "WO-001",
        PlantGrade = "304",
        Specification = "219*8",
        DefectiveQuantity = 5,
        ProblemDescription = "表面划伤",
        DisposalMethod = DisposalMethod.Rework,
        DisposalRemark = "已安排返整",
        DisposalIsCompleted = true,
        DisposalCompleteDate = new DateTime(2024, 6, 20),
        Severity = SeverityLevel.Critical,
        RootCauseAnalysis = "设备故障",
        AnalysisConfirmer = "王五",
        AnalysisConfirmDate = new DateTime(2024, 6, 18),
        ResponsibilityCategory = ResponsibilityCategory.ProductionInternal,
        ResponsibleDept = "轧制车间",
        OperationDate = new DateTime(2024, 6, 15),
        ResponsiblePerson = "李四",
        PersonDisposition = "警告处理",
        PersonIsCompleted = true,
        PersonCompleteDate = new DateTime(2024, 6, 22),
        CorrectiveAction = "更换模具",
        ActionPlanner = "赵六",
        ActionPlanDate = new DateTime(2024, 6, 19),
        ActionVerifier = "孙七",
        ActionVerifyDate = new DateTime(2024, 6, 25),
        VerifyResult = VerifyResult.Passed,
        ActionResult = "已完成整改",
        CreatedTime = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.FromHours(8)),
        UpdatedTime = new DateTimeOffset(2024, 6, 25, 14, 0, 0, TimeSpan.FromHours(8))
    };
}
