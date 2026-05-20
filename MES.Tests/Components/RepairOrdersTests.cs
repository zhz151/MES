using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Pages;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class RepairOrdersTests : TestBase
{
    public RepairOrdersTests()
    {
        RegisterServices(typeof(RepairOrderService));
        ConfigureEmptyResponse("/api/repair-order/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<RepairOrders>();
        cut.Markup.Should().Contain("维修工单");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<RepairOrders>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Theory]
    [InlineData("Normal", "普通")]
    [InlineData("Urgent", "紧急")]
    [InlineData("Emergency", "特急")]
    public void PriorityColumn_DisplaysCorrectText(string priority, string expectedText)
    {
        ConfigureListResponse(priority, "Pending");
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p =>
            p.AddChildContent<RepairOrders>());
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    [Theory]
    [InlineData("Pending", "待维修")]
    [InlineData("InProgress", "维修中")]
    [InlineData("Completed", "完成")]
    public void StatusColumn_DisplaysCorrectText(string status, string expectedText)
    {
        ConfigureListResponse("Normal", status);
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p =>
            p.AddChildContent<RepairOrders>());
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(string priority, string repairStatus)
    {
        ConfigureEmptyResponse("/api/repair-order/list");
        var pagedResult = new PagedResult<RepairOrderListDto>
        {
            Items = new List<RepairOrderListDto>
            {
                new()
                {
                    Id = 1,
                    RepairOrderNo = "RO-001",
                    EquipmentName = "设备A",
                    FaultDescription = "测试故障",
                    Priority = priority,
                    RepairStatus = repairStatus,
                    ReportPerson = "张三",
                    ReportTime = DateTime.Now
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/repair-order/list", new ApiResponse<PagedResult<RepairOrderListDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
