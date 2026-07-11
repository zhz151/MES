using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.WorkOrder;
using MES.Core.Models;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.WorkOrder;

namespace MES.Tests.Controllers;

public class WorkOrderExecutionControllerTests : ControllerTestBase
{
    private readonly Mock<IWorkOrderExecutionService> _serviceMock;
    private readonly Mock<IWorkOrderListSummaryRefreshService> _listSummaryServiceMock;
    private readonly WorkOrderExecutionController _controller;

    public WorkOrderExecutionControllerTests()
    {
        _serviceMock = new Mock<IWorkOrderExecutionService>();
        _listSummaryServiceMock = new Mock<IWorkOrderListSummaryRefreshService>();
        _controller = new WorkOrderExecutionController(_serviceMock.Object, _listSummaryServiceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<WorkOrderExecutionSummaryDto>
        {
            Items = new List<WorkOrderExecutionSummaryDto> { new() { Id = 1, WorkOrderNo = "WO001" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged(pageIndex: 1, pageSize: 20);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task RefreshAll_ReturnsOk()
    {
        // Arrange
        var dto = new WorkOrderExecutionRefreshResultDto { RefreshedCount = 5 };
        _serviceMock.Setup(x => x.RefreshAllAsync()).ReturnsAsync(dto);

        // Act
        var result = await _controller.RefreshAll();

        // Assert
        var (_, response) = AssertOk<ApiResponse<WorkOrderExecutionRefreshResultDto>>(result);
        Assert.Equal(5, response.Data?.RefreshedCount);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        var filterContexts = new Dictionary<string, List<string>>
        {
            ["Field1"] = new() { "A", "B" }
        };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(filterContexts);
        var result = await _controller.GetFilterContexts();
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetFilterContexts_Empty_ReturnsEmpty()
    {
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(new Dictionary<string, List<string>>());
        var result = await _controller.GetFilterContexts();
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new PagedResult<WorkOrderExecutionSummaryDto> { Items = new List<WorkOrderExecutionSummaryDto>() });
        var result = await _controller.GetPaged(pageSize: 9999);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new PagedResult<WorkOrderExecutionSummaryDto> { Items = new List<WorkOrderExecutionSummaryDto>() });
        await _controller.GetPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Keyword == "测试"), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new PagedResult<WorkOrderExecutionSummaryDto> { Items = new List<WorkOrderExecutionSummaryDto>() });
        await _controller.GetPaged(sortBy: "WorkOrderNo");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "WorkOrderNo"), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new PagedResult<WorkOrderExecutionSummaryDto> { Items = new List<WorkOrderExecutionSummaryDto>() });
        var filtersJson = "[{\"Field\":\"Status\",\"Operator\":\"equals\",\"Value\":\"Completed\"}]";
        await _controller.GetPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Filters != null && q.Filters.Count > 0), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
    }
}
