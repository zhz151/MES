using Microsoft.AspNetCore.Mvc;
using Moq;
using MES.Api.Controllers.Configuration;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class WorkstationControllerTests : ControllerTestBase
{
    private readonly Mock<IWorkstationService> _serviceMock;
    private readonly WorkstationController _controller;

    public WorkstationControllerTests()
    {
        _serviceMock = new Mock<IWorkstationService>();
        _controller = new WorkstationController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<WorkstationDto>
        {
            Items = new List<WorkstationDto> { new() { Id = 1, Code = "WS001", SectionName = "酸洗" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<WorkstationDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<WorkstationDto> { Items = new List<WorkstationDto>() });

        // Act
        var result = await _controller.GetPaged(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<WorkstationDto>>>(result);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetByCode_ReturnsOk()
    {
        // Arrange
        var dto = new WorkstationDto { Code = "WS001", SectionName = "酸洗", ReportType = "PicklingInRecord" };
        _serviceMock.Setup(x => x.GetByCodeAsync("WS001")).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetByCode("WS001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<WorkstationDto>>(result);
        Assert.Equal("WS001", response.Data?.Code);
    }

    [Fact]
    public async Task GetByCode_ReturnsBadRequest_WhenEmpty()
    {
        // Act
        var result = await _controller.GetByCode("");

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<WorkstationDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetByCode_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetByCodeAsync("NONEXISTENT")).ReturnsAsync((WorkstationDto?)null);

        // Act
        var result = await _controller.GetByCode("NONEXISTENT");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<WorkstationDto>>(notFoundResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Save_ReturnsOk()
    {
        // Arrange
        var dto = new WorkstationDto { Code = "WS001", SectionName = "酸洗", ReportType = "PicklingInRecord" };
        _serviceMock.Setup(x => x.SaveAsync(dto)).ReturnsAsync(true);

        // Act
        var result = await _controller.Save(dto);

        // Assert
        var (_, response) = AssertOk<ApiResponse<bool>>(result);
        Assert.True(response.Data);
    }

    [Fact]
    public async Task Delete_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteAsync(1)).ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<bool>>(result);
        Assert.True(response.Data);
    }
}
