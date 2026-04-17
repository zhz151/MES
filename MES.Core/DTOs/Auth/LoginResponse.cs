namespace MES.Core.DTOs.Auth;

/// <summary>
/// 鐧诲綍鍝嶅簲DTO
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// JWT浠ょ墝
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 閭
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 鐢ㄦ埛鍚?    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 瑙掕壊鍒楄〃
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// 杩囨湡鏃堕棿
    /// </summary>
    public DateTime Expires { get; set; }

    /// <summary>
    /// 鐢ㄦ埛鍏ㄥ悕
    /// </summary>
    public string FullName { get; set; } = string.Empty;
}