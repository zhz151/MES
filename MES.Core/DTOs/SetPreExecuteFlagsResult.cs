namespace MES.Core.DTOs;

/// <summary>
/// 批量设置预执行标记结果
/// </summary>
public class SetPreExecuteFlagsResult
{
    public int Count { get; set; }
    public string Message { get; set; } = string.Empty;
}
