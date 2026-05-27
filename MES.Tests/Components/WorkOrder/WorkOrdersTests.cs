using Bunit;
using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Pages.WorkOrders;
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
        // OnInitializedAsync 调用 LoadFilterContextsAsync
        ConfigureResponse("/api/workorder/filter-contexts", new ApiResponse<Dictionary<string, List<string>>>
        {
            Success = true,
            Code = 200,
            Data = new Dictionary<string, List<string>>()
        });
        // OnInitializedAsync 调用 LoadCancelledOrders
        ConfigureResponse("/api/workorder/cancelled-orders", new ApiResponse<List<CancelledOrderDto>>
        {
            Success = true,
            Code = 200,
            Data = new List<CancelledOrderDto>()
        });
    }

    [Fact]
    public void Render_HasTitle()
    {
        ConfigureEmptyListResponse();
        var cut = Ctx.RenderComponent<WorkOrders>();
        cut.WaitForState(() => cut.Markup.Contains("工单管理"), timeout: TimeSpan.FromSeconds(2));
        cut.Markup.Should().Contain("工单管理");
    }

    [Fact]
    public void Render_HasFilter()
    {
        ConfigureEmptyListResponse();
        var cut = Ctx.RenderComponent<WorkOrders>();
        cut.WaitForState(() => cut.Markup.Contains("模糊搜索"), timeout: TimeSpan.FromSeconds(2));
        cut.Markup.Should().Contain("模糊搜索");
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
        cut.WaitForState(() => cut.Markup.Contains(expectedText), timeout: TimeSpan.FromSeconds(2));
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(WorkOrderStatus status)
    {
        var pagedResult = new PagedResult<WorkOrderListDto>
        {
            Items = new List<WorkOrderListDto>
            {
                new()
                {
                    Id = 1,
                    WorkOrderNo = "WO001",
                    SalesOrderNo = "SO001",
                    ProductionMainNo = "PM001",
                    SignDate = DateTime.Today,
                    Salesman = "业务员A",
                    EndCustomer = "测试客户",
                    DeliveryDate = DateTime.Today.AddDays(30),
                    Specification = "规格",
                    TotalQuantity = 100,
                    TotalWeight = 1000m,
                    TotalItemCount = 1,
                    Status = (int)status,
                    MaterialPlanStatus = 0,
                    MaterialPlanRate = 0,
                    MainNoMaterialPlanStatus = 0,
                    MainNoMaterialPlanRate = 0,
                    OrderMaterialPlanStatus = 0,
                    CreatedTime = DateTimeOffset.Now,
                    PlantGrade = "20#",
                    MaterialName = MaterialName.SeamlessPipe,
                    SettlementMethod = SettlementMethod.Theoretical,
                    LengthStatus = LengthStatus.Fixed,
                    DeliveryState = DeliveryState.Bright,
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/workorder/list", new ApiResponse<PagedResult<WorkOrderListDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }

    private void ConfigureEmptyListResponse()
    {
        var pagedResult = new PagedResult<WorkOrderListDto>
        {
            Items = new List<WorkOrderListDto>(),
            TotalCount = 0,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/workorder/list", new ApiResponse<PagedResult<WorkOrderListDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
