using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Models;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using MES.Blazor.Pages;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class OrdersTests : TestBase
{
    public OrdersTests()
    {
        RegisterServices(typeof(OrderService), typeof(ProductRequirementService));
        ConfigureEmptyResponse("/api/order/list");
        ConfigureEmptyResponse("/api/order/filter-contexts");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Orders>());
        cut.Markup.Should().Contain("订单管理");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Orders>());
        cut.Markup.Should().Contain("关键字搜索");
    }

    [Theory]
    [InlineData(SalesOrderStatus.Pending, "待处理")]
    [InlineData(SalesOrderStatus.Confirmed, "已确认")]
    [InlineData(SalesOrderStatus.Cancelled, "已取消")]
    public void StatusColumn_DisplaysCorrectText(SalesOrderStatus status, string expectedText)
    {
        ConfigureListResponse(status);
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Orders>());
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(SalesOrderStatus status)
    {
        ConfigureEmptyResponse("/api/order/list");
        var pagedResult = new PagedResult<SalesOrderListDto>
        {
            Items = new List<SalesOrderListDto>
            {
                new()
                {
                    Id = 1,
                    OrderNumber = "SO001",
                    Status = status,
                    SignDate = DateTime.Today,
                    CustomerName = "测试客户",
                    Salesman = "业务员A"
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/order/list", new ApiResponse<PagedResult<SalesOrderListDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
