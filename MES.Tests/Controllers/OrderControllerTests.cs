using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class OrderControllerTests : ControllerTestBase
{
    private readonly Mock<IOrderService> _serviceMock;
    private readonly OrderController _controller;

    public OrderControllerTests()
    {
        _serviceMock = new Mock<IOrderService>();
        _controller = new OrderController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<SalesOrderListDto>
        {
            Items = new List<SalesOrderListDto> { new() { Id = 1, OrderNumber = "SO001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged(new QueryParams());

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<SalesOrderListDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new SalesOrderDetailDto { Id = 1, OrderNumber = "SO001" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SalesOrderDetailDto>>(result);
        Assert.Equal("SO001", response.Data?.OrderNumber);
    }

    [Fact]
    public async Task GetIdByOrderNumber_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetIdByOrderNumberAsync("SO001")).ReturnsAsync((int?)1);

        // Act
        var result = await _controller.GetIdByOrderNumber("SO001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<int?>>(result);
        Assert.Equal(1, response.Data);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateSalesOrderRequest { OrderNumber = "SO001" };
        var dto = new SalesOrderListDto { Id = 1, OrderNumber = "SO001" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SalesOrderListDto>>(result);
        Assert.Equal("SO001", response.Data?.OrderNumber);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateSalesOrderRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<SalesOrderListDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateSalesOrderRequest { OrderNumber = "SO002" };
        var dto = new SalesOrderListDto { Id = 1, OrderNumber = "SO002" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SalesOrderListDto>>(result);
        Assert.Equal("SO002", response.Data?.OrderNumber);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateSalesOrderRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<SalesOrderListDto>>(result);
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
    public async Task AddItem_ReturnsOk()
    {
        // Arrange
        var request = new AddOrderItemRequest { Sequence = 1 };
        var dto = new OrderItemDto { Id = 1, Sequence = 1 };
        _serviceMock.Setup(x => x.AddItemAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.AddItem(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<OrderItemDto>>(result);
        Assert.Equal(1, response.Data?.Sequence);
    }

    [Fact]
    public async Task UpdateItem_ReturnsOk()
    {
        // Arrange
        var request = new UpdateOrderItemRequest { Sequence = 2 };
        var dto = new OrderItemDto { Id = 1, Sequence = 2 };
        _serviceMock.Setup(x => x.UpdateItemAsync(1, 1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.UpdateItem(1, 1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<OrderItemDto>>(result);
        Assert.Equal(2, response.Data?.Sequence);
    }

    [Fact]
    public async Task DeleteItem_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteItemAsync(1, 1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteItem(1, 1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task SaveAll_ReturnsOk()
    {
        // Arrange
        var request = new SaveAllOrderRequest();
        var dto = new SaveAllOrderResponse { RowVersion = new byte[] { 1, 2, 3 } };
        _serviceMock.Setup(x => x.SaveAllAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.SaveAll(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SaveAllOrderResponse>>(result);
        Assert.NotNull(response.Data?.RowVersion);
    }

    [Fact]
    public async Task PrintOrder_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintOrderAsync(1)).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintOrder(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintOrderBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintOrderBatch(new OrderPrintBatchRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintOrderBatch_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintOrderBatchAsync(It.IsAny<int[]>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintOrderBatch(new OrderPrintBatchRequest { Ids = new[] { 1, 2 } });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintOrderAll_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintOrderAll(new OrderPrintAllRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintOrderAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintOrderAllAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintOrderAll(new OrderPrintAllRequest { Keyword = "SO001" });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintOrderRequirements_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintOrderRequirementsAsync(1)).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintOrderRequirements(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }
}
