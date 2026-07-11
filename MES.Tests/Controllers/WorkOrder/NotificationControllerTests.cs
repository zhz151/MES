using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.WorkOrder;
using MES.Core.Models;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.WorkOrder;

namespace MES.Tests.Controllers;

public class NotificationControllerTests : ControllerTestBase
{
    private readonly Mock<INotificationService> _serviceMock;
    private readonly NotificationController _controller;

    public NotificationControllerTests()
    {
        _serviceMock = new Mock<INotificationService>();
        _controller = new NotificationController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetUnreadCountAsync()).ReturnsAsync(5);

        // Act
        var result = await _controller.GetUnreadCount();

        // Assert
        var (_, response) = AssertOk<ApiResponse<int>>(result);
        Assert.Equal(5, response.Data);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<NotificationDto>
        {
            Items = new List<NotificationDto> { new() { Id = 1, Title = "通知1" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedNotificationsAsync(1, 20)).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<NotificationDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task MarkAsRead_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.MarkAsReadAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.MarkAsRead(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task MarkAllAsRead_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.MarkAllAsReadAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.MarkAllAsRead();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetByType_ReturnsOk()
    {
        // Arrange
        var list = new List<NotificationDto> { new() { Id = 1, Title = "通知1" } };
        _serviceMock.Setup(x => x.GetUnreadByTypeAsync("info")).ReturnsAsync(list);

        // Act
        var result = await _controller.GetByType("info");

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<NotificationDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task MarkAllByTypeAsRead_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.MarkAllByTypeAsReadAsync("info")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.MarkAllByTypeAsRead("info");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }
}
