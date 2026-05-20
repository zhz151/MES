using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Exceptions;

namespace MES.Tests.Controllers;

public class BatchControllerTests : ControllerTestBase
{
    private readonly Mock<IBatchService> _serviceMock;
    private readonly Mock<IProductionRecordService> _productionRecordServiceMock;
    private readonly Mock<ILogger<BatchController>> _loggerMock;
    private readonly BatchController _controller;

    public BatchControllerTests()
    {
        _serviceMock = new Mock<IBatchService>();
        _productionRecordServiceMock = new Mock<IProductionRecordService>();
        _loggerMock = CreateLoggerMock<BatchController>();
        _controller = new BatchController(_serviceMock.Object, _productionRecordServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<ProductionBatchListDto>
        {
            Items = new List<ProductionBatchListDto> { new() { Id = 1, BatchNo = "BATCH001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<BatchQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<ProductionBatchListDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new ProductionBatchDetailDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductionBatchDetailDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task GetByBatchNo_ReturnsOk()
    {
        // Arrange
        var dto = new ProductionBatchDetailDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.GetByBatchNoAsync("BATCH001")).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetByBatchNo("BATCH001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductionBatchDetailDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task GetByBatchNo_ReturnsNotFound_WhenBusinessException()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetByBatchNoAsync("UNKNOWN")).ThrowsAsync(new BusinessException("未找到"));

        // Act
        var result = await _controller.GetByBatchNo("UNKNOWN");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<ProductionBatchDetailDto>>(notFoundResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateProductionBatchRequest { WorkOrderNo = "WO001" };
        var dto = new ProductionBatchListDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductionBatchListDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateProductionBatchRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ProductionBatchListDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateProductionBatchRequest { TagNo = "TAG002" };
        var dto = new ProductionBatchDetailDto { Id = 1, BatchNo = "BATCH002" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductionBatchDetailDto>>(result);
        Assert.Equal("BATCH002", response.Data?.BatchNo);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateProductionBatchRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ProductionBatchDetailDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsOk()
    {
        // Arrange
        var request = new UpdateBatchStatusRequest { Status = "完成" };
        _serviceMock.Setup(x => x.UpdateStatusAsync(1, request)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdateStatus(1, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
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
    public async Task SaveAll_ReturnsOk()
    {
        // Arrange
        var request = new SaveBatchRequest();
        var dto = new SaveBatchResponse { Status = "保存成功" };
        _serviceMock.Setup(x => x.SaveAllAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.SaveAll(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SaveBatchResponse>>(result);
        Assert.Equal("保存成功", response.Data?.Status);
    }

    [Fact]
    public async Task GetProcessGroups_ReturnsOk()
    {
        // Arrange
        var list = new List<ProcessGroupDto> { new() { Id = 1, ProcessName = "热处理" } };
        _serviceMock.Setup(x => x.GetProcessGroupsAsync(1)).ReturnsAsync(list);

        // Act
        var result = await _controller.GetProcessGroups(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<ProcessGroupDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task AddProcessGroup_ReturnsOk()
    {
        // Arrange
        var request = new CreateProcessGroupRequest { ProcessName = "热处理" };
        var dto = new ProcessGroupDto { Id = 1, ProcessName = "热处理" };
        _serviceMock.Setup(x => x.AddProcessGroupAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.AddProcessGroup(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProcessGroupDto>>(result);
        Assert.Equal("热处理", response.Data?.ProcessName);
    }

    [Fact]
    public async Task DeleteProcessGroup_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteProcessGroupAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteProcessGroup(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetAvailableBatches_ReturnsOk()
    {
        // Arrange
        var list = new List<AvailableBatchDto> { new() { BatchNo = "BATCH001" } };
        _serviceMock.Setup(x => x.GetAvailableBatchesAsync()).ReturnsAsync(list);

        // Act
        var result = await _controller.GetAvailableBatches();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<AvailableBatchDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetLastBatchProcessGroups_ReturnsOk()
    {
        // Arrange
        var list = new List<CreateProcessGroupRequest> { new() { ProcessName = "热处理" } };
        _serviceMock.Setup(x => x.GetLastBatchProcessGroupsAsync()).ReturnsAsync(list);

        // Act
        var result = await _controller.GetLastBatchProcessGroups();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<CreateProcessGroupRequest>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetNextBatchNo_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetNextBatchNoAsync()).ReturnsAsync("BATCH-NEXT");

        // Act
        var result = await _controller.GetNextBatchNo();

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.Equal("BATCH-NEXT", response.Data);
    }

    [Fact]
    public async Task VerifyWorkOrderNos_ReturnsOk()
    {
        // Arrange
        var list = new List<BatchWorkOrderMismatchDto> { new() { BatchNo = "BATCH001" } };
        _serviceMock.Setup(x => x.VerifyWorkOrderNosAsync()).ReturnsAsync(list);

        // Act
        var result = await _controller.VerifyWorkOrderNos();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<BatchWorkOrderMismatchDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetByWorkOrderNo_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<ProductionBatchListDto>
        {
            Items = new List<ProductionBatchListDto> { new() { Id = 1, BatchNo = "BATCH001" } }
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<BatchQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetByWorkOrderNo("WO001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<ProductionBatchListDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetTrackingVisual_ReturnsOk()
    {
        // Arrange
        var dto = new BatchTrackingVisualDto { BatchNo = "BATCH001" };
        _productionRecordServiceMock.Setup(x => x.GetTrackingVisualAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetTrackingVisual(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<BatchTrackingVisualDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task GetAdjacentBatch_ReturnsOk()
    {
        // Arrange
        var dto = new AdjacentBatchDto { PrevId = null, NextId = 2 };
        _serviceMock.Setup(x => x.GetAdjacentBatchAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetAdjacentBatch(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<AdjacentBatchDto>>(result);
        Assert.Equal(2, response.Data?.NextId);
    }

    [Fact]
    public async Task GetOperationLogs_ReturnsOk()
    {
        // Arrange
        var list = new List<BatchOperationLogDto> { new() { Id = 1, OperationType = "创建" } };
        _serviceMock.Setup(x => x.GetOperationLogsAsync(1)).ReturnsAsync(list);

        // Act
        var result = await _controller.GetOperationLogs(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<BatchOperationLogDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetProcessGroupsByBatchNo_ReturnsOk()
    {
        // Arrange
        var list = new List<CreateProcessGroupRequest> { new() { ProcessName = "热处理" } };
        _serviceMock.Setup(x => x.GetProcessGroupsByBatchNoAsync("BATCH001")).ReturnsAsync(list);

        // Act
        var result = await _controller.GetProcessGroupsByBatchNo("BATCH001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<CreateProcessGroupRequest>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetProcessGroupsByBatchNo_ReturnsNotFound_WhenEmpty()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetProcessGroupsByBatchNoAsync("EMPTY")).ReturnsAsync(new List<CreateProcessGroupRequest>());

        // Act
        var result = await _controller.GetProcessGroupsByBatchNo("EMPTY");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<CreateProcessGroupRequest>>>(notFoundResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetProcessGroupsByBatchNo_ReturnsNotFound_WhenBusinessException()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetProcessGroupsByBatchNoAsync("UNKNOWN")).ThrowsAsync(new BusinessException("未找到"));

        // Act
        var result = await _controller.GetProcessGroupsByBatchNo("UNKNOWN");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<CreateProcessGroupRequest>>>(notFoundResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintBatch_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintBatchAsync(1)).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintBatch(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintBatchAll_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintBatchAll(new BatchPrintAllRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintBatchAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintBatchAllAsync(It.IsAny<BatchPrintAllRequest>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintBatchAll(new BatchPrintAllRequest());

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintBatchSelected_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintBatchSelected(new int[] { });

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintBatchSelected_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintBatchSelectedAsync(It.IsAny<int[]>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintBatchSelected(new[] { 1, 2 });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintProcessCard_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintProcessCard(new ProcessCardPrintRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintProcessCard_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintProcessCardAsync(It.IsAny<ProcessCardPrintRequest>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintProcessCard(new ProcessCardPrintRequest());

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }
}
