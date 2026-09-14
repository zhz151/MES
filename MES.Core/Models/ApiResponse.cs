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

    // ⚠️ 禁止再新增「只收 message」的 Ok(string message = "操作成功") 重载：
    // 它与 Ok(T data, string message = "操作成功") 在 T=string 时构成重载歧义，按 C# 决议规则
    // （所有参数都有实参者优于需补默认参数者）会绑定到 message-only 版本 ——
    // 于是 ApiResponse<string>.Ok("张三") 把值写进 Message、Data 留 null，且**编译期毫无提示**。
    // 历史事故（2026-09-14）：扫码链「当前巡检人」端点因此返回 data=null，前端取不到实名，
    // 提交时被后端 [Required] 拦成「巡检人不能为空 / 反馈人不能为空」。
    // 需要自定义消息请给两个实参：Ok(data, "消息")。
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