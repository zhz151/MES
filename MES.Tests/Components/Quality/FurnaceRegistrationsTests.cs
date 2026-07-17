using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Quality;
using MES.Blazor.Services;
using MES.Core.DTOs.Quality;
using MES.Core.Enums;

namespace MES.Tests.Components;

public class FurnaceRegistrationsTests : TestBase
{
    public FurnaceRegistrationsTests()
    {
        RegisterServices(typeof(FurnaceRegistrationService));
        ConfigureEmptyResponse("/api/furnace-registration/all");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<FurnaceRegistrations>();
        cut.Markup.Should().Contain("来料炉号登记");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<FurnaceRegistrations>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = Ctx.RenderComponent<FurnaceRegistrations>();
        cut.WaitForState(() => cut.Markup.Contains("FUR-001"));
        cut.Markup.Should().Contain("FUR-001");
    }

    private void ConfigureListResponse()
    {
        ConfigureEmptyResponse("/api/furnace-registration/all");
        var pagedResult = new PagedResult<FurnaceRegistrationDto>
        {
            Items = new List<FurnaceRegistrationDto>
            {
                new()
                {
                    Id = 1,
                    IncomingDate = DateTime.Today,
                    RawMaterialUnit = "宝钢",
                    RawMaterialType = MaterialType.RoughTube,
                    RegisteredGrade = "304",
                    RelatedPlantGrade = "S30408",
                    FurnaceNumber = "FUR-001",
                    Specification = "219*8",
                    Quantity = 50,
                    Weight = 2500m
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/furnace-registration/all", new ApiResponse<PagedResult<FurnaceRegistrationDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
