using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Quality;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class ProcessInspectionControllerTests : ControllerTestBase
{
    private readonly Mock<IProcessInspectionService> _serviceMock;
    private readonly Mock<ILogger<ProcessInspectionController>> _loggerMock;
    private readonly ProcessInspectionController _controller;

    public ProcessInspectionControllerTests()
    {
        _serviceMock = new Mock<IProcessInspectionService>();
        _loggerMock = CreateLoggerMock<ProcessInspectionController>();
        _controller = new ProcessInspectionController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<ProcessInspectionDto>
        {
            Items = new List<ProcessInspectionDto> { new() { Id = 1, ProcessName = "热处理" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<ProcessInspectionDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetAll_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<ProcessInspectionDto> { Items = new List<ProcessInspectionDto>() });

        // Act
        var result = await _controller.GetAll(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<ProcessInspectionDto>>>(result);
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateProcessInspectionRequest { InspectionDate = DateTime.Today };
        var dto = new ProcessInspectionDto { Id = 1, ProcessName = "热处理", InspectionDate = DateTime.Today };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProcessInspectionDto>>(result);
        Assert.Equal("热处理", response.Data?.ProcessName);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateProcessInspectionRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ProcessInspectionDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Delete_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task BatchCreate_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateProcessInspectionRequest>
        {
            new() { ProcessName = "新工序", InspectionDate = DateTime.Today, ManufacturingSpec = "规格", SectionName = "工段" }
        };
        var dtos = new List<ProcessInspectionDto>
        {
            new() { Id = 1, ProcessName = "新工序" }
        };
        _serviceMock.Setup(x => x.BatchCreateAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreate(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<ProcessInspectionDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task BatchCreate_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchCreate(new List<CreateProcessInspectionRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<ProcessInspectionDto>>>(result);
        Assert.False(response.Success);
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
    public async Task GetAll_PassesKeyword_ToService()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<ProcessInspectionDto> { Items = new List<ProcessInspectionDto>() });

        // Act
        await _controller.GetAll(keyword: "测试搜索");

        // Assert
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q => q.Keyword == "测试搜索")), Times.Once);
    }

    [Fact]
    public async Task GetAll_PassesSortBy_ToService()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<ProcessInspectionDto> { Items = new List<ProcessInspectionDto>() });

        // Act
        await _controller.GetAll(sortBy: "ProcessName", isDescending: false);

        // Assert
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q => q.SortBy == "ProcessName" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetAll_DefaultSortBy_IsCreatedTime()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<ProcessInspectionDto> { Items = new List<ProcessInspectionDto>() });

        // Act
        await _controller.GetAll();

        // Assert
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q => q.SortBy == "createdtime")), Times.Once);
    }

    [Fact]
    public async Task GetAll_PassesFiltersJson_ToService()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<ProcessInspectionDto> { Items = new List<ProcessInspectionDto>() });

        var filtersJson = "[{\"Field\":\"ProcessName\",\"Operator\":\"contains\",\"Value\":\"test\"}]";

        // Act
        await _controller.GetAll(filters: filtersJson);

        // Assert
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q =>
            q.Filters != null && q.Filters.Count == 1 && q.Filters[0].Field == "ProcessName")), Times.Once);
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
}
