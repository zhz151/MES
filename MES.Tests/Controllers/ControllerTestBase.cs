using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Core.Models;

namespace MES.Tests.Controllers;

/// <summary>
/// Controller 测试基类，封装 Mock 创建和断言辅助方法
/// </summary>
public abstract class ControllerTestBase
{
    /// <summary>
    /// 为 Controller 添加 ModelState 错误（模拟验证失败）
    /// </summary>
    protected static void AddModelError(ControllerBase controller, string key = "test", string message = "验证失败")
    {
        controller.ModelState.AddModelError(key, message);
    }

    /// <summary>
    /// 创建 ILogger 的 Mock
    /// </summary>
    protected static Mock<ILogger<T>> CreateLoggerMock<T>() where T : class
    {
        return new Mock<ILogger<T>>();
    }

    /// <summary>
    /// 验证 ActionResult 返回 OkObjectResult 且 ApiResponse.Success == true
    /// </summary>
    protected static (OkObjectResult Result, TResponse Response) AssertOk<TResponse>(ActionResult<TResponse> actionResult)
    {
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<TResponse>(okResult.Value);
        return (okResult, response);
    }

    /// <summary>
    /// 验证 ActionResult 返回 BadRequestObjectResult 且 ApiResponse.Success == false
    /// </summary>
    protected static (BadRequestObjectResult Result, TResponse Response) AssertBadRequest<TResponse>(ActionResult<TResponse> actionResult)
    {
        var badResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var response = Assert.IsType<TResponse>(badResult.Value);
        return (badResult, response);
    }
}
