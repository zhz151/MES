using Bunit;
using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Pages.ProductionStandard;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class ChemicalValidationRulesTests : TestBase
{
    public ChemicalValidationRulesTests()
    {
        RegisterServices(typeof(ChemicalValidationRuleService));
        ConfigureEmptyResponse("/api/chemical-validation-rule/all");
        ConfigureEmptyResponse("/api/chemical-validation-rule/filter-contexts");
        ConfigureEmptyResponse("/api/chemical-validation-rule/filter-contexts");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<ChemicalValidationRules>();
        cut.Markup.Should().Contain("工厂牌号化分验证");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<ChemicalValidationRules>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = Ctx.RenderComponent<ChemicalValidationRules>();
        cut.WaitForState(() => cut.Markup.Contains("S30408"));
        cut.Markup.Should().Contain("S30408");
    }

    private void ConfigureListResponse()
    {
        ConfigureEmptyResponse("/api/chemical-validation-rule/all");
        ConfigureEmptyResponse("/api/chemical-validation-rule/filter-contexts");
        var pagedResult = new PagedResult<ChemicalValidationRuleDto>
        {
            Items = new List<ChemicalValidationRuleDto>
            {
                new()
                {
                    Id = 1,
                    PlantGrade = "S30408",
                    CMin = "0.00",
                    CMax = "0.08",
                    SiMin = "0.00",
                    SiMax = "0.75"
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/chemical-validation-rule/all", new ApiResponse<PagedResult<ChemicalValidationRuleDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
