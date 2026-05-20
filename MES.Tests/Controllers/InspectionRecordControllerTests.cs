using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class InspectionRecordControllerTests : ControllerTestBase
{
    private readonly Mock<IInspectionRecordService> _serviceMock;
    private readonly Mock<ILogger<InspectionRecordController>> _loggerMock;
    private readonly InspectionRecordController _controller;

    public InspectionRecordControllerTests()
    {
        _serviceMock = new Mock<IInspectionRecordService>();
        _loggerMock = CreateLoggerMock<InspectionRecordController>();
        _controller = new InspectionRecordController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<InspectionRecordListDto>
        {
            Items = new List<InspectionRecordListDto> { new() { Id = 1, EquipmentName = "设备A" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InspectionRecordQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<InspectionRecordListDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new InspectionRecordListDto { Id = 1, EquipmentName = "设备A" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<InspectionRecordListDto>>(result);
        Assert.Equal("设备A", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateInspectionRecordRequest { EquipmentId = 1 };
        var dto = new InspectionRecordListDto { Id = 1, EquipmentName = "设备A" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<InspectionRecordListDto>>(result);
        Assert.Equal("设备A", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateInspectionRecordRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<InspectionRecordListDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateInspectionRequest { Inspector = "新检验员" };
        var dto = new InspectionRecordListDto { Id = 1, EquipmentName = "设备A", Inspector = "新检验员" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<InspectionRecordListDto>>(result);
        Assert.Equal("新检验员", response.Data?.Inspector);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateInspectionRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<InspectionRecordListDto>>(result);
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
    public async Task CreateBatch_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateInspectionRecordRequest> { new() { EquipmentId = 1 } };
        var dtos = new List<InspectionRecordListDto> { new() { Id = 1, EquipmentName = "设备A" } };
        _serviceMock.Setup(x => x.CreateBatchAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.CreateBatch(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<InspectionRecordListDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task CreateBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.CreateBatch(new List<CreateInspectionRecordRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<InspectionRecordListDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintBatch(new InspectionRecordPrintBatchRequest());

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
        var result = await _controller.PrintBatch(new InspectionRecordPrintBatchRequest { Ids = new[] { 1, 2 } });

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
        var result = await _controller.PrintAll(new InspectionRecordPrintAllRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintAllAsync(It.IsAny<InspectionRecordQueryParams>(), It.IsAny<List<PrintColumnDef>>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintAll(new InspectionRecordPrintAllRequest { Keyword = "设备" });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }
}
