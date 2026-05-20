using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Pages;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class WorkOrdersTests : TestBase
{
    public WorkOrdersTests()
    {
        RegisterServices(typeof(WorkOrderService), typeof(NotificationService));
        // OnInitializedAsync 调用通知接口
        ConfigureEmptyResponse("/api/notification/by-type/OrderChanged");
        ConfigureEmptyResponse("/api/notification/by-type/OrderDeleted");
        ConfigureEmptyResponse("/api/workorder/order-status");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<WorkOrders>();
        cut.Markup.Should().Contain("工单管理");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<WorkOrders>();
        cut.Markup.Should().Contain("工单状态");
    }

    [Theory]
    [InlineData(WorkOrderStatus.NotGenerated, "未编制")]
    [InlineData(WorkOrderStatus.Confirmed, "已确定")]
    [InlineData(WorkOrderStatus.Pending, "待修正")]
    [InlineData(WorkOrderStatus.Cancelled, "已取消")]
    public void StatusColumn_DisplaysCorrectText(WorkOrderStatus status, string expectedText)
    {
        ConfigureListResponse(status);
        var cut = Ctx.RenderComponent<WorkOrders>();
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(WorkOrderStatus status)
    {
        ConfigureEmptyResponse("/api/workorder/order-status");
        var pagedResult = new PagedResult<OrderWorkOrderStatusDto>
        {
            Items = new List<OrderWorkOrderStatusDto>
            {
                new()
                {
                    SalesOrderId = 1,
                    OrderNumber = "SO001",
                    WorkOrderStatus = status,
                    SignDate = DateTime.Today,
                    Salesman = "业务员A",
                    CustomerName = "测试客户"
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/workorder/order-status", new ApiResponse<PagedResult<OrderWorkOrderStatusDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
