using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Materials;
using MES.Core.Models;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Materials;

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
        // 设置非管理员的 HttpContext，避免 User.IsInRole("Admin") 引发 NullReferenceException
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
                {
                    new(ClaimTypes.Name, "testuser"),
                    new(ClaimTypes.Role, "MaterialDirector"),
                }, "test"))
            }
        };
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<PurchaseOrderDto>
        {
            Items = new List<PurchaseOrderDto> { new() { Id = 1, OrderNo = "PO001" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
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
        _serviceMock.Setup(x => x.UpdateAsync(1, request, false)).ReturnsAsync(dto);

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
        _serviceMock.Setup(x => x.DeleteAsync(1, false)).Returns(Task.CompletedTask);

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
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<PurchaseOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<PurchaseOrderDto> { Items = new List<PurchaseOrderDto>() });
        await _controller.GetPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<PurchaseOrderQueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<PurchaseOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<PurchaseOrderDto> { Items = new List<PurchaseOrderDto>() });
        await _controller.GetPaged(sortBy: "OrderNo");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<PurchaseOrderQueryParams>(q => q.SortBy == "OrderNo")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_UsesDefaultSortBy_WhenNotProvided()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<PurchaseOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<PurchaseOrderDto> { Items = new List<PurchaseOrderDto>() });
        await _controller.GetPaged();
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<PurchaseOrderQueryParams>(q => q.SortBy == "CreatedTime")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<PurchaseOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<PurchaseOrderDto> { Items = new List<PurchaseOrderDto>() });
        var filtersJson = "[{\"Field\":\"Status\",\"Operator\":\"equals\",\"Value\":\"Open\"}]";
        await _controller.GetPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<PurchaseOrderQueryParams>(q => q.Filters != null && q.Filters.Count > 0)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesStatus_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<PurchaseOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<PurchaseOrderDto> { Items = new List<PurchaseOrderDto>() });
        await _controller.GetPaged(status: "Open");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<PurchaseOrderQueryParams>(q => q.Status == "Open")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesDateParams_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<PurchaseOrderQueryParams>()))
            .ReturnsAsync(new PagedResult<PurchaseOrderDto> { Items = new List<PurchaseOrderDto>() });
        var dateFrom = new DateTime(2025, 1, 1);
        var dateTo = new DateTime(2025, 12, 31);
        var requiredFrom = new DateTime(2025, 2, 1);
        var requiredTo = new DateTime(2025, 11, 30);
        await _controller.GetPaged(dateFrom: dateFrom, dateTo: dateTo, requiredDateFrom: requiredFrom, requiredDateTo: requiredTo);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<PurchaseOrderQueryParams>(q =>
            q.DateFrom == dateFrom && q.DateTo == dateTo &&
            q.RequiredDateFrom == requiredFrom && q.RequiredDateTo == requiredTo)), Times.Once);
    }
}
