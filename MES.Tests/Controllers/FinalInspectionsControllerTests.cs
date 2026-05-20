using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class FinalInspectionsControllerTests : ControllerTestBase
{
    private readonly Mock<IFinalInspectionService> _serviceMock;
    private readonly Mock<ILogger<FinalInspectionsController>> _loggerMock;
    private readonly FinalInspectionsController _controller;

    public FinalInspectionsControllerTests()
    {
        _serviceMock = new Mock<IFinalInspectionService>();
        _loggerMock = CreateLoggerMock<FinalInspectionsController>();
        _controller = new FinalInspectionsController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<FinalInspectionDto>
        {
            Items = new List<FinalInspectionDto> { new() { Id = 1, InspectionItem = InspectionItem.PMIInspection } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<FinalInspectionDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetAll_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<FinalInspectionDto> { Items = new List<FinalInspectionDto>() });

        // Act
        var result = await _controller.GetAll(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<FinalInspectionDto>>>(result);
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new FinalInspectionDto { Id = 1, InspectionItem = InspectionItem.PMIInspection };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<FinalInspectionDto>>(result);
        Assert.Equal(InspectionItem.PMIInspection, response.Data?.InspectionItem);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((FinalInspectionDto?)null);

        // Act
        var result = await _controller.GetById(999);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<FinalInspectionDto>>(notFoundResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateFinalInspectionRequest { InspectionItem = InspectionItem.PMIInspection };
        var dto = new FinalInspectionDto { Id = 1, InspectionItem = InspectionItem.PMIInspection };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<FinalInspectionDto>>(result);
        Assert.Equal(InspectionItem.PMIInspection, response.Data?.InspectionItem);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateFinalInspectionRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<FinalInspectionDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateFinalInspectionRequest { EquipmentName = "设备A" };
        var dto = new FinalInspectionDto { Id = 1, EquipmentName = "设备A" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<FinalInspectionDto>>(result);
        Assert.Equal("设备A", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateFinalInspectionRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<FinalInspectionDto>>(result);
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
    public async Task BatchCreate_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateFinalInspectionRequest> { new() { InspectionItem = InspectionItem.PMIInspection } };
        var dtos = new List<FinalInspectionDto> { new() { Id = 1, InspectionItem = InspectionItem.PMIInspection } };
        _serviceMock.Setup(x => x.BatchCreateAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreate(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<FinalInspectionDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task BatchCreate_ReturnsBadRequest_WhenEmpty()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchCreate(new List<CreateFinalInspectionRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<FinalInspectionDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task LookupBatch_ReturnsOk()
    {
        // Arrange
        var dto = new BatchLookupResultDto { MaterialName = "材料A" };
        _serviceMock.Setup(x => x.LookupBatchAsync("BATCH001")).ReturnsAsync(dto);

        // Act
        var result = await _controller.LookupBatch("BATCH001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<BatchLookupResultDto?>>(result);
        Assert.Equal("材料A", response.Data!.MaterialName);
    }

    [Fact]
    public async Task LookupBatch_ReturnsOk_WhenEmptyNo()
    {
        // Arrange
        // Act
        var result = await _controller.LookupBatch("");

        // Assert
        var (_, response) = AssertOk<ApiResponse<BatchLookupResultDto?>>(result);
        Assert.Null(response.Data);
    }
}
