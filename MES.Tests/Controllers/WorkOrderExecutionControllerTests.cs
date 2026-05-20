using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class WorkOrderExecutionControllerTests : ControllerTestBase
{
    private readonly Mock<IWorkOrderExecutionService> _serviceMock;
    private readonly WorkOrderExecutionController _controller;

    public WorkOrderExecutionControllerTests()
    {
        _serviceMock = new Mock<IWorkOrderExecutionService>();
        _controller = new WorkOrderExecutionController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<WorkOrderExecutionSummaryDto>
        {
            Items = new List<WorkOrderExecutionSummaryDto> { new() { Id = 1, WorkOrderNo = "WO001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged(new QueryParams());

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
}
