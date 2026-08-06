using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Materials;
using MES.Blazor.Services;
using MES.Core.DTOs.Materials;

namespace MES.Tests.Components;

public class SuppliersTests : TestBase
{
    public SuppliersTests()
    {
        RegisterServices(typeof(SupplierService));
        ConfigureEmptyResponse("/api/supplier/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<Suppliers>();
        cut.Markup.Should().Contain("供应商管理");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<Suppliers>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Theory]
    [InlineData(true, "启用")]
    [InlineData(false, "停用")]
    public void StatusColumn_DisplaysCorrectText(bool isActive, string expectedText)
    {
        ConfigureListResponse(isActive);
        var cut = Ctx.RenderComponent<Suppliers>();
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(bool isActive)
    {
        ConfigureEmptyResponse("/api/supplier/list");
        var pagedResult = new PagedResult<SupplierProfileDto>
        {
            Items = new List<SupplierProfileDto>
            {
                new()
                {
                    Id = 1,
                    SupplierCode = "S001",
                    SupplierName = "测试供应商",
                    MaterialCategory = MES.Core.Enums.MaterialType.RoughTube,
                    ContactPerson = "联系人A",
                    ContactPhone = "13800138000",
                    IsActive = isActive
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/supplier/list", new ApiResponse<PagedResult<SupplierProfileDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
