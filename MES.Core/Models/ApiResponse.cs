namespace MES.Core.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }

    public int Code { get; set; } = 200;

    public static ApiResponse<T> Ok(T data, string message = "鎿嶄綔鎴愬姛")
    {
        return new ApiResponse<T> 
        { 
            Success = true, 
            Code = 200,
            Message = message, 
            Data = data 
        };
    }

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

    public static ApiResponse<T> Ok(string message = "鎿嶄綔鎴愬姛")
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

public class ApiResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public int Code { get; set; } = 200;

    public static ApiResponse Ok(string message = "鎿嶄綔鎴愬姛")
    {
        return new ApiResponse 
        { 
            Success = true, 
            Code = 200,
            Message = message
        };
    }

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