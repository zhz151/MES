namespace MES.Core.Models;

/// <summary>
/// 统一API响应格式
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// 请求是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 响应数据
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 响应代码
    /// </summary>
    public int Code { get; set; } = 200;

    /// <summary>
    /// 创建成功响应
    /// </summary>
    /// <param name="data">响应数据</param>
    /// <param name="message">成功消息</param>
    /// <returns>成功响应对象</returns>
    public static ApiResponse<T> Ok(T data, string message = "操作成功")
    {
        return new ApiResponse<T> 
        { 
            Success = true, 
            Code = 200,
            Message = message, 
            Data = data 
        };
    }

    /// <summary>
    /// 创建失败响应
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="code">错误代码</param>
    /// <returns>失败响应对象</returns>
    public static ApiResponse<T> Fail(string message, int code = 400)
    {
        return new ApiResponse<T> 
        { 
            Success = false, 
            Code = code,
            Message = message, 
            Data = default 
        };
    }

    /// <summary>
    /// 创建无数据成功响应
    /// </summary>
    /// <param name="message">成功消息</param>
    /// <returns>成功响应对象</returns>
    public static ApiResponse<T> Ok(string message = "操作成功")
    {
        return new ApiResponse<T> 
        { 
            Success = true, 
            Code = 200,
            Message = message, 
            Data = default 
        };
    }
}

/// <summary>
/// 无数据泛型的统一API响应格式
/// </summary>
public class ApiResponse
{
    /// <summary>
    /// 请求是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 响应代码
    /// </summary>
    public int Code { get; set; } = 200;

    /// <summary>
    /// 创建成功响应
    /// </summary>
    /// <param name="message">成功消息</param>
    /// <returns>成功响应对象</returns>
    public static ApiResponse Ok(string message = "操作成功")
    {
        return new ApiResponse 
        { 
            Success = true, 
            Code = 200,
            Message = message
        };
    }

    /// <summary>
    /// 创建失败响应
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="code">错误代码</param>
    /// <returns>失败响应对象</returns>
    public static ApiResponse Fail(string message, int code = 400)
    {
        return new ApiResponse 
        { 
            Success = false, 
            Code = code,
            Message = message
        };
    }
}