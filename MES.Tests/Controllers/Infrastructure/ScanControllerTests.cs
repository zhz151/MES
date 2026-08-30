using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Infrastructure;
using MES.Core.Models;
using MES.Core.DTOs.Infrastructure;
using MES.Core.Interfaces.Infrastructure;

namespace MES.Tests.Controllers;

public class ScanControllerTests : ControllerTestBase
{
    private readonly Mock<IScanService> _serviceMock;
    private readonly Mock<IQrCodeService> _qrCodeServiceMock;
    private readonly ScanController _controller;

    public ScanControllerTests()
    {
        _serviceMock = new Mock<IScanService>();
        _qrCodeServiceMock = new Mock<IQrCodeService>();
        _controller = new ScanController(_serviceMock.Object, _qrCodeServiceMock.Object);
    }

    [Fact]
    public async Task Resolve_ReturnsOk()
    {
        // Arrange
        var dto = new ScanResolveResultDto { BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.ResolveAsync("BATCH001", 1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Resolve("BATCH001", 1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ScanResolveResultDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task Resolve_ReturnsBadRequest_WhenBatchNoEmpty()
    {
        // Act
        var result = await _controller.Resolve("", 1);

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ScanResolveResultDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetBatchProcessGroups_ReturnsOk()
    {
        // Arrange
        var dto = new ScanBatchResolveResultDto { BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.GetBatchProcessGroupsAsync("BATCH001")).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetBatchProcessGroups("BATCH001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<ScanBatchResolveResultDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task GetBatchProcessGroups_ReturnsBadRequest_WhenBatchNoEmpty()
    {
        // Act
        var result = await _controller.GetBatchProcessGroups("");

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ScanBatchResolveResultDto>>(result);
        Assert.False(response.Success);
    }
}
