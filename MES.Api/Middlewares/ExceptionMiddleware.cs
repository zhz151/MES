using System.Text.Json;
using MES.Core.Models;
using MES.Core.Exceptions;

namespace MES.Api.Middlewares;

/// <summary>
/// 全局异常处理中间件
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            await HandleBusinessExceptionAsync(context, ex);
        }
        catch (UnauthorizedAccessException)
        {
            await HandleUnauthorizedExceptionAsync(context);
        }
        catch (Exception ex)
        {
            await HandleSystemExceptionAsync(context, ex);
        }
    }

    private async Task HandleBusinessExceptionAsync(HttpContext context, BusinessException ex)
    {
        context.Response.StatusCode = ex.Code;
        context.Response.ContentType = "application/json";

        var response = new ApiResponse<object>
        {
            Success = false,
            Code = ex.Code,
            Message = ex.Message
        };

        var result = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(result);
    }

    private async Task HandleUnauthorizedExceptionAsync(HttpContext context)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";

        var response = new ApiResponse<object>
        {
            Success = false,
            Code = 401,
            Message = "未授权访问"
        };

        var result = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(result);
    }

    private async Task HandleSystemExceptionAsync(HttpContext context, Exception ex)
    {
        _logger.LogError(ex, "系统异常: {Message}", ex.Message);

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var response = new ApiResponse<object>
        {
            Success = false,
            Code = 500,
            Message = "系统内部错误，请稍后重试"
        };

#if DEBUG
        response.Message = ex.Message;
#endif

        var result = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(result);
    }
}

/// <summary>
/// 全局异常处理中间件扩展方法
/// </summary>
public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionMiddleware>();
    }
}