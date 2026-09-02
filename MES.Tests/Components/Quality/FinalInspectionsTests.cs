using Bunit;
using FluentAssertions;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Pages.Quality;
using MES.Blazor.Services;
using MES.Core.DTOs.Quality;

namespace MES.Tests.Components;

public class FinalInspectionsTests : TestBase
{
    public FinalInspectionsTests()
    {
        RegisterServices(typeof(FinalInspectionService), typeof(EmployeeService));
        ConfigureEmptyResponse("/api/final-inspection/all");
        ConfigureEmptyResponse("/api/employee/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = RenderPage<FinalInspections>();
        cut.Markup.Should().Contain("成品检验");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = RenderPage<FinalInspections>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = RenderPage<FinalInspections>();
        cut.WaitForState(() => cut.Markup.Contains("BATCH-FI-001"));
        cut.Markup.Should().Contain("BATCH-FI-001");
    }

    private void ConfigureListResponse()
    {
        ConfigureEmptyResponse("/api/final-inspection/all");
        var pagedResult = new PagedResult<FinalInspectionDto>
        {
            Items = new List<FinalInspectionDto>
            {
                new()
                {
                    Id = 1,
                    BatchNo = "BATCH-FI-001",
                    InspectionItem = InspectionItem.PMIInspection,
                    InspectionDate = DateTime.Today,
                    PlantGrade = "304",
                    Specification = "219*8",
                    Quantity = 100
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/final-inspection/all", new ApiResponse<PagedResult<FinalInspectionDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
