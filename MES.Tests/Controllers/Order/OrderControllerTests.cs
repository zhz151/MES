using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Order;
using MES.Core.Models;
using MES.Services.Order;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Shared;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.Infrastructure;

namespace MES.Tests.Controllers;

public class OrderControllerTests : ControllerTestBase
{
    private readonly Mock<IOrderService> _serviceMock;
    private readonly Mock<IOperationLogService> _operationLogMock;
    private readonly OrderController _controller;

    public OrderControllerTests()
    {
        _serviceMock = new Mock<IOrderService>();
        _operationLogMock = new Mock<IOperationLogService>();
        _controller = new OrderController(_serviceMock.Object, _operationLogMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<SalesOrderListDto>
        {
            Items = new List<SalesOrderListDto> { new() { Id = 1, OrderNumber = "SO001" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged(pageIndex: 1, pageSize: 20);

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
    public async Task PrintListFile_ReturnsPdfFile()
    {
        // Arrange
        var request = new OrderPrintListRequest
        {
            Title = "订单列表",
            Items = new List<Dictionary<string, object>>
            {
                new() { ["ordernumber"] = "SO001", ["customername"] = "客户A" }
            },
            Columns = new List<PrintColumnDef>
            {
                new() { Key = "ordernumber", Label = "订单号" },
                new() { Key = "customername", Label = "客户名称" }
            }
        };
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        _serviceMock.Setup(x => x.PrintOrderListAsync(request.Title, request.Items, request.Columns))
            .ReturnsAsync(pdfBytes);

        // Act
        var result = await _controller.PrintListFile(request);

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal(pdfBytes, fileResult.FileContents);
        _serviceMock.Verify(x => x.PrintOrderListAsync(request.Title, request.Items, request.Columns), Times.Once);
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
    public async Task GetPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()))
            .ReturnsAsync(new PagedResult<SalesOrderListDto> { Items = new List<SalesOrderListDto>() });
        var result = await _controller.GetPaged(pageSize: 9999);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()))
            .ReturnsAsync(new PagedResult<SalesOrderListDto> { Items = new List<SalesOrderListDto>() });
        await _controller.GetPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Keyword == "测试"), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()))
            .ReturnsAsync(new PagedResult<SalesOrderListDto> { Items = new List<SalesOrderListDto>() });
        await _controller.GetPaged(sortBy: "OrderNumber");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "OrderNumber"), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()))
            .ReturnsAsync(new PagedResult<SalesOrderListDto> { Items = new List<SalesOrderListDto>() });
        var filtersJson = "[{\"Field\":\"Status\",\"Operator\":\"equals\",\"Value\":\"Open\"}]";
        await _controller.GetPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Filters != null && q.Filters.Count > 0), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesTechnicalStatus_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()))
            .ReturnsAsync(new PagedResult<SalesOrderListDto> { Items = new List<SalesOrderListDto>() });
        await _controller.GetPaged(technicalStatus: "已完成");
        _serviceMock.Verify(x => x.GetPagedAsync(It.IsAny<QueryParams>(), "已完成", It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesOrderStatus_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()))
            .ReturnsAsync(new PagedResult<SalesOrderListDto> { Items = new List<SalesOrderListDto>() });
        await _controller.GetPaged(orderStatus: "已确认");
        _serviceMock.Verify(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<string?>(), "已确认", It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OrderDeliveryEstimateFilterDto?>()), Times.Once);
    }
}
