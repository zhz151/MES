using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class FurnaceRegistrationControllerTests : ControllerTestBase
{
    private readonly Mock<IFurnaceRegistrationService> _serviceMock;
    private readonly Mock<ILogger<FurnaceRegistrationController>> _loggerMock;
    private readonly FurnaceRegistrationController _controller;

    public FurnaceRegistrationControllerTests()
    {
        _serviceMock = new Mock<IFurnaceRegistrationService>();
        _loggerMock = CreateLoggerMock<FurnaceRegistrationController>();
        _controller = new FurnaceRegistrationController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<FurnaceRegistrationDto>
        {
            Items = new List<FurnaceRegistrationDto> { new() { Id = 1, RawMaterialUnit = "原料厂" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<FurnaceRegistrationDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetAll_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<FurnaceRegistrationDto> { Items = new List<FurnaceRegistrationDto>() });

        // Act
        var result = await _controller.GetAll(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<FurnaceRegistrationDto>>>(result);
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new FurnaceRegistrationDto { Id = 1, RawMaterialUnit = "原料厂" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<FurnaceRegistrationDto>>(result);
        Assert.Equal("原料厂", response.Data?.RawMaterialUnit);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((FurnaceRegistrationDto?)null);

        // Act
        var result = await _controller.GetById(999);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<FurnaceRegistrationDto>>(notFoundResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task BatchCreate_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateFurnaceRegistrationRequest>
        {
            new() { RawMaterialUnit = "新原料" }
        };
        var dtos = new List<FurnaceRegistrationDto>
        {
            new() { Id = 1, RawMaterialUnit = "新原料" }
        };
        _serviceMock.Setup(x => x.BatchCreateAsync(It.IsAny<List<CreateFurnaceRegistrationRequest>>()))
            .ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreate(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<FurnaceRegistrationDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task BatchCreate_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchCreate(new List<CreateFurnaceRegistrationRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<FurnaceRegistrationDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateFurnaceRegistrationRequest { RawMaterialUnit = "更新原料" };
        var dto = new FurnaceRegistrationDto { Id = 1, RawMaterialUnit = "更新原料" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<FurnaceRegistrationDto>>(result);
        Assert.Equal("更新原料", response.Data?.RawMaterialUnit);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateFurnaceRegistrationRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<FurnaceRegistrationDto>>(result);
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
    public async Task LookupPlantGrade_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.LookupPlantGradeAsync("304")).ReturnsAsync("304H");

        // Act
        var result = await _controller.LookupPlantGrade("304");

        // Assert
        var (_, response) = AssertOk<ApiResponse<string?>>(result);
        Assert.Equal("304H", response.Data);
    }

    [Fact]
    public async Task LookupPlantGrade_ReturnsOk_WhenNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.LookupPlantGradeAsync("UNKNOWN")).ReturnsAsync((string?)null);

        // Act
        var result = await _controller.LookupPlantGrade("UNKNOWN");

        // Assert
        var (_, response) = AssertOk<ApiResponse<string?>>(result);
        Assert.Null(response.Data);
    }
}
