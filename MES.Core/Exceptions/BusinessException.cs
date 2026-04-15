namespace MES.Core.Exceptions;

/// <summary>
/// 业务异常（返回400状态码给前端）
/// </summary>
public class BusinessException : Exception
{
    /// <summary>
    /// 业务异常代码
    /// </summary>
    public int Code { get; set; } = 400;

    /// <summary>
    /// 初始化业务异常
    /// </summary>
    /// <param name="message">异常消息</param>
    public BusinessException(string message) : base(message) { }

    /// <summary>
    /// 初始化业务异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="code">异常代码</param>
    public BusinessException(string message, int code) : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// 初始化业务异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public BusinessException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// 初始化业务异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="code">异常代码</param>
    /// <param name="innerException">内部异常</param>
    public BusinessException(string message, int code, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }
}

/// <summary>
/// 未授权异常（返回401状态码）
/// </summary>
public class UnauthorizedException : BusinessException
{
    public UnauthorizedException(string message = "未授权访问") : base(message, 401) { }
}

/// <summary>
/// 禁止访问异常（返回403状态码）
/// </summary>
public class ForbiddenException : BusinessException
{
    public ForbiddenException(string message = "禁止访问") : base(message, 403) { }
}

/// <summary>
/// 资源未找到异常（返回404状态码）
/// </summary>
public class NotFoundException : BusinessException
{
    public NotFoundException(string message = "资源未找到") : base(message, 404) { }
}

/// <summary>
/// 数据验证异常（返回422状态码）
/// </summary>
public class ValidationException : BusinessException
{
    public ValidationException(string message = "数据验证失败") : base(message, 422) { }

    public ValidationException(string message, int code) : base(message, code) { }
}