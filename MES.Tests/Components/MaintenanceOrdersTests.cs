using Bunit;
using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Pages;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class MaintenanceOrdersTests : TestBase
{
    public MaintenanceOrdersTests()
    {
        RegisterServices(typeof(MaintenanceOrderService));
        ConfigureEmptyResponse("/api/maintenance-order/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<MaintenanceOrders>();
        cut.Markup.Should().Contain("保养工单");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<MaintenanceOrders>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_HasColumns()
    {
        var cut = Ctx.RenderComponent<MaintenanceOrders>();
        cut.Markup.Should().ContainAll("保养单号", "设备名称", "执行人");
    }
}
