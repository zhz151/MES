using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Quality;
using MES.Blazor.Services;
using MES.Core.DTOs.Quality;

namespace MES.Tests.Components;

public class ProcessInspectionsTests : TestBase
{
    public ProcessInspectionsTests()
    {
        RegisterServices(typeof(ProcessInspectionService));
        ConfigureEmptyResponse("/api/process-inspection/all");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = RenderPage<ProcessInspections>();
        cut.Markup.Should().Contain("过程检验");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = RenderPage<ProcessInspections>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = RenderPage<ProcessInspections>();
        cut.WaitForState(() => cut.Markup.Contains("BATCH-PI-001"));
        cut.Markup.Should().Contain("BATCH-PI-001");
    }

    private void ConfigureListResponse()
    {
        ConfigureEmptyResponse("/api/process-inspection/all");
        var pagedResult = new PagedResult<ProcessInspectionDto>
        {
            Items = new List<ProcessInspectionDto>
            {
                new()
                {
                    Id = 1,
                    ProductionBatchId = 1,
                    ProcessGroupId = 1,
                    BatchNo = "BATCH-PI-001",
                    ProcessName = "60冷轧",
                    SectionName = "冷轧车间",
                    InspectionDate = DateTime.Today,
                    Inspector = "张三",
                    Quantity = 100,
                    PlantGrade = "304"
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/process-inspection/all", new ApiResponse<PagedResult<ProcessInspectionDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
