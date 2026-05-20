using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class PurchaseOrderControllerTests : ControllerTestBase
{
    private readonly Mock<IPurchaseOrderService> _serviceMock;
    private readonly Mock<ILogger<PurchaseOrderController>> _loggerMock;
    private readonly PurchaseOrderController _controller;

    public PurchaseOrderControllerTests()
    {
        _serviceMock = new Mock<IPurchaseOrderService>();
        _loggerMock = CreateLoggerMock<PurchaseOrderController>();
        _controller = new PurchaseOrderController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<PurchaseOrderDto>
        {
            Items = new List<PurchaseOrderDto> { new() { Id = 1, OrderNo = "PO001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<PurchaseOrderQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<PurchaseOrderDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<PurchaseOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<PurchaseOrderDto> { Items = new List<PurchaseOrderDto>() });

        // Act
        var result = await _controller.GetPaged(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<PurchaseOrderDto>>>(result);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<PurchaseOrderQueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new PurchaseOrderDto { Id = 1, OrderNo = "PO001" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PurchaseOrderDto>>(result);
        Assert.Equal("PO001", response.Data?.OrderNo);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreatePurchaseOrderRequest { SupplierId = 1 };
        var dto = new PurchaseOrderDto { Id = 1, OrderNo = "PO001" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PurchaseOrderDto>>(result);
        Assert.Equal("PO001", response.Data?.OrderNo);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreatePurchaseOrderRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<PurchaseOrderDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task CreateBatch_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreatePurchaseOrderRequest> { new() { SupplierId = 1 } };
        var dtos = new List<PurchaseOrderDto> { new() { Id = 1, OrderNo = "PO001" } };
        _serviceMock.Setup(x => x.CreateBatchAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.CreateBatch(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<PurchaseOrderDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdatePurchaseOrderRequest { SupplierId = 1 };
        var dto = new PurchaseOrderDto { Id = 1, OrderNo = "PO002" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PurchaseOrderDto>>(result);
        Assert.Equal("PO002", response.Data?.OrderNo);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdatePurchaseOrderRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<PurchaseOrderDto>>(result);
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
    public async Task SyncAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.SyncAllAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.SyncAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task SyncSingle_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.SyncSingleAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.SyncSingle(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsOk()
    {
        // Arrange
        var request = new UpdateOrderStatusRequest { IsForceCompleted = true };
        _serviceMock.Setup(x => x.UpdateStatusAsync(1, request)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdateStatus(1, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetProcurementStatus_ReturnsOk()
    {
        // Arrange
        var list = new List<ProcurementStatusDto> { new() { WorkOrderNo = "PO001" } };
        _serviceMock.Setup(x => x.GetProcurementStatusAsync()).ReturnsAsync(list);

        // Act
        var result = await _controller.GetProcurementStatus();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<ProcurementStatusDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetMismatchedOrders_ReturnsOk()
    {
        // Arrange
        var list = new List<OrderMismatchInfo> { new() { OrderNo = "PO001" } };
        _serviceMock.Setup(x => x.GetMismatchedPurchaseOrdersAsync()).ReturnsAsync(list);

        // Act
        var result = await _controller.GetMismatchedOrders();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<OrderMismatchInfo>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetPlanDetail_ReturnsOk()
    {
        // Arrange
        var dto = new PlanDetailDto { WorkOrderNo = "WO001" };
        _serviceMock.Setup(x => x.GetPlanDetailAsync("WO001", "原料")).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetPlanDetail("WO001", "原料");

        // Assert
        var (_, response) = AssertOk<ApiResponse<PlanDetailDto>>(result);
        Assert.Equal("WO001", response.Data?.WorkOrderNo);
    }

    [Fact]
    public async Task GetPlanDetail_ReturnsFail_WhenNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPlanDetailAsync("UNKNOWN", "原料")).ReturnsAsync((PlanDetailDto?)null);

        // Act
        var result = await _controller.GetPlanDetail("UNKNOWN", "原料");

        // Assert
        var (_, response) = AssertOk<ApiResponse<PlanDetailDto>>(result);
        Assert.False(response.Success);
    }
}
