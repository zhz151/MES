using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class WorkOrderControllerTests : ControllerTestBase
{
    private readonly Mock<IWorkOrderService> _serviceMock;
    private readonly WorkOrderController _controller;

    public WorkOrderControllerTests()
    {
        _serviceMock = new Mock<IWorkOrderService>();
        _controller = new WorkOrderController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetOrderWorkOrderStatus_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<OrderWorkOrderStatusDto>
        {
            Items = new List<OrderWorkOrderStatusDto> { new() { SalesOrderId = 1, OrderNumber = "SO001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetOrderWorkOrderStatusPageAsync(It.IsAny<WorkOrderQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetOrderWorkOrderStatus(new WorkOrderQueryParams());

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<OrderWorkOrderStatusDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetCancelledOrders_ReturnsOk()
    {
        // Arrange
        var list = new List<CancelledOrderDto> { new() { SalesOrderId = 1, OrderNumber = "SO001" } };
        _serviceMock.Setup(x => x.GetCancelledOrdersAsync()).ReturnsAsync(list);

        // Act
        var result = await _controller.GetCancelledOrders();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<CancelledOrderDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetOrderItemsForWorkOrder_ReturnsOk()
    {
        // Arrange
        var list = new List<OrderItemForWorkOrderDto> { new() { Id = 1, Sequence = 1 } };
        _serviceMock.Setup(x => x.GetOrderItemsForWorkOrderAsync("SO001")).ReturnsAsync(list);

        // Act
        var result = await _controller.GetOrderItemsForWorkOrder("SO001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<OrderItemForWorkOrderDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetOrderItemsForWorkOrder_ReturnsBadRequest_WhenSalesOrderNoEmpty()
    {
        // Act
        var result = await _controller.GetOrderItemsForWorkOrder("");

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<OrderItemForWorkOrderDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GenerateWorkOrders_ReturnsOk()
    {
        // Arrange
        var request = new CreateWorkOrderRequest();
        var list = new List<GeneratedWorkOrderDto> { new() { Id = 1, WorkOrderNo = "WO001" } };
        _serviceMock.Setup(x => x.GenerateWorkOrdersAsync(request)).ReturnsAsync(list);

        // Act
        var result = await _controller.GenerateWorkOrders(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<GeneratedWorkOrderDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GenerateWorkOrders_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.GenerateWorkOrders(new CreateWorkOrderRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<GeneratedWorkOrderDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetList_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<WorkOrderListDto>
        {
            Items = new List<WorkOrderListDto> { new() { Id = 1, WorkOrderNo = "WO001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<WorkOrderQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetList(new WorkOrderQueryParams());

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<WorkOrderListDto>>>(result);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new WorkOrderDetailDto { Id = 1, WorkOrderNo = "WO001" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<WorkOrderDetailDto>>(result);
        Assert.Equal("WO001", response.Data?.WorkOrderNo);
    }

    [Fact]
    public async Task GetByWorkOrderNo_ReturnsOk()
    {
        // Arrange
        var dto = new WorkOrderDetailDto { Id = 1, WorkOrderNo = "WO001" };
        _serviceMock.Setup(x => x.GetByWorkOrderNoAsync("WO001")).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetByWorkOrderNo("WO001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<WorkOrderDetailDto>>(result);
        Assert.Equal("WO001", response.Data?.WorkOrderNo);
    }

    [Fact]
    public async Task GetByWorkOrderNo_ReturnsBadRequest_WhenEmpty()
    {
        // Act
        var result = await _controller.GetByWorkOrderNo("");

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<WorkOrderDetailDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetBySalesOrderNo_ReturnsOk()
    {
        // Arrange
        var list = new List<WorkOrderListDto> { new() { Id = 1, WorkOrderNo = "WO001" } };
        _serviceMock.Setup(x => x.GetBySalesOrderNoAsync("SO001")).ReturnsAsync(list);

        // Act
        var result = await _controller.GetBySalesOrderNo("SO001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<WorkOrderListDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetBySalesOrderNo_ReturnsBadRequest_WhenEmpty()
    {
        // Act
        var result = await _controller.GetBySalesOrderNo("");

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<WorkOrderListDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsOk()
    {
        // Arrange
        var request = new UpdateWorkOrderStatusRequest();
        var dto = new UpdateWorkOrderStatusResponseDto { Id = 1, Status = 1 };
        _serviceMock.Setup(x => x.UpdateStatusAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.UpdateStatus(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<UpdateWorkOrderStatusResponseDto>>(result);
        Assert.Equal(1, response.Data?.Status);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.UpdateStatus(1, new UpdateWorkOrderStatusRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<UpdateWorkOrderStatusResponseDto>>(result);
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
    public async Task SoftDelete_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.SoftDeleteAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.SoftDelete(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task CheckOrderChange_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.CheckAndUpdateWorkOrderStatusAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.CheckOrderChange(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task CheckAllOrderChange_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.CheckAllOrdersChangeAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.CheckAllOrderChange();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetOrderWorkOrderRelation_ReturnsOk()
    {
        // Arrange
        var dto = new OrderWorkOrderRelationDto { OrderNumber = "SO001" };
        _serviceMock.Setup(x => x.GetOrderWorkOrderRelationAsync("SO001")).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetOrderWorkOrderRelation("SO001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<OrderWorkOrderRelationDto>>(result);
        Assert.Equal("SO001", response.Data?.OrderNumber);
    }

    [Fact]
    public async Task GetOrderWorkOrderRelation_ReturnsBadRequest_WhenEmpty()
    {
        // Act
        var result = await _controller.GetOrderWorkOrderRelation("");

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<OrderWorkOrderRelationDto>>(result);
        Assert.False(response.Success);
    }
}
