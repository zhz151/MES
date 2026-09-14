using Moq;
using MES.Api.Controllers.Quality;
using MES.Core.Interfaces.Quality;
using MES.Core.Models;

namespace MES.Tests.Controllers;

/// <summary>
/// 扫码链质量端点测试。
///
/// 重点锁定「当前操作人」端点把姓名放在 <c>Data</c> 上（前端只读展示「巡检人/反馈人」读的是 Data）：
/// 该端点曾用单参 Ok 命中已删除的 message-only 重载，返回 data=null，
/// 导致扫码页身份取不到、提交被后端拦成「巡检人不能为空」（2026-09-14）。
/// </summary>
public class ScanQualityControllerTests : ControllerTestBase
{
    private readonly Mock<IScanQualityService> _serviceMock;
    private readonly ScanQualityController _controller;

    public ScanQualityControllerTests()
    {
        _serviceMock = new Mock<IScanQualityService>();
        _controller = new ScanQualityController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetCurrentOperator_姓名写入Data()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetCurrentOperatorNameAsync()).ReturnsAsync("张三");

        // Act
        var result = await _controller.GetCurrentOperator();

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.Equal("张三", response.Data);
    }
}
