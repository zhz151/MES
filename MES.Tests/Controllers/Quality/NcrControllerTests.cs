using Microsoft.AspNetCore.Mvc;
using Moq;
using MES.Api.Controllers.Quality;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class NcrControllerTests : ControllerTestBase
{
    private readonly Mock<INcrService> _serviceMock;
    private readonly NcrController _controller;

    public NcrControllerTests()
    {
        _serviceMock = new Mock<INcrService>();
        _controller = new NcrController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<NcrDto>
        {
            Items = new List<NcrDto> { new() { Id = 1, BatchNo = "BATCH001", PipeCategory = PipeCategory.OrderFinished } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<NcrDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetAll_PassesQueryParams()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<NcrDto> { Items = new List<NcrDto>() });

        // Act
        await _controller.GetAll(pageIndex: 1, pageSize: 20, keyword: "BATCH001", sortBy: "reportdate");

        // Assert
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q =>
            q.Keyword == "BATCH001" && q.SortBy == "reportdate")), Times.Once);
    }

    [Fact]
    public async Task GetAllList_ReturnsOk()
    {
        // Arrange
        var list = new List<NcrDto> { new() { Id = 1, BatchNo = "BATCH001", PipeCategory = PipeCategory.OrderFinished } };
        _serviceMock.Setup(x => x.GetAllListAsync()).ReturnsAsync(list);

        // Act
        var result = await _controller.GetAllList();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<NcrDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new NcrDto { Id = 1, BatchNo = "BATCH001", PipeCategory = PipeCategory.OrderFinished };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<NcrDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((NcrDto?)null);

        // Act
        var result = await _controller.GetById(999);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<NcrDto>>(notFoundResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateNcrRequest { BatchNo = "BATCH001" };
        var dto = new NcrDto { Id = 1, BatchNo = "BATCH001", PipeCategory = PipeCategory.OrderFinished, Status = NcrStatus.Processing };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<NcrDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateNcrRequest { ReportDate = DateTime.Today, DefectiveQuantity = 10 };
        var dto = new NcrDto { Id = 1, DefectiveQuantity = 10 };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<NcrDto>>(result);
        Assert.Equal(10, response.Data?.DefectiveQuantity);
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
    public async Task UpdateStatus_ReturnsOk()
    {
        // Arrange
        var request = new UpdateNcrStatusRequest { Status = NcrStatus.Closed };
        var dto = new NcrDto { Id = 1, Status = NcrStatus.Closed };
        _serviceMock.Setup(x => x.UpdateStatusAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.UpdateStatus(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<NcrDto>>(result);
        Assert.Equal(NcrStatus.Closed, response.Data?.Status);
    }

    [Fact]
    public async Task LookupBatch_ReturnsOk()
    {
        // Arrange
        var dto = new NcrLookupResultDto { WorkOrderNo = "WO-001", PlantGrade = "304" };
        _serviceMock.Setup(x => x.LookupBatchAsync("BATCH001")).ReturnsAsync(dto);

        // Act
        var result = await _controller.LookupBatch("BATCH001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<NcrLookupResultDto?>>(result);
        Assert.Equal("WO-001", response.Data!.WorkOrderNo);
    }

    [Fact]
    public async Task GetPendingChecks_ReturnsOk()
    {
        // Arrange
        var pendingList = new List<NcrPendingCheckDto>
        {
            new() { BatchNo = "BATCH001", DefectQuantity = 10, TotalQuantity = 100, Percentage = 10m }
        };
        _serviceMock.Setup(x => x.GetPendingChecksAsync()).ReturnsAsync(pendingList);

        // Act
        var result = await _controller.GetPendingChecks();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<NcrPendingCheckDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        // Arrange
        var filterContexts = new Dictionary<string, List<string>>
        {
            ["status"] = new() { "Processing", "Closed" }
        };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(filterContexts);

        // Act
        var result = await _controller.GetFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetFilterContexts_Empty_ReturnsEmpty()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(new Dictionary<string, List<string>>());

        // Act
        var result = await _controller.GetFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.Empty(response.Data!);
    }
}
