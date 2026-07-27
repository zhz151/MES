using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Batch;
using MES.Core.Models;
using MES.Core.DTOs.Batch;
using MES.Core.Interfaces.Batch;

namespace MES.Tests.Controllers;

public class ProductionRecordControllerTests : ControllerTestBase
{
    private readonly Mock<IProductionRecordService> _serviceMock;
    private readonly Mock<ILogger<ProductionRecordController>> _loggerMock;
    private readonly ProductionRecordController _controller;

    public ProductionRecordControllerTests()
    {
        _serviceMock = new Mock<IProductionRecordService>();
        _loggerMock = CreateLoggerMock<ProductionRecordController>();
        _controller = new ProductionRecordController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetProductionRecords_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<ProductionRecordDto>
        {
            Items = new List<ProductionRecordDto> { new() { Id = 1, BatchNo = "REC001" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetProductionRecordsAsync(1, It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetProductionRecords(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<ProductionRecordDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task CreateProductionRecord_ReturnsOk()
    {
        // Arrange
        var request = new CreateProductionRecordRequest { BatchNo = "REC001" };
        var dto = new ProductionRecordDto { Id = 1, BatchNo = "REC001" };
        _serviceMock.Setup(x => x.CreateProductionRecordAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateProductionRecord(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductionRecordDto>>(result);
        Assert.Equal("REC001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task CreateProductionRecord_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.CreateProductionRecord(new CreateProductionRecordRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ProductionRecordDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task BatchCreateProductionRecords_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateProductionRecordRequest> { new() { BatchNo = "REC001" } };
        var dtos = new List<ProductionRecordDto> { new() { Id = 1, BatchNo = "REC001" } };
        _serviceMock.Setup(x => x.BatchCreateProductionRecordsAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreateProductionRecords(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<ProductionRecordDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task DeleteProductionRecord_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteProductionRecordAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteProductionRecord(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task UpdateProductionRecord_ReturnsOk()
    {
        // Arrange
        var request = new UpdateProductionRecordRequest { ExecDate = DateTime.Now };
        var dto = new ProductionRecordDto { Id = 1, BatchNo = "REC002" };
        _serviceMock.Setup(x => x.UpdateProductionRecordAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.UpdateProductionRecord(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductionRecordDto>>(result);
        Assert.Equal("REC002", response.Data?.BatchNo);
    }

    [Fact]
    public async Task UpdateProductionRecord_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.UpdateProductionRecord(1, new UpdateProductionRecordRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ProductionRecordDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetSectionOutsources_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<SectionOutsourceDto>
        {
            Items = new List<SectionOutsourceDto> { new() { Id = 1, BatchNo = "BATCH001" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetSectionOutsourcesAsync(1, It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetSectionOutsources(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<SectionOutsourceDto>>>(result);
        Assert.Single(response.Data!.Items);
    }

    }
