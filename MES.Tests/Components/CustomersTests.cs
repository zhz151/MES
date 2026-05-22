using Bunit;
using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Pages;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class CustomersTests : TestBase
{
    public CustomersTests()
    {
        RegisterServices(typeof(CustomerService));
        ConfigureEmptyResponse("/api/customer/list");
        ConfigureEmptyResponse("/api/customer/filter-contexts");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<Customers>();
        cut.Markup.Should().Contain("客户管理");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<Customers>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Theory]
    [InlineData(CustomerStatus.Active, "启用")]
    [InlineData(CustomerStatus.Inactive, "停用")]
    public void StatusColumn_DisplaysCorrectText(CustomerStatus status, string expectedText)
    {
        ConfigureListResponse(status);
        var cut = Ctx.RenderComponent<Customers>();
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(CustomerStatus status)
    {
        ConfigureEmptyResponse("/api/customer/list");
        ConfigureEmptyResponse("/api/customer/filter-contexts");
        var pagedResult = new PagedResult<CustomerProfileDto>
        {
            Items = new List<CustomerProfileDto>
            {
                new()
                {
                    Id = 1,
                    CustomerCode = "C001",
                    CustomerUnit = "测试客户",
                    Salesman = "业务员A",
                    Status = status
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/customer/list", new ApiResponse<PagedResult<CustomerProfileDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
