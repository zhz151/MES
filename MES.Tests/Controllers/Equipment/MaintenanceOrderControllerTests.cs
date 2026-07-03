using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Equipment;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class MaintenanceOrderControllerTests : ControllerTestBase
{
    private readonly Mock<IMaintenanceOrderService> _serviceMock;
    private readonly Mock<ILogger<MaintenanceOrderController>> _loggerMock;
    private readonly MaintenanceOrderController _controller;

    public MaintenanceOrderControllerTests()
    {
        _serviceMock = new Mock<IMaintenanceOrderService>();
        _loggerMock = CreateLoggerMock<MaintenanceOrderController>();
        _controller = new MaintenanceOrderController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<MaintenanceOrderListDto>
        {
            Items = new List<MaintenanceOrderListDto> { new() { Id = 1, EquipmentName = "设备A" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<MaintenanceOrderQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<MaintenanceOrderListDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new MaintenanceOrderListDto { Id = 1, EquipmentName = "设备A" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaintenanceOrderListDto>>(result);
        Assert.Equal("设备A", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateMaintenanceOrderRequest { EquipmentId = 1 };
        var dto = new MaintenanceOrderListDto { Id = 1, EquipmentName = "设备A" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaintenanceOrderListDto>>(result);
        Assert.Equal("设备A", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateMaintenanceOrderRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<MaintenanceOrderListDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task CreateBatch_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateMaintenanceOrderRequest> { new() { EquipmentId = 1 } };
        var dtos = new List<MaintenanceOrderListDto> { new() { Id = 1, EquipmentName = "设备A" } };
        _serviceMock.Setup(x => x.CreateBatchAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.CreateBatch(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<MaintenanceOrderListDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task CreateBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.CreateBatch(new List<CreateMaintenanceOrderRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<MaintenanceOrderListDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateMaintenanceRequest { Executor = "更新人" };
        var dto = new MaintenanceOrderListDto { Id = 1, EquipmentName = "更新名称" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaintenanceOrderListDto>>(result);
        Assert.Equal("更新名称", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateMaintenanceRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<MaintenanceOrderListDto>>(result);
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
    public async Task PrintBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintBatch(new MaintenanceOrderPrintBatchRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintBatch_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintBatchAsync(It.IsAny<int[]>(), It.IsAny<List<PrintColumnDef>>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintBatch(new MaintenanceOrderPrintBatchRequest { Ids = new[] { 1, 2 } });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintAll_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintAll(new MaintenanceOrderPrintAllRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintAllAsync(It.IsAny<MaintenanceOrderQueryParams>(), It.IsAny<List<PrintColumnDef>>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintAll(new MaintenanceOrderPrintAllRequest { Keyword = "设备" });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
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
    public async Task GetPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<MaintenanceOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<MaintenanceOrderListDto> { Items = new List<MaintenanceOrderListDto>() });
        await _controller.GetPaged(pageSize: 9999);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<MaintenanceOrderQueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<MaintenanceOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<MaintenanceOrderListDto> { Items = new List<MaintenanceOrderListDto>() });
        await _controller.GetPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<MaintenanceOrderQueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<MaintenanceOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<MaintenanceOrderListDto> { Items = new List<MaintenanceOrderListDto>() });
        await _controller.GetPaged(sortBy: "Code", isDescending: false);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<MaintenanceOrderQueryParams>(q => q.SortBy == "Code" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_DefaultSortBy_IsId()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<MaintenanceOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<MaintenanceOrderListDto> { Items = new List<MaintenanceOrderListDto>() });
        await _controller.GetPaged();
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<MaintenanceOrderQueryParams>(q => q.SortBy == "Id")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFiltersJson_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<MaintenanceOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<MaintenanceOrderListDto> { Items = new List<MaintenanceOrderListDto>() });
        var filtersJson = "[{\"Field\":\"Code\",\"Operator\":\"contains\",\"Value\":\"T\"}]";
        await _controller.GetPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<MaintenanceOrderQueryParams>(q =>
            q.Filters != null && q.Filters.Count == 1 && q.Filters[0].Field == "Code")), Times.Once);
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
