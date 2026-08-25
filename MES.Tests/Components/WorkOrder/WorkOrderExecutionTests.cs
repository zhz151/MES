using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.WorkOrders;
using MES.Blazor.Services;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;

namespace MES.Tests.Components;

public class WorkOrderExecutionTests : TestBase
{
    public WorkOrderExecutionTests()
    {
        RegisterServices(typeof(WorkOrderExecutionService));
        ConfigureEmptyResponse("/api/workorder-execution/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = RenderPage<WorkOrderExecution>();
        cut.Markup.Should().Contain("工单执行状况");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = RenderPage<WorkOrderExecution>();
        cut.Markup.Should().Contain("工单号/订单号/业务员/客户/牌号/规格/主号");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = RenderPage<WorkOrderExecution>();
        cut.WaitForState(() => cut.Markup.Contains("WO001"));
        cut.Markup.Should().Contain("WO001");
    }

    private void ConfigureListResponse()
    {
        ConfigureEmptyResponse("/api/workorder-execution/list");
        var pagedResult = new PagedResult<WorkOrderExecutionSummaryDto>
        {
            Items = new List<WorkOrderExecutionSummaryDto>
            {
                new()
                {
                    Id = 1,
                    WorkOrderId = 1,
                    WorkOrderNo = "WO001",
                    Salesman = "业务员A",
                    CustomerName = "测试客户",
                    SalesOrderNo = "SO001",
                    ProductionMainNo = "D01",
                    PlantGrade = "304",
                    Specification = "219*8",
                    MaterialName = "无缝管",
                    DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
                    SettlementMethod = SettlementMethod.Theoretical,
                    LengthStatus = LengthStatus.Fixed,
                    TotalQuantity = 100,
                    TotalMeters = 600m,
                    TotalWeight = 2500m,
                    SignDate = DateTime.Today,
                    DeliveryDate = DateTime.Today.AddMonths(1)
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/workorder-execution/list", new ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }

}
