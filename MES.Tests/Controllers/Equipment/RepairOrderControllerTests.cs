using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class RepairOrderControllerTests : ControllerTestBase
{
    private readonly Mock<IRepairOrderService> _serviceMock;
    private readonly Mock<ILogger<RepairOrderController>> _loggerMock;
    private readonly RepairOrderController _controller;

    public RepairOrderControllerTests()
    {
        _serviceMock = new Mock<IRepairOrderService>();
        _loggerMock = CreateLoggerMock<RepairOrderController>();
        _controller = new RepairOrderController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<RepairOrderListDto>
        {
            Items = new List<RepairOrderListDto> { new() { Id = 1, EquipmentName = "设备A" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<RepairOrderQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<RepairOrderListDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new RepairOrderListDto { Id = 1, EquipmentName = "设备A" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<RepairOrderListDto>>(result);
        Assert.Equal("设备A", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateRepairOrderRequest { EquipmentId = 1 };
        var dto = new RepairOrderListDto { Id = 1, EquipmentName = "设备A" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<RepairOrderListDto>>(result);
        Assert.Equal("设备A", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateRepairOrderRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<RepairOrderListDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task CreateBatch_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateRepairOrderRequest> { new() { EquipmentId = 1 } };
        var dtos = new List<RepairOrderListDto> { new() { Id = 1, EquipmentName = "设备A" } };
        _serviceMock.Setup(x => x.CreateBatchAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.CreateBatch(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<RepairOrderListDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task CreateBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.CreateBatch(new List<CreateRepairOrderRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<RepairOrderListDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateRepairOrderRequest { FaultDescription = "故障描述" };
        var dto = new RepairOrderListDto { Id = 1, EquipmentName = "更新名称" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<RepairOrderListDto>>(result);
        Assert.Equal("更新名称", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateRepairOrderRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<RepairOrderListDto>>(result);
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
        var result = await _controller.PrintBatch(new RepairOrderPrintBatchRequest());

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
        var result = await _controller.PrintBatch(new RepairOrderPrintBatchRequest { Ids = new[] { 1, 2 } });

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
        var result = await _controller.PrintAll(new RepairOrderPrintAllRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintAllAsync(It.IsAny<RepairOrderQueryParams>(), It.IsAny<List<PrintColumnDef>>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintAll(new RepairOrderPrintAllRequest { Keyword = "设备" });

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
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<RepairOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<RepairOrderListDto> { Items = new List<RepairOrderListDto>() });
        await _controller.GetPaged(pageSize: 9999);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<RepairOrderQueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<RepairOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<RepairOrderListDto> { Items = new List<RepairOrderListDto>() });
        await _controller.GetPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<RepairOrderQueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<RepairOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<RepairOrderListDto> { Items = new List<RepairOrderListDto>() });
        await _controller.GetPaged(sortBy: "Code", isDescending: false);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<RepairOrderQueryParams>(q => q.SortBy == "Code" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_DefaultSortBy_IsReportTime()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<RepairOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<RepairOrderListDto> { Items = new List<RepairOrderListDto>() });
        await _controller.GetPaged();
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<RepairOrderQueryParams>(q => q.SortBy == "ReportTime")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFiltersJson_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<RepairOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<RepairOrderListDto> { Items = new List<RepairOrderListDto>() });
        var filtersJson = "[{\"Field\":\"Code\",\"Operator\":\"contains\",\"Value\":\"T\"}]";
        await _controller.GetPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<RepairOrderQueryParams>(q =>
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
