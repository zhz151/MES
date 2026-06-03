using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class InventoryControllerTests : ControllerTestBase
{
    private readonly Mock<IInventoryService> _serviceMock;
    private readonly InventoryController _controller;

    public InventoryControllerTests()
    {
        _serviceMock = new Mock<IInventoryService>();
        _controller = new InventoryController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<InventoryBatchDto>
        {
            Items = new List<InventoryBatchDto> { new() { Id = 1, BatchNo = "BATCH001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InventoryQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged(pageIndex: 1, pageSize: 20);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<InventoryBatchDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new InventoryBatchDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<InventoryBatchDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task Inbound_ReturnsOk()
    {
        // Arrange
        var request = new CreateInboundRequest { ProductionBatchNo = "BATCH001" };
        var dto = new InventoryBatchDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.InboundAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Inbound(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<InventoryBatchDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task Inbound_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Inbound(new CreateInboundRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<InventoryBatchDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Outbound_ReturnsOk()
    {
        // Arrange
        var request = new CreateOutboundRequest { SourceOrderNo = "OB001" };
        var dto = new OutboundRecordDto { Id = 1, SourceOrderNo = "OB001" };
        _serviceMock.Setup(x => x.OutboundAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Outbound(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<OutboundRecordDto>>(result);
        Assert.Equal("OB001", response.Data?.SourceOrderNo);
    }

    [Fact]
    public async Task Outbound_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Outbound(new CreateOutboundRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<OutboundRecordDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetOutboundRecords_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<OutboundRecordDto>
        {
            Items = new List<OutboundRecordDto> { new() { Id = 1, SourceOrderNo = "OB001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetOutboundRecordsAsync(It.IsAny<OutboundQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetOutboundRecords(pageIndex: 1, pageSize: 20);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<OutboundRecordDto>>>(result);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task UpdateInventoryBatch_ReturnsOk()
    {
        // Arrange
        var request = new UpdateInventoryBatchRequest { BatchNo = "BATCH002" };
        var dto = new InventoryBatchDto { Id = 1, BatchNo = "BATCH002" };
        _serviceMock.Setup(x => x.UpdateInventoryBatchAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.UpdateInventoryBatch(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<InventoryBatchDto>>(result);
        Assert.Equal("BATCH002", response.Data?.BatchNo);
    }

    [Fact]
    public async Task UpdateInventoryBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.UpdateInventoryBatch(1, new UpdateInventoryBatchRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<InventoryBatchDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task HardDeleteInventoryBatch_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.HardDeleteInventoryBatchAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.HardDeleteInventoryBatch(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task UpdateOutboundRecord_ReturnsOk()
    {
        // Arrange
        var request = new UpdateOutboundRecordRequest { SourceOrderNo = "OB002" };
        var dto = new OutboundRecordDto { Id = 1, SourceOrderNo = "OB002" };
        _serviceMock.Setup(x => x.UpdateOutboundRecordAsync(1L, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.UpdateOutboundRecord(1L, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<OutboundRecordDto>>(result);
        Assert.Equal("OB002", response.Data?.SourceOrderNo);
    }

    [Fact]
    public async Task UpdateOutboundRecord_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.UpdateOutboundRecord(1L, new UpdateOutboundRecordRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<OutboundRecordDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task HardDeleteOutboundRecord_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.HardDeleteOutboundRecordAsync(1L)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.HardDeleteOutboundRecord(1L);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task ValidateSourceOrder_ReturnsOk()
    {
        // Arrange
        var request = new SourceOrderValidationRequest { SourceOrderNo = "SO001" };
        var dto = new SourceOrderValidationResult { IsValid = true };
        _serviceMock.Setup(x => x.ValidateSourceOrderAsync("SO001", It.IsAny<string?>(), It.IsAny<int?>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _controller.ValidateSourceOrder(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SourceOrderValidationResult>>(result);
        Assert.True(response.Data?.IsValid);
    }

    [Fact]
    public async Task ValidateWorkOrderNos_ReturnsOk()
    {
        // Arrange
        var list = new List<string> { "WO001" };
        _serviceMock.Setup(x => x.ValidateWarehouseWorkOrderNosAsync(1)).ReturnsAsync(list);

        // Act
        var result = await _controller.ValidateWorkOrderNos(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<string>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task BatchInbound_ReturnsOk()
    {
        // Arrange
        var request = new BatchInboundRequest();
        var dto = new BatchInboundResult { SuccessCount = 5 };
        _serviceMock.Setup(x => x.BatchInboundAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.BatchInbound(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<BatchInboundResult>>(result);
        Assert.Equal(5, response.Data?.SuccessCount);
    }

    [Fact]
    public async Task BatchInbound_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchInbound(new BatchInboundRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<BatchInboundResult>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task BatchOutbound_ReturnsOk()
    {
        // Arrange
        var request = new BatchOutboundRequest();
        var dto = new BatchOutboundResult { SuccessCount = 3 };
        _serviceMock.Setup(x => x.BatchOutboundAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.BatchOutbound(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<BatchOutboundResult>>(result);
        Assert.Equal(3, response.Data?.SuccessCount);
    }

    [Fact]
    public async Task BatchOutbound_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchOutbound(new BatchOutboundRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<BatchOutboundResult>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetInventoryFilterContexts_ReturnsOk()
    {
        // Arrange
        var ctx = new Dictionary<string, List<string>> { ["BatchNo"] = new() { "CK001" } };
        _serviceMock.Setup(x => x.GetInventoryFilterContextsAsync()).ReturnsAsync(ctx);

        // Act
        var result = await _controller.GetInventoryFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetInventoryFilterContexts_Empty_ReturnsEmpty()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetInventoryFilterContextsAsync()).ReturnsAsync(new Dictionary<string, List<string>>());

        // Act
        var result = await _controller.GetInventoryFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
    }

    [Fact]
    public async Task GetOutboundFilterContexts_ReturnsOk()
    {
        // Arrange
        var ctx = new Dictionary<string, List<string>> { ["OutboundType"] = new() { "SalesOut" } };
        _serviceMock.Setup(x => x.GetOutboundFilterContextsAsync()).ReturnsAsync(ctx);

        // Act
        var result = await _controller.GetOutboundFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InventoryQueryParams>()))
            .ReturnsAsync(new PagedResult<InventoryBatchDto> { Items = new List<InventoryBatchDto>() });
        await _controller.GetPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<InventoryQueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InventoryQueryParams>()))
            .ReturnsAsync(new PagedResult<InventoryBatchDto> { Items = new List<InventoryBatchDto>() });
        await _controller.GetPaged(sortBy: "Code", isDescending: false);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<InventoryQueryParams>(q => q.SortBy == "Code" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_DefaultSortBy_IsCreatedTime()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InventoryQueryParams>()))
            .ReturnsAsync(new PagedResult<InventoryBatchDto> { Items = new List<InventoryBatchDto>() });
        await _controller.GetPaged(pageIndex: 1, pageSize: 20);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<InventoryQueryParams>(q => q.SortBy == "CreatedTime")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFiltersJson_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InventoryQueryParams>()))
            .ReturnsAsync(new PagedResult<InventoryBatchDto> { Items = new List<InventoryBatchDto>() });
        var filtersJson = "[{\"Field\":\"Code\",\"Operator\":\"contains\",\"Value\":\"T\"}]";
        await _controller.GetPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<InventoryQueryParams>(q =>
            q.Filters != null && q.Filters.Count == 1 && q.Filters[0].Field == "Code")), Times.Once);
    }

    [Fact]
    public async Task GetOutboundRecords_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetOutboundRecordsAsync(It.IsAny<OutboundQueryParams>()))
            .ReturnsAsync(new PagedResult<OutboundRecordDto> { Items = new List<OutboundRecordDto>() });
        await _controller.GetOutboundRecords(keyword: "测试");
        _serviceMock.Verify(x => x.GetOutboundRecordsAsync(It.Is<OutboundQueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetOutboundRecords_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetOutboundRecordsAsync(It.IsAny<OutboundQueryParams>()))
            .ReturnsAsync(new PagedResult<OutboundRecordDto> { Items = new List<OutboundRecordDto>() });
        await _controller.GetOutboundRecords(sortBy: "Code", isDescending: false);
        _serviceMock.Verify(x => x.GetOutboundRecordsAsync(It.Is<OutboundQueryParams>(q => q.SortBy == "Code" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetOutboundRecords_DefaultSortBy_IsCreatedTime()
    {
        _serviceMock.Setup(x => x.GetOutboundRecordsAsync(It.IsAny<OutboundQueryParams>()))
            .ReturnsAsync(new PagedResult<OutboundRecordDto> { Items = new List<OutboundRecordDto>() });
        await _controller.GetOutboundRecords(pageIndex: 1, pageSize: 20);
        _serviceMock.Verify(x => x.GetOutboundRecordsAsync(It.Is<OutboundQueryParams>(q => q.SortBy == "CreatedTime")), Times.Once);
    }

    [Fact]
    public async Task GetOutboundRecords_PassesFiltersJson_ToService()
    {
        _serviceMock.Setup(x => x.GetOutboundRecordsAsync(It.IsAny<OutboundQueryParams>()))
            .ReturnsAsync(new PagedResult<OutboundRecordDto> { Items = new List<OutboundRecordDto>() });
        var filtersJson = "[{\"Field\":\"Code\",\"Operator\":\"contains\",\"Value\":\"T\"}]";
        await _controller.GetOutboundRecords(filters: filtersJson);
        _serviceMock.Verify(x => x.GetOutboundRecordsAsync(It.Is<OutboundQueryParams>(q =>
            q.Filters != null && q.Filters.Count == 1 && q.Filters[0].Field == "Code")), Times.Once);
    }

    [Fact]
    public async Task GetOutboundFilterContexts_Empty_ReturnsEmpty()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetOutboundFilterContextsAsync()).ReturnsAsync(new Dictionary<string, List<string>>());

        // Act
        var result = await _controller.GetOutboundFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
    }
}
