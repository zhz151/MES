using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class PicklingControllerTests : ControllerTestBase
{
    private readonly Mock<IPicklingService> _serviceMock;
    private readonly Mock<ILogger<PicklingController>> _loggerMock;
    private readonly PicklingController _controller;

    public PicklingControllerTests()
    {
        _serviceMock = new Mock<IPicklingService>();
        _loggerMock = CreateLoggerMock<PicklingController>();
        _controller = new PicklingController(_serviceMock.Object, _loggerMock.Object);
    }

    // ========== 入缸记录 ==========

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<PicklingInRecordDto>
        {
            Items = new List<PicklingInRecordDto> { new() { Id = 1, BatchNo = "BATCH001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<PicklingInRecordDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<PicklingInRecordDto> { Items = new List<PicklingInRecordDto>() });

        // Act
        var result = await _controller.GetPaged(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<PicklingInRecordDto>>>(result);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreatePicklingInRecordRequest { BatchNo = "BATCH001", ProcessName = "冷拔", ManufacturingSpec = "219*8", SectionName = "酸洗", InDate = DateTime.Today };
        var dto = new PicklingInRecordDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PicklingInRecordDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreatePicklingInRecordRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<PicklingInRecordDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task BatchCreate_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreatePicklingInRecordRequest>
        {
            new() { BatchNo = "BATCH001", ProcessName = "冷拔", ManufacturingSpec = "219*8", SectionName = "酸洗", InDate = DateTime.Today }
        };
        var dtos = new List<PicklingInRecordDto> { new() { Id = 1, BatchNo = "BATCH001" } };
        _serviceMock.Setup(x => x.BatchCreateAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreate(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<PicklingInRecordDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdatePicklingInRecordRequest { Quantity = 20 };
        var dto = new PicklingInRecordDto { Id = 1, Quantity = 20 };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PicklingInRecordDto>>(result);
        Assert.Equal(20, response.Data?.Quantity);
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
    public async Task GetByBatch_ReturnsOk()
    {
        // Arrange
        var list = new List<PicklingInRecordDto> { new() { Id = 1, BatchNo = "BATCH001" } };
        _serviceMock.Setup(x => x.GetByBatchAsync("BATCH001")).ReturnsAsync(list);

        // Act
        var result = await _controller.GetByBatch("BATCH001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<PicklingInRecordDto>>>(result);
        Assert.Single(response.Data!);
    }

    // ========== 完工记录 ==========

    [Fact]
    public async Task GetOutRecordByInId_ReturnsOk()
    {
        // Arrange
        var dto = new PicklingOutRecordDto { Id = 1, PicklingInRecordId = 1 };
        _serviceMock.Setup(x => x.GetOutRecordByInIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetOutRecordByInId(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PicklingOutRecordDto?>>(result);
        Assert.Equal(1, response.Data?.PicklingInRecordId);
    }

    [Fact]
    public async Task GetOutRecordsPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<PicklingOutRecordDto>
        {
            Items = new List<PicklingOutRecordDto> { new() { Id = 1, PicklingInRecordId = 1 } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetOutRecordsPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetOutRecordsPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<PicklingOutRecordDto>>>(result);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetOutRecordsPaged_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetOutRecordsPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<PicklingOutRecordDto> { Items = new List<PicklingOutRecordDto>() });

        // Act
        var result = await _controller.GetOutRecordsPaged(pageSize: 10000);

        // Assert
        _serviceMock.Verify(x => x.GetOutRecordsPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task CreateOutRecord_ReturnsOk()
    {
        // Arrange
        var request = new CreatePicklingOutRecordRequest { PicklingInRecordId = 1, CompleteDate = DateTime.Today };
        var dto = new PicklingOutRecordDto { Id = 1, PicklingInRecordId = 1 };
        _serviceMock.Setup(x => x.CreateOutRecordAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateOutRecord(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PicklingOutRecordDto>>(result);
        Assert.Equal(1, response.Data?.PicklingInRecordId);
    }

    [Fact]
    public async Task UpdateOutRecord_ReturnsOk()
    {
        // Arrange
        var request = new UpdatePicklingOutRecordRequest { Remark = "更新备注" };
        var dto = new PicklingOutRecordDto { Id = 1, Remark = "更新备注" };
        _serviceMock.Setup(x => x.UpdateOutRecordAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.UpdateOutRecord(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PicklingOutRecordDto>>(result);
        Assert.Equal("更新备注", response.Data?.Remark);
    }

    [Fact]
    public async Task DeleteOutRecord_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteOutRecordAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteOutRecord(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    // ========== 筛选上下文 ==========

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        // Arrange
        var filterContexts = new Dictionary<string, List<string>> { ["SectionName"] = new() { "酸洗" } };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(filterContexts);

        // Act
        var result = await _controller.GetFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetOutRecordFilterContexts_ReturnsOk()
    {
        // Arrange
        var filterContexts = new Dictionary<string, List<string>> { ["EquipmentName"] = new() { "设备A" } };
        _serviceMock.Setup(x => x.GetOutRecordFilterContextsAsync()).ReturnsAsync(filterContexts);

        // Act
        var result = await _controller.GetOutRecordFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
    }
}
