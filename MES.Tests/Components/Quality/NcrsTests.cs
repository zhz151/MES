using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Pages.Quality;
using MES.Blazor.Services;
using MES.Core.DTOs.Quality;

namespace MES.Tests.Components.Quality;

public class NcrsTests : TestBase
{
    private const string NcrBase = "/api/ncr";

    public NcrsTests()
    {
        RegisterServices(typeof(NcrService));
        ConfigureEmptyResponse($"{NcrBase}/filter-contexts");
        ConfigureEmptyResponse($"{NcrBase}/pending-checks");
    }

    [Fact]
    public void Render_HasTitle()
    {
        ConfigureEmptyResponse($"{NcrBase}/all");
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Ncrs>());
        cut.Markup.Should().Contain("不合格报告");
    }

    [Fact]
    public void Render_HasStandardToolbar()
    {
        ConfigureEmptyResponse($"{NcrBase}/all");
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Ncrs>());
        cut.Markup.Should().Contain("重置");
        cut.Markup.Should().Contain("条记录");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Ncrs>());
        cut.WaitForState(() => cut.Markup.Contains("BATCH001"));
        cut.Markup.Should().Contain("BATCH001");
        cut.Markup.Should().Contain("WO-001");
    }

    [Fact]
    public void Render_OperationsColumn_HasPrintButton()
    {
        ConfigureListResponse();
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Ncrs>());
        cut.WaitForState(() => cut.Markup.Contains("BATCH001"));
        // 验证 Print 图标路径在渲染结果中存在（MudIconButton 渲染 SVG path）
        cut.Markup.Should().Contain("M19 8H5c-1.66");
        // 同时验证 Edit 和 Delete 图标
        cut.Markup.Should().Contain("M3 17.25V21h3.75L17.81");
        cut.Markup.Should().Contain("M6 19c0 1.1.9 2 2 2h8c1.1");
        // 操作列（最后 td）内应有 2 个 icon 按钮（编辑+删除，打印已移除）
        var lastTdButtons = cut.FindAll(".mud-table-container tbody tr:first-child td:last-child button.mud-icon-button");
        lastTdButtons.Count.Should().Be(2);
    }

    [Fact]
    public void Render_ShowPendingSection()
    {
        ConfigureEmptyResponse($"{NcrBase}/all");
        ConfigureResponse($"{NcrBase}/pending-checks", new ApiResponse<List<NcrPendingCheckDto>>
        {
            Success = true,
            Code = 200,
            Data = new List<NcrPendingCheckDto>
            {
                new()
                {
                    BatchNo = "PENDING-001",
                    WorkOrderNo = "WO-PENDING",
                    SourceType = "ProcessInspection",
                    DefectQuantity = 5,
                    TotalQuantity = 100,
                    Percentage = 5m,
                    DisposalMethod = DisposalMethod.Rework
                }
            }
        });
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Ncrs>());
        cut.WaitForState(() => cut.Markup.Contains("待处理批次"));
        cut.Markup.Should().Contain("待处理批次");
    }

    [Fact]
    public void Render_DisplaysStatusChips()
    {
        ConfigureListResponse();
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Ncrs>());
        cut.WaitForState(() => cut.Markup.Contains("BATCH001"));
        // 状态芯片应显示
        cut.Markup.Should().Contain("处理中");
    }

    // ========== 辅助方法 ==========

    private void ConfigureListResponse()
    {
        ConfigureEmptyResponse($"{NcrBase}/all");
        var pagedResult = new PagedResult<NcrDto>
        {
            Items = new List<NcrDto>
            {
                new()
                {
                    Id = 1,
                    Status = NcrStatus.Processing,
                    ReportDate = DateTime.Today,
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
                    DisposalIsCompleted = false,
                    Severity = SeverityLevel.General,
                    CreatedTime = DateTimeOffset.Now,
                    UpdatedTime = DateTimeOffset.Now
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse($"{NcrBase}/all", new ApiResponse<PagedResult<NcrDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
