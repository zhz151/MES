using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Materials;
using MES.Blazor.Services;
using MES.Core.DTOs.Materials;
using MES.Core.Enums;

namespace MES.Tests.Components;

public class MaterialsTests : TestBase
{
    public MaterialsTests()
    {
        RegisterServices(typeof(MaterialService));
        ConfigureEmptyResponse("/api/material/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<Materials>();
        cut.Markup.Should().Contain("物料档案");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<Materials>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Theory]
    [InlineData(true, "启用")]
    [InlineData(false, "停用")]
    public void StatusColumn_DisplaysCorrectText(bool isActive, string expectedText)
    {
        ConfigureListResponse(isActive);
        var cut = Ctx.RenderComponent<Materials>();
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(bool isActive)
    {
        ConfigureEmptyResponse("/api/material/list");
        var pagedResult = new PagedResult<MaterialDto>
        {
            Items = new List<MaterialDto>
            {
                new()
                {
                    Id = 1,
                    MaterialCode = "M001",
                    MaterialCategory = MaterialCategory.RoughTube,
                    PlantGrade = "304",
                    Specification = "219*8",
                    IsActive = isActive
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/material/list", new ApiResponse<PagedResult<MaterialDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
