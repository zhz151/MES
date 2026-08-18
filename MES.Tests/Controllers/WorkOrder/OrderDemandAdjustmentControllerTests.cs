using Microsoft.AspNetCore.Mvc;
using Moq;
using MES.Api.Controllers.WorkOrder;
using MES.Core.Models;
using MES.Tests.Tests;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.WorkOrder;

namespace MES.Tests.Controllers.WorkOrder;

public class OrderDemandAdjustmentControllerTests : ControllerTestBase
{
    private readonly Mock<IOrderDemandAdjustmentService> _serviceMock;
    private readonly OrderDemandAdjustmentController _controller;

    public OrderDemandAdjustmentControllerTests()
    {
        _serviceMock = new Mock<IOrderDemandAdjustmentService>();
        _controller = new OrderDemandAdjustmentController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        var pagedResult = new PagedResult<OrderDemandAdjustmentDto>
        {
            Items = new List<OrderDemandAdjustmentDto> { new() { Id = 1, WorkOrderNo = "WO001" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(pagedResult);

        var result = await _controller.GetPaged();

        var (_, response) = AssertOk<ApiResponse<PagedResult<OrderDemandAdjustmentDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task SaveUrging_ReturnsOk()
    {
        _serviceMock.Setup(x => x.SaveUrgingAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync(true);

        var request = new SaveUrgingRequest
        {
            WorkOrderId = 1,
            IsUrging = true,
            IsBatchDelivery = false,
            IsPaused = false,
            IsForceCompleted = false,
            AdjustmentRemark = "测试"
        };
        var result = await _controller.SaveUrging(request);

        var (_, response) = AssertOk<ApiResponse<bool>>(result);
        Assert.True(response.Success);
        Assert.True(response.Data);
    }

    [Fact]
    public async Task SaveUrging_PassesParams_ToService()
    {
        _serviceMock.Setup(x => x.SaveUrgingAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync(true);

        var request = new SaveUrgingRequest { WorkOrderId = 5, IsUrging = true, IsBatchDelivery = true, IsPaused = true, IsForceCompleted = true, AdjustmentRemark = "紧急" };
        await _controller.SaveUrging(request);

        _serviceMock.Verify(x => x.SaveUrgingAsync(5, true, true, true, true, "紧急"), Times.Once);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        var ctx = new Dictionary<string, List<string>> { ["Field1"] = new() { "A" } };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(ctx);

        var result = await _controller.GetFilterContexts();

        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new PagedResult<OrderDemandAdjustmentDto> { Items = new List<OrderDemandAdjustmentDto>() });

        await _controller.GetPaged(keyword: "WO001");

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Keyword == "WO001"), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new PagedResult<OrderDemandAdjustmentDto> { Items = new List<OrderDemandAdjustmentDto>() });

        await _controller.GetPaged(pageSize: 9999);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new PagedResult<OrderDemandAdjustmentDto> { Items = new List<OrderDemandAdjustmentDto>() });

        var filtersJson = "[{\"Field\":\"ScheduleStage\",\"Value\":\"1\"}]";
        await _controller.GetPaged(filters: filtersJson);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Filters != null && q.Filters.Count > 0), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
    }
}
