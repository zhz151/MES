using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.ProductionStandard;
using MES.Blazor.Services;
using MES.Core.DTOs.ProductionStandard;

namespace MES.Tests.Components;

public class GradeMappingsTests : TestBase
{
    public GradeMappingsTests()
    {
        RegisterServices(typeof(GradeMappingService));
        ConfigureEmptyResponse("/api/grade-mapping/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<GradeMappings>();
        cut.Markup.Should().Contain("牌号对照管理");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<GradeMappings>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Theory]
    [InlineData(true, "特殊")]
    [InlineData(false, "常规")]
    public void SpecialMaterialColumn_DisplaysCorrectText(bool specialMaterial, string expectedText)
    {
        ConfigureListResponse(specialMaterial);
        var cut = Ctx.RenderComponent<GradeMappings>();
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(bool specialMaterial)
    {
        ConfigureEmptyResponse("/api/grade-mapping/list");
        var pagedResult = new PagedResult<StandardGradeMappingDto>
        {
            Items = new List<StandardGradeMappingDto>
            {
                new()
                {
                    Id = 1,
                    StandardGrade = "304",
                    PlantGrade = "S30408",
                    Density = 7.93m,
                    SpecialMaterial = specialMaterial,
                    SteelProperty = "镍基合金"
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/grade-mapping/list", new ApiResponse<PagedResult<StandardGradeMappingDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
