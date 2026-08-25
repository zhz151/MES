using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.StandardRegister;
using MES.Blazor.Services;
using MES.Core.DTOs.StandardRegister;

namespace MES.Tests.Components;

public class ChemicalCompositionsTests : TestBase
{
    public ChemicalCompositionsTests()
    {
        RegisterServices(typeof(ChemicalCompositionService));
        ConfigureEmptyResponse("/api/chemical-composition/all");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = RenderPage<ChemicalCompositions>();
        cut.Markup.Should().Contain("工厂牌号化学成分");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = RenderPage<ChemicalCompositions>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureResponse();
        var cut = RenderPage<ChemicalCompositions>();
        cut.WaitForState(() => cut.Markup.Contains("304"));
        cut.Markup.Should().Contain("304");
    }

    private void ConfigureResponse()
    {
        ConfigureEmptyResponse("/api/chemical-composition/all");
        var pagedResult = new PagedResult<ChemicalCompositionDto>
        {
            Items = new List<ChemicalCompositionDto>
            {
                new()
                {
                    Id = 1,
                    PlantGrade = "304",
                    Carbon = "0.08",
                    Silicon = "0.75",
                    Manganese = "2.00",
                    CreatedTime = DateTimeOffset.Now,
                    UpdatedTime = DateTimeOffset.Now
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/chemical-composition/all", new ApiResponse<PagedResult<ChemicalCompositionDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
