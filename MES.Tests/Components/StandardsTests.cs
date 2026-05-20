using Bunit;
using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Pages;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class StandardsTests : TestBase
{
    public StandardsTests()
    {
        RegisterServices(typeof(ProductionStandardService));
        ConfigureEmptyResponse("/api/standard/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<Standards>();
        cut.Markup.Should().Contain("产品标准管理");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<Standards>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Theory]
    [InlineData(true, "启用")]
    [InlineData(false, "停用")]
    public void StatusColumn_DisplaysCorrectText(bool isActive, string expectedText)
    {
        ConfigureListResponse(isActive);
        var cut = Ctx.RenderComponent<Standards>();
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(bool isActive)
    {
        ConfigureEmptyResponse("/api/standard/list");
        var pagedResult = new PagedResult<ProductionStandardDto>
        {
            Items = new List<ProductionStandardDto>
            {
                new()
                {
                    Id = 1,
                    StandardCode = "GB/T 8163",
                    StandardName = "输送流体用无缝钢管",
                    IsActive = isActive
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/standard/list", new ApiResponse<PagedResult<ProductionStandardDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
