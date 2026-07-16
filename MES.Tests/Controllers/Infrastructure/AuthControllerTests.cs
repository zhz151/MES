using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Infrastructure;
using MES.Core.Interfaces.Auth;
using MES.Core.Models;
using MES.Core.DTOs.Auth;

namespace MES.Tests.Controllers;

public class AuthControllerTests : ControllerTestBase
{
    private readonly Mock<IAuthService> _serviceMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _serviceMock = new Mock<IAuthService>();
        _controller = new AuthController(_serviceMock.Object);
    }

    [Fact]
    public async Task Login_ReturnsOk()
    {
        // Arrange
        var request = new LoginRequest { Email = "admin@test.com", Password = "pass" };
        var response = ApiResponse<LoginResponse>.Ok(new LoginResponse { Token = "token123" }, "登录成功");
        _serviceMock.Setup(x => x.LoginAsync(request)).ReturnsAsync(response);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<ApiResponse<LoginResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal("token123", apiResponse.Data?.Token);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Login(new LoginRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<LoginResponse>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Logout_ReturnsOk()
    {
        // Arrange
        var response = ApiResponse<object>.Ok(null!, "退出成功");
        _serviceMock.Setup(x => x.LogoutAsync()).ReturnsAsync(response);

        // Act
        var result = await _controller.Logout();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(apiResponse.Success);
    }

    [Fact]
    public async Task RefreshToken_ReturnsOk()
    {
        // Arrange
        var request = new RefreshTokenRequest { RefreshToken = "refresh123" };
        var response = ApiResponse<LoginResponse>.Ok(new LoginResponse { Token = "newToken" }, "刷新成功");
        _serviceMock.Setup(x => x.RefreshTokenAsync("refresh123")).ReturnsAsync(response);

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<ApiResponse<LoginResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
    }

    [Fact]
    public async Task RefreshToken_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.RefreshToken(new RefreshTokenRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<LoginResponse>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsOk()
    {
        // Arrange
        var response = ApiResponse<UserInfoResponse>.Ok(new UserInfoResponse { UserName = "admin" }, "查询成功");
        _serviceMock.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(response);

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<ApiResponse<UserInfoResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
    }
}
