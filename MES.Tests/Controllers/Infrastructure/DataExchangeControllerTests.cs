using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.DataExchange;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class DataExchangeControllerTests : ControllerTestBase
{
    private readonly Mock<IDataExchangeService> _serviceMock;
    private readonly Mock<ILogger<DataExchangeController>> _loggerMock;
    private readonly DataExchangeController _controller;

    public DataExchangeControllerTests()
    {
        _serviceMock = new Mock<IDataExchangeService>();
        _loggerMock = CreateLoggerMock<DataExchangeController>();
        _controller = new DataExchangeController(_serviceMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin") }))
            }
        };
    }

    [Fact]
    public async Task GetEntities_ReturnsOk()
    {
        // Arrange
        var list = new List<EntityInfo> { new() { Key = "batch", Name = "batch" } };
        _serviceMock.Setup(x => x.GetEntitiesAsync()).ReturnsAsync(list);

        // Act
        var result = await _controller.GetEntities();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<EntityInfo>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task Export_ReturnsFile()
    {
        // Arrange
        _serviceMock.Setup(x => x.ExportAsync("batch")).ReturnsAsync(new byte[] { 0x50, 0x4B });
        _serviceMock.Setup(x => x.GetEntityDisplayName("batch")).Returns("批次");

        // Act
        var result = await _controller.Export("batch");

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
    }

    [Fact]
    public async Task Template_ReturnsFile()
    {
        // Arrange
        _serviceMock.Setup(x => x.GenerateTemplateAsync("batch")).ReturnsAsync(new byte[] { 0x50, 0x4B });
        _serviceMock.Setup(x => x.GetEntityDisplayName("batch")).Returns("批次");

        // Act
        var result = await _controller.Template("batch");

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
    }

    [Fact]
    public async Task Preview_NullFile_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.Preview("batch", null!);

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ImportPreviewResult>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Preview_ReturnsOk()
    {
        // Arrange
        var previewResult = new ImportPreviewResult { TotalRows = 5, ValidCount = 3, ErrorCount = 2 };
        _serviceMock.Setup(x => x.PreviewAsync("batch", It.IsAny<byte[]>(), It.IsAny<string?>()))
            .ReturnsAsync(previewResult);

        using var ms = new MemoryStream(new byte[] { 0x01, 0x02 });
        var file = new FormFile(ms, 0, ms.Length, "file", "test.xlsx");

        // Act
        var result = await _controller.Preview("batch", file);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ImportPreviewResult>>(result);
        Assert.Equal(5, response.Data!.TotalRows);
        Assert.Equal(3, response.Data.ValidCount);
    }

    [Fact]
    public async Task Import_NullFile_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.Import("batch", null!);

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ImportResult>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Import_ReturnsOk()
    {
        // Arrange
        var importResult = new ImportResult { TotalRows = 5, SuccessCount = 4, FailedCount = 1 };
        _serviceMock.Setup(x => x.ImportAsync("batch", It.IsAny<byte[]>(), "skip", It.IsAny<string?>()))
            .ReturnsAsync(importResult);

        using var ms = new MemoryStream(new byte[] { 0x01, 0x02 });
        var file = new FormFile(ms, 0, ms.Length, "file", "test.xlsx");

        // Act
        var result = await _controller.Import("batch", file);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ImportResult>>(result);
        Assert.Equal(5, response.Data!.TotalRows);
        Assert.Equal(4, response.Data.SuccessCount);
    }

    [Fact]
    public async Task Import_WithRollback_ReturnsOk()
    {
        // Arrange
        var importResult = new ImportResult { TotalRows = 5, SuccessCount = 0, FailedCount = 5, HasRolledBack = true };
        _serviceMock.Setup(x => x.ImportAsync("batch", It.IsAny<byte[]>(), "skip", It.IsAny<string?>()))
            .ReturnsAsync(importResult);

        using var ms = new MemoryStream(new byte[] { 0x01, 0x02 });
        var file = new FormFile(ms, 0, ms.Length, "file", "test.xlsx");

        // Act
        var result = await _controller.Import("batch", file);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ImportResult>>(result);
        Assert.True(response.Data!.HasRolledBack);
        Assert.Contains("已回滚", response.Message);
    }

    [Fact]
    public async Task FixAllSystemFields_ReturnsOk()
    {
        // Arrange
        var report = new DataFixReport
        {
            SequenceNumbersFixed = 5,
            OutsourceStatusFixed = 3,
            BatchTrackingFixed = 2,
            EquipmentFixed = 1
        };
        _serviceMock.Setup(x => x.FixAllSystemFieldsAsync()).ReturnsAsync(report);

        // Act
        var result = await _controller.FixAllSystemFields();

        // Assert
        var (_, response) = AssertOk<ApiResponse<DataFixReport>>(result);
        Assert.True(response.Success);
        Assert.Equal(5, response.Data!.SequenceNumbersFixed);
        Assert.Equal(3, response.Data.OutsourceStatusFixed);
        Assert.Equal(2, response.Data.BatchTrackingFixed);
        Assert.Equal(1, response.Data.EquipmentFixed);
        Assert.Equal(11, response.Data.Total);
        Assert.Contains("组内序号", response.Message);
        Assert.Contains("设备日期", response.Message);
    }
}
